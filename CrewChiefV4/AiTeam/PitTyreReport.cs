using System;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// IR3 (Rob 28.11., iRacing Navarra): „Wenig Reifen-Infos.“ iRacing liefert Reifentemperatur und Verschleiß für die meisten Autos
    /// nur beim Halt in der Box. Genau dann sagt Mik kurz das Reifenbild: Schnitt vorn und hinten, die auffälligste Verteilung
    /// (innen/Mitte/außen) mit vorsichtiger Deutung und welcher Reifen am meisten abbaut. Die Werte gelten danach als
    /// „Stand letzter Boxenhalt“ (N9) und werden nie als aktuell wiederholt.
    /// </summary>
    internal static class PitTyreReport
    {
        internal sealed class Reading
        {
            /// <summary>Je Reifen VL, VR, HL, HR in °C; innen = zur Wagenmitte.</summary>
            public float[] Inner = new float[4], Middle = new float[4], Outer = new float[4];
            /// <summary>Verschleiß in Prozent (0 = neu); NaN, wenn nicht geliefert.</summary>
            public float[] Wear = { float.NaN, float.NaN, float.NaN, float.NaN };
        }

        private static readonly object gate = new object();
        private static DateTime lastReadingUtc = DateTime.MinValue;

        /// <summary>Wann die Crew zuletzt ein Reifenbild aus der Box hatte (für die Antwort „Stand letzter Boxenhalt“).</summary>
        internal static DateTime LastReadingUtc { get { lock (gate) return lastReadingUtc; } }
        internal static void NoteReading(DateTime utc) { lock (gate) lastReadingUtc = utc; }
        internal static void Reset() { lock (gate) lastReadingUtc = DateTime.MinValue; }

        /// <summary>Reifenwerte, die nur beim Boxenhalt kommen: iRacing.</summary>
        internal static bool Applies() => Game.gameType == GameType.IRACING;

        internal static Reading Read(GameStateData gs)
        {
            var t = gs?.TyreData;
            if (t == null) return null;
            var r = new Reading();
            // Linke Reifen: rechte Seite ist innen; rechte Reifen: linke Seite ist innen.
            r.Inner[0] = t.FrontLeft_RightTemp; r.Middle[0] = t.FrontLeft_CenterTemp; r.Outer[0] = t.FrontLeft_LeftTemp;
            r.Inner[1] = t.FrontRight_LeftTemp; r.Middle[1] = t.FrontRight_CenterTemp; r.Outer[1] = t.FrontRight_RightTemp;
            r.Inner[2] = t.RearLeft_RightTemp; r.Middle[2] = t.RearLeft_CenterTemp; r.Outer[2] = t.RearLeft_LeftTemp;
            r.Inner[3] = t.RearRight_LeftTemp; r.Middle[3] = t.RearRight_CenterTemp; r.Outer[3] = t.RearRight_RightTemp;
            if (t.TyreWearActive)
            {
                r.Wear[0] = t.FrontLeftPercentWear; r.Wear[1] = t.FrontRightPercentWear; r.Wear[2] = t.RearLeftPercentWear; r.Wear[3] = t.RearRightPercentWear;
            }
            return Plausible(r) ? r : null;
        }

        internal static bool Plausible(Reading r) =>
            r != null && r.Inner.Concat(r.Middle).Concat(r.Outer).All(v => !float.IsNaN(v) && v >= 10 && v <= 200);

        /// <summary>Hat sich das Reifenbild seit dem letzten Stand geändert (iRacing hat beim Halt neu gemessen)?</summary>
        internal static bool Changed(Reading before, Reading after)
        {
            if (after == null) return false;
            if (before == null) return true;
            for (int i = 0; i < 4; i++)
                if (Math.Abs(before.Inner[i] - after.Inner[i]) > .5f || Math.Abs(before.Middle[i] - after.Middle[i]) > .5f || Math.Abs(before.Outer[i] - after.Outer[i]) > .5f)
                    return true;
            return false;
        }

        private static string Corner(int i) => i == 0 ? RadioLanguage.T("vorne links", "front left", "delantera izquierda") :
            i == 1 ? RadioLanguage.T("vorne rechts", "front right", "delantera derecha") :
            i == 2 ? RadioLanguage.T("hinten links", "rear left", "trasera izquierda") : RadioLanguage.T("hinten rechts", "rear right", "trasera derecha");

        private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        private static float Average(Reading r, int a, int b) =>
            (r.Inner[a] + r.Middle[a] + r.Outer[a] + r.Inner[b] + r.Middle[b] + r.Outer[b]) / 6f;

        /// <summary>
        /// Auffälligste Verteilung: Mitte mindestens 4 °C über/unter dem Schnitt der Flanken (Druck), innen mindestens 12 °C heißer
        /// oder kühler als außen (Sturz). Vorsichtig formuliert („deutet auf“), weil es eine Momentaufnahme beim Halt ist.
        /// </summary>
        internal static string Distribution(Reading r)
        {
            int best = -1; float score = 0; string text = null;
            for (int i = 0; i < 4; i++)
            {
                float edges = (r.Inner[i] + r.Outer[i]) / 2, centre = r.Middle[i] - edges, camber = r.Inner[i] - r.Outer[i];
                if (Math.Abs(centre) >= 4 && Math.Abs(centre) > score)
                {
                    best = i; score = Math.Abs(centre);
                    text = centre > 0
                        ? RadioLanguage.T(": Mitte heißer als die Flanken, deutet auf zu viel Druck.", ": centre hotter than the edges, points to too much pressure.", ": el centro más caliente que los bordes, apunta a demasiada presión.")
                        : RadioLanguage.T(": Mitte kühler als die Flanken, deutet auf zu wenig Druck.", ": centre cooler than the edges, points to too little pressure.", ": el centro más frío que los bordes, apunta a poca presión.");
                }
                if (camber >= 12 && camber - 8 > score)
                {
                    best = i; score = camber - 8;
                    text = RadioLanguage.T(": innen deutlich heißer als außen, viel Sturz.", ": inside much hotter than outside, lots of camber.", ": el interior mucho más caliente que el exterior, mucha caída.");
                }
                else if (camber <= -4 && -camber > score)
                {
                    best = i; score = -camber;
                    text = RadioLanguage.T(": außen heißer als innen, eher zu wenig Sturz.", ": outside hotter than inside, rather too little camber.", ": el exterior más caliente que el interior, más bien poca caída.");
                }
            }
            return best < 0 ? RadioLanguage.T("Die Verteilung innen, Mitte, außen ist unauffällig.", "Inside, middle and outside look even.", "Interior, centro y exterior se ven equilibrados.")
                : Capital(Corner(best)) + text;
        }

        internal static string WearLine(Reading r)
        {
            if (r.Wear.Any(w => float.IsNaN(w) || w < 0 || w > 100)) return null;
            int most = 0;
            for (int i = 1; i < 4; i++) if (r.Wear[i] > r.Wear[most]) most = i;
            if (r.Wear[most] < 1) return RadioLanguage.T("Verschleiß kaum messbar.", "Wear barely measurable.", "Desgaste apenas medible.");
            int left = (int)Math.Round(100 - r.Wear[most]);
            return RadioLanguage.T("Am meisten abgebaut hat " + Corner(most) + ", noch " + left + " Prozent.",
                "Most worn is " + Corner(most) + ", " + left + " percent left.",
                "La más gastada es la " + Corner(most) + ", queda un " + left + " por ciento.");
        }

        /// <summary>Zwei bis drei kurze Sätze für Mik; null, wenn die Werte nicht plausibel sind.</summary>
        internal static string Compose(Reading r, int variant)
        {
            if (!Plausible(r)) return null;
            int front = (int)Math.Round(Average(r, 0, 1)), rear = (int)Math.Round(Average(r, 2, 3));
            string head = RadioVariants.At(variant,
                RadioLanguage.T("Reifenbild vom Halt: vorn im Schnitt " + front + " Grad, hinten " + rear + ". ", "Tyre picture from the stop: fronts average " + front + " degrees, rears " + rear + ". ", "Neumáticos en la parada: delante una media de " + front + " grados, detrás " + rear + ". "),
                RadioLanguage.T("Reifencheck aus der Box: vorn " + front + ", hinten " + rear + " Grad. ", "Tyre check from the box: fronts " + front + ", rears " + rear + " degrees. ", "Control de neumáticos en boxes: delante " + front + ", detrás " + rear + " grados. "),
                RadioLanguage.T("Die Reifen beim Stopp: vorn " + front + " Grad, hinten " + rear + ". ", "Tyres at the stop: fronts " + front + " degrees, rears " + rear + ". ", "Los neumáticos en la parada: delante " + front + " grados, detrás " + rear + ". "));
            string wear = WearLine(r);
            return head + Distribution(r) + (wear == null ? "" : " " + wear);
        }
    }
}
