using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using CrewChiefV4.RaceRoom;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>
    /// Telemetrie-Messung (P1): liest die Rohdaten des Spiels in einem eigenen Faden, unabhängig von der
    /// Crew-Chief-Hauptschleife, und schreibt einen Bericht in den Diagnoseordner. Nur lokal, nur lesend.
    /// Ändert weder Spielzustand noch Funk. Unterstützt zuerst RaceRoom; weitere Spiele folgen.
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
            private readonly string folder;
            private readonly ManualResetEvent stop = new ManualResetEvent(false);
            private Thread thread;
            private volatile bool stopRequested, finished;
            private volatile string status = "startet";
            private readonly DateTime startedLocal = DateTime.Now;
            private readonly string baseName;

            internal Runner(string folder)
            {
                this.folder = folder;
                baseName = "R3E_" + startedLocal.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
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

            private void Run()
            {
                R3EProbeAnalyzer analyzer = null;
                MemoryMappedFile file = null;
                MemoryMappedViewAccessor view = null;
                byte[] buffer = null;
                bool timerRaised = false;
                var cpuWatch = Stopwatch.StartNew();
                TimeSpan cpuStart = TimeSpan.Zero;
                double cpuPercent = double.NaN;
                long nextReport = 0, lastReopenCheck = 0;
                try
                {
                    analyzer = new R3EProbeAnalyzer();
                    if (analyzer.LayoutError != null)
                    {
                        status = "Fehler · Datenstruktur passt nicht (" + analyzer.LayoutError + ")";
                        return;
                    }
                    buffer = new byte[analyzer.StructSize];
                    try { timerRaised = NativeMethods.timeBeginPeriod(1) == 0; } catch (Exception) { }
                    try { cpuStart = Process.GetCurrentProcess().TotalProcessorTime; } catch (Exception) { }
                    long reportTicks = Stopwatch.Frequency * ReportEveryMs / 1000;
                    nextReport = Stopwatch.GetTimestamp() + reportTicks;
                    status = "wartet auf RaceRoom";
                    while (!stopRequested)
                    {
                        if (view == null)
                        {
                            if (!TryOpen(analyzer.StructSize, out file, out view))
                            {
                                status = "wartet auf RaceRoom";
                                if (stop.WaitOne(2000)) break;
                                continue;
                            }
                            lastReopenCheck = Stopwatch.GetTimestamp();
                        }
                        long t0 = Stopwatch.GetTimestamp();
                        if (!Copy(view, buffer))
                        {
                            Close(ref file, ref view);
                            continue;
                        }
                        long t1 = Stopwatch.GetTimestamp();
                        analyzer.Observe(buffer, t0, t1 - t0);

                        // Spiel beendet? Die Abbildung bleibt bestehen, solange wir sie offen halten – dann neu öffnen.
                        if (!analyzer.Paused(buffer) && analyzer.MsSinceLastFrame(t1) > StalledReopenMs &&
                            (t1 - lastReopenCheck) * 1000 / Stopwatch.Frequency > StalledReopenMs)
                        {
                            Close(ref file, ref view);
                            lastReopenCheck = t1;
                            status = "keine neuen Daten – verbinde neu";
                            continue;
                        }

                        if (t1 >= nextReport)
                        {
                            cpuPercent = Cpu(cpuStart, cpuWatch);
                            TryWrite(analyzer, cpuPercent);
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
                    Close(ref file, ref view);
                    if (timerRaised) { try { NativeMethods.timeEndPeriod(1); } catch (Exception) { } }
                    if (analyzer != null && analyzer.LayoutError == null && analyzer.Polls > 0)
                    {
                        cpuPercent = Cpu(cpuStart, cpuWatch);
                        if (TryWrite(analyzer, cpuPercent)) status = "beendet · Bericht im Diagnoseordner (Telemetrie-Probe)";
                    }
                    else if (!status.StartsWith("Fehler", StringComparison.Ordinal))
                    {
                        status = "beendet · keine Daten von RaceRoom erhalten";
                    }
                    stopRequested = true;
                    finished = true;
                }
            }

            private static string Describe(R3EProbeAnalyzer a)
            {
                if (a.ActiveMs <= 0) return "verbunden · wartet auf Fahrt";
                return "läuft · RaceRoom · " + ((int)(a.ActiveMs / 5000) * 5).ToString(CultureInfo.InvariantCulture) + " s · " +
                    (double.IsNaN(a.SourceFramesPerSecond) ? "–" : Math.Round(a.SourceFramesPerSecond).ToString(CultureInfo.InvariantCulture)) +
                    " Bilder/s · " + a.MaxOpponents.ToString(CultureInfo.InvariantCulture) + " Gegner";
            }

            private bool TryWrite(R3EProbeAnalyzer analyzer, double cpuPercent)
            {
                try
                {
                    ProbeReport.Write(folder, baseName, analyzer, startedLocal, cpuPercent, SafeBuild());
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

            private static bool TryOpen(int size, out MemoryMappedFile file, out MemoryMappedViewAccessor view)
            {
                file = null; view = null;
                try
                {
                    file = MemoryMappedFile.OpenExisting(RaceRoomConstant.SharedMemoryName, MemoryMappedFileRights.Read);
                    view = file.CreateViewAccessor(0, size, MemoryMappedFileAccess.Read);
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

            private static unsafe bool Copy(MemoryMappedViewAccessor view, byte[] buffer)
            {
                byte* pointer = null;
                var handle = view.SafeMemoryMappedViewHandle;
                try
                {
                    handle.AcquirePointer(ref pointer);
                    Marshal.Copy(new IntPtr(pointer + view.PointerOffset), buffer, 0, buffer.Length);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
                finally
                {
                    if (pointer != null) handle.ReleasePointer();
                }
            }

            private static void Close(ref MemoryMappedFile file, ref MemoryMappedViewAccessor view)
            {
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
