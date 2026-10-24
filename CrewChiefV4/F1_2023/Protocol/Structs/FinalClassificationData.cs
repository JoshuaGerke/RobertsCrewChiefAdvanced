using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// Final classification data at the end of the session
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FinalClassificationData
    {
        /// <summary>
        /// Finishing position
        /// </summary>
        byte m_position;

        /// <summary>
        /// Number of laps completed
        /// </summary>
        byte m_numLaps;

        /// <summary>
        /// Grid position of the car
        /// </summary>
        byte m_gridPosition;

        /// <summary>
        /// Number of points scored
        /// </summary>
        byte m_points;

        /// <summary>
        /// Number of pit stops made
        /// </summary>
        byte m_numPitStops;

        /// <summary>
        /// Result status - 0 = invalid, 1 = inactive, 2 = active
        /// 3 = finished, 4 = didnotfinish, 5 = disqualified
        /// 6 = not classified, 7 = retired
        /// </summary>
        ResultStatus m_resultStatus;

        /// <summary>
        /// Best lap time of the session in milliseconds
        /// </summary>
        uint m_bestLapTimeInMS;

        /// <summary>
        /// Total race time in seconds without penalties
        /// </summary>
        double m_totalRaceTime;

        /// <summary>
        /// Total penalties accumulated in seconds
        /// </summary>
        byte m_penaltiesTime;

        /// <summary>
        /// Number of tyres stints up to maximum
        /// </summary>
        byte m_numTyreStints;

        /// <summary>
        /// Actual tyres used by this driver
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        byte[] m_tyreStintsActual;

        /// <summary>
        /// Visual tyres used by this driver
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        byte[] m_tyreStintsVisual;

        /// <summary>
        /// The lap number stints end on
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        byte[] m_tyreStintsEndLaps;
    }
}