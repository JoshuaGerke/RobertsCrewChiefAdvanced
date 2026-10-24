using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// Lobby information for multiplayer lobbies
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct LobbyInfoData
    {
        /// <summary>
        /// Whether the vehicle is AI (1) or Human (0) controlled
        /// </summary>
        byte m_aiControlled;

        /// <summary>
        /// Team id - see appendix (255 if no team currently selected)
        /// </summary>
        byte m_teamId;

        /// <summary>
        /// Nationality of the driver
        /// </summary>
        byte m_nationality;

        /// <summary>
        /// Name of participant in UTF-8 format - null terminated
        /// Will be truncated with ... (U+2026) if too long
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        byte[] m_name;

        /// <summary>
        /// Car number of the player
        /// </summary>
        byte m_carNumber;

        /// <summary>
        /// 0 = not ready, 1 = ready, 2 = spectating
        /// </summary>
        ReadyStatus m_readyStatus;
    }
}