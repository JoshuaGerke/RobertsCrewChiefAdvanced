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
        private readonly Dictionary<string, DateTime> said = new Dictionary<string, DateTime>(StringComparer.Ordinal);
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

        /// <summary>Sicherheit, Warnungen, Flaggen und Spotter laufen nie über das Gedächtnis.</summary>
        internal static bool Exempt(ObservedRaceCall call) =>
            call == null || call.Priority >= 6 || call.Member == TeamMember.Sara ||
            (call.Topic ?? "").StartsWith("warning-", StringComparison.Ordinal) || (call.Topic ?? "").StartsWith("race-hazard", StringComparison.Ordinal) ||
            (call.Topic ?? "").StartsWith("flag", StringComparison.Ordinal) || (call.Topic ?? "").StartsWith("spotter", StringComparison.Ordinal);

        internal bool SaidRecently(ObservedRaceCall call, DateTime now)
        {
            if (Exempt(call)) return false;
            string key = Normalize(call.Text);
            if (key == null) return false;
            lock (gate) return said.TryGetValue(key, out var when) && now >= when && now - when < Window;
        }

        internal void Remember(ObservedRaceCall call, DateTime now)
        {
            string key = call == null ? null : Normalize(call.Text);
            if (key == null) return;
            lock (gate)
            {
                if (said.Count > 200)
                    foreach (var old in said.Where(p => now - p.Value > Window).Select(p => p.Key).ToArray()) said.Remove(old);
                said[key] = now;
            }
        }

        internal void Clear() { lock (gate) said.Clear(); }
    }
}
