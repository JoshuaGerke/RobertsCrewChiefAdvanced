using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class FastestLapTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 13, 21, 0, 0, DateTimeKind.Utc);

        private static GameStateData State(SessionType type)
        {
            var s = new GameStateData(1);
            s.SessionData.SessionId = 7; s.SessionData.SessionType = type; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.ClassPosition = s.SessionData.OverallPosition = 14; s.SessionData.CompletedLaps = 3;
            s.SessionData.NumCarsOverall = 28; s.SessionData.NumCarsInPlayerClass = 27;
            s.carClass = new CarData.CarClass(); s.PositionAndMotionData.CarSpeed = 40;
            s.OpponentData["a"] = new OpponentData { DriverRawName = "John Reilly", CarClass = s.carClass, IsActive = true, CurrentBestLapTime = 112.5f, LastLapTime = 113f };
            return s;
        }

        [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)]
        public void New_fastest_lap_is_called_with_name_time_gap_and_motivation(SessionType type)
        {
            var gs = State(type); gs.SessionData.PlayerLapTimeSessionBest = 113.8f;
            var o = new SessionLapObserver(); o.Observe(gs, null, T, true);
            var rival = gs.OpponentData["a"]; rival.CurrentBestLapTime = rival.LastLapTime = 111.834f;
            o.Observe(gs, null, T.AddSeconds(1), true);
            var call = o.Take(gs, T.AddSeconds(1), false);
            Assert.IsNotNull(call);
            StringAssert.StartsWith("John Reilly fährt die schnellste Runde: " + LocalAnswers.LapTime(111.834f) + ". Dir fehlen 2,0 Sekunden. ", call.Text);
            CollectionAssert.Contains(SessionLapObserver.MotivationPhrases, call.Text.Substring(call.Text.IndexOf("Sekunden. ") + 10));
        }

        [Test]
        public void Historic_best_appearing_after_join_stays_quiet()
        {
            var gs = State(SessionType.Race);
            var o = new SessionLapObserver(); o.Observe(gs, null, T, true);
            gs.OpponentData["b"] = new OpponentData { DriverRawName = "Late", CarClass = gs.carClass, IsActive = true, CurrentBestLapTime = 110f, LastLapTime = 114f };
            o.Observe(gs, null, T.AddSeconds(1), true);
            Assert.IsNull(o.Take(gs, T.AddSeconds(1), false));
        }

        [Test]
        public void Motivation_rotates_and_gap_is_omitted_without_own_time()
        {
            string a = SessionLapObserver.FastestLapText("X", 90f, -1f, false, 0), b = SessionLapObserver.FastestLapText("X", 90f, -1f, false, 1);
            Assert.AreNotEqual(a, b);
            StringAssert.DoesNotContain("fehlen", a);
            StringAssert.Contains("der Klasse", SessionLapObserver.FastestLapText("X", 90f, 90.4f, true, 0));
            StringAssert.Contains("0,40 Sekunden", SessionLapObserver.FastestLapText("X", 90f, 90.4f, true, 0));
        }

        [Test]
        public void Same_class_and_overall_position_is_spoken_as_plain_place()
        {
            var s = State(SessionType.Race).SessionData;
            Assert.IsFalse(RaceRadioLanguage.SeparateClass(s));
            s.OverallPosition = 20;
            Assert.IsTrue(RaceRadioLanguage.SeparateClass(s));
        }

        [Test]
        public void Repeated_call_is_covered_unless_the_first_was_interrupted()
        {
            var first = new ObservedRaceCall { Topic = "status", Text = "Rob, Platz 14.", Member = TeamMember.Ben, DetectedUtc = T, ExpiresUtc = T.AddSeconds(6) };
            var second = new ObservedRaceCall { Topic = "summary", Text = "Rob, Platz 14.", Member = TeamMember.Ben, DetectedUtc = T, ExpiresUtc = T.AddSeconds(6) };
            Assert.AreEqual(AiTeamService.RepeatKey(first, out var window), AiTeamService.RepeatKey(second, out _));
            Assert.IsTrue(AiTeamService.Covers(first, T, second, window, T.AddSeconds(2)), "still rendering");
            Assert.IsTrue(first.TryReserve(T)); first.MarkPlaybackStarted(T.AddSeconds(1));
            Assert.IsTrue(AiTeamService.Covers(first, T, second, window, T.AddSeconds(40)));
            Assert.IsFalse(AiTeamService.Covers(first, T, second, window, T.AddSeconds(61)));
            first.MarkPlaybackFinished(T.AddSeconds(2), true);
            Assert.IsFalse(AiTeamService.Covers(first, T, second, window, T.AddSeconds(30)), "interrupted report may come again");
        }

        [Test]
        public void Start_verdict_after_half_of_lap_one_names_gain_loss_or_hold()
        {
            var s = State(SessionType.Race).SessionData; s.OverallPosition = s.ClassPosition = 11;
            Assert.AreEqual("Rob, guter Start! 3 Plätze gewonnen, jetzt Platz 11.", ActiveRaceObserver.StartVerdict(s, 14, "Rob"));
            StringAssert.StartsWith("Rob, am Start einen Platz verloren, jetzt Platz 11.", ActiveRaceObserver.StartVerdict(s, 10, "Rob"));
            Assert.AreEqual("Rob, sauberer Start, Platz 11 gehalten.", ActiveRaceObserver.StartVerdict(s, 11, "Rob"));

            var gs = State(SessionType.Race); gs.SessionData.CompletedLaps = 0; gs.SessionData.SessionStartClassPosition = 14;
            gs.SessionData.ClassPosition = gs.SessionData.OverallPosition = 12;
            gs.SessionData.TrackDefinition = new TrackDefinition("Algarve", 4600f);
            var observer = new ActiveRaceObserver();
            gs.PositionAndMotionData.DistanceRoundTrack = 1600; observer.Observe(gs, T, "Rob", 120);
            Assert.AreEqual(0, observer.PendingCount, "noch vor der halben Runde (Robs Vorgabe)");
            gs.PositionAndMotionData.DistanceRoundTrack = 2400; observer.Observe(gs, T.AddSeconds(1), "Rob", 120);
            gs.PositionAndMotionData.DistanceRoundTrack = 2500; observer.Observe(gs, T.AddSeconds(2), "Rob", 120);
            Assert.AreEqual(1, observer.PendingCount, "genau einmal");
        }

        [TestCase(1, "Du führst.")] [TestCase(3, "Podiumsplatz")] [TestCase(6, "vorderen Feld")] [TestCase(14, "Mittelfeld")] [TestCase(24, "hinteren Feld")]
        public void Position_is_assessed_not_just_named(int position, string expected)
        {
            var s = State(SessionType.Race).SessionData; s.ClassPosition = s.OverallPosition = position;
            StringAssert.Contains(expected, ActiveRaceObserver.Assess(s));
        }

        [Test]
        public void Fuel_report_says_whether_it_lasts_to_the_finish()
        {
            Assert.AreEqual("Das reicht bis ins Ziel.", new FuelEstimate { PerLap = 1.7f, AdditionalWithoutReserve = -3f, ReserveLitres = .5f }.Verdict());
            Assert.AreEqual("Bis ins Ziel wird es knapp.", new FuelEstimate { PerLap = 1.7f, AdditionalWithoutReserve = -.2f, ReserveLitres = .5f }.Verdict());
            Assert.AreEqual("So reicht es nicht bis ins Ziel.", new FuelEstimate { PerLap = 1.7f, AdditionalWithoutReserve = 2f, ReserveLitres = .5f }.Verdict());
            StringAssert.StartsWith("Nach aktueller Pace", new FuelEstimate { PerLap = 1.7f, AdditionalWithoutReserve = -3f, ReserveLitres = .5f, EstimatedFromTime = true }.Verdict());
        }

        [Test]
        public void Hazard_warnings_of_the_same_kind_count_as_one()
        {
            var a = new ObservedRaceCall { Topic = "race-hazard-ahead", Text = RaceTrafficWatch.DistancePhrase(80, true) };
            var b = new ObservedRaceCall { Topic = "race-hazard-ahead", Text = RaceTrafficWatch.DistancePhrase(60, false) };
            Assert.AreEqual(AiTeamService.RepeatKey(a, out var window), AiTeamService.RepeatKey(b, out _));
            Assert.AreEqual(TimeSpan.FromSeconds(12), window);
        }
    }
}
