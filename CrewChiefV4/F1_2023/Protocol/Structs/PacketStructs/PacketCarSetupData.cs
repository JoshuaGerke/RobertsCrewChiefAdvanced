using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// This packet details the car setups for each vehicle in the session. 
    /// Note that in multiplayer games, other player cars will appear as blank, you will only be able to see your own car setup, regardless of the “Your Telemetry” setting. 
    /// Spectators will also not be able to see any car setups.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketCarSetupData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Setups of the cars in the session.
        /// Only available for player's car and AI cars.
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        public CarSetupData[] m_carSetups;
    }
}
