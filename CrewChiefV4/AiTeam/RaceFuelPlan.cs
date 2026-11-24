using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Spritmenge für eine vom Fahrer genannte Renndistanz (LMU-Test 18.11.2026). Rob: „Geh von den zweieinhalb Litern aus und
    /// rechne hoch, was das bei 30 Minuten bedeutet.“ Die Crew verweigerte die Rechnung, weil die Renndistanz noch nicht aus
    /// dem Spiel kam. Die Distanz des Fahrers ist eine belegte Vorgabe: gerechnet wird mit gemessenem Verbrauch (sonst dem
    /// genannten) und der eigenen Rundenzeit; im Zeitrennen eine Runde nach Ablauf, dazu eine Runde Reserve.
    /// </summary>
    internal static class RaceFuelPlan
    {
        private static readonly Regex minutes = new Regex(@"(\d+(?:[.,]\d+)?)\s*(?:minuten|minute|min\b|minutes|minute|minutos|minuto)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex hours = new Regex(@"(\d+(?:[.,]\d+)?)\s*(?:stunden|stunde|std\b|hours|hour|horas|hora)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex laps = new Regex(@"(\d+)\s*(?:runden|runde|laps|lap|vueltas|vuelta)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex litres = new Regex(@"(\d+(?:[.,]\d+)?)\s*(?:liter|litern|litre|litres|liters|litros|l\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static bool AsksFuelForDistance(string q)
        {
            string s = (q ?? "").ToLowerInvariant();
            bool fuel = new[] { "sprit", "benzin", "kraftstoff", "tank", "liter", "fuel", "combustible", "gasolina", "litros" }.Any(s.Contains);
            bool plan = new[] { "mitnehm", "tanken", "brauch", "rechne", "hochrechn", "soll ich", "wie viel", "wieviel", "how much", "take", "need", "carry",
                "cuánto", "cuanto", "llevar", "necesito", "calcula" }.Any(s.Contains);
            return fuel && plan && (minutes.IsMatch(s) || hours.IsMatch(s) || laps.IsMatch(s) || s.Contains("halbe stunde") || s.Contains("half an hour") || s.Contains("media hora"));
        }

        private static double Number(string text) => double.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);

        /// <summary>Genannte Dauer in Sekunden oder genannte Runden; beides -1, wenn nichts erkannt.</summary>
        internal static void Distance(string q, out double seconds, out int lapCount)
        {
            string s = (q ?? "").ToLowerInvariant();
            seconds = -1; lapCount = -1;
            var h = hours.Match(s); var m = minutes.Match(s); var l = laps.Match(s);
            if (h.Success) seconds = Number(h.Groups[1].Value) * 3600;
            if (m.Success) seconds = Math.Max(0, seconds) + Number(m.Groups[1].Value) * 60;
            if (seconds < 0 && (s.Contains("halbe stunde") || s.Contains("half an hour") || s.Contains("media hora"))) seconds = 1800;
            if (seconds < 0 && l.Success) lapCount = int.Parse(l.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>Vom Fahrer genannter Verbrauch pro Runde (auch „zweieinhalb“), sonst -1.</summary>
        internal static float StatedPerLap(string q)
        {
            string s = (q ?? "").ToLowerInvariant();
            var m = litres.Match(s);
            if (m.Success) { double v = Number(m.Groups[1].Value); if (v > .2 && v < 30) return (float)v; }
            if (s.Contains("zweieinhalb") || s.Contains("two and a half") || s.Contains("dos y medio")) return 2.5f;
            if (s.Contains("dreieinhalb") || s.Contains("three and a half") || s.Contains("tres y medio")) return 3.5f;
            return -1;
        }

        /// <summary>Rundenzahl und Liter. Zeitrennen: angefangene Runden plus eine nach Ablauf; dazu eine Runde Reserve.</summary>
        internal static bool Compute(double seconds, int lapCount, float lapTime, float perLap, out int raceLaps, out double needed, out double withReserve)
        {
            raceLaps = -1; needed = withReserve = -1;
            if (!(perLap > .2f && perLap < 30)) return false;
            if (lapCount > 0) raceLaps = lapCount;
            else if (seconds > 0 && lapTime > 10 && lapTime < 3600) raceLaps = (int)Math.Ceiling(seconds / lapTime) + 1;
            else return false;
            needed = raceLaps * perLap;
            withReserve = Math.Ceiling(needed + perLap);
            return true;
        }

        internal static float LapTimeBasis(AiRaceMemory memory, GameStateData gs)
        {
            var clean = memory?.GetLaps().Where(l => l.Valid && !l.InPitLap && l.LapTime > 10 && l.LapTime < 3600).Select(l => l.LapTime).ToList();
            if (clean != null && clean.Count >= 2) return clean.OrderBy(t => t).Take(3).Average();
            var s = gs?.SessionData;
            if (s != null && s.PlayerLapTimeSessionBest > 10 && s.PlayerLapTimeSessionBest < 3600) return s.PlayerLapTimeSessionBest;
            return -1;
        }

        /// <summary>Herkunft des Verbrauchs für den Funksatz: gemessen, hochgerechnet oder aus Training und Quali geschätzt.</summary>
        internal static string BasisText(FuelEstimate.Basis source)
        {
            switch (source)
            {
                case FuelEstimate.Basis.Measured: return RadioLanguage.T("gemessen", "measured", "medido");
                case FuelEstimate.Basis.Projected: return RadioLanguage.T("hochgerechnet, noch keine volle Runde", "projected, no full lap yet", "proyectado, aún sin vuelta completa");
                default: return RadioLanguage.T("geschätzt aus Training und Quali", "estimated from practice and qualifying", "estimado de entrenamientos y clasificación");
            }
        }

        internal static string Answer(string question, GameStateData gs, AiRaceMemory memory)
        {
            Distance(question, out double seconds, out int lapCount);
            if (seconds <= 0 && lapCount <= 0) return null;
            float measured = FuelEstimate.Consumption(memory, gs, out bool provisional, out _, out var source);
            float stated = StatedPerLap(question);
            bool useMeasured = measured > 0 && !provisional, useStated = !useMeasured && stated > 0;
            float perLap = useMeasured ? measured : useStated ? stated : measured;
            float lap = LapTimeBasis(memory, gs);
            // LMU Sebring 23.11.: Zu Rennbeginn fehlte die Rundenzeit der neuen Sitzung, die schnelle Antwort sagte „Mir fehlt noch eine
            // Rundenzeit“, obwohl Training und Quali Verbrauch und Zeiten hatten. Ohne eigene Grundlage antwortet jetzt das Sprachmodell
            // mit dem Lagebild, das die Werte der vorherigen Sitzung enthält.
            if (!Compute(seconds, lapCount, lap, perLap, out int raceLaps, out double needed, out double reserve))
                return null;
            string pl = RadioLanguage.Num(perLap, "0.0"), n = RadioLanguage.Num(needed, "0"), r = RadioLanguage.Num(reserve, "0");
            // Review 24.11.: Eine Schätzung der Crew hieß bisher „dein Wert“, obwohl Rob keine Zahl genannt hatte (Prüfliste 9).
            string basis = useStated ? RadioLanguage.T("dein Wert", "your figure", "tu valor") : BasisText(source);
            if (lapCount > 0)
                return RadioLanguage.T(raceLaps + " Runden mit " + pl + " Litern (" + basis + ") sind " + n + " Liter. Mit einer Runde Reserve nimm " + r + " Liter mit.",
                    raceLaps + " laps at " + pl + " litres (" + basis + ") is " + n + " litres. With one lap in reserve, take " + r + " litres.",
                    raceLaps + " vueltas a " + pl + " litros (" + basis + ") son " + n + " litros. Con una vuelta de reserva, lleva " + r + " litros.");
            string lt = LocalAnswers.LapTime(lap), mins = RadioLanguage.Num(seconds / 60, "0");
            return RadioLanguage.T("Bei " + mins + " Minuten und " + lt + " pro Runde sind das " + raceLaps + " Runden, eine nach Ablauf schon eingerechnet. Mit " + pl + " Litern (" + basis + ") brauchst du " + n + " Liter, mit einer Runde Reserve " + r + " Liter.",
                mins + " minutes at " + lt + " a lap makes " + raceLaps + " laps, including one after the clock. At " + pl + " litres (" + basis + ") you need " + n + " litres, " + r + " with one lap in reserve.",
                "Con " + mins + " minutos y " + lt + " por vuelta son " + raceLaps + " vueltas, ya contando una tras el tiempo. Con " + pl + " litros (" + basis + ") necesitas " + n + " litros, " + r + " con una vuelta de reserva.");
        }
    }
}
