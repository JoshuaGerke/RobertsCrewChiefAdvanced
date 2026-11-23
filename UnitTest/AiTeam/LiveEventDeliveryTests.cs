using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using CrewChiefV4.RaceRoom;
using CrewChiefV4.RaceRoom.RaceRoomData;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class LiveEventDeliveryTests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        private static GameStateData State(SessionType type = SessionType.Race)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 41; s.SessionData.SessionType = type;
            s.SessionData.SessionPhase = SessionPhase.Green; s.SessionData.ClassPosition = 3;
            s.carClass = new CarData.CarClass(); s.PositionAndMotionData.CarSpeed = 40;
            return s;
        }
        private static RaceDataContext Context(GameStateData s, DateTime now)
        { return new RaceDataContext { GameState=s, NowUtc=now, LastTelemetryUtc=now, Memory=new AiRaceMemory() }; }
        private static AiRadioPreferences FuelOnly()
        {
            var p=new AiRadioPreferences(); foreach(var o in AiRadioPreferences.Options)p.Set(o.Id,0);
            p.Set("fuel",30);p.SetTolerance("fuel",0,0);return p;
        }
        private static ObservedRaceCall PositionCall(GameStateData s)
        {
            return new ObservedRaceCall {Topic="position",Text="Sauber vorbeigekommen. Position 3.",Priority=3,
                Position=3,SessionId=41,SessionType=s.SessionData.SessionType,DetectedUtc=T,ExpiresUtc=T.AddSeconds(8)};
        }

        [Test]
        public void RaceRoom_native_sector_references_keep_their_scope_and_simulator_timestamp()
        {
            var state = State();
            state.SourceTelemetryUtc = T;
            R3EGameStateMapper.MapNativeSectorReferences(state, new RaceRoomShared
            {
                BestIndividualSectorTimeSelf = new Sectors<float> { Sector1 = 31, Sector2 = -1, Sector3 = 29 },
                BestIndividualSectorTimeLeader = new Sectors<float> { Sector1 = 30 },
                BestIndividualSectorTimeLeaderClass = new Sectors<float> { Sector1 = 30.5f },
                TimeDeltaBestSelf = -.75f
            });
            var result = RaceDataTools.Execute("get_lap_times", Context(state, T), "{}");
            Assert.IsNotNull(result["native_sektorreferenzen"]);
            Assert.AreEqual(31, state.Measurements["timing.sector.personal.1"].Value);
            Assert.AreEqual("fehlt", state.Measurements["timing.sector.personal.2"].StatusAt(T));
            Assert.AreEqual(30, state.Measurements["timing.sector.leader.1"].Value);
            var reference = state.Measurements["timing.sector.class_leader.1"];
            Assert.AreEqual(30.5, reference.Value);
            Assert.AreEqual(T, reference.SampleUtc);
            Assert.AreEqual("veraltet", reference.StatusAt(T.AddSeconds(4)));
            Assert.AreEqual(-.75, state.Measurements["timing.lap.delta_personal"].Value);
            Assert.IsFalse(state.Measurements.ContainsKey("timing.sector.field.1"));
            var briefing = RaceDataTools.NativeSectorBriefing(Context(state, T.AddSeconds(.1)));
            Assert.AreEqual(30.5, (double)briefing["class_leader_einzelsektoren_s"][0]);
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null, briefing["personal_einzelsektoren_s"][1].Type);
            Assert.IsFalse(RaceDataTools.NativeSectorBriefing(Context(state, T.AddSeconds(4))).HasValues);
            R3EGameStateMapper.MapNativeSectorReferences(state, new RaceRoomShared { TimeDeltaBestSelf = -1000 });
            Assert.AreEqual("fehlt", state.Measurements["timing.lap.delta_personal"].StatusAt(T));
        }

        [Test]
        public void Ben_damage_followup_requires_current_damage_and_a_confirmed_open_pit_window()
        {
            var state = State();
            state.CarDamageData.DamageEnabled = true;
            state.CarDamageData.OverallAeroDamage = DamageLevel.MAJOR;
            state.PitData.PitWindow = PitWindow.Open;
            var advice = IncidentWatch.PitAdviceAfterDamage(state);
            Assert.AreEqual(IncidentWatch.DamagePitAdvicePhrase, advice);
            Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Ben, advice));
            Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Ben, TrackSituationWatch.ClosingAheadPhrase));
            StringAssert.DoesNotContain("Box", IncidentWatch.DamageReport(state.CarDamageData, includeAdvice: false));
            StringAssert.Contains("Box", IncidentWatch.DamageReport(state.CarDamageData));
            state.FlagData.isLocalYellow = true;
            Assert.IsNull(IncidentWatch.PitAdviceAfterDamage(state));
            state.FlagData.isLocalYellow = false;
            state.PitData.PitWindow = PitWindow.Closed;
            Assert.IsNull(IncidentWatch.PitAdviceAfterDamage(state));
            state.PitData.PitWindow = PitWindow.Open;
            state.PitData.HasRequestedPitStop = true;
            Assert.IsNull(IncidentWatch.PitAdviceAfterDamage(state));
            state.PitData.HasRequestedPitStop = false;
            state.CarDamageData.OverallAeroDamage = DamageLevel.NONE;
            Assert.IsNull(IncidentWatch.PitAdviceAfterDamage(state));
        }

        [Test]
        public void Overdue_race_update_waits_for_quiet_and_recomposes_after_position_changes()
        {
            var state = State();
            state.SessionData.SessionStartClassPosition = 5;
            var prefs = FuelOnly();
            prefs.Set("fuel", 0);
            prefs.Set("status", 30);
            prefs.SetTolerance("status", 0, 0);
            var schedule = new AiRadioSchedule(1);
            schedule.Observe(state, T, prefs);
            var ctx = Context(state, T.AddSeconds(51));
            ctx.Memory.AddRadio(TeamMember.Mik, "Boxenlimit", T.AddSeconds(46));
            Assert.IsNull(schedule.Take(ctx, prefs, true));
            var first = schedule.Take(ctx, prefs, false);
            Assert.IsNotNull(first);
            Assert.AreEqual("status", first.Topic);
            StringAssert.Contains("Platz 3", first.Text);
            StringAssert.Contains("2 Plätze gutgemacht", first.Text);

            state.SessionData.ClassPosition = 4;
            ctx.NowUtc = ctx.LastTelemetryUtc = T.AddSeconds(52);
            Assert.IsFalse(first.IsValid(state, ctx.NowUtc));
            Assert.IsNull(schedule.Take(ctx, prefs, false));
            ctx.NowUtc = ctx.LastTelemetryUtc = T.AddSeconds(57);
            var fresh = schedule.Take(ctx, prefs, false);
            Assert.IsNotNull(fresh);
            StringAssert.Contains("Platz 4", fresh.Text);
            fresh.MarkPlaybackStarted(ctx.NowUtc);
            ctx.NowUtc = ctx.LastTelemetryUtc = T.AddSeconds(58);
            Assert.IsNull(schedule.Take(ctx, prefs, false));
        }

        [Test]
        public void Interim_report_keeps_the_core_position_when_optional_rival_details_change()
        {
            var state = State();
            state.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            state.SessionData.CompletedLaps = 3;
            state.SessionData.OpponentKeyInFront = "rival";
            state.SessionData.TimeDeltaFront = 1.4f;
            state.SessionData.LapsDeltaFront = 0;
            var car = new OpponentData { DriverRawName = "Marco Wittmann", ClassPosition = 2,
                CarClass = state.carClass, CompletedLaps = 3, IsActive = true };
            state.OpponentData["rival"] = car;
            var context = Context(state, T);
            var call = new ObservedRaceCall { Topic = "status", RaceStatus = true, Text = "Platz 3.",
                SessionId = state.SessionData.SessionId, Position = 3, DetectedUtc = T, ExpiresUtc = T.AddSeconds(10) };
            Assert.IsTrue(call.TryReserve(T));
            string combined = call.ComposeForDelivery(context, out var detailValid);
            StringAssert.Contains("Marco Wittmann", combined);
            Assert.IsTrue(detailValid(state, T));
            car.r3eSlotId++;
            Assert.IsFalse(detailValid(state, T));
            call.ReleaseForRetry(T, "field_facts_changed");
            Assert.IsTrue(call.TryReserve(T.AddSeconds(1)));
            Assert.AreEqual("Platz 3.", call.ComposeForDelivery(context, out detailValid));
            Assert.IsNull(detailValid);
        }

        [Test]
        public void Unchanged_fuel_is_not_repeated_by_selecting_a_different_phrase()
        {
            var state = State();
            state.FuelData.FuelUseActive = true;
            state.FuelData.FuelLeft = 9f;
            state.SessionData.CurrentLapIsValid = state.SessionData.PreviousLapWasValid = true;
            var memory = new AiRaceMemory();
            for (int lap = 1; lap <= 3; lap++)
            {
                state.SessionData.IsNewLap = true;
                state.SessionData.CompletedLaps = lap;
                state.SessionData.LapTimePrevious = 100;
                memory.Update(null, state, T.AddSeconds(-300 + lap * 100));
            }
            var preferences = FuelOnly();
            var schedule = new AiRadioSchedule(1);
            schedule.Observe(state, T, preferences);
            var first = schedule.Take(Context(state, T.AddSeconds(31)), preferences, false);
            Assert.IsNotNull(first);
            first.MarkPlaybackStarted(T.AddSeconds(31));
            first.MarkPlaybackFinished(T.AddSeconds(31), false);
            var context = Context(state, T.AddSeconds(63));
            context.Memory = memory;
            Assert.IsNull(schedule.Take(context, preferences, false));
            state.FuelData.FuelLeft = 7.5f;
            var update = schedule.Take(Context(state, T.AddSeconds(94)), preferences, false);
            Assert.IsNotNull(update);
            StringAssert.Contains("7,5", update.Text);
        }

        [Test]
        public void Pre_start_answer_expires_as_soon_as_the_players_start_goes_green()
        {
            var grid = State();
            grid.SessionData.SessionPhase = SessionPhase.Gridwalk;
            var countdown = State();
            countdown.SessionData.SessionPhase = SessionPhase.Countdown;
            var green = State();
            Assert.IsTrue(RaceSituationWatch.AnswerMatchesStartPhase(grid, countdown, T));
            Assert.IsFalse(RaceSituationWatch.AnswerMatchesStartPhase(grid, green, T));
            Assert.IsTrue(RaceSituationWatch.AnswerMatchesStartPhase(green, green, T));
        }

        [Test]
        public void RaceRoom_finish_status_distinguishes_black_flag_from_confirmed_disqualification()
        {
            var before = State();
            before.SessionData.SessionPhase = SessionPhase.Checkered;
            var after = State();
            after.SessionData.SessionPhase = SessionPhase.Checkered;
            after.SessionData.Flag = FlagEnum.BLACK;
            R3EGameStateMapper.ApplyRaceFinishStatus(after.SessionData, (int)RaceRoomConstant.FinishStatus.None);
            Assert.IsFalse(after.SessionData.IsDisqualified);
            Assert.AreEqual(SessionPhase.Checkered, after.SessionData.SessionPhase);
            R3EGameStateMapper.ApplyRaceFinishStatus(after.SessionData, (int)RaceRoomConstant.FinishStatus.DQ);
            Assert.IsTrue(after.SessionData.IsDisqualified);
            Assert.AreEqual(SessionPhase.Finished, after.SessionData.SessionPhase);
            var text = new LapWatch().Evaluate(before, after, -1, -1, false);
            StringAssert.Contains("Disqualifikation", text);
            StringAssert.DoesNotContain("Im Ziel", text);
        }

        [Test]
        public void Important_position_loss_is_grouped_until_the_current_rank_stabilises()
        {
            var state = State();
            state.SessionData.ClassPosition = 15;
            state.PositionAndMotionData.CarSpeed = 6.5f;
            var observer = new ActiveRaceObserver();
            observer.Observe(state, T, "Rob", 0);
            state.SessionData.ClassPosition = 17;
            for (int tick = 1; tick <= 4; tick++) observer.Observe(state, T.AddMilliseconds(tick * 100), "Rob", 0);
            state.SessionData.ClassPosition = 24;
            for (int tick = 5; tick <= 10; tick++) observer.Observe(state, T.AddMilliseconds(tick * 100), "Rob", 0);
            Assert.IsNull(observer.Take(state, T.AddSeconds(1), false, false, false, 0, null, "Rob"));
            state.PositionAndMotionData.CarSpeed = 20;
            observer.Observe(state, T.AddSeconds(3), "Rob", 0);
            var call = observer.Take(state, T.AddSeconds(3), false, false, false, 0, null, "Rob");
            Assert.IsNotNull(call);
            Assert.AreEqual("position-loss", call.Topic);
            StringAssert.Contains("von Platz 15 auf 24", call.Text);
            state.SessionData.ClassPosition = 23;
            Assert.IsFalse(call.IsValid(state, T.AddMilliseconds(3100)));
        }

        [Test]
        public void Rear_warning_can_start_as_the_gap_enters_two_seconds()
        {
            var state = State();
            state.SessionData.CompletedLaps = 3;
            state.SessionData.OpponentKeyBehind = "rival";
            state.SessionData.LapsDeltaBehind = 0;
            state.OpponentData["rival"] = new OpponentData { DriverRawName = "Jack Aitken", IsActive = true,
                ClassPosition = 4, CompletedLaps = 3, CarClass = state.carClass };
            var observer = new ActiveRaceObserver();
            observer.Observe(state, T, "Rob", 0);
            state.SessionData.TimeDeltaBehind = 2.5f;
            observer.ObserveRearPressure(Context(state, T), new RaceGapMeasurement(2.5f, RaceGapKind.SimulatorTiming));
            state.SessionData.TimeDeltaBehind = 2.1f;
            var next = T.AddMilliseconds(1600);
            observer.ObserveRearPressure(Context(state, next), new RaceGapMeasurement(2.1f, RaceGapKind.SimulatorTiming));
            var call = observer.Take(state, next, false, false, false, 0, null, "Rob");
            Assert.IsNotNull(call);
            StringAssert.Contains("Jack Aitken", call.Text);
            StringAssert.Contains("2,1 Sekunden", call.Text);
        }

        [TestCase("26 Zehntel", "zwei Komma sechs Sekunden")]
        [TestCase("neunundzwanzig Zehntel", "zwei Komma neun Sekunden")]
        [TestCase("drei Zehntel", "drei Zehntel")]
        public void German_radio_obeys_the_driver_gap_units(string input, string expected)
        {
            Assert.AreEqual(expected, GermanRadioText.Prepare(input, true));
        }

        [Test]
        public void Local_lap_comparison_uses_the_named_drivers_valid_time_not_the_current_gap()
        {
            var state = State(SessionType.Practice);
            state.SessionData.LapTimePrevious = 112;
            state.SessionData.PreviousLapWasValid = true;
            var rival = new OpponentData { DriverRawName = "Ben Dörr", IsActive = true, LastLapTime = 114.59f, LastLapValid = true };
            state.OpponentData["doerr"] = rival;
            string answer = LocalAnswers.CompareRivalLap("Rundenzeitvergleich gegen Ben Dörr", Context(state, T));
            StringAssert.Contains("Ben Dörr 2,6 Sekunden langsamer", answer);
            Assert.AreEqual(answer, LocalAnswers.CompareRivalLap(
                "Wie sind die Rundenzeiten von Ben Dörr und mir in der Gegenüberstellung?", Context(state, T)));
            rival.LastLapValid = false;
            StringAssert.Contains("fehlen gültige Zeiten", LocalAnswers.CompareRivalLap("Rundenzeitvergleich gegen Ben Dörr", Context(state, T)));
        }

        [Test]
        public void Rear_pressure_warns_about_the_same_closing_rival_before_the_routine_interval()
        {
            var state = State();
            state.SessionData.CompletedLaps = 3;
            state.SessionData.OpponentKeyBehind = "solbes";
            state.SessionData.LapsDeltaBehind = 0;
            var rival = new OpponentData { DriverRawName = "Jordi Solbes", IsActive = true, ClassPosition = 4,
                CompletedLaps = 3, CarClass = state.carClass };
            state.OpponentData.Add("solbes", rival);
            var observer = new ActiveRaceObserver();
            observer.Observe(state, T, "Rob", 0);
            var context = Context(state, T);
            state.SessionData.TimeDeltaBehind = 3.1f;
            observer.ObserveRearPressure(context, new RaceGapMeasurement(3.1f, RaceGapKind.SimulatorTiming));
            context = Context(state, T.AddSeconds(3));
            state.SessionData.TimeDeltaBehind = 2.0f;
            observer.ObserveRearPressure(context, new RaceGapMeasurement(2.0f, RaceGapKind.SimulatorTiming));
            var call = observer.Take(state, context.NowUtc, false, false, false, 0, null, "Rob");
            Assert.IsNotNull(call);
            Assert.AreEqual("rear-pressure", call.Topic);
            StringAssert.Contains("Jordi Solbes", call.Text);
            StringAssert.Contains("2,0 Sekunden", call.Text);
            rival.r3eSlotId = 991;
            Assert.IsFalse(call.IsValid(state, context.NowUtc));
        }

        [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)] [TestCase(SessionType.PrivateQualify)]
        public void Similar_best_times_never_create_phantom_traffic(SessionType type)
        {
            var s=State(type);s.SessionData.PlayerLapTimeSessionBest=90;
            s.OpponentData["rival"]=new OpponentData {ClassPosition=2,CurrentBestLapTime=89,IsActive=true,CarClass=s.carClass};
            s.setPracOrQualiDeltas();Assert.AreEqual(1,s.SessionData.TimeDeltaFront);
            var director=new RaceRadioDirector();var q=new AiRadioSchedule(1);var p=FuelOnly();
            s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=40;q.Observe(s,T,p);
            for(int i=0;i<=310;i++)director.Observe(T.AddMilliseconds(i*100),s);
            Assert.IsFalse(director.TrafficQuiet(T.AddSeconds(31)));
            Assert.IsNotNull(q.Take(Context(s,T.AddSeconds(31)),p,director.TrafficQuiet(T.AddSeconds(31))));
        }

        [Test]
        public void Real_race_timing_gap_still_defers_optional_reports()
        {
            var s=State();s.SessionData.TimeDeltaFront=1;
            var director=new RaceRadioDirector();director.Observe(T,s);
            Assert.IsTrue(director.TrafficQuiet(T.AddSeconds(1)));
            Assert.IsFalse(director.TrafficQuiet(T.AddSeconds(9)));
        }

        [Test]
        public void Core_overtake_survives_neighbour_gap_change_during_audio_preparation()
        {
            var s=State();s.SessionData.OpponentKeyInFront="rival";s.SessionData.TimeDeltaFront=3;
            s.OpponentData["rival"]=new OpponentData {ClassPosition=2,DriverRawName="Alex",IsActive=true,CarClass=s.carClass};
            var call=PositionCall(s);string core=call.Text;
            call.IncludeNeighbour(Context(s,T),true,true);
            s.SessionData.TimeDeltaFront=3.6f;
            Assert.AreEqual(core,call.Text,"Optional gap must not extend a confirmed event's immutable speech.");
            Assert.IsTrue(call.IsValid(s,T.AddSeconds(1)));
            s.SessionData.ClassPosition=4;
            Assert.IsFalse(call.IsValid(s,T.AddSeconds(2)),"A changed core position must still cancel the old position claim.");
        }

        [Test]
        public void First_qualifying_lap_survives_ranking_updates_before_selection_and_playback()
        {
            var s=State(SessionType.Qualify);var observer=new SessionLapObserver();observer.Observe(s,null,T,true);
            s.SessionData.CompletedLaps=1;
            observer.Observe(s,new LapRecord {LapNumber=1,LapTime=92,Valid=true},T.AddSeconds(90),true);
            s.SessionData.ClassPosition=4;
            var call=observer.Take(s,T.AddSeconds(91),false);
            Assert.IsNotNull(call);StringAssert.Contains(LocalAnswers.LapTime(92),call.Text);
            StringAssert.DoesNotContain("Platz",call.Text);
            s.SessionData.ClassPosition=5;s.SessionData.PlayerClassSessionBestLapTime=88;
            Assert.IsTrue(call.IsValid(s,T.AddSeconds(92)));
            s.SessionData.CompletedLaps=2;
            Assert.IsFalse(call.IsValid(s,T.AddSeconds(93)),"The following lap must not revive an old lap announcement.");
        }

        [Test]
        public void Reservation_is_not_delivery_and_failed_render_can_retry_once()
        {
            var s=State();var observer=new ActiveRaceObserver();observer.Enqueue(PositionCall(s));
            var first=observer.Take(s,T,false,false,false,0,null,"Rob");Assert.IsNotNull(first);
            Assert.AreEqual(DateTime.MinValue,first.DeliveryStartedUtc);
            Assert.IsNull(observer.Take(s,T.AddMilliseconds(100),false,false,false,0,null,"Rob"));
            first.ReleaseForRetry(T.AddMilliseconds(200),"synthetic_tts_failure");
            Assert.IsNull(observer.Take(s,T.AddSeconds(1),false,false,false,0,null,"Rob"));
            var retry=observer.Take(s,T.AddSeconds(1.3),false,false,false,0,null,"Rob");
            Assert.AreSame(first,retry);
            retry.ReleaseForRetry(T.AddSeconds(1.4),"synthetic_second_failure");
            Assert.IsTrue(retry.DeliveryFailed);
            Assert.IsNull(observer.Take(s,T.AddSeconds(3),false,false,false,0,null,"Rob"));
        }

        [Test]
        public void Expired_call_cannot_retry_after_channel_clears()
        {
            var s=State();var observer=new ActiveRaceObserver();observer.Enqueue(PositionCall(s));
            var call=observer.Take(s,T,false,false,false,0,null,"Rob");
            call.ReleaseForRetry(T.AddSeconds(9),"synthetic_late_failure");
            Assert.IsNull(observer.Take(s,T.AddSeconds(10),false,false,false,0,null,"Rob"));
        }

        [Test]
        public void Report_interval_is_measured_from_actual_playback_start()
        {
            var s=State(SessionType.Practice);s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=40;
            var q=new AiRadioSchedule(1);var p=FuelOnly();q.Observe(s,T,p);
            var first=q.Take(Context(s,T.AddSeconds(30)),p,false);Assert.IsNotNull(first);
            Assert.IsNull(q.Take(Context(s,T.AddSeconds(31)),p,false),"Reserved audio must not be duplicated.");
            first.MarkPlaybackStarted(T.AddSeconds(34));
            Assert.IsNull(q.Take(Context(s,T.AddSeconds(60)),p,false),"The interval starts at hearing the report, not selecting it.");
            Assert.IsNotNull(q.Take(Context(s,T.AddSeconds(64)),p,false));
        }

        [Test]
        public void Failed_interval_report_can_retry_within_original_lifetime()
        {
            var s=State(SessionType.Practice);s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=40;
            var q=new AiRadioSchedule(1);var p=FuelOnly();q.Observe(s,T,p);
            var first=q.Take(Context(s,T.AddSeconds(30)),p,false);first.ReleaseForRetry(T.AddSeconds(31));
            Assert.AreSame(first,q.Take(Context(s,T.AddSeconds(32)),p,false));
            first.MarkPlaybackStarted(T.AddSeconds(33));
            Assert.IsNull(q.Take(Context(s,T.AddSeconds(40)),p,false));
        }

        [Test]
        public void Minimal_mode_does_not_retry_optional_position_praise()
        {
            var s=State();var observer=new ActiveRaceObserver();observer.Enqueue(PositionCall(s));
            Assert.IsNull(observer.Take(s,T,false,false,true,0,null,"Rob"));
            Assert.IsNull(observer.Take(s,T.AddSeconds(1),false,false,false,0,null,"Rob"));
        }

        [Test]
        public void Confirmed_lead_in_final_lap_gets_context_without_premature_victory()
        {
            var s=State();s.SessionData.ClassPosition=1;s.SessionData.SessionNumberOfLaps=5;s.SessionData.SessionLapsRemaining=2;
            var observer=new ActiveRaceObserver();observer.Observe(s,T,"Rob",0);
            s.SessionData.SessionLapsRemaining=1;s.SessionData.CompletedLaps++;observer.Observe(s,T.AddMilliseconds(100),"Rob",0);
            var call=observer.Take(s,T.AddMilliseconds(100),false,false,false,0,null,"Rob");
            Assert.IsNotNull(call);StringAssert.Contains("Letzte Runde",call.Text);StringAssert.Contains("Du führst",call.Text);
            StringAssert.DoesNotContain("gewonnen",call.Text);
        }

        [Test]
        public void Timed_race_never_invents_exact_last_lap_from_numeric_lap_field()
        {
            var s=State();s.SessionData.ClassPosition=1;s.SessionData.SessionNumberOfLaps=5;
            s.SessionData.SessionLapsRemaining=1;s.SessionData.SessionHasFixedTime=true;
            var observer=new ActiveRaceObserver();observer.Observe(s,T,"Rob",0);observer.Observe(s,T.AddMilliseconds(100),"Rob",0);
            Assert.IsNull(observer.Take(s,T.AddMilliseconds(100),false,false,false,0,null,"Rob"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Closing_radio_changes_after_completed_audio_but_not_interruption(bool interrupted)
        {
            var observer=new ActiveRaceObserver();
            var s=State();s.SessionData.SessionNumberOfLaps=5;s.SessionData.SessionLapsRemaining=2;
            s.SessionData.ClassPosition=1;s.SessionData.OverallPosition=5;
            // Die Ansage kommt an der eigenen Linie (AMS2 Spa 23.11.): Restrunde und eigene Rundenzahl wechseln gemeinsam.
            observer.Observe(s,T,"Alex",0);s.SessionData.SessionLapsRemaining=1;s.SessionData.CompletedLaps++;
            observer.Observe(s,T.AddMilliseconds(100),"Alex",0);
            var first=observer.Take(s,T.AddMilliseconds(100),false,false,false,0,null,"Alex");
            Assert.IsNotNull(first);StringAssert.Contains("Du führst in deiner Klasse",first.Text);
            first.MarkPlaybackStarted(T.AddMilliseconds(100));first.MarkPlaybackFinished(T.AddSeconds(1),interrupted);
            observer.Reset();s.SessionData.SessionId++;s.SessionData.SessionLapsRemaining=2;
            observer.Observe(s,T.AddSeconds(2),"Alex",0);s.SessionData.SessionLapsRemaining=1;s.SessionData.CompletedLaps++;
            observer.Observe(s,T.AddSeconds(3),"Alex",0);
            var second=observer.Take(s,T.AddSeconds(3),false,false,false,0,null,"Alex");
            Assert.IsNotNull(second);Assert.AreEqual(interrupted,first.Text==second.Text);
            s.SessionData.ClassPosition=2;Assert.IsFalse(second.IsValid(s,T.AddSeconds(3)),"A lost lead must invalidate the queued call.");
        }

        [Test]
        public void Old_race_playback_callback_cannot_set_new_session_cooldown()
        {
            var s=State();var observer=new ActiveRaceObserver();observer.Enqueue(PositionCall(s));
            var old=observer.Take(s,T,false,false,false,0,null,"Rob");
            observer.Reset();s.SessionData.SessionId=42;
            var fresh=PositionCall(s);fresh.SessionId=42;observer.Enqueue(fresh);
            old.MarkPlaybackStarted(T.AddSeconds(1));
            Assert.AreSame(fresh,observer.Take(s,T.AddSeconds(1),false,false,false,0,null,"Rob"));
            Assert.IsFalse(old.IsValid(s,T.AddSeconds(1)));
        }

        [Test]
        public void Old_lap_playback_callback_cannot_silence_new_session_lap()
        {
            var s=State(SessionType.Qualify);var observer=new SessionLapObserver();observer.Observe(s,null,T,true);
            s.SessionData.CompletedLaps=1;observer.Observe(s,new LapRecord {LapNumber=1,LapTime=90,Valid=true},T.AddSeconds(1),true);
            var old=observer.Take(s,T.AddSeconds(1),false);
            observer.Reset();var fresh=State(SessionType.Practice);fresh.SessionData.SessionId=42;
            observer.Observe(fresh,null,T.AddSeconds(2),true);fresh.SessionData.CompletedLaps=1;
            observer.Observe(fresh,new LapRecord {LapNumber=1,LapTime=92,Valid=true},T.AddSeconds(3),true);
            old.MarkPlaybackStarted(T.AddSeconds(3));
            Assert.IsNotNull(observer.Take(fresh,T.AddSeconds(3),false));
        }

        [Test]
        public void Old_report_playback_callback_cannot_move_new_session_schedule()
        {
            var s=State(SessionType.Practice);s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=40;
            var q=new AiRadioSchedule(1);var p=FuelOnly();q.Observe(s,T,p);
            var old=q.Take(Context(s,T.AddSeconds(30)),p,false);
            q.Reset();s.SessionData.SessionId=42;q.Observe(s,T.AddSeconds(31),p);
            old.MarkPlaybackStarted(T.AddSeconds(34));
            Assert.IsNotNull(q.Take(Context(s,T.AddSeconds(61)),p,false));
        }
    }
}
