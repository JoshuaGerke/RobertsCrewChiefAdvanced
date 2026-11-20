using System;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Verbrauch aus Training und Quali derselben Strecke mit demselben Auto (Rob 20.11.2026: „Ihr müsst die durchschnittlichen
    /// Verbräuche aus Practice und Quali mit rübernehmen bis in die Rennen“). Gilt nur in dieser Programmsitzung; sobald im Rennen
    /// gemessen ist, zählt die Messung.
    /// </summary>
    internal static class FuelCarry
    {
        private static readonly object gate = new object();
        private static string key;
        private static float perLap = -1;
        private static int laps;

        internal static string Key(GameStateData gs)
        {
            // Fahrzeugname zuerst: eine unbekannte Klasse bekommt je Sitzung eine neue Platzhalter-Kennung.
            string track = gs?.SessionData?.TrackDefinition?.name, car = !string.IsNullOrWhiteSpace(gs?.carName) ? gs.carName : gs?.carClass?.getClassIdentifier();
            return string.IsNullOrWhiteSpace(track) || string.IsNullOrWhiteSpace(car) ? null : track + "|" + car;
        }

        /// <summary>Nach jeder Runde außerhalb des Rennens: Mittel aus mindestens zwei gemessenen Runden merken.</summary>
        internal static void Remember(GameStateData gs, float average, int count)
        {
            if (gs == null || gs.SessionData.SessionType == SessionType.Race || count < 2 || !(average > .05f && average < 100)) return;
            string k = Key(gs);
            if (k == null) return;
            lock (gate) { key = k; perLap = average; laps = count; }
        }

        internal static float For(GameStateData gs)
        {
            string k = Key(gs);
            lock (gate) return k != null && k == key ? perLap : -1;
        }

        internal static int Laps(GameStateData gs) { string k = Key(gs); lock (gate) return k != null && k == key ? laps : 0; }

        internal static void Reset() { lock (gate) { key = null; perLap = -1; laps = 0; } }
    }

    /// <summary>The same balance, reserve and evidence are supplied to automatic radio and the language model.</summary>
    public sealed class FuelEstimate
    {
        public float PerLap, LapsRemaining, AdditionalWithoutReserve, ReserveLitres;
        public bool EstimatedFromTime;
        public bool ProvisionalConsumption;
        public string Source;

        internal static float Consumption(AiRaceMemory memory, GameStateData state, out bool provisional, out int fullLaps)
        {
            provisional = false;
            fullLaps = 0;
            if (memory == null) return -1;
            float average = memory.AverageFuelPerLap(out fullLaps);
            if (fullLaps > 0) return average;
            float estimate = memory.ProvisionalFuelPerLap(state);
            provisional = estimate > 0 && Finite(estimate);
            if (provisional) return estimate;
            // Rob 20.11.: Verbrauch aus Training und Quali ins Rennen mitnehmen, bis im Rennen selbst gemessen ist.
            float carried = FuelCarry.For(state);
            provisional = carried > 0;
            return provisional ? carried : -1;
        }

        public static FuelEstimate From(RaceDataContext ctx, int minimumMeasuredLaps = 2)
        {
            if (ctx == null || ctx.GameState == null || ctx.Memory == null) return null;
            var gs=ctx.GameState; var s=gs.SessionData;
            if (!gs.FuelData.FuelUseActive || !Finite(gs.FuelData.FuelLeft) || gs.FuelData.FuelLeft < 0 || s.SessionType!=SessionType.Race) return null;
            float perLap = Consumption(ctx.Memory, gs, out bool provisional, out int sampleCount);
            if ((!provisional && sampleCount < minimumMeasuredLaps) || !Finite(perLap) || perLap <= 0) return null;

            float remaining;
            bool estimated = s.SessionHasFixedTime;
            if (estimated)
            {
                var times = ctx.Memory.GetLaps().Where(l => !l.InPitLap && Finite(l.LapTime) &&
                    l.LapTime > 10 && l.LapTime < 3600).Reverse().Take(3).ToArray();
                if (times.Length == 0 || !Finite(s.SessionTimeRemaining) || s.SessionTimeRemaining <= 0) return null;
                remaining = s.SessionTimeRemaining / times.Average(l => l.LapTime) + 1;
            }
            else
            {
                if (s.SessionLapsRemaining <= 0 || s.SessionLapsRemaining >= 1000) return null;
                remaining = s.SessionLapsRemaining;
                float trackLength = s.TrackDefinition?.trackLength ?? 0;
                float trackDistance = gs.PositionAndMotionData.DistanceRoundTrack;
                if (Finite(trackLength) && trackLength > 0 && Finite(trackDistance) &&
                    trackDistance >= 0 && trackDistance < trackLength &&
                    s.OverallPosition == 1 && s.SessionPhase == SessionPhase.Green && !gs.PitData.InPitlane)
                {
                    // The mapped lap count follows the overall leader. Only our own track
                    // progress can refine that count when we are that leader.
                    remaining -= trackDistance / trackLength;
                }
            }
            bool crew = !provisional && Finite(ctx.CrewChiefAdditionalFuelNeeded) && Math.Abs(ctx.CrewChiefAdditionalFuelNeeded)<1000;
            return new FuelEstimate { PerLap=perLap, LapsRemaining=remaining, EstimatedFromTime=estimated,
                ProvisionalConsumption=provisional,
                AdditionalWithoutReserve=crew?ctx.CrewChiefAdditionalFuelNeeded:remaining*perLap-gs.FuelData.FuelLeft,
                ReserveLitres=perLap*FuelWatch.SafetyMarginLaps,
                Source=provisional && !(ctx.Memory.ProvisionalFuelPerLap(gs) > 0) && FuelCarry.For(gs) == perLap ? "Verbrauch aus Training und Quali dieser Strecke, im Rennen noch nicht gemessen" :
                    provisional ? "Vorläufige Schätzung aus Tankdifferenz und gefahrener Strecke" :
                    crew ? "Crew-Chief-Bedarfsrechnung ohne Reserve" : "Gemessener Verbrauch; Restdistanz bei Zeitrennen geschätzt" };
        }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

        /// <summary>Kurzes Urteil für den regelmäßigen Spritbericht. Die ausführliche Warnung mit Sparziel kommt von FuelWatch.</summary>
        public string Verdict()
        {
            if (!Finite(PerLap) || PerLap <= 0 || !Finite(AdditionalWithoutReserve)) return null;
            if (AdditionalWithoutReserve + ReserveLitres <= 0)
                return EstimatedFromTime ? RadioLanguage.T("Nach aktueller Pace reicht das bis ins Ziel.", "At the current pace that gets us to the finish.", "Al ritmo actual llegamos a meta.")
                    : RadioLanguage.T("Das reicht bis ins Ziel.", "That gets us to the finish.", "Con eso llegamos a meta.");
            if (AdditionalWithoutReserve <= 0)
                return RadioLanguage.T("Bis ins Ziel wird es knapp.", "It's going to be tight to the finish.", "Hasta meta va a ir justo.");
            return RadioLanguage.T("So reicht es nicht bis ins Ziel.", "That won't get us to the finish.", "Así no llegamos a meta.");
        }
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
        private bool lastWarningWasProvisional;
        private bool lastWarningWasDeficit;
        private float lastWarnedDeficit;
        private int lastWarnedLap = -100;
        // Bathurst 17.11.: „könnte reichen“ und 15 s später „könnte knapp werden“. Eine vorläufige Hochrechnung gibt es
        // nur einmal; danach meldet sich Mik erst wieder mit gemessenem Verbrauch.
        private bool provisionalSpoken;

        public void Reset()
        {
            warned = false; provisionalSpoken = false;
            lastWarningWasProvisional = false;
            lastWarningWasDeficit = false;
            lastWarnedDeficit = 0;
            lastWarnedLap = -100;
        }

        /// <summary>
        /// Regelmäßig bei frischer Telemetrie aufrufen. Liefert den Funkspruch oder null.
        /// lapsRemaining = noch zu fahrende Runden inkl. der aktuellen (bei Zeitrennen geschätzt).
        /// </summary>
        public string Evaluate(int completedLaps, float fuelLeft, float perLap, float lapsRemaining, bool refuellingAllowed, bool estimatedFromTime,
            float additionalWithoutReserve = float.MaxValue, bool provisionalConsumption = false) =>
            Evaluate(completedLaps, fuelLeft, perLap, lapsRemaining, refuellingAllowed, estimatedFromTime, out _,
                additionalWithoutReserve, provisionalConsumption);

        internal string Evaluate(int completedLaps, float fuelLeft, float perLap, float lapsRemaining, bool refuellingAllowed, bool estimatedFromTime,
            out float savingTarget, float additionalWithoutReserve = float.MaxValue, bool provisionalConsumption = false)
        {
            savingTarget = -1;
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
                    if (provisionalConsumption) return null;
                    warned = false;
                    return RadioVariants.Pick("fuel-ok-again",
                        RadioLanguage.T("Sprit passt jetzt wieder bis ins Ziel, weiter so.", "Fuel is good to the finish again, keep it up.", "El combustible vuelve a llegar hasta meta, sigue así."),
                        RadioLanguage.T("Entwarnung beim Sprit: Es reicht bis ins Ziel.", "All clear on fuel: we make the finish.", "Tranquilo con el combustible: llegamos a meta."),
                        RadioLanguage.T("Spritrechnung geht wieder auf. Normal weiterfahren.", "Fuel numbers add up again. Drive normally.", "Las cuentas del combustible vuelven a cuadrar. Conduce normal."),
                        RadioLanguage.T("Gute Nachricht: Der Sprit reicht wieder.", "Good news: fuel is enough again.", "Buenas noticias: el combustible vuelve a llegar."));
                }
                return null;
            }
            if (provisionalConsumption && provisionalSpoken) return null;
            bool worse = deficit > lastWarnedDeficit + perLap * 0.5f;
            bool confirmed = lastWarningWasProvisional && !provisionalConsumption;
            bool newDeficit = bareDeficit > 0 && !lastWarningWasDeficit;
            if (warned && !worse && !confirmed && !newDeficit && completedLaps - lastWarnedLap < 5)
            {
                return null;
            }
            warned = true;
            lastWarningWasProvisional = provisionalConsumption;
            lastWarningWasDeficit = bareDeficit > 0;
            lastWarnedDeficit = deficit;
            lastWarnedLap = completedLaps;

            if (provisionalConsumption) provisionalSpoken = true;
            if (provisionalConsumption)
                return bareDeficit > 0
                    ? RadioVariants.Pick("fuel-provisional-short",
                        RadioLanguage.T("Erste Hochrechnung: Bis ins Ziel könnte der Sprit knapp werden. Ich prüfe das weiter.", "First estimate: fuel could get tight to the finish. I'll keep checking.", "Primera estimación: el combustible podría ir justo hasta meta. Lo sigo vigilando."),
                        RadioLanguage.T("Vorläufig gerechnet wird der Sprit eng. Genauer wird's nach ein paar Runden.", "Provisionally, fuel looks tight. It gets more precise after a few laps.", "Cálculo provisional: el combustible va justo. Será más preciso en unas vueltas."),
                        RadioLanguage.T("Grobe Schätzung: Sprit könnte nicht ganz reichen. Ich behalte das im Auge.", "Rough estimate: fuel might not quite make it. I'm watching it.", "Estimación aproximada: puede que no llegue del todo. Lo vigilo."),
                        RadioLanguage.T("Noch geschätzt, aber der Sprit sieht knapp aus. Wir messen weiter.", "Still an estimate, but fuel looks tight. We keep measuring.", "Aún es estimación, pero el combustible pinta justo. Seguimos midiendo."))
                    : RadioVariants.Pick("fuel-provisional-reserve",
                        RadioLanguage.T("Erste Hochrechnung: Der Sprit könnte reichen, aber die Reserve ist knapp.", "First estimate: fuel might be enough, but the reserve is tight.", "Primera estimación: el combustible podría llegar, pero la reserva va justa."),
                        RadioLanguage.T("Vorläufig reicht es, nur die Reserve ist dünn.", "Provisionally it's enough, just a thin reserve.", "Provisionalmente llega, pero con poca reserva."),
                        RadioLanguage.T("Grobe Schätzung: Es reicht, aber ohne viel Luft.", "Rough estimate: it's enough, but without much margin.", "Estimación aproximada: llega, pero sin mucho margen."),
                        RadioLanguage.T("Noch geschätzt: bis ins Ziel ja, Reserve knapp.", "Still estimated: to the finish yes, reserve tight.", "Aún estimado: hasta meta sí, reserva justa."));

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
                savingTarget = perLap - savePerLap;
                text += RadioLanguage.T("Wir brauchen rund " + Litres(savePerLap) + " Einsparung pro Runde. Windschatten nutzen, früher vom Gas, in die Kurven rollen lassen und etwas früher schalten.",
                    "We need to save around " + Litres(savePerLap) + " per lap. Use the slipstream, lift earlier, coast into the corners and shift a bit earlier.",
                    "Necesitamos ahorrar unos " + Litres(savePerLap) + " por vuelta. Aprovecha el rebufo, levanta antes, deja rodar en las curvas y cambia un poco antes.");
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

        internal static string Litres(float litres)
        {
            string unit = RadioLanguage.T(" Liter", " litres", " litros");
            return litres < 1 ? RadioLanguage.Num(litres, "0.00") + unit : RadioLanguage.Num(litres, litres < 10 ? "0.0" : "0") + unit;
        }
    }
}
