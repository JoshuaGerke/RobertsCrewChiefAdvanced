using ksBroadcastingNetwork;
using ksBroadcastingNetwork.Structs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ksBroadcastingTestClient.Broadcasting
{
    internal class BroadcastingViewModel
    {
        public List<CarViewModel> Cars { get; set; } = new List<CarViewModel>();
        public TrackViewModel TrackVM { get; set; }
        public BroadcastingEventViewModel EventVM { get; } = new BroadcastingEventViewModel();

        private List<BroadcastingNetworkProtocol> _clients = new List<BroadcastingNetworkProtocol>();

        public BroadcastingViewModel()
        {
        }

        internal void RegisterNewClient(ACCUdpRemoteClient newClient)
        {
            if (newClient.MsRealtimeUpdateInterval > 0)
            {
                // This client will send realtime updates, we should listen
                newClient.MessageHandler.OnConnectionStateChanged += MessageHandler_OnConnectionStateChanged; ;
                newClient.MessageHandler.OnTrackDataUpdate += MessageHandler_OnTrackDataUpdate;
                newClient.MessageHandler.OnEntrylistUpdate += MessageHandler_OnEntrylistUpdate;
                newClient.MessageHandler.OnRealtimeUpdate += MessageHandler_OnRealtimeUpdate;
                newClient.MessageHandler.OnRealtimeCarUpdate += MessageHandler_OnRealtimeCarUpdate;
                newClient.MessageHandler.OnBroadcastingEvent += MessageHandler_OnBroadcastingEvent;
            }

            _clients.Add(newClient.MessageHandler);
        }

        private void MessageHandler_OnConnectionStateChanged(int connectionId, bool connectionSuccess, bool isReadonly, string error)
        {
            System.Diagnostics.Debug.WriteLine("CARS CLEARED: " + DateTime.UtcNow.ToString("HH:mm:ss.fff"));

            Cars.Clear();
        }

        private void MessageHandler_OnTrackDataUpdate(string sender, TrackData trackUpdate)
        {
            if (TrackVM?.TrackId != trackUpdate.TrackId)
            {
                TrackVM = new TrackViewModel(trackUpdate.TrackId, trackUpdate.TrackName, trackUpdate.TrackMeters);
            }

            // The track cams may update in between
            TrackVM.Update(trackUpdate);
        }

        private void MessageHandler_OnEntrylistUpdate(string sender, CarInfo carUpdate)
        {
            CarViewModel vm = Cars.SingleOrDefault(x => x.CarIndex == carUpdate.CarIndex);
            if (vm == null)
            {
                vm = new CarViewModel(carUpdate.CarIndex);
                Cars.Add(vm);
            }

            vm.Update(carUpdate);
        }

        private void MessageHandler_OnRealtimeUpdate(string sender, RealtimeUpdate update)
        {
            try
            {
                if (TrackVM?.TrackMeters > 0)
                {
                    var sortedCars = Cars.OrderBy(x => x.SplinePosition).ToArray();
                    for (int i = 1; i < sortedCars.Length; i++)
                    {
                        var carAhead = sortedCars[i - 1];
                        var carBehind = sortedCars[i];
                        var splineDistance = Math.Abs(carAhead.SplinePosition - carBehind.SplinePosition);
                        while (splineDistance > 1f)
                            splineDistance -= 1f;

                        carBehind.GapFrontMeters = splineDistance * TrackVM.TrackMeters;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void MessageHandler_OnRealtimeCarUpdate(string sender, RealtimeCarUpdate carUpdate)
        {
            var vm = Cars.FirstOrDefault(x => x.CarIndex == carUpdate.CarIndex);
            if (vm == null)
            {
                // Oh, we don't have this car yet. In this implementation, the Network protocol will take care of this
                // so hopefully we will display this car in the next cycles
                return;
            }

            vm.Update(carUpdate);
        }

        private void MessageHandler_OnBroadcastingEvent(string sender, BroadcastingEvent evt)
        {
            EventVM.Add(evt);
        }
    }
}
