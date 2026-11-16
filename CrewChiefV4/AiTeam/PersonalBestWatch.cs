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
        private int session = int.MinValue, variant, lastSectorLap = -100;
        private bool briefed, nearDone;
        private DateTime drivingSince = DateTime.MinValue;
        private ObservedRaceCall call;

        internal void Reset() { session = int.MinValue; lastSectorLap = -100; briefed = nearDone = false; drivingSince = DateTime.MinValue; call = null; }

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

        /// <summary>„vor 3 Monaten“, „vor 2 Wochen“, „letzte Woche“, „vor ein paar Tagen“ – aus dem gespeicherten Datum.</summary>
        internal static string Ago(DateTime then, DateTime now)
        {
            if (then == DateTime.MinValue || then > now) return "";
            double days = (now - then).TotalDays;
            if (days < 2) return RadioLanguage.T("gerade erst", "just recently", "hace muy poco");
            if (days < 7) return RadioLanguage.T("vor ein paar Tagen", "a few days ago", "hace unos días");
            if (days < 14) return RadioLanguage.T("letzte Woche", "last week", "la semana pasada");
            if (days < 60) return RadioLanguage.T("vor " + (int)(days / 7) + " Wochen", (int)(days / 7) + " weeks ago", "hace " + (int)(days / 7) + " semanas");
            if (days < 365) return RadioLanguage.T("vor " + (int)(days / 30) + " Monaten", (int)(days / 30) + " months ago", "hace " + (int)(days / 30) + " meses");
            int years = (int)(days / 365);
            return years == 1 ? RadioLanguage.T("vor einem Jahr", "a year ago", "hace un año") : RadioLanguage.T("vor " + years + " Jahren", years + " years ago", "hace " + years + " años");
        }

        internal static string Briefing(PersonalBest pb, float fuelNow, float trackNow, string name, int variant)
        {
            string n = string.IsNullOrWhiteSpace(name) ? "" : name + ", ";
            // Rob 16.11.: „Rob, vor 3 Monaten bist du hier schon mal eine … gefahren … danach richten wir uns. Ich schaue auf deine Sektoren.“
            string ago = Ago(pb.DateUtc, DateTime.UtcNow);
            if (((variant % 3) + 3) % 3 == 2 && ago.Length > 0)
            {
                string compareToday = Compare(pb, fuelNow, trackNow);
                return RadioLanguage.T(n + ago + " bist du hier schon mal eine " + Time(pb.LapSeconds) + " gefahren" + Conditions(pb).Replace(", gefahren mit ", ", mit ") +
                        ". Danach richten wir uns. Ich schaue auf deine Sektoren und sage dir, wo du gegenüber dieser Runde verlierst." + (compareToday.Length > 0 ? " " + compareToday : "") + " Auf geht's!",
                    n + ago + " you did a " + Time(pb.LapSeconds) + " here" + Conditions(pb).Replace(", done with ", ", with ") +
                        ". That's our reference. I'll watch your sectors and tell you where you lose against that lap." + (compareToday.Length > 0 ? " " + compareToday : "") + " Let's go!",
                    n + ago + " hiciste aquí un " + Time(pb.LapSeconds) + Conditions(pb).Replace(", hecha con ", ", con ") +
                        ". Esa es la referencia. Vigilo tus sectores y te digo dónde pierdes frente a esa vuelta." + (compareToday.Length > 0 ? " " + compareToday : "") + " ¡Vamos!");
            }
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

        internal static string SectorLoss(int sector, float loss, int variant)
        {
            string d = loss < 1 ? RadioLanguage.Num(loss, "0.00") : GermanRadioText.Interval(loss);
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T("Gegenüber deiner gespeicherten Bestrunde verlierst du in Sektor " + sector + " " + d + ". Da liegt die Zeit.",
                    "Against your stored best lap you're losing " + d + " in sector " + sector + ". That's where the time is.",
                    "Frente a tu mejor vuelta guardada pierdes " + d + " en el sector " + sector + ". Ahí está el tiempo.");
                case 1: return RadioLanguage.T("Sektor " + sector + " ist der Knackpunkt: " + d + " langsamer als auf deiner Bestrunde. Triff dort die Kurven sauber.",
                    "Sector " + sector + " is the key: " + d + " slower than on your best lap. Hit the corners cleanly there.",
                    "El sector " + sector + " es la clave: " + d + " más lento que en tu mejor vuelta. Clava las curvas ahí.");
                default: return RadioLanguage.T("Die meiste Zeit auf deine Bestmarke fehlt in Sektor " + sector + ", " + d + ". Den holen wir uns.",
                    "Most of the time to your benchmark is in sector " + sector + ", " + d + ". Let's get it.",
                    "La mayor parte del tiempo respecto a tu referencia está en el sector " + sector + ", " + d + ". Vamos a por él.");
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
                return;
            }
            // Rob 16.11.: In Quali/Training/Hotlap sagen, in welchem Sektor er gegenüber der gespeicherten Bestrunde verliert –
            // höchstens jede zweite Runde, nur ab 0,1 s und wenn Sektorzahl übereinstimmt.
            if (known != null && Practiceish(gs.SessionData.SessionType) && PersonalBest.GoodSectors(known.Sectors) && PersonalBest.GoodSectors(entry.Sectors) &&
                known.Sectors.Length == entry.Sectors.Length && gs.SessionData.CompletedLaps - lastSectorLap >= 2)
            {
                int worst = -1; float loss = .1f;
                for (int i = 0; i < entry.Sectors.Length; i++) if (entry.Sectors[i] - known.Sectors[i] > loss) { loss = entry.Sectors[i] - known.Sectors[i]; worst = i; }
                if (worst >= 0)
                {
                    lastSectorLap = gs.SessionData.CompletedLaps;
                    Offer(gs, now, "personal-best-sector", SectorLoss(worst + 1, loss, variant++), 3, "sector_vs_stored_personal_best_lap");
                }
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
