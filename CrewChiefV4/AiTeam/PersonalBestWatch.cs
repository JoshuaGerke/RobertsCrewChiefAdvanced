using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Kommentare aus Robs Bestzeiten-Datenbank (Wunsch 16.11.2026): Vor allem im Training und Qualifying sagt Mik einmal je
    /// Sitzung, wo Robs Allzeit-Bestzeit für dieses Auto auf dieser Strecke liegt und unter welchen Bedingungen sie gefahren wurde,
    /// vergleicht mit heute (Sprit, Streckentemperatur) und schließt mit wechselnder Motivation. Dazu: neue Allzeit-Bestzeit
    /// (jede Sitzungsart) und „knapp dran“ (einmal je Sitzung). Alles nur aus gespeicherten und aktuell gemessenen Werten.
    /// </summary>
    internal sealed class PersonalBestWatch
    {
        private int session = int.MinValue, variant;
        private bool briefed, nearDone;
        private DateTime drivingSince = DateTime.MinValue;
        private ObservedRaceCall call;

        internal void Reset() { session = int.MinValue; briefed = nearDone = false; drivingSince = DateTime.MinValue; call = null; }

        internal static bool Practiceish(SessionType t) => t == SessionType.Practice || t == SessionType.LonePractice || t == SessionType.Qualify ||
            t == SessionType.PrivateQualify || t == SessionType.HotLap;

        internal static string Time(float s) => LocalAnswers.LapTime(s);

        /// <summary>„gefahren mit 30 Litern, Soft, 27 Grad Strecke und 30 Grad Luft“ – nur was gespeichert ist.</summary>
        internal static string Conditions(PersonalBest pb)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (pb.FuelAtStart >= 0) parts.Add(RadioLanguage.T(RadioLanguage.Num(pb.FuelAtStart, "0") + " Litern", RadioLanguage.Num(pb.FuelAtStart, "0") + " litres",
                RadioLanguage.Num(pb.FuelAtStart, "0") + " litros"));
            if (!string.IsNullOrWhiteSpace(pb.Compound)) parts.Add(RadioLanguage.T("Reifen " + pb.Compound, pb.Compound + " tyres", "neumáticos " + pb.Compound));
            if (PersonalBest.Finite(pb.TrackC)) parts.Add(RadioLanguage.T(RadioLanguage.Num(pb.TrackC, "0") + " Grad Strecke", RadioLanguage.Num(pb.TrackC, "0") + " degrees track",
                RadioLanguage.Num(pb.TrackC, "0") + " grados de pista"));
            if (PersonalBest.Finite(pb.AirC)) parts.Add(RadioLanguage.T(RadioLanguage.Num(pb.AirC, "0") + " Grad Luft", RadioLanguage.Num(pb.AirC, "0") + " degrees air",
                RadioLanguage.Num(pb.AirC, "0") + " grados de aire"));
            if (parts.Count == 0) return "";
            string joined = parts.Count == 1 ? parts[0] : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + RadioLanguage.T(" und ", " and ", " y ") + parts[parts.Count - 1];
            return RadioLanguage.T(", gefahren mit ", ", done with ", ", hecha con ") + joined;
        }

        /// <summary>Vergleich mit heute: nur deutliche, gemessene Unterschiede.</summary>
        internal static string Compare(PersonalBest pb, float fuelNow, float trackNow)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (pb.FuelAtStart >= 0 && fuelNow >= 0)
            {
                float d = fuelNow - pb.FuelAtStart;
                if (d <= -5) parts.Add(RadioLanguage.T("Heute bist du leichter unterwegs, das hilft.", "You're lighter today, that helps.", "Hoy vas más ligero, eso ayuda."));
                else if (d >= 5) parts.Add(RadioLanguage.T("Heute hast du rund " + RadioLanguage.Num(d, "0") + " Liter mehr an Bord.", "Today you've about " + RadioLanguage.Num(d, "0") + " litres more on board.",
                    "Hoy llevas unos " + RadioLanguage.Num(d, "0") + " litros más."));
            }
            if (PersonalBest.Finite(pb.TrackC) && trackNow > -30 && trackNow < 90)
            {
                float d = trackNow - pb.TrackC;
                if (d >= 4) parts.Add(RadioLanguage.T("Die Strecke ist heute " + RadioLanguage.Num(d, "0") + " Grad wärmer, das kann Grip kosten.",
                    "The track is " + RadioLanguage.Num(d, "0") + " degrees warmer today, that can cost grip.", "La pista está " + RadioLanguage.Num(d, "0") + " grados más caliente, puede costar agarre."));
                else if (d <= -4) parts.Add(RadioLanguage.T("Die Strecke ist heute " + RadioLanguage.Num(-d, "0") + " Grad kühler.",
                    "The track is " + RadioLanguage.Num(-d, "0") + " degrees cooler today.", "La pista está " + RadioLanguage.Num(-d, "0") + " grados más fresca."));
            }
            return string.Join(" ", parts);
        }

        private static readonly string[] EndDe = { "Da ist noch etwas drin.", "Triff die Kurven sauber, dann kommt die Zeit.", "Das schaffst du.",
            "Erst Rhythmus finden, dann angreifen.", "Saubere Runden, die Zeit kommt von allein.", "Heute holen wir uns die Marke." };
        private static readonly string[] EndEn = { "There's still something in it.", "Hit your corners cleanly and the time will come.", "You can do it.",
            "Find your rhythm first, then attack.", "Clean laps and the time comes by itself.", "Today we go after that mark." };
        private static readonly string[] EndEs = { "Aún hay margen.", "Clava las curvas y el tiempo llegará.", "Tú puedes.",
            "Primero el ritmo, luego atacar.", "Vueltas limpias y el tiempo llega solo.", "Hoy vamos a por esa marca." };

        internal static string Briefing(PersonalBest pb, float fuelNow, float trackNow, string name, int variant)
        {
            string n = string.IsNullOrWhiteSpace(name) ? "" : name + ", ";
            string opening = ((variant % 2) + 2) % 2 == 0
                ? RadioLanguage.T(n + "deine Bestzeit hier liegt bei " + Time(pb.LapSeconds), n + "your best here is " + Time(pb.LapSeconds), n + "tu mejor tiempo aquí es " + Time(pb.LapSeconds))
                : RadioLanguage.T(n + "deine Bestmarke mit diesem Auto hier: " + Time(pb.LapSeconds), n + "your benchmark in this car here: " + Time(pb.LapSeconds), n + "tu referencia con este coche aquí: " + Time(pb.LapSeconds));
            string compare = Compare(pb, fuelNow, trackNow);
            var end = RadioLanguage.Pick(EndDe, EndEn, EndEs);
            return opening + Conditions(pb) + "." + (compare.Length > 0 ? " " + compare : "") + " " + end[((variant % end.Length) + end.Length) % end.Length];
        }

        internal static string Record(float now, PersonalBest previous, string name, int variant)
        {
            string n = string.IsNullOrWhiteSpace(name) ? "" : name + ", ";
            string gain = RadioLanguage.Num(previous.LapSeconds - now, "0.000");
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T(n + "das ist deine schnellste Runde überhaupt hier! " + Time(now) + ", " + gain + " besser als die alte Marke.",
                    n + "that's your fastest lap ever here! " + Time(now) + ", " + gain + " better than the old mark.",
                    n + "¡tu vuelta más rápida aquí! " + Time(now) + ", " + gain + " mejor que la marca anterior.");
                case 1: return RadioLanguage.T("Neuer Allzeit-Rekord für dich auf dieser Strecke: " + Time(now) + ". Stark, " + (n.Length > 0 ? n.TrimEnd(',', ' ') : "Rob") + "!",
                    "New all-time record for you on this track: " + Time(now) + ". Great job!", "Nuevo récord personal en esta pista: " + Time(now) + ". ¡Muy bien!");
                default: return RadioLanguage.T("Persönliche Bestmarke geknackt! " + Time(now) + ", alte Zeit war " + Time(previous.LapSeconds) + ".",
                    "Personal benchmark broken! " + Time(now) + ", old time was " + Time(previous.LapSeconds) + ".",
                    "¡Marca personal batida! " + Time(now) + ", la anterior era " + Time(previous.LapSeconds) + ".");
            }
        }

        internal static string Near(float now, PersonalBest pb, int variant)
        {
            string d = RadioLanguage.Num(now - pb.LapSeconds, "0.000");
            return ((variant % 2) + 2) % 2 == 0
                ? RadioLanguage.T("Nur " + d + " hinter deiner Allzeit-Bestzeit hier. Die Marke wackelt.", "Only " + d + " off your all-time best here. That mark is wobbling.",
                    "Solo " + d + " de tu mejor tiempo histórico aquí. Esa marca tiembla.")
                : RadioLanguage.T(d + " fehlen auf deine beste Runde überhaupt hier. Da geht was.", d + " short of your best lap ever here. Something's on.",
                    "Te faltan " + d + " para tu mejor vuelta aquí. Hay opciones.");
        }

        /// <summary>Pro Tick: Bestzeit-Briefing einmal je Training/Qualifying, sobald Rob 8 s auf der Strecke fährt.</summary>
        internal void Observe(GameStateData gs, PersonalBestStore store, string game, string name, DateTime now)
        {
            if (gs == null || store == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (briefed || !Practiceish(s.SessionType) || s.SessionPhase != SessionPhase.Green) return;
            bool driving = !gs.PitData.InPitlane && gs.PositionAndMotionData.CarSpeed > 20;
            if (!driving) { drivingSince = DateTime.MinValue; return; }
            if (drivingSince == DateTime.MinValue) drivingSince = now;
            if (now - drivingSince < TimeSpan.FromSeconds(8) || call != null) return;
            briefed = true;
            var ctx = RaceFactContext.FromGameState(game, gs, now);
            var pb = ctx.IdentityKnown ? store.Find(ctx.Game, ctx.Car, ctx.Track, ctx.Layout) : null;
            if (pb == null) return;
            var c = gs.Conditions.LatestConditions;
            float trackNow = c != null && c.Time != DateTime.MinValue ? c.TrackTemperature : float.NaN;
            float fuelNow = gs.FuelData.FuelUseActive ? gs.FuelData.FuelLeft : -1;
            Offer(gs, now, "personal-best-brief", Briefing(pb, fuelNow, trackNow, AiPersonas.CleanForRadio(name ?? "", 30, false), variant++), 3, "stored_personal_best_and_live_conditions");
        }

        /// <summary>Nach jeder beendeten Runde: speichern und bei Allzeit-Bestzeit oder knapp dahinter kommentieren.</summary>
        internal void ObserveLap(GameStateData gs, LapRecord lap, PersonalBestStore store, string game, string name, DateTime now)
        {
            if (gs == null || lap == null || store == null) return;
            var entry = PersonalBestStore.FromLap(game, gs, lap, now);
            if (entry == null) return;
            var known = store.Find(entry.Game, entry.Car, entry.Track, entry.Layout);
            if (store.Offer(entry, out var previous))
            {
                if (previous != null)
                    Offer(gs, now, "personal-best-record", Record(entry.LapSeconds, previous, AiPersonas.CleanForRadio(name ?? "", 30, false), variant++), 5, "lap_beats_stored_personal_best");
                LiveDiagnostics.Decision("personal-best", "stored", "valid_lap", entry.Track + " " + entry.Car + " " + entry.LapSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            if (!nearDone && known != null && entry.LapSeconds - known.LapSeconds <= .3f && entry.LapSeconds > known.LapSeconds)
            {
                nearDone = true;
                Offer(gs, now, "personal-best-near", Near(entry.LapSeconds, known, variant++), 3, "lap_within_0_3s_of_stored_personal_best");
            }
        }

        private void Offer(GameStateData gs, DateTime now, string topic, string text, int priority, string evidence)
        {
            call = new ObservedRaceCall
            {
                Topic = topic, Text = text, Evidence = evidence, Member = TeamMember.Mik, Priority = priority, Position = -1,
                SessionId = gs.SessionData.SessionId, SessionType = gs.SessionData.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(30),
                AdditionalValidity = live => !live.PitData.InPitlane
            };
            LiveDiagnostics.Decision(topic, "detected", evidence, text, call.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool mikEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !mikEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
