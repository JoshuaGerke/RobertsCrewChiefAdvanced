using ksBroadcastingNetwork.Structs;

namespace ksBroadcastingTestClient.Broadcasting
{
    public class TrackViewModel
    {
        public int TrackId { get; private set; }
        public float TrackMeters { get; private set; }
        public string TrackName { get; private set; }

        public TrackViewModel(int trackId, string trackName, float trackMeters)
        {
            TrackId = trackId;
            TrackName = trackName;
            TrackMeters = trackMeters;
        }

        internal void Update(TrackData trackUpdate)
        {
            TrackId = trackUpdate.TrackId;
            TrackName = trackUpdate.TrackName;
            TrackMeters = trackUpdate.TrackMeters;
        }
    }
}
