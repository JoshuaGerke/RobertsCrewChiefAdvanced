using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// This packets gives a more in-depth details about tyre sets assigned to a vehicle during the session.
    ///
    /// <para>Frequency: 20 per second but cycling through cars</para>
    /// <para>Size: 231 bytes</para>
    /// <para>Version: 1</para>
    ///
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketTyreSetsData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        PacketHeader m_header;

        /// <summary>
        /// Index of the car this data relates to
        /// </summary>
        byte m_carIdx;

        /// <summary>
        /// 13 (dry) + 7 (wet)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        TyreSetData[] m_tyreSetData;

        /// <summary>
        /// Index into array of fitted tyre
        /// </summary>
        byte m_fittedIdx;
    }
}