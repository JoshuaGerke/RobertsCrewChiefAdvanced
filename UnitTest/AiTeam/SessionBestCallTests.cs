using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Rob 24.11. (AMS2 Cascais, Training): Sektor-Bestzeiten im Training und Qualifying sofort und kurz, nach Sektor 1 und 2
    /// „das könnte deine schnellste Runde werden“, bei der schnellsten Runde Lob und Motivation.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class SessionBestCallTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 24, 18, 45, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData Frame(DateTime t, int completed, int sector, float[] sectors, float seedS1 = -1, float seedS2 = -1,
            float seedLap = -1, SessionType type = SessionType.Practice)
        {
            var g = new GameStateData(t.Ticks);
            g.Now = t; g.SourceTelemetryUtc = t;
            g.SessionData.SessionId = 42; g.SessionData.SessionType = type; g.SessionData.SessionPhase = SessionPhase.Green;
            g.SessionData.CompletedLaps = completed; g.SessionData.SectorNumber = sector; g.SessionData.CurrentLapIsValid = true;
            g.SessionData.PlayerBestSector1Time = seedS1; g.SessionData.PlayerBestSector2Time = seedS2;
            g.SessionData.PlayerLapTimeSessionBest = seedLap;
            g.PositionAndMotionData.CarSpeed = 50; g.carClass = new CarData.CarClass();
            if (sectors != null) g.SessionData.PlayerLapData.Add(new LapData { LapNumber = completed + 1, IsValid = true, SectorTimes = sectors });
            return g;
        }

        private static GameStateData LapEnd(DateTime t, int completed, float seedS1 = -1, float seedS2 = -1, float seedLap = -1)
        {
            var g = Frame(t, completed, 1, null, seedS1, seedS2, seedLap);
            g.SessionData.IsNewLap = true;
            return g;
        }

        private static bool IsOneOf(string text, string[] pool) =>
            pool.Any(p => text.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0); // „Rob, …“ schreibt den Satzanfang klein

        [Test]
        public void Cascais_improvement_against_a_seeded_sim_best_is_called_live_and_honestly()
        {
            // Funk startet nach sechs Runden; die Sim liefert S1 36,0 und Runde 1:37,640 als Bestmarken mit.
            var watch = new SessionLapObserver();
            var f0 = Frame(T, 2, 1, null, 36.0f, 27.4f, 97.64f);
            watch.Observe(f0, null, T, false, null, true, null, "Rob");
            var f1 = Frame(T.AddSeconds(36), 2, 2, new[] { 36.724f, -1f, -1f }, 36.0f, 27.4f, 97.64f);
            watch.Observe(f1, null, f1.Now, false, f0, true, null, "Rob");
            var none = watch.Take(f1, f1.Now, false);
            Assert.IsTrue(none == null || none.Topic != "sector-best", "langsamer als die mitgelieferte Marke, noch nichts selbst gesehen");
            var end = LapEnd(T.AddSeconds(99), 3, 36.0f, 27.4f, 97.64f);
            watch.Observe(end, new LapRecord { LapNumber = 3, LapTime = 99.285f, Valid = true, SectorTimes = new[] { 36.724f, 27.8f, 34.761f } },
                end.Now, false, f1, true, null, "Rob");
            watch.Take(end, end.Now, false);

            var g0 = Frame(T.AddSeconds(130), 3, 1, null, 36.0f, 27.4f, 97.64f);
            watch.Observe(g0, null, g0.Now, false, end, true, null, "Rob");
            var g1 = Frame(T.AddSeconds(135.5), 3, 2, new[] { 36.560f, -1f, -1f }, 36.0f, 27.4f, 97.64f);
            watch.Observe(g1, null, g1.Now, false, g0, true, null, "Rob");
            var call = watch.Take(g1, g1.Now, true, true, ownLapOnly: false) ?? watch.Take(g1, g1.Now, false, true, true);
            Assert.IsNotNull(call, "früher: not_faster_than_best_by_0.04s, Ansage erst eine Runde später");
            Assert.AreEqual("sector-best", call.Topic);
            StringAssert.Contains("0,164", call.Text);
            Assert.IsTrue(IsOneOf(call.Text, SessionBestPhrases.SectorImproved(0)), call.Text);
            StringAssert.DoesNotContain("Bestzeit", call.Text, "die Sim kennt eine schnellere Zeit: keine „persönliche Bestzeit“");
        }

        [Test]
        public void Sector_one_and_two_personal_bests_say_this_could_be_the_fastest_lap()
        {
            var watch = new SessionLapObserver();
            var f0 = Frame(T, 2, 1, null, 36.5f, 28.0f, 99f);
            watch.Observe(f0, null, T, false, null, true, null, "Rob");
            var f1 = Frame(T.AddSeconds(36), 2, 2, new[] { 36.3f, -1f, -1f }, 36.5f, 28.0f, 99f);
            watch.Observe(f1, null, f1.Now, false, f0, true, null, "Rob");
            var first = watch.Take(f1, f1.Now, false, true, true);
            Assert.IsNotNull(first);
            Assert.AreEqual("sector-best", first.Topic);
            Assert.IsTrue(IsOneOf(first.Text, SessionBestPhrases.SectorPersonalBest(0)), first.Text);
            StringAssert.StartsWith("Rob, ", first.Text, "der Name kommt jedes zweite Mal");
            Assert.Less(first.Text.Length, 80, first.Text);
            first.MarkPlaybackStarted(f1.Now);

            var f2 = Frame(T.AddSeconds(64), 2, 3, new[] { 36.3f, 27.9f, -1f }, 36.5f, 28.0f, 99f);
            watch.Observe(f2, null, f2.Now, false, f1, true, null, "Rob");
            var second = watch.Take(f2, f2.Now, false, true, true);
            Assert.IsNotNull(second);
            Assert.AreEqual("sector-best", second.Topic);
            Assert.IsTrue(IsOneOf(second.Text, SessionBestPhrases.SecondAfterFirstBest), second.Text);
            StringAssert.Contains("schnellste", second.Text);
        }

        [Test]
        public void Unheard_sector_one_is_folded_into_one_call_for_both_sectors()
        {
            var watch = new SessionLapObserver();
            var f0 = Frame(T, 2, 1, null, 36.5f, 28.0f, 99f);
            watch.Observe(f0, null, T, false, null, true, null, "Rob");
            var f1 = Frame(T.AddSeconds(36), 2, 2, new[] { 36.3f, -1f, -1f }, 36.5f, 28.0f, 99f);
            watch.Observe(f1, null, f1.Now, false, f0, true, null, "Rob");
            // Funk belegt: Sektor 1 wartet noch, als Sektor 2 fertig ist.
            var f2 = Frame(T.AddSeconds(64), 2, 3, new[] { 36.3f, 27.9f, -1f }, 36.5f, 28.0f, 99f);
            watch.Observe(f2, null, f2.Now, false, f1, true, null, "Rob");
            var call = watch.Take(f2, f2.Now, false, true, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("sector-best", call.Topic);
            Assert.IsFalse(IsOneOf(call.Text, SessionBestPhrases.SecondAfterFirstBest), "„auch im zweiten“ ohne gehörten ersten Sektor: " + call.Text);
            Assert.IsNull(watch.Take(f2, f2.Now.AddSeconds(1), false, true, true), "nur eine Meldung für beide Sektoren");
        }

        [Test]
        public void Sector_best_in_practice_does_not_wait_for_eight_seconds_of_silence()
        {
            var watch = new SessionLapObserver();
            var f0 = Frame(T, 2, 1, null, 36.5f, -1, 99f);
            watch.Observe(f0, null, T, false);
            var f1 = Frame(T.AddSeconds(36), 2, 2, new[] { 36.3f, -1f, -1f }, 36.5f, -1, 99f);
            watch.Observe(f1, null, f1.Now, false, f0);
            var call = watch.Take(f1, f1.Now, false, routineBusy: true, ownLapOnly: true);
            Assert.IsNotNull(call, "Sektor-Bestzeit kommt im ersten Durchgang, auch wenn Routine-Funk ruht");
            Assert.AreEqual("sector-best", call.Topic);
        }

        [Test]
        public void Race_sector_rules_are_unchanged()
        {
            var watch = new SessionLapObserver();
            var f0 = Frame(T, 2, 1, null, 36.5f, -1, 99f, SessionType.Race);
            watch.Observe(f0, null, T, false);
            var f1 = Frame(T.AddSeconds(36), 2, 2, new[] { 36.48f, -1f, -1f }, 36.5f, -1, 99f, SessionType.Race);
            watch.Observe(f1, null, f1.Now, false, f0);
            Assert.IsNull(watch.Take(f1, f1.Now, false, false, true), "im Rennen weiterhin erst ab 0,04 s");
        }

        [Test]
        public void Lap_faster_than_everything_seen_but_slower_than_the_seeded_best_is_called_as_improvement()
        {
            var watch = new SessionLapObserver();
            var f0 = Frame(T, 6, 1, null, -1, -1, 97.64f);
            watch.Observe(f0, null, T, false);
            var e1 = LapEnd(T.AddSeconds(99), 7, -1, -1, 97.64f);
            watch.Observe(e1, new LapRecord { LapNumber = 7, LapTime = 99.285f, Valid = true }, e1.Now, false, f0);
            var c1 = watch.Take(e1, e1.Now, false);
            Assert.IsTrue(c1 == null || c1.Topic != "own-lap", "erste gesehene Runde, langsamer als die mitgelieferte: kein Lob");
            var e2 = LapEnd(T.AddSeconds(198), 8, -1, -1, 97.64f);
            watch.Observe(e2, new LapRecord { LapNumber = 8, LapTime = 98.863f, Valid = true }, e2.Now, false, e1);
            var c2 = watch.Take(e2, e2.Now, false);
            Assert.IsNotNull(c2, "Cascais: 1:38,863 nach 1:39,285 blieb stumm");
            Assert.AreEqual("own-lap", c2.Topic);
            StringAssert.Contains("38,863", c2.Text);
            StringAssert.Contains("persönliche", c2.Text);
            StringAssert.Contains("1,2", c2.Text, "Rest auf die persönliche Bestzeit");
            Assert.IsFalse(c2.Text.Contains("neue persönliche Bestzeit") || c2.Text.Contains("Neue persönliche Bestzeit"), c2.Text);
        }

        [Test]
        public void New_personal_best_lap_gets_short_praise_and_motivation()
        {
            var watch = new SessionLapObserver();
            var f0 = Frame(T, 2, 1, null, -1, -1, 99f, SessionType.Qualify);
            watch.Observe(f0, null, T, false);
            var e1 = LapEnd(T.AddSeconds(98), 3, -1, -1, 99f);
            e1.SessionData.SessionType = SessionType.Qualify;
            watch.Observe(e1, new LapRecord { LapNumber = 3, LapTime = 98.2f, Valid = true }, e1.Now, false, f0);
            var call = watch.Take(e1, e1.Now, false);
            Assert.IsNotNull(call);
            Assert.AreEqual("own-lap", call.Topic);
            Assert.IsTrue(SessionBestPhrases.LapBestMotivation.Any(m => call.Text.EndsWith(m, StringComparison.Ordinal)), call.Text);
        }

        [Test]
        public void New_phrases_have_five_distinct_variants_in_every_language_and_name_own_bests_correctly()
        {
            foreach (string language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                var pools = new[] { SessionBestPhrases.SectorPersonalBest(0), SessionBestPhrases.SectorPersonalBest(1), SessionBestPhrases.SectorPersonalBest(2),
                    SessionBestPhrases.SectorImproved(0), SessionBestPhrases.SectorImproved(2), SessionBestPhrases.SecondAfterFirstBest,
                    SessionBestPhrases.SecondAfterFirstImproved, SessionBestPhrases.PurpleSuffix, SessionBestPhrases.LapBestMotivation,
                    SessionBestPhrases.LapImproved("1:38,145", "0,5 Sekunden") };
                foreach (var pool in pools)
                {
                    Assert.That(pool.Length, Is.InRange(4, 6), language);
                    Assert.AreEqual(pool.Length, pool.Distinct().Count(), language);
                    if (language == RadioLanguage.German)
                        foreach (var line in pool)
                            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(line, @"(?<!persönliche )(?<!persönlichen )Bestzeit",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                                "Rob 20.11.: eigene Zeit heißt „persönliche Bestzeit“: " + line);
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
            Assert.AreEqual("Rob, verbessert im ersten Sektor.", RadioPhraseCatalog.Address("Verbessert im ersten Sektor.", "Rob"));
        }
    }
}
