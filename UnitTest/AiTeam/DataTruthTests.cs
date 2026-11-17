using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Datenwahrheit (Rob 17.11.2026): Sektorereignisse dürfen nicht an einem einzigen Takt hängen.</summary>
    [TestFixture, Category("AiTeam")]
    public class DataTruthTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData State(int sector, float sectorOne, DateTime source, bool valid = true)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 7; gs.SessionData.SessionType = SessionType.Practice; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = 4; gs.PositionAndMotionData.CarSpeed = 50; gs.carClass = new CarData.CarClass();
            gs.SessionData.CompletedLaps = 2; gs.SessionData.SectorNumber = sector; gs.SessionData.CurrentLapIsValid = valid;
            gs.SessionData.PlayerBestSector1Time = 30f;
            gs.SourceTelemetryUtc = source;
            gs.SessionData.PlayerLapData.Add(new LapData { LapNumber = 3, IsValid = true, SectorTimes = new[] { sectorOne, -1f, -1f } });
            return gs;
        }

        [Test]
        public void Sector_time_arriving_one_tick_late_is_still_evaluated()
        {
            var before = State(1, -1, T.AddSeconds(30));
            var edge = State(2, -1, T.AddSeconds(30.1));          // Sektorwechsel, Zeit noch nicht eingetragen
            var late = State(2, 29.4f, T.AddSeconds(30.2));       // ein Takt später ist sie da
            var watch = new SessionLapObserver();
            watch.Observe(before, null, before.SourceTelemetryUtc, false);
            watch.Observe(edge, null, edge.SourceTelemetryUtc, false, before, true);
            Assert.IsNull(watch.Take(edge, edge.SourceTelemetryUtc, false));
            watch.Observe(late, null, late.SourceTelemetryUtc, false, edge, true);
            var call = watch.Take(late, late.SourceTelemetryUtc, false);
            Assert.IsNotNull(call, "früher ging die Gelegenheit für die ganze Runde verloren");
            Assert.AreEqual("sector-best", call.Topic);
            StringAssert.Contains("29,4", call.Text);
        }

        [Test]
        public void Late_sector_time_after_the_retry_window_is_ignored()
        {
            var before = State(1, -1, T.AddSeconds(30));
            var edge = State(2, -1, T.AddSeconds(30.1));
            var late = State(2, 29.4f, T.AddSeconds(33));
            var watch = new SessionLapObserver();
            watch.Observe(before, null, before.SourceTelemetryUtc, false);
            watch.Observe(edge, null, edge.SourceTelemetryUtc, false, before, true);
            watch.Observe(late, null, late.SourceTelemetryUtc, false, edge, true);
            Assert.IsNull(watch.Take(late, late.SourceTelemetryUtc, false));
        }

        [Test]
        public void Invalid_lap_at_the_sector_change_is_not_retried()
        {
            var before = State(1, -1, T.AddSeconds(30));
            var edge = State(2, 29.4f, T.AddSeconds(30.1), valid: false);
            var later = State(2, 29.4f, T.AddSeconds(30.2));
            var watch = new SessionLapObserver();
            watch.Observe(before, null, before.SourceTelemetryUtc, false);
            watch.Observe(edge, null, edge.SourceTelemetryUtc, false, before, true);
            watch.Observe(later, null, later.SourceTelemetryUtc, false, edge, true);
            Assert.IsNull(watch.Take(later, later.SourceTelemetryUtc, false));
        }

        [Test]
        public void Blocker_names_the_reason_and_only_transient_reasons_are_retried()
        {
            var before = State(1, -1, T.AddSeconds(30));
            Assert.AreEqual("sector_time_missing", SessionLapObserver.LiveSectorBlocker(State(2, -1, T.AddSeconds(30.1)), before, T.AddSeconds(30.1), 0, out _));
            Assert.AreEqual("duplicate_sample", SessionLapObserver.LiveSectorBlocker(State(2, 29.4f, T.AddSeconds(30)), before, T.AddSeconds(30.1), 0, out _));
            Assert.AreEqual("stale_source", SessionLapObserver.LiveSectorBlocker(State(2, 29.4f, T.AddSeconds(30.1)), before, T.AddSeconds(32), 0, out _));
            Assert.AreEqual("lap_invalid", SessionLapObserver.LiveSectorBlocker(State(2, 29.4f, T.AddSeconds(30.1), false), before, T.AddSeconds(30.1), 0, out _));
            Assert.IsNull(SessionLapObserver.LiveSectorBlocker(State(2, 29.4f, T.AddSeconds(30.1)), before, T.AddSeconds(30.1), 0, out var lap));
            Assert.AreEqual(3, lap.LapNumber);
            Assert.IsTrue(SessionLapObserver.RetryableSectorReason("sector_time_missing"));
            Assert.IsTrue(SessionLapObserver.RetryableSectorReason("duplicate_sample"));
            Assert.IsFalse(SessionLapObserver.RetryableSectorReason("lap_invalid"));
            Assert.IsFalse(SessionLapObserver.RetryableSectorReason("local_yellow"));
        }

        // ---------- Datenwahrheit je Kanal ----------

        private static GameStateData Driving(float wear, float temp, bool pit = false)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 9; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.PositionAndMotionData.CarSpeed = 50; gs.PitData.InPitlane = pit;
            var t = gs.TyreData; t.TyreWearActive = true;
            t.FrontLeftPercentWear = t.FrontRightPercentWear = t.RearLeftPercentWear = t.RearRightPercentWear = wear;
            t.FrontLeft_CenterTemp = t.FrontRight_CenterTemp = t.RearLeft_CenterTemp = t.RearRight_CenterTemp = temp;
            return gs;
        }

        [Test]
        public void Wear_that_does_not_move_while_driving_is_frozen_and_not_spoken_as_current()
        {
            var truth = new DataTruth();
            for (int i = 0; i <= 200; i++) truth.Observe(Driving(5f, 80 + i % 7), T.AddSeconds(i));
            Assert.AreEqual(ChannelState.Stale, truth.State(SimChannel.TyreWear));
            Assert.IsFalse(truth.MaySpeakAsCurrent(SimChannel.TyreWear));
            Assert.AreEqual(ChannelState.Live, truth.State(SimChannel.TyreTemperature));
            StringAssert.StartsWith("eingefroren seit", truth.Describe(SimChannel.TyreWear));
        }

        [Test]
        public void Moving_wear_is_live_and_standing_in_the_pits_does_not_count_as_frozen()
        {
            var truth = new DataTruth();
            for (int i = 0; i <= 200; i++) truth.Observe(Driving(5f + i * .02f, 80), T.AddSeconds(i));
            Assert.AreEqual(ChannelState.Live, truth.State(SimChannel.TyreWear));

            truth = new DataTruth();
            truth.Observe(Driving(5f, 80), T);
            truth.Observe(Driving(5.1f, 80), T.AddSeconds(1));
            for (int i = 2; i <= 400; i++) truth.Observe(Driving(5.1f, 80, pit: true), T.AddSeconds(i));
            Assert.AreEqual(ChannelState.Live, truth.State(SimChannel.TyreWear), "Boxenstand ist keine Fahrt");
        }

        [Test]
        public void Pauses_do_not_count_as_driving_and_unknown_keeps_the_old_behaviour()
        {
            var truth = new DataTruth();
            Assert.AreEqual(ChannelState.Unknown, truth.State(SimChannel.TyreWear));
            Assert.IsTrue(truth.MaySpeakAsCurrent(SimChannel.TyreWear), "Bestandsschutz vor dem ersten Beleg");
            for (int i = 0; i < 20; i++) truth.Observe(Driving(5f, 80), T.AddSeconds(i * 30)); // 30-s-Sprünge = Pause/Menü
            Assert.AreNotEqual(ChannelState.Stale, truth.State(SimChannel.TyreWear));
        }

        [Test]
        public void Frozen_wear_silences_the_routine_tyre_report()
        {
            var truth = new DataTruth();
            try
            {
                DataTruth.Use(truth);
                var gs = Driving(12f, 85);
                for (int i = 0; i <= 200; i++) truth.Observe(gs, T.AddSeconds(i));
                Assert.IsFalse(DataTruth.Current.MaySpeakAsCurrent(SimChannel.TyreWear));
                var prefs = new AiRadioPreferences();
                foreach (var o in AiRadioPreferences.Options) prefs.Set(o.Id, 0);
                prefs.Set("tyres", 30); prefs.SetTolerance("tyres", 0, 0);
                var schedule = new AiRadioSchedule(1); schedule.Observe(gs, T, prefs);
                var call = schedule.Take(new RaceDataContext { GameState = gs, NowUtc = T.AddSeconds(31), LastTelemetryUtc = T.AddSeconds(31) }, prefs, false);
                Assert.IsTrue(call == null || call.Topic != "tyres", "eingefrorener Verschleiß wird nicht als aktuell gemeldet");
            }
            finally { DataTruth.Use(new DataTruth()); }
        }

        [Test]
        public void Static_capabilities_follow_the_official_interfaces()
        {
            Assert.IsFalse(SimCapabilities.TyreWear(GameType.ASSETTO_EVO));
            Assert.IsTrue(SimCapabilities.TyreWear(GameType.AMS2));
            Assert.IsFalse(SimCapabilities.DamageValues(GameType.IRACING));
            Assert.IsTrue(SimCapabilities.IncidentPoints(GameType.IRACING));
            Assert.IsTrue(SimCapabilities.DamageValues(GameType.LMU));
            StringAssert.Contains("Vorfallpunkte", SimCapabilities.Describe(GameType.IRACING));
            Assert.AreEqual("", SimCapabilities.Describe(GameType.RACE_ROOM));
        }

        [Test]
        public void Report_lists_every_channel_for_the_diagnostics()
        {
            var truth = new DataTruth();
            truth.Observe(Driving(5f, 80), T);
            var r = truth.Report();
            foreach (SimChannel c in Enum.GetValues(typeof(SimChannel))) Assert.IsTrue(r.ContainsKey(c.ToString()), c.ToString());
            Assert.IsTrue(r.ContainsKey("game"));
        }
    }
}
