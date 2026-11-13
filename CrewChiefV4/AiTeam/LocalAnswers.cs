using System;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Einfache Standardfragen, die ohne KI-Aufruf zuverlässig aus der Telemetrie beantwortet werden
    /// (spart Latenz und Kosten). Alles mit "warum", "Strategie", "analysier" usw. geht an die KI.
    /// </summary>
    public static class LocalAnswers
    {
        private static readonly string[] complexMarkers = { "warum", "wieso", "weshalb", "analys", "strategie", "sollten", "sollen wir", "meinst du",
            "undercut", "overcut", "vergleich", "besser", "was hältst", "wie kann ich", "was kann ich", "reicht", "schaffen", "kommen wir", "kriege ich", "krieg ich",
            // Englisch
            "why", "analyz", "strategy", "should we", "should i", "do you think", "compare", "better", "how can i", "what can i", "enough", "make it", "will we", "can we",
            // Spanisch
            "por qué", "por que", "analiz", "estrategia", "deberíamos", "deberiamos", "debería", "deberia", "crees", "compar", "mejor", "opinas",
            "cómo puedo", "como puedo", "qué puedo", "que puedo", "alcanza", "llega", "llegamos", "llego" };

        private static readonly char[] trimChars = { ' ', '?', '.', '!', '¿', '¡' };

        private static bool AnyOf(string q, params string[] parts) => parts.Any(q.Contains);

        public static string TryAnswer(string question, GameStateData gs, AiRaceMemory memory, bool allowDriverNames = true)
        {
            if (gs == null || string.IsNullOrWhiteSpace(question))
            {
                return null;
            }
            string q = question.ToLowerInvariant();
            if (q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 10 || complexMarkers.Any(m => q.Contains(m)))
            {
                return null;
            }

            string trimmed = q.Trim(trimChars);
            if (q.Contains("wo bin ich") || q.Contains("wo auf der strecke") || q.Contains("welchem sektor") ||
                trimmed == "where am i" || trimmed == "where am i on track" || AnyOf(q, "which sector", "what sector") ||
                trimmed == "dónde estoy" || trimmed == "donde estoy" || AnyOf(q, "qué sector", "que sector"))
            {
                int sector = gs.SessionData.SectorNumber;
                return gs.PitData.InPitlane ? RadioLanguage.T("Du bist in der Boxengasse.", "You're in the pit lane.", "Estás en el carril de boxes.") :
                    sector >= 1 && sector <= 3 ? RadioLanguage.T("Du bist in Sektor ", "You're in sector ", "Estás en el sector ") + sector + "." :
                    RadioLanguage.T("Den aktuellen Streckensektor liefert die Simulation gerade nicht sicher.", "The sim isn't giving a reliable sector right now.",
                        "El simulador no da el sector con seguridad ahora mismo.");
            }

            bool lapTimeWords = AnyOf(q, "rundenzeit", "letzte runde", "lap time", "laptime", "last lap", "tiempo de vuelta", "última vuelta", "ultima vuelta");
            var named = RaceFieldTools.NamedInQuestion(question, gs);
            if (named.Count > 0 && !allowDriverNames)
                return RadioLanguage.T("Die Namensfreigabe ist ausgeschaltet.", "Driver names are switched off.", "Los nombres de pilotos están desactivados.");
            if (named.Count > 1)
                return RadioLanguage.T("Der Fahrername ist nicht eindeutig. Welche Startnummer meinst du?", "That name isn't unique. Which car number?",
                    "Ese nombre no es único. ¿Qué dorsal?");
            if (named.Count == 1)
            {
                var opponent = named[0];
                if (lapTimeWords)
                    return opponent.LastLapTime > 0 && opponent.LastLapTime < 3600 ?
                        opponent.DriverRawName + RadioLanguage.T(": letzte Runde ", ": last lap ", ": última vuelta ") + LapTime(opponent.LastLapTime) + "." :
                        RadioLanguage.T("Für diesen Fahrer liegt noch keine Rundenzeit vor.", "No lap time for that driver yet.", "Todavía no hay tiempo de vuelta de ese piloto.");
                if (AnyOf(q, "position", "platz", "place", "posición", "posicion", "puesto"))
                    return opponent.ClassPosition > 0 ?
                        opponent.DriverRawName + RadioLanguage.T(": Klassenposition ", ": class position ", ": posición en su clase ") + opponent.ClassPosition + "." :
                        RadioLanguage.T("Für diesen Fahrer fehlt gerade eine verlässliche Position.", "No reliable position for that driver right now.",
                            "Ahora mismo no tengo una posición fiable de ese piloto.");
                return null;
            }
            if (AnyOf(q, "sprit", "benzin", "kraftstoff", "tank", "fuel", "combustible", "gasolina", "depósito", "deposito") &&
                AnyOf(q, "wie viel", "wieviel", "stand", "noch", "how much", "left", "remaining", "level", "cuánt", "cuant", "queda", "nivel"))
            {
                // Unknown rival names must never fall through to our own tank value.
                if (q.Contains(" hat ") || q.Contains(" haben die ") || q.Contains("gegner") || q.Contains("vordermann") || q.Contains("hintermann") ||
                    AnyOf(q, " has ", " does ", " have they", "opponent", "rival", "car ahead", "car in front", "car behind", " tiene ", " tienen ",
                        "el de delante", "el de detrás", "el de detras")) return null;
                if (!gs.FuelData.FuelUseActive || float.IsNaN(gs.FuelData.FuelLeft) || float.IsInfinity(gs.FuelData.FuelLeft) || gs.FuelData.FuelLeft < 0)
                {
                    return RadioLanguage.T("Gerade kein verlässlicher Tankstand.", "No reliable fuel reading right now.", "Ahora mismo no hay un nivel de combustible fiable.");
                }
                string litres = RadioLanguage.Num(gs.FuelData.FuelLeft, "0");
                float perLap = memory != null ? memory.AverageFuelPerLap() : -1;
                if (perLap > 0)
                {
                    int laps = (int)Math.Floor(gs.FuelData.FuelLeft / perLap);
                    return RadioLanguage.T("Noch " + litres + " Liter, das reicht für etwa " + laps + (laps == 1 ? " Runde." : " Runden."),
                        litres + " litres left, good for about " + laps + (laps == 1 ? " lap." : " laps."),
                        "Quedan " + litres + " litros, dan para " + (laps == 1 ? "más o menos una vuelta." : "unas " + laps + " vueltas."));
                }
                return RadioLanguage.T("Noch " + litres + " Liter im Tank. Den Verbrauch hab ich noch nicht sauber gemessen.",
                    litres + " litres in the tank. I haven't measured consumption cleanly yet.",
                    "Quedan " + litres + " litros en el depósito. Aún no tengo bien medido el consumo.");
            }

            // Only explicitly self-referential short questions qualify for the own-car shortcut.
            bool ownQuestion = q.Contains("mein ") || q.Contains("meine") || q.Contains("unsere") || q.Contains("bin ich") || q.Contains("sind wir") ||
                q.Trim(' ', '?', '.', '!') == "letzte runde" || q.Trim(' ', '?', '.', '!') == "wie war die letzte runde" || q.Trim(' ', '?', '.', '!') == "rundenzeit" ||
                new[] { "my", "our", "am i", "are we", "mi", "mis", "nuestro", "nuestra", "estoy", "vamos", "voy" }.Any(w => TeamRouter.HasWord(q, w)) ||
                new[] { "last lap", "lap time", "how was the last lap", "última vuelta", "ultima vuelta", "tiempo de vuelta",
                    "cómo fue la última vuelta", "como fue la ultima vuelta" }.Contains(trimmed);
            if (!ownQuestion) return null;
            if (OwnTyreAnswer.Matches(question)) return OwnTyreAnswer.Answer(gs, DateTime.UtcNow);

            if (q.Contains("gesamtplatz") || q.Contains("klassenplatz") || q.Contains("position") || q.Contains("welcher platz") || q.Contains("welchem platz") || q.Contains("wievielter") || q.Contains("wie vielter") ||
                AnyOf(q, "what place", "which place", "posición", "posicion", "puesto", "qué lugar", "que lugar"))
            {
                var session = gs.SessionData;
                if (session.SessionType == SessionType.Race)
                {
                    if (AnyOf(q, "gesamt", "overall", "general"))
                        return session.OverallPosition > 0 ? RadioLanguage.T("Gesamtplatz " + session.OverallPosition + ".", "P" + session.OverallPosition + " overall.",
                                "Posición " + session.OverallPosition + " en la general.") :
                            RadioLanguage.T("Deinen Gesamtplatz habe ich gerade nicht sicher.", "I don't have your overall position for sure right now.",
                                "Ahora mismo no tengo seguro tu puesto en la general.");
                    if (AnyOf(q, "klasse", "class", "clase"))
                        return session.ClassPosition > 0 ? RadioLanguage.T("Klassenplatz " + session.ClassPosition + ".", "P" + session.ClassPosition + " in class.",
                                "Posición " + session.ClassPosition + " en tu clase.") :
                            RadioLanguage.T("Deinen Klassenplatz habe ich gerade nicht sicher.", "I don't have your class position for sure right now.",
                                "Ahora mismo no tengo seguro tu puesto en la clase.");
                    if (RaceRadioLanguage.SeparateClass(session)) return RaceRadioLanguage.RaceRanks(session) + ".";
                }
                int pos = session.ClassPosition > 0 ? session.ClassPosition : session.OverallPosition;
                if (pos <= 0)
                {
                    return RadioLanguage.T("Deinen Platz hab ich gerade nicht sicher.", "I'm not sure of your position right now.", "Ahora mismo no tengo seguro tu puesto.");
                }
                int cars = gs.SessionData.NumCarsInPlayerClass;
                return cars > 0 ? RadioLanguage.T("Platz " + pos + " von " + cars + ".", "P" + pos + " of " + cars + ".", "Posición " + pos + " de " + cars + ".") :
                    RadioLanguage.T("Platz " + pos + ".", "P" + pos + ".", "Posición " + pos + ".");
            }

            if (lapTimeWords)
            {
                float last = gs.SessionData.LapTimePrevious;
                if (float.IsNaN(last) || float.IsInfinity(last) || last <= 0 || last >= 3600)
                {
                    return RadioLanguage.T("Noch keine gezeitete Runde.", "No timed lap yet.", "Todavía no hay vuelta cronometrada.");
                }
                string answer = RadioLanguage.T("Letzte Runde ", "Last lap ", "Última vuelta ") + LapTime(last) +
                    (gs.SessionData.PreviousLapWasValid ? "." : RadioLanguage.T(", aber ungültig.", ", but invalid.", ", pero no válida."));
                float best = gs.SessionData.PlayerLapTimeSessionBest;
                if (best > 0 && best < last - 0.05f)
                {
                    answer += RadioLanguage.T(" Bestzeit ", " Best ", " Mejor vuelta ") + LapTime(best) + ".";
                }
                return answer;
            }
            return null;
        }

        public static string LapTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || seconds >= 86400)
                return RadioLanguage.T("Zeit nicht verfügbar", "Time not available", "Tiempo no disponible");
            var culture = RadioLanguage.Culture;
            long milliseconds = (long)Math.Round((double)seconds * 1000, MidpointRounding.AwayFromZero);
            long minutes = milliseconds / 60000;
            decimal remainder = (milliseconds % 60000) / 1000m;
            return minutes > 0 ? minutes + ":" + remainder.ToString("00.000", culture) : remainder.ToString("0.000", culture);
        }
    }
}
