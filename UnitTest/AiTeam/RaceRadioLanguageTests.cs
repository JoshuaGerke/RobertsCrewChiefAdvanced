using System;
using System.Linq;
using System.Reflection;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RaceRadioLanguageTests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        private static GameStateData State(int lap = 1)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 71;
            s.SessionData.SessionType = SessionType.Race;
            s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.CompletedLaps = lap;
            s.SessionData.ClassPosition = 7;
            s.PositionAndMotionData.CarSpeed = 40;
            return s;
        }

        private static void WithFuelEstimate(int laps, int atLap, Action test)
        {
            var fuel = typeof(LapWatch).Assembly.GetType("CrewChiefV4.Events.Fuel", true);
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var estimate = fuel.GetField("EstimatedLapsRemaining", flags);
            var stamp = fuel.GetField("EstimatedLapsRemainingAtLap", flags);
            object oldEstimate = estimate.GetValue(null), oldStamp = stamp.GetValue(null);
            try { estimate.SetValue(null, laps); stamp.SetValue(null, atLap); test(); }
            finally { estimate.SetValue(null, oldEstimate); stamp.SetValue(null, oldStamp); }
        }

        [Test]
        public void Lap_invalidation_does_not_invent_a_track_limit_cause()
        {
            var before = State(); var after = State(); after.SessionData.CurrentLapIsValid = false;
            var calls = new IncidentWatch().Evaluate(before, after, T, false);
            Assert.AreEqual(1, calls.Count);
            StringAssert.Contains("ungültig", calls[0].Value);
            StringAssert.DoesNotContain("Limits", calls[0].Value);
            StringAssert.DoesNotContain("Rand", calls[0].Value);
            StringAssert.DoesNotContain("Strafe", calls[0].Value);
        }

        [Test]
        public void Confirmed_cut_warning_does_not_invent_lap_invalidation_or_penalty()
        {
            var before = State(); var after = State(); after.PenaltiesData.CutTrackWarnings = 1;
            var watch = new IncidentWatch(); var calls = watch.Evaluate(before, after, T, false);
            Assert.AreEqual(1, calls.Count);
            Assert.IsTrue(after.SessionData.CurrentLapIsValid);
            StringAssert.DoesNotContain("ungültig", calls[0].Value);
            StringAssert.DoesNotContain("Strafe", calls[0].Value);
            Assert.AreEqual(1, watch.TrackLimitWarnings);
            Assert.AreEqual(0, watch.InvalidLaps);
            Assert.IsEmpty(watch.Evaluate(before, after, T.AddMilliseconds(100), false), "same cut-warning sample is not a second event");
        }

        [Test]
        public void Cut_and_invalidation_in_one_tick_produce_one_grounded_comment()
        {
            var before = State(); var after = State();
            after.PenaltiesData.CutTrackWarnings = 1; after.SessionData.CurrentLapIsValid = false;
            var watch = new IncidentWatch(); var calls = watch.Evaluate(before, after, T, false);
            Assert.AreEqual(1, calls.Count);
            StringAssert.Contains("Diese Runde ist ungültig.", calls[0].Value);
            Assert.AreEqual(1, watch.InvalidLaps); Assert.AreEqual(1, watch.TrackLimitWarnings);
        }

        [Test]
        public void Repeated_cut_has_different_context_and_no_immediate_wording_repeat()
        {
            var language = new RaceRadioLanguage(7);
            string first = language.TrackLimit(false, false);
            StringAssert.DoesNotContain("Wieder", first);
            StringAssert.DoesNotContain("wieder", first);
            string last = null;
            for (int i = 0; i < 30; i++)
            {
                string text = language.TrackLimit(true, false);
                Assert.AreNotEqual(last, text);
                Assert.Contains(text, CrewSpeechPolicy.RepeatedTrackLimitPhrases);
                last = text;
            }
        }

        [Test]
        public void All_local_invalidity_variants_can_be_prepared_in_original_voice()
        {
            var language = new RaceRadioLanguage(3);
            for (int i = 0; i < 40; i++)
            {
                foreach (bool repeated in new[] { false, true })
                {
                    Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Mik, language.InvalidLap(repeated)));
                    Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Mik, language.TrackLimit(repeated, false)));
                    Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Mik, language.TrackLimit(repeated, true)));
                }
            }
        }

        [Test]
        public void Minimal_mode_records_confirmed_facts_but_keeps_optional_comments_quiet()
        {
            var before = State(); var after = State();
            after.PenaltiesData.CutTrackWarnings = 1; after.SessionData.CurrentLapIsValid = false;
            var watch = new IncidentWatch();
            Assert.IsEmpty(watch.Evaluate(before, after, T, true));
            Assert.AreEqual(1, watch.InvalidLaps); Assert.AreEqual(1, watch.TrackLimitWarnings);
            StringAssert.Contains("eine ungültige Runde", watch.FinishSummary());
            StringAssert.Contains("eine Tracklimit-Meldung", watch.FinishSummary());
        }

        [Test]
        public void Empty_incident_record_does_not_claim_an_observed_clean_race()
        {
            string text = new IncidentWatch().FinishSummary();
            StringAssert.Contains("erfasst", text);
            StringAssert.DoesNotContain("ohne einen einzigen Ausritt", text);
            StringAssert.DoesNotContain("fehlerfrei", text);
        }

        [Test]
        public void Sudden_speed_loss_does_not_claim_a_confirmed_spin()
        {
            var watch = new IncidentWatch(); var before = State(); var after = State();
            after.PositionAndMotionData.CarSpeed = 1;
            watch.Evaluate(before, before, T, false);
            watch.Evaluate(before, after, T.AddSeconds(2), false);
            StringAssert.Contains("starke Verzögerung", watch.FinishSummary());
            StringAssert.DoesNotContain("Dreher", watch.FinishSummary());
        }

        [Test]
        public void Qualifying_best_lap_delta_is_not_spoken_as_recovery_traffic_gap()
        {
            var watch = new IncidentWatch(); var before = State(); var after = State();
            before.SessionData.SessionType = after.SessionData.SessionType = SessionType.Qualify;
            after.SessionData.TimeDeltaBehind = 1;
            after.PositionAndMotionData.CarSpeed = 1;
            watch.Evaluate(before, before, T, false);
            string text = watch.Evaluate(before, after, T.AddSeconds(2), false).Single().Value;
            StringAssert.DoesNotContain("Sekunden", text);
        }

        [Test]
        public void Victory_varies_across_sessions_without_immediate_repetition()
        {
            var watch = new LapWatch(); string last = null;
            for (int i = 0; i < 30; i++)
            {
                watch.Reset(); var before = State(); var finish = State();
                finish.SessionData.SessionPhase = SessionPhase.Finished; finish.SessionData.ClassPosition = 1;
                string text = watch.Evaluate(before, finish, -1, -1, false);
                Assert.Contains(text, CrewSpeechPolicy.FinishPhrases);
                Assert.AreNotEqual(last, text);
                Assert.IsNull(watch.Evaluate(finish, finish, -1, -1, false), "only one celebration per finish");
                last = text;
            }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void Retired_or_disqualified_P1_never_receives_victory_wording(bool dnf, bool dq)
        {
            var s = State(); s.SessionData.ClassPosition = 1; s.SessionData.SessionPhase = SessionPhase.Finished;
            s.SessionData.IsDNF = dnf; s.SessionData.IsDisqualified = dq;
            string text = new RaceRadioLanguage(1).Finish(s.SessionData);
            Assert.IsFalse(CrewSpeechPolicy.FinishPhrases.Contains(text));
            StringAssert.DoesNotContain("gewonnen", text);
            StringAssert.DoesNotContain("Sieg", text);
        }

        [Test]
        public void Podium_and_midfield_results_have_distinct_grounded_context()
        {
            var language = new RaceRadioLanguage(2); var s = State(); s.SessionData.SessionPhase = SessionPhase.Finished;
            s.SessionData.ClassPosition = 2; s.SessionData.SessionStartClassPosition = 7;
            string podium = language.Finish(s.SessionData);
            StringAssert.Contains("Platz 2. 5 Plätze gutgemacht.", podium);
            StringAssert.DoesNotContain("Sieg", podium);
            s.SessionData.ClassPosition = 9; s.SessionData.SessionStartClassPosition = 0;
            string finish = language.Finish(s.SessionData);
            StringAssert.Contains("Platz 9.", finish);
            StringAssert.DoesNotContain("Podium", finish); StringAssert.DoesNotContain("Podest", finish);
            StringAssert.DoesNotContain("gutgemacht", finish);
        }

        [Test]
        public void No_finish_or_praise_for_missing_confirmation()
        {
            Assert.IsNull(new RaceRadioLanguage(1).Finish(State().SessionData));
            Assert.IsNull(RaceRadioLanguage.Overtake(0, 7, "Rob"));
            Assert.IsNull(RaceRadioLanguage.Overtake(1, 0, "Rob"));
        }

        [Test]
        public void Confirmed_pass_comments_keep_rank_and_progress_without_invented_performance()
        {
            string first = RaceRadioLanguage.Overtake(1, 7, "Rob");
            StringAssert.Contains("Rob", first); StringAssert.Contains("Position 7", first);
            var comments = Enumerable.Range(2, 6).Select(n => RaceRadioLanguage.Overtake(n, 7, "Rob")).ToArray();
            Assert.AreEqual(6, comments.Distinct().Count());
            foreach (string text in comments)
            {
                StringAssert.Contains("Position 7", text);
                StringAssert.DoesNotContain("Reifen", text); StringAssert.DoesNotContain("Sprit", text);
                StringAssert.DoesNotContain("schneller", text); StringAssert.DoesNotContain("Plan", text);
            }
        }

        [TestCase(-1f)] [TestCase(0f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void Missing_or_invalid_timed_clock_does_not_become_the_last_lap(float remaining)
        {
            WithFuelEstimate(0, 9, () => {
                var before = State(8); var s = State(9); s.SessionData.SessionHasFixedTime = true;
                s.SessionData.SessionRunningTime = 901; s.SessionData.SessionTimeRemaining = remaining;
                s.SessionData.IsNewLap = true;
                Assert.AreEqual(-1, LapWatch.LapsLeft(s.SessionData));
                Assert.IsFalse(LapWatch.OnLastLap(s.SessionData));
                Assert.IsNull(new LapWatch().Evaluate(before, s, -1, -1, false));
            });
        }

        [TestCase(1)] [TestCase(2)]
        public void Positive_timed_race_estimates_remain_explicitly_estimates(int remainingLaps)
        {
            WithFuelEstimate(remainingLaps, 9, () => {
                var before = State(8); var s = State(9); s.SessionData.SessionHasFixedTime = true;
                s.SessionData.SessionRunningTime = 800; s.SessionData.SessionTimeRemaining = 100;
                s.SessionData.IsNewLap = true;
                Assert.AreEqual(remainingLaps, LapWatch.LapsLeft(s.SessionData));
                Assert.IsTrue(LapWatch.LapsAreEstimated(s.SessionData));
                StringAssert.StartsWith("Voraussichtlich", new LapWatch().Evaluate(before, s, -1, -1, false));
            });
        }

        [Test]
        public void A_stale_fuel_lap_estimate_is_not_reused()
        {
            WithFuelEstimate(1, 8, () => {
                var s = State(9); s.SessionData.SessionHasFixedTime = true; s.SessionData.SessionTimeRemaining = 100;
                Assert.AreEqual(-1, LapWatch.LapsLeft(s.SessionData));
                Assert.IsFalse(LapWatch.LapsAreEstimated(s.SessionData));
            });
        }

        [Test]
        public void Adapter_last_lap_signal_is_retained_even_without_a_timed_clock()
        {
            WithFuelEstimate(0, 9, () => {
                var before = State(8); var s = State(9); s.SessionData.SessionHasFixedTime = true;
                s.SessionData.SessionTimeRemaining = -1; s.SessionData.IsLastLap = true; s.SessionData.IsNewLap = true;
                Assert.AreEqual(1, LapWatch.LapsLeft(s.SessionData));
                Assert.IsFalse(LapWatch.LapsAreEstimated(s.SessionData));
                StringAssert.StartsWith("Letzte Runde!", new LapWatch().Evaluate(before, s, -1, -1, false));
            });
        }

        [Test]
        public void Confirmed_expired_clock_is_not_confused_with_extra_laps_or_victory()
        {
            WithFuelEstimate(0, 9, () => {
                var before = State(8); var s = State(9); s.SessionData.SessionHasFixedTime = true;
                s.SessionData.SessionTotalRunTime = 900; s.SessionData.SessionRunningTime = 900; s.SessionData.SessionTimeRemaining = 0;
                s.SessionData.ClassPosition = 1;
                var watch = new LapWatch(); string text = watch.Evaluate(before, s, -1, -1, false);
                StringAssert.Contains("Rennzeit ist abgelaufen", text);
                StringAssert.DoesNotContain("Letzte Runde", text); StringAssert.DoesNotContain("gewonnen", text);
                Assert.IsNull(watch.Evaluate(s, s, -1, -1, false));
                Assert.AreEqual(-1, LapWatch.LapsLeft(s.SessionData));
            });
        }

        [Test]
        public void Checkered_flag_precedes_celebration_until_actual_finish()
        {
            var before = State(9); var flag = State(9); flag.SessionData.SessionHasFixedTime = true;
            flag.SessionData.SessionTimeRemaining = -1; flag.SessionData.SessionPhase = SessionPhase.Checkered;
            flag.SessionData.ClassPosition = 1;
            var watch = new LapWatch(); string text = watch.Evaluate(before, flag, -1, -1, false);
            Assert.IsTrue(LapWatch.OnLastLap(flag.SessionData));
            StringAssert.Contains("Zielflagge", text); StringAssert.DoesNotContain("gewonnen", text);
            var finish = State(9); finish.SessionData.SessionPhase = SessionPhase.Finished; finish.SessionData.ClassPosition = 1;
            Assert.Contains(watch.Evaluate(flag, finish, -1, -1, false), CrewSpeechPolicy.FinishPhrases);
        }

        [TestCase(0)] [TestCase(-1)]
        public void Unknown_position_is_not_announced_as_a_zero_or_negative_rank(int position)
        {
            foreach (int laps in new[] { 1, 2 })
            {
                var before = State(8); var s = State(9); s.SessionData.IsNewLap = true;
                s.SessionData.SessionLapsRemaining = laps; s.SessionData.ClassPosition = position;
                string text = new LapWatch().Evaluate(before, s, -1, -1, false);
                Assert.IsNotNull(text); StringAssert.DoesNotContain("Platz", text);
            }
        }
    }
}
