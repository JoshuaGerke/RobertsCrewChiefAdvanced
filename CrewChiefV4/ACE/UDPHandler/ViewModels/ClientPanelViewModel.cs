using CrewChiefV4;

using ksEvoBroadcastingNetwork;

using System;

namespace ksEvoBroadcastingTestClient.ClientConnections
{
    internal class ClientPanelViewModel
    {
        public string IP { get; private set; }
        public int Port { get; private set; } = UserSettings.GetUserSettings().getInt("ace_udp_data_port");
        public string DisplayName { get; private set; }
        public string ConnectionPw { get; private set; }
        public string CommandPw { get; private set; }
        public int RealtimeUpdateIntervalMS { get; private set; }
        public ClientConnectionViewModel ClientConnection { get; private set; }
        public Action<ACEUdpRemoteClient> OnClientConnectedCallback { get; }

        public ClientPanelViewModel(string udpIpAddress, Action<ACEUdpRemoteClient> onClientConnectedCallback)
        {
            IP = udpIpAddress;
            DisplayName = "Your name";
            ConnectionPw = "asd";
            CommandPw = "";
            RealtimeUpdateIntervalMS = 250;
            OnClientConnectedCallback = onClientConnectedCallback;

            var c = new ACEUdpRemoteClient(IP, Port, DisplayName, ConnectionPw, CommandPw, RealtimeUpdateIntervalMS);

            ClientConnection = new ClientConnectionViewModel(c, OnClientConnectedCallback);
            Log.Commentary($"Initialised ACC UDP listener on {IP}:{Port}");
        }

        internal void RequestEntryList()
        {
            ClientConnection.Client.RequestEntryList();
        }

        internal void Shutdown()
        {
            if (ClientConnection != null)
            {
                ClientConnection.Client.Shutdown();
                ClientConnection = null;
            }
        }
    }
}
