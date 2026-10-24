using ksEvoBroadcastingNetwork;
using ksEvoBroadcastingTestClient.Broadcasting;
using ksEvoBroadcastingTestClient.ClientConnections;
using System;
using System.Threading.Tasks;

namespace ksEvoBroadcastingTestClient
{
    internal class UdpUpdateViewModel
    {
        public ClientPanelViewModel ClientPanelVM { get; }
        public BroadcastingViewModel BroadcastingVM { get; }
        public SessionInfoViewModel SessionInfoVM { get; }

        private ACEUdpRemoteClient udpClient;

        public UdpUpdateViewModel(string udpIpAddress)
        {
            ClientPanelVM = new ClientPanelViewModel(udpIpAddress, OnClientConnected);
            BroadcastingVM = new BroadcastingViewModel();
            SessionInfoVM = new SessionInfoViewModel();
        }
        public void OnClientConnected(ACEUdpRemoteClient newClient)
        {
            udpClient = newClient;

            BroadcastingVM.RegisterNewClient(newClient);
            SessionInfoVM.RegisterNewClient(newClient);
        }

        public void Shutdown()
        {
            ClientPanelVM.Shutdown();
        }
    }
}
