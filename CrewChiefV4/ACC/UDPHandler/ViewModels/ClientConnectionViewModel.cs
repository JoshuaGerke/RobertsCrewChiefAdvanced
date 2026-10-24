using System;
using ksBroadcastingNetwork;

namespace ksBroadcastingTestClient.ClientConnections
{
    internal class ClientConnectionViewModel
    {
        public string IPort => Client.IpPort;
        public ACCUdpRemoteClient Client { get; }
        public Action<ACCUdpRemoteClient> OnClientConnectedCallback { get; }
        public int ConnectionId { get; private set; }
        public bool Connected { get; private set; }
        public bool IsReadonly { get; private set; }
        public string ErrorMessage { get; private set; }

        public ClientConnectionViewModel(ACCUdpRemoteClient c, Action<ACCUdpRemoteClient> onClientConnectedCallback)
        {
            Client = c;
            OnClientConnectedCallback = onClientConnectedCallback;
            c.MessageHandler.OnConnectionStateChanged += ConnectionStateChanged;
        }

        private void ConnectionStateChanged(int connectionId, bool connectionSuccess, bool isReadonly, string error)
        {
            ConnectionId = connectionId;
            Connected = connectionSuccess;
            IsReadonly = IsReadonly;
            ErrorMessage = error;

            if (Connected)
                OnClientConnectedCallback?.Invoke(Client);
        }
    }
}