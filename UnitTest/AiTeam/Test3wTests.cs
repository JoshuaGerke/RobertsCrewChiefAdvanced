using System;
using System.IO;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using CrewChiefV4.RaceRoom;
using CrewChiefV4.RaceRoom.RaceRoomData;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class Test3wTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 15, 13, 0, 0, DateTimeKind.Utc);
        private static GameStateData State(SessionType type, DateTime utc)
        {
            var gs = new GameStateData(utc.Ticks) { SourceTelemetryUtc = utc, carClass = new CarData.CarClass() };
            gs.SessionData.SessionId = 12;
            gs.SessionData.SessionType = type;
            gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = 4;
            gs.SessionData.CurrentLapIsValid = true;
            gs.SessionData.TrackDefinition = new TrackDefinition("Monza", 5778);
            gs.PositionAndMotionData.CarSpeed = 50;
            return gs;
        }

        [Test]
        public void Routine_sampling_limits_repeated_frames_but_preserves_boundaries()
        {
            var director = new RaceRadioDirector();
            var previous = State(SessionType.Race, T);
            var current = State(SessionType.Race, T.AddMilliseconds(20));

            Assert.IsTrue(director.RoutineDue(T, null, previous));
            Assert.IsFalse(director.RoutineDue(T.AddMilliseconds(20), previous, current));
            Assert.IsTrue(director.RoutineDue(T.AddMilliseconds(125), previous, current));
            current.SessionData.SectorNumber = previous.SessionData.SectorNumber + 1;
            Assert.IsTrue(director.RoutineDue(T.AddMilliseconds(130), previous, current));
            Assert.IsFalse(director.RoutineDue(T.AddMilliseconds(135), current, current));
            current.SessionData.SessionPhase = SessionPhase.FullCourseYellow;
            Assert.IsTrue(director.RoutineDue(T.AddMilliseconds(140), previous, current));
        }

        [Test]
        public void Reachable_telemetry_source_clears_on_disconnect_and_accepts_a_new_sim_immediately()
        {
            try
            {
                GameDataConnectionStatus.Record("RaceRoom", T);
                GameDataConnectionStatus.Record("AMS2", T.AddMilliseconds(20));
                Assert.AreEqual("AMS2", GameDataConnectionStatus.SourceAt(T.AddMilliseconds(20)));
                GameDataConnectionStatus.Clear();
                Assert.IsNull(GameDataConnectionStatus.SourceAt(T.AddMilliseconds(21)));
            }
            finally
            {
                GameDataConnectionStatus.Clear();
            }
        }

        [Test]
        public void Lap_result_can_render_beside_routine_but_never_blocks_Sara_or_interrupts_a_driver_reply()
        {
            var gate = new RadioRenderGate();
            Assert.IsTrue(gate.TryEnter(false));
            Assert.IsTrue(gate.TryEnterResult());
            Assert.IsTrue(gate.TryEnter(true));
            Assert.IsTrue(CrewSpeechPolicy.CanInterruptForResult(true, false, false, 2));
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForResult(true, true, false, 2));
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForResult(true, false, true, 2));
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForResult(true, false, false, 5));
        }

        [Test]
        public void Qualifying_expiry_is_checkered_and_waits_for_our_crossing_instead_of_declaring_the_rank_final()
        {
            var gs = State(SessionType.Qualify, T);
            gs.SessionData.SessionHasFixedTime = true;
            gs.SessionData.SessionTimeRemaining = .11f;
            gs.SessionData.CompletedLaps = 2;
            int green = (int)RaceRoomConstant.SessionPhase.Green;
            int checkered = (int)RaceRoomConstant.SessionPhase.Checkered;
            Assert.AreEqual(checkered, R3EGameStateMapper.TimedSessionPhase(SessionType.Qualify, gs.SessionData, green, 0));
            Assert.AreEqual(green, R3EGameStateMapper.TimedSessionPhase(SessionType.Qualify, gs.SessionData, green, 12));
            Assert.AreEqual(green, R3EGameStateMapper.TimedSessionPhase(SessionType.Race, gs.SessionData, green, 0));

            var timedLaps = new SessionLapObserver();
            timedLaps.Observe(gs, null, T, true);
            var results = new QualifyingResultWatch();
            var flagged = State(SessionType.Qualify, T.AddMilliseconds(100));
            flagged.SessionData.SessionPhase = SessionPhase.Checkered;
            flagged.SessionData.CompletedLaps = 2;
            timedLaps.Observe(flagged, null, T.AddMilliseconds(100), true, gs, driverName: "Rob");
            var call = timedLaps.Take(flagged, T.AddMilliseconds(100), false, ownLapOnly: true);
            Assert.AreEqual("qualifying-last", call.Topic);
            Assert.AreEqual(TeamMember.Ben, call.Member);
            results.Observe(flagged, T.AddMilliseconds(100), "Rob", gs);
            Assert.IsNull(results.Take(flagged, T.AddMilliseconds(100), false, true));
            flagged.SessionData.CompletedLaps = 3;
            flagged.SessionData.ClassPosition = 1;
            flagged.SessionData.SessionPhase = SessionPhase.Finished;
            Assert.IsFalse(call.IsValid(flagged, T.AddMilliseconds(150)));
            results.Observe(flagged, T.AddMilliseconds(150), "Rob");
            StringAssert.Contains("Platz 1", results.Take(flagged, T.AddMilliseconds(150), false, true).Text);
        }

        [TestCase(1, false, true)]
        [TestCase(0, true, false)]
        [TestCase(-1, true, true)]
        public void RaceRoom_uses_the_completed_laps_flag_before_the_next_laps_flag(int nativePrevious, bool next, bool expected)
        {
            var shared = new RaceRoomShared { VersionMajor = 3, VersionMinor = 6,
                CompletedLaps = 4, LapTimePreviousSelf = 111.45193f, PrevLapValid = nativePrevious };
            Assert.AreEqual(expected, R3EGameStateMapper.PreviousLapValidity(shared, 3, next, false));
            Assert.IsFalse(R3EGameStateMapper.PreviousLapValidity(shared, 3, next, true));
        }

        [Test]
        public void Relative_time_comes_from_two_passages_and_is_cleared_after_an_identity_jump()
        {
            var tracks = new TrackAwareness();
            GameStateData gs = null;
            for (int i = 0; i <= 25; i++)
            {
                var now = T.AddMilliseconds(i * 200);
                gs = State(SessionType.Qualify, now);
                gs.PositionAndMotionData.DistanceRoundTrack = 100 + i * 10;
                gs.OpponentData["feller"] = new OpponentData { DriverRawName = "Ricardo Feller", CarNumber = "1",
                    CarClass = gs.carClass, Speed = 50, DistanceRoundTrack = i * 10 };
                Assert.IsTrue(tracks.Observe(gs, now));
            }
            var car = tracks.Snapshot.Cars[0];
            Assert.AreEqual(-100, car.RouteGapM.Value, .01);
            Assert.AreEqual(2, car.TrackIntervalSeconds.Value, .03);
            var ctx = new RaceDataContext { GameState = gs, Tracks = tracks, NowUtc = gs.SourceTelemetryUtc,
                LastTelemetryUtc = gs.SourceTelemetryUtc, SendDriverNames = true };
            StringAssert.Contains("ungefähr 2,0 Sekunden", LocalAnswers.TrackNeighbour("Wie weit ist der Hintermann?", ctx));
            StringAssert.Contains("100 Meter", LocalAnswers.TrackNeighbour("Wie weit ist der Hintermann in Metern?", ctx));
            gs = State(SessionType.Qualify, T.AddMilliseconds(5200));
            gs.PositionAndMotionData.DistanceRoundTrack = 4000;
            gs.OpponentData["feller"] = new OpponentData { DriverRawName = "Ricardo Feller",
                CarClass = gs.carClass, Speed = 50, DistanceRoundTrack = 3900 };
            Assert.IsTrue(tracks.Observe(gs, gs.SourceTelemetryUtc));
            Assert.IsNull(tracks.Snapshot.Cars[0].TrackIntervalSeconds);
        }

        [Test]
        public void Rear_traffic_preference_migrates_and_survives_restart()
        {
            string folder = Path.Combine(Environment.GetEnvironmentVariable("RCCA_DATA_ROOT"), "traffic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "funkwuensche.json"), "{\"schema\":2,\"routine\":0,\"gaps\":2,\"qualifying_sectors\":false}");
                var prefs = new DriverRadioPreferences(folder);
                Assert.AreEqual(3, prefs.RearTrafficSeconds);
                Assert.IsTrue(prefs.TryCommand("Schneller Verkehr von hinten erst bei zwei Sekunden", out string reply));
                StringAssert.Contains("2 Sekunden", reply);
                var reopened = new DriverRadioPreferences(folder);
                Assert.AreEqual(2, reopened.RearTrafficSeconds);
                Assert.IsFalse(reopened.QualifyingSectors);
                Assert.AreEqual(1.5, reopened.IntervalScale("behind"), .01);
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void Monza_fuel_arithmetic_is_local_and_first_full_lap_has_own_radio()
        {
            var before = State(SessionType.Race, T);
            before.SessionData.IsNewLap = true;
            var line = State(SessionType.Race, T.AddSeconds(125));
            line.SessionData.IsNewLap = true;
            line.SessionData.CompletedLaps = 1;
            line.SessionData.PreviousLapWasValid = false;
            line.SessionData.LapTimePrevious = 124.620f;
            before.FuelData.FuelUseActive = line.FuelData.FuelUseActive = true;
            before.FuelData.FuelLeft = 15.7f;
            line.FuelData.FuelLeft = 12.4f;
            var memory = new AiRaceMemory();
            memory.Update(null, before, T);
            memory.Update(before, line, line.SourceTelemetryUtc);
            var ctx = new RaceDataContext { GameState = line, Memory = memory, NowUtc = line.SourceTelemetryUtc,
                LastTelemetryUtc = line.SourceTelemetryUtc };
            StringAssert.Contains("2,5 Liter", LocalAnswers.FuelToFinish("Reicht der Sprit für 3 Runden?", ctx));
            Assert.IsNull(LocalAnswers.FuelToFinish("Merke dir, der Sprit reicht für 3 Runden", ctx));
            var observer = new SessionLapObserver();
            observer.Observe(before, null, T, true);
            observer.Observe(line, memory.GetLaps()[0], line.SourceTelemetryUtc, true, before);
            var fuel = observer.Take(line, line.SourceTelemetryUtc, false, ownLapOnly: true);
            Assert.AreEqual("first-lap-fuel", fuel.Topic);
            StringAssert.Contains("3,3 Liter", fuel.Text);
        }

        [Test]
        public void Routine_facts_ignore_noise_but_preserve_material_deltas_and_cooldown()
        {
            var memory = new StintRadioMemory();
            StintRadioMemory.Report Sample(double delta, int lap, string identity = "same") =>
                new StintRadioMemory.Report { Identity = identity, Lap = lap, Values = new[] { delta }, Thresholds = new[] { .2 } };
            memory.Remember("pace", Sample(90, 5), T);
            Assert.IsTrue(memory.Unchanged("pace", Sample(90.05, 6), T.AddSeconds(20)));
            Assert.IsFalse(memory.Unchanged("pace", Sample(90.4, 6), T.AddSeconds(20)));
            Assert.IsFalse(memory.Unchanged("pace", Sample(90, 8), T.AddSeconds(20)));
            Assert.IsFalse(memory.Unchanged("pace", Sample(90, 6, "new"), T.AddSeconds(20)));
            StringAssert.Contains("schneller", memory.Trend("pace", Sample(89.6, 6), T.AddSeconds(20)));
            memory.Clear();
            Assert.IsNull(memory.Trend("pace", Sample(89.6, 6), T.AddSeconds(20)));
        }

        [Test]
        public void A_short_pool_avoids_a_repeat_after_another_group_plays()
        {
            var cursor = new RadioVariantCursor();
            int first = cursor.Select("a", 2);
            cursor.OnPlaybackCompleted("a", first);
            int other = cursor.Select("b", 2);
            cursor.OnPlaybackCompleted("b", other);
            Assert.AreNotEqual(first, cursor.Select("a", 2));
            Assert.AreEqual(0, cursor.Select("single", 1));
        }

        [Test]
        public void A_tenth_of_drift_does_not_invalidate_relative_reply_but_a_new_opponent_or_stale_gap_does()
        {
            var original = ("Feller ist ungefähr 3,4 Sekunden hinter dir.", "feller");
            Assert.IsTrue(LocalAnswers.NeighbourReplyStillCurrent(original, ("Feller ist ungefähr 3,3 Sekunden hinter dir.", "feller")));
            Assert.IsFalse(LocalAnswers.NeighbourReplyStillCurrent(original, ("Feller ist ungefähr 2,1 Sekunden hinter dir.", "feller")));
            Assert.IsFalse(LocalAnswers.NeighbourReplyStillCurrent(original, ("Feller ist ungefähr 3,3 Sekunden hinter dir.", "other")));
        }
    }
}
