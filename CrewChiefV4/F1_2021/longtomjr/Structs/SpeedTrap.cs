using System.Runtime.InteropServices;

namespace F12021UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct SpeedTrap
    {
        public byte vehicleIdx; // Vehicle index of the vehicle triggering speed trap
        public float speed;      // Top speed achieved in kilometres per hour
    }
}