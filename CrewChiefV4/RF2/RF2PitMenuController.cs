/*
Use HW controls (and reading the Pit Info buffer) to set the rFactor 2 Pit Menu
using TheIronWolf's rF2 Shared Memory Map plugin
https://github.com/TheIronWolfModding/rF2SharedMemoryMapPlugin

Author: Tony Whitley (crewchiefTony@outlook.com)
*/

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CrewChiefV4.Events;
using PitMenuAPI;

using static CrewChiefV4.LMU.LMUPitMenuAPI;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("UnitTest")]

namespace CrewChiefV4.RF2
{
    /// <summary>
    /// PitMenuAPI consists PitMenuAbstractionLayer : PitMenuController : PitMenu
    /// </summary>
    public class Rf2PitMenuController : RF2PitMenu, IPitMenuController
    {
        #region Public Fields

        public string[] genericTyreTypes = {
            "Supersoft",
            "Soft",
            "Medium",
            "Hard",
            "Intermediate",
            "Wet",
            "Monsoon"
    };

        #region Public Class

        /// <summary>
        /// A category in the Pit Menu with a list
        /// of the choices for each tyre compound category
        /// (the choices list for the other categories is empty)
        /// </summary>
        public struct PitMenuItem
        {
            public string category;
            public List<string> choices;
            public PitMenuItem(string _category)
            {
                category = _category;
                choices = new List<string>();
            }
        }

        /// <summary>
        /// A list of the categories in the Pit Menu
        /// </summary>
        public class PitMenuContents
        {
            internal List<PitMenuItem> items { get; set; } = new List<PitMenuItem>();
            public int Count { get {return items.Count;} }

            public List<string> Categories
            {
                get
                {
                    List<string> result = new List<string>();
                    foreach (PitMenuItem item in items)
                    {
                        result.Add(item.category);
                    }

                    return result;
                }
            }

            public bool Contains(string category)
            {
                return Categories.Contains(category);
            }

            public void Add(PitMenuItem item)
            {
                items.Add(item);
            }

            public List<string> GetChoices(string category)
            {
                List<string> result = new List<string>();
                foreach (PitMenuItem item in items)
                {
                    if (item.category == category)
                    {
                        result = item.choices;
                    }
                }

                return result;
            }

            public IEnumerator<PitMenuItem> GetEnumerator()
            {
                return items.GetEnumerator();
            }
        }

        #endregion Public Class
        #endregion Public Fields

        #region Private Fields

        /// <summary>
        /// List of all menu categories for the current vehicle
        /// The tyre categories have a list of all the tyre choices
        /// </summary>
        private PitMenuContents shadowPitMenu;
        private List<string> shadowPitMenuCats = new List<string> { };
        #endregion Private Fields

        private PitMenuItem getShadowPitMenuItem(string category)
        {
            PitMenuItem result = new PitMenuItem();
            result.category = null;
            if (shadowPitMenu.Categories.Contains(category))
            {
                foreach (var cat in shadowPitMenu)
                {
                    if (cat.category == category)
                    {
                        result = cat;
                        break;
                    }
                }
            }
            return result;
        }

        #region Public Methods

        /// <summary>
        /// Get a dictionary of all choices for all tyre/tire menu categories
        /// TIREs are a special case, we want all values. For the others we might
        /// want min and max.  It will take quite a time to find them though.
        /// </summary>
        /// <returns>
        /// Dictionary of all choices for all tyre/tire menu categories
        /// </returns>
        public PitMenuContents GetMenuContents()
        {
            shadowPitMenu = new PitMenuContents();
            shadowPitMenuCats = new List<string> { };
            string category;
            string choice;

            Log.Verbose("GetMenuDict");
            if (startUsingPitMenu())
            {
                PitMenuItem pitMenuItem;
                string newCategory;
                SetCategory("FUEL:");
                do
                {
                    category = GetCategory();
                    if (string.IsNullOrWhiteSpace(category))
                    {
                        break;
                    }
                    pitMenuItem = new PitMenuItem(category);
                    shadowPitMenuCats.Add(category);
                    if (category.Contains("TIRE"))
                    { // The only category that wraps round
                        do
                        {
                            choice = GetChoice();
                            pitMenuItem.choices.Add(choice);
                            nextChoice();
                        } while (!pitMenuItem.choices.Contains(GetChoice()));
                    }
                    shadowPitMenu.Add(pitMenuItem);
                    newCategory = CategoryDown();
                } while (!shadowPitMenuCats.Contains(newCategory));
            }

            if (shadowPitMenu.Count < 2)
            {
                if (UnitTest.UnitTest.Active && UnitTest.UnitTest.pitMenu.Count != 0)
                {
                    shadowPitMenu = UnitTest.UnitTest.pitMenu;
                }
                else
                {
                    // return empty so this will be called again
                shadowPitMenu = new PitMenuContents();
                }
            }
            return shadowPitMenu;
        }

        /// <summary>
        /// Keep banging away until the menu choice changes
        /// </summary>
        private void nextChoice()
        {
            string newChoice;
            string currentChoice = GetChoice();
            do
            {
                newChoice = ChoiceInc();
                if (newChoice == currentChoice)
                {
                    startUsingPitMenu();
                }
            }
            while (newChoice == currentChoice);
        }

        /// <summary>
        /// Take the shortest way to "category"
        /// </summary>
        /// <param name="category"> Pit Menu category</param>
        public bool SmartSetCategory(string category)
        {
            if (shadowPitMenuCats.Count == 0)
            {
#pragma warning disable S1066
                if (GetMenuContents().Count == 0)
                {
                    return false;
                }
            }
            if (!shadowPitMenu.Contains(category))
            {
                Log.DontSpam($"rF2 Pit menu doesn't have category '{category}'").Warning();
                return false;
            }
            string currentCategory = GetCategory();
            if (category != currentCategory)
            {
                startUsingPitMenu();
                int origin = Array.IndexOf(shadowPitMenuCats.ToArray(), currentCategory);
                int target = Array.IndexOf(shadowPitMenuCats.ToArray(), category);

                for (int i = 1; i < shadowPitMenuCats.Count; i++)
                {
                    if (((origin + i) % shadowPitMenuCats.Count) == target)
                    {
                        //down
                        while (GetCategory(true) != category)
                        {
                            CategoryDown();
                        }
                        break;
                    }
                    if (((shadowPitMenuCats.Count + origin - i) % shadowPitMenuCats.Count) == target)
                    {
                        //up
                        while (GetCategory(true) != category)
                        {
                            CategoryUp();
                        }
                        break;
                    }
                }
            }
            return GetCategory() == category;
        }

        //////////////////////////////////////////////////////////////////////////
        // Menu Choices

        //////////////////////////////////////////////////////////////////////////
        // Fuel

        /// <summary>
        /// Read the fuel level to find if "Relative Fuel Strategy" is selected
        /// in Player.JSON (if it is there is a + before the fuel quantity)
        /// </summary>
        /// <returns>
        /// true: "Relative Fuel Strategy" is selected
        /// </returns>
        public bool RelativeFuelStrategy()
        {
            bool relativeFuelStrategy = false;
            Match match;
            Regex reggie = new Regex(@"(.*)/(.*)");
            if (SmartSetCategory("FUEL:"))
            {
                match = reggie.Match(GetChoice());
                if (match.Groups.Count == 3)
                {
                    if (match.Groups[1].Value.StartsWith("+"))
                    {
                        relativeFuelStrategy = true;
                    }
                }
            }
            Log.Debug(() => $"Relative Fuel Strategy: {relativeFuelStrategy}");
            return relativeFuelStrategy;
        }

        /// <summary>
        /// Read the fuel level in the Pit Menu display
        /// Player.JSON "Relative Fuel Strategy" affects the display
        /// "+ 1.6/2"	Gallons to ADD/laps "Relative Fuel Strategy":TRUE,
        /// "65/25"		Litres TOTAL/laps   "Relative Fuel Strategy":FALSE,
        /// </summary>
        /// <returns>
        /// Fuel level in litres
        /// -1 if parsing the number failed
        /// </returns>
        public int GetFuelLevelSetting()
        {
            if (SmartSetCategory("FUEL:"))
            {
                return ParseFuelLevel(GetChoice());
            }
            return -1;
        }
        internal static int ParseFuelLevel(string fuelMenu)
        {
            int current = -1;

            // Regex for "62.0gal/6 laps"
            Regex regexWithDecimalAndUnit = new Regex(@"(\d+\.\d+)(gal|L)/(.*)");

            // Regex for "62L/6 laps"
            Regex regexWithUnit = new Regex(@"(\d+)(gal|L)/(.*)");

            // Regex for "62.0/6 laps"
            Regex regexWithDecimal = new Regex(@"(\d+\.\d+)/(.*)");

            // Regex for "62/6 laps"
            Regex regexWithoutUnit = new Regex(@"(\d+)/(.*)");

            Match match;

            // Try matching "62.0gal/6 laps" (LMU)
            match = regexWithDecimalAndUnit.Match(fuelMenu);
            if (!match.Success)
            {
                // Try matching "62L/6 laps" (LMU)
                match = regexWithUnit.Match(fuelMenu);
            }
            if (!match.Success)
            {
                // Try matching "62.0/6 laps" (rF2 gallons)
                match = regexWithDecimal.Match(fuelMenu);
            }
            if (!match.Success)
            {
                // Try matching "62/6 laps" (rF2 litres)
                match = regexWithoutUnit.Match(fuelMenu);
            }

            if (match.Success && match.Groups.Count >= 2)
            {
                bool parsed = float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float _current);
                if (parsed)
                {
                    if (match.Value.Contains("."))
                    {   // Gallons are displayed in 10ths
                        current = Utilities.Conversions.convertGallonsToLitres(_current);
                        Fuel.fuelReportsInGallons = true;
                        Log.Verbose("Using gallons");
                    }
                    else
                    {
                        current = (int)_current;
                        Fuel.fuelReportsInGallons = false;
                        Log.Verbose("Using litres");
                    }
                    Log.Verbose(() => $"Fuel level {current}");
                }
                else
                {
                    Log.Warning($"Couldn't parse fuel level '{fuelMenu}'");
                }
            }
            else
            {
                Log.Error($"Couldn't parse fuel level '{fuelMenu}'");
            }

            return current;
        }

        /// <summary>
        /// Set the fuel level in the Pit Menu display
        /// </summary>
        /// <param name="requiredFuel"> in litres (even if current units are (US?) gallons)</param>
        /// <returns>
        /// true if level set (or it reached max/min possible)
        /// false if the level can't be read
        /// </returns>
        public bool SetFuelLevel(int requiredFuel)
        {
            const int retries = 5;
            int tryNo = retries;
            string newChoice;

            if (!SmartSetCategory("FUEL:")) // (has its own logging)
            {   // Menu doesn't contain FUEL
                return false;
            }
            int current = GetFuelLevelSetting();   // (has its own logging)
            if (current < 0)
            {
                return false; // Can't read value
            }

            Log.Commentary($"Set fuel level: current {current}, required {requiredFuel} litres");

            // Adjust if necessary
            while (current != requiredFuel)
            {

                if (current > requiredFuel)
                {
                    newChoice = ChoiceDec();
                }
                else
                {
                    newChoice = ChoiceInc();
                }
                Log.Fuel(() => $"{newChoice}");
                int newLevel = GetFuelLevelSetting();
                if (newLevel == current)
                { // Can't adjust further
                    if (tryNo-- < 0)
                    {
                        Log.Warning("Can't adjust fuel further");
                        break;
                    }
                    Log.Verbose("Retry Pit Menu fuel adjust");
                    startUsingPitMenu();
                    SmartSetCategory("FUEL:");
                }
                else
                {
                    current = newLevel;
                    tryNo = retries;
                }
            }
            Log.Commentary($"Fuel level set: {GetFuelLevelSetting()} litres");
            return true;
        }

        public float GetFuelRatio()
        {
            return 1f;
        }
        public float GetMaxFuelRatio()
        {
            return 1f;
        }
        public bool SetFuelRatio(int percentage)
        {
            return false;
        }

        public int GetVirtualEnergy()
        {
            return 1;
        }
        public bool SetVirtualEnergy(int percentage)
        {
            return false;
        }
        //////////////////////////////////////////////////////////////////////////
        // Tyres

        /// <summary>
        /// Get the names of tyres available in the menu. (Includes "No Change")
        /// </summary>
        /// <returns>
        /// List of the names of tyres available in the menu
        /// </returns>
        public List<string> GetTyreTypeNames()
        {
            List<string> result = new List<string> { "NO_TYRE" };
            foreach (var pitMenuItem in shadowPitMenu.items)
            {
                if (pitMenuItem.category.Contains("TIRE"))
                {
                    result = pitMenuItem.choices;
                    break;
                }
            }
            return result;
        }

        /// <summary>
        /// Get the choices of tyres to change - "FL FR RL RR", "F R" etc.
        /// </summary>
        /// <returns>
        /// List of the change options available in the menu
        /// e.g. {"F TIRES", "R TIRES"}
        /// </returns>
        public List<string> GetTyreChangeCategories()
        {
            List<string> result = new List<string>();
            if (shadowPitMenu.Count == 0)
            {
                if (GetMenuContents().Count == 0)
                {
                    return result;
                }
            }
            foreach (var pitMenuItem in shadowPitMenu.items)
            {
                if (pitMenuItem .category.Contains("TIRE"))
                {
                    result.Add(pitMenuItem .category);
                }
            }
            return result;
        }

        /// </summary>
        /// <param name="whichTyre">the tyre category</param>
        /// <param name="requiredType">The name of the type or No Change</param>
        /// <returns>
        /// true if successful
        /// </returns>
        public bool SetTyreType(string whichTyre, string requiredType)
        {
            // check shadowPitMenu first
            var shadowTyreCategory = getShadowPitMenuItem(whichTyre);
            if (shadowTyreCategory.category != null)
            {
                if (shadowTyreCategory.choices.Contains(requiredType))
                {
                    // It's there, now operate the game menu
                    if (GetCategory().Contains("TIRE"))
                    {
                        string current = GetChoice();

                        // Adjust  if necessary
                        while (GetChoice() != requiredType)
                        {
                            string newType = ChoiceInc();
                            if (newType == current)
                            { // Didn't find it
                                Log.Error($"'{whichTyre}' does not have '{requiredType}' even though in shadowPitMenu");
                                return false;
                            }
                        }

                        return true;
                    }
                }
                else
                {
                    Log.Error($"'{whichTyre}' does not have '{requiredType}' in shadowPitMenu");
                }
            }
            else
            {
                Log.Error($"'{whichTyre}' not in shadowPitMenu");
            }
            return false;
        }

        #endregion Public Methods
    }
}