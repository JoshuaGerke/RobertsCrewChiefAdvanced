using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using CrewChiefV4.LMU;

using Newtonsoft.Json;

using PitMenuAPI;


namespace CrewChiefV4.PitManager
{
    public partial class PitManagerEventHandlers_LMU // Also contains PM_event_dict_LMU in PitManager\PitManagerEventHandlersTable_LMU.cs
                                                     // public for unit testing
    {
        #region Public struct
#pragma warning disable S3925 // "ISerializable" should be implemented correctly
        public class TyreDictionary : Dictionary<string, List<string>>
#pragma warning restore S3925 // "ISerializable" should be implemented correctly
        {
            public TyreDictionary()
            {
                TyreTranslationDict = new Dictionary<string, List<string>>();
            }
            public Dictionary<string, List<string>> TyreTranslationDict
            {
                get; set;
            }
        }
        #endregion Public struct

        #region Private field made Public for unit testing
        public static readonly IPitMenuAbstractionLayer Pmal = new LMUPitMenuAbstractionLayer();

        // Complicated because rF2 has many names for tyres so use a dict of
        // possible alternative names for each type
        // Each entry has a list of possible matches in declining order
        // This is the default dict which is loaded into MyDocuments\CrewChiefV4\rF2\TyreDictionary.json
        // The user can edit that file to add new names if required
        public static readonly TyreDictionary SampleTyreTranslationDict =
          new TyreDictionary() {
            { "Hypersoft",    new List <string> {"soft cold", "hypersoft", "c1", "ultrasoft", "supersoft", "soft", "s7m - soft", "alternates",
                        "s310", "slick", "dry", "race", "allweather", "medium" } },
            { "Ultrasoft",    new List <string> {"soft cold", "ultrasoft","c1", "hypersoft", "supersoft", "soft", "s7m - soft", "alternates",
                        "s310", "slick", "dry", "race", "allweather", "medium" } },
            { "Supersoft",    new List <string> {"soft cold", "supersoft", "c2", "hypersoft", "ultrasoft", "soft", "s7m - soft", "alternates",
                        "s310", "slick", "dry", "race", "allweather", "medium" } },
            { "Soft",         new List <string> {"soft hot", "soft", "c3", "s7m - soft", "alternates",
                        "s310", "slick", "dry", "race", "allweather", "medium" } },
            { "Medium",       new List <string> { "medium", "c4", "s8m - medium", "default",
                        "s310", "slick", "dry", "race", "allweather" } },
            { "Hard",         new List <string> {"hard", "c5", "p310", "s9m - hard", "endur", "primary",
                        "medium", "default",
                                "slick", "dry", "race", "allweather" } },
            { "Intermediate", new List <string> { "intermediate", "inter", "inters",
                        "wet", "rain", "monsoon", "allweather" } },
            { "Wet",          new List <string> {
                        "wet", "rain", "monsoon", "pr2m - wet", "allweather", "intermediate", "inter", "inters" } },
            { "Monsoon",      new List <string> {"monsoon",
                        "wet", "rain",  "allweather", "intermediate", "inter", "inters" } },
            { "No Change",    new List <string> {"no change"} }
            };
        #endregion Private field made Public for unit testing

        #region Private Fields
        private static readonly TyreDictionary tyreTranslationDict =
                    TyreDictFile.getTyreDictionaryFromFile();

        private static readonly CurrentLMUTyreType currentLMUTyreType = new CurrentLMUTyreType();

        #endregion Private Fields

        #region Public Methods

        // Pit stop texts
        // R WING:
        // REAR DF:
        // DRIVER:
        // R SPOILER:
        // F WING:
        // FRONT DF:
        // F AIR DAM:
        // F SPLITTER:
        // L FENDER:
        // L FLIP UP:
        // R FENDER:
        // R FLIP UP:
        // FR TIRE:
        // FL TIRE:
        // RR TIRE:
        // RL TIRE:
        // R TIRES:
        // F TIRES:
        // RT TIRES:
        // LF TIRES:
        // FUEL RATIO:
        // PITSTOP:
        // TIRES:
        // VIRTUAL ENERGY:
        // TYPE:
        // STOP/GO:
        // REPLACE BRAKES:
        // RR RUBBER:
        // PITSTOPS:
        // DAMAGE:
        // FL RUBBER:
        // RR PRESS:
        // RL RUBBER:
        // FR RUBBER:
        // FL PRESS:
        // TRACK BAR:
        // RL PRESS:
        // FR PRESS:
        // GRILLE:
        // WEDGE:
        // OIL RADIATOR:
        // RADIATOR:

        /// <summary>
        /// Take a list of tyre types available in the menu and map them on to
        /// the set of cc tyre types
        /// Hypersoft
        /// Ultrasoft
        /// Supersoft
        /// Soft
        /// Medium
        /// Hard
        /// Intermediate
        /// Wet
        /// Monsoon
        /// (No Change) for completeness
        ///
        /// Algorithm:
        /// Check the first list item for each key in tyreDict
        /// if the word is in inMenu then that key is DONE
        /// if not, check the 2nd list item
        /// If there are no exact matches in the whole dictionary then see if
        /// one of the tyre dict values is a sub-string of one of the game's entries
        /// e.g. "soft" in "Soft COMPOUND"
        /// </summary>
        /// <param name="tyreDict">
        /// The dict used for translation
        /// </param>
        /// <param name="inMenu">
        /// The list returned by GetTyreTypes()
        /// </param>
        /// <returns>
        /// Dictionary mapping CC tyre types to names of those available
        /// </returns>
        public static Dictionary<string, string> TranslateTyreTypes(
          TyreDictionary tyreDict,
          List<string> inMenu)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            inMenu.Remove("No Change");
            int columnCount = 1; // will increase

            for (var run = 0; run < 2; run++)
            {   // run = 0, exact match; run = 1, any matching word
                for (var col = 0; col < columnCount; col++)
                {
                    foreach (var ccTyreType in tyreDict)
                    { // "Hypersoft", "Ultrasoft", "Supersoft", "Soft"...
                        if (!result.ContainsKey(ccTyreType.Key))
                        { // Didn't match in run 0
                            foreach (var LMUTyreType in inMenu)
                            {  // Tyre type in the menu
                                if (ccTyreType.Value.Count > columnCount)
                                {
                                    columnCount = ccTyreType.Value.Count;
                                }
                                if (col < ccTyreType.Value.Count)
                                {
                                    var dictTyreName = ccTyreType.Value[col];
                                    // Normalise the LMU tyre type name by removing spaces and -
                                    var normalisedLMUTyreType = Regex.Replace(LMUTyreType, " |-|_", "");
                                    if (run == 0)
                                    {
                                        if ((LMUTyreType.Length == dictTyreName.Length &&
                                            LMUTyreType.IndexOf(dictTyreName, StringComparison.OrdinalIgnoreCase) >= 0) // exact match
                                            || (normalisedLMUTyreType.Length == dictTyreName.Length &&
                                            normalisedLMUTyreType.IndexOf(dictTyreName, StringComparison.OrdinalIgnoreCase) >= 0)) // normalised match
                                        {
#pragma warning disable S1066
                                            if (!result.ContainsKey(ccTyreType.Key))
                                            {
                                                result[ccTyreType.Key] = LMUTyreType;
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Didn't find an exact match, see if dictTyreName
                                        // is in one of the menu items, e.g. "soft" in "Soft COMPOUND"
                                        if (normalisedLMUTyreType.IndexOf(dictTyreName, StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            // (Already checked that it's not in result)
                                            result[ccTyreType.Key] = LMUTyreType;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            foreach (var ccTyretype in tyreDict)
            {
                if (!result.ContainsKey(ccTyretype.Key))
                {
                    // Still didn't match, give it something
                    result[ccTyretype.Key] = inMenu[0];
                }
            }
            return result;
        }

        ///////////////////////////////////////////////////////////////////////
        // Event handlers

        private static bool EH_initialise(string __)
        {
            bool result = Pmal.Connect();
            if (result)
            {
                List<string> tyreTypeNames = Pmal.GetTyreTypeNames();
                currentLMUTyreType.Set(tyreTypeNames[0]);
                Log.Commentary("Pit Manager initialise");
            }
            return result;
        }
        private static bool EH_teardown(string __)
        {
            Pmal.Disconnect();
            return true;
        }

        /// <summary>
        /// Switch LMU to the menu page and wake up the inputs driver
        /// </summary>
        private static bool EH_prepareToUseMenu(string __)
        {
            LMUPitMenuAPI pm = new LMUPitMenuAPI();
            pm.startUsingPitMenu();
            return true;
        }

        /// <summary> EH_example
        /// Dummy action handler
        /// </summary>
        private static bool EH_example(string __)
        {
            return true;
        }

        #region Tyre compounds
        private static bool EH_TyreCompoundDry(string __)
        {
            return setTyreCompound("Dry");
        }

        private static bool EH_TyreCompoundHard(string __)
        {
            return setTyreCompound("Hard");
        }

        private static bool EH_TyreCompoundMedium(string __)
        {
            return setTyreCompound("Medium");
        }

        private static bool EH_TyreCompoundSoft(string __)
        {
            return setTyreCompound("Soft");
        }

        private static bool EH_TyreCompoundUsedHard(string __)
        {
            return setTyreCompound("Used Hard");
        }

        private static bool EH_TyreCompoundUsedMedium(string __)
        {
            return setTyreCompound("Used Medium");
        }

        private static bool EH_TyreCompoundUsedSoft(string __)
        {
            return setTyreCompound("Used Soft");
        }

        private static bool EH_TyreCompoundSupersoft(string __)
        {
            return setTyreCompound("Supersoft");
        }

        private static bool EH_TyreCompoundUltrasoft(string __)
        {
            return setTyreCompound("Ultrasoft");
        }

        private static bool EH_TyreCompoundHypersoft(string __)
        {
            return setTyreCompound("Hypersoft");
        }

        private static bool EH_TyreCompoundIntermediate(string __)
        {
            return setTyreCompound("Intermediate");
        }

        private static bool EH_TyreCompoundWet(string __)
        {
            return setTyreCompound("Wet");
        }

        private static bool EH_TyreCompoundMonsoon(string __)
        {
            return setTyreCompound("Monsoon");
        }

        private static bool EH_TyreCompoundOption(string __)
        {
            return setTyreCompound("Soft");
        }

        private static bool EH_TyreCompoundPrime(string __)
        {
            return setTyreCompound("Hard");
        }

        private static bool EH_TyreCompoundAlternate(string __)
        {
            return setTyreCompound("Soft");
        }

        private static bool EH_TyreCompoundNext(string __)
        {
            // Select the next compound available for this car
            // Get the current tyre type
            // Get the list of tyre type, remove "No Change"
            List<string> tyreTypes = Pmal.GetTyreTypeNames();
            tyreTypes.Remove("No Change");
            string currentTyreTypeStr = Pmal.GetCurrentTyreType();

            int currentTyreTypeIndex = tyreTypes.IndexOf(currentTyreTypeStr) + 1;
            if (currentTyreTypeIndex >= tyreTypes.Count)
            {
                currentTyreTypeIndex = 0;
            }
            currentLMUTyreType.Set(tyreTypes[currentTyreTypeIndex]);
            return EH_changeAllTyres(null);
        }
        #endregion Tyre compounds

        #region Which tyres to change
        private static bool EH_changeAllTyres(string __)
        {
            return changeTyres(Pmal.GetAllTyreCategories());
        }

        private static bool EH_changeNoTyres(string __)
        {
            return changeTyres(Pmal.GetAllTyreCategories(), true);
        }

        private static bool EH_changeFrontTyres(string __)
        {
            changeTyres(Pmal.GetAllTyreCategories(), true);
            return changeTyres(Pmal.GetFrontTyreCategories());
        }

        private static bool EH_changeRearTyres(string __)
        {
            changeTyres(Pmal.GetAllTyreCategories(), true);
            return changeTyres(Pmal.GetRearTyreCategories());
        }

        private static bool EH_changeLeftTyres(string __)
        {
            changeTyres(Pmal.GetAllTyreCategories(), true);
            return changeTyres(Pmal.GetLeftTyreCategories());
        }

        private static bool EH_changeRightTyres(string __)
        {
            changeTyres(Pmal.GetAllTyreCategories(), true);
            return changeTyres(Pmal.GetRightTyreCategories());
        }

        private static bool EH_changeFLTyre(string __)
        {
            return changeTyre("FL TIRE:");
        }

        private static bool EH_changeFRTyre(string __)
        {
            return changeTyre("FR TIRE:");
        }

        private static bool EH_changeRLTyre(string __)
        {
            return changeTyre("RL TIRE:");
        }

        private static bool EH_changeRRTyre(string __)
        {
            return changeTyre("RR TIRE:");
        }
        #endregion Which tyres to change

        #region Tyre pressures
        private static bool EH_changeFrontPressure(string voiceMessage)
        {
            if (changeTyrePressure("FL PRESS:", voiceMessage))
            {
                return changeTyrePressure("FR PRESS:", voiceMessage);
            }
            return false;
        }
        private static bool EH_changeRearPressure(string voiceMessage)
        {
            if (changeTyrePressure("RL PRESS:", voiceMessage))
            {
                return changeTyrePressure("RR PRESS:", voiceMessage);
            }
            return false;
        }
        private static bool EH_changeLFpressure(string voiceMessage)
        {
            return changeTyrePressure("FL PRESS:", voiceMessage);
        }
        private static bool EH_changeRFpressure(string voiceMessage)
        {
            return changeTyrePressure("FR PRESS:", voiceMessage);
        }
        private static bool EH_changeLRpressure(string voiceMessage)
        {
            return changeTyrePressure("RL PRESS:", voiceMessage);
        }
        private static bool EH_changeRRpressure(string voiceMessage)
        {
            return changeTyrePressure("RR PRESS:", voiceMessage);
        }
        private static bool EH_changePressure(string voiceMessage)
        {
            if (changeTyrePressure("FL PRESS:", voiceMessage))
            {
                if (changeTyrePressure("FR PRESS:", voiceMessage))
                {
                    if (changeTyrePressure("RL PRESS:", voiceMessage))
                    {
                        return changeTyrePressure("RR PRESS:", voiceMessage);
                    }
                }
            }
            return false;
        }

        #endregion Tyre pressures

        #region Fuel
        // Fuel Add:
        // If "Relative Fuel Strategy" set menu to X litres else set to X+current
        // X' = X+current
        // Fuel To:
        // If "Relative Fuel Strategy" set menu to X litres-current else set to X
        // X' = X
        //
        // in SetFuel(X')
        // If "Relative Fuel Strategy" set to X'-current else set to X'
        private static bool FuelAddXlitres(string voiceMessage, int current)
        {
            var amount = PitNumberHandling.processNumber(voiceMessage);
            amount = PitNumberHandling.processLitresGallons(amount, voiceMessage);
            if (amount == 0)
            {
                return false;
            }
            if (amount > PitManagerVoiceCmds.getFuelCapacity())
            {
                amount = (int)PitManagerVoiceCmds.getFuelCapacity();
            }
            FuelVoiceCommand.Given = true;
            FuelVoiceCommand.Added = amount;
            return SetFuel(amount + current);
        }
        private static bool EH_FuelAddXlitres(string voiceMessage)
        {
            return FuelAddXlitres(voiceMessage, (int)PitManagerVoiceCmds.getCurrentFuel());
        }
        private static bool EH_FuelToXlitres(string voiceMessage)
        {
            return FuelAddXlitres(voiceMessage, 0);
        }
        private static bool EH_FuelRatio(string voiceMessage)
        {
            FuelVoiceCommand.Given = true; 
            var amount = PitNumberHandling.processNumber(voiceMessage);
            var max = Pmal.GetMaxFuelRatio();
            amount = Math.Min(amount, 110); // REST API will allow higher values, UI doesn't
            FuelVoiceCommand.Given = true;
            return Pmal.SetFuelRatio(amount);
        }

        private static bool EH_VirtualEnergy(string voiceMessage)
        {
            FuelVoiceCommand.Given = true; 
            var amount = PitNumberHandling.processNumber(voiceMessage);
            FuelVoiceCommand.Given = true;
            return Pmal.SetVirtualEnergy(amount);
        }

        public static bool EH_FuelToEnd(string __)
        {
            var fuelRatio = Pmal.GetFuelRatio();
            if (fuelRatio > 0)
            {
                // Max range with virtual energy is the lesser of
                // the fuel capacity
                // 100% virtual energy
                var _fuelToEnd = PitFuelling.fuelToEnd(
                    PitManagerVoiceCmds.getFuelCapacity(),
                    PitManagerVoiceCmds.getCurrentFuel(),
                    fuelRatio * 100);
                MainWindow.instance.crewChief.audioPlayer.playMessage(_fuelToEnd.message);
                if (_fuelToEnd.litresNeeded < 0)
                {
                    return true; // Couldn't calculate
                }
                FuelVoiceCommand.Given = true;
                return Pmal.SetVirtualEnergy((int)(_fuelToEnd.litresNeeded / fuelRatio));
            }
            else
            {
                var _fuelToEnd = PitFuelling.fuelToEnd(
                    PitManagerVoiceCmds.getFuelCapacity(),
                    PitManagerVoiceCmds.getCurrentFuel());
                MainWindow.instance.crewChief.audioPlayer.playMessage(_fuelToEnd.message);
                if (_fuelToEnd.litresNeeded < 0)
                {
                    return true;    // Couldn't calculate
                }
                FuelVoiceCommand.Given = true;
                return SetFuel(_fuelToEnd.litresNeeded);
            }
        }

        private static bool EH_FuelNone(string __)
        {
            FuelVoiceCommand.Given = true;
            var fuelRatio = Pmal.GetFuelRatio();
            if (fuelRatio > 0)
            {
                return Pmal.SetVirtualEnergy(1);
            }
            else
            {
                return SetFuel(0);
            }
        }

        public static bool SetFuel(int amount)
        {
            if (Pmal.RelativeFuelStrategy())
            {
                amount = Math.Max(amount - (int)PitManagerVoiceCmds.getCurrentFuel(), 0);
            }
            return Pmal.SetFuelLevel(amount);
        }
        #endregion Fuel

        #region Repairs
        private static bool EH_RepairAll(string __)
    {
        if (!Pmal.SoftMatchCategory("DAMAGE:"))
        {
            Pmal.RereadPitMenu();   // DAMAGE is not in initial menu, check if it is now
        }
        if (Pmal.SoftMatchCategory("DAMAGE:"))
        {
            return Pmal.SetChoice("Repair All");
        }
        return false;
    }

    private static bool EH_RepairNone(string __)
    {
        if (!Pmal.SoftMatchCategory("DAMAGE:"))
        {
            Pmal.RereadPitMenu();   // DAMAGE is not in initial menu, check if it is now
        }
        if (Pmal.SoftMatchCategory("DAMAGE:"))
        {
            return Pmal.SetChoice("Do Not Repair");
        }
        return false;
    }

    private static bool EH_RepairBody(string __)
    {
        if (!Pmal.SoftMatchCategory("DAMAGE:"))
        {
            Pmal.RereadPitMenu();   // DAMAGE is not in initial menu, check if it is now
        }
        if (Pmal.SoftMatchCategory("DAMAGE:"))
        {
            return Pmal.SetChoice("Repair Body");
        }
        return false;
    }
    #endregion Repairs
        #region Penalties
        private static bool EH_PenaltyServe(string __)
        {
            if (!Pmal.GetCategories().Contains("STOP/GO"))
            {
                Pmal.RereadPitMenu();   // STOP/GO is not in initial menu, check if it is now
            }
            if (Pmal.GetCategories().Contains("STOP/GO"))
            {
                return Pmal.SetChoice("YES");
            }
            return false;
        }

        private static bool EH_PenaltyServeNone(string __)
        {
            if (!Pmal.GetCategories().Contains("STOP/GO"))
            {
                Pmal.RereadPitMenu();   // STOP/GO is not in initial menu, check if it is now
            }
            if (Pmal.GetCategories().Contains("STOP/GO"))
            {
                return Pmal.SetChoice("NO");
            }
            return false;
        }
        #endregion Penalties

        private static bool EH_ClearAll(string __)
        {
            if (EH_FuelNone(null) &&
                EH_changeNoTyres(null))
            {
                Pmal.RereadPitMenu();   // STOP/GO or DAMAGE: is not in initial menu, check if it is now
                var categories = Pmal.GetCategories();
                if (categories.Contains("STOP/GO"))
                {
                    EH_PenaltyServeNone(null);
                }
                if (categories.Contains("DAMAGE:"))
                {
                    EH_RepairNone(null);
                }
            }
            return true;
        }


        #endregion Public Methods

        #region MFD
        static string currentMFD = "MFDF";
        private static bool changeMFD(string mfd)
        {
            if (Pmal.MfdPage(mfd))
            {
                currentMFD = mfd;
                Log.Commentary($"Displaying {currentMFD}");
                return true;
            }
            return false;
        }
        private static bool EH_DisplaySectors(string __)
        {
            return changeMFD("MFDA");
        }
        private static bool EH_DisplayPitMenu(string __)
        {
            return changeMFD("MFDB");
        }
        private static bool EH_DisplayTyres(string __)
        {
            return changeMFD("MFDC");
        }
        // MFDD is Driving Aids, not worth a command
        private static bool EH_DisplayTemps(string __)
        {
            return changeMFD("MFDE");
        }
        private static bool EH_DisplayRaceInfo(string __)
        {
            return changeMFD("MFDF");
        }
        private static bool EH_DisplayStandings(string __)
        {
            return changeMFD("MFDG");
        }
        private static bool EH_DisplayPenalties(string __)
        {
            return changeMFD("MFDH");
        }
        /// <summary>
        /// Display the next MFD screen
        /// </summary>
        private static bool EH_DisplayNext(string __)
        {
            var lastLetter = currentMFD[currentMFD.Length - 1] + 1;
            if (lastLetter == 'I')
            {
                lastLetter = 'A';
            }
            return changeMFD("MFD" + (char)lastLetter);
        }
        #endregion MFD

        #region Private Methods

        /// <summary>
        /// Set the current tyre compound and fit them
        /// </summary>
        /// <param name="ccTyreType">Soft / Medium / Wet etc.</param>
        /// <returns></returns>
        private static bool setTyreCompound(string ccTyreType)
        {
            var inMenu = Pmal.GetTyreTypeNames();
            var result = TranslateTyreTypes(tyreTranslationDict, inMenu);
            if (!result.ContainsKey(ccTyreType))
            {   // Didn't find a match
                PitManagerResponseHandlers.PMrh_TyreCompoundNotAvailable();
                return false;
            }

            currentLMUTyreType.Set(result[ccTyreType]);
            Log.Commentary($"Fitting {result[ccTyreType]}");
            return EH_changeAllTyres(null);
        }

        /// <summary>
        /// Change a single tyre to the current compound
        /// </summary>
        /// <param name="tyreCategory"></param>
        /// <param name="noChange">Set it to "No change"</param>
        /// <returns>true => success</returns>
        private static bool changeTyre(string tyreCategory, bool noChange = false)
        {
            bool response = false;
            string tyreType = noChange ? "No Change" : currentLMUTyreType.Get();

            if (Pmal.GetAllTyreCategories().Contains(tyreCategory))
            {
                response = Pmal.SetCategoryAndChoice(tyreCategory, tyreType);
                if (response)
                {
                    // dict is the other direction currentccTyreCompound = ttDict[tyreType];
                    if (CrewChief.Debug.RunningUnderDebugger)
                    {
                        Log.Info(() => "Pit Manager tyre compound set to (" +
                            tyreCategory + ") " + tyreType);
                    }
                }
                else
                {   // Compound is not available
                    PitManagerResponseHandlers.PMrh_TyreCompoundNotAvailable();
                }
            }
            else
            {   // Category is not available
                PitManagerResponseHandlers.PMrh_CantChangeThose();
            }
            return response;
        }

        /// <summary>
        /// Change a set of tyres to the current compound
        /// </summary>
        /// <param name="tyreCategories"></param>
        /// <param name="noChange">Set them to "No Change"</param>
        /// <returns>true => success</returns>
        private static bool changeTyres(List<string> tyreCategories, bool noChange = false)
        {
            bool result = true;
            foreach (string tyreCategory in tyreCategories)
            {
                if (result && Pmal.SmartSetCategory(tyreCategory))
                {
                    result = changeTyre(tyreCategory, noChange);
                }
            }
            return result;
        }

        private static bool changeTyrePressure(string tyreCategory, string voiceMessage)
        {
            bool response = false;

            float pressure = NumberProcessing.SpokenNumberParser.Parse(voiceMessage);
            if (pressure > 0)
            {
                // If metric units "FL PRESS: 135", Imperial units have "FL PRESS: 19.6"
                Pmal.SmartSetCategory(tyreCategory);
                var choice = Pmal.GetChoice();
                if (choice != null)
                {
                    if (!choice.Contains(".") && pressure < 100)
                    { // command is like "1.45 bar"
                        pressure *= 100;
                    }
                }
                response = Pmal.SetCategoryAndChoice(tyreCategory, pressure.ToString());
                if (response)
                {
                    if (CrewChief.Debug.RunningUnderDebugger)
                    {
                        Log.Commentary("Pit Manager tyre pressure set to (" +
                            tyreCategory + ") " + pressure);
                    }
                }
                else
                {   // tbd: what failed? tyreCategory not available?
                    PitManagerResponseHandlers.PMrh_CantDoThat(); //tbd
                }
            }
            return response;
        }

        #endregion Private Methods

        #region Private Classes

        private class CurrentLMUTyreType
        {
            #region Private Fields

            private string currentTyreType = "No Change";

            #endregion Private Fields

            #region Public Methods

            public void Set(string tyreType)
            {
                currentTyreType = tyreType;
            }

            public string Get()
            {
                if (currentTyreType == "No Change")
                {
                    currentTyreType = Pmal.GetCurrentTyreType();
                    if (currentTyreType == "No Change")
                    {
                        // tbd: _currentTyreType = TranslateTyreTypes(tyreTranslationDict, tyreTypes)["Medium"];
                    }
                }
                return currentTyreType;
            }

            #endregion Public Methods
        }

        #endregion Private Classes

        private static class TyreDictFile
        {
            private static Tuple<string,  string> getUserTyreDictionaryFileLocation()
            {
                var path = DataFiles.LMUFolder;

                return new Tuple<string, string>(path, "TyreDictionary.json");
            }
            public static TyreDictionary getTyreDictionaryFromFile()
            {
                var filepath = Path.Combine(getUserTyreDictionaryFileLocation().Item1,
                    getUserTyreDictionaryFileLocation().Item2);
                if (File.Exists(filepath))
                {
                    try
                    {
                        using (StreamReader r = new StreamReader(filepath))
                        {
                            string json = r.ReadToEnd();
                            // Check if file contents are the old version
                            int hash = json.GetHashCode();
                            if (hash != 0X37AF3AAF) // It's not
                            {
                                TyreDictionary data = JsonConvert.DeserializeObject<TyreDictionary>(json);
                                if (data != null)
                                {
                                    return data;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Error parsing {filepath}: {e.Message}");
                    }
                }
                // else
                // No file or file is an old version so create a default one
                saveTyreDictionaryFile(SampleTyreTranslationDict);
                return SampleTyreTranslationDict;
            }

            private static void saveTyreDictionaryFile(TyreDictionary tyreDict)
            {
                var path = getUserTyreDictionaryFileLocation().Item1;
                var fileName = getUserTyreDictionaryFileLocation().Item2;
                var filePath = Path.Combine(path, fileName);
                JsonFiles.WriteFile(filePath, tyreDict);
            }
        }
    }
}