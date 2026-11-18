using System;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>C6 Strategie-Wahrscheinlichkeiten: Monte-Carlo-Kern für Stopp jetzt / in 1 / in 2 Runden.</summary>
    [TestFixture, Category("AiTeam")]
    public class StrategyOddsTests
    {
        [Test]
        public void Plenty_of_fuel_means_drive_to_the_finish()
        {
            var r = StrategyOdds.Evaluate(new StrategyInputs { FuelLaps = 12, FuelLapsSpread = .2, LapsToGo = 8 });
            Assert.AreEqual(100, StrategyOdds.Round5(r.NoStopReachesFinish));
            Assert.AreEqual("durchfahren", r.Recommendation);
        }

        [Test]
        public void Tight_fuel_recommends_the_latest_safe_stop()
        {
            // Reicht für etwa 2,6 Runden, 10 Runden bis zum Ziel: Stopp in einer Runde braucht 2 Runden Sprit (sicher), in zwei Runden 3 (zu knapp).
            var r = StrategyOdds.Evaluate(new StrategyInputs { FuelLaps = 2.6, FuelLapsSpread = .15, LapsToGo = 10, LapFractionToPitEntry = 1 });
            Assert.AreEqual(0, StrategyOdds.Round5(r.NoStopReachesFinish));
            Assert.AreEqual(3, r.Options.Count);
            Assert.Greater(r.Options[1].FuelReaches, StrategyOdds.Safe);
            Assert.Less(r.Options[2].FuelReaches, StrategyOdds.Safe);
            Assert.AreEqual("stopp_in_1", r.Recommendation);
            Assert.IsTrue(double.IsNaN(r.Options[0].AheadOfRearCar), "ohne gemessenen Boxenverlust keine Hintermann-Rechnung");
        }

        [Test]
        public void Closing_rear_car_favours_stopping_now()
        {
            // 24 s Luft, Boxenverlust 22 s, der Hintermann holt 1,5 s pro Runde auf.
            var r = StrategyOdds.Evaluate(new StrategyInputs { FuelLaps = 6, FuelLapsSpread = .2, LapsToGo = 15,
                PitLoss = 22, PitLossSpread = 1, GapBehind = 24, RearGainPerLap = 1.5, RearGainSpread = .2 });
            Assert.Greater(r.Options[0].AheadOfRearCar, r.Options[1].AheadOfRearCar);
            Assert.Greater(r.Options[1].AheadOfRearCar, r.Options[2].AheadOfRearCar);
            Assert.AreEqual("stopp_jetzt", r.Recommendation);
        }

        [Test]
        public void Same_seed_gives_same_numbers_and_output_is_rounded()
        {
            var i = new StrategyInputs { FuelLaps = 3.3, FuelLapsSpread = .4, LapsToGo = 9, PitLoss = 25, PitLossSpread = 2, GapBehind = 26, RearGainPerLap = .3, RearGainSpread = .3 };
            var a = StrategyOdds.Evaluate(i); var b = StrategyOdds.Evaluate(i);
            for (int k = 0; k < a.Options.Count; k++) Assert.AreEqual(a.Options[k].FuelReaches, b.Options[k].FuelReaches);
            var json = StrategyOdds.Describe(a, i);
            foreach (var opt in json["optionen"])
                Assert.AreEqual(0, (int)opt["sprit_reicht_bis_box_prozent"] % 5);
            Assert.IsNotNull(json["empfehlung"]);
        }

        [Test]
        public void Without_measured_fuel_nothing_is_guessed()
        {
            var r = StrategyOdds.Evaluate(new StrategyInputs());
            Assert.AreEqual(0, r.Options.Count);
            Assert.IsNull(r.Recommendation);
        }

        [Test]
        public void Last_lap_only_offers_no_later_stops()
        {
            var r = StrategyOdds.Evaluate(new StrategyInputs { FuelLaps = 0.5, LapsToGo = 1.5 });
            Assert.AreEqual(1, r.Options.Count, "bei weniger als zwei Restrunden nur „jetzt“");
        }

        [Test]
        public void Tool_is_registered_and_safe_without_race()
        {
            CollectionAssert.Contains(RaceDataTools.AllToolNames, "get_strategy_odds");
            var result = RaceDataTools.Execute("get_strategy_odds", new RaceDataContext { GameState = new CrewChiefV4.GameState.GameStateData(1), NowUtc = DateTime.UtcNow });
            Assert.IsNull(result["fehler"], result.ToString());
        }
    }
}
