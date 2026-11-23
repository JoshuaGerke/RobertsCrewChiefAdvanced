using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Lebendigere Crew (Rob 23.11.2026): gemeinsames Satzgedächtnis für alles tatsächlich Gesprochene, Rennverlauf als Geschichte
    /// (Zweikampf, freigefahren, abgesetzt, wie angekündigt) und die kurze Reaktion eines Kollegen.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class CrewLivelinessTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);
        private DateTime now;
        private bool wasDeterministic;
        private Func<double> wasRoll;

        [SetUp]
        public void SetUp()
        {
            RadioLanguage.Current = RadioLanguage.German;
            wasDeterministic = RadioVariants.Deterministic;
            wasRoll = RaceStorylineWatch.FollowUpRoll;
            now = T;
            PhraseMemory.Clock = () => now;
            PhraseMemory.Clear();
            RaceStorylineWatch.ForgetExpectations();
        }

        [TearDown]
        public void TearDown()
        {
            RadioVariants.Deterministic = wasDeterministic;
            RaceStorylineWatch.FollowUpRoll = wasRoll;
            PhraseMemory.Clock = () => DateTime.UtcNow;
            PhraseMemory.Clear();
            RaceStorylineWatch.ForgetExpectations();
        }

        // ---------- Satzgedächtnis ----------

        [Test]
        public void Phrase_memory_compares_the_sentence_frame_not_the_numbers()
        {
            Assert.AreEqual(PhraseMemory.Skeleton("Platz 5, sauber gefahren."), PhraseMemory.Skeleton("Platz 7, sauber gefahren!"));
            PhraseMemory.NoteSpoken(TeamMember.Ben, "Platz 5, sauber gefahren. Du holst auf.", T);
            Assert.AreEqual(T, PhraseMemory.LastHeard("Platz 9, sauber gefahren.", T.AddSeconds(30)));
            Assert.AreEqual(T, PhraseMemory.LastHeard("Platz 5, sauber gefahren. Du holst auf.", T.AddSeconds(30)), "ganzer Funkspruch");
            Assert.AreEqual(DateTime.MinValue, PhraseMemory.LastHeard("Ganz anderer Satz hier.", T.AddSeconds(30)));
            Assert.AreEqual(DateTime.MinValue, PhraseMemory.LastHeard("Platz 9, sauber gefahren.", T + PhraseMemory.Window + TimeSpan.FromMinutes(1)),
                "nach dem Fenster darf der Satz wiederkommen");
        }

        [Test]
        public void A_sentence_heard_from_another_place_is_not_picked_again()
        {
            RadioVariants.Deterministic = false;
            string[] options = { "Gute Runde, genau so weiter.", "Das war eine starke Runde.", "Saubere Runde, Rhythmus passt.", "Schön gefahren, bleib dabei." };
            foreach (string spoken in options.Take(3)) PhraseMemory.NoteSpoken(TeamMember.Mik, spoken, now);
            now = now.AddSeconds(20);
            for (int i = 0; i < 12; i++)
                Assert.AreEqual(options[3], RadioVariants.Pick("liveliness-test-" + i, options), "Monza 22.11.: nur die noch nicht gehörte Formulierung");
        }

        [Test]
        public void Catalog_cursor_skips_templates_that_were_just_heard()
        {
            var catalog = RadioPhraseCatalog.Current;
            int count = catalog.Count("pass-repeat");
            Assume.That(count, Is.GreaterThanOrEqualTo(5));
            int keep = count - 2;
            for (int i = 0; i < count; i++)
                if (i != keep) PhraseMemory.NoteSpoken(TeamMember.Ben, catalog.Format("pass-repeat", i, "5"), now);
            now = now.AddSeconds(10);
            Assert.AreEqual(keep, new RadioVariantCursor().Select("pass-repeat", count));
            CollectionAssert.IsNotEmpty(catalog.Literals("pass-repeat", keep));
            Assert.IsFalse(catalog.Literals("pass-repeat", keep).Any(l => l.Contains("position") && !l.Contains(" ")), "Platzhalternamen gehören nicht zum Text");
        }

        [Test]
        public void Fixed_variant_sentences_rotate_past_what_was_heard()
        {
            PhraseMemory.NoteSpoken(TeamMember.Ben, RaceStory.Passed("Krobber", 0), now);
            PhraseMemory.NoteSpoken(TeamMember.Ben, RaceStory.Passed("Krobber", 1), now);
            Assert.AreEqual(2, PhraseMemory.FreshVariant(0, 5, i => RaceStory.Passed("Krobber", i)));
            Assert.AreEqual(3, PhraseMemory.FreshVariant(3, 5, i => RaceStory.Passed("Krobber", i)), "eine ungehörte Wahl bleibt");
        }

        [Test]
        public void Praise_repeated_word_for_word_within_minutes_is_dropped()
        {
            PhraseMemory.NoteSpoken(TeamMember.Ben, "Sehr gleichmäßig, 5 Runden innerhalb von 3 Zehnteln.", T);
            var praise = new ObservedRaceCall { Topic = "judge-consistency", Text = "Sehr gleichmäßig, 6 Runden innerhalb von 2 Zehnteln.", Priority = 3 };
            Assert.IsTrue(RaceMind.RepeatsRecentColour(praise, T.AddMinutes(2)));
            Assert.IsFalse(RaceMind.RepeatsRecentColour(praise, T.AddMinutes(6)), "nach fünf Minuten wieder erlaubt");
            var position = new ObservedRaceCall { Topic = "position", Text = "Sehr gleichmäßig, 6 Runden innerhalb von 2 Zehnteln.", Priority = 4 };
            Assert.IsFalse(RaceMind.RepeatsRecentColour(position, T.AddMinutes(2)), "Ereignisse sind keine Floskeln");
        }

        // ---------- Rennverlauf ----------

        private static GameStateData Race(out OpponentData front, out OpponentData rear)
        {
            var gs = new GameStateData(T.Ticks);
            var s = gs.SessionData;
            s.SessionId = 7; s.SessionType = SessionType.Race; s.SessionPhase = SessionPhase.Green;
            s.ClassPosition = 5; s.OverallPosition = 5; s.CompletedLaps = 9;
            s.TrackDefinition = new TrackDefinition("Monza", 5793f);
            gs.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            front = new OpponentData { DriverRawName = "Krobber", CarNumber = "12", IsActive = true, ClassPosition = 4, OverallPosition = 4, CarClass = gs.carClass };
            rear = new OpponentData { DriverRawName = "Valgo", CarNumber = "31", IsActive = true, ClassPosition = 6, OverallPosition = 6, CarClass = gs.carClass };
            gs.OpponentData["krobber"] = front; gs.OpponentData["valgo"] = rear;
            s.OpponentKeyInFront = "krobber"; s.OpponentKeyBehind = "valgo";
            gs.SourceTelemetryUtc = T;
            return gs;
        }

        private static GapHistory History(OpponentData op) => new GapHistory { Id = op == null ? null : OpponentStopLog.Key(op) };

        private static void Live(GameStateData gs, DateTime at) { gs.Now = at; gs.SourceTelemetryUtc = at; }

        [Test]
        public void A_long_fight_is_told_once_with_its_length_and_not_again_right_away()
        {
            RadioVariants.Deterministic = true;
            var gs = Race(out var krobber, out var valgo);
            var frontGaps = History(krobber); var rearGaps = History(null);
            var watch = new RaceStorylineWatch();
            frontGaps.Gaps.Add(.8f); watch.Evaluate(gs, 7, frontGaps, rearGaps, true, now);
            frontGaps.Gaps.Add(.7f); watch.Evaluate(gs, 8, frontGaps, rearGaps, true, now);
            Live(gs, now);
            Assert.IsNull(watch.Take(gs, now, false, _ => true), "zwei Runden sind noch kein Zweikampf");
            frontGaps.Gaps.Add(.6f); watch.Evaluate(gs, 9, frontGaps, rearGaps, true, now);
            var call = watch.Take(gs, now, false, _ => true);
            Assert.IsNotNull(call);
            Assert.AreEqual("story-duel-front", call.Topic);
            Assert.AreEqual(TeamMember.Ben, call.Member);
            StringAssert.Contains("Krobber", call.Text);
            StringAssert.Contains("3 Runden", call.Text);
            call.MarkPlaybackStarted(now);

            frontGaps.Gaps.Add(.6f); watch.Evaluate(gs, 10, frontGaps, rearGaps, true, now);
            Assert.IsNull(watch.Take(gs, now, false, _ => true), "nicht jede Runde dieselbe Geschichte");
            frontGaps.Gaps.Add(.5f); watch.Evaluate(gs, 13, frontGaps, rearGaps, true, now);
            var again = watch.Take(gs, now, false, _ => true);
            Assert.IsNotNull(again, "nach vier Runden darf Ben den Zweikampf wieder aufgreifen");
            StringAssert.Contains("7 Runden", again.Text);
        }

        private static RaceStorylineWatch AfterFightBehind(GameStateData gs, OpponentData valgo, DateTime now)
        {
            var rearGaps = History(valgo);
            var watch = new RaceStorylineWatch();
            rearGaps.Gaps.Add(.7f); watch.Evaluate(gs, 7, History(null), rearGaps, true, now);
            rearGaps.Gaps.Add(.9f); watch.Evaluate(gs, 8, History(null), rearGaps, true, now);
            rearGaps.Gaps.Add(2.4f); watch.Evaluate(gs, 9, History(null), rearGaps, true, now);
            return watch;
        }

        [Test]
        public void Breaking_free_is_not_a_second_detection_but_Bens_comment_on_the_crew_chief_gap_call()
        {
            RadioVariants.Deterministic = true;
            RaceStorylineWatch.FollowUpRoll = () => 0;
            var gs = Race(out var krobber, out var valgo);
            var watch = AfterFightBehind(gs, valgo, now);
            Live(gs, now);
            Assert.IsNull(watch.Take(gs, now, false, _ => true), "Rob 23.11.: Crew Chief erkennt die wachsende Lücke, keine eigene Meldung");

            Assert.IsNull(watch.CommentOnGrowingGap(gs, krobber, true, true), "Krobber war nicht im Zweikampf");
            var comment = watch.CommentOnGrowingGap(gs, valgo, false, true);
            Assert.IsNotNull(comment);
            Assert.AreEqual(RaceStorylineWatch.Story.Free, comment.Kind);
            StringAssert.Contains("Valgo", comment.Text);
            Assert.IsFalse(comment.Text.Any(char.IsDigit), "den Abstand nennt schon die Crew-Chief-Meldung");
            Assert.IsNull(watch.CommentOnGrowingGap(gs, valgo, false, true), "jeder Zweikampf wird nur einmal bewertet");

            comment.PlaybackCompleted(now.AddSeconds(3));
            Live(gs, now.AddSeconds(3.5));
            Assert.IsNull(watch.Take(gs, now.AddSeconds(3.5), false, _ => true), "kurze Pause vor der Reaktion");
            Live(gs, now.AddSeconds(4));
            Assert.IsNull(watch.Take(gs, now.AddSeconds(4), false, m => m != TeamMember.Mik), "Mik abgeschaltet: keine Reaktion");
            var reply = watch.Take(gs, now.AddSeconds(4), false, _ => true);
            Assert.IsNotNull(reply);
            Assert.AreEqual("story-followup", reply.Topic);
            Assert.AreEqual(TeamMember.Mik, reply.Member);
            Assert.IsFalse(reply.Text.Any(char.IsDigit), "die Reaktion behauptet keine Zahlen");
            Assert.AreEqual(RadioClass.Encouragement, RadioLoad.Classify(reply.Topic, reply.Priority));
        }

        [Test]
        public void No_comment_when_the_fight_is_long_ago_or_the_name_is_not_speakable()
        {
            var gs = Race(out var krobber, out var valgo);
            var watch = AfterFightBehind(gs, valgo, now);
            gs.SessionData.CompletedLaps = 13;
            Assert.IsNull(watch.CommentOnGrowingGap(gs, valgo, false, true), "vier Runden nach dem Zweikampf ist es keine Neuigkeit mehr");
            gs.SessionData.CompletedLaps = 9;
            valgo.CanUseName = false;
            Assert.IsNull(watch.CommentOnGrowingGap(gs, valgo, false, true));
        }

        [Test]
        public void The_reaction_is_dropped_when_the_situation_has_changed()
        {
            RadioVariants.Deterministic = true;
            RaceStorylineWatch.FollowUpRoll = () => 0;
            var gs = Race(out var krobber, out var valgo);
            var watch = AfterFightBehind(gs, valgo, now);
            var comment = watch.CommentOnGrowingGap(gs, valgo, false, true);
            comment.PlaybackCompleted(now.AddSeconds(3));
            gs.SessionData.ClassPosition = 6; valgo.ClassPosition = 5; // Valgo ist doch vorbei
            Live(gs, now.AddSeconds(4));
            Assert.IsNull(watch.Take(gs, now.AddSeconds(4), false, _ => true));
        }

        [Test]
        public void A_projection_that_comes_true_is_called_as_announced()
        {
            RadioVariants.Deterministic = true;
            var gs = Race(out var krobber, out var valgo);
            RaceStorylineWatch.Expect(7, OpponentStopLog.Key(krobber), true, 9);
            var frontGaps = History(krobber);
            var watch = new RaceStorylineWatch();
            frontGaps.Gaps.Add(1.6f); watch.Evaluate(gs, 8, frontGaps, History(null), true, now);
            frontGaps.Gaps.Add(.9f); watch.Evaluate(gs, 9, frontGaps, History(null), true, now);
            Live(gs, now);
            var call = watch.Take(gs, now, false, _ => true);
            Assert.IsNotNull(call);
            Assert.AreEqual("story-kept-ahead", call.Topic);
            StringAssert.Contains("Wie angekündigt", call.Text);
            StringAssert.Contains("Krobber", call.Text);
        }

        [Test]
        public void Without_a_speakable_name_the_story_stays_silent()
        {
            var gs = Race(out var krobber, out var valgo);
            krobber.CanUseName = false;
            var frontGaps = History(krobber);
            var watch = new RaceStorylineWatch();
            foreach (int lap in new[] { 7, 8, 9 }) { frontGaps.Gaps.Add(.7f); watch.Evaluate(gs, lap, frontGaps, History(null), true, now); }
            Live(gs, now);
            Assert.IsNull(watch.Take(gs, now, false, _ => true));
        }

        [Test]
        public void Stories_wait_for_the_right_moment()
        {
            Func<string, RaceAttention, RacePhase, MindVerdict> judge = (topic, attention, phase) => {
                var x = new RaceSituation { SessionId = 7, Phase = phase, Attention = attention, Utc = T };
                var call = new ObservedRaceCall { Topic = topic, SessionId = 7, SessionType = SessionType.Race, Priority = 3 };
                return RaceMind.Judge(x, call, T, out _);
            };
            Assert.AreEqual(MindVerdict.Admit, judge("story-duel-front", RaceAttention.Battle, RacePhase.Middle), "der Zweikampf selbst");
            Assert.AreEqual(MindVerdict.Hold, judge("story-free", RaceAttention.Battle, RacePhase.Middle));
            Assert.AreEqual(MindVerdict.Admit, judge("story-free", RaceAttention.Open, RacePhase.Middle));
            Assert.AreEqual(MindVerdict.Hold, judge("story-followup", RaceAttention.Caution, RacePhase.Middle));
            Assert.AreEqual(MindVerdict.Drop, judge("story-duel-rear", RaceAttention.Battle, RacePhase.LastLap));
        }

        [Test]
        public void Every_race_control_call_has_at_least_four_wordings()
        {
            RadioVariants.Deterministic = false;
            var gs = Race(out var krobber, out var valgo);
            string[] codes = { "dq", "start-jump", "start-side", "formation-side", "pitlane-start", "meatball", "red", "slippery", "warning",
                "cut-giveback", "cut-penalty", "input-lock", "fcy-pits-closed", "fcy-pits-lead-lap", "fcy-pits-open", "fcy-last-lap-next",
                "fcy-last-lap", "one-to-green", "rival-penalty" };
            foreach (var language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                foreach (string code in codes)
                {
                    var e = new RaceControlEvent { Code = code, Value = 5, Remaining = 300, Count = 1, Car = code == "rival-penalty" ? "Krobber" : null };
                    var seen = Enumerable.Range(0, 60).Select(_ => RaceControlRadio.Call(e, gs).Text).Distinct().Count();
                    Assert.That(seen, Is.GreaterThanOrEqualTo(4), language + " " + code);
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Every_story_has_four_to_six_wordings_in_every_language()
        {
            RadioVariants.Deterministic = false;
            foreach (var language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                foreach (RaceStorylineWatch.Story story in Enum.GetValues(typeof(RaceStorylineWatch.Story)))
                {
                    var seen = Enumerable.Range(0, 80).Select(_ => RaceStorylineWatch.Text(story, "Krobber", 4, 2.3f)).Distinct().ToList();
                    Assert.That(seen.Count, Is.InRange(4, 6), language + " " + story);
                    Assert.IsTrue(seen.All(t => t.Contains("Krobber")), language + " " + story);
                }
                var replies = Enumerable.Range(0, 60).Select(_ => RaceStorylineWatch.FollowUpText(RaceStorylineWatch.Story.Free)).Distinct().Count();
                Assert.That(replies, Is.InRange(4, 6), language + " Reaktion");
            }
            RadioLanguage.Current = RadioLanguage.German;
        }
    }
}
