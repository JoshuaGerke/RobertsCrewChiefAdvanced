using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Collections.Generic;

using static CC_log_compare.CC_log_compare;

namespace CC_log_compare
{
    [TestClass]
    [Ignore]
    public class UnitTestCC_log_compare
    {
        [TestMethod]
        public void TestMethodParseContents()
        {
            List<string> testString = new List<string>{
                @"console_2022_05_29-12-52-18.txt",
                @"Profile: defaultSettings.json",
                @"VOICE_OPTION: ALWAYS_ON",
                @"",
                @"Non-default Properties:",
                @"gtr2_install_path: 'D:\games\SteamLibrary\steamapps\common\GTR 2 - FIA GT Racing Game'",
                @"interrupt_setting_listprop: 'IMPORTANT_MESSAGES'",
                @"iracing_launch_exe: 'C:\Progam Files (x86)\iRacing\ui\iRacingUI.exe'",
                @"log_type_verbose: True",
                @"max_complaints_per_session: 50",
                @"minimum_voice_recognition_confidence_system_sre: 0.2",
                @"pcars_enable_rain_prediction: True",
                @"pcars2_launch_exe: 'steam://launch/378860/othervr'",
                @"report_ambient_temp_changes_greater_than: 20",
                @"report_track_temp_changes_greater_than: 20",
                @"rf2_enable_auto_fuel_to_end_of_race: True",
                @"rf2_install_path: 'C:\Program Files (x86)\Steam\steamapps\common\rFactor 2'",
                @"speech_recognition_country: 'GB'",
                @"use_naudio: False",
                @"",
                @"12:46:45.464 : Loading screen opened",
                @"12:46:45.465 : Set rFactor 2 (64 bit) mode from previous launch",
                @"12:46:46.468 : Starting app.  Version: 4.16.2.2",
                @"12:46:48.544 : Using sound pack version 188, driver names version 140 and personalisations version 147"
            };
            var obj = new CC_log_compare(null);
            var result = obj.ParseContents(testString);
        }
        [TestMethod]
        public void TestMethodReadWriteFile()
        {
            var obj = new CC_log_compare(@"d:\Tony\repos\CC_log_compare\CC_log_compare\console_2022_06_14-13-13-06.txt");
            var contents = obj.ReadFile();
            var result = obj.ParseContents(contents);
            obj.WriteFile(result);
        }
    }
    [TestClass]
    //[Ignore]
    public class UnitTestFuelParsing
    {
        [TestMethod]
        public void TestMethodLapsCompleted()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "00:02:59 : Laps completed = 2";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(2, fuelParameters.LapsCompleted);
        }
        [TestMethod]
        public void TestMethodInitialFuelLevel()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:45:51.348 : Fuel: Fuel level initialised, initialFuelLevel = 120L, halfDistance = -1 halfTime = 1815.00";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(120.0f, fuelParameters.InitialFuelLevel);
        }
        [TestMethod]
        public void TestMethodPerMin()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:46:59.358 : Fuel: Fuel use per minute (basic calc) = 3.756L fuel left = 116L";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(3.756f, fuelParameters.FuelPerMin);
            Assert.AreEqual(116.0f, fuelParameters.FuelLeft);
        }
        [TestMethod]
        public void TestMethodPerMinWindowed()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:54:26.453 : Fuel: Fuel use per minute: windowed calc=4.128L, max per min calc=4.201L fuel left=91L";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(4.128f, fuelParameters.FuelPerMinWindowed);
            Assert.AreEqual(4.201f, fuelParameters.FuelPerMinMaxWindowed);
            Assert.AreEqual(91.0f, fuelParameters.FuelLeft);
        }
        [TestMethod]
        public void TestMethodPerLap()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:50:01.732 : Fuel: Fuel use per lap (basic calc) = 5.134L fuel left = 110L";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(5.134f, fuelParameters.FuelPerLap);
            Assert.AreEqual(110.0f, fuelParameters.FuelLeft);
        }
        [TestMethod]
        public void TestMethodPerLapWindowed()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:52:38.456 : Fuel: Fuel use per lap: windowed calc=5.390L, max per lap=5.450L left=99L";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(5.390f, fuelParameters.FuelPerLapWindowed);
            Assert.AreEqual(5.450f, fuelParameters.FuelPerLapMaxWindowed);
            Assert.AreEqual(99.0f, fuelParameters.FuelLeft);
        }
        [TestMethod]
        public void TestMethodMatchFuelCalculation()
        {
            string line = @"02:28:51 : Fuel: Use per minute = 4.046L estimated minutes to go (including final lap) = 26.6 current fuel = 114L additional fuel needed = 15L";

            bool result = MatchFuelCalculation(line).Length > 0;
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TestMethodInitialFuelLevelGal()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:45:51.348 : Fuel: Fuel level initialised, initialFuelLevel = 120gal, halfDistance = -1 halfTime = 1815.00";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(120.0f, fuelParameters.InitialFuelLevel);
        }
        [TestMethod]
        public void TestMethodPerMinGal()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:46:59.358 : Fuel: Fuel use per minute (basic calc) = 3.756gal fuel left = 116gal";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(3.756f, fuelParameters.FuelPerMin);
            Assert.AreEqual(116.0f, fuelParameters.FuelLeft);
        }
        [TestMethod]
        public void TestMethodPerMinWindowedGal()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:54:26.453 : Fuel: Fuel use per minute: windowed calc=4.128gal, max per min calc=4.201gal fuel left=91gal";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(4.128f, fuelParameters.FuelPerMinWindowed);
            Assert.AreEqual(4.201f, fuelParameters.FuelPerMinMaxWindowed);
            Assert.AreEqual(91.0f, fuelParameters.FuelLeft);
        }
        [TestMethod]
        public void TestMethodPerLapGal()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:50:01.732 : Fuel: Fuel use per lap (basic calc) = 5.134gal fuel left = 110gal";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(5.134f, fuelParameters.FuelPerLap);
            Assert.AreEqual(110.0f, fuelParameters.FuelLeft);
        }
        [TestMethod]
        public void TestMethodPerLapWindowedGal()
        {
            FuelParameters fuelParameters = new FuelParameters();
            string line = "10:52:38.456 : Fuel: Fuel use per lap: windowed calc=5.390gal, max per lap=5.450gal left=99gal";
            fuelParameters = CC_log_compare.ParseFuel(line, fuelParameters);
            Assert.AreEqual(5.390f, fuelParameters.FuelPerLapWindowed);
            Assert.AreEqual(5.450f, fuelParameters.FuelPerLapMaxWindowed);
            Assert.AreEqual(99.0f, fuelParameters.FuelLeft);
        }
        [TestMethod]
        public void TestMethodMatchFuelCalculationGal()
        {
            string line = @"02:28:51 : Fuel: Use per minute = 4.046gal estimated minutes to go (including final lap) = 26.6 current fuel = 114gal additional fuel needed = 15gal";

            bool result = MatchFuelCalculation(line).Length > 0;
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TestMethodFieldNames()
        {
            FuelParameters fuelParameters = new FuelParameters();
            var result = FuelParameters.FieldNames(fuelParameters);
            Assert.IsTrue(result.Contains("FuelPerLapMaxWindowed,"));
        }
    }
}
