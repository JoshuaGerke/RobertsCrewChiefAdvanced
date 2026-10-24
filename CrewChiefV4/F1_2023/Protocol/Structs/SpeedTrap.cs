using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SpeedTrap
    {
        /// <summary>
        /// Vehicle index of the vehicle triggering speed trap
        /// </summary>
        public byte vehicleIdx;

        /// <summary>
        /// Top speed achieved in kilometres per hour
        /// </summary>
        public float speed;

        /// <summary>
        /// Overall fastest speed in session = 1, otherwise 0
        /// </summary>
        public byte isOverallFastestInSession;

        /// <summary>
        /// Fastest speed for driver in session = 1, otherwise 0
        /// </summary>
        public byte isDriverFastestInSession;

        /// <summary>
        /// Vehicle index of the vehicle that is the fastest in this session
        /// </summary>
        public byte fastestVehicleIdxInSession;

        /// <summary>
        /// Speed of the vehicle that is the fastest in this session
        /// </summary>
        public float fastestSpeedInSession;
    }
}