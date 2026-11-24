using System;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Funklast (Rob 18.11.): Rennen kommentieren und Daten ansagen – aber nicht im engen Zweikampf zu viel.</summary>
    [TestFixture, Category("AiTeam")]
    public class RadioLoadTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Tension_comes_from_measured_race_situation()
        {
            Assert.AreEqual(0, RadioLoad.RawTension(8, 9, false, RaceAttention.Calm, RacePhase.Middle, DriverMoodState.Neutral), 1e-9);
            Assert.AreEqual(1, RadioLoad.RawTension(.4f, 9, false, RaceAttention.Battle, RacePhase.Middle, DriverMoodState.Neutral), 1e-9);
            Assert.AreEqual(1, RadioLoad.RawTension(8, 9, true, RaceAttention.Calm, RacePhase.Middle, DriverMoodState.Neutral), 1e-9, "Auto daneben");
            Assert.AreEqual(.9, RadioLoad.RawTension(8, 9, false, RaceAttention.Calm, RacePhase.Start, DriverMoodState.Neutral), 1e-9);
            Assert.Greater(RadioLoad.RawTension(1.2f, 9, false, RaceAttention.Open, RacePhase.Middle, DriverMoodState.Pressure),
                RadioLoad.RawTension(1.2f, 9, false, RaceAttention.Open, RacePhase.Middle, DriverMoodState.Neutral));
        }

        [Test]
        public void After_a_fight_the_tension_eases_off_slowly()
        {
            var load = new RadioLoad();
            Assert.AreEqual(1, load.Update(1, T), 1e-9);
            Assert.AreEqual(.5, load.Update(0, T.AddSeconds(8)), .01, "Halbwertszeit 8 s");
            Assert.Less(load.Update(0, T.AddSeconds(30)), RadioLoad.DataLimit, "eine halbe Minute später ist wieder Platz für Daten");
        }

        [Test]
        public void Close_fight_lets_only_the_important_through()
        {
            var load = new RadioLoad();
            load.Update(.85, T);
            Assert.IsTrue(load.Allows("race-hazard-ahead", 7, T, out _), "Sicherheit immer");
            Assert.IsTrue(load.Allows("position", 4, T, out _), "Platzwechsel ist das Rennen selbst");
            Assert.IsFalse(load.Allows("judge-consistency", 3, T, out _), "Lob wartet");
            Assert.IsFalse(load.Allows("insight-lockup", 3, T, out _), "Coaching wartet");
            Assert.IsFalse(load.Allows("fuel-range", 3, T, out string why), "Daten warten");
            StringAssert.Contains("tension", why);
        }

        [Test]
        public void Calm_phase_gets_data_but_with_gaps_and_a_budget()
        {
            var load = new RadioLoad();
            load.Update(0, T);
            Assert.IsTrue(load.Allows("fuel-range", 3, T, out _));
            load.NoteSpoken("fuel-range", "Sprit reicht noch für etwa zwölf Runden, das passt bis ins Ziel.", T);
            Assert.IsFalse(load.Allows("tyre-wear-warning", 3, T.AddSeconds(5), out string why), "nicht Zahl auf Zahl");
            StringAssert.Contains("gap", why);
            Assert.IsTrue(load.Allows("position", 4, T.AddSeconds(5), out _), "Rennereignisse brauchen keinen Abstand");
            for (int i = 0; i < 3; i++)
                load.NoteSpoken("tyre-wear-warning", "Die Reifen bauen ab, vorne links liegt bei sechzig Prozent, also etwas schonen in den schnellen Kurven.", T.AddSeconds(13 + i * 13));
            Assert.IsFalse(load.Allows("engine-oil", 3, T.AddSeconds(55), out why), "Datenbudget der Minute ist verbraucht");
            StringAssert.Contains("budget", why);
            Assert.IsTrue(load.Allows("engine-oil", 3, T.AddSeconds(110), out _), "eine Minute später wieder");
        }

        [Test]
        public void Topics_are_sorted_into_classes()
        {
            Assert.AreEqual(RadioClass.Safety, RadioLoad.Classify("warning-flag-3", 10));
            Assert.AreEqual(RadioClass.Safety, RadioLoad.Classify("pit-limit", 3));
            Assert.AreEqual(RadioClass.Event, RadioLoad.Classify("judge-dropped", 5));
            Assert.AreEqual(RadioClass.Event, RadioLoad.Classify("own-lap", 4));
            Assert.AreEqual(RadioClass.Encouragement, RadioLoad.Classify("judge-overtake", 4));
            Assert.AreEqual(RadioClass.Coaching, RadioLoad.Classify("corner-coaching", 2));
            Assert.AreEqual(RadioClass.Data, RadioLoad.Classify("fuel-range", 3));
            Assert.AreEqual(RadioClass.Data, RadioLoad.Classify("race-summary", 2));
            Assert.AreEqual(RadioClass.Safety, RadioLoad.Classify("fuel-range", 6), "ab Priorität 6 ist es dringend");
        }

        [Test]
        public void Mind_holds_data_during_a_fight()
        {
            var mind = new RaceMind();
            mind.Load.Update(.9, T);
            var data = new ObservedRaceCall { Topic = "tyre-wear", Text = "x", Priority = 3, SessionId = 5, SessionType = CrewChiefV4.GameState.SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(20) };
            Assert.AreEqual(MindVerdict.Hold, mind.Judge(data, T, out string why));
            StringAssert.StartsWith("load_", why);
            mind.Load.Update(0, T.AddSeconds(40));
            Assert.AreEqual(MindVerdict.Admit, mind.Judge(data, T.AddSeconds(40), out _));
        }

        [Test]
        public void Old_more_or_less_radio_wishes_no_longer_change_anything()
        {
            // Rob 24.11.: Keine Funkstufen mehr. Eine früher gespeicherte Funkdichte wirkt nicht, neue Wünsche werden nicht gespeichert.
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rrca-density-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(folder);
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "funkwuensche.json"), "{\"schema\":4,\"routine\":2,\"gaps\":1,\"qualifying_sectors\":true,\"rear_traffic_seconds\":3,\"radio_density\":2}");
                var prefs = new DriverRadioPreferences(folder);
                Assert.AreEqual(1, prefs.IntervalScale("fuel"), "altes „Routine seltener“ dämpft nicht");
                foreach (var said in new[] { "Bitte weniger Funk", "Mehr Infos!", "less radio", "Más radio", "Routinefunk seltener" })
                {
                    Assert.IsFalse(prefs.TryCommand(said, out _), said);
                    Assert.IsTrue(RadioAmountCommand.IsAmountWish(said), said + " bekommt die ehrliche Antwort");
                }
                Assert.AreEqual(1, DriverMemory.RadioDensity);
                Assert.IsTrue(new DriverRadioPreferences(folder).QualifyingSectors, "andere Wünsche bleiben erhalten");
            }
            finally { try { System.IO.Directory.Delete(folder, true); } catch (Exception) { } }
        }

        [Test]
        public void Density_moves_only_the_data_limits()
        {
            var quiet = new RadioLoad { DensityOverride = 2 };
            var chatty = new RadioLoad { DensityOverride = 0 };
            quiet.Update(.5, T); chatty.Update(.5, T);
            Assert.IsFalse(quiet.Allows("fuel-range", 3, T, out _), "weniger Funk: Daten nur bei richtig ruhiger Lage");
            Assert.IsTrue(chatty.Allows("fuel-range", 3, T, out _));
            Assert.IsTrue(quiet.Allows("position", 4, T, out _), "das Rennen selbst bleibt");
            Assert.IsTrue(quiet.Allows("race-hazard-ahead", 7, T, out _));
        }
    }
}
