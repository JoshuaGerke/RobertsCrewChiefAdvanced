using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Ein Startplatz für alle (RaceRoom Monza 17.11.2026): „2 Plätze hinter der Startposition“ und 20 s später „seit dem Start
    /// 14 Plätze verloren“ – manche Sims ändern den gemeldeten Startplatz während des Rennens. Der erste gemeldete Wert einer
    /// Rennsitzung gilt, für Status, Zwischenstand, Sprachfragen und Start-Urteil gleichermaßen.
    /// </summary>
    internal static class RaceStartLatch
    {
        private static readonly object gate = new object();
        private static int session = int.MinValue, position;

        /// <summary>Startplatz in der Klasse für diese Rennsitzung; 0 wenn unbekannt.</summary>
        internal static int Position(SessionData s)
        {
            if (s == null || s.SessionType != SessionType.Race) return 0;
            lock (gate)
            {
                if (s.SessionId != session) { session = s.SessionId; position = 0; }
                if (position <= 0 && s.SessionStartClassPosition > 0) position = s.SessionStartClassPosition;
                return position;
            }
        }

        internal static void Reset() { lock (gate) { session = int.MinValue; position = 0; } }
    }
}
