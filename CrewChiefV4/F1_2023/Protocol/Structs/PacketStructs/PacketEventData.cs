using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// Details of events that happen during the course of the race.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketEventData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Event string code:
        /// "SSTA" -> Sent when the session starts
        /// "SEND" -> Sent when the session ends
        /// “FTLP” -> When a driver achieves the fastest lap
        /// "RTMT" -> When a driver retires
        /// "DRSE" -> Race control have enabled DRS
        /// "DRSD" -> Race control have disabled DRS
        /// "TMPT" -> Your team mate has entered the pits
        /// "CHQF" -> The chequered flag has been waved
        /// "RCWN" -> The race winner is announced
        /// "PENA" -> A penalty has been issued – details in event
        /// "SPTP" -> Speed trap has been triggered by fastest speed
        /// "STLG" -> Start lights – number shown
        /// "LGOT" -> Lights out
        /// "DTSV" -> Drive through penalty served
        /// "SGSV" -> Stop go penalty served
        /// "FLBK" -> Flashback activated
        /// "BUTN" -> Button status changed
        /// "RDFL" -> Red flag shown
        /// "OVTK" -> Overtake occurred
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] m_eventStringCode;
    }
}
