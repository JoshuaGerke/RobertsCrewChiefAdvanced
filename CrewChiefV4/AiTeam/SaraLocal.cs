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
        public static string NoDataAnswer => RadioLanguage.T("Dafür hab ich gerade keine sicheren Spotterdaten. Selbst schauen.",
            "No reliable spotter data right now. Check yourself.", "Ahora mismo no tengo datos fiables del spotter. Compruébalo tú.");

        /// <summary>Alle festen Spotter-Antworten in der aktuellen Funksprache (für vorbereitete Clips).</summary>
        public static string[] PreparedPhrases => new[] { Clear(true) + ".", Clear(false) + ".", Cars(1, true) + ".", Cars(1, false) + ".",
            Cars(2, true) + ".", Cars(2, false) + ".", AllClear, Cars(1, true) + ", " + ClearTail(false) + ".", Cars(1, false) + ", " + ClearTail(true) + ".", NoDataAnswer };

        /// <summary>Liefert die Antwort oder null, wenn die Frage keine Spotterfrage ist.</summary>
        public static string TryAnswer(string question, SpotterSnapshot spotter, GameStateData gs, DateTime nowUtc)
        {
            string q = " " + (question ?? "").ToLowerInvariant() + " ";
            bool askLeft = TeamRouter.AsksLeft(q);
            bool askRight = TeamRouter.AsksRight(q);
            bool askBeside = TeamRouter.IsSideQuestion(q);

            if (askBeside)
            {
                if (!spotter.IsFresh(nowUtc))
                {
                    return NoDataAnswer;
                }
                if (askLeft && !askRight)
                {
                    return spotter.CarsLeft > 0 ? Cars(spotter.CarsLeft, true) + "." : Clear(true) + ".";
                }
                if (askRight && !askLeft)
                {
                    return spotter.CarsRight > 0 ? Cars(spotter.CarsRight, false) + "." : Clear(false) + ".";
                }
                if (spotter.CarsLeft > 0 && spotter.CarsRight > 0)
                {
                    return RadioLanguage.T("Du bist eingeklemmt, links und rechts je einer.", "You're three-wide, one on each side.", "Estás encajonado, uno a cada lado.");
                }
                if (spotter.CarsLeft > 0)
                {
                    return Cars(spotter.CarsLeft, true) + ", " + ClearTail(false) + ".";
                }
                if (spotter.CarsRight > 0)
                {
                    return Cars(spotter.CarsRight, false) + ", " + ClearTail(true) + ".";
                }
                return AllClear;
            }

            if (gs != null && (q.Contains("hinter mir") || q.Contains("hinter uns") || q.Contains("von hinten") || q.Contains("abstand hinten") ||
                q.Contains("behind me") || q.Contains("behind us") || q.Contains("from behind") || q.Contains("gap behind") || q.Contains("car behind") ||
                q.Contains("detrás") || q.Contains("detras") || q.Contains("atrás") || q.Contains("atras")))
            {
                float gap = gs.SessionData.TimeDeltaBehind;
                return gap > 0 && gap < 600 ? RadioLanguage.T("Hinter dir ", "Gap behind ", "Detrás, a ") + Seconds(gap) + "." :
                    RadioLanguage.T("Den Abstand nach hinten liefert die Simulation gerade nicht.", "The sim isn't giving the gap behind right now.",
                        "El simulador no da ahora mismo la distancia por detrás.");
            }
            if (gs != null && (q.Contains("vor mir") || q.Contains("vor uns") || q.Contains("abstand vorne") ||
                q.Contains("ahead of me") || q.Contains("ahead of us") || q.Contains("in front of me") || q.Contains("in front of us") ||
                q.Contains("gap ahead") || q.Contains("gap in front") || q.Contains("car ahead") || q.Contains("car in front") ||
                q.Contains("delante") || q.Contains("adelante")))
            {
                float gap = gs.SessionData.TimeDeltaFront;
                return gap > 0 && gap < 600 ? RadioLanguage.T("Vor dir ", "Gap ahead ", "Delante, a ") + Seconds(gap) + "." :
                    RadioLanguage.T("Den Abstand nach vorne liefert die Simulation gerade nicht.", "The sim isn't giving the gap ahead right now.",
                        "El simulador no da ahora mismo la distancia por delante.");
            }
            return null;
        }

        private static string AllClear => RadioLanguage.T("Frei, keiner neben dir.", "Clear, nobody alongside.", "Libre, no hay nadie a tu lado.");

        /// <summary>"Noch einer links" usw., ohne Satzpunkt.</summary>
        private static string Cars(int cars, bool left)
        {
            bool two = cars >= 2;
            return RadioLanguage.T((two ? "Noch zwei" : "Noch einer") + (left ? " links" : " rechts"),
                (two ? "Still two on your " : "Still one on your ") + (left ? "left" : "right"),
                (two ? "Siguen dos a tu " : "Sigue uno a tu ") + (left ? "izquierda" : "derecha"));
        }

        /// <summary>"Links frei" am Satzanfang.</summary>
        private static string Clear(bool left)
        {
            string tail = ClearTail(left);
            return char.ToUpperInvariant(tail[0]) + tail.Substring(1);
        }

        /// <summary>"links frei" als zweiter Satzteil.</summary>
        private static string ClearTail(bool left)
        {
            return RadioLanguage.T(left ? "links frei" : "rechts frei", left ? "clear left" : "clear right", left ? "libre a la izquierda" : "libre a la derecha");
        }

        internal static string Seconds(float seconds)
        {
            var culture = RadioLanguage.Culture;
            string unit = RadioLanguage.T(" Sekunden", " seconds", " segundos");
            if (seconds < 10)
            {
                return seconds.ToString("0.0", culture) + unit;
            }
            return Math.Round(seconds).ToString("0", culture) + unit;
        }
    }
}
