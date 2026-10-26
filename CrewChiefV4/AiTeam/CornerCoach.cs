using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Eine Kurve aus den Crew-Chief-Streckendaten (TrackLandmark), Distanzen in Metern ab Start/Ziel.</summary>
    public class CornerDefinition
    {
        public string Name;
        public float Start;
        public float End;
    }

    /// <summary>Messwerte einer Kurve in einer Runde.</summary>
    public class CornerPass
    {
        public float BrakePointBeforeEntryM = float.NaN;  // positiv = so viele Meter vor Kurveneingang gebremst
        public float MinSpeedKmh = float.NaN;
        public float ExitSpeedKmh = float.NaN;
        public float FullThrottleAfterEntryM = float.NaN;
        public float TimeS = float.NaN;                    // von 100 m vor bis 100 m nach der Kurve
        public int MinGear;                                // kleinster Vorwärtsgang in der Kurve, 0 = unbekannt
        public bool Complete { get { return !float.IsNaN(TimeS) && !float.IsNaN(MinSpeedKmh); } }
    }

    public class CornerLap
    {
        public int LapNumber;
        public float LapTime;
        public bool Valid;
        public Dictionary<int, CornerPass> Corners = new Dictionary<int, CornerPass>();
    }

    /// <summary>
    /// Kurven-Coaching für Ben. Zeichnet pro Runde und Kurve Bremspunkt, Scheiteltempo, Ausgangstempo,
    /// Vollgaspunkt und Kurvenzeit auf und vergleicht die letzte Runde mit der eigenen Bestrunde.
    /// Grundlage sind nur Crew-Chief-Telemetrie (Position, Tempo, Pedale) und die Crew-Chief-Kurvendaten.
    /// Auflösung: ein Messpunkt pro Telemetrie-Tick, also je nach Tempo etwa 3 bis 8 Meter.
    /// </summary>
    public class CornerCoach
    {
        public const float WindowBeforeM = 250;   // so weit vor der Kurve wird nach dem Bremspunkt gesucht
        public const float TimingMarginM = 100;   // Kurvenzeit von 100 m davor bis 100 m danach
        public const float BrakeThreshold = 0.3f;
        public const float FullThrottle = 0.95f;
        public const int MaxLaps = 30;

        private readonly object sync = new object();
        private List<CornerDefinition> corners = new List<CornerDefinition>();
        private float trackLength;
        private readonly List<CornerLap> laps = new List<CornerLap>();
        private CornerLap currentLap = new CornerLap();
        private readonly Dictionary<int, CornerTracker> trackers = new Dictionary<int, CornerTracker>();
        private readonly Dictionary<int, int> lastHintLap = new Dictionary<int, int>();
        private bool pedalsSeen;

        private class CornerTracker
        {
            public CornerPass Pass = new CornerPass();
            public float EnterTime = float.NaN;
            public bool Braking;
            public float PrevBrake = 1f;   // beim Eintritt ins Fenster als "schon gebremst" werten
            public bool ApexPassed;
            public float LastRel = float.NaN;
        }

        public bool HasCorners { get { lock (sync) { return corners.Count > 0; } } }
        public bool PedalDataAvailable { get { return pedalsSeen; } }

        /// <summary>Strecke setzen (bei neuer Session). Gerade Abschnitte werden ignoriert.</summary>
        public void SetTrack(IEnumerable<CornerDefinition> definitions, float length)
        {
            lock (sync)
            {
                corners = (definitions ?? Enumerable.Empty<CornerDefinition>())
                    .Where(c => c.End > c.Start && c.Start >= 0).OrderBy(c => c.Start).ToList();
                trackLength = length;
                laps.Clear();
                trackers.Clear();
                lastHintLap.Clear();
                currentLap = new CornerLap();
                pedalsSeen = false;
            }
        }

        /// <summary>Ein Telemetrie-Tick. Pedale 0..1 (0..100 wird umgerechnet), Tempo in m/s, Zeit = Session-Laufzeit in s.</summary>
        public void Tick(float distanceRoundTrack, float speedMs, float throttle, float brake, float sessionTime, bool inPit, int gear = 0)
        {
            lock (sync)
            {
                if (corners.Count == 0 || trackLength <= 0 || inPit || distanceRoundTrack < 0)
                {
                    return;
                }
                if (throttle > 1.5f) throttle /= 100f;
                if (brake > 1.5f) brake /= 100f;
                if (throttle > 0.01f || brake > 0.01f)
                {
                    pedalsSeen = true;
                }
                float kmh = speedMs * 3.6f;
                for (int i = 0; i < corners.Count; i++)
                {
                    var c = corners[i];
                    float length = c.End - c.Start;
                    float rel = Wrap(distanceRoundTrack - c.Start);
                    if (rel < -WindowBeforeM || rel > length + TimingMarginM + 50)
                    {
                        trackers.Remove(i);
                        continue;
                    }
                    CornerTracker t;
                    if (!trackers.TryGetValue(i, out t))
                    {
                        t = new CornerTracker();
                        trackers[i] = t;
                    }
                    // Sprung rückwärts (Dreher, Reset): Messung verwerfen
                    if (!float.IsNaN(t.LastRel) && rel < t.LastRel - 20)
                    {
                        t = new CornerTracker();
                        trackers[i] = t;
                    }
                    t.LastRel = rel;

                    if (float.IsNaN(t.EnterTime) && rel >= -TimingMarginM)
                    {
                        t.EnterTime = sessionTime;
                    }
                    // nur ein neuer Tritt zählt, nicht Restbremsung aus der Kurve davor
                    if (brake >= BrakeThreshold && t.PrevBrake < BrakeThreshold && !t.Braking && float.IsNaN(t.Pass.BrakePointBeforeEntryM) && rel <= length / 2)
                    {
                        t.Pass.BrakePointBeforeEntryM = -rel;
                        t.Braking = true;
                    }
                    t.PrevBrake = brake;
                    if (rel >= 0 && rel <= length)
                    {
                        if (gear > 0 && (t.Pass.MinGear == 0 || gear < t.Pass.MinGear))
                        {
                            t.Pass.MinGear = gear;
                        }
                        if (float.IsNaN(t.Pass.MinSpeedKmh) || kmh < t.Pass.MinSpeedKmh)
                        {
                            t.Pass.MinSpeedKmh = kmh;
                        }
                        else if (kmh > t.Pass.MinSpeedKmh + 3)
                        {
                            t.ApexPassed = true;
                        }
                    }
                    if (t.ApexPassed && float.IsNaN(t.Pass.FullThrottleAfterEntryM) && throttle >= FullThrottle)
                    {
                        t.Pass.FullThrottleAfterEntryM = rel;
                    }
                    if (float.IsNaN(t.Pass.ExitSpeedKmh) && rel >= length)
                    {
                        t.Pass.ExitSpeedKmh = kmh;
                    }
                    if (rel >= length + TimingMarginM && float.IsNaN(t.Pass.TimeS))
                    {
                        if (!float.IsNaN(t.EnterTime) && sessionTime > t.EnterTime)
                        {
                            t.Pass.TimeS = sessionTime - t.EnterTime;
                            currentLap.Corners[i] = t.Pass;
                        }
                        trackers.Remove(i);
                    }
                }
            }
        }

        /// <summary>Rundenende: Messungen dieser Runde ablegen.</summary>
        public void CompleteLap(int lapNumber, float lapTime, bool valid)
        {
            lock (sync)
            {
                currentLap.LapNumber = lapNumber;
                currentLap.LapTime = lapTime;
                currentLap.Valid = valid && lapTime > 0;
                if (currentLap.Corners.Count > 0)
                {
                    laps.Add(currentLap);
                    if (laps.Count > MaxLaps)
                    {
                        laps.RemoveAt(0);
                    }
                }
                currentLap = new CornerLap();
            }
        }

        /// <summary>Ergebnis für das Werkzeug get_corner_analysis: letzte gültige Runde gegen die Bestrunde.</summary>
        public JObject Analyse()
        {
            lock (sync)
            {
                var o = new JObject();
                if (corners.Count == 0)
                {
                    o["verfuegbar"] = false;
                    o["grund"] = "Für diese Strecke hat Crew Chief keine Kurvendaten (Track Landmarks).";
                    return o;
                }
                var valid = laps.Where(l => l.Valid).ToList();
                if (valid.Count < 2)
                {
                    o["verfuegbar"] = false;
                    o["grund"] = "Noch zu wenige gültige, vollständig gemessene Runden (mindestens 2 nötig, bisher " + valid.Count + ").";
                    return o;
                }
                var best = valid.OrderBy(l => l.LapTime).First();
                var last = valid.Last();
                o["vergleich"] = last == best
                    ? "Letzte Runde ist die Bestrunde – Vergleich mit der zweitbesten gültigen Runde"
                    : "Letzte gültige Runde " + last.LapNumber + " gegen Bestrunde " + best.LapNumber;
                if (last == best)
                {
                    best = valid.Where(l => l != last).OrderBy(l => l.LapTime).First();
                }
                o["bestrunde_s"] = Math.Round(best.LapTime, 3);
                o["letzte_runde_s"] = Math.Round(last.LapTime, 3);
                o["pedaldaten"] = pedalsSeen;
                o["aufloesung"] = "ein Messpunkt pro Telemetrie-Tick, Meterangaben auf etwa ±5 m genau";
                o["vorzeichen"] = "zeit_s positiv = letzte Runde langsamer; bremspunkt_m positiv = früher gebremst als in der Bestrunde";
                var list = new JArray();
                foreach (var pair in Compare(last, best).OrderByDescending(p => p.TimeLossS))
                {
                    var c = new JObject
                    {
                        ["kurve"] = pair.Name,
                        ["zeit_s"] = Math.Round(pair.TimeLossS, 2),
                    };
                    if (!float.IsNaN(pair.BrakeEarlierM)) c["bremspunkt_m"] = Math.Round(pair.BrakeEarlierM);
                    if (!float.IsNaN(pair.MinSpeedDeltaKmh)) c["scheitel_kmh"] = Math.Round(pair.MinSpeedDeltaKmh, 1);
                    if (!float.IsNaN(pair.ExitSpeedDeltaKmh)) c["ausgang_kmh"] = Math.Round(pair.ExitSpeedDeltaKmh, 1);
                    if (!float.IsNaN(pair.ThrottleLaterM)) c["vollgas_spaeter_m"] = Math.Round(pair.ThrottleLaterM);
                    if (pair.Gear > 0) c["gang"] = pair.Gear;
                    if (pair.Gear > 0 && pair.BestGear > 0 && pair.Gear != pair.BestGear) c["gang_bestrunde"] = pair.BestGear;
                    list.Add(c);
                }
                o["kurven"] = list;
                o["hinweis"] = "scheitel_kmh/ausgang_kmh negativ = langsamer als in der Bestrunde; vollgas_spaeter_m positiv = später voll aufs Gas; " +
                    "gang = kleinster Gang in der Kurve, gang_bestrunde nur wenn er in der Bestrunde anders war";
                return o;
            }
        }

        /// <summary>
        /// Für proaktive Hinweise: eine Kurve, in der die letzten zwei gültigen Runden jeweils deutlich Zeit gegenüber
        /// der Bestrunde verloren haben. Pro Kurve höchstens alle 5 Runden. null = nichts Auffälliges.
        /// </summary>
        public string FindConsistentLoss(float minLossS = 0.2f)
        {
            lock (sync)
            {
                var valid = laps.Where(l => l.Valid).ToList();
                if (valid.Count < 3)
                {
                    return null;
                }
                var best = valid.OrderBy(l => l.LapTime).First();
                var recent = valid.Skip(valid.Count - 2).ToList();
                if (recent.Contains(best))
                {
                    return null;
                }
                var first = Compare(recent[0], best).ToDictionary(p => p.Index);
                foreach (var p in Compare(recent[1], best).OrderByDescending(x => x.TimeLossS))
                {
                    CornerDelta earlier;
                    int lastHint;
                    if (p.TimeLossS < minLossS || !first.TryGetValue(p.Index, out earlier) || earlier.TimeLossS < minLossS)
                    {
                        continue;
                    }
                    if (lastHintLap.TryGetValue(p.Index, out lastHint) && recent[1].LapNumber - lastHint < 5)
                    {
                        continue;
                    }
                    lastHintLap[p.Index] = recent[1].LapNumber;
                    var de = CultureInfo.GetCultureInfo("de-DE");
                    string text = p.Name + ": in den letzten zwei Runden jeweils " + earlier.TimeLossS.ToString("0.00", de) + " und " +
                        p.TimeLossS.ToString("0.00", de) + " Sekunden langsamer als in der Bestrunde";
                    if (!float.IsNaN(p.BrakeEarlierM) && Math.Abs(p.BrakeEarlierM) >= 8)
                    {
                        text += ", Bremspunkt " + Math.Abs(Math.Round(p.BrakeEarlierM)) + " m " + (p.BrakeEarlierM > 0 ? "früher" : "später");
                    }
                    if (!float.IsNaN(p.MinSpeedDeltaKmh) && Math.Abs(p.MinSpeedDeltaKmh) >= 3)
                    {
                        text += ", Scheiteltempo " + p.MinSpeedDeltaKmh.ToString("+0;-0", de) + " km/h";
                    }
                    if (!float.IsNaN(p.ExitSpeedDeltaKmh) && Math.Abs(p.ExitSpeedDeltaKmh) >= 3)
                    {
                        text += ", Ausgangstempo " + p.ExitSpeedDeltaKmh.ToString("+0;-0", de) + " km/h";
                    }
                    if (!float.IsNaN(p.ThrottleLaterM) && Math.Abs(p.ThrottleLaterM) >= 10)
                    {
                        text += ", Vollgas " + Math.Abs(Math.Round(p.ThrottleLaterM)) + " m " + (p.ThrottleLaterM > 0 ? "später" : "früher");
                    }
                    if (p.Gear > 0 && p.BestGear > 0 && p.Gear != p.BestGear)
                    {
                        text += ", im Scheitel " + p.Gear + ". Gang statt " + p.BestGear + ". Gang wie in der Bestrunde";
                    }
                    return text + ".";
                }
                return null;
            }
        }

        private class CornerDelta
        {
            public int Index;
            public string Name;
            public float TimeLossS;
            public float BrakeEarlierM = float.NaN;
            public float MinSpeedDeltaKmh = float.NaN;
            public float ExitSpeedDeltaKmh = float.NaN;
            public float ThrottleLaterM = float.NaN;
            public int Gear;
            public int BestGear;
        }

        private IEnumerable<CornerDelta> Compare(CornerLap lap, CornerLap reference)
        {
            foreach (var pair in lap.Corners)
            {
                CornerPass refPass;
                if (!pair.Value.Complete || !reference.Corners.TryGetValue(pair.Key, out refPass) || !refPass.Complete)
                {
                    continue;
                }
                var p = pair.Value;
                yield return new CornerDelta
                {
                    Index = pair.Key,
                    Name = corners[pair.Key].Name,
                    TimeLossS = p.TimeS - refPass.TimeS,
                    BrakeEarlierM = p.BrakePointBeforeEntryM - refPass.BrakePointBeforeEntryM,
                    MinSpeedDeltaKmh = p.MinSpeedKmh - refPass.MinSpeedKmh,
                    ExitSpeedDeltaKmh = p.ExitSpeedKmh - refPass.ExitSpeedKmh,
                    ThrottleLaterM = p.FullThrottleAfterEntryM - refPass.FullThrottleAfterEntryM,
                    Gear = p.MinGear,
                    BestGear = refPass.MinGear,
                };
            }
        }

        private float Wrap(float delta)
        {
            if (delta > trackLength / 2) return delta - trackLength;
            if (delta < -trackLength / 2) return delta + trackLength;
            return delta;
        }

        private static readonly Regex turnPattern = new Regex(@"^(turn|t|corner|kurve)[_ ]?(\d+)$", RegexOptions.IgnoreCase);

        /// <summary>Crew-Chief-Kurvennamen ("turn6", "la_source") funktauglich machen.</summary>
        public static string FriendlyName(string raw, int index)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "Kurve " + (index + 1);
            }
            var m = turnPattern.Match(raw.Trim());
            if (m.Success)
            {
                return "Kurve " + int.Parse(m.Groups[2].Value);
            }
            var words = raw.Replace('_', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
        }
    }

    /// <summary>
    /// Für Strecken ohne hinterlegte Kurven (Crew-Chief-Landmarken): eine Runde Tempo mitschreiben und die Kurven
    /// als deutliche Tempo-Täler erkennen. Grob, aber genug für Bremspunkt, Kurventempo und Zeitvergleich.
    /// </summary>
    public class CornerDetector
    {
        public const float BinM = 10f;
        public const float MinDropKmh = 20f;
        public const float MinDropRatio = 0.12f;

        private float[] minSpeed;
        private float length;

        public void Reset(float trackLength)
        {
            length = trackLength;
            minSpeed = trackLength > 500 ? Enumerable.Repeat(float.NaN, (int)(trackLength / BinM) + 1).ToArray() : null;
        }

        public void Add(float distanceRoundTrack, float kmh)
        {
            if (minSpeed == null || distanceRoundTrack < 0 || distanceRoundTrack >= length)
            {
                return;
            }
            int i = (int)(distanceRoundTrack / BinM);
            if (float.IsNaN(minSpeed[i]) || kmh < minSpeed[i])
            {
                minSpeed[i] = kmh;
            }
        }

        /// <summary>Anteil der Strecke mit Messwerten (0..1).</summary>
        public float Coverage
        {
            get { return minSpeed == null ? 0 : minSpeed.Count(v => !float.IsNaN(v)) / (float)minSpeed.Length; }
        }

        public List<CornerDefinition> Detect()
        {
            var result = new List<CornerDefinition>();
            if (minSpeed == null || Coverage < 0.9f)
            {
                return result;
            }
            int n = minSpeed.Length;
            // Lücken mit dem letzten Wert füllen, dann über ±20 m glätten
            var raw = (float[])minSpeed.Clone();
            float last = raw.FirstOrDefault(x => !float.IsNaN(x));
            for (int i = 0; i < n; i++)
            {
                if (float.IsNaN(raw[i])) raw[i] = last; else last = raw[i];
            }
            var v = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0;
                for (int k = -2; k <= 2; k++) sum += raw[(i + k + n) % n];
                v[i] = sum / 5;
            }
            var apexes = new List<int>();
            for (int i = 0; i < n; i++)
            {
                bool lowest = true;
                float before = 0;
                for (int k = 1; k <= 40 && lowest; k++)  // 100 m Umgebung muss höher sein, 400 m davor das Maximum suchen
                {
                    if (k <= 10 && (v[(i + k) % n] < v[i] || v[(i - k + n) % n] < v[i])) lowest = false;
                    before = Math.Max(before, v[(i - k + n) % n]);
                }
                float drop = before - v[i];
                if (lowest && drop >= MinDropKmh && drop >= before * MinDropRatio)
                {
                    if (apexes.Count > 0 && i - apexes[apexes.Count - 1] < 15)
                    {
                        if (v[i] < v[apexes[apexes.Count - 1]]) apexes[apexes.Count - 1] = i;
                        continue;
                    }
                    apexes.Add(i);
                }
            }
            foreach (int a in apexes)
            {
                float start = (a - 5) * BinM, end = (a + 5) * BinM;
                if (start < 0 || end > length)
                {
                    continue; // Kurve direkt über Start/Ziel: auslassen statt falsch messen
                }
                result.Add(new CornerDefinition { Name = CornerCoach.FriendlyName(null, result.Count), Start = start, End = end });
            }
            return result;
        }
    }
}
