using System;
using System.Runtime.InteropServices;
using rF2SharedMemory.LMUData;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Fahrdynamik aus dem nativen LMU-Abbild (bis 100 Hz). Das eigene Auto steht als Index direkt in der Telemetrie;
    /// Streckenposition, Runden und Box kommen aus der Wertung (5 Hz) und werden mit der Fahrzeit seit dem Wertungsbild
    /// fortgeschrieben, wie beim rF2-Weg.
    /// </summary>
    internal sealed class LMUNativeDynamicsSource : DynamicsSource
    {
        private static int VOff(string f) => Marshal.OffsetOf(typeof(rF2VehicleTelemetry), f).ToInt32();
        private static int WOff(string f) => Marshal.OffsetOf(typeof(rF2Wheel), f).ToInt32();
        private static int IOff(string f) => Marshal.OffsetOf(typeof(rF2ScoringInfo), f).ToInt32();
        private static int VSOff(string f) => Marshal.OffsetOf(typeof(rF2VehicleScoring), f).ToInt32();
        private static readonly int wSize = Marshal.SizeOf(typeof(rF2Wheel));
        private static readonly int vId = VOff("mID"), vEt = VOff("mElapsedTime"), vVel = VOff("mLocalVel"), vAcc = VOff("mLocalAccel"), vGear = VOff("mGear"),
            vThrottle = VOff("mFilteredThrottle"), vBrake = VOff("mFilteredBrake"), vSteer = VOff("mFilteredSteering"), vRot = VOff("mLocalRot"),
            vWheels = VOff("mWheels"), vAbs = VOff("mABSActive"), wRot = WOff("mRotation"), wSurface = WOff("mSurfaceType"),
            iEt = IOff("mCurrentET"), iRealtime = IOff("mInRealtime"),
            vsId = VSOff("mID"), vsPlayer = VSOff("mIsPlayer"), vsDist = VSOff("mLapDist"), vsLaps = VSOff("mTotalLaps"), vsPits = VSOff("mInPits");

        private readonly LMUNativeReader reader = new LMUNativeReader();
        private readonly byte[] buffer = new byte[LMUNativeLayout.TotalSize];
        private double lastEt = double.NaN;

        internal override string Name => "LMU nativ";
        protected override string MapName => LMUNativeLayout.MapName;
        protected override int Size => LMUNativeLayout.TotalSize;
        protected override bool Parse(byte[] b, DynamicsFrame f) => Fill(b, f, ref lastEt);

        internal override bool IsOpen => reader.IsOpen;
        internal override bool Open() => reader.Open();
        internal override void Close() => reader.Dispose();

        internal override bool Read(DynamicsFrame f, double timeS)
        {
            if (!reader.IsOpen) return false;
            try
            {
                if (!reader.Copy(buffer) || !Fill(buffer, f, ref lastEt)) return false;
                f.TimeS = timeS;
                return true;
            }
            catch (Exception) { Close(); return false; }
        }

        /// <summary>Füllt ein Bild aus dem kopierten Abbild. Testbar mit künstlichen Puffern.</summary>
        internal static bool Fill(byte[] b, DynamicsFrame f, ref double lastEt)
        {
            if (b[LMUNativeLayout.PlayerHasVehicleOffset] == 0) return false;
            int active = Math.Min((int)b[LMUNativeLayout.ActiveVehiclesOffset], LMUNativeLayout.MaxVehicles), index = b[LMUNativeLayout.PlayerIndexOffset];
            if (index >= active) return false;
            int v = LMUNativeLayout.TelemetryVehiclesOffset + index * LMUNativeLayout.VehicleTelemetrySize;
            double et = BitConverter.ToDouble(b, v + vEt);
            if (et == lastEt) return false;
            int id = BitConverter.ToInt32(b, v + vId);

            // Wertung: eigenes Auto (gleiche Slot-ID oder Spielerkennung).
            int scored = Math.Min(Math.Max(BitConverter.ToInt32(b, LMUNativeLayout.ScoringCountOffset), 0), LMUNativeLayout.MaxVehicles);
            double dist = -1, scoringEt = BitConverter.ToDouble(b, LMUNativeLayout.ScoringOffset + iEt); int laps = 0; bool pit = false;
            for (int i = 0; i < scored; i++)
            {
                int o = LMUNativeLayout.ScoringVehiclesOffset + i * LMUNativeLayout.VehicleScoringSize;
                if (BitConverter.ToInt32(b, o + vsId) != id && b[o + vsPlayer] == 0) continue;
                dist = BitConverter.ToDouble(b, o + vsDist); laps = BitConverter.ToInt16(b, o + vsLaps); pit = b[o + vsPits] != 0;
                break;
            }

            double vx = BitConverter.ToDouble(b, v + vVel), vy = BitConverter.ToDouble(b, v + vVel + 8), vz = BitConverter.ToDouble(b, v + vVel + 16);
            f.Speed = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);
            f.LatAccel = (float)BitConverter.ToDouble(b, v + vAcc); f.LongAccel = (float)BitConverter.ToDouble(b, v + vAcc + 16);
            f.Gear = BitConverter.ToInt32(b, v + vGear);
            f.Throttle = (float)BitConverter.ToDouble(b, v + vThrottle); f.Brake = (float)BitConverter.ToDouble(b, v + vBrake);
            f.Steering = (float)BitConverter.ToDouble(b, v + vSteer); f.YawRate = (float)BitConverter.ToDouble(b, v + vRot + 8);
            for (int w = 0; w < 4; w++)
            {
                int o = v + vWheels + w * wSize;
                f.WheelRotation[w] = (float)BitConverter.ToDouble(b, o + wRot);
                f.Surfaces[w] = LMUDynamicsSource.Surface(b[o + wSurface]);
            }
            // Das native Abbild meldet den ABS-Eingriff (der rF2-Weg nicht).
            f.AbsActive = b[v + vAbs] != 0; f.AbsUnknown = false;
            f.Driving = b[LMUNativeLayout.ScoringOffset + iRealtime] != 0;
            f.InPit = pit;
            f.CompletedLaps = laps;
            double ahead = et > scoringEt && et - scoringEt < 1 ? f.Speed * (et - scoringEt) : 0;
            f.LapDistance = dist >= 0 ? (float)(dist + ahead) : -1;
            lastEt = et;
            return true;
        }
    }

    /// <summary>LMU: zuerst das native Abbild, sonst der ältere rF2-Weg.</summary>
    internal sealed class LMUPreferredDynamicsSource : DynamicsSource
    {
        private readonly LMUNativeDynamicsSource native = new LMUNativeDynamicsSource();
        private readonly LMUDynamicsSource legacy = new LMUDynamicsSource();
        private DynamicsSource active;
        private double nativeSince = double.NaN, lastFrame = double.NaN;
        internal const double NativeSilentSeconds = 3;

        internal override string Name => active?.Name ?? "LMU";
        protected override string MapName => LMUNativeLayout.MapName;
        protected override int Size => LMUNativeLayout.TotalSize;
        protected override bool Parse(byte[] b, DynamicsFrame f) => false;
        internal override bool IsOpen => active != null && active.IsOpen;

        internal override bool Open()
        {
            Close();
            nativeSince = lastFrame = double.NaN;
            if (native.Open()) { active = native; return true; }
            if (legacy.Open()) { active = legacy; return true; }
            return false;
        }

        internal override void Close() { native.Close(); legacy.Close(); active = null; }

        internal override bool Read(DynamicsFrame f, double timeS)
        {
            if (active == null) return false;
            bool ok = active.Read(f, timeS);
            if (ok) { lastFrame = timeS; return true; }
            // Natives Abbild vorhanden, aber das Spiel schreibt nicht hinein (z. B. ältere LMU-Version): auf den rF2-Weg wechseln.
            if (ReferenceEquals(active, native))
            {
                if (double.IsNaN(nativeSince)) nativeSince = timeS;
                double silent = timeS - (double.IsNaN(lastFrame) ? nativeSince : lastFrame);
                if (silent >= NativeSilentSeconds && legacy.Open()) { native.Close(); active = legacy; }
            }
            return false;
        }
    }
}
