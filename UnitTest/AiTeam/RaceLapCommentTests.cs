using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>LMU 21.11.: Im Rennen blieben vier eigene Runden ohne Kommentar, drei davon gestrichen.</summary>
    [TestFixture, Category("AiTeam")]
    public class RaceLapCommentTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 21, 30, 0, DateTimeKind.Utc);

        private static GameStateData Race(int laps)
        {
            var s = new GameStateData(1);
            s.SessionData.SessionId = 4; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.ClassPosition = 13; s.SessionData.CompletedLaps = laps; s.carClass = new CarData.CarClass();
            s.PositionAndMotionData.CarSpeed = 60;
            return s;
        }

        private static ObservedRaceCall LapCall(LapRecord lap, float best = 109.037f)
        {
            var gs = Race(lap.LapNumber - 1); var observer = new SessionLapObserver();
            observer.Observe(gs, null, T, true);
            gs.SessionData.CompletedLaps = lap.LapNumber - 1;
            observer.Observe(gs, new LapRecord { LapNumber = lap.LapNumber - 1, LapTime = best, Valid = true }, T.AddSeconds(1), true);
            observer.Take(gs, T.AddSeconds(1), false);
            gs.SessionData.CompletedLaps = lap.LapNumber;
            observer.Observe(gs, lap, T.AddSeconds(110), true);
            ObservedRaceCall call;
            while ((call = observer.Take(gs, T.AddSeconds(120), false)) != null)
                if (call.Topic == "race-lap") return call;
            return null;
        }

        [Test]
        public void A_deleted_lap_with_a_time_says_that_time_does_not_count()
        {
            var call = LapCall(new LapRecord { LapNumber = 5, LapTime = 107.657f, Valid = false });
            Assert.IsNotNull(call);
            StringAssert.Contains(LocalAnswers.LapTime(107.657f), call.Text);
            StringAssert.IsMatch("(?i)gestrichen|nicht gewertet|ohne Wertung|fällt raus", call.Text);
        }

        [Test]
        public void A_deleted_lap_without_a_time_uses_our_own_clock_and_says_so()
        {
            var call = LapCall(new LapRecord { LapNumber = 3, LapTime = -1, ClockLapTime = 109.61f, Valid = false });
            Assert.IsNotNull(call);
            StringAssert.Contains("1:49,6", call.Text);
            StringAssert.IsMatch("(?i)etwa|ungefähr|rund", call.Text);
        }

        [Test]
        public void A_deleted_lap_without_any_time_is_still_commented()
        {
            var call = LapCall(new LapRecord { LapNumber = 4, LapTime = -1, Valid = false });
            Assert.IsNotNull(call);
            StringAssert.DoesNotContain(":", call.Text);
        }

        [Test]
        public void A_valid_lap_names_its_time_and_the_gap_to_the_best()
        {
            var call = LapCall(new LapRecord { LapNumber = 6, LapTime = 109.837f, Valid = true });
            Assert.IsNotNull(call);
            StringAssert.Contains(LocalAnswers.LapTime(109.837f), call.Text);
            StringAssert.Contains("0,80", call.Text);
        }

        [Test]
        public void A_new_best_keeps_its_own_praise_and_gets_no_second_comment()
        {
            Assert.IsNull(LapCall(new LapRecord { LapNumber = 6, LapTime = 108.5f, Valid = true }));
        }

        [Test]
        public void Every_variant_set_has_five_lines_and_fills_its_placeholders()
        {
            for (int v = 0; v < 5; v++)
            {
                foreach (var text in new[] {
                    SessionLapObserver.RaceLapComment(false, 107.657f, -1, 109, v), SessionLapObserver.RaceLapComment(false, -1, 109.6f, 109, v),
                    SessionLapObserver.RaceLapComment(false, -1, -1, 109, v), SessionLapObserver.RaceLapComment(true, 109.2f, -1, 109, v),
                    SessionLapObserver.RaceLapComment(true, 110.5f, -1, 109, v), SessionLapObserver.RaceLapComment(true, 110.5f, -1, -1, v) })
                {
                    Assert.IsNotNull(text);
                    StringAssert.DoesNotContain("{", text);
                }
            }
            Assert.AreNotEqual(SessionLapObserver.RaceLapComment(true, 110.5f, -1, 109, 0), SessionLapObserver.RaceLapComment(true, 110.5f, -1, 109, 1));
        }

        [Test]
        public void The_memory_clocks_consecutive_laps_and_not_the_opening_one()
        {
            var memory = new AiRaceMemory();
            var before = Race(0); before.SessionData.SessionRunningTime = 300;
            memory.Update(null, before, T);
            var lap1 = Race(1); lap1.SessionData.IsNewLap = true; lap1.SessionData.SessionRunningTime = 424.4f; lap1.SessionData.LapTimePrevious = 124.4f;
            memory.Update(before, lap1, T.AddSeconds(124));
            var lap2 = Race(2); lap2.SessionData.IsNewLap = true; lap2.SessionData.SessionRunningTime = 534.0f; lap2.SessionData.LapTimePrevious = -1;
            memory.Update(lap1, lap2, T.AddSeconds(234));
            var laps = memory.GetLaps();
            Assert.AreEqual(-1, laps[0].ClockLapTime, "die Startrunde beginnt nicht an der Linie");
            Assert.AreEqual(109.6, laps[1].ClockLapTime, .05);
        }
    }
}
