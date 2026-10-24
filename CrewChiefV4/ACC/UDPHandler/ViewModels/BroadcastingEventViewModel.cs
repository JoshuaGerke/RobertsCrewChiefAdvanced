using ksBroadcastingNetwork.Structs;
using System.Collections.Generic;

namespace ksBroadcastingTestClient.Broadcasting
{
    internal class BroadcastingEventViewModel
    {
        private readonly object _Lock = new object();
        private readonly List<BroadcastingEvent> _Events = new List<BroadcastingEvent>();

        public void Add(BroadcastingEvent evt)
        {
            lock (_Lock)
            {
                _Events.Add(evt);
            }
        }

        public BroadcastingEvent[] GetEvents()
        {
            lock (_Lock)
            {
                var events = _Events.ToArray();

                _Events.Clear();

                return events;
            }
        }
    }
}
