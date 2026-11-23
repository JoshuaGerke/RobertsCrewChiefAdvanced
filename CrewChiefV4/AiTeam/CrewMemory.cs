using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Gemeinsames Crew-Gedächtnis (Rob 17.11.2026, Masterprompt C1): Was hat die Crew wann gesagt – über alle Wächter
    /// hinweg. Bisher hatte jeder Wächter nur seine eigene Sperre; kamen zwei Wächter auf denselben Satz, hörte Rob ihn
    /// doppelt (Bathurst: „Rafa Rivera: 2:03,178 …“). Derselbe Satz (ohne Satzzeichen, Groß/klein) wird innerhalb von
    /// vier Minuten nicht wiederholt. Sicherheits- und Warnmeldungen sind ausgenommen: Die müssen immer durch.
    /// </summary>
    internal sealed class CrewMemory
    {
        internal static readonly TimeSpan Window = TimeSpan.FromMinutes(4);
        /// <summary>RRCA 2.0 (6.3): Wie lange ein gemeldeter eigener Platz als „schon gesagt“ gilt.</summary>
        internal static readonly TimeSpan PositionWindow = TimeSpan.FromSeconds(90);
        internal static readonly TimeSpan RoutinePositionWindow = TimeSpan.FromSeconds(20);
        private readonly Dictionary<string, DateTime> said = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> events = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private int reportedPosition, reportedPositionSession = int.MinValue;
        private DateTime reportedPositionUtc = DateTime.MinValue;
        private readonly object gate = new object();

        internal static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var b = new StringBuilder(text.Length);
            foreach (char c in text.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) b.Append(c);
                else if (b.Length > 0 && b[b.Length - 1] != ' ') b.Append(' ');
            string n = b.ToString().Trim();
            return n.Length < 8 ? null : n; // sehr kurze Sätze („Gelb.“) nicht unterdrücken
        }

        /// <summary>
        /// Sicherheit, Warnungen, Flaggen und Spotter laufen nie über das Gedächtnis. Die im Cockpit eingestellten Funkberichte auch
        /// nicht (AMS2 Spa 23.11.: Rennstatus alle 120 s kam fast nie, „Auto in Ordnung“ und Rennfortschritt fielen als „derselbe
        /// Satz“ weg). Ob ein unveränderter Bericht im Modus Normal gebündelt wird, entscheidet allein der Funkplan.
        /// </summary>
        internal static bool Exempt(ObservedRaceCall call) =>
            call == null || call.Priority >= 6 || call.Member == TeamMember.Sara || RaceMind.RoutineReport(call) ||
            (call.Topic ?? "").StartsWith("warning-", StringComparison.Ordinal) || (call.Topic ?? "").StartsWith("race-hazard", StringComparison.Ordinal) ||
            (call.Topic ?? "").StartsWith("flag", StringComparison.Ordinal) || (call.Topic ?? "").StartsWith("spotter", StringComparison.Ordinal);

        internal bool SaidRecently(ObservedRaceCall call, DateTime now)
        {
            if (Exempt(call)) return false;
            string key = Normalize(call.Text);
            if (key == null) return false;
            lock (gate) return said.TryGetValue(key, out var when) && now >= when && now - when < Window;
        }

        /// <summary>
        /// RRCA 2.0 (6.3) Semantische Wiederholsperre: Bringt die Ansage nichts Neues? Gleiche Ereignis-ID binnen vier Minuten,
        /// oder sie nennt nur denselben eigenen Klassenplatz, den die Crew in den letzten 90 s schon gemeldet hat (Mosport 16.11.:
        /// „guter Start, jetzt Platz 14“ und eine Sekunde später „du liegst jetzt auf Position 14“). Sicherheit und Warnungen
        /// sind ausgenommen; ein neuer Platz ist immer neu.
        /// </summary>
        internal bool NothingNew(ObservedRaceCall call, DateTime now, out string reason)
        {
            reason = null;
            if (RaceMind.RoutineReport(call))
            {
                // Mosport 16.11. bleibt gültig: Ein eingestellter Bericht wiederholt den gerade gemeldeten Platz nicht im selben Atemzug.
                lock (gate)
                    if (call.StatedPosition > 0 && call.SessionId == reportedPositionSession && call.StatedPosition == reportedPosition &&
                        now >= reportedPositionUtc && now - reportedPositionUtc < RoutinePositionWindow)
                    { reason = "crew_memory_position_just_reported"; return true; }
                return false;
            }
            if (Exempt(call)) return false;
            lock (gate)
            {
                if (call.SemanticKey != null && events.TryGetValue(call.SemanticKey, out var when) && now >= when && now - when < Window)
                { reason = "crew_memory_same_event"; return true; }
                if (call.StatedPosition > 0 && call.SessionId == reportedPositionSession && call.StatedPosition == reportedPosition &&
                    now >= reportedPositionUtc && now - reportedPositionUtc < PositionWindow)
                { reason = "crew_memory_position_already_reported"; return true; }
            }
            return false;
        }

        internal void Remember(ObservedRaceCall call, DateTime now)
        {
            if (call != null)
                lock (gate)
                {
                    if (call.SemanticKey != null)
                    {
                        if (events.Count > 200)
                            foreach (var old in events.Where(p => now - p.Value > Window).Select(p => p.Key).ToArray()) events.Remove(old);
                        events[call.SemanticKey] = now;
                    }
                    if (call.StatedPosition > 0)
                    { reportedPosition = call.StatedPosition; reportedPositionSession = call.SessionId; reportedPositionUtc = now; }
                }
            string key = call == null ? null : Normalize(call.Text);
            if (key == null) return;
            lock (gate)
            {
                if (said.Count > 200)
                    foreach (var old in said.Where(p => now - p.Value > Window).Select(p => p.Key).ToArray()) said.Remove(old);
                said[key] = now;
            }
        }

        internal void Clear() { lock (gate) { said.Clear(); events.Clear(); reportedPosition = 0; reportedPositionSession = int.MinValue; reportedPositionUtc = DateTime.MinValue; } }
    }
}
