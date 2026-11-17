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
        private int session = int.MinValue, variant, lastHeavyLap = -100, lastBigOffLap = -100;

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
            if (s.SessionId != session) { session = s.SessionId; call = null; lastHeavyLap = lastBigOffLap = -100; }
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
            var off = analyzer.TakeBigOff();
            // RaceRoom meldet Dreck selbst (TyreGripWatch „tyre-dirt“) – dann nicht zusätzlich.
            if (off != null && (gs.TyreData.DirtPickupEmulationActive || now - TyreGripWatch.LastDirtCallUtc < TimeSpan.FromSeconds(90))) off = null;
            if (off != null && s.CompletedLaps - lastBigOffLap >= 2)
            {
                lastBigOffLap = s.CompletedLaps;
                Offer(gs, now, "insight-dirty-tyres", TeamMember.Mik, DirtyTyres(off.Wheels, variant++), 4, "three_or_more_wheels_off_track_for_at_least_0.6s");
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
                case DrivingDynamicsAnalyzer.Kind.Understeer:
                case DrivingDynamicsAnalyzer.Kind.Oversteer:
                    Offer(gs, now, "insight-balance", TeamMember.Mik,
                        Balance(p.Kind == DrivingDynamicsAnalyzer.Kind.Oversteer, p.Phase, Place(p.Corner, p.Distance, p.Phase == 2 ? 2 : 0), p.Laps, variant++), 3,
                        (p.Kind == DrivingDynamicsAnalyzer.Kind.Oversteer ? "yaw_rate_exceeds_lateral_acceleration" : "less_curvature_per_steering_than_learned") + "_same_place_and_phase_in_two_of_three_laps");
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
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T("Beim Anbremsen " + place + " blockiert " + wheel + ", " + Laps(laps) + ". Den Druck beim Einlenken etwas früher abbauen.",
                    "Braking " + place + " the " + wheel + " is locking, " + Laps(laps) + ". Ease off the pressure a bit earlier as you turn in.",
                    "Frenando " + place + " se bloquea la " + wheel + ", " + Laps(laps) + ". Suelta un poco antes la presión al girar.");
                case 1: return RadioLanguage.T("Mik hier: " + Up(wheel) + " steht " + place + " kurz still. Das kostet Zeit und Gummi. " + bias,
                    "Mik here: the " + wheel + " stops turning " + place + ". That costs time and rubber. " + bias,
                    "Aquí Mik: la " + wheel + " se para " + place + ". Eso cuesta tiempo y goma. " + bias);
                case 3: return RadioLanguage.T("Bremszone " + place + ": " + Up(wheel) + " blockiert, " + Laps(laps) + ". Etwas weniger Spitzendruck, dafür länger tragen.",
                    "Braking zone " + place + ": the " + wheel + " is locking, " + Laps(laps) + ". A little less peak pressure, carry it longer.",
                    "Zona de frenada " + place + ": se bloquea la " + wheel + ", " + Laps(laps) + ". Algo menos de presión punta y alárgala.");
                default: return RadioLanguage.T("Kleiner Hinweis: " + Up(place) + " blockiert " + wheel + " regelmäßig. Etwas dosierter rein, dann bleibt der Reifen heil.",
                    "Quick note: " + place + " the " + wheel + " keeps locking. A touch more progressive on the brake and the tyre stays healthy.",
                    "Un apunte: " + place + " se bloquea la " + wheel + " a menudo. Algo más progresivo con el freno y el neumático aguanta.");
            }
        }

        internal static string Wheelspin(string place, bool rear, int laps, int variant)
        {
            string axle = rear ? RadioLanguage.T("die Hinterräder", "the rears", "las traseras") : RadioLanguage.T("die Vorderräder", "the fronts", "las delanteras");
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T(Up(place) + " drehen " + axle + " durch, " + Laps(laps) + ". Etwas sanfter ans Gas, dann kommst du schneller raus.",
                    Up(place) + " " + axle + " are spinning up, " + Laps(laps) + ". Squeeze the throttle and you'll come out faster.",
                    Up(place) + " patinan " + axle + ", " + Laps(laps) + ". Acelera más suave y saldrás más rápido.");
                case 1: return RadioLanguage.T("Zu viel Schlupf " + place + ". Gas progressiver – das schont auch die Reifen.",
                    "Too much wheelspin " + place + ". Feed the throttle in, it saves the tyres too.",
                    "Demasiado patinaje " + place + ". Gas más progresivo, también cuida los neumáticos.");
                case 3: return RadioLanguage.T(Up(place) + " verlierst du Traktion: " + axle + " drehen durch. Kurzschalten oder später voll aufs Gas.",
                    Up(place) + " you're losing traction: " + axle + " are spinning. Short-shift or go full throttle later.",
                    Up(place) + " pierdes tracción: patinan " + axle + ". Cambia antes o pisa a fondo más tarde.");
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
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T(Up(n + place + " warst du " + Laps(laps) + " " + w + " neben der Strecke. Da liegt Zeit – und die Track Limits zählen mit."),
                    Up(n + "you've been off " + w + " " + place + " " + Laps(laps) + ". There's time there, and track limits are counting."),
                    Up(n + "te has salido " + w + " " + place + " " + Laps(laps) + ". Ahí hay tiempo, y los límites de pista cuentan."));
                case 1: return RadioLanguage.T("Problemstelle " + place + ": " + Laps(laps) + " raus. Lass dort einen halben Meter Reserve.",
                    "Problem spot " + place + ": off " + Laps(laps) + ". Leave half a metre of margin there.",
                    "Punto débil " + place + ": fuera " + Laps(laps) + ". Deja medio metro de margen ahí.");
                case 3: return RadioLanguage.T(Up(n + place + " wird's immer wieder zu weit – " + Laps(laps) + ". Früher einlenken, dann passt der Ausgang."),
                    Up(n + "you keep running too wide " + place + " – " + Laps(laps) + ". Turn in a touch earlier and the exit works."),
                    Up(n + place + " te abres demasiado una y otra vez, " + Laps(laps) + ". Gira un poco antes y la salida encaja."));
                default: return RadioLanguage.T("Wiederholt neben der Strecke " + place + ". Sauber und konstant bringt dort mehr als das letzte Zehntel.",
                    "Running wide again and again " + place + ". Clean and consistent gains more than the last tenth there.",
                    "Te sales una y otra vez " + place + ". Limpio y constante da más que la última décima.");
            }
        }

        /// <summary>Balance (Unter-/Übersteuern) je Kurvenphase: 1 Eingang, 0 Scheitel, 2 Ausgang.</summary>
        internal static string Balance(bool oversteer, int phase, string place, int laps, int variant)
        {
            int ph = phase == 1 ? 1 : phase == 2 ? 2 : 0;
            string obs, advice;
            if (!oversteer)
            {
                obs = ph == 1 ? RadioLanguage.T("schiebt das Auto beim Einlenken über die Vorderräder", "the car pushes on turn-in", "el coche empuja de morro al girar")
                    : ph == 2 ? RadioLanguage.T("schiebt das Auto unter Gas über die Vorderräder", "the car pushes wide on the throttle", "el coche se va de morro al acelerar")
                    : RadioLanguage.T("untersteuert das Auto im Scheitel", "the car understeers at the apex", "el coche subvira en el vértice");
                advice = ph == 1 ? RadioLanguage.T("Bremse etwas früher lösen, dann hat die Vorderachse mehr Grip.", "Release the brake a touch earlier and the front will bite.", "Suelta el freno un poco antes y el tren delantero agarrará más.")
                    : ph == 2 ? RadioLanguage.T("Lenkung erst öffnen, dann voll aufs Gas.", "Open the steering first, then full throttle.", "Primero abre la dirección y luego gas a fondo.")
                    : RadioLanguage.T("Etwas langsamer rein, dafür früher wieder aufs Gas.", "Slightly slower in, earlier back on the power.", "Entra algo más lento y acelera antes.");
            }
            else
            {
                obs = ph == 1 ? RadioLanguage.T("wird das Heck beim Anbremsen leicht", "the rear goes light under braking", "la trasera se aligera en la frenada")
                    : ph == 2 ? RadioLanguage.T("kommt das Heck unter Gas", "the rear steps out on the throttle", "la trasera se suelta al acelerar")
                    : RadioLanguage.T("kommt das Heck im Scheitel", "the rear steps out mid-corner", "la trasera se suelta en el vértice");
                advice = ph == 1 ? RadioLanguage.T("Wenn's bleibt: Bremsbalance einen Klick nach vorn.", "If it stays like that: brake bias one click forward.", "Si sigue así: reparto de frenada un clic hacia delante.")
                    : ph == 2 ? RadioLanguage.T("Sanfter ans Gas – oder Traktionskontrolle eine Stufe höher.", "Smoother on the throttle, or traction control up a step.", "Gas más suave, o sube un punto el control de tracción.")
                    : RadioLanguage.T("Ruhiger einlenken, nicht nachlenken.", "Calmer turn-in, don't add more steering.", "Gira con más calma, sin corregir de más.");
            }
            switch (((variant % 4) + 4) % 4)
            {
                case 3: return RadioLanguage.T("Mik zum Fahrverhalten: ", "Mik on the handling: ", "Mik sobre el comportamiento: ") + Up(place) + " " + obs + ". " + advice;
                case 0: return Up(place) + " " + obs + ", " + Laps(laps) + ". " + advice;
                case 1: return RadioLanguage.T("Balance-Check: ", "Balance check: ", "Equilibrio: ") + Up(place) + " " + obs + ". " + advice;
                default: return RadioLanguage.T("Aus den Daten: ", "From the data: ", "Según los datos: ") + Up(place) + " " + obs +
                    RadioLanguage.T(" – das sehen wir jetzt regelmäßig. ", " – we're seeing that regularly now. ", ", lo vemos con regularidad. ") + advice;
            }
        }

        internal static string DirtyTyres(int wheels, int variant)
        {
            bool all = wheels >= 4;
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T((all ? "Alle vier Räder waren draußen" : "Das war ein weiter Ausflug") + " – die Reifen haben Dreck aufgenommen. Die nächsten zwei Kurven etwas vorsichtiger.",
                    (all ? "All four wheels were off" : "That was a big trip off") + " – the tyres have picked up dirt. Take the next couple of corners a bit easier.",
                    (all ? "Las cuatro ruedas fuera" : "Eso fue una salida grande") + ": los neumáticos han cogido suciedad. Las dos próximas curvas con algo más de cuidado.");
                case 1: return RadioLanguage.T("Erst die Reifen wieder sauber fahren, dann zurück auf Tempo. Bremspunkte in den nächsten Kurven etwas früher.",
                    "Clean the tyres up first, then back to pace. Brake a little earlier for the next few corners.",
                    "Primero limpia los neumáticos y luego vuelve al ritmo. Frena un poco antes en las próximas curvas.");
                case 3: return RadioLanguage.T("Ausflug überstanden. Reifen sind noch schmutzig – zwei, drei Kurven mit Gefühl, dann wieder angreifen.",
                    "Trip survived. Tyres are still dirty – two or three corners with feel, then attack again.",
                    "Salida superada. Los neumáticos siguen sucios: dos o tres curvas con tacto y luego a atacar.");
                default: return RadioLanguage.T("Ruhig, das kostet nur kurz. Mit Dreck auf den Reifen fehlt Grip – eine halbe Runde sauber fahren, dann ist er wieder da.",
                    "Easy, that only costs a moment. Dirty tyres mean less grip – half a lap of clean driving and it's back.",
                    "Tranquilo, solo cuesta un momento. Con suciedad en los neumáticos falta agarre: media vuelta limpia y vuelve.");
            }
        }

        internal static string FlatSpot(string wheel, string corner, int variant)
        {
            string at = string.IsNullOrWhiteSpace(corner) ? "" : RadioLanguage.T(" vor " + corner, " into " + corner, " antes de " + corner);
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T("Das war ein heftiger Verbremser " + wheel + at + ". Achte auf Vibrationen – möglicher Bremsplatten.",
                    "That was a big lock-up " + wheel + at + ". Watch for vibrations, possible flat spot.",
                    "Eso fue un bloqueo fuerte " + wheel + at + ". Atento a vibraciones, posible plano.");
                case 1: return RadioLanguage.T(Up(wheel) + " hat" + at + " lange blockiert. Wenn es jetzt vibriert, ist das ein Bremsplatten – sag Bescheid.",
                    "The " + wheel + " locked for a long time" + at + ". If it vibrates now, that's a flat spot – let us know.",
                    "La " + wheel + " se ha bloqueado mucho rato" + at + ". Si ahora vibra, es un plano: avísanos.");
                case 3: return RadioLanguage.T("Der Verbremser " + wheel + at + " war heftig. Spürst du Vibrationen beim Bremsen, gib Bescheid.",
                    "That lock-up " + wheel + at + " was a big one. If you feel vibrations under braking, tell us.",
                    "Ese bloqueo " + wheel + at + " fue fuerte. Si notas vibraciones al frenar, avísanos.");
                default: return RadioLanguage.T("Starker Blockierer " + wheel + at + ". Wir behalten den Reifen im Auge, melde dich, wenn du Vibrationen spürst.",
                    "Heavy lock-up " + wheel + at + ". We're watching that tyre, shout if you feel vibrations.",
                    "Bloqueo fuerte " + wheel + at + ". Vigilamos ese neumático, avisa si notas vibraciones.");
            }
        }
    }
}
