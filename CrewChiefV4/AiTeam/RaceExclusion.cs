using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// AC Monza 21.11.: Schwarze Flagge um 14:55:30 – AC stellte Rob sofort in die Garage. Danach kamen noch
    /// „Gleich Boxengasse – Tempolimit nicht vergessen“, „Geht's? Schau in die Spiegel“ und eine Rennbegrüßung, die
    /// Ansage der schwarzen Flagge selbst fiel weg („warning_no_longer_current“), weil die Flagge nach dem Versetzen
    /// nicht mehr anlag. Ist Rob aus dem Rennen (schwarze Flagge, Disqualifikation, Aufgabe), merkt sich das diese Sperre
    /// bis zur nächsten Session: Dann gehen nur noch die Meldung dazu, Strafen, Antworten an Rob und die Auswertung raus.
    /// </summary>
    internal static class RaceExclusion
    {
        private static readonly object gate = new object();
        private static string session;
        private static string reason;

        internal static string SessionKey(GameStateData gs) => gs == null ? null :
            gs.SessionData.SessionId + "|" + gs.SessionData.SessionType;

        /// <summary>Grund, aus dem Rob in diesem Spielzustand aus dem Rennen ist; null = fährt regulär.</summary>
        internal static string ReasonOf(GameStateData gs)
        {
            if (gs == null || gs.SessionData.SessionType != SessionType.Race) return null;
            if (gs.SessionData.IsDisqualified) return "disqualified";
            if (gs.SessionData.Flag == FlagEnum.BLACK) return "black_flag";
            if (gs.SessionData.IsDNF) return "dnf";
            return null;
        }

        /// <summary>Einmal je Spielzustand aufrufen; true, wenn die Sperre gerade neu greift.</summary>
        internal static bool Observe(GameStateData gs)
        {
            string key = SessionKey(gs), why = ReasonOf(gs);
            lock (gate)
            {
                if (key != session) { session = key; reason = null; }
                if (why == null || reason != null) return false;
                reason = why;
            }
            LiveDiagnostics.Decision("race-exclusion", "detected", why);
            return true;
        }

        /// <summary>Ist Rob in der Session dieses Spielzustands aus dem Rennen?</summary>
        internal static bool Active(GameStateData gs)
        {
            string key = SessionKey(gs);
            lock (gate) return key != null && key == session && reason != null;
        }

        internal static void Reset() { lock (gate) { session = null; reason = null; } }

        /// <summary>Darf dieses Thema noch gesendet werden, wenn Rob aus dem Rennen ist?</summary>
        internal static bool Allowed(string topic)
        {
            if (string.IsNullOrEmpty(topic)) return false;
            return topic == "warning-flag-5" || topic == "race-disqualified" || topic == "session-debrief" ||
                topic.StartsWith("penalty", StringComparison.Ordinal);
        }
    }
}
