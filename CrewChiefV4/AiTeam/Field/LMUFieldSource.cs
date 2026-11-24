using System;
using System.Runtime.InteropServices;
using rF2SharedMemory;
using rFactor2Data = rF2SharedMemory.rFactor2Data;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>
    /// Le Mans Ultimate über Crew Chiefs Puffer „$LMU.3.8.SMMP_Scoring$“ (rFactor-2-Layout, Crew-Chief-Strukturen, Offsets per
    /// Marshal). Das Scoring schreibt die Sim etwa fünfmal pro Sekunde; neue Bilder erkennt die Quelle an der Sessionzeit.
    /// Im Garagenmonitor (nicht „in realtime“) gilt Rob als im Menü – dort stehen Strecke, Feld und Session schon fest.
    /// </summary>
    internal sealed class LMUFieldSource : MappedFieldSource
    {
        private static readonly Type Root = typeof(rFactor2Data.rF2Scoring), Info = typeof(rFactor2Data.rF2ScoringInfo), Veh = typeof(rFactor2Data.rF2VehicleScoring);
        private static int Off(Type t, string f) => Marshal.OffsetOf(t, f).ToInt32();
        internal static readonly int StructSize = Marshal.SizeOf(Root), VehSize = Marshal.SizeOf(Veh);
        internal static readonly int oInfo = Off(Root, "mScoringInfo"), oVeh = Off(Root, "mVehicles");
        internal static readonly int iTrack = Off(Info, "mTrackName"), iSession = Off(Info, "mSession"), iEt = Off(Info, "mCurrentET"), iEnd = Off(Info, "mEndET"),
            iMaxLaps = Off(Info, "mMaxLaps"), iLapDist = Off(Info, "mLapDist"), iNum = Off(Info, "mNumVehicles"), iPhase = Off(Info, "mGamePhase"),
            iRealtime = Off(Info, "mInRealtime"), iAir = Off(Info, "mAmbientTemp"), iTrackTemp = Off(Info, "mTrackTemp");
        internal static readonly int vId = Off(Veh, "mID"), vName = Off(Veh, "mDriverName"), vLaps = Off(Veh, "mTotalLaps"), vFinish = Off(Veh, "mFinishStatus"),
            vDist = Off(Veh, "mLapDist"), vBest1 = Off(Veh, "mBestSector1"), vBest2 = Off(Veh, "mBestSector2"), vBest = Off(Veh, "mBestLapTime"),
            vLast = Off(Veh, "mLastLapTime"), vPlayer = Off(Veh, "mIsPlayer"), vPits = Off(Veh, "mInPits"), vPlace = Off(Veh, "mPlace"),
            vLapStart = Off(Veh, "mLapStartET"), vPos = Off(Veh, "mPos"), vVel = Off(Veh, "mLocalVel"), vCount = Off(Veh, "mCountLapFlag"),
            vGarage = Off(Veh, "mInGarageStall"), vClass = Off(Veh, "mVehicleClass");
        private const int Max = rFactor2Constants.MAX_MAPPED_VEHICLES;
        private double lastEt = double.NaN;
        private readonly System.Collections.Generic.Dictionary<int, float> baseDist = new System.Collections.Generic.Dictionary<int, float>();
        private long nativeSequence;

        public override string Name => "LMU";
        protected override string MapName => LMUConstants.MM_SCORING_FILE_NAME;
        protected override int Size => StructSize;

        protected override bool Parse(byte[] b, FieldFrame f, DateTime utc)
        {
            if (b == null || b.Length < StructSize || BitConverter.ToUInt32(b, 0) != BitConverter.ToUInt32(b, 4)) return false; // Sim schreibt gerade
            double et = BitConverter.ToDouble(b, oInfo + iEt);
            bool realtime = b[oInfo + iRealtime] != 0;
            // Keine neue Wertung: zwischen den Wertungsbildern mit der nativen Telemetrie fortschreiben (bis 20 Hz statt 5 Hz).
            if (realtime && et == lastEt) return Refine(f, utc);
            lastEt = et;
            if (!ParseBuffer(b, f, utc)) return false;
            baseDist.Clear();
            foreach (var c in f.Cars) if (c != null) baseDist[c.Slot] = c.LapDistance;
            if (realtime) Refine(f, utc);
            return true;
        }

        private bool Refine(FieldFrame f, DateTime utc)
        {
            var native = LMUNativeField.Latest;
            if (native == null || native.Sequence == nativeSequence || utc < native.Utc || utc - native.Utc > TimeSpan.FromMilliseconds(250)) return false;
            nativeSequence = native.Sequence;
            if (LMUNativeField.Apply(f, native, lastEt, baseDist) == 0) return false;
            f.Utc = utc;
            return true;
        }

        internal static FieldSession MapSession(int s) =>
            s >= 0 && s <= 4 ? FieldSession.Practice : s >= 5 && s <= 8 ? FieldSession.Qualify : s == 9 ? FieldSession.Warmup : s >= 10 && s <= 13 ? FieldSession.Race : FieldSession.Unknown;

        internal static FieldPhase MapPhase(int p)
        {
            switch (p)
            {
                case 0: case 1: return FieldPhase.Garage;
                case 2: return FieldPhase.Gridwalk;
                case 3: return FieldPhase.Formation;
                case 4: return FieldPhase.Countdown;
                case 5: return FieldPhase.Green;
                // Review 24.11.: FCY und Rennunterbrechung galten im Lagebild als Grün – das halbe Feld wirkte „langsam“ oder „steht“.
                case 6: return FieldPhase.FullCourseYellow;
                case 7: return FieldPhase.Stopped;
                case 8: return FieldPhase.Checkered;
                default: return FieldPhase.Unknown;
            }
        }

        internal static bool ParseBuffer(byte[] b, FieldFrame f, DateTime utc)
        {
            int info = oInfo;
            int num = BitConverter.ToInt32(b, info + iNum);
            if (num < 0 || num > Max) return false;
            f.Utc = utc; f.Sim = "LMU"; f.LayoutVerified = true;
            f.Track = Utf8(b, info + iTrack, 64); f.Layout = null;
            double len = BitConverter.ToDouble(b, info + iLapDist); f.TrackLength = len > 100 && len < 100000 ? (float)len : float.NaN;
            int session = BitConverter.ToInt32(b, info + iSession);
            f.RawSession = session; f.SessionIteration = -1; f.Session = MapSession(session);
            f.Phase = MapPhase(b[info + iPhase]);
            bool realtime = b[info + iRealtime] != 0;
            f.InMenus = !realtime; f.Paused = false; f.InReplay = false;
            double et = BitConverter.ToDouble(b, info + iEt), end = BitConverter.ToDouble(b, info + iEnd);
            int maxLaps = BitConverter.ToInt32(b, info + iMaxLaps);
            f.SimTime = et;
            f.TimedSession = end > 0 && end < 360000;
            f.LapsSession = maxLaps > 0 && maxLaps < 10000;
            f.TimeDuration = f.TimedSession ? (float)end : float.NaN;
            f.TimeRemaining = f.TimedSession ? (float)Math.Max(0, end - et) : float.NaN;
            f.SessionLaps = f.LapsSession ? maxLaps : -1;
            for (int i = 0; i < 3; i++) { f.RaceMinutes[i] = -1; f.RaceLaps[i] = -1; }
            if (f.Session == FieldSession.Race) { if (f.TimedSession) f.RaceMinutes[0] = (int)Math.Round(end / 60); else if (f.LapsSession) f.RaceLaps[0] = maxLaps; }
            double air = BitConverter.ToDouble(b, info + iAir), track = BitConverter.ToDouble(b, info + iTrackTemp);
            f.AirTempC = air > -60 && air < 70 ? (float)air : float.NaN; f.TrackTempC = track > -60 && track < 90 ? (float)track : float.NaN;
            f.PositionsToGiveBack = -1; f.NumCars = num;
            if (f.Cars.Length != num) f.Cars = new FieldCarFrame[num];
            f.PlayerIndex = -1; f.PlayerInGarage = false;
            for (int i = 0; i < num; i++)
            {
                int o = oVeh + i * VehSize;
                var c = f.Cars[i] ?? (f.Cars[i] = new FieldCarFrame());
                c.Slot = BitConverter.ToInt32(b, o + vId); c.Name = Utf8(b, o + vName, 32);
                // Rob 24.11.: Multiclass (Sebring) – ohne Klasse nannte die Rennbilanz den Gesamtsieger als Klassensieger.
                c.ClassId = FieldClasses.IdOf(Utf8(b, o + vClass, 32));
                c.CompletedLaps = BitConverter.ToInt16(b, o + vLaps);
                sbyte finish = unchecked((sbyte)b[o + vFinish]); c.FinishStatus = finish >= 0 && finish <= 3 ? finish : -1;
                double d = BitConverter.ToDouble(b, o + vDist); c.LapDistance = d >= 0 && d < 100000 ? (float)d : float.NaN;
                c.BestS1 = Time(BitConverter.ToDouble(b, o + vBest1)); c.BestS12 = Time(BitConverter.ToDouble(b, o + vBest2));
                c.BestLap = Time(BitConverter.ToDouble(b, o + vBest)); c.LastLap = Time(BitConverter.ToDouble(b, o + vLast));
                c.SplitBest1 = c.SplitBest2 = c.SplitBest3 = float.NaN;
                c.IsPlayer = b[o + vPlayer] != 0; c.InPit = b[o + vPits] != 0; c.Place = b[o + vPlace];
                double lapStart = BitConverter.ToDouble(b, o + vLapStart);
                c.LapTimeCurrent = lapStart > 0 && et > lapStart ? (float)(et - lapStart) : float.NaN;
                c.CurrentLapValid = b[o + vCount] == 2;
                c.X = (float)BitConverter.ToDouble(b, o + vPos); c.Y = (float)BitConverter.ToDouble(b, o + vPos + 8); c.Z = (float)BitConverter.ToDouble(b, o + vPos + 16);
                double vx = BitConverter.ToDouble(b, o + vVel), vy = BitConverter.ToDouble(b, o + vVel + 8), vz = BitConverter.ToDouble(b, o + vVel + 16);
                double sp = Math.Sqrt(vx * vx + vy * vy + vz * vz); c.Speed = sp >= 0 && sp < 200 ? (float)sp : float.NaN;
                c.PenaltyType = c.PenaltyReason = -1; c.SlowDown = c.DriveThrough = c.StopAndGo = c.TimeDeduction = -1;
                c.Extrapolated = false;
                if (c.IsPlayer) { f.PlayerIndex = i; f.PlayerInGarage = b[o + vGarage] != 0; }
            }
            FieldClasses.RankWithinClass(f.Cars);
            return true;
        }
    }
}
