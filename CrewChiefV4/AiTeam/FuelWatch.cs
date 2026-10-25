using System;
using System.Globalization;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Feste Sprit-Warnung für Mik, ohne KI: Reicht der Tank laut gemessenem Verbrauch nicht bis ins Ziel,
    /// meldet sich Mik sofort mit Fehlmenge und Sparziel pro Runde. Wiederholt sich nur, wenn es deutlich
    /// schlimmer wird oder alle 5 Runden, und meldet einmal, wenn es wieder reicht.
    /// </summary>
    public class FuelWatch
    {
        public const float SafetyMarginLaps = 0.3f;
        private static readonly CultureInfo de = CultureInfo.GetCultureInfo("de-DE");

        private bool warned;
        private float lastWarnedDeficit;
        private int lastWarnedLap = -100;

        public void Reset()
        {
            warned = false;
            lastWarnedDeficit = 0;
            lastWarnedLap = -100;
        }

        /// <summary>
        /// Nach jeder Runde aufrufen. Liefert den Funkspruch oder null.
        /// lapsRemaining = noch zu fahrende Runden inkl. der aktuellen (bei Zeitrennen geschätzt).
        /// </summary>
        public string Evaluate(int completedLaps, float fuelLeft, float perLap, float lapsRemaining, bool refuellingAllowed, bool estimatedFromTime)
        {
            if (fuelLeft <= 0 || perLap <= 0 || lapsRemaining <= 0 || float.IsNaN(lapsRemaining))
            {
                return null;
            }
            float needed = (lapsRemaining + SafetyMarginLaps) * perLap;
            float deficit = needed - fuelLeft;
            if (deficit <= 0)
            {
                if (warned)
                {
                    warned = false;
                    return "Sprit passt jetzt wieder bis ins Ziel, weiter so.";
                }
                return null;
            }
            bool worse = deficit > lastWarnedDeficit + perLap * 0.5f;
            if (warned && !worse && completedLaps - lastWarnedLap < 5)
            {
                return null;
            }
            warned = true;
            lastWarnedDeficit = deficit;
            lastWarnedLap = completedLaps;

            float savePerLap = deficit / Math.Max(1f, lapsRemaining);
            string distance = estimatedFromTime ? "nach aktueller Pace" : "bis ins Ziel";
            string text = "So kommen wir " + distance + " nicht durch, uns fehlen etwa " + Litres(deficit) + ". ";
            // mehr als ~8 % sparen ist kaum realistisch, dann lieber stoppen
            if (savePerLap <= perLap * 0.08f || !refuellingAllowed)
            {
                text += "Spar pro Runde " + Litres(savePerLap) + ", früher hochschalten und mehr rollen lassen.";
            }
            else
            {
                text += "Mit Sparen allein wird das knapp, wir planen einen Stopp.";
            }
            return text;
        }

        private static string Litres(float litres)
        {
            return litres < 1 ? litres.ToString("0.0", de) + " Liter" : litres.ToString(litres < 10 ? "0.0" : "0", de) + " Liter";
        }
    }
}
