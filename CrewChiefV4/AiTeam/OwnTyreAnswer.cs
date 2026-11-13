using System;
using System.Globalization;
using System.Linq;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    public static class OwnTyreAnswer
    {
        public static bool Matches(string question)
        {
            string q = RaceFieldTools.Normalize(question);
            return q.Contains("was machen meine reifen") || q.Contains("wie sind meine reifen") ||
                q.Contains("wie geht es meinen reifen") || q.Contains("sind meine reifen") &&
                new[] { "okay", "ok", "bedenklich", "in ordnung", "noch gut" }.Any(q.Contains) ||
                // Englisch und Spanisch (Normalize entfernt Akzente und Satzzeichen)
                new[] { "how are my tyres", "how are my tires", "what are my tyres doing", "what are my tires doing",
                    "como estan mis neumaticos", "como van mis neumaticos", "como estan mis gomas", "como van mis gomas",
                    "que tal mis neumaticos", "que tal mis gomas" }.Any(q.Contains) ||
                (q.Contains("are my tyres") || q.Contains("are my tires")) && new[] { "okay", "ok", "alright", "all right", "fine", "good" }.Any(q.Contains) ||
                (q.Contains("mis neumaticos") || q.Contains("mis gomas")) && q.Contains("bien");
        }

        private static bool Good(float value, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= maximum;
        private static float Mean(float a, float b, float c) => a > 1 && b > 1 && c > 1 && Good(a, 500) && Good(b, 500) && Good(c, 500) ? (a + b + c) / 3 : -1;

        public static string Answer(GameStateData gs, DateTime now)
        {
            if (gs == null) return RadioLanguage.T("Aktuell fehlen mir die Reifenwerte.", "I don't have tyre data right now.", "Ahora mismo no tengo datos de neumáticos.");
            DateTime source = gs.SourceTelemetryUtc == DateTime.MinValue ? gs.Now : gs.SourceTelemetryUtc;
            if (now < source || now - source > TimeSpan.FromSeconds(2))
                return RadioLanguage.T("Aktuell fehlen mir frische Reifenwerte.", "No fresh tyre data right now.", "Ahora mismo no tengo datos frescos de neumáticos.");
            var t = gs.TyreData;
            float[] temps = { Mean(t.FrontLeft_LeftTemp, t.FrontLeft_CenterTemp, t.FrontLeft_RightTemp),
                Mean(t.FrontRight_LeftTemp, t.FrontRight_CenterTemp, t.FrontRight_RightTemp),
                Mean(t.RearLeft_LeftTemp, t.RearLeft_CenterTemp, t.RearLeft_RightTemp),
                Mean(t.RearRight_LeftTemp, t.RearRight_CenterTemp, t.RearRight_RightTemp) };
            bool temperaturesKnown = temps.All(v => v > 1 && Good(v, 500));
            if (temperaturesKnown && t.TyreTempStatus.hasValueAtLevel(TyreTemp.COOKING))
                return RadioLanguage.T("Die Reifen sind überhitzt. Geh die nächsten Kurven ruhiger an.",
                    "Tyres are overheating. Take the next few corners easier.", "Los neumáticos están sobrecalentados. Tómate las próximas curvas con más calma.");
            if (temperaturesKnown && t.TyreTempStatus.hasValueAtLevel(TyreTemp.HOT))
                return RadioLanguage.T("Die Reifen werden zu heiß. Nimm etwas Belastung raus.",
                    "Tyres are getting too hot. Ease off a bit.", "Los neumáticos se están calentando demasiado. Quítales algo de carga.");
            if (t.TyreWearActive && t.TyreConditionStatus.hasValueAtLevel(TyreCondition.WORN_OUT))
                return RadioLanguage.T("Die Reifen werden als abgefahren gemeldet. Geh vorsichtig mit ihnen um.",
                    "Tyres are reported worn out. Look after them.", "Los neumáticos aparecen como gastados. Cuídalos.");
            float[] wear = { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
            bool wearKnown = t.TyreWearActive && wear.All(v => Good(v, 100));
            string minTemp = Math.Round(temps.Min()).ToString(CultureInfo.InvariantCulture), maxTemp = Math.Round(temps.Max()).ToString(CultureInfo.InvariantCulture);
            string tempText = temperaturesKnown ? RadioLanguage.T("Temperaturen " + minTemp + " bis " + maxTemp + " Grad.",
                "Temperatures " + minTemp + " to " + maxTemp + " degrees.", "Temperaturas de " + minTemp + " a " + maxTemp + " grados.") : null;
            if (wearKnown)
            {
                int worst = Array.IndexOf(wear, wear.Max());
                string percent = Math.Round(wear[worst]).ToString(CultureInfo.InvariantCulture);
                string wearText = RadioLanguage.T(
                    new[] { "Vorn links", "Vorn rechts", "Hinten links", "Hinten rechts" }[worst] + " liegt der höchste Verschleiß bei " + percent + " Prozent.",
                    new[] { "Front left", "Front right", "Rear left", "Rear right" }[worst] + " has the most wear, " + percent + " percent.",
                    "El " + new[] { "delantero izquierdo", "delantero derecho", "trasero izquierdo", "trasero derecho" }[worst] + " tiene el mayor desgaste, " + percent + " por ciento.");
                return wearText + (tempText == null ? RadioLanguage.T(" Die Temperaturen fehlen noch.", " No temperatures yet.", " Aún faltan las temperaturas.") : " " + tempText);
            }
            return tempText == null ? RadioLanguage.T("Aktuell fehlen mir verlässliche Reifenwerte.", "No reliable tyre data right now.", "Ahora mismo no tengo datos fiables de neumáticos.") :
                tempText + RadioLanguage.T(" Den Verschleiß liefert die Simulation gerade nicht.", " The sim isn't reporting wear right now.", " El simulador no está dando el desgaste ahora mismo.");
        }
    }
}
