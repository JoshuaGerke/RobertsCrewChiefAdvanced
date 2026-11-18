using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Angriffs- und Verteidigungspunkt im Zweikampf aus den Sektorzeiten beider Fahrer.</summary>
    [TestFixture, Category("AiTeam")]
    public class BattleSectorTests
    {
        private static List<float[]> Laps(params float[][] laps) => laps.ToList();

        [Test]
        public void Finds_where_Rob_is_faster_than_the_car_ahead()
        {
            var own = Laps(new[] { 30.1f, 40.0f, 35.2f }, new[] { 30.2f, 39.9f, 35.1f }, new[] { 30.0f, 40.1f, 35.2f });
            var rival = Laps(new[] { 30.1f, 40.4f, 35.1f }, new[] { 30.2f, 40.3f, 35.2f }, new[] { 30.1f, 40.5f, 35.1f });
            Assert.AreEqual(1, BattleSectorWatch.DecisiveSector(own, rival, true, out float delta), "Sektor 2");
            Assert.AreEqual(.4f, delta, .02f);
        }

        [Test]
        public void Finds_where_the_car_behind_is_faster()
        {
            var own = Laps(new[] { 30.0f, 40.0f, 35.5f }, new[] { 30.0f, 40.0f, 35.6f }, new[] { 30.0f, 40.0f, 35.4f });
            var rival = Laps(new[] { 30.1f, 40.1f, 35.2f }, new[] { 30.1f, 40.0f, 35.2f }, new[] { 30.0f, 40.1f, 35.1f });
            Assert.AreEqual(2, BattleSectorWatch.DecisiveSector(own, rival, false, out _), "Sektor 3");
        }

        [Test]
        public void Small_or_inconsistent_differences_say_nothing()
        {
            var even = Laps(new[] { 30f, 40f, 35f }, new[] { 30f, 40f, 35f }, new[] { 30f, 40f, 35f });
            Assert.AreEqual(-1, BattleSectorWatch.DecisiveSector(even, Laps(new[] { 30.1f, 40.1f, 35.1f }, new[] { 30.1f, 40.1f, 35.1f }, new[] { 30.1f, 40.1f, 35.1f }), true, out _));
            var oneOff = Laps(new[] { 30f, 39.0f, 35f }, new[] { 30f, 40.5f, 35f }, new[] { 30f, 40.4f, 35f });
            Assert.AreEqual(-1, BattleSectorWatch.DecisiveSector(oneOff, Laps(new[] { 30f, 40.2f, 35f }, new[] { 30f, 40.2f, 35f }, new[] { 30f, 40.2f, 35f }), true, out _), "ein Ausreißer ist kein Muster");
            Assert.AreEqual(-1, BattleSectorWatch.DecisiveSector(even, Laps(new[] { 30f, 40f, 35f }), true, out _), "zu wenige Gegnerrunden");
        }

        [Test]
        public void Attack_and_defence_calls_vary_in_every_language()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                foreach (string name in new[] { "Timo", "" })
                {
                    var attack = Enumerable.Range(0, 5).Select(v => BattleSectorWatch.AttackText(name, 2, .3f, v)).ToList();
                    var defend = Enumerable.Range(0, 5).Select(v => BattleSectorWatch.DefendText(name, 3, .2f, v)).ToList();
                    Assert.AreEqual(5, attack.Distinct().Count());
                    Assert.AreEqual(5, defend.Distinct().Count());
                    foreach (var t in attack.Concat(defend)) { StringAssert.DoesNotContain("  ", t); Assert.IsFalse(char.IsLower(t[0]), t); }
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
            StringAssert.Contains("Sektor 2", BattleSectorWatch.AttackText("Timo", 2, .3f, 0));
            StringAssert.Contains("angreifen", BattleSectorWatch.AttackText("Timo", 2, .3f, 0));
        }
    }
}
