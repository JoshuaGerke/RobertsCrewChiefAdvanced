using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CrewChiefV4.Dirt
{
    public class DirtUDPreader : GameDataReader
    {
        private long packetRateCheckInterval = 1000;
        private long packetCountAtStartOfNextRateCheck = 0;
        private long ticksAtStartOfCurrentPacketRateCheck = 0;

        int packetCount = 0;

        private Boolean newSpotterData = true;
        private Boolean running = false;
        private Boolean initialised = false;
        private List<DirtStructWrapper> dataToDump;
        private DirtStructWrapper workingData = new DirtStructWrapper();
        private DirtStructWrapper[] dataReadFromFile = null;
        private int dataReadFromFileIndex = 0;
        private int udpPort = Game.DIRT ? UserSettings.GetUserSettings().getInt("dirt_rally_udp_data_port") : UserSettings.GetUserSettings().getInt("dirt_rally_2_udp_data_port");
        
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
                SerializeObject(dataToDump.ToArray<DirtStructWrapper>(), filenameToDump);
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
                dataReadFromFile = DeSerializeObject<DirtStructWrapper[]>(filePathResolved);
                lastReadFileName = filename;
                Thread.Sleep(pauseBeforeStart);
            }
            if (dataReadFromFile != null && dataReadFromFile.Length > dataReadFromFileIndex)
            {
                DirtStructWrapper structWrapperData = dataReadFromFile[dataReadFromFileIndex];
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
                    dataToDump = new List<DirtStructWrapper>();
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
            DirtStructWrapper latestData = workingData.CreateCopy(DateTime.UtcNow.Ticks, forSpotter);
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

        private int readFromOffset(int offset, byte[] rawData)
        {
            // for packets of type Motion:
            // lapDistance is System.BitConverter.ToSingle(rawData, 8)
            // trackLength is System.BitConverter.ToSingle(rawData, 244)
            this.workingData.dirtData.time = BitConverter.ToSingle(rawData, 0);
            this.workingData.dirtData.currentStageTime = BitConverter.ToSingle(rawData, 4);
            this.workingData.dirtData.lapDistance = BitConverter.ToSingle(rawData, 8);
            this.workingData.dirtData.worldX = BitConverter.ToSingle(rawData, 16);
            this.workingData.dirtData.worldY = BitConverter.ToSingle(rawData, 20);
            this.workingData.dirtData.worldZ = BitConverter.ToSingle(rawData, 24);
            this.workingData.dirtData.speed = BitConverter.ToSingle(rawData, 28);
            this.workingData.dirtData.stageLength = BitConverter.ToSingle(rawData, 244);
            this.workingData.dirtData.trackNumber = BitConverter.ToInt32(rawData, 272);
            return 0;
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
