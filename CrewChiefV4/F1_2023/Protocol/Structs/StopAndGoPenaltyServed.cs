using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StopAndGoPenaltyServed
    {
        /// <summary>
        /// Vehicle index of the vehicle serving stop and go
        /// </summary>
        public byte vehicleIdx;
    }
}