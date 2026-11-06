using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class NativeTimingEventsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 4, 14, 0, 0, DateTimeKind.Utc);
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly Type TimingsType = typeof(CrewChief).Assembly.GetType("CrewChiefV4.Events.Timings");
        private bool savedPitting, savedFormation;
        [SetUp]
        public void SaveNativeGlobalContext()
        {
            savedPitting = CrewChiefV4.Events.PitStops.isPittingThisLap;
            savedFormation = GameStateData.onManualFormationLap;
            CrewChiefV4.Events.PitStops.isPittingThisLap = false; GameStateData.onManualFormationLap = false;
        }
        private static GameStateData State()
        {
            var s = new GameStateData(T.Ticks);
            s.SourceTelemetryUtc = T;
            s.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            s.SessionData.SessionId = 14; s.SessionData.SessionType = SessionType.Race;
            s.SessionData.SessionStartTime = T.AddMinutes(-5); s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.ClassPosition = 4; s.SessionData.CompletedLaps = 3;
            s.SessionData.TrackDefinition = new TrackDefinition("Native timing test", 5000);
            s.SessionData.OpponentKeyInFront = "original-front"; s.SessionData.OpponentKeyBehind = "original-behind";
            s.SessionData.LapsDeltaFront = s.SessionData.LapsDeltaBehind = 0;
            s.SessionData.TimeDeltaFront = 4.2f; s.SessionData.TimeDeltaBehind = 5.2f;
            s.PositionAndMotionData.DistanceRoundTrack = 1000;
            s.OpponentData["original-front"] = new OpponentData {
                DriverRawName = "Nick Heidfeld", CarNumber = "17", CostId = 127, CarClass = s.carClass,
                ClassPosition = 3, OverallPosition = 3, CompletedLaps = 3, IsActive = true, DistanceRoundTrack = 1200, Speed = 50
            };
            s.OpponentData["original-behind"] = new OpponentData {
                DriverRawName = "Alex Wurz", CarNumber = "18", CostId = 128, CarClass = s.carClass,
                ClassPosition = 5, OverallPosition = 5, CompletedLaps = 3, IsActive = true, DistanceRoundTrack = 750, Speed = 50
            };
            return s;
        }
        private static void Advance(GameStateData s, double seconds)
        { s.Now = s.SourceTelemetryUtc = T.AddSeconds(seconds); }
        private static NativeTimingFact Fact(GameStateData s, bool ahead = true, NativeTimingTrend trend = NativeTimingTrend.Decreasing)
        {
            return NativeTimingFact.Capture(s, ahead ? "original-front" : "original-behind",
                ahead ? NativeTimingRelation.ClassAhead : NativeTimingRelation.ClassBehind, trend,
                ahead ? s.SessionData.TimeDeltaFront : s.SessionData.TimeDeltaBehind, 0);
        }
        private static NativeTimingEvents Observe(GameStateData s, NativeTimingFact fact = null, AiRaceMemory memory = null, bool names = true)
        {
            var q = new NativeTimingEvents(); q.Capture(fact ?? Fact(s)); q.Observe(s, s.Now, memory, names); return q;
        }
        [TearDown]
        public void RemoveHandoff()
        {
            TimingsType.GetField("NativeTimingHandoff", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            CrewChiefV4.Events.PitStops.isPittingThisLap = savedPitting; GameStateData.onManualFormationLap = savedFormation;
        }

        [Test]
        public void Semantic_snapshot_keeps_original_key_class_session_source_and_identity_as_values()
        {
            var s = State(); var f = Fact(s);
            Assert.IsNotNull(f); Assert.AreEqual("original-front", f.Key); Assert.AreEqual("Nick Heidfeld", f.Name);
            Assert.AreEqual("17", f.CarNumber); Assert.AreEqual("GT3", f.OpponentClass);
            Assert.AreEqual(T, f.SourceUtc); Assert.AreEqual(T.AddMinutes(-5), f.SessionStart);
            Assert.AreEqual(0, f.SignedLapDelta); Assert.AreEqual(3, f.OpponentLap);
            s.OpponentData["original-front"].DriverRawName = "Reused slot";
            Assert.AreEqual("Nick Heidfeld", f.Name); Assert.IsFalse(NativeTimingEvents.StillCurrent(f, s, T));
        }
        [TestCase(true, "native-gap-front")] [TestCase(false, "native-gap-behind")]
        public void Native_class_trend_is_delivered_once_and_does_not_create_another_detector(bool ahead, string topic)
        {
            var s = State(); var memory = new AiRaceMemory(); var f = Fact(s, ahead); var q = Observe(s, f, memory);
            var call = q.Take(s, T, false, true, false); Assert.IsNotNull(call); Assert.AreEqual(topic, call.Topic);
            StringAssert.Contains(ahead ? "Nick Heidfeld" : "Alex Wurz", call.Text);
            StringAssert.DoesNotContain("überholen", call.Text); Assert.IsTrue(call.IsValid(s, T));
            call.MarkPlaybackStarted(T); q.Capture(f); q.Observe(s, T.AddSeconds(.1), memory, true);
            Assert.IsNull(q.Take(s, T.AddSeconds(.1), false, true, false));
            Assert.AreEqual(1, memory.GetEvents(TimeSpan.FromMinutes(1), T).Count);
        }
        [Test]
        public void Gap_phrases_rotate_per_direction_even_when_front_and_rear_reports_alternate()
        {
            var q = new NativeTimingEvents();
            var s = State();
            var fronts = new System.Collections.Generic.HashSet<string>();
            var rears = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 8; i++)
            {
                bool ahead = i % 2 == 0;
                Advance(s, i * 31);
                q.Capture(Fact(s, ahead));
                q.Observe(s, s.Now, null, true);
                var call = q.Take(s, s.Now, false, true, false);
                Assert.IsNotNull(call);
                Assert.IsTrue((ahead ? fronts : rears).Add(call.Text));
                StringAssert.Contains(ahead ? "4,2 Sekunden" : "5,2 Sekunden", call.Text);
                call.MarkPlaybackStarted(s.Now);
            }
        }

        [Test]
        public void Gap_variant_advances_only_after_playback_starts()
        {
            var q = new NativeTimingEvents();
            var s = State();
            string first = null;
            for (int i = 0; i < 3; i++)
            {
                Advance(s, i * 31);
                q.Capture(Fact(s));
                q.Observe(s, s.Now, null, true);
                var call = q.Take(s, s.Now, false, true, false);
                Assert.IsNotNull(call);
                if (i == 0) first = call.Text;
                if (i == 1)
                {
                    Assert.AreEqual(first, call.Text);
                    call.MarkPlaybackStarted(s.Now);
                }
                if (i == 2) Assert.AreNotEqual(first, call.Text);
            }
        }

        [Test]
        public void Freshness_is_checked_at_capture_take_and_playback_without_expiring_during_normal_tts_rendering()
        {
            var s = State(); var q = Observe(s); var call = q.Take(s, T, false, true, false);
            Advance(s, 2.5); Assert.IsTrue(call.IsValid(s, s.Now), "A fresh, unchanged live source survives a normal rendering delay.");
            Assert.IsFalse(call.IsValid(s, T.AddSeconds(3.6)), "A frozen current source is never re-aged from its decision time.");
            Advance(s, 8.1); Assert.IsFalse(call.IsValid(s, s.Now), "Native decisions still have a bounded lifetime.");
            s = State(); s.SourceTelemetryUtc = T.AddSeconds(-1.1); Assert.IsNull(Fact(s));
            s = State(); q = Observe(s); s.SourceTelemetryUtc = T.AddSeconds(-1.1);
            Assert.IsNull(q.Take(s, T, false, true, false));
        }
        [Test]
        public void Fresh_live_source_can_take_a_recent_decision_but_stale_incoming_and_clock_rollback_are_discarded()
        {
            var s = State(); var f = Fact(s); var q = new NativeTimingEvents(); q.Capture(f);
            Advance(s, 2); q.Observe(s, s.Now, null, true); Assert.IsNotNull(q.Take(s, s.Now, false, true, false));
            q = new NativeTimingEvents(); q.Capture(f); Advance(s, 8.1); q.Observe(s, s.Now, null, true);
            Assert.IsNull(q.Take(s, s.Now, false, true, false));
            Assert.IsFalse(NativeTimingEvents.StillCurrent(f, State(), T.AddMilliseconds(-1)));
        }
        [TestCase("neighbor")] [TestCase("session")] [TestCase("start")] [TestCase("type")]
        [TestCase("class")] [TestCase("lap")] [TestCase("own-lap")] [TestCase("lap-diff")]
        [TestCase("pit")] [TestCase("own-pit")] [TestCase("inactive")] [TestCase("number")]
        [TestCase("gap")] [TestCase("new-session")] [TestCase("outlap")] [TestCase("pit-request")]
        public void Queued_snapshot_is_cancelled_when_its_original_context_changes(string change)
        {
            var s = State(); var q = Observe(s); var call = q.Take(s, T, false, true, false);
            var op = s.OpponentData["original-front"];
            switch (change)
            {
                case "neighbor": s.SessionData.OpponentKeyInFront = "original-behind"; break;
                case "session": s.SessionData.SessionId++; break;
                case "start": s.SessionData.SessionStartTime = T; break;
                case "type": s.SessionData.SessionType = SessionType.Practice; break;
                case "class": op.CarClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.ALPINE }; break;
                case "lap": op.CompletedLaps++; break;
                case "own-lap": s.SessionData.CompletedLaps++; break;
                case "lap-diff": s.SessionData.LapsDeltaFront = 1; break;
                case "pit": op.InPits = true; break;
                case "own-pit": s.PitData.InPitlane = true; break;
                case "inactive": op.IsActive = false; break;
                case "number": op.CarNumber = "99"; break;
                case "gap": s.SessionData.TimeDeltaFront += .6f; break;
                case "new-session": s.SessionData.IsNewSession = true; break;
                case "outlap": s.PitData.OnOutLap = true; break;
                case "pit-request": CrewChiefV4.Events.PitStops.isPittingThisLap = true; break;
            }
            Assert.IsFalse(call.IsValid(s, T)); Assert.IsNull(q.Take(s, T, false, true, false));
        }
        [Test]
        public void Speech_uses_fresh_take_measurement_not_captured_native_number_and_rechecks_it_at_playback()
        {
            var s = State(); var q = Observe(s); Advance(s, .2); s.SessionData.TimeDeltaFront = 4.5f;
            var call = q.Take(s, s.Now, false, true, false); StringAssert.Contains("4,5 Sekunden", call.Text);
            StringAssert.DoesNotContain("4,2", call.Text); s.SessionData.TimeDeltaFront = 5.1f;
            Assert.IsFalse(call.IsValid(s, s.Now));
        }
        [Test]
        public void Derived_adapter_gap_is_qualitative_without_a_valid_same_point_tracker_result()
        {
            var s = State(); s.SessionData.TimeDeltaFrontIsDerived = true; var q = Observe(s);
            var call = q.Take(new RaceDataContext { GameState = s, NowUtc = T, LastTelemetryUtc = T, Tracks = new TrackAwareness() }, false, true, false);
            Assert.IsNotNull(call); StringAssert.DoesNotContain("Sekunden", call.Text); StringAssert.DoesNotContain("4,2", call.Text);
            q = Observe(s, Fact(s, true, NativeTimingTrend.Steady)); Assert.IsNull(q.Take(s, T, false, true, false));
        }
        [Test]
        public void Fresh_same_point_tracker_result_is_explicitly_approximate_and_revalidated()
        {
            var s = State(); s.SessionData.TimeDeltaFrontIsDerived = true; var tracks = new TrackAwareness();
            // Actual paired passages at the same five-metre points, two seconds apart.
            for (int i = 0; i <= 30; i++)
            {
                Advance(s, i / 10d); s.PositionAndMotionData.DistanceRoundTrack = 1000 + i * 5;
                s.OpponentData["original-front"].DistanceRoundTrack = 1100 + i * 5;
                tracks.Gaps.Observe(s, s.Now);
            }
            var f = Fact(s); var q = Observe(s, f);
            var call = q.Take(new RaceDataContext { GameState = s, NowUtc = s.Now, LastTelemetryUtc = s.Now, Tracks = tracks }, false, true, false);
            Assert.IsNotNull(call); StringAssert.Contains("ungefähr 2,0 Sekunden", call.Text);
            Advance(s, 4.1); Assert.IsFalse(call.IsValid(s, s.Now), "A stale tracker result cannot pass merely because the raw telemetry clock advanced.");
        }
        [Test]
        public void Track_relation_is_recorded_without_relabelling_it_as_class_behind_or_claiming_official_seconds()
        {
            var s = State(); var car = new OpponentData {
                DriverRawName = "Track car", CarNumber = "1", CarClass = s.carClass, ClassPosition = 1,
                CompletedLaps = 4, DistanceRoundTrack = 900, Speed = 50, IsActive = true
            };
            s.OpponentData["track"] = car;
            var f = NativeTimingFact.Capture(s, "track", NativeTimingRelation.BehindOnTrack, NativeTimingTrend.Decreasing, 2, -1);
            Assert.IsNotNull(f); Assert.AreEqual(-1, f.SignedLapDelta); var memory = new AiRaceMemory(); var q = Observe(s, f, memory);
            Assert.IsNull(q.Take(s, T, false, true, false)); Assert.AreEqual(1, memory.GetEvents(TimeSpan.FromMinutes(1), T).Count);
            StringAssert.DoesNotContain("Sekunden", memory.GetEvents(TimeSpan.FromMinutes(1), T)[0].Text);
            s.SessionData.OpponentKeyBehind = "track"; Assert.IsFalse(NativeTimingEvents.StillCurrent(f, s, T));
        }
        [TestCase(true, true, false)] [TestCase(false, false, false)] [TestCase(false, true, true)]
        public void Busy_disabled_and_minimal_respect_optional_radio_ownership(bool busy, bool enabled, bool minimal)
        { var s = State(); var q = Observe(s); Assert.IsNull(q.Take(s, T, busy, enabled, minimal)); }
        [Test]
        public void Repeated_capture_is_bounded_deduplicated_and_names_preference_is_respected()
        {
            var s = State(); var f = Fact(s); var q = new NativeTimingEvents();
            for (int i = 0; i < 1000; i++) q.Capture(f);
            Assert.LessOrEqual(q.IncomingCount, NativeTimingEvents.MaximumIncoming);
            var memory = new AiRaceMemory(); q.Observe(s, T, memory, false);
            Assert.AreEqual(1, q.PendingCount); Assert.AreEqual(1, memory.GetEvents(TimeSpan.FromMinutes(1), T).Count);
            StringAssert.DoesNotContain("Heidfeld", q.Take(s, T, false, true, false).Text);
            q.Reset(); Assert.AreEqual(0, q.PendingCount); Assert.AreEqual(0, q.IncomingCount);
        }
        [Test]
        public void Missing_or_mismatched_identity_and_ambiguous_class_never_become_a_substitute_neighbor()
        {
            var s = State();
            Assert.IsNull(NativeTimingFact.Capture(s, "missing", NativeTimingRelation.ClassAhead, NativeTimingTrend.Decreasing, 4.2f, 0));
            Assert.IsNull(NativeTimingFact.Capture(s, "original-behind", NativeTimingRelation.ClassAhead, NativeTimingTrend.Decreasing, 4.2f, 0));
            s.carClass = new CarData.CarClass(); s.OpponentData["original-front"].CarClass = new CarData.CarClass();
            Assert.IsNull(Fact(s));
        }
        [Test]
        public void A_second_native_decision_cannot_replace_a_reserved_delivery_or_bypass_real_playback_cooldown()
        {
            var s = State(); var q = Observe(s); var first = q.Take(s, T, false, true, false);
            Advance(s, .2); q.Capture(Fact(s, false)); q.Observe(s, s.Now, null, true);
            Assert.IsNull(q.Take(s, s.Now, false, true, false), "The first delivery is still rendering.");
            first.MarkPlaybackStarted(s.Now); Assert.IsNull(q.Take(s, s.Now, false, true, false));
            Advance(s, 2.5); Assert.IsNull(q.Take(s, s.Now, false, true, false), "Only actual playback starts the separation window.");
        }
        [Test]
        public void Native_same_car_window_checks_every_original_key_instead_of_the_first_key_three_times()
        {
            Type gapType = TimingsType.GetNestedType("Gap", BindingFlags.NonPublic);
            var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(gapType));
            list.Add(Activator.CreateInstance(gapType, new object[] { -4f, 0, "one" }));
            list.Add(Activator.CreateInstance(gapType, new object[] { -5f, 0, "two" }));
            list.Add(Activator.CreateInstance(gapType, new object[] { -6f, 0, "one" }));
            var timings = FormatterServices.GetUninitializedObject(TimingsType);
            Assert.IsFalse((bool)TimingsType.GetMethod("isSameCar", PrivateInstance).Invoke(timings, new object[] { list, 3 }));
        }
        [Test]
        public void Native_behind_status_includes_newest_sample_before_deciding_that_the_rival_is_closing()
        {
            var s = State(); s.SessionData.OverallPosition = 4;
            s.SessionData.NumCarsOverall = s.SessionData.NumCarsInPlayerClass = 6;
            s.SessionData.TimeDeltaBehind = 4.5f;
            // This fixture tests class timing only. No moving on-track candidate may reach
            // GetRelativeDelta without populated passage points (or show Debug.Assert UI).
            foreach (var opponent in s.OpponentData.Values) opponent.Speed = 0;
            var timings = FormatterServices.GetUninitializedObject(TimingsType);
            Type gapType = TimingsType.GetNestedType("Gap", BindingFlags.NonPublic);
            Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(gapType);
            var behindSamples = (IList)Activator.CreateInstance(listType);
            foreach (float seconds in new[] { 3f, 4f, 5f })
                behindSamples.Add(Activator.CreateInstance(gapType, new object[] { seconds, 0, "original-behind" }));
            Set(timings, "gapsInFront", Activator.CreateInstance(listType));
            Set(timings, "gapsBehind", behindSamples);
            Set(timings, "gapsBehindOnTrack", Activator.CreateInstance(listType));
            Set(timings, "gapBehindAtLastReport", -1f);
            object oldStatus = TimingsType.GetMethod("getGapStatus", PrivateInstance).Invoke(timings, new object[] { behindSamples, -1f });
            Assert.AreEqual("DECREASING", oldStatus.ToString(), "The previous three samples really were closing.");
            object gaps = TimingsType.GetMethod("_getGaps", PrivateInstance).Invoke(timings, new object[] { s });
            object behind = gaps.GetType().GetField("behind", PrivateInstance | BindingFlags.Public).GetValue(gaps);
            object currentStatus = behind.GetType().GetField("status", PrivateInstance | BindingFlags.Public).GetValue(behind);
            Assert.AreEqual("NONE", currentStatus.ToString(), "The newest increase to 4.5 seconds invalidates the old closing trend.");
            Assert.AreEqual(4.5f, gapType.GetField("timeDelta").GetValue(behindSamples[0]));
        }

        private static void Set(object target, string field, object value)
        { target.GetType().GetField(field, PrivateInstance | BindingFlags.Public).SetValue(target, value); }
        [Test]
        public void Selected_native_decision_handoff_skips_delayed_audio_and_keeps_configured_sector_thresholds()
        {
            var s = State(); s.SessionData.TrackDefinition.gapPoints = new[] { 250f };
            var timings = FormatterServices.GetUninitializedObject(TimingsType);
            Type gapType = TimingsType.GetNestedType("Gap", BindingFlags.NonPublic);
            Type decisionType = TimingsType.GetNestedType("gapToOpponent", BindingFlags.NonPublic);
            Type statusType = TimingsType.GetNestedType("GapStatus", BindingFlags.NonPublic);
            Func<string, object> decision = status => {
                var value = FormatterServices.GetUninitializedObject(decisionType);
                Set(value, "status", Enum.Parse(statusType, status)); return value;
            };
            object front = decision("DECREASING");
            Set(front, "gap", Activator.CreateInstance(gapType, new object[] { -4.2f, 0, "original-front" }));
            Set(front, "opponent", s.OpponentData["original-front"]);
            var gaps = FormatterServices.GetUninitializedObject(TimingsType.GetNestedType("Gaps", BindingFlags.NonPublic));
            Set(gaps, "inFront", front); Set(gaps, "behind", decision("NONE")); Set(gaps, "behindOnTrack", decision("NONE"));
            Set(timings, "gapAheadReportFrequency", 1); Set(timings, "gapBehindReportFrequency", 0); Set(timings, "gapBehindOnTrackReportFrequency", 0);
            Set(timings, "gapAheadMinSectorWait", 4); Set(timings, "gapAheadMaxSectorWait", 9);
            Set(timings, "sectorsSinceLastGapAheadReport", 4); Set(timings, "sectorsUntilNextGapAheadReport", 4);
            Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(gapType);
            Set(timings, "gapsInFront", Activator.CreateInstance(listType)); Set(timings, "gapsBehind", Activator.CreateInstance(listType));
            NativeTimingFact captured = null;
            TimingsType.GetField("NativeTimingHandoff", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null,
                new Func<NativeTimingFact, bool>(fact => { captured = fact; return true; }));
            // audioPlayer remains null: reaching the original delayed-audio path would fail this test.
            TimingsType.GetMethod("_announceGaps", PrivateInstance).Invoke(timings, new object[] { s, gaps });
            Assert.IsNotNull(captured); Assert.AreEqual("original-front", captured.Key); Assert.AreEqual(NativeTimingTrend.Decreasing, captured.Trend);
            Assert.AreEqual(0, TimingsType.GetField("sectorsSinceLastGapAheadReport", PrivateInstance).GetValue(timings));
            int next = (int)TimingsType.GetField("sectorsUntilNextGapAheadReport", PrivateInstance).GetValue(timings);
            Assert.That(next, Is.GreaterThanOrEqualTo(4).And.LessThan(9));
            TimingsType.GetField("NativeTimingHandoff", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, new Func<NativeTimingFact, bool>(fact => false));
            Assert.IsFalse((bool)TimingsType.GetMethod("TryNativeTimingHandoff", PrivateInstance).Invoke(timings, new object[] { s, front, NativeTimingRelation.ClassAhead }));
        }
    }
}
