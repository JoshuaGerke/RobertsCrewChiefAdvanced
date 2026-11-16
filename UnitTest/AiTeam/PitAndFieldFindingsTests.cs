using System;
using System.Linq;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Erkenntnisse aus Robs AMS2-Fahrt 16.11.2026: Boxenzeiten, Boxenbestätigung, weniger Limit-Wiederholung, Gegnerzeiten.</summary>
    [TestFixture, Category("AiTeam")]
    public class PitAndFieldFindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 16, 15, 12, 0, DateTimeKind.Utc);

        private static GameStateData State(float sessionSeconds, bool inLane, float speed)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 42; s.SessionData.SessionType = SessionType.Practice; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.CompletedLaps = 6; s.SessionData.SessionRunningTime = sessionSeconds;
            s.PitData.InPitlane = inLane; s.PositionAndMotionData.CarSpeed = speed;
            return s;
        }

        [Test]
        public void Pit_stop_is_timed_in_entry_stationary_and_exit_and_reported_once_by_Ben()
        {
            RadioLanguage.Current = RadioLanguage.German;
            var timer = new PitStopTimer();
            float t = 100;
            timer.Observe(State(t, false, 40), T);
            for (int i = 0; i < 184; i++) { t += .1f; timer.Observe(State(t, true, 20), T.AddSeconds(t - 100)); }   // 18,4 s Einfahrt
            for (int i = 0; i < 121; i++) { t += .1f; timer.Observe(State(t, true, 0), T.AddSeconds(t - 100)); }    // 12,1 s Stand
            for (int i = 0; i < 150; i++) { t += .1f; timer.Observe(State(t, true, 20), T.AddSeconds(t - 100)); }   // 15,0 s Ausfahrt
            t += .1f; var outside = State(t, false, 30);
            timer.Observe(outside, T.AddSeconds(t - 100));
            var m = timer.Last;
            Assert.IsNotNull(m); Assert.IsTrue(m.Stopped);
            Assert.AreEqual(18.4, m.EntrySeconds, .15); Assert.AreEqual(12.1, m.StationarySeconds, .15); Assert.AreEqual(15.0, m.ExitSeconds, .15);
            Assert.AreEqual(45.6, m.TotalSeconds, .2);
            var call = timer.Take(outside, T.AddSeconds(t - 100), false, true);
            Assert.IsNotNull(call); Assert.AreEqual(TeamMember.Ben, call.Member); Assert.AreEqual("pit-stop-time", call.Topic);
            StringAssert.Contains("12,1", call.Text);
            call.MarkPlaybackStarted(T.AddSeconds(t - 100));
            Assert.IsNull(timer.Take(outside, T.AddSeconds(t - 99), false, true));
            Assert.AreEqual(12.1, (double)timer.Last.Describe()["standzeit_s"], .15);
        }

        [Test]
        public void Drive_through_and_garage_are_handled_honestly()
        {
            var timer = new PitStopTimer();
            timer.Observe(State(100, false, 40), T);
            for (int i = 1; i <= 200; i++) timer.Observe(State(100 + i * .1f, true, 20), T.AddSeconds(i * .1));
            var outside = State(120.1f, false, 30);
            timer.Observe(outside, T.AddSeconds(20.1));
            Assert.IsFalse(timer.Last.Stopped);
            StringAssert.Contains("ohne Halt", timer.Take(outside, T.AddSeconds(20.1), false, true).Text);

            var garage = new PitStopTimer();
            garage.Observe(State(100, true, 0), T);
            var reset = State(130, false, 0); reset.PitData.IsInGarage = true;
            garage.Observe(reset, T.AddSeconds(30));
            Assert.IsNull(garage.Last);
        }

        [Test]
        public void Pit_request_is_confirmed_and_the_lane_entry_is_not_a_second_reminder()
        {
            RadioLanguage.Current = RadioLanguage.German;
            var s = State(100, false, 50); s.PitData.HasRequestedPitStop = true;
            var watch = new PitLaneWatch();
            watch.Observe(s, T);
            var request = watch.Take(s, T, false, 7);
            Assert.IsNotNull(request);
            Assert.IsTrue(PitLaneWatch.RequestConfirmations.Any(c => request.Text.StartsWith(c, StringComparison.Ordinal)), request.Text);
            StringAssert.DoesNotContain("nächste Runde", request.Text);
            request.MarkPlaybackStarted(T);
            for (int i = 1; i <= 30; i++) { s.SessionData.SessionRunningTime = 100 + i; watch.Observe(s, T.AddSeconds(i)); watch.Take(s, T.AddSeconds(i), false); }
            s.PitData.InPitlane = true; s.PositionAndMotionData.CarSpeed = 20; s.SessionData.SessionRunningTime = 131;
            watch.Observe(s, T.AddSeconds(31));
            Assert.IsNull(watch.Take(s, T.AddSeconds(31), false), "Einfahrt nach bestätigter Anfrage: keine zweite Limit-Ansage");
        }

        [Test]
        public void Ams2_lap_times_fill_gaps_but_never_replace_observed_times()
        {
            var unknown = new OpponentData();
            var fastest = new float[64]; var last = new float[64];
            for (int i = 0; i < 64; i++) { fastest[i] = -1; last[i] = -1; }
            fastest[3] = 125.24f; last[3] = 126.5f;
            CrewChiefV4.AMS2.AMS2GameStateMapper.FillMissingLapTimesFromSimulator(unknown, fastest, last, 3);
            Assert.AreEqual(125.24f, unknown.CurrentBestLapTime, .001f);
            Assert.AreEqual(126.5f, unknown.LastLapTime, .001f);

            var observed = new OpponentData { CurrentBestLapTime = 124.9f, LastLapTime = 127f };
            CrewChiefV4.AMS2.AMS2GameStateMapper.FillMissingLapTimesFromSimulator(observed, fastest, last, 3);
            Assert.AreEqual(124.9f, observed.CurrentBestLapTime, .001f);
            Assert.AreEqual(127f, observed.LastLapTime, .001f);

            var invalid = new OpponentData();
            fastest[4] = 0; last[4] = -123f;
            CrewChiefV4.AMS2.AMS2GameStateMapper.FillMissingLapTimesFromSimulator(invalid, fastest, last, 4);
            Assert.AreEqual(-1, invalid.CurrentBestLapTime);
            Assert.AreEqual(-1, invalid.LastLapTime);
        }
    }
}
