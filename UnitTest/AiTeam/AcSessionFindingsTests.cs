using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Assetto Corsa 20.11. (Nürburgring, BMW M3 GT2): Namen bei Gelb, Dreher, Ausfall und Serverabgang.</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class AcSessionFindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 20, 22, 33, 0, DateTimeKind.Utc);

        [SetUp]
        public void Setup() { RadioVariants.Deterministic = true; RaceTrafficWatch.ClearHazard(); }

        [TearDown]
        public void TearDown() { RadioVariants.Deterministic = false; RaceTrafficWatch.ClearHazard(); }

        private static GameStateData Race(int session = 9)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = session; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.ClassPosition = 4; s.SessionData.CompletedLaps = 2;
            return s;
        }

        private static OpponentData Car(string name, int position) =>
            new OpponentData { DriverRawName = name, ClassPosition = position, CompletedLaps = 2, IsActive = true };

        private static QueuedMessage NativeMessage(string path)
        {
            var q = (QueuedMessage)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(QueuedMessage));
            q.messageName = path; q.messageFolders = new System.Collections.Generic.List<string> { path }; return q;
        }

        [Test]
        public void Yellow_sector_local_and_double_yellow_name_the_car_involved()
        {
            var p = FlagWatch.Phrases;
            Assert.IsTrue(FlagWatch.NamesIncident(p[0]), "Gelb! Langsam");
            Assert.IsTrue(FlagWatch.NamesIncident(p[2]), "Gelb im Sektor");
            Assert.IsTrue(FlagWatch.NamesIncident(p[3]), "Doppelt Gelb");
            Assert.IsTrue(FlagWatch.NamesIncident(p[8]), "Unfall voraus");
            Assert.IsFalse(FlagWatch.NamesIncident(p[1]), "Gelb vorbei");
            Assert.IsFalse(FlagWatch.NamesIncident(p[4]), "Blaue Flagge");
            Assert.IsFalse(FlagWatch.NamesIncident(p[7]), "Grün");
        }

        [Test]
        public void Measured_hazard_ahead_names_the_yellow_for_twenty_seconds_in_the_same_session()
        {
            var gs = Race();
            Assert.IsNull(FlagWatch.IncidentName(gs, T), "ohne Beleg kein Name");
            RaceTrafficWatch.NoteHazard("minmos", 9, T);
            Assert.AreEqual("minmos", FlagWatch.IncidentName(gs, T.AddSeconds(5)));
            Assert.IsNull(FlagWatch.IncidentName(gs, T.AddSeconds(25)), "zu alt");
            Assert.IsNull(FlagWatch.IncidentName(Race(10), T.AddSeconds(5)), "andere Sitzung");
            RaceTrafficWatch.NoteHazard(" ", 9, T.AddSeconds(6));
            Assert.AreEqual("minmos", FlagWatch.IncidentName(gs, T.AddSeconds(7)), "leerer Name überschreibt nichts");
        }

        [Test]
        public void Game_flag_and_crew_chief_yellow_are_one_call_not_two()
        {
            var gs = Race();
            var w = new LiveWarningDispatcher();
            w.Offer("warning-flag-2", FlagWatch.Phrases[2], TeamMember.Sara, 10, gs, T, live => true, "mapped_flag_transition");
            Assert.IsTrue(w.Capture(NativeMessage("flags/yellow_flag"), gs, T));
            w.Observe(gs, T);
            var first = w.Take(gs, T, false, m => true);
            Assert.IsNotNull(first);
            Assert.AreEqual("warning-flag-2", first.Topic);
            Assert.IsNull(w.Take(gs, T, false, m => true), "das zweite Gelb derselben Phase entfällt");

            first.MarkPlaybackStarted(T);
            w.Observe(gs, T.AddSeconds(1));
            w.Offer("warning-flag-3", FlagWatch.Phrases[3], TeamMember.Sara, 10, gs, T.AddSeconds(2), live => true, "mapped_flag_transition");
            var dbl = w.Take(gs, T.AddSeconds(2), false, m => true);
            Assert.IsNotNull(dbl, "Doppelt Gelb ist eine neue Lage");
            Assert.AreEqual("warning-flag-3", dbl.Topic);

            var later = new LiveWarningDispatcher();
            later.Offer("warning-flag-2", FlagWatch.Phrases[2], TeamMember.Sara, 10, gs, T, live => true, "mapped_flag_transition");
            var spoken = later.Take(gs, T, false, m => true); spoken.MarkPlaybackStarted(T); later.Observe(gs, T.AddSeconds(1));
            later.Offer("warning-flag-0", FlagWatch.Phrases[0], TeamMember.Sara, 10, gs, T.AddSeconds(12), live => true, "mapped_flag_transition");
            Assert.IsNotNull(later.Take(gs, T.AddSeconds(12), false, m => true), "nach 10 s ist das lokale Gelb wieder eine eigene Ansage");
        }

        [Test]
        public void Crew_chief_yellow_carries_the_name_of_the_car_involved()
        {
            var gs = Race();
            var w = new LiveWarningDispatcher { IncidentName = (g, n) => "minmos" };
            Assert.IsTrue(w.Capture(NativeMessage("flags/yellow_flag"), gs, T));
            w.Observe(gs, T);
            var call = w.Take(gs, T, false, m => true);
            Assert.IsNotNull(call);
            Assert.AreEqual(RaceTrafficWatch.NameClipText("minmos"), call.OpponentNameClip);

            var anonymous = new LiveWarningDispatcher();
            Assert.IsTrue(anonymous.Capture(NativeMessage("flags/yellow_flag"), gs, T));
            anonymous.Observe(gs, T);
            Assert.IsNull(anonymous.Take(gs, T, false, m => true).OpponentNameClip, "ohne erlaubte Namen kein Name");
        }

        [Test]
        public void Opponent_leaving_the_server_mid_race_is_announced_by_name_after_confirmation()
        {
            var before = Race(); var after = Race();
            before.OpponentData["car-7"] = Car("TheDarkSide", 9);
            before.OpponentData["car-8"] = Car("Bananekk", 5);
            after.OpponentData["car-8"] = Car("Bananekk", 5);
            var events = new NativeFieldEvents();
            events.CaptureDepartures(before, after, T);
            events.Observe(after, T, null, true);
            Assert.IsNull(events.Take(after, T, false, true, false), "erst nach der Bestätigung");

            var later = Race(); later.OpponentData["car-8"] = Car("Bananekk", 5); later.Now = T.AddSeconds(3.5);
            events.CaptureDepartures(after, later, T.AddSeconds(3.5));
            events.Observe(later, T.AddSeconds(3.5), null, true);
            var call = events.Take(later, T.AddSeconds(3.5), false, true, false);
            Assert.IsNotNull(call, "auch ein Gegner weit hinter Rob wird genannt");
            Assert.AreEqual("field-left", call.Topic);
            StringAssert.StartsWith("TheDarkSide", call.Text);
            StringAssert.Contains("Server", call.Text);

            var back = Race(); back.OpponentData["car-7"] = Car("TheDarkSide", 9);
            Assert.IsFalse(call.IsValid(back, T.AddSeconds(4)), "ist er wieder da, entfällt die Meldung");
        }

        [Test]
        public void Short_dropout_inactive_flicker_and_finish_are_no_departure()
        {
            var before = Race(); before.OpponentData["car-7"] = Car("TheDarkSide", 9);
            var gone = Race();
            var events = new NativeFieldEvents();
            events.CaptureDepartures(before, gone, T);
            var back = Race(); back.OpponentData["car-7"] = Car("TheDarkSide", 9);
            events.CaptureDepartures(gone, back, T.AddSeconds(1));
            var still = Race(); still.OpponentData["car-7"] = Car("TheDarkSide", 9);
            events.CaptureDepartures(back, still, T.AddSeconds(5));
            events.Observe(still, T.AddSeconds(5), null, true);
            Assert.IsNull(events.Take(still, T.AddSeconds(5), false, true, false), "kurz weg und wieder da ist kein Abgang");

            var finished = Race(); finished.SessionData.LeaderHasFinishedRace = true;
            var afterFlag = new NativeFieldEvents();
            afterFlag.CaptureDepartures(before, finished, T);
            afterFlag.CaptureDepartures(finished, finished, T.AddSeconds(5));
            afterFlag.Observe(finished, T.AddSeconds(5), null, true);
            Assert.IsNull(afterFlag.Take(finished, T.AddSeconds(5), false, true, false), "nach der Zielflagge gehen alle, das ist keine Meldung");
        }

        [Test]
        public void Departure_text_has_five_variants_in_three_languages_with_the_name_first()
        {
            var before = Race(); before.OpponentData["car-7"] = Car("TheDarkSide", 9);
            RadioVariants.Deterministic = false;
            try
            {
                foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
                {
                    RadioLanguage.Current = lang;
                    RadioVariants.Seed(5);
                    var texts = new System.Collections.Generic.HashSet<string>();
                    for (int i = 0; i < 60; i++)
                    {
                        var events = new NativeFieldEvents();
                        var gone = Race();
                        events.CaptureDepartures(before, gone, T);
                        gone.Now = T.AddSeconds(4);
                        events.CaptureDepartures(gone, gone, T.AddSeconds(4));
                        events.Observe(gone, T.AddSeconds(4), null, true);
                        var call = events.Take(gone, T.AddSeconds(4), false, true, false);
                        Assert.IsNotNull(call, lang);
                        StringAssert.StartsWith("TheDarkSide ", call.Text);
                        texts.Add(call.Text);
                    }
                    Assert.AreEqual(5, texts.Count, lang + ": fünf Formulierungen");
                }
            }
            finally { RadioLanguage.Current = RadioLanguage.German; RadioVariants.Deterministic = true; }
        }

        [Test]
        public void Retirement_is_named_even_when_the_car_is_far_from_rob()
        {
            var gs = Race();
            gs.OpponentData["car-3"] = Car("Akos Forgacs", 12);
            gs.retriedDriverNames["Akos Forgacs"] = null;
            var events = new NativeFieldEvents();
            events.Capture(new NativeFieldFact("retired", "car-3", "Akos Forgacs", null, "retirement", gs, gs.OpponentData["car-3"], 0));
            events.Observe(gs, T, null, true);
            var call = events.Take(gs, T, false, true, false);
            Assert.IsNotNull(call, "Platz 12 gegen Platz 4: früher stillschweigend verworfen");
            StringAssert.StartsWith("Akos Forgacs", call.Text);
        }

        [TestCase("ks_nurburgring:layout_gp_b", "Nürburgring")]
        [TestCase("ks_red_bull_ring:layout_gp", "Red Bull Ring")]
        [TestCase("ks_laguna_seca", "Laguna Seca")]
        [TestCase("spa", "Spa")]
        [TestCase("Monza:Monza_2020", "Monza")]
        [TestCase("Brands Hatch Indy", "Brands Hatch Indy")]
        public void Ac_track_ids_are_spoken_as_track_names(string id, string spoken)
        {
            Assert.AreEqual(spoken, SessionGreetingDirector.SpokenTrack(id));
        }

        [TestCase("Was hat so viel Temperatur gehabt?")]
        [TestCase("Welcher Reifen ist so heiß?")]
        [TestCase("Which tyre is so hot?")]
        [TestCase("¿Qué temperatura tienen los neumáticos?")]
        public void Temperature_questions_get_the_live_data_tools(string question)
        {
            Assert.IsTrue(AiTeamService.NeedsTools(question, Race()), question);
        }

        [Test]
        public void Name_budget_is_per_session_and_covers_a_full_field()
        {
            Assert.GreaterOrEqual(AiTeamService.OpponentWarmBudget, 40, "ein volles Feld, nicht 24 Namen für die ganze Programmlaufzeit");
            Assert.Greater(AiTeamService.OpponentWarmBudgetParked, 8);
        }
    }
}
