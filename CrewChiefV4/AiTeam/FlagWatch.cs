using System;
using System.Collections.Generic;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Flaggen und Gefahren auf Deutsch durch Sara, lokal und ohne KI. Ersetzt im Modus "Nur noch das KI-Team spricht"
    /// die englischen Flaggen-Ansagen von Crew Chief. Quelle sind die Flaggendaten, die Crew Chief aus dem Spiel liest.
    /// </summary>
    public class FlagWatch
    {
        public static readonly string[] Phrases =
        {
            "Gelb! Langsam, nicht überholen.", "Gelb vorbei.", "Gelb im Sektor.", "Doppelt Gelb, Vorsicht.", "Blaue Flagge, lass ihn vorbei.",
            "Schwarze Flagge.", "Full Course Yellow, Tempo runter.", "Grün, wieder freie Fahrt.", "Unfall voraus!",
        };

        private DateTime lastIncidentCallUtc = DateTime.MinValue;
        private DateTime lastBlueUtc = DateTime.MinValue;

        public void Reset()
        {
            lastIncidentCallUtc = lastBlueUtc = DateTime.MinValue;
        }

        public static bool StillRelevant(string phrase, GameStateData gs)
        {
            if (gs == null || gs.PitData.InPitlane || (gs.SessionData.SessionPhase != SessionPhase.Green && gs.SessionData.SessionPhase != SessionPhase.FullCourseYellow)) return false;
            var f=gs.FlagData;
            switch (phrase)
            {
                case "Unfall voraus!": return f.distanceToNearestIncident > 0 && f.distanceToNearestIncident < 250;
                case "Gelb! Langsam, nicht überholen.": return f.isLocalYellow;
                case "Gelb vorbei.": return !f.isLocalYellow && !f.isFullCourseYellow && gs.SessionData.Flag != FlagEnum.YELLOW && gs.SessionData.Flag != FlagEnum.DOUBLE_YELLOW;
                case "Gelb im Sektor.": return gs.SessionData.Flag == FlagEnum.YELLOW;
                case "Doppelt Gelb, Vorsicht.": return gs.SessionData.Flag == FlagEnum.DOUBLE_YELLOW;
                case "Blaue Flagge, lass ihn vorbei.": return gs.SessionData.Flag == FlagEnum.BLUE;
                case "Schwarze Flagge.": return gs.SessionData.Flag == FlagEnum.BLACK;
                case "Full Course Yellow, Tempo runter.": return f.isFullCourseYellow;
                case "Grün, wieder freie Fahrt.": return !f.isFullCourseYellow && !f.isLocalYellow;
                default: return false;
            }
        }

        public List<string> Evaluate(GameStateData previous, GameStateData current, DateTime nowUtc)
        {
            var result = new List<string>();
            if (previous == null || current == null || current.PitData.InPitlane)
            {
                return result;
            }
            var phase = current.SessionData.SessionPhase;
            if (phase != SessionPhase.Green && phase != SessionPhase.FullCourseYellow)
            {
                return result;
            }
            var f = current.FlagData;
            var pf = previous.FlagData;

            // Full Course Yellow / Safety Car
            if (f.isFullCourseYellow && !pf.isFullCourseYellow)
            {
                result.Add("Full Course Yellow, Tempo runter.");
                return result;
            }
            if (!f.isFullCourseYellow && pf.isFullCourseYellow)
            {
                result.Add("Grün, wieder freie Fahrt.");
                return result;
            }

            // Unfall unmittelbar voraus (innerhalb 250 m), höchstens alle 20 s
            float d = f.distanceToNearestIncident;
            float pd = pf.distanceToNearestIncident;
            if (d > 0 && d < 250 && (pd <= 0 || pd >= 250) && nowUtc - lastIncidentCallUtc > TimeSpan.FromSeconds(20))
            {
                lastIncidentCallUtc = nowUtc;
                result.Add("Unfall voraus!");
            }

            // lokale Gelbphase
            if (f.isLocalYellow && !pf.isLocalYellow)
            {
                result.Add("Gelb! Langsam, nicht überholen.");
            }
            else if (!f.isLocalYellow && pf.isLocalYellow)
            {
                result.Add("Gelb vorbei.");
            }

            // Flagge für den eigenen Wagen
            var flag = current.SessionData.Flag;
            var prevFlag = previous.SessionData.Flag;
            if (flag != prevFlag)
            {
                switch (flag)
                {
                    case FlagEnum.YELLOW:
                        if (!f.isLocalYellow) result.Add("Gelb im Sektor.");
                        break;
                    case FlagEnum.DOUBLE_YELLOW:
                        result.Add("Doppelt Gelb, Vorsicht.");
                        break;
                    case FlagEnum.BLUE:
                        if (nowUtc - lastBlueUtc > TimeSpan.FromSeconds(15))
                        {
                            lastBlueUtc = nowUtc;
                            result.Add("Blaue Flagge, lass ihn vorbei.");
                        }
                        break;
                    case FlagEnum.BLACK:
                        result.Add("Schwarze Flagge.");
                        break;
                }
            }
            return result;
        }
    }
}
