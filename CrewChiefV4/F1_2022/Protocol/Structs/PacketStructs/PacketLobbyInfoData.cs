using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// This packet details the players currently in a multiplayer lobby. 
    /// It details each player’s selected car, any AI involved in the game and also the ready status of each of the participants.
    /// 
    /// <para>Frequency: Two every second when in the lobby</para>
    /// <para>Size: 1218 bytes</para>
    /// <para>Version: 1</para>
    /// 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketLobbyInfoData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        PacketHeader m_header;

        /// <summary>
        /// Number of players in the lobby data
        /// </summary>
        byte m_numPlayers;

        /// <summary>
        /// Lobby info data for each player in the lobby
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        LobbyInfoData[] m_lobbyPlayers;
    }
}