using System;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Zu viel Sprit im Qualifying (Rob 16.11.2026, Mosport: „Ich hab zu viel Sprit für Quali … das habt ihr vergessen“).
    /// Nach der ersten vollen Runde mit gemessenem Verbrauch vergleicht Ben die Spritmenge mit dem, was die restliche
    /// Qualifying-Zeit noch braucht. Sind es mindestens drei Runden zu viel, sagt er es einmal – als Hinweis fürs nächste Mal,
    /// ohne einen Zeitverlust zu beziffern (der ist nicht gemessen).
    /// </summary>
    internal sealed class QualiFuelWatch
    {
        private int session = int.MinValue;
        private bool done;
        private ObservedRaceCall call;

        internal void Reset() { session = int.MinValue; done = false; call = null; }

        internal static bool IsQuali(SessionType t) => t == SessionType.Qualify || t == SessionType.PrivateQualify;

        /// <summary>Runden, die der Sprit reicht, und Runden, die die Restzeit noch erlaubt (+1 für die angefangene).</summary>
        internal static string Advice(float fuelLeft, float perLap, float timeLeft, float lapTime, string name)
        {
            if (!(fuelLeft > 0) || !(perLap > .1f) || !(timeLeft > 0) || !(lapTime > 10)) return null;
            int fuelLaps = (int)Math.Floor(fuelLeft / perLap);
            int needLaps = (int)Math.Ceiling(timeLeft / lapTime) + 1;
            if (fuelLaps < needLaps + 3) return null;
            float extra = fuelLeft - needLaps * perLap;
            string n = string.IsNullOrWhiteSpace(name) ? "" : name + ", ";
            string litres = RadioLanguage.Num(extra, "0");
            return RadioVariants.Pick("quali-fuel-ballast",
                RadioLanguage.T(n + "du hast Sprit für etwa " + fuelLaps + " Runden dabei, die Quali braucht nur noch rund " + needLaps + ". Das sind gut " +
                    RadioLanguage.Num(extra, "0") + " Liter Ballast. Fürs nächste Qualifying leichter rausfahren.",
                n + "you've got fuel for about " + fuelLaps + " laps, qualifying only needs around " + needLaps + " more. That's a good " +
                    RadioLanguage.Num(extra, "0") + " litres of ballast. Go out lighter next qualifying.",
                n + "llevas combustible para unas " + fuelLaps + " vueltas, la clasificación solo necesita unas " + needLaps + ". Son unos " +
                    RadioLanguage.Num(extra, "0") + " litros de lastre. En la próxima clasificación sal más ligero."),
                RadioLanguage.T(n + "für die restliche Quali reichen rund " + needLaps + " Runden, du hast Sprit für etwa " + fuelLaps + ". Gut " + litres + " Liter zu viel an Bord. Nächstes Mal weniger tanken.",
                    n + "the rest of qualifying needs around " + needLaps + " laps, you've got fuel for about " + fuelLaps + ". A good " + litres + " litres too much on board. Fuel less next time.",
                    n + "para lo que queda de clasificación bastan unas " + needLaps + " vueltas, llevas para unas " + fuelLaps + ". Unos " + litres + " litros de más. La próxima vez carga menos."),
                RadioLanguage.T(n + "du fährst gut " + litres + " Liter Ballast spazieren: Sprit für etwa " + fuelLaps + " Runden, nötig sind rund " + needLaps + ". Fürs nächste Qualifying leichter.",
                    n + "you're carrying a good " + litres + " litres of ballast: fuel for about " + fuelLaps + " laps, around " + needLaps + " needed. Lighter for next qualifying.",
                    n + "llevas unos " + litres + " litros de lastre: combustible para unas " + fuelLaps + " vueltas, hacen falta unas " + needLaps + ". Más ligero en la próxima clasificación."),
                RadioLanguage.T(n + "nur zur Info: rund " + needLaps + " Runden Quali bleiben, im Tank ist Sprit für etwa " + fuelLaps + ". Gut " + litres + " Liter könnten nächstes Mal draußen bleiben.",
                    n + "just so you know: around " + needLaps + " laps of qualifying left, fuel for about " + fuelLaps + " in the tank. A good " + litres + " litres could stay out next time.",
                    n + "para que lo sepas: quedan unas " + needLaps + " vueltas de clasificación y hay combustible para unas " + fuelLaps + ". Unos " + litres + " litros podrían quedarse fuera la próxima vez."));
        }

        /// <summary>
        /// LMU Bahrain 22.11.: „In der Quali keine Spritschätzung.“ Passte die Menge, schwieg Ben. Jetzt gibt es immer eine kurze
        /// Schätzung nach der ersten gemessenen Runde: reicht knapp, passt, oder reicht nicht bis zum Ende.
        /// </summary>
        internal static string Estimate(float fuelLeft, float perLap, float timeLeft, float lapTime, string name)
        {
            if (!(fuelLeft > 0) || !(perLap > .1f) || !(timeLeft > 0) || !(lapTime > 10)) return null;
            int fuelLaps = (int)Math.Floor(fuelLeft / perLap);
            int needLaps = (int)Math.Ceiling(timeLeft / lapTime) + 1;
            string n = string.IsNullOrWhiteSpace(name) ? "" : name + ", ";
            string fl = fuelLaps.ToString(), nl = needLaps.ToString();
            if (fuelLaps < needLaps)
                return RadioVariants.Pick("quali-fuel-short",
                    RadioLanguage.T(n + "Sprit reicht für etwa " + fl + " Runden, bis zum Ende der Quali wären es rund " + nl + ". Plan deine schnellen Runden früh.",
                        n + "fuel is good for about " + fl + " laps, the rest of qualifying would need around " + nl + ". Plan your fast laps early.",
                        n + "el combustible da para unas " + fl + " vueltas, hasta el final harían falta unas " + nl + ". Planifica pronto tus vueltas rápidas."),
                    RadioLanguage.T(n + "der Tank reicht nicht bis zum Schluss: etwa " + fl + " Runden, gebraucht wären rund " + nl + ".",
                        n + "the tank won't last to the end: about " + fl + " laps, around " + nl + " would be needed.",
                        n + "el depósito no llega al final: unas " + fl + " vueltas, harían falta unas " + nl + "."),
                    RadioLanguage.T(n + "Achtung Sprit: noch etwa " + fl + " Runden im Tank, die Quali läuft noch rund " + nl + ". Nutze die Runden, die du hast.",
                        n + "fuel warning: about " + fl + " laps left in the tank, qualifying runs around " + nl + " more. Use the laps you have.",
                        n + "ojo con el combustible: quedan unas " + fl + " vueltas en el depósito, la clasificación dura unas " + nl + " más. Aprovecha las que tienes."),
                    RadioLanguage.T(n + "mit dem Sprit kommst du etwa " + fl + " Runden weit, die Zeit reicht für rund " + nl + ". Die besten Versuche lieber gleich.",
                        n + "your fuel gets you about " + fl + " laps, the clock allows around " + nl + ". Better do your best attempts now.",
                        n + "con este combustible haces unas " + fl + " vueltas, el tiempo da para unas " + nl + ". Mejor tus mejores intentos ya."));
            return RadioVariants.Pick("quali-fuel-ok",
                RadioLanguage.T(n + "Sprit passt: etwa " + fl + " Runden im Tank, die Quali braucht noch rund " + nl + ".",
                    n + "fuel is fine: about " + fl + " laps in the tank, qualifying needs around " + nl + " more.",
                    n + "el combustible está bien: unas " + fl + " vueltas en el depósito, la clasificación necesita unas " + nl + "."),
                RadioLanguage.T(n + "kurz zum Sprit: reicht für etwa " + fl + " Runden, nötig sind rund " + nl + ". Alles im grünen Bereich.",
                    n + "quick fuel note: good for about " + fl + " laps, around " + nl + " needed. All in the green.",
                    n + "nota rápida de combustible: da para unas " + fl + " vueltas, hacen falta unas " + nl + ". Todo en orden."),
                RadioLanguage.T(n + "der Tank reicht bis zum Ende: etwa " + fl + " Runden, die Restzeit erlaubt rund " + nl + ".",
                    n + "the tank lasts to the end: about " + fl + " laps, the time left allows around " + nl + ".",
                    n + "el depósito llega al final: unas " + fl + " vueltas, el tiempo restante permite unas " + nl + "."),
                RadioLanguage.T(n + "Spritschätzung: rund " + nl + " Runden Quali bleiben, im Tank ist genug für etwa " + fl + ". Du kannst voll fahren.",
                    n + "fuel estimate: around " + nl + " laps of qualifying left, enough in the tank for about " + fl + ". You can push.",
                    n + "estimación de combustible: quedan unas " + nl + " vueltas de clasificación, hay para unas " + fl + ". Puedes apretar."));
        }

        internal void Observe(GameStateData gs, AiRaceMemory memory, string name, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (done || !IsQuali(s.SessionType) || s.SessionPhase != SessionPhase.Green || !s.IsNewLap || gs.PitData.InPitlane) return;
            if (!gs.FuelData.FuelUseActive || !s.SessionHasFixedTime) return;
            float perLap = FuelEstimate.Consumption(memory, gs, out bool provisional, out int fullLaps);
            if (provisional || fullLaps < 1) return;
            float lap = s.LapTimePrevious > 10 && s.LapTimePrevious < 3600 ? s.LapTimePrevious : s.PlayerLapTimeSessionBest;
            string radioName = AiPersonas.CleanForRadio(name ?? "", 30, false);
            string text = Advice(gs.FuelData.FuelLeft, perLap, s.SessionTimeRemaining, lap, radioName) ??
                Estimate(gs.FuelData.FuelLeft, perLap, s.SessionTimeRemaining, lap, radioName);
            done = true; // einmal je Qualifying – auch wenn es passt, wird nicht später erneut gerechnet
            if (text == null) return;
            call = new ObservedRaceCall
            {
                Topic = "quali-fuel", Text = text, Evidence = "measured_fuel_per_lap_vs_remaining_qualifying_time", Member = TeamMember.Ben, Priority = 3,
                Position = -1, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(40),
                AdditionalValidity = live => !live.PitData.InPitlane
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !benEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
