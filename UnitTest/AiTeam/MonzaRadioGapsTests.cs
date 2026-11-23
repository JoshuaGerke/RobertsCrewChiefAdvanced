using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// RaceRoom Monza 22.11.2026, 21:32–21:48 (Flash v2.5, keine Stimmstörung): Abstände 18-mal erkannt, 3-mal gesprochen;
    /// beide Klassenbestzeiten verfallen; Startfazit 68-mal zurückgestellt und verfallen; „auf Bestzeitkurs“ im Rennen abgeschaltet;
    /// viermal derselbe Überholsatz; Robs Wunsch „Abstände häufiger“ nicht erkannt. Dazu Robs Änderung vom 23.11.: Sara meldet
    /// kein gewöhnliches Gelb mehr.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class MonzaRadioGapsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 22, 20, 36, 0, DateTimeKind.Utc);
        private const string RobsWish = "Das muss häufiger kommen, Ben. Das müsstest du häufiger ab und zu melden, wenn ich im Zweikampf bin zum Vordermann. Hin und wieder mir mitteilen, wie die Abstände sind zum Hintermann, zum Vordermann. Das muss häufiger kommen.";

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; SaraFlagPolicy.PlainYellowRadio = false; }

        [Test]
        public void Gaps_to_the_direct_rivals_are_not_held_by_a_close_fight()
        {
            var load = new RadioLoad { DensityOverride = 1 };
            load.Update(1, T); // 0,5 s vor und hinter Rob
            Assert.IsTrue(load.Allows("ahead", 1, T, out _), "Monza: „Jero Tula liegt 0,5 Sekunden vor dir“ verfiel");
            Assert.IsTrue(load.Allows("behind", 1, T, out _), "Monza: „Kuzler Tibor liegt 0,2 Sekunden hinter dir“ verfiel");
            Assert.IsFalse(load.Allows("fuel-range", 3, T, out _), "andere Daten warten weiter");
            Assert.IsTrue(AiRadioSchedule.FightGap("ahead"));
            Assert.IsTrue(AiRadioSchedule.FightGap("behind"));
            Assert.IsFalse(AiRadioSchedule.FightGap("status"));
            Assert.IsFalse(AiRadioSchedule.FightGap("fuel"));
        }

        [Test]
        public void In_a_fight_the_next_gap_report_comes_after_half_the_interval()
        {
            Assert.AreEqual(60, AiRadioSchedule.BattleGapDelay(120));
            Assert.AreEqual(30, AiRadioSchedule.BattleGapDelay(45), "nie öfter als alle 30 s");
            Assert.AreEqual(0, AiRadioSchedule.BattleGapDelay(0), "abgeschaltet bleibt abgeschaltet");
        }

        [Test]
        public void Class_best_lap_of_a_rival_is_a_race_event_not_data()
        {
            Assert.AreEqual(RadioClass.Event, RadioLoad.Classify("field-fastest", 4));
            Assert.AreEqual(RadioClass.Event, RadioLoad.Classify("field-fastest-overall", 4));
            Assert.AreEqual(RadioClass.Data, RadioLoad.Classify("field-pit", 3));
            var load = new RadioLoad { DensityOverride = 1 };
            load.Update(.85, T);
            Assert.IsTrue(load.Allows("field-fastest", 4, T, out _), "Duval 1:57,397 und Molck 1:56,978 verfielen als Daten");
        }

        [Test]
        public void Start_verdict_counts_as_short_tactical_radio()
        {
            Assert.IsTrue(AiTeamService.IsShortTactical("start-verdict"), "68-mal auf ruhigen Funk gewartet, nach 30 s verfallen");
            Assert.IsTrue(AiTeamService.IsShortTactical("ahead"));
            Assert.IsFalse(AiTeamService.IsShortTactical("race-summary"));
        }

        [Test]
        public void Robs_own_words_set_gaps_more_often()
        {
            Assert.AreEqual("abstaende haeufiger", DriverRadioPreferences.LooseGapWish(RobsWish));
            Assert.IsTrue(DriverRadioPreferences.IsCommand(RobsWish));
            Assert.AreEqual("abstaende seltener", DriverRadioPreferences.LooseGapWish("Die Abstände bitte seltener melden."));
            Assert.IsNull(DriverRadioPreferences.LooseGapWish("Kommen die Abstände jetzt häufiger?"), "Frage ändert nichts");
            Assert.IsNull(DriverRadioPreferences.LooseGapWish("Melde mir Abstände nicht häufiger"), "Verneinung bleibt mehrdeutig");
            Assert.IsNull(DriverRadioPreferences.LooseGapWish("Abstände seltener, aber nur wenn ich führe"), "Bedingung bleibt mehrdeutig");
            Assert.IsNull(DriverRadioPreferences.LooseGapWish("Das muss häufiger kommen."), "ohne Abstand kein Abstandswunsch");
            Assert.IsNull(DriverRadioPreferences.LooseGapWish("Aber von dir kommt ja nichts mehr, wie ich mich schlage zu meinem Vordermann, Abstände, Verluste, Gewinne."));
        }

        [Test]
        public void Gaps_more_often_halves_the_interval_and_is_stored()
        {
            string folder = System.IO.Path.Combine(Environment.GetEnvironmentVariable("RCCA_DATA_ROOT") ?? System.IO.Path.GetTempPath(),
                "monza-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                var prefs = new DriverRadioPreferences(folder);
                Assert.IsTrue(prefs.TryCommand(RobsWish, out string reply));
                StringAssert.Contains("Gespeichert", reply);
                Assert.AreEqual(.5, new DriverRadioPreferences(folder).IntervalScale("ahead"), 1e-9);
                Assert.AreEqual(.5, new DriverRadioPreferences(folder).IntervalScale("behind"), 1e-9);
                Assert.AreEqual(1, new DriverRadioPreferences(folder).IntervalScale("fuel"), 1e-9);
            }
            finally { System.IO.Directory.Delete(folder, true); }
        }

        [TestCase("warning-flag-0")] // Gelb! Nicht überholen.
        [TestCase("warning-flag-1")] // Gelb aufgehoben.
        [TestCase("warning-flag-2")] // Gelb voraus.
        [TestCase("warning-flag-3")] // Doppelt Gelb
        [TestCase("warning-rc-yellow-1")] // Gelb in Sektor 1.
        [TestCase("warning-rc-yellow-0")]
        [TestCase("warning-rc-dyellow-2")]
        [TestCase("warning-rc-green-3")] // Sektor 3 wieder grün.
        public void Sara_no_longer_calls_plain_yellow(string topic)
        {
            Assert.IsFalse(SaraFlagPolicy.Allows(topic, Race(), null, T, out string why));
            Assert.AreEqual("rob_no_plain_yellow", why);
        }

        [TestCase("warning-flag-6")] // Full Course Yellow
        [TestCase("warning-flag-7")] // Grün nach FCY
        [TestCase("warning-flag-4")] // Blau
        [TestCase("warning-flag-5")] // Schwarz
        [TestCase("warning-rc-fcy-pits")]
        [TestCase("warning-rc-fcy-last")] // Safety Car kommt rein
        [TestCase("warning-rc-red")]
        [TestCase("warning-rc-slippery")]
        [TestCase("race-hazard-ahead")] // langsamer oder stehender Wagen direkt voraus
        [TestCase("warning-hazard-far")] // Dreher/steht weiter voraus
        [TestCase("race-traffic-rear")]
        public void Other_safety_calls_still_come_at_once(string topic)
        {
            Assert.IsTrue(SaraFlagPolicy.Allows(topic, Race(), null, T, out _));
        }

        [Test]
        public void Incident_ahead_needs_a_stopped_or_crawling_car_not_a_brief_off()
        {
            var gs = Race();
            gs.OpponentData["off"] = new OpponentData { DriverRawName = "Carlos Marín", IsActive = true, Speed = 38, DistanceRoundTrack = 1150 };
            Assert.IsFalse(SaraFlagPolicy.Allows("warning-flag-8", gs, null, T, out string why), "kurz neben der Strecke, fährt weiter");
            Assert.AreEqual("incident_without_stopped_car_ahead", why);
            gs.OpponentData["stopped"] = new OpponentData { DriverRawName = "Amaury Duval", IsActive = true, Speed = 1, DistanceRoundTrack = 1200 };
            Assert.IsTrue(SaraFlagPolicy.Allows("warning-flag-8", gs, null, T, out _), "Auto steht 200 m voraus");
            var behind = Race();
            behind.OpponentData["stopped"] = new OpponentData { DriverRawName = "Amaury Duval", IsActive = true, Speed = 0, DistanceRoundTrack = 900 };
            Assert.IsFalse(SaraFlagPolicy.Allows("warning-flag-8", behind, null, T, out _), "hinter Rob ist keine Gefahr voraus");
        }

        [Test]
        public void Plain_yellow_can_be_switched_back_on()
        {
            SaraFlagPolicy.PlainYellowRadio = true;
            try { Assert.IsTrue(SaraFlagPolicy.Allows("warning-rc-yellow-1", Race(), null, T, out _)); }
            finally { SaraFlagPolicy.PlainYellowRadio = false; }
        }

        [Test]
        public void Stopped_car_far_ahead_is_called_without_the_word_yellow()
        {
            string text = YellowAheadWatch.Text(SaraFlagPolicy.PlainYellowRadio && true, "Ascari", 600, true);
            StringAssert.DoesNotContain("Gelb", text);
            StringAssert.Contains("Ascari", text);
            StringAssert.Contains("Auto steht", text);
        }

        private static GameStateData Race()
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 3; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.TrackDefinition = new TrackDefinition("Autodromo di Monza", 5778.8f);
            s.PositionAndMotionData.CarSpeed = 60; s.PositionAndMotionData.DistanceRoundTrack = 1000;
            return s;
        }
    }
}
