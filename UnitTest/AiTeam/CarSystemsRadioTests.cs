using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// DRS, Push-to-Pass, Fahrerwechsel und Batterie (Rob 23.11.2026): Crew Chief erkennt, die Crew sagt es mit mindestens vier
    /// Formulierungen in jeder Sprache. Ben spricht Fahrerwechsel und das DRS-Fenster zum Gegner, Mik Technik und Energie.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class CarSystemsRadioTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 20, 0, 0, DateTimeKind.Utc);
        private bool wasDeterministic;

        [SetUp]
        public void SetUp()
        {
            RadioLanguage.Current = RadioLanguage.German;
            wasDeterministic = RadioVariants.Deterministic;
            PhraseMemory.Clock = () => T;
            PhraseMemory.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            RadioVariants.Deterministic = wasDeterministic;
            RadioLanguage.Current = RadioLanguage.German;
            PhraseMemory.Clock = () => DateTime.UtcNow;
            PhraseMemory.Clear();
        }

        private static GameStateData Race(DateTime at)
        {
            var gs = new GameStateData(at.Ticks);
            var s = gs.SessionData;
            s.SessionId = 3; s.SessionType = SessionType.Race; s.SessionPhase = SessionPhase.Green;
            s.ClassPosition = 4; s.OverallPosition = 4; s.CompletedLaps = 12;
            s.TrackDefinition = new TrackDefinition("Spa", 7004f);
            gs.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            gs.Now = at; gs.SourceTelemetryUtc = at;
            return gs;
        }

        private static void Live(GameStateData gs, DateTime at) { gs.Now = at; gs.SourceTelemetryUtc = at; }

        [Test]
        public void Every_car_system_call_has_at_least_four_wordings_in_every_language()
        {
            RadioVariants.Deterministic = false;
            foreach (var language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                foreach (string kind in NativeCarSystems.Kinds())
                {
                    var options = NativeCarSystems.Options(kind);
                    Assert.IsNotNull(options, language + " " + kind);
                    Assert.That(options.Distinct().Count(), Is.GreaterThanOrEqualTo(4), language + " " + kind);
                    var seen = Enumerable.Range(0, 60).Select(_ => NativeCarSystems.Text(kind)).Distinct().Count();
                    Assert.That(seen, Is.GreaterThanOrEqualTo(4), language + " " + kind);
                }
            }
        }

        [Test]
        public void Crew_chief_folders_are_read_as_the_matching_car_system_call()
        {
            var gs = Race(T);
            gs.OvertakingAids.PushToPassActivationsRemaining = 7;
            gs.OvertakingAids.DrsActivationsRemaining = 4;
            Assert.AreEqual("ptp-ready:7", NativeCarSystems.Classify(new[] { "overtaking_aids/push_to_pass_now_available", "numbers/7", "overtaking_aids/activations_remaining" }, "compound_message:push_to_pass_remaining", gs));
            Assert.AreEqual("ptp-left:1", NativeCarSystems.Classify(new[] { "overtaking_aids/one_activation_remaining" }, null, gs));
            Assert.AreEqual("drs-left:4", NativeCarSystems.Classify(new[] { "numbers/4", "overtaking_aids/drs_activations_remaining" }, null, gs));
            Assert.AreEqual("drs-left:1", NativeCarSystems.Classify(new[] { "numbers/1", "overtaking_aids/one_drs_activation_remaining" }, null, gs));
            Assert.AreEqual("drs-behind", NativeCarSystems.Classify(null, "overtaking_aids/guy_behind_has_drs", gs), "einfache Meldung: der Name ist der Ordner");
            Assert.AreEqual("stint-min:10", NativeCarSystems.Classify(new[] { "driver_swaps/10_minutes_left_in_stint" }, null, gs));
            Assert.AreEqual("stint-final", NativeCarSystems.Classify(new[] { "driver_swaps/pit_this_lap_driver_change_no_more_stints" }, null, gs));
            Assert.AreEqual("batt-out:box", NativeCarSystems.Classify(new[] { "battery/about_to_run_out", "mandatory_pit_stops/pit_this_lap" }, null, gs));
            Assert.AreEqual("batt-laps:3", NativeCarSystems.Classify(new[] { "battery\\three_laps_battery" }, null, gs));
            Assert.IsNull(NativeCarSystems.Classify(new[] { "battery/low_battery", "battery/percent_remaining" }, null, gs), "Antwort auf eine Frage ist keine Erkennung");
            Assert.IsNull(NativeCarSystems.Classify(new[] { "lap_counter/two_to_go" }, null, gs));

            Assert.AreEqual(TeamMember.Ben, NativeCarSystems.Member("stint-box"));
            Assert.AreEqual(TeamMember.Ben, NativeCarSystems.Member("drs-reach:second"));
            Assert.AreEqual(TeamMember.Mik, NativeCarSystems.Member("batt-low"));
            Assert.AreEqual(TeamMember.Mik, NativeCarSystems.Member("ptp-left:3"));
            Assert.AreEqual("warning-car-stint-boxnow", NativeCarSystems.Topic("stint-boxnow"));
            Assert.AreEqual("car-drs-on", NativeCarSystems.Topic("drs-on"));
        }

        [Test]
        public void A_detected_driver_change_is_said_by_Ben_even_while_the_radio_is_busy()
        {
            RadioVariants.Deterministic = true;
            var gs = Race(T);
            var events = new NativeCarSystemEvents();
            Assert.IsTrue(events.Capture(new[] { "driver_swaps/pit_this_lap_for_driver_change" }, "driver_swaps/pit_this_lap_for_driver_change", gs, T));
            DateTime now = T.AddMilliseconds(300); Live(gs, now);
            events.Observe(gs, now);
            Assert.IsNull(events.Take(gs, now, true, true, _ => true), "Sara oder dringender Funk geht vor");
            var call = events.Take(gs, now, false, true, _ => true);
            Assert.IsNotNull(call, "Box zum Fahrerwechsel wartet nicht auf freien Funk");
            Assert.AreEqual(TeamMember.Ben, call.Member);
            Assert.AreEqual("warning-car-stint-box", call.Topic);
            StringAssert.Contains("Fahrerwechsel", call.Text);
            Assert.AreEqual(12, call.CompletedLaps, "„diese Runde“ gilt nur in dieser Runde");
        }

        [Test]
        public void Routine_calls_wait_for_a_free_radio_and_drop_when_the_count_changed()
        {
            RadioVariants.Deterministic = true;
            var gs = Race(T);
            gs.OvertakingAids.PushToPassActivationsRemaining = 5;
            var events = new NativeCarSystemEvents();
            events.Capture(new[] { "overtaking_aids/five_ptp_activations_remaining" }, null, gs, T);
            DateTime now = T.AddMilliseconds(200); Live(gs, now);
            events.Observe(gs, now);
            Assert.IsNull(events.Take(gs, now, false, true, _ => true), "Routine wartet auf freien Funk");
            gs.OvertakingAids.PushToPassActivationsRemaining = 4;
            now = now.AddSeconds(2); Live(gs, now);
            events.Observe(gs, now);
            Assert.IsNull(events.Take(gs, now, false, false, _ => true), "die Zahl stimmt nicht mehr, also nicht sagen");
            Assert.AreEqual(0, events.PendingCount);
        }

        [Test]
        public void The_same_call_is_not_repeated_right_away_but_a_new_count_is()
        {
            RadioVariants.Deterministic = true;
            var gs = Race(T);
            var events = new NativeCarSystemEvents();
            events.Capture(new[] { "battery/low_battery" }, null, gs, T);
            DateTime now = T.AddMilliseconds(200); Live(gs, now);
            events.Observe(gs, now);
            var call = events.Take(gs, now, false, false, _ => true);
            Assert.IsNotNull(call);
            Assert.AreEqual(TeamMember.Mik, call.Member);
            call.MarkPlaybackStarted(now);
            events.Observe(gs, now);

            now = now.AddSeconds(10); Live(gs, now);
            events.Capture(new[] { "battery/low_battery" }, null, gs, now);
            events.Observe(gs, now);
            Assert.IsNull(events.Take(gs, now, false, false, _ => true), "eben erst gesagt");

            events.Capture(new[] { "battery/three_laps_battery" }, null, gs, now);
            events.Observe(gs, now);
            var laps = events.Take(gs, now, false, false, _ => true);
            Assert.IsNotNull(laps, "neue Lage, neue Meldung");
            StringAssert.Contains("3", laps.Text);
        }

        [Test]
        public void Nothing_is_said_from_an_old_or_foreign_session()
        {
            var gs = Race(T);
            var events = new NativeCarSystemEvents();
            events.Capture(new[] { "overtaking_aids/drs_enabled" }, null, gs, T);
            gs.SessionData.SessionId = 4;
            DateTime now = T.AddMilliseconds(200); Live(gs, now);
            events.Observe(gs, now);
            Assert.AreEqual(0, events.PendingCount);
            events.Capture(new[] { "overtaking_aids/drs_enabled" }, null, gs, now);
            now = now.AddSeconds(5); Live(gs, now);
            events.Observe(gs, now);
            Assert.AreEqual(0, events.PendingCount, "zu alt");
        }
    }
}
