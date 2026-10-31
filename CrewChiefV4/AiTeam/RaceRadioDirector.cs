using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Lokaler, deterministischer Funk-Entscheider. Er verarbeitet höchstens 20
    /// Stichproben je Sekunde aus dem vorhandenen Crew-Chief-Tick, ohne Cloud-Aufruf.
    /// Quellen: verifizierte Renntelemetrie und valide, nicht in der Box gefahrene Runden.
    /// Er gibt keine Kollisions- oder Freigabewarnungen aus: das bleibt Saras Aufgabe.
    /// </summary>
    public sealed class RaceRadioDirector
    {
        public static readonly TimeSpan SamplePeriod = TimeSpan.FromMilliseconds(50); // höchstens 20 Hz; tatsächliche Rate vom Spieladapter abhängig
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
        private readonly Dictionary<string, DateTime> lastTopicUtc = new Dictionary<string, DateTime>();
        private DateTime lastSampleUtc = DateTime.MinValue;
        private DateTime lastTrafficUtc = DateTime.MinValue;
        private DateTime lastAnyInsightUtc = DateTime.MinValue;
        private int lastPaceLap = -1;
        private string trackedOpponentKey;
        private readonly Queue<Tuple<int, float>> measuredOpponentGaps = new Queue<Tuple<int, float>>();
        private int lastOpponentTrendLap = -1;
        private int lastFuelTrendLap = -1;
        private string trackedBehindKey;
        private readonly Queue<Tuple<int, float>> measuredBehindGaps = new Queue<Tuple<int, float>>();
        private int lastBehindTrendLap = -1;
        public int SampleCount { get; private set; }

        public void Reset()
        {
            lastSampleUtc = DateTime.MinValue;
            lastTrafficUtc = DateTime.MinValue;
            lastAnyInsightUtc = DateTime.MinValue;
            lastPaceLap = -1;
            trackedOpponentKey = null;
            measuredOpponentGaps.Clear();
            lastOpponentTrendLap = -1;
            lastFuelTrendLap = -1;
            SampleCount = 0;
            trackedBehindKey = null;
            measuredBehindGaps.Clear();
            lastBehindTrendLap = -1;
            lastTopicUtc.Clear();
        }

        /// <summary>
        /// Nur Stichprobenverarbeitung (niemals API). Achtung: Die Eingangsfrequenz
        /// hängt vom Spieladapter ab; 50 ms begrenzen nur die Höchstfrequenz.
        /// Geringe Abstände dienen ausschließlich der Funkruhe, nicht für
        /// Gegner- oder Überholprognosen ohne stabile Gegner-ID.
        /// </summary>
        /// <summary>Non-race TimeDelta fields are best-lap differences, never physical traffic.</summary>
        public bool Observe(DateTime nowUtc, GameStateData state)
        {
            if (state == null || state.SessionData.SessionType != SessionType.Race)
            {
                lastTrafficUtc = DateTime.MinValue;
                return Observe(nowUtc, -1, -1);
            }
            return Observe(nowUtc, state.SessionData.TimeDeltaFront, state.SessionData.TimeDeltaBehind);
        }

        public bool Observe(DateTime nowUtc, float gapFrontSeconds, float gapBehindSeconds)
        {
            if (nowUtc <= lastSampleUtc || nowUtc - lastSampleUtc < SamplePeriod)
            {
                return false;
            }
            lastSampleUtc = nowUtc;
            SampleCount++;
            if (Close(gapFrontSeconds) || Close(gapBehindSeconds))
            {
                lastTrafficUtc = nowUtc;
            }
            return true;
        }

        private static bool Close(float seconds)
        {
            return !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds > 0 && seconds < 2.25f;
        }

        public bool TrafficQuiet(DateTime nowUtc)
        {
            return lastTrafficUtc != DateTime.MinValue && nowUtc >= lastTrafficUtc && nowUtc - lastTrafficUtc < TimeSpan.FromSeconds(9);
        }

        /// <summary>Pro Themenkomplex nur nach Cooldown, zusätzlich 70 s allgemeine Funkruhe.</summary>
        public bool ReserveInsight(string topic, DateTime nowUtc, TimeSpan topicCooldown)
        {
            if (string.IsNullOrWhiteSpace(topic) || TrafficQuiet(nowUtc) ||
                (lastAnyInsightUtc != DateTime.MinValue && nowUtc < lastAnyInsightUtc) ||
                (lastAnyInsightUtc != DateTime.MinValue && nowUtc - lastAnyInsightUtc < TimeSpan.FromSeconds(70)))
            {
                LiveDiagnostics.Decision(topic,"suppressed",string.IsNullOrWhiteSpace(topic)?"empty_topic":TrafficQuiet(nowUtc)?"traffic_quiet":nowUtc<lastAnyInsightUtc?"clock_rollback":"global_cooldown");
                return false;
            }
            DateTime last;
            if (lastTopicUtc.TryGetValue(topic, out last) && (nowUtc < last || nowUtc - last < topicCooldown))
            {
                LiveDiagnostics.Decision(topic,"suppressed","topic_cooldown");
                return false;
            }
            LiveDiagnostics.Decision(topic,"reserved","cooldown_passed");
            lastTopicUtc[topic] = nowUtc;
            lastAnyInsightUtc = nowUtc;
            return true;
        }

        /// <summary>
        /// Positionsbereinigter Gegnertrend: ausschließlich echter Sim-Abstand
        /// in Sekunden zum selben, über Crew Chief identifizierten Vordermann.
        /// Keine Schätzwerte aus Streckendistanz, keine Überholprognose.
        /// Nur einmal bei drei unmittelbar aufeinanderfolgenden Runden.
        /// </summary>
        public string MeasuredOpponentClosing(string opponentKey, int completedLap, float measuredGapSeconds)
        {
            if (string.IsNullOrWhiteSpace(opponentKey) || completedLap <= 0 ||
                float.IsNaN(measuredGapSeconds) || float.IsInfinity(measuredGapSeconds) ||
                measuredGapSeconds <= 0 || measuredGapSeconds > 90)
            {
                trackedOpponentKey = null;
                measuredOpponentGaps.Clear();
                return LiveDiagnostics.NoTrend("opponent_front","invalid_measurement_or_identity");
            }
            if (!string.Equals(opponentKey, trackedOpponentKey, StringComparison.Ordinal))
            {
                trackedOpponentKey = opponentKey;
                measuredOpponentGaps.Clear();
            }
            if (measuredOpponentGaps.Count > 0 && measuredOpponentGaps.Last().Item1 == completedLap)
                return LiveDiagnostics.NoTrend("opponent_front","same_lap");
            if (measuredOpponentGaps.Count > 0 && measuredOpponentGaps.Last().Item1 + 1 != completedLap)
                measuredOpponentGaps.Clear();
            measuredOpponentGaps.Enqueue(Tuple.Create(completedLap, measuredGapSeconds));
            while (measuredOpponentGaps.Count > 3) measuredOpponentGaps.Dequeue();
            if (measuredOpponentGaps.Count < 3 || lastOpponentTrendLap == completedLap)
                return LiveDiagnostics.NoTrend("opponent_front","window_or_already_reported");
            float gain = measuredOpponentGaps.First().Item2 - measuredGapSeconds;
            if (gain < 0.8f || measuredGapSeconds < 2.5f) return LiveDiagnostics.NoTrend("opponent_front","gain_threshold_or_close_traffic");
            lastOpponentTrendLap = completedLap;
            return "Auf denselben Vordermann haben wir über zwei Runden " +
                gain.ToString("0.0", German) + " Sekunden aufgeholt. Der gemessene Abstand liegt jetzt bei " +
                measuredGapSeconds.ToString("0.0", German) + " Sekunden. Bleib bei deinem Rhythmus.";
        }

        /// <summary>
        /// Robuster Vergleich von drei aufeinanderfolgenden sauberen Runden
        /// mit den vorherigen drei sauberen Runden. Nur Messbefund, keine
        /// Ursachenbehauptung (Verkehr, Wetter, Reifen könnten mitwirken).
        /// </summary>
        public string ConsistentPaceLoss(IReadOnlyList<LapRecord> laps)
        {
            if (laps == null) return LiveDiagnostics.NoTrend("pace","no_laps");
            var valid = laps.Where(l => l.Valid && !l.InPitLap && l.LapTime > 20 &&
                !float.IsNaN(l.LapTime) && !float.IsInfinity(l.LapTime) && l.LapTime < 1200).ToList();
            if (valid.Count < 6) return LiveDiagnostics.NoTrend("pace","minimum_six_valid_laps");
            var recent = valid.Skip(valid.Count - 3).ToList();
            // Lücken durch ungültige Runden/Boxenstopps machen einen Vergleich irreführend.
            if (recent[2].LapNumber <= lastPaceLap ||
                !Contiguous(valid.Skip(valid.Count - 6).ToList())) return LiveDiagnostics.NoTrend("pace","non_contiguous_or_reported");
            float before = Median(valid.Skip(valid.Count - 6).Take(3).Select(l => l.LapTime));
            float after = Median(recent.Select(l => l.LapTime));
            float delta = after - before;
            if (delta < Math.Max(1.0f, before * 0.012f)) return LiveDiagnostics.NoTrend("pace","median_delta_threshold");
            // Mindestens zwei der drei Runden müssen tatsächlich langsamer sein.
            if (recent.Count(l => l.LapTime >= before + 0.8f) < 2) return LiveDiagnostics.NoTrend("pace","two_of_three_not_confirmed");
            lastPaceLap = recent[2].LapNumber;
            return "In den letzten drei sauberen Runden sind wir im Median " +
                delta.ToString("0.0", German) + " Sekunden langsamer als in den drei davor. " +
                "Ich prüfe den Trend weiter – aus den Rundenzeiten allein kenne ich die Ursache noch nicht.";
        }

        /// <summary>
        /// Mik erkennt an sechs unmittelbar aufeinanderfolgenden sauberen Runden
        /// einen deutlichen Verbrauchsanstieg; das ist eine Beobachtung, keine
        /// behauptete Ursache oder Boxenstrategie. Keine Prognose aus Ausreißern.
        /// </summary>
        public string MeasuredFuelIncrease(IReadOnlyList<LapRecord> laps)
        {
            if (laps == null) return LiveDiagnostics.NoTrend("fuel","no_laps");
            var valid = laps.Where(l => l.Valid && !l.InPitLap && l.FuelUsed > 0 &&
                !float.IsNaN(l.FuelUsed) && !float.IsInfinity(l.FuelUsed)).ToList();
            if (valid.Count < 6) return LiveDiagnostics.NoTrend("fuel","minimum_six_fuel_laps");
            var six = valid.Skip(valid.Count - 6).ToList();
            if (!Contiguous(six) || six[5].LapNumber <= lastFuelTrendLap) return LiveDiagnostics.NoTrend("fuel","non_contiguous_or_reported");
            float before = Median(six.Take(3).Select(l => l.FuelUsed));
            float after = Median(six.Skip(3).Select(l => l.FuelUsed));
            float diff = after - before;
            if (diff < Math.Max(0.12f, before * 0.06f) ||
                six.Skip(3).Count(l => l.FuelUsed > before + Math.Max(0.08f, before * 0.04f)) < 2)
                return LiveDiagnostics.NoTrend("fuel","fuel_delta_threshold");
            lastFuelTrendLap = six[5].LapNumber;
            return "In den letzten drei sauberen Runden brauchen wir im Median " +
                diff.ToString("0.00", German) + " Liter mehr pro Runde als in den drei davor. " +
                "Ich beobachte, ob sich das bestätigt – die Ursache ist noch unklar.";
        }

        /// <summary>Bestätigter Abstandstrend desselben Hintermanns; keine Überholprognose.</summary>
        public string MeasuredOpponentApproaching(string opponentKey, int completedLap, float gap)
        {
            if (string.IsNullOrWhiteSpace(opponentKey) || completedLap <= 0 ||
                float.IsNaN(gap) || float.IsInfinity(gap) || gap <= 0 || gap > 90)
            {
                trackedBehindKey = null;
                measuredBehindGaps.Clear();
                return LiveDiagnostics.NoTrend("opponent_behind","invalid_measurement_or_identity");
            }
            if (!string.Equals(opponentKey, trackedBehindKey, StringComparison.Ordinal))
            {
                trackedBehindKey = opponentKey;
                measuredBehindGaps.Clear();
            }
            if (measuredBehindGaps.Count > 0 && measuredBehindGaps.Last().Item1 == completedLap) return LiveDiagnostics.NoTrend("opponent_behind","same_lap");
            if (measuredBehindGaps.Count > 0 && measuredBehindGaps.Last().Item1 + 1 != completedLap) measuredBehindGaps.Clear();
            measuredBehindGaps.Enqueue(Tuple.Create(completedLap, gap));
            while (measuredBehindGaps.Count > 3) measuredBehindGaps.Dequeue();
            if (measuredBehindGaps.Count < 3 || lastBehindTrendLap == completedLap) return LiveDiagnostics.NoTrend("opponent_behind","window_or_already_reported");
            var points = measuredBehindGaps.ToArray();
            float gain = points[0].Item2 - gap;
            // Kontinuierliches Aufholen, kein einzelner Sprung oder Positionswechsel.
            if (gain < 0.8f || points[1].Item2 >= points[0].Item2 || gap >= points[1].Item2 || gap < 2.5f) return LiveDiagnostics.NoTrend("opponent_behind","not_continuous_threshold_or_close_traffic");
            lastBehindTrendLap = completedLap;
            return "Derselbe Hintermann hat über zwei Runden " + gain.ToString("0.0", German) +
                " Sekunden aufgeholt. Der gemessene Abstand liegt jetzt bei " + gap.ToString("0.0", German) + " Sekunden.";
        }

        private static bool Contiguous(IReadOnlyList<LapRecord> laps)
        {
            for (int i = 1; i < laps.Count; i++)
            {
                if (laps[i].LapNumber != laps[i - 1].LapNumber + 1) return false;
            }
            return true;
        }

        private static float Median(IEnumerable<float> values)
        {
            var v = values.OrderBy(x => x).ToArray();
            return v.Length % 2 == 0 ? (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2 : v[v.Length / 2];
        }
    }
}
