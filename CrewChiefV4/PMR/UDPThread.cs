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

        private void decodePacket(ref byte[] data)
        {
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
