using System;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rob 21.11.2026 (Monza): „Du musst das Unterbrochene, was du sagen wolltest, nachliefern. Nicht nur durch den Spotter
    /// abwürgen lassen und dann gar nicht mehr nennen.“ Wird eine Ansage von Sara oder einer Gefahrmeldung abgebrochen, merkt
    /// sich die Crew die letzte unterbrochene Meldung und reicht sie einmal nach, sobald der Funk frei ist – höchstens 25 s später.
    /// Zeitkritisches (Gefahr, Verkehr, Flaggen, Platz, Abstände) wird nicht nachgereicht: das wäre dann veraltet.
    /// AC 21.11. (Monza): „Wo war ich? Alles klar, Rob.“, „Zurück zu eben: du bist raus“ auf dem Startplatz und eine nachgereichte
    /// Bestzeit eines Gegners klangen nach Maschine. Nachgereicht wird deshalb nur noch, was ohne Zeitbezug gilt (Antworten auf
    /// Robs Fragen, Sprit, Stint, Wetter, Technik, eigene Einordnung), mit mindestens sechs Wörtern, und nur in derselben Phase
    /// derselben Session – wechselt Startaufstellung zu Rennen oder Training zu Quali, ist das Unterbrochene erledigt.
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

        /// <summary>Was ohne Zeitbezug gilt und deshalb nachgereicht werden darf (alles andere ist nach 25 s veraltet).</summary>
        private static readonly string[] Lasting =
        {
            "fuel", "stint", "weather", "vehicle", "tyres", "tyre-", "personal-best-brief", "session-debrief", "insight-", "pace",
            "quali-lap-analysis", "coach", "engine-", "damage", "strategy"
        };

        internal const int MinWords = 6;

        internal static bool Resumable(string topic, TeamMember member) => Resumable(topic, member, null);

        internal static bool Resumable(string topic, TeamMember member, string spoken)
        {
            if (member == TeamMember.Sara) return false;
            if (spoken != null && Words(spoken) < MinWords) return false; // „Alles klar, Rob.“ ist kein Inhalt
            if (string.IsNullOrEmpty(topic)) return true; // Antwort auf Robs Frage
            foreach (var prefix in TimeCritical) if (topic.StartsWith(prefix, StringComparison.Ordinal)) return false;
            foreach (var prefix in Lasting) if (topic.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int Words(string s)
        {
            int colon = s.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0 && colon < 12) s = s.Substring(colon + 2); // „Mik: …“
            return s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private string phase;

        internal void Note(TeamMember who, string spoken, string what, DateTime now) => Note(who, spoken, what, now, null);

        /// <param name="sessionPhase">Session und Phase beim Abbruch; nachgereicht wird nur in derselben.</param>
        internal void Note(TeamMember who, string spoken, string what, DateTime now, string sessionPhase)
        {
            if (string.IsNullOrWhiteSpace(spoken) || !Resumable(what, who, spoken)) return;
            lock (gate)
            {
                if (spoken == lastResumed) return; // schon einmal nachgereicht, kein zweites Mal
                member = who; text = spoken; topic = what; utc = now; phase = sessionPhase;
            }
        }

        /// <summary>Liefert die nachzureichende Meldung (mit kurzer Einleitung) oder null.</summary>
        internal string TryTake(DateTime now, bool radioFree, out TeamMember who, out string what) => TryTake(now, radioFree, null, out who, out what);

        internal string TryTake(DateTime now, bool radioFree, string sessionPhase, out TeamMember who, out string what)
        {
            who = TeamMember.Ben; what = null;
            lock (gate)
            {
                if (text == null) return null;
                if (now < utc || now - utc > MaxAge || phase != null && sessionPhase != null && phase != sessionPhase) { text = null; return null; }
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
                new[] { "Noch mal von eben: ", "Kurz nachgereicht: ", "Noch kurz dazu: ", "Zurück zu eben: ", "Was ich sagen wollte: " },
                new[] { "Again from before: ", "Quickly catching up: ", "One more thing on that: ", "Back to that: ", "What I wanted to say: " },
                new[] { "Lo de antes otra vez: ", "Lo completo rápido: ", "Una cosa más sobre eso: ", "Volviendo a lo de antes: ", "Lo que quería decir: " });
            return lines[Math.Abs(v) % lines.Length];
        }
    }
}
