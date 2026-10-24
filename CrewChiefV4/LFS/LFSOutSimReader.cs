using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CrewChiefV4.LFS
{
    /// <summary>
    /// UDP receiver for Live for Speed OutSim telemetry packets.
    /// Reads tyre air temperatures and lap distance when OutSim Opts 1ff is enabled in LFS cfg.txt.
    /// </summary>
    class LFSOutSimReader : IDisposable
    {
        private UdpClient udpClient;
        private Thread listenThread;
        private volatile bool running;

        public byte RL_AirTemp { get; private set; }
        public byte RR_AirTemp { get; private set; }
        public byte FL_AirTemp { get; private set; }
        public byte FR_AirTemp { get; private set; }
        public float CurrentLapDist { get; private set; }
        public DateTime LastPacketReceived { get; private set; } = DateTime.MinValue;

        public bool HasData
        {
            get
            {
                return (DateTime.UtcNow - LastPacketReceived).TotalSeconds < 2.5;
            }
        }

        public void Start()
        {
            if (running)
            {
                return;
            }

            bool enabled = UserSettings.GetUserSettings().getBoolean("lfs_outsim_enabled");
            if (!enabled)
            {
                Console.WriteLine("[LFS OutSim] OutSim telemetry is disabled in settings.");
                return;
            }

            int port = UserSettings.GetUserSettings().getInt("lfs_outsim_port");
            if (port <= 0)
            {
                port = 30000;
            }

            try
            {
                udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                running = true;

                listenThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "LFSOutSimReaderThread"
                };
                listenThread.Start();

                Console.WriteLine($"[LFS OutSim] Listening for OutSim packets on UDP port {port} (Opts 1ff)...");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"[LFS OutSim] Failed to bind UDP port {port}: {ex.Message}");
                running = false;
                if (udpClient != null)
                {
                    try { udpClient.Close(); } catch { }
                    udpClient = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LFS OutSim] Unexpected error starting listener: {ex.Message}");
                running = false;
            }
        }

        private void ListenLoop()
        {
            var remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (running && udpClient != null)
            {
                try
                {
                    byte[] bytes = udpClient.Receive(ref remoteEP);
                    if (bytes != null && bytes.Length > 0)
                    {
                        ParsePacket(bytes);
                    }
                }
                catch (SocketException)
                {
                    // Socket closed on Stop()
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (running)
                    {
                        Console.WriteLine($"[LFS OutSim] Error receiving packet: {ex.Message}");
                    }
                }
            }
        }

        private void ParsePacket(byte[] bytes)
        {
            // OutSim Opts 1ff full packet layout (280 bytes):
            // 0..3:    Header 'LFST' (4 bytes)
            // 4..7:    ID (4 bytes)
            // 8..11:   Time (4 bytes)
            // 12..71:  OSMain (12 floats + 3 ints = 60 bytes)
            // 72..91:  OSInputs (5 floats = 20 bytes)
            // 92..103: OSDrive (4 bytes + 2 floats = 12 bytes)
            // 104..111:OSDistance (CurrentLapDist float + IndexedDistance float = 8 bytes)
            // 112..271:OSWheels (4 wheels * 40 bytes = 160 bytes)
            //          Each wheel (40 bytes):
            //          +0:  SuspDeflect (float)
            //          +4:  Steer (float)
            //          +8:  XForce (float)
            //          +12: YForce (float)
            //          +16: VerticalLoad (float)
            //          +20: AngVel (float)
            //          +24: LeanRelToRoad (float)
            //          +28: AirTemp (byte, °C)
            //          +29: SlipFraction (byte)
            //          +30: Touching (byte)
            //          +31: Sp3 (byte)
            //          +32: SlipRatio (float)
            //          +36: TanSlipAngle (float)
            //          Wheel order: 0 = RL, 1 = RR, 2 = FL, 3 = FR
            // 272..279:OSExtra1 (2 floats = 8 bytes)

            int wheelOffset = -1;
            int distOffset = -1;

            if (bytes.Length >= 280 && bytes[0] == (byte)'L' && bytes[1] == (byte)'F' && bytes[2] == (byte)'S' && bytes[3] == (byte)'T')
            {
                // Standard Opts 1ff with header
                distOffset = 104;
                wheelOffset = 112;
            }
            else if (bytes.Length == 276)
            {
                // Opts 1fe (no header)
                distOffset = 100;
                wheelOffset = 108;
            }
            else if (bytes.Length >= 272 && bytes[0] == (byte)'L' && bytes[1] == (byte)'F' && bytes[2] == (byte)'S' && bytes[3] == (byte)'T')
            {
                // Opts 0xff (with header, without Extra1)
                distOffset = 104;
                wheelOffset = 112;
            }

            if (wheelOffset >= 0 && bytes.Length >= wheelOffset + 160)
            {
                // Wheel 0: Rear Left (RL)
                RL_AirTemp = bytes[wheelOffset + 28];
                // Wheel 1: Rear Right (RR)
                RR_AirTemp = bytes[wheelOffset + 40 + 28];
                // Wheel 2: Front Left (FL)
                FL_AirTemp = bytes[wheelOffset + 80 + 28];
                // Wheel 3: Front Right (FR)
                FR_AirTemp = bytes[wheelOffset + 120 + 28];

                if (distOffset >= 0 && bytes.Length >= distOffset + 4)
                {
                    CurrentLapDist = BitConverter.ToSingle(bytes, distOffset);
                }

                LastPacketReceived = DateTime.UtcNow;
            }
        }

        public void Stop()
        {
            running = false;

            if (udpClient != null)
            {
                try
                {
                    udpClient.Close();
                }
                catch { }
                udpClient = null;
            }

            if (listenThread != null && listenThread.IsAlive)
            {
                try
                {
                    listenThread.Join(500);
                }
                catch { }
                listenThread = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
