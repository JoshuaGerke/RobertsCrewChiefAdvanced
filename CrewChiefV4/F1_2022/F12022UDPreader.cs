using CrewChiefV4.F1_2022.Utility;
using F12022UdpNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace CrewChiefV4.F1_2022
{
    public class F12022UDPreader : GameDataReader
    {
        private long packetRateCheckInterval = 1000;
        private long packetCountAtStartOfNextRateCheck = 0;
        private long ticksAtStartOfCurrentPacketRateCheck = 0;

        int packetCount = 0;

        private bool hasNewAvailableSpotterData = true;
        private bool isRunning = false;
        private bool isInitialized = false;
        private List<F12022StructWrapper> dataToDump;
        private F12022StructWrapper workingData = new F12022StructWrapper();
        private F12022StructWrapper[] dataReadFromFile = null;
        private int dataReadFromFileIndex = 0;
        private readonly int udpPort = UserSettings.GetUserSettings().getInt("f1_2022_udp_data_port");

        private readonly Dictionary<PacketId, Action<GCHandle>> dataTransformByPacketId;

        private byte[] receivedDataBuffer;

        private UdpClient udpClient;

        private string lastReadFileName = null;

        private AsyncCallback socketCallback;

        public bool ShouldDumpToFile { get => currentlyTracingGameData && dataToDump != null; }

        public static bool[] ConvertBytesToBoolArray(uint buttons)
        {
            bool[] result = new bool[32];

            // check each bit in each of the bytes. if 1 set to true, if 0 set to false
            for (int i = 0; i < 32; i++)
                result[i] = (buttons & (1 << i)) != 0;

            return result;
        }

        #region Constructors

        public F12022UDPreader()
        {
            dataTransformByPacketId = new Dictionary<PacketId, Action<GCHandle>>()
            {
                { PacketId.CarSetups, TransformCarSetups },
                { PacketId.CarStatus, TransformCarStatus },
                { PacketId.CarTelemetry, TransformCarTelemetry },
                { PacketId.Event, TransformEvent },
                { PacketId.LapData, TransformLapData },
                { PacketId.Motion, TransformMotion },
                { PacketId.Participants, TransformParticipants },
                { PacketId.Session, TransformSessionData },
                { PacketId.FinalClassification, TransformFinalClassification },
                { PacketId.LobbyInfo, TransformLobbyInfo },
                { PacketId.CarDamage, TransformCarDamage },
                { PacketId.SessionHistory, TransformSessionHistoryData }
            };
        }

        #endregion

        #region Overridden methods
        public override bool hasNewSpotterData() => hasNewAvailableSpotterData;

        public override void DumpRawGameData()
        {
            var canDumpData = ShouldDumpToFile && dataToDump.Count > 0 && filenameToDump != null;
            if (!canDumpData) return;

            SerializeObject(dataToDump.ToArray<F12022StructWrapper>(), filenameToDump);
        }

        public override void ResetGameDataFromFile()
        {
            dataReadFromFileIndex = 0;
        }

        public override TracedGameData ReadGameDataFromFile(string filename, int pauseBeforeStart)
        {
            var shouldReadNewFile = dataReadFromFile == null || filename != lastReadFileName;
            if (shouldReadNewFile)
            {
                ResetGameDataFromFile();
                var filePathResolved = Utilities.ResolveDataFile(dataFilesPath, filename);
                dataReadFromFile = DeSerializeObject<F12022StructWrapper[]>(filePathResolved);
                lastReadFileName = filename;
                Thread.Sleep(pauseBeforeStart);
            }

            var isReading = dataReadFromFile != null && dataReadFromFile.Length > dataReadFromFileIndex;
            if (!isReading) return null;

            F12022StructWrapper structWrapperData = dataReadFromFile[dataReadFromFileIndex];
            workingData = structWrapperData;
            hasNewAvailableSpotterData = true;
            dataReadFromFileIndex++;
            return new TracedGameData
            {
                GameData = structWrapperData, 
                TicksWhenRead = structWrapperData.ticksWhenRead
            };
        }

        protected override bool InitialiseInternal()
        {
            if (!isInitialized) Init();
            return isInitialized;
        }

        public override object ReadGameData(bool forSpotter)
        {
            F12022StructWrapper latestData = workingData.CreateCopy(DateTime.UtcNow.Ticks, forSpotter);

            lock (this)
            {
                if (!isInitialized && !InitialiseInternal())
                    throw new GameDataReadException("Failed to initialise UDP client");

                if (forSpotter) hasNewAvailableSpotterData = false;
            }

            var shouldDumpData = !forSpotter && ShouldDumpToFile && workingData != null /* && latestData has some sane data?*/;
            if (shouldDumpData) dataToDump.Add(latestData);

            return latestData;
        }

        public override void Dispose()
        {
            CloseUdpClient();
            isInitialized = false;
        }

        public override void stop()
        {
            // Stop execution
            isRunning = false;

            // Disconnect UDP client
            var isUdpClientConnected = udpClient != null && udpClient.Client != null && udpClient.Client.Connected;
            if (isUdpClientConnected) udpClient.Client.Disconnect(true);

            // Flush and reset
            Console.WriteLine("Stopped UDP data receiver, received " + packetCount + " packets");
            Reset();
        }

        #endregion

        #region Private methods

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                // Process incoming data
                var socket = result.AsyncState as Socket; // Socket was passed in as state
                var receivedBytes = socket.EndReceive(result);
                if (receivedBytes > 0) ReadReceived();

                // If still running continue receiving data
                if (isRunning)
                    socket.BeginReceive(receivedDataBuffer, 0, this.receivedDataBuffer.Length, SocketFlags.None, socketCallback, socket);
            }
            catch (Exception e) when (e is ObjectDisposedException || e is SocketException)
            {
                isInitialized = false;
                Console.WriteLine("Socket is closed");
                return;
            }
            catch
            {
                isInitialized = false;
                throw;
            }
        }

        private int GetFrameLength(PacketId packetId)
        {
            switch (packetId)
            {
                case PacketId.Motion:
                    return 1464;
                case PacketId.Session:
                    return 251;
                case PacketId.LapData:
                    return 1190;
                case PacketId.Event:
                    return 35;
                case PacketId.Participants:
                    return 1213;
                case PacketId.CarSetups:
                    return 1102;
                case PacketId.CarTelemetry:
                    return 1307;
                case PacketId.CarStatus:
                    return 1344;
            }
            return -1;
        }

        private int ReadFromOffset(int offset, byte[] rawData)
        {
            // Fifth byte is always the packet id
            var packetId = (PacketId)rawData[5];

            var frameLength = packetId.GetPacketSize();
            if (frameLength <= 0) return frameLength + offset;
            GCHandle handle = GCHandle.Alloc(rawData.Skip(offset).Take(frameLength).ToArray(), GCHandleType.Pinned);
            try
            {
                // Do stuff with fetched data depending on its packet id
                var transformation = dataTransformByPacketId[packetId];
                transformation(handle);
            }
            finally
            {
                handle.Free();
            }

            return frameLength + offset;
        }

        private PacketEventDataWithDetails MarshalEventData(PacketEventData tempEventData, IntPtr pointer)
        {
            var details = EventDetailFactory.Parse(tempEventData.m_eventStringCode, pointer);
            return new PacketEventDataWithDetails(tempEventData, details);
        }

        private void Init()
        {
            socketCallback = new AsyncCallback(ReceiveCallback);
            packetCount = 0;
            packetCountAtStartOfNextRateCheck = packetRateCheckInterval;
            ticksAtStartOfCurrentPacketRateCheck = DateTime.UtcNow.Ticks;

            if (dumpToFile) dataToDump = new List<F12022StructWrapper>();

            udpClient = ProduceUdpClient();
            receivedDataBuffer = new byte[udpClient.Client.ReceiveBufferSize];
            isRunning = true;
            udpClient.Client.BeginReceive(receivedDataBuffer, 0, receivedDataBuffer.Length, SocketFlags.None, ReceiveCallback, udpClient.Client);
            isInitialized = true;
            Console.WriteLine("Listening for UDP data on port " + udpPort);
        }

        private UdpClient ProduceUdpClient()
        {
            var broadcastAddress = new IPEndPoint(IPAddress.Any, udpPort);
            var client = new UdpClient();
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.ExclusiveAddressUse = false; // only if you want to send/receive on same machine.
            client.Client.Bind(broadcastAddress);

            return client;
        }

        private void ReadReceived()
        {
            lock (this)
            {
                packetCount++;
                try
                {
                    ReadFromOffset(0, receivedDataBuffer);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error reading UDP data ");
                }
            }
        }

        private void CloseUdpClient()
        {
            if (udpClient == null) return;

            try
            {
                if (isRunning) stop();
                udpClient.Close();
            }
            catch (Exception e) { Log.Exception(e); }
        }

        private void Reset()
        {
            isInitialized = false;
            packetCount = 0;
        }

        #region Data transformation

        private void TransformCarSetups(GCHandle handle)
        {
            workingData.packetCarSetupData = handle.PointerCast<PacketCarSetupData>();
        }

        private void TransformCarStatus(GCHandle handle)
        {
            workingData.packetCarStatusData = handle.PointerCast<PacketCarStatusData>();
        }

        private void TransformCarTelemetry(GCHandle handle)
        {
            workingData.packetCarTelemetryData = handle.PointerCast<PacketCarTelemetryData>();
        }

        private void TransformEvent(GCHandle handle)
        {
            var tempEventData = handle.PointerCast<PacketEventData>();
            var detailsPointer = IntPtr.Add(handle.AddrOfPinnedObject(), Marshal.SizeOf(tempEventData));
            workingData.packetEventData = MarshalEventData(tempEventData, detailsPointer);
        }

        private void TransformLapData(GCHandle handle)
        {
            workingData.packetLapData = handle.PointerCast<PacketLapData>();
        }

        private void TransformMotion(GCHandle handle)
        {
            workingData.packetMotionData = handle.PointerCast<PacketMotionData>();
            hasNewAvailableSpotterData = true;
        }

        private void TransformParticipants(GCHandle handle)
        {
            workingData.packetParticipantsData = handle.PointerCast<PacketParticipantsData>();
        }

        private void TransformSessionData(GCHandle handle)
        {
            workingData.packetSessionData = handle.PointerCast<PacketSessionData>();
        }

        private void TransformFinalClassification(GCHandle handle)
        {
            workingData.packetFinalClassificationData = handle.PointerCast<PacketFinalClassificationData>();
        }

        private void TransformLobbyInfo(GCHandle handle)
        {
            workingData.packetLobbyInfoData = handle.PointerCast<PacketLobbyInfoData>();
        }

        private void TransformCarDamage(GCHandle handle)
        {
            workingData.packetCarDamageData = handle.PointerCast<PacketCarDamageData>();
        }

        private void TransformSessionHistoryData(GCHandle handle)
        {
            workingData.packetSessionHistoryData = handle.PointerCast<PacketSessionHistoryData>();
        }

        #endregion

        #endregion
    }
}
