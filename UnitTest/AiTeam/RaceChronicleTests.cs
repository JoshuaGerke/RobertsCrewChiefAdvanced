using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Masterplan Schritt 1 und 2 (Rob 21.11.2026): Rennchronik, Rückbezüge auf Duelle, Überholbilanz, Endspurt-Briefing.</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RaceChronicleTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 20, 0, 0, DateTimeKind.Utc);
        private bool wasDeterministic;
        private string wasLanguage;

        [SetUp]
        public void Setup()
        {
            wasDeterministic = RadioVariants.Deterministic; wasLanguage = RadioLanguage.Current;
            RadioVariants.Deterministic = true; RadioLanguage.Current = RadioLanguage.German; RaceChronicle.Current.Reset();
        }

        [TearDown]
        public void TearDown() { RadioVariants.Deterministic = wasDeterministic; RadioLanguage.Current = wasLanguage; RaceChronicle.Current.Reset(); }

        private static GameStateData Race(int own = 5, SessionPhase phase = SessionPhase.Green)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 7; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = phase;
            gs.SessionData.ClassPosition = own; gs.SessionData.CompletedLaps = 2;
            gs.carClass = new CarData.CarClass();
            return gs;
        }

        private static OpponentData Car(GameStateData gs, string key, string name, int rank, bool pit = false)
        {
            var o = new OpponentData { DriverRawName = name, ClassPosition = rank, IsActive = true, InPits = pit, CarClass = gs.carClass, Speed = 40, CompletedLaps = 2 };
            gs.OpponentData[key] = o;
            return o;
        }

        private static GameStateData Copy(GameStateData template, int own, params (string key, string name, int rank, bool pit)[] cars)
        {
            var gs = Race(own, template.SessionData.SessionPhase);
            gs.SessionData.CompletedLaps = template.SessionData.CompletedLaps;
            foreach (var c in cars) Car(gs, c.key, c.name, c.rank, c.pit);
            return gs;
        }

        [Test]
        public void Recorder_names_on_track_passes_and_marks_pit_swaps_after_the_position_settles()
        {
            var rec = new ChronicleRecorder(RaceChronicle.Current);
            var a = Copy(Race(), 5, ("k4", "Bananekk", 4, false), ("k6", "Michiman", 6, false));
            rec.Observe(a, T);
            Assert.AreEqual(ChronicleKind.Grid, RaceChronicle.Current.All().Single().Kind);

            var b = Copy(a, 4, ("k4", "Bananekk", 5, false), ("k6", "Michiman", 6, false));
            rec.Observe(b, T.AddSeconds(1));
            rec.Observe(b, T.AddSeconds(2));
            Assert.AreEqual(0, RaceChronicle.Current.Of(ChronicleKind.PositionGained).Count, "erst nach 2 s stabil");
            rec.Observe(b, T.AddSeconds(3.2));
            var gain = RaceChronicle.Current.Of(ChronicleKind.PositionGained).Single();
            Assert.AreEqual("Bananekk", gain.Subject); Assert.AreEqual("k4", gain.SubjectKey);
            Assert.AreEqual(ChronicleRecorder.OnTrack, gain.Detail);
            Assert.AreEqual(5, gain.PositionBefore); Assert.AreEqual(4, gain.Position);

            var c = Copy(b, 5, ("k4", "Bananekk", 6, false), ("k6", "Michiman", 4, true));
            rec.Observe(c, T.AddSeconds(10));
            rec.Observe(c, T.AddSeconds(12.5));
            var pitLoss = RaceChronicle.Current.Of(ChronicleKind.PositionLost).Single();
            Assert.AreEqual("Michiman", pitLoss.Subject);
            Assert.AreEqual(ChronicleRecorder.ByPit, pitLoss.Detail, "Platzwechsel durch einen Boxenstopp ist kein Duell");
        }

        [Test]
        public void Short_position_flicker_records_nothing()
        {
            var rec = new ChronicleRecorder(RaceChronicle.Current);
            var a = Copy(Race(), 5, ("k4", "Bananekk", 4, false));
            rec.Observe(a, T);
            var b = Copy(a, 4, ("k4", "Bananekk", 5, false));
            rec.Observe(b, T.AddSeconds(1));
            rec.Observe(a, T.AddSeconds(1.5));
            rec.Observe(a, T.AddSeconds(5));
            Assert.AreEqual(0, RaceChronicle.Current.Of(ChronicleKind.PositionGained).Count);
        }

        [Test]
        public void Laps_personal_best_own_pit_and_finish_are_recorded()
        {
            var rec = new ChronicleRecorder(RaceChronicle.Current);
            var gs = Race(); rec.Observe(gs, T);
            gs.SessionData.CompletedLaps = 3; gs.SessionData.LapTimePrevious = 92.345f; gs.SessionData.PreviousLapWasValid = true;
            gs.SessionData.PlayerLapTimeSessionBest = 92.345f;
            rec.Observe(gs, T.AddSeconds(90));
            Assert.AreEqual(92.345, RaceChronicle.Current.Of(ChronicleKind.OwnLap).Single().Value, .001);
            Assert.AreEqual(1, RaceChronicle.Current.Of(ChronicleKind.OwnBest).Count);
            gs.PitData.InPitlane = true; rec.Observe(gs, T.AddSeconds(100));
            Assert.AreEqual(1, RaceChronicle.Current.Of(ChronicleKind.OwnPit).Count);
            gs.PitData.InPitlane = false; gs.SessionData.SessionPhase = SessionPhase.Checkered; rec.Observe(gs, T.AddSeconds(200));
            Assert.AreEqual(1, RaceChronicle.Current.Of(ChronicleKind.Finish).Count);
        }

        [Test]
        public void One_yellow_from_two_sources_is_one_entry_and_keeps_the_known_name()
        {
            var gs = Race();
            RaceChronicle.Current.Add(gs, T, ChronicleKind.Yellow, detail: "gelb_sektor", source: "flaggen");
            RaceChronicle.Current.Add(gs, T.AddSeconds(1), ChronicleKind.Yellow, detail: "yellow_0", source: "native", subjectName: "minmos", dedupe: TimeSpan.FromSeconds(20));
            var y = RaceChronicle.Current.Of(ChronicleKind.Yellow).Single();
            Assert.AreEqual("minmos", y.Subject, "der zweite Hinweis kennt den Betroffenen");
        }

        [Test]
        public void New_session_starts_an_empty_chronicle_and_names_can_be_withheld()
        {
            var gs = Race();
            RaceChronicle.Current.Add(gs, T, ChronicleKind.Hazard, subjectName: "minmos", detail: "dreher");
            var json = RaceChronicle.Current.ToJson(T.AddSeconds(5), 10, names: false);
            Assert.IsNull(json["eintraege"][0]["gegner"], "Namen nur, wenn erlaubt");
            Assert.AreEqual("minmos", (string)RaceChronicle.Current.ToJson(T.AddSeconds(5))["eintraege"][0]["gegner"]);
            var next = Race(); next.SessionData.SessionId = 8;
            RaceChronicle.Current.Follow(next);
            Assert.AreEqual(0, RaceChronicle.Current.Count());
        }

        private static void Duel(GameStateData gs, DateTime at, ChronicleKind kind, string key = "k4", string name = "Bananekk", int lap = 2, string how = ChronicleRecorder.OnTrack)
        {
            gs.SessionData.CompletedLaps = lap;
            RaceChronicle.Current.Add(gs, at, kind, subjectKey: key, subjectName: name, detail: how, dedupe: TimeSpan.Zero);
        }

        [Test]
        public void Revenge_duel_count_and_approach_refer_only_to_older_on_track_duels()
        {
            var gs = Race();
            Assert.AreEqual("", RaceStory.AfterGain("k4", T, 0), "ohne Vorgeschichte kein Rückbezug");
            Duel(gs, T.AddMinutes(-3), ChronicleKind.PositionLost, lap: 1);
            StringAssert.Contains("Runde 2", RaceStory.AfterGain("k4", T, 0), "Revanche nennt die Runde, wie Rob sie zählt");
            StringAssert.Contains("Revanche", RaceStory.AfterGain("k4", T, 0));
            Duel(gs, T.AddMinutes(-2), ChronicleKind.PositionGained, lap: 3);
            StringAssert.Contains("Runde 4", RaceStory.AfterLoss("k4", T, 0));
            Duel(gs, T.AddMinutes(-1), ChronicleKind.PositionLost, lap: 5);
            Duel(gs, T.AddMinutes(-0.5), ChronicleKind.PositionGained, lap: 6);
            StringAssert.Contains("5.", RaceStory.AfterGain("k4", T, 0), "vier Duelle, das ist der fünfte Platztausch");
            Assert.AreEqual(4, RaceChronicle.Current.Exchanges("k4"));
            StringAssert.Contains("Runde 7", RaceStory.Approaching("k4", T, 0));

            Duel(gs, T.AddSeconds(-1), ChronicleKind.PositionLost, key: "k9", name: "Neu");
            Assert.AreEqual("", RaceStory.AfterGain("k9", T, 0), "das gerade gemeldete Ereignis ist keine Vorgeschichte");
            Duel(gs, T.AddMinutes(-1), ChronicleKind.PositionLost, key: "k8", name: "Boxer", how: ChronicleRecorder.ByPit);
            Assert.AreEqual("", RaceStory.AfterGain("k8", T, 0), "Boxenstopp ist kein Duell");
        }

        [Test]
        public void Story_sentences_exist_in_three_languages_with_five_variants()
        {
            var gs = Race();
            Duel(gs, T.AddMinutes(-3), ChronicleKind.PositionLost, lap: 1);
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.AreEqual(5, Enumerable.Range(0, 5).Select(v => RaceStory.AfterGain("k4", T, v)).Distinct().Count(), lang);
                Assert.AreEqual(5, Enumerable.Range(0, 5).Select(v => RaceStory.Approaching("k4", T, v)).Distinct().Count(), lang);
                Assert.AreEqual(5, Enumerable.Range(0, 5).Select(v => RaceStory.Passed("Bananekk", v)).Distinct().Count(), lang);
                Assert.AreEqual(5, Enumerable.Range(0, 5).Select(v => RaceStory.PassedBy("Bananekk", v)).Distinct().Count(), lang);
                foreach (var v in Enumerable.Range(0, 5))
                    Assert.IsTrue(char.IsUpper(RaceStory.AfterGain("k4", T, v)[0]), lang + " " + v);
            }
        }

        [Test]
        public void Tally_counts_on_track_duels_and_names_the_main_rival()
        {
            var gs = Race();
            Assert.AreEqual("", RaceStory.TallyText(RaceChronicle.Current, true, 0));
            Duel(gs, T.AddMinutes(-5), ChronicleKind.PositionGained);
            Duel(gs, T.AddMinutes(-4), ChronicleKind.PositionLost);
            Duel(gs, T.AddMinutes(-3), ChronicleKind.PositionGained);
            Duel(gs, T.AddMinutes(-2), ChronicleKind.PositionGained, key: "k6", name: "Michiman");
            Duel(gs, T.AddMinutes(-1), ChronicleKind.PositionLost, key: "k8", name: "Boxer", how: ChronicleRecorder.ByPit);
            string text = RaceStory.TallyText(RaceChronicle.Current, true, 0);
            StringAssert.Contains("3 Überholmanöver", text);
            StringAssert.Contains("einmal überholt", text);
            StringAssert.Contains("Bananekk", text);
            StringAssert.DoesNotContain("Bananekk", RaceStory.TallyText(RaceChronicle.Current, false, 0), "Namen nur, wenn erlaubt");
        }

        private static RaceSituation Situation(float ahead, float frontRate, float behind, float rearRate)
        {
            return new RaceSituation
            {
                Utc = T, SessionId = 7, GapAhead = ahead, GapBehind = behind, FrontRate = frontRate, RearRate = rearRate,
                FrontLapsToCatch = ahead > 0 ? GapTrendWatch.LapsToCatch(ahead, frontRate) : -1,
                RearLapsToCatch = behind > 0 ? GapTrendWatch.LapsToCatch(behind, rearRate) : -1
            };
        }

        [Test]
        public void Endgame_brief_says_whether_the_catch_and_the_defence_work_out()
        {
            // 2,0 s vorn, 0,5 s pro Runde schneller: in 3 Runden auf Angriffsdistanz, bei 4 Restrunden bleiben 3 – reicht.
            string reach = EndgameBriefWatch.Compose(Situation(2f, .5f, 3f, 0), 4, 5, "Bananekk", "Michiman", "", 0);
            StringAssert.Contains("Bananekk", reach); StringAssert.Contains("das reicht", reach);
            StringAssert.Contains("Luft", reach);
            // 4,0 s vorn, 0,3 s pro Runde: 11 Runden nötig – reicht nicht, gebraucht würden etwa 1,1 s pro Runde.
            string shortOf = EndgameBriefWatch.Compose(Situation(4f, .3f, -1, 0), 4, 5, "Bananekk", null, "", 0);
            StringAssert.Contains("reicht es knapp nicht", shortOf);
            StringAssert.Contains("bräuchtest", shortOf);
            // Hinten 1,5 s, 0,4 s pro Runde schneller: 2 Runden – vor der Flagge dran.
            string defend = EndgameBriefWatch.Compose(Situation(-1, 0, 1.5f, .4f), 4, 5, null, "Michiman", "Den hast du in Runde 3 überholt.", 0);
            StringAssert.Contains("verteidigen", defend); StringAssert.Contains("Runde 3", defend);
            // Hinten 5 s, 0,3 s pro Runde: reicht ihm nicht.
            StringAssert.Contains("reicht es ihm nicht", EndgameBriefWatch.Compose(Situation(-1, 0, 5f, .3f), 4, 5, null, "Michiman", "", 0));
            Assert.IsNull(EndgameBriefWatch.Compose(Situation(-1, 0, -1, 0), 4, 5, "A", "B", "", 0), "ohne Messung kein Briefing");
            StringAssert.Contains("Du führst", EndgameBriefWatch.Compose(Situation(2f, .5f, 3f, 0), 4, 1, "A", "B", "", 0));
            StringAssert.DoesNotContain("Vorn", EndgameBriefWatch.Compose(Situation(2f, .5f, 3f, 0), 4, 1, "A", "B", "", 0), "als Führender gibt es niemanden vorn");
        }

        [Test]
        public void Endgame_brief_varies_and_exists_in_three_languages()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                foreach (var x in new[] { Situation(2f, .5f, 3f, 0), Situation(4f, .3f, 1.5f, .4f), Situation(3f, -.4f, 5f, .3f), Situation(1f, 0, 1f, 0) })
                {
                    var texts = Enumerable.Range(0, 5).Select(v => EndgameBriefWatch.Compose(x, 4, 5, "Bananekk", "Michiman", "", v)).ToList();
                    Assert.IsTrue(texts.All(t => !string.IsNullOrWhiteSpace(t) && char.IsUpper(t[0])), lang);
                    Assert.GreaterOrEqual(texts.Distinct().Count(), 4, lang + ": mindestens vier Formulierungen");
                }
            }
        }

        [Test]
        public void Endgame_brief_comes_once_three_to_four_laps_from_the_end()
        {
            var gs = Race(); gs.SessionData.SessionNumberOfLaps = 10; gs.SessionData.SessionLapsRemaining = 6; gs.SessionData.CompletedLaps = 4;
            var watch = new EndgameBriefWatch();
            var x = Situation(2f, .5f, 3f, 0);
            watch.Observe(gs, x, "Bananekk", "Michiman", T);
            Assert.IsNull(watch.Take(gs, T, false, true), "zu früh");
            gs.SessionData.SessionLapsRemaining = 4; gs.SessionData.CompletedLaps = 6;
            watch.Observe(gs, x, "Bananekk", "Michiman", T);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual(TeamMember.Ben, call.Member);
            call.MarkPlaybackStarted(T);
            gs.SessionData.SessionLapsRemaining = 3;
            watch.Observe(gs, x, "Bananekk", "Michiman", T.AddSeconds(90));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(90), false, true), "einmal je Rennen");
        }

        private static GameStateData Track(int own)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 7; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = own; gs.SessionData.CompletedLaps = 1; gs.SessionData.CurrentLapIsValid = true;
            gs.SessionData.TrackDefinition = new TrackDefinition("Fixture", 4000);
            gs.carClass = new CarData.CarClass(); gs.PositionAndMotionData.CarSpeed = 40; gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            return gs;
        }

        [Test]
        public void Position_calls_name_the_rival_and_recall_the_earlier_duel()
        {
            var gs = Track(10);
            var rival = new OpponentData { DriverRawName = "Bananekk", ClassPosition = 9, CarClass = gs.carClass, CompletedLaps = 1, DistanceRoundTrack = 1010, Speed = 40, IsActive = true };
            gs.OpponentData["rival"] = rival;
            Duel(gs, T.AddMinutes(-2), ChronicleKind.PositionLost, key: "rival", lap: 0);
            gs.SessionData.CompletedLaps = 1;
            var observer = new ActiveRaceObserver();
            observer.Observe(gs, T, "Rob", 0, null, true);
            gs.SessionData.ClassPosition = 9; rival.ClassPosition = 10; rival.DistanceRoundTrack = 990;
            foreach (int ms in new[] { 100, 200, 350 }) observer.Observe(gs, T.AddMilliseconds(ms), "Rob", 0, null, true);
            var call = observer.Take(gs, T.AddMilliseconds(350), false, false, false, 0, null, "Rob");
            Assert.IsNotNull(call);
            StringAssert.Contains("Bananekk", call.Text);
            StringAssert.Contains("Runde 1", call.Text, "Revanche für das Duell aus Runde 1");
        }

        [Test]
        public void Position_loss_names_the_car_that_came_through()
        {
            var gs = Track(10);
            var rival = new OpponentData { DriverRawName = "Michiman", ClassPosition = 11, CarClass = gs.carClass, CompletedLaps = 1, DistanceRoundTrack = 990, Speed = 40, IsActive = true };
            gs.OpponentData["rival"] = rival;
            var observer = new ActiveRaceObserver();
            observer.Observe(gs, T, "Rob", 0, null, true);
            gs.SessionData.ClassPosition = 11; rival.ClassPosition = 10; rival.DistanceRoundTrack = 1010;
            foreach (int ms in new[] { 100, 200, 350 }) observer.Observe(gs, T.AddMilliseconds(ms), "Rob", 0, null, true);
            var call = observer.Take(gs, T.AddMilliseconds(350), false, false, false, 0, null, "Rob");
            Assert.IsNotNull(call);
            Assert.AreEqual("stable_position_loss_same_class_rival_crossed", call.Evidence);
            StringAssert.StartsWith("Das war Michiman.", call.Text);

            var anonymous = new ActiveRaceObserver();
            var gs2 = Track(10);
            var r2 = new OpponentData { DriverRawName = "Michiman", ClassPosition = 11, CarClass = gs2.carClass, CompletedLaps = 1, DistanceRoundTrack = 990, Speed = 40, IsActive = true };
            gs2.OpponentData["rival"] = r2;
            anonymous.Observe(gs2, T, "Rob", 0, null, false);
            gs2.SessionData.ClassPosition = 11; r2.ClassPosition = 10;
            foreach (int ms in new[] { 100, 200, 350 }) anonymous.Observe(gs2, T.AddMilliseconds(ms), "Rob", 0, null, false);
            StringAssert.DoesNotContain("Michiman", anonymous.Take(gs2, T.AddMilliseconds(350), false, false, false, 0, null, "Rob").Text, "Namen nur, wenn erlaubt");
        }

        [Test]
        public void Chronicle_is_offered_to_the_model_as_a_tool()
        {
            CollectionAssert.Contains(RaceDataTools.AllToolNames, "get_race_chronicle");
            var gs = Race();
            RaceChronicle.Current.Add(gs, T, ChronicleKind.Retired, subjectName: "Akos Forgacs");
            var result = RaceDataTools.Execute("get_race_chronicle", new RaceDataContext { GameState = gs, NowUtc = T, LastTelemetryUtc = T, Memory = new AiRaceMemory(), SendDriverNames = true });
            Assert.IsNull(result["fehler"], result.ToString());
            StringAssert.Contains("Akos Forgacs", result.ToString());
        }
    }
}
