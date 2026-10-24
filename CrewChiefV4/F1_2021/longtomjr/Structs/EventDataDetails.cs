using System.Runtime.InteropServices;

namespace F12021UdpNet
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    struct EventDataDetails
    {
        [FieldOffset(0)]
        public Retirement retirement;
        [FieldOffset(0)]
        public TeamMateInPits teamMateInPits;
        [FieldOffset(0)]
        public RaceWinner raceWinner;
        [FieldOffset(0)]
        public FastestLap fastestLap;
    }
}
