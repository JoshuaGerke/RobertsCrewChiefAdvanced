using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// Structure of a marshal zone.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MarshalZone
    {
        /// <summary>
        /// Fraction (0..1) of way through the lap the marshal zone starts.
        /// </summary>
        public float m_zoneStart;

        /// <summary>
        /// Flag displayed in the marshal zone.
        /// -1 = invalid/unknown, 0 = none, 1 = green, 2 = blue, 3 = yellow, 4 = red
        /// </summary>
        public ZoneFlag m_zoneFlag;
    }
}
