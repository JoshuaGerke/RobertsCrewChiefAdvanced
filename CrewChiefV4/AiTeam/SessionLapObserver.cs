using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Bounded, local timed-lap and race personal-best observation. No network, audio or disk on the telemetry thread.</summary>
    public sealed class SessionLapObserver
    {
        private readonly List<ObservedRaceCall> pending = new List<ObservedRaceCall>();
        private int session = int.MinValue, lastLap = -1;
        private RadioVariantCursor improvements = new RadioVariantCursor(), rivalBests = new RadioVariantCursor();
        private readonly RadioVariantCursor firstValidVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor nearBestVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor sectorVariants = new RadioVariantCursor();
        private SessionType type = SessionType.Unavailable;
        private float personalBest = -1, classBest = -1;
        private DateTime lastRadio = DateTime.MinValue;
        // Zuletzt angesagte eigene Bestzeit dieser Sitzung (Rob 17.11., Bathurst): Der Allzeit-Rekord derselben Runde soll die
        // Zeit nicht ein zweites Mal vorlesen.
        private float announcedLapSeconds = -1;
        private DateTime announcedLapUtc = DateTime.MinValue;
        internal static readonly TimeSpan AnnouncedLapWindow = TimeSpan.FromSeconds(20);

        /// <summary>Wurde genau diese Rundenzeit gerade als eigene Bestzeit angesagt (oder steht sie zur Ansage an)?</summary>
        internal bool AnnouncedOwnLap(float seconds, DateTime now) =>
            Good(announcedLapSeconds) && Good(seconds) && Math.Abs(announcedLapSeconds - seconds) < .02f &&
            now >= announcedLapUtc && now - announcedLapUtc < AnnouncedLapWindow;
        internal static string TimedLapStartPhrase => RadioLanguage.T("Zeit läuft. Die erste gezeitete Runde beginnt, bleib bei deinem Rhythmus.",
            "Clock's running. First timed lap starts now, keep your rhythm.",
            "Corre el tiempo. Empieza la primera vuelta cronometrada, mantén tu ritmo.");
        internal static string TimedLapApproachPhrase => RadioLanguage.T("Gleich beginnt deine erste schnelle Runde. Konzentrier dich auf den Ausgang.",
            "Your first flying lap is coming up. Focus on the exit.",
            "Ya llega tu primera vuelta rápida. Concéntrate en la salida de curva.");
        private bool firstTimedLapCalled, lastQualifyingLapOffered, firstFuelOffered, fuelBaselineReported;
        // Rob 17.11.: Im Quali/Training jede neue schnelle Runde nach einer Ausfahrt kurz anschieben und ab und zu sagen,
        // was auf die Bestzeit fehlt – leise, nicht zu oft.
        private int timedLapAttempts, lastGapLap = -100, gapVariant, startVariant;
        private DateTime lastGapUtc = DateTime.MinValue;
        private bool wasInPit, pendingQualiTarget;

        /// <summary>Ansage zum Start einer schnellen Runde; <paramref name="attempt"/> 0 = erste der Session.</summary>
        internal static string TimedLapStart(int attempt, string name, int variant)
        {
            string n = string.IsNullOrWhiteSpace(name) ? "" : name;
            string[] first = RadioLanguage.Pick(
                new[] { "Zeit läuft. Die erste gezeitete Runde beginnt, bleib bei deinem Rhythmus.",
                    "So, auf geht's" + (n.Length > 0 ? ", " + n : "") + ": deine erste schnelle Runde. Sauber und mit Rhythmus.",
                    "Erste fliegende Runde läuft. Kopf runter, Kurve für Kurve.",
                    "Jetzt zählt's – die erste gezeitete Runde. Bau sie sauber auf.",
                    "Uhr läuft" + (n.Length > 0 ? ", " + n : "") + ". Erste Runde: lieber sauber als am Limit vorbei.",
                    "Los geht's mit der ersten Zeit. Reifen sind vorbereitet, jetzt zeig, was geht." },
                new[] { "Clock's running. First timed lap starts now, keep your rhythm.",
                    "Right, here we go" + (n.Length > 0 ? ", " + n : "") + ": your first flying lap. Clean and in rhythm.",
                    "First flying lap is live. Head down, corner by corner.",
                    "This is it – first timed lap. Build it up cleanly.",
                    "Clock's running" + (n.Length > 0 ? ", " + n : "") + ". First lap: clean beats over the limit.",
                    "Off we go for the first time. Tyres are ready, show us what you've got." },
                new[] { "Corre el tiempo. Empieza la primera vuelta cronometrada, mantén tu ritmo.",
                    "Vamos allá" + (n.Length > 0 ? ", " + n : "") + ": tu primera vuelta rápida. Limpia y con ritmo.",
                    "Primera vuelta lanzada en marcha. Concentración, curva a curva.",
                    "Ahora cuenta: primera vuelta cronometrada. Constrúyela limpia.",
                    "Corre el reloj" + (n.Length > 0 ? ", " + n : "") + ". Primera vuelta: mejor limpia que pasarse.",
                    "A por el primer tiempo. Los neumáticos están listos, enséñanos lo que hay." });
            string[] again = RadioLanguage.Pick(
                new[] { "Neuer Versuch, die Runde zählt. Hol dir die Zehntel.", "Nächste schnelle Runde läuft. Du weißt jetzt, wo die Zeit liegt.",
                    "Auf geht's, nächster Anlauf. Sauber rein, sauber raus.", "Runde läuft – jetzt die Bestzeit knacken.",
                    "Noch ein Versuch" + (n.Length > 0 ? ", " + n : "") + ". Ruhig bleiben, dann kommt die Zeit.", "Zeit läuft wieder. Konzentriert, Kurve für Kurve." },
                new[] { "New attempt, this one counts. Go find those tenths.", "Next flying lap is on. You know where the time is now.",
                    "Here we go, next run. Clean in, clean out.", "Lap's running – time to beat that best.",
                    "One more go" + (n.Length > 0 ? ", " + n : "") + ". Stay calm and the time will come.", "Clock's running again. Focused, corner by corner." },
                new[] { "Nuevo intento, esta cuenta. Ve a por esas décimas.", "Siguiente vuelta rápida en marcha. Ya sabes dónde está el tiempo.",
                    "Vamos, siguiente intento. Limpio al entrar, limpio al salir.", "Vuelta en marcha: a batir ese mejor tiempo.",
                    "Un intento más" + (n.Length > 0 ? ", " + n : "") + ". Tranquilo y el tiempo llegará.", "Vuelve a correr el reloj. Concentrado, curva a curva." });
            var pool = attempt <= 0 ? first : again;
            return pool[((variant % pool.Length) + pool.Length) % pool.Length];
        }

        /// <summary>Kurzer Zwischenstand zur Bestzeit der Klasse (Quali/Training).</summary>
        internal static string GapToBest(float gap, int position, string leader, int variant)
        {
            string g = RadioLanguage.Num(gap, gap < 1 ? "0.000" : "0.00");
            string p = position > 0 ? position.ToString(RadioLanguage.Culture) : null;
            string who = string.IsNullOrWhiteSpace(leader) ? null : leader;
            if (gap <= 0)
                return Of(variant, RadioLanguage.T("Du hältst die Bestzeit. Weiter so.", "You've got the fastest time. Keep it up.", "Tienes el mejor tiempo. Sigue así."),
                    RadioLanguage.T("Ganz vorn in der Zeitenliste. Jetzt nachlegen, wenn es geht.", "Top of the timesheet. Add to it if you can.", "Primero en la tabla. Mejóralo si puedes."),
                    RadioLanguage.T("Die Bestzeit gehört dir. Die anderen müssen jetzt kommen.", "Fastest time is yours. The others have to respond now.", "El mejor tiempo es tuyo. Ahora les toca a los demás."),
                    RadioLanguage.T("P1 steht. Sauber weiterfahren.", "P1 for now. Keep it clean.", "P1 por ahora. Sigue limpio."));
            return Of(variant,
                RadioLanguage.T((p != null ? "Stand jetzt Platz " + p + ". " : "") + "Auf die Bestzeit fehlen dir noch " + g + " Sekunden.",
                    (p != null ? "Currently P" + p + ". " : "") + "You're " + g + " seconds off the best time.",
                    (p != null ? "Ahora mismo P" + p + ". " : "") + "Te faltan " + g + " segundos para el mejor tiempo."),
                RadioLanguage.T(g + " Sekunden bis zur Spitze" + (who != null ? ", " + who + " führt" : "") + ". Das ist machbar.",
                    g + " seconds to the top" + (who != null ? ", " + who + " leads" : "") + ". That's doable.",
                    g + " segundos hasta arriba" + (who != null ? ", manda " + who : "") + ". Se puede."),
                RadioLanguage.T("Zwischenstand: " + (p != null ? "P" + p + ", " : "") + g + " hinter der Bestzeit. Da liegt noch was.",
                    "Update: " + (p != null ? "P" + p + ", " : "") + g + " behind the best. There's more in it.",
                    "Actualización: " + (p != null ? "P" + p + ", " : "") + g + " detrás del mejor. Queda margen."),
                RadioLanguage.T("Die Bestzeit ist " + g + " Sekunden weg. Stück für Stück ran.",
                    "Best time is " + g + " seconds away. Chip away at it.",
                    "El mejor tiempo está a " + g + " segundos. Poco a poco."),
                RadioLanguage.T((who != null ? who + " hat die Bestzeit, " : "Bis zur Bestzeit ") + g + (who != null ? " vor dir." : " Sekunden.") + " Ruhig weiter aufbauen.",
                    (who != null ? who + " has the best time, " + g + " ahead." : g + " seconds to the best time.") + " Keep building calmly.",
                    (who != null ? who + " tiene el mejor tiempo, " + g + " por delante." : g + " segundos al mejor tiempo.") + " Sigue construyendo con calma."));
        }

        private static string Of(int variant, params string[] options) => options[((variant % options.Length) + options.Length) % options.Length];
        private readonly Random qualiLastVariation = new Random();
        private int lastSectorCallLap = -1, lastSectorCall = -1;
        private readonly float[] completedSectorBest = { -1, -1, -1 };
        private DateTime lastSectorBestCall;
        private readonly RadioVariantCursor sectorBestVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor bestPaceVariants = new RadioVariantCursor();

        public void Reset()
        {
            pending.Clear(); session = int.MinValue; type = SessionType.Unavailable; personalBest = classBest = -1;
            lastLap = lastSectorCallLap = lastSectorCall = -1; improvements = new RadioVariantCursor(); rivalBests = new RadioVariantCursor();
            lastRadio = lastSectorBestCall = lastSectorTimeCall = announcedLapUtc = DateTime.MinValue; firstSectorTimeCalled = false; announcedLapSeconds = -1;
            firstTimedLapCalled = lastQualifyingLapOffered = firstFuelOffered = fuelBaselineReported = false;
            for (int i = 0; i < completedSectorBest.Length; i++) completedSectorBest[i] = -1;
            edgeLap = edgeSector = -1; edgeUtc = DateTime.MinValue; flyingLap = -1; hotLap = -1;
            timedLapAttempts = 0; lastGapLap = -100; lastGapUtc = DateTime.MinValue; wasInPit = false; pendingQualiTarget = false; lastAnalysisLap = -100; lastAnalysisUtc = DateTime.MinValue; lastAttemptsAnnounced = -1;
        }

        public static bool Supported(SessionType t)
        { return t == SessionType.Practice || t == SessionType.LonePractice || t == SessionType.Qualify || t == SessionType.PrivateQualify || t == SessionType.HotLap; }

        public void Observe(GameStateData gs, LapRecord completed, DateTime now, bool names, GameStateData previous = null,
            bool sectorRadio = true, TrackAwareness tracks = null, string driverName = "Rob", AiRaceMemory fuelHistory = null)
        {
            if (gs == null) return;
            sectorDriverName = AiPersonas.CleanForRadio(driverName ?? "Rob", 30, false);
            var s = gs.SessionData;
            bool race = s.SessionType == SessionType.Race;
            if (!race && !Supported(s.SessionType)) { Reset(); return; }
            if (session != s.SessionId || type != s.SessionType)
            {
                Reset(); session = s.SessionId; type = s.SessionType;
                // Joining an existing session does not turn its historic best into a new event.
                personalBest = s.PlayerLapTimeSessionBest;
                classBest = FieldBest(gs);
                return;
            }
            bool green = s.SessionPhase == SessionPhase.Green;
            // Neue Ausfahrt im Quali/Training: die nächste schnelle Runde bekommt wieder eine Ansage.
            if (!race && gs.PitData.InPitlane && !wasInPit && firstTimedLapCalled) { firstTimedLapCalled = false; timedLapAttempts++; }
            wasInPit = gs.PitData.InPitlane;
            if (!green && (race || s.SessionPhase != SessionPhase.Checkered && s.SessionPhase != SessionPhase.Finished))
            { pending.Clear(); return; }
            bool ownCall = false;
            // Some servers publish historical timing a few frames after joining. Seed it quietly
            // until the first locally completed lap; never seed from that lap's newly updated best.
            if(completed==null && lastLap<0 && Good(s.PlayerLapTimeSessionBest))
                personalBest=Good(personalBest)?Math.Min(personalBest,s.PlayerLapTimeSessionBest):s.PlayerLapTimeSessionBest;
            var length = s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength;
            float step = previous == null ? float.NaN : s.SessionRunningTime - previous.SessionData.SessionRunningTime;
            float distance = gs.PositionAndMotionData.DistanceRoundTrack;
            float speed = gs.PositionAndMotionData.CarSpeed;
            if (QualifyingResultWatch.Supported(type) && green && !firstTimedLapCalled && previous != null &&
                previous.SessionData.SessionId == s.SessionId && previous.SessionData.SessionType == type &&
                s.CurrentLapIsValid && gs.PitData.OnOutLap && !gs.PitData.InPitlane && length > 100 &&
                distance > length * .8f && distance < length && speed > 15 && speed < 120 &&
                (length - distance) / speed >= 3 && (length - distance) / speed <= 7 &&
                step > 0 && step <= 1.5f && distance >= previous.PositionAndMotionData.DistanceRoundTrack &&
                distance - previous.PositionAndMotionData.DistanceRoundTrack <= 180 * step + 15)
            {
                firstTimedLapCalled = true;
                pendingQualiTarget = true;
                Add(gs, now, "timed-lap-approach", timedLapAttempts == 0 ? TimedLapApproachPhrase : TimedLapStart(timedLapAttempts, sectorDriverName, startVariant++),
                    "confirmed_qualifying_outlap_approaching_line", 4);
            }
            bool continuous = !float.IsNaN(step) && !float.IsInfinity(step) && step > 0 && step <= 1.5f && previous != null &&
                length - previous.PositionAndMotionData.DistanceRoundTrack + gs.PositionAndMotionData.DistanceRoundTrack <= 180 * step + 15;
            if (!race && green && !firstTimedLapCalled && continuous && previous != null && previous.SessionData.SessionId == s.SessionId &&
                previous.SessionData.SessionType == s.SessionType && previous.PitData.OnOutLap && !gs.PitData.OnOutLap &&
                !gs.PitData.InPitlane && s.IsNewLap && gs.SessionData.CurrentLapIsValid && length > 100 &&
                previous.PositionAndMotionData.DistanceRoundTrack > length * .8f &&
                gs.PositionAndMotionData.DistanceRoundTrack >= 0 && gs.PositionAndMotionData.DistanceRoundTrack < length * .2f)
            {
                firstTimedLapCalled = true;
                Add(gs, now, "timed-lap-start", TimedLapStart(timedLapAttempts, sectorDriverName, startVariant++),
                    "mapped_outlap_end_new_lap_and_start_finish_crossing", 3);
                OfferQualiTarget(gs, now, names);
                pendingQualiTarget = false;
            }
            // Nach der Ansage vor der Linie kommt die Zielzeit, sobald die schnelle Runde wirklich läuft.
            if (pendingQualiTarget && !race && green && s.IsNewLap && !gs.PitData.OnOutLap && !gs.PitData.InPitlane && s.CurrentLapIsValid)
            {
                pendingQualiTarget = false;
                OfferQualiTarget(gs, now, names);
            }
            if (gs.PitData.InPitlane) pendingQualiTarget = false;
            // Zeitqualifying: Wie viele schnelle Runden passen nach dieser noch in die Restzeit?
            int moreLaps = QualifyingResultWatch.Supported(type) && green && s.IsNewLap && !gs.PitData.InPitlane && s.CurrentLapIsValid
                ? AttemptsAfterThisLap(s.SessionHasFixedTime ? s.SessionTimeRemaining : -1, Good(s.PlayerLapTimeSessionBest) ? s.PlayerLapTimeSessionBest : s.LapTimePrevious) : -1;
            bool finalQualifyingLap = QualifyingResultWatch.Supported(type) && previous?.SessionData.SessionId == session &&
                previous.SessionData.SessionPhase == SessionPhase.Green && (s.SessionPhase == SessionPhase.Checkered ||
                green && s.IsNewLap && s.SessionLapsRemaining == 1 && !s.SessionHasFixedTime || moreLaps == 0 && s.CompletedLaps > 0);
            if ((moreLaps == 1 || moreLaps == 2) && moreLaps != lastAttemptsAnnounced && !gs.PitData.OnOutLap && !pending.Any(c => c.Topic == "quali-target"))
            {
                lastAttemptsAnnounced = moreLaps;
                var attempts = Add(gs, now, "quali-attempts", AttemptsText(moreLaps, attemptsVariant++), "session_time_remaining_vs_own_lap_time_" + moreLaps + "_more", 3);
                attempts.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && !live.PitData.InPitlane && !live.FlagData.isLocalYellow;
            }
            if (!lastQualifyingLapOffered && finalQualifyingLap && !gs.PitData.InPitlane && !gs.PitData.OnOutLap &&
                s.CurrentLapIsValid && gs.PositionAndMotionData.CarSpeed > 12 && s.CompletedLaps > 0)
            {
                lastQualifyingLapOffered = true;
                string name = AiPersonas.CleanForRadio(driverName ?? "Rob", 30, false);
                string[] push = RadioLanguage.Pick(
                    new[] { "Letzte schnelle Runde. Hol raus, was geht.", "Letzte Chance. Sauber durchziehen.", "Eine Runde noch, volle Konzentration.",
                        "Die Uhr läuft ab, das ist dein letzter Versuch. Alles rein.", "Letzte Runde im Qualifying. Bremspunkte treffen, Ausgänge sauber." },
                    new[] { "Last flying lap. Get everything out of it.", "Last chance. Make it clean.", "One more lap, full focus.",
                        "The clock's run down, this is your last attempt. Everything in.", "Final lap of qualifying. Hit the braking points, clean exits." },
                    new[] { "Última vuelta rápida. Sácale todo.", "Última oportunidad. Hazla limpia.", "Una vuelta más, concentración total.",
                        "Se acaba el tiempo, es tu último intento. Dalo todo.", "Última vuelta de la clasificación. Clava las frenadas, salidas limpias." });
                var last = Add(gs, now, "qualifying-last", RadioPhraseCatalog.Address(push[qualiLastVariation.Next(push.Length)], name),
                    "confirmed_final_qualifying_lap", 5);
                last.Member = TeamMember.Ben;
                last.AdditionalValidity = live => !live.PitData.OnOutLap && live.SessionData.CurrentLapIsValid &&
                    !live.FlagData.isLocalYellow && live.PositionAndMotionData.CarSpeed > 5;
            }
            if (completed != null && completed.LapNumber != lastLap)
            {
                lastLap = completed.LapNumber;
                if (!firstFuelOffered && green && !completed.InPitLap && !gs.PitData.InPitlane &&
                    Good(completed.LapTime) && TrackAwareness.InRange(completed.FuelUsed, .05, 50).HasValue)
                {
                    firstFuelOffered = true;
                    string used = FuelWatch.Litres(completed.FuelUsed);
                    Add(gs, now, "first-lap-fuel", RadioVariants.Pick("first-lap-fuel",
                        RadioLanguage.T("Erste volle Runde, " + used + " verbraucht.", "First full lap, " + used + " used.", "Primera vuelta completa, " + used + " gastados."),
                        RadioLanguage.T("Erste Messrunde: " + used + " Sprit.", "First measured lap: " + used + " of fuel.", "Primera vuelta medida: " + used + " de combustible."),
                        RadioLanguage.T("Verbrauch der ersten vollen Runde: " + used + ".", "Fuel on the first full lap: " + used + ".", "Consumo de la primera vuelta completa: " + used + "."),
                        RadioLanguage.T("Die erste ganze Runde hat " + used + " gekostet.", "The first full lap took " + used + ".", "La primera vuelta entera se ha llevado " + used + ".")),
                        "measured_first_full_lap_fuel", 4);
                }
                if (race && green && !fuelBaselineReported && !completed.InPitLap && !gs.PitData.InPitlane &&
                    !pending.Any(call => call.Topic == "fuel-baseline" && call.IsValid(gs, now)) && fuelHistory != null)
                {
                    float fuelPerLap = fuelHistory.AverageFuelPerLap(out int samples, 3);
                    if (samples == 3 && TrackAwareness.InRange(fuelPerLap, .05, 50).HasValue)
                    {
                        string consumption = FuelWatch.Litres(fuelPerLap);
                        var call = Add(gs, now, "fuel-baseline",
                            RadioVariants.Pick("fuel-baseline",
                                RadioLanguage.T("Über drei volle Runden liegst du bei " + consumption + " pro Runde.",
                                    "Over three full laps you're using " + consumption + " per lap.",
                                    "En tres vueltas completas, el consumo es de " + consumption + " por vuelta."),
                                RadioLanguage.T("Verbrauch steht: " + consumption + " pro Runde, gemessen über drei Runden.",
                                    "Consumption is set: " + consumption + " per lap, measured over three laps.",
                                    "Consumo fijado: " + consumption + " por vuelta, medido en tres vueltas."),
                                RadioLanguage.T("Drei saubere Runden, im Schnitt " + consumption + " pro Runde.",
                                    "Three clean laps, averaging " + consumption + " per lap.",
                                    "Tres vueltas limpias, de media " + consumption + " por vuelta."),
                                RadioLanguage.T("Gemessener Schnitt nach drei Runden: " + consumption + " je Runde.",
                                    "Measured average after three laps: " + consumption + " a lap.",
                                    "Media medida tras tres vueltas: " + consumption + " por vuelta.")),
                            "three_measured_full_laps", 4);
                        call.AdditionalValidity = live => live.FuelData.FuelUseActive && !live.FlagData.isLocalYellow;
                    }
                }
                if (completed.Valid && !completed.InPitLap && Good(completed.LapTime))
                {
                    float time = completed.LapTime;
                    bool first = !Good(personalBest), improved = !first && time < personalBest - .01f;
                    if ((!race && first) || improved)
                    {
                        bool classRecord = Good(s.PlayerClassSessionBestLapTime) && Math.Abs(time - s.PlayerClassSessionBestLapTime) < .01f &&
                            (!Good(classBest) || time < classBest - .01f);
                        string formatted = LocalAnswers.LapTime(time);
                        string group = first ? "lap-first-valid" : "lap-best";
                        var variants = first ? firstValidVariants : improvements;
                        int heard = SelectVariant(group, variants);
                        string text = RadioPhraseCatalog.Current.Format(group, heard, formatted) +
                            (classRecord ? RadioVariants.Pick("class-best-suffix",
                                RadioLanguage.T(" Klassenbestzeit an der Linie.", " That's the class best!", " ¡Es el mejor tiempo de la clase!"),
                                RadioLanguage.T(" Damit hast du die Klassenbestzeit!", " That's the fastest in class!", " ¡Con eso tienes el mejor tiempo de la clase!"),
                                RadioLanguage.T(" Schnellster in der Klasse!", " Quickest in class!", " ¡El más rápido de la clase!"),
                                RadioLanguage.T(" Ganz oben in der Klasse.", " Top of the class.", " Primero de la clase.")) :
                                first ? FirstLapComparison(gs, time, names, firstComparisonVariant++) : "");
                        // The recorded lap remains true when standings or another driver's best changes.
                        // Fresh classification and gaps belong to the separate timing reports.
                        Add(gs, now, "own-lap", text, first ? "first_valid_lap" : "personal_best_improved", 5,
                            variants, group, heard);
                        ownCall = true;
                        announcedLapSeconds = time; announcedLapUtc = now;
                    }
                    else if (!race && Good(personalBest) && time > personalBest + .01f && time - personalBest <= .30f)
                    {
                        string delta = RadioLanguage.Num(time - personalBest, "0.00");
                        int variant = SelectVariant("lap-near-best", nearBestVariants);
                        Add(gs, now, "near-best", RadioPhraseCatalog.Current.Format("lap-near-best", variant, delta),
                            "valid_lap_within_0.30s_of_recorded_personal_best", 3, nearBestVariants, "lap-near-best", variant);
                        ownCall = true;
                    }
                    personalBest = Good(personalBest) ? Math.Min(personalBest, time) : time;
                }
            }
            // Schnellste Runde im Feld (bei mehreren Klassen: in Robs Klasse), in jeder Session.
            // Die erste Zeit einer Session ist kein Ereignis, erst jede Verbesserung danach.
            float nextBest = FieldBest(gs);
            if (!race && green && Good(nextBest) && Good(classBest) && nextBest < classBest - .01f && !ownCall)
            {
                // Nur eine gerade gefahrene Runde; nachgeladene alte Bestzeiten (Session-Beitritt) bleiben still.
                var rival = gs.OpponentData.Values.FirstOrDefault(o => SameClass(gs, o) && Good(o.CurrentBestLapTime) && Math.Abs(o.CurrentBestLapTime - nextBest) < .01f &&
                    (!Good(o.LastLapTime) || Math.Abs(o.LastLapTime - nextBest) < .01f));
                if (rival != null)
                {
                    string name = names ? new string((rival.DriverRawName ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').Take(50).ToArray()).Trim() : "";
                    float mine = Good(personalBest) && Good(s.PlayerLapTimeSessionBest) ? Math.Min(personalBest, s.PlayerLapTimeSessionBest) :
                        Good(personalBest) ? personalBest : s.PlayerLapTimeSessionBest;
                    Add(gs, now, "rival-best", FastestLapText(name, nextBest, mine, RaceRadioLanguage.SeparateClass(s), rivalBests.Heard),
                        "measured_field_best_and_identified_rival", 2, rivalBests);
                }
            }
            if (Good(nextBest)) classBest = nextBest;
            // Zwischenstand zur Bestzeit: nach einer sauberen Runde ohne eigene Ansage, höchstens alle drei Runden und 150 s.
            if (!race && green && !ownCall && completed != null && completed.Valid && !completed.InPitLap && Good(classBest) &&
                Good(s.PlayerLapTimeSessionBest) && s.CompletedLaps - lastGapLap >= 3 && now - lastGapUtc >= TimeSpan.FromSeconds(150) &&
                !pending.Any(c => c.IsValid(gs, now)))
            {
                lastGapLap = s.CompletedLaps; lastGapUtc = now;
                float gap = s.PlayerLapTimeSessionBest - classBest;
                var leader = gs.OpponentData.Values.FirstOrDefault(o => SameClass(gs, o) && Good(o.CurrentBestLapTime) && Math.Abs(o.CurrentBestLapTime - classBest) < .01f);
                string leaderName = names && leader != null ? new string((leader.DriverRawName ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').Take(50).ToArray()).Trim() : null;
                Add(gs, now, "quali-gap", GapToBest(Math.Max(0, gap), s.ClassPosition, leaderName, gapVariant++), "own_session_best_vs_class_best_after_clean_lap", 2);
            }
            // Wo liegt die Zeit? Nach jeder sauberen Quali-Runde (höchstens jede Minute), außer der Zwischenstand kam gerade.
            if (QualifyingResultWatch.Supported(type) && green && !s.SectorTimingEstimated && completed?.SectorTimes?.Length == 3 && completed.Valid &&
                !completed.InPitLap && Good(completed.LapTime) && lastGapLap != s.CompletedLaps && s.CompletedLaps != lastAnalysisLap &&
                now - lastAnalysisUtc >= TimeSpan.FromSeconds(60))
            {
                string analysis = LapAnalysis(completed.SectorTimes, FieldBestSectors(gs), analysisVariant++);
                if (analysis != null)
                {
                    lastAnalysisLap = s.CompletedLaps; lastAnalysisUtc = now;
                    var call = Add(gs, now, "quali-lap-analysis", analysis, "valid_lap_sectors_vs_class_opponent_best_lap_sectors", 2);
                    call.AdditionalValidity = live => !live.PitData.InPitlane && !live.FlagData.isLocalYellow;
                }
            }
            if (!s.SectorTimingEstimated && completed?.SectorTimes?.Length == 3 && completed.Valid && !completed.InPitLap && Good(completed.LapTime) &&
                completed.LapNumber == s.CompletedLaps && s.IsNewLap &&
                Math.Abs(completed.SectorTimes.Sum() - completed.LapTime) <= 1)
                ObserveCompletedSectorBest(gs, completed, now, green && sectorRadio && !ownCall);
            if (sectorRadio && green && !ownCall && !s.SectorTimingEstimated)
            {
                bool live = TryLiveSector(gs, previous, now, out var lap, out int sector);
                if (live && (ObserveFlyingLap(gs, previous, lap, sector, now) || ObserveLivePersonalBest(gs, previous, lap, sector, now) ||
                    ObservePersonalBestPace(gs, lap, sector, now))) return;
                if (QualifyingResultWatch.Supported(type))
                    ObserveQualifyingSector(gs, previous, now, names, tracks);
                // Der Vergleich mit dem Führenden hat Vorrang; die eigene Sektorzeit nur, wenn sonst nichts ansteht.
                if (live && !pending.Any(c => c.Topic == "quali-sector" && c.DeliveryStartedUtc == DateTime.MinValue))
                    ObserveInterestingSector(gs, lap, sector, now);
            }
        }

        // Rob 17.11.: Die lila Runde in AMS2 blieb stumm. Bisher zählte nur genau der eine Takt, in dem die Sektornummer
        // wechselte – war dort irgendetwas noch nicht frisch (gleicher Datensatz, Sektorzeit noch nicht eingetragen), war
        // die Gelegenheit für die ganze Runde verloren, und niemand konnte sehen, warum. Jetzt merkt sich der Beobachter den
        // Sektorwechsel und versucht es bei vorübergehenden Gründen bis zu zwei Sekunden lang weiter; jeder Grund, aus dem
        // ein Sektor nicht ausgewertet wird, landet in der Live-Diagnose („sector-live“).
        private int edgeLap = -1, edgeSector = -1;
        private DateTime edgeUtc = DateTime.MinValue;
        internal static readonly TimeSpan SectorEdgeRetry = TimeSpan.FromSeconds(2);

        /// <summary>Vorübergehende Gründe: kann sich in den nächsten Takten von selbst erledigen.</summary>
        internal static bool RetryableSectorReason(string reason) =>
            reason == "stale_source" || reason == "duplicate_sample" || reason == "sector_time_missing";

        /// <summary>Erster Grund, der eine Live-Auswertung des gerade beendeten Sektors verhindert; null = auswertbar.</summary>
        internal static string LiveSectorBlocker(GameStateData state, GameStateData previous, DateTime now, int sector, out LapData lap)
        {
            var s = state.SessionData;
            var before = previous.SessionData;
            lap = null;
            if (before.SessionId != s.SessionId || before.SessionType != s.SessionType) return "session_changed";
            if (s.SessionPhase != SessionPhase.Green) return "not_green";
            if (s.IsNewLap) return "new_lap";
            if (s.SectorTimingEstimated || before.SectorTimingEstimated) return "sector_timing_estimated";
            if (!s.CurrentLapIsValid) return "lap_invalid";
            if (state.PitData.InPitlane || state.PitData.OnOutLap || state.PitData.OnInLap) return "pit_or_out_lap";
            if (state.FlagData.isLocalYellow) return "local_yellow";
            DateTime source = state.SourceTelemetryUtc == DateTime.MinValue ? state.Now : state.SourceTelemetryUtc;
            if (now < source || now - source > TimeSpan.FromSeconds(1)) return "stale_source";
            if (previous.SourceTelemetryUtc != DateTime.MinValue && source <= previous.SourceTelemetryUtc) return "duplicate_sample";
            lap = s.PlayerLapData.LastOrDefault();
            if (lap == null || lap.LapNumber != s.CompletedLaps + 1) return "lap_record_missing";
            if (!lap.IsValid || lap.InLap || lap.OutLap) return "lap_record_not_timed";
            if (lap.SectorTimes == null || lap.SectorTimes.Length <= sector || !GoodSector(lap.SectorTimes[sector])) return "sector_time_missing";
            return null;
        }

        private bool TryLiveSector(GameStateData state, GameStateData previous, DateTime now, out LapData lap, out int sector)
        {
            var s = state.SessionData;
            var before = previous?.SessionData;
            sector = s.SectorNumber - 2;
            lap = null;
            if (before == null || sector < 0 || sector > 1) return false;
            bool edge = before.SectorNumber + 1 == s.SectorNumber && before.CompletedLaps == s.CompletedLaps;
            if (edge) { edgeLap = s.CompletedLaps + 1; edgeSector = sector; edgeUtc = now; }
            else if (edgeLap != s.CompletedLaps + 1 || edgeSector != sector || now - edgeUtc > SectorEdgeRetry) return false;
            string reason = LiveSectorBlocker(state, previous, now, sector, out lap);
            if (reason == null) { edgeLap = -1; return true; }
            bool retry = RetryableSectorReason(reason) && now - edgeUtc < SectorEdgeRetry;
            if (!retry) edgeLap = -1;
            if (edge || !retry)
                LiveDiagnostics.Decision("sector-live", retry ? "waiting" : "rejected", "sector_" + (sector + 1) + ":" + reason);
            lap = null;
            return false;
        }

        private bool ObserveLivePersonalBest(GameStateData state, GameStateData previous, LapData lap, int sector, DateTime now)
        {
            string key = "timing.sector.personal." + (sector + 1);
            float time = lap.SectorTimes[sector];
            float prior = completedSectorBest[sector];
            if (GoodSector(prior) && prior - time < .04f) return false;
            string evidence;
            if (state.Measurements.TryGetValue(key, out var value) && previous.Measurements.TryGetValue(key, out var old) &&
                old.Value.HasValue && value.Value.HasValue)
            {
                if (value.StatusAt(now) != "gueltig" || old.StatusAt(now) != "gueltig" || value.SampleUtc <= old.SampleUtc ||
                    Math.Abs(value.Value.Value - time) > .035 || old.Value.Value - value.Value.Value < .04) return false;
                evidence = "native_personal_sector_best_and_completed_current_split";
            }
            else
            {
                float previousBest = sector == 0 ? previous.SessionData.PlayerBestSector1Time : previous.SessionData.PlayerBestSector2Time;
                float reference = GoodSector(prior) && GoodSector(previousBest) ? Math.Min(prior, previousBest) :
                    GoodSector(prior) ? prior : previousBest;
                if (!GoodSector(reference))
                { LiveDiagnostics.Decision("sector-best", "skipped", "sector_" + (sector + 1) + ":no_prior_personal_best"); return false; }
                if (reference - time < .04f)
                { LiveDiagnostics.Decision("sector-best", "skipped", "sector_" + (sector + 1) + ":not_faster_than_best_by_0.04s"); return false; }
                evidence = "mapped_valid_sector_and_prior_personal_best";
            }
            completedSectorBest[sector] = prior > 0 ? Math.Min(prior, time) : time;
            if (now - lastSectorBestCall < TimeSpan.FromSeconds(55))
            { LiveDiagnostics.Decision("sector-best", "skipped", "sector_" + (sector + 1) + ":cooldown_55s"); return false; }
            var call = OfferSectorBest(state, now, sector, time, evidence, true);
            int currentSector = state.SessionData.SectorNumber;
            call.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && !live.SessionData.SectorTimingEstimated &&
                live.SessionData.SectorNumber == currentSector &&
                !live.PitData.OnInLap && !live.PitData.OnOutLap && !live.FlagData.isLocalYellow;
            return true;
        }

        // Rob 16.11. (AMS2 live): Sektor 1 und 2 lila, keine Reaktion. Ursache: Nach der Meldung zu Sektor 1 sperrte die
        // 55-Sekunden-Pause jede weitere Sektormeldung; ohne frühere eigene Bestzeit (erste schnelle Runde) gab es gar nichts.
        // Jetzt merkt sich Mik Sektor 1 der laufenden Runde und meldet nach Sektor 2 beide zusammen – eigene Bestzeit oder
        // lila (schneller als alle Bestzeiten der Klasse) – mit „das könnte deine schnellste Runde werden“.
        private int flyingLap = -1, flyingCount, hotLap = -1;
        internal const float HotLapMargin = .25f;

        /// <summary>
        /// Läuft gerade eine schnelle Runde außerhalb des Rennens (Sektor 1 oder 2 auf Bestzeitkurs, Runde gültig, nicht in der
        /// Box)? Dann hält das KI-Team Routine- und Hintergrundmeldungen zurück – ein Renningenieur redet nicht in eine
        /// Pushrunde hinein. Die Meldung zu den Bestsektoren selbst und alle Sicherheitsmeldungen bleiben.
        /// </summary>
        internal bool HotLap(GameStateData gs)
        {
            var s = gs?.SessionData;
            return s != null && hotLap > 0 && hotLap == s.CompletedLaps + 1 && s.SessionType != SessionType.Race &&
                s.CurrentLapIsValid && !gs.PitData.InPitlane && s.SessionPhase == SessionPhase.Green;
        }

        /// <summary>Nur für Tests: markiert die laufende Runde als schnelle Runde.</summary>
        internal void MarkHotLapForTest(int lapNumber) { hotLap = lapNumber; }
        private bool flyingS1Own, flyingS1Purple;
        private readonly Random flyingRandom = new Random();
        private int flyingLastVariant = -1;

        private static float FieldBestSector(GameStateData gs, int sector)
        {
            float best = -1;
            foreach (var o in gs.OpponentData.Values)
            {
                if (o == null || !SameClass(gs, o)) continue;
                float t = sector == 0 ? o.bestSector1Time : sector == 1 ? o.bestSector2Time : o.bestSector3Time;
                if (GoodSector(t) && (best < 0 || t < best)) best = t;
            }
            return best;
        }

        /// <summary>Eigene Bestzeit (mit früherer Bestmarke) und lila (schneller als jede Bestzeit der Klasse) für einen gerade beendeten Sektor.</summary>
        internal static void RateSector(float time, float ownPrior, float fieldBest, out bool own, out bool purple)
        {
            own = GoodSector(ownPrior) && ownPrior - time >= .01f;
            purple = GoodSector(time) && GoodSector(fieldBest) && fieldBest - time >= .001f && (!GoodSector(ownPrior) || ownPrior - time >= .001f);
        }

        private bool ObserveFlyingLap(GameStateData state, GameStateData previous, LapData lap, int sector, DateTime now)
        {
            if (sector > 1) return false;
            float time = lap.SectorTimes[sector];
            float prior = sector == 0 ? previous.SessionData.PlayerBestSector1Time : previous.SessionData.PlayerBestSector2Time;
            float kept = completedSectorBest[sector];
            float own = GoodSector(prior) && GoodSector(kept) ? Math.Min(prior, kept) : GoodSector(prior) ? prior : kept;
            RateSector(time, own, FieldBestSector(state, sector), out bool ownBest, out bool purple);
            // Schnelle Runde (Rob 17.11.): Liegt der Sektor auf Bestzeitkurs oder höchstens 0,25 s darüber, ist die Runde
            // „heiß“ – dann schweigen Routine und Hintergrundmeldungen bis zum Rundenende.
            bool onPace = ownBest || purple || GoodSector(own) && GoodSector(time) && time - own <= HotLapMargin;
            if (onPace) hotLap = lap.LapNumber; else if (hotLap == lap.LapNumber) hotLap = -1;
            if (sector == 0)
            {
                flyingLap = lap.LapNumber; flyingS1Own = ownBest; flyingS1Purple = purple;
                return false; // Sektor 1 läuft wie bisher; die Sammelmeldung kommt nach Sektor 2
            }
            bool s1 = flyingLap == lap.LapNumber && (flyingS1Own || flyingS1Purple);
            if (!s1 || !(ownBest || purple))
                LiveDiagnostics.Decision("lap-on-pace", "skipped", "sector_1_best:" + s1 + ";sector_2_own:" + ownBest + ";sector_2_purple:" + purple);
            bool bothPurple = flyingS1Purple && purple;
            flyingLap = -1;
            if (!s1 || !(ownBest || purple)) return false;
            if (GoodSector(time)) completedSectorBest[sector] = GoodSector(kept) ? Math.Min(kept, time) : time;
            // Die noch nicht gesprochene Einzelmeldung zu Sektor 1 wird ersetzt; war sie schon zu hören, heißt es „auch Sektor 2“.
            bool s1Spoken = pending.All(c => c.Topic != "sector-best" || c.DeliveryStartedUtc != DateTime.MinValue) &&
                now - lastSectorBestCall < TimeSpan.FromSeconds(45);
            pending.RemoveAll(c => c.Topic == "sector-best" && c.DeliveryStartedUtc == DateTime.MinValue);
            double delta = PersonalBestSplitDelta(state, lap);
            int variant = flyingRandom.Next(6);
            if (variant == flyingLastVariant) variant = (variant + 1 + flyingRandom.Next(5)) % 6;
            flyingLastVariant = variant;
            string name = flyingCount++ % 2 == 0 ? sectorDriverName : null;
            string text = FlyingLapText(name, bothPurple, s1Spoken, delta, variant);
            var call = Add(state, now, "lap-on-pace", text, bothPurple ? "two_live_sectors_faster_than_class_best" : "two_live_sectors_personal_best", 4);
            call.Member = TeamMember.Mik;
            call.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && !live.SessionData.SectorTimingEstimated && live.SessionData.SectorNumber == 3 &&
                !live.PitData.OnInLap && !live.FlagData.isLocalYellow;
            return true;
        }

        /// <summary>Vorsprung der ersten beiden Sektoren auf die Splits der eigenen Bestzeitrunde; -1 wenn nicht belegt.</summary>
        private double PersonalBestSplitDelta(GameStateData state, LapData lap)
        {
            var s = state.SessionData;
            if (!Good(personalBest) || !GoodSector(lap.SectorTimes[0]) || !GoodSector(lap.SectorTimes[1])) return -1;
            var best = s.PlayerLapData.LastOrDefault(candidate => candidate != lap && candidate.IsValid && !candidate.InLap && !candidate.OutLap &&
                Math.Abs(candidate.LapTime - personalBest) < .01f && candidate.SectorTimes?.Length == 3 &&
                candidate.SectorTimes.All(GoodSector) && Math.Abs(candidate.SectorTimes.Sum() - candidate.LapTime) <= .10f);
            if (best == null) return -1;
            double d = best.SectorTimes[0] + best.SectorTimes[1] - lap.SectorTimes[0] - lap.SectorTimes[1];
            return d >= .05 && d <= 5 ? d : -1;
        }

        /// <summary>Sammelmeldung nach zwei Bestzeit-Sektoren – sechs Formulierungen, lila oder persönlich, mit Vorsprung, wenn belegt.</summary>
        internal static string FlyingLapText(string name, bool purple, bool sectorOneSpoken, double delta, int variant)
        {
            string n = string.IsNullOrWhiteSpace(name) ? null : name;
            int v = ((variant % 6) + 6) % 6;
            string text;
            if (sectorOneSpoken)
                text = v % 2 == 0
                    ? RadioLanguage.T("Und Sektor 2 ist auch Bestzeit! Das könnte deine schnellste Runde werden" + (n != null ? ", " + n : "") + ".",
                        "And sector 2 is a best as well! This could be your fastest lap" + (n != null ? ", " + n : "") + ".",
                        "¡Y el sector 2 también es récord! Esta puede ser tu vuelta más rápida" + (n != null ? ", " + n : "") + ".")
                    : RadioLanguage.T((purple ? "Wieder lila! " : "Der nächste Bestsektor! ") + "Jetzt den letzten Sektor sauber durchziehen" + (n != null ? ", " + n : "") + ".",
                        (purple ? "Purple again! " : "Another best sector! ") + "Now bring the last sector home cleanly" + (n != null ? ", " + n : "") + ".",
                        (purple ? "¡Otra vez morado! " : "¡Otro mejor sector! ") + "Ahora cierra limpio el último sector" + (n != null ? ", " + n : "") + ".");
            else if (purple)
                text = v % 3 == 0
                    ? RadioLanguage.T((n != null ? n + ", l" : "L") + "ila in Sektor 1 und 2, so schnell war heute noch keiner. Das könnte deine schnellste Runde werden!",
                        (n != null ? n + ", p" : "P") + "urple in sectors 1 and 2, nobody has been that quick today. This could be your fastest lap!",
                        (n != null ? n + ", m" : "M") + "orado en los sectores 1 y 2, nadie ha ido tan rápido hoy. ¡Esta puede ser tu vuelta más rápida!")
                    : v % 3 == 1
                    ? RadioLanguage.T("Zwei lila Sektoren! Jetzt nur noch sauber durch den letzten" + (n != null ? ", " + n : "") + ".",
                        "Two purple sectors! Now just a clean final sector" + (n != null ? ", " + n : "") + ".",
                        "¡Dos sectores morados! Ahora solo un último sector limpio" + (n != null ? ", " + n : "") + ".")
                    : RadioLanguage.T("Sektor 1 und 2 sind die schnellsten im ganzen Feld. Bring das nach Hause" + (n != null ? ", " + n : "") + "!",
                        "Sectors 1 and 2 are the fastest in the whole field. Bring it home" + (n != null ? ", " + n : "") + "!",
                        "Los sectores 1 y 2 son los más rápidos de todo el grupo. ¡Llévala a casa" + (n != null ? ", " + n : "") + "!");
            else
                text = v % 3 == 0
                    ? RadioLanguage.T((n != null ? n + ", B" : "B") + "estzeit in Sektor 1 und 2. Das könnte deine schnellste Runde werden.",
                        (n != null ? n + ", p" : "P") + "ersonal best in sectors 1 and 2. This could be your fastest lap.",
                        (n != null ? n + ", m" : "M") + "ejor marca personal en los sectores 1 y 2. Esta puede ser tu vuelta más rápida.")
                    : v % 3 == 1
                    ? RadioLanguage.T("Zwei Bestsektoren hintereinander. Die Runde sieht richtig gut aus, jetzt ruhig bleiben.",
                        "Two best sectors in a row. This lap looks really good, stay calm now.",
                        "Dos mejores sectores seguidos. La vuelta pinta muy bien, ahora tranquilo.")
                    : RadioLanguage.T("Starke Runde bis hierher, Sektor 1 und 2 so schnell wie noch nie" + (n != null ? ", " + n : "") + ". Letzter Sektor, sauber bleiben.",
                        "Strong lap so far, sectors 1 and 2 quicker than ever" + (n != null ? ", " + n : "") + ". Last sector, keep it clean.",
                        "Vuelta fuerte hasta aquí, sectores 1 y 2 más rápidos que nunca" + (n != null ? ", " + n : "") + ". Último sector, limpio.");
            if (delta > 0)
            {
                string d = GermanRadioText.Interval(delta);
                text += " " + (v % 2 == 0
                    ? RadioLanguage.T("Du liegst " + d + " unter deiner Bestzeit.", "You're " + d + " up on your best lap.", "Vas " + d + " por debajo de tu mejor vuelta.")
                    : RadioLanguage.T(d + " Vorsprung auf deine Bestzeit.", d + " up on your best.", d + " de ventaja sobre tu mejor marca."));
            }
            return text;
        }

        private ObservedRaceCall OfferSectorBest(GameStateData state, DateTime now, int sector, float seconds, string evidence, bool lapStillRunning = false)
        {
            var catalog = RadioPhraseCatalog.Current;
            int variant = SelectVariant("own-sector-best", sectorBestVariants);
            // Rob 14.11.: Zeiten mit drei Nachkommastellen.
            string time = seconds.ToString("0.000", RadioLanguage.Culture);
            string text = catalog.Format("own-sector-best", variant, (sector + 1).ToString(CultureInfo.InvariantCulture), time);
            sectorBestCount++;
            // Rob 16.11.: menschlich und persönlich – aber nicht jedes Mal, nur wenn es passt.
            if (sectorBestCount % 2 == 1 && !string.IsNullOrWhiteSpace(sectorDriverName))
                text = RadioPhraseCatalog.Address(text, sectorDriverName);
            if (lapStillRunning && SectorPushFits(state))
                text += " " + catalog.Format("own-sector-best-push", pushVariants.Select("own-sector-best-push", catalog.Count("own-sector-best-push")));
            return Add(state, now, "sector-best", text, evidence, 3, sectorBestVariants, "own-sector-best", variant);
        }

        private int sectorBestCount;
        private string sectorDriverName = "Rob";
        private readonly RadioVariantCursor pushVariants = new RadioVariantCursor();

        /// <summary>Ansporn nur, wenn die Runde noch läuft und gültig ist; im Rennen seltener und nie im engen Zweikampf.</summary>
        private bool SectorPushFits(GameStateData state)
        {
            var s = state.SessionData;
            if (!s.CurrentLapIsValid || state.PitData.OnInLap || state.FlagData.isLocalYellow) return false;
            if (s.SessionType != SessionType.Race) return sectorBestCount % 3 != 0;
            bool battle = Math.Abs(s.TimeDeltaFront) > 0 && Math.Abs(s.TimeDeltaFront) < 1f || Math.Abs(s.TimeDeltaBehind) > 0 && Math.Abs(s.TimeDeltaBehind) < 1f;
            return !battle && sectorBestCount % 3 == 1;
        }

        private bool ObservePersonalBestPace(GameStateData state, LapData lap, int sector, DateTime now)
        {
            if (sector != 1 || type == SessionType.Race || !GoodSector(lap.SectorTimes[0]) ||
                now - lastSectorBestCall < TimeSpan.FromSeconds(55)) return false;
            var s = state.SessionData;
            float s1 = s.PlayerBestLapSector1Time, s2 = s.PlayerBestLapSector2Time, s3 = s.PlayerBestLapSector3Time;
            if (!Good(personalBest)) return false;
            if (!GoodSector(s1) || !GoodSector(s2) || !GoodSector(s3) || Math.Abs(s1 + s2 + s3 - personalBest) > .10f)
            {
                var best = s.PlayerLapData.LastOrDefault(candidate => candidate.IsValid && !candidate.InLap && !candidate.OutLap &&
                    Math.Abs(candidate.LapTime - personalBest) < .01f && candidate.SectorTimes?.Length == 3 &&
                    candidate.SectorTimes.All(GoodSector) && Math.Abs(candidate.SectorTimes.Sum() - candidate.LapTime) <= .10f);
                if (best == null) return false;
                s1 = best.SectorTimes[0];
                s2 = best.SectorTimes[1];
            }
            double delta = s1 + s2 - lap.SectorTimes[0] - lap.SectorTimes[1];
            if (delta < .25 || delta > 3) return false;
            var catalog = RadioPhraseCatalog.Current;
            int variant = SelectVariant("own-best-pace", bestPaceVariants);
            var call = Add(state, now, "own-best-pace", catalog.Format("own-best-pace", variant, GermanRadioText.Interval(delta)),
                "same_personal_best_lap_two_elapsed_splits", 3, bestPaceVariants, "own-best-pace", variant);
            call.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && !live.SessionData.SectorTimingEstimated && live.SessionData.SectorNumber == 3 &&
                !live.PitData.OnInLap && !live.FlagData.isLocalYellow;
            return true;
        }

        private bool firstSectorTimeCalled;
        private DateTime lastSectorTimeCall;
        private int sectorTimeVariant;

        /// <summary>
        /// Rob 14.–16.11.: erste Sektorzeit zeitnah hören; sonst nur interessante Sektoren (knapp an der eigenen Bestmarke),
        /// selten und nie im Rennen. Bestsektoren selbst laufen über ObserveLivePersonalBest.
        /// </summary>
        private bool ObserveInterestingSector(GameStateData state, LapData lap, int sector, DateTime now)
        {
            if (type == SessionType.Race || now - lastSectorBestCall < TimeSpan.FromSeconds(55)) return false;
            var s = state.SessionData;
            float time = lap.SectorTimes[sector];
            float prior = completedSectorBest[sector];
            float native = sector == 0 ? s.PlayerBestSector1Time : s.PlayerBestSector2Time;
            float reference = GoodSector(prior) && GoodSector(native) ? Math.Min(prior, native) : GoodSector(prior) ? prior : native;
            string text;
            int v = sectorTimeVariant++;
            string t = time.ToString("0.000", RadioLanguage.Culture);
            if (!GoodSector(reference))
            {
                if (sector != 0 || firstSectorTimeCalled) return false;
                firstSectorTimeCalled = true;
                text = v % 2 == 0
                    ? RadioLanguage.T("Erster Sektor: " + t + " Sekunden. Das ist unsere Referenz.", "First sector: " + t + " seconds. That's our reference.", "Primer sector: " + t + " segundos. Esa es nuestra referencia.")
                    : RadioLanguage.T("Sektor eins in " + t + ". Ab hier vergleichen wir.", "Sector one in " + t + ". We compare from here.", "Sector uno en " + t + ". A partir de aquí comparamos.");
            }
            else
            {
                double over = time - reference;
                // Rob 16.11. (Mosport): „0,000 Sekunden über deiner Bestmarke“ – gleiche Zeit ist keine Nachricht.
                if (over < .001 || over > .15 || now - lastSectorTimeCall < TimeSpan.FromMinutes(3)) return false;
                string d = over < .1 ? RadioLanguage.Num(over, "0.000") + RadioLanguage.T(" Sekunden", " seconds", " segundos") : GermanRadioText.Interval(over);
                switch (v % 3)
                {
                    case 0: text = RadioLanguage.T("Sektor " + (sector + 1) + " nur " + d + " über deiner Bestmarke. Da ist mehr drin.",
                        "Sector " + (sector + 1) + " only " + d + " off your best. There's more in it.", "Sector " + (sector + 1) + " a solo " + d + " de tu mejor marca. Hay más."); break;
                    case 1: text = RadioLanguage.T("Knapp dran in Sektor " + (sector + 1) + ": " + d + " fehlen auf deine Bestzeit.",
                        "Close in sector " + (sector + 1) + ": " + d + " to your best.", "Muy cerca en el sector " + (sector + 1) + ": te faltan " + d + "."); break;
                    default: text = RadioLanguage.T("Sektor " + (sector + 1) + " in " + t + ", " + d + " hinter deinem besten. Fast.",
                        "Sector " + (sector + 1) + " in " + t + ", " + d + " behind your best. Nearly.", "Sector " + (sector + 1) + " en " + t + ", " + d + " por detrás de tu mejor. Casi."); break;
                }
            }
            lastSectorTimeCall = now;
            var call = Add(state, now, "sector-time", text, GoodSector(reference) ? "valid_split_close_to_personal_best" : "first_valid_split_without_reference", 2);
            int currentSector = s.SectorNumber;
            call.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && live.SessionData.SectorNumber == currentSector &&
                !live.PitData.OnInLap && !live.FlagData.isLocalYellow;
            return true;
        }

        private static bool GoodSector(float seconds) => seconds >= 1 && seconds < 3600;
        private static int SelectVariant(string group, RadioVariantCursor cursor) => cursor.Select(group, RadioPhraseCatalog.Current.Count(group));

        private void ObserveCompletedSectorBest(GameStateData state, LapRecord lap, DateTime now, bool announce)
        {
            int sector = -1;
            float improvement = .019f;
            for (int i = 0; i < completedSectorBest.Length; i++)
            {
                float measured = lap.SectorTimes[i], prior = completedSectorBest[i];
                if (!TrackAwareness.InRange(measured, 1, 3600).HasValue || measured > lap.LapTime) continue;
                if (prior > 0 && prior - measured > improvement)
                {
                    sector = i;
                    improvement = prior - measured;
                }
                if (prior <= 0 || measured < prior) completedSectorBest[i] = measured;
            }
            if (sector < 0 || !announce || now - lastSectorBestCall < TimeSpan.FromSeconds(55)) return;
            OfferSectorBest(state, now, sector, lap.SectorTimes[sector], "valid_completed_player_lap_and_improved_recorded_sector");
        }

        private void ObserveQualifyingSector(GameStateData state, GameStateData previous, DateTime now, bool names, TrackAwareness tracks)
        {
            var s = state.SessionData;
            int sector = s.SectorNumber - 2;
            if (previous == null || previous.SessionData.SessionId != session || previous.SessionData.SessionType != type ||
                previous.SessionData.SectorNumber != s.SectorNumber - 1 || sector < 0 || sector > 1 ||
                !s.CurrentLapIsValid || state.PitData.InPitlane || state.PitData.OnOutLap || state.PitData.OnInLap ||
                lastSectorCallLap == s.CompletedLaps && lastSectorCall == sector) return;
            var current = s.PlayerLapData.LastOrDefault();
            if (current == null || current.LapNumber != s.CompletedLaps + 1 || !current.IsValid ||
                current.SectorTimes == null || sector >= current.SectorTimes.Length) return;
            float own = current.SectorTimes[sector];
            if (!TrackAwareness.InRange(own, 1, 3600).HasValue) return;
            var leaders = state.OpponentData.Where(p => p.Value != null && p.Value.IsActive && p.Value.ClassPosition == 1 &&
                state.carClass != null && CarData.IsCarClassEqual(state.carClass, p.Value.CarClass)).Take(2).ToArray();
            if (leaders.Length != 1) { FieldSector(state, sector, own, now); return; }
            var leader = leaders[0];
            if (tracks != null && !tracks.OpponentFresh(new RaceDataContext { GameState = state, NowUtc = now,
                LastTelemetryUtc = state.SourceTelemetryUtc == DateTime.MinValue ? state.Now : state.SourceTelemetryUtc }, leader.Value)) { FieldSector(state, sector, own, now); return; }
            var best = leader.Value.OpponentLapData.LastOrDefault(lap => lap.IsValid && !lap.InLap && !lap.OutLap &&
                Good(lap.LapTime) && Math.Abs(lap.LapTime - leader.Value.CurrentBestLapTime) < .001f);
            if (best?.SectorTimes == null || sector >= best.SectorTimes.Length ||
                !TrackAwareness.InRange(best.SectorTimes[sector], 1, 3600).HasValue) { FieldSector(state, sector, own, now); return; }
            double delta = own - best.SectorTimes[sector];
            if (Math.Abs(delta) < .025 || Math.Abs(delta) > 15) return;

            string name = names && leader.Value.CanUseName ? RadioPhraseCatalog.SafeRivalName(leader.Value.DriverRawName) : "";
            string formatted = RadioLanguage.Num(Math.Abs(delta), "0.000");
            string reference = name.Length == 0 ? RadioLanguage.T("auf der Bestzeit des Klassenführenden", "the class leader's best lap", "la mejor vuelta del líder de la clase") :
                RadioLanguage.T(name + " auf seiner Bestzeit", name + " on his best lap", name + " en su mejor vuelta");
            int variant = SelectVariant("quali-sector-comparison", sectorVariants);
            string text = RadioPhraseCatalog.Current.Format("quali-sector-comparison", variant, (sector + 1).ToString(CultureInfo.InvariantCulture),
                RadioLanguage.Num(own, "0.000"), formatted, delta < 0 ? RadioLanguage.T("schneller", "faster", "más rápido") : RadioLanguage.T("langsamer", "slower", "más lento"), reference);
            var call = Add(state, now, "quali-sector", text, "completed_sector_vs_verified_class_leader_best_lap", 4, sectorVariants,
                "quali-sector-comparison", variant);
            string identity = RaceReader.Identity(leader.Key, leader.Value);
            int observedSector = s.SectorNumber;
            float referenceLapTime = best.LapTime;
            call.FieldNamesAllowed = name.Length != 0;
            call.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && live.SessionData.SectorNumber == observedSector &&
                live.OpponentData.TryGetValue(leader.Key, out var opponent) && opponent.IsActive && opponent.ClassPosition == 1 &&
                RaceReader.Identity(leader.Key, opponent) == identity && Math.Abs(opponent.CurrentBestLapTime - referenceLapTime) < .001f;
            if (tracks != null)
                call.TimedValidity = (live, at) => live.OpponentData.TryGetValue(leader.Key, out var opponent) &&
                    tracks.OpponentFresh(new RaceDataContext { GameState = live, NowUtc = at, LastTelemetryUtc = live.Now }, opponent);
            lastSectorCallLap = s.CompletedLaps;
            lastSectorCall = sector;
        }

        private int firstComparisonVariant;

        // ---------- Rob 18.11.: Qualifying professioneller – Zielzeit, Sektorvergleich, wo die Zeit liegt ----------

        /// <summary>Sektorzeiten der schnellsten Runde der Konkurrenz in Robs Klasse (Crew-Chief-Zeitnahme); null ohne Daten.</summary>
        internal static float[] FieldBestSectors(GameStateData gs)
        {
            var t = gs?.TimingData;
            if (t == null) return null;
            var v = new[] { t.getPlayerClassOpponentBestLapSector1Time(), t.getPlayerClassOpponentBestLapSector2Time(), t.getPlayerClassOpponentBestLapSector3Time() };
            return v.All(x => x >= 1 && x < 3600) ? v : null;
        }

        /// <summary>Ersatz, wenn der Klassenführende keine prüfbaren Sektorzeiten liefert: Vergleich mit der schnellsten Runde der Konkurrenz.</summary>
        private void FieldSector(GameStateData state, int sector, float own, DateTime now)
        {
            var s = state.SessionData;
            var field = FieldBestSectors(state);
            if (field == null) return;
            double delta = own - field[sector];
            if (Math.Abs(delta) < .025 || Math.Abs(delta) > 15) return;
            var call = Add(state, now, "quali-sector", SectorVsField(sector, delta, fieldSectorVariant++), "completed_sector_vs_class_opponent_best_lap_sector", 4);
            int observedSector = s.SectorNumber;
            call.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && live.SessionData.SectorNumber == observedSector && !live.FlagData.isLocalYellow;
            lastSectorCallLap = s.CompletedLaps;
            lastSectorCall = sector;
        }
        private int fieldSectorVariant, targetVariant, analysisVariant, lastAnalysisLap = -100, attemptsVariant, lastAttemptsAnnounced = -1;

        /// <summary>Schnelle Runden, die nach der gerade begonnenen noch starten können; -1, wenn unsicher (Restzeit nah an einer
        /// Rundenzeit, ±3 s) oder ohne Messwerte. Eine Runde, die vor Ablauf beginnt, darf zu Ende gefahren werden.</summary>
        internal static int AttemptsAfterThisLap(float remaining, float lapTime)
        {
            if (!(remaining > 0) || !Good(lapTime)) return -1;
            double ratio = remaining / lapTime;
            double frac = ratio - Math.Floor(ratio);
            if (frac * lapTime < 3 || (1 - frac) * lapTime < 3) return -1;
            return (int)Math.Ceiling(ratio) - 1;
        }

        internal static string AttemptsText(int more, int variant)
        {
            if (more == 1)
                return Of(variant,
                    RadioLanguage.T("Nach dieser Runde reicht die Zeit noch für einen Versuch.", "After this lap there's time for one more attempt.", "Después de esta vuelta queda tiempo para un intento más."),
                    RadioLanguage.T("Zwei Versuche bleiben, diese und eine weitere.", "Two attempts left, this one and one more.", "Quedan dos intentos, esta y una más."),
                    RadioLanguage.T("Die Uhr gibt noch eine Runde nach dieser her.", "The clock gives you one more lap after this.", "El reloj da para una vuelta más después de esta."),
                    RadioLanguage.T("Eine Chance noch nach dieser. Jetzt die Zeit hinstellen.", "One more chance after this. Get the time in now.", "Una oportunidad más después de esta. Marca el tiempo ahora."),
                    RadioLanguage.T("Danach noch ein Versuch, also diese Runde sauber durchbringen.", "One more go after this, so bring this lap home clean.", "Después queda un intento, así que termina esta vuelta limpia."));
            return Of(variant,
                RadioLanguage.T("Nach dieser Runde ist noch Zeit für zwei Versuche.", "After this lap there's time for two more attempts.", "Después de esta vuelta queda tiempo para dos intentos."),
                RadioLanguage.T("Drei Runden passen noch rein, diese eingerechnet.", "Three laps still fit in, this one included.", "Aún caben tres vueltas, contando esta."),
                RadioLanguage.T("Zeit für diese und zwei weitere. Aufbauen, dann zuschlagen.", "Time for this one and two more. Build up, then strike.", "Tiempo para esta y dos más. Construye y luego ataca."),
                RadioLanguage.T("Zwei Chancen nach dieser. Keine Hektik.", "Two chances after this. No rush.", "Dos oportunidades después de esta. Sin prisa."),
                RadioLanguage.T("Die Restzeit reicht noch für zwei schnelle Runden nach dieser.", "Remaining time covers two more flying laps after this.", "El tiempo restante da para dos vueltas rápidas más después de esta."));
        }
        private DateTime lastAnalysisUtc = DateTime.MinValue;

        internal static string SectorVsField(int sector, double delta, int variant)
        {
            string n = (sector + 1).ToString(CultureInfo.InvariantCulture);
            string d = RadioLanguage.Num(Math.Abs(delta), "0.000");
            if (delta < 0)
                return Of(variant,
                    RadioLanguage.T("Sektor " + n + ": " + d + " schneller als die Bestzeit. Stark, weiter so.", "Sector " + n + ": " + d + " up on the best time. Strong, keep going.", "Sector " + n + ": " + d + " más rápido que el mejor tiempo. Muy bien, sigue."),
                    RadioLanguage.T("Grüner Sektor " + n + ", minus " + d + " auf die Bestzeit.", "Green sector " + n + ", minus " + d + " to the best time.", "Sector " + n + " en verde, menos " + d + " respecto al mejor tiempo."),
                    RadioLanguage.T("Du bist " + d + " vorn nach Sektor " + n + ". Jetzt nicht nachlassen.", "You're " + d + " up after sector " + n + ". Don't back off now.", "Vas " + d + " por delante tras el sector " + n + ". No aflojes."),
                    RadioLanguage.T("Sektor " + n + " schneller als alle anderen, " + d + " Vorsprung. Sauber zu Ende bringen.", "Sector " + n + " quicker than everyone, " + d + " up. Bring it home clean.", "Sector " + n + " más rápido que todos, " + d + " de ventaja. Termínala limpia."),
                    RadioLanguage.T("Minus " + d + " nach Sektor " + n + ". Das ist Pole-Tempo.", "Minus " + d + " after sector " + n + ". That's pole pace.", "Menos " + d + " tras el sector " + n + ". Eso es ritmo de pole."));
            return Of(variant,
                RadioLanguage.T("Sektor " + n + ": plus " + d + " auf die Bestzeit.", "Sector " + n + ": plus " + d + " to the best time.", "Sector " + n + ": más " + d + " respecto al mejor tiempo."),
                RadioLanguage.T("Nach Sektor " + n + " " + d + " hinter der Bestzeit. Weiter pushen.", "After sector " + n + ", " + d + " behind the best time. Keep pushing.", "Tras el sector " + n + ", " + d + " detrás del mejor tiempo. Sigue apretando."),
                RadioLanguage.T("Sektor " + n + " kostet " + d + " auf die Spitze. Da geht noch was.", "Sector " + n + " costs " + d + " to the top. There's more there.", "El sector " + n + " cuesta " + d + " respecto a la cabeza. Hay margen."),
                RadioLanguage.T("Plus " + d + " in Sektor " + n + ". Ruhig bleiben, die Runde ist noch offen.", "Plus " + d + " in sector " + n + ". Stay calm, the lap is still open.", "Más " + d + " en el sector " + n + ". Tranquilo, la vuelta sigue abierta."),
                RadioLanguage.T(d + " Rückstand nach Sektor " + n + ". Hol es dir im nächsten zurück.", d + " down after sector " + n + ". Get it back in the next one.", d + " de desventaja tras el sector " + n + ". Recupéralo en el siguiente."));
        }

        /// <summary>Beste Rundenzeit der Klasse, die für einen Zielplatz reicht (nächste Stufe: P10, P5, P3, Pole).</summary>
        internal static int TargetPosition(int position) => position > 10 ? 10 : position > 5 ? 5 : position > 3 ? 3 : 1;

        /// <summary>Zielansage beim Start einer schnellen Runde im Qualifying: Bestzeit, eigener Stand, was es für den nächsten Schritt braucht.</summary>
        internal static string TargetText(float best, string bestName, int position, float mine, int targetPos, float targetTime, int variant)
        {
            if (!Good(best))
                return Of(variant,
                    RadioLanguage.T("Noch keine Zeit auf der Uhr. Du setzt die Marke.", "No times on the board yet. You set the benchmark.", "Aún no hay tiempos. Tú marcas la referencia."),
                    RadioLanguage.T("Die Zeitenliste ist leer. Erste Referenz gehört dir.", "The timesheet is empty. First reference is yours.", "La tabla está vacía. La primera referencia es tuya."),
                    RadioLanguage.T("Freie Bahn auf der Zeitenliste. Bau eine saubere Runde auf.", "Clear timesheet. Build a clean lap.", "Tabla libre. Construye una vuelta limpia."),
                    RadioLanguage.T("Keiner hat bisher eine Zeit. Leg vor.", "Nobody's set a time yet. Put one in.", "Nadie tiene tiempo todavía. Pon uno."));
            string b = LocalAnswers.LapTime(best);
            string who = string.IsNullOrWhiteSpace(bestName) ? "" : bestName;
            if (!Good(mine) || position <= 0)
                return Of(variant,
                    RadioLanguage.T("Die Bestzeit steht bei " + b + (who.Length > 0 ? " von " + who : "") + ". Das ist unsere Referenz.", "Best time is " + b + (who.Length > 0 ? " from " + who : "") + ". That's our reference.", "El mejor tiempo es " + b + (who.Length > 0 ? " de " + who : "") + ". Esa es nuestra referencia."),
                    RadioLanguage.T("Referenz: " + b + ". Erst mal eine saubere Zeit hinstellen.", "Reference: " + b + ". Get a clean time on the board first.", "Referencia: " + b + ". Primero un tiempo limpio."),
                    RadioLanguage.T((who.Length > 0 ? who + " führt mit " : "Vorne steht eine ") + b + ". Bau dich da ran.", (who.Length > 0 ? who + " leads with " : "Top time is ") + b + ". Build up to it.", (who.Length > 0 ? who + " lidera con " : "Arriba hay un ") + b + ". Acércate."),
                    RadioLanguage.T("Zielmarke " + b + ". Rhythmus finden, dann angreifen.", "Benchmark " + b + ". Find your rhythm, then attack.", "Marca a batir " + b + ". Coge el ritmo y ataca."));
            string p = position.ToString(RadioLanguage.Culture), m = LocalAnswers.LapTime(mine);
            if (position == 1)
                return Of(variant,
                    RadioLanguage.T("Du stehst auf Pole mit " + m + ". Jede Zehntel mehr macht es sicherer.", "You're on pole with " + m + ". Every tenth makes it safer.", "Estás en la pole con " + m + ". Cada décima la hace más segura."),
                    RadioLanguage.T("P1 mit " + m + ". Bau den Vorsprung aus.", "P1 with " + m + ". Extend the margin.", "P1 con " + m + ". Amplía la ventaja."),
                    RadioLanguage.T("Pole steht, " + m + ". Jetzt noch eine Schippe drauf.", "Pole is yours, " + m + ". Now add a bit more.", "La pole es tuya, " + m + ". Ahora un poco más."),
                    RadioLanguage.T("Ganz vorn mit " + m + ". Die anderen kommen noch, leg nach.", "Top with " + m + ". The others are still coming, go again.", "Arriba con " + m + ". Los demás aún vienen, mejora."));
            string t = Good(targetTime) ? LocalAnswers.LapTime(targetTime) : null;
            string goal = targetPos == 1 ? RadioLanguage.T("die Pole", "pole", "la pole") : RadioLanguage.T("Platz " + targetPos, "P" + targetPos, "P" + targetPos);
            if (t == null)
                return Of(variant,
                    RadioLanguage.T("Du bist P" + p + " mit " + m + ", die Bestzeit ist " + b + ".", "You're P" + p + " with " + m + ", best time is " + b + ".", "Vas P" + p + " con " + m + ", el mejor tiempo es " + b + "."),
                    RadioLanguage.T("Stand P" + p + ", " + m + ". Vorne steht " + b + ".", "P" + p + " right now, " + m + ". Top time " + b + ".", "Ahora P" + p + ", " + m + ". Arriba " + b + "."),
                    RadioLanguage.T(b + " ist die Marke, du hast " + m + ". Angriff.", b + " is the mark, you've got " + m + ". Attack.", b + " es la marca, tienes " + m + ". Al ataque."),
                    RadioLanguage.T("P" + p + " im Moment. Bis " + b + " ist noch Luft, hol sie dir.", "P" + p + " at the moment. There's a gap to " + b + ", go get it.", "P" + p + " ahora mismo. Hay margen hasta " + b + ", ve a por él."));
            return Of(variant,
                RadioLanguage.T("Du bist P" + p + " mit " + m + ". Für " + goal + " brauchst du eine " + t + ", Bestzeit " + b + ".", "You're P" + p + " with " + m + ". For " + goal + " you need a " + t + ", best is " + b + ".", "Vas P" + p + " con " + m + ". Para " + goal + " necesitas un " + t + ", el mejor es " + b + "."),
                RadioLanguage.T("Ziel: " + t + ", das reicht für " + goal + ". Aktuell P" + p + ".", "Target: " + t + ", that's " + goal + ". Currently P" + p + ".", "Objetivo: " + t + ", eso da " + goal + ". Ahora P" + p + "."),
                RadioLanguage.T(t + " bringt dich auf " + goal + ". Du hast " + m + ", das ist drin.", t + " puts you on " + goal + ". You've got " + m + ", it's in there.", t + " te pone en " + goal + ". Tienes " + m + ", se puede."),
                RadioLanguage.T("P" + p + " im Moment. " + goal.Substring(0, 1).ToUpper(RadioLanguage.Culture) + goal.Substring(1) + " liegt bei " + t + ". Hol sie dir.", "P" + p + " at the moment. " + goal.Substring(0, 1).ToUpper(RadioLanguage.Culture) + goal.Substring(1) + " is a " + t + ". Go get it.", "P" + p + " ahora. " + goal.Substring(0, 1).ToUpper(RadioLanguage.Culture) + goal.Substring(1) + " está en " + t + ". Ve a por ella."),
                RadioLanguage.T("Diese Runde: " + t + " für " + goal + ", Bestzeit " + b + ". Konzentration.", "This lap: " + t + " for " + goal + ", best " + b + ". Focus.", "Esta vuelta: " + t + " para " + goal + ", mejor tiempo " + b + ". Concentración."));
        }

        /// <summary>Nach einer sauberen Quali-Runde: In welchem Sektor liegt die Zeit gegenüber der schnellsten Runde der Konkurrenz?</summary>
        internal static string LapAnalysis(float[] own, float[] field, int variant)
        {
            if (own == null || field == null || own.Length < 3 || field.Length < 3) return null;
            var d = Enumerable.Range(0, 3).Select(i => (double)own[i] - field[i]).ToArray();
            int worst = Array.IndexOf(d, d.Max()), best = Array.IndexOf(d, d.Min());
            string W = (worst + 1).ToString(CultureInfo.InvariantCulture), B = (best + 1).ToString(CultureInfo.InvariantCulture);
            string lose = RadioLanguage.Num(Math.Abs(d[worst]), "0.000"), gain = RadioLanguage.Num(Math.Abs(d[best]), "0.000");
            if (d.All(x => x <= 0))
                return Of(variant,
                    RadioLanguage.T("In allen drei Sektoren schneller als die Konkurrenz. So sieht eine Pole-Runde aus.", "Quicker than the field in all three sectors. That's what a pole lap looks like.", "Más rápido que todos en los tres sectores. Así es una vuelta de pole."),
                    RadioLanguage.T("Drei grüne Sektoren. Am meisten holst du in Sektor " + B + ", minus " + gain + ".", "Three green sectors. Most gained in sector " + B + ", minus " + gain + ".", "Tres sectores en verde. Lo que más ganas es en el sector " + B + ", menos " + gain + "."),
                    RadioLanguage.T("Überall vorn, Sektor " + B + " ist deine Stärke mit " + gain + ".", "Ahead everywhere, sector " + B + " is your strength with " + gain + ".", "Por delante en todo, el sector " + B + " es tu fuerte con " + gain + "."),
                    RadioLanguage.T("Keine Schwäche zu sehen. Sektor " + B + " besonders stark.", "No weakness to see. Sector " + B + " especially strong.", "Sin puntos débiles. El sector " + B + " especialmente fuerte."));
            if (d[worst] < .025) return null;
            bool strong = d[best] < 0;
            return Of(variant,
                RadioLanguage.T("Die Zeit liegt in Sektor " + W + ": " + lose + " auf die Bestzeit." + (strong ? " Sektor " + B + " war schneller als alle." : ""), "The time is in sector " + W + ": " + lose + " to the best." + (strong ? " Sector " + B + " was quicker than everyone." : ""), "El tiempo está en el sector " + W + ": " + lose + " respecto al mejor." + (strong ? " El sector " + B + " fue el más rápido." : "")),
                RadioLanguage.T("Sektor " + W + " kostet am meisten, " + lose + ". Da müssen wir ran.", "Sector " + W + " costs the most, " + lose + ". That's where we work.", "El sector " + W + " es el que más cuesta, " + lose + ". Ahí hay que trabajar."),
                RadioLanguage.T("Analyse: " + lose + " Verlust in Sektor " + W + (strong ? ", in Sektor " + B + " bist du vorn" : "") + ".", "Analysis: " + lose + " lost in sector " + W + (strong ? ", you're ahead in sector " + B : "") + ".", "Análisis: " + lose + " perdidos en el sector " + W + (strong ? ", vas por delante en el sector " + B : "") + "."),
                RadioLanguage.T("Größter Rückstand in Sektor " + W + ", " + lose + ". Konzentrier dich nächste Runde darauf.", "Biggest deficit in sector " + W + ", " + lose + ". Focus on that next lap.", "Mayor desventaja en el sector " + W + ", " + lose + ". Céntrate ahí la próxima vuelta."),
                RadioLanguage.T((strong ? "Sektor " + B + " top, " : "") + "Sektor " + W + " mit " + lose + " Rückstand. Da liegt deine Zeit.", (strong ? "Sector " + B + " top, " : "") + "sector " + W + " " + lose + " down. That's where your time is.", (strong ? "Sector " + B + " de primera, " : "") + "sector " + W + " con " + lose + " de desventaja. Ahí está tu tiempo."));
        }

        /// <summary>Zeit der Konkurrenz auf einem Klassenplatz (Bestzeit), -1 ohne Daten.</summary>
        internal static float TimeAtPosition(GameStateData gs, int position)
        {
            var o = gs.OpponentData.Values.FirstOrDefault(x => SameClass(gs, x) && x.ClassPosition == position && Good(x.CurrentBestLapTime));
            return o == null ? -1 : o.CurrentBestLapTime;
        }

        private void OfferQualiTarget(GameStateData gs, DateTime now, bool names)
        {
            if (!QualifyingResultWatch.Supported(type)) return;
            var s = gs.SessionData;
            float best = FieldBest(gs), mine = s.PlayerLapTimeSessionBest;
            var holder = Good(best) ? gs.OpponentData.Values.Where(o => SameClass(gs, o) && Good(o.CurrentBestLapTime) && Math.Abs(o.CurrentBestLapTime - best) < .01f).Take(2).ToArray() : new OpponentData[0];
            string who = names && holder.Length == 1 && holder[0].CanUseName ? RadioPhraseCatalog.SafeRivalName(holder[0].DriverRawName) : "";
            int pos = s.ClassPosition, target = TargetPosition(pos);
            float targetTime = Good(mine) && pos > 1 ? TimeAtPosition(gs, target) : -1;
            if (Good(mine) && Good(best) && mine < best) best = mine; // Rob selbst hat die Bestzeit
            var call = Add(gs, now, "quali-target", TargetText(best, who, Good(mine) ? pos : 0, mine, target, targetTime, targetVariant++),
                "timed_lap_start_class_best_own_best_and_target_position_time", 3);
            call.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && !live.PitData.InPitlane && !live.FlagData.isLocalYellow;
            call.FieldNamesAllowed = who.Length > 0;
        }

        /// <summary>
        /// Rob 16.11.: Nach der ersten gezeiteten Runde ein kurzer, natürlicher Vergleich mit der aktuellen Bestzeit der Klasse
        /// (mit Namen, wenn erlaubt und eindeutig). Ohne bekannte Bestzeit kein Satz.
        /// </summary>
        internal static string FirstLapComparison(GameStateData gs, float mine, bool names, int variant)
        {
            float best = FieldBest(gs);
            if (!Good(best) || !Good(mine) || mine <= best + .01f) return "";
            var holder = gs.OpponentData.Values.Where(o => SameClass(gs, o) && Good(o.CurrentBestLapTime) && Math.Abs(o.CurrentBestLapTime - best) < .01f).Take(2).ToArray();
            string who = names && holder.Length == 1 && holder[0].CanUseName ? RadioPhraseCatalog.SafeRivalName(holder[0].DriverRawName) : "";
            string gap = GermanRadioText.Interval(mine - best);
            string bestTime = LocalAnswers.LapTime(best);
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return who.Length > 0
                    ? RadioLanguage.T(" Die Bestzeit hat " + who + " mit " + bestTime + ", dir fehlen " + gap + ". Ein guter Anfang.",
                        " " + who + " has the best with " + bestTime + ", you're " + gap + " off. A good start.",
                        " " + who + " tiene el mejor tiempo con " + bestTime + ", te faltan " + gap + ". Buen comienzo.")
                    : RadioLanguage.T(" Zur Bestzeit fehlen " + gap + ". Ein guter Anfang.", " You're " + gap + " off the best. A good start.", " Te faltan " + gap + " para el mejor tiempo. Buen comienzo.");
                case 1: return RadioLanguage.T(" Damit " + gap + " hinter der Spitze" + (who.Length > 0 ? ", " + who : "") + ". Da arbeiten wir uns ran.",
                    " That's " + gap + " behind the top" + (who.Length > 0 ? ", " + who : "") + ". We'll work our way in.",
                    " Eso es " + gap + " detrás de la cabeza" + (who.Length > 0 ? ", " + who : "") + ". Vamos a acercarnos.");
                default: return RadioLanguage.T(" Die Messlatte liegt bei " + bestTime + (who.Length > 0 ? " von " + who : "") + ", also " + gap + " Luft nach oben.",
                    " The benchmark is " + bestTime + (who.Length > 0 ? " from " + who : "") + ", so " + gap + " to find.",
                    " La referencia es " + bestTime + (who.Length > 0 ? " de " + who : "") + ", así que hay " + gap + " por encontrar.");
            }
        }

        private static bool SameClass(GameStateData gs, OpponentData o) =>
            o != null && o.IsActive && gs.carClass != null && CarData.IsCarClassEqual(gs.carClass, o.CarClass);

        private static float FieldBest(GameStateData gs)
        {
            float best = gs.SessionData.PlayerClassSessionBestLapTime;
            foreach (var o in gs.OpponentData.Values)
                if (SameClass(gs, o) && Good(o.CurrentBestLapTime) && (!Good(best) || o.CurrentBestLapTime < best)) best = o.CurrentBestLapTime;
            return best;
        }

        internal static string FastestLapText(string name, float best, float mine, bool classOnly, int variant)
        {
            string who = string.IsNullOrEmpty(name) ? RadioLanguage.T("Ein Gegner", "A rival", "Un rival") : name;
            return who + (classOnly
                ? RadioLanguage.T(" fährt die schnellste Runde der Klasse: ", " sets the fastest lap in class: ", " marca la vuelta rápida de la clase: ")
                : RadioLanguage.T(" fährt die schnellste Runde: ", " sets the fastest lap: ", " marca la vuelta rápida: ")) + LocalAnswers.LapTime(best) + ". " +
                GapAndPush(best, mine, variant);
        }

        /// <summary>Eigener Rückstand auf die schnellste Runde (falls bekannt) und ein Ansporn.</summary>
        internal static string GapAndPush(float best, float mine, int variant)
        {
            string text = "";
            if (Good(mine) && mine > best + .01f)
            {
                string gap = RadioLanguage.Num(mine - best, mine - best < 1 ? "0.00" : "0.0");
                text = RadioLanguage.T("Dir fehlen " + gap + " Sekunden. ", "You're " + gap + " seconds off. ", "Te faltan " + gap + " segundos. ");
            }
            var push = MotivationPhrases;
            // Unabhängig vom Satzvarianten-Zähler weiterdrehen, damit der Ansporn nicht jedes Mal gleich klingt.
            int next = System.Threading.Interlocked.Increment(ref motivationCounter);
            return text + push[(int)((uint)(variant + next) % push.Length)];
        }

        private static int motivationCounter = -1;
        private static readonly string[] MotivationDe = {
            "Da geht noch was, bleib dran.",
            "Das ist die Messlatte. Du kannst das.",
            "Ruhig bleiben, die Zeit kommt.",
            "Hol sie dir Stück für Stück.",
            "Sauber fahren, dann kommst du ran.",
            "Nimm das als Ansporn.",
            "Lass dich nicht hetzen, dein Rhythmus zählt.",
            "Kopf hoch, wir arbeiten uns ran."
        };
        private static readonly string[] MotivationEn = {
            "There's more in it, stay with it.",
            "That's the benchmark. You can do this.",
            "Stay calm, the time will come.",
            "Chip away at it, bit by bit.",
            "Drive it clean and you'll get there.",
            "Use that as motivation.",
            "Don't rush it, your rhythm matters.",
            "Head up, we're closing in."
        };
        private static readonly string[] MotivationEs = {
            "Aún hay margen, sigue así.",
            "Esa es la referencia. Tú puedes.",
            "Tranquilo, el tiempo llegará.",
            "Ve recortando poco a poco.",
            "Conduce limpio y llegarás.",
            "Tómalo como motivación.",
            "Sin prisas, lo que cuenta es tu ritmo.",
            "Ánimo, nos estamos acercando."
        };
        internal static string[] MotivationPhrases => RadioLanguage.Pick(MotivationDe, MotivationEn, MotivationEs);

        private static bool Good(float t) { return !float.IsNaN(t) && !float.IsInfinity(t) && t > 10 && t < 3600; }
        private static bool IdentityMatches(string key, OpponentData before, OpponentData current) =>
            before != null && RaceReader.Identity(key, before) == RaceReader.Identity(key, current);
        private ObservedRaceCall Add(GameStateData gs, DateTime now, string topic, string text, string evidence, int priority,
            RadioVariantCursor cursor = null, string group = null, int variant = -1)
        {
            pending.RemoveAll(c => c.Topic == topic);
            var call = new ObservedRaceCall { Topic=topic, Text=text, Evidence=evidence, Priority=priority, Member=TeamMember.Mik,
                Position=-1, SessionId=session, SessionType=type, CompletedLaps=gs.SessionData.CompletedLaps,
                AllowLapFinish=topic == "own-lap" || topic == "near-best",
                DetectedUtc=now, ExpiresUtc=now.AddSeconds(topic.StartsWith("timed-lap-", StringComparison.Ordinal) ? 4 :
                    topic == "own-lap" ? 22 : topic == "first-lap-fuel" || topic == "fuel-baseline" ? 30 :
                    topic == "qualifying-last" ? 7 : topic == "quali-target" ? 15 : type == SessionType.Race ? 8 : 12) };
            if (group != null) call.BindVariant(cursor, group, variant);
            if (topic == "timed-lap-approach")
                call.AdditionalValidity = live => live.PitData.OnOutLap && live.SessionData.CurrentLapIsValid;
            pending.Add(call);
            LiveDiagnostics.Decision(topic,"detected",evidence,text,call.EventId);
            return call;
        }
        public ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool routineBusy = false, bool ownLapOnly = false)
        {
            foreach (var delivered in pending.Where(c => c.DeliveryStartedUtc != DateTime.MinValue).ToArray())
            {
                if (delivered.DeliveryStartedUtc > lastRadio) lastRadio = delivered.DeliveryStartedUtc;
                if (delivered.Topic == "fuel-baseline") fuelBaselineReported = true;
                if ((delivered.Topic == "sector-best" || delivered.Topic == "own-best-pace" || delivered.Topic == "lap-on-pace") &&
                    delivered.DeliveryStartedUtc > lastSectorBestCall)
                    lastSectorBestCall = delivered.DeliveryStartedUtc;
                pending.Remove(delivered);
            }
            foreach (var expired in pending.Where(c => !c.IsValid(gs,now)).ToArray())
            {
                LiveDiagnostics.Decision(expired.Topic,"suppressed","expired_or_lap_changed",expired.Evidence,expired.EventId);
                pending.Remove(expired);
            }
            if (busy) return null;
            var call = pending.Where(c => c.CanReserve(now) && (c.Topic == "own-lap" || c.Topic == "qualifying-last" ||
                c.Topic == "first-lap-fuel" || c.Topic == "fuel-baseline" || !ownLapOnly &&
                (c.Topic == "near-best" || c.Topic == "lap-on-pace" || c.Topic == "quali-target" && now - lastRadio >= TimeSpan.FromSeconds(3) || !routineBusy && now - lastRadio >= TimeSpan.FromSeconds(8)))).OrderByDescending(c => c.Priority).FirstOrDefault();
            return call != null && call.TryReserve(now) ? call : null;
        }
    }
}
