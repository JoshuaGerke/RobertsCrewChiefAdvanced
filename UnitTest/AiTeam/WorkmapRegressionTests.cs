using System;
using System.Linq;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;
using Newtonsoft.Json.Linq;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class WorkmapRegressionTests
    {
        private static readonly DateTime T = new DateTime(2026,11,2,19,0,0,DateTimeKind.Utc);
        private static GameStateData Frame(int completed, bool crossing, bool pit, bool valid, float fuel=40)
        {
            var s=new GameStateData(T.Ticks);
            s.SessionData.SessionId=4;s.SessionData.SessionType=SessionType.Practice;
            s.SessionData.SessionPhase=SessionPhase.Green;s.SessionData.CompletedLaps=completed;
            s.SessionData.IsNewLap=crossing;s.SessionData.CurrentLapIsValid=valid;
            s.SessionData.PreviousLapWasValid=valid;s.SessionData.LapTimePrevious=127.041f;
            s.PitData.InPitlane=pit;s.FuelData.FuelUseActive=true;s.FuelData.FuelLeft=fuel;
            return s;
        }
        [TestCase(127.041f)] [TestCase(149.254f)] [TestCase(124.767f)]
        public void T05_First_flying_lap_does_not_inherit_outlap_or_garage_flags(float time)
        {
            var m=new AiRaceMemory();var garage=Frame(0,false,true,false,50);
            m.Update(null,garage,T);var start=Frame(0,true,false,true,49);
            Assert.IsFalse(m.Update(garage,start,T.AddSeconds(10)));
            var flying=Frame(0,false,false,true,47);m.Update(start,flying,T.AddSeconds(20));
            var finish=Frame(1,true,false,true,45);finish.SessionData.LapTimePrevious=time;
            Assert.IsTrue(m.Update(flying,finish,T.AddSeconds(10+time)));
            var lap=m.GetLaps()[0];Assert.IsTrue(lap.Valid);Assert.IsFalse(lap.InPitLap);
            Assert.AreEqual(time,lap.LapTime);Assert.That(lap.FuelUsed,Is.EqualTo(4).Within(.001));
        }
        [Test]
        public void T05_New_flying_lap_after_garage_crossing_with_unchanged_completed_count_is_clean()
        {
            var m=new AiRaceMemory();var first=Frame(1,true,false,true);m.Update(null,first,T);
            var garage=Frame(1,false,true,false);m.Update(first,garage,T.AddSeconds(1));
            var start=Frame(1,true,false,true);m.Update(garage,start,T.AddSeconds(10));
            var flying=Frame(1,false,false,true);m.Update(start,flying,T.AddSeconds(11));
            var finish=Frame(2,true,false,true);m.Update(flying,finish,T.AddSeconds(138));
            var lap=m.GetLaps()[1];Assert.IsTrue(lap.Valid);Assert.IsFalse(lap.InPitLap);
        }
        [Test]
        public void T05_Actual_invalid_lap_and_inlap_are_not_promoted_to_clean_pace_samples()
        {
            var m=new AiRaceMemory();var start=Frame(0,true,false,true);m.Update(null,start,T);
            var invalid=Frame(0,false,false,false);m.Update(start,invalid,T.AddSeconds(1));
            var finish=Frame(1,true,false,true);finish.SessionData.PreviousLapWasValid=false;
            m.Update(invalid,finish,T.AddSeconds(128));Assert.IsFalse(m.GetLaps()[0].Valid);
            var pit=Frame(1,false,true,true);m.Update(finish,pit,T.AddSeconds(140));
            var next=Frame(2,true,false,true);m.Update(pit,next,T.AddSeconds(260));Assert.IsTrue(m.GetLaps()[1].InPitLap);
        }
        [Test]
        public void T29_Finish_uses_recorded_invalid_lap_when_no_warning_transition_was_observed()
        {
            var m=new AiRaceMemory();var start=Frame(0,false,false,false);
            start.SessionData.SessionType=SessionType.Race;m.Update(null,start,T);
            var finish=Frame(1,true,false,true);finish.SessionData.SessionType=SessionType.Race;
            finish.SessionData.PreviousLapWasValid=false;m.Update(start,finish,T.AddSeconds(136));
            var watch=new IncidentWatch();string text=watch.FinishSummary(m.RecordedInvalidLaps);
            StringAssert.Contains("eine ungültige Runde",text);StringAssert.DoesNotContain("Keine Strafen",text);
            Assert.AreEqual(string.Empty,new IncidentWatch().FinishSummary(0));
        }
        private static CrewChiefV4.QueuedMessage NativeMessage(string path)
        {
            var q=(CrewChiefV4.QueuedMessage)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(CrewChiefV4.QueuedMessage));
            q.messageName=path;q.messageFolders=new System.Collections.Generic.List<string>{path};return q;
        }
        [Test]
        public void T28_Late_audio_completion_cannot_clear_the_newly_playing_successor()
        {
            var cache=typeof(CrewChiefV4.Audio.SoundCache);
            var field=cache.GetField("currentlyPlayingSound",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var done=cache.GetMethod("CompletePlayback",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var previous=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(field.FieldType);
            var successor=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(field.FieldType);
            object saved=field.GetValue(null);
            try
            {
                field.SetValue(null,successor);done.Invoke(null,new[]{previous});
                Assert.AreSame(successor,field.GetValue(null),"The old beep must not erase the interrupt target.");
                done.Invoke(null,new[]{successor});Assert.IsNull(field.GetValue(null));
            }
            finally{field.SetValue(null,saved);}
        }
        [Test]
        public void T28_Still_active_double_yellow_is_retried_after_expiry_but_lifted_yellow_is_not()
        {
            var gs=Frame(1,false,false,true);gs.SessionData.Flag=FlagEnum.DOUBLE_YELLOW;
            var w=new LiveWarningDispatcher();string text="Doppelt Gelb, Vorsicht.";
            w.Offer("warning-flag-double",text,TeamMember.Sara,10,gs,T,s=>FlagWatch.StillRelevant(text,s),"mapped flag");
            var first=w.Take(gs,T,false,m=>true);Assert.IsNotNull(first);
            Assert.AreEqual(10,first.Priority);Assert.IsTrue(first.IsValid(gs,T));
            w.Observe(gs,T.AddSeconds(9));Assert.IsNull(w.Take(gs,T.AddSeconds(9),false,m=>true));
            var retry=w.Take(gs,T.AddSeconds(10.1),false,m=>true);Assert.IsNotNull(retry);
            Assert.AreNotSame(first,retry);Assert.IsTrue(retry.IsValid(gs,T.AddSeconds(10.1)));
            gs.SessionData.Flag=FlagEnum.GREEN;w.Observe(gs,T.AddSeconds(10.2));
            Assert.IsNull(w.Take(gs,T.AddSeconds(11),false,m=>true));Assert.IsFalse(retry.IsValid(gs,T.AddSeconds(11)));
        }
        [Test]
        public void T27_Native_hot_oil_survives_voice_filter_as_local_selected_voice_warning()
        {
            var gs=Frame(1,false,false,true);gs.carClass=new CrewChiefV4.CarData.CarClass {maxSafeOilTemp=120,maxSafeWaterTemp=110};
            gs.EngineData.EngineOilTemp=130;gs.EngineData.EngineWaterTemp=90;
            var w=new LiveWarningDispatcher();Assert.IsTrue(w.Capture(NativeMessage("engine_monitor/hot_oil"),gs,T));
            w.Observe(gs,T);var call=w.Take(gs,T,false,m=>true);Assert.IsNotNull(call);
            Assert.AreEqual(TeamMember.Mik,call.Member);Assert.AreEqual(9,call.Priority);
            Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(call.Member,call.Text));
            StringAssert.Contains("native:",call.Evidence);
            gs.EngineData.EngineOilTemp=110;w.Observe(gs,T.AddSeconds(.1));Assert.IsFalse(call.IsValid(gs,T.AddSeconds(.1)));
        }
        [Test]
        public void T06_Native_tracklimit_pre_warning_does_not_fabricate_invalid_lap()
        {
            var gs=Frame(2,false,false,true);var w=new LiveWarningDispatcher();
            Assert.IsTrue(w.Capture(NativeMessage("penalties/possible_track_limits_warning"),gs,T));
            w.Observe(gs,T);var call=w.Take(gs,T,false,m=>true);Assert.IsNotNull(call);
            CollectionAssert.Contains(LiveWarningDispatcher.LimitPhrases,call.Text);
            StringAssert.DoesNotContain("ungültig",call.Text);Assert.IsTrue(gs.SessionData.CurrentLapIsValid);
            gs.SessionData.CompletedLaps=3;w.Observe(gs,T.AddSeconds(1));Assert.IsFalse(call.IsValid(gs,T.AddSeconds(1)));
            Assert.IsNull(w.Take(gs,T.AddSeconds(2),false,m=>true));
        }
        [Test]
        public void T07_Invalidity_warning_is_acknowledged_only_at_playback_and_never_follows_next_lap()
        {
            var gs=Frame(2,false,false,false);var w=new LiveWarningDispatcher();
            w.Offer("warning-invalid-lap",CrewSpeechPolicy.FirstInvalidLapPhrases[0],TeamMember.Mik,7,gs,T,
                s=>!s.SessionData.CurrentLapIsValid,"current lap invalid",2);
            Assert.IsNull(w.Take(gs,T,true,m=>true));var call=w.Take(gs,T.AddSeconds(1),false,m=>true);
            Assert.IsNotNull(call);Assert.AreEqual(DateTime.MinValue,call.DeliveryStartedUtc);
            gs.SessionData.CompletedLaps=3;gs.SessionData.CurrentLapIsValid=true;
            w.Observe(gs,T.AddSeconds(2));Assert.IsFalse(call.IsValid(gs,T.AddSeconds(2)));
            Assert.IsNull(w.Take(gs,T.AddSeconds(3),false,m=>true));
        }
        [Test]
        public void T28_Flag_warning_survives_FCY_and_successful_playback_is_deduplicated()
        {
            var gs=Frame(1,false,false,true);gs.SessionData.SessionPhase=SessionPhase.FullCourseYellow;
            gs.FlagData.isFullCourseYellow=true;var w=new LiveWarningDispatcher();string text="Full Course Yellow, Tempo runter.";
            w.Offer("warning-flag-fcy",text,TeamMember.Sara,10,gs,T,s=>FlagWatch.StillRelevant(text,s),"mapped flag");
            var call=w.Take(gs,T,false,m=>true);Assert.IsNotNull(call);call.MarkPlaybackStarted(T);
            w.Observe(gs,T.AddSeconds(1));w.Offer("warning-flag-fcy",text,TeamMember.Sara,10,gs,T.AddSeconds(1),s=>true,"same flag");
            Assert.IsNull(w.Take(gs,T.AddSeconds(1),false,m=>true));
        }
        private static RaceDataContext Context(GameStateData gs,DateTime now)
        {return new RaceDataContext {GameState=gs,NowUtc=now,LastTelemetryUtc=T};}
        [Test]
        public void T02_Zero_missing_invalid_paused_and_stale_are_distinct_and_never_become_zero()
        {
            var m=new TelemetryMeasurement(0,1,0,"test API","field","component","%","%",T,0,100);
            Assert.AreEqual("gueltig",m.StatusAt(T));Assert.AreEqual(0,m.Value);
            Assert.AreEqual("pausiert",m.StatusAt(T,true));Assert.AreEqual("veraltet",m.StatusAt(T.AddSeconds(4)));
            Assert.AreEqual("ungueltige_zeitbasis",m.StatusAt(T.AddSeconds(-1)));
            var absent=new TelemetryMeasurement(null,1,0,"API","field","part","%","%",T,0,100);
            var invalid=new TelemetryMeasurement(double.NaN,1,0,"API","field","part","%","%",T,0,100);
            Assert.AreEqual("fehlt",absent.Status);Assert.AreEqual("ungueltig",invalid.Status);
            Assert.IsNull(TelemetryMeasurementTools.Describe(m,T.AddSeconds(4))["wert"].Value<double?>());
            Assert.IsNull(TelemetryMeasurementTools.Describe(m,T,true)["wert"].Value<double?>());
            Assert.IsNull(TelemetryMeasurementTools.Describe(invalid,T)["rohwert"].Value<double?>());
        }
        [TestCase(0,0)] [TestCase(.01f,1)] [TestCase(.015f,1.5)]
        public void T03_Aero_fraction_keeps_raw_precision_and_has_independent_normalized_value(float raw,double expected)
        {
            var gs=Frame(1,false,false,true);TelemetryMeasurements.DamageFractions(gs,raw,0,null,null,"AMS2.API",T);
            var j=RaceDataTools.Execute("get_damage_and_engine",Context(gs,T));
            Assert.That(j["schaden_prozent"]["aero"].Value<double>(),Is.EqualTo(expected).Within(.00001));
            Assert.That(j["messwerte"]["damage.aero"]["rohwert"].Value<double>(),Is.EqualTo(raw).Within(.00000001));
            Assert.AreEqual("mAeroDamage",j["messwerte"]["damage.aero"]["feld"].Value<string>());
            Assert.AreEqual("AMS2.API",j["messwerte"]["damage.aero"]["quelle"].Value<string>());
        }
        [Test]
        public void T04_Engine_and_four_corners_are_not_hidden_by_missing_aero_or_damage_enable_flag()
        {
            var gs=Frame(1,false,false,true);gs.CarDamageData.DamageEnabled=false;
            TelemetryMeasurements.DamageFractions(gs,-1,.00337f,new[]{.02061f,.02032f,.00608f,.00606f},new[]{0f,.01f,.02f,.03f},"AMS2.API",T);
            var j=RaceDataTools.Execute("get_damage_and_engine",Context(gs,T));var d=j["schaden_prozent"];
            Assert.IsNull(d["aero"].Value<double?>());Assert.That(d["motor"].Value<double>(),Is.EqualTo(.337).Within(.0001));
            double[] brakes={2.061,2.032,.608,.606};
            for(int i=0;i<4;i++)
            {Assert.That(d["bremsen"][i].Value<double>(),Is.EqualTo(brakes[i]).Within(.0001));Assert.That(d["aufhaengung"][i].Value<double>(),Is.EqualTo(i).Within(.0001));}
            Assert.IsNull(d["getriebe"].Value<double?>());
        }
        [Test]
        public void T04_RaceRoom_health_is_inverted_once_and_missing_components_stay_unknown()
        {
            var gs=Frame(1,false,false,true);TelemetryMeasurements.HealthDamage(gs,-1,.98f,.75f,"RaceRoom.API",T);
            var d=RaceDataTools.Execute("get_damage_and_engine",Context(gs,T))["schaden_prozent"];
            Assert.IsNull(d["aero"].Value<double?>());Assert.That(d["motor"].Value<double>(),Is.EqualTo(2).Within(.0001));
            Assert.AreEqual(25,d["getriebe"].Value<double>());
        }
        [Test]
        public void T12_Pressure_reading_and_warning_are_separate_and_an_unset_sensor_is_not_zero_pressure()
        {
            var gs=Frame(1,false,false,true);
            TelemetryMeasurements.Put(gs,"engine.oil_pressure",450,"AMS2.API","mOilPressureKPa","motor","kPa",T,0,100000);
            TelemetryMeasurements.Put(gs,"engine.fuel_pressure",0,"AMS2.API","mFuelPressureKPa","motor","kPa",T,0,100000,false);
            var j=RaceDataTools.Execute("get_telemetry_values",Context(gs,T))["messwerte"];
            Assert.AreEqual(450,j["engine.oil_pressure"]["wert"].Value<double>());
            Assert.AreEqual("kPa",j["engine.oil_pressure"]["einheit"].Value<string>());
            Assert.AreEqual("fehlt",j["engine.fuel_pressure"]["status"].Value<string>());
            Assert.IsNull(j["engine.fuel_pressure"]["wert"].Value<double?>());Assert.IsFalse(gs.EngineData.EngineOilPressureWarning);
        }
        [Test]
        public void T01_Sdk14_layout_matches_published_field_offsets_and_native_boolean_sizes()
        {
            var type=typeof(CrewChiefV4.AMS2.ams2APIStruct);
            Assert.AreEqual(20700,System.Runtime.InteropServices.Marshal.SizeOf(type));
            string[] names={"mmfOnly_mCurrentTime","mAeroDamage","mEngineDamage","mSequenceNumber","mJoyPad0","mDPad","mAntiLockSetting","mTractionControlSetting","mErsDeploymentMode","mErsAutoModeEnabled","mClutchTemp","mClutchWear","mClutchOverheated","mClutchSlipping","mYellowFlagState","mSessionIsPrivate","mLaunchStage"};
            int[] offsets={6724,7284,7288,7320,20652,20656,20660,20664,20668,20672,20676,20680,20684,20685,20688,20692,20696};
            for(int i=0;i<names.Length;i++)Assert.AreEqual(offsets[i],System.Runtime.InteropServices.Marshal.OffsetOf(type,names[i]).ToInt32(),names[i]);
        }
        [Test]
        public void T01_Tail_fields_are_not_confused_with_button_masks_or_adjacent_booleans()
        {
            var b=new byte[20700];Action<int,byte[]> put=(i,v)=>Buffer.BlockCopy(v,0,b,i,v.Length);
            put(20652,BitConverter.GetBytes(0x12345678u));put(20656,BitConverter.GetBytes(0x87654321u));
            put(20660,BitConverter.GetBytes(5));put(20664,BitConverter.GetBytes(3));
            put(20668,BitConverter.GetBytes(2));b[20672]=1;put(20676,BitConverter.GetBytes(320.5f));
            put(20680,BitConverter.GetBytes(.0125f));b[20684]=1;b[20685]=0;
            put(20688,BitConverter.GetBytes(4));b[20692]=1;put(20696,BitConverter.GetBytes(2));
            var raw=new CrewChiefV4.AMS2.AMS2SharedMemoryReader().BytesToStructure(b);
            Assert.AreEqual(5,raw.mAntiLockSetting);Assert.AreEqual(3,raw.mTractionControlSetting);
            Assert.AreEqual(0x87654321u,raw.mDPad);Assert.IsTrue(raw.mErsAutoModeEnabled);
            Assert.AreEqual(320.5f,raw.mClutchTemp);Assert.AreEqual(.0125f,raw.mClutchWear);
            Assert.IsTrue(raw.mClutchOverheated);Assert.IsFalse(raw.mClutchSlipping);
            Assert.AreEqual(4,raw.mYellowFlagState);Assert.IsTrue(raw.mSessionIsPrivate);Assert.AreEqual(2,raw.mLaunchStage);
            Assert.Throws<ArgumentException>(()=>new CrewChiefV4.AMS2.AMS2SharedMemoryReader().BytesToStructure(new byte[20692]));
        }
        [Test]
        public void T01_Unfinished_frame_cannot_spin_forever_on_the_spotter_read_path()
        {
            int reads=0,copies=0,retries;var b=new byte[20700];
            bool ok=CrewChiefV4.AMS2.AMS2SharedMemoryReader.TryReadStableFrame(()=>{reads++;return 3;},x=>copies++,b,out retries);
            Assert.IsFalse(ok);Assert.AreEqual(8,reads);Assert.AreEqual(0,copies);Assert.AreEqual(8,retries);
        }
        [Test]
        public void T01_Frame_sequence_is_checked_before_after_and_inside_copied_payload()
        {
            uint seq=2;int copies=0,retries;var b=new byte[20700];
            bool ok=CrewChiefV4.AMS2.AMS2SharedMemoryReader.TryReadStableFrame(()=>seq,x=>{
                Buffer.BlockCopy(BitConverter.GetBytes(seq),0,x,7320,4);if(copies++==0)seq=4;
            },b,out retries);
            Assert.IsTrue(ok);Assert.AreEqual(2,copies);Assert.AreEqual(1,retries);
        }
        [Test]
        public void T02_Repeated_application_reads_do_not_refresh_a_frozen_simulator_snapshot()
        {
            var gs=Frame(1,false,false,true);gs.SourceTelemetryUtc=T;
            var memory=new AiRaceMemory();memory.Update(null,gs,T.AddSeconds(10));
            Assert.AreEqual(T,memory.LastTickUtc);
            Assert.AreEqual(10,new RaceDataContext{NowUtc=T.AddSeconds(10),LastTelemetryUtc=memory.LastTickUtc}.TelemetryAgeSeconds);
        }
        [Test]
        public void T22_Tiny_damage_precision_changes_do_not_cancel_all_speech_but_material_change_does()
        {
            var gs=Frame(1,false,false,true);TelemetryMeasurements.DamageFractions(gs,0,.001f,null,null,"AMS2.API",T);
            var evidence=new RaceSpeechEvidence(Context(gs,T));
            TelemetryMeasurements.DamageFractions(gs,0,.00101f,null,null,"AMS2.API",T);
            Assert.IsTrue(evidence.IsCurrent(Context(gs,T.AddSeconds(.1))));
            TelemetryMeasurements.DamageFractions(gs,0,.2f,null,null,"AMS2.API",T);
            Assert.IsFalse(evidence.IsCurrent(Context(gs,T.AddSeconds(.2))));
        }
        private static ThermalProfile Profile(float tyre=110,float brake=1000)
        {return new ThermalProfile{Source="test_validated_profile",TyreCookingC=new[]{tyre,tyre,tyre,tyre},BrakeCookingC=new[]{brake,brake,brake,brake},OilLimitC=120,WaterLimitC=110};}
        private static void Temps(GameStateData gs,float front,float rear,float brake=500)
        {
            var t=gs.TyreData;
            t.FrontLeft_LeftTemp=t.FrontLeft_CenterTemp=t.FrontLeft_RightTemp=t.FrontRight_LeftTemp=t.FrontRight_CenterTemp=t.FrontRight_RightTemp=front;
            t.RearLeft_LeftTemp=t.RearLeft_CenterTemp=t.RearLeft_RightTemp=t.RearRight_LeftTemp=t.RearRight_CenterTemp=t.RearRight_RightTemp=rear;
            t.LeftFrontBrakeTemp=t.RightFrontBrakeTemp=t.LeftRearBrakeTemp=t.RightRearBrakeTemp=brake;
        }
        private static ObservedRaceCall ThermalTick(ThermalWatch watch,LiveWarningDispatcher dispatcher,GameStateData gs,double seconds)
        {
            DateTime now=T.AddSeconds(seconds);gs.Now=now;watch.Observe(gs,now,dispatcher);dispatcher.Observe(gs,now);
            return dispatcher.Take(gs,now,false,m=>true);
        }
        [Test]
        public void T24_Rear_heat_is_automatic_specific_and_does_not_require_a_driver_question_or_spin_claim()
        {
            var gs=Frame(1,false,false,true);Temps(gs,80,125);var output=new LiveWarningDispatcher();var w=new ThermalWatch(s=>Profile());
            Assert.IsNull(ThermalTick(w,output,gs,0));Assert.IsNull(ThermalTick(w,output,gs,1));
            var call=ThermalTick(w,output,gs,2.1);Assert.IsNotNull(call);StringAssert.Contains("Reifen hinten",call.Text);
            Assert.AreEqual(7,call.Priority);StringAssert.DoesNotContain("Dreher",call.Text);
            Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Mik,call.Text));
        }
        [TestCase(95,true)] [TestCase(150,false)]
        public void T24_Identical_temperature_has_different_meaning_with_different_tyre_profiles(float limit,bool warns)
        {
            var gs=Frame(1,false,false,true);Temps(gs,80,105);var output=new LiveWarningDispatcher();var w=new ThermalWatch(s=>Profile(limit));
            ThermalTick(w,output,gs,0);ThermalTick(w,output,gs,1);var call=ThermalTick(w,output,gs,2.1);
            Assert.AreEqual(warns,call!=null);
        }
        [Test]
        public void T24_Unknown_profile_missing_sensor_and_frozen_source_cannot_claim_overheating()
        {
            var gs=Frame(1,false,false,true);Temps(gs,300,300);var output=new LiveWarningDispatcher();var unknown=new ThermalWatch(s=>null);
            for(int i=0;i<5;i++)Assert.IsNull(ThermalTick(unknown,output,gs,i));
            Temps(gs,80,125);gs.TyreData.RearRight_CenterTemp=0;var w=new ThermalWatch(s=>Profile());output.Reset();
            ThermalTick(w,output,gs,0);ThermalTick(w,output,gs,1);var call=ThermalTick(w,output,gs,2.1);
            Assert.IsNotNull(call);StringAssert.Contains("hinten links",call.Text);StringAssert.DoesNotContain("Reifen hinten sind",call.Text);
            output.Reset();w.Reset();gs.SourceTelemetryUtc=T;
            for(int i=2;i<8;i++)Assert.IsNull(ThermalTick(w,output,gs,i));
        }
        [Test]
        public void T25_Normal_braking_spike_is_ignored_but_persistent_profile_overheat_is_reported()
        {
            var gs=Frame(1,false,false,true);var o=new LiveWarningDispatcher();var w=new ThermalWatch(s=>Profile());
            Temps(gs,80,80,1250);ThermalTick(w,o,gs,0);Assert.IsNull(ThermalTick(w,o,gs,1));
            Temps(gs,80,80,800);for(int i=2;i<6;i++)Assert.IsNull(ThermalTick(w,o,gs,i));
            Temps(gs,80,80,1250);ThermalTick(w,o,gs,6);for(int i=7;i<10;i++)Assert.IsNull(ThermalTick(w,o,gs,i));
            var call=ThermalTick(w,o,gs,10.1);Assert.IsNotNull(call);StringAssert.Contains("Bremsen",call.Text);
            StringAssert.DoesNotContain("ausgefallen",call.Text);
        }
        [Test]
        public void T26_Cooling_requires_actual_prior_playback_and_sustained_below_limit_values()
        {
            var gs=Frame(1,false,false,true);Temps(gs,80,125);var o=new LiveWarningDispatcher();var w=new ThermalWatch(s=>Profile());
            ThermalTick(w,o,gs,0);ThermalTick(w,o,gs,1);var call=ThermalTick(w,o,gs,2);Assert.IsNotNull(call);call.MarkPlaybackStarted(T.AddSeconds(2));
            o.Observe(gs,T.AddSeconds(2));Temps(gs,80,80);
            for(int i=3;i<13;i++)Assert.IsNull(ThermalTick(w,o,gs,i));
            var clear=ThermalTick(w,o,gs,13.1);Assert.IsNotNull(clear);StringAssert.Contains("beruhigt",clear.Text);
            Temps(gs,80,125);gs.Now=T.AddSeconds(13.2);o.Observe(gs,gs.Now);Assert.IsFalse(clear.IsValid(gs,gs.Now));
        }
        [Test]
        public void T26_Wording_variants_keep_component_and_caution_and_do_not_invent_failures()
        {
            var variants=System.Linq.Enumerable.Range(0,3).Select(v=>ThermalWatch.Phrase(false,12,v)).ToArray();
            Assert.AreEqual(3,variants.Distinct().Count());foreach(var s in variants){StringAssert.Contains("Reifen hinten",s);StringAssert.DoesNotContain("Gripverlust",s);}
        }
        [Test]
        public void T27_Profiled_engine_heat_warns_before_damage_threshold_without_waiting_for_native_timer()
        {
            var gs=Frame(1,false,false,true);Temps(gs,80,80);gs.EngineData.EngineRpm=4000;gs.EngineData.EngineOilTemp=130;gs.EngineData.EngineWaterTemp=90;
            var o=new LiveWarningDispatcher();var w=new ThermalWatch(s=>Profile());for(int i=0;i<5;i++)Assert.IsNull(ThermalTick(w,o,gs,i));
            var call=ThermalTick(w,o,gs,5);Assert.IsNotNull(call);StringAssert.Contains("Öl",call.Text);Assert.AreEqual(9,call.Priority);
            Assert.AreEqual(DamageLevel.UNKNOWN,gs.CarDamageData.OverallEngineDamage);
        }

        private static RaceDataContext FreshContext(GameStateData gs,DateTime now)
        {return new RaceDataContext {GameState=gs,NowUtc=now,LastTelemetryUtc=now};}
        private static AiRadioPreferences RoutineOnly(string topic,int seconds=120)
        {
            var p=new AiRadioPreferences();foreach(var o in AiRadioPreferences.Options)p.Set(o.Id,0);
            p.Set(topic,seconds);p.SetTolerance(topic,0,0);return p;
        }
        [TestCase(true)] [TestCase(false)]
        public void T31_Completion_feedback_runs_once_only_after_backend_start(bool interrupted)
        {
            int count=0;bool heard=false;
            string id=AiAudioStore.Put(new byte[44],"Test",finished:value=>{count++;heard=value;});
            try
            {
                AiAudioStore.MarkFinished(id,interrupted);Assert.AreEqual(0,count);
                Assert.IsTrue(AiAudioStore.TryStart(id,()=>{}));AiAudioStore.MarkFinished(id,interrupted);
                AiAudioStore.MarkFinished(id,!interrupted);Assert.AreEqual(1,count);Assert.AreEqual(interrupted,heard);
            }finally{AiAudioStore.Remove(id);}
        }
        [Test]
        public void T31_Interrupted_routine_is_recomposed_after_thirty_seconds_with_current_values()
        {
            var gs=Frame(2,false,false,true);gs.EngineData.EngineWaterTemp=90;
            var p=RoutineOnly("vehicle");var q=new AiRadioSchedule(1);q.Observe(gs,T,p);
            var first=q.Take(FreshContext(gs,T.AddSeconds(120)),p,false);Assert.IsNotNull(first);
            first.MarkPlaybackStarted(T.AddSeconds(120));first.MarkPlaybackFinished(T.AddSeconds(121),true);
            q.Take(FreshContext(gs,T.AddSeconds(121)),p,false);q.Take(FreshContext(gs,T.AddSeconds(122)),p,false);
            Assert.IsNull(q.Take(FreshContext(gs,T.AddSeconds(151)),p,false));
            gs.EngineData.EngineWaterTemp=97;var fresh=q.Take(FreshContext(gs,T.AddSeconds(152)),p,false);
            Assert.IsNotNull(fresh);Assert.AreNotSame(first,fresh);StringAssert.Contains("97",fresh.Text);
        }
        [Test]
        public void T31_Uninterrupted_report_keeps_configured_interval()
        {
            var gs=Frame(2,false,false,true);var p=RoutineOnly("fuel");var q=new AiRadioSchedule(1);q.Observe(gs,T,p);
            var first=q.Take(FreshContext(gs,T.AddSeconds(120)),p,false);first.MarkPlaybackStarted(T.AddSeconds(120));
            first.MarkPlaybackFinished(T.AddSeconds(125),false);q.Take(FreshContext(gs,T.AddSeconds(126)),p,false);
            Assert.IsNull(q.Take(FreshContext(gs,T.AddSeconds(160)),p,false));Assert.IsNotNull(q.Take(FreshContext(gs,T.AddSeconds(240)),p,false));
        }
        [TestCase(SessionType.HotLap,false)] [TestCase(SessionType.Practice,true)] [TestCase(SessionType.Race,true)]
        public void T17_Hotlap_mode_protects_push_lap_without_silencing_all_practice(SessionType type,bool canSpeak)
        {
            var gs=Frame(2,false,false,true);gs.SessionData.SessionType=type;
            var p=RoutineOnly("fuel",30);var q=new AiRadioSchedule(1);q.Observe(gs,T,p);
            Assert.AreEqual(canSpeak,q.Take(FreshContext(gs,T.AddSeconds(30)),p,false)!=null);
        }
        private static ObservedRaceCall DamageTick(NumericDamageWatch w,LiveWarningDispatcher o,GameStateData gs,double seconds)
        {
            DateTime now=T.AddSeconds(seconds);gs.Now=now;gs.SourceTelemetryUtc=now;
            w.Observe(gs,now,o);o.Observe(gs,now);return o.Take(gs,now,false,m=>true);
        }
        [Test]
        public void T11_Fine_damage_accumulates_below_coarse_levels_and_is_acknowledged_on_playback()
        {
            var gs=Frame(1,false,false,true);gs.CarDamageData.AeroDamagePercent=0;var w=new NumericDamageWatch();var o=new LiveWarningDispatcher();
            Assert.IsNull(DamageTick(w,o,gs,0));gs.CarDamageData.AeroDamagePercent=.4f;Assert.IsNull(DamageTick(w,o,gs,1));
            gs.CarDamageData.AeroDamagePercent=.8f;Assert.IsNull(DamageTick(w,o,gs,2));gs.CarDamageData.AeroDamagePercent=1;
            var call=DamageTick(w,o,gs,3);Assert.IsNotNull(call);StringAssert.Contains("1,0 Prozent",call.Text);
            Assert.AreEqual(DamageLevel.UNKNOWN,gs.CarDamageData.OverallAeroDamage);call.MarkPlaybackStarted(T.AddSeconds(3));
            Assert.IsNull(DamageTick(w,o,gs,3.1));Assert.IsNull(DamageTick(w,o,gs,40));
            gs.CarDamageData.AeroDamagePercent=2;Assert.IsNotNull(DamageTick(w,o,gs,41));
        }
        [Test]
        public void T11_Initial_measurement_repair_missing_value_and_session_change_are_not_new_incidents()
        {
            var gs=Frame(1,false,false,true);var w=new NumericDamageWatch();var o=new LiveWarningDispatcher();gs.CarDamageData.EngineDamagePercent=4;
            Assert.IsNull(DamageTick(w,o,gs,0));gs.CarDamageData.EngineDamagePercent=.1f;Assert.IsNull(DamageTick(w,o,gs,1));
            gs.CarDamageData.EngineDamagePercent=-1;Assert.IsNull(DamageTick(w,o,gs,2));gs.CarDamageData.EngineDamagePercent=10;
            Assert.IsNull(DamageTick(w,o,gs,3));gs.SessionData.SessionId++;o.Reset();Assert.IsNull(DamageTick(w,o,gs,4));
        }
        [Test]
        public void T11_Damage_report_does_not_hide_numeric_corners_behind_disabled_global_flag_or_infer_force_loss()
        {
            var gs=Frame(1,false,false,true);gs.CarDamageData.DamageEnabled=false;gs.CarDamageData.EngineDamagePercent=.337f;
            gs.CarDamageData.BrakeDamagePercent[0]=2.061f;string text=IncidentWatch.DamageReport(gs.CarDamageData);
            StringAssert.Contains("Motor-Schadenswert 0,3 Prozent",text);StringAssert.Contains("Bremse vorne links-Schadenswert 2,1 Prozent",text);
            StringAssert.DoesNotContain("Bremskraft",text);StringAssert.DoesNotContain("fahrbar",text);
        }

        [TestCase(10.0343628f,10.0343628f,-1,-1)]
        [TestCase(79.426f,42.2f,37.226f,-1)]
        [TestCase(124.142f,42.2f,40.5f,41.442f)]
        public void A02_Native_clock_and_UDP_sector_sum_use_complete_available_values(float clock,float s1,float s2,float s3)
        {
            var raw=new CrewChiefV4.AMS2.ams2APIStruct {mmfOnly_mCurrentTime=clock,
                mCurrentSector1Times=new float[64],mCurrentSector2Times=new float[64],mCurrentSector3Times=new float[64]};
            raw.mCurrentSector1Times[0]=s1;raw.mCurrentSector2Times[0]=s2;raw.mCurrentSector3Times[0]=s3;
            Assert.AreEqual(clock,CrewChiefV4.AMS2.AMS2GameStateMapper.CurrentLapSeconds(raw,0,true));
            Assert.That(CrewChiefV4.AMS2.AMS2GameStateMapper.CurrentLapSeconds(raw,0,false),Is.EqualTo(clock).Within(.001));
        }
        [Test]
        public void A02_Missing_or_invalid_lap_timer_is_never_a_negative_sector_sum_or_invented_native_zero()
        {
            var raw=new CrewChiefV4.AMS2.ams2APIStruct {mmfOnly_mCurrentTime=float.NaN,
                mCurrentSector1Times=new float[64],mCurrentSector2Times=new float[64],mCurrentSector3Times=new float[64]};
            raw.mCurrentSector1Times[0]=raw.mCurrentSector2Times[0]=raw.mCurrentSector3Times[0]=-1;
            Assert.AreEqual(-1,CrewChiefV4.AMS2.AMS2GameStateMapper.CurrentLapSeconds(raw,0,true));
            Assert.AreEqual(-1,CrewChiefV4.AMS2.AMS2GameStateMapper.CurrentLapSeconds(raw,0,false));
        }
        [Test]
        public void T15_Garage_teleport_cancels_old_pit_approach_instead_of_claiming_a_physical_entry()
        {
            var gs=Frame(1,false,false,true);gs.PitData.HasRequestedPitStop=true;gs.PositionAndMotionData.CarSpeed=50;
            gs.PitData.PitSpeedLimit=80/3.6f;gs.SessionData.SessionRunningTime=100;var w=new PitLaneWatch();w.Observe(gs,T);
            var approach=w.Take(gs,T,false,1);Assert.IsNotNull(approach);
            gs.PitData.InPitlane=true;gs.PitData.JumpedToPits=true;gs.PositionAndMotionData.CarSpeed=0;gs.SessionData.SessionRunningTime=101;
            w.Observe(gs,T.AddSeconds(1));Assert.IsNull(w.Take(gs,T.AddSeconds(1),false,1));Assert.IsFalse(approach.IsValid(gs,T.AddSeconds(1)));
        }

        [Test]
        public void T30_Delayed_historic_best_on_join_is_a_baseline_not_a_new_record_event()
        {
            var gs=Frame(4,false,false,true);gs.SessionData.PlayerLapTimeSessionBest=-1;var w=new SessionLapObserver();w.Observe(gs,null,T,false);
            gs.SessionData.PlayerLapTimeSessionBest=120;w.Observe(gs,null,T.AddSeconds(.1),false);
            var lap=new LapRecord {LapNumber=5,LapTime=125,Valid=true};gs.SessionData.CompletedLaps=5;w.Observe(gs,lap,T.AddSeconds(125),false);
            Assert.IsNull(w.Take(gs,T.AddSeconds(125),false));
            lap=new LapRecord {LapNumber=6,LapTime=119,Valid=true};gs.SessionData.CompletedLaps=6;gs.SessionData.PlayerLapTimeSessionBest=119;
            w.Observe(gs,lap,T.AddSeconds(244),false);var call=w.Take(gs,T.AddSeconds(244),false);Assert.IsNotNull(call);
            StringAssert.Contains("Bestzeit",call.Text);StringAssert.DoesNotContain("Erste gültige",call.Text);
        }
        [TestCase(false,false,true)] [TestCase(true,false,false)] [TestCase(false,true,false)]
        public void T18_Wear_trends_require_adjacent_clean_laps_without_a_tyre_reset(bool missingLap,bool freshTyres,bool expected)
        {
            var m=new AiRaceMemory();var start=Frame(0,true,false,true);m.Update(null,start,T);
            GameStateData prev=start;
            for(int i=1;i<=3;i++)
            {
                var gs=Frame(i,true,false,true);gs.TyreData.TyreWearActive=!(missingLap && i==2);
                float wear=freshTyres && i==3?1:10+i;
                gs.TyreData.FrontLeftPercentWear=gs.TyreData.FrontRightPercentWear=gs.TyreData.RearLeftPercentWear=gs.TyreData.RearRightPercentWear=wear;
                gs.PitData.InPitlane=freshTyres && i==2;m.Update(prev,gs,T.AddSeconds(i*128));prev=gs;
            }
            Assert.AreEqual(expected,m.AverageWearPerLap()!=null);
            if(expected)Assert.That(m.AverageWearPerLap()[0],Is.EqualTo(1).Within(.001));
        }

        [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)]
        public void T05_Uncounted_start_line_crossing_is_recognized_for_practice_and_qualifying(SessionType type)
        {
            var before=Frame(0,false,false,false);var next=Frame(0,false,false,true);
            before.SessionData.SessionType=next.SessionData.SessionType=type;
            before.SessionData.SectorNumber=3;next.SessionData.SectorNumber=1;
            before.SessionData.SessionRunningTime=100;next.SessionData.SessionRunningTime=100.1f;
            before.PositionAndMotionData.DistanceRoundTrack=6398;next.PositionAndMotionData.DistanceRoundTrack=1;
            next.SessionData.TrackDefinition=new CrewChiefV4.TrackDefinition("Test",6400);
            Assert.IsTrue(CrewChiefV4.AMS2.AMS2GameStateMapper.ConfirmedUncountedStartLineCrossing(before,next));
            next.PitData.JumpedToPits=true;Assert.IsFalse(CrewChiefV4.AMS2.AMS2GameStateMapper.ConfirmedUncountedStartLineCrossing(before,next));
            next.PitData.JumpedToPits=false;next.SessionData.SessionRunningTime=105;
            Assert.IsFalse(CrewChiefV4.AMS2.AMS2GameStateMapper.ConfirmedUncountedStartLineCrossing(before,next));
        }

        [Test]
        public void T22_Diagnostic_live_temperature_is_current_LCR_average_not_lap_peak_or_missing_zero()
        {
            Assert.AreEqual(100,LiveDiagnostics.MeanTemperature(90,100,110));
            Assert.IsNull(LiveDiagnostics.MeanTemperature(0,100,110));Assert.IsNull(LiveDiagnostics.MeanTemperature(float.NaN,100,110));
        }
        [Test]
        public void T02_Sub_kPa_pressure_noise_does_not_cancel_radio_but_material_changes_do()
        {
            var gs=Frame(1,false,false,true);TelemetryMeasurements.Put(gs,"engine.oil_pressure",450,"AMS2.API","mOilPressureKPa","motor","kPa",T,0,100000);
            var evidence=new RaceSpeechEvidence(FreshContext(gs,T));
            TelemetryMeasurements.Put(gs,"engine.oil_pressure",450.2,"AMS2.API","mOilPressureKPa","motor","kPa",T.AddSeconds(.1),0,100000);
            Assert.IsTrue(evidence.IsCurrent(FreshContext(gs,T.AddSeconds(.1))));
            TelemetryMeasurements.Put(gs,"engine.oil_pressure",460,"AMS2.API","mOilPressureKPa","motor","kPa",T.AddSeconds(.2),0,100000);
            Assert.IsFalse(evidence.IsCurrent(FreshContext(gs,T.AddSeconds(.2))));
        }

        [Test]
        public void T31_Audio_driver_failure_reports_interruption_and_releases_the_waiter()
        {
            bool? interrupted=null;string id=AiAudioStore.Put(new byte[44],"Test",finished:failure=>interrupted=failure);AiAudioStore.MarkStarted(id);
            var type=typeof(CrewChiefV4.Audio.SoundCache).Assembly.GetType("CrewChiefV4.Audio.SingleSound");
            object clip=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            using(var waiter=new System.Threading.AutoResetEvent(false))
            {
                type.GetField("isAiAudio",flags).SetValue(clip,true);type.GetField("aiAudioId",flags).SetValue(clip,id);
                type.GetField("playWaitHandle",flags).SetValue(clip,waiter);var method=type.GetMethod("playbackStopped",flags);
                object stopped=Activator.CreateInstance(method.GetParameters()[1].ParameterType,new object[]{new InvalidOperationException("synthetic driver failure")});
                method.Invoke(clip,new object[]{null,stopped});Assert.AreEqual(true,interrupted);
                Assert.IsTrue(waiter.WaitOne(0));
            }
            AiAudioStore.Remove(id);
        }
    }
}
