using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// Information for a tyre set
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TyreSetData
    {
        /// <summary>
        /// Actual tyre compound used
        /// </summary>
        TyreCompound m_actualTyreCompound;

        /// <summary>
        /// Visual tyre compound used
        /// </summary>
        VisualTyreCompound m_visualTyreCompound;

        /// <summary>
        /// Tyre wear (percentage)
        /// </summary>
        byte m_wear;

        /// <summary>
        /// Whether this set is currently available
        /// </summary>
        byte m_available;

        /// <summary>
        /// Recommended session for tyre set
        /// </summary>
        byte m_recommendedSession;

        /// <summary>
        /// Laps left in this tyre set
        /// </summary>
        byte m_lifeSpan;

        /// <summary>
        /// Max number of laps recommended for this compound
        /// </summary>
        byte m_usableLife;

        /// <summary>
        /// Lap delta time in milliseconds compared to fitted set
        /// </summary>
        ushort m_lapDeltaTime;

        /// <summary>
        /// Whether the set is fitted or not
        /// </summary>
        byte m_fitted;
    }
}