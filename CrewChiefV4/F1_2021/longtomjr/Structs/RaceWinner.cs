using System.Runtime.InteropServices;

namespace F12021UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct RaceWinner
    {
        public byte vehicleIdx;
    }
}
