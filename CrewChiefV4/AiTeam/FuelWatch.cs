using System;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>The same balance, reserve and evidence are supplied to automatic radio and the language model.</summary>
    public sealed class FuelEstimate
    {
        public float PerLap, LapsRemaining, AdditionalWithoutReserve, ReserveLitres;
        public bool EstimatedFromTime;
        public string Source;
        public static FuelEstimate From(RaceDataContext ctx)
        {
            if (ctx == null || ctx.GameState == null || ctx.Memory == null) return null;
            var gs=ctx.GameState; var s=gs.SessionData;
            if (!gs.FuelData.FuelUseActive || !Finite(gs.FuelData.FuelLeft) || gs.FuelData.FuelLeft < 0 || s.SessionType!=SessionType.Race) return null;
            var laps=ctx.Memory.GetLaps().Where(l=>l.Valid && !l.InPitLap && l.FuelUsed>0).ToList();
            float perLap=ctx.Memory.AverageFuelPerLap();
            if (laps.Count<2 || !Finite(perLap) || perLap<=0) return null;
            float remaining; bool estimated=s.SessionHasFixedTime;
            if (!estimated && s.SessionLapsRemaining>0 && s.SessionLapsRemaining<1000) remaining=s.SessionLapsRemaining;
            else {
                var times=laps.Where(l=>Finite(l.LapTime) && l.LapTime>10).Reverse().Take(3).ToList();
                if (!estimated || times.Count==0 || !Finite(s.SessionTimeRemaining) || s.SessionTimeRemaining<=0) return null;
                remaining=s.SessionTimeRemaining/times.Average(l=>l.LapTime)+1;
            }
            bool crew=Finite(ctx.CrewChiefAdditionalFuelNeeded) && Math.Abs(ctx.CrewChiefAdditionalFuelNeeded)<1000;
            return new FuelEstimate { PerLap=perLap, LapsRemaining=remaining, EstimatedFromTime=estimated,
                AdditionalWithoutReserve=crew?ctx.CrewChiefAdditionalFuelNeeded:remaining*perLap-gs.FuelData.FuelLeft,
                ReserveLitres=perLap*FuelWatch.SafetyMarginLaps, Source=crew?"Crew-Chief-Bedarfsrechnung ohne Reserve":"Gemessener Verbrauch; Restdistanz bei Zeitrennen geschätzt" };
        }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
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
        public string Evaluate(int completedLaps, float fuelLeft, float perLap, float lapsRemaining, bool refuellingAllowed, bool estimatedFromTime, float additionalWithoutReserve = float.MaxValue)
        {
            if (fuelLeft < 0 || perLap <= 0 || lapsRemaining <= 0 ||
                float.IsNaN(fuelLeft) || float.IsInfinity(fuelLeft) ||
                float.IsNaN(perLap) || float.IsInfinity(perLap) ||
                float.IsNaN(lapsRemaining) || float.IsInfinity(lapsRemaining))
            {
                return null;
            }
            float bareDeficit = additionalWithoutReserve != float.MaxValue && !float.IsNaN(additionalWithoutReserve) && !float.IsInfinity(additionalWithoutReserve)
                ? additionalWithoutReserve : lapsRemaining * perLap - fuelLeft;
            float deficit = bareDeficit + SafetyMarginLaps * perLap;
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

            if (bareDeficit <= 0)
                return "Sprit reicht rechnerisch bis ins Ziel. Für unsere Reserve fehlen " + Litres(deficit) + ".";

            float savePerLap = deficit / Math.Max(1f, lapsRemaining);
            string distance = estimatedFromTime ? "nach aktueller Pace" : "bis ins Ziel";
            string text = (estimatedFromTime ? "Nach aktueller Rechnung wird der Sprit knapp. " : "So kommen wir " + distance + " nicht durch: ") +
                "Ohne Reserve fehlen etwa " + Litres(bareDeficit) + ". ";
            // mehr als ~8 % sparen ist kaum realistisch, dann lieber stoppen
            if (savePerLap <= perLap * 0.08f)
            {
                text += "Wir brauchen rund " + Litres(savePerLap) + " Einsparung pro Runde. Früher schalten und mehr rollen lassen.";
            }
            else if (refuellingAllowed)
            {
                text += estimatedFromTime ? "Stell dich auf einen Tankstopp ein. Die Restdistanz ist geschätzt." : "Mit Sparen allein ist das nicht realistisch, wir brauchen einen Tankstopp.";
            }
            else
            {
                text += "Nachtanken ist nicht erlaubt. Das Sparziel liegt über acht Prozent je Runde; " +
                    "damit ist das Ziel nach aktueller Rechnung nicht abgesichert.";
            }
            return text;
        }

        private static string Litres(float litres)
        {
            return litres < 1 ? litres.ToString("0.00", de) + " Liter" : litres.ToString(litres < 10 ? "0.0" : "0", de) + " Liter";
        }
    }
}
