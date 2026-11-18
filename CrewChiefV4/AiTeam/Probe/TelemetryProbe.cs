using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>
    /// Telemetrie-Messung (P1): liest die Rohdaten des Spiels in einem eigenen Faden, unabhängig von der
    /// Crew-Chief-Hauptschleife, und schreibt einen Bericht in den Diagnoseordner. Nur lokal, nur lesend.
    /// Ändert weder Spielzustand noch Funk. Erkennt RaceRoom, AMS2 und Le Mans Ultimate automatisch.
    /// </summary>
    public static class TelemetryProbe
    {
        private static readonly object gate = new object();
        private static Runner runner;

        public static string Folder { get { return Path.Combine(LiveDiagnostics.DefaultFolder, "Telemetrie-Probe"); } }

        public static bool Enabled { get { var r = Volatile.Read(ref runner); return r != null && !r.StopRequested; } }

        public static string Status
        {
            get
            {
                var r = Volatile.Read(ref runner);
                return "Messung: " + (r == null ? "aus" : r.Status);
            }
        }

        public static string Start()
        {
            lock (gate)
            {
                if (runner != null && !runner.StopRequested) return "Telemetrie-Messung läuft bereits.";
                if (runner != null && !runner.Finished) return "Die vorige Messung wird noch abgeschlossen.";
                try
                {
                    var next = new Runner(Folder);
                    Volatile.Write(ref runner, next);
                    next.Begin();
                    return "Telemetrie-Messung aktiv. Bericht: " + Folder;
                }
                catch (Exception e)
                {
                    return "Telemetrie-Messung konnte nicht starten: " + e.GetType().Name;
                }
            }
        }

        public static void Stop()
        {
            var r = Volatile.Read(ref runner);
            if (r != null) r.RequestStop();
        }

        /// <summary>Beim Beenden der Anwendung: Bericht schreiben, höchstens kurz warten.</summary>
        public static void Shutdown()
        {
            var r = Volatile.Read(ref runner);
            if (r == null) return;
            r.RequestStop();
            r.Join(TimeSpan.FromSeconds(3));
        }

        private sealed class Runner
        {
            private const int ReportEveryMs = 30000;
            private const int StalledReopenMs = 5000;
            private const int SequenceAttempts = 8;
            private readonly string folder;
            private readonly ManualResetEvent stop = new ManualResetEvent(false);
            private Thread thread;
            private volatile bool stopRequested, finished;
            private volatile string status = "startet";
            private readonly DateTime startedLocal = DateTime.Now;
            private readonly string stamp;

            internal Runner(string folder)
            {
                this.folder = folder;
                stamp = startedLocal.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            }

            internal bool StopRequested { get { return stopRequested; } }
            internal bool Finished { get { return finished; } }
            internal string Status { get { return status; } }

            internal void Begin()
            {
                thread = new Thread(Run) { IsBackground = true, Name = "RRCA Telemetrie-Messung" };
                thread.Start();
            }

            internal void RequestStop() { stopRequested = true; stop.Set(); }

            internal void Join(TimeSpan timeout) { var t = thread; if (t != null) t.Join(timeout); }

            /// <summary>Unterstützte Spiele in Prüfreihenfolge. Das laufende Spiel wird automatisch erkannt.</summary>
            private static ProbeAnalyzer[] CreateAnalyzers()
            {
                return new ProbeAnalyzer[] { new R3EProbeAnalyzer(), new AMS2ProbeAnalyzer(), new LMUProbeAnalyzer() };
            }

            private const string Waiting = "wartet auf RaceRoom, AMS2 oder LMU";

            private void Run()
            {
                ProbeAnalyzer[] analyzers = null;
                ProbeAnalyzer analyzer = null;
                byte[][] buffers = null;
                int current = -1, preferred = 0;
                MemoryMappedFile file = null;
                MemoryMappedViewAccessor view = null;
                ProbeSource source = null;
                bool timerRaised = false;
                var cpuWatch = Stopwatch.StartNew();
                TimeSpan cpuStart = TimeSpan.Zero;
                double cpuPercent = double.NaN;
                long nextReport = 0, lastReopenCheck = 0;
                try
                {
                    analyzers = CreateAnalyzers();
                    buffers = new byte[analyzers.Length][];
                    for (int i = 0; i < analyzers.Length; i++) buffers[i] = new byte[analyzers[i].StructSize];
                    try { timerRaised = NativeMethods.timeBeginPeriod(1) == 0; } catch (Exception) { }
                    try { cpuStart = Process.GetCurrentProcess().TotalProcessorTime; } catch (Exception) { }
                    long reportTicks = Stopwatch.Frequency * ReportEveryMs / 1000;
                    nextReport = Stopwatch.GetTimestamp() + reportTicks;
                    status = Waiting;
                    while (!stopRequested)
                    {
                        if (view == null && source == null)
                        {
                            current = -1;
                            for (int k = 0; k < analyzers.Length && current < 0; k++)
                            {
                                int i = (preferred + k) % analyzers.Length;
                                if (analyzers[i].LayoutError != null) continue;
                                ProbeSource own = analyzers[i].CreateSource();
                                if (own != null)
                                {
                                    if (own.Open()) { source = own; current = i; }
                                    else own.Dispose();
                                }
                                else if (TryOpen(analyzers[i], out file, out view)) current = i;
                            }
                            if (current < 0)
                            {
                                status = LayoutProblem(analyzers) ?? Waiting;
                                if (stop.WaitOne(2000)) break;
                                continue;
                            }
                            analyzer = analyzers[current];
                            lastReopenCheck = Stopwatch.GetTimestamp();
                            status = "verbunden · " + analyzer.SimName;
                        }
                        byte[] buffer = buffers[current];
                        long t0 = Stopwatch.GetTimestamp();
                        int torn;
                        bool ok = source != null ? source.Copy(buffer, out torn) : Copy(view, buffer, analyzer.SequenceOffset, out torn);
                        analyzer.TornFrames += torn;
                        if (!ok)
                        {
                            if (torn == 0) { Close(ref file, ref view, ref source); continue; }
                            stop.WaitOne(1);
                            continue; // Spiel schreibt gerade: nächstes Bild abwarten
                        }
                        long t1 = Stopwatch.GetTimestamp();
                        analyzer.Observe(buffer, t0, t1 - t0);

                        // Spiel beendet oder anderes Spiel gestartet? Eine alte Abbildung kann bestehen bleiben – dann das nächste Spiel zuerst prüfen.
                        // Im Menü länger warten (dort stehen manche Spiele still), bei Fahrt schon nach 5 s.
                        int stallLimit = analyzer.Inactive(buffer) ? StalledReopenMs * 3 : StalledReopenMs;
                        if (analyzer.MsSinceLastFrame(t1) > stallLimit && (t1 - lastReopenCheck) * 1000 / Stopwatch.Frequency > stallLimit)
                        {
                            Close(ref file, ref view, ref source);
                            preferred = (current + 1) % analyzers.Length;
                            lastReopenCheck = t1;
                            status = "keine neuen Daten – suche Spiel";
                            continue;
                        }

                        if (t1 >= nextReport)
                        {
                            cpuPercent = Cpu(cpuStart, cpuWatch);
                            WriteAll(analyzers, cpuPercent);
                            nextReport = t1 + reportTicks;
                        }
                        if (analyzer.Polls % 500 == 0) status = Describe(analyzer);
                        stop.WaitOne(1);
                    }
                }
                catch (Exception e)
                {
                    status = "Fehler · " + e.GetType().Name;
                }
                finally
                {
                    Close(ref file, ref view, ref source);
                    if (timerRaised) { try { NativeMethods.timeEndPeriod(1); } catch (Exception) { } }
                    int written = analyzers == null ? 0 : WriteAll(analyzers, Cpu(cpuStart, cpuWatch));
                    if (written > 0) status = "beendet · Bericht im Diagnoseordner (Telemetrie-Probe)";
                    else if (!status.StartsWith("Fehler", StringComparison.Ordinal)) status = "beendet · keine Fahrdaten erhalten";
                    stopRequested = true;
                    finished = true;
                }
            }

            private static string LayoutProblem(ProbeAnalyzer[] analyzers)
            {
                foreach (ProbeAnalyzer a in analyzers)
                    if (a.LayoutError != null) return "Fehler · Datenstruktur passt nicht (" + a.SimName + ": " + a.LayoutError + ")";
                return null;
            }

            /// <summary>Schreibt je Spiel mit Fahrdaten einen Bericht. Gibt die Zahl der Berichte zurück.</summary>
            private int WriteAll(ProbeAnalyzer[] analyzers, double cpuPercent)
            {
                int n = 0;
                foreach (ProbeAnalyzer a in analyzers)
                    if (a.ActiveFrames > 0 && TryWrite(a, cpuPercent)) n++;
                return n;
            }

            private static string Describe(ProbeAnalyzer a)
            {
                if (a.ActiveMs <= 0) return "verbunden · wartet auf Fahrt";
                return "läuft · " + a.SimName + " · " + ((int)(a.ActiveMs / 5000) * 5).ToString(CultureInfo.InvariantCulture) + " s · " +
                    (double.IsNaN(a.SourceFramesPerSecond) ? "–" : Math.Round(a.SourceFramesPerSecond).ToString(CultureInfo.InvariantCulture)) +
                    " Bilder/s · " + a.MaxOpponents.ToString(CultureInfo.InvariantCulture) + " Gegner";
            }

            private bool TryWrite(ProbeAnalyzer analyzer, double cpuPercent)
            {
                try
                {
                    ProbeReport.Write(folder, analyzer.FilePrefix + "_" + stamp, analyzer, startedLocal, cpuPercent, SafeBuild());
                    return true;
                }
                catch (Exception e)
                {
                    status = "Fehler beim Schreiben · " + e.GetType().Name;
                    return false;
                }
            }

            private static string SafeBuild()
            {
                try { return RrcaForm.Version(); } catch (Exception) { return null; }
            }

            private static double Cpu(TimeSpan start, Stopwatch wall)
            {
                try
                {
                    double used = (Process.GetCurrentProcess().TotalProcessorTime - start).TotalMilliseconds;
                    double available = wall.Elapsed.TotalMilliseconds * Environment.ProcessorCount;
                    return available <= 0 ? double.NaN : 100.0 * used / available;
                }
                catch (Exception) { return double.NaN; }
            }

            private static bool TryOpen(ProbeAnalyzer analyzer, out MemoryMappedFile file, out MemoryMappedViewAccessor view)
            {
                file = null; view = null;
                try
                {
                    file = MemoryMappedFile.OpenExisting(analyzer.SharedMemoryName, MemoryMappedFileRights.Read);
                    view = file.CreateViewAccessor(0, analyzer.StructSize, MemoryMappedFileAccess.Read);
                    return true;
                }
                catch (Exception)
                {
                    if (view != null) view.Dispose();
                    if (file != null) file.Dispose();
                    file = null; view = null;
                    return false;
                }
            }

            /// <summary>
            /// Kopiert ein Rohbild. Mit Sequenznummer (AMS2): nur ein vollständig geschriebenes Bild zählt;
            /// <paramref name="torn"/> zählt verworfene Versuche. Rückgabe false und torn 0 = Verbindung verloren.
            /// </summary>
            private static unsafe bool Copy(MemoryMappedViewAccessor view, byte[] buffer, int sequenceOffset, out int torn)
            {
                torn = 0;
                byte* pointer = null;
                var handle = view.SafeMemoryMappedViewHandle;
                try
                {
                    handle.AcquirePointer(ref pointer);
                    byte* start = pointer + view.PointerOffset;
                    if (sequenceOffset < 0)
                    {
                        Marshal.Copy(new IntPtr(start), buffer, 0, buffer.Length);
                        return true;
                    }
                    for (int attempt = 0; attempt < SequenceAttempts; attempt++)
                    {
                        uint before = *(uint*)(start + sequenceOffset);
                        if ((before & 1) == 0)
                        {
                            Marshal.Copy(new IntPtr(start), buffer, 0, buffer.Length);
                            uint after = *(uint*)(start + sequenceOffset);
                            if (before == after && BitConverter.ToUInt32(buffer, sequenceOffset) == before) return true;
                        }
                        torn++;
                        Thread.Yield();
                    }
                    return false;
                }
                catch (Exception)
                {
                    torn = 0;
                    return false;
                }
                finally
                {
                    if (pointer != null) handle.ReleasePointer();
                }
            }

            private static void Close(ref MemoryMappedFile file, ref MemoryMappedViewAccessor view, ref ProbeSource source)
            {
                try { if (source != null) source.Dispose(); } catch (Exception) { }
                source = null;
                try { if (view != null) view.Dispose(); } catch (Exception) { }
                try { if (file != null) file.Dispose(); } catch (Exception) { }
                view = null; file = null;
            }
        }

        private static class NativeMethods
        {
            [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
            [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);
        }
    }
}
