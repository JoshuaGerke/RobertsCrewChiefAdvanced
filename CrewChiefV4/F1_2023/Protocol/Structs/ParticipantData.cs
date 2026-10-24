using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// <para>
    /// This is a list of participants in the race.
    /// If the vehicle is controlled by AI, then the name will be the driver name. 
    /// If this is a multiplayer game, the names will be the Steam Id on PC, or the LAN name if appropriate.
    /// </para>
    /// 
    /// <para>If the vehicle is controlled by AI, then the name will be the driver name.</para>
    ///
    /// <para>
    /// If this is a multiplayer game, the names will be the Steam Id on PC,
    /// or the LAN name if appropriate.
    /// </para>
    ///
    /// <para>On Xbox One, the names will always be the driver name.</para>
    ///
    /// <para>
    /// On PS4 the name will be the LAN name if playing a LAN game,
    /// otherwise it will be the driver name.
    /// </para>
    /// 
    /// <para>
    /// The array should be indexed by vehicle index.
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ParticipantData
    {
        /// <summary>
        /// Whether the vehicle is AI (1) or Human (0) controlled
        /// </summary>
        public byte m_aiControlled;

        /// <summary>
        /// Driver id - see appendix 255 if network human
        /// </summary>
        public byte m_driverId;

        /// <summary>
        /// Network id – unique identifier for network players
        /// </summary>
        public byte m_networkId;

        /// <summary>
        /// Team id - see appendix
        /// </summary>
        public byte m_teamId;

        /// <summary>
        /// My team flag – 1 = My Team, 0 = otherwise
        /// </summary>
        public byte m_myTeam;

        /// <summary>
        /// Race number of the car
        /// </summary>
        public byte m_raceNumber;

        /// <summary>
        /// Nationality of the driver
        /// </summary>
        public byte m_nationality;

        /// <summary>
        /// Name of participant in UTF-8 format – null terminated
        /// Will be truncated with … (U+2026) if too long
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] m_name;

        /// <summary>
        /// The player's UDP setting, 0 = restricted, 1 = public
        /// </summary>
        public byte m_yourTelemetry;

        /// <summary>
        /// The player's show online names setting, 0 = off, 1 = on
        /// </summary>
        public byte m_showOnlineNames;

        /// <summary>
        /// 1 = Steam, 3 = PlayStation, 4 = Xbox, 6 = Origin, 255 = unknown
        /// </summary>
        public Platform m_platform;
    }
}
