using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// Lap details of all the cars in the session.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketLapData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Lap data for all the cars on track
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        public LapData[] m_lapData;

        /// <summary>
        /// Index of Personal Best car in time trial (255 if invalid)
        /// </summary>
        byte m_timeTrialPBCarIdx;

        /// <summary>
        /// Index of Rival car in time trial (255 if invalid)
        /// </summary>
        byte m_timeTrialRivalCarIdx;
    }
}
