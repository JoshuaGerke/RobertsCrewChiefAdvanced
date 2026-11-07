using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class RaceReaderPipelineTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc);
        private static GameStateData State()
        {
            var gs = new GameStateData(T.Ticks);
            gs.SourceTelemetryUtc = T;
            gs.SessionData.SessionId = 9;
            gs.SessionData.SessionType = SessionType.Race;
            gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.SessionRunningTime = 330;
            gs.SessionData.CurrentLapIsValid = true;
            gs.SessionData.PreviousLapWasValid = true;
            gs.SessionData.ClassPosition = 3;
            gs.SessionData.OverallPosition = 5;
            gs.SessionData.TrackDefinition = new TrackDefinition("Kansai", 5000);
            gs.PositionAndMotionData.CarSpeed = 50;
            gs.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            return gs;
        }

        private static RaceDataContext Context(GameStateData gs, AiRaceMemory memory, DateTime now) =>
            new RaceDataContext { GameState = gs, Memory = memory, NowUtc = now, LastTelemetryUtc = gs.SourceTelemetryUtc };

        private static void Advance(GameStateData gs, double seconds)
        {
            gs.Now = gs.SourceTelemetryUtc = T.AddSeconds(seconds);
            gs.SessionData.SessionRunningTime = 330 + (float)seconds;
        }

        [Test]
        public void Raw_samples_are_bounded_at_100Hz_and_paused_ticks_do_not_inflate_frequency()
        {
            var gs = State();
            var workload = new DriverWorkload();
            for (int i = 0; i < 2500; i++)
            {
                Advance(gs, i * .005);
                workload.Observe(gs, gs.Now, false, null, null, null);
            }
            Assert.AreEqual(DriverWorkload.Capacity, workload.Count);
            Assert.AreEqual(100, (double)workload.Describe(gs.Now)["gemessene_abtastrate_hz"], .001);
            int before = workload.Count;
            gs.SourceTelemetryUtc = gs.Now.AddMilliseconds(25);
            workload.Observe(gs, gs.SourceTelemetryUtc, false, null, null, null);
            gs.SourceTelemetryUtc = gs.SourceTelemetryUtc.AddMilliseconds(25);
            Assert.IsFalse(workload.Observe(gs, gs.SourceTelemetryUtc, false, null, null, null));
            Assert.AreEqual(before, workload.Count);
            Assert.IsFalse(workload.AllowsRoutine(gs.SourceTelemetryUtc.AddSeconds(2)));
        }

        [Test]
        public void Steering_units_are_explicit_and_calm_requires_hysteresis()
        {
            var gs = State();
            var workload = new DriverWorkload();
            gs.ControlData.SteeringWheelAngle = 180;
            workload.Observe(gs, T, false, null, null, null);
            Assert.AreEqual(DriverWorkloadMode.Management, workload.Mode);

            Advance(gs, .1);
            gs.ControlData.SteeringWheelAngle = .3f;
            workload.Observe(gs, gs.Now, false, null, null, null, SteeringMeasurement.Normalized);
            Assert.AreEqual(DriverWorkloadMode.Corner, workload.Mode);
            Assert.IsFalse(workload.AllowsRoutine(gs.Now));

            Advance(gs, .2);
            gs.ControlData.SteeringWheelAngle = 0;
            workload.Observe(gs, gs.Now, false, null, null, null, SteeringMeasurement.Normalized);
            Assert.AreEqual(DriverWorkloadMode.Management, workload.Mode);
            Assert.IsFalse(workload.AllowsRoutine(gs.Now));
            Advance(gs, 1);
            workload.Observe(gs, gs.Now, false, null, null, null);
            Assert.IsTrue(workload.AllowsRoutine(gs.Now));
        }

        [Test]
        public void Close_traffic_allows_short_tactical_calls_only_when_controls_are_calm()
        {
            var gs = State();
            var workload = new DriverWorkload();
            var gap = new RaceGapMeasurement(.6f, RaceGapKind.SimulatorTiming, T);
            workload.Observe(gs, T, false, null, gap, null);
            Assert.AreEqual(DriverWorkloadMode.Battle, workload.Mode);
            Assert.IsFalse(workload.AllowsRoutine(T));
            Assert.IsTrue(workload.AllowsReport(T, true));
            Advance(gs, .1);
            gs.ControlData.BrakePedal = .5f;
            workload.Observe(gs, gs.Now, false, null, gap, null);
            Assert.IsFalse(workload.AllowsReport(gs.Now, true));
            Advance(gs, .2);
            gs.ControlData.BrakePedal = 0;
            workload.Observe(gs, gs.Now, false, null, gap, null);
            Assert.IsFalse(workload.AllowsReport(gs.Now, true));
            Advance(gs, .6);
            workload.Observe(gs, gs.Now, false, null, gap, null);
            Assert.IsTrue(workload.AllowsReport(gs.Now, true));
            Assert.IsFalse(workload.AllowsReport(gs.Now, false));
        }

        [Test]
        public void Overlap_is_battle_and_offtrack_is_crisis_even_without_class_gaps()
        {
            var gs = State();
            var workload = new DriverWorkload();
            workload.Observe(gs, T, true, null, null, null);
            Assert.AreEqual(DriverWorkloadMode.Battle, workload.Mode);
            Advance(gs, .1);
            gs.PenaltiesData.IsOffRacingSurface = true;
            workload.Observe(gs, gs.Now, false, null, null, null);
            Assert.AreEqual(DriverWorkloadMode.Crisis, workload.Mode);
        }

        [Test]
        public void Reader_publishes_2Hz_immutable_private_deltas_and_rejects_stale_or_different_track()
        {
            var gs = State();
            var reader = new RaceReader();
            var context = Context(gs, new AiRaceMemory(), T);
            Assert.IsTrue(reader.Observe(context, null));
            Assert.AreEqual(1, (int)reader.Read(context)["revision"]);
            var copy = reader.Read(context);
            copy["phase"] = "tampered";
            Assert.AreEqual("Green", (string)reader.Read(context)["phase"]);

            Advance(gs, .1);
            context.NowUtc = gs.Now;
            context.LastTelemetryUtc = gs.Now;
            Assert.IsFalse(reader.Observe(context, null));
            Assert.AreEqual(1, (int)reader.Read(context, 1)["revision"]);
            Assert.IsFalse((bool)reader.Read(context, 1)["vollstaendiger_snapshot"]);

            Advance(gs, .5);
            context.NowUtc = context.LastTelemetryUtc = gs.Now;
            Assert.IsTrue(reader.Observe(context, null));
            Assert.AreEqual(2, (int)reader.Read(context, 1)["revision"]);
            StringAssert.DoesNotContain("eigene_letzte_saubere_runden_s", reader.Read(context, 1).ToString());

            context.NowUtc = T.AddSeconds(2);
            Assert.IsFalse((bool)reader.Read(context)["verfuegbar"]);
            context.NowUtc = gs.Now;
            gs.SessionData.TrackDefinition = new TrackDefinition("Other", 5000);
            Assert.IsFalse((bool)reader.Read(context)["verfuegbar"]);
        }

        private static AiRaceMemory OwnLaps(GameStateData gs, int count, float time = 90)
        {
            var memory = new AiRaceMemory();
            for (int i = 1; i <= count; i++)
            {
                Advance(gs, i * .1);
                gs.SessionData.IsNewLap = true;
                gs.SessionData.CompletedLaps = i;
                gs.SessionData.LapTimePrevious = time;
                gs.SessionData.PlayerLapData.Add(new LapData { LapNumber = i, LapTime = time,
                    SectorTimes = new[] { time / 3, time / 3, time / 3 } });
                memory.Update(null, gs, gs.Now);
            }
            gs.SessionData.IsNewLap = false;
            return memory;
        }

        [Test]
        public void Pace_calls_require_three_clean_laps_to_the_same_identified_rival_and_name_privacy()
        {
            var gs = State();
            gs.SessionData.OpponentKeyInFront = "Driver Name";
            var op = new OpponentData { DriverRawName = "Driver Name", CarNumber = "18", CostId = 77,
                ClassPosition = 2, OverallPosition = 2, CarClass = gs.carClass, IsActive = true };
            gs.OpponentData["Driver Name"] = op;
            for (int i = 1; i <= 3; i++) op.OpponentLapData.Add(new LapData { LapNumber = i,
                LapTime = 91, GameTimeAtLapStart = i * 90 - 80, SectorTimes = new[] { 31f, 30f, 30f } });
            var memory = OwnLaps(gs, 3);
            var reader = new RaceReader();
            var context = Context(gs, memory, gs.Now);
            context.SendDriverNames = false;
            Assert.IsTrue(reader.Observe(context, null));
            var result = reader.Read(context).ToString();
            StringAssert.DoesNotContain("Driver Name", result);
            Assert.AreEqual(1, (double)reader.Read(context)["klassengegner_vorne"]["eigener_tempovorteil_s_pro_runde"], .001);
            var call = reader.Take(gs, gs.Now, false);
            Assert.IsNotNull(call);
            StringAssert.DoesNotContain("Driver Name", call.Text);
            StringAssert.Contains("1,0", call.Text);
            op.CarNumber = "99";
            Assert.IsFalse(call.IsValid(gs, gs.Now));
        }

        [Test]
        public void No_pace_advice_after_pit_invalid_lap_or_rival_swap()
        {
            var gs = State();
            var laps = Enumerable.Range(1, 5).Select(i => new LapRecord { LapNumber = i, LapTime = 90,
                Valid = true, TyreWear = new[] { (float)i, (float)i, (float)i, (float)i } }).ToArray();
            laps[3].Valid = false;
            Assert.AreEqual(1, RaceReader.RecentOwn(laps).Length);
            laps[3].Valid = true;
            laps[3].InPitLap = true;
            Assert.AreEqual(1, RaceReader.RecentOwn(laps).Length);
        }

        [Test]
        public void Wear_regression_never_infers_a_grip_cliff_or_fits_across_new_tyres()
        {
            var laps = Enumerable.Range(1, 5).Select(i => new LapRecord { LapNumber = i, LapTime = 90,
                Valid = true, TyreWear = new[] { (float)i, (float)i, (float)i, (float)i } }).ToArray();
            var regression = RaceReader.WearRegression(laps);
            Assert.AreEqual(1, (double)regression["prozentpunkte_pro_runde_vl_vr_hl_hr"][0], .001);
            Assert.IsFalse((bool)regression["gripklippe_berechenbar"]);
            laps[4].TyreWear[0] = 0;
            Assert.IsNull(RaceReader.WearRegression(laps));
        }

        [Test]
        public void Prepared_routine_has_one_slot_and_leaves_rendering_and_urgent_lane_available()
        {
            var gate = new RadioRenderGate();
            Assert.IsTrue(gate.TryHoldRoutine());
            Assert.IsFalse(gate.TryHoldRoutine());
            Assert.IsFalse(gate.Busy);
            Assert.IsTrue(gate.TryEnter(false));
            Assert.IsTrue(gate.TryEnter(true));
            gate.Exit(false);
            gate.Exit(true);
            gate.ReleaseRoutine();
            Assert.IsTrue(gate.TryHoldRoutine());
            gate.ReleaseRoutine();
        }

        [Test]
        public void Wet_tyre_observation_requires_a_real_change_and_two_postchange_sector_comparisons()
        {
            var gs = State();
            gs.TyreData.FrontLeftTyreType = gs.TyreData.FrontRightTyreType =
                gs.TyreData.RearLeftTyreType = gs.TyreData.RearRightTyreType = TyreType.Soft;
            gs.SessionData.OpponentKeyInFront = "Muller";
            var rival = new OpponentData { DriverRawName = "Muller", CarNumber = "18", CarClass = gs.carClass,
                ClassPosition = 2, OverallPosition = 2, IsActive = true, CurrentTyres = TyreType.Soft };
            gs.OpponentData["Muller"] = rival;
            for (int i = 1; i <= 3; i++)
                rival.OpponentLapData.Add(new LapData { LapNumber = i, LapTime = 90,
                    GameTimeAtLapStart = i * 90 - 80, SectorTimes = new[] { 30f, 30f, 30f } });
            var memory = OwnLaps(gs, 3);
            var reader = new RaceReader();
            var context = Context(gs, memory, gs.Now);
            reader.Observe(context, null);
            for (int second = 1; second <= 190; second++)
            {
                Advance(gs, second);
                context.NowUtc = context.LastTelemetryUtc = gs.Now;
                gs.Conditions.UpdateLiveSample(gs.Now, 3, 1, 20, 25, .1f, 0, 0, 0, .5f, false,
                    CrewChiefV4.Events.ConditionsMonitor.TrackStatus.WET,
                    CrewChiefV4.Events.ConditionsMonitor.TrackWetness.ModeratelyWet);
                rival.CurrentTyres = TyreType.Intermediate;
                if (second == 100 || second == 190)
                    rival.OpponentLapData.Add(new LapData { LapNumber = second == 100 ? 4 : 5,
                        LapTime = 88, GameTimeAtLapStart = second == 100 ? 332 : 422,
                        SectorTimes = new[] { 28f, 30f, 30f } });
                reader.Observe(context, null);
                if (second < 190) Assert.IsNull(reader.Take(gs, gs.Now, false));
            }
            var call = reader.Take(gs, gs.Now, false);
            Assert.IsNotNull(call);
            Assert.AreEqual("reader-wet-compound", call.Topic);
            Assert.AreEqual(TeamMember.Ben, call.Member);
            StringAssert.Contains("Intermediates", call.Text);
            gs.TyreData.FrontLeftTyreType = gs.TyreData.FrontRightTyreType =
                gs.TyreData.RearLeftTyreType = gs.TyreData.RearRightTyreType = TyreType.Wet;
            Assert.IsFalse(call.IsValid(gs, gs.Now));
        }


        [TestCase("{\"after_revision\":-1}")]
        [TestCase("{\"after_revision\":1.5}")]
        [TestCase("{\"after_revision\":\"1\"}")]
        [TestCase("{\"bad\":1}")]
        public void Reader_rejects_invalid_delta_requests(string args)
        {
            Assert.IsNotNull(RaceDataTools.Execute("get_race_reader", Context(State(), null, T), args)["fehler"]);
        }
    }
}
