using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Bilanz der eingestellten Funkberichte je Thema (Rob 23.11.: „das geht seit gefühlt 10 Tagen nicht mehr richtig“).
    /// Zählt je Session, wie oft ein Bericht fällig war, wie oft er gesprochen wurde, wie spät und warum er wartete.
    /// Die Live-Diagnose schreibt alle 5 Minuten und beim Sessionwechsel eine Zeile „routine.summary“ je Thema, damit nach
    /// jedem Rennen schwarz auf weiß steht, ob die Berichte im eingestellten Fenster kamen.
    /// </summary>
    internal sealed class RoutineLedger
    {
        /// <summary>So spät darf ein Bericht nach seinem geplanten Zeitpunkt kommen und gilt noch als pünktlich (Zweikampf hält bis 8 s).</summary>
        internal const double OnTimeSeconds = 10;
        internal static readonly TimeSpan SummaryEvery = TimeSpan.FromMinutes(5);

        internal sealed class Topic
        {
            public int Due, Spoken, OnTime;
            public double MaxLateSeconds, MaxGapSeconds;
            public DateTime LastSpokenUtc = DateTime.MinValue;
            public readonly Dictionary<string, int> Reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private readonly object gate = new object();
        private readonly Dictionary<string, Topic> topics = new Dictionary<string, Topic>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> intervals = new Dictionary<string, int>(StringComparer.Ordinal);
        private int session = int.MinValue;
        private DateTime lastSummaryUtc = DateTime.MinValue;

        internal static RoutineLedger Current { get; } = new RoutineLedger();

        internal static bool IsRoutineTopic(string topic) => topic != null && AiRadioPreferences.Options.Any(o => o.Id == topic);

        private Topic For(string topic)
        {
            if (!topics.TryGetValue(topic, out var t)) topics[topic] = t = new Topic();
            return t;
        }

        /// <summary>Neue Session: alte Bilanz ausgeben und neu beginnen.</summary>
        internal void Session(int sessionId, DateTime now)
        {
            bool changed;
            lock (gate) changed = sessionId != session;
            if (!changed) return;
            Flush(now);
            lock (gate) { topics.Clear(); session = sessionId; lastSummaryUtc = now; }
        }

        internal void Due(string topic, int intervalSeconds)
        {
            lock (gate) { For(topic).Due++; intervals[topic] = intervalSeconds; }
        }

        internal void Spoken(string topic, DateTime dueUtc, DateTime startedUtc)
        {
            lock (gate)
            {
                var t = For(topic);
                t.Spoken++;
                double late = dueUtc == DateTime.MinValue ? 0 : Math.Max(0, (startedUtc - dueUtc).TotalSeconds);
                if (late <= OnTimeSeconds) t.OnTime++;
                t.MaxLateSeconds = Math.Max(t.MaxLateSeconds, late);
                if (t.LastSpokenUtc != DateTime.MinValue) t.MaxGapSeconds = Math.Max(t.MaxGapSeconds, (startedUtc - t.LastSpokenUtc).TotalSeconds);
                t.LastSpokenUtc = startedUtc;
            }
        }

        internal void Reason(string topic, string reason)
        {
            if (string.IsNullOrEmpty(reason) || !IsRoutineTopic(topic)) return;
            lock (gate)
            {
                var r = For(topic).Reasons;
                r[reason] = r.TryGetValue(reason, out int n) ? n + 1 : 1;
            }
        }

        internal void Tick(DateTime now)
        {
            bool due;
            lock (gate) due = session != int.MinValue && (lastSummaryUtc == DateTime.MinValue || now - lastSummaryUtc >= SummaryEvery);
            if (!due) return;
            Flush(now);
            lock (gate) lastSummaryUtc = now;
        }

        /// <summary>Für Tests und Diagnose: Bilanz eines Themas (Kopie).</summary>
        internal Topic Snapshot(string topic)
        {
            lock (gate)
            {
                if (!topics.TryGetValue(topic, out var t)) return new Topic();
                var copy = new Topic { Due = t.Due, Spoken = t.Spoken, OnTime = t.OnTime, MaxLateSeconds = t.MaxLateSeconds, MaxGapSeconds = t.MaxGapSeconds, LastSpokenUtc = t.LastSpokenUtc };
                foreach (var p in t.Reasons) copy.Reasons[p.Key] = p.Value;
                return copy;
            }
        }

        internal static string Reasons(Topic t) =>
            string.Join(",", t.Reasons.OrderByDescending(p => p.Value).Take(6).Select(p => p.Key + ":" + p.Value.ToString(CultureInfo.InvariantCulture)));

        private void Flush(DateTime now)
        {
            if (!LiveDiagnostics.Enabled) return;
            List<KeyValuePair<string, Topic>> rows;
            Dictionary<string, int> iv;
            lock (gate)
            {
                if (session == int.MinValue) return;
                rows = topics.Select(p => new KeyValuePair<string, Topic>(p.Key, Snapshot(p.Key))).ToList();
                iv = new Dictionary<string, int>(intervals);
            }
            foreach (var row in rows.OrderBy(r => r.Key, StringComparer.Ordinal))
            {
                var t = row.Value;
                LiveDiagnostics.Emit("routine.summary", LiveDiagnostics.Data(
                    "topic", row.Key, "interval_s", iv.TryGetValue(row.Key, out int s) ? s : 0, "due_count", t.Due, "spoken_count", t.Spoken,
                    "on_time_count", t.OnTime, "max_late_s", Math.Round(t.MaxLateSeconds, 1), "max_gap_s", Math.Round(t.MaxGapSeconds, 1),
                    "reasons", Reasons(t)));
            }
        }
    }
}
