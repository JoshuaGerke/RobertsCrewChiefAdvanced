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
    /// RaceRoom Hockenheim 25.11.2026 (Hotlap, Training, 15-Minuten-Rennen, Start P6, Ziel P6) und Astra-Prüfbericht zu Alpha 0.38:
    /// falscher Startplatz aus der ersten Rennsitzung, Ben jubelte beim Platzverlust und behauptete „du bist schneller“, der
    /// Trainingsvergleich nahm die falsche eigene Bestzeit, die Verteidigung nach hinten und der erste Topspeed verfielen ungehört,
    /// die Bestzeiten-Datenbank blieb für RaceRoom leer und Rob vermisste die Bestzeit-Referenz zu Beginn.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class Hockenheim25FindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 25, 20, 32, 0, DateTimeKind.Utc);
        private static readonly string[] Field = { "Keller", "Malik", "Pacak", "Swayzee" };
        private string wasLanguage;

        [SetUp]
        public void SetUp() { wasLanguage = RadioLanguage.Current; RadioLanguage.Current = RadioLanguage.German; RaceStartLatch.Reset(); BattleVoice.Reset(); }

        [TearDown]
        public void TearDown() { RadioLanguage.Current = wasLanguage; RaceStartLatch.Reset(); BattleVoice.Reset(); RadioVariants.Deterministic = false; }

        // ---------- Startplatz ----------

        [Test]
        public void The_grid_place_before_the_start_wins_over_the_first_reported_start_place()
        {
            var s = new GameStateData(T.Ticks).SessionData;
            s.SessionId = 2; s.SessionType = SessionType.Race; s.SessionStartClassPosition = 4;
            s.SessionPhase = SessionPhase.Countdown; s.ClassPosition = 6;
            Assert.AreEqual(6, RaceStartLatch.Position(s), "Startaufstellung: Platz 6, nicht die 4 vom Anlegen der Sitzung");
            s.SessionPhase = SessionPhase.Green;
            Assert.AreEqual(6, RaceStartLatch.Position(s));
            s.ClassPosition = 7;
            Assert.AreEqual(6, RaceStartLatch.Position(s), "mit Grün festgehalten");
            s.SessionPhase = SessionPhase.Checkered; s.ClassPosition = 6;
            Assert.AreEqual(6, RaceStartLatch.Position(s));
        }

        [Test]
        public void Without_a_grid_observation_the_first_start_place_of_the_sim_stays()
        {
            var s = new GameStateData(T.Ticks).SessionData;
            s.SessionId = 3; s.SessionType = SessionType.Race; s.SessionPhase = SessionPhase.Green; s.SessionStartClassPosition = 9; s.ClassPosition = 8;
            Assert.AreEqual(9, RaceStartLatch.Position(s));
            s.SessionStartClassPosition = 4;
            Assert.AreEqual(9, RaceStartLatch.Position(s));
        }

        // ---------- Bens Sprachprüfung ----------

        [TestCase("Keller fällt zurück, du kannst durchatmen.", "Keller holt auf, 0,8 Sekunden hinter dir.", "direction_flipped")]
        [TestCase("Keller holt auf, pass auf.", "Keller fällt zurück, der Abstand wächst.", "direction_flipped")]
        [TestCase("Keller hat wohl einen Reifenschaden, er verliert Zeit.", "Keller verliert Zeit, Abstand jetzt 2,1 Sekunden.", "invented_cause:reifenschaden")]
        [TestCase("Jawohl, Swayzee ist vorbei, wir sind auf Platz 7 zurückgefallen!", "Swayzee ist vorbei. Platz 7.", "cheer_on_loss:jawohl")]
        [TestCase("Pacak ist wieder vorbei, aber du bist schneller.", "Pacak ist wieder vorbei. Platz 7.", "unproven_claim")]
        public void A_rewording_that_changes_the_meaning_falls_back_to_the_fixed_sentence(string candidate, string template, string expected)
        {
            string name = Field.First(n => template.Contains(n));
            Assert.IsNull(BattleVoice.Validate(candidate, template, new List<string> { name }, Field, new List<string>(), out string why), candidate);
            Assert.AreEqual(expected, why);
        }

        [TestCase("Keller kommt näher, 0,8 Sekunden. Jetzt keine Fehler.", "Keller holt auf, 0,8 Sekunden hinter dir.")]
        [TestCase("Swayzee ist vorbei, Platz 7. Kopf hoch, dranbleiben.", "Swayzee ist vorbei. Platz 7.")]
        [TestCase("Platz 6, Pacak ist geschluckt! Stark gemacht.", "Platz 6. Vorbei an Pacak.")]
        public void A_rewording_with_the_same_meaning_is_still_accepted(string candidate, string template)
        {
            string name = Field.First(n => template.Contains(n));
            Assert.IsNotNull(BattleVoice.Validate(candidate, template, new List<string> { name }, Field, new List<string>(), out string why), why);
        }

        [Test]
        public void A_counter_is_never_explained_with_being_quicker()
        {
            foreach (var language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                for (int v = 0; v < 12; v++)
                {
                    string line = DriverJudgement.Countered("Rob", "Patrick Swayzee", v);
                    StringAssert.DoesNotContain("schneller", line); StringAssert.DoesNotContain("quicker", line); StringAssert.DoesNotContain("más rápido", line);
                }
            }
        }

        [Test]
        public void Other_class_traffic_makes_no_promise_about_the_other_driver()
        {
            foreach (var language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                for (int v = 0; v < 10; v++)
                {
                    string line = MulticlassTraffic.Ahead("GT4", v);
                    StringAssert.DoesNotContain("der macht dir Platz", line); StringAssert.DoesNotContain("he'll make room", line);
                    StringAssert.DoesNotContain("te dejará sitio", line);
                }
            }
        }

        // ---------- Bestzeit-Referenz zu Beginn von Training und Quali ----------

        private static GameStateData Practice()
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 7; gs.SessionData.SessionType = SessionType.Practice; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.TrackDefinition = new TrackDefinition("Hockenheimring Modern", 4574f);
            gs.carName = "BMW M4 GT3";
            gs.PositionAndMotionData.CarSpeed = 45;
            return gs;
        }

        private static PersonalBest Stored(GameStateData gs)
        {
            var ctx = RaceFactContext.FromGameState("RaceRoom", gs, T);
            return new PersonalBest
            {
                Game = ctx.Game, Car = ctx.Car, Track = ctx.Track, Layout = ctx.Layout, LapSeconds = 95.698f, FuelAtStart = 32, TrackC = 22, AirC = 33,
                DateUtc = T.AddDays(-12), SessionType = "Practice"
            };
        }

        [Test]
        public void The_reference_names_time_fuel_track_and_air_in_four_wordings()
        {
            RadioVariants.Deterministic = false;
            var pb = new PersonalBest { LapSeconds = 95.698f, FuelAtStart = 32, TrackC = 22, AirC = 33, DateUtc = DateTime.MinValue };
            foreach (var language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                var lines = Enumerable.Range(0, 80).Select(i => PersonalBestWatch.Briefing(pb, -1, float.NaN, "Rob", i)).ToList();
                var openings = lines.Select(t => t.Substring(0, t.IndexOf("1:35", StringComparison.Ordinal))).Distinct().Count();
                Assert.That(openings, Is.InRange(4, 6), language);
                foreach (var line in lines)
                {
                    StringAssert.StartsWith("Rob, ", line);
                    StringAssert.Contains("1:35", line); StringAssert.Contains("32", line); StringAssert.Contains("22", line); StringAssert.Contains("33", line);
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
            string first = PersonalBestWatch.Briefing(pb, -1, float.NaN, "Rob", 0);
            StringAssert.Contains("32 Litern Sprit", first);
            StringAssert.Contains("Das ist deine Referenz.", first);
            var none = new PersonalBest { LapSeconds = 95.698f, FuelAtStart = -1, TrackC = float.NaN, AirC = float.NaN, DateUtc = DateTime.MinValue };
            StringAssert.DoesNotContain("Grad", PersonalBestWatch.Briefing(none, -1, float.NaN, "Rob", 0), "fehlende Werte werden weggelassen");
        }

        [Test]
        public void Ben_gives_the_reference_and_it_comes_back_when_it_was_not_heard()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rrca-pb-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new PersonalBestStore(folder, "RaceRoom");
                var gs = Practice();
                Assert.IsTrue(store.Offer(Stored(gs), out _));
                var watch = new PersonalBestWatch();
                watch.Observe(gs, store, "RaceRoom", "Rob", T);
                watch.Observe(gs, store, "RaceRoom", "Rob", T.AddSeconds(9));
                var call = watch.Take(gs, T.AddSeconds(9), false, true, true);
                Assert.IsNotNull(call);
                Assert.AreEqual("personal-best-brief", call.Topic);
                Assert.AreEqual(TeamMember.Ben, call.Member);
                StringAssert.Contains("1:35,698", call.Text);
                Assert.GreaterOrEqual((call.ExpiresUtc - call.DetectedUtc).TotalSeconds, 120, "Rob funkt gerade: die Referenz wartet");
                Assert.IsNull(watch.Take(gs, T.AddSeconds(10), false, true, true), "einmal vorgemerkt, nicht doppelt");

                // Nicht abgespielt und verfallen: beim nächsten Losfahren noch einmal.
                var late = T.AddSeconds(140);
                Assert.IsNull(watch.Take(gs, late, false, true, true));
                watch.Observe(gs, store, "RaceRoom", "Rob", late);
                watch.Observe(gs, store, "RaceRoom", "Rob", late.AddSeconds(9));
                var again = watch.Take(gs, late.AddSeconds(9), false, true, true);
                Assert.IsNotNull(again, "nicht gehört heißt nicht erledigt");
                Assert.AreEqual("personal-best-brief", again.Topic);

                // Gesprochen: kein weiterer Anlauf in dieser Sitzung.
                again.MarkPlaybackStarted(late.AddSeconds(10)); again.MarkPlaybackFinished(late.AddSeconds(18), false);
                Assert.IsNull(watch.Take(gs, late.AddSeconds(20), false, true, true));
                watch.Observe(gs, store, "RaceRoom", "Rob", late.AddSeconds(30));
                watch.Observe(gs, store, "RaceRoom", "Rob", late.AddSeconds(40));
                Assert.IsNull(watch.Take(gs, late.AddSeconds(40), false, true, true));
            }
            finally { try { System.IO.Directory.Delete(folder, true); } catch (Exception) { } }
        }

        [Test]
        public void Without_ben_mik_gives_the_reference()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rrca-pb-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new PersonalBestStore(folder, "RaceRoom");
                var gs = Practice();
                Assert.IsTrue(store.Offer(Stored(gs), out _));
                var watch = new PersonalBestWatch();
                watch.Observe(gs, store, "RaceRoom", "Rob", T);
                watch.Observe(gs, store, "RaceRoom", "Rob", T.AddSeconds(9));
                var call = watch.Take(gs, T.AddSeconds(9), false, true, false);
                Assert.IsNotNull(call);
                Assert.AreEqual(TeamMember.Mik, call.Member);
            }
            finally { try { System.IO.Directory.Delete(folder, true); } catch (Exception) { } }
        }

        [Test]
        public void Without_a_known_car_the_reference_is_not_given_up()
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rrca-pb-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new PersonalBestStore(folder, "RaceRoom");
                var gs = Practice();
                Assert.IsTrue(store.Offer(Stored(gs), out _));
                string car = gs.carName; gs.carName = null;
                var watch = new PersonalBestWatch();
                watch.Observe(gs, store, "RaceRoom", "Rob", T);
                watch.Observe(gs, store, "RaceRoom", "Rob", T.AddSeconds(9));
                Assert.IsNull(watch.Take(gs, T.AddSeconds(9), false, true, true));
                gs.carName = car; // Automodell kommt später an
                watch.Observe(gs, store, "RaceRoom", "Rob", T.AddSeconds(12));
                Assert.IsNotNull(watch.Take(gs, T.AddSeconds(12), false, true, true));
            }
            finally { try { System.IO.Directory.Delete(folder, true); } catch (Exception) { } }
        }

        // ---------- Verteidigung nach hinten ----------

        [Test]
        public void Rear_pressure_waits_long_enough_to_be_heard()
        {
            var state = new GameStateData(T.Ticks);
            state.SessionData.SessionId = 41; state.SessionData.SessionType = SessionType.Race;
            state.SessionData.SessionPhase = SessionPhase.Green; state.SessionData.ClassPosition = 6;
            state.carClass = new CarData.CarClass(); state.PositionAndMotionData.CarSpeed = 40;
            state.SessionData.CompletedLaps = 3;
            state.SessionData.OpponentKeyBehind = "malik";
            state.SessionData.LapsDeltaBehind = 0;
            state.OpponentData.Add("malik", new OpponentData { DriverRawName = "Simon Malik", IsActive = true, ClassPosition = 7,
                CompletedLaps = 3, CarClass = state.carClass });
            var observer = new ActiveRaceObserver();
            observer.Observe(state, T, "Rob", 0);
            state.SessionData.TimeDeltaBehind = 1.6f;
            observer.ObserveRearPressure(new RaceDataContext { GameState = state, NowUtc = T, LastTelemetryUtc = T, Memory = new AiRaceMemory() },
                new RaceGapMeasurement(1.6f, RaceGapKind.SimulatorTiming));
            var at = T.AddSeconds(3);
            state.SessionData.TimeDeltaBehind = 0.9f;
            observer.ObserveRearPressure(new RaceDataContext { GameState = state, NowUtc = at, LastTelemetryUtc = at, Memory = new AiRaceMemory() },
                new RaceGapMeasurement(0.9f, RaceGapKind.SimulatorTiming));
            var call = observer.Take(state, at, false, false, false, 0, null, "Rob");
            Assert.IsNotNull(call);
            Assert.AreEqual("rear-pressure", call.Topic);
            Assert.GreaterOrEqual((call.ExpiresUtc - call.DetectedUtc).TotalSeconds, 20, "Hockenheim: zweimal nach 10 s ungehört verfallen");
            Assert.GreaterOrEqual(call.Priority, 5);
        }
    }
}
