using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FastestLap
    {
        /// <summary>
        /// Vehicle index of the car achieving the fastest lap.
        /// </summary>
        public byte vehicleIdx;

        /// <summary>
        /// Lap time in seconds.
        /// </summary>
        public float lapTime;
    }
}
