using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Le Mans Ultimate, Sebring 23.11. (19 Autos, 20 Minuten): Bis 22:35 funkte Ben so viel wie vor zwei Wochen, dann schaltete
    /// „Weniger Infos bitte“ auf Ruhig, „Volle Informationen“ wurde nicht erkannt, und Ruhig sperrte auch Überholmanöver, Restzeit
    /// und Feldnachrichten. Diese Tests sichern die Antworten darauf.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class SebringRadioTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 21, 38, 0, DateTimeKind.Utc);
        private string wasLanguage;

        [SetUp]
        public void SetUp() { wasLanguage = RadioLanguage.Current; RadioLanguage.Current = RadioLanguage.German; }

        [TearDown]
        public void TearDown() { RadioLanguage.Current = wasLanguage; }

        [TestCase("Volle Informationen."), TestCase("Volle Informationen, insbesondere Hintermann."), TestCase("Ich möchte volle Informationen."),
         TestCase("Alle Informationen bitte"), TestCase("Wieder alles"), TestCase("Volles Programm"), TestCase("full info please"),
         TestCase("toda la información")]
        public void Full_information_is_understood(string said)
        {
            Assert.AreEqual(RadioAmount.MuchMore, RadioAmountCommand.Parse(said));
        }

        [TestCase("Infos wieder an"), TestCase("Funk wieder an"), TestCase("Wieder Infos bitte"), TestCase("radio back on")]
        public void Radio_back_on_is_normal(string said)
        {
            Assert.AreEqual(RadioAmount.Normal, RadioAmountCommand.Parse(said));
        }

        [Test]
        public void Less_info_is_one_step_down_not_silence()
        {
            Assert.AreEqual(RadioAmount.More, RadioAmountCommand.Parse("Weniger Infos bitte.", RadioAmount.MuchMore), "Sebring 22:35: aus Viel mehr Infos");
            Assert.AreEqual(RadioAmount.Normal, RadioAmountCommand.Parse("Weniger Infos bitte.", RadioAmount.More));
            Assert.AreEqual(RadioAmount.Quiet, RadioAmountCommand.Parse("Weniger Infos bitte.", RadioAmount.Normal));
            Assert.AreEqual(RadioAmount.Quiet, RadioAmountCommand.Parse("Ruhe bitte", RadioAmount.MuchMore), "Ruhe bleibt Ruhe");
            Assert.AreEqual(RadioAmount.Normal, RadioAmountCommand.Parse("less radio", RadioAmount.More));
        }

        [TestCase("Ben, kommt von dir nichts mehr?"), TestCase("Keine Reaktion auf meine Überholmanöver"),
         TestCase("Keine Ansagen mehr über die Rennzeit, das ist schlecht"), TestCase("Warum hast du mir nicht gesagt, dass ich auf der letzten Runde bin?"),
         TestCase("Why didn't you tell me it was the last lap?"), TestCase("¿Por qué no me avisas?")]
        public void Complaints_about_silence_bring_the_radio_back_when_quiet(string said)
        {
            Assert.IsTrue(RadioAmountCommand.ComplainsAboutSilence(said, RadioAmount.Quiet));
            Assert.IsFalse(RadioAmountCommand.ComplainsAboutSilence(said, RadioAmount.Normal), "bei normalem Funk ist es eine Frage, kein Befehl");
            Assert.GreaterOrEqual(Enumerable.Range(0, 4).Select(RadioAmountCommand.SilenceReply).Distinct().Count(), 4);
        }

        [Test]
        public void Ordinary_questions_are_no_complaint()
        {
            foreach (var q in new[] { "Wie weit ist der Vordermann?", "Wer ist hinter mir?", "Wie viel Sprit habe ich noch?" })
                Assert.IsFalse(RadioAmountCommand.ComplainsAboutSilence(q, RadioAmount.Quiet), q);
        }

        private static GameStateData TimedRace(float totalSeconds, float leftSeconds, int position = 6)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 4; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.SessionHasFixedTime = true; s.SessionData.SessionTotalRunTime = totalSeconds; s.SessionData.SessionTimeRemaining = leftSeconds;
            s.SessionData.ClassPosition = position;
            return s;
        }

        [Test]
        public void Time_left_is_called_at_ten_five_and_two_minutes_in_a_twenty_minute_race()
        {
            var watch = new TimeLeftWatch();
            var heard = new List<string>();
            var now = T;
            for (float left = 1200; left > 0; left -= 1, now = now.AddSeconds(1))
            {
                var gs = TimedRace(1200, left);
                watch.Observe(gs, now);
                var call = watch.Take(gs, now, false, true);
                if (call != null) { heard.Add(call.Text); call.MarkPlaybackStarted(now); }
            }
            TestContext.WriteLine(string.Join("\n", heard));
            Assert.AreEqual(4, heard.Count, "15, 10, 5 und 2 Minuten (15 liegt im letzten Dreiviertel eines 20-Minuten-Rennens)");
            StringAssert.Contains("15", heard[0]);
            StringAssert.Contains("10", heard[1]);
            StringAssert.Contains("zwei", heard[3].ToLowerInvariant());
            Assert.IsTrue(heard.All(h => h.Contains("6")), "mit Platz");
            Assert.AreEqual(heard.Count, heard.Distinct().Count());
        }

        [Test]
        public void Time_left_skips_marks_too_close_to_the_start_and_missed_marks()
        {
            var watch = new TimeLeftWatch();
            var gs = TimedRace(900, 899);
            watch.Observe(gs, T);
            Assert.IsNull(watch.Take(gs, T, false, true), "kein „noch 15 Minuten“ in einem 15-Minuten-Rennen");
            var late = new TimeLeftWatch();
            var joined = TimedRace(1800, 400);
            late.Observe(joined, T);
            Assert.IsNull(late.Take(joined, T, false, true), "nach dem Einstieg bei 6:40 keine verspätete 10-Minuten-Ansage");
            var two = TimedRace(1800, 119);
            late.Observe(two, T.AddMinutes(5));
            Assert.IsNotNull(late.Take(two, T.AddMinutes(5), false, true));
        }

        [Test]
        public void Time_left_passes_the_radio_load_like_an_own_result()
        {
            Assert.IsTrue(RadioLoad.OwnResult("time-left"));
        }

        private static FieldCarView Car(string name, float gapM, CarMotion motion) =>
            new FieldCarView { Name = name, GapM = gapM, GapS = gapM / 50f, Motion = motion, MotionSinceUtc = T.AddSeconds(-1) };

        [Test]
        public void A_multi_car_crash_behind_is_one_sentence_with_all_names()
        {
            var player = new FieldCarView { Name = "Rob", IsPlayer = true, GapM = 0, GapS = 0 };
            var view = new FieldView
            {
                Utc = T, Session = FieldSession.Race, Player = player,
                Cars = new List<FieldCarView> { Car("Cyril Jacob", -60, CarMotion.Stopped), Car("Karabiner Karl", -90, CarMotion.Spun),
                    Car("Maxim Kharuk", -120, CarMotion.Stopped), player }
            };
            var incident = RearIncidentWatch.Nearest(view, T);
            Assert.AreEqual("Cyril Jacob", incident.Name);
            CollectionAssert.AreEqual(new[] { "Karabiner Karl", "Maxim Kharuk" }, incident.Others);
            var watch = new RearIncidentWatch();
            string text = watch.CrashText(new[] { incident.Name }.Concat(incident.Others).ToList());
            TestContext.WriteLine(text);
            StringAssert.Contains("Cyril Jacob, Karabiner Karl und Maxim Kharuk", text);
            Assert.GreaterOrEqual(Enumerable.Range(0, 4).Select(_ => watch.CrashText(new[] { "A", "B" })).Distinct().Count(), 4);
        }

        [Test]
        public void Who_is_with_an_unknown_name_goes_to_the_model()
        {
            Assert.IsTrue(LocalAnswers.MentionsUnknownName("Wer ist dieser Wilcox hinter mir?"));
            Assert.IsFalse(LocalAnswers.MentionsUnknownName("Wer ist hinter mir?"));
            Assert.IsFalse(LocalAnswers.MentionsUnknownName("Wer ist der Hintermann?"));
        }

        [Test]
        public void Rival_top_speed_waits_in_a_fight_instead_of_being_dropped()
        {
            var x = new RaceSituation { Utc = T, SessionId = 4, Phase = RacePhase.Middle, Attention = RaceAttention.Battle, Focus = RaceFocus.Defend };
            var call = new ObservedRaceCall { Topic = "field-topspeed", Text = "Auf der Geraden ist Bartosz mit 253 km/h vorn.", SessionId = 4,
                SessionType = SessionType.Race, Priority = 2, DetectedUtc = T };
            Assert.AreNotEqual(MindVerdict.Drop, RaceMind.Judge(x, call, T, out string why), why);
        }

        [Test]
        public void A_silent_spotter_names_its_reason()
        {
            Assert.AreEqual("ready_to_start", SpotterStartCheck.Reason(true, false, true, true, false, false, true, false));
            Assert.AreEqual("realistic_mode_on_road_course", SpotterStartCheck.Reason(true, false, false, true, true, false, true, false));
            Assert.AreEqual("spotter_switched_off_in_settings", SpotterStartCheck.Reason(true, false, false, false, false, false, true, false));
            Assert.AreEqual("crew_or_sara_switched_off", SpotterStartCheck.Reason(true, false, true, true, false, false, false, false));
            Assert.AreEqual("spotter_off_for_this_session", SpotterStartCheck.Reason(true, false, false, true, false, false, true, false));
            Assert.AreEqual("no_spotter_for_this_game", SpotterStartCheck.Reason(false, false, true, true, false, false, true, false));
            SpotterStartCheck.Reset();
            Assert.AreEqual("running", SpotterStartCheck.Changed("running"));
            Assert.IsNull(SpotterStartCheck.Changed("running"), "nur Änderungen ins Protokoll");
            SpotterStartCheck.Reset();
        }

        [Test]
        public void A_faulty_game_tick_is_logged_and_crew_chief_keeps_running()
        {
            TickGuard.Reset();
            var gs = TimedRace(1200, 600);
            gs.SessionData.TrackDefinition = null;
            Exception boom;
            try { object o = null; o.ToString(); boom = null; } catch (Exception e) { boom = e; }
            for (int i = 0; i < 1000; i++) Assert.IsTrue(TickGuard.Survive(boom, gs, null));
            Assert.AreEqual(1000, TickGuard.Consecutive);
            TickGuard.Ok();
            Assert.AreEqual(0, TickGuard.Consecutive);
            string nulls = TickGuard.NullFields(gs, null);
            StringAssert.Contains("current.SessionData.TrackDefinition", nulls);
            StringAssert.Contains("previous", nulls);
            StringAssert.Contains("Race/Green", nulls);
            TickGuard.Reset();
        }

        [Test]
        public void An_older_game_loop_ends_when_a_new_start_begins()
        {
            Assert.IsTrue(CrewChief.LoopIsCurrent(true, 3, 3));
            Assert.IsFalse(CrewChief.LoopIsCurrent(true, 2, 3), "LMU 24.11.: alter Zyklus nach automatischem Neustart");
            Assert.IsFalse(CrewChief.LoopIsCurrent(false, 3, 3));
        }

        [Test]
        public void Race_debrief_names_the_pit_stop()
        {
            var player = new FieldCarView { Name = "Rob", IsPlayer = true, PlaceClass = 6, Place = 6, ClassId = 1, CompletedLaps = 18 };
            var winner = new FieldCarView { Name = "Bartosz Jerzykiewicz", PlaceClass = 1, Place = 1, ClassId = 1, CompletedLaps = 18 };
            var view = new FieldView { Utc = T, Session = FieldSession.Race, Player = player, Cars = new List<FieldCarView> { winner, player } };
            var chronicle = new List<ChronicleEntry> { new ChronicleEntry { Kind = ChronicleKind.Grid, Position = 2 },
                new ChronicleEntry { Kind = ChronicleKind.OwnPit, Lap = 9 } };
            string text = SessionDebrief.ComposeRace(view, "Rob", chronicle, 0);
            TestContext.WriteLine(text);
            StringAssert.Contains("Ein Boxenstopp", text);
            StringAssert.Contains("Bartosz Jerzykiewicz", text);
        }
    }
}
