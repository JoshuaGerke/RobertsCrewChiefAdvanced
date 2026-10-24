using System.Runtime.InteropServices;

namespace F12019UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct FastestLap
    {
        public byte vehicleIdx;
        public float lapTime;
    }
}
