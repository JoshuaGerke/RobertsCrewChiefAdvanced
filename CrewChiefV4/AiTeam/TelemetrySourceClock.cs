using System;

namespace CrewChiefV4.AiTeam
{
    // Tracks observed source progression, not read frequency or individual network-car latency.
    internal sealed class TelemetrySourceClock
    {
        private double marker;
        private int session, phase;
        private ulong slot, slotHigh;
        private long sourceTicks;
        private static readonly TimeSpan LiveFrameAge = TimeSpan.FromMilliseconds(300);

        internal static bool SupportsNativeCadence(GameEnum game) =>
            game == GameEnum.IRACING || game == GameEnum.RACE_ROOM || game == GameEnum.AMS2 ||
            game == GameEnum.ASSETTO_32BIT || game == GameEnum.ASSETTO_64BIT || game == GameEnum.ASSETTO_128CARS ||
            game == GameEnum.ACC || game == GameEnum.ASSETTO_EVO ||
            game == GameEnum.RF2_64BIT || game == GameEnum.LMU || game == GameEnum.PMR;

        internal static DateTime SampleUtc(long ticks, long received) =>
            ticks > 0 && ticks <= received && ticks <= DateTime.MaxValue.Ticks ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue;

        internal static int NativePollDelay(int normalDelay, DateTime iterationStarted, DateTime now, DateTime source)
        {
            if (normalDelay <= 20 || source == DateTime.MinValue || now < source || now - source > LiveFrameAge)
                return normalDelay;
            double workMilliseconds = Math.Max(0, (now - iterationStarted).TotalMilliseconds);
            // Leave time for the simulator when mapping or logging already consumes the frame budget.
            return Math.Max((int)Math.Min(normalDelay, Math.Max(1, workMilliseconds / 4)),
                20 - (int)Math.Min(20, workMilliseconds));
        }

        public void Reset() { sourceTicks=0; }
        public long Observe(double value, int type, int nextPhase, int vehicle, long received) =>
            vehicle < 0 ? 0 : Observe(value, type, nextPhase, (ulong)vehicle, 0, received);

        internal long Observe(double value, int type, int nextPhase, ulong vehicle, ulong vehicleHigh, long received)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || received <= 0) return 0;
            if (sourceTicks == 0 || value != marker || type != session || nextPhase != phase ||
                vehicle != slot || vehicleHigh != slotHigh || received < sourceTicks)
            {
                marker = value;
                session = type;
                phase = nextPhase;
                slot = vehicle;
                slotHigh = vehicleHigh;
                sourceTicks = received;
            }
            return sourceTicks;
        }
    }
}
