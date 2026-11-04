using System;
using System.Collections.Generic;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam"),NonParallelizable]
    public class NativeFieldEventsTests
    {
        private static readonly DateTime T=new DateTime(2026,11,4,12,0,0,DateTimeKind.Utc);
        private static GameStateData State(SessionType type=SessionType.Race)
        {
            var s=new GameStateData(T.Ticks);s.SessionData.SessionId=9;s.SessionData.SessionType=type;s.SessionData.SessionPhase=SessionPhase.Green;
            s.SessionData.ClassPosition=4;s.SessionData.CompletedLaps=3;s.SessionData.CurrentLapIsValid=true;
            var op=new OpponentData {DriverRawName="Nick Heidfeld",ClassPosition=3,CompletedLaps=3,InPits=true,IsActive=true};
            s.OpponentData["driver-17"]=op;s.PitData.OpponentForCarAheadPitting=op;return s;
        }
        private static NativeFieldFact Pit(GameStateData s)
        {return NativeFieldFact.Capture("car_in_front_is_pitting",null,s);}
        [TestCase(SessionType.Race)] [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)]
        public void Named_pit_event_keeps_identity_and_session_and_does_not_claim_position_gain(SessionType type)
        {
            var s=State(type);var q=new NativeFieldEvents();var m=new AiRaceMemory();q.Capture(Pit(s));q.Observe(s,T,m,true);
            var call=q.Take(s,T,false,true,false);Assert.IsNotNull(call);Assert.AreEqual(type,call.SessionType);
            StringAssert.Contains("Nick Heidfeld",call.Text);StringAssert.DoesNotContain("Platz gewonnen",call.Text);
            Assert.IsTrue(call.IsValid(s,T));Assert.AreEqual(1,m.GetEvents(TimeSpan.FromMinutes(1),T).Count);
        }
        [Test]
        public void Native_and_mapped_pit_notifications_are_one_event_not_two()
        {
            var s=State();var before=State();s.PitData.CarInFrontIsPitting=true;var q=new NativeFieldEvents();var m=new AiRaceMemory();
            q.CaptureMappedPits(before,s);q.Capture(Pit(s));q.Observe(s,T,m,true);Assert.AreEqual(1,m.GetEvents(TimeSpan.FromMinutes(1),T).Count);
            var call=q.Take(s,T,false,true,false);call.MarkPlaybackStarted(T);q.Observe(s,T.AddSeconds(.1),m,true);
            Assert.IsNull(q.Take(s,T.AddSeconds(9),false,true,false));
        }
        [Test]
        public void Captured_driver_is_not_reassigned_when_neighbor_changes_and_pit_exit_cancels_message()
        {
            var s=State();var f=Pit(s);var q=new NativeFieldEvents();q.Capture(f);q.Observe(s,T,null,true);
            s.PitData.OpponentForCarAheadPitting=new OpponentData {DriverRawName="Other"};
            var call=q.Take(s,T,false,true,false);StringAssert.Contains("Nick Heidfeld",call.Text);
            s.OpponentData["driver-17"].InPits=false;Assert.IsFalse(call.IsValid(s,T.AddSeconds(.1)));
            s.OpponentData["driver-17"].DriverRawName="Reused slot";Assert.IsFalse(NativeFieldEvents.StillCurrent(f,s));
        }
        [TestCase(true,false,false)] [TestCase(false,true,false)] [TestCase(false,false,true)]
        public void Busy_disabled_and_minimal_do_not_produce_optional_speech(bool busy,bool disabled,bool minimal)
        {var s=State();var q=new NativeFieldEvents();q.Capture(Pit(s));q.Observe(s,T,null,true);Assert.IsNull(q.Take(s,T,busy,!disabled,minimal));}
        [Test]
        public void Qualifying_push_defers_optional_news_but_outlap_can_report()
        {
            var s=State(SessionType.Qualify);s.PositionAndMotionData.CarSpeed=40;var q=new NativeFieldEvents();q.Capture(Pit(s));q.Observe(s,T,null,true);
            Assert.IsNull(q.Take(s,T,false,true,false));s.PitData.OnOutLap=true;Assert.IsNotNull(q.Take(s,T.AddSeconds(1),false,true,false));
        }
        [Test]
        public void Old_session_and_stale_incoming_events_never_enter_current_memory()
        {
            var s=State();var q=new NativeFieldEvents();var m=new AiRaceMemory();q.Capture(Pit(s));s.SessionData.SessionId++;
            q.Observe(s,T,m,true);Assert.IsNull(q.Take(s,T,false,true,false));Assert.AreEqual(0,m.GetEvents(TimeSpan.FromMinutes(1),T).Count);
            q.Capture(Pit(s));q.Observe(s,T.AddSeconds(3),m,true);Assert.AreEqual(0,m.GetEvents(TimeSpan.FromMinutes(1),T.AddSeconds(3)).Count);
        }
        [Test]
        public void No_names_setting_and_missing_identity_are_respected()
        {
            var s=State();var q=new NativeFieldEvents();q.Capture(Pit(s));q.Observe(s,T,null,false);
            StringAssert.DoesNotContain("Heidfeld",q.Take(s,T,false,true,false).Text);
            s.PitData.OpponentForCarAheadPitting=null;Assert.IsNull(Pit(s));
            Assert.IsNull(NativeFieldFact.Capture("incident_corner",null,s),"Never turn remote or heuristic incident sound paths into an invented accident ahead.");
        }
        [Test]
        public void Snapshot_is_immutable_and_queue_is_bounded()
        {
            var s=State();var f=Pit(s);s.OpponentData["driver-17"].DriverRawName="Changed";Assert.AreEqual("Nick Heidfeld",f.Name);
            var q=new NativeFieldEvents();for(int i=0;i<1000;i++)q.Capture(f);q.Observe(s,T,null,true);Assert.IsNull(q.Take(s,T,false,true,false));
        }
    }
}
