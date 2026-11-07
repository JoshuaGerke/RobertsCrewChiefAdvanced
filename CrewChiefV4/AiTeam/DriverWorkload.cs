using System;
using System.Threading;
using CircularBuffer;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    public enum DriverWorkloadMode { Unavailable, Management, Corner, Battle, Crisis }
    public enum SteeringMeasurement { Unknown, Normalized, Radians }

    /// <summary>One telemetry-thread writer. Fixed-size value samples, no I/O, model calls or per-sample heap objects.</summary>
    public sealed class DriverWorkload
    {
        public const int Capacity = 600;
        private struct Sample
        {
            public long SourceTicks;
            public float Brake, Throttle, Steering, Speed, SimTime;
            public float? LateralAcceleration;
        }

        private CircularBuffer<Sample> history = new CircularBuffer<Sample>(Capacity);
        private int session = int.MinValue, publishedMode, score;
        private SessionType type;
        private long sourceTicks, calmAfterTicks, controlCalmAfterTicks, crisisUntilTicks;
        private DateTime lastSource = DateTime.MinValue;
        private float lastSimTime = float.NaN;

        public DriverWorkloadMode Mode => (DriverWorkloadMode)Volatile.Read(ref publishedMode);
        public int Count => history.Size;

        public void Reset()
        {
            history = new CircularBuffer<Sample>(Capacity);
            session = int.MinValue;
            lastSource = DateTime.MinValue;
            lastSimTime = float.NaN;
            Interlocked.Exchange(ref sourceTicks, 0);
            Interlocked.Exchange(ref calmAfterTicks, 0);
            Interlocked.Exchange(ref controlCalmAfterTicks, 0);
            Interlocked.Exchange(ref crisisUntilTicks, 0);
            Volatile.Write(ref publishedMode, (int)DriverWorkloadMode.Unavailable);
        }

        public void NoteIncident(DateTime now)
        { Interlocked.Exchange(ref crisisUntilTicks, now.AddSeconds(3).Ticks); }

        public bool Observe(GameStateData state, DateTime now, bool overlap, TrackPositionSnapshot track,
            RaceGapMeasurement front, RaceGapMeasurement rear, SteeringMeasurement steering = SteeringMeasurement.Unknown)
        {
            if (state == null) return false;
            DateTime source = state.SourceTelemetryUtc == DateTime.MinValue ? state.Now : state.SourceTelemetryUtc;
            if (source == DateTime.MinValue || now < source || now - source > TimeSpan.FromSeconds(1))
            {
                Volatile.Write(ref publishedMode, (int)DriverWorkloadMode.Unavailable);
                return false;
            }
            var s = state.SessionData;
            if (session != s.SessionId || type != s.SessionType || source < lastSource ||
                lastSource != DateTime.MinValue && source - lastSource > TimeSpan.FromSeconds(1))
            {
                Reset();
                session = s.SessionId;
                type = s.SessionType;
            }
            if (source <= lastSource || lastSource != DateTime.MinValue && source - lastSource < TimeSpan.FromMilliseconds(10))
                return false;
            // A paused simulation can still be polled. Its clock is not a new driving observation.
            if (s.SessionPhase == SessionPhase.Green && s.SessionRunningTime == lastSimTime && history.Size > 0)
                return false;
            lastSource = source;
            lastSimTime = s.SessionRunningTime;
            var motion = state.PositionAndMotionData;
            var controls = state.ControlData;
            var sample = new Sample { SourceTicks = source.Ticks, Brake = controls.BrakePedal,
                Throttle = controls.ThrottlePedal, Steering = controls.SteeringWheelAngle,
                Speed = motion.CarSpeed, SimTime = s.SessionRunningTime,
                LateralAcceleration = motion.AccelerationVector?.LatAccel };
            history.PushBack(sample);
            Interlocked.Exchange(ref sourceTicks, source.Ticks);

            bool driving = motion.CarSpeed > 5 && !state.PitData.InPitlane;
            bool crisis = state.PenaltiesData.IsOffRacingSurface || now.Ticks < Interlocked.Read(ref crisisUntilTicks);
            bool steeringLoad = steering == SteeringMeasurement.Normalized && Finite(sample.Steering) && Math.Abs(sample.Steering) >= .18f ||
                steering == SteeringMeasurement.Radians && Finite(sample.Steering) && Math.Abs(sample.Steering) >= .35f;
            bool lateralLoad = sample.LateralAcceleration.HasValue && Finite(sample.LateralAcceleration.Value) &&
                Math.Abs(sample.LateralAcceleration.Value) >= 4;
            bool close = s.SessionType == SessionType.Race && (Close(front) || Close(rear));
            // Physical headway is used solely for workload, never presented as a timing gap.
            if (!close && driving && track != null && now >= track.Utc && now - track.Utc <= TimeSpan.FromSeconds(1))
                foreach (var car in track.Cars)
                    if (!car.InPits && car.SpatiallyConsistent && car.RouteGapM.HasValue &&
                        Math.Abs(car.RouteGapM.Value) <= Math.Max(10, motion.CarSpeed) && Math.Abs(car.RouteGapM.Value) >= .5)
                    { close = true; break; }
            bool mappedCorner = track != null && now >= track.Utc && now - track.Utc <= TimeSpan.FromSeconds(1) &&
                !string.IsNullOrWhiteSpace(track.CurrentSection);
            bool controlLoad = driving && (controls.BrakePedal >= .18f || steeringLoad || lateralLoad || mappedCorner || state.FlagData.isLocalYellow);
            if (controlLoad || overlap || crisis) Interlocked.Exchange(ref controlCalmAfterTicks, now.AddMilliseconds(350).Ticks);
            DriverWorkloadMode mode = crisis ? DriverWorkloadMode.Crisis :
                overlap || driving && close ? DriverWorkloadMode.Battle :
                controlLoad ? DriverWorkloadMode.Corner : DriverWorkloadMode.Management;
            if (mode != DriverWorkloadMode.Management)
                Interlocked.Exchange(ref calmAfterTicks, now.AddMilliseconds(750).Ticks);
            Volatile.Write(ref score, mode == DriverWorkloadMode.Crisis ? 100 : mode == DriverWorkloadMode.Battle ? 85 :
                mode == DriverWorkloadMode.Corner ? 65 : 10);
            Volatile.Write(ref publishedMode, (int)mode);
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Close(RaceGapMeasurement gap) => gap != null && gap.IsRaceGap && gap.Seconds > .05f && gap.Seconds < 1;

        public bool AllowsRoutine(DateTime now)
        {
            long source = Interlocked.Read(ref sourceTicks);
            return source > 0 && now.Ticks >= source && now.Ticks - source <= TimeSpan.TicksPerSecond &&
                Mode == DriverWorkloadMode.Management && now.Ticks >= Interlocked.Read(ref calmAfterTicks);
        }

        public bool AllowsReport(DateTime now, bool shortTactical)
        {
            if (AllowsRoutine(now)) return true;
            long source = Interlocked.Read(ref sourceTicks);
            return shortTactical && Mode == DriverWorkloadMode.Battle && source > 0 && now.Ticks >= source &&
                now.Ticks - source <= TimeSpan.TicksPerSecond && now.Ticks >= Interlocked.Read(ref controlCalmAfterTicks);
        }

        internal string Guidance(DateTime now)
        {
            string mode = Mode.ToString();
            return "\nAktuelle Funklage " + mode + ". " +
                (Mode == DriverWorkloadMode.Battle || Mode == DriverWorkloadMode.Corner || Mode == DriverWorkloadMode.Crisis ?
                    "Nur die wichtigste belegte Information. Höchstens zwölf Wörter. Im Krisenmodus erst eine ruhige kurze Rückmeldung, dann den gesicherten Befund. " :
                    "Höchstens ein bis zwei kurze Sätze und insgesamt achtzehn Wörter während der Fahrt. ") +
                "Normale Meldungen gibt die Anwendung erst bei geringer Fahrerbelastung frei.\n";
        }

        /// <summary>Called by the telemetry-thread aggregator, never by the audio callback.</summary>
        internal JObject Describe(DateTime now)
        {
            double? rate = history.Size > 1 && history.Back().SourceTicks > history.Front().SourceTicks ?
                (double?)(history.Size - 1) * TimeSpan.TicksPerSecond / (history.Back().SourceTicks - history.Front().SourceTicks) : null;
            return new JObject { ["modus"] = Mode.ToString(), ["belastungsindex"] = Volatile.Read(ref score),
                ["routine_freigegeben"] = AllowsRoutine(now), ["stichproben"] = history.Size,
                ["gemessene_abtastrate_hz"] = rate.HasValue ? (JToken)Math.Round(rate.Value, 2) : null,
                ["maximale_abtastrate_hz"] = 100, ["puffer_kapazitaet"] = Capacity,
                ["querbeschleunigung_vorhanden"] = history.Size > 0 && history.Back().LateralAcceleration.HasValue,
                ["hinweis"] = "Belastung ist ein lokaler Schätzwert. Die tatsächliche Datenrate folgt dem Spieladapter." };
        }
    }
}
