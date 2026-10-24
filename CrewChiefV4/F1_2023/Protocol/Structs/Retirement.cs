using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Retirement
    {
        /// <summary>
        /// Vehicle index of the car retiring.
        /// </summary>
        public byte vehicleIdx;
    }
}
