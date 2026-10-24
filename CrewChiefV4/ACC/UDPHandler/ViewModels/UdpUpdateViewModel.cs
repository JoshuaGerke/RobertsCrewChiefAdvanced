using ksBroadcastingNetwork;
using ksBroadcastingTestClient.Broadcasting;
using ksBroadcastingTestClient.ClientConnections;
using System;
using System.Threading.Tasks;

namespace ksBroadcastingTestClient
{
    internal class UdpUpdateViewModel
    {
        public ClientPanelViewModel ClientPanelVM { get; }
        public BroadcastingViewModel BroadcastingVM { get; }
        public SessionInfoViewModel SessionInfoVM { get; }

        private ACCUdpRemoteClient udpClient;

        public UdpUpdateViewModel(string udpIpAddress)
        {
            ClientPanelVM = new ClientPanelViewModel(udpIpAddress, OnClientConnected);
            BroadcastingVM = new BroadcastingViewModel();
            SessionInfoVM = new SessionInfoViewModel();
        }
        public void OnClientConnected(ACCUdpRemoteClient newClient)
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
