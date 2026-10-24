using F12019UdpNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrewChiefV4.F1_2019
{
    public class F12019UDPreader : GameDataReader
    {
        private long packetRateCheckInterval = 1000;
        private long packetCountAtStartOfNextRateCheck = 0;
        private long ticksAtStartOfCurrentPacketRateCheck = 0;

        int packetCount = 0;

        private Boolean newSpotterData = true;
        private Boolean running = false;
        private Boolean initialised = false;
        private List<F12019StructWrapper> dataToDump;
        private F12019StructWrapper workingData = new F12019StructWrapper();
        private F12019StructWrapper[] dataReadFromFile = null;
        private int dataReadFromFileIndex = 0;
        private readonly int udpPort = UserSettings.GetUserSettings().getInt("f1_2019_udp_data_port");

        private byte[] receivedDataBuffer;

        private IPEndPoint broadcastAddress;
        private UdpClient udpClient;

        private String lastReadFileName = null;

        private AsyncCallback socketCallback;

        private static Boolean[] buttonsState = new Boolean[32];

        public override void DumpRawGameData()
        {
            if (dumpToFile && dataToDump != null && dataToDump.Count > 0 && filenameToDump != null)
            {
                SerializeObject(dataToDump.ToArray<F12019StructWrapper>(), filenameToDump);
            }
        }

        public override void ResetGameDataFromFile()
        {
            dataReadFromFileIndex = 0;
        }

        public override TracedGameData ReadGameDataFromFile(String filename, int pauseBeforeStart)
        {
            if (dataReadFromFile == null || filename != lastReadFileName)
            {
                dataReadFromFileIndex = 0;
                var filePathResolved = Utilities.ResolveDataFile(this.dataFilesPath, filename);
                dataReadFromFile = DeSerializeObject<F12019StructWrapper[]>(filePathResolved);
                lastReadFileName = filename;
                Thread.Sleep(pauseBeforeStart);
            }
            if (dataReadFromFile != null && dataReadFromFile.Length > dataReadFromFileIndex)
            {
                F12019StructWrapper structWrapperData = dataReadFromFile[dataReadFromFileIndex];
                workingData = structWrapperData;
                newSpotterData = true;
                dataReadFromFileIndex++;
                return new TracedGameData
                {
                    GameData = structWrapperData, 
                    TicksWhenRead = structWrapperData.ticksWhenRead
                };
            }
            else
            {
                return null;
            }
        }

        protected override Boolean InitialiseInternal()
        {
            if (!this.initialised)
            {
                socketCallback = new AsyncCallback(ReceiveCallback);
                packetCount = 0;
                packetCountAtStartOfNextRateCheck = packetRateCheckInterval;
                ticksAtStartOfCurrentPacketRateCheck = DateTime.UtcNow.Ticks;

                if (dumpToFile)
                {
                    dataToDump = new List<F12019StructWrapper>();
                }
                this.broadcastAddress = new IPEndPoint(IPAddress.Any, udpPort);
                this.udpClient = new UdpClient();
                this.udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                this.udpClient.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.
                this.udpClient.Client.Bind(this.broadcastAddress);
                this.receivedDataBuffer = new byte[this.udpClient.Client.ReceiveBufferSize];
                this.running = true;
                this.udpClient.Client.BeginReceive(this.receivedDataBuffer, 0, this.receivedDataBuffer.Length, SocketFlags.None, ReceiveCallback, this.udpClient.Client);
                this.initialised = true;
                Console.WriteLine("Listening for UDP data on port " + udpPort);
            }
            return this.initialised;
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            //Socket was the passed in as the state
            try
            {
                Socket socket = (Socket)result.AsyncState;
                int received = socket.EndReceive(result);
                if (received > 0)
                {
                    // do something with the data
                    lock (this)
                    {
                        packetCount++;
                        try
                        {
                            readFromOffset(0, this.receivedDataBuffer);
                        }
                        catch (Exception e)
                        {
                            Log.Exception(e, "Error reading UDP data ");
                        }
                    }
                }
                if (running)
                {
                    // socket.BeginReceive(this.receivedDataBuffer, 0, this.receivedDataBuffer.Length, SocketFlags.None, new AsyncCallback(ReceiveCallback), socket);
                    socket.BeginReceive(this.receivedDataBuffer, 0, this.receivedDataBuffer.Length, SocketFlags.None, socketCallback, socket);
                }
            }
            catch (Exception e)
            {
                this.initialised = false;
                if (e is ObjectDisposedException || e is SocketException)
                {
                    Console.WriteLine("Socket is closed");
                    return;
                }
                throw;
            }
        }

        public override Object ReadGameData(Boolean forSpotter)
        {
            F12019StructWrapper latestData = workingData.CreateCopy(DateTime.UtcNow.Ticks, forSpotter);
            lock (this)
            {
                if (!initialised)
                {
                    if (!InitialiseInternal())
                    {
                        throw new GameDataReadException("Failed to initialise UDP client");
                    }
                }
                if (forSpotter)
                {
                    newSpotterData = false;
                }
            }
            if (!forSpotter && currentlyTracingGameData && dataToDump != null && workingData != null /* && latestData has some sane data?*/)
            {
                dataToDump.Add(latestData);
            }
            return latestData;
        }

        private int getFrameLength(e_PacketId packetId)
        {
            switch (packetId)
            {
                case e_PacketId.Motion:
                    return 1343;
                case e_PacketId.Session:
                    return 149;
                case e_PacketId.LapData:
                    return 843;
                case e_PacketId.Event:
                    return 32;
                case e_PacketId.Participants:
                    return 1104;
                case e_PacketId.CarSetups:
                    return 843;
                case e_PacketId.CarTelemetry:
                    return 1347;
                case e_PacketId.CarStatus:
                    return 1143;
            }
            return -1;
        }

        private int readFromOffset(int offset, byte[] rawData)
        {
            e_PacketId packetId = (e_PacketId) rawData[5];
            int frameLength = getFrameLength(packetId);
            if (frameLength > 0)
            {
                GCHandle handle = GCHandle.Alloc(rawData.Skip(offset).Take(frameLength).ToArray(), GCHandleType.Pinned);
                try
                {
                    switch (packetId)
                    {
                        case e_PacketId.CarSetups:
                            workingData.packetCarSetupData = (PacketCarSetupData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketCarSetupData));
                            break;
                        case e_PacketId.CarStatus:
                            workingData.packetCarStatusData = (PacketCarStatusData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketCarStatusData));
                            break;
                        case e_PacketId.CarTelemetry:
                            workingData.packetCarTelemetryData = (PacketCarTelemetryData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketCarTelemetryData));
                            buttonsState = ConvertBytesToBoolArray(workingData.packetCarTelemetryData.m_buttonStatus);
                            break;
                        case e_PacketId.Event:
                            var tempEventData = (PacketEventData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketEventData));
                            workingData.packetEventData = MarshalEventData(tempEventData, IntPtr.Add(handle.AddrOfPinnedObject(), Marshal.SizeOf(tempEventData)));
                            break;
                        case e_PacketId.LapData:
                            workingData.packetLapData = (PacketLapData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketLapData));
                            break;
                        case e_PacketId.Motion:
                            workingData.packetMotionData = (PacketMotionData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketMotionData));
                            newSpotterData = true;
                            break;
                        case e_PacketId.Participants:
                            workingData.packetParticipantsData = (PacketParticipantsData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketParticipantsData));
                            break;
                        case e_PacketId.Session:
                            workingData.packetSessionData = (PacketSessionData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(PacketSessionData));
                            break;
                    }
                }
                finally
                {
                    handle.Free();
                }
            }
            return frameLength + offset;
        }

        private PacketEventDataWithDetails MarshalEventData(PacketEventData tempEventData, IntPtr pointer)
        {
            var eventTypeString = Encoding.UTF8.GetString(tempEventData.m_eventStringCode);
            switch(eventTypeString)
            {
                case "FTLP":
                    var fastestLapEvent = (FastestLap)Marshal.PtrToStructure(pointer, typeof(FastestLap));
                    return new PacketEventDataWithDetails(tempEventData, fastestLapEvent);
                case "RTMT":
                    var retirementEvent = (Retirement)Marshal.PtrToStructure(pointer, typeof(Retirement));
                    return new PacketEventDataWithDetails(tempEventData, retirementEvent);
                case "TMPT":
                    var teamMateInPitEvent = (TeamMateInPits)Marshal.PtrToStructure(pointer, typeof(TeamMateInPits));
                    return new PacketEventDataWithDetails(tempEventData, teamMateInPitEvent);
                case "RCWN":
                    var raceWinnerEvent = (RaceWinner)Marshal.PtrToStructure(pointer, typeof(RaceWinner));
                    return new PacketEventDataWithDetails(tempEventData, raceWinnerEvent);
                default:
                    return new PacketEventDataWithDetails(tempEventData, null);
            }
        }

        public override void Dispose()
        {
            if (udpClient != null)
            {
                try
                {
                    if (running)
                    {
                        stop();
                    }
                    udpClient.Close();
                }
                catch (Exception e) {Log.Exception(e);}
            }
            initialised = false;
        }

        public override bool hasNewSpotterData()
        {
            return newSpotterData;
        }

        public override void stop()
        {
            running = false;
            if (udpClient != null && udpClient.Client != null && udpClient.Client.Connected)
            {
                udpClient.Client.Disconnect(true);
            }
            Console.WriteLine("Stopped UDP data receiver, received " + packetCount + " packets");
            this.initialised = false;
            packetCount = 0;
        }

        public static bool[] ConvertBytesToBoolArray(uint buttons)
        {
            bool[] result = new bool[32];
            // check each bit in each of the bytes. if 1 set to true, if 0 set to false
            for (int i = 0; i < 32; i++)
            {
                result[i] = (buttons & (1 << i)) == 0 ? false : true;
            }

            return result;
        }
    }
}
