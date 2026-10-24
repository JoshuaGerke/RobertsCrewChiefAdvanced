using ksBroadcastingNetwork;
using ksBroadcastingNetwork.Structs;
using System;

namespace ksBroadcastingTestClient.Broadcasting
{
    public class SessionInfoViewModel
    {
        public TimeSpan TimeOfDay { get; private set; }
        public TimeSpan SessionTime { get; private set; }
        public TimeSpan RemainingTime { get; private set; }
        public string TrackDisplayName { get; private set; }
        public SessionPhase Phase { get; private set; }
        public RaceSessionType SessionType { get; private set; }

        public int AmbientTempC { get; private set; }
        public int TrackTempC { get; private set; }
        public float CloudCoverPercent { get; private set; }
        public float RainLevel { get; private set; }
        public float WetnessLevel { get; private set; }

        internal void RegisterNewClient(ACCUdpRemoteClient newClient)
        {
            if (newClient.MsRealtimeUpdateInterval > 0)
            {
                // This client will send realtime updates, we should listen
                newClient.MessageHandler.OnTrackDataUpdate += MessageHandler_OnTrackDataUpdate;
                newClient.MessageHandler.OnRealtimeUpdate += MessageHandler_OnRealtimeUpdate;
            }
        }

        private void MessageHandler_OnRealtimeUpdate(string sender, RealtimeUpdate update)
        {
            SessionTime = update.SessionTime;
            RemainingTime = update.SessionEndTime;

            Phase = update.Phase;
            SessionType = update.SessionType;

            TimeOfDay = update.TimeOfDay;

            CloudCoverPercent = update.Clouds;

            AmbientTempC = update.AmbientTemp;
            TrackTempC = update.TrackTemp;

            RainLevel = update.RainLevel;
            WetnessLevel = update.Wetness;
        }

        private void MessageHandler_OnTrackDataUpdate(string sender, TrackData trackUpdate)
        {
            TrackDisplayName = trackUpdate.TrackName;
        }
    }
}
