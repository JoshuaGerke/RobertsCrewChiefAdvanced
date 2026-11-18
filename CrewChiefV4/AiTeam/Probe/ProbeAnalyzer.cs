using System;
using System.Collections.Generic;
using System.Text;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>
    /// Spielunabhängige Auswertung der Telemetrie-Messung. Ein Spiel liefert nur: Zustand (Fahrt/Pause/Menü/Wiederholung),
    /// Bildtakt, Kopfprüfung, Strecke, Fahrzeugliste und seine Kanäle. Alles Zählen und Messen passiert hier.
    /// Testbar mit künstlichen Puffern; der Lesefaden liegt in <see cref="TelemetryProbe"/>.
    /// </summary>
    internal abstract class ProbeAnalyzer
    {
        internal readonly List<ProbeChannel> Channels = new List<ProbeChannel>();
        internal int StructSize { get; private protected set; }
        internal string LayoutError { get; private protected set; }
        internal abstract string SimName { get; }
        internal abstract string FilePrefix { get; }
        internal abstract string SharedMemoryName { get; }
        /// <summary>Byte-Offset einer Sequenznummer (ungerade = Spiel schreibt gerade), sonst -1.</summary>
        internal virtual int SequenceOffset { get { return -1; } }
        /// <summary>Liefert das Spiel einen eigenen Physik-Takt (Ticks), der mehr sagt als die Bildfolge?</summary>
        internal virtual bool HasSimClock { get { return true; } }
        /// <summary>
        /// Eigene Quelle für Spiele, die mehr als eine Abbildung brauchen oder anders gegen halb geschriebene Bilder
        /// schützen (LMU: Telemetrie und Wertung mit rF2-Versionsblock). null = eine Abbildung über <see cref="SharedMemoryName"/>.
        /// </summary>
        internal virtual ProbeSource CreateSource() { return null; }
        /// <summary>Wird vor jeder Auswertung eines Rohbilds aufgerufen (z. B. um das eigene Auto an eine feste Stelle zu kopieren).</summary>
        protected virtual void Prepare(byte[] buffer) { }

        protected readonly double ticksPerMs;
        private ProbeChannel[] singles = new ProbeChannel[0], opponents = new ProbeChannel[0], ownEntry = new ProbeChannel[0];

        // Zustand
        private bool hasFrame, wasActive, frameIntervalValid;
        private long lastClock;
        private double lastClockTime;
        private long lastFrameTicks, lastPollTicks;
        private readonly bool[] presentNow = new bool[ProbeChannel.MaxSlots];
        private readonly bool[] presentBefore = new bool[ProbeChannel.MaxSlots];
        private readonly int[] entryIndex = new int[ProbeChannel.MaxSlots];
        private readonly int[] entrySlot = new int[ProbeChannel.MaxSlots];
        private long lastSessionKey = long.MinValue;

        // Ergebnisse (für Bericht)
        internal readonly IntervalHistogram FrameIntervals = new IntervalHistogram();
        internal readonly IntervalHistogram PollIntervals = new IntervalHistogram();
        internal long Polls, ActivePolls, Frames, ActiveFrames, CostTicksSum, CostTicksMax, TornFrames;
        internal double ActiveMs, PausedMs, MenuMs, ReplayMs, StalledMs;
        internal long SimTicksAdvanced;
        internal double SimTicksWallMs;
        internal int MaxOpponents, Sessions;
        internal double OpponentSlotMs;
        internal string TrackName, LayoutName, HeaderError, DataVersion;

        protected ProbeAnalyzer(long tickFrequency)
        {
            ticksPerMs = tickFrequency / 1000.0;
        }

        /// <summary>Am Ende des Konstruktors der Unterklasse aufrufen, wenn alle Kanäle angelegt sind.</summary>
        protected void Finish()
        {
            var s = new List<ProbeChannel>(); var o = new List<ProbeChannel>(); var l = new List<ProbeChannel>();
            foreach (ProbeChannel c in Channels)
            {
                if (c.Scope == ProbeScope.Opponents) o.Add(c);
                else if (c.Scope == ProbeScope.OwnListEntry) l.Add(c);
                else s.Add(c);
            }
            singles = s.ToArray(); opponents = o.ToArray(); ownEntry = l.ToArray();
        }

        // ---- je Spiel ----
        /// <param name="clock">Zähler, der sich mit jedem neuen Bild ändert.</param>
        /// <param name="clockTime">Zusätzliche Simulationszeit, falls vorhanden (sonst 0).</param>
        protected abstract void ReadState(byte[] b, out bool paused, out bool menu, out bool replay, out long clock, out double clockTime);
        /// <summary>Gibt eine Fehlerbeschreibung zurück, wenn das Spiel ein anderes Datenformat meldet; setzt <see cref="DataVersion"/>.</summary>
        protected abstract string CheckHeader(byte[] b);
        protected abstract long SessionKey(byte[] b);
        protected abstract void ReadTrack(byte[] b, out string track, out string layout);
        /// <summary>Füllt Eintragsnummer und Fahrzeug-Slot aller Gegner; <paramref name="own"/> ist der eigene Eintrag oder -1.</summary>
        protected abstract int CollectEntries(byte[] b, int[] index, int[] slot, out int own);

        internal bool ReadyFor(byte[] buffer) { return buffer != null && buffer.Length >= StructSize; }

        internal bool Inactive(byte[] buffer)
        {
            if (!ReadyFor(buffer)) return true;
            bool paused, menu, replay; long clock; double time;
            ReadState(buffer, out paused, out menu, out replay, out clock, out time);
            return paused || menu || replay;
        }

        /// <summary>Wertet ein Rohbild aus. <paramref name="now"/> und <paramref name="costTicks"/> in Stopwatch-Ticks.</summary>
        internal void Observe(byte[] buffer, long now, long costTicks)
        {
            if (!ReadyFor(buffer)) return;
            Prepare(buffer);
            Polls++;
            CostTicksSum += costTicks;
            if (costTicks > CostTicksMax) CostTicksMax = costTicks;
            double sinceLastPollMs = lastPollTicks == 0 ? 0 : (now - lastPollTicks) / ticksPerMs;
            if (lastPollTicks != 0) PollIntervals.Add(sinceLastPollMs);
            lastPollTicks = now;

            HeaderError = CheckHeader(buffer);

            bool paused, menu, replay; long clock; double clockTime;
            ReadState(buffer, out paused, out menu, out replay, out clock, out clockTime);
            bool newFrame = !hasFrame || clock != lastClock || !clockTime.Equals(lastClockTime);
            bool active = !paused && !menu && !replay;

            if (newFrame)
            {
                Frames++;
                if (active && frameIntervalValid) FrameIntervals.Add((now - lastFrameTicks) / ticksPerMs);
                if (active && frameIntervalValid && clock > lastClock)
                {
                    // Nur zusammenhängende aktive Abschnitte zählen (keine Pausen, kein Sitzungswechsel).
                    SimTicksAdvanced += clock - lastClock;
                    SimTicksWallMs += (now - lastFrameTicks) / ticksPerMs;
                }
                lastFrameTicks = now;
                lastClock = clock;
                lastClockTime = clockTime;
                hasFrame = true;
                frameIntervalValid = active;
            }
            if (!active) frameIntervalValid = false;

            if (paused) PausedMs += sinceLastPollMs;
            else if (menu) MenuMs += sinceLastPollMs;
            else if (replay) ReplayMs += sinceLastPollMs;
            else if (!newFrame && hasFrame && (now - lastFrameTicks) / ticksPerMs > 1000) StalledMs += sinceLastPollMs;
            else ActiveMs += sinceLastPollMs;

            if (!active)
            {
                if (wasActive) InterruptAll();
                wasActive = false;
                return;
            }
            if (!wasActive) InterruptAll();
            wasActive = true;
            ActivePolls++;
            if (newFrame) ActiveFrames++;

            NoteSession(buffer);

            for (int i = 0; i < singles.Length; i++)
                singles[i].ObserveSingle(singles[i].Read(buffer, 0), now, newFrame, ticksPerMs);
            ObserveDrivers(buffer, now, newFrame, sinceLastPollMs);
        }

        private void InterruptAll()
        {
            foreach (ProbeChannel c in Channels) c.Interrupt();
            Array.Clear(presentBefore, 0, presentBefore.Length);
        }

        private void ObserveDrivers(byte[] buffer, long now, bool newFrame, double sinceLastPollMs)
        {
            int own;
            int n = CollectEntries(buffer, entryIndex, entrySlot, out own);
            if (own >= 0)
                for (int k = 0; k < ownEntry.Length; k++) ownEntry[k].ObserveSingle(ownEntry[k].Read(buffer, own), now, newFrame, ticksPerMs);
            Array.Clear(presentNow, 0, presentNow.Length);
            int count = 0;
            for (int e = 0; e < n; e++)
            {
                int slot = entrySlot[e];
                if (slot < 0 || slot >= ProbeChannel.MaxSlots || presentNow[slot]) continue; // doppelter Slot: nur den ersten zählen
                presentNow[slot] = true;
                count++;
                for (int k = 0; k < opponents.Length; k++) opponents[k].ObserveSlot(slot, opponents[k].Read(buffer, entryIndex[e]), now, newFrame, ticksPerMs);
            }
            for (int slot = 0; slot < ProbeChannel.MaxSlots; slot++)
            {
                if (presentBefore[slot] && !presentNow[slot])
                    for (int k = 0; k < opponents.Length; k++) opponents[k].ForgetSlot(slot);
                presentBefore[slot] = presentNow[slot];
            }
            if (count > MaxOpponents) MaxOpponents = count;
            OpponentSlotMs += sinceLastPollMs * count;
        }

        private void NoteSession(byte[] buffer)
        {
            long key = SessionKey(buffer);
            if (key == lastSessionKey) return;
            lastSessionKey = key;
            Sessions++;
            string track, layout;
            ReadTrack(buffer, out track, out layout);
            TrackName = track; LayoutName = layout;
        }

        protected static string Utf8(byte[] buffer, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && end < buffer.Length && buffer[end] != 0) end++;
            return Encoding.UTF8.GetString(buffer, offset, end - offset);
        }

        protected void AddChannel(string id, string group, string path, string unit, ProbeScope scope, ProbeKind kind, int offset, int stride,
            string note = null, double sentinel = double.NaN)
        {
            Channels.Add(new ProbeChannel(id, group, path, unit, scope, kind, offset, stride, note, sentinel));
        }

        internal double SourceFramesPerSecond { get { return ActiveMs <= 0 ? double.NaN : ActiveFrames / (ActiveMs / 1000.0); } }
        internal double SimTicksPerSecond { get { return !HasSimClock || SimTicksWallMs <= 0 ? double.NaN : SimTicksAdvanced / (SimTicksWallMs / 1000.0); } }

        /// <summary>Änderungen je Sekunde: Spieler/Sitzung auf aktive Zeit, Gegner je Auto.</summary>
        internal double ChangesPerSecond(ProbeChannel c)
        {
            double ms = c.Scope == ProbeScope.Opponents ? OpponentSlotMs : ActiveMs;
            return ms <= 0 ? double.NaN : c.Changes / (ms / 1000.0);
        }

        internal double FrameShare(ProbeChannel c) { return c.Frames == 0 ? double.NaN : (double)c.Changes / c.Frames; }

        internal double MsSinceLastFrame(long now) { return !hasFrame ? double.PositiveInfinity : (now - lastFrameTicks) / ticksPerMs; }

        internal double TicksPerMs { get { return ticksPerMs; } }
    }

    /// <summary>Lesezugriff auf eine Abbildung des Spiels. Eigene Schnittstelle, damit die Kopierlogik ohne Spiel prüfbar ist.</summary>
    internal interface IProbeMap
    {
        uint U32(int at);
        int I32(int at);
        void Bytes(int at, byte[] target, int targetOffset, int count);
    }

    /// <summary>Quelle mit eigener Öffnungs- und Kopierlogik (siehe <see cref="ProbeAnalyzer.CreateSource"/>). Nur lesend.</summary>
    internal abstract class ProbeSource : IDisposable
    {
        internal abstract bool Open();
        /// <summary>Kopiert ein vollständiges Rohbild. Rückgabe false und <paramref name="torn"/> 0 = Verbindung verloren.</summary>
        internal abstract bool Copy(byte[] buffer, out int torn);
        public abstract void Dispose();
    }
}
