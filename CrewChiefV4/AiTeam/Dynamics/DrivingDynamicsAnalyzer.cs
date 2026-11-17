using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Untergrund unter einem Rad, über alle Sims vereinheitlicht.</summary>
    internal enum WheelSurface : byte { Unknown, Road, Kerb, Off, Wall }

    /// <summary>
    /// Ein Datenbild der schnellen Abtastung (Rob 17.11.2026: 100 Hz). Wird je Quelle wiederverwendet, damit bei hundert
    /// Bildern pro Sekunde kein Speicher anfällt. Räder: 0 vorne links, 1 vorne rechts, 2 hinten links, 3 hinten rechts.
    /// </summary>
    internal sealed class DynamicsFrame
    {
        public double TimeS;
        public float Speed, Throttle, Brake, Steering, LatAccel, LongAccel, LapDistance = -1;
        public int Gear, CompletedLaps;
        public bool Driving, InPit, AbsActive;
        /// <summary>Die Sim meldet keinen ABS-Eingriff (LMU): Blockierer müssen dann länger und deutlicher sein.</summary>
        public bool AbsUnknown;
        public readonly float[] WheelRotation = new float[4];
        public readonly WheelSurface[] Surfaces = new WheelSurface[4];
    }

    /// <summary>
    /// Fahrdynamik-Auswertung des Haupt-Ingenieurs (Rob 17.11.2026). Aus den schnellen Bildern erkennt sie, was ein
    /// Renningenieur in der Datenaufzeichnung sieht: blockierende Räder beim Anbremsen, durchdrehende Räder am Ausgang und
    /// Ausritte mit zwei oder mehr Rädern neben der Strecke – jeweils mit Ort (Kurve) und Runde. Grundlage sind nur
    /// gemessene Werte. Die Radgeschwindigkeit wird je Rad gegen das Fahrzeugtempo geeicht (lernt beim Rollen geradeaus),
    /// dadurch passt die Rechnung ohne Reifendaten in jeder Sim.
    /// </summary>
    internal sealed class DrivingDynamicsAnalyzer
    {
        internal enum Kind { Lockup, HeavyLockup, Wheelspin, OffTrack }

        internal sealed class Event
        {
            public Kind Kind;
            public int Lap, Wheel = -1, Wheels;
            public float Distance, Duration, Ratio, SpeedKmh;
            public string Corner;
        }

        internal sealed class Pattern
        {
            public Kind Kind;
            public string Key, Corner;
            public float Distance;
            public int Wheel = -1, Laps, Wheels;
        }

        internal const float LockRatio = .80f, HeavyRatio = .35f, SpinRatio = 1.12f;
        internal const double LockMin = .05, LockMinWithoutAbsFlag = .15, HeavyMin = .30, SpinMin = .15, OffMin = .20, OffRecover = .30;
        internal const int CalibrationSamples = 30;

        private readonly object gate = new object();
        private readonly float[] scale = new float[4];
        private readonly int[] scaleSamples = new int[4];
        private readonly double[] lockSince = { double.NaN, double.NaN, double.NaN, double.NaN };
        private readonly double[] spinSince = { double.NaN, double.NaN, double.NaN, double.NaN };
        private readonly float[] lockExtreme = new float[4], spinExtreme = new float[4], lockDistance = new float[4], spinDistance = new float[4], lockSpeed = new float[4];
        private double offSince = double.NaN, offClear = double.NaN;
        private float offDistance, offSpeed;
        private int offWheels, lap = int.MinValue;
        private readonly List<Event> events = new List<Event>();
        private readonly Queue<Event> heavy = new Queue<Event>();
        private readonly Dictionary<string, int> reported = new Dictionary<string, int>(StringComparer.Ordinal);
        private double firstFrame = double.NaN, lastFrame = double.NaN;
        private long frames;
        private float lapPeakLat, lapPeakBrake, lastLapPeakLat, lastLapPeakBrake;

        /// <summary>Kurvenname zu einer Streckenposition (Meter ab Start/Ziel) und Art (1 Anbremsen, 2 Ausgang, 0 egal); null ohne Kurvendaten.</summary>
        internal Func<float, int, string> CornerAt;

        /// <summary>Quelle der Daten (Spielname), für Diagnose und Sprachfragen.</summary>
        internal string Source { get; set; }

        internal void Reset()
        {
            lock (gate)
            {
                for (int i = 0; i < 4; i++)
                {
                    scale[i] = 0; scaleSamples[i] = 0; lockSince[i] = spinSince[i] = double.NaN;
                }
                offSince = offClear = double.NaN; offWheels = 0; lap = int.MinValue;
                events.Clear(); heavy.Clear(); reported.Clear();
                firstFrame = lastFrame = double.NaN; frames = 0; lastFrameUtc = DateTime.MinValue;
                lapPeakLat = lapPeakBrake = lastLapPeakLat = lastLapPeakBrake = 0;
            }
        }

        /// <summary>Gemessene Bildrate der Quelle in Hertz (über die ganze Fahrt); 0 ohne Daten.</summary>
        internal double Hz
        {
            get { lock (gate) return frames > 10 && lastFrame > firstFrame ? (frames - 1) / (lastFrame - firstFrame) : 0; }
        }

        internal double LastFrameTime { get { lock (gate) return lastFrame; } }

        private DateTime lastFrameUtc = DateTime.MinValue;
        /// <summary>Wanduhrzeit des letzten ausgewerteten Bildes (ob die Abtastung gerade lebt).</summary>
        internal DateTime LastFrameUtc { get { lock (gate) return lastFrameUtc; } }

        internal bool Calibrated(int wheel) { lock (gate) return scaleSamples[wheel] >= CalibrationSamples; }

        /// <summary>Verhältnis Radumfangsgeschwindigkeit zu Fahrzeugtempo (1 = rollt frei, 0 = steht); NaN ungeeicht.</summary>
        private float Ratio(DynamicsFrame f, int i) =>
            scaleSamples[i] >= CalibrationSamples && f.Speed > 1 ? Math.Abs(f.WheelRotation[i]) * scale[i] / f.Speed : float.NaN;

        internal void Observe(DynamicsFrame f)
        {
            if (f == null) return;
            lock (gate)
            {
                if (!double.IsNaN(lastFrame) && f.TimeS <= lastFrame) return; // altes Bild
                if (double.IsNaN(firstFrame) || f.TimeS - lastFrame > 5) { firstFrame = f.TimeS; frames = 0; }
                lastFrame = f.TimeS; frames++; lastFrameUtc = DateTime.UtcNow;
                if (f.CompletedLaps != lap)
                {
                    if (lap != int.MinValue) { lastLapPeakLat = lapPeakLat; lastLapPeakBrake = lapPeakBrake; }
                    lapPeakLat = lapPeakBrake = 0;
                    lap = f.CompletedLaps;
                }
                if (!f.Driving || f.InPit || f.Speed < 1)
                {
                    for (int i = 0; i < 4; i++) lockSince[i] = spinSince[i] = double.NaN;
                    offSince = offClear = double.NaN;
                    return;
                }
                Learn(f);
                float lat = Math.Abs(f.LatAccel) / 9.81f;
                if (lat < 6 && lat > lapPeakLat) lapPeakLat = lat;
                if (f.Brake > .5f) { float b = Math.Abs(f.LongAccel) / 9.81f; if (b < 6 && b > lapPeakBrake) lapPeakBrake = b; }
                for (int i = 0; i < 4; i++)
                {
                    float r = Ratio(f, i);
                    if (float.IsNaN(r)) continue;
                    Track(f, i, r);
                }
                TrackOff(f);
            }
        }

        private void Learn(DynamicsFrame f)
        {
            // Nur frei rollend: zügig, ohne Bremse, wenig Gas, geradeaus, kaum Längsbeschleunigung.
            if (f.Speed < 15 || f.Brake > .02f || f.Throttle > .9f || Math.Abs(f.LatAccel) > 3f || Math.Abs(f.LongAccel) > 2f) return;
            for (int i = 0; i < 4; i++)
            {
                float rot = Math.Abs(f.WheelRotation[i]);
                if (rot < .1f || f.Surfaces[i] == WheelSurface.Off) continue;
                float k = f.Speed / rot;
                if (scaleSamples[i] == 0) scale[i] = k;
                else if (Math.Abs(k - scale[i]) / scale[i] < .15f) scale[i] += (k - scale[i]) * (scaleSamples[i] < CalibrationSamples ? .2f : .02f);
                else continue; // Ausreißer (Schlupf) nicht lernen
                if (scaleSamples[i] < int.MaxValue) scaleSamples[i]++;
            }
        }

        private void Track(DynamicsFrame f, int i, float r)
        {
            bool locking = f.Brake >= .15f && f.Speed >= 12 && r < LockRatio && !f.AbsActive;
            if (locking)
            {
                if (double.IsNaN(lockSince[i])) { lockSince[i] = f.TimeS; lockExtreme[i] = r; lockDistance[i] = f.LapDistance; lockSpeed[i] = f.Speed; }
                else if (r < lockExtreme[i]) lockExtreme[i] = r;
            }
            else if (!double.IsNaN(lockSince[i]))
            {
                double d = f.TimeS - lockSince[i];
                lockSince[i] = double.NaN;
                if (d >= (f.AbsUnknown ? LockMinWithoutAbsFlag : LockMin) && (!f.AbsUnknown || lockExtreme[i] < .65f))
                {
                    bool hard = lockExtreme[i] < HeavyRatio && d >= HeavyMin;
                    var e = Add(hard ? Kind.HeavyLockup : Kind.Lockup, i, 0, lockDistance[i], (float)d, lockExtreme[i], lockSpeed[i]);
                    if (hard) { heavy.Enqueue(e); while (heavy.Count > 5) heavy.Dequeue(); }
                }
            }
            bool spinning = f.Throttle >= .5f && f.Speed >= 8 && r > SpinRatio;
            if (spinning)
            {
                if (double.IsNaN(spinSince[i])) { spinSince[i] = f.TimeS; spinExtreme[i] = r; spinDistance[i] = f.LapDistance; }
                else if (r > spinExtreme[i]) spinExtreme[i] = r;
            }
            else if (!double.IsNaN(spinSince[i]))
            {
                double d = f.TimeS - spinSince[i];
                spinSince[i] = double.NaN;
                if (d >= SpinMin) Add(Kind.Wheelspin, i, 0, spinDistance[i], (float)d, spinExtreme[i], f.Speed);
            }
        }

        private void TrackOff(DynamicsFrame f)
        {
            int off = f.Surfaces.Count(s => s == WheelSurface.Off || s == WheelSurface.Wall);
            if (off >= 2 && f.Speed >= 5)
            {
                if (double.IsNaN(offSince)) { offSince = f.TimeS; offDistance = f.LapDistance; offWheels = off; offSpeed = f.Speed; }
                else if (off > offWheels) offWheels = off;
                offClear = double.NaN;
                return;
            }
            if (double.IsNaN(offSince)) return;
            if (double.IsNaN(offClear)) { offClear = f.TimeS; return; }
            if (f.TimeS - offClear < OffRecover) return;
            double d = offClear - offSince;
            if (d >= OffMin) Add(Kind.OffTrack, -1, offWheels, offDistance, (float)d, 0, offSpeed);
            offSince = offClear = double.NaN;
        }

        private Event Add(Kind kind, int wheel, int wheels, float distance, float duration, float ratio, float speed)
        {
            string corner = null;
            int mode = kind == Kind.Wheelspin ? 2 : kind == Kind.OffTrack ? 0 : 1;
            try { corner = distance >= 0 ? CornerAt?.Invoke(distance, mode) : null; } catch (Exception) { }
            var e = new Event { Kind = kind, Lap = lap, Wheel = wheel, Wheels = wheels, Distance = distance, Duration = duration, Ratio = ratio, SpeedKmh = speed * 3.6f, Corner = corner };
            events.Add(e);
            if (events.Count > 400) events.RemoveRange(0, events.Count - 400);
            LiveDiagnostics.Decision("dynamics-" + kind.ToString().ToLowerInvariant(), "measured",
                (Source ?? "sim") + "_wheel_" + wheel + "_wheels_" + wheels + "_" + duration.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "s",
                (corner ?? "") + " " + distance.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "m");
            return e;
        }

        /// <summary>Ort als Schlüssel: Kurvenname oder 100-m-Abschnitt.</summary>
        private static string Place(Event e) => e.Corner ?? ("m" + (int)Math.Round(e.Distance / 100f));

        private static string Key(Event e) =>
            e.Kind == Kind.Wheelspin ? "spin|" + Place(e) + "|" + (e.Wheel < 2 ? "front" : "rear")
            : e.Kind == Kind.OffTrack ? "off|" + Place(e)
            : "lock|" + Place(e) + "|" + e.Wheel;

        /// <summary>Starke Blockierer seit dem letzten Abruf (für die Sofort-Meldung „möglicher Bremsplatten“).</summary>
        internal Event TakeHeavy()
        {
            lock (gate) return heavy.Count > 0 ? heavy.Dequeue() : null;
        }

        /// <summary>
        /// Wiederkehrendes Muster der letzten drei Runden – gleiche Art, gleicher Ort, in mindestens zwei Runden, davon eine
        /// die gerade beendete. Jedes Muster höchstens alle sechs Runden. <paramref name="completedLaps"/> = beendete Runden.
        /// </summary>
        internal Pattern FindPattern(int completedLaps)
        {
            lock (gate)
            {
                int newest = completedLaps - 1, oldest = completedLaps - 3;
                var recent = events.Where(e => e.Lap >= oldest && e.Lap <= newest && e.Kind != Kind.HeavyLockup || e.Kind == Kind.HeavyLockup && e.Lap >= oldest && e.Lap <= newest)
                    .Select(e => e.Kind == Kind.HeavyLockup ? new Event { Kind = Kind.Lockup, Lap = e.Lap, Wheel = e.Wheel, Distance = e.Distance, Corner = e.Corner } : e).ToList();
                Pattern best = null;
                foreach (var group in recent.GroupBy(Key))
                {
                    var laps = group.Select(e => e.Lap).Distinct().ToList();
                    if (laps.Count < 2 || !laps.Contains(newest)) continue;
                    if (reported.TryGetValue(group.Key, out int when) && completedLaps - when < 6) continue;
                    var first = group.Last();
                    var p = new Pattern { Kind = first.Kind, Key = group.Key, Corner = first.Corner, Distance = first.Distance, Wheel = first.Wheel, Laps = laps.Count, Wheels = group.Max(e => e.Wheels) };
                    if (best == null || Rank(p) > Rank(best)) best = p;
                }
                if (best != null) reported[best.Key] = completedLaps;
                return best;
            }
        }

        private static int Rank(Pattern p) => (p.Kind == Kind.OffTrack ? 30 : p.Kind == Kind.Lockup ? 20 : 10) + p.Laps;

        /// <summary>Lagebild der Fahrdynamik für Sprachfragen und Diagnose; null ohne Daten.</summary>
        internal JObject Snapshot(int completedLaps)
        {
            lock (gate)
            {
                if (frames < 10) return null;
                var recent = events.Where(e => e.Lap >= completedLaps - 5).ToList();
                var o = new JObject
                {
                    ["quelle"] = Source, ["abtastung_hz"] = Math.Round(frames > 10 && lastFrame > firstFrame ? (frames - 1) / (lastFrame - firstFrame) : 0),
                    ["raeder_geeicht"] = scaleSamples.Count(n => n >= CalibrationSamples),
                    ["blockierer_letzte_5_runden"] = recent.Count(e => e.Kind == Kind.Lockup || e.Kind == Kind.HeavyLockup),
                    ["starke_blockierer_letzte_5_runden"] = recent.Count(e => e.Kind == Kind.HeavyLockup),
                    ["durchdrehen_letzte_5_runden"] = recent.Count(e => e.Kind == Kind.Wheelspin),
                    ["ausritte_letzte_5_runden"] = recent.Count(e => e.Kind == Kind.OffTrack),
                    ["max_querbeschleunigung_g_letzte_runde"] = lastLapPeakLat > 0 ? (JToken)Math.Round(lastLapPeakLat, 2) : null,
                    ["max_bremsverzoegerung_g_letzte_runde"] = lastLapPeakBrake > 0 ? (JToken)Math.Round(lastLapPeakBrake, 2) : null
                };
                var spots = recent.GroupBy(e => Place(e) + " · " + Label(e.Kind)).Select(g => new { g.Key, n = g.Count(), laps = g.Select(e => e.Lap).Distinct().Count() })
                    .OrderByDescending(x => x.laps).ThenByDescending(x => x.n).Take(5)
                    .Select(x => new JObject { ["ort_art"] = x.Key, ["anzahl"] = x.n, ["runden"] = x.laps });
                o["problemstellen"] = new JArray(spots);
                o["hinweis"] = "Gemessen aus der schnellen Abtastung: Radgeschwindigkeit gegen Fahrzeugtempo und Untergrund je Rad. Ort ist die Crew-Chief-Kurve oder der 100-m-Abschnitt (m12 = bei 1200 m).";
                return o;
            }
        }

        private static string Label(Kind k) =>
            k == Kind.Wheelspin ? "durchdrehen" : k == Kind.OffTrack ? "ausritt" : k == Kind.HeavyLockup ? "starker_blockierer" : "blockierer";

        internal int EventCount { get { lock (gate) return events.Count; } }
    }
}
