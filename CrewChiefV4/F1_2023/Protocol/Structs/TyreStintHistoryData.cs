using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// Historical tyre stint data
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TyreStintHistoryData
    {
        /// <summary>
        /// Lap the tyre usage ends on (255 of current tyre)
        /// </summary>
        byte m_endLap;

        /// <summary>
        /// Actual tyres used by this driver
        /// </summary>
        TyreCompound m_tyreActualCompound;

        /// <summary>
        /// Visual tyres used by this driver
        /// </summary>
        VisualTyreCompound m_tyreVisualCompound;
    }
}