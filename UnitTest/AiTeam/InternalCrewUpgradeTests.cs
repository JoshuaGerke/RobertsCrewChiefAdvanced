using System;
using System.IO;
using System.Linq;
using System.Reflection;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using CrewChiefV4.PMR;
using CrewChiefV4.rFactor2;
using rF2SharedMemory;
using CrewChiefV4.ACE;
using CrewChiefV4.ACE.aceData;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam"),NonParallelizable]
    public class InternalCrewUpgradeTests
    {
        private static readonly DateTime T=new DateTime(2026,11,10,10,0,0,DateTimeKind.Utc);
        private static string Folder()
        {
            string folder=Path.Combine(Environment.GetEnvironmentVariable("RCCA_DATA_ROOT")??Path.GetTempPath(),"internal-upgrade",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);return folder;
        }
        [Test]
        public void Qualifying_sectors_preference_is_migrated_persisted_and_reset()
        {
            string folder = Folder();
            File.WriteAllText(Path.Combine(folder, "funkwuensche.json"), "{\"schema\":1,\"routine\":2,\"gaps\":1}");
            var preferences = new DriverRadioPreferences(folder);
            Assert.IsTrue(preferences.QualifyingSectors);
            Assert.IsTrue(preferences.TryCommand("Merke dir: Sektorvergleiche im Quali aus.", out var answer));
            StringAssert.Contains("Quali aus", answer);
            preferences = new DriverRadioPreferences(folder);
            Assert.IsFalse(preferences.QualifyingSectors);
            Assert.AreEqual(2, preferences.IntervalScale("fuel"));
            Assert.IsFalse(preferences.TryCommand("Sektorvergleiche im Quali nicht aus", out _));
            Assert.IsTrue(preferences.TryCommand("Funkwünsche zurücksetzen", out _));
            Assert.IsTrue(new DriverRadioPreferences(folder).QualifyingSectors);
        }
        [Test]
        public void Repeated_source_tick_does_not_become_fresh_but_session_phase_and_vehicle_changes_do()
        {
            var clock=new TelemetrySourceClock();
            Assert.AreEqual(T.Ticks,clock.Observe(100,1,4,2,T.Ticks));
            Assert.AreEqual(T.Ticks,clock.Observe(100,1,4,2,T.AddSeconds(8).Ticks));
            Assert.AreEqual(T.AddSeconds(9).Ticks,clock.Observe(101,1,4,2,T.AddSeconds(9).Ticks));
            Assert.AreEqual(T.AddSeconds(10).Ticks,clock.Observe(0,2,4,2,T.AddSeconds(10).Ticks));
            Assert.AreEqual(T.AddSeconds(11).Ticks,clock.Observe(0,2,5,2,T.AddSeconds(11).Ticks));
            Assert.AreEqual(T.AddSeconds(12).Ticks,clock.Observe(0,2,5,3,T.AddSeconds(12).Ticks));
            Assert.AreEqual(0,clock.Observe(double.NaN,2,5,3,T.AddSeconds(13).Ticks));
            clock.Reset();Assert.AreEqual(T.AddSeconds(14).Ticks,clock.Observe(0,2,5,3,T.AddSeconds(14).Ticks));
        }
        [Test]
        public void Evo_graphics_and_physics_clocks_keep_their_own_age_across_pauses()
        {
            var reader = new ACESharedMemoryReader();
            var data = new ACEShared();
            data.aceGraphic.totalDrivers=1;
            data.aceGraphic.carIds=new[]{new carId {carIdA=7,carIdB=9}};
            data.aceGraphic.carCoordinates=new aceVec3[1];
            data.aceGraphic.playerCarIdA=7;data.aceGraphic.playerCarIdB=9;
            data.aceGraphic.status=ACEVO_STATUS.AC_LIVE;
            var frame = new ACESharedMemoryReader.ACEStructWrapper {data=data,ticksWhenRead=T.Ticks};
            reader.ObserveSourceProgress(frame, true);
            Assert.AreEqual(T.Ticks,frame.graphicsSampleTicks);
            Assert.AreEqual(T.Ticks,frame.physicsSampleTicks);
            data.aceGraphic.packetId++;
            frame = new ACESharedMemoryReader.ACEStructWrapper {data=data,ticksWhenRead=T.AddSeconds(1).Ticks};
            reader.ObserveSourceProgress(frame, true);
            Assert.AreEqual(T.AddSeconds(1).Ticks,frame.graphicsSampleTicks);
            Assert.AreEqual(T.Ticks,frame.physicsSampleTicks);
            data.aceGraphic.status=ACEVO_STATUS.AC_PAUSE;
            frame = new ACESharedMemoryReader.ACEStructWrapper {data=data,ticksWhenRead=T.AddSeconds(2).Ticks};
            reader.ObserveSourceProgress(frame, true);
            Assert.IsTrue(frame.nativeSourceClock);
            Assert.AreEqual(0,frame.graphicsSampleTicks);
            Assert.AreEqual(0,frame.physicsSampleTicks);
        }

        [Test]
        public void Evo_geometry_does_not_invent_driver_speed_class_or_rank_and_identity_is_unique()
        {
            var data=new ACEShared();
            data.aceGraphic.totalDrivers=3;
            data.aceGraphic.carIds=new[]{new carId {carIdA=4},new carId {carIdA=7,carIdB=9},new carId {carIdA=8}};
            data.aceGraphic.carCoordinates=new aceVec3[3];
            data.aceGraphic.playerCarIdA=7;data.aceGraphic.playerCarIdB=9;
            Assert.AreEqual(1,ACESharedMemoryReader.PlayerIndex(data));
            var car=ACESharedMemoryReader.GeometryVehicle(data,0);
            Assert.AreEqual(-1,car.speedMS);Assert.AreEqual(0,car.carLeaderboardPosition);
            Assert.AreEqual(-1,car.isCarInPitlane,"Unknown opponent pit state must not be inferred from standstill or geometry.");
            Assert.IsTrue(car.driverName.All(b=>b==0));Assert.IsTrue(car.carModel.All(b=>b==0));
            data.aceGraphic.carIds[2]=data.aceGraphic.carIds[1];Assert.AreEqual(-1,ACESharedMemoryReader.PlayerIndex(data));
            data.aceGraphic.totalDrivers=1;Assert.AreEqual(-1,ACESharedMemoryReader.PlayerIndex(data));
            data.aceGraphic.totalDrivers=uint.MaxValue;Assert.AreEqual(3,ACESharedMemoryReader.VehicleCount(data));
            data.aceGraphic.carCoordinates=null;Assert.AreEqual(0,ACESharedMemoryReader.VehicleCount(data));
        }
        [Test]
        public void Evo_own_best_and_overall_position_remain_but_unverified_field_facts_are_unknown()
        {
            var gs=new GameStateData(T.Ticks);
            gs.SessionData.OverallPosition=7;gs.SessionData.ClassPosition=1;
            gs.SessionData.PlayerLapTimeSessionBest=100;gs.SessionData.PlayerClassSessionBestLapTime=100;
            gs.OpponentData["fake"]=new OpponentData {DriverRawName="invented",Speed=50};
            CrewChiefV4.assetto.ACEGameStateMapper.ClearUnavailableOpponentTiming(gs);
            Assert.AreEqual(7,gs.SessionData.OverallPosition);Assert.AreEqual(100,gs.SessionData.PlayerLapTimeSessionBest);
            Assert.AreEqual(0,gs.SessionData.ClassPosition);Assert.AreEqual(-1,gs.SessionData.PlayerClassSessionBestLapTime);
            Assert.IsEmpty(gs.OpponentData);Assert.AreEqual(-1,gs.SessionData.TimeDeltaFront);
        }
        [Test]
        public void Lmu_never_uses_rf2_legacy_mappings_even_when_legacy_setting_is_enabled()
        {
            foreach(bool legacy in new[]{false,true})
            {
                var reader=new RF2_LMUSharedMemoryReader(SharedMemoryMode.LMU,legacy);
                var constants=typeof(RF2_LMUSharedMemoryReader).GetField("constants",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(reader);
                foreach(string name in new[]{"MM_TELEMETRY_FILE_NAME","MM_SCORING_FILE_NAME","MM_RULES_FILE_NAME","MM_EXTENDED_FILE_NAME"})
                {
                    string mapping=(string)constants.GetType().GetField(name).GetValue(constants);
                    StringAssert.Contains("LMU",mapping);
                }
            }
            var rf2=new RF2_LMUSharedMemoryReader(SharedMemoryMode.RF2,true);
            var rf2Constants=typeof(RF2_LMUSharedMemoryReader).GetField("constants",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(rf2);
            Assert.AreEqual(rFactor2Constants.MM_TELEMETRY_FILE_NAME_LEGACY,rf2Constants.GetType().GetField("MM_TELEMETRY_FILE_NAME").GetValue(rf2Constants));
        }
        [Test]
        public void Pmr_other_packets_cannot_refresh_an_old_player_channel()
        {
            long tick=0;var store=new DataStore(()=>tick,1000,()=>T.Ticks+tick*TimeSpan.TicksPerMillisecond);
            store.writeRaceState(new UDPParticipantRaceState {m_vehicleId=1,m_isPlayer=true});
            store.writeTelemetry(new UDPVehicleTelemetry {m_vehicleId=1});
            tick=2000;store.writeRaceState(new UDPParticipantRaceState {m_vehicleId=1,m_isPlayer=true});
            var snapshot=store.FreshSnapshot();
            Assert.That((snapshot.ticksWhenRead-snapshot.sourceSampleTicks)/(double)TimeSpan.TicksPerSecond,Is.EqualTo(2).Within(.001));
            Assert.AreEqual(T.Ticks, snapshot.playerTelemetrySampleTicks);
            Assert.AreEqual(T.Ticks, snapshot.opponentPositionTicks[1]);
            tick=2100;
            Assert.AreEqual(snapshot.sourceSampleTicks,store.FreshSnapshot().sourceSampleTicks);
            tick=6000;store.writeRaceState(new UDPParticipantRaceState {m_vehicleId=1,m_isPlayer=true});
            store.writeTelemetry(new UDPVehicleTelemetry {m_vehicleId=2});store.writeTimestamp();
            Assert.AreEqual(0,store.timeSinceLastWrite());
            Assert.IsNull(store.FreshSnapshot().PlayerTelemetry);
            Assert.IsNotNull(store.getTelemetryForVehicle(1),"The separate native spotter API has not changed.");
            store.writeTelemetry(new UDPVehicleTelemetry {m_vehicleId=1});Assert.IsNotNull(store.FreshSnapshot().PlayerTelemetry);
            tick=0;Assert.IsNull(store.FreshSnapshot().PlayerRaceState);
            store.clear();Assert.IsEmpty(store.FreshSnapshot().AllTelemetry);
        }
        [TestCase("0", 0)]
        [TestCase("65536", 0)]
        [TestCase("127.0.0.1:7578", 0)]
        [TestCase("7578", 7578)]
        public void Pmr_forwarded_input_is_explicit_and_uses_only_a_valid_local_port(string value, int expected) =>
            Assert.AreEqual(expected, PMRUDPReader.ParseLoopbackPort(value));

        [Test]
        public void Pmr_ambiguous_player_is_not_assigned_another_cars_telemetry()
        {
            var store=new DataStore(()=>0,1000);
            foreach(int id in new[]{1,2}) {
                store.writeRaceState(new UDPParticipantRaceState {m_vehicleId=id,m_isPlayer=true});
                store.writeTelemetry(new UDPVehicleTelemetry {m_vehicleId=id});
            }
            Assert.IsNull(store.FreshSnapshot().PlayerRaceState);Assert.IsNull(store.FreshSnapshot().PlayerTelemetry);
        }
        [Test]
        public void A_fresh_player_frame_does_not_refresh_a_stale_native_opponent_position()
        {
            var state = new GameStateData(T.Ticks) { SourceTelemetryUtc=T };
            state.SessionData.TrackDefinition = new CrewChiefV4.TrackDefinition("Test", 3000);
            var car = new OpponentData { DriverRawName="Lara", Speed=0, IsActive=true, DistanceRoundTrack=120, NativePositionUtc=T.AddSeconds(-2) };
            state.OpponentData["rival"] = car;
            var tracks = new TrackAwareness();
            Assert.IsTrue(tracks.Observe(state, T, true));
            Assert.IsFalse(tracks.Snapshot.Cars.Single().SpatiallyConsistent);
            state.SourceTelemetryUtc = T.AddMilliseconds(40);
            car.NativePositionUtc = state.SourceTelemetryUtc;
            Assert.IsTrue(tracks.Observe(state, state.SourceTelemetryUtc, true));
            Assert.IsTrue(tracks.Snapshot.Cars.Single().SpatiallyConsistent);
        }

        [Test]
        public void Stale_and_future_source_frames_clear_spatial_evidence()
        {
            var gs=new GameStateData(T.Ticks) { SourceTelemetryUtc=T };
            var tracks=new TrackAwareness();Assert.IsTrue(tracks.Observe(gs,T));
            Assert.IsFalse(tracks.Observe(gs,T.AddSeconds(2)));Assert.IsNull(tracks.Snapshot);
            gs.SourceTelemetryUtc=T.AddSeconds(5);Assert.IsFalse(tracks.Observe(gs,T));
            gs.SourceTelemetryUtc=T.AddSeconds(6);Assert.IsTrue(tracks.Observe(gs,T.AddSeconds(6)));
        }
        [Test]
        public void Explicit_radio_wishes_survive_restart_and_can_be_queried_and_revoked()
        {
            string folder=Folder(), reply;
            var prefs=new DriverRadioPreferences(folder);
            Assert.IsTrue(prefs.TryCommand("Merke dir: Routinefunk seltener.",out reply));
            Assert.AreEqual(2,prefs.IntervalScale("fuel"));
            var reopened=new DriverRadioPreferences(folder);Assert.AreEqual(2,reopened.IntervalScale("fuel"));
            Assert.IsTrue(reopened.TryCommand("Abstandsmeldungen häufiger",out reply));
            Assert.AreEqual(1.5,reopened.IntervalScale("ahead"));Assert.AreEqual(2,reopened.IntervalScale("fuel"));
            Assert.IsTrue(reopened.TryCommand("Welche Funkwünsche sind gespeichert?",out reply));StringAssert.Contains("häufiger",reply);
            Assert.IsTrue(reopened.TryCommand("Funkwünsche zurücksetzen",out reply));
            Assert.AreEqual(1,new DriverRadioPreferences(folder).IntervalScale("ahead"));
            Assert.AreEqual(1,Directory.GetFiles(folder,"*.bak").Length);
        }
        [TestCase("Bitte keine Gefahren mehr melden")]
        [TestCase("Melde mir Abstände nicht häufiger")]
        [TestCase("Ich möchte Abstandsberichte seltener hören, wenn ich führe")]
        [TestCase("Melde mir Unfälle seltener")]
        [TestCase("Routinefunk seltener aber nur im Training")]
        [TestCase("Ich möchte wissen warum du Routinefunk seltener meldest")]
        [TestCase("Abstandsmeldungen nicht häufiger")]
        public void Ambiguous_or_safety_wishes_are_not_silently_applied(string text)
        {
            var prefs=new DriverRadioPreferences(Folder());string reply;
            Assert.IsFalse(prefs.TryCommand(text,out reply));Assert.AreEqual(1,prefs.IntervalScale("ahead"));
        }

        [TestCase("Bitte melde mir Abstände seltener.",2)]
        [TestCase("Melde Abstandsberichte bitte häufiger",0.75)]
        [TestCase("Ich möchte Abstandsberichte seltener hören",2)]
        public void Natural_explicit_gap_wishes_persist_on_first_use_without_affecting_other_topics(string command,double scale)
        {
            string folder=Path.Combine(Folder(),"new-driver"),reply;
            var prefs=new DriverRadioPreferences(folder);
            Assert.IsTrue(prefs.TryCommand(command,out reply));StringAssert.Contains("Gespeichert",reply);
            var loaded=new DriverRadioPreferences(folder);
            Assert.AreEqual(scale,loaded.IntervalScale("ahead"));Assert.AreEqual(scale,loaded.IntervalScale("behind"));
            Assert.AreEqual(1,loaded.IntervalScale("fuel"));
            Assert.IsTrue(loaded.TryCommand("Funkwünsche zurücksetzen",out reply));
            Assert.AreEqual(1,new DriverRadioPreferences(folder).IntervalScale("ahead"));
        }
        [Test]
        public void Corrupt_preferences_are_preserved_and_do_not_change_frequency()
        {
            string folder=Folder(), path=Path.Combine(folder,"funkwuensche.json"), reply;
            File.WriteAllText(path,"{broken");var prefs=new DriverRadioPreferences(folder);
            Assert.IsTrue(prefs.TryCommand("Routinefunk seltener",out reply));
            Assert.AreEqual(1,prefs.IntervalScale("fuel"));Assert.AreEqual("{broken",File.ReadAllText(path));
            StringAssert.DoesNotContain("Gespeichert",reply);
        }
        [Test]
        public void Routine_commands_route_to_pit_wall_after_sara_but_explicit_address_is_preserved()
        {
            Assert.AreEqual(TeamMember.Ben,TeamRouter.Route("Routinefunk seltener",TeamMember.Sara).Member);
            Assert.AreEqual(TeamMember.Mik,TeamRouter.Route("Mik, Routinefunk seltener",TeamMember.Sara).Member);
            Assert.AreEqual(TeamMember.Sara,TeamRouter.Route("Sara, links frei?",TeamMember.Ben).Member);
        }

        [Test]
        public void Explicit_reset_recovers_corrupt_wishes_without_losing_the_original()
        {
            string folder=Folder(),path=Path.Combine(folder,"funkwuensche.json"),reply;
            File.WriteAllText(path,"{broken");var prefs=new DriverRadioPreferences(folder);
            Assert.IsTrue(prefs.TryCommand("Funkwünsche zurücksetzen",out reply));
            StringAssert.Contains("Gespeichert",reply);
            var backups=Directory.GetFiles(folder,"*.corrupt.*.bak");Assert.AreEqual(1,backups.Length);
            Assert.AreEqual("{broken",File.ReadAllText(backups[0]));
            var reopened=new DriverRadioPreferences(folder);
            Assert.IsTrue(reopened.TryCommand("Routinefunk seltener",out reply));
            Assert.AreEqual(2,new DriverRadioPreferences(folder).IntervalScale("fuel"));
        }
        [TestCase("{\"schema\":1,\"routine\":1.5,\"gaps\":1}")]
        [TestCase("{\"schema\":1,\"routine\":\"2\",\"gaps\":1}")]
        [TestCase("{\"schema\":1,\"gaps\":1}")]
        public void Invalid_preference_types_do_not_change_frequency(string json)
        {
            string folder=Folder(), reply;
            File.WriteAllText(Path.Combine(folder,"funkwuensche.json"),json);
            var prefs=new DriverRadioPreferences(folder);
            Assert.AreEqual(1,prefs.IntervalScale("fuel"));
            prefs.TryCommand("Routinefunk seltener",out reply);
            StringAssert.DoesNotContain("Gespeichert",reply);
        }
        [Test]
        public void Optional_interval_scaling_preserves_disabled_topics_and_limits_frequency()
        {
            Assert.AreEqual(0,AiRadioPreferences.ApplyDriverScale("fuel",0,2));
            Assert.AreEqual(240,AiRadioPreferences.ApplyDriverScale("fuel",120,2));
            Assert.AreEqual(30,AiRadioPreferences.ApplyDriverScale("fuel",30,.5625));
            Assert.AreEqual(3600,AiRadioPreferences.ApplyDriverScale("fuel",3600,4));
        }
        [Test]
        public void Audio_completion_requires_start_and_late_duplicate_cannot_rewrite_result()
        {
            var call=new ObservedRaceCall {DetectedUtc=T,ExpiresUtc=T.AddSeconds(8)};
            call.MarkPlaybackFinished(T,false);Assert.AreEqual(DateTime.MinValue,call.PlaybackEndedUtc);
            Assert.IsTrue(call.TryReserve(T));call.MarkPlaybackStarted(T.AddSeconds(1));
            call.MarkPlaybackFinished(T.AddSeconds(2),true);call.MarkPlaybackFinished(T.AddSeconds(3),false);
            Assert.IsTrue(call.PlaybackInterrupted);Assert.AreEqual(T.AddSeconds(2),call.PlaybackEndedUtc);
        }
    }
}
