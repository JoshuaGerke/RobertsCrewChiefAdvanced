using System;
using System.IO;
using System.Text;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam")]
    public class EvidenceAccuracyTests
    {
        private static readonly DateTime T=new DateTime(2026,11,4,10,0,0,DateTimeKind.Utc);
        [Test]
        public void Raceroom_history_identity_rejects_reused_name_car_number_and_slot()
        {
            var old=new OpponentData {DriverRawName="Alex",CarNumber="7",r3eUserId=101,r3eSlotId=3};
            var current=new CrewChiefV4.RaceRoom.RaceRoomData.DriverData {
                DriverInfo=new CrewChiefV4.RaceRoom.RaceRoomData.DriverInfo {CarNumber=7,UserId=101,SlotId=3}};
            Assert.IsTrue(CrewChiefV4.RaceRoom.R3EGameStateMapper.SameOpponentIdentity(old,current));
            string identity=RaceReader.Identity("Alex",old);
            current.DriverInfo.UserId=102;
            Assert.IsFalse(CrewChiefV4.RaceRoom.R3EGameStateMapper.SameOpponentIdentity(old,current));
            old.r3eUserId=102;
            Assert.AreNotEqual(identity,RaceReader.Identity("Alex",old));
            current.DriverInfo.UserId=102;current.DriverInfo.SlotId=4;
            Assert.IsFalse(CrewChiefV4.RaceRoom.R3EGameStateMapper.SameOpponentIdentity(old,current));
        }

        [Test]
        public void Raceroom_text_fields_do_not_abort_mapping_when_empty_or_without_terminator()
        {
            Assert.AreEqual("",CrewChiefV4.RaceRoom.R3EGameStateMapper.getNameFromBytes(null));
            Assert.AreEqual("",CrewChiefV4.RaceRoom.R3EGameStateMapper.getNameFromBytes(new byte[0]));
            Assert.AreEqual("Müller",CrewChiefV4.RaceRoom.R3EGameStateMapper.getNameFromBytes(Encoding.UTF8.GetBytes("Müller")));
            Assert.AreEqual("Rob",CrewChiefV4.RaceRoom.R3EGameStateMapper.getNameFromBytes(Encoding.UTF8.GetBytes("Rob\0old slot data")));
        }

        [Test]
        public void Raceroom_player_uses_unique_active_slot_even_when_names_collide_or_change()
        {
            var shared=new CrewChiefV4.RaceRoom.RaceRoomData.RaceRoomShared {NumCars=2};
            shared.VehicleInfo.SlotId=42;
            shared.PlayerName=Encoding.UTF8.GetBytes("Rob");
            shared.DriverData=new[] {
                new CrewChiefV4.RaceRoom.RaceRoomData.DriverData {DriverInfo=new CrewChiefV4.RaceRoom.RaceRoomData.DriverInfo {SlotId=7,Name=Encoding.UTF8.GetBytes("Rob")}},
                new CrewChiefV4.RaceRoom.RaceRoomData.DriverData {DriverInfo=new CrewChiefV4.RaceRoom.RaceRoomData.DriverInfo {SlotId=42,Name=Encoding.UTF8.GetBytes("Different display name")}},
                new CrewChiefV4.RaceRoom.RaceRoomData.DriverData {DriverInfo=new CrewChiefV4.RaceRoom.RaceRoomData.DriverInfo {SlotId=42}}};
            Assert.AreEqual(1,CrewChiefV4.RaceRoom.R3EGameStateMapper.FindPlayerIndex(shared));
            Array.Reverse(shared.DriverData,0,2);
            Assert.AreEqual(0,CrewChiefV4.RaceRoom.R3EGameStateMapper.FindPlayerIndex(shared));
            shared.NumCars=3;
            Assert.AreEqual(-1,CrewChiefV4.RaceRoom.R3EGameStateMapper.FindPlayerIndex(shared),"Duplicate active slots must not select an arbitrary vehicle.");
            shared.NumCars=2;shared.VehicleInfo.SlotId=99;
            Assert.AreEqual(-1,CrewChiefV4.RaceRoom.R3EGameStateMapper.FindPlayerIndex(shared),"No fallback to array zero or a matching name.");
            shared.NumCars=4;
            Assert.AreEqual(-1,CrewChiefV4.RaceRoom.R3EGameStateMapper.FindPlayerIndex(shared));
            shared.NumCars=-1;
            Assert.AreEqual(-1,CrewChiefV4.RaceRoom.R3EGameStateMapper.FindPlayerIndex(shared));
        }

        [Test]
        public void Lmu_physics_clock_follows_player_id_not_array_order_or_opponent_progress()
        {
            var reader=new CrewChiefV4.rFactor2.RF2_LMUSharedMemoryReader(CrewChiefV4.rFactor2.SharedMemoryMode.LMU,false);
            var frame=new CrewChiefV4.rFactor2.RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper {ticksWhenRead=T.Ticks};
            frame.scoring.mScoringInfo.mNumVehicles=2;
            frame.scoring.mVehicles=new[] {
                new rF2SharedMemory.rFactor2Data.rF2VehicleScoring {mID=7,mIsPlayer=1},
                new rF2SharedMemory.rFactor2Data.rF2VehicleScoring {mID=8}};
            frame.telemetry.mNumVehicles=2;
            frame.telemetry.mVehicles=new[] {
                new rF2SharedMemory.rFactor2Data.rF2VehicleTelemetry {mID=8,mElapsedTime=10},
                new rF2SharedMemory.rFactor2Data.rF2VehicleTelemetry {mID=7,mElapsedTime=10}};
            Assert.AreEqual(T.Ticks,reader.ObservePlayerTelemetryProgress(frame));
            frame.ticksWhenRead=T.AddSeconds(3).Ticks;
            frame.telemetry.mVehicles[0].mElapsedTime=13;
            Assert.AreEqual(T.Ticks,reader.ObservePlayerTelemetryProgress(frame),"Opponent movement cannot refresh frozen player physics.");
            Array.Reverse(frame.telemetry.mVehicles);
            frame.telemetry.mVehicles[0].mElapsedTime=13;
            Assert.AreEqual(frame.ticksWhenRead,reader.ObservePlayerTelemetryProgress(frame));
            frame.ticksWhenRead=T.AddSeconds(5).Ticks;
            frame.telemetry.mVehicles[1].mID=7;
            Assert.AreEqual(T.AddSeconds(3).Ticks,reader.ObservePlayerTelemetryProgress(frame),"Duplicate IDs must keep aging, not renew freshness.");
            frame.telemetry.mVehicles[1].mID=8;
            frame.telemetry.mVehicles[0].mElapsedTime=double.NaN;
            Assert.AreEqual(T.AddSeconds(3).Ticks,reader.ObservePlayerTelemetryProgress(frame));
            frame.scoring.mScoringInfo.mNumVehicles=3;
            Assert.AreEqual(T.AddSeconds(3).Ticks,reader.ObservePlayerTelemetryProgress(frame));
        }

        [Test]
        public void Lmu_combined_source_uses_the_older_channel_and_preserves_legacy_recordings()
        {
            var frame=new CrewChiefV4.rFactor2.RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper {
                ticksWhenRead=T.AddSeconds(5).Ticks,scoringSampleTicks=T.AddSeconds(4).Ticks,playerTelemetrySampleTicks=T.Ticks};
            Assert.AreEqual(T.Ticks,frame.OldestKnownSourceTicks);
            frame.playerTelemetrySampleTicks=T.AddSeconds(5).Ticks;
            Assert.AreEqual(T.AddSeconds(4).Ticks,frame.OldestKnownSourceTicks);
            frame.playerTelemetrySampleTicks=0;
            Assert.AreEqual(T.AddSeconds(4).Ticks,frame.OldestKnownSourceTicks);
            frame.scoringSampleTicks=0;
            Assert.AreEqual(0,frame.OldestKnownSourceTicks);
        }

        private static GameStateData State(DateTime? time=null)
        {
            var gs=new GameStateData((time??T).Ticks);gs.SourceTelemetryUtc=time??T;
            gs.carClass=new CarData.CarClass {carClassEnum=CarData.CarClassEnum.AUDI_TT_CUP};
            gs.SessionData.SessionType=SessionType.Race;gs.SessionData.SessionPhase=SessionPhase.Green;
            gs.SessionData.ClassPosition=3;gs.SessionData.LapsDeltaFront=gs.SessionData.LapsDeltaBehind=0;
            gs.SessionData.TrackDefinition=new TrackDefinition("Test",0,5000,new float[]{0,0},new float[]{0,0});
            gs.OpponentData["a"]=new OpponentData {IsActive=true,DriverRawName="Alex",CarNumber="5",ClassPosition=2,CarClass=gs.carClass};
            gs.SessionData.OpponentKeyInFront="a";gs.SessionData.TimeDeltaFront=3;
            return gs;
        }
        private static RaceDataContext Context(GameStateData gs,DateTime now)
        {return new RaceDataContext {GameState=gs,NowUtc=now,LastTelemetryUtc=now};}

        private static CrewChiefV4.AMS2.AMS2SharedMemoryReader.AMS2StructWrapper Ams2Raw(GameStateData gs)
        {
            gs.SessionData.DriverRawName="Rob"; gs.OpponentData["a"].OverallPosition=2;
            var raw=new CrewChiefV4.AMS2.ams2APIStruct {
                mVersion=14,mNumParticipants=1,
                mParticipantData=new[] {new CrewChiefV4.AMS2.ams2APIParticipantStruct {
                    mIsActive=true,mName=Encoding.ASCII.GetBytes("Alex\0"),mRacePosition=2}},
                mFastestSector1Times=new[]{32.1234f},mFastestSector2Times=new[]{-1f},mFastestSector3Times=new[]{float.NaN},
                mFastestLapTimes=new[]{103.456f},mLastLapTimes=new[]{104.567f}
            };
            var wrapper=new CrewChiefV4.AMS2.AMS2SharedMemoryReader.AMS2StructWrapper {data=raw,sdk14LayoutSelected=true};
            gs.rawGameData=wrapper;return wrapper;
        }

        private static Newtonsoft.Json.Linq.JToken Ams2Opponent(GameStateData gs,DateTime now)
        { return RaceFieldTools.Read("get_opponent",Context(gs,now),"{\"overall_position\":2}")["fahrer"][0]["ams2_simulator_bestwerte"]; }

        [Test]
        public void Ams2_sdk_best_times_reach_the_matching_opponent_with_missing_values_and_provenance()
        {
            var gs=State();Ams2Raw(gs);var values=Ams2Opponent(gs,T);
            Assert.IsNotNull(values);Assert.That((double)values["beste_einzelsektoren_s"][0],Is.EqualTo(32.123).Within(.001));
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null,values["beste_einzelsektoren_s"][1].Type);
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null,values["beste_einzelsektoren_s"][2].Type);
            Assert.That((double)values["beste_runde_s"],Is.EqualTo(103.456).Within(.001));
            StringAssert.Contains("nicht belegt",(string)values["hinweis"]);
        }

        [Test]
        public void Ams2_sdk_best_times_reject_stale_unverified_mismatched_and_ambiguous_participants()
        {
            var gs=State();var wrapper=Ams2Raw(gs);
            Assert.IsNull(Ams2Opponent(gs,T.AddSeconds(2)),"Old source data must not look live.");
            wrapper.sdk14LayoutSelected=false;Assert.IsNull(Ams2Opponent(gs,T));wrapper.sdk14LayoutSelected=true;
            var raw=wrapper.data;var p=raw.mParticipantData[0];p.mRacePosition=3;raw.mParticipantData[0]=p;wrapper.data=raw;
            Assert.IsNull(Ams2Opponent(gs,T),"A reused participant slot must not be attached to another position.");
            p.mRacePosition=2;raw.mParticipantData=new[]{p,p};raw.mNumParticipants=2;wrapper.data=raw;
            Assert.IsNull(Ams2Opponent(gs,T),"Duplicate active names are ambiguous even if positions match.");
        }

        [Test]
        public void Ams2_sdk_best_times_ignore_retired_slots_after_field_shrinks()
        {
            var gs=State();var wrapper=Ams2Raw(gs);var raw=wrapper.data;
            var alex=raw.mParticipantData[0];
            raw.mParticipantData=new[]{alex,alex};wrapper.data=raw;
            Assert.IsNotNull(Ams2Opponent(gs,T),"Ignore stale duplicates outside the current field.");
            var other=alex;other.mName=Encoding.ASCII.GetBytes("Other\0");
            raw.mParticipantData=new[]{other,alex};wrapper.data=raw;
            Assert.IsNull(Ams2Opponent(gs,T),"Retired slots must not supply opponent timing.");
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(2)]
        [TestCase(65)]
        public void Ams2_sdk_best_times_reject_invalid_participant_counts(int count)
        {
            var gs=State();var wrapper=Ams2Raw(gs);var raw=wrapper.data;
            raw.mNumParticipants=count;wrapper.data=raw;
            Assert.IsNull(Ams2Opponent(gs,T));
        }

        [Test]
        public void Partial_radio_override_uses_a_validated_contract_and_keeps_other_defaults()
        {
            const string json = "{\"version\":1,\"groups\":{\"pit-gain\":[\"{opponent} kommt rein. Platz {position}.\"]}}";
            var catalog = RadioPhraseCatalog.ParseOverrides(new StringReader(json));
            Assert.AreEqual("Müller kommt rein. Platz 7.", catalog.Preview("pit-gain", 0, "Rob"));
            Assert.AreEqual(RadioPhraseCatalog.Current.Preview("fuel", 1, "Rob"), catalog.Preview("fuel", 1, "Rob"));
            Assert.AreEqual("{position} kommt rein. Platz 7.", catalog.Format("pit-gain", 0, "{position}", "7"));
        }

        [Test]
        public void Ams2_udp_rejects_truncated_datagrams_and_legacy_layout_conflicts()
        {
            var buffer=new byte[2048];buffer[10]=(byte)CrewChiefV4.AMS2.EUDPStreamerPacketHandlerType.eGameState;
            Assert.IsFalse(CrewChiefV4.AMS2.AMS2UDPreader.PacketFitsDeclaredLayout(buffer,11));
            Assert.IsFalse(CrewChiefV4.AMS2.AMS2UDPreader.PacketFitsDeclaredLayout(buffer,23));
            Assert.IsTrue(CrewChiefV4.AMS2.AMS2UDPreader.PacketFitsDeclaredLayout(buffer,24));
            Assert.IsFalse(CrewChiefV4.AMS2.AMS2UDPreader.PacketFitsDeclaredLayout(buffer,25));
            Assert.IsFalse(CrewChiefV4.AMS2.AMS2UDPreader.PacketFitsDeclaredLayout(buffer,2049));
            Assert.IsFalse(CrewChiefV4.AMS2.AMS2UDPreader.PacketFitsDeclaredLayout(null,24));
            foreach(var type in new[]{CrewChiefV4.AMS2.EUDPStreamerPacketHandlerType.eCarPhysics,
                CrewChiefV4.AMS2.EUDPStreamerPacketHandlerType.eParticipants,CrewChiefV4.AMS2.EUDPStreamerPacketHandlerType.eTimings,
                CrewChiefV4.AMS2.EUDPStreamerPacketHandlerType.eTimeStats})
            {
                buffer[10]=(byte)type;
                int declared=type==CrewChiefV4.AMS2.EUDPStreamerPacketHandlerType.eCarPhysics?538:
                    type==CrewChiefV4.AMS2.EUDPStreamerPacketHandlerType.eParticipants?1040:
                    type==CrewChiefV4.AMS2.EUDPStreamerPacketHandlerType.eTimings?993:784;
                Assert.IsFalse(CrewChiefV4.AMS2.AMS2UDPreader.PacketFitsDeclaredLayout(buffer,declared),
                    "Legacy declared bytes do not cover the unmanaged structure; reject before marshaling.");
            }
            buffer[10]=255;Assert.IsFalse(CrewChiefV4.AMS2.AMS2UDPreader.PacketFitsDeclaredLayout(buffer,24));
        }

        [Test]
        public void Ac_source_freshness_requires_both_physics_and_graphics_to_advance()
        {
            var reader=new CrewChiefV4.assetto.ACSSharedMemoryReader();
            var raw=new CrewChiefV4.assetto.assettoData.AssettoCorsaShared();
            raw.acsGraphic.packetId=1;raw.acsPhysics.packetId=1;
            Assert.AreEqual(T.Ticks,reader.ObserveSourceProgress(raw,T.Ticks));
            raw.acsPhysics.packetId=2;
            Assert.AreEqual(T.Ticks,reader.ObserveSourceProgress(raw,T.AddSeconds(2).Ticks));
            raw.acsGraphic.packetId=2;
            Assert.AreEqual(T.AddSeconds(2).Ticks,reader.ObserveSourceProgress(raw,T.AddSeconds(3).Ticks));
            raw.acsPhysics.packetId=int.MinValue;raw.acsGraphic.packetId=int.MinValue;
            Assert.AreEqual(T.AddSeconds(4).Ticks,reader.ObserveSourceProgress(raw,T.AddSeconds(4).Ticks));
            Assert.AreEqual(T.AddSeconds(4).Ticks,reader.ObserveSourceProgress(raw,T.AddSeconds(6).Ticks));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Measured_gap_variants_keep_class_and_qualified_measurement(bool ahead)
        {
            var heard = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < RadioPhraseCatalog.Current.Count(ahead ? "gap-front-measured" : "gap-rear-measured"); i++)
            {
                string text = RaceRadioLanguage.MeasuredGap(ahead, "Müller", "ungefähr 1,2 Sekunden", true, i);
                StringAssert.StartsWith("In deiner Klasse: ", text);
                StringAssert.Contains("Müller", text);
                StringAssert.Contains("ungefähr 1,2 Sekunden", text);
                Assert.IsTrue(heard.Add(text));
            }
        }

        [TestCase(true, 0)]
        [TestCase(false, 1)]
        public void Radio_variants_advance_only_after_completed_audio(bool interrupted,int expected)
        {
            var cursor=new RadioVariantCursor();
            var call=new ObservedRaceCall {DetectedUtc=T,ExpiresUtc=T.AddSeconds(5),VariantCursor=cursor};
            Assert.IsTrue(call.TryReserve(T));call.MarkPlaybackStarted(T);
            Assert.AreEqual(0,cursor.Heard);
            call.MarkPlaybackFinished(T.AddSeconds(1),interrupted);
            call.MarkPlaybackFinished(T.AddSeconds(2),false);
            Assert.AreEqual(expected,cursor.Heard);
        }

        [Test]
        public void Event_validity_rejects_stale_and_future_source_even_with_current_mapping()
        {
            var gs=State();var call=new ObservedRaceCall {DetectedUtc=T,ExpiresUtc=T.AddSeconds(10),
                SessionId=gs.SessionData.SessionId,SessionType=SessionType.Race,Position=-1};
            Assert.IsTrue(call.IsValid(gs,T));
            Assert.IsFalse(call.IsValid(gs,T.AddSeconds(2)));
            gs.SourceTelemetryUtc=T.AddSeconds(1);Assert.IsFalse(call.IsValid(gs,T));
        }

        [TestCase(-2, false)]
        [TestCase(-1, false)]
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(64, false)]
        public void Ams2_player_lookup_rejects_unavailable_and_retired_indices(int index, bool valid)
        {
            var gs=State();var wrapper=Ams2Raw(gs);
            Assert.AreEqual(valid,CrewChiefV4.AMS2.AMS2GameStateMapper.ValidParticipantIndex(wrapper.data,index));
            var p=wrapper.data.mParticipantData[0];p.mIsActive=false;wrapper.data.mParticipantData[0]=p;
            Assert.IsFalse(CrewChiefV4.AMS2.AMS2GameStateMapper.ValidParticipantIndex(wrapper.data,0));
        }

        [Test, NonParallelizable]
        public void Ams2_menu_does_not_inherit_a_reassigned_player_slot()
        {
            var gs=State();var raw=Ams2Raw(gs).data;raw.mViewedParticipantIndex=0;
            raw.mGameState=(uint)CrewChiefV4.AMS2.eGameState.GAME_INGAME_PLAYING;
            Assert.AreEqual(0,CrewChiefV4.AMS2.AMS2GameStateMapper.getPlayerIndex(raw));
            raw.mGameState=(uint)CrewChiefV4.AMS2.eGameState.GAME_INGAME_INMENU_TIME_TICKING;
            Assert.AreEqual(0,CrewChiefV4.AMS2.AMS2GameStateMapper.getPlayerIndex(raw));
            var p=raw.mParticipantData[0];p.mName=Encoding.ASCII.GetBytes("NewDriver\0");raw.mParticipantData[0]=p;
            Assert.AreEqual(-1,CrewChiefV4.AMS2.AMS2GameStateMapper.getPlayerIndex(raw));
            raw.mGameState=(uint)CrewChiefV4.AMS2.eGameState.GAME_INGAME_PLAYING;
            Assert.AreEqual(0,CrewChiefV4.AMS2.AMS2GameStateMapper.getPlayerIndex(raw));
        }

        [Test]
        public void Pace_calls_offer_variety_and_preserve_the_measured_driver_and_difference()
        {
            var catalog=RadioPhraseCatalog.Current;
            foreach(string group in new[]{"pace-front-faster","pace-front-slower","pace-rear-faster","pace-rear-slower"})
            {
                Assert.GreaterOrEqual(catalog.Count(group),6);
                for(int i=0;i<catalog.Count(group);i++) {
                    string line=catalog.Format(group,i,"0,4","Müller");
                    StringAssert.Contains("0,4",line);StringAssert.Contains("Müller",line);
                }
            }
        }

        [TestCase("{opponent} kommt rein.")]
        [TestCase("{opponent} kommt rein. Platz {position}. {weather}")]
        [TestCase("{opponent} kommt rein. Platz {position}. {}")]
        public void Radio_override_rejects_wrong_or_missing_facts(string line)
        {
            string json = "{\"version\":1,\"groups\":{\"pit-gain\":[\"" + line + "\"]}}";
            Assert.Throws<InvalidDataException>(() => RadioPhraseCatalog.ParseOverrides(new StringReader(json)));
        }

        [Test]
        public void Retained_mapper_key_never_attaches_old_gap_to_new_class_neighbour()
        {
            var gs=State();var old=gs.OpponentData["a"];old.ClassPosition=4;
            var current=new OpponentData {IsActive=true,DriverRawName="Bea",ClassPosition=2,CarClass=gs.carClass};
            gs.OpponentData["b"]=current;
            Assert.AreSame(current,RaceDataTools.Neighbour(gs,true));
            Assert.IsFalse(RaceDataTools.ReadGap(gs,current,true).HasValue);
        }
        [TestCase(-2)] [TestCase(2)]
        public void Fresh_context_does_not_refresh_stale_or_future_source(int seconds)
        {
            var gs=State();gs.SourceTelemetryUtc=T.AddSeconds(seconds);
            Assert.IsFalse(RaceDataTools.ReadGap(Context(gs,T),gs.OpponentData["a"],true).HasValue);
        }
        [Test]
        public void Native_gap_carries_actual_adapter_snapshot_time()
        {
            var gs=State();var gap=RaceDataTools.ReadGap(Context(gs,T.AddMilliseconds(200)),gs.OpponentData["a"],true);
            Assert.AreEqual(RaceGapKind.SimulatorTiming,gap.Kind);Assert.AreEqual(T,gap.MeasuredUtc);
        }
        [TestCase(true)] [TestCase(false)]
        public void Same_name_does_not_let_a_new_number_or_slot_reuse_queued_report(bool changeNumber)
        {
            var now=DateTime.UtcNow;var gs=State(now);var call=new ObservedRaceCall {Topic="ahead",Position=-1,
                SessionId=gs.SessionData.SessionId,DetectedUtc=now,ExpiresUtc=now.AddSeconds(6)};
            call.IncludeNeighbour(Context(gs,now),true,false);
            Assert.IsTrue(call.IsValid(gs,now));
            if(changeNumber)gs.OpponentData["a"].CarNumber="99";
            else {var car=gs.OpponentData["a"];gs.OpponentData.Remove("a");gs.OpponentData["b"]=car;gs.SessionData.OpponentKeyInFront="b";}
            Assert.IsFalse(call.IsValid(gs,now));
        }
        [Test]
        public void Native_interval_cannot_silently_turn_into_an_equal_distance_estimate_during_TTS()
        {
            var gs=State();gs.SessionData.LapTimePrevious=100;
            gs.PositionAndMotionData.DistanceRoundTrack=1000;gs.OpponentData["a"].DistanceRoundTrack=1150;
            var call=new ObservedRaceCall {Topic="ahead",Position=-1,SessionId=gs.SessionData.SessionId,DetectedUtc=T,ExpiresUtc=T.AddSeconds(6)};
            call.IncludeNeighbour(Context(gs,T),true,false);Assert.IsTrue(call.IsValid(gs,T));
            gs.SessionData.TimeDeltaFront=-1;
            Assert.AreEqual(3,RaceDataTools.ReadGap(gs,gs.OpponentData["a"],true).Seconds);
            Assert.IsFalse(call.IsValid(gs,T));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Queued_gap_is_rejected_when_same_named_raceroom_identity_changes(bool userChanges)
        {
            var gs=State();var op=gs.OpponentData["a"];op.r3eUserId=12;op.r3eSlotId=3;
            var call=new ObservedRaceCall {Topic="ahead",Position=-1,SessionId=gs.SessionData.SessionId,DetectedUtc=T,ExpiresUtc=T.AddSeconds(6)};
            call.IncludeNeighbour(Context(gs,T),true,false);Assert.IsTrue(call.IsValid(gs,T));
            if(userChanges)op.r3eUserId++;else op.r3eSlotId++;
            Assert.IsFalse(call.IsValid(gs,T));
        }
        [Test]
        public void Small_backward_position_jitter_never_crashes_passage_history_eviction()
        {
            var gs=State();gs.OpponentData.Clear();var gaps=new RaceGapTracker();
            float[] jitter={99.99f,100.01f,99.99f,100.01f};
            Assert.DoesNotThrow(()=> {
                for(int i=0;i<1300;i++) {
                    gs.PositionAndMotionData.DistanceRoundTrack=i<4?jitter[i]:100.01f+(i-3)*.1f;
                    gaps.Observe(gs,T.AddMilliseconds(i*100));
                }
            });
        }
        [Test]
        public void Driver_memory_owner_recovers_measured_facts_after_reopening()
        {
            string folder=Path.Combine(Environment.GetEnvironmentVariable("RCCA_DATA_ROOT")??Path.GetTempPath(),"fact-integration-"+Guid.NewGuid().ToString("N"));
            var context=new RaceFactContext("AMS2","Porsche","Kansai","GP/5000","GT3","Soft","dry");
            using(var memory=new DriverMemoryStore(folder)) {
                memory.BeginMeasuredSession(context,"Race",5);
                memory.RecordMeasuredLap(context,new LapRecord {LapNumber=1,Valid=true,LapTime=119.328f,
                    RecordedUtc=T,FuelUsed=3.2f,FuelAtEnd=40},"Race",5);
                Assert.IsTrue(memory.Facts.Flush(TimeSpan.FromSeconds(3)));
            }
            using(var reopened=new DriverMemoryStore(folder)) {
                var summary=reopened.Facts.Query(context,T.AddSeconds(1));
                Assert.AreEqual(1,summary.Laps.Count);Assert.AreEqual(119.328f,summary.BestObservedLapSeconds);
                Assert.IsFalse(reopened.Facts.PromptBlock(context,T.AddSeconds(1)).Contains("aktueller Tank"));
            }
        }
        [Test]
        public void Delivered_native_gap_report_postpones_only_the_matching_optional_routine()
        {
            var gs=State();var prefs=new AiRadioPreferences();foreach(var option in AiRadioPreferences.Options)prefs.Set(option.Id,0);
            prefs.Set("ahead",30);prefs.SetTolerance("ahead",0,0);var schedule=new AiRadioSchedule(1);
            schedule.Observe(gs,T,prefs);schedule.NoteRelatedReport("ahead",T.AddSeconds(29),prefs);
            gs.SourceTelemetryUtc=T.AddSeconds(31);
            Assert.IsNull(schedule.Take(Context(gs,T.AddSeconds(31)),prefs,false,m=>true,false));
            gs.SourceTelemetryUtc=T.AddSeconds(60);
            Assert.IsNotNull(schedule.Take(Context(gs,T.AddSeconds(60)),prefs,false,m=>true,false));
        }
        [TestCase("Runde 1:52,328.","Runde eine Minute zweiundfünfzig Komma drei zwei acht Sekunden.")]
        [TestCase("0,05 Sekunden, 26,5 PSI.","null Komma null fünf Sekunden, sechsundzwanzig Komma fünf PSI.")]
        [TestCase("102 Grad und 80 km/h.","einhundertzwei Grad und achtzig Kilometer pro Stunde.")]
        [TestCase("-0,5 Grad; 1,000 Liter.","minus null Komma fünf Grad; eins Komma null null null Liter.")]
        [TestCase("Porsche 992 GT3, Robert 17, P12.","Porsche 992 GT3, Robert 17, P12.")]
        public void German_numbers_keep_precision_without_rewriting_car_or_driver_identifiers(string input,string output)
        {Assert.AreEqual(output,GermanRadioText.Prepare(input));}

        [TestCase("Platz 7. Noch 0,8 Sekunden auf Müller.", "Platz sieben. Noch acht Zehntel auf Müller.")]
        [TestCase("Ungefähr 0,1 Sekunden. Porsche 992 GT3.", "Ungefähr ein Zehntel. Porsche 992 GT3.")]
        [TestCase("Noch 0,05 Sekunden.", "Noch null Komma null fünf Sekunden.")]
        public void Pit_wall_pronunciation_keeps_the_same_number_in_speakable_german(string input, string expected)
        {
            Assert.AreEqual(expected, GermanRadioText.Prepare(input, pitWall: true));
        }
    }
}
