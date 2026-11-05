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
            "undercut", "overcut", "vergleich", "besser", "was hältst", "wie kann ich", "was kann ich", "reicht", "schaffen", "kommen wir", "kriege ich", "krieg ich" };

        private static readonly CultureInfo de = CultureInfo.GetCultureInfo("de-DE");

        public static string TryAnswer(string question, GameStateData gs, AiRaceMemory memory)
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

            if (q.Contains("wo bin ich") || q.Contains("wo auf der strecke") || q.Contains("welchem sektor"))
            {
                int sector = gs.SessionData.SectorNumber;
                return gs.PitData.InPitlane ? "Du bist in der Boxengasse." : sector >= 1 && sector <= 3 ?
                    "Du bist in Sektor " + sector + "." : "Den aktuellen Streckensektor liefert die Simulation gerade nicht sicher.";
            }

            var named = RaceFieldTools.NamedInQuestion(question, gs);
            if (named.Count > 1) return "Der Fahrername ist nicht eindeutig. Welche Startnummer meinst du?";
            if (named.Count == 1)
            {
                var opponent = named[0];
                if (q.Contains("rundenzeit") || q.Contains("letzte runde"))
                    return opponent.LastLapTime > 0 && opponent.LastLapTime < 3600 ?
                        opponent.DriverRawName + ": letzte Runde " + LapTime(opponent.LastLapTime) + "." :
                        "Für diesen Fahrer liegt noch keine Rundenzeit vor.";
                if (q.Contains("position") || q.Contains("platz"))
                    return opponent.ClassPosition > 0 ? opponent.DriverRawName + ": Klassenposition " + opponent.ClassPosition + "." :
                        "Für diesen Fahrer fehlt gerade eine verlässliche Position.";
                return null;
            }
            if ((q.Contains("sprit") || q.Contains("benzin") || q.Contains("kraftstoff") || q.Contains("tank")) &&
                (q.Contains("wie viel") || q.Contains("wieviel") || q.Contains("stand") || q.Contains("noch")))
            {
                // Unknown rival names must never fall through to our own tank value.
                if (q.Contains(" hat ") || q.Contains(" haben die ") || q.Contains("gegner") || q.Contains("vordermann") || q.Contains("hintermann")) return null;
                if (!gs.FuelData.FuelUseActive || float.IsNaN(gs.FuelData.FuelLeft) || float.IsInfinity(gs.FuelData.FuelLeft) || gs.FuelData.FuelLeft < 0)
                {
                    return "Gerade kein verlässlicher Tankstand.";
                }
                string litres = gs.FuelData.FuelLeft.ToString("0", de);
                float perLap = memory != null ? memory.AverageFuelPerLap() : -1;
                if (perLap > 0)
                {
                    int laps = (int)Math.Floor(gs.FuelData.FuelLeft / perLap);
                    return "Noch " + litres + " Liter, das reicht für etwa " + laps + (laps == 1 ? " Runde." : " Runden.");
                }
                return "Noch " + litres + " Liter im Tank. Den Verbrauch hab ich noch nicht sauber gemessen.";
            }

            // Only explicitly self-referential short questions qualify for the own-car shortcut.
            bool ownQuestion = q.Contains("meine") || q.Contains("unsere") || q.Contains("bin ich") || q.Contains("sind wir") ||
                q.Trim(' ', '?', '.', '!') == "letzte runde" || q.Trim(' ', '?', '.', '!') == "wie war die letzte runde" || q.Trim(' ', '?', '.', '!') == "rundenzeit";
            if (!ownQuestion) return null;

            if (q.Contains("position") || q.Contains("welcher platz") || q.Contains("welchem platz") || q.Contains("wievielter") || q.Contains("wie vielter"))
            {
                int pos = gs.SessionData.ClassPosition > 0 ? gs.SessionData.ClassPosition : gs.SessionData.OverallPosition;
                if (pos <= 0)
                {
                    return "Deinen Platz hab ich gerade nicht sicher.";
                }
                int cars = gs.SessionData.NumCarsInPlayerClass;
                return cars > 0 ? "Platz " + pos + " von " + cars + "." : "Platz " + pos + ".";
            }

            if (q.Contains("letzte runde") || q.Contains("rundenzeit"))
            {
                float last = gs.SessionData.LapTimePrevious;
                if (float.IsNaN(last) || float.IsInfinity(last) || last <= 0 || last >= 3600)
                {
                    return "Noch keine gezeitete Runde.";
                }
                string answer = "Letzte Runde " + LapTime(last) + (gs.SessionData.PreviousLapWasValid ? "." : ", aber ungültig.");
                float best = gs.SessionData.PlayerLapTimeSessionBest;
                if (best > 0 && best < last - 0.05f)
                {
                    answer += " Bestzeit " + LapTime(best) + ".";
                }
                return answer;
            }
            return null;
        }

        public static string LapTime(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || seconds >= 86400) return "Zeit nicht verfügbar";
            long milliseconds = (long)Math.Round((double)seconds * 1000, MidpointRounding.AwayFromZero);
            long minutes = milliseconds / 60000;
            decimal remainder = (milliseconds % 60000) / 1000m;
            return minutes > 0 ? minutes + ":" + remainder.ToString("00.000", de) : remainder.ToString("0.000", de);
        }
    }
}
