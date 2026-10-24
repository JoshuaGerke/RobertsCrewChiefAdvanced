using F12022UdpNet;

namespace CrewChiefV4.F1_2022
{
    public class F12022StructWrapper
    {
        public long ticksWhenRead = 0;

        public PacketCarSetupData packetCarSetupData;
        public PacketCarStatusData packetCarStatusData;
        public PacketCarTelemetryData packetCarTelemetryData;
        public PacketEventDataWithDetails packetEventData;
        public PacketLapData packetLapData;
        public PacketMotionData packetMotionData;
        public PacketParticipantsData packetParticipantsData;
        public PacketSessionData packetSessionData;
        public PacketFinalClassificationData packetFinalClassificationData;
        public PacketLobbyInfoData packetLobbyInfoData;
        public PacketCarDamageData packetCarDamageData;
        public PacketSessionHistoryData packetSessionHistoryData;

        public F12022StructWrapper CreateCopy(long ticksWhenCopied, bool forSpotter)
        {
            F12022StructWrapper copy = new F12022StructWrapper
            {
                ticksWhenRead = ticksWhenCopied,
                packetLapData = packetLapData,
                packetSessionData = packetSessionData,
                packetMotionData = packetMotionData,
                packetCarTelemetryData = packetCarTelemetryData,
                packetSessionHistoryData = packetSessionHistoryData,

            };

            if (!forSpotter)
            {
                copy.packetCarSetupData = packetCarSetupData;
                copy.packetCarStatusData = packetCarStatusData;
                copy.packetEventData = packetEventData;
                copy.packetParticipantsData = packetParticipantsData;
                copy.packetFinalClassificationData = packetFinalClassificationData;
                copy.packetLobbyInfoData = packetLobbyInfoData;
                copy.packetCarDamageData = packetCarDamageData;
            }

            return copy;
        }
    }
}