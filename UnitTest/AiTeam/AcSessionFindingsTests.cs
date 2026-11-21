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

        private bool wasDeterministic;
        private string wasLanguage;

        [SetUp]
        public void Setup()
        {
            wasDeterministic = RadioVariants.Deterministic; wasLanguage = RadioLanguage.Current;
            RadioVariants.Deterministic = true; RadioLanguage.Current = RadioLanguage.German; RaceTrafficWatch.ClearHazard();
        }

        [TearDown]
        public void TearDown() { RadioVariants.Deterministic = wasDeterministic; RadioLanguage.Current = wasLanguage; RaceTrafficWatch.ClearHazard(); }

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
        public void Give_back_after_passing_under_yellow_reaches_the_crew_through_the_voice_filter()
        {
            var gs = Race(); gs.FlagData.numCarsPassedIllegally = 1;
            var w = new LiveWarningDispatcher();
            Assert.IsTrue(w.Capture(NativeMessage("flags/give_one_position_back_first_warning"), gs, T));
            w.Observe(gs, T);
            var call = w.Take(gs, T, false, m => true);
            Assert.IsNotNull(call);
            Assert.AreEqual(TeamMember.Sara, call.Member);
            Assert.AreEqual(9, call.Priority);
            StringAssert.Contains("den Platz zurück", call.Text);
            gs.FlagData.numCarsPassedIllegally = 0;
            Assert.IsFalse(call.IsValid(gs, T.AddSeconds(1)), "schon zurückgegeben: Meldung entfällt");

            var two = Race(); two.FlagData.numCarsPassedIllegally = 2;
            var w2 = new LiveWarningDispatcher();
            Assert.IsTrue(w2.Capture(NativeMessage("flags/give_positions_back_first_warning_intro"), two, T));
            w2.Observe(two, T);
            StringAssert.Contains("2 Plätze", w2.Take(two, T, false, m => true).Text);

            var done = Race();
            var w3 = new LiveWarningDispatcher();
            Assert.IsTrue(w3.Capture(NativeMessage("flags/give_positions_back_completed"), done, T));
            w3.Observe(done, T);
            var ok = w3.Take(done, T, false, m => true);
            Assert.AreEqual(TeamMember.Ben, ok.Member);
            CollectionAssert.Contains(NativeRaceControl.Options("giveback-done"), ok.Text);
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.AreEqual(5, NativeRaceControl.Options("giveback:1").Distinct().Count(), lang);
                Assert.AreEqual(4, NativeRaceControl.Options("giveback-done").Distinct().Count(), lang);
                Assert.IsTrue(NativeRaceControl.Options("giveback:3").All(t => char.IsUpper(t[0])), lang);
            }
        }

        [Test]
        public void Give_back_names_the_real_cause_cut_or_yellow()
        {
            // Monza 21.11.: Rob überholte beim Abkürzen, Sara sagte „Überholen unter Gelb“ – er erkannte die Strafe nicht.
            var cut = Race(); cut.FlagData.numCarsPassedIllegally = 2; cut.SessionData.CurrentLapIsValid = false;
            string kind = NativeRaceControl.Resolve("giveback:n", cut);
            Assert.AreEqual("giveback:2:cut", kind);
            foreach (var t in NativeRaceControl.Options(kind)) { StringAssert.Contains("2 Plätze", t); StringAssert.DoesNotContain("Gelb", t); }
            Assert.AreEqual("warning-rc-giveback-2", NativeRaceControl.Topic(kind));
            Assert.AreEqual(9, NativeRaceControl.Priority(kind));
            Assert.IsTrue(NativeRaceControl.StillCurrent(kind, cut));

            var yellow = Race(); yellow.FlagData.numCarsPassedIllegally = 1; yellow.FlagData.isLocalYellow = true; yellow.SessionData.CurrentLapIsValid = false;
            kind = NativeRaceControl.Resolve("giveback:1", yellow);
            Assert.AreEqual("giveback:1:yellow", kind);
            foreach (var t in NativeRaceControl.Options(kind)) StringAssert.Contains("Gelb", t);

            var unknown = Race(); unknown.FlagData.numCarsPassedIllegally = 1; unknown.SessionData.CurrentLapIsValid = true;
            kind = NativeRaceControl.Resolve("giveback:1", unknown);
            Assert.AreEqual("giveback:1:rc", kind);
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                foreach (var k in new[] { "giveback:1:cut", "giveback:3:rc", "giveback:1:yellow", "giveback:2:cut:12" })
                {
                    var options = NativeRaceControl.Options(k);
                    Assert.AreEqual(5, options.Distinct().Count(), lang + " " + k);
                    Assert.IsTrue(options.All(t => char.IsUpper(t[0])), lang + " " + k);
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
            StringAssert.EndsWith("Dafür hast du noch 12 Sekunden.", NativeRaceControl.Options("giveback:2:cut:12")[0]);
        }

        [Test]
        public void Ac_native_extras_are_only_reported_while_fresh()
        {
            var saved = AcNativeExtras.Latest;
            try
            {
                AcNativeExtras.Latest = new AcNativeExtras { Utc = T, TyreDirt = new[] { 0f, 0f, 1.5f, 2f }, TyreCore = new float[4], BrakeTemp = new float[4],
                    TyreWear = new float[4], PerformanceMeter = -.25f, SurfaceGrip = .98f };
                var fresh = AcNativeExtras.Fresh(T.AddSeconds(1));
                Assert.IsNotNull(fresh);
                Assert.AreEqual(2f, ((float[])fresh["reifen_schmutz"])[3]);
                Assert.IsNull(AcNativeExtras.Fresh(T.AddSeconds(5)), "älter als 3 s: nichts melden");
                Assert.Greater(ACSDynamicsSource.pPacket + 1, 0, "Offsets der Zusatzfelder lassen sich bilden (Feldnamen stimmen)");
            }
            finally { AcNativeExtras.Latest = saved; }
        }

        [Test]
        public void IRacing_off_track_flag_of_an_opponent_names_the_incident_even_at_speed()
        {
            var gs = Race();
            gs.SessionData.TrackDefinition = new TrackDefinition("Fixture", 4000);
            gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            var o = Car("Sam Racer", 3); o.DistanceRoundTrack = 1150; o.Speed = 35;
            gs.OpponentData["sam"] = o;
            Assert.IsNull(FlagWatch.IncidentDriver(gs), "schnell und auf der Strecke: kein Unfall");
            o.trackSurface = 0;
            Assert.AreEqual("Sam Racer", FlagWatch.IncidentDriver(gs), "iRacing meldet ihn neben der Strecke");
        }

        [Test]
        public void Name_budget_is_per_session_and_covers_a_full_field()
        {
            Assert.GreaterOrEqual(AiTeamService.OpponentWarmBudget, 40, "ein volles Feld, nicht 24 Namen für die ganze Programmlaufzeit");
            Assert.Greater(AiTeamService.OpponentWarmBudgetParked, 8);
        }
    }
}
