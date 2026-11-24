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
            for (int i = 0; i <= 60; i++) timer.Observe(State(t - 6 + i * .1f, false, 40), T.AddSeconds(-6 + i * .1)); // vorher auf der Strecke
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
            for (int i = 0; i <= 60; i++) timer.Observe(State(94 + i * .1f, false, 40), T.AddSeconds(-6 + i * .1));
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

            // Rob 16.11. (Mosport): Aus der Garage zu Sitzungsbeginn losfahren ist kein Boxenstopp.
            var start = new PitStopTimer();
            for (int i = 0; i <= 120; i++) start.Observe(State(10 + i * .1f, true, i < 60 ? 0 : 20), T.AddSeconds(i * .1));
            var onTrack = State(22.2f, false, 30);
            start.Observe(onTrack, T.AddSeconds(12.2));
            Assert.IsNull(start.Last);
            Assert.IsNull(start.Take(onTrack, T.AddSeconds(12.2), false, true));
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
        public void Ams2_own_bests_fill_gaps_after_a_restart_but_never_replace_measured_values()
        {
            var s = new SessionData();
            CrewChiefV4.AMS2.AMS2GameStateMapper.FillMissingPlayerBestsFromSimulator(s, 123.359f, 34.5f, 50.1f, 38.7f);
            Assert.AreEqual(123.359f, s.PlayerLapTimeSessionBest, .001f);
            Assert.AreEqual(34.5f, s.PlayerBestSector1Time, .001f);
            s.PlayerBestSector2Time = 49.9f;
            CrewChiefV4.AMS2.AMS2GameStateMapper.FillMissingPlayerBestsFromSimulator(s, 120f, 33f, 48f, -1f);
            Assert.AreEqual(123.359f, s.PlayerLapTimeSessionBest, .001f);
            Assert.AreEqual(49.9f, s.PlayerBestSector2Time, .001f);
            Assert.AreEqual(38.7f, s.PlayerBestSector3Time, .001f);
        }

        private static GameStateData SectorFrame(int sectorNumber, float s1, DateTime source, float bestS1, SessionType type = SessionType.Practice)
        {
            var g = new GameStateData(source.Ticks);
            g.Now = source; g.SourceTelemetryUtc = source;
            g.SessionData.SessionId = 42; g.SessionData.SessionType = type; g.SessionData.SessionPhase = SessionPhase.Green;
            g.SessionData.CompletedLaps = 2; g.SessionData.SectorNumber = sectorNumber; g.SessionData.CurrentLapIsValid = true;
            g.SessionData.PlayerBestSector1Time = bestS1;
            g.PositionAndMotionData.CarSpeed = 50;
            if (sectorNumber == 2) g.SessionData.PlayerLapData.Add(new LapData { LapNumber = 3, IsValid = true, SectorTimes = new[] { s1, -1f, -1f } });
            return g;
        }

        [Test]
        public void First_sector_without_reference_is_announced_once_with_three_decimals()
        {
            RadioLanguage.Current = RadioLanguage.German;
            var before = SectorFrame(1, -1, T, -1);
            var now = T.AddMilliseconds(50);
            var present = SectorFrame(2, 34.512f, now, -1);
            var watch = new SessionLapObserver();
            watch.Observe(before, null, T, false);
            watch.Observe(present, null, now, false, before);
            var call = watch.Take(present, now, false);
            Assert.IsNotNull(call); Assert.AreEqual("sector-time", call.Topic);
            StringAssert.Contains("34,512", call.Text);
        }

        [Test]
        public void Live_sector_best_sounds_human_and_sometimes_encourages()
        {
            RadioLanguage.Current = RadioLanguage.German;
            var before = SectorFrame(1, -1, T, 35f);
            var now = T.AddMilliseconds(50);
            var present = SectorFrame(2, 34.512f, now, 35f);
            var watch = new SessionLapObserver();
            watch.Observe(before, null, T, false, null, true, null, "Rob");
            watch.Observe(present, null, now, false, before, true, null, "Rob");
            var call = watch.Take(present, now, false);
            Assert.IsNotNull(call); Assert.AreEqual("sector-best", call.Topic);
            StringAssert.StartsWith("Rob, ", call.Text);
            // Rob 24.11.: im Training kurz und knapp – welcher Sektor und wie viel schneller (35,000 → 34,512).
            Assert.IsTrue(call.Text.Contains("Sektor 1") || call.Text.Contains("ersten Sektor"), call.Text);
            StringAssert.Contains("0,488", call.Text);
            Assert.Less(call.Text.Length, 90, "kurz und knapp: " + call.Text);
        }

        [Test]
        public void Encouragement_catalog_exists_in_all_three_languages()
        {
            foreach (string language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                Assert.AreEqual(10, RadioPhraseCatalog.Current.Count("own-sector-best-push"), language); // Rob 16.11.: mehr Auswahl
            }
            RadioLanguage.Current = RadioLanguage.German;
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
