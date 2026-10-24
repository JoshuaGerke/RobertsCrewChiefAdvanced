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

            if ((q.Contains("sprit") || q.Contains("benzin") || q.Contains("kraftstoff") || q.Contains("tank")) &&
                (q.Contains("wie viel") || q.Contains("wieviel") || q.Contains("stand") || q.Contains("noch")))
            {
                if (!gs.FuelData.FuelUseActive || gs.FuelData.FuelLeft <= 0)
                {
                    return "Den Tankinhalt liefert die Simulation gerade nicht.";
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

            if (q.Contains("position") || q.Contains("welcher platz") || q.Contains("welchem platz") || q.Contains("wievielter") || q.Contains("wie vielter"))
            {
                int pos = gs.SessionData.ClassPosition > 0 ? gs.SessionData.ClassPosition : gs.SessionData.OverallPosition;
                if (pos <= 0)
                {
                    return "Die Position liefert die Simulation gerade nicht.";
                }
                int cars = gs.SessionData.NumCarsInPlayerClass;
                return cars > 0 ? "P" + pos + " von " + cars + "." : "P" + pos + ".";
            }

            if (q.Contains("letzte runde") || q.Contains("rundenzeit"))
            {
                float last = gs.SessionData.LapTimePrevious;
                if (last <= 0)
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
            var t = TimeSpan.FromSeconds(seconds);
            int minutes = (int)t.TotalMinutes;
            string secs = (seconds - minutes * 60).ToString("00.000", de);
            return minutes > 0 ? minutes + ":" + secs : (seconds.ToString("0.000", de));
        }
    }
}
