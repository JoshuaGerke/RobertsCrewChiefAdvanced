using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using CrewChiefV4.Events;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam"),NonParallelizable]
    public class LiveRaceReliabilityTests
    {
        private static readonly DateTime T=new DateTime(2026,11,1,12,0,0,DateTimeKind.Utc);
        private static GameStateData State()
        {
            var s=new GameStateData(T.Ticks);
            s.SessionData.SessionId=44;s.SessionData.SessionType=SessionType.Race;s.SessionData.SessionPhase=SessionPhase.Green;
            s.SessionData.ClassPosition=2;s.SessionData.NumCarsOverall=s.SessionData.NumCarsInPlayerClass=3;
            s.SessionData.LapsDeltaFront=s.SessionData.LapsDeltaBehind=0; // Fixture explicitly models same-lap intervals.
            s.SessionData.TrackDefinition=new TrackDefinition("Test",0,5000,new float[]{0,0},new float[]{0,0});
            s.carClass=new CarData.CarClass {carClassEnum=CarData.CarClassEnum.AUDI_TT_CUP};
            s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=50;s.EngineData.EngineWaterTemp=90;
            return s;
        }
        private static OpponentData Car(GameStateData s,bool otherClass=false,bool rear=false,string key="rival")
        {
            var c=new OpponentData {DriverRawName="Native Driver",CarNumber="5",ClassPosition=rear?3:1,
                CarClass=otherClass?new CarData.CarClass {carClassEnum=CarData.CarClassEnum.GT3}:s.carClass};
            s.OpponentData[key]=c;
            if(rear)s.SessionData.OpponentKeyBehind=key;else s.SessionData.OpponentKeyInFront=key;
            return c;
        }
        private static float[] World(double distance)
        {double a=distance/5000*2*Math.PI;return new[]{(float)(Math.Cos(a)*795.77),0f,(float)(Math.Sin(a)*795.77)};}
        private static void Move(GameStateData s,OpponentData c,double time,double gap,double speed,double start=1000,DateTime? origin=null)
        {
            s.Now=(origin??T).AddSeconds(time);s.SessionData.SessionRunningTime=100+(float)time;
            double own=start+50*time, other=start+gap+speed*time;
            s.SessionData.CompletedLaps=3+(int)Math.Floor(own/5000);c.CompletedLaps=3+(int)Math.Floor(other/5000);
            s.PositionAndMotionData.DistanceRoundTrack=(float)(own%5000);c.DistanceRoundTrack=(float)(other%5000);
            s.PositionAndMotionData.CarSpeed=50;c.Speed=(float)speed;
            s.PositionAndMotionData.WorldPosition=World(own);c.WorldPosition=World(other);
        }
        private static ObservedRaceCall Traffic(GameStateData s,OpponentData c,double gap,double speed,out TrackAwareness tracks,out RaceTrafficWatch watch)
        {
            tracks=new TrackAwareness();watch=new RaceTrafficWatch();ObservedRaceCall call=null;
            for(int i=0;i<=35;i++)
            {Move(s,c,i*.1,gap,speed);tracks.Observe(s,T.AddSeconds(i*.1));watch.Observe(tracks,s,T.AddSeconds(i*.1));call=watch.Take(s,T.AddSeconds(i*.1),false,true)??call;}
            return call;
        }
        [SetUp] public void Setup() {NativeTrafficBridge.Enabled=false;NativeTrafficBridge.Clear();}
        [TearDown] public void Cleanup() {NativeTrafficBridge.Enabled=false;NativeTrafficBridge.Clear();}

        [Test]
        public void Reenabling_Ben_restores_native_traffic_handoff_without_restarting_or_changing_Sara()
        {
            var user=UserSettings.GetUserSettings();
            string[] keys={"enable_ai_team","enable_ai_team_replaces_crew_chief_voice","enable_ai_team_ben"};
            bool[] saved=keys.Select(k=>user.getBoolean(k)).ToArray();
            var filter=CrewChiefV4.Audio.AudioPlayer.messageFilter;
            bool mute=CrewChiefV4.Audio.AudioPlayer.aiTeamSpotterMuted;
            var service=AiTeamService.Instance;bool sara=service.Settings.SaraEnabled;
            try
            {
                user.setProperty(keys[0],true);user.setProperty(keys[1],true);
                service.SetMemberEnabled(TeamMember.Ben,false);
                NativeTrafficBridge.Publish(State(),new[]{"Traffic"},true);
                NativeTrafficBridge.Detection detection;Assert.IsFalse(NativeTrafficBridge.TryTake(out detection));
                service.SetMemberEnabled(TeamMember.Ben,true);
                NativeTrafficBridge.Publish(State(),new[]{"Traffic"},true);
                Assert.IsTrue(NativeTrafficBridge.TryTake(out detection));
                CollectionAssert.AreEqual(new[]{"Traffic"},detection.Names);
                Assert.AreEqual(sara,service.Settings.SaraEnabled);
            }
            finally
            {
                for(int i=0;i<keys.Length;i++)user.setProperty(keys[i],saved[i]);
                user.saveUserSettings();service.ReloadSettings();
                CrewChiefV4.Audio.AudioPlayer.messageFilter=filter;
                CrewChiefV4.Audio.AudioPlayer.aiTeamSpotterMuted=mute;
            }
        }
        [TestCase(0)] [TestCase(3)] [TestCase(5)]
        public void Race_warns_of_stopped_or_crawling_vehicle_without_outlap_requirement(float speed)
        {
            var s=State();var c=Car(s);TrackAwareness tracks;RaceTrafficWatch watch;
            var call=Traffic(s,c,300,speed,out tracks,out watch);
            Assert.IsNotNull(call);Assert.AreEqual("race-hazard-ahead",call.Topic);Assert.AreEqual(TeamMember.Ben,call.Member);
            Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(call.Member,call.Text));
        }
        [TestCase(25)] [TestCase(35)] [TestCase(48)]
        public void Normal_corner_braking_without_reference_does_not_invent_a_race_hazard(float speed)
        {
            var s=State();var c=Car(s);TrackAwareness tracks;RaceTrafficWatch watch;
            Assert.IsNull(Traffic(s,c,220,speed,out tracks,out watch));
        }
        [Test]
        public void Distant_stopped_vehicle_is_not_announced_early()
        {
            var s=State();TrackAwareness tracks;RaceTrafficWatch watch;
            Assert.IsNull(Traffic(s,Car(s),1200,0,out tracks,out watch));
        }
        [TestCase(true,true)] [TestCase(false,false)]
        public void Closing_other_class_is_automatic_but_normal_same_class_battle_is_not_spammed(bool otherClass,bool expected)
        {
            var s=State();TrackAwareness tracks;RaceTrafficWatch watch;
            var call=Traffic(s,Car(s,otherClass,true),-250,70,out tracks,out watch);
            Assert.AreEqual(expected,call!=null);if(call!=null)Assert.AreEqual("race-traffic-rear",call.Topic);
        }
        [Test]
        public void Cars_in_pits_never_create_race_traffic_warnings()
        {
            var s=State();var c=Car(s,true,true);c.InPits=true;TrackAwareness tracks;RaceTrafficWatch watch;
            Assert.IsNull(Traffic(s,c,-150,70,out tracks,out watch));
        }
        [Test]
        public void Local_traffic_delivery_rechecks_motion_pits_and_freshness()
        {
            var s=State();var c=Car(s);TrackAwareness tracks;RaceTrafficWatch watch;
            var call=Traffic(s,c,300,0,out tracks,out watch);Assert.IsNotNull(call);
            Assert.IsTrue(call.IsValid(s,T.AddSeconds(3.5)));
            Assert.IsNull(watch.Take(s,T.AddSeconds(3.5),true,true),"Sara overlap must retain priority.");
            c.Speed=20;Assert.IsFalse(call.IsValid(s,T.AddSeconds(3.5)));
            c.Speed=0;Assert.IsFalse(call.IsValid(s,T.AddSeconds(4.6)),"A stale snapshot is not a current warning.");
            s.PitData.InPitlane=true;Assert.IsFalse(call.IsValid(s,T.AddSeconds(3.5)));
        }
        [Test]
        public void Native_multiclass_detection_survives_legacy_voice_suppression()
        {
            var s=State();var c=Car(s,true,true);var now=DateTime.UtcNow;
            Move(s,c,0,-250,70,origin:now);var tracks=new TrackAwareness();tracks.Observe(s,now);
            NativeTrafficBridge.Enabled=true;NativeTrafficBridge.Publish(s,new[]{c.DriverRawName},true);
            var watch=new RaceTrafficWatch();var read=DateTime.UtcNow;
            watch.Observe(tracks,s,read);var call=watch.Take(s,read,false,true);
            Assert.IsNotNull(call,"The confirmed native detector must reach KI radio even before closing-history warmup.");
            StringAssert.Contains("native_multiclass_and_positions",call.Evidence);
        }
        [Test]
        public void Native_bridge_is_bounded_and_disabled_with_team()
        {
            var s=State();NativeTrafficBridge.Detection d;
            NativeTrafficBridge.Publish(s,new[]{"Driver"},true);Assert.IsFalse(NativeTrafficBridge.TryTake(out d));
            NativeTrafficBridge.Enabled=true;
            for(int i=0;i<100;i++)NativeTrafficBridge.Publish(s,new[]{"Driver"+i},true);
            int n=0;while(NativeTrafficBridge.TryTake(out d))n++;
            Assert.That(n,Is.LessThanOrEqualTo(32));Assert.That(n,Is.GreaterThan(0));
        }
        [Test]
        public void Delivered_traffic_is_deduplicated_for_the_same_vehicle()
        {
            var s=State();var c=Car(s);var tracks=new TrackAwareness();var watch=new RaceTrafficWatch();int n=0;
            for(int i=0;i<50;i++)
            {
                double t=i*.1;Move(s,c,t,350,0);tracks.Observe(s,T.AddSeconds(t));watch.Observe(tracks,s,T.AddSeconds(t));
                var call=watch.Take(s,T.AddSeconds(t),false,true);
                if(call!=null){call.MarkPlaybackStarted(T.AddSeconds(t));n++;}
            }
            Assert.AreEqual(1,n);
        }
        [TestCase(false,1000)] [TestCase(true,1000)] [TestCase(false,4900)]
        public void Passage_interval_is_correct_in_both_directions_and_across_start_finish(bool rear,double start)
        {
            var s=State();var c=Car(s,false,rear);var gaps=new RaceGapTracker();
            for(int i=0;i<=60;i++){Move(s,c,i*.1,rear?-100:100,50,start);gaps.Observe(s,T.AddSeconds(i*.1));}
            var value=gaps.Read(s,c,!rear,T.AddSeconds(6));
            Assert.AreEqual(RaceGapKind.InterpolatedTiming,value.Kind);Assert.That(value.Seconds,Is.EqualTo(2).Within(.011));
            Assert.AreNotEqual(DateTime.MinValue,value.MeasuredUtc);
        }
        [Test]
        public void Variable_pace_uses_shared_passage_point_instead_of_distance_divided_by_current_speed()
        {
            var s=State();var c=Car(s);var gaps=new RaceGapTracker();
            for(int i=0;i<=60;i++)
            {
                double t=i*.1;Move(s,c,t,100,50);
                if(t>3){c.DistanceRoundTrack=(float)(1250+30*(t-3));c.Speed=30;}
                gaps.Observe(s,T.AddSeconds(t));
            }
            var gap=gaps.Read(s,c,true,T.AddSeconds(6));Assert.AreEqual(RaceGapKind.InterpolatedTiming,gap.Kind);
            Assert.That(gap.Seconds,Is.EqualTo(4d/3).Within(.02));
            Assert.That(gap.Seconds,Is.Not.EqualTo((c.DistanceRoundTrack-s.PositionAndMotionData.DistanceRoundTrack)/50).Within(.1));
        }
        [Test]
        public void Repeated_position_polls_do_not_move_the_last_actual_opponent_measurement_time()
        {
            var s=State();var c=Car(s);var tracker=new RaceGapTracker();
            for(int i=0;i<=62;i++)
            {
                double t=i*.1;Move(s,c,t,100,50);
                c.DistanceRoundTrack=(float)(1100+50*((i/5)*.5)); // Opponent packets at 2 Hz; polling at 10 Hz.
                tracker.Observe(s,T.AddSeconds(t));
            }
            var gap=tracker.Read(s,c,true,T.AddSeconds(6.2));
            Assert.AreEqual(RaceGapKind.InterpolatedTiming,gap.Kind);
            Assert.That(gap.Seconds,Is.EqualTo(2).Within(.011));
        }
        [Test]
        public void Old_passage_is_not_reused_for_another_driver_or_after_a_telemetry_gap()
        {
            var s=State();var c=Car(s);var gaps=new RaceGapTracker();
            for(int i=0;i<=60;i++){Move(s,c,i*.1,100,50);gaps.Observe(s,T.AddSeconds(i*.1));}
            Assert.IsTrue(gaps.Read(s,c,true,T.AddSeconds(6)).HasValue);
            Assert.IsFalse(gaps.Read(s,c,true,T.AddSeconds(8)).HasValue);
            c.DriverRawName="Replacement";Assert.IsFalse(gaps.Read(s,c,true,T.AddSeconds(6)).HasValue);
            gaps.Observe(s,T.AddSeconds(9));Assert.IsFalse(gaps.Read(s,c,true,T.AddSeconds(9)).HasValue);
        }
        [Test]
        public void Calculated_adapter_gap_is_never_labelled_as_native_simulator_timing()
        {
            var s=State();var c=Car(s);s.SessionData.TimeDeltaFront=5;s.SessionData.LapsDeltaFront=0;
            s.SessionData.TimeDeltaFrontIsDerived=true;
            Assert.AreEqual(RaceGapKind.DerivedTiming,RaceDataTools.ReadGap(s,c,true).Kind);
            var ctx=new RaceDataContext{GameState=s,Tracks=new TrackAwareness(),NowUtc=T};
            Assert.IsFalse(RaceDataTools.ReadGap(ctx,c,true).HasValue,"Cold passage history must not silently fall back to the old precise label.");
        }
        [Test]
        public void Race_memory_keeps_the_same_passage_measurement_used_for_live_reports_and_trends()
        {
            var s=State();s.SessionData.IsNewLap=true;s.SessionData.CompletedLaps=1;s.SessionData.TimeDeltaFront=9;
            s.SessionData.LapTimePrevious=120;s.SessionData.PreviousLapWasValid=true;
            var memory=new AiRaceMemory();
            memory.Update(null,s,T,new RaceGapMeasurement(3.25f,RaceGapKind.InterpolatedTiming,T),new RaceGapMeasurement());
            var lap=memory.GetLaps().Single();
            Assert.AreEqual(3.25f,lap.GapAhead);Assert.AreEqual(RaceGapKind.InterpolatedTiming,lap.GapAheadKind);
            Assert.AreEqual(-1,lap.GapBehind,"Unknown must not become a precise-looking historical interval.");
        }
        [Test]
        public void Radio_opens_only_for_a_still_valid_AI_clip_at_backend_start()
        {
            bool valid=true;int beeps=0,backend=0;
            var id=AiAudioStore.Put(new byte[44],"Test",()=>valid);
            string name="COMPOUND_ai_team_reply_"+id;
            Assert.IsTrue(AiAudioStore.PrepareRadio(name,()=>beeps++));Assert.AreEqual(0,beeps);
            valid=false;
            Assert.IsFalse(AiAudioStore.TryStart(id,()=>backend++));Assert.AreEqual(0,beeps);Assert.AreEqual(0,backend);
            AiAudioStore.Remove(id);
        }
        [Test]
        public void Valid_clip_opens_radio_before_the_backend_and_only_once()
        {
            string order="";var id=AiAudioStore.Put(new byte[44],"Test");
            Assert.IsTrue(AiAudioStore.PrepareRadio("COMPOUND_ai_team_reply_"+id,()=>order+="radio;"));
            Assert.IsTrue(AiAudioStore.TryStart(id,()=>order+="voice;"));
            Assert.IsFalse(AiAudioStore.TryStart(id,()=>order+="duplicate;"));
            Assert.AreEqual("radio;voice;",order);AiAudioStore.Remove(id);
        }
        [Test]
        public void Missing_clip_and_silent_TTS_do_not_open_a_radio_channel()
        {
            Assert.IsFalse(AiAudioStore.PrepareRadio("ai_team_reply_"+Guid.NewGuid().ToString("N"),()=>Assert.Fail("Unexpected radio")));
            Assert.IsFalse(AiAudio.HasSpeechSignal(new byte[4800]));Assert.IsFalse(AiAudio.HasSpeechSignal(new byte[47]));
            var pcm=new byte[4800];for(int i=0;i<pcm.Length;i+=2){pcm[i]=200;pcm[i+1]=1;}
            Assert.IsTrue(AiAudio.HasSpeechSignal(pcm));
        }
        [Test]
        public void Recent_flags_do_not_postpone_all_routines_for_thirty_seconds()
        {
            var s=State();var prefs=new AiRadioPreferences();foreach(var o in AiRadioPreferences.Options)prefs.Set(o.Id,0);
            prefs.Set("fuel",30);prefs.SetTolerance("fuel",0,0);var scheduler=new AiRadioSchedule(1);scheduler.Observe(s,T,prefs);
            var memory=new AiRaceMemory();memory.AddRadio(TeamMember.Sara,"Gelb im Sektor.",T.AddSeconds(20));
            var ctx=new RaceDataContext{GameState=s,Memory=memory,NowUtc=T.AddSeconds(30),LastTelemetryUtc=T.AddSeconds(30)};
            Assert.IsNotNull(scheduler.Take(ctx,prefs,false));
        }
        [Test]
        public void All_ten_enabled_routine_topics_recover_after_busy_radio_without_starving_one_topic()
        {
            var s=State();var memory=new AiRaceMemory();
            s.SessionData.CurrentLapIsValid=s.SessionData.PreviousLapWasValid=true;s.TyreData.TyreWearActive=true;
            for(int i=1;i<=4;i++)
            {
                s.SessionData.IsNewLap=true;s.SessionData.CompletedLaps=i;s.SessionData.LapTimePrevious=120;
                s.FuelData.FuelLeft=60-3*i;s.TyreData.FrontLeftPercentWear=s.TyreData.FrontRightPercentWear=i;
                s.TyreData.RearLeftPercentWear=s.TyreData.RearRightPercentWear=2*i;
                memory.Update(null,s,T.AddSeconds((i-4)*120));
            }
            s.SessionData.IsNewLap=false;s.SessionData.SessionHasFixedTime=true;s.SessionData.SessionTimeRemaining=1800;
            s.SessionData.SessionStartClassPosition=3;
            s.Conditions.addSample(T,4,1,25,30,0,0,0,0,1,false,ConditionsMonitor.TrackStatus.UNKNOWN);
            Car(s,false,false,"front").DriverRawName="Front";Car(s,false,true,"rear").DriverRawName="Rear";
            s.SessionData.LapsDeltaFront=s.SessionData.LapsDeltaBehind=0;
            s.SessionData.TimeDeltaFront=2;s.SessionData.TimeDeltaBehind=3;
            var prefs=new AiRadioPreferences();foreach(var o in AiRadioPreferences.Options){prefs.Set(o.Id,120);prefs.SetTolerance(o.Id,0,0);}
            var scheduler=new AiRadioSchedule(1);scheduler.Observe(s,T,prefs);
            var context=new RaceDataContext {GameState=s,Memory=memory,NowUtc=T.AddSeconds(120),LastTelemetryUtc=T.AddSeconds(120)};
            Assert.IsNull(scheduler.Take(context,prefs,true));
            var seen=new System.Collections.Generic.HashSet<string>();
            for(int i=200;i<=410;i++)
            {
                context.NowUtc=context.LastTelemetryUtc=T.AddSeconds(i);
                var call=scheduler.Take(context,prefs,false,filterUnchanged:false);
                if(call!=null){seen.Add(call.Topic);call.MarkPlaybackStarted(context.NowUtc);}
            }
            CollectionAssert.AreEquivalent(AiRadioPreferences.Options.Select(o=>o.Id),seen);
        }
        [TestCase(20,true)] [TestCase(30,false)]
        public void Prior_lap_reference_distinguishes_unusually_slow_vehicle_from_normal_corner_speed(float rivalSpeed,bool expected)
        {
            var s=State();var c=Car(s);var tracks=new TrackAwareness();var watch=new RaceTrafficWatch();ObservedRaceCall found=null;
            for(int i=0;i<=1030;i++)
            {
                double t=i*.1;
                if(t<100)Move(s,c,t,300,50);
                else Move(s,c,t,180+(50-rivalSpeed)*100,rivalSpeed);
                tracks.Observe(s,T.AddSeconds(t));watch.Observe(tracks,s,T.AddSeconds(t));
                if(t>=100)found=watch.Take(s,T.AddSeconds(t),false,true)??found;
            }
            Assert.AreEqual(expected,found!=null);
        }
        [Test]
        public void Passage_tracker_runs_one_virtual_hour_with_a_sixteen_car_field_and_keeps_correct_interval()
        {
            var s=State();var front=Car(s);var tracker=new RaceGapTracker();
            for(int j=0;j<15;j++)s.OpponentData["other"+j]=new OpponentData {DriverRawName="Other"+j,ClassPosition=3+j,CarClass=s.carClass};
            for(int i=0;i<=36000;i++)
            {
                double t=i*.1;Move(s,front,t,100,50);
                int j=0;foreach(var car in s.OpponentData.Values.Where(c=>c!=front))
                {
                    double position=1000+50*t+200+(j++)*100;
                    car.CompletedLaps=3+(int)(position/5000);car.DistanceRoundTrack=(float)(position%5000);car.Speed=50;
                }
                tracker.Observe(s,T.AddSeconds(t));
            }
            var gap=tracker.Read(s,front,true,T.AddHours(1));
            Assert.AreEqual(RaceGapKind.InterpolatedTiming,gap.Kind);Assert.That(gap.Seconds,Is.EqualTo(2).Within(.011));
            s.SessionData.SessionPhase=SessionPhase.Garage;tracker.Observe(s,T.AddHours(1).AddSeconds(.1));
            Assert.IsFalse(tracker.Read(s,front,true,T.AddHours(1).AddSeconds(.1)).HasValue);
        }
        [Test]
        public void Group_warning_is_revalidated_when_one_faster_vehicle_leaves_the_track_for_pits()
        {
            var s=State();var one=Car(s,true,true,"one");var two=Car(s,true,true,"two");two.DriverRawName="Fast Two";
            var tracks=new TrackAwareness();var watch=new RaceTrafficWatch();ObservedRaceCall found=null;double end=0;
            for(int i=0;i<=35 && found==null;i++)
            {
                double t=i*.1;Move(s,one,t,-250,70);Move(s,two,t,-312.5,75);
                tracks.Observe(s,T.AddSeconds(t));watch.Observe(tracks,s,T.AddSeconds(t));found=watch.Take(s,T.AddSeconds(t),false,true);end=t;
            }
            Assert.IsNotNull(found);CollectionAssert.Contains(RaceTrafficWatch.RearGroupPhrases,found.Text);
            two.InPits=true;Move(s,one,end+.1,-250,70);Move(s,two,end+.1,-312.5,75);
            tracks.Observe(s,T.AddSeconds(end+.1));watch.Observe(tracks,s,T.AddSeconds(end+.1));
            Assert.IsFalse(found.IsValid(s,T.AddSeconds(end+.1)),"Do not announce multiple cars after only one remains.");
        }
        [Test]
        public void Every_urgent_race_phrase_uses_selected_Ben_voice_and_requires_no_cloud_render_at_warning_time()
        {
            foreach(string phrase in RaceTrafficWatch.PreparedPhrases)
                Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Ben,phrase));
            Assert.IsTrue(CrewSpeechPolicy.CacheOnly(true,7,TeamMember.Ben));
            Assert.IsFalse(CrewSpeechPolicy.CacheOnly(false,7,TeamMember.Ben),"A driver's noncritical question still permits synthesis.");
        }
        [TestCase(1)] [TestCase(3)] [TestCase(12)] [TestCase(0)]
        public void Qualifying_summary_uses_actual_position_and_only_confirms_finished_result(int position)
        {
            var s=State();s.SessionData.SessionType=SessionType.Qualify;s.SessionData.SessionPhase=SessionPhase.Checkered;
            s.SessionData.ClassPosition=position;s.PitData.InPitlane=true;
            var watch=new QualifyingResultWatch();watch.Observe(s,T,"Rob");var provisional=watch.Take(s,T,false,true);
            Assert.IsNotNull(provisional);Assert.IsTrue(provisional.IsValid(s,T));
            if(position>0)StringAssert.Contains("vorläufig",provisional.Text);
            else StringAssert.Contains("nicht bestätigt",provisional.Text);
            provisional.MarkPlaybackStarted(T);watch.Observe(s,T.AddSeconds(1),"Rob");Assert.IsNull(watch.Take(s,T.AddSeconds(1),false,true));
            s.SessionData.SessionPhase=SessionPhase.Finished;watch.Observe(s,T.AddSeconds(2),"Rob");var final=watch.Take(s,T.AddSeconds(2),false,true);
            Assert.IsNotNull(final);Assert.IsTrue(final.IsValid(s,T.AddSeconds(2)));
            if(position>0){StringAssert.DoesNotContain("vorläufig",final.Text);StringAssert.Contains("Platz "+position,final.Text);}
            if(position==3)Assert.IsTrue(final.Text.Contains("ersten drei") || final.Text.Contains("Top drei"));
            s.SessionData.SessionType=SessionType.Race;s.SessionData.SessionPhase=SessionPhase.Green;
            Assert.IsFalse(final.IsValid(s,T.AddSeconds(3)),"Never announce an old qualifying result in active racing.");
        }
    }
}
