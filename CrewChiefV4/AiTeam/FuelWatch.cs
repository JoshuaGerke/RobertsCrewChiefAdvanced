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
                    return RadioLanguage.T("Sprit passt jetzt wieder bis ins Ziel, weiter so.", "Fuel is good to the finish again, keep it up.",
                        "El combustible vuelve a llegar hasta meta, sigue así.");
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
                return RadioLanguage.T("Sprit reicht rechnerisch bis ins Ziel. Für unsere Reserve fehlen " + Litres(deficit) + ".",
                    "On paper the fuel reaches the finish. We're " + Litres(deficit) + " short of our reserve.",
                    "En teoría el combustible llega a meta. Para nuestra reserva faltan " + Litres(deficit) + ".");

            float savePerLap = deficit / Math.Max(1f, lapsRemaining);
            string distance = estimatedFromTime ? "nach aktueller Pace" : "bis ins Ziel";
            string text = RadioLanguage.T(
                (estimatedFromTime ? "Nach aktueller Rechnung wird der Sprit knapp. " : "So kommen wir " + distance + " nicht durch: ") +
                    "Ohne Reserve fehlen etwa " + Litres(bareDeficit) + ". ",
                (estimatedFromTime ? "On current numbers, fuel is getting tight. " : "We won't make it to the finish like this: ") +
                    "Without reserve we're about " + Litres(bareDeficit) + " short. ",
                (estimatedFromTime ? "Según el cálculo actual, el combustible va justo. " : "Así no llegamos a meta: ") +
                    "Sin reserva faltan unos " + Litres(bareDeficit) + ". ");
            // mehr als ~8 % sparen ist kaum realistisch, dann lieber stoppen
            if (savePerLap <= perLap * 0.08f)
            {
                text += RadioLanguage.T("Wir brauchen rund " + Litres(savePerLap) + " Einsparung pro Runde. Früher schalten und mehr rollen lassen.",
                    "We need to save around " + Litres(savePerLap) + " per lap. Shift earlier and coast more.",
                    "Necesitamos ahorrar unos " + Litres(savePerLap) + " por vuelta. Cambia antes y deja rodar más.");
            }
            else if (refuellingAllowed)
            {
                text += estimatedFromTime
                    ? RadioLanguage.T("Stell dich auf einen Tankstopp ein. Die Restdistanz ist geschätzt.", "Expect a fuel stop. The remaining distance is an estimate.",
                        "Prepárate para una parada a repostar. La distancia restante es una estimación.")
                    : RadioLanguage.T("Mit Sparen allein ist das nicht realistisch, wir brauchen einen Tankstopp.", "Saving alone won't do it, we need a fuel stop.",
                        "Solo ahorrando no es realista, necesitamos una parada a repostar.");
            }
            else
            {
                text += RadioLanguage.T("Nachtanken ist nicht erlaubt. Das Sparziel liegt über acht Prozent je Runde; " +
                    "damit ist das Ziel nach aktueller Rechnung nicht abgesichert.",
                    "Refuelling isn't allowed. The saving target is over eight percent per lap, so on current numbers the finish isn't secured.",
                    "No se permite repostar. El objetivo de ahorro supera el ocho por ciento por vuelta; con el cálculo actual, la llegada a meta no está asegurada.");
            }
            return text;
        }

        private static string Litres(float litres)
        {
            string unit = RadioLanguage.T(" Liter", " litres", " litros");
            return litres < 1 ? RadioLanguage.Num(litres, "0.00") + unit : RadioLanguage.Num(litres, litres < 10 ? "0.0" : "0") + unit;
        }
    }
}
