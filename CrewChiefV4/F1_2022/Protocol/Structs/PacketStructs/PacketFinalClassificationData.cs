using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// This packet details the final classification at the end of the race, and the data will match with the post race results screen.
    /// This is especially useful for multiplayer games where it is not always possible to send lap times on the final frame because of network delay.
    /// 
    /// <para>Frequency: Once at the end of a race</para>
    /// <para>Size: 1020 bytes</para>
    /// <para>Version: 1</para>
    /// 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketFinalClassificationData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        PacketHeader m_header;

        /// <summary>
        /// Number of cars in the final classification
        /// </summary>
        byte m_numCars;

        /// <summary>
        /// Final classification data for each car
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        FinalClassificationData[] m_classificationData;
    }
}