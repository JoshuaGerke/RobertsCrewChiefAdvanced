using System;
using System.IO;
using System.Linq;
using System.Text;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RaceFactStoreTests
    {
        private readonly DateTime now = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
        private string Root()
        {
            string root = Path.Combine(Environment.GetEnvironmentVariable("RCCA_DATA_ROOT") ?? Path.GetTempPath(),
                "fact-memory-cases", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); return root;
        }
        private RaceFactContext Context(string car = "BMW M4 GT3", string track = "Monza", string layout = "Monza/1/5793m",
            string compound = "Soft", string conditions = "rain:none/surface:dry", string game = "RaceRoom")
        { return new RaceFactContext(game, car, track, layout, "GT3", compound, conditions); }
        private LapRecord Lap(int number = 1, float seconds = 109.90643f, float fuel = 2.5f)
        { return new LapRecord { LapNumber = number, LapTime = seconds, Valid = true, FuelUsed = fuel, FuelAtEnd = 30, RecordedUtc = now }; }
        private RaceFactStore Store(string folder, bool enabled = true)
        { return new RaceFactStore(folder, enabled, () => now); }
        private bool Add(RaceFactStore store, RaceFactContext context, string session, LapRecord lap)
        { return store.ObserveCompletedLap(context, lap, session, "Practice", -1); }
        private void Flush(RaceFactStore store)
        { Assert.IsTrue(store.Flush(TimeSpan.FromSeconds(5)), store.Status); }

        [Test]
        public void Restart_preserves_observed_pb_units_source_time_and_historical_context()
        {
            string folder = Root(); var context = Context();
            using (var store = Store(folder))
            {
                string session = store.BeginSession(context, "Practice", -1);
                Assert.IsTrue(Add(store, context, session, Lap())); Flush(store);
            }
            using (var store = Store(folder))
            {
                var result = store.Query(context, now);
                Assert.AreEqual(1, result.Laps.Count);
                Assert.AreEqual(109.90643f, result.BestObservedLapSeconds);
                Assert.AreEqual(2.5f, result.AverageCleanFuelLitres);
                Assert.AreEqual(now, result.Laps[0].ObservedUtc);
                Assert.AreEqual(RaceFactContext.TelemetrySource, result.Laps[0].Source);
                Assert.AreEqual(RaceFactStore.SchemaVersion, result.Laps[0].SchemaVersion);
                StringAssert.Contains("historische", store.PromptBlock(context, now));
                StringAssert.Contains("keine aktuellen Messwerte", store.PromptBlock(context, now));
                StringAssert.Contains("keine vollständige persönliche Rekordliste", store.PromptBlock(context, now));
            }
        }

        [Test]
        public void Dispose_finishes_pending_writer_and_captures_values_instead_of_mutable_lap()
        {
            string folder = Root(); var context = Context();
            using (var store = Store(folder))
            {
                string session = store.BeginSession(context, "Practice", -1); var lap = Lap();
                Assert.IsTrue(Add(store, context, session, lap)); lap.LapTime = 1; lap.FuelUsed = 99;
            }
            using (var reopened = Store(folder))
            {
                Assert.AreEqual(109.90643f, reopened.Query(context, now).BestObservedLapSeconds);
                Assert.AreEqual(2.5f, reopened.Query(context, now).AverageCleanFuelLitres);
            }
        }

        [Test]
        public void Duplicates_and_reordered_laps_are_rejected_but_same_simulator_id_can_begin_new_session()
        {
            using (var store = Store(Root()))
            {
                var context = Context(); string first = store.BeginSession(context, "Practice", -1);
                Assert.IsTrue(Add(store, context, first, Lap(2)));
                Assert.IsFalse(Add(store, context, first, Lap(2)));
                Assert.IsFalse(Add(store, context, first, Lap(1)));
                string second = store.BeginSession(context, "Practice", -1);
                Assert.AreNotEqual(first, second);
                Assert.IsFalse(Add(store, context, first, Lap(3)));
                Assert.IsTrue(Add(store, context, second, Lap(1, 108)));
                Assert.AreEqual(2, store.Query(context, now).Laps.Count);
                Flush(store);
            }
        }

        [TestCase(false, false, 100f)]
        [TestCase(true, true, 100f)]
        [TestCase(true, false, -1f)]
        [TestCase(true, false, float.NaN)]
        [TestCase(true, false, float.PositiveInfinity)]
        [TestCase(true, false, 0f)]
        public void Invalid_pit_or_nonfinite_laps_never_become_facts(bool valid, bool pit, float seconds)
        {
            using (var store = Store(Root()))
            {
                var context = Context(); string session = store.BeginSession(context, "Practice", -1);
                var lap = Lap(); lap.Valid = valid; lap.InPitLap = pit; lap.LapTime = seconds;
                Assert.IsFalse(Add(store, context, session, lap)); Assert.AreEqual(0, store.Count);
            }
        }

        [Test]
        public void Car_track_layout_game_compound_and_condition_mismatch_never_retrieve_other_context()
        {
            using (var store = Store(Root()))
            {
                var context = Context(); string session = store.BeginSession(context, "Practice", -1);
                Add(store, context, session, Lap());
                foreach (var other in new[] { Context(car: "Porsche"), Context(track: "Spa"), Context(layout: "Monza/short"),
                    Context(game: "AMS2"), Context(compound: "Medium"), Context(conditions: "rain:heavy/surface:wet") })
                { Assert.AreEqual(0, store.Query(other, now).Laps.Count); Assert.AreEqual("", store.PromptBlock(other, now)); }
                Assert.IsFalse(Add(store, Context(car: "Porsche"), session, Lap(2)));
                string next = store.BeginSession(Context(car: "Porsche"), "Practice", -1);
                Assert.IsTrue(Add(store, Context(car: "Porsche"), next, Lap(1)));
                Assert.AreEqual(1, store.Query(context, now).Laps.Count);
                Flush(store);
            }
        }

        [Test]
        public void Missing_identity_does_not_establish_comparability_or_capture_a_fact()
        {
            using (var store = Store(Root()))
                foreach (var context in new[] { Context(car: null), Context(game: null), Context(track: null), Context(layout: null) })
                {
                    Assert.IsNull(store.BeginSession(context, "Practice", 1));
                    Assert.IsFalse(Add(store, context, "arbitrary", Lap()));
                    Assert.AreEqual(0, store.Query(context, now).Laps.Count);
                }
        }

        [Test]
        public void Session_type_and_id_mismatch_are_rejected_and_unknown_new_session_invalidates_previous_uid()
        {
            using (var store = Store(Root()))
            {
                var context = Context(); string session = store.BeginSession(context, "Practice", -1);
                Assert.IsFalse(store.ObserveCompletedLap(context, Lap(), session, "Race", -1));
                Assert.IsFalse(store.ObserveCompletedLap(context, Lap(), session, "Practice", 2));
                Assert.IsNull(store.BeginSession(Context(car: null), "Practice", -1));
                Assert.IsFalse(Add(store, context, session, Lap()));
                Assert.AreEqual(0, store.Count);
            }
        }

        [Test]
        public void Fuel_uses_only_positive_clean_samples_and_unknown_conditions_or_compound_disable_comparison()
        {
            using (var store = Store(Root()))
            {
                var context = Context(); string session = store.BeginSession(context, "Practice", -1);
                foreach (float fuel in new[] { -1f, 0f, float.NaN, float.PositiveInfinity, 251f })
                    Assert.IsTrue(Add(store, context, session, Lap(store.Count + 1, fuel: fuel)));
                Assert.IsNull(store.Query(context, now).AverageCleanFuelLitres);
                Assert.IsTrue(Add(store, context, session, Lap(6, fuel: 2)));
                Assert.IsTrue(Add(store, context, session, Lap(7, fuel: 4)));
                Assert.AreEqual(3, store.Query(context, now).AverageCleanFuelLitres);
                foreach (var unknown in new[] { Context(conditions: null), Context(compound: null), Context(conditions: "rain:none/surface:unknown") })
                {
                    string other = store.BeginSession(unknown, "Practice", -1);
                    Assert.IsTrue(Add(store, unknown, other, Lap()));
                    Assert.AreEqual(1, store.Query(unknown, now).Laps.Count);
                    Assert.IsNull(store.Query(unknown, now).AverageCleanFuelLitres);
                }
                Flush(store);
            }
        }

        [Test]
        public void Corrupt_primary_is_preserved_and_valid_backup_recovers_readonly()
        {
            string folder = Root(), path; var context = Context();
            using (var store = Store(folder))
            {
                path = store.FilePath; string session = store.BeginSession(context, "Practice", -1);
                Add(store, context, session, Lap()); Flush(store);
                Add(store, context, session, Lap(2, 108)); Flush(store);
            }
            byte[] corrupt = Encoding.UTF8.GetBytes("{broken primary"); File.WriteAllBytes(path, corrupt);
            using (var store = Store(folder))
            {
                Assert.AreEqual(1, store.Query(context, now).Laps.Count); StringAssert.Contains("corrupt primary preserved", store.Status);
                string session = store.BeginSession(context, "Practice", -1); Assert.IsTrue(Add(store, context, session, Lap(3)));
                Assert.IsFalse(store.Flush(TimeSpan.FromSeconds(1))); CollectionAssert.AreEqual(corrupt, File.ReadAllBytes(path));
            }
        }

        [TestCase("{bad json", "corrupt")]
        [TestCase("{\"schema_version\":2,\"facts\":[]}", "version")]
        [TestCase("{\"schema_version\":1,\"facts\":[1]}", "corrupt")]
        public void Malformed_or_unknown_version_files_are_ignored_without_overwriting_evidence(string content, string reason)
        {
            string folder = Root(), path = Path.Combine(folder, "telemetrie-fakten-v1.json"); File.WriteAllText(path, content);
            using (var store = Store(folder))
            {
                Assert.AreEqual(0, store.Count); StringAssert.Contains(reason, store.Status);
                var context = Context(); var session = store.BeginSession(context, "Practice", -1); Add(store, context, session, Lap());
                Assert.IsFalse(store.Flush(TimeSpan.FromSeconds(1))); Assert.AreEqual(content, File.ReadAllText(path));
            }
        }

        [Test]
        public void Oversized_file_is_not_loaded_or_replaced_and_disabled_store_creates_nothing()
        {
            string folder = Root(), path = Path.Combine(folder, "telemetrie-fakten-v1.json");
            File.WriteAllBytes(path, new byte[RaceFactStore.MaxFileBytes + 1]);
            using (var store = Store(folder))
            { Assert.AreEqual(0, store.Count); StringAssert.Contains("oversize", store.Status); }
            Assert.AreEqual(RaceFactStore.MaxFileBytes + 1, new FileInfo(path).Length);
            string disabled = Path.Combine(folder, "disabled");
            using (var store = Store(disabled, false))
            {
                Assert.IsNull(store.BeginSession(Context(), "Practice", -1)); Assert.AreEqual("", store.PromptBlock(Context(), now));
                Assert.IsFalse(Add(store, Context(), "unused", Lap())); Assert.IsTrue(store.Flush(TimeSpan.FromSeconds(1)));
            }
            Assert.IsFalse(Directory.Exists(disabled));
        }

        [Test]
        public void Io_failure_is_contained_and_capture_query_and_prompt_do_not_wait_for_locked_file()
        {
            using (var store = Store(Root()))
            {
                var context = Context(); string session = store.BeginSession(context, "Practice", -1);
                Add(store, context, session, Lap()); Flush(store);
                using (new FileStream(store.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.IsTrue(Add(store, context, session, Lap(2)));
                    Assert.AreEqual(2, store.Query(context, now).Laps.Count);
                    StringAssert.Contains("historische", store.PromptBlock(context, now));
                    Assert.IsFalse(store.Flush(TimeSpan.FromSeconds(2))); StringAssert.Contains("save failed", store.Status);
                }
                Assert.IsTrue(Add(store, context, session, Lap(3))); Flush(store);
            }
        }

        [Test]
        public void Expired_and_future_values_are_not_returned_as_current_comparable_history()
        {
            string folder = Root(); var context = Context();
            using (var store = Store(folder))
            {
                string session = store.BeginSession(context, "Practice", -1);
                var expired = Lap(); expired.RecordedUtc = now - RaceFactStore.MaxAge - TimeSpan.FromSeconds(1);
                var future = Lap(); future.RecordedUtc = now + TimeSpan.FromMinutes(1);
                Assert.IsFalse(Add(store, context, session, expired)); Assert.IsFalse(Add(store, context, session, future));
                Add(store, context, session, Lap()); Flush(store);
                Assert.AreEqual(0, store.Query(context, now - TimeSpan.FromSeconds(1)).Laps.Count);
                Assert.AreEqual(0, store.Query(context, now + RaceFactStore.MaxAge + TimeSpan.FromSeconds(1)).Laps.Count);
            }
            using (var reopened = new RaceFactStore(folder, true, () => now + RaceFactStore.MaxAge + TimeSpan.FromSeconds(1)))
                Assert.AreEqual(0, reopened.Count);
        }

        [Test]
        public void Store_and_prompt_are_bounded_and_long_distinct_labels_do_not_merge()
        {
            string folder = Root(); var context = Context(car: new string('a', 100) + "one");
            using (var store = Store(folder))
            {
                string session = store.BeginSession(context, "Practice", -1);
                for (int lap = 1; lap <= 400; lap++) Assert.IsTrue(Add(store, context, session, Lap(lap)));
                Flush(store); Assert.AreEqual(RaceFactStore.MaxFacts, store.Count);
                Assert.LessOrEqual(new FileInfo(store.FilePath).Length, RaceFactStore.MaxFileBytes);
                Assert.Less(store.PromptBlock(context, now).Length, 2500);
                Assert.AreEqual(0, store.Query(Context(car: new string('a', 100) + "two"), now).Laps.Count);
            }
            using (var reopened = Store(folder)) Assert.AreEqual(RaceFactStore.MaxFacts, reopened.Query(context, now).Laps.Count);
        }

        [Test]
        public void Long_and_sanitized_identity_labels_remain_canonical_and_match_exactly_after_restart()
        {
            foreach (string rawCar in new[] { new string('a', 100), "BMW \"M4\" <GT3>\nEdition#1" })
            {
                string folder = Root(); var original = Context(car: rawCar);
                Assert.LessOrEqual(original.Car.Length, 96);
                Assert.AreEqual(original.Car, Context(car: original.Car).Car, "Canonical labels must be idempotent.");
                using (var store = Store(folder))
                {
                    string session = store.BeginSession(original, "Practice", -1);
                    Assert.IsTrue(Add(store, original, session, Lap())); Flush(store);
                }
                using (var reopened = Store(folder))
                {
                    var newContextFromTelemetry = Context(car: rawCar);
                    var recovered = reopened.Query(newContextFromTelemetry, now);
                    Assert.AreEqual(1, recovered.Laps.Count, reopened.Status);
                    Assert.AreEqual(original.Car, recovered.Laps[0].Context.Car);
                    Assert.IsTrue(newContextFromTelemetry.Matches(recovered.Laps[0].Context));
                    Assert.AreEqual(0, reopened.Query(Context(car: rawCar + "different"), now).Laps.Count,
                        "Truncating or removing unsupported characters must not merge distinct source labels.");
                }
            }
        }

        [Test]
        public void FromGameState_does_not_invent_missing_identity_compound_or_conditions()
        {
            var context = RaceFactContext.FromGameState("RaceRoom", new GameStateData(0), now);
            Assert.IsFalse(context.IdentityKnown); Assert.IsFalse(context.FuelComparable);
            Assert.AreEqual(RaceFactContext.Unknown, context.Compound); Assert.AreEqual(RaceFactContext.Unknown, context.Conditions);
        }

        [Test]
        public void Invalid_persisted_measurement_is_not_promoted_to_a_fact()
        {
            string folder = Root(), path;
            using (var store = Store(folder))
            {
                path = store.FilePath; var context = Context(); string session = store.BeginSession(context, "Practice", -1);
                Add(store, context, session, Lap()); Flush(store);
            }
            var document = JObject.Parse(File.ReadAllText(path)); document["facts"][0]["valid"] = false; File.WriteAllText(path, document.ToString());
            using (var reopened = Store(folder)) { Assert.AreEqual(0, reopened.Count); StringAssert.Contains("corrupt", reopened.Status); }
        }
    }
}
