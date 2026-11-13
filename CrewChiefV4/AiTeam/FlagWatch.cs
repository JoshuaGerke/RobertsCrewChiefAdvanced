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
        private static readonly string[] PhrasesDe =
        {
            "Gelb! Langsam, nicht überholen.", "Gelb vorbei.", "Gelb im Sektor.", "Doppelt Gelb, Vorsicht.", "Blaue Flagge, lass ihn vorbei.",
            "Schwarze Flagge.", "Full Course Yellow, Tempo runter.", "Grün, wieder freie Fahrt.", "Unfall voraus!",
        };
        private static readonly string[] PhrasesEn =
        {
            "Yellow! Slow down, no overtaking.", "Yellow's clear.", "Yellow in the sector.", "Double yellow, careful.", "Blue flag, let him by.",
            "Black flag.", "Full course yellow, slow down.", "Green, track is clear.", "Incident ahead!",
        };
        private static readonly string[] PhrasesEs =
        {
            "¡Amarilla! Despacio, no adelantes.", "Fin de amarilla.", "Amarilla en el sector.", "Doble amarilla, cuidado.", "Bandera azul, déjale pasar.",
            "Bandera negra.", "Full course yellow, baja el ritmo.", "Verde, vía libre.", "¡Accidente delante!",
        };
        // Index in all three arrays: 0 local yellow, 1 yellow clear, 2 sector yellow, 3 double yellow, 4 blue, 5 black, 6 FCY, 7 green, 8 incident.
        public static string[] Phrases => RadioLanguage.Pick(PhrasesDe, PhrasesEn, PhrasesEs);

        // A phrase offered before a language switch is still recognised.
        private static int IndexOf(string phrase)
        {
            int i = Array.IndexOf(PhrasesDe, phrase);
            if (i < 0) i = Array.IndexOf(PhrasesEn, phrase);
            return i >= 0 ? i : Array.IndexOf(PhrasesEs, phrase);
        }

        private DateTime lastIncidentCallUtc = DateTime.MinValue;
        private DateTime lastBlueUtc = DateTime.MinValue;

        public void Reset()
        {
            lastIncidentCallUtc = lastBlueUtc = DateTime.MinValue;
        }

        internal static bool FreshClear(string phrase, DateTime detected, DateTime now)
        {
            int i = IndexOf(phrase);
            return (i != 1 && i != 7) ||
                (now >= detected && now - detected <= TimeSpan.FromSeconds(3));
        }

        public static bool StillRelevant(string phrase, GameStateData gs)
        {
            if (gs == null || gs.PitData.InPitlane || (gs.SessionData.SessionPhase != SessionPhase.Green && gs.SessionData.SessionPhase != SessionPhase.FullCourseYellow)) return false;
            var f=gs.FlagData;
            switch (IndexOf(phrase))
            {
                case 8: return f.distanceToNearestIncident > 0 && f.distanceToNearestIncident < 250;
                case 0: return f.isLocalYellow;
                case 1: return !f.isLocalYellow && !f.isFullCourseYellow && gs.SessionData.Flag != FlagEnum.YELLOW && gs.SessionData.Flag != FlagEnum.DOUBLE_YELLOW;
                case 2: return gs.SessionData.Flag == FlagEnum.YELLOW;
                case 3: return gs.SessionData.Flag == FlagEnum.DOUBLE_YELLOW;
                case 4: return gs.SessionData.Flag == FlagEnum.BLUE;
                case 5: return gs.SessionData.Flag == FlagEnum.BLACK;
                case 6: return f.isFullCourseYellow;
                case 7: return !f.isFullCourseYellow && !f.isLocalYellow;
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
                result.Add(Phrases[6]);
                return result;
            }
            if (!f.isFullCourseYellow && pf.isFullCourseYellow)
            {
                result.Add(Phrases[7]);
                return result;
            }

            // Unfall unmittelbar voraus (innerhalb 250 m), höchstens alle 20 s
            float d = f.distanceToNearestIncident;
            float pd = pf.distanceToNearestIncident;
            if (d > 0 && d < 250 && (pd <= 0 || pd >= 250) && nowUtc - lastIncidentCallUtc > TimeSpan.FromSeconds(20))
            {
                lastIncidentCallUtc = nowUtc;
                result.Add(Phrases[8]);
            }

            // lokale Gelbphase
            if (f.isLocalYellow && !pf.isLocalYellow)
            {
                result.Add(Phrases[0]);
            }
            else if (!f.isLocalYellow && pf.isLocalYellow)
            {
                result.Add(Phrases[1]);
            }

            // Flagge für den eigenen Wagen
            var flag = current.SessionData.Flag;
            var prevFlag = previous.SessionData.Flag;
            if (flag != prevFlag)
            {
                switch (flag)
                {
                    case FlagEnum.YELLOW:
                        if (!f.isLocalYellow) result.Add(Phrases[2]);
                        break;
                    case FlagEnum.DOUBLE_YELLOW:
                        result.Add(Phrases[3]);
                        break;
                    case FlagEnum.BLUE:
                        if (nowUtc - lastBlueUtc > TimeSpan.FromSeconds(15))
                        {
                            lastBlueUtc = nowUtc;
                            result.Add(Phrases[4]);
                        }
                        break;
                    case FlagEnum.BLACK:
                        result.Add(Phrases[5]);
                        break;
                }
            }
            return result;
        }
    }
}
