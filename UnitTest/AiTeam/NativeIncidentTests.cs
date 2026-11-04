using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam"),NonParallelizable]
    public class NativeIncidentTests
    {
        private static readonly DateTime T=new DateTime(2026,11,4,13,0,0,DateTimeKind.Utc);
        private NativeIncidentWatch watch;
        [SetUp]public void Setup(){watch=new NativeIncidentWatch();watch.Reset();NativeIncidentWatch.Enabled=true;}
        [TearDown]public void Cleanup(){watch.Reset();NativeIncidentWatch.Enabled=false;}
        private static GameStateData State(SessionType type=SessionType.Race)
        {
            var s=new GameStateData(T.Ticks);s.SessionData.SessionId=12;s.SessionData.SessionType=type;s.SessionData.SessionPhase=SessionPhase.Green;
            s.SessionData.TrackDefinition=new TrackDefinition("Test",5000);s.PositionAndMotionData.DistanceRoundTrack=1000;s.PositionAndMotionData.CarSpeed=50;
            s.OpponentData["rival"]=new OpponentData {DriverRawName="Nick Heidfeld",IsActive=true,DistanceRoundTrack=1150,Speed=0,stoppedInLandmark="turn_4"};return s;
        }
        private static void Publish(GameStateData s){NativeIncidentWatch.Publish(s,"turn_4",new[]{s.OpponentData["rival"]});}
        private ObservedRaceCall Tick(GameStateData s,LiveWarningDispatcher output,double seconds,bool busy=false)
        {
            s.Now=T.AddSeconds(seconds);watch.Observe(s,s.Now,output,null,true);output.Observe(s,s.Now);return output.Take(s,s.Now,busy,m=>true);
        }
        [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)] [TestCase(SessionType.Race)]
        public void Existing_native_detection_yields_current_ahead_warning_in_each_session(SessionType type)
        {
            var s=State(type);Publish(s);var o=new LiveWarningDispatcher();var call=Tick(s,o,0);
            Assert.IsNotNull(call);Assert.AreEqual(7,call.Priority);Assert.AreEqual(TeamMember.Ben,call.Member);
            Assert.IsTrue(watch.CoversAhead("rival"));Assert.IsTrue(call.IsValid(s,T));StringAssert.DoesNotContain("Unfall",call.Text);
            Assert.IsTrue(CrewSpeechPolicy.CacheOnly(true,7,TeamMember.Ben));Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Ben,call.Text));
        }
        [TestCase(900,50,false)] [TestCase(4900,50,false)] [TestCase(1800,20,false)] [TestCase(1400,50,true)]
        public void Nearby_world_or_rank_is_not_enough_route_direction_and_approach_time_are_required(float rivalDistance,float speed,bool expected)
        {
            var s=State();s.OpponentData["rival"].DistanceRoundTrack=rivalDistance;s.PositionAndMotionData.CarSpeed=speed;
            Assert.AreEqual(expected,NativeIncidentWatch.IsAhead(s,"rival","Nick Heidfeld","turn_4",T));
        }
        [Test]
        public void Start_finish_wrap_is_forward_but_slot_reuse_and_recovered_car_cancel_the_message()
        {
            var s=State();s.PositionAndMotionData.DistanceRoundTrack=4900;s.OpponentData["rival"].DistanceRoundTrack=50;
            Publish(s);var call=Tick(s,new LiveWarningDispatcher(),0);Assert.IsNotNull(call);
            s.OpponentData["rival"].Speed=30;Assert.IsFalse(call.IsValid(s,T));
            s.OpponentData["rival"].Speed=0;s.OpponentData["rival"].DriverRawName="Someone else";Assert.IsFalse(call.IsValid(s,T));
        }
        [Test]
        public void Pit_car_inactive_car_same_landmark_and_stale_source_do_not_create_ahead_warnings()
        {
            var s=State();var car=s.OpponentData["rival"];car.InPits=true;Assert.IsFalse(NativeIncidentWatch.IsAhead(s,"rival",car.DriverRawName,"turn_4",T));
            car.InPits=false;car.IsActive=false;Assert.IsFalse(NativeIncidentWatch.IsAhead(s,"rival",car.DriverRawName,"turn_4",T));car.IsActive=true;
            s.SessionData.stoppedInLandmark="turn_4";Assert.IsFalse(NativeIncidentWatch.IsAhead(s,"rival",car.DriverRawName,"turn_4",T));s.SessionData.stoppedInLandmark=null;
            s.SourceTelemetryUtc=T.AddSeconds(-2);Assert.IsFalse(NativeIncidentWatch.IsAhead(s,"rival",car.DriverRawName,"turn_4",T));
        }
        [Test]
        public void Overlap_delays_warning_and_still_current_warning_can_retry_but_passed_or_old_incident_cannot()
        {
            var s=State();Publish(s);var o=new LiveWarningDispatcher();Assert.IsNull(Tick(s,o,0,true));
            var first=Tick(s,o,1);Assert.IsNotNull(first);
            Assert.IsNull(Tick(s,o,10));var retry=Tick(s,o,11);Assert.IsNotNull(retry);
            Assert.IsFalse(retry.IsValid(s,T.AddSeconds(16)));s.PositionAndMotionData.DistanceRoundTrack=1200;
            Assert.IsFalse(retry.IsValid(s,T.AddSeconds(11)));
        }
        [Test]
        public void Remote_interesting_rival_is_recorded_without_an_ahead_call_and_new_session_drops_capture()
        {
            var s=State();s.OpponentData["rival"].DistanceRoundTrack=4000;Publish(s);var memory=new AiRaceMemory();var o=new LiveWarningDispatcher();
            watch.Observe(s,T,o,memory,false);o.Observe(s,T);Assert.IsNull(o.Take(s,T,false,m=>true));Assert.AreEqual(1,memory.GetEvents(TimeSpan.FromMinutes(1),T).Count);
            StringAssert.DoesNotContain("Heidfeld",memory.GetEvents(TimeSpan.FromMinutes(1),T)[0].Text);
            Publish(s);s.SessionData.SessionId++;watch.Reset();Assert.IsNull(Tick(s,new LiveWarningDispatcher(),0));
        }
        [Test]
        public void Repeated_capture_is_bounded_and_does_not_duplicate_race_memory()
        {
            var s=State();for(int i=0;i<1000;i++)Publish(s);var memory=new AiRaceMemory();watch.Observe(s,T,new LiveWarningDispatcher(),memory,true);
            Assert.AreEqual(1,memory.GetEvents(TimeSpan.FromMinutes(1),T).Count);
        }
        [TestCase(false,false)] [TestCase(true,true)]
        public void Native_ahead_coverage_suppresses_only_duplicate_front_call_never_native_rear_warning(bool rear,bool expected)
        {
            var s=State();var op=s.OpponentData["rival"];op.Speed=rear?65:0;
            var car=new TrackCarPosition(1,"rival",op,s,0,0,1150,rear?-100:150,150,rear?15:50,0,true);
            var snap=new TrackPositionSnapshot(s,T,new System.Collections.Generic.List<TrackCarPosition>{car});
            var traffic=new RaceTrafficWatch {CoveredAhead=key=>true};
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var confirmations=(System.Collections.Generic.Dictionary<string,DateTime>)typeof(RaceTrafficWatch).GetField("nativeRear",flags).GetValue(traffic);
            confirmations[op.DriverRawName]=T;
            var method=typeof(RaceTrafficWatch).GetMethod("Relevant",flags);
            Assert.AreEqual(expected,(bool)method.Invoke(traffic,new object[]{car,s,snap,T}));
        }
    }
}
