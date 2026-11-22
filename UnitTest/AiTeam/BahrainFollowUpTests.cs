using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Nacharbeit zu Robs LMU-Rennen in Bahrain (22.11.2026): was nach dem Absturz-Fix in v0.22.0 noch fehlte.
    /// Startfazit, Abstände im Zweikampf, Zwischenfazit zur Rennhälfte, Klassen-Bestzeit, letzte Runde, fremde schnellste Runde,
    /// Spritschätzung im Qualifying.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class BahrainFollowUpTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 22, 11, 45, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData Race(int classPosition = 5)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 7; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.ClassPosition = classPosition; s.SessionData.OverallPosition = classPosition; s.SessionData.CompletedLaps = 6;
            s.SessionData.TrackDefinition = new TrackDefinition("Bahrain International Circuit", 5412f);
            s.PositionAndMotionData.CarSpeed = 60; s.PositionAndMotionData.DistanceRoundTrack = 1000;
            s.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            return s;
        }

        [Test]
        public void Start_verdict_and_gaps_to_the_direct_rivals_are_not_held_by_tension()
        {
            var load = new RadioLoad { DensityOverride = 1 };
            load.Update(1, T); // Gegner unter 0,5 s: höchste Anspannung
            Assert.IsTrue(load.Allows("start-verdict", 5, T, out _), "Startfazit verfiel in Bahrain nach 8 s");
            Assert.IsTrue(load.Allows("native-gap-front", 3, T, out _), "Abstand nach vorn wurde viermal gehalten");
            Assert.IsTrue(load.Allows("native-gap-behind", 3, T, out _), "Abstand nach hinten kam 0 von 2 Mal");
            Assert.IsFalse(load.Allows("judge-consistency", 3, T, out _), "Lob wartet weiter");
            Assert.AreEqual(30, ActiveRaceObserver.StartVerdictSeconds);
            Assert.GreaterOrEqual(NativeTimingEvents.DecisionLifetime.TotalSeconds, 12);
        }

        [Test]
        public void Race_summary_needs_only_a_short_breath_after_the_last_call()
        {
            var load = new RadioLoad { DensityOverride = 1 };
            load.NoteSpoken("position", "Platz fünf.", T);
            Assert.IsFalse(load.Allows("fuel-range", 3, T.AddSeconds(4), out string why), "andere Daten brauchen weiter 8 s");
            Assert.AreEqual("load_data_needs_a_gap", why);
            Assert.IsTrue(load.Allows("race-summary", 2, T.AddSeconds(4), out _), "Zwischenstand: 3 s genügen");
            Assert.IsFalse(load.Allows("race-summary", 2, T.AddSeconds(2), out _));
        }

        [Test]
        public void A_fixed_summary_is_planned_at_half_distance()
        {
            var gs = Race();
            var s = gs.SessionData;
            s.SessionHasFixedTime = true; s.SessionTotalRunTime = 1440; s.SessionRunningTime = 30; // 24 Minuten wie in Bahrain
            var watch = new RaceSummaryWatch(5);
            var ctx = new RaceDataContext { GameState = gs, NowUtc = T, LastTelemetryUtc = T, Memory = new AiRaceMemory(), DriverName = "Rob" };
            watch.Observe(gs, null, () => ctx, T);
            Assert.GreaterOrEqual(watch.HalfWindow, 1);
            Assert.AreEqual(RaceSummaryWatch.HalfRace, watch.Windows[watch.HalfWindow], 1e-9);
            for (int i = 1; i < watch.Windows.Count; i++) Assert.Greater(watch.Windows[i], watch.Windows[i - 1], "Fenster aufsteigend");
        }

        [Test]
        public void Class_best_claim_is_checked_against_the_field()
        {
            var gs = Race();
            gs.SessionData.PlayerLapTimeSessionBest = gs.SessionData.PlayerClassSessionBestLapTime = 82.361f;
            Assert.AreEqual(-1, RaceSummaryWatch.FieldCheckedClassBest(gs), "ohne eine Gegnerzeit ist „schnellste Runde“ nicht belegt");
            var krobber = new OpponentData { DriverRawName = "Krobber", IsActive = true, CurrentBestLapTime = 82.104f, CarClass = gs.carClass };
            gs.OpponentData["krobber"] = krobber;
            Assert.AreEqual(82.104f, RaceSummaryWatch.FieldCheckedClassBest(gs), 1e-4, "die schnellere Gegnerzeit zählt, auch wenn die Sim Robs Zeit zeigt");
            krobber.CarClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.LMP2 };
            Assert.AreEqual(82.361f, RaceSummaryWatch.FieldCheckedClassBest(gs), 1e-4, "andere Klasse zählt nicht");
        }

        [Test]
        public void Last_lap_is_called_once_even_when_the_clock_runs_out_right_after()
        {
            LapWatch.ForgetTimedFinish();
            try
            {
                var before = Race();
                before.SessionData.SessionHasFixedTime = true; before.SessionData.SessionTotalRunTime = 1440;
                before.SessionData.SessionRunningTime = 1436; before.SessionData.SessionTimeRemaining = 4;
                var lastLap = Race();
                lastLap.SessionData.SessionHasFixedTime = true; lastLap.SessionData.SessionTotalRunTime = 1440;
                lastLap.SessionData.SessionRunningTime = 1437; lastLap.SessionData.SessionTimeRemaining = 3;
                lastLap.SessionData.IsLastLap = true;
                var watch = new LapWatch();
                StringAssert.Contains("etzte Runde", watch.Evaluate(before, lastLap, -1, -1, false));

                var expired = Race();
                expired.SessionData.SessionHasFixedTime = true; expired.SessionData.SessionTotalRunTime = 1440;
                expired.SessionData.SessionRunningTime = 1440; expired.SessionData.SessionTimeRemaining = 0;
                expired.SessionData.IsLastLap = true;
                expired.PositionAndMotionData.DistanceRoundTrack = 900;
                expired.OpponentData["leader"] = new OpponentData { OverallPosition = 1, ClassPosition = 1, DistanceRoundTrack = 2500, IsActive = true, DriverRawName = "Leader" };
                Assert.IsNull(watch.Evaluate(lastLap, expired, -1, -1, false), "12:04:28: kein zweites „letzte Runde“ über die Uhr");
            }
            finally { LapWatch.ForgetTimedFinish(); }
        }

        [Test]
        public void Rival_fastest_lap_is_reported_when_the_sim_class_best_lags()
        {
            var before = Race(); var after = Race(); var later = Race();
            after.OpponentData = later.OpponentData = before.OpponentData;
            foreach (var s in new[] { before, after, later })
                s.SessionData.PlayerLapTimeSessionBest = s.SessionData.PlayerClassSessionBestLapTime = 82.361f; // Sim bleibt auf Robs Zeit
            var rival = new OpponentData { DriverRawName = "Krobber", IsActive = true, ClassPosition = 2, CompletedLaps = 5,
                CurrentBestLapTime = 82.9f, LastLapTime = 82.9f, LastLapValid = true, CarClass = before.carClass, Speed = 60 };
            before.OpponentData["krobber"] = rival;
            var events = new NativeFieldEvents();
            events.CaptureMappedFastestLap(null, before);

            rival.CompletedLaps = 6; rival.IsNewLap = true;
            rival.CurrentBestLapTime = rival.LastLapTime = 82.104f;
            events.CaptureMappedFastestLap(before, after);
            events.Observe(after, after.Now, null, true);
            Assert.IsNull(events.Take(after, after.Now, false, true, false), "erst die Sim abwarten");

            rival.IsNewLap = false;
            later.Now = T.AddSeconds(3);
            later.SourceTelemetryUtc = later.Now;
            events.CaptureMappedFastestLap(after, later);
            events.Observe(later, later.Now, null, true);
            var call = events.Take(later, later.Now, false, true, false);
            Assert.IsNotNull(call, "Gegenprobe am Feld: Krobber ist schneller als alle");
            Assert.AreEqual("field-fastest", call.Topic);
            StringAssert.Contains("Krobber", call.Text);
            Assert.GreaterOrEqual((call.ExpiresUtc - call.DetectedUtc).TotalSeconds, NativeFieldEvents.FastestLapSeconds - .1);

            later.SessionData.PlayerLapTimeSessionBest = 81.9f;
            Assert.IsFalse(call.IsValid(later, later.Now), "Rob war inzwischen schneller");
        }

        [Test]
        public void Qualifying_always_gets_a_fuel_estimate()
        {
            Assert.IsNotNull(QualiFuelWatch.Advice(30, 3, 300, 100, "Rob"), "zu viel Sprit: Ballast-Hinweis wie bisher");
            Assert.IsNull(QualiFuelWatch.Advice(15, 3, 300, 100, "Rob"));
            string ok = QualiFuelWatch.Estimate(15, 3, 300, 100, "Rob");
            StringAssert.Contains("5 Runden", ok); StringAssert.Contains("4", ok);
            string shortFuel = QualiFuelWatch.Estimate(9, 3, 300, 100, "Rob");
            StringAssert.Contains("3 Runden", shortFuel);
            Assert.AreNotEqual(ok, shortFuel);
            Assert.IsNull(QualiFuelWatch.Estimate(0, 3, 300, 100, "Rob"));
        }
    }
}
