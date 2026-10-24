using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// This packet contains lap times and tyre usage for the session.
    /// This packet works slightly differently to other packets.
    /// To reduce CPU and bandwidth, each packet relates to a specific vehicle and is sent every 1/20 s, and the vehicle being sent is cycled through.
    /// Therefore in a 20 car race you should receive an update for each vehicle at least once per second.
    /// Note that at the end of the race, after the final classification packet has been sent, a final bulk update of all the session histories for the vehicles in that session will be sent.
    /// 
    /// <para>Frequency: 20 per second but cycling through cars</para>
    /// <para>Size: 1460 bytes</para>
    /// <para>Version: 1</para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketSessionHistoryData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        PacketHeader m_header;

        /// <summary>
        /// Index of the car this lap data relates to
        /// </summary>
        byte m_carIdx;

        /// <summary>
        /// Num laps in the data (including current partial lap)
        /// </summary>
        byte m_numLaps;

        /// <summary>
        /// Lap the best lap time was achieved on
        /// </summary>
        byte m_bestLapTimeLapNum;

        /// <summary>
        /// Lap the best Sector 1 time was achieved on
        /// </summary>
        byte m_bestSector1LapNum;

        /// <summary>
        /// Lap the best Sector 2 time was achieved on
        /// </summary>
        byte m_bestSector2LapNum;

        /// <summary>
        /// Lap the best Sector 3 time was achieved on
        /// </summary>
        byte m_bestSector3LapNum;

        /// <summary>
        /// 100 laps of data max
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 100)]
        LapHistoryData[] m_lapHistoryData;

        /// <summary>
        /// History data for tyre stints
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        TyreStintHistoryData[] m_tyreStintsHistoryData;
    }
}