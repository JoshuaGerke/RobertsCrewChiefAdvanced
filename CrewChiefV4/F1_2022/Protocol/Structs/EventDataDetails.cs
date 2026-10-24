using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct EventDataDetails
    {
        [FieldOffset(0)]
        public FastestLap fastestLap;
        [FieldOffset(0)]
        public Retirement retirement;
        [FieldOffset(0)]
        public TeamMateInPits teamMateInPits;
        [FieldOffset(0)]
        public RaceWinner raceWinner;
        [FieldOffset(0)]
        public Penalty penalty;
        [FieldOffset(0)]
        public SpeedTrap speedTrap;
        [FieldOffset(0)]
        public StartLights startLights;
        [FieldOffset(0)]
        public DriveThroughPenaltyServed driveThroughPenaltyServed;
        [FieldOffset(0)]
        public StopAndGoPenaltyServed stopAndGoPenaltyServed;
        [FieldOffset(0)]
        public Flashback flashback;
        [FieldOffset(0)]
        public Buttons buttons;
    }
}
