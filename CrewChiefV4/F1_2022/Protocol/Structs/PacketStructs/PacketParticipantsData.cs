using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// This is a list of participants in the race. 
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketParticipantsData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Number of active cars in the data – should match number of cars on HUD
        /// </summary>
        public byte m_numActiveCars;        

        /// <summary>
        /// Participant data for every car
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        public ParticipantData[] m_participants;
    }
}
