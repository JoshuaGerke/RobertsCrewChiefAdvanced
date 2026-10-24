using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// This packet details car statuses for all the cars in the race.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketCarStatusData
    {
        /// <summary>
        /// Header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Car status data for every car in the session.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        public CarStatusData[] m_carStatusData;
    }
}
