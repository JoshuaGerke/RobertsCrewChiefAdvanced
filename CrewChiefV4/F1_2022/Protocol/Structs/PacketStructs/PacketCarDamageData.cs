using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// This packet details car damage parameters for all the cars in the race.
    ///
    /// <para>Frequency: 10 per second</para>
    /// <para>Size: 953 bytes</para>
    /// <para>Version: 1</para>
    /// 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketCarDamageData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        PacketHeader m_header;

        /// <summary>
        /// Car damage data for each car
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        CarDamageData[] m_carDamageData;
    }
}