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
        private bool finishCalled;
        private static int finishVariant;

        public void Reset()
        {
            lastCalledLap = pendingLap = -1;
            finishCalled = false;
        }

        /// <summary>
        /// Restrunden inklusive der aktuellen, -1 = unbekannt. Rundenrennen: vom Spiel. Zeitrennen: Crew Chiefs Schätzung
        /// (berücksichtigt den Führenden), sonst nach Ablauf der Uhr 1.
        /// </summary>
        public static int LapsLeft(SessionData s)
        {
            if (s.IsLastLap)
            {
                return 1;
            }
            if (!s.SessionHasFixedTime)
            {
                return s.SessionLapsRemaining > 0 && s.SessionLapsRemaining < 10000 ? s.SessionLapsRemaining : -1;
            }
            if (CrewChiefV4.Events.Fuel.EstimatedLapsRemainingAtLap == s.CompletedLaps)
            {
                return Math.Max(1, CrewChiefV4.Events.Fuel.EstimatedLapsRemaining);
            }
            return s.SessionTimeRemaining <= 0 && s.SessionRunningTime > 60 ? 1 : -1;
        }

        public static bool OnLastLap(SessionData s)
        {
            return s.SessionType == SessionType.Race && (s.SessionPhase == SessionPhase.Checkered || LapsLeft(s) == 1);
        }

        /// <summary>Ein Telemetrie-Tick. gapAhead/gapBehind in Sekunden, &lt;= 0 = unbekannt. Liefert den Funkspruch oder null.</summary>
        public string Evaluate(GameStateData previous, GameStateData current, float gapAhead, float gapBehind, bool minimalMode)
        {
            if (previous == null || current == null || current.SessionData.SessionType != SessionType.Race)
            {
                return null;
            }
            var s = current.SessionData;
            var ps = previous.SessionData;
            if (s.SessionPhase == SessionPhase.Finished && ps.SessionPhase != SessionPhase.Finished && !finishCalled)
            {
                finishCalled = true;
                if(s.IsDNF)return "Die Session ist beendet. Du bist als ausgefallen geführt.";
                if(s.IsDisqualified)return "Die Session ist beendet. Die Wertung meldet eine Disqualifikation.";
                if (s.ClassPosition == 1) return CrewSpeechPolicy.FinishPhrases[(System.Threading.Interlocked.Increment(ref finishVariant)-1) % CrewSpeechPolicy.FinishPhrases.Length];
                if (s.ClassPosition <= 0) return "Im Ziel. Die endgültige Platzierung ist noch nicht gemeldet.";
                return "Im Ziel! Platz " + s.ClassPosition + "." + Gained(s) +
                    (s.ClassPosition <= 3 ? " Podium, richtig stark!" : " Gut gekämpft, beim nächsten Mal holen wir mehr raus.");
            }
            if (s.SessionPhase == SessionPhase.Checkered && ps.SessionPhase == SessionPhase.Green && lastCalledLap != s.CompletedLaps)
            {
                lastCalledLap = s.CompletedLaps;
                return "Zielflagge ist draußen, bring die Runde sauber zu Ende." + (s.ClassPosition == 1 ? " Du führst, jetzt konzentriert nach Hause bringen." : s.ClassPosition > 0 ? " Platz " + s.ClassPosition + "." : "") + Duel(gapAhead, gapBehind);
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
                return "Letzte Runde! " + (s.ClassPosition == 1 ? "Du führst. Bleib ruhig, bring es sauber nach Hause." : "Platz " + s.ClassPosition + ". Bleib konzentriert.") + Duel(gapAhead, gapBehind);
            }
            if (!minimalMode && left == 2)
            {
                lastCalledLap = s.CompletedLaps;
                return "Noch zwei Runden, Platz " + s.ClassPosition + ".";
            }
            return null;
        }

        private static string Duel(float gapAhead, float gapBehind)
        {
            if (gapBehind > 0 && gapBehind < 1.5f)
            {
                return " Hinter dir " + SaraLocal.Seconds(gapBehind) + ", bleib aufmerksam.";
            }
            if (gapAhead > 0 && gapAhead < 1.5f)
            {
                return " Vorne " + SaraLocal.Seconds(gapAhead) + ", da geht noch was.";
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
            return diff > 0 ? " " + diff + (diff == 1 ? " Platz" : " Plätze") + " gutgemacht." : "";
        }
    }
}
