using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Kleinigkeiten aus der LMU-Aufnahme vom 21.11.: Tippfehler, Reifenziel, doppelter Langsam-Hinweis.</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RobDetailFixTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 21, 13, 50, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        [TearDown]
        public void BackToGerman() { RadioLanguage.Current = RadioLanguage.German; }

        [Test]
        public void Track_temperature_change_is_spelled_correctly()
        {
            var gs = new GameStateData(T.Ticks); gs.SessionData.SessionId = 9; gs.SessionData.SessionType = SessionType.Practice;
            gs.SessionData.SessionPhase = SessionPhase.Green;
            var watch = new WeatherWatch();
            gs.Now = T; gs.Conditions.UpdateLiveSample(T, 0, 1, 21, 26, 0, 0, 0, 0, 0, false, ConditionsMonitor.TrackStatus.UNKNOWN, ConditionsMonitor.TrackWetness.Dry);
            watch.Observe(gs, T);
            var later = T.AddSeconds(5);
            gs.Now = later; gs.Conditions.UpdateLiveSample(later, 0, 1, 21, 30, 0, 0, 0, 0, 0, false, ConditionsMonitor.TrackStatus.UNKNOWN, ConditionsMonitor.TrackWetness.Dry);
            watch.Observe(gs, later);
            var call = watch.Take(gs, later, false);
            Assert.IsNotNull(call);
            StringAssert.StartsWith("Streckentemperatur verändert", call.Text);
            StringAssert.DoesNotContain("Strecketemperatur", call.Text);
        }

        [Test]
        public void Tyre_guide_is_usable_from_65_and_aims_for_80_to_90()
        {
            Assert.AreEqual(65f, TyreGripWatch.SlickUsable);
            Assert.AreEqual(90f, TyreGripWatch.SlickTargetHigh);
            string text = TyreGripWatch.WarmText(TyreGripWatch.SlickGood, TyreGripWatch.SlickTop, 1);
            StringAssert.Contains("65", text);
            StringAssert.Contains("80 bis 90", text);
            RadioLanguage.Current = RadioLanguage.English;
            StringAssert.Contains("65", TyreGripWatch.WarmText(TyreGripWatch.SlickGood, TyreGripWatch.SlickTop, 1));
            RadioLanguage.Current = RadioLanguage.Spanish;
            StringAssert.Contains("65", TyreGripWatch.WarmText(TyreGripWatch.SlickGood, TyreGripWatch.SlickTop, 1));
        }

        private static GameStateData Quali()
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 25; s.SessionData.SessionType = SessionType.Qualify; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.TrackDefinition = new TrackDefinition("Synthetic", 0, 5000, new float[] { 0, 0 }, new float[] { 0, 0 });
            s.SessionData.SessionRunningTime = 100; s.PositionAndMotionData.CarSpeed = 50; s.PositionAndMotionData.DistanceRoundTrack = 1000;
            s.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.AUDI_TT_CUP };
            return s;
        }

        private static void Move(GameStateData s, OpponentData car, double seconds, float gap, float rivalSpeed)
        {
            s.Now = T.AddSeconds(seconds); s.SessionData.SessionRunningTime = 100 + (float)seconds;
            s.PositionAndMotionData.DistanceRoundTrack = (1000 + (float)seconds * 50) % 5000;
            s.PositionAndMotionData.WorldPosition = new[] { s.PositionAndMotionData.DistanceRoundTrack, 0f, 0f };
            car.DistanceRoundTrack = (1000 + gap + (float)seconds * rivalSpeed) % 5000; car.Speed = rivalSpeed;
            car.WorldPosition = new[] { car.DistanceRoundTrack, 0f, 0f };
        }

        private static int Calls(bool attemptFirst)
        {
            var s = Quali();
            var car = new OpponentData { DriverRawName = "Ahmad Dwaik", CarClass = s.carClass, CompletedLaps = 2 };
            car.OpponentLapData.Add(new LapData { OutLap = true }); s.OpponentData["dwaik"] = car;
            var tracks = new TrackAwareness(); var watch = new TrackSituationWatch(); int calls = 0;
            for (int i = 0; i <= 80; i++)
            {
                double t = i / 10d; Move(s, car, t, 360, 0); tracks.Observe(s, T.AddSeconds(t));
                var call = watch.Evaluate(tracks, s, T.AddSeconds(t));
                if (call == null) continue;
                calls++;
                if (attemptFirst && call.TryReserve(T.AddSeconds(t))) call.ReleaseForRetry(T.AddSeconds(t), "audio_discarded_before_playback");
            }
            return calls;
        }

        [Test]
        public void A_slow_car_call_that_reached_the_audio_is_not_repeated_for_the_same_driver()
        {
            Assert.AreEqual(1, Calls(true), "angefangen, verworfen: derselbe Fahrer nicht noch einmal");
        }

        [Test]
        public void A_slow_car_call_that_never_reached_the_audio_may_still_come()
        {
            Assert.GreaterOrEqual(Calls(false), 2, "nie versucht: die Warnung kommt noch");
        }
    }
}
