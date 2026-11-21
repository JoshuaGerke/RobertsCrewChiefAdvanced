using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>Zustand eines Wagens im Lagebild.</summary>
    internal enum CarMotion { Unknown, Normal, Slow, Stopped, Spun, InPit, Finished, Retired }

    /// <summary>Ein Wagen im Lagebild. Unveränderlich, darf zwischen Fäden geteilt werden.</summary>
    internal sealed class FieldCarView
    {
        public int Slot, ClassId, Place, PlaceClass, CompletedLaps;
        public string Name;
        public bool IsPlayer, InPit;
        public float LapDistance, Speed;
        /// <summary>Tempo, das das Feld an genau dieser Stelle normalerweise fährt (NaN = noch nicht gelernt).</summary>
        public float ExpectedSpeed = float.NaN;
        public CarMotion Motion;
        public DateTime MotionSinceUtc;
        /// <summary>Auf der Strecke relativ zu Rob: positiv = vor ihm, negativ = hinter ihm (höchstens eine halbe Runde).</summary>
        public float GapM = float.NaN, GapS = float.NaN;
        /// <summary>Runden Unterschied im Rennen: +1 = eine Runde vor Rob, -1 = von Rob überrundet.</summary>
        public int LapDelta;
        public float TopSpeed = float.NaN, TopSpeedAtM = float.NaN;
        public float BestLap = float.NaN, BestS1 = float.NaN, BestS2 = float.NaN, BestS3 = float.NaN;
        public float LastLap = float.NaN;
        public bool LastLapValid;
        /// <summary>Schnellste Runde dieser Session, auch gestrichen (NaN = noch keine).</summary>
        public float FastestAnyLap = float.NaN;
        public int LapsTimed;
    }

    /// <summary>Unveränderlicher Schnappschuss des Lagebilds.</summary>
    internal sealed class FieldView
    {
        public DateTime Utc;
        public string Sim, Track, Layout;
        public float TrackLength;
        public FieldSession Session;
        public FieldPhase Phase;
        public bool TimedSession, LapsSession, InMenus, Paused, InReplay, PlayerInGarage, LayoutVerified;
        public float TimeRemaining, TimeDuration;
        public float AirTempC = float.NaN, TrackTempC = float.NaN;
        public int SessionLaps, NumCars, MaxPlace, PositionsToGiveBack;
        /// <summary>Offene Slow-down-Zeit des Spielers in Sekunden (RaceRoom: Zeit zum Zurückgeben), -1 = keine.</summary>
        public float PlayerSlowDown = -1;
        public int[] RaceMinutes, RaceLaps;
        public FieldUpcoming[] Upcoming = new FieldUpcoming[0];
        public IReadOnlyList<FieldCarView> Cars;
        public FieldCarView Player;
        /// <summary>Runden, die Rob noch fährt, inklusive der laufenden; -1 = unbekannt, 0 = fertig.</summary>
        public int PlayerLapsLeft = -1;
        public bool PlayerOnLastLap => PlayerLapsLeft == 1;
        /// <summary>Anteil der Strecke, für den das Feldtempo schon gelernt ist (0–1).</summary>
        public float ProfileCoverage;

        public bool Fresh(DateTime now, double seconds = 1.0) => now >= Utc && (now - Utc).TotalSeconds <= seconds;

        /// <summary>Die nächsten Wagen vor Rob auf der Strecke (ohne Box), nächster zuerst.</summary>
        public IEnumerable<FieldCarView> Ahead(int n) => Cars.Where(c => !c.IsPlayer && !c.InPit && c.GapS > 0 && !float.IsNaN(c.GapS))
            .OrderBy(c => c.GapS).Take(n);
        /// <summary>Die nächsten Wagen hinter Rob auf der Strecke (ohne Box), nächster zuerst.</summary>
        public IEnumerable<FieldCarView> Behind(int n) => Cars.Where(c => !c.IsPlayer && !c.InPit && c.GapS <= 0 && !float.IsNaN(c.GapS))
            .OrderByDescending(c => c.GapS).Take(n);
        public FieldCarView ByName(string name) => string.IsNullOrWhiteSpace(name) ? null :
            Cars.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Das Lagebild (Rob 21.11.2026: „Die KI weiß nicht in Echtzeit, wo sich jeder Fahrer auf der Strecke befindet“).
    /// Rechnet aus den Rohbildern der Sim selbst – nicht aus Crew Chiefs Spielzustand:
    /// <list type="bullet">
    /// <item>Feldtempo je 10 m der Strecke, gelernt aus allen Wagen. Ein Wagen ist nur dann langsam oder gedreht, wenn er
    /// deutlich von dem abweicht, was das Feld an genau dieser Stelle fährt (Monza 21.11.: Bremsen vor der ersten
    /// Schikane galt als „Dreher“ bei zehn Fahrern).</item>
    /// <item>Fahrtrichtung gegen die gelernte Richtung der Strecke: quer oder rückwärts = Dreher.</item>
    /// <item>Abstand auf der Strecke in Sekunden über das Feldtempo (wie eine Relativ-Anzeige), mit Rundenunterschied.</item>
    /// <item>Höchsttempo und Ort, beste Sektoren, letzte und schnellste Runde je Fahrer – auch gestrichene Runden.</item>
    /// <item>Restrunden für Rob nach Uhr bzw. Rundenzahl, mit der Regel „Uhr bei null, die laufende Runde ist die letzte“.</item>
    /// </list>
    /// </summary>
    internal sealed class FieldPicture
    {
        internal const float CellM = 10f;
        internal const int ProfileMin = 3;
        internal static readonly FieldPicture Shared = new FieldPicture();

        private sealed class CarState
        {
            public string Name;
            public float LastX = float.NaN, LastZ = float.NaN, DirX, DirZ, LastDist = float.NaN;
            public bool HasDir;
            public DateTime SlowSince, StopSince, SpinSince, NormalSince;
            public CarMotion Motion = CarMotion.Unknown;
            public DateTime MotionSince;
            public float Top = float.NaN, TopAt = float.NaN;
            public int LastLaps = -1;
            public float LastCurrent = float.NaN;
            public bool LastValid = true;
            public float LastLap = float.NaN, FastestAny = float.NaN;
            public bool LastLapValid;
            public readonly List<float> Laps = new List<float>();
        }

        private readonly object gate = new object();
        private float[] speed = new float[0], dirX = new float[0], dirZ = new float[0];
        private int[] count = new int[0];
        private string profileKey, sessionKey;
        private float length = float.NaN;
        private readonly Dictionary<int, CarState> cars = new Dictionary<int, CarState>();
        private int lapsAtTimeZero = -1;
        private FieldView view;

        internal FieldView Current => Volatile.Read(ref view);

        internal void Reset()
        {
            lock (gate)
            {
                speed = new float[0]; dirX = new float[0]; dirZ = new float[0]; count = new int[0];
                profileKey = null; sessionKey = null; length = float.NaN; cars.Clear(); lapsAtTimeZero = -1;
                Volatile.Write(ref view, null);
            }
        }

        internal FieldView Observe(FieldFrame f)
        {
            if (f == null) return null;
            lock (gate)
            {
                PrepareTrack(f);
                string sk = f.Sim + "|" + f.Track + "|" + f.Layout + "|" + f.RawSession + "|" + f.SessionIteration;
                if (sk != sessionKey) { sessionKey = sk; cars.Clear(); lapsAtTimeZero = -1; }
                bool racing = f.Phase == FieldPhase.Green || f.Phase == FieldPhase.Checkered;
                var result = new List<FieldCarView>(f.Cars.Length);
                FieldCarView player = null;
                foreach (var c in f.Cars)
                {
                    if (c == null || c.Slot < 0) continue;
                    var v = Track(c, f, racing);
                    result.Add(v);
                    if (c.IsPlayer) player = v;
                }
                if (player != null) Relate(player, result);
                var built = new FieldView
                {
                    Utc = f.Utc, Sim = f.Sim, Track = f.Track, Layout = f.Layout, TrackLength = length, Session = f.Session, Phase = f.Phase,
                    TimedSession = f.TimedSession, LapsSession = f.LapsSession, InMenus = f.InMenus, Paused = f.Paused, InReplay = f.InReplay,
                    PlayerInGarage = f.PlayerInGarage, LayoutVerified = f.LayoutVerified, TimeRemaining = f.TimeRemaining,
                    TimeDuration = f.TimeDuration, SessionLaps = f.SessionLaps, NumCars = f.NumCars,
                    MaxPlace = result.Count == 0 ? 0 : result.Max(x => x.Place), PositionsToGiveBack = f.PositionsToGiveBack,
                    RaceMinutes = (int[])f.RaceMinutes.Clone(), RaceLaps = (int[])f.RaceLaps.Clone(),
                    Cars = result.AsReadOnly(), Player = player, ProfileCoverage = Coverage(),
                    PlayerSlowDown = f.Player != null && f.Player.SlowDown > 0 ? f.Player.SlowDown : -1,
                    AirTempC = f.AirTempC, TrackTempC = f.TrackTempC, Upcoming = f.Upcoming ?? new FieldUpcoming[0]
                };
                built.PlayerLapsLeft = LapsLeft(built);
                Volatile.Write(ref view, built);
                return built;
            }
        }

        private void PrepareTrack(FieldFrame f)
        {
            float len = f.TrackLength;
            if (float.IsNaN(len) || len < 200 || len > 100000)
                len = Math.Max(float.IsNaN(length) ? 0 : length, f.Cars.Where(c => c != null && !float.IsNaN(c.LapDistance)).Select(c => c.LapDistance).DefaultIfEmpty(0).Max());
            string key = f.Sim + "|" + f.Track + "|" + f.Layout;
            if (key != profileKey || len > 0 && (float.IsNaN(length) || Math.Abs(len - length) > 50))
            {
                profileKey = key; length = len;
                int n = Math.Max(1, (int)Math.Ceiling(Math.Max(len, CellM) / CellM));
                speed = new float[n]; dirX = new float[n]; dirZ = new float[n]; count = new int[n];
            }
        }

        private int Cell(float d) => count.Length == 0 || float.IsNaN(d) ? -1 : Math.Max(0, Math.Min(count.Length - 1, (int)(d / CellM)));
        private float Expected(int cell) => cell >= 0 && count[cell] >= ProfileMin ? speed[cell] : float.NaN;
        private float Coverage() => count.Length == 0 ? 0 : count.Count(x => x >= ProfileMin) / (float)count.Length;

        private FieldCarView Track(FieldCarFrame c, FieldFrame f, bool racing)
        {
            DateTime now = f.Utc;
            CarState s;
            if (!cars.TryGetValue(c.Slot, out s) || s.Name != c.Name) { s = new CarState { Name = c.Name, MotionSince = now, NormalSince = now }; cars[c.Slot] = s; }
            int cell = Cell(c.LapDistance);
            float expected = Expected(cell);
            bool moved = false; float hx = 0, hz = 0;
            if (!float.IsNaN(c.X) && !float.IsNaN(s.LastX))
            {
                float dx = c.X - s.LastX, dz = c.Z - s.LastZ, len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len >= 0.3f && len < 80f) { moved = true; hx = dx / len; hz = dz / len; }
                else if (len >= 80f) { s.HasDir = false; } // Sprung (Box, Neustart): keine Richtung
            }
            if (!float.IsNaN(c.X) && (moved || float.IsNaN(s.LastX))) { s.LastX = c.X; s.LastZ = c.Z; }
            // Sims ohne Weltposition (iRacing): Richtung aus der Streckenposition – rückwärts = Dreher, vorwärts = Lernprobe.
            bool noWorld = float.IsNaN(c.X);
            float step = float.IsNaN(c.LapDistance) || float.IsNaN(s.LastDist) ? 0 : c.LapDistance - s.LastDist;
            bool progressed = noWorld && step >= .3f && step < 80, backwards = noWorld && step <= -.3f && step > -50;
            if (!float.IsNaN(c.LapDistance) && (!noWorld || Math.Abs(step) >= .3f || float.IsNaN(s.LastDist))) s.LastDist = c.LapDistance;
            if (moved) { s.DirX = hx; s.DirZ = hz; s.HasDir = true; }

            CarMotion m;
            float v = float.IsNaN(c.Speed) ? -1 : c.Speed;
            if (c.InPit) m = CarMotion.InPit;
            else if (c.FinishStatus == 1) m = CarMotion.Finished;
            else if (c.FinishStatus >= 2) m = CarMotion.Retired;
            else if (!racing || v < 0) { m = CarMotion.Normal; s.StopSince = s.SlowSince = s.SpinSince = default(DateTime); }
            else
            {
                bool stopCond = v < 2.5f;
                bool slowCond = !float.IsNaN(expected) && expected >= 20 && v < expected * .55f && expected - v > 12;
                bool spinCond = backwards && v > 2;
                if (s.HasDir && v > 2 && cell >= 0 && count[cell] >= ProfileMin)
                {
                    float rx = dirX[cell], rz = dirZ[cell], rl = (float)Math.Sqrt(rx * rx + rz * rz);
                    if (rl > .5f) spinCond = (s.DirX * rx + s.DirZ * rz) / rl < .34f; // mehr als ~70° quer zur Fahrtrichtung
                }
                s.StopSince = stopCond ? (s.StopSince == default(DateTime) ? now : s.StopSince) : default(DateTime);
                s.SlowSince = slowCond ? (s.SlowSince == default(DateTime) ? now : s.SlowSince) : default(DateTime);
                s.SpinSince = spinCond ? (s.SpinSince == default(DateTime) ? now : s.SpinSince) : default(DateTime);
                if (s.StopSince != default(DateTime) && now - s.StopSince >= TimeSpan.FromSeconds(1)) m = CarMotion.Stopped;
                else if (s.SpinSince != default(DateTime) && now - s.SpinSince >= TimeSpan.FromMilliseconds(200)) m = CarMotion.Spun;
                else if (s.SlowSince != default(DateTime) && now - s.SlowSince >= TimeSpan.FromMilliseconds(400)) m = CarMotion.Slow;
                else m = CarMotion.Normal;
                // Ein Dreher bleibt zwei Sekunden lang ein Dreher, auch wenn der Wagen schon wieder gerade steht.
                if (m == CarMotion.Normal && s.Motion == CarMotion.Spun && now - s.MotionSince < TimeSpan.FromSeconds(2)) m = CarMotion.Spun;
            }
            if (m != s.Motion) { s.Motion = m; s.MotionSince = now; }

            // Feldtempo nur aus Wagen lernen, die normal fahren.
            if (m == CarMotion.Normal && racing && cell >= 0 && v > 5 && (float.IsNaN(expected) || v >= expected * .6f))
            {
                speed[cell] = count[cell] == 0 ? v : speed[cell] * .8f + v * .2f;
                if (progressed && count[cell] < int.MaxValue) count[cell]++;
                if (moved)
                {
                    float px = count[cell] == 0 ? hx : dirX[cell] * .8f + hx * .2f, pz = count[cell] == 0 ? hz : dirZ[cell] * .8f + hz * .2f;
                    float pl = (float)Math.Sqrt(px * px + pz * pz);
                    if (pl > 1e-3f) { dirX[cell] = px / pl; dirZ[cell] = pz / pl; }
                    if (count[cell] < int.MaxValue) count[cell]++;
                }
                if (float.IsNaN(s.Top) || v > s.Top) { s.Top = v; s.TopAt = c.LapDistance; }
            }

            // Runde abgeschlossen: Zeit (auch gestrichen) merken.
            if (s.LastLaps >= 0 && c.CompletedLaps == s.LastLaps + 1)
            {
                float t = !float.IsNaN(c.LastLap) && c.LastLap > 0 ? c.LastLap : s.LastCurrent;
                if (!float.IsNaN(t) && t > 5 && t < 7200)
                {
                    s.LastLap = t; s.LastLapValid = s.LastValid; s.Laps.Add(t);
                    if (s.Laps.Count > 8) s.Laps.RemoveAt(0);
                    if (float.IsNaN(s.FastestAny) || t < s.FastestAny) s.FastestAny = t;
                }
            }
            s.LastLaps = c.CompletedLaps;
            if (!float.IsNaN(c.LapTimeCurrent) && c.LapTimeCurrent > 0) s.LastCurrent = c.LapTimeCurrent;
            s.LastValid = c.CurrentLapValid;

            var r = new FieldCarView
            {
                Slot = c.Slot, Name = c.Name, ClassId = c.ClassId, Place = c.Place, PlaceClass = c.PlaceClass, CompletedLaps = c.CompletedLaps,
                IsPlayer = c.IsPlayer, InPit = c.InPit, LapDistance = c.LapDistance, Speed = c.Speed, ExpectedSpeed = expected,
                Motion = m, MotionSinceUtc = s.MotionSince, TopSpeed = s.Top, TopSpeedAtM = s.TopAt, BestLap = c.BestLap,
                LastLap = s.LastLap, LastLapValid = s.LastLapValid, FastestAnyLap = s.FastestAny, LapsTimed = s.Laps.Count
            };
            if (!float.IsNaN(c.SplitBest1) || !float.IsNaN(c.SplitBest2) || !float.IsNaN(c.SplitBest3))
            {
                r.BestS1 = c.SplitBest1; r.BestS2 = c.SplitBest2; r.BestS3 = c.SplitBest3;
            }
            else
            {
                if (!float.IsNaN(c.BestS1) && c.BestS1 > 0) r.BestS1 = c.BestS1;
                if (!float.IsNaN(c.BestS12) && c.BestS12 > 0 && !float.IsNaN(r.BestS1)) r.BestS2 = c.BestS12 - c.BestS1;
                if (!float.IsNaN(c.BestLap) && c.BestLap > 0 && !float.IsNaN(c.BestS12) && c.BestS12 > 0) r.BestS3 = c.BestLap - c.BestS12;
            }
            return r;
        }

        /// <summary>Zeit, die das Feld von Streckenmeter a bis b (vorwärts, über die Ziellinie hinweg) braucht.</summary>
        internal float TravelSeconds(float a, float b)
        {
            if (float.IsNaN(a) || float.IsNaN(b) || count.Length == 0 || float.IsNaN(length) || length <= 0) return float.NaN;
            float dist = b - a; if (dist < 0) dist += length;
            float fallback = FallbackSpeed();
            double t = 0; float pos = a, left = dist;
            int guard = count.Length + 2;
            while (left > 1e-3f && guard-- > 0)
            {
                int cell = Cell(pos % length);
                float cellEnd = (cell + 1) * CellM;
                float step = Math.Min(left, Math.Max(.01f, cellEnd - pos % length));
                float sp = Expected(cell);
                t += step / (float.IsNaN(sp) || sp < 5 ? fallback : sp);
                pos += step; left -= step;
                if (pos >= length) pos -= length;
            }
            return (float)t;
        }

        private float FallbackSpeed()
        {
            float sum = 0; int n = 0;
            for (int i = 0; i < count.Length; i++) if (count[i] >= ProfileMin) { sum += speed[i]; n++; }
            return n > 0 ? Math.Max(10, sum / n) : 40f;
        }

        private float LapSeconds() => float.IsNaN(length) ? float.NaN : TravelSeconds(0, length - .01f);

        private void Relate(FieldCarView p, List<FieldCarView> all)
        {
            if (float.IsNaN(length) || length <= 0 || float.IsNaN(p.LapDistance)) return;
            double pProg = p.CompletedLaps + p.LapDistance / length;
            foreach (var c in all)
            {
                if (c.IsPlayer) { c.GapM = 0; c.GapS = 0; continue; }
                if (float.IsNaN(c.LapDistance)) continue;
                float d = c.LapDistance - p.LapDistance;
                if (d > length / 2) d -= length; else if (d <= -length / 2) d += length;
                c.GapM = d;
                c.GapS = d >= 0 ? TravelSeconds(p.LapDistance, c.LapDistance) : -TravelSeconds(c.LapDistance, p.LapDistance);
                double cProg = c.CompletedLaps + c.LapDistance / length;
                c.LapDelta = (int)Math.Round(cProg - pProg - d / length);
            }
        }

        private float PaceOf(FieldCarView c)
        {
            CarState s;
            if (cars.TryGetValue(c.Slot, out s) && s.Laps.Count > 0)
            {
                var recent = s.Laps.Skip(Math.Max(0, s.Laps.Count - 3)).OrderBy(x => x).ToList();
                return recent[recent.Count / 2];
            }
            if (!float.IsNaN(c.BestLap) && c.BestLap > 0) return c.BestLap;
            float lap = LapSeconds();
            return Coverage() > .5f ? lap : float.NaN;
        }

        private float ToLine(FieldCarView c, float pace)
        {
            if (float.IsNaN(c.LapDistance) || float.IsNaN(length) || length <= 0) return float.NaN;
            return pace * Math.Max(0, 1 - c.LapDistance / length);
        }

        /// <summary>
        /// Restrunden für Rob inklusive der laufenden. Zeitsession: Fällt die Uhr auf null, ist die laufende Runde die letzte
        /// (im Rennen: die, in der Rob nach dem Führenden die Linie kreuzt). Rennen nach Runden: bis der Führende fertig ist.
        /// </summary>
        private int LapsLeft(FieldView v)
        {
            var p = v.Player;
            if (p == null || float.IsNaN(length)) return -1;
            if (p.Motion == CarMotion.Finished) return 0;
            bool race = v.Session == FieldSession.Race;
            if (v.TimedSession && !float.IsNaN(v.TimeRemaining) && v.TimeRemaining <= 0)
            {
                if (lapsAtTimeZero < 0) lapsAtTimeZero = p.CompletedLaps;
                if (p.CompletedLaps > lapsAtTimeZero && !race) return 0;
            }
            else lapsAtTimeZero = -1;
            float pp = PaceOf(p);
            if (float.IsNaN(pp) || pp <= 0) return -1;
            float tp = ToLine(p, pp);
            if (float.IsNaN(tp)) return -1;
            double finishAt;
            if (race)
            {
                var leader = v.Cars.Where(c => c.Place == 1).FirstOrDefault();
                if (leader == null) return -1;
                if (leader.Motion == CarMotion.Finished) return 1;
                float lp = leader.IsPlayer ? pp : PaceOf(leader);
                if (float.IsNaN(lp) || lp <= 0) return -1;
                float tl = ToLine(leader, lp);
                if (v.TimedSession && !float.IsNaN(v.TimeRemaining))
                {
                    int k = v.TimeRemaining <= tl ? 0 : (int)Math.Ceiling((v.TimeRemaining - tl) / lp);
                    finishAt = tl + k * lp;
                }
                else if (v.LapsSession && v.SessionLaps > 0)
                {
                    int remaining = v.SessionLaps - leader.CompletedLaps; // inkl. laufender Runde
                    if (remaining <= 0) return 1;
                    finishAt = tl + (remaining - 1) * lp;
                }
                else return -1;
                if (leader.IsPlayer) return (int)Math.Round((finishAt - tp) / pp) + 1;
                // Rob kreuzt nach dem Führenden: erste Linie bei oder nach finishAt.
                int j = finishAt <= tp ? 0 : (int)Math.Ceiling((finishAt - tp) / pp - 1e-6);
                return j + 1;
            }
            if (v.TimedSession && !float.IsNaN(v.TimeRemaining))
            {
                if (v.TimeRemaining <= 0) return 1;
                int j = v.TimeRemaining <= tp ? 0 : (int)Math.Ceiling((v.TimeRemaining - tp) / pp - 1e-6);
                return j + 1;
            }
            if (v.LapsSession && v.SessionLaps > 0) return Math.Max(0, v.SessionLaps - p.CompletedLaps);
            return -1;
        }
    }
}
