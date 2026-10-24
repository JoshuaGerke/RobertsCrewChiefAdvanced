using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DriveThroughPenaltyServed
    {
        /// <summary>
        /// Vehicle index of the vehicle serving drive through
        /// </summary>
        public byte vehicleIdx;
    }
}