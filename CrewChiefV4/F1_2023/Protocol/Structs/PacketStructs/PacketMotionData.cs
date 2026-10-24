using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// Definition of the motion packet. It provides physics data for all the cars being driven.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketMotionData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Data for all cars on track
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        public CarMotionData[] m_carMotionData;
    }
}
