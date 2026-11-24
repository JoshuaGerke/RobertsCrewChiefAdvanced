using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Höchstgeschwindigkeit im Rennen (Rob 23.11.2026: „Topgeschwindigkeiten … vereinzelt was an der Spitze los ist“). Aus dem
    /// Lagebild der Sim: der direkte Gegner vorn oder hinten ist auf der Geraden deutlich schneller oder langsamer (Ben sagt, was
    /// das für Angriff und Verteidigung heißt), und ab und zu, wer in der Klasse den höchsten Topspeed hat. Einmal je Runde geprüft,
    /// mit großem Abstand zwischen den Meldungen.
    /// </summary>
    internal sealed class TopSpeedWatch
    {
        /// <summary>Erst ab dieser Runde: am Start verfälschen Windschatten im Pulk die Spitzenwerte.</summary>
        internal const int StartLap = 3;
        /// <summary>Unterschied zum direkten Gegner, ab dem es für Angriff oder Verteidigung zählt.</summary>
        internal const float RivalDeltaKmh = 5f;
        /// <summary>Größer ist kein echter Unterschied, sondern ein Messfehler (Box, Abflug, Neustart).</summary>
        internal const float ImplausibleDeltaKmh = 40f;
        /// <summary>Nur wenn der Gegner in Reichweite ist; weiter weg ist der Topspeed kein Thema.</summary>
        internal const float MaximumGapSeconds = 2.5f;
        /// <summary>RaceRoom Hockenheim 23.11.: in neun Runden kam keine einzige Topspeed-Meldung durch (Rob nach dem Ziel). Öfter
        /// anbieten; die Meldung wartet bis zu 45 s auf eine Gerade.</summary>
        internal const int RivalEveryLaps = 4, FieldEveryLaps = 6;
        /// <summary>Unterschied zur Klassenspitze, ab dem sie erwähnt wird.</summary>
        internal const float FieldDeltaKmh = 3f;

        /// <summary>RaceRoom Salzburg 24.11. (Rob: „das Höchsttempo wenigstens zweimal im Rennen vergleichen, im ersten Drittel und im
        /// letzten Viertel“): Der Klassenvergleich war zweimal fertig, wartete aber hinter anderem Funk und verfiel; in den letzten
        /// Runden war er gesperrt. Jetzt gibt es zwei feste Termine mit den drei Schnellsten und Robs Wert, die bis zum Ende ihres
        /// Abschnitts gültig bleiben.</summary>
        internal const int FieldStartLap = 2;
        /// <summary>Ende des ersten Drittels (mit etwas Spielraum, falls die Runde knapp darüber endet).</summary>
        internal const double FirstWindowEnd = 0.40;
        /// <summary>Beginn des letzten Viertels.</summary>
        internal const double LastWindowStart = 0.75;

        private int session = -1, lastLap = -1, rivalAheadLap = -99, rivalBehindLap = -99;
        private readonly bool[] fieldSlotDone = new bool[2];
        private ObservedRaceCall call, fieldCall;

        internal void Reset()
        {
            session = -1; lastLap = -1; rivalAheadLap = -99; rivalBehindLap = -99; call = null; fieldCall = null;
            fieldSlotDone[0] = fieldSlotDone[1] = false;
        }

        /// <summary>Anteil des Rennens (0..1) nach Uhr oder Runden; -1 = unbekannt.</summary>
        internal static double Progress(SessionData s)
        {
            if (s == null) return -1;
            if (s.SessionHasFixedTime && s.SessionTotalRunTime > 0 && s.SessionTimeRemaining >= 0)
                return Math.Max(0, Math.Min(1, 1 - s.SessionTimeRemaining / s.SessionTotalRunTime));
            if (s.SessionNumberOfLaps > 0) return Math.Max(0, Math.Min(1, s.CompletedLaps / (double)s.SessionNumberOfLaps));
            return -1;
        }

        /// <summary>Welcher feste Topspeed-Termin gerade offen ist: 0 = erstes Drittel, 1 = letztes Viertel, -1 = keiner.</summary>
        internal static int FieldSlot(SessionData s)
        {
            if (s == null || s.CompletedLaps < FieldStartLap) return -1;
            double p = Progress(s);
            if (p < 0) return s.CompletedLaps >= StartLap ? 0 : -1;
            if (p <= FirstWindowEnd) return 0;
            if (p >= LastWindowStart) return 1;
            return -1;
        }

        /// <summary>Pro Tick: einmal je beendeter Runde mit dem aktuellen Lagebild prüfen.</summary>
        internal void Observe(GameStateData gs, DateTime now, bool names)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; lastLap = s.CompletedLaps; return; }
            if (s.CompletedLaps == lastLap) return;
            lastLap = s.CompletedLaps;
            FieldView view;
            try { view = FieldService.Fresh(2); } catch (Exception) { view = null; }
            Evaluate(gs, view, names, now);
        }

        internal void Evaluate(GameStateData gs, FieldView view, bool names, DateTime now)
        {
            var s = gs.SessionData;
            EvaluateField(gs, view, names, now);
            if (call != null && (call.DeliveryStartedUtc != DateTime.MinValue || now > call.ExpiresUtc || call.DeliveryFailed)) call = null;
            if (call != null || s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane ||
                s.CompletedLaps < StartLap || view == null || view.Player == null || view.Cars == null) return;
            float mine = view.Player.TopSpeed * 3.6f;
            if (float.IsNaN(mine) || mine < 60) return;
            int lap = s.CompletedLaps;

            ObservedRaceCall best = null; float bestDelta = 0;
            foreach (bool ahead in new[] { true, false })
            {
                if (lap - (ahead ? rivalAheadLap : rivalBehindLap) < RivalEveryLaps) continue;
                var op = RaceDataTools.Neighbour(gs, ahead);
                if (op == null || op.InPits || !op.CanUseName || !names) continue;
                string name = RadioPhraseCatalog.SafeRivalName(op.DriverRawName);
                if (string.IsNullOrWhiteSpace(name)) continue;
                var car = Match(view, op.DriverRawName);
                if (car == null || car.InPit || float.IsNaN(car.TopSpeed)) continue;
                if (!float.IsNaN(car.GapS) && Math.Abs(car.GapS) > MaximumGapSeconds) continue;
                float delta = car.TopSpeed * 3.6f - mine;
                if (Math.Abs(delta) < RivalDeltaKmh || Math.Abs(delta) > ImplausibleDeltaKmh || Math.Abs(delta) <= Math.Abs(bestDelta)) continue;
                string key = OpponentStopLog.Key(op);
                bool side = ahead;
                bestDelta = delta;
                best = Build(gs, now, "rival-topspeed", RivalText(ahead, delta > 0, name, Kmh(Math.Abs(delta))), 3,
                    live => RaceDataTools.Neighbour(live, side) is OpponentData n && OpponentStopLog.Key(n) == key,
                    "field_top_speed_rival;ahead=" + ahead + ";delta_kmh=" + Kmh(delta));
                best.Evidence += ";side=" + (ahead ? "ahead" : "behind");
            }
            if (best != null)
            {
                if (best.Evidence.EndsWith("side=ahead", StringComparison.Ordinal)) rivalAheadLap = lap; else rivalBehindLap = lap;
                call = best;
                LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, call.Text, call.EventId);
                return;
            }
        }

        /// <summary>Feste Termine: Robs Höchsttempo gegen die drei Schnellsten seiner Klasse.</summary>
        private void EvaluateField(GameStateData gs, FieldView view, bool names, DateTime now)
        {
            var s = gs.SessionData;
            if (fieldCall != null && (fieldCall.DeliveryStartedUtc != DateTime.MinValue || fieldCall.DeliveryFailed)) fieldCall = null;
            if (fieldCall != null || s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Green ||
                view == null || view.Player == null || view.Cars == null) return;
            int slot = FieldSlot(s);
            if (slot < 0 || fieldSlotDone[slot]) return;
            float mine = view.Player.TopSpeed * 3.6f;
            if (float.IsNaN(mine) || mine < 60) return;
            var ranked = view.Cars.Where(c => c != null && c.ClassId == view.Player.ClassId && !float.IsNaN(c.TopSpeed) &&
                    c.TopSpeed * 3.6f >= 60 && c.TopSpeed * 3.6f - mine <= ImplausibleDeltaKmh)
                .OrderByDescending(c => c.TopSpeed).ToList();
            if (ranked.Count < 2) return;
            int rank = ranked.FindIndex(c => c.IsPlayer) + 1;
            if (rank < 1) return;
            var top = ranked.Take(3).Select(c => new TopSpeedEntry
            {
                Name = c.IsPlayer ? null : SpeakableName(gs, c.Name, names),
                IsPlayer = c.IsPlayer,
                Kmh = Kmh(c.TopSpeed * 3.6f)
            }).ToList();
            fieldSlotDone[slot] = true;
            float best = ranked[0].TopSpeed * 3.6f;
            string text = TopThreeText(top, Kmh(mine), rank, ranked.Count, Kmh(Math.Max(0, best - mine)));
            fieldCall = Build(gs, now, "field-topspeed", text, 4, null,
                "field_top_speed_top3;slot=" + (slot == 0 ? "first_third" : "last_quarter") + ";top_kmh=" + Kmh(best) + ";own_kmh=" + Kmh(mine) + ";rank=" + rank);
            // Bis zum Ende des Abschnitts gültig: das erste Drittel bis zur Rennmitte, das letzte Viertel bis zur Zielflagge.
            fieldCall.ExpiresUtc = now.AddSeconds(slot == 0 ? 240 : 600);
            LiveDiagnostics.Decision(fieldCall.Topic, "detected", fieldCall.Evidence, fieldCall.Text, fieldCall.EventId);
        }

        internal sealed class TopSpeedEntry { public string Name; public bool IsPlayer; public string Kmh; }

        private static string SpeakableName(GameStateData gs, string raw, bool names)
        {
            if (!names || string.IsNullOrWhiteSpace(raw)) return null;
            var op = gs.OpponentData.Values.FirstOrDefault(o => o != null && SameName(o.DriverRawName, raw));
            if (op == null || !op.CanUseName) return null;
            string name = RadioPhraseCatalog.SafeRivalName(op.DriverRawName);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        /// <summary>Liste „A 249, B 247 und C 246 km/h“; Robs Eintrag heißt „du“, ohne Namen nur die Werte.</summary>
        internal static string TopList(IList<TopSpeedEntry> top)
        {
            var parts = top.Select(e => e.IsPlayer ? RadioLanguage.T("du ", "you ", "tú ") + e.Kmh :
                string.IsNullOrWhiteSpace(e.Name) ? e.Kmh : e.Name + " " + e.Kmh).ToList();
            string and = RadioLanguage.T(" und ", " and ", " y ");
            string list = parts.Count == 1 ? parts[0] : string.Join(", ", parts.Take(parts.Count - 1)) + and + parts[parts.Count - 1];
            return list + " km/h";
        }

        /// <summary>Top 3 im Topspeed und Robs Wert, sechs Formulierungen je Sprache (DE/EN/ES).</summary>
        internal static string TopThreeText(IList<TopSpeedEntry> top, string mine, int rank, int total, string behind)
        {
            string list = TopList(top);
            string r = rank.ToString(CultureInfo.InvariantCulture), n = total.ToString(CultureInfo.InvariantCulture);
            if (rank == 1)
                return RadioVariants.Pick("topspeed-top3-own-best",
                    RadioLanguage.T("Topspeed-Vergleich: " + list + ". Du bist auf der Geraden der Schnellste.",
                        "Top speed check: " + list + ". You're the quickest on the straight.",
                        "Velocidad punta: " + list + ". Eres el más rápido en recta."),
                    RadioLanguage.T("Höchsttempo in der Klasse: " + list + ". Keiner ist schneller als du.",
                        "Class top speeds: " + list + ". Nobody is faster than you.",
                        "Punta de la clase: " + list + ". Nadie es más rápido que tú."),
                    RadioLanguage.T("Auf der Geraden führst du die Liste an: " + list + ".",
                        "You top the speed trap: " + list + ".",
                        "Encabezas la lista en recta: " + list + "."),
                    RadioLanguage.T("Kurzer Blick auf die Endgeschwindigkeit: " + list + ". Platz 1, das Auto geht gut.",
                        "Quick look at the top end: " + list + ". P1, the car is working.",
                        "Un vistazo a la punta: " + list + ". Primero, el coche va bien."),
                    RadioLanguage.T("Die Top 3 im Topspeed: " + list + ". Du hast den Bestwert.",
                        "Top three on speed: " + list + ". The best figure is yours.",
                        "Los tres más rápidos en punta: " + list + ". El mejor registro es tuyo."),
                    RadioLanguage.T("Speedtrap: " + list + ". Ganz vorn bist du.",
                        "Speed trap: " + list + ". You're at the top.",
                        "Trampa de velocidad: " + list + ". Estás arriba del todo."));
            if (rank <= 3)
                return RadioVariants.Pick("topspeed-top3-own-in",
                    RadioLanguage.T("Topspeed-Vergleich: " + list + ". Du bist Platz " + r + " von " + n + ".",
                        "Top speed check: " + list + ". You're P" + r + " of " + n + ".",
                        "Velocidad punta: " + list + ". Eres " + r + "º de " + n + "."),
                    RadioLanguage.T("Höchsttempo in der Klasse: " + list + ". Damit bist du in den Top 3.",
                        "Class top speeds: " + list + ". That puts you in the top three.",
                        "Punta de la clase: " + list + ". Con eso estás entre los tres primeros."),
                    RadioLanguage.T("Auf der Geraden die Schnellsten: " + list + ". Dir fehlen " + behind + " km/h auf die Spitze.",
                        "Quickest on the straight: " + list + ". You're " + behind + " km/h off the top.",
                        "Los más rápidos en recta: " + list + ". Te faltan " + behind + " km/h para el primero."),
                    RadioLanguage.T("Kurzer Blick auf die Endgeschwindigkeit: " + list + ". Platz " + r + ", das passt.",
                        "Quick look at the top end: " + list + ". P" + r + ", that's fine.",
                        "Un vistazo a la punta: " + list + ". " + r + "º, está bien."),
                    RadioLanguage.T("Die Top 3 im Topspeed: " + list + ". Du bist dabei.",
                        "Top three on speed: " + list + ". You're in there.",
                        "Los tres más rápidos en punta: " + list + ". Estás entre ellos."),
                    RadioLanguage.T("Speedtrap: " + list + ". Rang " + r + " für dich.",
                        "Speed trap: " + list + ". P" + r + " for you.",
                        "Trampa de velocidad: " + list + ". Puesto " + r + " para ti."));
            return RadioVariants.Pick("topspeed-top3-own-out",
                RadioLanguage.T("Topspeed-Vergleich: vorn " + list + ". Du hast " + mine + ", Platz " + r + " von " + n + ".",
                    "Top speed check: up front " + list + ". You've got " + mine + ", P" + r + " of " + n + ".",
                    "Velocidad punta: delante " + list + ". Tú tienes " + mine + ", " + r + "º de " + n + "."),
                RadioLanguage.T("Auf der Geraden sind am schnellsten " + list + ". Bei dir stehen " + mine + ", " + behind + " weniger als die Spitze.",
                    "Quickest on the straight: " + list + ". You're showing " + mine + ", " + behind + " down on the top.",
                    "Los más rápidos en recta: " + list + ". Tú marcas " + mine + ", " + behind + " menos que el primero."),
                RadioLanguage.T("Höchsttempo in der Klasse: " + list + ". Du liegst mit " + mine + " km/h auf Platz " + r + ".",
                    "Class top speeds: " + list + ". You're on " + mine + " km/h, P" + r + ".",
                    "Punta de la clase: " + list + ". Tú vas a " + mine + " km/h, " + r + "º."),
                RadioLanguage.T("Kurzer Blick auf die Endgeschwindigkeit: " + list + ". Dein Wert: " + mine + " km/h, Rang " + r + " von " + n + ".",
                    "Quick look at the top end: " + list + ". Yours: " + mine + " km/h, P" + r + " of " + n + ".",
                    "Un vistazo a la punta: " + list + ". La tuya: " + mine + " km/h, " + r + "º de " + n + "."),
                RadioLanguage.T("Die Top 3 im Topspeed: " + list + ". Du bist bei " + mine + ", " + behind + " km/h hinter der Spitze.",
                    "Top three on speed: " + list + ". You're at " + mine + ", " + behind + " km/h off the top.",
                    "Los tres más rápidos en punta: " + list + ". Tú estás en " + mine + ", a " + behind + " km/h del primero."),
                RadioLanguage.T("Speedtrap: " + list + ". Für dich " + mine + " km/h, Platz " + r + ". Die Zeit holst du in den Kurven.",
                    "Speed trap: " + list + ". You've got " + mine + " km/h, P" + r + ". You find the time in the corners.",
                    "Trampa de velocidad: " + list + ". Tú " + mine + " km/h, " + r + "º. El tiempo lo ganas en las curvas."));
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool enabled)
        {
            if (busy || !enabled) return null;
            if (fieldCall != null)
            {
                if (fieldCall.IsValid(gs, now)) { if (fieldCall.TryReserve(now)) return fieldCall; }
                else if (now > fieldCall.ExpiresUtc) { LiveDiagnostics.Decision(fieldCall.Topic, "suppressed", "expired", fieldCall.Evidence, fieldCall.EventId); fieldCall = null; }
            }
            if (call == null) return null;
            if (!call.IsValid(gs, now))
            {
                if (now > call.ExpiresUtc) { LiveDiagnostics.Decision(call.Topic, "suppressed", "expired", call.Evidence, call.EventId); call = null; }
                return null;
            }
            return call.TryReserve(now) ? call : null;
        }

        private static FieldCarView Match(FieldView view, string rawName) =>
            view.Cars.FirstOrDefault(c => c != null && !c.IsPlayer && SameName(c.Name, rawName));

        private static bool SameName(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
            string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string Kmh(float v) => ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);

        private static ObservedRaceCall Build(GameStateData gs, DateTime now, string topic, string text, int priority,
            Func<GameStateData, bool> still, string evidence)
        {
            var s = gs.SessionData;
            int position = s.ClassPosition;
            return new ObservedRaceCall
            {
                Topic = topic, Text = text, Evidence = evidence, Member = TeamMember.Ben, Priority = priority,
                Position = position, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now,
                ExpiresUtc = now.AddSeconds(45), AdditionalValidity = still
            };
        }

        private static string Cap(string who) => string.IsNullOrEmpty(who) ? who : char.ToUpperInvariant(who[0]) + who.Substring(1);

        /// <summary>Direkter Gegner auf der Geraden schneller oder langsamer, je fünf Formulierungen (DE/EN/ES).</summary>
        internal static string RivalText(bool ahead, bool rivalFaster, string who, string d)
        {
            string Who = Cap(who);
            if (ahead && rivalFaster)
                return RadioVariants.Pick("topspeed-front-faster",
                    RadioLanguage.T(Who + " ist auf der Geraden " + d + " km/h schneller als du. Hol ihn dir auf der Bremse, nicht im Windschatten.",
                        Who + " is " + d + " km/h quicker than you on the straight. Get him on the brakes, not in the slipstream.",
                        Who + " es " + d + " km/h más rápido que tú en recta. Gánale en la frenada, no en el rebufo."),
                    RadioLanguage.T(d + " km/h mehr Topspeed bei " + who + ". Auf der Geraden kommst du nicht vorbei, mach es in den Kurven.",
                        d + " km/h more top speed for " + who + ". You won't pass on the straight, do it in the corners.",
                        d + " km/h más de punta para " + who + ". En recta no pasas, hazlo en las curvas."),
                    RadioLanguage.T("Zur Info: " + who + " hat " + d + " km/h mehr Endgeschwindigkeit. Dein Platz für den Angriff ist die Kurve.",
                        "For info: " + who + " has " + d + " km/h more top end. Your place to attack is the corner.",
                        "Para que lo sepas: " + who + " tiene " + d + " km/h más de punta. Tu sitio para atacar es la curva."),
                    RadioLanguage.T(Who + " zieht dir auf der Geraden mit " + d + " km/h davon. Bleib am Kurvenausgang dicht dran.",
                        Who + " pulls " + d + " km/h on you down the straight. Stay tight on the exits.",
                        Who + " te saca " + d + " km/h en recta. Pégate a la salida de las curvas."),
                    RadioLanguage.T("Topspeed-Vergleich: " + who + " ist " + d + " km/h schneller. Die Zeit holst du dir in den Kurven.",
                        "Top speed check: " + who + " is " + d + " km/h faster. You find the time in the corners.",
                        "Velocidad punta: " + who + " va " + d + " km/h más rápido. El tiempo lo ganas en las curvas."));
            if (ahead)
                return RadioVariants.Pick("topspeed-front-slower",
                    RadioLanguage.T(Who + " fehlen auf der Geraden " + d + " km/h auf dich. Da ist deine Chance.",
                        Who + " is " + d + " km/h down on you on the straight. That's your chance.",
                        A(who) + " le faltan " + d + " km/h en recta respecto a ti. Ahí está tu oportunidad."),
                    RadioLanguage.T("Du bist in der Spitze " + d + " km/h schneller als " + who + ". Guter Ausgang, dann Windschatten.",
                        "You're " + d + " km/h faster than " + who + " at the top end. Good exit, then the slipstream.",
                        "Eres " + d + " km/h más rápido que " + who + " en punta. Buena salida y luego rebufo."),
                    RadioLanguage.T("Auf der Geraden hast du " + d + " km/h mehr als " + who + ". Bau den Angriff dort auf.",
                        "You've got " + d + " km/h more than " + who + " on the straight. Build the move there.",
                        "En recta tienes " + d + " km/h más que " + who + ". Prepara el ataque ahí."),
                    RadioLanguage.T(Who + " ist auf der Geraden " + d + " km/h langsamer. Sauber aus der Kurve, dann geht was.",
                        Who + " is " + d + " km/h slower on the straight. Clean exit and something's on.",
                        Who + " es " + d + " km/h más lento en recta. Sal limpio de la curva y hay opción."),
                    RadioLanguage.T(d + " km/h Topspeed-Vorteil für dich gegenüber " + who + ". Nutz den Windschatten.",
                        d + " km/h top speed advantage for you over " + who + ". Use the slipstream.",
                        d + " km/h de ventaja en punta sobre " + who + ". Aprovecha el rebufo."));
            if (rivalFaster)
                return RadioVariants.Pick("topspeed-rear-faster",
                    RadioLanguage.T(Who + " hinter dir hat " + d + " km/h mehr Topspeed. Kurvenausgänge sauber, sonst kommt er im Windschatten.",
                        Who + " behind you has " + d + " km/h more top speed. Clean exits, or he'll come in the tow.",
                        Who + ", detrás, tiene " + d + " km/h más de punta. Salidas limpias o te pilla en el rebufo."),
                    RadioLanguage.T("Achtung, " + who + " ist auf der Geraden " + d + " km/h schneller. Verteidige vor den Bremszonen.",
                        "Heads up, " + who + " is " + d + " km/h faster on the straight. Defend into the braking zones.",
                        "Atención, " + who + " es " + d + " km/h más rápido en recta. Defiende antes de las frenadas."),
                    RadioLanguage.T(d + " km/h mehr Endgeschwindigkeit bei " + who + ". Mach die Gerade mit einem guten Ausgang zu.",
                        d + " km/h more top end for " + who + ". Close the straight with a good exit.",
                        d + " km/h más de punta para " + who + ". Cierra la recta con una buena salida."),
                    RadioLanguage.T(Who + " hat auf der Geraden " + d + " km/h mehr. Du musst ihn in den Kurven hinter dir halten.",
                        Who + " has " + d + " km/h more on the straight. You have to keep him behind in the corners.",
                        Who + " tiene " + d + " km/h más en recta. Tienes que contenerlo en las curvas."),
                    RadioLanguage.T("Dein Hintermann " + who + " hat " + d + " km/h Vorteil auf der Geraden. Sauber rausbeschleunigen.",
                        "The car behind, " + who + ", has " + d + " km/h on you on the straight. Get out of the corners cleanly.",
                        "El de atrás, " + who + ", te saca " + d + " km/h en recta. Acelera limpio a la salida."));
            return RadioVariants.Pick("topspeed-rear-slower",
                RadioLanguage.T(Who + " fehlen auf der Geraden " + d + " km/h auf dich. Da bist du sicher, pass in den Kurven auf.",
                    Who + " is " + d + " km/h down on you on the straight. You're safe there, watch the corners.",
                    A(who) + " le faltan " + d + " km/h en recta. Ahí estás seguro, ojo en las curvas."),
                RadioLanguage.T("Auf der Geraden bist du " + d + " km/h schneller als " + who + ". Er muss es in den Kurven versuchen.",
                    "You're " + d + " km/h faster than " + who + " on the straight. He has to try it in the corners.",
                    "En recta eres " + d + " km/h más rápido que " + who + ". Tendrá que intentarlo en las curvas."),
                RadioLanguage.T(Who + " hat " + d + " km/h weniger Topspeed. Mach die Kurven zu, dann kommt er nicht vorbei.",
                    Who + " has " + d + " km/h less top speed. Shut the corners and he won't get by.",
                    Who + " tiene " + d + " km/h menos de punta. Cierra las curvas y no pasará."),
                RadioLanguage.T(d + " km/h Topspeed-Vorteil gegenüber " + who + ". Die Bremszonen sind seine einzige Chance.",
                    d + " km/h top speed advantage over " + who + ". The braking zones are his only chance.",
                    d + " km/h de ventaja en punta sobre " + who + ". Las frenadas son su única opción."),
                RadioLanguage.T(Who + " ist auf der Geraden " + d + " km/h langsamer. Keine Fehler in den Kurven, dann bleibt es so.",
                    Who + " is " + d + " km/h slower on the straight. No mistakes in the corners and it stays that way.",
                    Who + " es " + d + " km/h más lento en recta. Sin errores en las curvas y seguirá así."));
        }

        private static string A(string who) => "A " + who;

        /// <summary>Höchster Topspeed der Klasse, je fünf Formulierungen (DE/EN/ES).</summary>
        internal static string FieldText(bool mineIsBest, string who, string top, string mine, string d)
        {
            if (mineIsBest)
                return RadioVariants.Pick("topspeed-field-own",
                    RadioLanguage.T("Du hast den höchsten Topspeed der Klasse, " + top + " km/h.", "You've got the highest top speed in the class, " + top + " km/h.", "Tienes la velocidad punta más alta de la clase, " + top + " km/h."),
                    RadioLanguage.T("Keiner ist auf der Geraden schneller als du: " + top + " km/h.", "Nobody is faster than you on the straight: " + top + " km/h.", "Nadie es más rápido que tú en recta: " + top + " km/h."),
                    RadioLanguage.T("Topspeed-Liste: Du stehst ganz oben mit " + top + " km/h.", "Top speed list: you're at the top with " + top + " km/h.", "Lista de velocidad punta: estás arriba del todo con " + top + " km/h."),
                    RadioLanguage.T(top + " km/h, das ist der Bestwert auf der Geraden in deiner Klasse.", top + " km/h, that's the best on the straight in your class.", top + " km/h, es lo mejor en recta de tu clase."),
                    RadioLanguage.T("Auf der Geraden bist du der Schnellste, " + top + " km/h. Das Auto geht gut.", "You're the quickest on the straight, " + top + " km/h. The car is working.", "Eres el más rápido en recta, " + top + " km/h. El coche va bien."));
            string Who = Cap(who);
            return RadioVariants.Pick("topspeed-field-other",
                RadioLanguage.T("Höchster Topspeed in der Klasse: " + who + " mit " + top + " km/h. Du liegst bei " + mine + ".", "Highest top speed in the class: " + who + " with " + top + " km/h. You're at " + mine + ".", "Velocidad punta más alta de la clase: " + who + " con " + top + " km/h. Tú estás en " + mine + "."),
                RadioLanguage.T(Who + " ist der Schnellste auf der Geraden, " + top + " km/h. Dir fehlen " + d + ".", Who + " is the quickest on the straight, " + top + " km/h. You're " + d + " down.", Who + " es el más rápido en recta, " + top + " km/h. Te faltan " + d + "."),
                RadioLanguage.T("Topspeed-Liste: vorn " + who + " mit " + top + " km/h, du mit " + mine + ".", "Top speed list: " + who + " leads with " + top + " km/h, you've got " + mine + ".", "Lista de punta: delante " + who + " con " + top + " km/h, tú con " + mine + "."),
                RadioLanguage.T("Auf der Geraden ist " + who + " mit " + top + " km/h vorn. Bei dir stehen " + mine + " km/h.", "On the straight " + who + " is top with " + top + " km/h. You're showing " + mine + " km/h.", "En recta manda " + who + " con " + top + " km/h. Tú marcas " + mine + " km/h."),
                RadioLanguage.T("Spitzentempo in der Klasse hat " + who + ": " + top + " km/h, " + d + " mehr als du.", "Top speed in the class belongs to " + who + ": " + top + " km/h, " + d + " more than you.", "La punta de la clase es de " + who + ": " + top + " km/h, " + d + " más que tú."));
        }
    }
}
