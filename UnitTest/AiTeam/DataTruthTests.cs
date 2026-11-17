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
    }
}
