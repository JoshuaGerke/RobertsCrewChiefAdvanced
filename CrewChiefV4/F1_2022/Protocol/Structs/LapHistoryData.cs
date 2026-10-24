using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// Historical lap data
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LapHistoryData
    {
        /// <summary>
        /// Lap time in milliseconds
        /// </summary>
        uint m_lapTimeInMS;

        /// <summary>
        /// Sector 1 time in milliseconds
        /// </summary>
        ushort m_sector1TimeInMS;

        /// <summary>
        /// Sector 2 time in milliseconds
        /// </summary>
        ushort m_sector2TimeInMS;

        /// <summary>
        /// Sector 3 time in milliseconds
        /// </summary>
        ushort m_sector3TimeInMS;

        /// <summary>
        /// 0x01 bit set-lap valid, 0x02 bit set-sector 1 valid
        /// 0x04 bit set-sector 2 valid, 0x08 bit set-sector 3 valid
        /// </summary>
        byte m_lapValidBitFlags;
    }
}