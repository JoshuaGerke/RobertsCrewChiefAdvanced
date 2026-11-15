using System;

namespace CrewChiefV4.AiTeam
{
    // Tracks observed source progression, not read frequency or individual network-car latency.
    internal sealed class TelemetrySourceClock
    {
        private double marker;
        private int session, phase, slot;
        private long sourceTicks;
        public void Reset() { sourceTicks=0; }
        public long Observe(double value,int type,int nextPhase,int vehicle,long received)
        {
            if(double.IsNaN(value) || double.IsInfinity(value) || value<0 || vehicle<0 || received<=0) return 0;
            if(sourceTicks==0 || value!=marker || type!=session || nextPhase!=phase || vehicle!=slot || received<sourceTicks)
            { marker=value; session=type; phase=nextPhase; slot=vehicle; sourceTicks=received; }
            return sourceTicks;
        }
    }
}
