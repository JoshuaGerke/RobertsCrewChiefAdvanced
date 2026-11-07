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
                new[] { "okay", "ok", "bedenklich", "in ordnung", "noch gut" }.Any(q.Contains);
        }

        private static bool Good(float value, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= maximum;
        private static float Mean(float a, float b, float c) => a > 1 && b > 1 && c > 1 && Good(a, 500) && Good(b, 500) && Good(c, 500) ? (a + b + c) / 3 : -1;

        public static string Answer(GameStateData gs, DateTime now)
        {
            if (gs == null) return "Aktuell fehlen mir die Reifenwerte.";
            DateTime source = gs.SourceTelemetryUtc == DateTime.MinValue ? gs.Now : gs.SourceTelemetryUtc;
            if (now < source || now - source > TimeSpan.FromSeconds(2)) return "Aktuell fehlen mir frische Reifenwerte.";
            var t = gs.TyreData;
            float[] temps = { Mean(t.FrontLeft_LeftTemp, t.FrontLeft_CenterTemp, t.FrontLeft_RightTemp),
                Mean(t.FrontRight_LeftTemp, t.FrontRight_CenterTemp, t.FrontRight_RightTemp),
                Mean(t.RearLeft_LeftTemp, t.RearLeft_CenterTemp, t.RearLeft_RightTemp),
                Mean(t.RearRight_LeftTemp, t.RearRight_CenterTemp, t.RearRight_RightTemp) };
            bool temperaturesKnown = temps.All(v => v > 1 && Good(v, 500));
            if (temperaturesKnown && t.TyreTempStatus.hasValueAtLevel(TyreTemp.COOKING))
                return "Die Reifen sind überhitzt. Geh die nächsten Kurven ruhiger an.";
            if (temperaturesKnown && t.TyreTempStatus.hasValueAtLevel(TyreTemp.HOT))
                return "Die Reifen werden zu heiß. Nimm etwas Belastung raus.";
            if (t.TyreWearActive && t.TyreConditionStatus.hasValueAtLevel(TyreCondition.WORN_OUT))
                return "Die Reifen werden als abgefahren gemeldet. Geh vorsichtig mit ihnen um.";
            float[] wear = { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
            bool wearKnown = t.TyreWearActive && wear.All(v => Good(v, 100));
            string tempText = temperaturesKnown ? "Temperaturen " + Math.Round(temps.Min()).ToString(CultureInfo.InvariantCulture) +
                " bis " + Math.Round(temps.Max()).ToString(CultureInfo.InvariantCulture) + " Grad." : null;
            if (wearKnown)
            {
                int worst = Array.IndexOf(wear, wear.Max());
                string corner = new[] { "Vorn links", "Vorn rechts", "Hinten links", "Hinten rechts" }[worst];
                string wearText = corner + " liegt der höchste Verschleiß bei " + Math.Round(wear[worst]).ToString(CultureInfo.InvariantCulture) + " Prozent.";
                return wearText + (tempText == null ? " Die Temperaturen fehlen noch." : " " + tempText);
            }
            return tempText == null ? "Aktuell fehlen mir verlässliche Reifenwerte." : tempText + " Den Verschleiß liefert die Simulation gerade nicht.";
        }
    }
}
