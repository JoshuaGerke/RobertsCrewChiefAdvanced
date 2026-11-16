using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Erkenntnisse aus Robs RaceRoom-Rennen 16.11.2026: Vorfallpunkte, letzte Runde im Zeitrennen, Platzmeldungen, Zwischenresümee.</summary>
    [TestFixture, Category("AiTeam")]
    public class RaceMindTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 16, 15, 40, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData Race(int classPosition = 12)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 5; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.ClassPosition = classPosition; s.SessionData.OverallPosition = classPosition; s.SessionData.CompletedLaps = 6;
            s.SessionData.TrackDefinition = new TrackDefinition("Mantorp Park", 3106f);
            s.PositionAndMotionData.CarSpeed = 45; s.PositionAndMotionData.DistanceRoundTrack = 1000;
            return s;
        }

        [Test]
        public void Incident_points_are_warned_in_steps_before_the_limit()
        {
            var watch = new IncidentPointsWatch();
            var gs = Race(); gs.SessionData.HasLimitedIncidents = true; gs.SessionData.MaxIncidentCount = 30; gs.SessionData.CurrentIncidentCount = 4;
            watch.Observe(gs, T);
            Assert.IsNull(watch.Take(gs, T, false, true), "Ausgangsstand beim Verbinden wird nicht gemeldet");
            gs.SessionData.CurrentIncidentCount = 15; watch.Observe(gs, T.AddSeconds(1));
            var half = watch.Take(gs, T.AddSeconds(1), false, true);
            Assert.IsNotNull(half); StringAssert.Contains("15 von 30", half.Text); half.MarkPlaybackStarted(T.AddSeconds(1));
            gs.SessionData.CurrentIncidentCount = 16; watch.Observe(gs, T.AddSeconds(2));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(2), false, true), "gleiche Stufe nicht erneut");
            gs.SessionData.CurrentIncidentCount = 27; watch.Observe(gs, T.AddSeconds(3));
            var tight = watch.Take(gs, T.AddSeconds(3), false, true);
            Assert.IsNotNull(tight); Assert.AreEqual(7, tight.Priority); StringAssert.Contains("3", tight.Text); tight.MarkPlaybackStarted(T.AddSeconds(3));
            gs.SessionData.CurrentIncidentCount = 29; watch.Observe(gs, T.AddSeconds(4));
            var last = watch.Take(gs, T.AddSeconds(4), false, true);
            Assert.IsNotNull(last); Assert.AreEqual(7, last.Priority); StringAssert.Contains("Punkt", last.Text);
        }

        [Test]
        public void Without_a_limit_nothing_is_claimed()
        {
            var watch = new IncidentPointsWatch();
            var gs = Race(); gs.SessionData.HasLimitedIncidents = false; gs.SessionData.MaxIncidentCount = -1; gs.SessionData.CurrentIncidentCount = 10;
            watch.Observe(gs, T); gs.SessionData.CurrentIncidentCount = 20; watch.Observe(gs, T.AddSeconds(1));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(1), false, true));
            Assert.AreEqual(5, IncidentPointsWatch.Level(29, 30)); Assert.AreEqual(4, IncidentPointsWatch.Level(27, 30));
            Assert.AreEqual(3, IncidentPointsWatch.Level(23, 30)); Assert.AreEqual(2, IncidentPointsWatch.Level(15, 30));
        }

        private static GameStateData TimedExpired(float leaderDistance, float ownDistance)
        {
            var s = Race();
            s.SessionData.SessionHasFixedTime = true; s.SessionData.SessionTotalRunTime = 900; s.SessionData.SessionRunningTime = 900; s.SessionData.SessionTimeRemaining = 0;
            s.PositionAndMotionData.DistanceRoundTrack = ownDistance;
            var leader = new OpponentData { OverallPosition = 1, ClassPosition = 1, DistanceRoundTrack = leaderDistance, IsActive = true, DriverRawName = "Leader" };
            s.OpponentData["leader"] = leader;
            return s;
        }

        [Test]
        public void Expired_clock_says_last_lap_when_the_leader_reaches_the_line_first()
        {
            LapWatch.ForgetTimedFinish();
            var before = Race(); var s = TimedExpired(2500, 900);
            string text = new LapWatch().Evaluate(before, s, -1, -1, false, true, null, null, "Rob");
            StringAssert.Contains("letzte Runde", text);
            Assert.AreEqual(1, LapWatch.LapsLeft(s.SessionData));
            LapWatch.ForgetTimedFinish();
        }

        [Test]
        public void Expired_clock_announces_one_more_lap_when_we_cross_the_line_first()
        {
            LapWatch.ForgetTimedFinish();
            var before = Race(); var s = TimedExpired(300, 2900);
            var watch = new LapWatch();
            StringAssert.Contains("Noch diese Runde", watch.Evaluate(before, s, -1, -1, false));
            Assert.AreEqual(2, LapWatch.LapsLeft(s.SessionData));
            var next = TimedExpired(800, 10); next.SessionData.CompletedLaps = 7; next.SessionData.IsNewLap = true;
            Assert.AreEqual(1, LapWatch.LapsLeft(next.SessionData));
            StringAssert.Contains("Letzte Runde", watch.Evaluate(s, next, -1, -1, false));
            LapWatch.ForgetTimedFinish();
        }

        [Test]
        public void Position_texts_vary_and_never_call_a_loss_progress()
        {
            var s = Race().SessionData;
            var up = Enumerable.Range(0, 5).Select(i => ActiveRaceObserver.MovedUp(s, 13, i, "Rob")).ToArray();
            var down = Enumerable.Range(0, 5).Select(i => ActiveRaceObserver.PositionLost(s, 17, i, "Rob")).ToArray();
            Assert.AreEqual(5, up.Distinct().Count()); Assert.AreEqual(5, down.Distinct().Count());
            Assert.IsFalse(down.Any(t => t.Contains("vorgerückt")));
            Assert.IsFalse(Enumerable.Range(0, 3).Select(i => ActiveRaceObserver.PositionNow(s, 17, i)).Any(t => t.Contains("vorgerückt")));
        }

        [Test]
        public void Calm_requires_five_seconds_of_free_road()
        {
            var gs = Race();
            Assert.IsTrue(RaceSummaryWatch.Calm(gs, 8, 9));
            Assert.IsFalse(RaceSummaryWatch.Calm(gs, 4.5f, 9));
            gs.OpponentData["near"] = new OpponentData { IsActive = true, DistanceRoundTrack = 1150 }; // 150 m = 3,3 s bei 45 m/s
            Assert.IsFalse(RaceSummaryWatch.Calm(gs, -1, -1));
            gs.OpponentData["near"].DistanceRoundTrack = 1400; // 400 m = 8,9 s
            Assert.IsTrue(RaceSummaryWatch.Calm(gs, -1, -1));
            gs.FlagData.isLocalYellow = true;
            Assert.IsFalse(RaceSummaryWatch.Calm(gs, -1, -1));
        }

        [Test]
        public void Summary_closing_line_fits_the_situation_and_varies()
        {
            var s = Race().SessionData;
            Assert.AreEqual(RaceSummaryWatch.Mood.Attack, RaceSummaryWatch.ChooseMood(s, 6f, 20f, -1f, 0f, 3, 0, false));
            Assert.AreEqual(RaceSummaryWatch.Mood.Defend, RaceSummaryWatch.ChooseMood(s, 30f, 6f, 0f, -1f, 0, 3, false));
            Assert.AreEqual(RaceSummaryWatch.Mood.Safe, RaceSummaryWatch.ChooseMood(s, 6f, 6f, -1f, -1f, 3, 3, true));
            Assert.AreEqual(RaceSummaryWatch.Mood.Steady, RaceSummaryWatch.ChooseMood(s, 40f, 30f, 0f, 0f, 3, 3, false));
            var lines = Enumerable.Range(0, 4).Select(i => RaceSummaryWatch.Closing(RaceSummaryWatch.Mood.Attack, i, "Rob")).ToArray();
            Assert.AreEqual(4, lines.Distinct().Count());
            StringAssert.Contains("Rob", lines[0]);
            StringAssert.DoesNotContain(", ,", RaceSummaryWatch.Closing(RaceSummaryWatch.Mood.Attack, 0, ""));
        }

        [Test]
        public void Planned_summaries_scale_with_race_length()
        {
            var s = Race().SessionData; s.SessionHasFixedTime = true;
            s.SessionTotalRunTime = 10 * 60; Assert.AreEqual(1, RaceSummaryWatch.PlannedCount(s));
            s.SessionTotalRunTime = 25 * 60; Assert.AreEqual(2, RaceSummaryWatch.PlannedCount(s));
            s.SessionTotalRunTime = 60 * 60; Assert.AreEqual(3, RaceSummaryWatch.PlannedCount(s));
        }

        [Test]
        public void Summary_is_composed_from_measured_values_only()
        {
            var gs = Race(12); gs.SessionData.SessionStartClassPosition = 19;
            var ctx = new RaceDataContext { GameState = gs, NowUtc = T, LastTelemetryUtc = T, Memory = new AiRaceMemory(), DriverName = "Rob" };
            string text = new RaceSummaryWatch(7).Compose(ctx, -1, -1);
            StringAssert.Contains("Rob", text);
            StringAssert.Contains("Platz 12", text);
            StringAssert.Contains("7 Plätze gutgemacht", text);
            StringAssert.DoesNotContain("Sekunden", text, "ohne gemessene Abstände keine Abstandszahl");
        }
    }
}
