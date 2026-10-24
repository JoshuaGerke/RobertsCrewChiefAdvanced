using System.Runtime.InteropServices;

namespace F12019UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Retirement
    {
        public byte vehicleIdx;
    }
}
