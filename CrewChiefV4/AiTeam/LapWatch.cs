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
        private bool finishCalled;
        private static int finishVariant;

        public void Reset()
        {
            lastCalledLap = -1;
            finishCalled = false;
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
                    (s.ClassPosition <= 3 ? " Auf dem Podium, klasse gemacht!" : " Danke für deinen Einsatz. Wir schauen gemeinsam, wo noch Zeit drin ist.");
            }
            if (s.SessionPhase == SessionPhase.Checkered && ps.SessionPhase == SessionPhase.Green && lastCalledLap != s.CompletedLaps)
            {
                lastCalledLap = s.CompletedLaps;
                return "Zielflagge ist draußen, bring die Runde sauber zu Ende." + (s.ClassPosition == 1 ? " Du führst, jetzt konzentriert nach Hause bringen." : s.ClassPosition > 0 ? " Platz " + s.ClassPosition + "." : "") + Duel(gapAhead, gapBehind);
            }
            if (!s.IsNewLap || s.SessionPhase != SessionPhase.Green || lastCalledLap == s.CompletedLaps)
            {
                return null;
            }
            if (s.SessionLapsRemaining == 1 || s.IsLastLap)
            {
                lastCalledLap = s.CompletedLaps;
                return "Letzte Runde! " + (s.ClassPosition == 1 ? "Du führst. Bleib ruhig, bring es sauber nach Hause." : "Platz " + s.ClassPosition + ". Bleib konzentriert.") + Duel(gapAhead, gapBehind);
            }
            if (!minimalMode && !s.SessionHasFixedTime && s.SessionLapsRemaining == 2)
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
