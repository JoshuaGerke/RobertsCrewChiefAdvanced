using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Rob 24.11.: „Warum werden die Verwarnungen und Strafen der Rennleitung nicht kommentiert? Wenigstens einen Hinweis.“
    /// Bei „nur das KI-Team spricht“ fielen Crew Chiefs Meldungen dazu bisher weg; im LMU-Rennen blieben Cut-Warnungen stumm.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class PenaltyHintTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 21, 42, 13, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; NativeRaceControl.DriverName = "Rob"; }

        [TearDown]
        public void Clean() { LMUNativeSnapshot.Reset(); RadioLanguage.Current = RadioLanguage.German; NativeRaceControl.DriverName = "Rob"; }

        [TestCase("penalties/warning_unsportsmanlike_driving", "pen-warn:conduct")]
        [TestCase("penalties/warning_driving_too_slow", "pen-warn:slow")]
        [TestCase("penalties/warning_wrong_way", "pen-warn:wrongway")]
        [TestCase("penalties/warning_headlights_required", "pen-warn:lights")]
        [TestCase("penalties/warning_headlights_required_when_raining", "pen-warn:lights")]
        [TestCase("penalties/warning_enter_pits_to_avoid_exceeding_laps", "pen-warn:stint")]
        [TestCase("penalties/too_many_car_to_car_collisions", "pen-warn:contact")]
        [TestCase("penalties/one_more_collision_before_kick", "pen-warn:kick-contact")]
        [TestCase("penalties/one_more_off_track_before_kick", "pen-warn:kick-offtrack")]
        [TestCase("flags/black_flag", "pen-black")]
        [TestCase("penalties/new_penalty_black_flag", "pen-black")]
        [TestCase("penalties/meatball_flag", "pen-meatball")]
        [TestCase("penalties/penalty_disqualified", "pen-dq")]
        [TestCase("penalties/disqualified_ignored_drive_through", "pen-dq")]
        [TestCase("penalties/you_still_have_a_penalty", "pen-still")]
        [TestCase("penalties/still_have_to_serve_stop_go", "pen-still")]
        [TestCase("penalties/penalty_not_served", "pen-still")]
        public void Dropped_crew_chief_warnings_and_penalties_are_recognised(string folder, string kind)
        {
            Assert.AreEqual(kind, NativeRaceControl.Classify(new[] { folder }));
            Assert.AreEqual(TeamMember.Ben, NativeRaceControl.Member(kind));
            Assert.IsTrue(RaceExclusion.Allowed(NativeRaceControl.Topic(kind)), "Pflichtmeldung auch nach Rennausschluss");
        }

        [Test]
        public void New_penalties_stay_with_the_penalty_watch_so_nothing_is_said_twice()
        {
            Assert.IsNull(NativeRaceControl.Classify(new[] { "penalties/you_have_a_penalty" }));
            Assert.IsNull(NativeRaceControl.Classify(new[] { "penalties/new_penalty_slowdown" }));
            Assert.IsNull(NativeRaceControl.Classify(new[] { "penalties/cut_track_in_race" }), "Cut-Warnungen zählt die Zwischenfallwache");
        }

        [Test]
        public void Every_hint_has_four_to_six_variants_in_each_language_and_names_the_driver()
        {
            var kinds = new[] { "pen-warn:conduct", "pen-warn:wrongway", "pen-warn:", "pen-black", "pen-meatball", "pen-dq", "pen-still" };
            foreach (string language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                foreach (string kind in kinds)
                {
                    var options = NativeRaceControl.Options(kind);
                    Assert.IsNotNull(options, kind);
                    Assert.That(options.Length, Is.InRange(4, 6), language + " " + kind);
                    Assert.AreEqual(options.Length, options.Distinct().Count(), language + " " + kind);
                    Assert.IsTrue(options.All(o => o.Contains("Rob") && !o.Contains("{n}")), language + " " + kind);
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
            Assert.IsTrue(NativeRaceControl.Options("pen-warn:conduct").All(o => o.Contains("Verwarnung") || o.Contains("verwarnt")));
            Assert.IsTrue(NativeRaceControl.Options("pen-warn:conduct").All(o => o.EndsWith("Grund: unsportliches Fahren.", StringComparison.Ordinal)));
        }

        [Test]
        public void Without_a_name_the_hint_is_still_a_proper_sentence()
        {
            Assert.AreEqual("Du hast noch eine offene Strafe.", NativeRaceControl.Lead("{n}, du hast noch eine offene Strafe.", ""));
            Assert.AreEqual("Achtung, Verwarnung der Rennleitung.", NativeRaceControl.Lead("Achtung {n}, Verwarnung der Rennleitung.", null));
            Assert.AreEqual("Verwarnung für uns. Jetzt sauber bleiben.", NativeRaceControl.Lead("Verwarnung für uns, {n}. Jetzt sauber bleiben.", " "));
        }

        private static GameStateData Race(int laps = 5)
        {
            var gs = new GameStateData(T.Ticks);
            gs.Now = T; gs.SourceTelemetryUtc = T;
            gs.SessionData.SessionId = 3; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.CompletedLaps = laps; gs.SessionData.CurrentLapIsValid = true;
            gs.PositionAndMotionData.CarSpeed = 50;
            return gs;
        }

        [Test]
        public void A_race_control_warning_reaches_Ben_when_only_the_ai_team_speaks()
        {
            var w = new LiveWarningDispatcher();
            var gs = Race();
            var message = new QueuedMessage("penalties/warning_unsportsmanlike_driving", 0);
            message.messageFolders = new List<string> { "penalties/warning_unsportsmanlike_driving" };
            Assert.IsTrue(w.Capture(message, gs, T), "früher: stumm verworfen");
            w.Observe(gs, T.AddMilliseconds(100));
            var call = w.Take(gs, T.AddMilliseconds(200), false, _ => true);
            Assert.IsNotNull(call);
            Assert.AreEqual("warning-rc-pen-warn-conduct", call.Topic);
            Assert.AreEqual(TeamMember.Ben, call.Member);
            StringAssert.StartsWith("Rob", call.Text);
            StringAssert.Contains("unsportliches Fahren", call.Text);
        }

        [Test]
        public void Disqualification_at_the_flag_is_left_to_the_session_end_call()
        {
            var gs = Race();
            Assert.IsTrue(NativeRaceControl.StillCurrent("pen-dq", gs));
            gs.SessionData.SessionPhase = SessionPhase.Finished;
            Assert.IsFalse(NativeRaceControl.StillCurrent("pen-dq", gs));
            gs.PitData.InPitlane = true;
            Assert.IsFalse(NativeRaceControl.StillCurrent("pen-still", gs), "in der Box wird die Strafe gerade abgesessen");
        }

        private static int TelOff(string f) => System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2VehicleTelemetry), f).ToInt32();
        private static int InfoOff(string f) => System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2ScoringInfo), f).ToInt32();
        private static int ScoOff(string f) => System.Runtime.InteropServices.Marshal.OffsetOf(typeof(rF2SharedMemory.LMUData.rF2VehicleScoring), f).ToInt32();

        private static void PublishLmu(int steps, DateTime at)
        {
            var b = new byte[LMUNativeLayout.TotalSize];
            b[LMUNativeLayout.PlayerHasVehicleOffset] = 1; b[LMUNativeLayout.ActiveVehiclesOffset] = 2; b[LMUNativeLayout.PlayerIndexOffset] = 1;
            int v = LMUNativeLayout.TelemetryVehiclesOffset + LMUNativeLayout.VehicleTelemetrySize;
            b[v + TelOff("mLapInvalidated")] = 0; b[v + TelOff("mTrackLimitsSteps")] = (byte)steps;
            b[LMUNativeLayout.ScoringOffset + InfoOff("mTrackLimitsStepsPerPoint")] = 2; b[LMUNativeLayout.ScoringOffset + InfoOff("mTrackLimitsStepsPerPenalty")] = 8;
            BitConverter.GetBytes(2).CopyTo(b, LMUNativeLayout.ScoringCountOffset);
            for (int i = 0; i < 2; i++)
            {
                int o = LMUNativeLayout.ScoringVehiclesOffset + i * LMUNativeLayout.VehicleScoringSize;
                b[o + ScoOff("mPlace")] = (byte)(i == 0 ? 12 : 13);
                b[o + ScoOff("mIsPlayer")] = (byte)(i == 1 ? 1 : 0);
            }
            LMUNativeSnapshot.Reset();
            LMUNativeSnapshot.Observe(b, at);
            Assert.IsNotNull(LMUNativeSnapshot.Fresh(at));
        }

        [TestCase(0, true)]
        [TestCase(4, false)]
        public void Lmu_race_cut_warning_is_spoken_when_lmu_counts_no_track_limit_steps(int steps, bool spoken)
        {
            // LMU Sebring 23.11., 21:42:13: Cut-Warnung 1 → 2, mTrackLimitsSteps blieb 0 – Mik schwieg.
            PublishLmu(steps, T);
            var watch = new IncidentWatch();
            var before = Race(); before.PenaltiesData.CutTrackWarnings = 1;
            var now = Race(); now.PenaltiesData.CutTrackWarnings = 2;
            var said = watch.Evaluate(before, now, T, false);
            Assert.AreEqual(spoken, said.Any(x => x.Key == TeamMember.Mik), string.Join(" | ", said.Select(x => x.Value)));
        }
    }
}
