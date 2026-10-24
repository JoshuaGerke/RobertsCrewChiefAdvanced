using F12019UdpNet;
using System.Runtime.InteropServices;

namespace F12019UdpNet
{
    public struct PacketEventDataWithDetails
    {
        /// <summary>
        /// Header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Event string code:
        /// "SSTA" -> On session start
        /// "SEND" -> On session end
        /// “FTLP” -> Fastest lap
        /// "RTMT" -> Driver retired
        /// "DRSE" -> DRS Enabled
        /// "DRSD" -> DRS Disabled
        /// "TMPT" -> Team mate entered pits
        /// "CHQF" -> Chequered flag has been waved
        /// "RCWN" -> Race winner announced
        /// </summary>
        public byte[] m_eventStringCode;

        public object eventDetails;

        public PacketEventDataWithDetails(PacketEventData packetEventData, object eventDetails)
        {
            m_header = packetEventData.m_header;
            m_eventStringCode = packetEventData.m_eventStringCode;
            this.eventDetails = eventDetails;
        }
    }
}
