using System;
using System.Globalization;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Saras direkte Antworten auf Sicherheitsfragen. Ausschließlich lokal und deterministisch aus
    /// der Crew-Chief-Spotterlogik – keine KI, kein Netzwerk. "Frei" nur mit frischen Daten.
    /// Die Echtzeit-Warnungen ("Links!", "Frei!") spricht weiterhin der originale Crew-Chief-Spotter.
    /// </summary>
    public static class SaraLocal
    {
        public const string NoDataAnswer = "Dafür hab ich gerade keine sicheren Spotterdaten. Selbst schauen.";

        /// <summary>Liefert die Antwort oder null, wenn die Frage keine Spotterfrage ist.</summary>
        public static string TryAnswer(string question, SpotterSnapshot spotter, GameStateData gs, DateTime nowUtc)
        {
            string q = " " + (question ?? "").ToLowerInvariant() + " ";
            bool askLeft = TeamRouter.HasWord(q, "links");
            bool askRight = TeamRouter.HasWord(q, "rechts");
            bool askBeside = TeamRouter.IsSideQuestion(q);

            if (askBeside)
            {
                if (!spotter.IsFresh(nowUtc))
                {
                    return NoDataAnswer;
                }
                if (askLeft && !askRight)
                {
                    return spotter.CarsLeft > 0 ? Count(spotter.CarsLeft) + " links." : "Links frei.";
                }
                if (askRight && !askLeft)
                {
                    return spotter.CarsRight > 0 ? Count(spotter.CarsRight) + " rechts." : "Rechts frei.";
                }
                if (spotter.CarsLeft > 0 && spotter.CarsRight > 0)
                {
                    return "Du bist eingeklemmt, links und rechts je einer.";
                }
                if (spotter.CarsLeft > 0)
                {
                    return Count(spotter.CarsLeft) + " links, rechts frei.";
                }
                if (spotter.CarsRight > 0)
                {
                    return Count(spotter.CarsRight) + " rechts, links frei.";
                }
                return "Frei, keiner neben dir.";
            }

            if (gs != null && (q.Contains("hinter mir") || q.Contains("hinter uns") || q.Contains("von hinten") || q.Contains("abstand hinten")))
            {
                float gap = gs.SessionData.TimeDeltaBehind;
                return gap > 0 && gap < 600 ? "Hinter dir " + Seconds(gap) + "." : "Den Abstand nach hinten liefert die Simulation gerade nicht.";
            }
            if (gs != null && (q.Contains("vor mir") || q.Contains("vor uns") || q.Contains("abstand vorne")))
            {
                float gap = gs.SessionData.TimeDeltaFront;
                return gap > 0 && gap < 600 ? "Vor dir " + Seconds(gap) + "." : "Den Abstand nach vorne liefert die Simulation gerade nicht.";
            }
            return null;
        }

        private static string Count(int cars)
        {
            return cars >= 2 ? "Noch zwei" : "Noch einer";
        }

        internal static string Seconds(float seconds)
        {
            var de = CultureInfo.GetCultureInfo("de-DE");
            if (seconds < 10)
            {
                return seconds.ToString("0.0", de) + " Sekunden";
            }
            return Math.Round(seconds).ToString("0", de) + " Sekunden";
        }
    }
}
