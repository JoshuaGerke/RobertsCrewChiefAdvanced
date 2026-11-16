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
        private readonly UdpClient m_udpClient;
        private const int DefaultPort = 7576;
        private const string DefaultMulticastGroup = "224.0.0.150";
        public static UInt16 m_expectedVersion = 1;

        private DataStore m_dataStore = null;
        private DateTime nextInvalidPacketWarning = DateTime.MinValue;

        public UDPThread(DataStore ds, string[] args)
        {
            m_dataStore = ds;

            bool multicast = getOptionValue<bool>(args, "multicast", true);
            int port = getOptionValue<int>(args, "port", DefaultPort);
            string group = getOptionValue<string>(args, "multicast_group", DefaultMulticastGroup);
            if (!multicast && getOptionValue<bool>(args, "loopback", false))
            {
                m_udpClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                return;
            }
            m_udpClient = new UdpClient(port);
            try
            {
                if (multicast) m_udpClient.JoinMulticastGroup(IPAddress.Parse(group));
            }
            catch
            {
                m_udpClient.Close();
                throw;
            }
        }

        private T getOptionValue<T>(string[] args, string option, T defaultValue)
        {
            foreach (string s in args)
            {
                if (s.StartsWith(option + "=", StringComparison.Ordinal))
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
                catch (SocketException)
                {
                    if (!m_aborted) WarnInvalidPacket("UDP receive failed");
                    break;
                }
                catch (ObjectDisposedException) when (m_aborted) { break; }
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

        private volatile bool m_aborted = false;
        private Thread m_updateThread;
    }
}
