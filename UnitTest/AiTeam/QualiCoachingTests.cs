using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Rob 18.11.: Qualifying lebendiger und professionell – Zielzeit beim Start, Sektorvergleich, wo die Zeit liegt.</summary>
    [TestFixture, Category("AiTeam")]
    public class QualiCoachingTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Target_position_steps_like_an_engineer_would()
        {
            Assert.AreEqual(10, SessionLapObserver.TargetPosition(14));
            Assert.AreEqual(5, SessionLapObserver.TargetPosition(8));
            Assert.AreEqual(3, SessionLapObserver.TargetPosition(4));
            Assert.AreEqual(1, SessionLapObserver.TargetPosition(2));
        }

        [Test]
        public void Target_text_names_best_own_and_target_time_in_every_language()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var cases = new Func<int, string>[]
                {
                    v => SessionLapObserver.TargetText(-1, "", 0, -1, 1, -1, v),
                    v => SessionLapObserver.TargetText(107.3f, "Timo", 0, -1, 1, -1, v),
                    v => SessionLapObserver.TargetText(107.3f, "", 1, 107.3f, 1, -1, v),
                    v => SessionLapObserver.TargetText(107.3f, "Timo", 8, 108.1f, 5, 107.9f, v),
                    v => SessionLapObserver.TargetText(107.3f, "", 8, 108.1f, 5, -1, v),
                };
                foreach (var c in cases)
                {
                    var texts = Enumerable.Range(0, 6).Select(c).ToList();
                    Assert.GreaterOrEqual(texts.Distinct().Count(), 4, lang + ": " + texts[0]);
                    foreach (var t in texts) { StringAssert.DoesNotContain("  ", t); Assert.IsFalse(char.IsLower(t[0]), t); }
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
            string german = SessionLapObserver.TargetText(107.3f, "Timo", 8, 108.1f, 5, 107.9f, 0);
            StringAssert.Contains("P8", german);
            StringAssert.Contains("Platz 5", german);
            StringAssert.Contains("1:47", german);
        }

        [Test]
        public void Lap_analysis_finds_where_the_time_is()
        {
            string text = SessionLapObserver.LapAnalysis(new[] { 30.10f, 40.50f, 35.80f }, new[] { 30.20f, 40.40f, 35.40f }, 0);
            StringAssert.Contains("Sektor 3", text);
            StringAssert.Contains("0,400", text);
            StringAssert.Contains("Sektor 1", text, "die Stärke wird mitgenannt");
            StringAssert.Contains("allen drei", SessionLapObserver.LapAnalysis(new[] { 30f, 40f, 35f }, new[] { 30.1f, 40.1f, 35.1f }, 0));
            Assert.IsNull(SessionLapObserver.LapAnalysis(new[] { 30f, 40f, 35.01f }, new[] { 30f, 40f, 35f }, 0), "Hundertstel sind kein Thema");
            Assert.IsNull(SessionLapObserver.LapAnalysis(new[] { 30f, 40f, 35f }, null, 0), "ohne Vergleich kein Satz");
            Assert.AreEqual(5, Enumerable.Range(0, 5).Select(v => SessionLapObserver.LapAnalysis(new[] { 30.10f, 40.50f, 35.80f }, new[] { 30.20f, 40.40f, 35.40f }, v)).Distinct().Count());
        }

        [Test]
        public void Sector_against_the_field_says_green_or_plus()
        {
            StringAssert.Contains("schneller", SessionLapObserver.SectorVsField(0, -.123, 0));
            StringAssert.Contains("plus 0,123", SessionLapObserver.SectorVsField(1, .123, 0));
            Assert.AreEqual(5, Enumerable.Range(0, 5).Select(v => SessionLapObserver.SectorVsField(1, .2, v)).Distinct().Count());
            Assert.AreEqual(5, Enumerable.Range(0, 5).Select(v => SessionLapObserver.SectorVsField(1, -.2, v)).Distinct().Count());
        }

        private static GameStateData State()
        {
            var s = new GameStateData(T.Ticks);
            s.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.AUDI_TT_CUP };
            s.SessionData.SessionId = 12; s.SessionData.SessionType = SessionType.Qualify; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.TrackDefinition = new TrackDefinition("Test", 0, 5000, new float[] { 0, 0 }, new float[] { 0, 0 });
            s.SessionData.ClassPosition = 8; s.SessionData.OverallPosition = 8; s.SessionData.CompletedLaps = 2;
            s.SessionData.CurrentLapIsValid = true; s.PositionAndMotionData.DistanceRoundTrack = 1000; s.PositionAndMotionData.CarSpeed = 40;
            return s;
        }

        [Test]
        public void Crossing_the_line_on_a_quali_flying_lap_brings_start_and_target()
        {
            var before = State(); before.PitData.OnOutLap = true; before.PositionAndMotionData.DistanceRoundTrack = 4990; before.SessionData.SessionRunningTime = 100;
            var after = State(); after.SessionData.IsNewLap = true; after.SessionData.CompletedLaps = 3; after.PositionAndMotionData.DistanceRoundTrack = 10; after.SessionData.SessionRunningTime = 100.1f;
            after.SessionData.PlayerLapTimeSessionBest = 108.1f;
            after.OpponentData["a"] = new OpponentData { DriverRawName = "Timo", ClassPosition = 1, OverallPosition = 1, CarClass = after.carClass, CurrentBestLapTime = 107.3f, IsActive = true };
            after.OpponentData["b"] = new OpponentData { DriverRawName = "Ana", ClassPosition = 5, OverallPosition = 5, CarClass = after.carClass, CurrentBestLapTime = 107.9f, IsActive = true };
            var observer = new SessionLapObserver();
            observer.Observe(before, null, T, false);
            observer.Observe(after, null, T.AddSeconds(.1), false, before);
            var start = observer.Take(after, T.AddSeconds(.1), false);
            Assert.AreEqual("timed-lap-start", start.Topic);
            start.MarkPlaybackStarted(T.AddSeconds(.1)); start.MarkPlaybackFinished(T.AddSeconds(3), false);
            Assert.IsNull(observer.Take(after, T.AddSeconds(1), false), "nicht direkt hinterher");
            var target = observer.Take(after, T.AddSeconds(4), false);
            Assert.IsNotNull(target);
            Assert.AreEqual("quali-target", target.Topic);
            StringAssert.Contains("1:47", target.Text);
        }
    }
}
