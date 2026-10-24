using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using F12021UdpNet;

namespace CrewChiefV4.F1_2021
{
    public class F12021StructWrapper
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

        public F12021StructWrapper CreateCopy(long ticksWhenCopied, Boolean forSpotter)
        {
            F12021StructWrapper copy = new F12021StructWrapper();
            copy.ticksWhenRead = ticksWhenCopied;
            copy.packetLapData = this.packetLapData;
            copy.packetSessionData = this.packetSessionData;
            copy.packetMotionData = this.packetMotionData;
            copy.packetCarTelemetryData = this.packetCarTelemetryData;

            if (!forSpotter)
            {
                copy.packetCarSetupData = this.packetCarSetupData;
                copy.packetCarStatusData = this.packetCarStatusData;
                copy.packetEventData = this.packetEventData;
                copy.packetParticipantsData = this.packetParticipantsData;
            }
            return copy;
        }
    }
}