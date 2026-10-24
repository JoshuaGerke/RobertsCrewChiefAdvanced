using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TeamMateInPits
    {
        /// <summary>
        /// Vehicle index of team mate.
        /// </summary>
        public byte vehicleIdx;
    }
}
