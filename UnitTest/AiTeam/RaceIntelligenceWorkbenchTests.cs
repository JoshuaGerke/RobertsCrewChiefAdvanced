using System;
using System.IO;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RaceIntelligenceWorkbenchTests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 31, 19, 0, 0, DateTimeKind.Utc);
        private static GameStateData State(SessionPhase phase = SessionPhase.Green)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 7; gs.SessionData.SessionType = SessionType.Race;
            gs.SessionData.SessionPhase = phase; gs.SessionData.ClassPosition = 10;
            gs.SessionData.CompletedLaps = 1; gs.SessionData.CurrentLapIsValid = true;
            gs.SessionData.TrackDefinition = new TrackDefinition("Fixture", 4000);
            gs.carClass = new CarData.CarClass(); gs.PositionAndMotionData.CarSpeed = 40;
            gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            gs.OpponentData["rival"] = new OpponentData { DriverRawName = "Opponent", ClassPosition = 9,
                CarClass = gs.carClass, CompletedLaps = 1, DistanceRoundTrack = 1010, Speed = 40, IsActive = true };
            return gs;
        }
        private static void Pass(GameStateData gs)
        { gs.SessionData.ClassPosition = 9; gs.OpponentData["rival"].ClassPosition = 10; gs.OpponentData["rival"].DistanceRoundTrack = 990; }
        private static void Confirm(ActiveRaceObserver observer, GameStateData gs, int start = 0)
        { foreach (int ms in new[] { 100, 200, 350 }) observer.Observe(gs, T.AddMilliseconds(start + ms), "Rob", 0); }
        private static ObservedRaceCall Take(ActiveRaceObserver observer, GameStateData gs, int ms = 350)
        { return observer.Take(gs, T.AddMilliseconds(ms), false, false, false, 0, null, "Rob"); }

        [TestCase("cut")] [TestCase("invalid")] [TestCase("offtrack")]
        public void New_track_limit_evidence_turns_gain_into_neutral_classification(string reason)
        {
            var gs = State(); var observer = new ActiveRaceObserver(); observer.Observe(gs, T, "Rob", 0);
            Pass(gs);
            if (reason == "cut") gs.PenaltiesData.CutTrackWarnings++;
            if (reason == "invalid") gs.SessionData.CurrentLapIsValid = false;
            if (reason == "offtrack") gs.PenaltiesData.IsOffRacingSurface = true;
            Confirm(observer, gs);
            var call = Take(observer, gs); Assert.IsNotNull(call);
            Assert.AreEqual("stable_rank_with_track_limit_uncertainty", call.Evidence);
            StringAssert.Contains("Position 9", call.Text);
            StringAssert.DoesNotContain("Sauber", call.Text); StringAssert.DoesNotContain("erkämpft", call.Text);
        }

        [TestCase("cut")] [TestCase("invalid")] [TestCase("offtrack")]
        public void Late_track_limit_evidence_invalidates_praise_before_audio(string reason)
        {
            var gs = State(); var observer = new ActiveRaceObserver(); observer.Observe(gs, T, "Rob", 0);
            Pass(gs); Confirm(observer, gs); var call = Take(observer, gs);
            Assert.IsNotNull(call); Assert.AreEqual("stable_rank_and_same_active_rival_crossing", call.Evidence);
            Assert.IsTrue(call.IsValid(gs, T.AddMilliseconds(400)));
            if (reason == "cut") gs.PenaltiesData.CutTrackWarnings++;
            if (reason == "invalid") gs.SessionData.CurrentLapIsValid = false;
            if (reason == "offtrack") gs.PenaltiesData.IsOffRacingSurface = true;
            Assert.IsFalse(call.IsValid(gs, T.AddMilliseconds(650)));
        }

        [Test]
        public void Already_invalid_lap_and_old_cut_do_not_suppress_a_later_clean_pass()
        {
            var gs = State(); gs.SessionData.CurrentLapIsValid = false; gs.PenaltiesData.CutTrackWarnings = 2;
            var observer = new ActiveRaceObserver(); observer.Observe(gs, T, "Rob", 0);
            Pass(gs); Confirm(observer, gs);
            Assert.AreEqual("stable_rank_and_same_active_rival_crossing", Take(observer, gs).Evidence);
        }

        [Test]
        public void Prior_lap_invalidity_is_not_a_new_cut_at_start_finish()
        {
            var gs = State(); var observer = new ActiveRaceObserver(); observer.Observe(gs, T, "Rob", 0);
            gs.SessionData.CompletedLaps++; gs.SessionData.CurrentLapIsValid = false;
            gs.OpponentData["rival"].CompletedLaps++; Pass(gs); Confirm(observer, gs);
            Assert.AreEqual("stable_rank_and_same_active_rival_crossing", Take(observer, gs).Evidence);
        }

        [Test]
        public void Formation_support_survives_rolling_samples_but_expires_at_green()
        {
            var gs = State(SessionPhase.Formation); var watch = new RaceSituationWatch();
            var call = watch.Evaluate(null, gs, T, "Robert").Single();
            Assert.AreEqual("formation", call.Topic); StringAssert.StartsWith("Robert,", call.Text);
            Assert.IsTrue(call.IsValid(gs, T.AddSeconds(1)));
            var observer = new ActiveRaceObserver(); observer.Observe(gs, T, "Robert", 0); observer.Enqueue(call);
            observer.Observe(gs, T.AddMilliseconds(100), "Robert", 0);
            Assert.IsNull(observer.Take(gs, T.AddMilliseconds(100), true, false, false, 0, null, "Robert"));
            Assert.AreSame(call, observer.Take(gs, T.AddMilliseconds(200), false, false, false, 0, null, "Robert"));
            call.ReleaseForRetry(T.AddMilliseconds(300));
            observer.Observe(gs, T.AddSeconds(1), "Robert", 0);
            Assert.AreSame(call, observer.Take(gs, T.AddSeconds(1.4), false, false, false, 0, null, "Robert"));
            gs.SessionData.SessionPhase = SessionPhase.Green;
            Assert.IsFalse(call.IsValid(gs, T.AddSeconds(2)));
        }

        [Test]
        public void Formation_is_bounded_varied_and_prepared_for_selected_voice()
        {
            var gs = State(SessionPhase.Formation); var watch = new RaceSituationWatch();
            string previous = null; int count = 0;
            for (int second = 0; second <= 360; second++)
                foreach (var call in watch.Evaluate(gs, gs, T.AddSeconds(second), "Rob"))
                {
                    Assert.AreEqual("formation", call.Topic); Assert.AreNotEqual(previous, call.Text);
                    Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Ben, call.Text));
                    Assert.Contains(call.Text, RaceSituationWatch.PreparedPhrases("Rob").ToList());
                    previous = call.Text; count++;
                }
            Assert.AreEqual(3, count);
        }

        [TestCase(SessionType.Practice)] [TestCase(SessionType.Qualify)]
        public void Non_race_formation_does_not_invent_a_race_start(SessionType type)
        {
            var gs = State(SessionPhase.Formation); gs.SessionData.SessionType = type;
            Assert.IsEmpty(new RaceSituationWatch().Evaluate(null, gs, T));
        }

        [TestCase(SessionPhase.Green)] [TestCase(SessionPhase.Countdown)] [TestCase(SessionPhase.Garage)]
        public void Joining_other_phases_does_not_invent_a_formation(SessionPhase phase)
        { Assert.IsEmpty(new RaceSituationWatch().Evaluate(null, State(phase), T)); }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)] [TestCase(100f)]
        public void Invalid_formation_speed_is_silent(float speed)
        {
            var gs = State(SessionPhase.Formation); gs.PositionAndMotionData.CarSpeed = speed;
            Assert.IsEmpty(new RaceSituationWatch().Evaluate(null, gs, T));
        }

        [Test]
        public void New_session_gets_formation_support_but_pitlane_does_not()
        {
            var gs = State(SessionPhase.Formation); var watch = new RaceSituationWatch();
            gs.PitData.InPitlane = true; Assert.IsEmpty(watch.Evaluate(null, gs, T));
            gs.PitData.InPitlane = false; Assert.AreEqual(1, watch.Evaluate(gs, gs, T.AddSeconds(1)).Count);
            gs.SessionData.SessionId++; Assert.AreEqual(1, watch.Evaluate(gs, gs, T.AddSeconds(2)).Count);
        }

        [Test]
        public void Actual_formation_to_green_produces_a_fresh_start_call()
        {
            var before = State(SessionPhase.Formation); var after = State();
            var call = new RaceSituationWatch().Evaluate(before, after, T, "Rob").Single();
            Assert.AreEqual("race-start", call.Topic); Assert.IsTrue(call.IsValid(after, T.AddMilliseconds(100)));
            Assert.IsFalse(call.IsValid(after, T.AddSeconds(7)));
            after.SessionData.SessionPhase = SessionPhase.Formation; Assert.IsFalse(call.IsValid(after, T.AddSeconds(1)));
        }

        [Test]
        public void Minimal_mode_still_suppresses_optional_formation_encouragement()
        {
            var gs = State(SessionPhase.Formation); var observer = new ActiveRaceObserver(); observer.Observe(gs, T, "Rob", 0);
            observer.Enqueue(new RaceSituationWatch().Evaluate(null, gs, T).Single());
            Assert.IsNull(observer.Take(gs, T, false, false, true, 0, null, "Rob"));
        }

        [TestCase("complete", true, "plausibel_keine_genauigkeitsgarantie")]
        [TestCase("cumulative", false, "sektorsumme_widerspricht_rundenzeit")]
        [TestCase("missing", false, "unvollstaendig")]
        [TestCase("nan", false, "unvollstaendig")]
        [TestCase("invalid", false, "ungueltige_runde")]
        [TestCase("pit", false, "boxenrunde")]
        [TestCase("no-lap-time", false, "rundenzeit_fehlt")]
        public void Opponent_sector_quality_prevents_false_comparisons(string scenario, bool comparable, string expected)
        {
            var gs = State(); var rival = gs.OpponentData["rival"];
            var lap = new LapData { LapNumber = 1, LapTime = 90, SectorTimes = new[] { 30f, 30f, 30f } };
            if (scenario == "cumulative") lap.SectorTimes = new[] { 30f, 60f, 90f };
            if (scenario == "missing") lap.SectorTimes = new[] { 30f };
            if (scenario == "nan") lap.SectorTimes[1] = float.NaN;
            if (scenario == "invalid") lap.IsValid = false;
            if (scenario == "pit") lap.InLap = true;
            if (scenario == "no-lap-time") lap.LapTime = -1;
            rival.OpponentLapData.Add(lap);
            var ctx = new RaceDataContext { GameState = gs, NowUtc = T, LastTelemetryUtc = T, SendDriverNames = true };
            var data = RaceDataTools.Execute("get_opponent", ctx, "{\"name\":\"Opponent\"}")["fahrer"][0]["sektorrunden"][0];
            Assert.AreEqual(comparable, (bool)data["fuer_pacevergleich_geeignet"]);
            Assert.AreEqual(expected, (string)data["qualitaet"]);
            if (scenario == "cumulative") Assert.AreEqual(90, (double)data["sektorsumme_abweichung_s"]);
            if (scenario == "no-lap-time") Assert.AreEqual(JTokenType.Null, data["sektorsumme_plausibel"].Type);
        }

        [Test]
        public void Completion_diagnostics_retain_safe_metadata_and_still_reject_headers()
        {
            string root = Environment.GetEnvironmentVariable("RCCA_DATA_ROOT"); Assert.IsNotEmpty(root);
            string folder = Path.Combine(root, "workbench-diagnostics", Guid.NewGuid().ToString("N"));
            using (var recorder = new DiagnosticRecorder(folder))
                recorder.TryRecord("api.completion", LiveDiagnostics.Data("model", "fixture-model", "finish_reason", "length",
                    "completion_tokens", 1536L, "reasoning_tokens", 1400L, "content_chars", 0, "completion_limit", 1536,
                    "Authorization", "private-header", "request_body", "private-body"));
            var record = Directory.GetFiles(folder, "*.jsonl").SelectMany(File.ReadLines).Select(JObject.Parse)
                .Single(row => (string)row["kind"] == "api.completion")["data"];
            Assert.AreEqual("length", (string)record["finish_reason"]);
            Assert.AreEqual(1400L, (long)record["reasoning_tokens"]);
            Assert.AreEqual(0, (int)record["content_chars"]); Assert.AreEqual(1536, (int)record["completion_limit"]);
            Assert.IsNull(record["Authorization"]); Assert.IsNull(record["request_body"]);
        }
    }
}
