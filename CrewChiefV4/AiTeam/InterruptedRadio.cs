using System;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rob 21.11.2026 (Monza): „Du musst das Unterbrochene, was du sagen wolltest, nachliefern. Nicht nur durch den Spotter
    /// abwürgen lassen und dann gar nicht mehr nennen.“ Wird eine Ansage von Sara oder einer Gefahrmeldung abgebrochen, merkt
    /// sich die Crew die letzte unterbrochene Meldung und reicht sie einmal nach, sobald der Funk frei ist – höchstens 25 s später.
    /// Zeitkritisches (Gefahr, Verkehr, Flaggen, Platz, Abstände) wird nicht nachgereicht: das wäre dann veraltet.
    /// </summary>
    internal sealed class InterruptedRadio
    {
        internal static readonly TimeSpan MinPause = TimeSpan.FromSeconds(1.5), MaxAge = TimeSpan.FromSeconds(25);
        private readonly object gate = new object();
        private TeamMember member;
        private string text, topic;
        private DateTime utc = DateTime.MinValue;
        private string lastResumed;
        private int variant;

        private static readonly string[] TimeCritical =
        {
            "race-hazard", "race-traffic", "race-class", "warning-", "traffic-", "spotter", "pit-limit", "pit-exit", "position", "ahead", "behind",
            "rear-pressure", "status", "formation", "race-start", "start-verdict", "qualifying-last", "practice-last", "lap-", "greeting",
            "track-welcome", "near-best", "sector-best", "native-gap-", "resume", "race-disqualified", "pit-finish", "endgame"
        };

        internal static bool Resumable(string topic, TeamMember member)
        {
            if (member == TeamMember.Sara) return false;
            if (string.IsNullOrEmpty(topic)) return true; // Antwort auf Robs Frage
            foreach (var prefix in TimeCritical) if (topic.StartsWith(prefix, StringComparison.Ordinal)) return false;
            return true;
        }

        internal void Note(TeamMember who, string spoken, string what, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(spoken) || !Resumable(what, who)) return;
            lock (gate)
            {
                if (spoken == lastResumed) return; // schon einmal nachgereicht, kein zweites Mal
                member = who; text = spoken; topic = what; utc = now;
            }
        }

        /// <summary>Liefert die nachzureichende Meldung (mit kurzer Einleitung) oder null.</summary>
        internal string TryTake(DateTime now, bool radioFree, out TeamMember who, out string what)
        {
            who = TeamMember.Ben; what = null;
            lock (gate)
            {
                if (text == null) return null;
                if (now < utc || now - utc > MaxAge) { text = null; return null; }
                if (!radioFree || now - utc < MinPause) return null;
                who = member; what = topic;
                string resumed = LeadIn(variant++) + text;
                lastResumed = resumed; text = null;
                return resumed;
            }
        }

        internal void Clear() { lock (gate) { text = null; utc = DateTime.MinValue; } }

        internal static string LeadIn(int v)
        {
            string[] lines = RadioLanguage.Pick(
                new[] { "Noch mal von eben: ", "Kurz nachgereicht: ", "Wo war ich? ", "Zurück zu eben: ", "Was ich sagen wollte: " },
                new[] { "Again from before: ", "Quickly catching up: ", "Where was I? ", "Back to that: ", "What I wanted to say: " },
                new[] { "Lo de antes otra vez: ", "Lo completo rápido: ", "¿Dónde estaba? ", "Volviendo a lo de antes: ", "Lo que quería decir: " });
            return lines[Math.Abs(v) % lines.Length];
        }
    }
}
