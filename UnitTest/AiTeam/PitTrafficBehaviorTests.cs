using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class PitTrafficBehaviorTests
    {
        private static readonly DateTime T=new DateTime(2026,11,1,10,0,0,DateTimeKind.Utc);
        private static GameStateData State(SessionType type=SessionType.Practice)
        {
            var s=new GameStateData(T.Ticks);
            s.SessionData.SessionId=25;s.SessionData.SessionType=type;s.SessionData.SessionPhase=SessionPhase.Green;
            s.SessionData.TrackDefinition=new TrackDefinition("Synthetic",0,5000,new float[]{0,0},new float[]{0,0});
            s.SessionData.SessionRunningTime=100;s.PositionAndMotionData.CarSpeed=50;s.PositionAndMotionData.DistanceRoundTrack=1000;
            s.carClass=new CarData.CarClass {carClassEnum=CarData.CarClassEnum.AUDI_TT_CUP};return s;
        }
        private static OpponentData Car(GameStateData s,bool outlap=false,bool inlap=false)
        {
            var car=new OpponentData {DriverRawName="Test Driver",CarClass=s.carClass,CompletedLaps=2};
            car.OpponentLapData.Add(new LapData {OutLap=outlap,InLap=inlap});s.OpponentData["rival"]=car;return car;
        }
        private static void Move(GameStateData s,OpponentData car,double seconds,float gap,float rivalSpeed,float ownSpeed=50)
        {
            s.Now=T.AddSeconds(seconds);s.SessionData.SessionRunningTime=100+(float)seconds;
            s.PositionAndMotionData.CarSpeed=ownSpeed;
            s.PositionAndMotionData.DistanceRoundTrack=(1000+(float)seconds*ownSpeed)%5000;
            s.PositionAndMotionData.WorldPosition=new[]{s.PositionAndMotionData.DistanceRoundTrack,0f,0f};
            car.DistanceRoundTrack=(1000+gap+(float)seconds*rivalSpeed)%5000;car.Speed=rivalSpeed;
            car.WorldPosition=new[]{car.DistanceRoundTrack,0f,0f};
        }
        private static ObservedRaceCall Drive(GameStateData s,OpponentData car,float gap,float speed,double duration=2,float ownSpeed=50)
        {
            var tracks=new TrackAwareness();var watch=new TrackSituationWatch();ObservedRaceCall found=null;
            for(int i=0;i<=duration*10;i++)
            {double t=i/10d;Move(s,car,t,gap,speed,ownSpeed);tracks.Observe(s,T.AddSeconds(t));found=watch.Evaluate(tracks,s,T.AddSeconds(t))??found;}
            return found;
        }

        [TestCase(SessionType.Practice,true)] [TestCase(SessionType.Qualify,true)]
        [TestCase(SessionType.Race,false)] [TestCase(SessionType.HotLap,false)]
        public void Physical_slow_traffic_is_only_announced_in_practice_and_qualifying(SessionType type,bool expected)
        {var s=State(type);Assert.AreEqual(expected,Drive(s,Car(s,true),300,10)!=null);}

        [Test]
        public void Distant_stopped_car_is_not_announced_twenty_seconds_early()
        {var s=State();Assert.IsNull(Drive(s,Car(s),1000,0));}

        [Test]
        public void First_stopped_car_alert_is_in_the_four_to_six_second_intercept_window()
        {
            var s=State();var car=Car(s);var tracks=new TrackAwareness();var watch=new TrackSituationWatch();ObservedRaceCall call=null;
            for(int i=0;i<=50 && call==null;i++)
            {double t=i/10d;Move(s,car,t,400,0);tracks.Observe(s,T.AddSeconds(t));call=watch.Evaluate(tracks,s,T.AddSeconds(t));}
            Assert.IsNotNull(call);double seconds=tracks.Snapshot.Cars.Single().RouteGapM.Value/tracks.Snapshot.Cars.Single().ClosingMps.Value;
            Assert.That(seconds,Is.InRange(4d,6.01d));CollectionAssert.Contains(TrackSituationWatch.StoppedPhrases,call.Text);
        }

        [TestCase(48,true)] [TestCase(25,false)] [TestCase(20,false)]
        public void Small_pace_differences_and_ordinary_braking_are_not_slow_traffic(float speed,bool outlap)
        {var s=State();Assert.IsNull(Drive(s,Car(s,outlap),200,speed));}

        [TestCase(true,false)] [TestCase(false,true)]
        public void A_genuinely_slow_outlap_or_inlap_is_recognized(bool outlap,bool inlap)
        {var s=State();Assert.IsNotNull(Drive(s,Car(s,outlap,inlap),220,20));}

        [Test]
        public void A_crawling_car_can_be_reported_without_inventing_its_lap_status()
        {var s=State();var call=Drive(s,Car(s),280,5);Assert.IsNotNull(call);StringAssert.DoesNotContain("Auslaufrunde",call.Text);}

        [TestCase(SessionType.Practice,true)] [TestCase(SessionType.Qualify,true)] [TestCase(SessionType.Race,false)]
        public void Much_faster_rear_traffic_uses_closing_speed_in_nonrace_sessions(SessionType type,bool expected)
        {var s=State(type);var call=Drive(s,Car(s),-150,70);Assert.AreEqual(expected,call!=null);if(call!=null)Assert.AreEqual("traffic-approaching",call.Topic);}

        [Test]
        public void A_rear_car_with_a_small_speed_advantage_does_not_trigger_a_warning()
        {var s=State();Assert.IsNull(Drive(s,Car(s),-70,53));}

        [TestCase(true)] [TestCase(false)]
        public void Cars_in_pits_do_not_generate_ontrack_traffic_alerts(bool own)
        {var s=State();var car=Car(s);if(own)s.PitData.InPitlane=true;else car.InPits=true;Assert.IsNull(Drive(s,car,300,0));}

        [Test]
        public void A_delivered_call_is_not_repeated_each_sample_for_the_same_opponent()
        {
            var s=State();var car=Car(s);var tracks=new TrackAwareness();var watch=new TrackSituationWatch();int delivered=0;
            for(int i=0;i<=45;i++)
            {
                double t=i/10d;Move(s,car,t,300,0);tracks.Observe(s,T.AddSeconds(t));var call=watch.Evaluate(tracks,s,T.AddSeconds(t));
                if(call!=null){Assert.IsTrue(call.TryReserve(T.AddSeconds(t)));call.MarkPlaybackStarted(T.AddSeconds(t));delivered++;}
            }
            Assert.AreEqual(1,delivered);
        }

        [Test]
        public void Queued_stopped_car_alert_expires_when_the_car_moves_or_snapshot_is_stale()
        {
            var s=State();var car=Car(s);var tracks=new TrackAwareness();var watch=new TrackSituationWatch();ObservedRaceCall call=null;
            for(int i=0;i<=20;i++){double t=i/10d;Move(s,car,t,300,0);tracks.Observe(s,T.AddSeconds(t));call=watch.Evaluate(tracks,s,T.AddSeconds(t))??call;}
            Assert.IsNotNull(call);Assert.IsTrue(call.IsValid(s,T.AddSeconds(2)));
            car.Speed=5;Assert.IsFalse(call.IsValid(s,T.AddSeconds(2.1)),"A stopped-car statement must remain true at playback.");
            car.Speed=0;Assert.IsFalse(call.IsValid(s,T.AddSeconds(3.1)),"Stale physical positions cannot validate a traffic call.");
        }

        [Test]
        public void Pit_approach_announces_actual_limit_before_the_lane_transition()
        {
            var s=State();s.PitData.IsApproachingPitlane=true;s.PitData.PitSpeedLimit=80/3.6f;
            var watch=new PitLaneWatch();watch.Observe(s,T);var call=watch.Take(s,T,false,7);
            Assert.IsNotNull(call);Assert.IsFalse(s.PitData.InPitlane);StringAssert.Contains("80",call.Text);
            Assert.IsTrue(call.IsValid(s,T));s.PitData.InPitlane=true;Assert.IsFalse(call.IsValid(s,T));
        }

        [TestCase(true,true)] [TestCase(false,false)]
        public void A_valid_route_approach_anchor_requires_pit_intent(bool requested,bool expected)
        {
            var s=State();s.PitData.HasRequestedPitStop=requested;s.PitData.PitSpeedLimit=60/3.6f;
            s.SessionData.TrackDefinition.pitApproachPoint=new[]{1400f,1400f,0f};
            var watch=new PitLaneWatch();watch.Observe(s,T);Assert.AreEqual(expected,watch.Take(s,T,false,7)!=null);
        }

        [Test]
        public void Generic_last_three_hundred_metres_anchor_is_never_a_speed_marker()
        {
            var s=State();s.PitData.HasRequestedPitStop=true;s.PositionAndMotionData.DistanceRoundTrack=4700;
            Assert.IsFalse(PitLaneWatch.Approaching(s));
        }

        [TestCase(0)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void Invalid_or_stationary_motion_does_not_create_a_pit_approach(float speed)
        {var s=State();s.PositionAndMotionData.CarSpeed=speed;s.PitData.IsApproachingPitlane=true;Assert.IsFalse(PitLaneWatch.Approaching(s));}

        [Test]
        public void Unknown_limit_uses_only_a_signage_reminder_and_never_claims_overspeed()
        {
            var s=State();s.PitData.IsApproachingPitlane=true;s.PitData.PitSpeedLimit=-1;
            var watch=new PitLaneWatch();watch.Observe(s,T);var call=watch.Take(s,T,false,7);
            Assert.IsNotNull(call);Assert.IsFalse(call.Text.Any(char.IsDigit));
            CollectionAssert.Contains(PitLaneWatch.UnknownLimitPhrases,call.Text);
            s.PitData.InPitlane=true;watch.Observe(s,T.AddSeconds(.3));var inside=watch.Take(s,T.AddSeconds(.3),false,7);
            Assert.IsNotNull(inside);Assert.AreEqual("pit-limit",inside.Topic);
            CollectionAssert.Contains(PitLaneWatch.UnknownLimitPhrases,inside.Text);
        }

        [Test]
        public void Actual_limit_arriving_during_approach_replaces_the_generic_reminder()
        {
            var s=State();s.PitData.IsApproachingPitlane=true;var watch=new PitLaneWatch();watch.Observe(s,T);
            var old=watch.Take(s,T,false,7);s.PitData.PitSpeedLimit=60/3.6f;watch.Observe(s,T.AddSeconds(.1));
            var call=watch.Take(s,T.AddSeconds(.1),false,7);Assert.IsNotNull(call);StringAssert.Contains("60",call.Text);Assert.IsFalse(old.IsValid(s,T.AddSeconds(.1)));
        }

        [Test]
        public void Delivered_approach_is_not_repeated_immediately_after_crossing_into_the_lane()
        {
            var s=State();s.PitData.IsApproachingPitlane=true;s.PitData.PitSpeedLimit=80/3.6f;
            var watch=new PitLaneWatch();watch.Observe(s,T);var call=watch.Take(s,T,false,7);call.MarkPlaybackStarted(T);
            s.PitData.InPitlane=true;s.PositionAndMotionData.CarSpeed=10;watch.Observe(s,T.AddSeconds(.1));Assert.IsNull(watch.Take(s,T.AddSeconds(.1),false));
        }

        [Test]
        public void Departure_after_parking_gives_one_fresh_varied_limit_reminder()
        {
            var s=State();s.PitData.InPitlane=true;s.PitData.PitSpeedLimit=80/3.6f;s.PositionAndMotionData.CarSpeed=0;
            var watch=new PitLaneWatch();watch.Observe(s,T);var first=watch.Take(s,T,false);first.MarkPlaybackStarted(T);
            for(int i=1;i<=25;i++){s.SessionData.SessionRunningTime=100+i;watch.Observe(s,T.AddSeconds(i));watch.Take(s,T.AddSeconds(i),false);}
            s.PositionAndMotionData.CarSpeed=4;s.SessionData.SessionRunningTime=126;watch.Observe(s,T.AddSeconds(26));var call=watch.Take(s,T.AddSeconds(26),false);
            Assert.IsNotNull(call);StringAssert.Contains("80",call.Text);Assert.AreNotEqual(first.Text,call.Text);call.MarkPlaybackStarted(T.AddSeconds(26));
            s.SessionData.SessionRunningTime=127;watch.Observe(s,T.AddSeconds(27));Assert.IsNull(watch.Take(s,T.AddSeconds(27),false));
        }

        [TestCase(40)] [TestCase(60)] [TestCase(80)] [TestCase(97)] [TestCase(120)]
        public void Numeric_reminders_are_varied_and_cacheable_in_the_selected_voice(int kmh)
        {
            var phrases=PitLaneWatch.Phrases(kmh/3.6f).ToArray();Assert.AreEqual(3,phrases.Distinct().Count());
            foreach(string p in phrases){StringAssert.Contains(kmh.ToString(),p);Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Mik,p));}
            foreach(string p in PitLaneWatch.SpeedPhrases(kmh/3.6f)){StringAssert.Contains(kmh.ToString(),p);Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Mik,p));}
            Assert.IsFalse(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Mik,"Invented: 80 Kilometer pro Stunde."));
        }
        [Test]
        public void Missing_zero_opponent_speed_does_not_claim_a_moving_car_is_stopped()
        {
            var s=State();var car=Car(s);var tracks=new TrackAwareness();var watch=new TrackSituationWatch();
            for(int i=0;i<=25;i++)
            {
                double t=i/10d;Move(s,car,t,220,20);car.Speed=0;tracks.Observe(s,T.AddSeconds(t));
                Assert.IsNull(watch.Evaluate(tracks,s,T.AddSeconds(t)),"Reported speed conflicts with observed route movement.");
            }
            var ctx=new RaceDataContext {GameState=s,Tracks=tracks,NowUtc=T.AddSeconds(2.5),LastTelemetryUtc=T.AddSeconds(2.5)};
            var data=TrackAwareness.Read(ctx)["fahrzeuge"][0];
            Assert.AreEqual(0,(double)data["tempo_roh_m_s"]);Assert.AreEqual(JTokenType.Null,data["tempo_m_s"].Type);
            Assert.IsFalse((bool)data["tempo_zu_positionen_plausibel"]);
        }
        [TestCase(22.222222f,true)] [TestCase(-1f,false)] [TestCase(float.NaN,false)] [TestCase(float.PositiveInfinity,false)]
        public void Pit_tool_exposes_only_a_finite_measured_limit_with_explicit_units(float limit,bool known)
        {
            var s=State();s.PitData.PitSpeedLimit=limit;
            var ctx=new RaceDataContext {GameState=s,NowUtc=T,LastTelemetryUtc=T,Memory=new AiRaceMemory()};
            var data=RaceDataTools.Execute("get_pit_info",ctx);
            Assert.AreEqual(known,(bool)data["boxenlimit_verfuegbar"]);
            if(known){Assert.AreEqual(80,(int)data["boxenlimit_kmh"]);Assert.IsNotNull(data["boxenlimit_ms"]);}
            else{Assert.IsNull(data["boxenlimit_kmh"]);Assert.IsNull(data["boxenlimit_ms"]);}
        }
    }
}
