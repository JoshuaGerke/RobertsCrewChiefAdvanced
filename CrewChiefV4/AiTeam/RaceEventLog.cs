using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>RRCA 2.0 (4.2): Lebenslauf eines Ereignisses am Funk.</summary>
    internal enum EventStatus { Detected, Deferred, Reported, Stale, Dropped }

    /// <summary>RRCA 2.0 (3): Herkunft eines Werts. Gemessen vor Spielereignis vor berechnet vor interpoliert vor geschätzt.</summary>
    internal enum DataOrigin { Measured, SimulatorReportedEvent, Calculated, Interpolated, Estimated, Unavailable, Stale }

    /// <summary>Ein Eintrag im Ereignisgedächtnis: was, wann, warum, von wem gemeldet oder warum nicht.</summary>
    internal sealed class CrewEventRecord
    {
        public string EventId, SemanticKey, Topic, Evidence, Reason;
        public int SessionId, Priority;
        public TeamMember ReportedBy;
        public DateTime DetectedAt, UpdatedAt;
        public DateTime? ReportedAt;
        public EventStatus Status;
    }

    /// <summary>
    /// RRCA 2.0 (4.2): Ereignisgedächtnis der Crew. Jede lokale Ansage wird mit ihrer Ereignis-ID, ihrem Beleg und ihrem
    /// Status geführt (erkannt → gemeldet / zurückgestellt / veraltet / verworfen, mit Grund). Begrenzt auf die letzten 256
    /// Ereignisse je Session. Das Sprachmodell sieht daraus, was die Crew schon gemeldet hat, damit es denselben Vorfall nicht
    /// als neue Information erzählt. Ändert keine Funkentscheidung.
    /// </summary>
    internal sealed class RaceEventLog
    {
        internal const int Capacity = 256;
        private readonly object gate = new object();
        private readonly LinkedList<CrewEventRecord> records = new LinkedList<CrewEventRecord>();
        private readonly Dictionary<string, LinkedListNode<CrewEventRecord>> byId = new Dictionary<string, LinkedListNode<CrewEventRecord>>(StringComparer.Ordinal);

        internal void Clear() { lock (gate) { records.Clear(); byId.Clear(); } }

        internal int Count { get { lock (gate) return records.Count; } }

        internal void Note(ObservedRaceCall call, EventStatus status, DateTime now, string reason = null)
        {
            if (call == null || string.IsNullOrEmpty(call.EventId)) return;
            lock (gate)
            {
                if (!byId.TryGetValue(call.EventId, out var node))
                {
                    var r = new CrewEventRecord
                    {
                        EventId = call.EventId, SemanticKey = call.SemanticKey ?? StatedKey(call), Topic = call.Topic, Evidence = call.Evidence,
                        SessionId = call.SessionId, Priority = call.Priority, DetectedAt = call.DetectedUtc == DateTime.MinValue ? now : call.DetectedUtc
                    };
                    node = records.AddLast(r);
                    byId[call.EventId] = node;
                    while (records.Count > Capacity) { byId.Remove(records.First.Value.EventId); records.RemoveFirst(); }
                }
                var rec = node.Value;
                // Gemeldet ist endgültig; ein späteres „verworfen“ desselben Objekts (Wiederholversuch) überschreibt das nicht.
                if (rec.Status == EventStatus.Reported && status != EventStatus.Reported) return;
                rec.Status = status; rec.UpdatedAt = now; rec.Reason = reason;
                if (status == EventStatus.Reported) { rec.ReportedAt = now; rec.ReportedBy = call.Member; }
            }
        }

        internal static string StatedKey(ObservedRaceCall call) => call != null && call.StatedPosition > 0 ? "eigener-platz|" + call.StatedPosition : null;

        internal CrewEventRecord[] Recent(int sessionId, int count, EventStatus? status = null)
        {
            lock (gate)
                return records.Where(r => r.SessionId == sessionId && (status == null || r.Status == status))
                    .Reverse().Take(Math.Max(0, count)).ToArray();
        }

        /// <summary>Für das Sprachmodell: die zuletzt gemeldeten Crew-Ereignisse (Thema, Ereignis-ID, wer, wann).</summary>
        internal JArray DescribeReported(int sessionId, DateTime now, int count = 10)
        {
            var a = new JArray();
            foreach (var r in Recent(sessionId, count, EventStatus.Reported))
                a.Add(new JObject
                {
                    ["thema"] = r.Topic, ["ereignis"] = r.SemanticKey, ["von"] = r.ReportedBy.ToString(),
                    ["vor_s"] = r.ReportedAt.HasValue ? (JToken)Math.Round((now - r.ReportedAt.Value).TotalSeconds) : null
                });
            return a;
        }
    }
}
