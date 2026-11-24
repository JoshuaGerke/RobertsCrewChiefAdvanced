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
        private bool firstTimedLapCalled, lastQualifyingLapOffered, firstFuelOffered, fuelBaselineReported, lastPracticeLapOffered;
        private int practiceLastVariant;

        /// <summary>Letzte Runde im Training – 5 Varianten je Sprache.</summary>
        internal static string PracticeLastLap(int variant)
        {
            string[] lines = RadioLanguage.Pick(
                new[] { "Letzte Runde im Training, die Uhr läuft ab. Nimm noch mit, was geht.",
                        "Das ist die letzte Trainingsrunde. Noch einmal sauber alles testen.",
                        "Die Zeit reicht nur noch für diese Runde. Nutze sie.",
                        "Letzte Runde, dann ist das Training vorbei. Ruhig und sauber.",
                        "Trainingsende nach dieser Runde. Bremspunkte noch einmal bestätigen." },
                new[] { "Last lap of practice, the clock is running out. Take what you can.",
                        "This is the last practice lap. One more clean run through everything.",
                        "Time only allows this lap. Make use of it.",
                        "Last lap, then practice is over. Calm and clean.",
                        "Practice ends after this lap. Confirm the braking points once more." },
                new[] { "Última vuelta de entrenamiento, se acaba el tiempo. Aprovecha lo que puedas.",
                        "Es la última vuelta de entrenamiento. Repasa todo una vez más, limpio.",
                        "El tiempo solo da para esta vuelta. Aprovéchala.",
                        "Última vuelta, luego se acaba el entrenamiento. Tranquilo y limpio.",
                        "El entrenamiento termina tras esta vuelta. Confirma otra vez las frenadas." });
            return lines[Math.Abs(variant) % lines.Length];
        }
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
        // Training/Qualifying (Rob 24.11., AMS2 Cascais): Die beim Einstieg mitgelieferte Bestmarke der Sim (Sektoren und Runde) ist
        // nur der Maßstab für „persönliche Bestzeit“; jede Verbesserung gegenüber dem, was das Team selbst gesehen hat, wird angesagt.
        private readonly float[] sectorSeed = { -1, -1, -1 };
        private bool sectorSeedOpen = true;
        private float observedBest = -1;
        // RaceRoom Salzburg 24.11. (Hotlap): Robs 1:29,508 zählte die Sim als Bestzeit, Crew Chief führte die Runde als ungültig;
        // keine Ansage. Eine Runde, die die Sim als Bestzeit der Klasse übernimmt und die kein Gegner hält, ist gültig.
        private LapRecord unconfirmedLap;
        private DateTime unconfirmedUtc = DateTime.MinValue;
        internal static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);
        private bool earlyFuelOffered;
        private int liveS1Lap = -1, sessionSectorCount;
        private bool liveS1Pb, liveS1Purple;
        private DateTime liveS1Utc = DateTime.MinValue;

        public void Reset()
        {
            pending.Clear(); session = int.MinValue; type = SessionType.Unavailable; personalBest = classBest = -1;
            lastLap = lastSectorCallLap = lastSectorCall = -1; improvements = new RadioVariantCursor(); rivalBests = new RadioVariantCursor();
            lastRadio = lastSectorBestCall = lastSectorTimeCall = announcedLapUtc = DateTime.MinValue; firstSectorTimeCalled = false; announcedLapSeconds = -1;
            firstTimedLapCalled = lastQualifyingLapOffered = firstFuelOffered = fuelBaselineReported = lastPracticeLapOffered = false;
            for (int i = 0; i < completedSectorBest.Length; i++) completedSectorBest[i] = sectorSeed[i] = -1;
            unconfirmedLap = null; unconfirmedUtc = DateTime.MinValue; earlyFuelOffered = false;
            sectorSeedOpen = true; observedBest = -1; liveS1Lap = -1; liveS1Pb = liveS1Purple = false; liveS1Utc = DateTime.MinValue; sessionSectorCount = 0;
            edgeLap = edgeSector = -1; edgeUtc = DateTime.MinValue; flyingLap = -1; hotLap = -1;
            timedLapAttempts = 0; lastGapLap = -100; lastGapUtc = DateTime.MinValue; wasInPit = false; pendingQualiTarget = false; lastAnalysisLap = -100; lastAnalysisUtc = DateTime.MinValue; lastAttemptsAnnounced = -1; lastOutlapGapLap = -1;
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
                SeedSectors(gs);
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
            if (!race && completed != null && !completed.Valid && !completed.InPitLap && Good(completed.LapTime))
            { unconfirmedLap = completed; unconfirmedUtc = now; }
            if (!race && unconfirmedLap != null)
            {
                if (now < unconfirmedUtc || now - unconfirmedUtc > ConfirmWindow) unconfirmedLap = null;
                else if (SimCountsAsOwnBest(gs, unconfirmedLap.LapTime))
                {
                    LiveDiagnostics.Decision("own-lap", "cross_checked", "sim_counts_lap_as_own_class_best", LocalAnswers.LapTime(unconfirmedLap.LapTime));
                    completed = Confirmed(unconfirmedLap);
                    unconfirmedLap = null;
                    if (lastLap == completed.LapNumber) lastLap = -1;
                }
            }
            RememberTodayBest(gs, completed);
            OfferEarlyFuel(gs, now, race, green, fuelHistory);
            // Mitgelieferte Sektor-Bestmarken bis zum ersten selbst gesehenen Sektorwechsel (danach trägt Crew Chief eigene Zeiten ein).
            if (sectorSeedOpen && previous != null && previous.SessionData.SectorNumber != s.SectorNumber) sectorSeedOpen = false;
            if (sectorSeedOpen) SeedSectors(gs);
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
            ObserveOutlapGap(gs, tracks, now);
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
            // Monza 21.11.: „Du hast mir wieder nicht gesagt, dass ich auf der letzten Runde bin.“ Alle Quali-Runden waren
            // gestrichen, die Restzeitprüfung lief nur auf gültigen Runden – die Ansage kam erst mit der Zielflagge. Das Lagebild
            // weiß an der Linie, ob die Uhr noch eine Runde zulässt, auch auf einer gestrichenen Runde.
            bool fieldLast = green && s.IsNewLap && !gs.PitData.InPitlane && LapWatch.FieldLapsLeft(s) == 1;
            bool finalQualifyingLap = QualifyingResultWatch.Supported(type) && previous?.SessionData.SessionId == session &&
                previous.SessionData.SessionPhase == SessionPhase.Green && (s.SessionPhase == SessionPhase.Checkered ||
                green && s.IsNewLap && s.SessionLapsRemaining == 1 && !s.SessionHasFixedTime || moreLaps == 0 && s.CompletedLaps > 0 || fieldLast);
            if ((moreLaps == 1 || moreLaps == 2) && moreLaps != lastAttemptsAnnounced && !gs.PitData.OnOutLap && !pending.Any(c => c.Topic == "quali-target"))
            {
                lastAttemptsAnnounced = moreLaps;
                var attempts = Add(gs, now, "quali-attempts", AttemptsText(moreLaps, attemptsVariant++), "session_time_remaining_vs_own_lap_time_" + moreLaps + "_more", 3);
                attempts.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && !live.PitData.InPitlane && !live.FlagData.isLocalYellow;
            }
            if (!lastQualifyingLapOffered && finalQualifyingLap && !gs.PitData.InPitlane && !gs.PitData.OnOutLap &&
                (s.CurrentLapIsValid || fieldLast) && gs.PositionAndMotionData.CarSpeed > 12 && s.CompletedLaps > 0)
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
                last.AdditionalValidity = live => !live.PitData.OnOutLap && (live.SessionData.CurrentLapIsValid || fieldLast) &&
                    !live.FlagData.isLocalYellow && live.PositionAndMotionData.CarSpeed > 5;
            }
            // Training: Rob 21.11.: „dass die Practice in ein, zwei Minuten ausläuft – genau solche Infos solltest du mir sagen.“
            if (!lastPracticeLapOffered && fieldLast && (type == SessionType.Practice || type == SessionType.LonePractice) &&
                !gs.PitData.OnOutLap && gs.PositionAndMotionData.CarSpeed > 12 && s.CompletedLaps > 0)
            {
                lastPracticeLapOffered = true;
                var last = Add(gs, now, "practice-last", PracticeLastLap(practiceLastVariant++), "field_picture_clock_allows_no_further_lap", 5);
                last.Member = TeamMember.Ben;
                last.AdditionalValidity = live => !live.PitData.InPitlane && !live.FlagData.isLocalYellow && live.PositionAndMotionData.CarSpeed > 5;
            }
            if (completed != null && completed.LapNumber != lastLap)
            {
                lastLap = completed.LapNumber;
                // RaceRoom Salzburg 24.11. (Training): Die erste volle Runde war gestrichen und hatte keine Zeit; der Verbrauch (1,66 l)
                // war gemessen, kam aber erst auf Nachfrage. Für den Verbrauch zählt die volle Runde, nicht ihre Zeit.
                if (!firstFuelOffered && green && !completed.InPitLap && !gs.PitData.InPitlane &&
                    (Good(completed.LapTime) || Good(completed.ClockLapTime) || !race) && TrackAwareness.InRange(completed.FuelUsed, .05, 50).HasValue)
                {
                    firstFuelOffered = true;
                    pending.RemoveAll(c => c.Topic == "fuel-early");
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
                float bestBefore = personalBest;
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
                        // LMU Sebring 24.11.: Im Rennen hieß jede schnellere Runde „neue persönliche Bestzeit“, obwohl Robs Quali-Runde
                        // (2:04,297) schneller war. Im Rennen ist es die beste Rennrunde.
                        string text = (race && !first ? RaceBestLine(formatted, heard) : RadioPhraseCatalog.Current.Format(group, heard, formatted)) +
                            (classRecord ? RadioVariants.Pick("class-best-suffix",
                                RadioLanguage.T(" Klassenbestzeit an der Linie.", " That's the class best!", " ¡Es el mejor tiempo de la clase!"),
                                RadioLanguage.T(" Damit hast du die Klassenbestzeit!", " That's the fastest in class!", " ¡Con eso tienes el mejor tiempo de la clase!"),
                                RadioLanguage.T(" Schnellster in der Klasse!", " Quickest in class!", " ¡El más rápido de la clase!"),
                                RadioLanguage.T(" Ganz oben in der Klasse.", " Top of the class.", " Primero de la clase.")) :
                                first ? FirstLapComparison(gs, time, names, firstComparisonVariant++) : "");
                        // Rob 24.11.: Bei der neuen schnellsten Runde im Training/Qualifying kurzes Lob und Motivation.
                        if (!race && !first) text += " " + RadioVariants.Pick("lap-best-motivation", SessionBestPhrases.LapBestMotivation);
                        // The recorded lap remains true when standings or another driver's best changes.
                        // Fresh classification and gaps belong to the separate timing reports.
                        Add(gs, now, "own-lap", text, first ? "first_valid_lap" : "personal_best_improved", 5,
                            variants, group, heard);
                        ownCall = true;
                        announcedLapSeconds = time; announcedLapUtc = now;
                    }
                    else if (!race && Good(observedBest) && time < observedBest - .01f && time > personalBest + .01f)
                    {
                        // AMS2 Cascais 24.11.: Funk erst nach sechs Runden gestartet, die Sim lieferte 1:37,640 als Bestzeit mit. Die
                        // Steigerung von 1:38,863 über 1:38,241 auf 1:38,145 blieb stumm. Schneller als alles seit dem Einstieg ist
                        // eine Nachricht – ehrlich mit dem Rest auf die persönliche Bestzeit.
                        string text = RadioVariants.Pick("lap-improved", SessionBestPhrases.LapImproved(LocalAnswers.LapTime(time), Delta(time - personalBest)));
                        Add(gs, now, "own-lap", text, "valid_lap_faster_than_observed_best_above_seeded_personal_best", 5);
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
                    if (!race) observedBest = Good(observedBest) ? Math.Min(observedBest, time) : time;
                }
                // LMU 21.11.: „Ich hab grad eine Runde gefahren, gar kein Kommentar dazu.“ Im Rennen kam nur die Bestzeit
                // eine Ansage; vier Runden (drei gestrichen) blieben stumm. Ab Runde 2 bekommt jede eigene Runde einen Satz.
                if (race && green && !ownCall && !completed.InPitLap && !gs.PitData.InPitlane && completed.LapNumber >= 2)
                {
                    string comment = RaceLapComment(completed.Valid, completed.LapTime, completed.ClockLapTime, bestBefore, raceLapVariant++);
                    if (comment != null)
                    {
                        Add(gs, now, "race-lap", comment, completed.Valid ? "completed_race_lap_vs_personal_best" : "completed_race_lap_not_counted", 4);
                        ownCall = true;
                    }
                }
            }
            // Schnellste Runde im Feld (bei mehreren Klassen: in Robs Klasse), in jeder Session.
            // Die erste Zeit einer Session ist kein Ereignis, erst jede Verbesserung danach.
            float nextBest = FieldBest(gs);
            // RaceRoom Salzburg 24.11. (Training): Rethemeiers 1:28,207 war die erste Zeit der Session und blieb still. Rob: „Da ist gerade
            // die erste schnelle Runde von jemandem reingekommen. Ihr solltet mir sowas melden.“ Auch die erste Zeit zählt, wenn sie
            // gerade gefahren wurde (letzte Runde = Bestzeit); beim Einstieg mitgelieferte Zeiten bleiben still.
            bool firstFieldTime = !Good(classBest) && Good(nextBest);
            if (!race && green && Good(nextBest) && (firstFieldTime || nextBest < classBest - .01f) && !ownCall)
            {
                // Nur eine gerade gefahrene Runde; nachgeladene alte Bestzeiten (Session-Beitritt) bleiben still.
                var rival = gs.OpponentData.Values.FirstOrDefault(o => SameClass(gs, o) && Good(o.CurrentBestLapTime) && Math.Abs(o.CurrentBestLapTime - nextBest) < .01f &&
                    (firstFieldTime ? Good(o.LastLapTime) && Math.Abs(o.LastLapTime - nextBest) < .01f : !Good(o.LastLapTime) || Math.Abs(o.LastLapTime - nextBest) < .01f));
                if (rival != null)
                {
                    string name = names ? new string((rival.DriverRawName ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').Take(50).ToArray()).Trim() : "";
                    float mine = Good(personalBest) && Good(s.PlayerLapTimeSessionBest) ? Math.Min(personalBest, s.PlayerLapTimeSessionBest) :
                        Good(personalBest) ? personalBest : s.PlayerLapTimeSessionBest;
                    if (!Good(mine)) mine = TodayBest(gs);
                    var rivalCall = Add(gs, now, "rival-best", FastestLapText(name, nextBest, mine, RaceRadioLanguage.SeparateClass(s), rivalBests.Heard),
                        firstFieldTime ? "first_field_time_just_driven" : "measured_field_best_and_identified_rival", 4, rivalBests);
                    // Ben sagt die Bestzeiten im Feld an; sie bleiben gültig, bis jemand schneller ist.
                    rivalCall.Member = TeamMember.Ben;
                    rivalCall.ExpiresUtc = now.AddSeconds(NativeFieldEvents.FastestLapSeconds);
                    float record = nextBest;
                    rivalCall.AdditionalValidity = live => !(FieldBest(live) < record - .01f);
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
                if (live && !race && (ObserveSessionSectorBest(gs, lap, sector, now) || ObservePersonalBestPace(gs, lap, sector, now))) return;
                if (live && race && (ObserveFlyingLap(gs, previous, lap, sector, now) || ObserveLivePersonalBest(gs, previous, lap, sector, now) ||
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
            if (GoodSector(prior) && prior - time < .04f)
            { LiveDiagnostics.Decision("sector-best", "skipped", "sector_" + (sector + 1) + ":not_faster_than_kept_best_by_0.04s"); return false; }
            string evidence;
            float gain;
            if (state.Measurements.TryGetValue(key, out var value) && previous.Measurements.TryGetValue(key, out var old) &&
                old.Value.HasValue && value.Value.HasValue)
            {
                if (value.StatusAt(now) != "gueltig" || old.StatusAt(now) != "gueltig" || value.SampleUtc <= old.SampleUtc ||
                    Math.Abs(value.Value.Value - time) > .035 || old.Value.Value - value.Value.Value < .04)
                { LiveDiagnostics.Decision("sector-best", "skipped", "sector_" + (sector + 1) + ":native_personal_best_unchanged"); return false; }
                evidence = "native_personal_sector_best_and_completed_current_split";
                gain = (float)(old.Value.Value - value.Value.Value);
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
                gain = reference - time;
            }
            completedSectorBest[sector] = prior > 0 ? Math.Min(prior, time) : time;
            if (now - lastSectorBestCall < TimeSpan.FromSeconds(55))
            { LiveDiagnostics.Decision("sector-best", "skipped", "sector_" + (sector + 1) + ":cooldown_55s"); return false; }
            var call = OfferSectorBest(state, now, sector, time, evidence, true, gain);
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

        /// <summary>
        /// LMU 18.11. (Quali): Die Ansage zur schnellen Runde verfiel, weil kurz vor der Linie eine Bestzeit-Info des Gegners
        /// und eine lange Antwort liefen. Letztes Viertel der Aufwärmrunde und die ersten 400 m danach: Platz für die Ansage.
        /// </summary>
        internal static bool FlyingLapWindow(GameStateData gs)
        {
            var s = gs?.SessionData;
            if (s == null || s.SessionType == SessionType.Race || !QualifyingResultWatch.Supported(s.SessionType) || s.SessionPhase != SessionPhase.Green ||
                gs.PitData.InPitlane || s.TrackDefinition == null || s.TrackDefinition.trackLength <= 100) return false;
            float length = s.TrackDefinition.trackLength, distance = gs.PositionAndMotionData.DistanceRoundTrack;
            return gs.PitData.OnOutLap ? distance > length * .75f && distance <= length : s.CurrentLapIsValid && distance >= 0 && distance < 400 && s.CompletedLaps >= 1 && s.LapTimeCurrent > 0 && s.LapTimeCurrent < 8;
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

        /// <summary>Mitgelieferte Sektor-Bestmarken übernehmen (Einstieg in eine laufende Session).</summary>
        private void SeedSectors(GameStateData gs)
        {
            var s = gs.SessionData;
            float[] values = { s.PlayerBestSector1Time, s.PlayerBestSector2Time, s.PlayerBestSector3Time };
            for (int i = 0; i < values.Length; i++)
                if (GoodSector(values[i])) sectorSeed[i] = GoodSector(sectorSeed[i]) ? Math.Min(sectorSeed[i], values[i]) : values[i];
        }

        /// <summary>
        /// Training/Qualifying, live nach Sektor 1 und 2 (Rob 24.11., AMS2 Cascais): Von 26 Prüfungen ging keine durch – der Vergleich
        /// lief gegen die beim Einstieg mitgelieferte Bestmarke der Sim und verlangte 0,04 s Gewinn. Die Ansage kam dann eine Runde später
        /// am Rundenende, lang und mit Zahlen. Jetzt zählt jede Verbesserung gegenüber dem, was das Team in dieser Session gesehen hat,
        /// sofort und kurz; „persönliche Bestzeit“ heißt es nur, wenn auch die mitgelieferte Marke fällt. Nach Sektor 1 und 2 auf Bestzeit:
        /// „das könnte deine schnellste Runde werden“.
        /// </summary>
        private bool ObserveSessionSectorBest(GameStateData state, LapData lap, int sector, DateTime now)
        {
            if (sector > 1) return false;
            float time = lap.SectorTimes[sector];
            float observed = completedSectorBest[sector], seed = sectorSeed[sector];
            float reference = GoodSector(observed) ? observed : seed;
            float own = GoodSector(observed) && GoodSector(seed) ? Math.Min(observed, seed) : GoodSector(observed) ? observed : seed;
            RateSector(time, own, FieldBestSector(state, sector), out _, out bool purple);
            bool pb = GoodSector(own) && own - time >= .001f;
            bool improved = GoodSector(reference) && reference - time >= .001f;
            // Schnelle Runde: Bestzeitkurs oder höchstens 0,25 s darüber – Routine und Hintergrund schweigen bis zum Rundenende.
            bool onPace = pb || improved || purple || GoodSector(own) && time - own <= HotLapMargin;
            if (onPace) hotLap = lap.LapNumber; else if (hotLap == lap.LapNumber) hotLap = -1;
            bool afterS1 = sector == 1 && liveS1Lap == lap.LapNumber;
            if (!(pb || improved || purple))
            {
                if (sector == 0) liveS1Lap = -1;
                LiveDiagnostics.Decision("sector-best", "skipped", "sector_" + (sector + 1) + (GoodSector(reference) ? ":not_faster_than_session_best" : ":no_reference_yet"));
                return false;
            }
            // Nur eine Verbesserung wird sofort zur neuen Marke; die erste Zeit setzt das Rundenende (gültige Runde).
            completedSectorBest[sector] = GoodSector(observed) ? Math.Min(observed, time) : time;
            bool s1Spoken = afterS1 && (pending.Any(c => c.Topic == "sector-best" && c.DeliveryStartedUtc != DateTime.MinValue) || lastSectorBestCall >= liveS1Utc);
            bool bothPb = afterS1 && pb && liveS1Pb;
            string text, evidence;
            if (afterS1 && s1Spoken)
            {
                text = RadioVariants.Pick(bothPb ? "sector2-after-best" : "sector2-after-improved",
                    bothPb ? SessionBestPhrases.SecondAfterFirstBest : SessionBestPhrases.SecondAfterFirstImproved);
                if (purple) text += " " + RadioVariants.Pick("sector-purple", SessionBestPhrases.PurpleSuffix);
                evidence = bothPb ? "live_sectors_1_and_2_personal_best" : "live_sectors_1_and_2_improved";
            }
            else if (bothPb)
            {
                // Sektor 1 war noch nicht zu hören: beide in einem Satz (die Einzelmeldung zu Sektor 1 wird unten ersetzt).
                int variant = flyingRandom.Next(6);
                if (variant == flyingLastVariant) variant = (variant + 1 + flyingRandom.Next(5)) % 6;
                flyingLastVariant = variant;
                text = FlyingLapText(null, liveS1Purple && purple, false, PersonalBestSplitDelta(state, lap), variant);
                evidence = "live_sectors_1_and_2_personal_best";
            }
            else
            {
                text = pb ? RadioVariants.Pick("sector-pb-" + sector, SessionBestPhrases.SectorPersonalBest(sector)) :
                    improved ? RadioVariants.Pick("sector-improved-" + sector, SessionBestPhrases.SectorImproved(sector)) : null;
                if (purple) text = (text == null ? "" : text + " ") + RadioVariants.Pick("sector-purple", SessionBestPhrases.PurpleSuffix);
                string gained = improved ? SessionBestPhrases.Gain(reference - time) : null;
                if (gained != null) text += " " + gained;
                evidence = pb ? "live_sector_personal_best" : improved ? "live_sector_faster_than_session_best" : "live_sector_faster_than_class_best";
            }
            if (sessionSectorCount++ % 2 == 0 && !string.IsNullOrWhiteSpace(sectorDriverName))
                text = RadioPhraseCatalog.Address(text, sectorDriverName);
            if (sector == 0) { liveS1Lap = lap.LapNumber; liveS1Pb = pb; liveS1Purple = purple; liveS1Utc = now; }
            else liveS1Lap = -1;
            var call = Add(state, now, "sector-best", text, evidence, 4);
            int currentSector = state.SessionData.SectorNumber;
            call.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && !live.SessionData.SectorTimingEstimated &&
                live.SessionData.SectorNumber == currentSector && !live.PitData.OnInLap && !live.PitData.OnOutLap && !live.FlagData.isLocalYellow;
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
                    // Rob 20.11.: „Bestzeit“ allein heißt Feldbestzeit; die eigene heißt „persönliche Bestzeit“.
                    ? RadioLanguage.T((purple ? "Und Sektor 2 ist auch lila!" : "Und Sektor 2 ist auch persönliche Bestzeit!") + " Das könnte deine schnellste Runde werden" + (n != null ? ", " + n : "") + ".",
                        (purple ? "And sector 2 is purple as well!" : "And sector 2 is a personal best as well!") + " This could be your fastest lap" + (n != null ? ", " + n : "") + ".",
                        (purple ? "¡Y el sector 2 también es morado!" : "¡Y el sector 2 también es tu mejor marca personal!") + " Esta puede ser tu vuelta más rápida" + (n != null ? ", " + n : "") + ".")
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
                    ? RadioLanguage.T((n != null ? n + ", p" : "P") + "ersönliche Bestzeit in Sektor 1 und 2. Das könnte deine schnellste Runde werden.",
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

        private ObservedRaceCall OfferSectorBest(GameStateData state, DateTime now, int sector, float seconds, string evidence, bool lapStillRunning = false, float gain = 0)
        {
            var catalog = RadioPhraseCatalog.Current;
            int variant = SelectVariant("own-sector-best", sectorBestVariants);
            // Rob 14.11.: Zeiten mit drei Nachkommastellen.
            string time = seconds.ToString("0.000", RadioLanguage.Culture);
            string text = catalog.Format("own-sector-best", variant, (sector + 1).ToString(CultureInfo.InvariantCulture), time);
            // Rob 20.11. (LMU-Rennen): „Wenn ich mich im Sektor verbessert habe, musst du mir auch sagen, um wie viel.“
            // LMU Algarve 24.11.: „8,483 Sekunden Gewinn auf deine alte persönliche Bestmarke“ – die alte Marke war der Sektor aus der
            // stehenden Startrunde. Ein Gewinn über 1,5 s (bzw. 5 % des Sektors) ist kein Vergleich zweier fliegender Sektoren.
            string gained = PlausibleSectorGain(gain, seconds) ? SectorGainText(gain) : null;
            if (gained != null) text += " " + gained;
            // Rob 24.11.: „Die Sektorenbestzeiten werden nicht mal mit der Bestzeit verglichen.“ Im Rennen kommt dazu der Vergleich
            // mit demselben Sektor auf der schnellsten Runde der Klasse.
            if (state.SessionData.SessionType == SessionType.Race)
            {
                string classBest = ClassBestSectorText(state, sector, seconds, SendNames);
                if (classBest != null) text += " " + classBest;
            }
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

        /// <summary>Gewinn aus zwei fliegenden Sektoren: höchstens 1,5 s bzw. 5 % des Sektors.</summary>
        internal static bool PlausibleSectorGain(float gain, float sectorSeconds) =>
            gain >= .01f && sectorSeconds > 0 && gain <= Math.Max(1.5f, sectorSeconds * .05f);

        /// <summary>Namen von Gegnern im Sektorvergleich nennen (Einstellung „Fahrernamen senden“).</summary>
        internal bool SendNames = true;

        /// <summary>
        /// Derselbe Sektor auf der schnellsten gültigen Runde der Klasse (nur Gegner mit gespeicherten Sektorzeiten): „Auf der
        /// schnellsten Runde der Klasse hatte Mendez hier 22,433. Dir fehlen 0,164.“ Null ohne belastbare Vergleichsrunde.
        /// </summary>
        internal static string ClassBestSectorText(GameStateData state, int sector, float own, bool names)
        {
            if (state?.OpponentData == null || sector < 0 || !(own > 0)) return null;
            OpponentData holder = null; LapData best = null;
            foreach (var o in state.OpponentData.Values)
            {
                if (o == null || o.OpponentLapData == null) continue;
                if (state.carClass != null && o.CarClass != null && !CarData.IsCarClassEqual(state.carClass, o.CarClass)) continue;
                foreach (var lap in o.OpponentLapData)
                {
                    if (lap == null || !lap.IsValid || lap.InLap || lap.OutLap || !Good(lap.LapTime) || lap.SectorTimes == null ||
                        sector >= lap.SectorTimes.Length || !TrackAwareness.InRange(lap.SectorTimes[sector], 1, 3600).HasValue) continue;
                    if (best == null || lap.LapTime < best.LapTime) { best = lap; holder = o; }
                }
            }
            if (best == null) return null;
            float reference = best.SectorTimes[sector];
            double delta = own - reference;
            if (Math.Abs(delta) > Math.Max(3, reference * .1)) return null;
            string name = names && holder.CanUseName ? RadioPhraseCatalog.SafeRivalName(holder.DriverRawName) : null;
            return ClassBestSectorPhrase(name, reference, delta);
        }

        internal static string ClassBestSectorPhrase(string name, float reference, double delta)
        {
            string r = RadioLanguage.Num(reference, "0.000"), d = RadioLanguage.Num(Math.Abs(delta), "0.000");
            bool named = !string.IsNullOrWhiteSpace(name);
            string who = named ? name : RadioLanguage.T("der Schnellste deiner Klasse", "the class fastest", "el más rápido de tu clase");
            if (Math.Abs(delta) < .0005)
                return RadioVariants.Pick("sector-class-equal",
                    RadioLanguage.T("Genau so schnell wie " + who + " auf der schnellsten Runde der Klasse.", "Exactly as quick as " + who + " on the fastest lap of the class.", "Igual de rápido que " + who + " en la vuelta más rápida de la clase."),
                    RadioLanguage.T("Gleichauf mit " + who + " auf seiner Bestrunde.", "Level with " + who + " on their best lap.", "Empatado con " + who + " en su mejor vuelta."),
                    RadioLanguage.T("Auf die Tausendstel wie " + who + " auf der Klassenbestzeit.", "To the thousandth like " + who + " on the class best.", "Al milésimo como " + who + " en el mejor tiempo de la clase."),
                    RadioLanguage.T("Das ist exakt die Zeit von " + who + " auf der Klassenbestzeit.", "That's exactly " + who + "'s time on the class best.", "Es exactamente el tiempo de " + who + " en el mejor de la clase."));
            if (delta < 0)
                return RadioVariants.Pick("sector-class-faster",
                    RadioLanguage.T("Schneller als " + who + " auf der schnellsten Runde der Klasse, " + d + " besser.", "Quicker than " + who + " on the fastest lap of the class, " + d + " better.", "Más rápido que " + who + " en la vuelta más rápida de la clase, " + d + " mejor."),
                    RadioLanguage.T("Das schlägt " + who + " auf der Klassenbestzeit um " + d + ".", "That beats " + who + " on the class best by " + d + ".", "Eso supera a " + who + " en el mejor de la clase por " + d + "."),
                    RadioLanguage.T("Hier bist du " + d + " schneller als " + who + " auf seiner Bestrunde.", "Here you're " + d + " quicker than " + who + " on their best lap.", "Aquí eres " + d + " más rápido que " + who + " en su mejor vuelta."),
                    RadioLanguage.T("Klassenbestzeit hier geschlagen: " + d + " vor " + who + ".", "Class best beaten here: " + d + " ahead of " + who + ".", "Mejor de la clase batido aquí: " + d + " por delante de " + who + "."),
                    RadioLanguage.T("Besser als " + who + " auf der schnellsten Runde, um " + d + ".", "Better than " + who + " on the fastest lap, by " + d + ".", "Mejor que " + who + " en la vuelta más rápida, por " + d + "."));
            return RadioVariants.Pick("sector-class-slower",
                RadioLanguage.T("Auf der schnellsten Runde der Klasse hatte " + who + " hier " + r + ". Dir fehlen " + d + ".", "On the fastest lap of the class " + who + " had " + r + " here. You're " + d + " off.", "En la vuelta más rápida de la clase " + who + " hizo " + r + " aquí. Te faltan " + d + "."),
                RadioLanguage.T("Zur Klassenbestzeit fehlen hier noch " + d + ", " + who + " hatte " + r + ".", "To the class best you're still " + d + " off here, " + who + " did " + r + ".", "Al mejor de la clase te faltan " + d + " aquí, " + who + " hizo " + r + "."),
                RadioLanguage.T("Vergleich mit " + who + " auf seiner Bestrunde: " + d + " langsamer.", "Compared with " + who + " on their best lap: " + d + " slower.", "Comparado con " + who + " en su mejor vuelta: " + d + " más lento."),
                RadioLanguage.T("Mit der Klassenbestzeit hatte " + who + " hier " + r + ", das sind noch " + d + " für dich.", "On the class best " + who + " had " + r + " here, that's " + d + " still to find.", "Con el mejor de la clase " + who + " marcó " + r + " aquí, te quedan " + d + "."),
                RadioLanguage.T("Noch " + d + " hinter " + who + " auf der schnellsten Runde der Klasse.", "Still " + d + " behind " + who + " on the class fastest lap.", "Aún " + d + " detrás de " + who + " en la vuelta más rápida de la clase."));
        }

        /// <summary>Gewinn gegenüber der bisherigen eigenen Sektorbestzeit, null ohne belegten Gewinn.</summary>
        internal static string SectorGainText(float gain)
        {
            if (!(gain >= .01f) || gain > 30) return null;
            string g = gain.ToString("0.000", RadioLanguage.Culture) + RadioLanguage.T(" Sekunden", " seconds", " segundos");
            return RadioVariants.Pick("sector-gain",
                RadioLanguage.T(g + " schneller als dein bisher bester Sektor.", g + " quicker than your previous best.", g + " más rápido que tu mejor anterior."),
                RadioLanguage.T("Das sind " + g + " Gewinn auf deine alte persönliche Bestmarke.", "That's " + g + " gained on your old mark.", "Son " + g + " ganados sobre tu marca anterior."),
                RadioLanguage.T("Verbessert um " + g + ".", "Improved by " + g + ".", "Mejorado en " + g + "."),
                RadioLanguage.T(g + " besser als vorher.", g + " better than before.", g + " mejor que antes."),
                RadioLanguage.T("Gegenüber deinem alten Bestsektor " + g + " gefunden.", g + " found on your previous best.", g + " encontrados respecto a tu mejor anterior."));
        }
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

        /// <summary>Größter Vorsprung auf die eigene Bestzeit nach zwei Sektoren, der im Rennen noch angesagt wird.</summary>
        internal const double MaxRacePaceDelta = 15;

        /// <summary>Hinweis „auf Bestzeitkurs“ im Rennen: Runde gültig, keine Gelbe Flagge, kein Zweikampf unter einer Sekunde.</summary>
        internal static bool RacePaceHintFits(GameStateData state)
        {
            var s = state?.SessionData;
            if (s == null || !s.CurrentLapIsValid || state.PitData.OnInLap || state.PitData.InPitlane || state.FlagData.isLocalYellow) return false;
            return !(Math.Abs(s.TimeDeltaFront) > 0 && Math.Abs(s.TimeDeltaFront) < 1f || Math.Abs(s.TimeDeltaBehind) > 0 && Math.Abs(s.TimeDeltaBehind) < 1f);
        }

        private bool ObservePersonalBestPace(GameStateData state, LapData lap, int sector, DateTime now)
        {
            // RaceRoom Monza 22.11. (Rob): „Du solltest ansagen, wenn ich meine besten Sektorzeiten fahre, dass ich auf meiner
            // schnellsten Runde sein könnte.“ In Runde 7 war Sektor 2 persönliche Bestzeit, die Runde wurde Bestzeit – Funk: nichts,
            // weil dieser Hinweis im Rennen abgeschaltet war. Jetzt auch im Rennen, nur nicht im engen Zweikampf.
            bool race = type == SessionType.Race;
            if (sector != 1 || !GoodSector(lap.SectorTimes[0]) || race && now - lastSectorBestCall < TimeSpan.FromSeconds(55)) return false;
            if (race && !RacePaceHintFits(state)) { LiveDiagnostics.Decision("own-best-pace", "skipped", "race_battle_or_invalid_lap"); return false; }
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
            // Im Rennen ist die erste Bestzeit oft die Startrunde aus dem Stand – dann liegt die schnelle Runde deutlich darunter.
            if (delta < .25 || delta > (race ? MaxRacePaceDelta : 3))
            { LiveDiagnostics.Decision("own-best-pace", "skipped", "delta_" + delta.ToString("0.00", CultureInfo.InvariantCulture)); return false; }
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
            bool race = type == SessionType.Race;
            int sector = -1;
            // Training/Qualifying: jede Verbesserung zählt (Sektor 1 und 2 kamen schon live, hier bleibt meist Sektor 3).
            float improvement = race ? .019f : .0009f;
            bool pb = false;
            for (int i = 0; i < completedSectorBest.Length; i++)
            {
                float measured = lap.SectorTimes[i], prior = completedSectorBest[i];
                if (!TrackAwareness.InRange(measured, 1, 3600).HasValue || measured > lap.LapTime) continue;
                float reference = race || prior > 0 ? prior : sectorSeed[i];
                if (reference > 0 && reference - measured > improvement)
                {
                    sector = i;
                    improvement = reference - measured;
                    pb = !GoodSector(sectorSeed[i]) || sectorSeed[i] - measured >= .001f;
                }
                if (prior <= 0 || measured < prior) completedSectorBest[i] = measured;
            }
            if (sector < 0 || !announce) return;
            if (race)
            {
                if (now - lastSectorBestCall < TimeSpan.FromSeconds(55)) return;
                OfferSectorBest(state, now, sector, lap.SectorTimes[sector], "valid_completed_player_lap_and_improved_recorded_sector", false, improvement);
                return;
            }
            string text = pb ? RadioVariants.Pick("sector-pb-" + sector, SessionBestPhrases.SectorPersonalBest(sector)) :
                RadioVariants.Pick("sector-improved-" + sector, SessionBestPhrases.SectorImproved(sector));
            string gained = SessionBestPhrases.Gain(improvement);
            if (gained != null) text += " " + gained;
            if (sessionSectorCount++ % 2 == 0 && !string.IsNullOrWhiteSpace(sectorDriverName))
                text = RadioPhraseCatalog.Address(text, sectorDriverName);
            var call = Add(state, now, "sector-best", text, pb ? "completed_lap_sector_personal_best" : "completed_lap_sector_faster_than_session_best", 4);
            call.AdditionalValidity = live => !live.PitData.InPitlane && !live.FlagData.isLocalYellow;
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
                RadioLanguage.T(name + " auf seiner Bestzeit", name + " on their best lap", name + " en su mejor vuelta");
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

        private int lastOutlapGapLap = -1, outlapGapVariant;

        /// <summary>Freie Bahn für die schnelle Runde? Auf der Aufwärmrunde im Qualifying (60–92 % der Runde) einmal den Abstand
        /// zu den Autos vor und hinter Rob ansagen. Nur mit gemessenem Zeitabstand auf der Strecke (gleicher Streckenpunkt).</summary>
        private void ObserveOutlapGap(GameStateData gs, TrackAwareness tracks, DateTime now)
        {
            var s = gs.SessionData;
            if (!QualifyingResultWatch.Supported(type) || tracks == null || !gs.PitData.OnOutLap || gs.PitData.InPitlane ||
                s.SessionPhase != SessionPhase.Green || lastOutlapGapLap == s.CompletedLaps) return;
            var snap = tracks.Snapshot;
            if (snap == null || snap.SessionId != s.SessionId || now < snap.Utc || now - snap.Utc > TimeSpan.FromSeconds(1) || !(snap.LengthM > 0) || !snap.PlayerDistanceM.HasValue) return;
            double progress = snap.PlayerDistanceM.Value / snap.LengthM.Value;
            if (progress < .6 || progress > .92) return;
            TrackCarPosition ahead = null, behind = null;
            foreach (var c in snap.Cars)
            {
                if (c.InPits || !c.SpatiallyConsistent || !c.RouteGapM.HasValue || !c.TrackIntervalSeconds.HasValue) continue;
                if (c.RouteGapM > 0 && (ahead == null || c.RouteGapM < ahead.RouteGapM)) ahead = c;
                if (c.RouteGapM < 0 && (behind == null || c.RouteGapM > behind.RouteGapM)) behind = c;
            }
            string text = OutlapGapText(ahead == null ? -1 : Math.Abs(ahead.TrackIntervalSeconds.Value), ahead != null && !ahead.OnOutLap && !ahead.OnInLap,
                behind == null ? -1 : Math.Abs(behind.TrackIntervalSeconds.Value), behind != null && !behind.OnOutLap && !behind.OnInLap, outlapGapVariant++);
            lastOutlapGapLap = s.CompletedLaps;
            if (text == null) return;
            var call = Add(gs, now, "quali-outlap-gap", text, "outlap_track_interval_to_nearest_cars_ahead_and_behind", 3);
            call.AdditionalValidity = live => live.PitData.OnOutLap && !live.PitData.InPitlane && !live.FlagData.isLocalYellow;
        }

        /// <summary>Ansage zur Lücke vor der schnellen Runde; null, wenn nichts Belastbares zu sagen ist.</summary>
        internal static string OutlapGapText(double ahead, bool aheadFlying, double behind, bool behindFlying, int variant)
        {
            // LMU 18.11.: „schnelles Auto hinten, 0 Sekunden“ – unter einer Sekunde mit Zehntel.
            string a = ahead > 0 ? RadioLanguage.Num(ahead, ahead < 1 ? "0.0" : "0") : null, b = behind > 0 ? RadioLanguage.Num(behind, behind < 1 ? "0.0" : "0") : null;
            if (behind > 0 && behind < 4 && behindFlying)
                return Of(variant,
                    RadioLanguage.T("Hinter dir kommt einer auf schneller Runde, " + b + " Sekunden. Lass ihn vorbei, dann hast du freie Bahn.", "Car behind on a flying lap, " + b + " seconds. Let him by, then you've got clear track.", "Detrás viene uno en vuelta rápida, a " + b + " segundos. Déjale pasar y tendrás pista libre."),
                    RadioLanguage.T("Achtung, schnelles Auto hinten, " + b + " Sekunden. Platz machen, nicht im Weg stehen.", "Heads up, fast car behind, " + b + " seconds. Make room, don't hold him up.", "Atención, coche rápido detrás, a " + b + " segundos. Hazle sitio, no le estorbes."),
                    RadioLanguage.T(b + " Sekunden hinter dir einer auf seiner schnellen Runde. Erst vorbeilassen, dann starten.", b + " seconds behind you, someone on a flying lap. Let him through, then start yours.", "A " + b + " segundos detrás, uno en su vuelta rápida. Déjale pasar y luego empieza la tuya."),
                    RadioLanguage.T("Schneller Verkehr von hinten, " + b + " Sekunden. Ideallinie freigeben.", "Fast traffic behind, " + b + " seconds. Give him the racing line.", "Tráfico rápido detrás, " + b + " segundos. Déjale la trazada."));
            if (ahead > 0 && ahead < 5)
                return aheadFlying ? Of(variant,
                    RadioLanguage.T("Vor dir nur " + a + " Sekunden, der ist auf schneller Runde. Etwas rausnehmen, Lücke schaffen.", "Only " + a + " seconds to the car ahead, he's on a flying lap. Back off a bit, make a gap.", "Solo " + a + " segundos al de delante, va en vuelta rápida. Levanta un poco, haz hueco."),
                    RadioLanguage.T(a + " Sekunden zum Vordermann, das ist zu knapp. Lass dir Luft für deine Runde.", a + " seconds to the car ahead, that's too close. Give yourself room for your lap.", a + " segundos al de delante, demasiado cerca. Date espacio para tu vuelta."),
                    RadioLanguage.T("Zu dicht dran, " + a + " Sekunden. Etwas Abstand, sonst fährst du in seiner Luftverwirbelung.", "Too close, " + a + " seconds. Drop back a little or you'll be in his dirty air.", "Demasiado cerca, " + a + " segundos. Deja algo de distancia o irás en su aire sucio."),
                    RadioLanguage.T("Vorne " + a + " Sekunden. Lücke auf mindestens fünf aufmachen.", "Ahead " + a + " seconds. Open the gap to at least five.", "Delante " + a + " segundos. Abre el hueco a por lo menos cinco.")) : Of(variant,
                    RadioLanguage.T("Vor dir einer auf der Aufwärmrunde, " + a + " Sekunden. Lücke schaffen, bevor du loslegst.", "Car ahead on his out lap, " + a + " seconds. Make a gap before you go.", "Delante uno en vuelta de salida, a " + a + " segundos. Haz hueco antes de empezar."),
                    RadioLanguage.T(a + " Sekunden zu einem langsamen Auto vorne. Zurückfallen lassen, freie Runde ist wichtiger.", a + " seconds to a slow car ahead. Drop back, a clear lap matters more.", a + " segundos a un coche lento delante. Quédate atrás, importa más una vuelta libre."),
                    RadioLanguage.T("Vorne bummelt einer, " + a + " Sekunden. Abstand halten, sonst kostet es dich die Runde.", "Someone's cruising ahead, " + a + " seconds. Keep your distance or it'll cost you the lap.", "Uno va despacio delante, a " + a + " segundos. Mantén distancia o te costará la vuelta."),
                    RadioLanguage.T("Verkehr vor dir, " + a + " Sekunden. Nimm etwas raus und such dir eine freie Lücke.", "Traffic ahead, " + a + " seconds. Ease off and find a clean gap.", "Tráfico delante, a " + a + " segundos. Levanta y busca un hueco libre."));
            if ((ahead < 0 || ahead >= 8) && (behind < 0 || behind >= 6) && (ahead > 0 || behind > 0))
                return Of(variant,
                    RadioLanguage.T("Gute Lücke für die schnelle Runde" + (a != null ? ", vorne " + a + " Sekunden" : "") + (b != null ? ", hinten " + b + " Sekunden" : "") + ".", "Good gap for the flying lap" + (a != null ? ", " + a + " seconds ahead" : "") + (b != null ? ", " + b + " seconds behind" : "") + ".", "Buen hueco para la vuelta rápida" + (a != null ? ", " + a + " segundos delante" : "") + (b != null ? ", " + b + " segundos detrás" : "") + "."),
                    RadioLanguage.T("Freie Bahn vor dir. Reifen und Bremsen vorbereiten, dann voll.", "Clear track ahead. Get tyres and brakes ready, then go flat out.", "Pista libre delante. Prepara neumáticos y frenos y luego a fondo."),
                    RadioLanguage.T("Die Lücke passt" + (a != null ? ", " + a + " Sekunden nach vorn" : "") + ". Das wird eine saubere Runde.", "The gap is good" + (a != null ? ", " + a + " seconds ahead" : "") + ". That'll be a clean lap.", "El hueco es bueno" + (a != null ? ", " + a + " segundos delante" : "") + ". Será una vuelta limpia."),
                    RadioLanguage.T("Platz nach vorn und hinten. Bereit machen für deine Runde.", "Room ahead and behind. Get ready for your lap.", "Espacio delante y detrás. Prepárate para tu vuelta."));
            return null;
        }

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

        /// <summary>
        /// Eigene Bestzeit für den Vergleich mit der schnellsten Runde im Feld. LMU Sebring 24.11.: „Deine Beste heute 12,5 dahinter“ –
        /// im Rennen war nur die Startrunde gefahren. Die Startrunde ist kein Maßstab; im Rennen zählt die eigene Beste erst ab der
        /// zweiten Runde. Ohne Vergleichsrunde nennt Ben nur die Zeit des Gegners.
        /// </summary>
        internal static float ComparableOwnBest(GameStateData gs)
        {
            if (gs == null) return -1;
            var s = gs.SessionData;
            if (s.SessionType == SessionType.Race && s.CompletedLaps < 2) return -1;
            return s.PlayerLapTimeSessionBest;
        }

        /// <summary>Die Sim übernimmt die Runde als Klassenbestzeit, und kein Gegner hält diese Zeit: Es ist Robs eigene Bestzeit.</summary>
        internal static bool SimCountsAsOwnBest(GameStateData gs, float lapTime)
        {
            if (gs == null || !Good(lapTime)) return false;
            var s = gs.SessionData;
            if (!Good(s.PlayerClassSessionBestLapTime) || Math.Abs(s.PlayerClassSessionBestLapTime - lapTime) >= .01f) return false;
            return !gs.OpponentData.Values.Any(o => o != null && Good(o.CurrentBestLapTime) && Math.Abs(o.CurrentBestLapTime - lapTime) < .01f);
        }

        private static LapRecord Confirmed(LapRecord lap) => new LapRecord
        {
            LapNumber = lap.LapNumber, LapTime = lap.LapTime, ClockLapTime = lap.ClockLapTime, Valid = true, Green = lap.Green,
            Position = lap.Position, FuelAtEnd = lap.FuelAtEnd, FuelUsed = lap.FuelUsed, TyreWear = lap.TyreWear, PeakTyreTemps = lap.PeakTyreTemps,
            SectorTimes = lap.SectorTimes, InPitLap = lap.InPitLap, RecordedUtc = lap.RecordedUtc
        };

        // Robs beste gültige Runde heute auf dieser Strecke, über alle Sessions (Hotlap, Training, Quali, Rennen).
        private static readonly object todayGate = new object();
        private static string todayKey;
        private static float todayBest = -1;

        private static string TodayKey(GameStateData gs)
        {
            var track = gs?.SessionData?.TrackDefinition?.name;
            return string.IsNullOrWhiteSpace(track) ? null : track + "|" + DateTime.UtcNow.ToLocalTime().Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static void RememberTodayBest(GameStateData gs, LapRecord lap)
        {
            if (lap == null || !lap.Valid || lap.InPitLap || !Good(lap.LapTime)) return;
            string key = TodayKey(gs);
            if (key == null) return;
            lock (todayGate)
            {
                if (key != todayKey) { todayKey = key; todayBest = -1; }
                if (!Good(todayBest) || lap.LapTime < todayBest) todayBest = lap.LapTime;
            }
        }

        /// <summary>Robs beste gültige Runde heute auf dieser Strecke (auch aus Hotlap, Training oder Quali), -1 = keine.</summary>
        internal static float TodayBest(GameStateData gs)
        {
            string key = TodayKey(gs);
            lock (todayGate) return key != null && key == todayKey ? todayBest : -1;
        }

        internal static void ResetTodayBest() { lock (todayGate) { todayKey = null; todayBest = -1; } }

        /// <summary>
        /// RaceRoom Salzburg 24.11.: Gegner-Bestzeiten im Rennen kamen ohne Vergleich, weil Robs erste Rennrunden gestrichen waren.
        /// Rob: „Kommt eine Bestzeit von einem Fahrer, muss ich die hören und wissen im Vergleich zu mir.“ Verglichen wird mit der
        /// besten Rennrunde, sonst mit Robs Bestzeit von heute (Training, Quali), sonst mit seiner letzten Runde.
        /// </summary>
        internal static string GapToOwnAndPush(float best, GameStateData gs, int variant)
        {
            float mine = ComparableOwnBest(gs);
            if (Good(mine)) return GapAndPush(best, mine, variant);
            string text = "";
            float today = TodayBest(gs);
            var s = gs?.SessionData;
            float last = s != null && (s.SessionType != SessionType.Race || s.CompletedLaps >= 2) ? s.LapTimePrevious : -1;
            if (Good(today) && today > best + .01f)
            {
                string gap = RadioLanguage.Num(today - best, today - best < 1 ? "0.00" : "0.0");
                text = RadioVariants.Pick("field-best-vs-today",
                    RadioLanguage.T("Deine Bestzeit heute liegt " + gap + " dahinter. ", "Your best today is " + gap + " slower. ", "Tu mejor tiempo de hoy está " + gap + " por detrás. "),
                    RadioLanguage.T("Zu deiner Bestzeit von heute fehlen " + gap + ". ", "That's " + gap + " quicker than your best today. ", "Son " + gap + " menos que tu mejor vuelta de hoy. "),
                    RadioLanguage.T("Verglichen mit deiner Bestzeit von heute: " + gap + " schneller. ", "Against your best today: " + gap + " quicker. ", "Comparado con tu mejor de hoy: " + gap + " más rápido. "),
                    RadioLanguage.T("Deine schnellste Runde heute ist " + gap + " langsamer. ", "Your quickest lap today is " + gap + " slower. ", "Tu vuelta más rápida de hoy es " + gap + " más lenta. "),
                    RadioLanguage.T("Auf deine Bestzeit von heute sind das " + gap + ". ", "That's " + gap + " up on your best today. ", "Eso son " + gap + " sobre tu mejor de hoy. "));
            }
            else if (Good(last) && last > best + .01f)
            {
                string gap = RadioLanguage.Num(last - best, last - best < 1 ? "0.00" : "0.0");
                text = RadioVariants.Pick("field-best-vs-last",
                    RadioLanguage.T("Deine letzte Runde war " + gap + " langsamer. ", "Your last lap was " + gap + " slower. ", "Tu última vuelta fue " + gap + " más lenta. "),
                    RadioLanguage.T("Auf deine letzte Runde sind das " + gap + ". ", "That's " + gap + " up on your last lap. ", "Son " + gap + " sobre tu última vuelta. "),
                    RadioLanguage.T("Deine letzte Zeit liegt " + gap + " dahinter. ", "Your last time is " + gap + " behind that. ", "Tu último tiempo está " + gap + " por detrás. "),
                    RadioLanguage.T("Verglichen mit deiner letzten Runde: " + gap + " schneller. ", "Compared with your last lap: " + gap + " quicker. ", "Comparado con tu última vuelta: " + gap + " más rápido. "),
                    RadioLanguage.T("Zu deiner letzten Runde fehlen " + gap + ". ", "You're " + gap + " off that with your last lap. ", "A tu última vuelta le faltan " + gap + ". "));
            }
            return text + GapAndPush(best, -1, variant);
        }

        /// <summary>
        /// RaceRoom Salzburg 24.11. (Rob: „so schnell wie möglich brauche ich meinen Verbrauch, wenigstens gut geschätzt“): Im Training
        /// kam der Verbrauch erst auf Nachfrage. Nach gut einem Sechstel Runde ohne Box sagt Mik eine erste Schätzung an.
        /// </summary>
        private void OfferEarlyFuel(GameStateData gs, DateTime now, bool race, bool green, AiRaceMemory fuelHistory)
        {
            if (race || !green || earlyFuelOffered || firstFuelOffered || fuelHistory == null || gs.PitData.InPitlane) return;
            float length = gs.SessionData.TrackDefinition == null ? 0 : gs.SessionData.TrackDefinition.trackLength;
            float estimate = fuelHistory.ProvisionalFuelPerLap(gs, out float metres);
            if (!(length > 500) || metres < Math.Max(EarlyFuelMinimumMetres, length * .15f) || !TrackAwareness.InRange(estimate, .05, 50).HasValue) return;
            earlyFuelOffered = true;
            string used = FuelWatch.Litres(estimate);
            var call = Add(gs, now, "fuel-early", EarlyFuelText(used), "provisional_fuel_from_tank_and_distance_" + (int)metres + "m", 4);
            call.AdditionalValidity = live => live.FuelData.FuelUseActive && !live.PitData.InPitlane;
        }

        internal const float EarlyFuelMinimumMetres = 400;

        internal static string EarlyFuelText(string used) => RadioVariants.Pick("fuel-early",
            RadioLanguage.T("Erste Schätzung Verbrauch: etwa " + used + " pro Runde. Genauer nach der ersten vollen Runde.",
                "First fuel estimate: about " + used + " per lap. More precise after the first full lap.",
                "Primera estimación de consumo: unos " + used + " por vuelta. Más preciso tras la primera vuelta completa."),
            RadioLanguage.T("Verbrauch vorläufig: rund " + used + " je Runde, aus Tank und Strecke gerechnet.",
                "Provisional fuel: around " + used + " a lap, from tank and distance.",
                "Consumo provisional: unos " + used + " por vuelta, calculado con depósito y distancia."),
            RadioLanguage.T("Hochrechnung Sprit: ungefähr " + used + " pro Runde. Die volle Runde bestätigt es.",
                "Fuel projection: roughly " + used + " per lap. The full lap will confirm it.",
                "Proyección de combustible: aproximadamente " + used + " por vuelta. La vuelta completa lo confirmará."),
            RadioLanguage.T("Schätzung steht: etwa " + used + " Sprit pro Runde.",
                "Estimate's in: about " + used + " of fuel per lap.",
                "Ya hay estimación: unos " + used + " de combustible por vuelta."),
            RadioLanguage.T("Erster Wert für den Verbrauch: ungefähr " + used + " pro Runde, noch vorläufig.",
                "First consumption figure: roughly " + used + " per lap, still provisional.",
                "Primer dato de consumo: aproximadamente " + used + " por vuelta, todavía provisional."));

        /// <summary>Eigener Rückstand auf die schnellste Runde (falls bekannt) und ein Ansporn.</summary>
        internal static string GapAndPush(float best, float mine, int variant)
        {
            string text = "";
            if (Good(mine) && mine > best + .01f)
            {
                string gap = RadioLanguage.Num(mine - best, mine - best < 1 ? "0.00" : "0.0");
                // Rob 23.11.: Bestzeiten im Feld immer gegen seine eigene beste Runde in diesem Rennen, nie zweimal gleich gesagt.
                text = RadioVariants.Pick("field-best-vs-own",
                    RadioLanguage.T("Dir fehlen " + gap + " Sekunden. ", "You're " + gap + " seconds off. ", "Te faltan " + gap + " segundos. "),
                    RadioLanguage.T("Deine Beste heute liegt " + gap + " dahinter. ", "Your best today is " + gap + " behind that. ", "Tu mejor vuelta de hoy está " + gap + " por detrás. "),
                    RadioLanguage.T("Das sind " + gap + " auf deine schnellste Runde. ", "That's " + gap + " quicker than your fastest lap. ", "Son " + gap + " menos que tu vuelta más rápida. "),
                    RadioLanguage.T("Zu deiner eigenen Bestzeit fehlen " + gap + ". ", "Compared with your own best, that's " + gap + " quicker. ", "Respecto a tu mejor tiempo, son " + gap + " menos. "),
                    RadioLanguage.T("Deine schnellste Runde ist " + gap + " langsamer. ", "Your quickest lap is " + gap + " slower. ", "Tu vuelta más rápida es " + gap + " más lenta. "));
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

        private int raceLapVariant;

        /// <summary>Selbst gestoppte Zeit auf Zehntel, damit sie nicht genauer klingt, als sie ist („1:47,7“).</summary>
        internal static string ApproxLapTime(float seconds)
        {
            long tenths = (long)Math.Round(seconds * 10.0, MidpointRounding.AwayFromZero);
            long minutes = tenths / 600;
            decimal rest = (tenths % 600) / 10m;
            return minutes > 0 ? minutes + ":" + rest.ToString("00.0", RadioLanguage.Culture) : rest.ToString("0.0", RadioLanguage.Culture);
        }

        internal static string Delta(float seconds) =>
            RadioLanguage.Num(seconds, seconds < 1 ? "0.00" : "0.0") + RadioLanguage.T(" Sekunden", " seconds", " segundos");

        /// <summary>Ein Satz zur gerade beendeten Rennrunde; null, wenn es nichts Ehrliches zu sagen gibt.</summary>
        internal static string RaceLapComment(bool valid, float lapTime, float clockTime, float bestBefore, int variant)
        {
            string[] set; string a = null, b = null;
            if (!valid)
            {
                if (Good(lapTime))
                {
                    a = LocalAnswers.LapTime(lapTime);
                    set = RadioLanguage.Pick(
                        new[] { "Runde gestrichen, die {0} zählt nicht.", "Die {0} wird nicht gewertet, die Runde ist gestrichen.",
                            "Gestrichen. {0} wäre es gewesen.", "Die Runde fällt raus, {0} ohne Wertung.", "{0}, aber gestrichen. Die nächste sauber halten." },
                        new[] { "Lap deleted, the {0} doesn't count.", "The {0} won't be counted, that lap's deleted.",
                            "Deleted. It would have been a {0}.", "That lap's out, {0} with no credit.", "{0}, but deleted. Keep the next one clean." },
                        new[] { "Vuelta anulada, el {0} no cuenta.", "El {0} no se cuenta, la vuelta está anulada.",
                            "Anulada. Habría sido un {0}.", "Esa vuelta no vale, {0} sin contar.", "{0}, pero anulada. La próxima, limpia." });
                }
                else if (Good(clockTime))
                {
                    a = ApproxLapTime(clockTime);
                    set = RadioLanguage.Pick(
                        new[] { "Runde gestrichen, auf unserer Uhr etwa {0}.", "Gestrichen, keine offizielle Zeit. Wir haben ungefähr {0} gestoppt.",
                            "Die Runde zählt nicht. Gestoppt etwa {0}.", "Ohne Wertung, nach unserer Uhr rund {0}.", "Gestrichene Runde, ungefähr {0}." },
                        new[] { "Lap deleted, our clock says about {0}.", "Deleted, no official time. We had it at roughly {0}.",
                            "That lap doesn't count. Timed at about {0}.", "No credit, around {0} on our watch.", "Deleted lap, roughly {0}." },
                        new[] { "Vuelta anulada, en nuestro reloj unos {0}.", "Anulada, sin tiempo oficial. La cronometramos en unos {0}.",
                            "Esa vuelta no cuenta. Cronometrada en unos {0}.", "Sin validez, por nuestro reloj alrededor de {0}.", "Vuelta anulada, aproximadamente {0}." });
                }
                else
                    set = RadioLanguage.Pick(
                        new[] { "Runde gestrichen, keine Zeit gewertet.", "Die Runde ist gestrichen, die Zeit zählt nicht.",
                            "Gestrichen. Die nächste Runde sauber halten.", "Keine Wertung für diese Runde.", "Die Runde fällt raus, ohne Zeit." },
                        new[] { "Lap deleted, no time counted.", "That lap's deleted, the time doesn't count.",
                            "Deleted. Keep the next lap clean.", "No credit for that lap.", "That lap's out, no time." },
                        new[] { "Vuelta anulada, sin tiempo.", "La vuelta está anulada, el tiempo no cuenta.",
                            "Anulada. Mantén limpia la próxima.", "Esa vuelta no puntúa.", "Esa vuelta no vale, sin tiempo." });
            }
            else
            {
                if (!Good(lapTime)) return null;
                a = LocalAnswers.LapTime(lapTime);
                float gap = Good(bestBefore) ? lapTime - bestBefore : float.NaN;
                if (float.IsNaN(gap))
                    set = RadioLanguage.Pick(
                        new[] { "Runde in {0}.", "{0} für diese Runde.", "Die Runde: {0}.", "Gestoppt: {0}.", "{0} an der Linie." },
                        new[] { "Lap in {0}.", "{0} for that lap.", "That lap: {0}.", "Timed: {0}.", "{0} at the line." },
                        new[] { "Vuelta en {0}.", "{0} en esta vuelta.", "La vuelta: {0}.", "Cronometrado: {0}.", "{0} en la línea." });
                else if (gap <= .01f) return null; // Bestzeit oder gleich schnell: das übernimmt die Bestzeit-Ansage
                else
                {
                    b = Delta(gap);
                    set = gap <= .3f ? RadioLanguage.Pick(
                        new[] { "{0}, nur {1} an der Bestzeit vorbei.", "Knapp: {0}, {1} über deinem Besten.", "{0}. Die Bestzeit ist nur {1} weg.",
                            "Starke Runde, {0}, {1} hinter deiner Bestzeit.", "{0}, ganz nah dran, {1} fehlen." },
                        new[] { "{0}, just {1} off your best.", "Close: {0}, {1} above your best.", "{0}. Your best is only {1} away.",
                            "Strong lap, {0}, {1} behind your best.", "{0}, right there, {1} short." },
                        new[] { "{0}, solo {1} por encima de tu mejor.", "Cerca: {0}, {1} más que tu mejor.", "{0}. Tu mejor está a solo {1}.",
                            "Buena vuelta, {0}, {1} detrás de tu mejor.", "{0}, muy cerca, faltan {1}." }) : RadioLanguage.Pick(
                        new[] { "{0}, {1} über deiner Bestzeit.", "Runde in {0}. Auf die Bestzeit fehlen {1}.", "{0} gefahren, {1} langsamer als dein Bestes.",
                            "{0}, Abstand zur Bestzeit {1}.", "Die Runde: {0}, {1} hinter deiner Bestzeit." },
                        new[] { "{0}, {1} off your best.", "Lap in {0}. {1} short of your best.", "{0}, {1} slower than your best.",
                            "{0}, gap to your best {1}.", "That lap: {0}, {1} behind your best." },
                        new[] { "{0}, {1} por encima de tu mejor.", "Vuelta en {0}. Te faltan {1} para tu mejor.", "{0}, {1} más lenta que tu mejor.",
                            "{0}, diferencia con tu mejor {1}.", "La vuelta: {0}, {1} detrás de tu mejor." });
                }
            }
            string line = set[((variant % set.Length) + set.Length) % set.Length];
            return line.Replace("{0}", a ?? "").Replace("{1}", b ?? "");
        }
        /// <summary>Verbesserung im Rennen: beste Rennrunde, nicht „persönliche Bestzeit“ (die kann aus Training oder Quali stammen).</summary>
        internal static string RaceBestLine(string time, int variant)
        {
            string[] de = { "Beste Rennrunde bisher: {0}.", "{0}, deine schnellste Runde in diesem Rennen.", "Schneller geworden, {0}. Beste Runde im Rennen.",
                "Da ist deine beste Rennrunde: {0}.", "{0}. So schnell warst du in diesem Rennen noch nicht.", "Verbessert auf {0}, Rennbestzeit." };
            string[] en = { "Best race lap so far: {0}.", "{0}, your fastest lap of this race.", "Quicker again, {0}. Best lap of the race.",
                "There's your best race lap: {0}.", "{0}. Your quickest yet in this race.", "Improved to {0}, your race best." };
            string[] es = { "Mejor vuelta de carrera hasta ahora: {0}.", "{0}, tu vuelta más rápida en esta carrera.", "Más rápido otra vez, {0}. Mejor vuelta de la carrera.",
                "Ahí está tu mejor vuelta de carrera: {0}.", "{0}. Nunca habías ido tan rápido en esta carrera.", "Mejoras a {0}, tu mejor de carrera." };
            string[] set = RadioLanguage.Current == RadioLanguage.English ? en : RadioLanguage.Current == RadioLanguage.Spanish ? es : de;
            return set[((variant % set.Length) + set.Length) % set.Length].Replace("{0}", time);
        }

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
                    topic == "own-lap" ? 22 : topic == "race-lap" ? 20 : topic == "first-lap-fuel" || topic == "fuel-baseline" || topic == "fuel-early" ? 30 :
                    topic == "qualifying-last" ? 7 : topic == "quali-target" ? 15 :
                    // Die Sektorbestzeit darf im Rennen auf eine Lücke im Funk warten (LMU Sebring 24.11.: dreimal nach 8 s verfallen).
                    topic == "sector-best" && type == SessionType.Race ? 20 : type == SessionType.Race ? 8 : 12) };
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
                c.Topic == "first-lap-fuel" || c.Topic == "fuel-baseline" || c.Topic == "fuel-early" ||
                (c.Topic == "race-lap" || c.Topic == "rival-best") && now - lastRadio >= TimeSpan.FromSeconds(3) ||
                // Rob 24.11.: Sektor-Bestzeiten im Training/Qualifying kommen sofort, nicht erst nach acht Sekunden Funkstille.
                type != SessionType.Race && (c.Topic == "sector-best" || c.Topic == "own-best-pace") && now - lastRadio >= TimeSpan.FromSeconds(2) || !ownLapOnly &&
                (c.Topic == "near-best" || c.Topic == "lap-on-pace" || c.Topic == "quali-target" && now - lastRadio >= TimeSpan.FromSeconds(3) || !routineBusy && now - lastRadio >= TimeSpan.FromSeconds(8)))).OrderByDescending(c => c.Priority).FirstOrDefault();
            return call != null && call.TryReserve(now) ? call : null;
        }
    }

    /// <summary>
    /// Rob 24.11. (AMS2 Cascais, Training): „In der Quali und Practice immer eine kurze Ansage – Bestzeit im ersten Sektor, auch im
    /// zweiten, das könnte deine schnellste Runde werden. Bei der schnellsten Runde kurzes Lob und Motivation.“ Kurze Sätze, je fünf
    /// Formulierungen in DE/EN/ES. „Persönliche Bestzeit“ nur, wenn die Zeit auch die beim Einstieg mitgelieferte Bestmarke schlägt;
    /// sonst heißt es „verbessert“.
    /// </summary>
    internal static class SessionBestPhrases
    {
        private static string Ordinal(int sector) => sector == 0 ? RadioLanguage.T("ersten", "first", "primer") :
            sector == 1 ? RadioLanguage.T("zweiten", "second", "segundo") : RadioLanguage.T("dritten", "third", "tercer");
        private static string Word(int sector) => sector == 0 ? RadioLanguage.T("1", "one", "uno") :
            sector == 1 ? RadioLanguage.T("2", "two", "dos") : RadioLanguage.T("3", "three", "tres");

        /// <summary>Persönliche Bestzeit in einem Sektor (kurz).</summary>
        internal static string[] SectorPersonalBest(int sector)
        {
            string o = Ordinal(sector), w = Word(sector);
            return RadioLanguage.Pick(
                new[] { "Persönliche Bestzeit im " + o + " Sektor.", "Sektor " + w + ": persönliche Bestzeit!", "Sektor " + w + " so schnell wie noch nie.",
                    "Persönliche Bestmarke in Sektor " + w + ". Weiter so.", "Im " + o + " Sektor persönliche Bestzeit. Stark!" },
                new[] { "Personal best in sector " + w + ".", "Sector " + w + ": personal best!", "Sector " + w + ", quicker than ever.",
                    "Personal best mark in sector " + w + ". Keep going.", "Personal best in the " + o + " sector. Strong!" },
                new[] { "Mejor marca personal en el " + o + " sector.", "Sector " + w + ": ¡mejor marca personal!", "Sector " + w + ", más rápido que nunca.",
                    "Mejor marca personal en el sector " + w + ". Sigue así.", "En el " + o + " sector, mejor marca personal. ¡Fuerte!" });
        }

        /// <summary>Schneller als alles seit dem Einstieg, aber nicht unter der mitgelieferten Bestmarke.</summary>
        internal static string[] SectorImproved(int sector)
        {
            string o = Ordinal(sector), w = Word(sector);
            return RadioLanguage.Pick(
                new[] { "Verbessert im " + o + " Sektor.", "Sektor " + w + " schneller als zuletzt.", "Im " + o + " Sektor wieder Zeit gefunden.",
                    "Sektor " + w + " besser als die Runden davor.", "Sektor " + w + " geht nach vorn. Gut so." },
                new[] { "Improved in sector " + w + ".", "Sector " + w + " quicker than last time.", "Found time again in sector " + w + ".",
                    "Sector " + w + " better than the laps before.", "Sector " + w + " is coming. Good." },
                new[] { "Mejoras en el " + o + " sector.", "Sector " + w + " más rápido que la última vez.", "Has vuelto a encontrar tiempo en el sector " + w + ".",
                    "Sector " + w + " mejor que las vueltas anteriores.", "El sector " + w + " va a mejor. Bien." });
        }

        /// <summary>Sektor 2 persönliche Bestzeit, nachdem Sektor 1 schon als persönliche Bestzeit zu hören war.</summary>
        internal static string[] SecondAfterFirstBest => RadioLanguage.Pick(
            new[] { "Auch im zweiten Sektor persönliche Bestzeit, das könnte deine schnellste Runde werden!",
                "Sektor 2 auch persönliche Bestzeit! Das kann deine schnellste Runde werden.",
                "Zweimal persönliche Bestzeit! Die Runde könnte deine schnellste werden, sauber bleiben.",
                "Und Sektor 2 legt nach, persönliche Bestzeit. Da kommt vielleicht deine schnellste Runde.",
                "Auch der zweite Sektor ist persönliche Bestzeit. Bring die Runde heim, das könnte die schnellste werden." },
            new[] { "Personal best in sector two as well, this could be your fastest lap!",
                "Sector two is a personal best too! This can be your fastest lap.",
                "Two personal best sectors! This could be your quickest lap, keep it clean.",
                "And sector two follows up, personal best. Your fastest lap might be coming.",
                "Sector two is a personal best as well. Bring it home, this could be the fastest." },
            new[] { "¡También mejor marca personal en el segundo sector, esta puede ser tu vuelta más rápida!",
                "¡El sector dos también es mejor marca personal! Esta puede ser tu vuelta más rápida.",
                "¡Dos sectores con mejor marca personal! Puede ser tu vuelta más rápida, mantenla limpia.",
                "Y el sector dos también, mejor marca personal. Puede venir tu vuelta más rápida.",
                "El segundo sector también es mejor marca personal. Tráela a casa, puede ser la más rápida." });

        /// <summary>Sektor 2 verbessert, nachdem Sektor 1 schon verbessert war (die mitgelieferte Bestmarke ist noch schneller).</summary>
        internal static string[] SecondAfterFirstImproved => RadioLanguage.Pick(
            new[] { "Auch Sektor 2 verbessert, die Runde läuft richtig gut.", "Sektor 2 legt auch zu. Starke Runde bis hier.",
                "Zweimal verbessert, jetzt sauber zu Ende fahren.", "Auch im zweiten Sektor schneller. Die Runde sieht gut aus.",
                "Sektor 2 ebenfalls besser. Bleib dran, das wird eine gute Runde." },
            new[] { "Sector two improved as well, this lap is going really well.", "Sector two picks up too. Strong lap so far.",
                "Two improvements, now finish it cleanly.", "Quicker in sector two as well. This lap looks good.",
                "Sector two better too. Stay with it, this will be a good lap." },
            new[] { "También mejoras en el sector dos, la vuelta va muy bien.", "El sector dos también mejora. Vuelta fuerte hasta aquí.",
                "Dos mejoras, ahora termínala limpia.", "También más rápido en el segundo sector. La vuelta pinta bien.",
                "El sector dos también mejor. Sigue así, será una buena vuelta." });

        /// <summary>Lila: schneller als jede Bestzeit der Klasse (Zusatz).</summary>
        internal static string[] PurpleSuffix => RadioLanguage.Pick(
            new[] { "Und das ist lila, schneller als alle in deiner Klasse.", "Lila, da war noch keiner schneller.", "Das ist sogar die schnellste Zeit der Klasse.",
                "Schnellster der Klasse in diesem Sektor.", "Lila Sektor!" },
            new[] { "And that's purple, quicker than everyone in your class.", "Purple, nobody has been quicker there.", "That's even the fastest time in class.",
                "Quickest in class in that sector.", "Purple sector!" },
            new[] { "Y es morado, más rápido que todos en tu clase.", "Morado, nadie ha sido más rápido ahí.", "Es incluso el mejor tiempo de la clase.",
                "El más rápido de la clase en ese sector.", "¡Sector morado!" });

        /// <summary>Kurzer Gewinn gegenüber der bisherigen Marke, null ohne belegten Gewinn.</summary>
        internal static string Gain(float gain)
        {
            if (!(gain >= .01f) || gain > 30) return null;
            string g = gain.ToString("0.000", RadioLanguage.Culture);
            return RadioLanguage.T(g + " schneller.", g + " quicker.", g + " más rápido.");
        }

        /// <summary>Kurzes Lob und Motivation nach einer neuen persönlichen Bestzeit im Training oder Qualifying.</summary>
        internal static string[] LapBestMotivation => RadioLanguage.Pick(
            new[] { "Stark gefahren! Da geht vielleicht noch mehr.", "Sauber! Jetzt die nächste genauso.", "Klasse Runde. Bleib in dem Rhythmus.",
                "Sehr gut! Halt den Schwung, das war noch nicht das Ende.", "Top! Die kannst du noch einmal unterbieten." },
            new[] { "Strong driving! There might be even more in it.", "Clean! Now the next one just like that.", "Great lap. Stay in that rhythm.",
                "Very good! Keep the momentum, that's not the end of it.", "Top! You can go under that again." },
            new[] { "¡Muy bien conducido! Puede que haya aún más.", "¡Limpio! Ahora la siguiente igual.", "Gran vuelta. Mantén ese ritmo.",
                "¡Muy bien! Mantén el impulso, esto no se acaba aquí.", "¡Genial! Puedes bajar de ahí otra vez." });

        /// <summary>Schneller als alle Runden seit dem Einstieg, aber noch über der mitgelieferten persönlichen Bestzeit.</summary>
        internal static string[] LapImproved(string time, string gap) => RadioLanguage.Pick(
            new[] { time + ". Schneller als deine Runden davor, noch " + gap + " auf deine persönliche Bestzeit.",
                "Verbessert auf " + time + ". Bis zur persönlichen Bestzeit fehlen noch " + gap + ".",
                time + ", wieder ein Stück schneller. " + gap + " fehlen noch auf deine persönliche Bestmarke.",
                "Gute Runde, " + time + ". Du kommst an deine persönliche Bestzeit ran, noch " + gap + ".",
                "Da geht es nach vorn: " + time + ". Noch " + gap + " bis zu deiner persönlichen Bestzeit." },
            new[] { time + ". Quicker than your laps before, " + gap + " still to your personal best.",
                "Improved to " + time + ". " + gap + " still missing to your personal best.",
                time + ", a bit quicker again. " + gap + " still to your personal best mark.",
                "Good lap, " + time + ". You're closing in on your personal best, " + gap + " to go.",
                "Moving forward: " + time + ". " + gap + " to your personal best." },
            new[] { time + ". Más rápido que tus vueltas anteriores, aún " + gap + " a tu mejor marca personal.",
                "Mejoras a " + time + ". Te faltan " + gap + " para tu mejor marca personal.",
                time + ", otra vez un poco más rápido. Faltan " + gap + " para tu mejor marca personal.",
                "Buena vuelta, " + time + ". Te acercas a tu mejor marca personal, faltan " + gap + ".",
                "Vas a mejor: " + time + ". Aún " + gap + " hasta tu mejor marca personal." });
    }
}
