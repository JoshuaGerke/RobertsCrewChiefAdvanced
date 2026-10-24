using System;
using System.Collections.Generic;
using CrewChiefV4;

using NUnit.Framework;

using Newtonsoft.Json;

using static CrewChiefV4.UserSettings;

namespace UnitTest.Misc
{
    [TestFixture]
    public class RegressionTestGameDefinition
    {
        internal class oldGameDefinition
        {
                      String               lookupName;
                      GameEnum             gameEnum;
                      String               friendlyName;
                      String               macroEditorName;
             CrewChief.RacingType racingType = CrewChief.RacingType.Undefined;
             internal         String               processName;
                      String               spotterName;
                      String               gameStartCommandProperty;
                      String               gameStartCommandOptionsProperty;
                      String               gameStartEnabledProperty;
                      String               gameInstallDirectory;  // Not the full path, only used by games that need to install plugins
                      String[]             alternativeProcessNames;
                      String[]             alternativeFilterNames;
                      Boolean              allowsUserCreatedCars;
                      String               commandLineName;
            internal oldGameDefinition(GameEnum gameEnum, String lookupName, String processName, String spotterName, String gameStartCommandProperty, String gameStartCommandOptionsProperty, String gameStartEnabledProperty, Boolean allowsUserCreatedCars, String gameInstallDirectory = "", String[] alternativeProcessNames = null, String[] approxFilterNames = null, String macroEditorName = null, CrewChief.RacingType racingType = CrewChief.RacingType.Circuit, String commandLineName = null)
            {
                this.gameEnum = gameEnum;
                this.lookupName = lookupName;
                this.friendlyName = Configuration.getUIString(lookupName);
                this.processName = processName;
                this.spotterName = spotterName;
                this.gameStartCommandProperty = gameStartCommandProperty;
                this.gameStartCommandOptionsProperty = gameStartCommandOptionsProperty;
                this.gameStartEnabledProperty = gameStartEnabledProperty;
                this.alternativeProcessNames = alternativeProcessNames;
                this.gameInstallDirectory = gameInstallDirectory;
                this.allowsUserCreatedCars = allowsUserCreatedCars;
                this.macroEditorName = macroEditorName == null ? this.friendlyName : macroEditorName;
                this.racingType = racingType;
                this.commandLineName = commandLineName == null ? gameEnum.ToString() : commandLineName;
                this.alternativeFilterNames = approxFilterNames;
            }
        }

        private static oldGameDefinition pCars64Bit = new oldGameDefinition(GameEnum.PCARS_64BIT, "pcars_64_bit", "pCARS64",
    "CrewChiefV4.PCars.PCarsSpotterv2", "pcars64_launch_exe", "pcars64_launch_params", "launch_pcars", false,
    alternativeProcessNames: new String[] { "pCARS", "pCARSGld", "pCARSQA", "pCARSAVX" },
    approxFilterNames: new String[] { "pcars" });
        private static oldGameDefinition AMS2 = new oldGameDefinition(GameEnum.AMS2, "ams2", "AMS2AVX",
            "CrewChiefV4.AMS2.AMS2Spotter", "ams2_launch_exe", "ams2_launch_params", "launch_ams2", false,
            alternativeProcessNames: new String[] { "AMS2", "AMS2AVX" },
            approxFilterNames: new String[] { "ams2", "ams_2", "ams 2", "ams-2" });
        private static oldGameDefinition pCars32Bit = new oldGameDefinition(GameEnum.PCARS_32BIT, "pcars_32_bit", "pCARS",
            "CrewChiefV4.PCars.PCarsSpotterv2", "pcars32_launch_exe", "pcars32_launch_params", "launch_pcars", false);
        private static oldGameDefinition pCars2 = new oldGameDefinition(GameEnum.PCARS2, "pcars_2", "pCARS2AVX",
            "CrewChiefV4.PCars2.PCars2Spotterv2", "pcars2_launch_exe", "pcars2_launch_params", "launch_pcars2", false,
            alternativeProcessNames: new String[] { "pCARS2", "pCARS2Gld" }, macroEditorName: "pCARS2",
            approxFilterNames: new String[] { "pcars2", "pcars_2", "pcars 2", "pcars-2" });

        // pCars3 - uses all the pCars2 classes
        private static oldGameDefinition pCars3 = new oldGameDefinition(GameEnum.PCARS3, "pcars_3", "pCARS3",
            "CrewChiefV4.PCars2.PCars2Spotterv2", "pcars3_launch_exe", "pcars3_launch_params", "launch_pcars3", false,
            alternativeProcessNames: new String[] { "pCARS3", "pCARS3Gld" },
            approxFilterNames: new String[] { "pcars3", "pcars_3", "pcars 3", "pcars-3" });
        private static oldGameDefinition raceRoom = new oldGameDefinition(GameEnum.RACE_ROOM, "race_room", "RRRE64", "CrewChiefV4.RaceRoom.R3ESpotterv2",
            "r3e_launch_exe", "r3e_launch_params", "launch_raceroom", false,
            alternativeProcessNames: new String[] { "RRRE" },
            approxFilterNames: new String[] { "room", "r3e", "rrre" });
        private static oldGameDefinition pCarsNetwork = new oldGameDefinition(GameEnum.PCARS_NETWORK, "pcars_udp", null, "CrewChiefV4.PCars.PCarsSpotterv2",
            null, null, null, false,
            approxFilterNames: new String[] { "pcars_network", "pcars network", "pcars-network" });
        private static oldGameDefinition pCars2Network = new oldGameDefinition(GameEnum.PCARS2_NETWORK, "pcars2_udp", null, "CrewChiefV4.PCars2.PCars2Spotterv2",
            null, null, null, false,
            approxFilterNames: new String[] { "pcars2_network", "pcars 2 network", "pcars-2-network" });
        private static oldGameDefinition ams2Network = new oldGameDefinition(GameEnum.AMS2_NETWORK, "ams2_udp", null, "CrewChiefV4.AMS2.AMS2Spotter",
            null, null, null, false);
        private static oldGameDefinition rFactor1 = new oldGameDefinition(GameEnum.RF1, "rfactor1", "rFactor", "CrewChiefV4.rFactor1.RF1Spotter",
            "rf1_launch_exe", "rf1_launch_params", "launch_rfactor1", true, "rFactor",
            approxFilterNames: new String[] { "rf", "factor1", "factor 1", "factor_1", "factor-1" });

        private static oldGameDefinition gameStockCar = new oldGameDefinition(GameEnum.RF1, "gamestockcar", "GSC", "CrewChiefV4.rFactor1.RF1Spotter",
            "gsc_launch_exe", "gsc_launch_params", "launch_gsc", true, commandLineName: "GSC");
        private static oldGameDefinition automobilista = new oldGameDefinition(GameEnum.RF1, "automobilista", "AMS", "CrewChiefV4.rFactor1.RF1Spotter",
            "ams_launch_exe", "ams_launch_params", "launch_ams", true, "Automobilista", commandLineName: "AMS",
            approxFilterNames: new String[] { "ams", "automobilista" });
        private static oldGameDefinition marcas = new oldGameDefinition(GameEnum.RF1, "marcas", "MARCAS", "CrewChiefV4.rFactor1.RF1Spotter",
            "marcas_launch_exe", "marcas_launch_params", "launch_marcas", true, commandLineName: "MARCAS");
        private static oldGameDefinition ftruck = new oldGameDefinition(GameEnum.RF1, "ftruck", "FTRUCK", "CrewChiefV4.rFactor1.RF1Spotter",
            "ftruck_launch_exe", "ftruck_launch_params", "launch_ftruck", true, commandLineName: "FTRUCK");
        private static oldGameDefinition arcaSimRacing = new oldGameDefinition(GameEnum.RF1, "asr", "ARCA", "CrewChiefV4.rFactor1.RF1Spotter",
            "asr_launch_exe", gameStartCommandOptionsProperty: null, gameStartEnabledProperty: "launch_asr", allowsUserCreatedCars: true, gameInstallDirectory: "arca", commandLineName: "ASR",
            approxFilterNames: new String[] { "asrx", "asr" });
        private static oldGameDefinition assetto64Bit = new oldGameDefinition(GameEnum.ASSETTO_64BIT, "assetto_64_bit", "acs", "CrewChiefV4.assetto.ACSSpotter",
            "acs_launch_exe", "acs_launch_params", "launch_acs", true, "assettocorsa",
            approxFilterNames: new String[] { "assetto", "corsa" });
        private static oldGameDefinition assetto64BitRallyMode = new oldGameDefinition(GameEnum.ASSETTO_64BIT_RALLY, "assetto_64_bit_rally_mode", "acs", null,
            "acs_launch_exe", "acs_launch_params", "launch_acs", true, "assettocorsa", racingType: CrewChief.RacingType.Rally,
            approxFilterNames: new String[] { "assetto", "corsa" });
        private static oldGameDefinition assetto32Bit = new oldGameDefinition(GameEnum.ASSETTO_32BIT, "assetto_32_bit", "acs_x86", "CrewChiefV4.assetto.ACSSpotter",
            "acs_launch_exe", "acs_launch_params", "launch_acs", true, "assettocorsa");
        private static oldGameDefinition rfactor2_64bit = new oldGameDefinition(GameEnum.RF2_64BIT, "rfactor2_64_bit", "rFactor2", "CrewChiefV4.rFactor2.RF2Spotter",
            "rf2_launch_exe", "rf2_launch_params", "launch_rfactor2", true, "rFactor 2", commandLineName: "RF2",
            approxFilterNames: new String[] { "rf2", "rf_2", "rf 2", "factor2", "factor 2", "factor_2", "factor-2" });
        private static oldGameDefinition lmu = new oldGameDefinition(GameEnum.LMU, "lmu", "Le Mans Ultimate", "CrewChiefV4.rFactor2.RF2Spotter",
            "lmu_launch_exe", "lmu_launch_params", "launch_lmu", true, "Le Mans Ultimate", commandLineName: "LMU",
            approxFilterNames: new String[] { "lmu", "le mans ultimate", "le mans", "ultimate" });
        private static oldGameDefinition iracing = new oldGameDefinition(GameEnum.IRACING, "iracing", "iRacingSim64DX11", "CrewChiefV4.iRacing.iRacingSpotter",
            "iracing_launch_exe", "iracing_launch_params", "launch_iracing", false,
            approxFilterNames: new String[] { "iracing", "i racing", "i_racing", "i-racing" });
        private static oldGameDefinition f1_2018 = new oldGameDefinition(GameEnum.F1_2018, "f1_2018", null, "CrewChiefV4.F1_2018.F12018Spotter",
            "f1_2018_launch_exe", "f1_2018_launch_params", "launch_f1_2018", false,
            approxFilterNames: new String[] { "2018" });
        private static oldGameDefinition acc = new oldGameDefinition(GameEnum.ACC, "acc", "AC2-Win64-Shipping", "CrewChiefV4.ACC.ACCSpotter",
            "acc_launch_exe", "acc_launch_params", "launch_acc", false,
            approxFilterNames: new String[] { "competizione", "acc" });
        private static oldGameDefinition f1_2019 = new oldGameDefinition(GameEnum.F1_2019, "f1_2019", null, "CrewChiefV4.F1_2019.F12019Spotter",
            "f1_2019_launch_exe", "f1_2019_launch_params", "launch_f1_2019", false,
            approxFilterNames: new String[] { "2019" });
        private static oldGameDefinition f1_2020 = new oldGameDefinition(GameEnum.F1_2020, "f1_2020", null, "CrewChiefV4.F1_2020.F12020Spotter",
            "f1_2020_launch_exe", "f1_2020_launch_params", "launch_f1_2020", false,
            approxFilterNames: new String[] { "2020" });
        private static oldGameDefinition f1_2021 = new oldGameDefinition(GameEnum.F1_2021, "f1_2021", null, "CrewChiefV4.F1_2021.F12021Spotter",
            "f1_2021_launch_exe", "f1_2021_launch_params", "launch_f1_2021", false,
            approxFilterNames: new String[] { "2021" });
        private static oldGameDefinition f1_2022 = new oldGameDefinition(GameEnum.F1_2022, "f1_2022", null, "CrewChiefV4.F1_2022.F12022Spotter",
            "f1_2022_launch_exe", "f1_2022_launch_params", "launch_f1_2022", false,
            approxFilterNames: new String[] { "2022" });
        private static oldGameDefinition f1_2023 = new oldGameDefinition(GameEnum.F1_2023, "f1_2023", null, "CrewChiefV4.F1_2023.F12023Spotter",
            "f1_2023_launch_exe", "f1_2023_launch_params", "launch_f1_2023", false,
            approxFilterNames: new String[] { "2023" });
        private static oldGameDefinition rbr = new oldGameDefinition(GameEnum.RBR, "rbr", "RichardBurnsRally_SSE", null /*spotterName*/,
            "rbr_launch_exe", null /*gameStartCommandOptionsProperty*/, "launch_rbr", true, "RBR", null, racingType: CrewChief.RacingType.Rally);
        private static oldGameDefinition gtr2 = new oldGameDefinition(GameEnum.GTR2, "gtr2", "GTR2", "CrewChiefV4.GTR2.GTR2Spotter",
            "gtr2_launch_exe", null /*gameStartCommandOptionsProperty*/, "launch_gtr2", true, "GTR2",
            approxFilterNames: new String[] { "gtr2", "gtr 2", "gtr_2", "gtr-2" });
        private static oldGameDefinition dirt = new oldGameDefinition(GameEnum.DIRT, "dirt", null, null /*spotterName*/,
            "dirt_launch_exe", "dirt_launch_params" /*gameStartCommandOptionsProperty*/, "launch_dirt", false, "", null, racingType: CrewChief.RacingType.Rally,
            approxFilterNames: new String[] { "dirt" });
        private static oldGameDefinition dirt2 = new oldGameDefinition(GameEnum.DIRT_2, "dirt2", null, null /*spotterName*/,
            "dirt2_launch_exe", "dirt2_launch_params" /*gameStartCommandOptionsProperty*/, "launch_dirt2", false, "", null, racingType: CrewChief.RacingType.Rally,
            approxFilterNames: new String[] { "dirt_2" });

        private static oldGameDefinition any = new oldGameDefinition(GameEnum.ANY, "all_games", null, null, null, null, null, false, "", macroEditorName: "Generic (all games)");
        private static oldGameDefinition none = new oldGameDefinition(GameEnum.NONE, "unsupported_game", null, null, null, null, null, false, "", macroEditorName: "Unsupported games");

        private static List<oldGameDefinition> getAllGameDefinitions()
        {
            List<oldGameDefinition> definitions = new List<oldGameDefinition>();
            definitions.Add(automobilista);
            definitions.Add(AMS2);
            definitions.Add(gameStockCar);
            definitions.Add(marcas);
            definitions.Add(ftruck);
            definitions.Add(arcaSimRacing);
            definitions.Add(pCars2);
            definitions.Add(pCars3);
            definitions.Add(pCars64Bit);
            definitions.Add(pCars32Bit);
            definitions.Add(raceRoom);
            definitions.Add(pCarsNetwork);

            // TODO: reinstate this when it actually works:
            // definitions.Add(pCars2Network);

            // Add fixes present in new method
            f1_2018.processName = "F1_2018";
            f1_2019.processName = "F1_2019";
            f1_2020.processName = "F1_2020";
            f1_2021.processName = "F1_2021";
            f1_2022.processName = "F1_2022";
            f1_2023.processName = "F1_2023";
            dirt.processName = "dirtrally";
            dirt2.processName = "dirtrally2";

            definitions.Add(rFactor1);
            definitions.Add(assetto64Bit);
            definitions.Add(assetto64BitRallyMode);
            definitions.Add(assetto32Bit);
            definitions.Add(rfactor2_64bit);
            definitions.Add(lmu);
            definitions.Add(iracing);
            definitions.Add(acc);
            definitions.Add(f1_2018);
            definitions.Add(f1_2019);
            definitions.Add(f1_2020);
            definitions.Add(f1_2021);
            definitions.Add(f1_2022);
            definitions.Add(f1_2023);
            definitions.Add(rbr);
            definitions.Add(gtr2);
            definitions.Add(none);
            definitions.Add(dirt);
            definitions.Add(dirt2);
            return definitions;
        }

        [Ignore("Only checking original change, broken by rearranging args")]
        [Test]
        public void TestMethod1()
        {
            currentActiveProfile = new UserProfileSettings();
            var oldMethod = getAllGameDefinitions();
            var newMethod = GameDefinition.AllGameDefinitions;
            Console.Write(oldMethod);
            Console.Write(newMethod);
            // Serialize both lists to JSON
            string oldJson = JsonConvert.SerializeObject(oldMethod);
            string newJson = JsonConvert.SerializeObject(newMethod);
            Assert.AreEqual(oldJson, newJson);
        }
    }
}
