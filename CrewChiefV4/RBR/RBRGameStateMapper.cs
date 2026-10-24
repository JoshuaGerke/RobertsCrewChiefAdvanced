//#define SIMULATE_ONLINE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrewChiefV4.GameState;
using CrewChiefV4.Events;
using CrewChiefV4.RBR.RBRData;
using System.Diagnostics;
using static CrewChiefV4.RBR.Constants;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;

/**
 * Maps memory mapped file to a local game-agnostic representation.
 */
namespace CrewChiefV4.RBR
{
    public class RBRGameStateMapper : GameStateMapper
    {
        public RBRGameStateMapper()
        {}

        private int[] minimumSupportedVersionParts = new int[] { 1, 3, 0, 0 };
        public static bool pluginVerified = false;
        private static int reinitWaitAttempts = 0;
        // regex using the chars .Net says are invalid
        private string tracknameToValidFolderNameRegex = string.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidPathChars())));
        // max length for track name folder
        private int maxLengthForTrackNameFolder = 65;

        // BTB tracks all use track ID 41
        private int BTBTrackID = 41;

        private class CarID
        {
            public int hash = -1;

            // Both of those fields are for debugging only.  carCgsFileName must never be empty (otherwise we can't hash).
            public string carCgsFileName = "";

            // carName might be empty, but it appears to be available on most modern cars.
            public string carName = "";
        }

        private Dictionary<int, CarID> slotToCarID = null;

        public override void versionCheck(Object memoryMappedFileStruct)
        {
            if (RBRGameStateMapper.pluginVerified || CrewChief.loadDataFromFile)
                return;

            var shared = memoryMappedFileStruct as RBRSharedMemoryReader.RBRStructWrapper;
            var versionStr = RBRGameStateMapper.GetStringFromBytes(shared.extended.mVersion);
            if (string.IsNullOrWhiteSpace(versionStr)
                && RBRGameStateMapper.reinitWaitAttempts < 500)
            {
                // SimHub (and possibly other tools) leaks the shared memory block, making us read the empty one.
                // Wait a bit before re-checking version string.
                ++RBRGameStateMapper.reinitWaitAttempts;
                Thread.Sleep(100);
                return;
            }

            // Only verify once.
            RBRGameStateMapper.pluginVerified = true;
            RBRGameStateMapper.reinitWaitAttempts = 0;

            var failureHelpMsg = ".\nMake sure you have \"Update game plugins on startup\" option enabled."
                + "\nAlternatively, visit https://forum.studio-397.com/index.php?threads/crew-chief-v4-5-with-rfactor-2-support.54421/ "
                + "to download and update plugin manually.";

            var versionParts = versionStr.Split('.');
            if (versionParts.Length != 4)
            {
                Console.WriteLine("Corrupt or leaked RBR Shared Memory.  Version string: " + versionStr + failureHelpMsg);
                return;
            }

            int smVer = 0;
            int minVer = 0;
            int partFactor = 1;
            for (int i = 3; i >= 0; --i)
            {
                int versionPart = 0;
                if (!int.TryParse(versionParts[i], out versionPart))
                {
                    Console.WriteLine("Corrupt or leaked RBR Shared Memory version.  Version string: " + versionStr + failureHelpMsg);
                    return;
                }

                smVer += (versionPart * partFactor);
                minVer += (this.minimumSupportedVersionParts[i] * partFactor);
                partFactor *= 100;
            }

            if (smVer < minVer)
            {
                var minVerStr = string.Join(".", this.minimumSupportedVersionParts);
                var msg = "Unsupported RBR Shared Memory version: "
                    + versionStr
                    + "  Minimum supported version is: "
                    + minVerStr
                    + "  Please update CrewChief.dll" + failureHelpMsg;
                Console.WriteLine(msg);
            }
            else
            {
                var msg = "RBR Shared Memory version: " + versionStr;
                Console.WriteLine(msg);
            }

            populateCarIds();

        }

        private void populateCarIds()
        {
            try
            {
                // Read cars.ini in an attempt to uniquely identify cars.
                var carsIniPath = Path.Combine(CrewChief.gameExeParentDirectory, @"Cars\Cars.ini");
                if (File.Exists(carsIniPath))
                {
                    this.slotToCarID = new Dictionary<int, CarID>();
                    for (var i = 0; i < 8; ++i)
                    {
                        var carCgsFileName = Utilities.ReadIniValue($"Car0{i}", "FileName", carsIniPath);
                        var carName = Utilities.ReadIniValue($"Car0{i}", "CarName", carsIniPath);
                        if (string.IsNullOrWhiteSpace(carCgsFileName))
                            Console.WriteLine($"Failed to load car at slot ID: {i}");
                        else
                        {
                            this.slotToCarID.Add(i, new CarID()
                            {
                                hash = carCgsFileName.GetHashCode(),
                                carCgsFileName = carCgsFileName,
                                carName = carName
                            });

                            Console.WriteLine($"Car in slot: {i} hashed to: 0x{carCgsFileName.GetHashCode():X}. ({(string.IsNullOrWhiteSpace(carName) ? string.Empty : "Car name: " + carName + "    ")}File name: {carCgsFileName})");
                        }
                    }
                }
                else
                    Console.WriteLine($"File `{carsIniPath}` was not found, car identification will not work.");
            }
            catch (Exception e)
            {
                Utilities.ReportException(e, "Failed to parse cars.ini", needReport: false);
            }
        }

        private enum RBRSurfaceType
        {
            Unknown = 0,
            Snow = 1,
            Gravel = 2,
            Tarmac = 3
   
        }

        [Flags]
        private enum RBRPacenoteModifier : int
        {
            none = 0,
            detail_narrows = 1,
            detail_wideout = 2,
            detail_tightens = 4,
            detail_dont_cut = 32,
            detail_cut = 64,
            detail_double_tightens = 128,
            detail_no_link = 256,
            detail_long = 1024,
            detail_maybe = 8192
        }
        private static int SanitizePacenoteModifier(int input)
        {
            // Initialize a variable to hold the result bitfield.
            int resultBitfield = 0;

            // Iterate through the RBRPacenoteModifier enum values.
            foreach (RBRPacenoteModifier modifier in Enum.GetValues(typeof(RBRPacenoteModifier)))
            {
                // Check if the current modifier bit is set in the input.
                if ((input & (int)modifier) != 0)
                {
                    // Add the modifier to the result bitfield.
                    resultBitfield |= (int)modifier;
                }
            }

            return resultBitfield;
        }

        private class RBRTrackDefinition
        {
            public RBRTrackDefinition(string name, string country, RBRGameStateMapper.RBRSurfaceType surface, double approxLengthKM)
            {
                this.name = name;
                this.country = country;
                this.surface = surface;
                this.approxLengthKM = approxLengthKM;
            }

            public string name;
            public string country;
            public RBRGameStateMapper.RBRSurfaceType surface;
            public double approxLengthKM;
        }

        // BTB tracks all use slot ID 41, but they'll have a different name from the expected track in slot 41
        private bool IsBTBTrack(string trackName, int trackID)
        {
            return trackID == BTBTrackID
                && knownTracks.TryGetValue(BTBTrackID, out var trackDefinitionForBTBSlot) && trackDefinitionForBTBSlot.name != trackName;
        }

        // List from RBRCZ.
        private Dictionary<int, RBRGameStateMapper.RBRTrackDefinition> knownTracks = new Dictionary<int, RBRTrackDefinition>()
        {
            { 10, new RBRTrackDefinition("Kaihuavaara", "Finland", RBRGameStateMapper.RBRSurfaceType.Snow, 6.1) },
            { 11, new RBRTrackDefinition( "Mustaselka", "Finland", RBRGameStateMapper.RBRSurfaceType.Snow, 7.9) },
            { 12, new RBRTrackDefinition( "Sikakama", "Finland", RBRGameStateMapper.RBRSurfaceType.Snow, 10.2) },
            { 13, new RBRTrackDefinition( "Autiovaara", "Finland", RBRGameStateMapper.RBRSurfaceType.Snow, 6.1) },
            { 14, new RBRTrackDefinition( "Kaihuavaara II", "Finland", RBRGameStateMapper.RBRSurfaceType.Snow, 6.1) },
            { 15, new RBRTrackDefinition( "Mustaselka II", "Finland", RBRGameStateMapper.RBRSurfaceType.Snow, 7.7) },
            { 16, new RBRTrackDefinition( "Sikakama II", "Finland", RBRGameStateMapper.RBRSurfaceType.Snow, 10.2) },
            { 17, new RBRTrackDefinition( "Autiovaara II", "Finland", RBRGameStateMapper.RBRSurfaceType.Snow, 6.1) },
            { 20, new RBRTrackDefinition( "Harwood Forest", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.1) },
            { 21, new RBRTrackDefinition( "Falstone", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.6) },
            { 22, new RBRTrackDefinition( "Chirdonhead", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 7) },
            { 23, new RBRTrackDefinition( "Shepherds Shield", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 4.8) },
            { 24, new RBRTrackDefinition( "Harwood Forest II", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.9) },
            { 25, new RBRTrackDefinition( "Chirdonhead II", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.9) },
            { 26, new RBRTrackDefinition( "Falstone II", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.6) },
            { 27, new RBRTrackDefinition( "Shepherds Shield II", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 4.9) },
            { 30, new RBRTrackDefinition( "Greenhills II", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 6) },
            { 31, new RBRTrackDefinition( "New Bobs", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 10.1) },
            { 32, new RBRTrackDefinition( "Greenhills", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 6) },
            { 33, new RBRTrackDefinition( "Mineshaft", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.2) },
            { 34, new RBRTrackDefinition( "East-West", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 9.5) },
            { 35, new RBRTrackDefinition( "New Bobs II", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 10) },
            { 36, new RBRTrackDefinition( "East-West II", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 9.6) },
            { 37, new RBRTrackDefinition( "Mineshaft II", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.2) },
            { 41, new RBRTrackDefinition( "Cote D'Arbroz", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 4.5) },
            { 42, new RBRTrackDefinition( "Joux Verte", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 7.9) },
            { 43, new RBRTrackDefinition( "Bisanne", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.6) },
            { 44, new RBRTrackDefinition( "Joux Plane", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 11.1) },
            { 45, new RBRTrackDefinition( "Joux Verte II", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 7.8) },
            { 46, new RBRTrackDefinition( "Cote D'Arbroz II", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 4.3) },
            { 47, new RBRTrackDefinition( "Bisanne II", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.6) },
            { 48, new RBRTrackDefinition( "Joux Plane II", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 11.1) },
            { 50, new RBRTrackDefinition( "Sipirkakim II", "Japan", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.7) },
            { 51, new RBRTrackDefinition( "Noiker", "Japan", RBRGameStateMapper.RBRSurfaceType.Gravel, 13.8) },
            { 52, new RBRTrackDefinition( "Sipirkakim", "Japan", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.7) },
            { 53, new RBRTrackDefinition( "Pirka Menoko", "Japan", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.7) },
            { 54, new RBRTrackDefinition( "Tanner", "Japan", RBRGameStateMapper.RBRSurfaceType.Gravel, 3.9) },
            { 55, new RBRTrackDefinition( "Noiker II", "Japan", RBRGameStateMapper.RBRSurfaceType.Gravel, 13.7) },
            { 56, new RBRTrackDefinition( "Tanner II", "Japan", RBRGameStateMapper.RBRSurfaceType.Gravel, 4) },
            { 57, new RBRTrackDefinition( "Pirka Menoko II", "Japan", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.7) },
            { 60, new RBRTrackDefinition( "Frazier Wells II", "USA", RBRGameStateMapper.RBRSurfaceType.Gravel, 5) },
            { 61, new RBRTrackDefinition( "Fraizer Wells", "USA", RBRGameStateMapper.RBRSurfaceType.Gravel, 5) },
            { 62, new RBRTrackDefinition( "Prospect Ridge", "USA", RBRGameStateMapper.RBRSurfaceType.Gravel, 7.8) },
            { 63, new RBRTrackDefinition( "Diamond Creek", "USA", RBRGameStateMapper.RBRSurfaceType.Gravel, 7.1) },
            { 64, new RBRTrackDefinition( "Hualapai Nation", "USA", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.6) },
            { 65, new RBRTrackDefinition( "Prospect Ridge II", "USA", RBRGameStateMapper.RBRSurfaceType.Gravel, 7.9) },
            { 66, new RBRTrackDefinition( "Diamond Creek II", "USA", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.8) },
            { 67, new RBRTrackDefinition( "Hualapai Nation II", "USA", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.6) },
            { 70, new RBRTrackDefinition( "Prospect Ridge II A", "USA", RBRGameStateMapper.RBRSurfaceType.Gravel, 7.6) },
            { 71, new RBRTrackDefinition( "Rally School Stage", "Rally school", RBRGameStateMapper.RBRSurfaceType.Gravel, 2.2) },
            { 90, new RBRTrackDefinition( "Rally School Stage II", "Rally school", RBRGameStateMapper.RBRSurfaceType.Gravel, 2.3) },
            { 94, new RBRTrackDefinition( "Stryckovy okruh", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 9.2) },
            { 95, new RBRTrackDefinition( "Sumburk 2007", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 12.4) },
            { 96, new RBRTrackDefinition( "Sosnova", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 7.1) },
            { 99, new RBRTrackDefinition( "Zadverice", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 14.9) },
            { 100, new RBRTrackDefinition( "Vinec-Skalsko", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 17.8) },
            { 101, new RBRTrackDefinition( "Vinec-Skalsko NIGHT", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 17.8) },
            { 103, new RBRTrackDefinition( "Zadverice II", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 14.9) },
            { 105, new RBRTrackDefinition( "Sosnova 2010", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 4.2) },
            { 106, new RBRTrackDefinition( "Stryckovy - Zadni Porici", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 6.9) },
            { 107, new RBRTrackDefinition( "PTD Rallysprint", "Netherlands", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.1) },
            { 108, new RBRTrackDefinition( "Osli - Stryckovy", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 10.6) },
            { 125, new RBRTrackDefinition( "Bergheim v1.1", "Germany", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8) },
            { 131, new RBRTrackDefinition( "Lyon - Gerland", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 0.7) },
            { 132, new RBRTrackDefinition( "Gestel", "Netherlands", RBRGameStateMapper.RBRSurfaceType.Tarmac, 7.2) },
            { 139, new RBRTrackDefinition( "RSI slalom Shonen", "Ireland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 1) },
            { 140, new RBRTrackDefinition( "RSI slalom gegeWRC", "Ireland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 1.8) },
            { 141, new RBRTrackDefinition( "Mlynky", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 7.1) },
            { 142, new RBRTrackDefinition( "Mlynky Snow", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Snow, 7.1) },
            { 143, new RBRTrackDefinition( "Peklo", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8.5) },
            { 144, new RBRTrackDefinition( "Peklo Snow", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Snow, 8.5) },
            { 145, new RBRTrackDefinition( "Versme", "Lithuania", RBRGameStateMapper.RBRSurfaceType.Gravel, 3.2) },
            { 146, new RBRTrackDefinition( "Peklo II", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8.5) },
            { 147, new RBRTrackDefinition( "Peklo II Snow", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Snow, 8.5) },
            { 148, new RBRTrackDefinition( "ROC 2008", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Tarmac, 2) },
            { 149, new RBRTrackDefinition( "Sieversdorf V1.1", "Germany", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8) },
            { 152, new RBRTrackDefinition( "RP 2009 Shakedown", "Poland", RBRGameStateMapper.RBRSurfaceType.Gravel, 4.4) },
            { 153, new RBRTrackDefinition( "RP 2009 Shakedown Reversed", "Poland", RBRGameStateMapper.RBRSurfaceType.Gravel, 4.4) },
            { 154, new RBRTrackDefinition( "Bruchsal-Unteroewisheim", "Germany", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8.9) },
            { 155, new RBRTrackDefinition( "Humalamaki 1.0", "Finland", RBRGameStateMapper.RBRSurfaceType.Gravel, 4.4) },
            { 156, new RBRTrackDefinition( "Mlynky II", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 7.1) },
            { 157, new RBRTrackDefinition( "Grand Canaria ROC 2000", "Spain", RBRGameStateMapper.RBRSurfaceType.Gravel, 3.8) },
            { 158, new RBRTrackDefinition( "Sweet Lamb", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.1) },
            { 159, new RBRTrackDefinition( "Sweet Lamb II", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.1) },
            { 471, new RBRTrackDefinition( "Aragona", "Italy", RBRGameStateMapper.RBRSurfaceType.Tarmac, 6.4) },
            { 472, new RBRTrackDefinition( "Muxarello", "Italy", RBRGameStateMapper.RBRSurfaceType.Tarmac, 15.4) },
            { 478, new RBRTrackDefinition( "Rallysprint Hondarribia 2011", "Spain", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8) },
            { 479, new RBRTrackDefinition( "Shomaru Pass", "Japan", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.8) },
            { 480, new RBRTrackDefinition( "Shomaru Pass II", "Japan", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.8) },
            { 481, new RBRTrackDefinition( "Karlstad", "Sweeden", RBRGameStateMapper.RBRSurfaceType.Snow, 1.9) },
            { 482, new RBRTrackDefinition( "Karlstad II", "Sweeden", RBRGameStateMapper.RBRSurfaceType.Snow, 1.9) },
            { 484, new RBRTrackDefinition( "Humalamaki Reversed", "Finland", RBRGameStateMapper.RBRSurfaceType.Gravel, 4.4) },
            { 485, new RBRTrackDefinition( "Torsby Shakedown", "Sweeden", RBRGameStateMapper.RBRSurfaceType.Snow, 4.2) },
            { 488, new RBRTrackDefinition( "Jirkovicky", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 3.5) },
            { 489, new RBRTrackDefinition( "Jirkovicky II", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 3.5) },
            { 490, new RBRTrackDefinition( "Sourkov", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.2) },
            { 491, new RBRTrackDefinition( "Lernovec", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 5) },
            { 492, new RBRTrackDefinition( "Uzkotin", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 7.8) },
            { 493, new RBRTrackDefinition( "Hroudovany", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 7.1) },
            { 494, new RBRTrackDefinition( "Snekovice", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.6) },
            { 495, new RBRTrackDefinition( "Lernovec II", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 5) },
            { 496, new RBRTrackDefinition( "Uzkotin II", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 7.6) },
            { 497, new RBRTrackDefinition( "Hroudovany II", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 7.1) },
            { 498, new RBRTrackDefinition( "Snekovice II", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.6) },
            { 499, new RBRTrackDefinition( "Sourkov 2", "Montekland", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.2) },
            { 516, new RBRTrackDefinition( "Hradek 1", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.8) },
            { 517, new RBRTrackDefinition( "Hradek 2", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.8) },
            { 518, new RBRTrackDefinition( "Liptakov 1", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 6) },
            { 519, new RBRTrackDefinition( "Liptakov 2", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 6) },
            { 522, new RBRTrackDefinition( "Rally School Czech", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 3.2) },
            { 524, new RBRTrackDefinition( "Rally School Czech II", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 3.1) },
            { 528, new RBRTrackDefinition( "Kuadonvaara", "Finland", RBRGameStateMapper.RBRSurfaceType.Snow, 5.7) },
            { 533, new RBRTrackDefinition( "Karowa 2009", "Poland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 1.6) },
            { 534, new RBRTrackDefinition( "Haugenau 2012", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.7) },
            { 544, new RBRTrackDefinition( "Fernet Branca", "Argentina", RBRGameStateMapper.RBRSurfaceType.Gravel, 6) },
            { 545, new RBRTrackDefinition( "Junior Wheels I", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.6) },
            { 546, new RBRTrackDefinition( "Junior Wheels II", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.6) },
            { 550, new RBRTrackDefinition( "Foron", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 9.2) },
            { 551, new RBRTrackDefinition( "Foron II", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 9.2) },
            { 552, new RBRTrackDefinition( "Foron Snow", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 9.1) },
            { 553, new RBRTrackDefinition( "Foron Snow II", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 9.1) },
            { 555, new RBRTrackDefinition( "Maton I", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 3.5) },
            { 556, new RBRTrackDefinition( "Maton II", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 3.5) },
            { 557, new RBRTrackDefinition( "Red Bull HC", "Italy", RBRGameStateMapper.RBRSurfaceType.Gravel, 14) },
            { 558, new RBRTrackDefinition( "Maton snow", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 3.5) },
            { 559, new RBRTrackDefinition( "Maton snow II", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 3.5) },
            { 560, new RBRTrackDefinition( "Loch Ard", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.3) },
            { 561, new RBRTrackDefinition( "Loch Ard II", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.3) },
            { 565, new RBRTrackDefinition( "Undva Reversed", "Estonia", RBRGameStateMapper.RBRSurfaceType.Gravel, 10) },
            { 566, new RBRTrackDefinition( "Undva", "Estonia", RBRGameStateMapper.RBRSurfaceType.Gravel, 10) },
            { 570, new RBRTrackDefinition( "Peyregrosse - Mandagout", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 12.8) },
            { 571, new RBRTrackDefinition( "Peyregrosse - Mandagout NIGHT", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 12.8) },
            { 572, new RBRTrackDefinition( "Castrezzato", "Italy", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8.1) },
            { 573, new RBRTrackDefinition( "SS Daniel Bonara", "Italy", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.5) },
            { 574, new RBRTrackDefinition( "Sorica", "Slovenia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 15.5) },
            { 582, new RBRTrackDefinition( "Barum rally 2009 Semetin", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 11.7) },
            { 583, new RBRTrackDefinition( "Barum rally 2010 Semetin", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 11.7) },
            { 585, new RBRTrackDefinition( "SWISS II", "Switzerland", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.6) },
            { 586, new RBRTrackDefinition( "SWISS I", "Switzerland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.6) },
            { 587, new RBRTrackDefinition( "Swiss IV", "Swiss", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.2) },
            { 589, new RBRTrackDefinition( "Swiss III", "Swiss", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8.2) },
            { 590, new RBRTrackDefinition( "Blanare", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 7.6) },
            { 591, new RBRTrackDefinition( "Blanare II", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 6.6) },
            { 592, new RBRTrackDefinition( "Slovakia Ring 2014", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 11) },
            { 593, new RBRTrackDefinition( "Slovakia Ring 2014 II", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 11) },
            { 595, new RBRTrackDefinition( "Sardian", "USA", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.1) },
            { 596, new RBRTrackDefinition( "Sardian Night", "USA", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.1) },
            { 597, new RBRTrackDefinition( "Mlynky Snow II", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Snow, 7.1) },
            { 598, new RBRTrackDefinition( "Pikes Peak 2008", "USA", RBRGameStateMapper.RBRSurfaceType.Tarmac, 19.9) },
            { 599, new RBRTrackDefinition( "Northumbria", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 9) },
            { 601, new RBRTrackDefinition( "Northumbria Tarmac", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Tarmac, 9) },
            { 607, new RBRTrackDefinition( "Sturec", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8.1) },
            { 608, new RBRTrackDefinition( "Sturec II", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8.2) },
            { 609, new RBRTrackDefinition( "Sturec snow", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Snow, 8.1) },
            { 610, new RBRTrackDefinition( "Sturec snow II", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Snow, 8.2) },
            { 611, new RBRTrackDefinition( "Capo Di Feno", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 4.5) },
            { 612, new RBRTrackDefinition( "Capo Di Feno II", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 4.5) },
            { 613, new RBRTrackDefinition( "Uhorna", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 11.5) },
            { 614, new RBRTrackDefinition( "Uhorna II", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Tarmac, 11.5) },
            { 615, new RBRTrackDefinition( "Uhorna snow", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Snow, 11.5) },
            { 616, new RBRTrackDefinition( "Uhorna snow II", "Slovakia", RBRGameStateMapper.RBRSurfaceType.Snow, 11.5) },
            { 700, new RBRTrackDefinition( "Passo Valle", "Italy", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.8) },
            { 701, new RBRTrackDefinition( "Passo Valle Reverse", "Itally", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.8) },
            { 703, new RBRTrackDefinition( "Lousada - WRC", "Portugal", RBRGameStateMapper.RBRSurfaceType.Gravel, 3.3) },
            { 704, new RBRTrackDefinition( "Lousada - RX", "Portugal", RBRGameStateMapper.RBRSurfaceType.Tarmac, 3.6) },
            { 705, new RBRTrackDefinition( "Lousada - RG", "Portugal", RBRGameStateMapper.RBRSurfaceType.Gravel, 3.8) },
            { 707, new RBRTrackDefinition( "Carvalho de Rei 2008 II", "Portugal", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.2) },
            { 708, new RBRTrackDefinition( "Carvalho de Rei 2008", "Portugal", RBRGameStateMapper.RBRSurfaceType.Gravel, 8.2) },
            { 709, new RBRTrackDefinition( "Travanca do Monte", "Portugal", RBRGameStateMapper.RBRSurfaceType.Gravel, 3) },
            { 711, new RBRTrackDefinition( "Akagi Mountain", "Japan", RBRGameStateMapper.RBRSurfaceType.Tarmac, 3.5) },
            { 712, new RBRTrackDefinition( "Akagi Mountain II", "Japan", RBRGameStateMapper.RBRSurfaceType.Tarmac, 3.5) },
            { 720, new RBRTrackDefinition( "Verkiai 2010", "Lithuania", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.6) },
            { 721, new RBRTrackDefinition( "Verkiai 2010 Reverse", "Lithuania", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.9) },
            { 723, new RBRTrackDefinition( "Verkiai 2010 SSS", "Lithuania", RBRGameStateMapper.RBRSurfaceType.Tarmac, 1) },
            { 755, new RBRTrackDefinition( "Dolmen", "Italy", RBRGameStateMapper.RBRSurfaceType.Gravel, 13.3) },
            { 777, new RBRTrackDefinition( "Pian del Colle", "Italy", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8.3) },
            { 778, new RBRTrackDefinition( "Pian del Colle Reversed", "Italy", RBRGameStateMapper.RBRSurfaceType.Tarmac, 8.4) },
            { 779, new RBRTrackDefinition( "Pian del Colle Snow", "Italy", RBRGameStateMapper.RBRSurfaceType.Snow, 8.3) },
            { 780, new RBRTrackDefinition( "Pian del Colle Snow Reversed", "Italy", RBRGameStateMapper.RBRSurfaceType.Snow, 8.4) },
            { 800, new RBRTrackDefinition( "Ai-Petri", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Tarmac, 17.3) },
            { 801, new RBRTrackDefinition( "Uchan-Su", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Tarmac, 10.8) },
            { 802, new RBRTrackDefinition( "Ai-Petri Winter", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Snow, 17.3) },
            { 803, new RBRTrackDefinition( "Uchan-Su Winter", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Snow, 10.8) },
            { 810, new RBRTrackDefinition( "Livadija", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.5) },
            { 811, new RBRTrackDefinition( "Livadija II", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.5) },
            { 820, new RBRTrackDefinition( "La Rocca", "Italy", RBRGameStateMapper.RBRSurfaceType.Gravel, 7.4) },
            { 830, new RBRTrackDefinition( "Azov", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Gravel, 19.1) },
            { 831, new RBRTrackDefinition( "Azov II", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Gravel, 19.2) },
            { 833, new RBRTrackDefinition( "Shurdin II", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Gravel, 22.1) },
            { 834, new RBRTrackDefinition( "Shurdin I", "Ukraine", RBRGameStateMapper.RBRSurfaceType.Gravel, 22.1) },
            { 848, new RBRTrackDefinition( "Luceram-Col Saint Roch", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.6) },
            { 886, new RBRTrackDefinition( "Zaraso Salos Trekas - 5 laps", "Lithuania", RBRGameStateMapper.RBRSurfaceType.Gravel, 5) },
            { 887, new RBRTrackDefinition( "Zaraso Salos Trekas - 2 laps", "Lithuania", RBRGameStateMapper.RBRSurfaceType.Gravel, 2) },
            { 888, new RBRTrackDefinition( "Shakedown Rally del Salento 2014", "Italy", RBRGameStateMapper.RBRSurfaceType.Tarmac, 3.8) },
            { 911, new RBRTrackDefinition( "Torre Vecchia", "Italy", RBRGameStateMapper.RBRSurfaceType.Tarmac, 9.8) },
            { 929, new RBRTrackDefinition( "Svince", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 4.8) },
            { 930, new RBRTrackDefinition( "Svince II", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 4.8) },
            { 931, new RBRTrackDefinition( "Rally school mix", "Rally School", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.2) },
            { 932, new RBRTrackDefinition( "Rally school mix II", "Rally School", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.2) },
            { 933, new RBRTrackDefinition( "Rally school tarmac", "Rally School", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.2) },
            { 934, new RBRTrackDefinition( "Rally school tarmac II", "Rally School", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5.2) },
            { 969, new RBRTrackDefinition( "Tavia", "Italy", RBRGameStateMapper.RBRSurfaceType.Gravel, 3.8) },
            { 979, new RBRTrackDefinition( "Berica", "Italy", RBRGameStateMapper.RBRSurfaceType.Gravel, 14.8) },
            { 980, new RBRTrackDefinition( "Rally Wisla Shakedown", "Poland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 2.5) },
            { 981, new RBRTrackDefinition( "Hyppyjulma gravel", "Finland", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.1) },
            { 982, new RBRTrackDefinition( "Hyppyjulma gravel II", "Finland", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.1) },
            { 983, new RBRTrackDefinition( "Hyppyjulma tarmac", "Finland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 6.1) },
            { 984, new RBRTrackDefinition( "Hyppyjulma tarmac II", "Finland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 6.1) },
            { 985, new RBRTrackDefinition( "Kolmenjarvet gravel", "Finland", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.1) },
            { 986, new RBRTrackDefinition( "Kolmenjarvet gravel II", "Finland", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.1) },
            { 987, new RBRTrackDefinition( "Kolmenjarvet tarmac", "Finland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 6.1) },
            { 988, new RBRTrackDefinition( "Kolmenjarvet tarmac II", "Finland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 6.1) },
            { 989, new RBRTrackDefinition( "Joukkovaara gravel", "Finland", RBRGameStateMapper.RBRSurfaceType.Gravel, 10.2) },
            { 990, new RBRTrackDefinition( "Joukkovaara gravel II", "Finland", RBRGameStateMapper.RBRSurfaceType.Gravel, 10.2) },
            { 991, new RBRTrackDefinition( "Joukkovaara tarmac", "Finland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 10.2) },
            { 992, new RBRTrackDefinition( "Joukkovaara tarmac II", "Finland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 10.2) },
            { 993, new RBRTrackDefinition( "Kormoran Shakedown", "Poland", RBRGameStateMapper.RBRSurfaceType.Gravel, 5.2) },
            { 994, new RBRTrackDefinition( "Kormoran I", "Poland", RBRGameStateMapper.RBRSurfaceType.Gravel, 10.3) },
            { 995, new RBRTrackDefinition( "Kormoran II", "Poland", RBRGameStateMapper.RBRSurfaceType.Gravel, 12) },
            { 996, new RBRTrackDefinition( "SSS Mikolajki I", "Poland", RBRGameStateMapper.RBRSurfaceType.Gravel, 2.6) },
            { 997, new RBRTrackDefinition( "SSS Mikolajki II", "Poland", RBRGameStateMapper.RBRSurfaceType.Gravel, 2.6) },
            { 998, new RBRTrackDefinition( "SSS York I", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 4.3) },
            { 999, new RBRTrackDefinition( "SSS York II", "Australia", RBRGameStateMapper.RBRSurfaceType.Gravel, 4.3) },
            { 1012, new RBRTrackDefinition( "Puy du Lac", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 5) },
            { 1024, new RBRTrackDefinition( "GB Sprint Extreme", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 6.7) },
            { 1025, new RBRTrackDefinition( "FSO Zeran - Warsaw", "Poland", RBRGameStateMapper.RBRSurfaceType.Tarmac, 7.1) },
            { 1141, new RBRTrackDefinition( "Snow Cote D'Arbroz", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 4.5) },
            { 1142, new RBRTrackDefinition( "Snow Joux Verte", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 7.9) },
            { 1143, new RBRTrackDefinition( "Snow Bisanne", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 5.6) },
            { 1144, new RBRTrackDefinition( "Snow Joux Plane", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 11.1) },
            { 1145, new RBRTrackDefinition( "Snow Joux Verte II", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 7.9) },
            { 1146, new RBRTrackDefinition( "Snow Cote D'Arbroz II", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 4.5) },
            { 1147, new RBRTrackDefinition( "Snow Bisanne II", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 5.6) },
            { 1148, new RBRTrackDefinition( "Snow Joux Plane II", "France", RBRGameStateMapper.RBRSurfaceType.Snow, 11.1) },
            { 1521, new RBRTrackDefinition( "Sherwood Forest I", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 13.5) },
            { 1522, new RBRTrackDefinition( "Sherwood Forest II", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 13.5) },
            { 1523, new RBRTrackDefinition( "Sherwood Forest I Summer", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 13.5) },
            { 1524, new RBRTrackDefinition( "Sherwood Forest II Summer", "Great Britain", RBRGameStateMapper.RBRSurfaceType.Gravel, 13.5) },
            { 1899, new RBRTrackDefinition( "Courcelles Val'd Esnoms", "France", RBRGameStateMapper.RBRSurfaceType.Tarmac, 9.9) },
            { 1900, new RBRTrackDefinition( "Vieux Moulin-Perrancey", "France", RBRGameStateMapper.RBRSurfaceType.Gravel, 20.5) },
            { 1914, new RBRTrackDefinition( "Mitterbach", "Austria", RBRGameStateMapper.RBRSurfaceType.Snow, 2.7) },
            { 1996, new RBRTrackDefinition( "Vicar", "Spain", RBRGameStateMapper.RBRSurfaceType.Tarmac, 4.7) },
            { 1999, new RBRTrackDefinition( "Halenkovice SD", "Czech Republic", RBRGameStateMapper.RBRSurfaceType.Tarmac, 4.2) }
        };

        private bool pacenotesLoaded = false;
        private float finishDistance = -1.0f;
        private void ClearState()
        {
            this.pacenotesLoaded = false;
            this.finishDistance = -1.0f;
        }

        public override GameStateData mapToGameStateData(Object memoryMappedFileStruct, GameStateData previousGameState)
        {
            var pgs = previousGameState;
            var shared = memoryMappedFileStruct as RBRSharedMemoryReader.RBRStructWrapper;
            var cgs = new GameStateData(shared.ticksWhenRead);
            cgs.rawGameData = shared;

            var csd = cgs.SessionData;
            var psd = pgs != null ? pgs.SessionData : null;


            // --------------------------------
            // session data

            // TODO: check for replay, return

            csd.EventIndex = 0;
            csd.SessionIteration = 0;

            csd.SessionType = this.MapToSessionType(shared);

            csd.SessionPhase = this.MapToSessionPhase(shared, csd.SessionType);
            csd.SessionNumberOfLaps = 1;  // TODO: multilap circuits

            // New session always starts with countdown.
            if ((pgs == null
                    && csd.SessionPhase != SessionPhase.Unavailable
                    && csd.SessionType != SessionType.Unavailable)
                ||
                    (pgs != null
                    && psd.SessionPhase != csd.SessionPhase
                    && csd.SessionPhase == SessionPhase.Countdown))
            {
                csd.IsNewSession = true;
            }

            if (csd.IsNewSession)
            {
                // Do not use previous game state if this is the new session.
                pgs = null;

                this.ClearState();

                // Initialize variables that persist for the duration of a session.
                string trackName = GetWideStringFromBytes(shared.perFrame.currentLocationStringWide);
                // workaround for RBR CZ tracks plugin. The track may be reported as "Rally HQ"
                if ((string.IsNullOrEmpty(trackName) || "Rally HQ".Equals(trackName))
                    && this.knownTracks.TryGetValue(shared.perFrame.mRBRMapSettings.trackID, out var trackDefinitionForMissingName))
                {
                    trackName = trackDefinitionForMissingName.name;
                }
                // version of the track name that can be used for a folder name
                string trackNameValidFolderName = Regex.Replace(trackName, tracknameToValidFolderNameRegex, "");
                trackNameValidFolderName = trackNameValidFolderName.Substring(0, Math.Min(trackNameValidFolderName.Length, maxLengthForTrackNameFolder));

                // use track length from known tracks, or -1 for BTB and unknown tracks
                double trackLength = -1;
                if (!IsBTBTrack(trackName, shared.perFrame.mRBRMapSettings.trackID)
                    && this.knownTracks.TryGetValue(shared.perFrame.mRBRMapSettings.trackID, out var rbrtd))
                {
                    trackLength = rbrtd.approxLengthKM;
                }
                csd.TrackDefinition = TrackData.getTrackDefinition(trackNameValidFolderName, (float) trackLength);

                // reload car IDs, since they may have changed between sessions, e.g. RSF Launcher does this
                this.populateCarIds();
                // check if this.slotToCarId contains a valid car ID
                this.slotToCarID.TryGetValue(shared.perFrame.mRBRMapSettings.carID, out var carId);
                if (carId != null)
                {
                    cgs.carName = carId.carName;
                }
                else
                {
                    cgs.carName = shared.perFrame.mRBRMapSettings.carID.ToString();
                }

                // based on shared.perFrame.mRBRMapSettings.tyreType int value, set the tyre type name
                string[] tyreTypes =
                {
                    "Dry Tarmac",
                    "Intermediate Tarmac",
                    "Wet Tarmac",
                    "Dry Gravel",
                    "Intermediate Gravel",
                    "Wet Gravel",
                    "Snow",
                };
                cgs.TyreData.TyreTypeName = shared.perFrame.mRBRMapSettings.tyreType < tyreTypes.Length ?
                    tyreTypes[shared.perFrame.mRBRMapSettings.tyreType] :
                    "Unknown";

                csd.SessionStartTime = cgs.Now;
            }

            // Restore cumulative data.
            if (psd != null && !csd.IsNewSession)
            {
                cgs.CoDriverPacenotes = pgs.CoDriverPacenotes;
                csd.IsDNF = psd.IsDNF;
                csd.TrackDefinition = psd.TrackDefinition;
                csd.SessionStartTime = psd.SessionStartTime;
                cgs.carName = pgs.carName;
                cgs.TyreData.TyreTypeName = pgs.TyreData.TyreTypeName;
            }

            if (psd != null
                && psd.SessionPhase != SessionPhase.Finished
                && csd.SessionPhase == SessionPhase.Finished)
            {
                if (shared.perFrame.mRBRCarInfo.distanceFromStartControl < this.finishDistance)
                {
                    Console.WriteLine("DNF detected.");
                    csd.IsDNF = true;
                }
            }

            csd.SessionRunningTime = -shared.perFrame.mRBRCarInfo.stageStartCountdown;
            csd.LapTimeCurrent = shared.perFrame.mRBRCarInfo.raceTime;

            // --------------------------------
            // engine data
            cgs.EngineData.EngineRpm = shared.perFrame.mRBRCarInfo.rpm;
            cgs.EngineData.MinutesIntoSessionBeforeMonitoring = 1;
            cgs.EngineData.EngineWaterTemp = shared.perFrame.mRBRCarInfo.temp;

            // JB: stall detection hackery
            //if (cgs.EngineData.EngineRpm > 5.0f)
              //  this.lastTimeEngineWasRunning = cgs.Now;

            // transmission data
            cgs.TransmissionData.Gear = shared.perFrame.mRBRCarInfo.gear;

            // controls
            cgs.ControlData.BrakePedal = shared.perFrame.mRBRCarControls.brake;
            cgs.ControlData.ThrottlePedal = shared.perFrame.mRBRCarControls.throttle;
            cgs.ControlData.ClutchPedal = shared.perFrame.mRBRCarControls.clutch;
            cgs.ControlData.SteeringWheelAngle = shared.perFrame.mRBRCarControls.steering;
            cgs.ControlData.HandBrake = shared.perFrame.mRBRCarControls.handBrake;

            // --------------------------------
            // damage
            //cgs.CarDamageData.DamageEnabled = true;
            //cgs.CarDamageData.LastImpactTime = (float)playerTelemetry.mLastImpactET;


            ////////////////////////////////////
            // motion data
            cgs.PositionAndMotionData.CarSpeed = shared.perFrame.mRBRCarInfo.speed / 3.6f;  // Convert to m/s
            cgs.PositionAndMotionData.DistanceRoundTrack = shared.perFrame.mRBRCarInfo.distanceFromStartControl;

            if (shared.perFrame.mRBRCarMovement.carMapLocation != null)
            {
                // Thank you, Nicolas! (Wotever).
                var single4 = shared.perFrame.mRBRCarMovement.carMapLocation[0]; // ReadFloat(processHandle, carPointer + 0x110);
                var single5 = shared.perFrame.mRBRCarMovement.carMapLocation[1]; // ReadFloat(processHandle, carPointer + 0x114);
                var single6 = shared.perFrame.mRBRCarMovement.carMapLocation[2]; // ReadFloat(processHandle, carPointer + 0x118);
                var single7 = shared.perFrame.mRBRCarMovement.carMapLocation[5]; // ReadFloat(processHandle, carPointer + 0x124);

                cgs.PositionAndMotionData.Orientation.Roll = -(single6 * 180.0f) / 3.1415926535897931f;
                cgs.PositionAndMotionData.Orientation.Pitch = -(single7 * 180.0f) / 3.1415926535897931f;

                if (single5 == 0f)
                    single5 = 1E-07f;

                var angle = (float)Math.Atan2((double)single4, (double)single5);
                cgs.PositionAndMotionData.Orientation.Yaw = -(angle * 180.0f) / 3.1415926535897931f;

                cgs.PositionAndMotionData.WorldPosition = new float[] {
                    shared.perFrame.mRBRCarMovement.carMapLocation[12],
                    shared.perFrame.mRBRCarMovement.carMapLocation[13],
                    shared.perFrame.mRBRCarMovement.carMapLocation[14]
                };
            }

            // If we don't have a world position yet, use the car position.
            if (cgs.PositionAndMotionData.WorldPosition == null && shared.perFrame.mRBRCarInfo.carPosition != null)
                cgs.PositionAndMotionData.WorldPosition = new float[] {
                    shared.perFrame.mRBRCarInfo.carPosition[0],
                    shared.perFrame.mRBRCarInfo.carPosition[1],
                    shared.perFrame.mRBRCarInfo.carPosition[2]
                };

            if (!this.pacenotesLoaded
                && !cgs.UseCrewchiefPaceNotes
                && ((csd.SessionPhase == SessionPhase.Countdown
                        && csd.SessionType == SessionType.Race
                        && csd.SessionRunningTime > -6.9f)
                    || (pgs == null  // Mid-session connect case.
                        && csd.SessionPhase == SessionPhase.Green)))
                this.LoadPacenotes(shared, cgs);


            // TODO: detect rain, snow etc?
#if false

            ////////////////////////////////////
            // Timings
            if (psd != null && !csd.IsNewSession)
            {
                // Preserve current values.
                // Those values change on sector/lap change, otherwise stay the same between updates.
                psd.restorePlayerTimings(csd);
            }

            // --------------------------------
            // track conditions
            if (cgs.Now > nextConditionsSampleDue)
            {
                nextConditionsSampleDue = cgs.Now.Add(ConditionsMonitor.ConditionsSampleFrequency);
                cgs.Conditions.addSample(cgs.Now, csd.CompletedLaps, csd.SectorNumber,
                    (float)shared.scoring.mScoringInfo.mAmbientTemp, (float)shared.scoring.mScoringInfo.mTrackTemp, (float)shared.scoring.mScoringInfo.mRaining,
                    (float)Math.Sqrt((double)(shared.scoring.mScoringInfo.mWind.x * shared.scoring.mScoringInfo.mWind.x + shared.scoring.mScoringInfo.mWind.y * shared.scoring.mScoringInfo.mWind.y + shared.scoring.mScoringInfo.mWind.z * shared.scoring.mScoringInfo.mWind.z)),
                    0, 0, 0, csd.IsNewLap);
            }

#endif
            // --------------------------------
            // console output
            if (csd.IsNewSession)
            {
                Console.WriteLine("New session, trigger data:");
                Console.WriteLine("SessionType: " + csd.SessionType);
                Console.WriteLine("SessionPhase: " + csd.SessionPhase);
                Console.WriteLine("SessionNumberOfLaps: " + csd.SessionNumberOfLaps);
                Console.WriteLine("EventIndex: " + csd.EventIndex);
                Console.WriteLine("RBR CarID: " + shared.perFrame.mRBRMapSettings.carID);
                Console.WriteLine("RBR TrackID: " + shared.perFrame.mRBRMapSettings.trackID);

                if (this.knownTracks.TryGetValue(shared.perFrame.mRBRMapSettings.trackID, out var rbrtd))
                {
                    Console.WriteLine("RBR TrackName: " + rbrtd.name);
                    Console.WriteLine("RBR TrackCountry: " + rbrtd.country);
                    Console.WriteLine("RBR TrackSurface: " + rbrtd.surface);
                    Console.WriteLine($"RBR Approximate length: {rbrtd.approxLengthKM.ToString("0.000")}km");
                }

                Utilities.TraceEventClass(cgs);
            }
            if (pgs != null && psd.SessionPhase != csd.SessionPhase)
            {
                Console.WriteLine("SessionPhase changed from " + psd.SessionPhase +
                    " to " + csd.SessionPhase + "  New gameMode: " + shared.perFrame.mRBRGameMode.gameMode);
                if (csd.SessionPhase == SessionPhase.Checkered ||
                    csd.SessionPhase == SessionPhase.Finished)
                    Console.WriteLine("Checkered - completed " + csd.CompletedLaps + " laps, session running time = " + csd.SessionRunningTime);
            }

            /*CrewChief.trackName = csd.TrackDefinition.name;
            CrewChief.carClass = cgs.carClass.carClassEnum;
            CrewChief.distanceRoundTrack = cgs.PositionAndMotionData.DistanceRoundTrack;
            CrewChief.viewingReplay = false;*/

            if (pgs != null
                && csd.SessionType == SessionType.Race
                && csd.SessionPhase == SessionPhase.Green
                && (pgs.SessionData.SessionPhase == SessionPhase.Formation
                    || pgs.SessionData.SessionPhase == SessionPhase.Countdown))
            {
                csd.JustGoneGreen = true;
                if (shared.perFrame.mRBRCarInfo.raceTime - 5.0f > -shared.perFrame.mRBRCarInfo.stageStartCountdown)
                {
                    cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.TEN_SECONDS;
                    cgs.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.FALSE_START;
                    Console.WriteLine("False start detected.");
                }
            }

            // ------------------------
            // Chart telemetry data.
            if (CrewChief.recordChartTelemetryDuringRace || csd.SessionType != SessionType.Race)
            {
#if false
                cgs.EngineData.Gear = playerTelemetry.mGear;

                cgs.TelemetryData.FrontDownforce = playerTelemetry.mFrontDownforce;
                cgs.TelemetryData.RearDownforce = playerTelemetry.mRearDownforce;

                cgs.TelemetryData.FrontLeftData.SuspensionDeflection = wheelFrontLeft.mSuspensionDeflection;
                cgs.TelemetryData.FrontRightData.SuspensionDeflection = wheelFrontRight.mSuspensionDeflection;
                cgs.TelemetryData.RearLeftData.SuspensionDeflection = wheelRearLeft.mSuspensionDeflection;
                cgs.TelemetryData.RearRightData.SuspensionDeflection = wheelRearRight.mSuspensionDeflection;

                cgs.TelemetryData.FrontLeftData.RideHeight = wheelFrontLeft.mRideHeight;
                cgs.TelemetryData.FrontRightData.RideHeight = wheelFrontRight.mRideHeight;
                cgs.TelemetryData.RearLeftData.RideHeight = wheelRearLeft.mRideHeight;
                cgs.TelemetryData.RearRightData.RideHeight = wheelRearRight.mRideHeight;
#endif
            }

            return cgs;
        }

        // Weird stuff:
        // * I saw some pace notes before detail_start at some tracks.  Those also have weird flags on them, maybe worth ignoring all pre-start entries?
        // * Distance call calculation ignores Care/Caution possibly more stuff.  Needs polishing.
        private void LoadPacenotes(RBRSharedMemoryReader.RBRStructWrapper shared, GameStateData cgs)
        {
            Debug.Assert(!this.pacenotesLoaded);
            this.pacenotesLoaded = true;
            int undefinedPacenoteTypesCount = 0;
            var numPacenotes = shared.coDriver.mRBRPacenoteInfo.numPacenotes;
            if (numPacenotes == 0)
            {
                Console.WriteLine("LoadPacenotes: No pacenotes found");
                return;
            }

            for (int i = 0; i < numPacenotes; ++i)
            {
                var distance = shared.coDriver.mPacenotes[i].distance;
                var pacenote = (CoDriver.PacenoteType)shared.coDriver.mPacenotes[i].type;

                if (!Enum.IsDefined(typeof(CoDriver.PacenoteType), pacenote))
                {
                    undefinedPacenoteTypesCount++;
#if DEBUG
                    Console.WriteLine($"LoadPacenotes: WARNING: unknown pacenote type at: {i}  type: {shared.coDriver.mPacenotes[i].type}  flags: {shared.coDriver.mPacenotes[i].flags}  distance: {distance.ToString("0.000")}.  Skipping.");
#endif  // DEBUG
                    continue;
                }

                // Remove modifier flags that are not yet interesting.
                var flagsSanitized = shared.coDriver.mPacenotes[i].flags;

                flagsSanitized = RBRGameStateMapper.SanitizePacenoteModifier(flagsSanitized);


                if ((shared.coDriver.mPacenotes[i].flags & 256) != 0)
                {
                    // I don't get WTF 256 is.  But all 'into' calls seem to be ignored.
                    flagsSanitized &= ~256;
                    if (pacenote == CoDriver.PacenoteType.detail_into)
                    {
#if DEBUG
                        Console.WriteLine($"LoadPacenotes: WARNING: detected modifier 256 on pacenote at: {i}  {pacenote}  flags: {shared.coDriver.mPacenotes[i].flags}  distance: {distance.ToString("0.000")}.  Skipping.");
#endif  // DEBUG
                        continue;
                    }
#if DEBUG
                    else
                        Console.WriteLine($"LoadPacenotes: WARNING: removed modifier 256 on pacenote at: {i}  {pacenote}  flags: {shared.coDriver.mPacenotes[i].flags}  distance: {distance.ToString("0.000")}.");
#endif  // DEBUG
                }


                var modifier = (CoDriver.PacenoteModifier)flagsSanitized;

                object options = null;

                
                // Make sure we don't miss modifiers.  Validate the sum of known flags matches the passed in value.
                var flagsSum = 0;
                if (modifier != CoDriver.PacenoteModifier.none)
                {
                    foreach (var mod in Utilities.GetEnumFlags(modifier))
                        flagsSum += (int)((CoDriver.PacenoteModifier)mod);
                }

                // TODO: Investigate modifer 256 at harwood forest 2
#if DEBUG
                if (flagsSum != (int)modifier)
                    Console.WriteLine($"LoadPacenotes: WARNING: unknown pacenote modifiers at: {i}  {pacenote}  flags: {shared.coDriver.mPacenotes[i].flags}  distance: {distance.ToString("0.000")}.");
#endif  // DEBUG
                // Skip pacenotes that are no longer relevant (mid-session connect with CC started after countdown).
                if (cgs.SessionData.SessionPhase == SessionPhase.Green
                    && (cgs.PositionAndMotionData.DistanceRoundTrack + 4.0f * cgs.PositionAndMotionData.CarSpeed > distance))
                {
#if DEBUG
                    Console.WriteLine($"LoadPacenotes: WARNING: mid-session load detected.  Skipping call at: {i}.");
#endif  // DEBUG
                    continue;
                }

                // Skip distance calls below 26m (30m rounded).
                if (pacenote == CoDriver.PacenoteType.detail_distance_call)
                {
                    if (i + 1 >= numPacenotes)
                    {
#if DEBUG
                        Console.WriteLine($"LoadPacenotes: no more elements after distance call at: {i}.  Skipping.");
#endif  // DEBUG
                        continue;
                    }

                    
                    var lookAheadIdx = i + 1;
                    var nextDist = 0.0;
                    while (lookAheadIdx < numPacenotes)
                    {
                        var typeAhead = shared.coDriver.mPacenotes[lookAheadIdx].type;
                        
                        // Ignore those, list is incomplete probably.
                        if (typeAhead == (int)CoDriver.PacenoteType.detail_care
                            || typeAhead == (int)CoDriver.PacenoteType.detail_caution
                            || typeAhead == (int)CoDriver.PacenoteType.detail_keep_centre
                            || typeAhead == (int)CoDriver.PacenoteType.detail_keep_left
                            || typeAhead == (int)CoDriver.PacenoteType.detail_keep_right
                            || typeAhead == (int)CoDriver.PacenoteType.detail_keep_middle
                            || typeAhead == (int)CoDriver.PacenoteType.detail_keep_out
                            || typeAhead == (int)CoDriver.PacenoteType.detail_keep_in)
                        {
#if DEBUG
                            Console.WriteLine($"LoadPacenotes: skipping element: {(CoDriver.PacenoteType)typeAhead} in distance call calculation at: {i}.");
#endif  // DEBUG
                            ++lookAheadIdx;
                            continue;
                        }
                        else if (!CoDriver.terminologies.chainedNotes.Contains(((CoDriver.PacenoteType)typeAhead).ToString()))
                        {
#if DEBUG
                            Console.WriteLine($"LoadPacenotes: skipping element: {(CoDriver.PacenoteType)typeAhead} in distance call calculation  at: {i}. Not in a list of chained types.");
#endif  // DEBUG
                            ++lookAheadIdx;
                            continue;
                        }

                        nextDist = shared.coDriver.mPacenotes[lookAheadIdx].distance;
                        break;
                    }

                    var distanceToNext = nextDist != 0.0
                        ? nextDist - distance 
                        : -1.0;

                    if (distanceToNext < 25.0f || distanceToNext > 1050.0f)
                    {
#if DEBUG
                        Console.WriteLine($"LoadPacenotes: unable to process distance call at: {i} distance to next: {distanceToNext.ToString("0.000")}.  Skipping.");
#endif  // DEBUG
                        continue;
                    }

                    options = CoDriver.GetClosestValueForDistanceCall(distanceToNext);
                }

                if (pacenote == CoDriver.PacenoteType.detail_finish)
                {
                    this.finishDistance = distance;
#if DEBUG
                    Console.WriteLine($"LoadPacenotes: finish distance: {distance.ToString("0.000")}");
#endif  // DEBUG
                }
#if DEBUG
                Console.WriteLine($"LoadPacenotes: pacenote loaded at: {i}      {distance.ToString("0.000")},       {pacenote},     { (options == null ? "null" : options.ToString()) },      [{modifier}]");
#endif  // DEBUG
                cgs.CoDriverPacenotes.Add(new CoDriverPacenote() {
                    Distance = distance,
                    Pacenote = pacenote,
                    Options = options,
                    Modifier = modifier
                });
            }
            Console.WriteLine("LoadPacenotes: loaded " + cgs.CoDriverPacenotes.Count + " pacenotes with " + undefinedPacenoteTypesCount + " unrecognised pacenotes");
        }

        public SessionType MapToSessionType(object wrapper)
        {
            var shared = wrapper as RBRSharedMemoryReader.RBRStructWrapper;
            if ((GameMode)shared.perFrame.mRBRGameMode.gameMode == GameMode.Driving
                || (GameMode)shared.perFrame.mRBRGameMode.gameMode == GameMode.Paused
                || (GameMode)shared.perFrame.mRBRGameMode.gameMode == GameMode.SessionEnd
                || (GameMode)shared.perFrame.mRBRGameMode.gameMode == GameMode.Replay)
                return SessionType.Race;

            return SessionType.Unavailable;
        }

        private SessionPhase MapToSessionPhase(
            object wrapper,
            SessionType sessionType)
        {
            var shared = wrapper as RBRSharedMemoryReader.RBRStructWrapper;
            if (sessionType == SessionType.Race)
            {
                if (shared.perFrame.mRBRCarInfo.stageStartCountdown > 0.0f)
                    return SessionPhase.Countdown;
                else if (shared.perFrame.mRBRCarInfo.raceFinished == 1
                    || (GameMode)shared.perFrame.mRBRGameMode.gameMode == GameMode.SessionEnd)
                {
                    if (this.finishDistance > 0.0f)
                        return SessionPhase.Finished;
                    else
                        return SessionPhase.Unavailable;
                }
                else
                    return SessionPhase.Green;
            }

            return SessionPhase.Unavailable;
        }

        public static string GetStringFromBytes(byte[] bytes)
        {
            var nullIdx = Array.IndexOf(bytes, (byte)0);

            return nullIdx >= 0
              ? Encoding.Default.GetString(bytes, 0, nullIdx)
              : Encoding.Default.GetString(bytes);
        }

        // Quick implementation, do not call on every frame, need to find faster way of getting rid of empty '\0'.
        public static string GetWideStringFromBytes(byte[] bytes)
        {
            var str = Encoding.Unicode.GetString(bytes);
            return str.Substring(0, str.IndexOf('\0'));
        }
    }
}
