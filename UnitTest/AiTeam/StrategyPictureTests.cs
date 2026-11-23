using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Strategy;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Testkanal B: Strategiebild aus Sprit, Energie, Reifen und Tempo, dazu die Funksätze.</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class StrategyPictureTests
    {
        private bool wasDeterministic;
        private string wasLanguage;
        [SetUp] public void Deterministic() { wasDeterministic = RadioVariants.Deterministic; wasLanguage = RadioLanguage.Current; RadioVariants.Deterministic = true; RadioLanguage.Current = "de"; }
        [TearDown] public void Restore() { RadioVariants.Deterministic = wasDeterministic; RadioLanguage.Current = wasLanguage; }

        private static StrategyInput Fuel(int lap, double toGo, double left, double perLap, double capacity = double.NaN) =>
            new StrategyInput { Lap = lap, LapsToGo = toGo, FuelLeft = left, FuelPerLap = perLap, FuelCapacity = capacity };

        [Test]
        public void Enough_fuel_means_no_stop_with_the_margin()
        {
            var p = StrategyPicture.Compute(Fuel(10, 15, 40, 2));
            Assert.AreEqual("durchfahren", p.Plan);
            Assert.AreEqual("sprit", p.Limit);
            Assert.AreEqual(5, p.Margin, 1e-9);
            Assert.IsFalse(p.WindowKnown);
        }

        [Test]
        public void One_stop_window_runs_from_the_first_lap_a_full_tank_reaches_the_finish_to_the_last_lap_of_fuel()
        {
            // 20 Runden im Tank, voller Tank 30 Runden, 40 Runden zu fahren.
            var p = StrategyPicture.Compute(Fuel(10, 40, 40, 2, 60));
            Assert.AreEqual("ein_stopp", p.Plan);
            Assert.AreEqual("sprit", p.PlanReason);
            Assert.AreEqual(21, p.WindowFirstLap, "Stopp am Ende von Runde 21: danach 29 Runden, voller Tank reicht 30");
            Assert.AreEqual(29, p.WindowLastLap, "Sprit reicht 20 Runden ab Runde 10");
        }

        [Test]
        public void When_one_tank_cannot_bridge_the_rest_the_crew_plans_two_stops()
        {
            var p = StrategyPicture.Compute(Fuel(10, 60, 40, 2, 60));
            Assert.AreEqual("zwei_stopps", p.Plan);
            Assert.AreEqual(29, p.WindowLastLap);
        }

        [Test]
        public void Only_this_lap_left_puts_the_stop_at_the_end_of_the_running_lap()
        {
            var p = StrategyPicture.Compute(Fuel(10, 20, 1.5, 2, 60));
            Assert.AreEqual("ein_stopp", p.Plan);
            Assert.AreEqual(11, p.WindowFirstLap);
            Assert.AreEqual(11, p.WindowLastLap);
        }

        [Test]
        public void Small_energy_shortfall_means_saving_with_a_target_per_lap()
        {
            var i = Fuel(20, 10, 100, 2);
            i.EnergyLeft = 30; i.EnergyPerLap = 3.1;
            var p = StrategyPicture.Compute(i);
            Assert.AreEqual("energie", p.Limit);
            Assert.AreEqual("sparen", p.Plan);
            Assert.AreEqual(30 / 10.2, p.EnergyTargetPerLap, 1e-6);
            Assert.Greater(p.EnergySavePerLap, 0);
            StringAssert.Contains("2,9 Prozent pro Runde", StrategyRadio.Plan(p, null));
        }

        [Test]
        public void Fuel_that_disagrees_with_our_own_measurement_is_not_used()
        {
            var i = Fuel(10, 15, 40, 2);
            i.FuelPerLapMeasured = 2.6;
            var p = StrategyPicture.Compute(i);
            Assert.IsFalse(p.FuelChecked);
            Assert.IsNull(p.Limit, "keine Zahl ohne Gegenprobe");
            Assert.AreEqual("unklar", p.Plan);
            CollectionAssert.Contains(p.Missing, "verbrauch_gegenprobe_abweichend");
            i.FuelPerLapMeasured = 2.1;
            Assert.IsTrue(StrategyPicture.Compute(i).FuelChecked, "5 % Abweichung ist in Ordnung");
        }

        [Test]
        public void Worn_tyres_with_enough_fuel_mean_looking_after_them()
        {
            var i = Fuel(10, 14, 60, 2);
            i.Wear = new float[] { 40, 40, 50, 50 }; i.WearPerLap = new float[] { 2, 2, 3, 3 }; i.WearCalibrated = true;
            var p = StrategyPicture.Compute(i);
            Assert.AreEqual("reifen", p.Limit);
            Assert.AreEqual(13, p.TyreLaps);
            Assert.AreEqual(2, p.TyreCorner);
            Assert.AreEqual("reifen_schonen", p.Plan);
            StringAssert.Contains("hinten links", StrategyRadio.Plan(p, null));
        }

        [Test]
        public void Uncalibrated_wear_is_not_a_limit()
        {
            var i = Fuel(10, 14, 60, 2);
            i.Wear = new float[] { 40, 40, 50, 50 }; i.WearPerLap = new float[] { 2, 2, 3, 3 }; i.WearCalibrated = false;
            Assert.AreEqual("sprit", StrategyPicture.Compute(i).Limit);
        }

        [Test]
        public void Pace_trend_ignores_a_single_traffic_lap()
        {
            var i = Fuel(9, 15, 40, 2);
            double[] times = { 90.0, 90.1, 90.2, 95.0, 90.4, 90.5, 90.6 };
            i.StintLaps = times.Select((t, k) => new KeyValuePair<int, double>(k + 2, t)).ToList();
            var p = StrategyPicture.Compute(i);
            Assert.AreEqual("baut_ab", p.PaceTrend);
            Assert.AreEqual(.1, p.PaceSlope, .01);
            StringAssert.Contains("ein Zehntel pro Runde", StrategyRadio.PaceFade(p));

            double[] flat = { 90.0, 90.3, 89.9, 90.2, 90.0, 90.1 };
            i.StintLaps = flat.Select((t, k) => new KeyValuePair<int, double>(k + 2, t)).ToList();
            Assert.AreEqual("stabil", StrategyPicture.Compute(i).PaceTrend);
        }

        [Test]
        public void Stint_laps_start_after_the_last_pit_lap_and_skip_the_standing_start()
        {
            var laps = new List<LapRecord>();
            for (int n = 1; n <= 8; n++)
                laps.Add(new LapRecord { LapNumber = n, LapTime = 90 + n * .1f, Valid = n != 7, InPitLap = n == 3 });
            var stint = StrategyWatch.StintLaps(laps);
            CollectionAssert.AreEqual(new[] { 4, 5, 6, 8 }, stint.Select(l => l.Key).ToArray());
            laps.RemoveRange(2, 6);
            CollectionAssert.AreEqual(new[] { 2 }, StrategyWatch.StintLaps(laps).Select(l => l.Key).ToArray(), "Runde 1 zählt nicht");
        }

        [Test]
        public void A_plan_is_worth_a_call_only_when_it_asks_for_something()
        {
            var through = StrategyPicture.Compute(Fuel(10, 15, 40, 2));
            Assert.IsFalse(StrategyWatch.WorthSaying(through, null), "5 Runden Reserve: nichts zu sagen");
            Assert.IsTrue(StrategyWatch.WorthSaying(through, "ein_stopp"), "Entwarnung nach Stopp-Plan");
            var tight = StrategyPicture.Compute(Fuel(10, 15, 31, 2));
            Assert.AreEqual("durchfahren", tight.Plan);
            Assert.IsTrue(StrategyWatch.WorthSaying(tight, null), "knappe Reserve");
            var stop = StrategyPicture.Compute(Fuel(10, 40, 40, 2, 60));
            Assert.IsTrue(StrategyWatch.WorthSaying(stop, null));
            var fuelSave = StrategyPicture.Compute(Fuel(10, 15, 29, 2));
            Assert.AreEqual("sparen", fuelSave.Plan);
            Assert.IsFalse(StrategyWatch.WorthSaying(fuelSave, null), "reiner Spritmangel bleibt bei der festen Sprit-Warnung");
        }

        [Test]
        public void Radio_lines_name_the_window_and_the_limit_in_every_language()
        {
            var stop = StrategyPicture.Compute(Fuel(10, 40, 40, 2, 60));
            StringAssert.Contains("Runde 21 bis 29", StrategyRadio.Plan(stop, null));
            StringAssert.Contains("Runde 29", StrategyRadio.WindowOpen(stop));
            StringAssert.Contains("der Sprit", StrategyRadio.WindowLast(stop));
            RadioLanguage.Current = "en";
            try
            {
                StringAssert.Contains("lap 21 to 29", StrategyRadio.Plan(stop, null));
                StringAssert.Contains("the fuel", StrategyRadio.WindowLast(stop));
                RadioLanguage.Current = "es";
                StringAssert.Contains("vuelta 21 a la 29", StrategyRadio.Plan(stop, null));
            }
            finally { RadioLanguage.Current = "de"; }
        }

        [Test]
        public void Every_strategy_call_has_at_least_four_wordings()
        {
            RadioVariants.Deterministic = false;
            var stop = StrategyPicture.Compute(Fuel(10, 40, 40, 2, 60));
            var seen = new HashSet<string>();
            for (int n = 0; n < 60; n++) seen.Add(StrategyRadio.Plan(stop, null));
            Assert.GreaterOrEqual(seen.Count, RadioVariants.Minimum);
            seen.Clear();
            for (int n = 0; n < 60; n++) seen.Add(StrategyRadio.WindowLast(stop));
            Assert.GreaterOrEqual(seen.Count, RadioVariants.Minimum);
        }

        [Test]
        public void Picture_json_carries_plan_window_and_check()
        {
            var o = StrategyPicture.Compute(Fuel(10, 40, 40, 2, 60)).ToJson();
            Assert.AreEqual("ein_stopp", (string)o["plan"]);
            Assert.AreEqual(21, (int)o["boxenfenster"]["fruehestens_ende_runde"]);
            Assert.IsTrue((bool)o["sprit"]["gegenprobe_ok"]);
            StrategyWatch.Publish(StrategyPicture.Compute(Fuel(10, 40, 40, 2, 60)), 7, new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc));
            Assert.IsNotNull(StrategyWatch.Latest(7, new DateTime(2026, 11, 23, 18, 1, 0, DateTimeKind.Utc)));
            Assert.IsNull(StrategyWatch.Latest(8, new DateTime(2026, 11, 23, 18, 1, 0, DateTimeKind.Utc)), "andere Session");
            Assert.IsNull(StrategyWatch.Latest(7, new DateTime(2026, 11, 23, 18, 6, 0, DateTimeKind.Utc)), "zu alt");
            StrategyWatch.ClearLatest();
        }
    }
}
