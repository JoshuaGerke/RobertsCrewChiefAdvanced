using System;
using System.Linq;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Rob 21.11.: Am Start nur das Nötigste, danach Routine nach und nach, und die Routine passt sich der Rennlänge an.
    /// Grundlage: erste Runde RaceRoom Mantorp 21.11. (Grün 15:32:29, 22 Autos, 15 Minuten).
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class RaceRhythmTests
    {
        private static readonly DateTime Green = new DateTime(2026, 11, 21, 15, 32, 29, DateTimeKind.Utc);

        private static GameStateData Race(int laps, int place)
        {
            var g = new GameStateData(Green.Ticks);
            g.SessionData.SessionId = -1; g.SessionData.SessionType = SessionType.Race; g.SessionData.SessionPhase = SessionPhase.Green;
            g.SessionData.CompletedLaps = laps; g.SessionData.ClassPosition = place;
            g.SessionData.SessionHasFixedTime = true; g.SessionData.SessionTotalRunTime = 900;
            return g;
        }

        [Test]
        public void Field_counts_as_settled_after_the_first_lap_and_a_held_place()
        {
            Assert.IsFalse(RaceRhythm.Settled(0, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)), "erste Runde läuft");
            Assert.IsFalse(RaceRhythm.Settled(1, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)), "zu früh nach Grün");
            Assert.IsFalse(RaceRhythm.Settled(1, TimeSpan.FromSeconds(130), TimeSpan.FromSeconds(4)), "Platz wechselt noch");
            Assert.IsTrue(RaceRhythm.Settled(1, TimeSpan.FromSeconds(130), TimeSpan.FromSeconds(12)));
            Assert.IsTrue(RaceRhythm.Settled(0, TimeSpan.FromMinutes(3), TimeSpan.Zero), "spätestens nach 3 Minuten");
        }

        [Test]
        public void Start_phase_runs_from_green_until_the_field_settles()
        {
            var r = new RaceRhythm();
            var grid = Race(0, 18); grid.SessionData.SessionPhase = SessionPhase.Countdown;
            r.Observe(grid, Green.AddSeconds(-5));
            Assert.IsFalse(r.InStart(grid), "vor Grün ist keine Startphase");
            var g = Race(0, 18);
            r.Observe(g, Green);
            Assert.IsTrue(r.InStart(g));
            // Mantorp: Platz 18 → 14 → 20 → 19 in der ersten Runde, Runde 1 fertig um 15:34:38 auf Platz 21
            var lap1 = Race(1, 21);
            Assert.IsFalse(r.Observe(lap1, Green.AddSeconds(129)), "Platz gerade gewechselt");
            Assert.IsTrue(r.InStart(lap1));
            Assert.IsTrue(r.Observe(lap1, Green.AddSeconds(140)), "10 s auf Platz 21 gehalten");
            Assert.IsFalse(r.InStart(lap1));
            Assert.AreEqual(Green.AddSeconds(140), r.SettledUtc);
            Assert.AreEqual(RaceLength.Short, r.Length, "15 Minuten Mantorp");
        }

        [Test]
        public void Race_length_is_classified_from_time_or_laps()
        {
            Assert.AreEqual(RaceLength.Short, RaceRhythm.Classify(true, 900, 0, -1), "Mantorp 15 Minuten");
            Assert.AreEqual(RaceLength.Medium, RaceRhythm.Classify(true, 1800, 0, -1));
            Assert.AreEqual(RaceLength.Long, RaceRhythm.Classify(true, 3600, 0, -1));
            Assert.AreEqual(RaceLength.Short, RaceRhythm.Classify(false, 0, 5, -1), "Monza AC: 5 Runden");
            Assert.AreEqual(RaceLength.Medium, RaceRhythm.Classify(false, 0, 20, 100f), "20 × 1:40 = 33 Minuten");
            Assert.AreEqual(RaceLength.Long, RaceRhythm.Classify(false, 0, 12, 480f), "12 Runden Nordschleife");
            Assert.AreEqual(RaceLength.Long, RaceRhythm.Classify(false, 0, 40, -1));
            Assert.AreEqual(RaceLength.Unknown, RaceRhythm.Classify(false, 0, 0, -1));
        }

        [Test]
        public void Routine_spacing_follows_the_race_length()
        {
            Assert.AreEqual(2.0, RaceRhythm.Scale(RaceLength.Short, "fuel"), "kurz: Sprit halb so oft");
            Assert.AreEqual(1.0, RaceRhythm.Scale(RaceLength.Short, "status"), "kurz: Position wie eingestellt");
            Assert.AreEqual(0.85, RaceRhythm.Scale(RaceLength.Long, "tyres"), "lang: Reifen öfter");
            Assert.AreEqual(1.25, RaceRhythm.Scale(RaceLength.Long, "ahead"), "lang: Abstände etwas seltener");
            Assert.AreEqual(1.0, RaceRhythm.Scale(RaceLength.Medium, "fuel"));
            Assert.AreEqual(480, AiRadioPreferences.ApplyDriverScale("fuel", 240, RaceRhythm.Scale(RaceLength.Short, "fuel")));
            Assert.AreEqual(0, AiRadioPreferences.ApplyDriverScale("fuel", 0, 2.0), "ausgeschaltet bleibt aus");
        }

        [Test]
        public void Mantorp_first_lap_keeps_only_the_essentials()
        {
            // Aus der Aufnahme: Thema, Sekunden nach Grün, genannter Fahrer
            var calls = new[] {
                ("race-start", 0, (string)null), ("race-hazard-ahead", 3, "Luka Radojevic"), ("race-hazard-ahead", 4, (string)null),
                ("warning-flag-8", 16, "Nacho Blanco"), ("warning-rc-yellow-1", 16, "Nacho Blanco"), ("race-hazard-ahead", 19, "ramon de la rosa"),
                ("warning-rc-yellow-1", 19, "Nacho Blanco"), ("warning-flag-1", 22, (string)null), ("race-hazard-ahead", 25, "JAH TESÏ"),
                ("warning-tracklimit", 27, (string)null), ("warning-invalid-lap", 27, (string)null), ("incident-points", 27, (string)null),
                ("race-hazard-ahead", 30, "lorenzo nieddu"), ("warning-invalid-lap", 34, (string)null), ("warning-flag-1", 36, (string)null),
                ("position-loss", 39, (string)null), ("warning-rc-yellow-2", 46, "JAH TESÏ"),
            };
            var names = new StartHazardNames(); int kept = 0;
            foreach (var (topic, s, name) in calls)
            {
                var at = Green.AddSeconds(s);
                if (!RaceRhythm.StartEssential(topic) || names.NamedRecently(name, at)) continue;
                names.Note(name, at);
                kept++;
            }
            Assert.AreEqual(11, kept, "17 Meldungen erkannt, 6 davon gehören nicht in den Start");
            Assert.IsFalse(RaceRhythm.StartEssential("position-loss"));
            Assert.IsFalse(RaceRhythm.StartEssential("incident-points"));
            Assert.IsFalse(RaceRhythm.StartEssential("race-summary"));
            Assert.IsFalse(RaceRhythm.StartEssential("focus-brief"));
            Assert.IsTrue(RaceRhythm.StartEssential("warning-flag-5"));
            Assert.IsTrue(RaceRhythm.StartEssential("pit-limit"));
        }
    }
}
