using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json.Linq;
using rF2SharedMemory.LMUData;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Was LMU nur nativ liefert (Rob 20.11.2026: „Geh tiefer in die native Schnittstelle und hol alles raus“): ob die laufende Runde
    /// gestrichen ist, Tracklimit-Punkte und ab wann es eine Strafe gibt, die Abstände des Spiels nach vorn und hinten, virtuelle
    /// Energie, Fahrhilfen, und für jedes Auto Platz, Klasse, Runden, Abstände, Stopps, offene Strafen und Boxenstatus.
    /// Wird aus demselben Abbild wie die Fahrdynamik gefüllt (höchstens 4-mal je Sekunde).
    /// </summary>
    internal sealed class LMUNativeSnapshot
    {
        internal sealed class Car
        {
            public int Id, Place, Laps, Pitstops, Penalties, PitState, FinishStatus, CountLapFlag, LapsBehindLeader;
            public string Name, Class;
            public double BestLap, LastLap, BehindNext, BehindLeader;
            public bool Player, InPits;
        }

        public DateTime Utc;
        public bool LapInvalidated, SpeedLimiter, AbsActive, TcActive;
        public int TrackLimitsSteps, StepsPerPoint, StepsPerPenalty, Tc, TcMax, Abs, AbsMax, MotorMap, MotorMapMax, BoostState;
        public float VirtualEnergy, GapCarAhead, GapCarBehind, GapPlaceAhead, SoC;
        public Car[] Cars = new Car[0];

        private static LMUNativeSnapshot current;
        private static long lastTicks;
        internal static LMUNativeSnapshot Current => Volatile.Read(ref current);
        internal static void Reset() { Volatile.Write(ref current, null); Interlocked.Exchange(ref lastTicks, 0); }

        /// <summary>Noch frisch (höchstens 2 s alt)?</summary>
        internal static LMUNativeSnapshot Fresh(DateTime nowUtc)
        {
            var s = Current;
            return s != null && nowUtc >= s.Utc && nowUtc - s.Utc <= TimeSpan.FromSeconds(2) ? s : null;
        }

        private static int T(string f) => Marshal.OffsetOf(typeof(rF2VehicleTelemetry), f).ToInt32();
        private static int S(string f) => Marshal.OffsetOf(typeof(rF2VehicleScoring), f).ToInt32();
        private static int I(string f) => Marshal.OffsetOf(typeof(rF2ScoringInfo), f).ToInt32();
        private static readonly int tLapInvalidated = T("mLapInvalidated"), tLimiter = T("mSpeedLimiterActive"), tAbsActive = T("mABSActive"), tTcActive = T("mTCActive"),
            tTc = T("mTC"), tTcMax = T("mTCMax"), tAbs = T("mABS"), tAbsMax = T("mABSMax"), tMap = T("mMotorMap"), tMapMax = T("mMotorMapMax"),
            tSteps = T("mTrackLimitsSteps"), tEnergy = T("mVirtualEnergy"), tAhead = T("mTimeGapCarAhead"), tBehind = T("mTimeGapCarBehind"),
            tPlaceAhead = T("mTimeGapPlaceAhead"), tSoC = T("mStateOfCharge"), tBoost = T("mElectricBoostMotorState"), tId = T("mID");
        private static readonly int iPerPenalty = I("mTrackLimitsStepsPerPenalty"), iPerPoint = I("mTrackLimitsStepsPerPoint");
        private static readonly int sId = S("mID"), sName = S("mDriverName"), sLaps = S("mTotalLaps"), sFinish = S("mFinishStatus"), sBest = S("mBestLapTime"),
            sLast = S("mLastLapTime"), sStops = S("mNumPitstops"), sPen = S("mNumPenalties"), sPlayer = S("mIsPlayer"), sInPits = S("mInPits"),
            sPlace = S("mPlace"), sClass = S("mVehicleClass"), sBehindNext = S("mTimeBehindNext"), sBehindLeader = S("mTimeBehindLeader"),
            sLapsBehindLeader = S("mLapsBehindLeader"), sPitState = S("mPitState"), sCount = S("mCountLapFlag");

        /// <summary>Aus dem kopierten Abbild; null, wenn kein eigenes Auto im Bild ist. Für Tests ohne Drosselung.</summary>
        internal static LMUNativeSnapshot Parse(byte[] b, DateTime nowUtc)
        {
            if (b == null || b.Length < LMUNativeLayout.TotalSize || b[LMUNativeLayout.PlayerHasVehicleOffset] == 0) return null;
            int active = Math.Min((int)b[LMUNativeLayout.ActiveVehiclesOffset], LMUNativeLayout.MaxVehicles), index = b[LMUNativeLayout.PlayerIndexOffset];
            if (index >= active) return null;
            int v = LMUNativeLayout.TelemetryVehiclesOffset + index * LMUNativeLayout.VehicleTelemetrySize, info = LMUNativeLayout.ScoringOffset;
            var s = new LMUNativeSnapshot
            {
                Utc = nowUtc, LapInvalidated = b[v + tLapInvalidated] != 0, SpeedLimiter = b[v + tLimiter] != 0, AbsActive = b[v + tAbsActive] != 0, TcActive = b[v + tTcActive] != 0,
                Tc = b[v + tTc], TcMax = b[v + tTcMax], Abs = b[v + tAbs], AbsMax = b[v + tAbsMax], MotorMap = b[v + tMap], MotorMapMax = b[v + tMapMax],
                TrackLimitsSteps = b[v + tSteps], StepsPerPoint = b[info + iPerPoint], StepsPerPenalty = b[info + iPerPenalty], BoostState = b[v + tBoost],
                VirtualEnergy = BitConverter.ToSingle(b, v + tEnergy), GapCarAhead = BitConverter.ToSingle(b, v + tAhead), GapCarBehind = BitConverter.ToSingle(b, v + tBehind),
                GapPlaceAhead = BitConverter.ToSingle(b, v + tPlaceAhead), SoC = BitConverter.ToSingle(b, v + tSoC),
            };
            int scored = Math.Min(Math.Max(BitConverter.ToInt32(b, LMUNativeLayout.ScoringCountOffset), 0), LMUNativeLayout.MaxVehicles);
            var cars = new List<Car>(scored);
            for (int i = 0; i < scored; i++)
            {
                int o = LMUNativeLayout.ScoringVehiclesOffset + i * LMUNativeLayout.VehicleScoringSize;
                cars.Add(new Car
                {
                    Id = BitConverter.ToInt32(b, o + sId), Name = LMURaceControlStream.Decode(b, o + sName, 32), Class = LMURaceControlStream.Decode(b, o + sClass, 32),
                    Laps = BitConverter.ToInt16(b, o + sLaps), FinishStatus = (sbyte)b[o + sFinish], BestLap = BitConverter.ToDouble(b, o + sBest), LastLap = BitConverter.ToDouble(b, o + sLast),
                    Pitstops = BitConverter.ToInt16(b, o + sStops), Penalties = BitConverter.ToInt16(b, o + sPen), Player = b[o + sPlayer] != 0, InPits = b[o + sInPits] != 0,
                    Place = b[o + sPlace], BehindNext = BitConverter.ToDouble(b, o + sBehindNext), BehindLeader = BitConverter.ToDouble(b, o + sBehindLeader),
                    LapsBehindLeader = BitConverter.ToInt32(b, o + sLapsBehindLeader), PitState = b[o + sPitState], CountLapFlag = b[o + sCount],
                });
            }
            s.Cars = cars.OrderBy(c => c.Place <= 0 ? 999 : c.Place).ToArray();
            return s;
        }

        /// <summary>Aus der Fahrdynamik-Schleife: höchstens alle 250 ms ein neues Bild.</summary>
        internal static void Observe(byte[] b, DateTime nowUtc)
        {
            long last = Interlocked.Read(ref lastTicks);
            if (last != 0 && nowUtc.Ticks - last < TimeSpan.TicksPerMillisecond * 250 && nowUtc.Ticks >= last) return;
            Interlocked.Exchange(ref lastTicks, nowUtc.Ticks);
            var s = Parse(b, nowUtc);
            if (s != null) Volatile.Write(ref current, s);
        }

        internal Car PlayerCar => Cars.FirstOrDefault(c => c.Player);

        /// <summary>Tracklimit-Punkte (gerundet auf Zehntel), -1 = vom Spiel nicht geliefert.</summary>
        internal double LimitPoints => StepsPerPoint > 0 ? Math.Round((double)TrackLimitsSteps / StepsPerPoint, 1) : -1;
        internal double PointsPerPenalty => StepsPerPoint > 0 && StepsPerPenalty > 0 ? Math.Round((double)StepsPerPenalty / StepsPerPoint, 1) : -1;

        private static readonly string[] PitStates = { "keiner", "angemeldet", "einfahrt", "steht", "ausfahrt" };
        private static readonly string[] Finish = { "faehrt", "im_ziel", "ausgefallen", "disqualifiziert" };

        /// <summary>Für das Sprachmodell (Werkzeug get_lmu_native).</summary>
        internal JObject ToJson(DateTime nowUtc, IEnumerable<string> raceControl)
        {
            var me = PlayerCar;
            var o = new JObject
            {
                ["quelle"] = "LMU natives Abbild LMU_Data", ["alter_s"] = Math.Round((nowUtc - Utc).TotalSeconds, 1),
                ["laufende_runde_gestrichen"] = LapInvalidated,
                ["tracklimits"] = new JObject { ["punkte"] = LimitPoints, ["punkte_bis_strafe"] = PointsPerPenalty, ["schritte"] = TrackLimitsSteps,
                    ["schritte_je_punkt"] = StepsPerPoint, ["schritte_je_strafe"] = StepsPerPenalty },
                ["abstand_auto_vorn_s"] = Gap(GapCarAhead), ["abstand_auto_hinten_s"] = Gap(GapCarBehind), ["abstand_platz_vorn_s"] = Gap(GapPlaceAhead),
                ["virtuelle_energie"] = VirtualEnergy > 0 && VirtualEnergy < 1e6 ? (JToken)Math.Round(VirtualEnergy, 3) : null,
                ["fahrhilfen"] = new JObject { ["tc"] = Tc + "/" + TcMax, ["abs"] = Abs + "/" + AbsMax, ["motor_map"] = MotorMap + "/" + MotorMapMax,
                    ["tc_greift"] = TcActive, ["abs_greift"] = AbsActive, ["limiter"] = SpeedLimiter },
                ["offene_strafen"] = me?.Penalties, ["boxenstatus"] = me == null ? null : PitStates[Math.Min(Math.Max(me.PitState, 0), 4)],
                ["hinweis"] = "Direkt vom Spiel. Abstände des Spiels in Sekunden, 0 oder kleiner heißt kein Wert.",
            };
            o["feld"] = new JArray(Cars.Take(40).Select(c => new JObject
            {
                ["platz"] = c.Place, ["fahrer"] = c.Name, ["klasse"] = c.Class, ["runden"] = c.Laps, ["rueckstand_vordermann_s"] = Math.Round(c.BehindNext, 2),
                ["rueckstand_fuehrender_s"] = Math.Round(c.BehindLeader, 2), ["runden_zurueck"] = c.LapsBehindLeader, ["stopps"] = c.Pitstops, ["offene_strafen"] = c.Penalties,
                ["in_box"] = c.InPits, ["boxenstatus"] = PitStates[Math.Min(Math.Max(c.PitState, 0), 4)], ["status"] = Finish[Math.Min(Math.Max(c.FinishStatus, 0), 3)],
                ["beste_runde_s"] = c.BestLap > 0 ? (JToken)Math.Round(c.BestLap, 3) : null, ["letzte_runde_s"] = c.LastLap > 0 ? (JToken)Math.Round(c.LastLap, 3) : null,
                ["ich"] = c.Player,
            }));
            o["rennleitung_zuletzt"] = new JArray((raceControl ?? Enumerable.Empty<string>()).Select(t => (JToken)t));
            return o;
        }

        private static JToken Gap(float v) => v > 0 && v < 3600 ? (JToken)Math.Round(v, 2) : null;
    }
}

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Tracklimit-Punkte aus LMU (Rob 20.11.: Tracklimits, Vorfallpunkte und Strafen unterscheiden, „das wird geliefert, wertet es klug aus“).
    /// Steigt der Zähler, sagt Mik den Stand und wie viel bis zur Strafe fehlt.
    /// </summary>
    internal sealed class LMUTrackLimitPoints
    {
        private int lastSteps = -1, session = int.MinValue;

        internal string Observe(LMUNativeSnapshot s, GameState.GameStateData gs)
        {
            if (s == null || gs == null) return null;
            if (gs.SessionData.SessionId != session) { session = gs.SessionData.SessionId; lastSteps = -1; }
            int steps = s.TrackLimitsSteps;
            bool increased = lastSteps >= 0 && steps > lastSteps;
            lastSteps = steps;
            if (!increased || s.StepsPerPoint <= 0) return null;
            return Text(s.LimitPoints, s.PointsPerPenalty, s.StepsPerPoint, s.StepsPerPenalty, steps);
        }

        private static string N(double v) => Math.Abs(v - Math.Round(v)) < .05 ? ((int)Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture) : RadioLanguage.Num(v, "0.0");

        internal static string Text(double points, double max, int stepsPerPoint, int stepsPerPenalty, int steps)
        {
            string p = N(points);
            if (!(max > 0))
                return RadioVariants.Pick("lmu-limits-nomax",
                    RadioLanguage.T("Tracklimit gemeldet, jetzt " + p + " Punkte.", "Track limits reported, now " + p + " points.", "Límites de pista señalados, ahora " + p + " puntos."),
                    RadioLanguage.T("Die Rennkommissare zählen " + p + " Tracklimit-Punkte.", "The stewards count " + p + " track limit points.", "Los comisarios cuentan " + p + " puntos de límites."),
                    RadioLanguage.T("Wieder ein Tracklimit, " + p + " Punkte. Rand meiden.", "Another track limit, " + p + " points. Stay off the edge.", "Otro límite de pista, " + p + " puntos. Evita el borde."),
                    RadioLanguage.T("Vorsicht mit den Limits, wir stehen bei " + p + " Punkten.", "Careful with the limits, we're on " + p + " points.", "Cuidado con los límites, llevamos " + p + " puntos."));
            string m = N(max), left = N(Math.Max(0, max - points));
            string text = RadioVariants.Pick("lmu-limits",
                RadioLanguage.T("Tracklimit gemeldet, jetzt " + p + " von " + m + " Punkten.", "Track limits reported, now " + p + " of " + m + " points.", "Límites de pista señalados, ahora " + p + " de " + m + " puntos."),
                RadioLanguage.T("Die Rennkommissare zählen " + p + " von " + m + " Tracklimit-Punkten. Noch " + left + " bis zur Strafe.", "The stewards count " + p + " of " + m + " track limit points. " + left + " left before a penalty.", "Los comisarios cuentan " + p + " de " + m + " puntos de límites. Quedan " + left + " para la sanción."),
                RadioLanguage.T("Vorsicht mit den Limits: " + p + " Punkte, bei " + m + " gibt es eine Strafe.", "Careful with the limits: " + p + " points, at " + m + " there's a penalty.", "Cuidado con los límites: " + p + " puntos, con " + m + " hay sanción."),
                RadioLanguage.T("Wieder ein Tracklimit, " + p + " von " + m + ". Rand meiden.", "Another track limit, " + p + " of " + m + ". Stay off the edge.", "Otro límite de pista, " + p + " de " + m + ". Evita el borde."),
                RadioLanguage.T("Tracklimits: " + p + " von " + m + ". Noch " + left + " Luft.", "Track limits: " + p + " of " + m + ". " + left + " to spare.", "Límites: " + p + " de " + m + ". Margen de " + left + "."));
            // Nächster Verstoß führt zur Strafe?
            if (stepsPerPenalty > 0 && steps + stepsPerPoint >= stepsPerPenalty && steps < stepsPerPenalty)
                text += " " + RadioVariants.Pick("lmu-limits-last",
                    RadioLanguage.T("Beim nächsten gibt es die Strafe!", "The next one is a penalty!", "¡El próximo es sanción!"),
                    RadioLanguage.T("Jetzt keinen mehr, sonst Strafe.", "No more now, or it's a penalty.", "Ni uno más, o sanción."),
                    RadioLanguage.T("Ab jetzt sauber bleiben, der nächste kostet.", "Keep it clean now, the next one costs us.", "Ahora limpio, el próximo cuesta."),
                    RadioLanguage.T("Letzte Warnung vor der Strafe.", "Last warning before a penalty.", "Último aviso antes de la sanción."));
            return text;
        }
    }
}
