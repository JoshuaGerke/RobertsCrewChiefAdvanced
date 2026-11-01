using System;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class RadioDisciplineTests
    {
        private static readonly DateTime T=new DateTime(2026,11,1,12,0,0,DateTimeKind.Utc);
        private static GameStateData State(SessionType type=SessionType.Practice)
        {
            var s=new GameStateData(T.Ticks);s.SessionData.SessionId=42;s.SessionData.SessionType=type;
            s.SessionData.SessionPhase=SessionPhase.Green;s.SessionData.CompletedLaps=2;
            s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=40;s.PositionAndMotionData.CarSpeed=40;
            s.EngineData.EngineWaterTemp=90;return s;
        }
        private static RaceDataContext Context(GameStateData s,int seconds)
        {return new RaceDataContext {GameState=s,NowUtc=T.AddSeconds(seconds),LastTelemetryUtc=T.AddSeconds(seconds),Memory=new AiRaceMemory()};}
        private static AiRadioPreferences Only(string topic)
        {
            var p=new AiRadioPreferences();foreach(var o in AiRadioPreferences.Options)p.Set(o.Id,0);
            p.Set(topic,30);p.SetTolerance(topic,0,0);return p;
        }
        [TestCase(SessionType.Practice,true)] [TestCase(SessionType.Qualify,false)] [TestCase(SessionType.Race,true)]
        public void Confirmed_qualifying_push_lap_suppresses_only_optional_routine(SessionType type,bool expected)
        {
            var s=State(type);var p=Only("fuel");var q=new AiRadioSchedule(1);q.Observe(s,T,p);
            Assert.AreEqual(expected,q.Take(Context(s,30),p,false)!=null);
        }
        [TestCase(true,false)] [TestCase(false,true)]
        public void Qualifying_outlap_and_inlap_keep_optional_reports_available(bool outlap,bool inlap)
        {
            var s=State(SessionType.Qualify);s.PitData.OnOutLap=outlap;s.PitData.OnInLap=inlap;
            var p=Only("fuel");var q=new AiRadioSchedule(1);q.Observe(s,T,p);Assert.IsNotNull(q.Take(Context(s,30),p,false));
        }
        [Test]
        public void Queued_optional_report_is_invalidated_when_a_flying_lap_begins()
        {
            var s=State(SessionType.Qualify);s.PitData.OnOutLap=true;var p=Only("fuel");var q=new AiRadioSchedule(1);q.Observe(s,T,p);
            var call=q.Take(Context(s,30),p,false);Assert.IsNotNull(call);s.PitData.OnOutLap=false;Assert.IsFalse(call.IsValid(s,T.AddSeconds(31)));
        }
        [Test]
        public void An_unchanged_delivered_vehicle_report_is_not_repeated_every_timer_cycle()
        {
            var s=State();var p=Only("vehicle");var q=new AiRadioSchedule(1);q.Observe(s,T,p);
            var first=q.Take(Context(s,30),p,false);Assert.IsNotNull(first);first.MarkPlaybackStarted(T.AddSeconds(30));
            Assert.IsNull(q.Take(Context(s,31),p,false));Assert.IsNull(q.Take(Context(s,60),p,false));
            s.EngineData.EngineWaterTemp=96;var changed=q.Take(Context(s,90),p,false);Assert.IsNotNull(changed);StringAssert.Contains("96",changed.Text);
        }
        [Test]
        public void Failed_audio_does_not_mark_a_technical_report_as_heard()
        {
            var s=State();var p=Only("vehicle");var q=new AiRadioSchedule(1);q.Observe(s,T,p);
            var first=q.Take(Context(s,30),p,false);first.ReleaseForRetry(T.AddSeconds(31));
            Assert.AreSame(first,q.Take(Context(s,32),p,false));first.ReleaseForRetry(T.AddSeconds(33));
            Assert.IsNull(q.Take(Context(s,34),p,false));Assert.IsNotNull(q.Take(Context(s,64),p,false));
        }
        [Test]
        public void Long_quiet_sessions_still_allow_a_fresh_technical_reminder()
        {
            var s=State();var p=Only("vehicle");var q=new AiRadioSchedule(1);q.Observe(s,T,p);
            var first=q.Take(Context(s,30),p,false);first.MarkPlaybackStarted(T.AddSeconds(30));q.Take(Context(s,31),p,false);
            Assert.IsNotNull(q.Take(Context(s,631),p,false));
        }
        [Test]
        public void Active_mode_can_keep_the_drivers_explicit_interval_even_for_unchanged_values()
        {
            var s=State();var p=Only("vehicle");var q=new AiRadioSchedule(1);q.Observe(s,T,p);
            var first=q.Take(Context(s,30),p,false,filterUnchanged:false);first.MarkPlaybackStarted(T.AddSeconds(30));
            q.Take(Context(s,31),p,false,filterUnchanged:false);
            Assert.IsNotNull(q.Take(Context(s,60),p,false,filterUnchanged:false));
        }
        [Test]
        public void Known_braking_defers_routine_but_not_pit_speed_attention()
        {
            var s=State();s.ControlData.BrakePedal=.7f;var p=Only("fuel");var q=new AiRadioSchedule(1);q.Observe(s,T,p);
            Assert.IsNull(q.Take(Context(s,30),p,false));
            s.PitData.IsApproachingPitlane=true;s.PitData.PitSpeedLimit=80/3.6f;s.SessionData.SessionRunningTime=100;
            var pits=new PitLaneWatch();pits.Observe(s,T);Assert.IsNotNull(pits.Take(s,T,false,7));
            s.ControlData.BrakePedal=0;Assert.IsNotNull(q.Take(Context(s,31),p,false));
        }
        [Test]
        public void A_pit_request_gives_an_early_reminder_without_claiming_a_marker_position()
        {
            var s=State();s.PitData.HasRequestedPitStop=true;s.PitData.PitSpeedLimit=80/3.6f;s.SessionData.SessionRunningTime=100;
            var pits=new PitLaneWatch();pits.Observe(s,T);var call=pits.Take(s,T,false,7);
            Assert.IsNotNull(call);Assert.IsTrue(call.IsValid(s,T));StringAssert.Contains("marker_position_not_known",call.Evidence);
            s.SessionData.SessionRunningTime=100.1f;pits.Observe(s,T.AddSeconds(.1));Assert.IsNull(pits.Take(s,T.AddSeconds(.1),false,7));
            call.MarkPlaybackStarted(T.AddSeconds(.1));Assert.IsTrue(call.IsValid(s,T.AddSeconds(.1)));
        }
        [Test]
        public void Same_id_session_restart_invalidates_an_old_pit_call()
        {
            var s=State();s.PitData.IsApproachingPitlane=true;s.PitData.PitSpeedLimit=80/3.6f;s.SessionData.SessionRunningTime=100;
            var pits=new PitLaneWatch();pits.Observe(s,T);var old=pits.Take(s,T,false,7);
            s.SessionData.IsNewSession=true;pits.Observe(s,T.AddSeconds(.1));Assert.IsFalse(old.IsValid(s,T.AddSeconds(.1)));
        }

        [Test]
        public void Urgent_pit_rendering_can_start_during_a_normal_render_without_opening_more_slots()
        {
            var gate=new RadioRenderGate();Assert.IsTrue(gate.TryEnter(false));
            Assert.IsFalse(gate.TryEnter(false));Assert.IsTrue(gate.TryEnter(true));Assert.IsFalse(gate.TryEnter(true));
            gate.Exit(false);Assert.IsTrue(gate.Busy);Assert.IsTrue(gate.UrgentBusy);Assert.IsFalse(gate.TryEnter(false));
            gate.Exit(true);Assert.IsFalse(gate.Busy);Assert.IsTrue(gate.TryEnter(false));gate.Exit(false);
        }
        [Test]
        public void Completing_urgent_render_does_not_release_an_existing_normal_render()
        {
            var gate=new RadioRenderGate();Assert.IsTrue(gate.TryEnter(false));Assert.IsTrue(gate.TryEnter(true));
            gate.Exit(true);Assert.IsTrue(gate.Busy);Assert.IsFalse(gate.TryEnter(false));gate.Exit(false);Assert.IsFalse(gate.Busy);
        }
        [TestCase("pit-limit",7,true)] [TestCase("pit-speed",7,true)]
        [TestCase("pit-limit",3,false)] [TestCase("weather-rain",7,false)]
        public void Only_urgent_pit_events_use_the_reserved_render_slot(string topic,int priority,bool expected)
        {Assert.AreEqual(expected,RadioRenderGate.IsUrgentPit(new ObservedRaceCall {Topic=topic,Priority=priority}));}
        [Test]
        public void An_urgent_pit_announcement_can_interrupt_a_lower_ai_message_but_never_Sara()
        {
            Assert.IsTrue(CrewSpeechPolicy.CanInterruptForHazard(true,false,false,4,7));
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForHazard(true,true,false,4,7));
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForHazard(true,false,true,4,7));
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForHazard(true,false,false,8,7));
        }
    }
}
