using F12023UdpNet;

namespace CrewChiefV4.F1_2023
{
    public class F12023StructWrapper
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
        public PacketTyreSetsData packetTyreSetsData;
        public PacketMotionExData packetMotionExData;

        public F12023StructWrapper CreateCopy(long ticksWhenCopied, bool forSpotter)
        {
            F12023StructWrapper copy = new F12023StructWrapper
            {
                ticksWhenRead = ticksWhenCopied,
                packetLapData = packetLapData,
                packetSessionData = packetSessionData,
                packetMotionData = packetMotionData,
                packetCarTelemetryData = packetCarTelemetryData,
                packetSessionHistoryData = packetSessionHistoryData,
                packetMotionExData = packetMotionExData

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
                copy.packetTyreSetsData = packetTyreSetsData;
            }

            return copy;
        }
    }
}