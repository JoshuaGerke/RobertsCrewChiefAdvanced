using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Ein Startplatz für alle (RaceRoom Monza 17.11.2026): „2 Plätze hinter der Startposition“ und 20 s später „seit dem Start
    /// 14 Plätze verloren“ – manche Sims ändern den gemeldeten Startplatz während des Rennens. Der erste gemeldete Wert einer
    /// Rennsitzung gilt, für Status, Zwischenstand, Sprachfragen und Start-Urteil gleichermaßen.
    /// AC und RaceRoom melden jede Session mit der Kennung -1 (21.11.): Der Startplatz 7 aus dem AC-Rennen in Monza galt
    /// danach im RaceRoom-Rennen in Mantorp (Start 18) weiter – „Platz 21, seit dem Start 14 Plätze verloren“. Die Strecke
    /// gehört deshalb zur Kennung, und jede neue Session setzt den Startplatz zurück.
    /// </summary>
    internal static class RaceStartLatch
    {
        private static readonly object gate = new object();
        private static string session;
        private static int position;

        internal static string Key(SessionData s) => s == null ? null :
            s.SessionId + "|" + (s.TrackDefinition == null ? "" : s.TrackDefinition.name);

        /// <summary>Startplatz in der Klasse für diese Rennsitzung; 0 wenn unbekannt.</summary>
        internal static int Position(SessionData s)
        {
            if (s == null || s.SessionType != SessionType.Race) return 0;
            lock (gate)
            {
                string key = Key(s);
                if (key != session) { session = key; position = 0; }
                if (position <= 0 && s.SessionStartClassPosition > 0) position = s.SessionStartClassPosition;
                return position;
            }
        }

        internal static void Reset() { lock (gate) { session = null; position = 0; } }
    }
}
