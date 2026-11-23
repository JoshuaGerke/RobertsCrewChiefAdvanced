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
        private DateTime drivingSince = DateTime.MinValue, sessionStartUtc = DateTime.MinValue;
        private ObservedRaceCall call;

        internal void Reset() { session = int.MinValue; lastSectorLap = -100; briefed = nearDone = false; drivingSince = sessionStartUtc = DateTime.MinValue; call = null; }

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
            return RadioLanguage.T(", gefahren mit ", ", set with ", ", marcado con ") + joined;
        }

        /// <summary>Vergleich mit heute: nur deutliche, gemessene Unterschiede.</summary>
        internal static string Compare(PersonalBest pb, float fuelNow, float trackNow)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (pb.FuelAtStart >= 0 && fuelNow >= 0)
            {
                float d = fuelNow - pb.FuelAtStart;
                if (d <= -5) parts.Add(RadioLanguage.T("Heute bist du leichter unterwegs, das hilft.", "You're lighter today, that helps.", "Hoy vas más ligero, eso ayuda."));
                else if (d >= 5) parts.Add(RadioLanguage.T("Heute hast du rund " + RadioLanguage.Num(d, "0") + " Liter mehr an Bord.", "You've got about " + RadioLanguage.Num(d, "0") + " litres more on board today.",
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
                    n + ago + " you did a " + Time(pb.LapSeconds) + " here" + Conditions(pb).Replace(", set with ", ", with ") +
                        ". That's our reference. I'll watch your sectors and tell you where you lose against that lap." + (compareToday.Length > 0 ? " " + compareToday : "") + " Let's go!",
                    n + ago + " hiciste aquí un " + Time(pb.LapSeconds) + Conditions(pb).Replace(", marcado con ", ", con ") +
                        ". Esa es la referencia. Vigilo tus sectores y te digo dónde pierdes frente a esa vuelta." + (compareToday.Length > 0 ? " " + compareToday : "") + " ¡Vamos!");
            }
            // Rob 23.11.: Der Vergleich aus der Datenbank am Anfang der Session, immer mit mehreren Formulierungen.
            string t = Time(pb.LapSeconds);
            string opening = RadioVariants.Pick("pb-brief-opening",
                RadioLanguage.T(n + "deine Bestzeit hier liegt bei " + t, n + "your best here is " + t, n + "tu mejor tiempo aquí es " + t),
                RadioLanguage.T(n + "deine Bestmarke mit diesem Auto hier: " + t, n + "your benchmark in this car here: " + t, n + "tu referencia con este coche aquí: " + t),
                RadioLanguage.T(n + "aus unserer Datenbank: Deine schnellste Runde hier ist eine " + t, n + "from our database: your fastest lap here is a " + t,
                    n + "según nuestra base de datos, tu vuelta más rápida aquí es un " + t),
                RadioLanguage.T(n + "zur Orientierung, deine Referenz auf dieser Strecke ist eine " + t, n + "for reference, your benchmark at this track is a " + t,
                    n + "como orientación, tu referencia en este circuito es un " + t),
                RadioLanguage.T(n + "die Messlatte für heute ist deine " + t + " von früher", n + "the bar for today is your " + t + " from before",
                    n + "el listón para hoy es tu " + t + " de antes"),
                RadioLanguage.T(n + "in unseren Daten steht für dich hier eine " + t, n + "our data has you at a " + t + " here", n + "en nuestros datos tienes aquí un " + t));
            if (n.Length == 0) opening = char.ToUpperInvariant(opening[0]) + opening.Substring(1);
            string compare = Compare(pb, fuelNow, trackNow);
            var end = RadioLanguage.Pick(EndDe, EndEn, EndEs);
            return opening + Conditions(pb) + "." + (compare.Length > 0 ? " " + compare : "") + " " + end[((variant % end.Length) + end.Length) % end.Length];
        }

        internal static string Record(float now, PersonalBest previous, string name, int variant)
        {
            string n = string.IsNullOrWhiteSpace(name) ? "" : name + ", ";
            string gain = RadioLanguage.Num(previous.LapSeconds - now, "0.000");
            string who = n.Length > 0 ? n.TrimEnd(',', ' ') : "Rob";
            switch (((variant % 5) + 5) % 5)
            {
                case 3: return RadioLanguage.T("Allzeit-Bestzeit! " + Time(now) + " – " + gain + " schneller als je zuvor hier. Das ist eine Ansage, " + who + ".",
                    "All-time best! " + Time(now) + " – " + gain + " quicker than ever before here. That's a statement, " + who + ".",
                    "¡Mejor tiempo de siempre! " + Time(now) + ": " + gain + " más rápido que nunca aquí. Eso es un aviso, " + who + ".");
                case 4: return RadioLanguage.T("Rekordrunde, " + who + ": " + Time(now) + ". Die alte Marke von " + Time(previous.LapSeconds) + " ist Geschichte.",
                    "Record lap, " + who + ": " + Time(now) + ". The old mark of " + Time(previous.LapSeconds) + " is history.",
                    "Vuelta récord, " + who + ": " + Time(now) + ". La marca anterior de " + Time(previous.LapSeconds) + " es historia.");
                case 0: return RadioLanguage.T(n + "das ist deine schnellste Runde überhaupt hier! " + Time(now) + ", " + gain + " besser als die alte Marke.",
                    n + "that's your fastest lap ever here! " + Time(now) + ", " + gain + " better than the old mark.",
                    n + "¡tu vuelta más rápida aquí! " + Time(now) + ", " + gain + " mejor que la marca anterior.");
                case 1: return RadioLanguage.T("Neuer Allzeit-Rekord für dich auf dieser Strecke: " + Time(now) + ". Stark, " + (n.Length > 0 ? n.TrimEnd(',', ' ') : "Rob") + "!",
                    "New all-time record for you on this track: " + Time(now) + ". Great job, " + (n.Length > 0 ? n.TrimEnd(',', ' ') : "Rob") + "!",
                    "Nuevo récord personal en esta pista: " + Time(now) + ". ¡Muy bien, " + (n.Length > 0 ? n.TrimEnd(',', ' ') : "Rob") + "!");
                default: return RadioLanguage.T("Persönliche Bestmarke geknackt! " + Time(now) + ", alte Zeit war " + Time(previous.LapSeconds) + ".",
                    "Personal benchmark broken! " + Time(now) + ", old time was " + Time(previous.LapSeconds) + ".",
                    "¡Marca personal batida! " + Time(now) + ", la anterior era " + Time(previous.LapSeconds) + ".");
            }
        }

        /// <summary>
        /// Allzeit-Rekord, wenn dieselbe Runde gerade schon als Bestzeit angesagt wurde (Rob 17.11., Bathurst: „Sauber, neue
        /// Bestzeit. 2:03,173.“ und fünf Sekunden später „Persönliche Bestmarke geknackt! 2:03,173 …“). Die Zeit kennt Rob dann
        /// schon – Mik ordnet nur ein: Allzeit-Bestmarke und Abstand zur alten Marke, ohne die Zeit zu wiederholen.
        /// </summary>
        internal static string RecordFollowUp(float now, PersonalBest previous, string name, int variant)
        {
            string gain = RadioLanguage.Num(previous.LapSeconds - now, "0.000");
            string who = string.IsNullOrWhiteSpace(name) ? "Rob" : name;
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T("Das war zugleich deine Allzeit-Bestmarke hier, " + gain + " schneller als bei allen Fahrten davor.",
                    "That's also your all-time best here, " + gain + " quicker than any run before.",
                    "Además es tu mejor marca de siempre aquí, " + gain + " más rápido que en cualquier tanda anterior.");
                case 1: return RadioLanguage.T("Und nicht nur heute: Die Runde ist dein bester Wert auf dieser Strecke überhaupt, " + gain + " unter der alten Marke.",
                    "And not just today: that lap is your best ever on this track, " + gain + " under the old mark.",
                    "Y no solo hoy: esa vuelta es lo mejor que has hecho en esta pista, " + gain + " por debajo de la marca anterior.");
                case 2: return RadioLanguage.T("Kleiner Nachtrag, " + who + ": Damit ist auch dein Streckenrekord gefallen, " + gain + " schneller als die alte Zeit.",
                    "Quick add-on, " + who + ": that also beats your track record, " + gain + " faster than the old time.",
                    "Un apunte, " + who + ": con eso también cae tu récord en esta pista, " + gain + " más rápido que el tiempo anterior.");
                default: return RadioLanguage.T("Die alte Bestmarke aus früheren Fahrten ist damit Geschichte, " + gain + " schneller.",
                    "The old benchmark from earlier runs is history, " + gain + " faster.",
                    "La marca anterior de otras tandas ya es historia, " + gain + " más rápido.");
            }
        }

        internal static string SectorLoss(int sector, float loss, int variant)
        {
            string d = loss < 1 ? RadioLanguage.Num(loss, "0.00") : GermanRadioText.Interval(loss);
            switch (((variant % 5) + 5) % 5)
            {
                case 3: return RadioLanguage.T("Vergleich mit deiner Bestrunde: In Sektor " + sector + " bleiben " + d + " liegen. Da schauen wir hin.",
                    "Compared with your best lap: " + d + " left in sector " + sector + ". That's where we look.",
                    "Comparado con tu mejor vuelta: en el sector " + sector + " se quedan " + d + ". Ahí miramos.");
                case 4: return RadioLanguage.T("Sektor " + sector + " kostet dich gerade " + d + " auf deine Bestmarke. Ruhig rein, sauber raus.",
                    "Sector " + sector + " is costing you " + d + " to your benchmark. Calm in, clean out.",
                    "El sector " + sector + " te cuesta " + d + " frente a tu referencia. Tranquilo al entrar, limpio al salir.");
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
            switch (((variant % 5) + 5) % 5)
            {
                case 0: return RadioLanguage.T("Nur " + d + " hinter deiner Allzeit-Bestzeit hier. Die Marke wackelt.", "Only " + d + " off your all-time best here. That mark is wobbling.",
                    "Solo " + d + " de tu mejor tiempo histórico aquí. Esa marca tiembla.");
                case 1: return RadioLanguage.T(d + " fehlen auf deine beste Runde überhaupt hier. Da geht was.", d + " short of your best lap ever here. There's more in it.",
                    "Te faltan " + d + " para tu mejor vuelta aquí. Hay opciones.");
                case 2: return RadioLanguage.T("Ganz knapp: " + d + " an deinem Rekord vorbei. Die nächste kann es sein.", "So close: " + d + " off your record. The next one could be it.",
                    "Muy cerca: a " + d + " de tu récord. La próxima puede ser.");
                case 3: return RadioLanguage.T("Deine Allzeit-Marke ist in Reichweite, nur noch " + d + ".", "Your all-time mark is within reach, just " + d + " away.",
                    "Tu mejor marca está a tiro, solo " + d + ".");
                default: return RadioLanguage.T("Das war " + d + " von deinem Streckenrekord entfernt. Dranbleiben!", "That was " + d + " from your track record. Keep at it!",
                    "Eso fue a " + d + " de tu récord en esta pista. ¡Sigue así!");
            }
        }

        /// <summary>Pro Tick: Bestzeit-Briefing einmal je Training/Qualifying, sobald Rob 8 s auf der Strecke fährt.</summary>
        internal void Observe(GameStateData gs, PersonalBestStore store, string game, string name, DateTime now)
        {
            if (gs == null || store == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (sessionStartUtc == DateTime.MinValue) sessionStartUtc = now;
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
        /// <summary>Verschleißrate dieser Fahrt in die Datenbank (nach jeder sauberen Runde, sobald zwei Runden Messung vorliegen).</summary>
        internal static void RecordWear(GameStateData gs, PersonalBestStore store, string game, float[] perLap, DateTime now)
        {
            if (gs == null || store == null || perLap == null) return;
            var ctx = RaceFactContext.FromGameState(game, gs, now);
            if (ctx.IdentityKnown) store.RecordWear(ctx.Game, ctx.Car, ctx.Track, ctx.Layout, PersonalBestStore.Compound(gs), perLap);
        }

        internal void ObserveLap(GameStateData gs, LapRecord lap, PersonalBestStore store, string game, string name, DateTime now,
            Func<float, bool> lapTimeAlreadyAnnounced = null)
        {
            if (gs == null || lap == null || store == null) return;
            if (gs.SessionData.SessionId != session) { Reset(); session = gs.SessionData.SessionId; }
            if (sessionStartUtc == DateTime.MinValue) sessionStartUtc = now;
            var entry = PersonalBestStore.FromLap(game, gs, lap, now);
            if (entry == null) return;
            var known = store.Find(entry.Game, entry.Car, entry.Track, entry.Layout);
            if (store.Offer(entry, out var previous))
            {
                // Rob 16.11., Bathurst: „schnellste Runde überhaupt … 2,9 besser als die alte Marke“ kam, obwohl die alte Marke
                // erst eine Runde vorher in derselben Sitzung gespeichert war. Ein Rekord zählt nur gegen frühere Sitzungen.
                if (previous != null && previous.DateUtc < sessionStartUtc)
                {
                    string cleanName = AiPersonas.CleanForRadio(name ?? "", 30, false);
                    bool announced = lapTimeAlreadyAnnounced != null && lapTimeAlreadyAnnounced(entry.LapSeconds);
                    Offer(gs, now, "personal-best-record", announced ? RecordFollowUp(entry.LapSeconds, previous, cleanName, variant++) :
                        Record(entry.LapSeconds, previous, cleanName, variant++), 5,
                        announced ? "lap_beats_stored_personal_best_time_already_announced" : "lap_beats_stored_personal_best");
                }
                LiveDiagnostics.Decision("personal-best", "stored", "valid_lap", entry.Track + " " + entry.Car + " " + entry.LapSeconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            if (known != null && known.DateUtc >= sessionStartUtc) known = null; // nur gegen frühere Sitzungen vergleichen
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
