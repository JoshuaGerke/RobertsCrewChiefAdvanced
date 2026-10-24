using CrewChiefV4;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ksBroadcastingNetwork
{
    public class ACCUdpRemoteClient : IDisposable
    {
        private UdpClient _client;
        
        public BroadcastingNetworkProtocol MessageHandler { get; }
        public string IpPort { get; }
        public string DisplayName { get; }
        public string ConnectionPassword { get; }
        public string CommandPassword { get; }
        public int MsRealtimeUpdateInterval { get; }

        readonly int Port;
        readonly string IpAddress;
        readonly static object udpLock = new object();
        readonly static SemaphoreSlim udpTaskSemaphore = new SemaphoreSlim(1, 1);
        bool allowRun;
        
        /// <summary>
        /// To get the events delivered inside the UI thread, just create this object from the UI thread/synchronization context.
        /// </summary>
        public ACCUdpRemoteClient(string ip, int port, string displayName, string connectionPassword, string commandPassword, int msRealtimeUpdateInterval)
        {
            Port = port;
            IpAddress = ip;

            IpPort = $"{ip}:{port}";
            MessageHandler = new BroadcastingNetworkProtocol(IpPort, Send);

            DisplayName = displayName;
            ConnectionPassword = connectionPassword;
            CommandPassword = commandPassword;
            MsRealtimeUpdateInterval = msRealtimeUpdateInterval;

            ConnectAndRun();
        }

        private UdpClient GetUdpClient(bool isSending = false)
        {
            lock(udpLock)
            { 
                if (_client == null || !_client.Client.Connected)
                {
                    if (_client != null)
                    {
                        try { _client.Close(); } catch (Exception e) { Log.Exception(e); }
                    }

                    _client = new UdpClient();
                    _client.Connect(IpAddress, Port);

                    if(!isSending)
                        MessageHandler.RequestConnection(DisplayName, ConnectionPassword, MsRealtimeUpdateInterval, CommandPassword);
                }

                return _client;
            }
        }

        private void Send(byte[] payload)
        {
            UdpClient client = GetUdpClient(true);

            client.Send(payload, payload.Length);
        }

        internal void Shutdown()
        {
            allowRun = false;

            if (_client != null)
            {
                try { _client.Close(); } catch (Exception e) { Log.Exception(e); }
            }
        }

        internal void RequestEntryList()
        {
            MessageHandler.RequestEntryList();
        }

        private void ConnectAndRun()
        {
            Task.Run(async () =>
            {
                allowRun = true;

                while (allowRun)
                {
                    try
                    {
                        UdpClient client = GetUdpClient();

                        if (!client.Client.Connected)
                        {
                            await Task.Delay(1000);
                            continue;
                        }

                        var udpReceiveTask = client.ReceiveAsync();

                        // While hokey, it usually takes 0 milliseconds so this should be plenty
                        if(!udpReceiveTask.IsCompleted)
                            udpReceiveTask.Wait(20000);

                        if (udpReceiveTask.Status == TaskStatus.WaitingForActivation)
                        {
                            try { client.Close(); } catch (Exception e) { Log.Exception(e); }
                            _client = null;
                        }
                        else
                        {
                            UdpReceiveResult udpPacket = udpReceiveTask.Result;

                            if (udpPacket.Buffer.Length == 0)
                                await Task.Delay(10);
                            else
                            {
                                using (var ms = new System.IO.MemoryStream(udpPacket.Buffer))
                                using (var reader = new System.IO.BinaryReader(ms))
                                {
                                    MessageHandler.ProcessMessage(reader);
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            });
        }

        #region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_client != null)
                    {
                        _client.Close();
                        _client = null;
                    }

                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
