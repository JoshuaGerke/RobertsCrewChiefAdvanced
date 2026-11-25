using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Merkt sich, ob die laufende Session schon Grün war. RaceRoom Salzburg 24.11.: Nach dem Training meldete Crew Chief die neue
    /// Session schon als Rennen, aber noch mit der Zielflagge des Trainings; die Crew hielt das Rennen für beendet.
    /// </summary>
    internal static class SessionGreenTracker
    {
        private static readonly object gate = new object();
        private static int session = int.MinValue;
        private static SessionType type = SessionType.Unavailable;
        private static bool green;

        internal static void Observe(GameStateData gs)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            lock (gate)
            {
                if (s.SessionId != session || s.SessionType != type) { session = s.SessionId; type = s.SessionType; green = false; }
                if (s.SessionPhase == SessionPhase.Green || s.SessionPhase == SessionPhase.FullCourseYellow) green = true;
            }
        }

        /// <summary>War diese Session schon Grün? Ohne Beobachtung gilt sie als gestartet (kein Rätselraten aus alten Daten).</summary>
        internal static bool SawGreen(SessionData s)
        {
            if (s == null) return true;
            lock (gate)
            {
                if (session == int.MinValue) return true;
                return s.SessionId != session || s.SessionType != type || green;
            }
        }

        internal static void Reset() { lock (gate) { session = int.MinValue; type = SessionType.Unavailable; green = false; } }
    }
}
