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
        internal static string TimedLapStartPhrase => RadioLanguage.T("Zeit läuft. Die erste gezeitete Runde beginnt, bleib bei deinem Rhythmus.",
            "Clock's running. First timed lap starts now, keep your rhythm.",
            "Corre el tiempo. Empieza la primera vuelta cronometrada, mantén tu ritmo.");
        internal static string TimedLapApproachPhrase => RadioLanguage.T("Gleich beginnt deine erste schnelle Runde. Konzentrier dich auf den Ausgang.",
            "Your first flying lap is coming up. Focus on the exit.",
            "Ya llega tu primera vuelta rápida. Concéntrate en la salida de curva.");
        private bool firstTimedLapCalled, lastQualifyingLapOffered, firstFuelOffered;
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
            lastRadio = lastSectorBestCall = DateTime.MinValue;
            firstTimedLapCalled = lastQualifyingLapOffered = firstFuelOffered = false;
            for (int i = 0; i < completedSectorBest.Length; i++) completedSectorBest[i] = -1;
        }

        public static bool Supported(SessionType t)
        { return t == SessionType.Practice || t == SessionType.LonePractice || t == SessionType.Qualify || t == SessionType.PrivateQualify || t == SessionType.HotLap; }

        public void Observe(GameStateData gs, LapRecord completed, DateTime now, bool names, GameStateData previous = null,
            bool sectorRadio = true, TrackAwareness tracks = null, string driverName = "Rob")
        {
            if (gs == null) return;
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
                Add(gs, now, "timed-lap-approach", TimedLapApproachPhrase, "confirmed_qualifying_outlap_approaching_line", 4);
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
                Add(gs, now, "timed-lap-start", TimedLapStartPhrase,
                    "mapped_outlap_end_new_lap_and_start_finish_crossing", 3);
            }
            bool finalQualifyingLap = QualifyingResultWatch.Supported(type) && previous?.SessionData.SessionId == session &&
                previous.SessionData.SessionPhase == SessionPhase.Green && (s.SessionPhase == SessionPhase.Checkered ||
                green && s.IsNewLap && s.SessionLapsRemaining == 1 && !s.SessionHasFixedTime);
            if (!lastQualifyingLapOffered && finalQualifyingLap && !gs.PitData.InPitlane && !gs.PitData.OnOutLap &&
                s.CurrentLapIsValid && gs.PositionAndMotionData.CarSpeed > 12 && s.CompletedLaps > 0)
            {
                lastQualifyingLapOffered = true;
                string name = AiPersonas.CleanForRadio(driverName ?? "Rob", 30, false);
                string[] push = RadioLanguage.Pick(
                    new[] { "Letzte schnelle Runde. Hol raus, was geht.", "Letzte Chance. Sauber durchziehen.", "Eine Runde noch, volle Konzentration." },
                    new[] { "Last flying lap. Get everything out of it.", "Last chance. Make it clean.", "One more lap, full focus." },
                    new[] { "Última vuelta rápida. Sácale todo.", "Última oportunidad. Hazla limpia.", "Una vuelta más, concentración total." });
                var last = Add(gs, now, "qualifying-last", name + ", " + push[qualiLastVariation.Next(push.Length)],
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
                    Add(gs, now, "first-lap-fuel", RadioLanguage.T("Erste volle Runde, " + FuelWatch.Litres(completed.FuelUsed) + " verbraucht.",
                        "First full lap, " + FuelWatch.Litres(completed.FuelUsed) + " used.",
                        "Primera vuelta completa, " + FuelWatch.Litres(completed.FuelUsed) + " gastados."), "measured_first_full_lap_fuel", 4);
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
                            (classRecord ? RadioLanguage.T(" Klassenbestzeit an der Linie.", " That's the class best!", " ¡Es el mejor tiempo de la clase!") : "");
                        // The recorded lap remains true when standings or another driver's best changes.
                        // Fresh classification and gaps belong to the separate timing reports.
                        Add(gs, now, "own-lap", text, first ? "first_valid_lap" : "personal_best_improved", 5,
                            variants, group, heard);
                        ownCall = true;
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
            if (!s.SectorTimingEstimated && completed?.SectorTimes?.Length == 3 && completed.Valid && !completed.InPitLap && Good(completed.LapTime) &&
                completed.LapNumber == s.CompletedLaps && s.IsNewLap &&
                Math.Abs(completed.SectorTimes.Sum() - completed.LapTime) <= 1)
                ObserveCompletedSectorBest(gs, completed, now, green && sectorRadio && !ownCall);
            if (sectorRadio && green && !ownCall && !s.SectorTimingEstimated)
            {
                if (TryLiveSector(gs, previous, now, out var lap, out int sector) &&
                    (ObserveLivePersonalBest(gs, previous, lap, sector, now) ||
                    ObservePersonalBestPace(gs, lap, sector, now))) return;
                if (QualifyingResultWatch.Supported(type))
                    ObserveQualifyingSector(gs, previous, now, names, tracks);
            }
        }

        private static bool TryLiveSector(GameStateData state, GameStateData previous, DateTime now, out LapData lap, out int sector)
        {
            var s = state.SessionData;
            var before = previous?.SessionData;
            sector = s.SectorNumber - 2;
            lap = null;
            if (before == null || sector < 0 || sector > 1 || before.SessionId != s.SessionId ||
                before.SessionType != s.SessionType || before.SessionPhase != SessionPhase.Green ||
                before.CompletedLaps != s.CompletedLaps || before.SectorNumber + 1 != s.SectorNumber ||
                s.IsNewLap || s.SectorTimingEstimated || before.SectorTimingEstimated || !s.CurrentLapIsValid ||
                state.PitData.InPitlane || state.PitData.OnOutLap ||
                state.PitData.OnInLap || state.FlagData.isLocalYellow) return false;
            DateTime source = state.SourceTelemetryUtc == DateTime.MinValue ? state.Now : state.SourceTelemetryUtc;
            if (now < source || now - source > TimeSpan.FromSeconds(1)) return false;
            if (previous.SourceTelemetryUtc != DateTime.MinValue && source <= previous.SourceTelemetryUtc) return false;
            lap = s.PlayerLapData.LastOrDefault();
            return lap != null && lap.LapNumber == s.CompletedLaps + 1 && lap.IsValid && !lap.InLap && !lap.OutLap &&
                lap.SectorTimes != null && lap.SectorTimes.Length > sector && GoodSector(lap.SectorTimes[sector]);
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
                if (!GoodSector(reference) || reference - time < .04f) return false;
                evidence = "mapped_valid_sector_and_prior_personal_best";
            }
            completedSectorBest[sector] = prior > 0 ? Math.Min(prior, time) : time;
            if (now - lastSectorBestCall < TimeSpan.FromSeconds(55)) return false;
            var call = OfferSectorBest(state, now, sector, time, evidence);
            int currentSector = state.SessionData.SectorNumber;
            call.AdditionalValidity = live => live.SessionData.CurrentLapIsValid && !live.SessionData.SectorTimingEstimated &&
                live.SessionData.SectorNumber == currentSector &&
                !live.PitData.OnInLap && !live.PitData.OnOutLap && !live.FlagData.isLocalYellow;
            return true;
        }

        private ObservedRaceCall OfferSectorBest(GameStateData state, DateTime now, int sector, float seconds, string evidence)
        {
            var catalog = RadioPhraseCatalog.Current;
            int variant = SelectVariant("own-sector-best", sectorBestVariants);
            string time = seconds.ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));
            return Add(state, now, "sector-best", catalog.Format("own-sector-best", variant,
                (sector + 1).ToString(CultureInfo.InvariantCulture), time), evidence, 3, sectorBestVariants,
                "own-sector-best", variant);
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
            if (leaders.Length != 1) return;
            var leader = leaders[0];
            if (tracks != null && !tracks.OpponentFresh(new RaceDataContext { GameState = state, NowUtc = now,
                LastTelemetryUtc = state.SourceTelemetryUtc == DateTime.MinValue ? state.Now : state.SourceTelemetryUtc }, leader.Value)) return;
            var best = leader.Value.OpponentLapData.LastOrDefault(lap => lap.IsValid && !lap.InLap && !lap.OutLap &&
                Good(lap.LapTime) && Math.Abs(lap.LapTime - leader.Value.CurrentBestLapTime) < .001f);
            if (best?.SectorTimes == null || sector >= best.SectorTimes.Length ||
                !TrackAwareness.InRange(best.SectorTimes[sector], 1, 3600).HasValue) return;
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
            return text + push[(int)((uint)variant % push.Length)];
        }

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
                    topic == "own-lap" && type != SessionType.Race ? 18 : topic == "qualifying-last" ? 7 : type == SessionType.Race ? 8 : 12) };
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
                if ((delivered.Topic == "sector-best" || delivered.Topic == "own-best-pace") &&
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
                c.Topic == "first-lap-fuel" || !ownLapOnly &&
                (c.Topic == "near-best" || !routineBusy && now - lastRadio >= TimeSpan.FromSeconds(8)))).OrderByDescending(c => c.Priority).FirstOrDefault();
            return call != null && call.TryReserve(now) ? call : null;
        }
    }
}
