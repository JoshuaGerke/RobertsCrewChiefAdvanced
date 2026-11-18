using System;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Schwerer Schaden, Boxenbesuch und Aufgabe (LMU-Test 18.11.2026, Interlagos).
    /// Rob: „Ihr müsst doch sehen, dass ich mein Rad verloren habe. Ich muss ganz dringend an die Box, nicht nur vielleicht.“
    /// „Kurz vor der Boxeneinfahrt muss was kommen: halt durch, gleich bist du in der Box, wir reparieren das.“
    /// „Kommentar zur Reparatur: das dauert, entscheide selbst, ob du noch rausfährst.“ „Analysier, wo ich rauskomme, gib mir
    /// Infos und Motivation.“ „Ich habe aufgegeben – da hättet ihr ein paar gute Worte finden können.“
    /// Alles aus gemessenen Daten: Rad nicht mehr am Auto, Reifen schlagartig auf 100 % Verschleiß, Schadensstufe des Spiels,
    /// Standzeit in der Box, Fahrzeuge auf der Strecke nach dem Stopp, Ausfallstatus des Spiels.
    /// </summary>
    internal sealed class PitDamageWatch
    {
        internal const double ReminderSeconds = 40, RepairFirstSeconds = 30, RepairSecondSeconds = 90;
        private int session = int.MinValue, lostCorner = -1, reminders, variant;
        private bool lostWheel, severe, urgentSpoken, arrivalSpoken, repairFirst, repairSecond, retiredSpoken, wasInPit, wasDnf, wasCrewReady, pitDamaged;
        private DateTime lastUrgentUtc = DateTime.MinValue, stoppedSinceUtc = DateTime.MinValue, exitUtc = DateTime.MinValue, lastWearUtc = DateTime.MinValue;
        private float[] lastWear;
        private ObservedRaceCall call;

        internal void Reset()
        {
            session = int.MinValue; lostCorner = -1; reminders = 0; lostWheel = severe = urgentSpoken = arrivalSpoken = repairFirst = repairSecond = false;
            retiredSpoken = wasInPit = wasDnf = wasCrewReady = pitDamaged = false;
            lastUrgentUtc = stoppedSinceUtc = exitUtc = lastWearUtc = DateTime.MinValue; lastWear = null; call = null;
        }

        internal static string Corner(int i)
        {
            switch (i)
            {
                case 0: return RadioLanguage.T("vorne links", "front left", "delantera izquierda");
                case 1: return RadioLanguage.T("vorne rechts", "front right", "delantera derecha");
                case 2: return RadioLanguage.T("hinten links", "rear left", "trasera izquierda");
                default: return RadioLanguage.T("hinten rechts", "rear right", "trasera derecha");
            }
        }

        /// <summary>Ecke mit verlorenem Rad oder schlagartig zerstörtem Reifen; -1 ohne solchen Befund.</summary>
        internal static int LostCorner(GameStateData gs, float[] previousWear, double secondsSincePrevious, out bool wheelGone)
        {
            wheelGone = false;
            var t = gs.TyreData;
            bool[] attached = { t.LeftFrontAttached, t.RightFrontAttached, t.LeftRearAttached, t.RightRearAttached };
            for (int i = 0; i < 4; i++) if (!attached[i]) { wheelGone = true; return i; }
            if (!t.TyreWearActive || previousWear == null || secondsSincePrevious <= 0 || secondsSincePrevious > 3) return -1;
            float[] wear = Wear(gs);
            for (int i = 0; i < 4; i++)
                if (previousWear[i] >= 0 && previousWear[i] < 60 && wear[i] >= 95) return i; // von normal auf voll verschlissen in Sekunden
            return -1;
        }

        internal static float[] Wear(GameStateData gs)
        {
            var t = gs.TyreData;
            return new[] { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
        }

        internal static bool Destroyed(CarDamageData d) =>
            d != null && (d.OverallAeroDamage == DamageLevel.DESTROYED ||
                d.SuspensionDamageStatus != null && d.SuspensionDamageStatus.hasValueAtLevel(DamageLevel.DESTROYED));

        internal static bool Major(CarDamageData d) =>
            d != null && (d.OverallAeroDamage == DamageLevel.MAJOR || d.OverallEngineDamage == DamageLevel.MAJOR || d.OverallEngineDamage == DamageLevel.DESTROYED ||
                d.SuspensionDamageStatus != null && d.SuspensionDamageStatus.hasValueAtLevel(DamageLevel.MAJOR));

        private static string Who(string name, string fallback = "") => string.IsNullOrEmpty(name) ? fallback : name + ", ";
        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        private static string Pick(string[] options, int variant) => options[((variant % options.Length) + options.Length) % options.Length];

        internal static string Urgent(string name, int corner, bool wheelGone, int variant)
        {
            string c = corner >= 0 ? Corner(corner) : null, n = Who(name);
            string whatDe = c == null ? "das Auto ist zu stark beschädigt" : wheelGone ? "das Rad " + c + " ist weg" : "der Reifen " + c + " ist zerstört";
            string whatEn = c == null ? "the car is too badly damaged" : wheelGone ? "the " + c + " wheel is gone" : "the " + c + " tyre is destroyed";
            string whatEs = c == null ? "el coche está demasiado dañado" : wheelGone ? "has perdido la rueda " + c : "el neumático " + c + " está destrozado";
            return Pick(new[]
            {
                RadioLanguage.T(Cap(n + whatDe) + ". Sofort an die Box, so kannst du nicht weiterfahren.", Cap(n + whatEn) + ". Box now, you can't carry on like this.", Cap(n + whatEs) + ". A boxes ya, así no puedes seguir."),
                RadioLanguage.T("Box, Box! " + Cap(whatDe) + ". Langsam rein und weg von der Ideallinie.", "Box, box! " + Cap(whatEn) + ". Bring it in slowly and stay off the racing line.", "¡Box, box! " + Cap(whatEs) + ". Entra despacio y fuera de la trazada."),
                RadioLanguage.T(Cap(n + whatDe) + ". Komm direkt rein, wir richten das in der Box.", Cap(n + whatEn) + ". Come straight in, we'll fix it in the pits.", Cap(n + whatEs) + ". Entra directo, lo arreglamos en boxes."),
                RadioLanguage.T("Ernst jetzt: " + whatDe + ". Diese Runde an die Box, nichts riskieren.", "This is serious: " + whatEn + ". Pit this lap, don't risk anything.", "Esto es serio: " + whatEs + ". A boxes esta vuelta, sin arriesgar."),
                RadioLanguage.T(Cap(n + whatDe) + ". Wir brauchen dich in der Box, vorsichtig zurück und Spiegel im Blick.", Cap(n + whatEn) + ". We need you in the pits, bring it back carefully and watch your mirrors.", Cap(n + whatEs) + ". Te necesitamos en boxes, vuelve con cuidado y mira los espejos."),
            }, variant);
        }

        internal static string Reminder(string name, int variant) => Pick(new[]
        {
            RadioLanguage.T("Box, Box. Das Auto muss rein, " + (string.IsNullOrEmpty(name) ? "bitte." : name + "."), "Box, box. The car has to come in" + (string.IsNullOrEmpty(name) ? "." : ", " + name + "."), "Box, box. El coche tiene que entrar" + (string.IsNullOrEmpty(name) ? "." : ", " + name + ".")),
            RadioLanguage.T("Weiter Richtung Box, ruhig und kontrolliert. Wir stehen bereit.", "Keep heading for the pits, calm and controlled. We're ready.", "Sigue hacia boxes, tranquilo y con control. Estamos listos."),
            RadioLanguage.T("Nicht mehr pushen, nur noch heil in die Box bringen.", "No more pushing, just get it back to the pits in one piece.", "No aprietes más, solo trae el coche entero a boxes."),
            RadioLanguage.T("Die Crew wartet, Box diese Runde. Vorsicht beim Verkehr.", "Crew is waiting, box this lap. Watch the traffic.", "El equipo espera, box esta vuelta. Ojo con el tráfico."),
        }, variant);

        internal static string Arrival(string name, int variant) => Pick(new[]
        {
            RadioLanguage.T(Cap(Who(name) + "halt durch, gleich bist du in der Box. Wir reparieren das."), Cap(Who(name) + "hang in there, you're nearly in the box. We'll fix it."), Cap(Who(name) + "aguanta, ya casi estás en boxes. Lo arreglamos.")),
            RadioLanguage.T("Fast geschafft. Limiter rein, die Jungs stehen mit allem bereit.", "Nearly there. Limiter on, the guys are ready with everything.", "Casi está. Limitador puesto, el equipo está preparado con todo."),
            RadioLanguage.T("Gut gemacht bis hierhin. Rein in die Gasse, wir kümmern uns um das Auto.", "Well done getting it here. Into the lane, we'll take care of the car.", "Bien hecho hasta aquí. Entra al pit lane, nosotros nos ocupamos del coche."),
            RadioLanguage.T("Du bist gleich da. Ruhig an den Stellplatz, die Reparatur ist vorbereitet.", "Almost there. Calmly to the box, the repair is prepared.", "Ya llegas. Tranquilo hasta tu box, la reparación está preparada."),
            RadioLanguage.T("Box-Einfahrt, sehr gut. Durchatmen, jetzt übernimmt die Crew.", "Pit entry, very good. Take a breath, the crew takes over now.", "Entrada a boxes, muy bien. Respira, ahora se encarga el equipo."),
        }, variant);

        internal static string RepairWait(int seconds, bool second, int variant)
        {
            string s = seconds.ToString(RadioLanguage.Culture);
            return second ? Pick(new[]
            {
                RadioLanguage.T("Du stehst seit " + s + " Sekunden, die Reparatur ist umfangreich. Das Rennen wird schwer, aber jede Runde bringt Erfahrung. Deine Entscheidung, ob du weiterfährst.",
                    "You've been stationary for " + s + " seconds, it's a big repair. The race will be tough, but every lap is experience. Your call whether you carry on.",
                    "Llevas " + s + " segundos parado, la reparación es grande. La carrera será dura, pero cada vuelta suma experiencia. Tú decides si sigues."),
                RadioLanguage.T(s + " Sekunden Standzeit. Vorne ist das Rennen weg, aber wir können noch Daten sammeln. Entscheide in Ruhe.",
                    s + " seconds stationary. The front is gone, but we can still collect data. Decide calmly.",
                    s + " segundos parado. La cabeza se ha ido, pero aún podemos recoger datos. Decide con calma."),
            }, variant) : Pick(new[]
            {
                RadioLanguage.T("Die Reparatur läuft, du stehst seit " + s + " Sekunden. Das wird ein langer Stopp.",
                    "Repair in progress, you've been stationary for " + s + " seconds. This will be a long stop.",
                    "Reparación en curso, llevas " + s + " segundos parado. Será una parada larga."),
                RadioLanguage.T(s + " Sekunden in der Box, die Jungs arbeiten noch. Bleib ruhig, das dauert etwas.",
                    s + " seconds in the box, the guys are still working. Stay calm, it'll take a while.",
                    s + " segundos en boxes, el equipo sigue trabajando. Tranquilo, tardará un poco."),
                RadioLanguage.T("Noch nicht fertig, " + s + " Sekunden bisher. Wir sagen dir, wenn das Auto bereit ist.",
                    "Not done yet, " + s + " seconds so far. We'll tell you when the car is ready.",
                    "Aún no está, " + s + " segundos hasta ahora. Te avisamos cuando el coche esté listo."),
            }, variant);
        }

        /// <summary>Verkehr beim Wiedereinsetzen; Sekunden &lt;= 0 heißt kein Auto in Reichweite.</summary>
        internal static string Rejoin(string name, int classPosition, double aheadS, string aheadClass, double behindS, string behindClass, int variant)
        {
            string pos = classPosition > 0 ? RadioLanguage.T("Klassenplatz " + classPosition + ". ", "P" + classPosition + " in class. ", "Posición " + classPosition + " de clase. ") : "";
            string traffic;
            if (behindS > 0 && behindS < 3)
                traffic = RadioLanguage.T("Hinter dir kommt " + (behindClass != null ? "ein " + behindClass : "einer") + " in " + RadioLanguage.Num(behindS, "0.0") + " Sekunden, Linie halten. ",
                    (behindClass != null ? "A " + behindClass : "A car") + " is " + RadioLanguage.Num(behindS, "0.0") + " seconds behind, hold your line. ",
                    (behindClass != null ? "Un " + behindClass : "Un coche") + " viene a " + RadioLanguage.Num(behindS, "0.0") + " segundos detrás, mantén la trazada. ");
            else if (aheadS > 0 && aheadS < 5)
                traffic = RadioLanguage.T("Vor dir " + (aheadClass != null ? "ein " + aheadClass : "ein Wagen") + " mit " + RadioLanguage.Num(aheadS, "0.0") + " Sekunden, den hast du als Ziel. ",
                    (aheadClass != null ? "A " + aheadClass : "A car") + " " + RadioLanguage.Num(aheadS, "0.0") + " seconds ahead, that's your target. ",
                    (aheadClass != null ? "Un " + aheadClass : "Un coche") + " a " + RadioLanguage.Num(aheadS, "0.0") + " segundos delante, ese es tu objetivo. ");
            else
                traffic = RadioLanguage.T("Freie Bahn um dich herum. ", "Clear track around you. ", "Pista libre a tu alrededor. ");
            string push = Pick(new[]
            {
                RadioLanguage.T("Reifen auf Temperatur bringen, dann holen wir uns Platz für Platz zurück.", "Get the tyres up to temperature, then we take them back one by one.", "Pon las gomas en temperatura y luego recuperamos puesto a puesto."),
                RadioLanguage.T("Neuer Stint, neue Chance. Sauber fahren, der Rest kommt.", "New stint, new chance. Drive clean, the rest will come.", "Nuevo stint, nueva oportunidad. Conduce limpio, lo demás llegará."),
                RadioLanguage.T("Kopf hoch, das Auto ist wieder ganz. Jetzt zeigen wir, was es kann.", "Head up, the car is whole again. Now let's show what it can do.", "Cabeza alta, el coche vuelve a estar entero. Ahora a demostrar lo que vale."),
                RadioLanguage.T("Ruhig rein in den Rhythmus, die schnellen Runden kommen von allein.", "Ease into the rhythm, the quick laps will come by themselves.", "Entra tranquilo en el ritmo, las vueltas rápidas llegarán solas."),
                RadioLanguage.T("Wir sind wieder im Rennen. Konzentriert bleiben, jede Runde zählt.", "We're back in the race. Stay focused, every lap counts.", "Volvemos a estar en carrera. Concentrado, cada vuelta cuenta."),
            }, variant);
            return Cap(Who(name) + RadioLanguage.T("du bist raus. ", "you're out. ", "ya estás fuera. ")) + pos + traffic + push;
        }

        internal static string Retired(string name, int variant)
        {
            string n = string.IsNullOrEmpty(name) ? "" : name + ", ";
            return Pick(new[]
            {
                RadioLanguage.T(Cap(n + "das Rennen ist für uns vorbei. Schade, aber kein Kopf hängen lassen. Beim nächsten Mal schaffen wir mehr."),
                    Cap(n + "the race is over for us. A shame, but heads up. Next time we'll get more out of it."),
                    Cap(n + "la carrera ha terminado para nosotros. Una pena, pero cabeza alta. La próxima sacaremos más.")),
                RadioLanguage.T(Cap(n + "du hast aufgegeben, die richtige Entscheidung mit dem Auto. Durchatmen, wir blicken nach vorn."),
                    Cap(n + "you've retired, the right call with that car. Take a breath, we look forward."),
                    Cap(n + "has abandonado, la decisión correcta con ese coche. Respira, miramos hacia delante.")),
                RadioLanguage.T(Cap(n + "Feierabend für heute. Das Tempo war da, das nehmen wir mit ins nächste Rennen."),
                    Cap(n + "that's it for today. The pace was there, we take that into the next race."),
                    Cap(n + "se acabó por hoy. El ritmo estaba ahí, eso nos lo llevamos a la próxima carrera.")),
                RadioLanguage.T(Cap(n + "nicht unser Tag. Runterkommen, cool down, und dann schauen wir gemeinsam, was wir lernen."),
                    Cap(n + "not our day. Cool down, and then we look together at what we learn."),
                    Cap(n + "no ha sido nuestro día. Relájate y luego vemos juntos qué aprendemos.")),
                RadioLanguage.T(Cap(n + "Rennen beendet. Das tut weh, aber du hast gekämpft. Nächstes Mal wird's besser."),
                    Cap(n + "race over. It hurts, but you fought. Next time will be better."),
                    Cap(n + "carrera terminada. Duele, pero has luchado. La próxima irá mejor.")),
                RadioLanguage.T(Cap(n + "wir stellen das Auto ab. Danke für den Einsatz, wir kommen stärker zurück."),
                    Cap(n + "we park the car. Thanks for the effort, we'll come back stronger."),
                    Cap(n + "aparcamos el coche. Gracias por el esfuerzo, volveremos más fuertes.")),
            }, variant);
        }

        internal static string CrewReady(int variant) => Pick(new[]
        {
            RadioLanguage.T("Crew steht bereit, du kannst reinkommen.", "Crew is ready, you can come in.", "El equipo está listo, puedes entrar."),
            RadioLanguage.T("Die Jungs sind bereit für den Stopp.", "The guys are ready for the stop.", "Los mecánicos están listos para la parada."),
            RadioLanguage.T("Boxencrew bereit. Komm rein, wenn es passt.", "Pit crew ready. Come in when it suits.", "Equipo de boxes listo. Entra cuando te venga bien."),
            RadioLanguage.T("Stopp ist vorbereitet, die Crew wartet auf dich.", "Stop is prepared, the crew is waiting for you.", "Parada preparada, el equipo te espera."),
        }, variant);

        internal void Observe(GameStateData gs, TrackAwareness tracks, string name, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (call != null && (call.DeliveryStartedUtc != DateTime.MinValue || now > call.ExpiresUtc)) call = null;
            bool inPit = gs.PitData.InPitlane, green = s.SessionPhase == SessionPhase.Green || s.SessionPhase == SessionPhase.FullCourseYellow;
            bool race = s.SessionType == SessionType.Race;

            // 1. Rad verloren, Reifen zerstört oder Schaden „zerstört“: dringend an die Box.
            int corner = LostCorner(gs, lastWear, lastWearUtc == DateTime.MinValue ? -1 : (now - lastWearUtc).TotalSeconds, out bool gone);
            if (gs.TyreData.TyreWearActive) { lastWear = Wear(gs); lastWearUtc = now; }
            if (corner >= 0 && !inPit) { if (lostCorner < 0 || gone && !lostWheel) { lostCorner = corner; lostWheel = gone; urgentSpoken = false; reminders = 0; } }
            bool destroyed = Destroyed(gs.CarDamageData);
            if (!severe && (lostCorner >= 0 || destroyed) && !inPit && green) { severe = true; urgentSpoken = false; }
            if (Major(gs.CarDamageData) || severe) pitDamaged = true;

            // 2. Boxengasse: Zuspruch beim Hineinfahren, Standzeit-Kommentar, danach Wiedereinsetzen.
            if (inPit && !wasInPit)
            {
                if (pitDamaged && !arrivalSpoken && race && !s.IsDNF) Offer(gs, now, "pit-arrival-support", Arrival(name, variant++), "pit_lane_entered_with_confirmed_serious_damage", TeamMember.Mik, 6, 10);
                arrivalSpoken = true; severe = false; lostCorner = -1; lostWheel = false; reminders = 0;
            }
            if (inPit && gs.PositionAndMotionData.CarSpeed < .5f)
            {
                if (stoppedSinceUtc == DateTime.MinValue) stoppedSinceUtc = now;
                double stood = (now - stoppedSinceUtc).TotalSeconds;
                if (race && pitDamaged && !s.IsDNF && !repairFirst && stood >= RepairFirstSeconds)
                { repairFirst = true; Offer(gs, now, "pit-repair-wait", RepairWait((int)stood, false, variant++), "measured_standstill_in_pit_with_damage", TeamMember.Ben, 4, 15); }
                else if (race && pitDamaged && !s.IsDNF && !repairSecond && stood >= RepairSecondSeconds)
                { repairSecond = true; Offer(gs, now, "pit-repair-wait", RepairWait((int)stood, true, variant++), "measured_standstill_in_pit_with_damage", TeamMember.Ben, 4, 15); }
            }
            else if (!inPit) stoppedSinceUtc = DateTime.MinValue;
            if (!inPit && wasInPit)
            {
                exitUtc = now;
                pitDamaged = Major(gs.CarDamageData); arrivalSpoken = repairFirst = repairSecond = false;
            }
            wasInPit = inPit;
            if (race && green && !inPit && !s.IsDNF && exitUtc != DateTime.MinValue && (now - exitUtc).TotalSeconds >= 3 && call == null)
            {
                exitUtc = DateTime.MinValue;
                Nearest(tracks, gs, out double ahead, out string aheadClass, out double behind, out string behindClass);
                Offer(gs, now, "pit-rejoin", Rejoin(name, s.ClassPosition, ahead, aheadClass, behind, behindClass, variant++), "pit_exit_measured_track_positions", TeamMember.Ben, 4, 12);
                return;
            }

            // 3. Dringender Boxenruf und Erinnerungen, bis das Auto in der Gasse ist.
            if (severe && !inPit && green && call == null && (!urgentSpoken || reminders < 2 && (now - lastUrgentUtc).TotalSeconds >= ReminderSeconds))
            {
                string text = !urgentSpoken ? Urgent(name, lostCorner, lostWheel, variant++) : Reminder(name, variant++);
                Offer(gs, now, "pit-urgent-damage", text, lostWheel ? "wheel_detached" : lostCorner >= 0 ? "tyre_wear_jumped_to_100_percent" : "damage_level_destroyed",
                    TeamMember.Mik, 8, 12);
                if (urgentSpoken) reminders++;
                urgentSpoken = true; lastUrgentUtc = now;
            }

            // 4. Boxencrew bereit (Spielmeldung).
            if (gs.PitData.IsPitCrewReady && !wasCrewReady && !inPit && call == null)
                Offer(gs, now, "pit-crew-ready", CrewReady(variant++), "game_message_crew_ready_for_pitstop", TeamMember.Mik, 5, 10);
            wasCrewReady = gs.PitData.IsPitCrewReady;

            // 5. Aufgabe: ein paar gute Worte.
            if (race && s.IsDNF && !wasDnf && !retiredSpoken)
            {
                retiredSpoken = true;
                call = null;
                Offer(gs, now, "race-retired", Retired(name, variant++), "game_reports_dnf", TeamMember.Ben, 6, 20, allowInPit: true);
            }
            wasDnf = s.IsDNF;
        }

        private static void Nearest(TrackAwareness tracks, GameStateData gs, out double ahead, out string aheadClass, out double behind, out string behindClass)
        {
            ahead = behind = -1; aheadClass = behindClass = null;
            var snap = tracks?.Snapshot;
            if (snap == null || snap.SessionId != gs.SessionData.SessionId) return;
            var cars = snap.Cars.Where(c => !c.InPits && c.SpatiallyConsistent && c.RouteGapM.HasValue && c.TrackIntervalSeconds.HasValue).ToArray();
            var a = cars.Where(c => c.RouteGapM > 0).OrderBy(c => c.RouteGapM).FirstOrDefault();
            var b = cars.Where(c => c.RouteGapM < 0).OrderByDescending(c => c.RouteGapM).FirstOrDefault();
            if (a != null) { ahead = Math.Abs(a.TrackIntervalSeconds.Value); aheadClass = MulticlassTraffic.OtherClass(a) ? a.ClassLabel : null; }
            if (b != null) { behind = Math.Abs(b.TrackIntervalSeconds.Value); behindClass = MulticlassTraffic.OtherClass(b) ? b.ClassLabel : null; }
        }

        private void Offer(GameStateData gs, DateTime now, string topic, string text, string evidence, TeamMember member, int priority, int seconds, bool allowInPit = false)
        {
            bool pitTopic = topic == "pit-arrival-support" || topic == "pit-repair-wait" || allowInPit;
            call = new ObservedRaceCall
            {
                Topic = topic, Text = text, Evidence = evidence, Member = member, Priority = priority, Position = -1,
                SessionId = gs.SessionData.SessionId, SessionType = gs.SessionData.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(seconds),
                AllowPitlane = pitTopic,
                AdditionalValidity = live => pitTopic ? (topic != "pit-repair-wait" || live.PitData.InPitlane) : !live.PitData.InPitlane
            };
            LiveDiagnostics.Decision(topic, "detected", evidence, text, call.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, Func<TeamMember, bool> enabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (call == null || busy || !enabled(call.Member)) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
