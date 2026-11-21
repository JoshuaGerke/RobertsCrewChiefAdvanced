using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Rob 21.11.: Zwischenfazit 1–3-mal je Rennen, Reaktion auf gute und weniger gute Überholmanöver.</summary>
    [TestFixture, Category("AiTeam")]
    public class RaceCommentaryTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 21, 31, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        [TearDown]
        public void BackToGerman() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData Race(int position)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 5; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = position; gs.SessionData.OverallPosition = position; gs.SessionData.CompletedLaps = 4;
            gs.SessionData.TrackDefinition = new TrackDefinition("Algarve", 4635);
            gs.carClass = new CarData.CarClass(); gs.PositionAndMotionData.CarSpeed = 45; gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            return gs;
        }

        private static OpponentData Rival(GameStateData gs, string key, string name, int position, float distance)
        {
            var o = new OpponentData { DriverRawName = name, ClassPosition = position, OverallPosition = position, CarClass = gs.carClass,
                CurrentBestLapTime = 110, CompletedLaps = 4, Speed = 40, DistanceRoundTrack = distance, IsActive = true };
            gs.OpponentData[key] = o;
            return o;
        }

        private static List<LapRecord> Laps() =>
            Enumerable.Range(1, 4).Select(i => new LapRecord { LapNumber = i, LapTime = 110, Valid = true, Position = 5, RecordedUtc = T }).ToList();

        [Test]
        public void A_summary_waiting_too_long_for_a_quiet_minute_needs_only_one_second_of_air()
        {
            var gs = Race(13); var s = gs.SessionData;
            s.SessionHasFixedTime = true; s.SessionTotalRunTime = 2100; s.SessionRunningTime = 30;
            Rival(gs, "pack", "Pablo", 12, 1080); // 80 m vor uns: kein ruhiger Moment mit 3 s, aber kein Zweikampf unter 1 s
            var watch = new RaceSummaryWatch(3);
            var ctx = new RaceDataContext { GameState = gs, NowUtc = T, LastTelemetryUtc = T, Memory = new AiRaceMemory(), DriverName = "Rob" };
            watch.Observe(gs, null, () => ctx, T);
            double first = watch.Windows[0];
            s.SessionRunningTime = (float)(first * s.SessionTotalRunTime) + 5;
            watch.Observe(gs, null, () => ctx, T.AddSeconds(1));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(1), false, true), "pünktlich, aber Pulk: wartet");
            s.SessionRunningTime = (float)((first + RaceSummaryWatch.LateAfter + .01) * s.SessionTotalRunTime);
            watch.Observe(gs, null, () => ctx, T.AddSeconds(2));
            var call = watch.Take(gs, T.AddSeconds(2), false, true);
            Assert.IsNotNull(call, "spät: eine Sekunde Luft genügt");
            Assert.AreEqual("race-summary", call.Topic);
            Assert.IsTrue(call.Guaranteed, "der Rennkopf hält sie im Verkehr nicht mehr auf");
        }

        [Test]
        public void Every_clean_held_overtake_gets_a_reaction()
        {
            var gs = Race(5); gs.SessionData.PlayerLapTimeSessionBest = 110.5f;
            var timo = Rival(gs, "timo", "Timo", 4, 1050);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, Laps(), false, "Rob", true, T);
            gs.SessionData.ClassPosition = 4; timo.ClassPosition = 5; timo.DistanceRoundTrack = 950; timo.CurrentBestLapTime = 111;
            watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(.5));
            watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(16));
            var call = watch.Take(gs, T.AddSeconds(16), false, true);
            Assert.IsNotNull(call, "auch ein normales, sauberes Manöver");
            Assert.AreEqual("judge-overtake", call.Topic);
        }

        [Test]
        public void A_pass_with_new_incident_points_gets_an_honest_hint()
        {
            var gs = Race(5); gs.SessionData.CurrentIncidentCount = 2;
            var timo = Rival(gs, "timo", "Timo", 4, 1050);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, Laps(), false, "Rob", true, T);
            gs.SessionData.ClassPosition = 4; timo.ClassPosition = 5; timo.DistanceRoundTrack = 950;
            watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(.5));
            gs.SessionData.CurrentIncidentCount = 4; // Kontaktwertung während des Manövers
            watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(3));
            watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(16));
            var call = watch.Take(gs, T.AddSeconds(16), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-overtake-rough", call.Topic);
        }

        [Test]
        public void Contact_just_before_the_place_changes_belongs_to_the_overtake()
        {
            var gs = Race(5); gs.SessionData.CurrentIncidentCount = 2;
            var timo = Rival(gs, "timo", "Timo", 4, 1050);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, Laps(), false, "Rob", true, T);
            gs.SessionData.CurrentIncidentCount = 4;
            watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(1));
            gs.SessionData.ClassPosition = 4; timo.ClassPosition = 5; timo.DistanceRoundTrack = 950;
            watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(2));
            for (int t = 3; t <= 17; t += 2) watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(t));
            var call = watch.Take(gs, T.AddSeconds(17), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-overtake-rough", call.Topic);
        }

        [Test]
        public void Being_passed_straight_back_by_the_same_car_is_noticed()
        {
            var gs = Race(5);
            var timo = Rival(gs, "timo", "Timo", 4, 1050);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, Laps(), false, "Rob", true, T);
            gs.SessionData.ClassPosition = 4; timo.ClassPosition = 5; timo.DistanceRoundTrack = 950;
            watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(.5));
            gs.SessionData.ClassPosition = 5; timo.ClassPosition = 4; timo.DistanceRoundTrack = 1060;
            watch.Observe(gs, Laps(), false, "Rob", true, T.AddSeconds(8));
            var call = watch.Take(gs, T.AddSeconds(8), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("judge-overtake-countered", call.Topic);
            StringAssert.Contains("Timo", call.Text);
        }

        [Test]
        public void Rough_and_countered_lines_vary_in_every_language()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                foreach (string passed in new[] { "Timo", null })
                {
                    var rough = Enumerable.Range(0, 5).Select(v => DriverJudgement.RoughPass("Rob", passed, v)).ToArray();
                    var counter = Enumerable.Range(0, 5).Select(v => DriverJudgement.Countered("Rob", passed, v)).ToArray();
                    Assert.AreEqual(5, rough.Distinct().Count());
                    Assert.AreEqual(5, counter.Distinct().Count());
                    foreach (var t in rough.Concat(counter)) { StringAssert.DoesNotContain("{n}", t); StringAssert.DoesNotContain("  ", t); }
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
        }
    }
}
