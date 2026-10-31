using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class TrackAwarenessTests
    {
        private static readonly DateTime T=new DateTime(2026,10,31,10,0,0,DateTimeKind.Utc);
        private static GameStateData State(SessionType type=SessionType.Race)
        {
            var s=new GameStateData(T.Ticks);
            s.carClass=new CarData.CarClass {carClassEnum=CarData.CarClassEnum.AUDI_TT_CUP};
            s.SessionData.SessionId=12;s.SessionData.SessionType=type;s.SessionData.SessionPhase=SessionPhase.Green;
            s.SessionData.TrackDefinition=new TrackDefinition("Test",0,5000,new float[]{0,0},new float[]{0,0});
            s.SessionData.ClassPosition=3;s.SessionData.OverallPosition=3;s.SessionData.CompletedLaps=2;
            s.SessionData.CurrentLapIsValid=true;s.PositionAndMotionData.DistanceRoundTrack=1000;s.PositionAndMotionData.CarSpeed=40;
            return s;
        }
        private static OpponentData Add(GameStateData s,string key="Bob",float distance=1100)
        {
            var car=new OpponentData {DriverRawName=key,ClassPosition=4,OverallPosition=4,CompletedLaps=2,
                DistanceRoundTrack=distance,Speed=35,CarClass=s.carClass};s.OpponentData[key]=car;return car;
        }
        private static RaceDataContext Context(GameStateData s,TrackAwareness tracks,DateTime now)
        {return new RaceDataContext {GameState=s,Tracks=tracks,NowUtc=now,LastTelemetryUtc=now,SendDriverNames=true,GameName="Synthetic"};}
        private static void Move(GameStateData s,OpponentData car,double seconds,float initialGap=100,float rivalSpeed=35)
        {
            s.PositionAndMotionData.DistanceRoundTrack=1000+(float)seconds*40;
            s.PositionAndMotionData.WorldPosition=new[]{s.PositionAndMotionData.DistanceRoundTrack,0f,0f};
            car.DistanceRoundTrack=1000+initialGap+(float)seconds*rivalSpeed;car.Speed=rivalSpeed;
            car.WorldPosition=new[]{car.DistanceRoundTrack,0f};
        }

        [Test]
        public void Mixed_own_xyz_and_opponent_xz_are_the_same_horizontal_frame()
        {
            var s=State();s.PositionAndMotionData.WorldPosition=new[]{10f,800f,20f};
            var car=Add(s);car.WorldPosition=new[]{40f,60f};var a=new TrackAwareness();a.Observe(s,T);
            Assert.AreEqual(50,a.Snapshot.Cars.Single().WorldDistanceM.Value,.001);
            car.WorldPosition=new[]{40f,-900f,60f};a.Observe(s,T.AddSeconds(.1));
            Assert.AreEqual(50,a.Snapshot.Cars.Single().WorldDistanceM.Value,.001);
        }
        [Test]
        public void Physical_car_ahead_is_independent_of_lower_classification_rank()
        {
            var s=State();Add(s);var a=new TrackAwareness();a.Observe(s,T);
            Assert.AreEqual(100,a.Snapshot.Cars.Single().RouteGapM);Assert.AreEqual(4,a.Snapshot.Cars.Single().ClassPosition);
        }
        [Test]
        public void Start_finish_wrap_is_not_a_lap_of_separation()
        {
            var s=State();s.PositionAndMotionData.DistanceRoundTrack=4990;var car=Add(s,distance:10);car.CompletedLaps=3;
            var a=new TrackAwareness();a.Observe(s,T);var point=a.Snapshot.Cars.Single();
            Assert.AreEqual(20,point.RouteGapM);Assert.AreEqual(0,point.LapOffset);
        }
        [Test]
        public void Lapping_car_can_be_physically_behind_without_becoming_classification_neighbour()
        {
            var s=State();var car=Add(s,distance:970);car.CompletedLaps=3;var a=new TrackAwareness();a.Observe(s,T);
            Assert.AreEqual(-30,a.Snapshot.Cars.Single().RouteGapM);Assert.AreEqual(1,a.Snapshot.Cars.Single().LapOffset);
        }
        [Test]
        public void Nearby_parallel_track_sections_do_not_turn_air_distance_into_route_distance()
        {
            var s=State();s.PositionAndMotionData.WorldPosition=new[]{1000f,0f,0f};var car=Add(s,distance:1800);
            car.WorldPosition=new[]{1010f,0f};var a=new TrackAwareness();a.Observe(s,T);
            Assert.AreEqual(10,a.Snapshot.Cars.Single().WorldDistanceM);Assert.AreEqual(800,a.Snapshot.Cars.Single().RouteGapM);
        }
        [Test]
        public void Inconsistent_zero_route_distance_is_not_used_as_physical_overlap()
        {
            var s=State();s.PositionAndMotionData.DistanceRoundTrack=0;s.PositionAndMotionData.WorldPosition=new[]{100f,0f,100f};
            var car=Add(s,distance:0);car.WorldPosition=new[]{900f,100f};var a=new TrackAwareness();a.Observe(s,T);
            Assert.IsFalse(a.Snapshot.Cars.Single().SpatiallyConsistent);
            Assert.AreEqual(JTokenType.Null,TrackAwareness.Read(Context(s,a,T))["fahrzeuge"][0]["versatz_entlang_strecke_m"].Type);
        }
        [TestCase(-1f)] [TestCase(6000f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void Invalid_lap_distance_stays_unknown(float distance)
        {
            var s=State();Add(s,distance:distance);var a=new TrackAwareness();a.Observe(s,T);
            Assert.IsNull(a.Snapshot.Cars.Single().RouteGapM);
        }
        [Test]
        public void Unknown_track_length_does_not_produce_start_finish_distance()
        {
            var s=State();s.SessionData.TrackDefinition=null;
            Assert.AreEqual(JTokenType.Null,TrackAwareness.Player(s)["meter_bis_start_ziel"].Type);
        }
        [Test]
        public void Invalid_or_absent_world_coordinates_are_not_zero_position_claims()
        {
            var s=State();s.PositionAndMotionData.WorldPosition=new[]{float.NaN,1f,1f};var a=new TrackAwareness();Add(s);a.Observe(s,T);
            Assert.IsNull(a.Snapshot.PlayerX);Assert.IsNull(a.Snapshot.Cars.Single().WorldDistanceM);
        }
        [Test]
        public void Snapshot_copies_coordinate_values_and_does_not_follow_mutated_game_state()
        {
            var s=State();s.PositionAndMotionData.WorldPosition=new[]{100f,0f,10f};var car=Add(s);car.WorldPosition=new[]{150f,10f};
            var a=new TrackAwareness();a.Observe(s,T);var saved=a.Snapshot;
            s.PositionAndMotionData.WorldPosition[0]=900;car.WorldPosition[0]=950;car.DistanceRoundTrack=4000;
            Assert.AreEqual(100,saved.PlayerX);Assert.AreEqual(150,saved.Cars[0].X);Assert.AreEqual(1100,saved.Cars[0].LapDistanceM);
        }
        [Test]
        public void Data_older_than_one_second_and_wrong_session_are_explicitly_unavailable()
        {
            var s=State();var a=new TrackAwareness();a.Observe(s,T);
            Assert.IsFalse((bool)TrackAwareness.Read(Context(s,a,T.AddSeconds(1.01)))["verfuegbar"]);
            s.SessionData.SessionId++;
            Assert.IsFalse((bool)TrackAwareness.Read(Context(s,a,T))["verfuegbar"]);
        }
        [Test]
        public void Closing_rate_requires_continuous_multiple_samples_not_one_speed_reading()
        {
            var s=State();var car=Add(s);var a=new TrackAwareness();Move(s,car,0);a.Observe(s,T);
            Assert.IsNull(a.Snapshot.Cars.Single().ClosingMps);
            for(int i=1;i<=20;i++){Move(s,car,i/10d);a.Observe(s,T.AddSeconds(i/10d));}
            Assert.AreEqual(5,a.Snapshot.Cars.Single().ClosingMps.Value,.01);
        }
        [Test]
        public void Sampling_is_capped_at_twenty_hz()
        {
            var s=State();var a=new TrackAwareness();int sampled=0;
            for(int i=0;i<1000;i++)if(a.Observe(s,T.AddMilliseconds(i)))sampled++;
            Assert.LessOrEqual(sampled,20);Assert.GreaterOrEqual(sampled,19);
        }
        [Test]
        public void Session_change_disconnect_and_position_reset_break_the_trend()
        {
            var s=State();var car=Add(s);var a=new TrackAwareness();
            for(int i=0;i<=20;i++){Move(s,car,i/10d);a.Observe(s,T.AddSeconds(i/10d));}
            Assert.IsNotNull(a.Snapshot.Cars.Single().ClosingMps);
            s.SessionData.SessionId++;a.Observe(s,T.AddSeconds(2.1));Assert.IsNull(a.Snapshot.Cars.Single().ClosingMps);
            a.Observe(s,T.AddSeconds(6));Assert.IsNull(a.Snapshot.Cars.Single().ClosingMps);
            s.PositionAndMotionData.DistanceRoundTrack=4000;a.Observe(s,T.AddSeconds(6.1));Assert.IsNull(a.Snapshot.Cars.Single().ClosingMps);
        }
        [Test]
        public void Inactive_and_pit_cars_do_not_supply_closing_trends()
        {
            var s=State();var car=Add(s);car.InPits=true;var a=new TrackAwareness();
            for(int i=0;i<=20;i++){Move(s,car,i/10d);a.Observe(s,T.AddSeconds(i/10d));}
            Assert.IsNull(a.Snapshot.Cars.Single().ClosingMps);
            car.IsActive=false;a.Observe(s,T.AddSeconds(2.1));Assert.IsEmpty(a.Snapshot.Cars);
        }
        [Test]
        public void History_and_published_field_are_bounded()
        {
            var s=State();for(int i=0;i<400;i++)Add(s,"car"+i,1100+i);var a=new TrackAwareness();a.Observe(s,T);
            Assert.AreEqual(TrackAwareness.MaxCars,a.Snapshot.Cars.Count);
            Assert.AreEqual(12,((JArray)TrackAwareness.Read(Context(s,a,T))["fahrzeuge"]).Count);
        }
        [Test]
        public void Driver_name_opt_out_applies_to_spatial_tool()
        {
            var s=State();Add(s,"PrivateRival");var a=new TrackAwareness();a.Observe(s,T);var c=Context(s,a,T);c.SendDriverNames=false;
            StringAssert.DoesNotContain("PrivateRival",TrackAwareness.Read(c).ToString());
        }
        [Test]
        public void Own_position_is_exposed_even_when_model_only_asks_for_race_state()
        {
            var s=State();s.PitData.OnOutLap=true;var data=RaceDataTools.Execute("get_race_state",Context(s,null,T));
            Assert.AreEqual(4000,(double)data["streckenposition"]["meter_bis_start_ziel"]);
            Assert.IsTrue((bool)data["outlap"]);
        }
        [Test]
        public void Position_movement_does_not_invalidate_unchanged_fuel_or_tyre_evidence()
        {
            var s=State();var c=Context(s,null,T);var evidence=new RaceSpeechEvidence(c);
            s.PositionAndMotionData.DistanceRoundTrack+=20;c.NowUtc=c.LastTelemetryUtc=T.AddSeconds(.5);
            Assert.IsTrue(evidence.IsCurrent(c));
        }
        [Test]
        public void Sustained_traffic_closing_produces_one_non_safety_call_and_cooldown()
        {
            var s=State(SessionType.Practice);var car=Add(s);var a=new TrackAwareness();var watch=new TrackSituationWatch();
            var calls=new List<ObservedRaceCall>();
            for(int i=0;i<=40;i++){var now=T.AddSeconds(i/10d);Move(s,car,i/10d);a.Observe(s,now);var call=watch.Evaluate(a,s,now);if(call!=null)calls.Add(call);}
            Assert.AreEqual(1,calls.Count);Assert.AreEqual("traffic-catching",calls[0].Topic);Assert.AreEqual(TeamMember.Ben,calls[0].Member);
            Assert.Less(calls[0].Priority,6);StringAssert.DoesNotContain("frei",calls[0].Text.ToLowerInvariant());
        }
        [Test]
        public void Spatial_call_is_withdrawn_when_the_identified_car_pits_or_disappears()
        {
            var s=State();var car=Add(s);var a=new TrackAwareness();
            for(int i=0;i<=20;i++){Move(s,car,i/10d);a.Observe(s,T.AddSeconds(i/10d));}
            var id=a.Snapshot.Cars[0].Id;
            Assert.IsTrue(TrackSituationWatch.StillRelevant(a,s,"Bob",id,"traffic-catching",T.AddSeconds(2)));
            car.InPits=true;Assert.IsFalse(TrackSituationWatch.StillRelevant(a,s,"Bob",id,"traffic-catching",T.AddSeconds(2)));
            s.OpponentData.Clear();Assert.IsFalse(TrackSituationWatch.StillRelevant(a,s,"Bob",id,"traffic-catching",T.AddSeconds(2)));
        }
        [Test]
        public void Outlap_validity_change_is_not_an_invalid_timed_lap()
        {
            var before=State(SessionType.Practice);var after=State(SessionType.Practice);before.PitData.OnOutLap=after.PitData.OnOutLap=true;
            after.SessionData.CurrentLapIsValid=false;var watch=new IncidentWatch();
            Assert.IsEmpty(watch.Evaluate(before,after,T,false));Assert.AreEqual(0,watch.InvalidLaps);
        }
        [Test]
        public void Confirmed_outlap_finish_crossing_announces_first_timed_lap_once()
        {
            var before=State(SessionType.Practice);before.PitData.OnOutLap=true;before.PositionAndMotionData.DistanceRoundTrack=4990;before.SessionData.SessionRunningTime=100;
            var after=State(SessionType.Practice);after.SessionData.IsNewLap=true;after.SessionData.CompletedLaps=3;after.PositionAndMotionData.DistanceRoundTrack=10;after.SessionData.SessionRunningTime=100.1f;
            var observer=new SessionLapObserver();observer.Observe(before,null,T,false);
            observer.Observe(after,null,T.AddSeconds(.1),false,before);
            var call=observer.Take(after,T.AddSeconds(.1),false);Assert.IsNotNull(call);Assert.AreEqual("timed-lap-start",call.Topic);
            call.MarkPlaybackStarted(T.AddSeconds(.1));Assert.IsNull(observer.Take(after,T.AddSeconds(.2),false));
        }
        [TestCase(4990, 10, 10)]
        [TestCase(4100, 100, 0.1f)]
        public void Clock_gap_or_teleport_across_start_line_is_not_a_timed_lap_start(float beforeDistance, float afterDistance, float seconds)
        {
            var before=State(SessionType.Practice);before.PitData.OnOutLap=true;
            before.PositionAndMotionData.DistanceRoundTrack=beforeDistance;before.SessionData.SessionRunningTime=100;
            var after=State(SessionType.Practice);after.SessionData.IsNewLap=true;
            after.PositionAndMotionData.DistanceRoundTrack=afterDistance;after.SessionData.SessionRunningTime=100+seconds;
            var observer=new SessionLapObserver();observer.Observe(before,null,T,false);
            observer.Observe(after,null,T.AddSeconds(seconds),false,before);
            Assert.IsNull(observer.Take(after,T.AddSeconds(seconds),false));
        }
        [Test]
        public void Joining_mid_lap_or_leaving_pit_is_not_a_start_line_crossing()
        {
            var before=State(SessionType.Practice);before.PitData.OnOutLap=true;before.PositionAndMotionData.DistanceRoundTrack=200;
            var after=State(SessionType.Practice);after.SessionData.IsNewLap=true;after.PositionAndMotionData.DistanceRoundTrack=220;
            var observer=new SessionLapObserver();observer.Observe(before,null,T,false);observer.Observe(after,null,T.AddSeconds(.1),false,before);
            Assert.IsNull(observer.Take(after,T.AddSeconds(.1),false));
        }
        [Test]
        public void Section_lookup_uses_mapped_landmarks_and_wraps_the_next_start()
        {
            var s=State();s.PositionAndMotionData.DistanceRoundTrack=4950;
            s.SessionData.TrackDefinition.trackLandmarks=new List<TrackLandmark> {
                new TrackLandmark {landmarkNames=new List<string>{"last_turn"},distanceRoundLapStart=4900,distanceRoundLapEnd=4990},
                new TrackLandmark {landmarkNames=new List<string>{"first_turn"},distanceRoundLapStart=50,distanceRoundLapEnd=200}};
            var data=TrackAwareness.Player(s);
            Assert.AreEqual("Last Turn",(string)data["abschnitt"]);Assert.AreEqual("First Turn",(string)data["naechster_abschnitt"]);
            Assert.AreEqual(100,(double)data["bis_naechster_abschnitt_m"]);Assert.AreEqual(50,(double)data["meter_bis_start_ziel"]);
        }
        [Test]
        public void Reused_session_id_and_clock_rollback_clear_prior_motion()
        {
            var s=State();var car=Add(s);var a=new TrackAwareness();
            for(int i=0;i<=20;i++){Move(s,car,i/10d);a.Observe(s,T.AddSeconds(i/10d));}
            Assert.IsNotNull(a.Snapshot.Cars[0].ClosingMps);
            s.SessionData.IsNewSession=true;a.Observe(s,T.AddSeconds(2.1));Assert.IsNull(a.Snapshot.Cars[0].ClosingMps);
            a.Observe(s,T);Assert.IsNull(a.Snapshot.Cars[0].ClosingMps);
        }
        [Test]
        public void Inactive_entries_do_not_hide_active_cars_behind_the_bound()
        {
            var s=State();for(int i=0;i<300;i++)Add(s,"inactive"+i).IsActive=false;
            Add(s,"present");var a=new TrackAwareness();a.Observe(s,T);Assert.AreEqual("present",a.Snapshot.Cars.Single().Name);
        }
        [TestCase(false)] [TestCase(true)]
        public void Faster_class_or_practice_traffic_behind_has_a_non_safety_observation(bool practice)
        {
            var s=State(practice ? SessionType.Practice : SessionType.Race);var car=Add(s);
            if(!practice)car.CarClass=new CarData.CarClass {carClassEnum=CarData.CarClassEnum.ALPINE};
            var a=new TrackAwareness();var watch=new TrackSituationWatch();ObservedRaceCall found=null;
            for(int i=0;i<=20;i++){var now=T.AddSeconds(i/10d);Move(s,car,i/10d,-150,50);a.Observe(s,now);found=watch.Evaluate(a,s,now)??found;}
            Assert.IsNotNull(found);Assert.AreEqual("traffic-approaching",found.Topic);Assert.AreEqual(TeamMember.Ben,found.Member);
        }
        [Test]
        public void Unknown_class_is_not_evidence_for_a_different_faster_class()
        {
            var s=State();var car=Add(s);car.CarClass=new CarData.CarClass();var a=new TrackAwareness();var watch=new TrackSituationWatch();
            for(int i=0;i<=20;i++){var now=T.AddSeconds(i/10d);Move(s,car,i/10d,-150,50);a.Observe(s,now);Assert.IsNull(watch.Evaluate(a,s,now));}
            Assert.AreEqual(JTokenType.Null,TrackAwareness.Read(Context(s,a,T.AddSeconds(2)))["fahrzeuge"][0]["gleiche_klasse"].Type);
        }
        [Test]
        public void Track_limit_warning_on_outlap_does_not_claim_a_timed_lap_was_invalidated()
        {
            var before=State(SessionType.Practice);var after=State(SessionType.Practice);
            before.PitData.OnOutLap=after.PitData.OnOutLap=true;before.SessionData.CurrentLapIsValid=after.SessionData.CurrentLapIsValid=false;
            before.PenaltiesData.CutTrackWarnings=0;after.PenaltiesData.CutTrackWarnings=1;
            var calls=new IncidentWatch().Evaluate(before,after,T,false);
            Assert.AreEqual(1,calls.Count);StringAssert.DoesNotContain("ungültig",calls[0].Value);
        }
        [Test]
        public void Spatial_jsonl_keeps_all_rows_in_bounded_batches_not_only_first_twelve_numbers()
        {
            string root=Environment.GetEnvironmentVariable("RCCA_DATA_ROOT");Assert.IsNotEmpty(root);
            string folder=Path.Combine(root,"spatial-test-"+Guid.NewGuid().ToString("N"));
            var s=State();for(int i=0;i<35;i++)Add(s,"car"+i,1100+i);var a=new TrackAwareness();a.Observe(s,T);
            try {
                LiveDiagnostics.Start(folder);LiveDiagnostics.Sample(null,s);LiveDiagnostics.TrackPositions(a.Snapshot);LiveDiagnostics.TrackPositions(a.Snapshot);LiveDiagnostics.Shutdown();
                var rows=Directory.GetFiles(folder,"*.jsonl").SelectMany(File.ReadLines).Select(JObject.Parse).Where(r=>(string)r["kind"]=="track.positions").ToArray();
                Assert.AreEqual(3,rows.Length);Assert.AreEqual(36,rows.Sum(r=>(int)r["data"]["sample_count"]));
                Assert.AreEqual(144,((JArray)rows[0]["data"]["position_matrix"]).Count);
                Assert.IsTrue(rows.All(r=>(int)r["session_id"]==12));
                StringAssert.DoesNotContain("car34",string.Join("",rows.Select(r=>r.ToString())));
            } finally {LiveDiagnostics.Shutdown();}
        }
    }
}
