using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rennfortschritt ohne KI und deshalb sofort: noch zwei Runden, letzte Runde, Zielflagge.
    /// Ersetzt im Modus "Nur noch das KI-Team spricht" die Rundenzähler-Ansagen von Crew Chief.
    /// </summary>
    public class LapWatch
    {
        private int lastCalledLap = -1;
        private int pendingLap = -1;
        private float pendingSince;
        private bool finishCalled, timedClockCalled;
        private readonly RaceRadioLanguage language = new RaceRadioLanguage();

        public void Reset()
        {
            lastCalledLap = pendingLap = -1;
            finishCalled = timedClockCalled = false;
        }

        /// <summary>
        /// Restrunden inklusive der aktuellen, -1 = unbekannt. Rundenrennen: vom Spiel. Zeitrennen: nur eine
        /// positive Schätzung zur aktuellen Runde bei gültiger Restzeit oder ein explizites letztes-Runden-Signal.
        /// Das Ablaufen der Uhr allein beweist bei möglichen Zusatzrunden keine letzte Runde.
        /// </summary>
        public static int LapsLeft(SessionData s)
        {
            if (s == null) return -1;
            if (s.IsLastLap || (s.SessionType == SessionType.Race && s.SessionPhase == SessionPhase.Checkered)) return 1;
            if (!s.SessionHasFixedTime)
                return s.SessionLapsRemaining > 0 && s.SessionLapsRemaining < 10000 ? s.SessionLapsRemaining : -1;
            return LapsAreEstimated(s) ? CrewChiefV4.Events.Fuel.EstimatedLapsRemaining : -1;
        }

        public static bool LapsAreEstimated(SessionData s)
        {
            int estimate = CrewChiefV4.Events.Fuel.EstimatedLapsRemaining;
            return s != null && s.SessionHasFixedTime && !s.IsLastLap && s.SessionPhase != SessionPhase.Checkered &&
                Finite(s.SessionTimeRemaining) && s.SessionTimeRemaining > 0 &&
                CrewChiefV4.Events.Fuel.EstimatedLapsRemainingAtLap == s.CompletedLaps && estimate > 0 && estimate < 10000;
        }

        public static bool OnLastLap(SessionData s)
        {
            return s != null && s.SessionType == SessionType.Race && (s.SessionPhase == SessionPhase.Checkered || (!LapsAreEstimated(s) && LapsLeft(s) == 1));
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        private static bool TimedClockElapsed(SessionData s)
        {
            return s.SessionHasFixedTime && s.SessionTimeRemaining == 0 && Finite(s.SessionTotalRunTime) &&
                s.SessionTotalRunTime > 0 && Finite(s.SessionRunningTime) && s.SessionRunningTime >= s.SessionTotalRunTime;
        }

        /// <summary>Ein Telemetrie-Tick. gapAhead/gapBehind in Sekunden, &lt;= 0 = unbekannt. Liefert den Funkspruch oder null.</summary>
        internal string ReferenceTemplate { get; private set; }

        public string Evaluate(GameStateData previous, GameStateData current, float gapAhead, float gapBehind, bool minimalMode)
        {
            ReferenceTemplate = null;
            if (previous == null || current == null || current.SessionData.SessionType != SessionType.Race)
            {
                return null;
            }
            var s = current.SessionData;
            var ps = previous.SessionData;
            bool separate = RaceRadioLanguage.SeparateClass(s);
            string rank = s.ClassPosition > 0 ? RaceRadioLanguage.Rank(s, s.ClassPosition) + "." : "";
            string duel = separate ? "" : Duel(gapAhead, gapBehind);
            if (s.SessionPhase == SessionPhase.Finished && ps.SessionPhase != SessionPhase.Finished && !finishCalled)
            {
                finishCalled = true;
                return language.Finish(s);
            }
            if (s.SessionPhase == SessionPhase.Checkered && ps.SessionPhase == SessionPhase.Green && lastCalledLap != s.CompletedLaps)
            {
                lastCalledLap = s.CompletedLaps;
                return RadioLanguage.T("Zielflagge ist draußen, bring die Runde sauber zu Ende.", "Chequered flag is out, bring the lap home clean.", "Bandera a cuadros, termina la vuelta limpio.") +
                    (s.ClassPosition == 1 ? (separate ? RadioLanguage.T(" Du führst deine Klasse.", " You're leading your class.", " Lideras tu clase.") :
                        RadioLanguage.T(" Du führst, jetzt konzentriert nach Hause bringen.", " You're leading, bring it home focused.", " Vas líder, llévalo a casa concentrado.")) :
                    rank.Length > 0 ? " " + rank : "") + duel;
            }
            if (!timedClockCalled && s.SessionPhase == SessionPhase.Green && !current.PitData.InPitlane && TimedClockElapsed(s))
            {
                timedClockCalled = true;
                return RadioLanguage.T("Die Rennzeit ist abgelaufen. Achte auf die Zielflagge.", "Race time is up. Watch for the chequered flag.", "Se acabó el tiempo de carrera. Atento a la bandera a cuadros.");
            }
            if (s.IsNewLap && s.SessionPhase == SessionPhase.Green)
            {
                pendingLap = s.CompletedLaps;
                pendingSince = s.SessionRunningTime;
            }
            if (pendingLap != s.CompletedLaps || lastCalledLap == s.CompletedLaps || s.SessionPhase != SessionPhase.Green)
            {
                return null;
            }
            // die Schätzung für Zeitrennen rechnet Crew Chief im selben Moment, deshalb bis zu 3 s darauf warten
            int left = LapsLeft(s);
            if (left < 0 && s.SessionRunningTime - pendingSince < 3)
            {
                return null;
            }
            pendingLap = -1;
            if (left == 1)
            {
                lastCalledLap = s.CompletedLaps;
                string focus = RadioLanguage.T("Bleib konzentriert.", "Stay focused.", "Mantén la concentración.");
                return (LapsAreEstimated(s) ? RadioLanguage.T("Voraussichtlich letzte Runde! ", "Probably the last lap! ", "¡Probablemente última vuelta! ") :
                        RadioLanguage.T("Letzte Runde! ", "Last lap! ", "¡Última vuelta! ")) +
                    (s.ClassPosition == 1 ? (separate ? RadioLanguage.T("Du führst deine Klasse. Bring es sauber nach Hause.", "You're leading your class. Bring it home clean.", "Lideras tu clase. Llévalo a casa limpio.") :
                        RadioLanguage.T("Du führst. Bleib ruhig, bring es sauber nach Hause.", "You're leading. Stay calm, bring it home clean.", "Vas líder. Tranquilo, llévalo a casa limpio.")) :
                        rank.Length > 0 ? rank + " " + focus : focus) + duel;
            }
            if (!minimalMode && left == 2)
            {
                lastCalledLap = s.CompletedLaps;
                return (LapsAreEstimated(s) ? RadioLanguage.T("Voraussichtlich noch zwei Runden", "Probably two laps to go", "Probablemente quedan dos vueltas") :
                        RadioLanguage.T("Noch zwei Runden", "Two laps to go", "Quedan dos vueltas")) +
                    (rank.Length > 0 ? ", " + rank : ".");
            }
            return null;
        }

        private static string Duel(float gapAhead, float gapBehind)
        {
            if (gapBehind > 0 && gapBehind < 1.5f)
            {
                return RadioLanguage.T(" Hinter dir ", " Behind you ", " Detrás tienes ") + SaraLocal.Seconds(gapBehind) + RadioLanguage.T(", bleib aufmerksam.", ", stay alert.", ", atento.");
            }
            if (gapAhead > 0 && gapAhead < 1.5f)
            {
                return RadioLanguage.T(" Nach vorn ", " Gap ahead ", " Por delante ") + SaraLocal.Seconds(gapAhead) + ".";
            }
            return "";
        }

        private static string Gained(SessionData s)
        {
            int start = s.SessionStartClassPosition;
            if (start <= 0 || start == s.ClassPosition)
            {
                return "";
            }
            int diff = start - s.ClassPosition;
            return diff > 0 ? RadioLanguage.T(" " + diff + (diff == 1 ? " Platz" : " Plätze") + " gutgemacht.",
                " " + diff + (diff == 1 ? " place" : " places") + " gained.",
                " " + diff + (diff == 1 ? " posición ganada." : " posiciones ganadas.")) : "";
        }
    }
}
