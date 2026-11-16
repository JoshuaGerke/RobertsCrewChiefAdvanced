using System;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>Feste Intervall-Klassen in Millisekunden; legt im Messbetrieb nichts neu an.</summary>
    internal sealed class IntervalHistogram
    {
        internal static readonly double[] UpperMs =
            { 1, 2, 3, 5, 8, 12, 17, 20, 25, 34, 50, 75, 100, 150, 250, 500, 1000, 2000, 5000, double.PositiveInfinity };

        private readonly long[] counts = new long[UpperMs.Length];
        internal long Count;
        internal double SumMs;
        internal double MinMs = double.PositiveInfinity;
        internal double MaxMs;

        internal void Add(double ms)
        {
            if (double.IsNaN(ms) || ms < 0) return;
            int i = 0;
            while (ms > UpperMs[i]) i++;
            counts[i]++;
            Count++;
            SumMs += ms;
            if (ms < MinMs) MinMs = ms;
            if (ms > MaxMs) MaxMs = ms;
        }

        internal double MeanMs { get { return Count == 0 ? double.NaN : SumMs / Count; } }

        /// <summary>Perzentil (0..1), linear innerhalb der Klasse; begrenzt auf den gemessenen Bereich.</summary>
        internal double Percentile(double p)
        {
            if (Count == 0) return double.NaN;
            double target = Math.Max(1, Math.Ceiling(p * Count));
            long cumulative = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0) continue;
                if (cumulative + counts[i] >= target)
                {
                    double lower = i == 0 ? 0 : UpperMs[i - 1];
                    double upper = double.IsInfinity(UpperMs[i]) ? MaxMs : UpperMs[i];
                    lower = Math.Max(lower, MinMs);
                    upper = Math.Min(upper, MaxMs);
                    if (upper < lower) upper = lower;
                    double within = (target - cumulative) / counts[i];
                    return lower + (upper - lower) * within;
                }
                cumulative += counts[i];
            }
            return MaxMs;
        }

        internal long[] Buckets() { return (long[])counts.Clone(); }
    }

    internal enum ProbeScope { Player, OwnListEntry, Opponents, Session }

    internal enum ProbeKind { Int32, UInt32, Single, Double, Byte }

    /// <summary>Ein gemessener Kanal mit Zählern. Für Gegner werden Werte je Fahrzeug-Slot verfolgt.</summary>
    internal sealed class ProbeChannel
    {
        internal const int MaxSlots = 256;

        internal readonly string Id, Group, Path, Unit, Note;
        internal readonly ProbeScope Scope;
        internal readonly ProbeKind Kind;
        internal readonly int Offset, Stride;
        internal readonly double Sentinel;

        internal long Samples, Changes, ChangesWithoutFrame, Frames, Invalid, SentinelHits;
        internal double Min = double.PositiveInfinity, Max = double.NegativeInfinity;
        internal readonly IntervalHistogram Intervals = new IntervalHistogram();

        // Einzelwert (Spieler, Sitzung, eigener Listeneintrag)
        internal bool HasLast;
        internal double Last;
        internal long LastChangeTicks;

        // Je Gegner-Slot
        internal readonly bool[] SlotHas;
        internal readonly double[] SlotLast;
        internal readonly long[] SlotLastChange;

        /// <param name="offset">Byte-Offset ab Beginn des Rohbilds (bei Fahrzeugkanälen: für Eintrag 0).</param>
        /// <param name="stride">Abstand zwischen zwei Fahrzeugeinträgen in Byte; 0 für Einzelwerte.</param>
        internal ProbeChannel(string id, string group, string path, string unit, ProbeScope scope, ProbeKind kind, int offset, int stride,
            string note = null, double sentinel = double.NaN)
        {
            Id = id; Group = group; Path = path; Unit = unit; Scope = scope; Kind = kind; Offset = offset; Stride = stride; Note = note; Sentinel = sentinel;
            if (scope == ProbeScope.Opponents)
            {
                SlotHas = new bool[MaxSlots];
                SlotLast = new double[MaxSlots];
                SlotLastChange = new long[MaxSlots];
            }
        }

        /// <summary>Liest den Wert; <paramref name="index"/> ist der Fahrzeugeintrag (bei Einzelwerten 0).</summary>
        internal double Read(byte[] buffer, int index)
        {
            int at = Offset + index * Stride;
            switch (Kind)
            {
                case ProbeKind.Int32: return BitConverter.ToInt32(buffer, at);
                case ProbeKind.UInt32: return BitConverter.ToUInt32(buffer, at);
                case ProbeKind.Single: return BitConverter.ToSingle(buffer, at);
                case ProbeKind.Byte: return buffer[at];
                default: return BitConverter.ToDouble(buffer, at);
            }
        }

        internal static ProbeKind KindOf(Type type)
        {
            if (type == typeof(int)) return ProbeKind.Int32;
            if (type == typeof(uint)) return ProbeKind.UInt32;
            if (type == typeof(float)) return ProbeKind.Single;
            if (type == typeof(double)) return ProbeKind.Double;
            if (type == typeof(byte) || type == typeof(bool)) return ProbeKind.Byte;
            throw new NotSupportedException("Kein Zahlenfeld: " + type.Name);
        }

        /// <summary>Wertet einen Einzelwert aus. Gibt true zurück, wenn er sich geändert hat.</summary>
        internal bool ObserveSingle(double value, long ticks, bool newFrame, double ticksPerMs)
        {
            Samples++;
            if (newFrame) Frames++;
            if (!Track(value)) { HasLast = false; return false; }
            bool changed = false;
            if (HasLast && !value.Equals(Last))
            {
                changed = true;
                Changes++;
                if (!newFrame) ChangesWithoutFrame++;
                if (LastChangeTicks != 0) Intervals.Add((ticks - LastChangeTicks) / ticksPerMs);
                LastChangeTicks = ticks;
            }
            else if (!HasLast)
            {
                LastChangeTicks = 0;
            }
            Last = value;
            HasLast = true;
            return changed;
        }

        internal void ObserveSlot(int slot, double value, long ticks, bool newFrame, double ticksPerMs)
        {
            Samples++;
            if (newFrame) Frames++;
            if (!Track(value)) { SlotHas[slot] = false; return; }
            if (SlotHas[slot] && !value.Equals(SlotLast[slot]))
            {
                Changes++;
                if (!newFrame) ChangesWithoutFrame++;
                if (SlotLastChange[slot] != 0) Intervals.Add((ticks - SlotLastChange[slot]) / ticksPerMs);
                SlotLastChange[slot] = ticks;
            }
            else if (!SlotHas[slot])
            {
                SlotLastChange[slot] = 0;
            }
            SlotLast[slot] = value;
            SlotHas[slot] = true;
        }

        internal void ForgetSlot(int slot)
        {
            if (SlotHas != null) { SlotHas[slot] = false; SlotLastChange[slot] = 0; }
        }

        /// <summary>Nach Pause/Menü: Intervalle nicht über die Unterbrechung hinweg messen.</summary>
        internal void Interrupt()
        {
            HasLast = false;
            LastChangeTicks = 0;
            if (SlotHas != null) { Array.Clear(SlotHas, 0, SlotHas.Length); Array.Clear(SlotLastChange, 0, SlotLastChange.Length); }
        }

        private bool Track(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) { Invalid++; return false; }
            if (!double.IsNaN(Sentinel) && value == Sentinel) { SentinelHits++; return true; }
            if (value < Min) Min = value;
            if (value > Max) Max = value;
            return true;
        }

        /// <summary>Kurzer Zustand für den Bericht.</summary>
        internal string Status
        {
            get
            {
                if (Samples == 0) return "keine Daten";
                if (Invalid == Samples) return "ungültig";
                if (SentinelHits > 0 && SentinelHits + Invalid == Samples) return "fehlt (meldet " + FormatSentinel() + ")";
                if (Changes == 0) return "konstant";
                return "vorhanden";
            }
        }

        private string FormatSentinel() { return Sentinel.ToString(System.Globalization.CultureInfo.InvariantCulture); }
    }
}
