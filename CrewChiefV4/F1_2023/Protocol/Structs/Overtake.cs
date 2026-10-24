using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Overtake
    {
        /// <summary>
        /// Vehicle index of the vehicle overtaking
        /// </summary>
        public byte overtakingVehicleIdx;

        /// <summary>
        /// Vehicle index of the vehicle being overtaken
        /// </summary>
        public byte beingOvertakenVehicleIdx;
    }
}