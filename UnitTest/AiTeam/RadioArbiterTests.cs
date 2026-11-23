using System;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Rob 23.11.2026: „Es muss eine grundsätzliche Priorisierung her … wann bei mehreren Sprüchen wer Prio hat. Wenn eine gewisse
    /// Zeit vergangen ist, muss der Spruch nicht gleich kommen, sondern warten. Oder ganz wegfallen.“
    /// RaceRoom Monza 22.11.: rund 56 Routineberichte eingestellt, 11 gesprochen; Wetter, Sprit, Pace und Stint nie.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class RadioArbiterTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 22, 20, 40, 0, DateTimeKind.Utc);

        [TestCase("warning-flag-6", "Safety")]       // Full Course Yellow
        [TestCase("warning-rc-fcy-last", "Safety")]  // Safety Car kommt rein
        [TestCase("warning-hazard-far", "Safety")]
        [TestCase("race-hazard-ahead", "Safety")]
        [TestCase("warning-flag-4", "Safety")]       // Blau
        [TestCase("warning-flag-5", "Critical")]     // Schwarz
        [TestCase("warning-penalty", "Critical")]
        [TestCase("pit-limit", "Critical")]
        [TestCase("damage-detected", "Critical")]
        [TestCase("engine-water", "Critical")]
        [TestCase("tyre-wear-warning", "Critical")]
        [TestCase("incident-points", "Critical")]
        [TestCase("position", "OwnRace")]
        [TestCase("judge-overtake", "OwnRace")]
        [TestCase("start-verdict", "OwnRace")]
        [TestCase("field-fastest", "OwnRace")]
        [TestCase("lap-on-pace", "OwnRace")]
        [TestCase("own-best-pace", "OwnRace")]
        [TestCase("ahead", "Fight")]
        [TestCase("behind", "Fight")]
        [TestCase("native-gap-front", "Fight")]
        [TestCase("gap-trend", "Fight")]
        [TestCase("status", "Routine")]
        [TestCase("fuel", "Routine")]
        [TestCase("weather", "Routine")]
        [TestCase("stint", "Routine")]
        [TestCase("race-summary", "Routine")]
        [TestCase("field-pit", "Routine")]
        [TestCase("corner-coaching", "Colour")]
        public void Every_call_has_a_racing_rank(string topic, string expected)
        {
            Assert.AreEqual(expected, RadioArbiter.Tier(topic, 3).ToString());
        }

        [Test]
        public void Routine_gains_rank_while_waiting_but_never_passes_its_own_race_or_safety()
        {
            Assert.AreEqual(300, RadioArbiter.Score(RadioTier.Routine, 0));
            Assert.AreEqual(400, RadioArbiter.Score(RadioTier.Routine, 20));
            Assert.AreEqual(550, RadioArbiter.Score(RadioTier.Routine, 600), "Obergrenze");
            Assert.Less(RadioArbiter.Score(RadioTier.Routine, 3600), RadioArbiter.Score(RadioTier.OwnRace, 0));
            Assert.Less(RadioArbiter.Score(RadioTier.Fight, 3600), RadioArbiter.Score(RadioTier.OwnRace, 0));
            Assert.AreEqual(200, RadioArbiter.Score(RadioTier.Colour, 600), "Farbe altert nicht – sie entfällt");
        }

        [Test]
        public void Praise_and_coaching_give_way_to_a_report_that_has_waited_twenty_seconds()
        {
            var a = new RadioArbiter();
            a.Note("fuel", RadioTier.Routine, T.AddSeconds(-25), T);
            Assert.IsTrue(a.ShouldYield("corner-coaching", RadioTier.Colour, T, T, out string to));
            Assert.AreEqual("fuel", to);
            var fresh = new RadioArbiter();
            fresh.Note("fuel", RadioTier.Routine, T, T);
            Assert.IsTrue(fresh.ShouldYield("corner-coaching", RadioTier.Colour, T, T, out _), "Routine steht immer über Farbe");
        }

        [Test]
        public void Own_race_news_never_waits_for_statistics_but_statistics_wait_for_news()
        {
            var a = new RadioArbiter();
            a.Note("weather", RadioTier.Routine, T.AddSeconds(-500), T);
            Assert.IsFalse(a.ShouldYield("position", RadioTier.OwnRace, T, T, out _));
            a.Note("position", RadioTier.OwnRace, T.AddSeconds(-1), T);
            Assert.IsTrue(a.ShouldYield("weather", RadioTier.Routine, T.AddSeconds(-500), T, out string to));
            Assert.AreEqual("position", to);
        }

        [Test]
        public void Safety_never_yields_and_duty_yields_only_to_safety()
        {
            var a = new RadioArbiter();
            a.Note("pit-limit", RadioTier.Critical, T, T);
            a.Note("position", RadioTier.OwnRace, T, T);
            Assert.IsFalse(a.ShouldYield("warning-flag-6", RadioTier.Safety, T, T, out _));
            Assert.IsFalse(a.ShouldYield("damage-detected", RadioTier.Critical, T, T, out _), "gleiche Stufe ordnet ihr Wächter");
            a.Note("race-hazard-ahead", RadioTier.Safety, T, T);
            Assert.IsTrue(a.ShouldYield("damage-detected", RadioTier.Critical, T, T, out string to));
            Assert.AreEqual("race-hazard-ahead", to);
            Assert.AreEqual("race-hazard-ahead", a.Next(T));
        }

        [Test]
        public void Configured_reports_never_wait_for_each_other()
        {
            // Nur ein eingestellter Bericht ist gleichzeitig unterwegs – der Abstand darf nicht auf den Rennstatus warten,
            // den der Plan erst nach ihm bilden kann.
            var a = new RadioArbiter();
            a.Note("status", RadioTier.Routine, T.AddSeconds(-90), T);
            Assert.IsFalse(a.ShouldYield("ahead", RadioTier.Fight, T, T, out _));
            Assert.IsTrue(a.ShouldYield("native-gap-front", RadioTier.Fight, T, T, out _), "der Abstand aus dem Zeitenmonitor schon");
        }

        [Test]
        public void A_call_its_watcher_stopped_offering_is_forgotten_so_nobody_waits_forever()
        {
            var a = new RadioArbiter();
            a.Note("position", RadioTier.OwnRace, T, T);
            Assert.IsTrue(a.ShouldYield("status", RadioTier.Routine, T, T.AddSeconds(3), out _));
            Assert.IsFalse(a.ShouldYield("status", RadioTier.Routine, T, T.AddSeconds(5), out _), "nicht erneut angeboten");
            a.Note("position", RadioTier.OwnRace, T, T.AddSeconds(6));
            a.Done("position");
            Assert.IsFalse(a.ShouldYield("status", RadioTier.Routine, T, T.AddSeconds(6), out _), "gesprochen");
            Assert.AreEqual(0, a.Count(T.AddSeconds(6)));
        }

        [Test]
        public void Waiting_time_is_kept_while_the_call_keeps_being_offered()
        {
            var a = new RadioArbiter();
            a.Note("fuel", RadioTier.Routine, T, T);
            a.Note("fuel", RadioTier.Routine, T.AddSeconds(30), T.AddSeconds(3));
            Assert.AreEqual(T, a.WaitingSince("fuel", T.AddSeconds(4)));
            Assert.AreEqual(T.AddSeconds(20), a.WaitingSince("weather", T.AddSeconds(20)), "wartet nicht");
        }

        [Test]
        public void News_goes_stale_reports_do_not()
        {
            Assert.IsTrue(RadioArbiter.Stale(RadioTier.OwnRace, T, T.AddSeconds(31), false), "„Platz gewonnen“ nach 31 s ist keine Nachricht mehr");
            Assert.IsFalse(RadioArbiter.Stale(RadioTier.OwnRace, T, T.AddSeconds(29), false));
            Assert.IsFalse(RadioArbiter.Stale(RadioTier.OwnRace, T, T.AddSeconds(60), true), "frisch gebildet");
            Assert.IsTrue(RadioArbiter.Stale(RadioTier.Colour, T, T.AddSeconds(21), false));
            Assert.IsTrue(RadioArbiter.Stale(RadioTier.Fight, T, T.AddSeconds(16), false));
            Assert.IsFalse(RadioArbiter.Stale(RadioTier.Routine, T, T.AddSeconds(600), false));
            Assert.IsFalse(RadioArbiter.Stale(RadioTier.Safety, T, T.AddSeconds(600), false), "Gefahr prüft ihre Gültigkeit selbst");
            Assert.IsFalse(RadioArbiter.Stale(RadioTier.Critical, T, T.AddSeconds(600), false));
        }

        [Test]
        public void Technical_spacing_follows_the_configured_intervals()
        {
            var q = new AiRadioSchedule(1);
            Assert.AreEqual(180, q.TechnicalSpacingFor(new AiRadioPreferences()), "Standard bleibt 3–4 Minuten (Rob 16.11.)");
            var monza = new AiRadioPreferences(); monza.Set("fuel", 120);
            Assert.AreEqual(90, q.TechnicalSpacingFor(monza), "Sprit alle 2 Minuten → Technik alle 90 s");
            var tight = new AiRadioPreferences(); foreach (var o in AiRadioPreferences.Options) tight.Set(o.Id, 30);
            Assert.AreEqual(60, q.TechnicalSpacingFor(tight), "nie mehrere Technikberichte pro Minute");
            Assert.AreEqual(0, new AiRadioSchedule(1) { TechnicalSpacingSeconds = 0 }.TechnicalSpacingFor(monza));
        }

        [Test]
        public void A_topic_late_by_its_whole_interval_no_longer_waits_for_the_technical_budget()
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 3; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            var prefs = new AiRadioPreferences();
            foreach (var o in AiRadioPreferences.Options) prefs.Set(o.Id, 0);
            prefs.Set("weather", 300); prefs.SetTolerance("weather", 0, 0);
            var q = new AiRadioSchedule(1); q.Observe(gs, T, prefs);
            Assert.IsFalse(q.OverdueByItsInterval("weather", T.AddSeconds(300 + 299), prefs));
            Assert.IsTrue(q.OverdueByItsInterval("weather", T.AddSeconds(300 + 300), prefs), "Monza: Wetter 7 Minuten zu spät, nie gesprochen");
            Assert.IsFalse(q.OverdueByItsInterval("fuel", T.AddSeconds(3600), prefs), "abgeschaltet");
        }

        [Test]
        public void The_most_overdue_report_goes_first_relative_to_its_interval()
        {
            Assert.Greater(AiRadioSchedule.Urgency("weather", 300, 300), AiRadioSchedule.Urgency("fuel", 10, 120));
            Assert.Greater(AiRadioSchedule.Urgency("ahead", 0, 120), AiRadioSchedule.Urgency("weather", 0, 300), "bei gleicher Verspätung zählt der Rennbezug");
        }
    }
}
