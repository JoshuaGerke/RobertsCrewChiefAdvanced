using CrewChiefV4;

using ksBroadcastingNetwork;

using System;

namespace ksBroadcastingTestClient.ClientConnections
{
    internal class ClientPanelViewModel
    {
        private string IP { get; set; }
        public static int Port { get; set; } = UserSettings.GetUserSettings().getInt("acc_udp_data_port");
        private string DisplayName { get; set; }
        public static string ConnectionPw { get; set; } = "asd";
        public static string CommandPw { get; set; } = "";

        private int RealtimeUpdateIntervalMS { get; set; }
        private ClientConnectionViewModel ClientConnection { get; set; }
        private Action<ACCUdpRemoteClient> OnClientConnectedCallback { get; }

        public ClientPanelViewModel(string udpIpAddress, Action<ACCUdpRemoteClient> onClientConnectedCallback)
        {
            IP = udpIpAddress;
            DisplayName = "Your name";
            ConnectionPw = "asd";
            CommandPw = "";
            RealtimeUpdateIntervalMS = 250;
            OnClientConnectedCallback = onClientConnectedCallback;

            var c = new ACCUdpRemoteClient(IP, Port, DisplayName, ConnectionPw, CommandPw, RealtimeUpdateIntervalMS);

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
