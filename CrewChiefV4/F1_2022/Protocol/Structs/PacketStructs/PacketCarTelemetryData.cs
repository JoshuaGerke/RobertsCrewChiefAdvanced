using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// This packet details telemetry for all the cars in the race. 
    /// It details various values that would be recorded on the car such as speed, throttle application, DRS etc. 
    /// Note that the rev light configurations are presented separately as well and will mimic real life driver preferences.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketCarTelemetryData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Telemetry data for every car
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        public CarTelemetryData[] m_carTelemetryData;

        /// <summary>
        /// Index of MFD panel open - 255 = MFD closed. Single player, race – 0 = Car setup, 1 = Pits
        /// </summary>
        public byte m_mfdPanelIndex;

        /// <summary>
        /// See above
        /// </summary>
        public byte m_mfdPanelIndexSecondaryPlayer;


        /// <summary>
        /// Suggested gear for the player (1-8). 0 if no gear suggested
        /// </summary>
        public sbyte m_suggestedGear;
    }
}
