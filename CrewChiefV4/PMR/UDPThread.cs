using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrewChiefV4.PMR
{
    internal class UDPThread
    {
        static IPEndPoint m_endPoint = null;
        static IPAddress m_multicastGroup = null;
        static UdpClient m_udpClient = null;
        static bool m_multiCast = true;
        static int m_defaultPort = 7576;
        static string m_defaultMulticastGroup = "224.0.0.150";
        public static UInt16 m_expectedVersion = 1;

        private DataStore m_dataStore = null;
        private DateTime nextInvalidPacketWarning = DateTime.MinValue;

        public UDPThread(DataStore ds, string[] args)
        {
            m_dataStore = ds;

            m_multiCast = getOptionValue<bool>(args, "multicast", true);
            int port = getOptionValue<int>(args, "port", m_defaultPort);
            string multicastGroup = getOptionValue<string>(args, "multicast_group", m_defaultMulticastGroup);

            if (m_multiCast)
            {
                m_udpClient = new UdpClient(port);
                m_multicastGroup = IPAddress.Parse(multicastGroup);
                m_endPoint = new IPEndPoint(m_multicastGroup, port);
                m_udpClient.JoinMulticastGroup(m_multicastGroup);
            }
            else
            {
                m_endPoint = new IPEndPoint(IPAddress.Any, port);
                m_udpClient = new UdpClient(m_endPoint);
            }
        }

        private T getOptionValue<T>(string[] args, string option, T defaultValue)
        {
            foreach (string s in args)
            {
                if (s.Contains(option))
                {
                    string[] split = s.Split('=');
                    if (split.Length > 1)
                    {
                        return (T)Convert.ChangeType(split[1], typeof(T));
                    }
                    else
                    {
                        return defaultValue;
                    }
                }
            }

            return defaultValue;
        }

        internal static bool SupportedHeader(byte[] data)
        {
            if (data == null || data.Length < 3) return false;
            ushort version = BitConverter.ToUInt16(data, 1);
            return data[0] == (byte)UDPPacketType.RaceInfo && version == UDPRaceInfo.m_expectedPacketVersion ||
                data[0] == (byte)UDPPacketType.ParticipantRaceState && version == UDPParticipantRaceState.m_expectedPacketVersion ||
                data[0] == (byte)UDPPacketType.ParticipantVehicleTelemetry && version == UDPVehicleTelemetry.m_expectedPacketVersion;
        }

        private void decodePacket(ref byte[] data)
        {
            if (!SupportedHeader(data)) { WarnInvalidPacket("unsupported or incomplete header"); return; }
            byte packetType = data[0];
            if (packetType == (byte)UDPPacketType.RaceInfo)
            {
                UDPRaceInfo p = UDPRaceInfo.decode(ref data, 1);
                m_dataStore.writeRaceInfo(p);
            }
            else if (packetType == (byte)UDPPacketType.ParticipantRaceState)
            {
                UDPParticipantRaceState p = UDPParticipantRaceState.decode(ref data, 1);
                m_dataStore.writeRaceState(p);
            }
            else if (packetType == (byte)UDPPacketType.ParticipantVehicleTelemetry)
            {
                UDPVehicleTelemetry p = UDPVehicleTelemetry.decode(ref data, 1);
                m_dataStore.writeTelemetry(p);
            }

            m_dataStore.writeTimestamp();
        }

        private void WarnInvalidPacket(string reason)
        {
            if (DateTime.UtcNow < nextInvalidPacketWarning) return;
            nextInvalidPacketWarning = DateTime.UtcNow.AddSeconds(10);
            Console.WriteLine("PMR: telemetry packet ignored (" + reason + ").");
        }

        private void doWork(object thread)
        {
            // Create an endpoint to store the sender's address
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);

            while (!m_aborted)
            {
                // Receive data from a client
                try
                {
                    byte[] data = m_udpClient.Receive(ref sender);
                    decodePacket(ref data);
                }
                catch (SocketException) {
                    Console.WriteLine("UDPClient Closed");
                }
                catch (Exception e) when (e is ArgumentException || e is IndexOutOfRangeException || e is OverflowException)
                {
                    WarnInvalidPacket(e.GetType().Name);
                }
            }
        }

        public void startThread()
        {
            m_updateThread = new Thread(new ParameterizedThreadStart(doWork));
            m_updateThread.Start(this);
        }

        public void shutdown(Object sender, EventArgs e)
        {
            m_aborted = true;
            m_udpClient.Close();
        }

        private bool m_aborted = false;
        private Thread m_updateThread;
    }
}
