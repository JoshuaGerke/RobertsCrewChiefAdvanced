using System.Runtime.InteropServices;

namespace F12020UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct TeamMateInPits
    {
        public byte vehicleIdx;
    }
}
