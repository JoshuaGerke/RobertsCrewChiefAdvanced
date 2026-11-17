using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Funk aus der Fahrdynamik (Rob 17.11.2026): Was die schnelle Abtastung sieht, sagt die Crew so, wie ein
    /// Renningenieur es aus der Datenaufzeichnung sagen würde – nur wiederkehrende Muster (zwei von drei Runden, gleiche
    /// Stelle), nie jeder einzelne Ausrutscher. Ausnahme: ein starker Blockierer, der einen Bremsplatten verursachen kann,
    /// kommt gleich. Mik spricht über Bremsen und Traktion (Auto), Ben über Ausritte (Fahrlinie).
    /// </summary>
    internal sealed class DrivingDynamicsWatch
    {
        private readonly DrivingDynamicsAnalyzer analyzer;
        private readonly Random random;
        private ObservedRaceCall call;
        private int session = int.MinValue, variant, lastHeavyLap = -100;

        internal DrivingDynamicsWatch(DrivingDynamicsAnalyzer analyzer, int? seed = null)
        {
            this.analyzer = analyzer;
            random = seed.HasValue ? new Random(seed.Value) : new Random();
            variant = random.Next(12);
        }

        internal void Observe(GameStateData gs, DateTime now, bool lapCompleted, string driver)
        {
            if (gs == null || analyzer == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { session = s.SessionId; call = null; lastHeavyLap = -100; }
            if (call != null && (call.DeliveryStartedUtc != DateTime.MinValue || !call.IsValid(gs, now))) call = null;
            if (call != null || s.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane) return;

            var heavy = analyzer.TakeHeavy();
            if (heavy != null && s.CompletedLaps - lastHeavyLap >= 3)
            {
                lastHeavyLap = s.CompletedLaps;
                Offer(gs, now, "insight-flatspot", TeamMember.Mik, FlatSpot(Wheel(heavy.Wheel), heavy.Corner, variant++), 4,
                    "wheel_locked_below_35_percent_for_at_least_0.3s");
                return;
            }
            if (!lapCompleted) return;
            var p = analyzer.FindPattern(s.CompletedLaps);
            if (p == null) return;
            string n = string.IsNullOrWhiteSpace(driver) ? null : driver;
            switch (p.Kind)
            {
                case DrivingDynamicsAnalyzer.Kind.OffTrack:
                    Offer(gs, now, "insight-offtrack", TeamMember.Ben, OffTrack(n, Place(p.Corner, p.Distance, 0), p.Laps, p.Wheels, variant++), 3, "two_or_more_wheels_off_same_place_in_two_of_three_laps");
                    break;
                case DrivingDynamicsAnalyzer.Kind.Wheelspin:
                    Offer(gs, now, "insight-wheelspin", TeamMember.Mik, Wheelspin(Place(p.Corner, p.Distance, 2), p.Wheel >= 2, p.Laps, variant++), 3, "wheelspin_on_exit_same_place_in_two_of_three_laps");
                    break;
                default:
                    Offer(gs, now, "insight-lockup", TeamMember.Mik, Lockup(Place(p.Corner, p.Distance, 1), Wheel(p.Wheel), p.Wheel >= 2, p.Laps, variant++), 3, "wheel_lockup_same_place_in_two_of_three_laps");
                    break;
            }
        }

        private void Offer(GameStateData gs, DateTime now, string topic, TeamMember member, string text, int priority, string evidence)
        {
            call = new ObservedRaceCall
            {
                Topic = topic, Text = text, Evidence = evidence, Member = member, Priority = priority, Position = -1,
                SessionId = gs.SessionData.SessionId, SessionType = gs.SessionData.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(40),
                AdditionalValidity = live => !live.PitData.InPitlane && !live.FlagData.isLocalYellow
            };
            LiveDiagnostics.Decision(topic, "detected", evidence, text, call.EventId);
        }

        /// <summary>Wer die wartende Meldung spricht (für die Abfrage, ob dieses Crew-Mitglied aktiv ist).</summary>
        internal TeamMember PendingMember => call?.Member ?? TeamMember.Mik;

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) return null;
            if (busy || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }

        // ---------- Sprache (DE/EN/ES, selbst formuliert) ----------

        internal static string Wheel(int wheel)
        {
            switch (wheel)
            {
                case 0: return RadioLanguage.T("vorne links", "front left", "delantera izquierda");
                case 1: return RadioLanguage.T("vorne rechts", "front right", "delantera derecha");
                case 2: return RadioLanguage.T("hinten links", "rear left", "trasera izquierda");
                case 3: return RadioLanguage.T("hinten rechts", "rear right", "trasera derecha");
                default: return RadioLanguage.T("ein Rad", "a wheel", "una rueda");
            }
        }

        /// <summary>
        /// Ortsangabe mit Präposition: 1 Anbremsen („vor Kurve 5“), 2 Ausgang („am Ausgang von Kurve 5“), 0 in der Kurve
        /// („in Kurve 5“); ohne Kurvendaten „bei etwa 1200 Metern“.
        /// </summary>
        internal static string Place(string corner, float distance, int mode)
        {
            if (string.IsNullOrWhiteSpace(corner))
            {
                string m = (Math.Round(distance / 50f) * 50).ToString("0", RadioLanguage.Culture);
                return RadioLanguage.T("bei etwa " + m + " Metern", "at about " + m + " metres", "a unos " + m + " metros");
            }
            return mode == 1 ? RadioLanguage.T("vor " + corner, "into " + corner, "antes de " + corner)
                : mode == 2 ? RadioLanguage.T("am Ausgang von " + corner, "on the exit of " + corner, "a la salida de " + corner)
                : RadioLanguage.T("in " + corner, "at " + corner, "en " + corner);
        }

        private static string Laps(int laps) => laps >= 3
            ? RadioLanguage.T("in allen drei letzten Runden", "on each of the last three laps", "en las tres últimas vueltas")
            : RadioLanguage.T("in zwei der letzten drei Runden", "on two of the last three laps", "en dos de las tres últimas vueltas");

        private static string Up(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], RadioLanguage.Culture) + s.Substring(1);

        internal static string Lockup(string place, string wheel, bool rear, int laps, int variant)
        {
            string bias = rear
                ? RadioLanguage.T("Wenn's so bleibt: Bremsbalance einen Klick nach vorn.", "If it stays like that: brake bias one click forward.", "Si sigue así: reparto de frenada un clic hacia delante.")
                : RadioLanguage.T("Wenn's so bleibt: Bremsbalance einen Klick nach hinten.", "If it stays like that: brake bias one click rearward.", "Si sigue así: reparto de frenada un clic hacia atrás.");
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T("Beim Anbremsen " + place + " blockiert " + wheel + ", " + Laps(laps) + ". Den Druck beim Einlenken etwas früher abbauen.",
                    "Braking " + place + " the " + wheel + " is locking, " + Laps(laps) + ". Ease off the pressure a bit earlier as you turn in.",
                    "Frenando " + place + " se bloquea la " + wheel + ", " + Laps(laps) + ". Suelta un poco antes la presión al girar.");
                case 1: return RadioLanguage.T("Mik hier: " + Up(wheel) + " steht " + place + " kurz still. Das kostet Zeit und Gummi. " + bias,
                    "Mik here: the " + wheel + " stops turning " + place + ". That costs time and rubber. " + bias,
                    "Aquí Mik: la " + wheel + " se para " + place + ". Eso cuesta tiempo y goma. " + bias);
                default: return RadioLanguage.T("Kleiner Hinweis: " + Up(place) + " blockiert " + wheel + " regelmäßig. Etwas dosierter rein, dann bleibt der Reifen heil.",
                    "Quick note: " + place + " the " + wheel + " keeps locking. A touch more progressive on the brake and the tyre stays healthy.",
                    "Un apunte: " + place + " se bloquea la " + wheel + " a menudo. Algo más progresivo con el freno y el neumático aguanta.");
            }
        }

        internal static string Wheelspin(string place, bool rear, int laps, int variant)
        {
            string axle = rear ? RadioLanguage.T("die Hinterräder", "the rears", "las traseras") : RadioLanguage.T("die Vorderräder", "the fronts", "las delanteras");
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T(Up(place) + " drehen " + axle + " durch, " + Laps(laps) + ". Etwas sanfter ans Gas, dann kommst du schneller raus.",
                    Up(place) + " " + axle + " are spinning up, " + Laps(laps) + ". Squeeze the throttle and you'll come out faster.",
                    Up(place) + " patinan " + axle + ", " + Laps(laps) + ". Acelera más suave y saldrás más rápido.");
                case 1: return RadioLanguage.T("Zu viel Schlupf " + place + ". Gas progressiver – das schont auch die Reifen.",
                    "Too much wheelspin " + place + ". Feed the throttle in, it saves the tyres too.",
                    "Demasiado patinaje " + place + ". Gas más progresivo, también cuida los neumáticos.");
                default: return RadioLanguage.T("Traktion ist das Thema " + place + ". Erst die Lenkung öffnen, dann voll aufs Gas.",
                    "Traction is the issue " + place + ". Open the steering first, then full throttle.",
                    "La tracción es el problema " + place + ". Primero abre la dirección y luego gas a fondo.");
            }
        }

        internal static string OffTrack(string driver, string place, int laps, int wheels, int variant)
        {
            string n = driver == null ? "" : driver + ", ";
            string w = wheels >= 4 ? RadioLanguage.T("mit allen vier Rädern", "with all four wheels", "con las cuatro ruedas")
                : wheels == 3 ? RadioLanguage.T("mit drei Rädern", "with three wheels", "con tres ruedas")
                : RadioLanguage.T("mit zwei Rädern", "with two wheels", "con dos ruedas");
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T(Up(n + place + " warst du " + Laps(laps) + " " + w + " neben der Strecke. Da liegt Zeit – und die Track Limits zählen mit."),
                    Up(n + "you've been off " + w + " " + place + " " + Laps(laps) + ". There's time there, and track limits are counting."),
                    Up(n + "te has salido " + w + " " + place + " " + Laps(laps) + ". Ahí hay tiempo, y los límites de pista cuentan."));
                case 1: return RadioLanguage.T("Problemstelle " + place + ": " + Laps(laps) + " raus. Lass dort einen halben Meter Reserve.",
                    "Problem spot " + place + ": off " + Laps(laps) + ". Leave half a metre of margin there.",
                    "Punto débil " + place + ": fuera " + Laps(laps) + ". Deja medio metro de margen ahí.");
                default: return RadioLanguage.T("Wiederholt neben der Strecke " + place + ". Sauber und konstant bringt dort mehr als das letzte Zehntel.",
                    "Running wide again and again " + place + ". Clean and consistent gains more than the last tenth there.",
                    "Te sales una y otra vez " + place + ". Limpio y constante da más que la última décima.");
            }
        }

        internal static string FlatSpot(string wheel, string corner, int variant)
        {
            string at = string.IsNullOrWhiteSpace(corner) ? "" : RadioLanguage.T(" vor " + corner, " into " + corner, " antes de " + corner);
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T("Das war ein heftiger Verbremser " + wheel + at + ". Achte auf Vibrationen – möglicher Bremsplatten.",
                    "That was a big lock-up " + wheel + at + ". Watch for vibrations, possible flat spot.",
                    "Eso fue un bloqueo fuerte " + wheel + at + ". Atento a vibraciones, posible plano.");
                case 1: return RadioLanguage.T(Up(wheel) + " hat" + at + " lange blockiert. Wenn es jetzt vibriert, ist das ein Bremsplatten – sag Bescheid.",
                    "The " + wheel + " locked for a long time" + at + ". If it vibrates now, that's a flat spot – let us know.",
                    "La " + wheel + " se ha bloqueado mucho rato" + at + ". Si ahora vibra, es un plano: avísanos.");
                default: return RadioLanguage.T("Starker Blockierer " + wheel + at + ". Wir behalten den Reifen im Auge, melde dich, wenn du Vibrationen spürst.",
                    "Heavy lock-up " + wheel + at + ". We're watching that tyre, shout if you feel vibrations.",
                    "Bloqueo fuerte " + wheel + at + ". Vigilamos ese neumático, avisa si notas vibraciones.");
            }
        }
    }
}
