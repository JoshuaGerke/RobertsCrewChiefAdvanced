using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Gemessener Boxenverlust (Rob 17.11.2026, Masterprompt C5): Wie viel Zeit kostet Rob ein Stopp auf dieser Strecke?
    /// Nach jedem eigenen Stopp: Summe der Runden mit Boxengasse (Ein- und Ausfahrt) minus gleich viele Referenzrunden
    /// (Median der bis zu fünf sauberen Runden davor). Das enthält die Standzeit, also Tanken und Reifen. Nur unter Grün und
    /// mit mindestens zwei Referenzrunden; ohne Messung gibt es keinen Wert – die Crew rechnet dann nicht damit.
    /// </summary>
    internal sealed class PitLossMeter
    {
        internal const float Min = 5f, Max = 150f;
        private readonly List<float> losses = new List<float>();
        private int lastMeasuredLap = -1, session = int.MinValue;

        internal void Reset() { losses.Clear(); lastMeasuredLap = -1; session = int.MinValue; }

        /// <summary>Median der gemessenen Stopps dieser Sitzung in Sekunden; -1 ohne Messung.</summary>
        internal float Seconds
        {
            get
            {
                if (losses.Count == 0) return -1;
                var sorted = losses.OrderBy(v => v).ToArray();
                return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2f;
            }
        }

        internal int Samples => losses.Count;

        private static bool Clean(LapRecord l) => l != null && l.Valid && l.Green && !l.InPitLap && l.LapTime > 1 && l.LapTime < 3600;

        /// <summary>
        /// Nach jeder beendeten Runde mit den Runden der Sitzung (alt → neu). Gemessen wird, sobald nach einem Stopp die erste
        /// Runde ohne Boxengasse fertig ist. Liefert den neuen Messwert oder -1.
        /// </summary>
        internal float Observe(int sessionId, IList<LapRecord> laps)
        {
            if (sessionId != session) { Reset(); session = sessionId; }
            if (laps == null || laps.Count < 4) return -1;
            int last = laps.Count - 1;
            if (laps[last] == null || laps[last].InPitLap || laps[last - 1] == null || !laps[last - 1].InPitLap) return -1;
            int end = last - 1, start = end;
            while (start - 1 >= 0 && laps[start - 1] != null && laps[start - 1].InPitLap) start--;
            if (laps[end].LapNumber <= lastMeasuredLap) return -1;
            lastMeasuredLap = laps[end].LapNumber;
            int pitLaps = end - start + 1;
            if (pitLaps > 2) return -1; // mehr als Ein- und Ausfahrt: keine normale Durchfahrt
            var run = laps.Skip(start).Take(pitLaps).ToList();
            if (run.Any(l => !l.Green || !(l.LapTime > 1) || l.LapTime >= 3600) || run.First().LapNumber <= 1) return -1;
            var reference = laps.Take(start).Reverse().Take(5).Where(Clean).Select(l => l.LapTime).OrderBy(v => v).ToArray();
            if (reference.Length < 2) return -1;
            float median = reference.Length % 2 == 1 ? reference[reference.Length / 2] : (reference[reference.Length / 2 - 1] + reference[reference.Length / 2]) / 2f;
            float loss = run.Sum(l => l.LapTime) - pitLaps * median;
            if (loss < Min || loss > Max) return -1;
            losses.Add(loss);
            LiveDiagnostics.Decision("pit-loss", "measured", "pit_laps_minus_clean_reference", loss.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            return loss;
        }
    }
}
