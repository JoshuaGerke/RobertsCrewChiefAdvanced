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
        private static int gridPosition;
        private static bool frozen;

        internal static string Key(SessionData s) => s == null ? null :
            s.SessionId + "|" + (s.TrackDefinition == null ? "" : s.TrackDefinition.name);

        /// <summary>Vor dem Start (Startaufstellung, Einführungsrunde, Countdown) zeigt die Sim den Startplatz als aktuellen Platz.</summary>
        internal static bool BeforeStart(SessionPhase p) =>
            p == SessionPhase.Countdown || p == SessionPhase.Formation || p == SessionPhase.Gridwalk || p == SessionPhase.Garage;

        /// <summary>
        /// Startplatz in der Klasse für diese Rennsitzung; 0 wenn unbekannt.
        /// RaceRoom Hockenheim 25.11.2026: Bei der neuen Rennsitzung setzte Crew Chief den Startplatz auf 4, weil RaceRoom das Feld
        /// zu dem Zeitpunkt noch nicht sortiert hatte. Ben begrüßte mit „Startplatz 6“, später hieß es bei Platz 6 „seit dem Start
        /// 2 Plätze verloren“. Vor dem Start gilt deshalb der aktuelle Platz in der Startaufstellung; mit Grün wird er festgehalten.
        /// Ohne Beobachtung vor dem Start bleibt es beim ersten gemeldeten Startplatz der Sim.
        /// </summary>
        internal static int Position(SessionData s)
        {
            if (s == null || s.SessionType != SessionType.Race) return 0;
            lock (gate)
            {
                string key = Key(s);
                if (key != session) { session = key; position = 0; gridPosition = 0; frozen = false; }
                if (!frozen)
                {
                    if (BeforeStart(s.SessionPhase)) { if (s.ClassPosition > 0) gridPosition = s.ClassPosition; }
                    else if (s.SessionPhase == SessionPhase.Green || s.SessionPhase == SessionPhase.FullCourseYellow ||
                             s.SessionPhase == SessionPhase.Checkered || s.SessionPhase == SessionPhase.Finished)
                    {
                        if (gridPosition > 0) position = gridPosition;
                        frozen = true;
                    }
                }
                if (!frozen && gridPosition > 0) return gridPosition;
                if (position <= 0 && s.SessionStartClassPosition > 0) position = s.SessionStartClassPosition;
                return position;
            }
        }

        /// <summary>Jeden Takt aufrufen, damit der Platz in der Startaufstellung auch ohne Ansage mitläuft.</summary>
        internal static void Observe(SessionData s) => Position(s);

        internal static void Reset() { lock (gate) { session = null; position = 0; gridPosition = 0; frozen = false; } }
    }
}
