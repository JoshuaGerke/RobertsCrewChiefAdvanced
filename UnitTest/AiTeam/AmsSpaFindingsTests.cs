using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Strategy;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Testkanal B, AMS2 Spa 23.11.: Befunde aus dem Testrennen.</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class AmsSpaFindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 18, 40, 0, DateTimeKind.Utc);

        [Test]
        public void Section_reference_is_the_lowest_own_speed_not_the_entry_speed()
        {
            // Bremszone vor La Source: Einfahrt 75 m/s, Scheitel 18 m/s im selben 100-m-Abschnitt.
            float reference = 75;
            foreach (float v in new[] { 70f, 55f, 34f, 18f, 22f }) reference = RaceTrafficWatch.CellReference(reference, v);
            Assert.AreEqual(18f, reference);
            Assert.IsFalse(RaceTrafficWatch.SuddenSlowdown(new List<KeyValuePair<DateTime, float>>
            {
                new KeyValuePair<DateTime, float>(T.AddSeconds(-2.5), 72), new KeyValuePair<DateTime, float>(T.AddSeconds(-1), 40)
            }, 20, T, reference, 70), "normales Anbremsen in der Haarnadel ist kein Dreher");
            Assert.IsTrue(RaceTrafficWatch.SuddenSlowdown(new List<KeyValuePair<DateTime, float>>
            {
                new KeyValuePair<DateTime, float>(T.AddSeconds(-2.5), 72), new KeyValuePair<DateTime, float>(T.AddSeconds(-1), 40)
            }, 5, T, reference, 70), "Einbruch auf 5 m/s bleibt ein Dreher");
        }

        [Test]
        public void Background_call_waits_in_traffic_only_a_few_seconds()
        {
            var call = new ObservedRaceCall { Topic = "reader-pace-rear", Text = "x", Priority = 3, SessionId = 5, SessionType = SessionType.Race,
                DetectedUtc = T, ExpiresUtc = T.AddSeconds(10) };
            RaceSituation At(DateTime now, RaceAttention a) => new RaceSituation { Utc = now, SessionId = 5, Phase = RacePhase.Middle, Attention = a, Focus = RaceFocus.Rhythm };
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(At(T.AddSeconds(2), RaceAttention.Traffic), call, T.AddSeconds(2), out _));
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(At(T.AddSeconds(7), RaceAttention.Traffic), call, T.AddSeconds(7), out _),
                "Rob: „Ich höre nichts von euch“ – Verkehr hält nicht ewig auf");
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(At(T.AddSeconds(7), RaceAttention.Battle), call, T.AddSeconds(7), out _), "Zweikampf wartet weiter");
        }

        [Test]
        public void Configured_reports_are_not_held_by_traffic_and_only_briefly_by_a_battle()
        {
            RaceSituation At(DateTime now, RaceAttention a) => new RaceSituation { Utc = now, SessionId = 5, Phase = RacePhase.Middle, Attention = a, Focus = RaceFocus.Rhythm };
            foreach (var topic in new[] { "status", "ahead", "behind", "fuel", "tyres", "weather" })
            {
                var call = new ObservedRaceCall { Topic = topic, Text = "x", Priority = 1, SessionId = 5, SessionType = SessionType.Race,
                    Evidence = "configured_interval_current_measurements", DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
                Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(At(T, RaceAttention.Traffic), call, T, out _), topic + ": Verkehr hält nicht auf");
                Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(At(T.AddSeconds(3), RaceAttention.Battle), call, T.AddSeconds(3), out _), topic + ": Zweikampf kurz");
                Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(At(T.AddSeconds(9), RaceAttention.Battle), call, T.AddSeconds(9), out _), topic + ": dann doch");
                Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(At(T.AddSeconds(9), RaceAttention.Caution), call, T.AddSeconds(9), out _), topic + ": Gelb wartet");
                Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.Final, Attention = RaceAttention.Open },
                    call, T, out _), topic + ": auch in der Schlussphase");
            }
        }

        [Test]
        public void Questions_about_penalties_validity_closeness_and_corrections_use_the_data()
        {
            foreach (var q in new[] { "45 Sekunden Zeitstrafe?", "Ist diese Runde gültig?", "JJ ist plötzlich ganz nah, vorne.", "Dann notieren, sehr genau rechnen.",
                "Das ist falsch, Kozmy fährt 2:17 und ich 2:19.", "Do I have a penalty?", "¿Tengo una sanción?" })
                Assert.IsTrue(AiTeamService.NeedsTools(q, null), q);
            foreach (var q in new[] { "Wie viel Sprit habe ich noch?", "Wie groß ist der Abstand nach vorne?", "Welche Position?", "Völlig okay so?", "Noch mal bitte?" })
                Assert.IsFalse(AiTeamService.NeedsTools(q, null), q);
        }

        [Test]
        public void A_single_slow_last_lap_is_not_a_pace_fade()
        {
            double[] spa = { 139.00, 139.45, 139.07, 139.35, 139.95 };
            var input = new StrategyInput { Lap = 6, LapsToGo = 4, FuelLeft = 12.5, FuelPerLap = double.NaN,
                StintLaps = spa.Select((t, k) => new KeyValuePair<int, double>(k + 2, t)).ToList() };
            Assert.AreEqual("stabil", StrategyPicture.Compute(input).PaceTrend);
            double[] fading = { 139.0, 139.2, 139.4, 139.6, 139.8, 140.0 };
            input.StintLaps = fading.Select((t, k) => new KeyValuePair<int, double>(k + 2, t)).ToList();
            Assert.AreEqual("baut_ab", StrategyPicture.Compute(input).PaceTrend, "ein echter Abbau bleibt einer");
        }

        [Test]
        public void Closing_lap_call_waits_for_the_own_line_not_the_leaders()
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 7; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.ClassPosition = 2; s.SessionData.SessionNumberOfLaps = 10; s.SessionData.SessionLapsRemaining = 4; s.SessionData.CompletedLaps = 6;
            s.carClass = new CarData.CarClass(); s.PositionAndMotionData.CarSpeed = 60;
            var observer = new ActiveRaceObserver();
            ObservedRaceCall Run(double from, double to)
            {
                ObservedRaceCall got = null;
                for (double t = from; t <= to; t += 0.5)
                {
                    observer.Observe(s, T.AddSeconds(t), "Rob", 0);
                    var c = observer.Take(s, T.AddSeconds(t), false, false, false, 0, null, "Rob");
                    if (c != null && c.Topic == "race-end") got = c;
                }
                return got;
            }
            Assert.IsNull(Run(0, 2));
            // Der Führende quert die Linie: Restrunden 3, Rob ist noch 14 s davor.
            s.SessionData.SessionLapsRemaining = 3;
            Assert.IsNull(Run(2.5, 16), "nicht schon beim Führenden");
            s.SessionData.CompletedLaps = 7;
            Assert.IsNotNull(Run(16.5, 17), "an Robs eigener Linie");
            // Führt Rob selbst und stellt die Sim die Restrunden einen Tick später um, kommt die Ansage trotzdem.
            s.SessionData.CompletedLaps = 8;
            Assert.IsNull(Run(17.5, 18));
            s.SessionData.SessionLapsRemaining = 2;
            var next = Run(18.5, 19);
            Assert.IsNotNull(next); Assert.AreEqual("race-end", next.Topic);
        }
    }
}
