using System;
using System.Collections.Generic;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using CrewChiefV4.Events.PubSub;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class LiveSessionFixTests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 31, 13, 0, 0, DateTimeKind.Utc);
        private static GameStateData State(SessionType type = SessionType.Practice, SessionPhase phase = SessionPhase.Green)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 7; gs.SessionData.SessionType = type; gs.SessionData.SessionPhase = phase;
            gs.SessionData.ClassPosition = 4; gs.PositionAndMotionData.CarSpeed = 40;
            gs.carClass = new CarData.CarClass();
            return gs;
        }

        [TestCase("Rob", "Rob")] [TestCase("", "Robert Gerke")] [TestCase("  ", "Robert Gerke")]
        public void Personal_radio_uses_nickname_with_backward_compatible_name_fallback(string nickname, string expected)
        {
            var settings = new AiTeamSettings { DriverName = "Robert Gerke", DriverNickname = nickname };
            Assert.AreEqual(expected, settings.RadioName);
            var call = new SessionGreetingDirector().CreateCall(GreetingContext(), T.AddSeconds(40), settings.RadioName, TeamMember.Ben);
            StringAssert.Contains(expected, call.Text);
        }

        private static RaceDataContext GreetingContext()
        {
            var gs = State(); gs.PitData.InPitlane = true; gs.PositionAndMotionData.CarSpeed = 0;
            gs.SessionData.TrackDefinition = new TrackDefinition("Monza", 0, 5793, new float[] { 0, 0 }, new float[] { 0, 0 });
            return new RaceDataContext { GameState = gs, NowUtc = T, LastTelemetryUtc = T };
        }

        [Test]
        public void Greeting_uses_confirmed_session_and_driver_without_inventing_weather()
        {
            var c = GreetingContext(); var director = new SessionGreetingDirector();
            var call = director.CreateCall(c, T.AddSeconds(40), "Robert", TeamMember.Ben);
            Assert.IsNotNull(call); StringAssert.Contains("Robert", call.Text); StringAssert.Contains("Monza", call.Text);
            StringAssert.Contains("Training", call.Text); StringAssert.DoesNotContain("Sonne", call.Text);
            StringAssert.DoesNotContain("Grad", call.Text);
            c.GameState.SessionData.ClassPosition = 20; c.GameState.SessionData.TimeDeltaFront = 10;
            c.GameState.EngineData.EngineOilTemp = 110;
            Assert.IsTrue(call.IsValid(c.GameState, T.AddSeconds(2)), "Unspoken gaps, position or engine values must not cancel the greeting.");
        }

        [Test]
        public void Greeting_audio_discard_retries_once_then_stops_after_backend_start()
        {
            var c = GreetingContext(); var call = new SessionGreetingDirector().CreateCall(c, T.AddSeconds(40), "Robert", TeamMember.Ben);
            Assert.IsTrue(call.TryReserve(T));
            string first = AiAudioStore.Put(new byte[44], call.Text, null,
                () => call.MarkPlaybackStarted(T), () => call.ReleaseForRetry(T));
            AiAudioStore.DiscardMessage("COMPOUND_ai_team_reply_" + first);
            Assert.AreEqual(DateTime.MinValue, call.DeliveryStartedUtc);
            Assert.IsFalse(call.TryReserve(T.AddMilliseconds(500)));
            Assert.IsTrue(call.TryReserve(T.AddSeconds(2)));
            int backend = 0;
            string second = AiAudioStore.Put(new byte[44], call.Text, () => call.IsValid(c.GameState, T.AddSeconds(2)),
                () => call.MarkPlaybackStarted(T.AddSeconds(2)), () => call.ReleaseForRetry(T.AddSeconds(2)));
            Assert.IsTrue(AiAudioStore.TryStart(second, () => backend++));
            AiAudioStore.Remove(second);
            Assert.AreEqual(1, backend); Assert.AreEqual(T.AddSeconds(2), call.DeliveryStartedUtc);
            Assert.IsFalse(call.TryReserve(T.AddSeconds(3)));
        }

        [Test]
        public void Greeting_second_audio_failure_is_terminal_and_never_spams()
        {
            var c = GreetingContext(); var call = new SessionGreetingDirector().CreateCall(c, T.AddSeconds(40), "Robert", TeamMember.Ben);
            Assert.IsTrue(call.TryReserve(T)); call.ReleaseForRetry(T);
            Assert.IsTrue(call.TryReserve(T.AddSeconds(2))); call.ReleaseForRetry(T.AddSeconds(2));
            Assert.IsTrue(call.DeliveryFailed); Assert.IsFalse(call.TryReserve(T.AddSeconds(10)));
        }

        [TestCase("driving")] [TestCase("session")] [TestCase("track")] [TestCase("ended")] [TestCase("expired")]
        public void Greeting_never_crosses_into_unsafe_or_other_context(string change)
        {
            var c = GreetingContext(); var call = new SessionGreetingDirector().CreateCall(c, T.AddSeconds(40), "Robert", TeamMember.Ben);
            DateTime now = T.AddSeconds(2);
            if (change == "driving") { c.GameState.PitData.InPitlane = false; c.GameState.PositionAndMotionData.CarSpeed = 40; }
            if (change == "session") c.GameState.SessionData.SessionId++;
            if (change == "track") c.GameState.SessionData.TrackDefinition = null;
            if (change == "ended") c.GameState.SessionData.SessionPhase = SessionPhase.Finished;
            if (change == "expired") now = T.AddSeconds(41);
            Assert.IsFalse(call.IsValid(c.GameState, now));
        }

        [Test]
        public void Greeting_omits_stale_weather_but_reports_fresh_temperature_and_rain()
        {
            var c = GreetingContext(); var director = new SessionGreetingDirector();
            c.GameState.Conditions.UpdateLiveSample(T.AddMinutes(-5),0,1,20,30,.4f,0,0,0,0,false,ConditionsMonitor.TrackStatus.UNKNOWN);
            StringAssert.DoesNotContain("Grad", director.CreateCall(c,T.AddSeconds(40),"Robert",TeamMember.Mik).Text);
            c.NowUtc = c.LastTelemetryUtc = DateTime.UtcNow;
            c.GameState.Conditions.UpdateLiveSample(c.NowUtc,0,1,20,30,.4f,0,0,0,0,false,ConditionsMonitor.TrackStatus.UNKNOWN);
            var call = director.CreateCall(c,c.NowUtc.AddSeconds(40),"Robert",TeamMember.Mik);
            StringAssert.Contains("Luft 20 Grad", call.Text); StringAssert.Contains("Regen wird gemeldet", call.Text);
            Assert.IsTrue(call.IsValid(c.GameState,c.NowUtc));
            c.GameState.Conditions.UpdateLiveSample(c.NowUtc,0,1,20,30,0,0,0,0,0,false,ConditionsMonitor.TrackStatus.UNKNOWN);
            Assert.IsFalse(call.IsValid(c.GameState,c.NowUtc));
        }

        [OneTimeSetUp]
        public void Init()
        {
            if (CrewChief.Debug == null) CrewChief.Debug = new Debugging();
            DataFiles.MakeFolders();
        }

        [Test]
        public void Finished_practice_does_not_finish_new_qualifying_or_race()
        {
            var detector = new GameStateEventDetector(null);
            var practice = State();
            var finished = State(phase: SessionPhase.Finished);
            detector.UpdateMappedState(practice, finished);
            Assert.IsTrue(GameEventBool.SessionFinished);
            var quali = State(SessionType.Qualify);
            quali.SessionData.IsNewSession = true;
            detector.UpdateMappedState(finished, quali);
            Assert.IsFalse(GameEventBool.SessionFinished, "First mapped tick must clear the previous session before lifecycle consumers run.");
            var next = State(SessionType.Qualify);
            detector.UpdateMappedState(quali, next);
            Assert.IsFalse(GameEventBool.SessionFinished);
            next.SessionData.SessionPhase = SessionPhase.Finished;
            detector.UpdateMappedState(quali, next);
            Assert.IsTrue(GameEventBool.SessionFinished);
            var formation = State(SessionType.Race, SessionPhase.Formation);
            formation.SessionData.IsNewSession = true;
            detector.UpdateMappedState(next, formation);
            Assert.IsFalse(GameEventBool.SessionFinished);
            detector.UpdateMappedState(formation, State(SessionType.Race));
            Assert.IsFalse(GameEventBool.SessionFinished, "Race must remain eligible for normal event processing and spotter unpause.");
            detector.clearState();
        }

        [TestCase("ai_team_reply_", true)]
        [TestCase("ai_team_info_", false)]
        public void Actual_compound_audio_message_is_classified_and_releases_rejected_clip(string prefix, bool reply)
        {
            int discarded = 0;
            string id = AiAudioStore.Put(new byte[44], "test", null, null, () => discarded++);
            var message = new QueuedMessage(prefix + id, 2,
                messageFragments: new List<MessageFragment> { MessageFragment.Text(SoundCache.AI_AUDIO_IDENTIFIER + id) });
            StringAssert.StartsWith("COMPOUND_", message.messageName);
            Assert.IsTrue(AiAudioStore.IsMessage(message.messageName));
            Assert.AreEqual(reply, AiAudioStore.IsMessage(message.messageName, true));
            AiAudioStore.DiscardMessage(message.messageName);
            AiAudioStore.DiscardMessage(message.messageName);
            Assert.AreEqual(1, discarded);
            Assert.IsFalse(AiAudioStore.TryStart(id, () => Assert.Fail("Rejected audio must never play.")));
        }

        [TestCase("COMPOUND_spotter_car_left")]
        [TestCase("COMPOUND_ai_team_reply_fake")]
        [TestCase("DELAYED_ai_team_reply_01234567890123456789012345678901")]
        public void Other_audio_cannot_be_mistaken_for_interruptible_ai(string name)
        { Assert.IsFalse(AiAudioStore.IsMessage(name)); }

        [TestCase("Du hast die komplette Freigabe, aktiv zu kommentieren.")]
        [TestCase("Meine Rechtschreibung ist schlecht.")]
        [TestCase("Die Reifen sind linksherum ungleich abgenutzt.")]
        public void Incidental_substrings_are_not_side_clearance_requests(string text)
        {
            Assert.AreNotEqual(TeamMember.Sara, TeamRouter.Route(text, null).Member);
            Assert.IsNull(SaraLocal.TryAnswer(text, SpotterSnapshot.None, null, T));
        }

        [TestCase("Ist rechts frei?")]
        [TestCase("Ist einer neben mir?")]
        [TestCase("Haben wir eine Überlappung?")]
        public void Real_side_questions_still_go_to_local_safety_path(string text)
        {
            Assert.AreEqual(TeamMember.Sara, TeamRouter.Route(text, null).Member);
            Assert.AreEqual(SaraLocal.NoDataAnswer, SaraLocal.TryAnswer(text, SpotterSnapshot.None, null, T));
            Assert.AreEqual(TeamMember.Mik, TeamRouter.Route("Mik, " + text, null).Member);
        }

        private static CornerData Corners(DamageLevel level, CornerData.Corners corners)
        {
            var data = new CornerData(); data.cornersForEachStatus.Add(level, corners); return data;
        }

        [TestCase(CornerData.Corners.FRONT_LEFT)] [TestCase(CornerData.Corners.FRONT_RIGHT)]
        [TestCase(CornerData.Corners.REAR_LEFT)] [TestCase(CornerData.Corners.REAR_RIGHT)]
        [TestCase(CornerData.Corners.FRONTS)] [TestCase(CornerData.Corners.REARS)]
        [TestCase(CornerData.Corners.LEFTS)] [TestCase(CornerData.Corners.RIGHTS)] [TestCase(CornerData.Corners.ALL)]
        public void Suspension_and_brake_damage_trigger_independently_of_aero_and_engine(CornerData.Corners corner)
        {
            var before = State(); var now = State();
            before.CarDamageData.DamageEnabled = now.CarDamageData.DamageEnabled = true;
            before.CarDamageData.SuspensionDamageStatus = before.CarDamageData.BrakeDamageStatus = Corners(DamageLevel.NONE, CornerData.Corners.ALL);
            now.CarDamageData.SuspensionDamageStatus = Corners(DamageLevel.MAJOR, corner);
            Assert.IsTrue(IncidentWatch.DamageIncreased(before.CarDamageData, now.CarDamageData));
            var watch = new IncidentWatch();
            watch.Evaluate(before, now, T, false);
            var assessment = watch.TakeDamageAssessment();
            Assert.IsNotNull(assessment, "A confirmed serious suspension change must be ready this tick, without waiting four seconds.");
            Assert.IsTrue(assessment.Serious); StringAssert.Contains("Aufhängung", assessment.LocalReport);
            now.CarDamageData.SuspensionDamageStatus = null;
            now.CarDamageData.BrakeDamageStatus = Corners(DamageLevel.MAJOR, corner);
            Assert.IsTrue(IncidentWatch.DamageIncreased(before.CarDamageData, now.CarDamageData));
        }

        [Test]
        public void First_known_corner_measurement_or_repair_is_not_invented_contact()
        {
            var before = State().CarDamageData; var now = State().CarDamageData;
            before.DamageEnabled = now.DamageEnabled = true;
            now.SuspensionDamageStatus = Corners(DamageLevel.NONE, CornerData.Corners.ALL);
            Assert.IsFalse(IncidentWatch.DamageIncreased(before, now));
            before.SuspensionDamageStatus = Corners(DamageLevel.MAJOR, CornerData.Corners.FRONT_LEFT);
            Assert.IsFalse(IncidentWatch.DamageIncreased(before, now));
        }

        [TestCase(SessionPhase.Checkered)] [TestCase(SessionPhase.Finished)]
        public void Valid_final_flying_lap_gets_best_time_praise_after_session_clock_expires(SessionPhase phase)
        {
            var gs = State(); gs.SessionData.PlayerLapTimeSessionBest = 113.60941f;
            gs.SessionData.PlayerClassSessionBestLapTime = 113.60941f;
            var observer = new SessionLapObserver(); observer.Observe(gs, null, T, false);
            gs.SessionData.SessionPhase = phase; observer.Observe(gs, null, T.AddSeconds(1), false);
            gs.SessionData.CompletedLaps = 1; gs.SessionData.PlayerClassSessionBestLapTime = 109.90643f;
            observer.Observe(gs, new LapRecord { LapNumber=1, LapTime=109.90643f, Valid=true }, T.AddSeconds(87), false);
            var call = observer.Take(gs, T.AddSeconds(87), false);
            Assert.IsNotNull(call); StringAssert.Contains("Bestzeit", call.Text);
            StringAssert.Contains("Klassenbestzeit", call.Text);
            Assert.IsTrue(call.IsValid(gs, T.AddSeconds(88)));
            gs.SessionData.SessionId++;
            Assert.IsFalse(call.IsValid(gs, T.AddSeconds(88)), "No praise can leak into a subsequent session.");
        }

        [TestCase(90.2f, true)] [TestCase(90.8f, false)]
        public void Near_best_feedback_requires_a_measured_small_delta(float lapTime, bool expected)
        {
            var gs = State(); gs.SessionData.PlayerLapTimeSessionBest = 90;
            var observer = new SessionLapObserver(); observer.Observe(gs, null, T, false);
            gs.SessionData.CompletedLaps = 1;
            observer.Observe(gs, new LapRecord { LapNumber=1, LapTime=lapTime, Valid=true }, T.AddSeconds(91), false);
            var call = observer.Take(gs, T.AddSeconds(91), false);
            Assert.AreEqual(expected, call != null);
            if (expected) StringAssert.Contains("0,20", call.Text);
        }

        [Test]
        public void Ordinary_race_position_comment_is_not_allowed_after_finish()
        {
            var call = new ObservedRaceCall { Topic="position", AllowLapFinish=true, SessionId=7, Position=-1,
                DetectedUtc=T, ExpiresUtc=T.AddSeconds(8) };
            Assert.IsFalse(call.IsValid(State(SessionType.Race, SessionPhase.Finished), T));
        }

        [Test]
        public void Opponent_sectors_are_available_before_our_first_lap_and_never_sum_independent_bests()
        {
            var gs = State();
            var opponent = new OpponentData { DriverRawName="Alex", OverallPosition=2, ClassPosition=2,
                IsActive=true, CarClass=gs.carClass, bestSector1Time=30, bestSector2Time=31, bestSector3Time=float.NaN };
            opponent.OpponentLapData.Add(new LapData { LapNumber=1, LapTime=92, IsValid=true,
                SectorTimes=new[] {30f,31f,31f}, GameTimeAtSectorEnd=new[] {30f,61f,92f} });
            opponent.OpponentLapData.Add(new LapData { LapNumber=2, LapTime=-1, IsValid=false,
                SectorTimes=new[] {29f,-1f,float.NaN} });
            gs.OpponentData["alex"] = opponent;
            var ctx = new RaceDataContext { GameState=gs, Memory=new AiRaceMemory(), NowUtc=T, LastTelemetryUtc=T, SendDriverNames=true };
            var result = RaceDataTools.Execute("get_opponent", ctx, "{\"name\":\"Alex\"}");
            var car = result["fahrer"][0];
            Assert.AreEqual(30, (float)car["beste_einzelsektoren_s"][0]);
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null, car["beste_einzelsektoren_s"][2].Type);
            Assert.IsNull(car["sektoren_bester_lauf_s"], "Independent sector bests are not a real lap.");
            Assert.IsFalse((bool)car["sektorrunden"][0]["gueltig"]);
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null, car["sektorrunden"][0]["sektoren_s"][1].Type);
            Assert.AreEqual(31, (float)car["sektorrunden"][1]["sektoren_s"][2]);
            for (int lap=3; lap<=10; lap++) opponent.OpponentLapData.Add(new LapData { LapNumber=lap });
            result = RaceDataTools.Execute("get_race_field", ctx, "{}");
            Assert.AreEqual(3, ((Newtonsoft.Json.Linq.JArray)result["fahrer"][0]["sektorrunden"]).Count);
        }

        [Test]
        public void Accessible_raw_source_is_not_a_claim_of_live_driving_data_and_expires()
        {
            GameDataConnectionStatus.Record("RaceRoom", T);
            Assert.AreEqual("RaceRoom", GameDataConnectionStatus.SourceAt(T.AddSeconds(2)));
            Assert.IsNull(GameDataConnectionStatus.SourceAt(T.AddSeconds(4)));
            Assert.IsNull(GameDataConnectionStatus.SourceAt(T.AddSeconds(-1)));
        }

        [TestCase(true, false, false, 5, 8, true)]
        [TestCase(true, false, false, 6, 7, true)]
        [TestCase(true, false, false, 8, 7, false)]
        [TestCase(true, false, false, 8, 8, false)]
        [TestCase(true, true, false, 20, 8, false)]
        [TestCase(true, true, false, 1, 8, false)]
        [TestCase(false, true, false, 20, 8, false)]
        [TestCase(false, false, false, 3, 8, false)]
        [TestCase(true, false, true, 1, 8, false)]
        public void Hazards_interrupt_only_lower_priority_ai_and_never_the_actual_spotter(bool ai, bool spotter, bool beep, int current, int incoming, bool expected)
        { Assert.AreEqual(expected, CrewSpeechPolicy.CanInterruptForHazard(ai, spotter, beep, current, incoming)); }

        [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)]
        public void Slowly_leaving_pit_does_not_miss_the_session_greeting(SessionType type)
        {
            Assert.IsTrue(SessionGreetingDirector.SafeToGreet(type,SessionPhase.Green,true,5));
            Assert.IsFalse(SessionGreetingDirector.SafeToGreet(type,SessionPhase.Green,false,5));
            Assert.IsFalse(SessionGreetingDirector.SafeToGreet(type,SessionPhase.Green,true,15));
            Assert.IsFalse(SessionGreetingDirector.SafeToGreet(SessionType.Race,SessionPhase.Green,true,5));
        }

        [Test]
        public void Joshua_heavy_impact_question_precedes_serious_damage_even_while_stopped()
        {
            var before=State(SessionType.Race); var crash=State(SessionType.Race);
            before.PositionAndMotionData.CarSpeed=45; crash.PositionAndMotionData.CarSpeed=0;
            before.CarDamageData.DamageEnabled=crash.CarDamageData.DamageEnabled=true;
            before.CarDamageData.OverallAeroDamage=DamageLevel.NONE;
            crash.CarDamageData.OverallAeroDamage=DamageLevel.MAJOR; crash.CarDamageData.LastImpactTime=12;
            var watch=new IncidentWatch(); var calls=watch.Evaluate(before,crash,T,false);
            CollectionAssert.Contains(CrewSpeechPolicy.HeavyContactPhrases,calls[0].Value);
            Assert.IsNull(watch.TakeDamageAssessment(),"The driver check must not be skipped by immediate serious-damage reporting.");
            watch.Evaluate(crash,crash,T.AddSeconds(4.5),false);
            var assessment=watch.TakeDamageAssessment();
            Assert.IsNotNull(assessment,"Known serious damage must follow the driver check even if the car is still stopped.");
            Assert.IsTrue(assessment.Serious); StringAssert.Contains("Aero",assessment.LocalReport);
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForHazard(true,false,false,7,7),"The following damage report must not cut off the equal-priority driver check.");
        }

        [Test]
        public void New_session_does_not_inherit_a_contact_from_previous_session()
        {
            var before=State(); var next=State(); next.SessionData.IsNewSession=true;
            next.CarDamageData.LastImpactTime=10;
            Assert.IsEmpty(new IncidentWatch().Evaluate(before,next,T,false));
        }

        [Test]
        public void One_long_excursion_counts_once_and_a_second_exit_counts_separately()
        {
            var on=State(); var off=State(); off.PenaltiesData.IsOffRacingSurface=true;
            var watch=new IncidentWatch(); watch.Evaluate(on,off,T,false);
            Assert.AreEqual(1,watch.Evaluate(off,off,T.AddSeconds(3),false).Count);
            Assert.IsEmpty(watch.Evaluate(off,off,T.AddSeconds(25),false));
            Assert.AreEqual(1,watch.OffTracks,"Remaining outside for 25 seconds is one excursion, not two.");
            watch.Evaluate(off,on,T.AddSeconds(26),false);
            watch.Evaluate(on,off,T.AddSeconds(27),false);
            watch.Evaluate(off,off,T.AddSeconds(30),false);
            Assert.AreEqual(2,watch.OffTracks); StringAssert.Contains("2 Ausritte",watch.FinishSummary());
        }

        [TestCase(ConditionsMonitor.TrackWetness.ModeratelyWet, ConditionsMonitor.TrackStatus.UNKNOWN, 0f, true, false)]
        [TestCase(ConditionsMonitor.TrackWetness.UNKNOWN, ConditionsMonitor.TrackStatus.WET, 0f, true, false)]
        [TestCase(ConditionsMonitor.TrackWetness.UNKNOWN, ConditionsMonitor.TrackStatus.UNKNOWN, .4f, false, true)]
        [TestCase(ConditionsMonitor.TrackWetness.Dry, ConditionsMonitor.TrackStatus.UNKNOWN, 0f, false, false)]
        public void Excursion_distinguishes_measured_wet_surface_from_current_rain(ConditionsMonitor.TrackWetness wetness, ConditionsMonitor.TrackStatus status, float rain, bool wetExpected, bool rainExpected)
        {
            var on=State(); var off=State(); off.PenaltiesData.IsOffRacingSurface=true;
            off.Conditions.addSample(T,0,1,20,25,rain,0,0,0,1,false,status,wetness);
            var watch=new IncidentWatch(); watch.Evaluate(on,off,T,false);
            var calls=watch.Evaluate(off,off,T.AddSeconds(3),false);
            Assert.AreEqual(1,calls.Count);
            Assert.AreEqual(wetExpected,calls[0].Value.Contains(CrewSpeechPolicy.WetTrackHint));
            Assert.AreEqual(rainExpected,calls[0].Value.Contains(CrewSpeechPolicy.RainTrackHint));
            Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Mik,calls[0].Value),"All complete variants must use the selected prepared voice.");
        }

        [Test]
        public void Spatial_standard_question_uses_local_sector_and_updates_with_telemetry()
        {
            var gs = State(); gs.SessionData.SectorNumber=2;
            Assert.AreEqual("Du bist in Sektor 2.", LocalAnswers.TryAnswer("Wo bin ich?", gs, null));
            gs.SessionData.SectorNumber=3;
            Assert.AreEqual("Du bist in Sektor 3.", LocalAnswers.TryAnswer("Wo bin ich?", gs, null));
            gs.PitData.InPitlane=true;
            Assert.AreEqual("Du bist in der Boxengasse.", LocalAnswers.TryAnswer("Wo bin ich?", gs, null));
        }

        [Test]
        public void Optional_interval_report_leaves_space_after_other_delivered_radio()
        {
            var gs = State(); gs.FuelData.FuelUseActive=true; gs.FuelData.FuelLeft=40;
            var prefs = new AiRadioPreferences(); foreach (var option in AiRadioPreferences.Options) prefs.Set(option.Id,0);
            prefs.Set("fuel",30); prefs.SetTolerance("fuel",0,0);
            var schedule = new AiRadioSchedule(1); schedule.Observe(gs,T,prefs);
            var memory = new AiRaceMemory(); memory.AddRadio(TeamMember.Mik,"Bestzeit!",T.AddSeconds(29));
            var ctx = new RaceDataContext { GameState=gs,Memory=memory,NowUtc=T.AddSeconds(31),LastTelemetryUtc=T.AddSeconds(31) };
            Assert.IsNull(schedule.Take(ctx,prefs,false));
            ctx.NowUtc=ctx.LastTelemetryUtc=T.AddSeconds(36);
            Assert.IsNull(schedule.Take(ctx,prefs,false),"Routine radio leaves eight seconds after another call, without starving enabled reports.");
            ctx.NowUtc=ctx.LastTelemetryUtc=T.AddSeconds(38);
            Assert.IsNotNull(schedule.Take(ctx,prefs,false));
        }

        [TestCase("👍", false)] [TestCase(" … ! ", false)] [TestCase("", false)]
        [TestCase("Stark, Rob! 👍", true)] [TestCase("Position 7.", true)]
        public void Silent_emoji_acknowledgements_are_not_sent_to_tts(string text, bool expected)
        { Assert.AreEqual(expected, CrewSpeechPolicy.HasSpokenText(text)); }
    }
}
