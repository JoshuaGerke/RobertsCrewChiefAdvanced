using System.Runtime.InteropServices;

namespace F12023UdpNet
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