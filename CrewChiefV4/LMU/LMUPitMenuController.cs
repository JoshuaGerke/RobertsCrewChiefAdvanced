using System;
using System.Collections.Generic;
using CrewChiefV4.RF2;
using PitMenuAPI;
using static CrewChiefV4.RF2.Rf2PitMenuController;

namespace CrewChiefV4.LMU
{
    public class LMUPitMenuController : LMUPitMenuAPI, IPitMenuController
    {
        public PitMenuContents GetMenuContents()
        {
            PitMenuContents result = new PitMenuContents();
            PitMenuItem pitMenuItem;
            foreach (var category in cachedPitMenu)
            {
                pitMenuItem = new PitMenuItem(category.name);
                foreach (var choice in category.settings)
                {
                    pitMenuItem.choices.Add(choice.text);
                }
                result.Add(pitMenuItem);
            }
            return result;
        }

        public bool SmartSetCategory(string category)
        {
            for (var i = 0; i < cachedPitMenu.Count; i++)
            {
                if (cachedPitMenu[i].name == category)
                {
                    PitMenuIndex = i;
                    return true;
                }
            }
            Log.DontSpam($"LMU Pit menu doesn't have category '{category}'").Warning();
            return false;
        }

        public bool RelativeFuelStrategy()
        {
            bool relativeFuelStrategy = false;
            if (SmartSetCategory("FUEL:"))
            {
                string choice = GetChoice();
                if (choice.StartsWith("+"))
                {
                    relativeFuelStrategy = true;
                }
            }
            Log.Debug(() => $"LMU Relative Fuel Strategy: {relativeFuelStrategy}");
            return relativeFuelStrategy;
        }

        public int GetFuelLevelSetting()
        {
            if (SmartSetCategory("VIRTUAL ENERGY:"))
            {
                return ParseFuelLevelFromVirtualEnergySetting(GetChoice());
            }
            if (SmartSetCategory("FUEL:"))
            {
                return Rf2PitMenuController.ParseFuelLevel(GetChoice());
            }
			return -1;
        }

        private readonly RestAPI restAPI = new RestAPI();
        public bool SetFuelLevel(int requiredFuel)
        {
            bool status = reReadPitMenu();
            if (status)
            {
                if (!SmartSetCategory("FUEL:")) // (has its own logging)
                { // Menu doesn't contain FUEL
                    return false;
                }
                int current = GetFuelLevelSetting(); // (has its own logging)
                if (current < 0)
                {
                    return false; // Can't readPitMenu value
                }

                Log.Commentary($"Set fuel level: current {current}, required {requiredFuel} litres");

                for (var i = 0; i < cachedPitMenu[PitMenuIndex].settings.Count; i++)
                {
                    if (requiredFuel <= Rf2PitMenuController.ParseFuelLevel(cachedPitMenu[PitMenuIndex].settings[i].text))
                    {
                        cachedPitMenu[PitMenuIndex].currentSetting = i;
                        status = restAPI.postPitMenu(cachedPitMenu);
                        if (status)
                        {
                            Log.Commentary($"Fuel level set: {GetFuelLevelSetting()} litres");
                        }
                        return status;
                    }
                }
                //Log.Warning("LMU Can't adjust fuel further");
                cachedPitMenu[PitMenuIndex].currentSetting = cachedPitMenu[PitMenuIndex].settings.Count - 1;
                status = restAPI.postPitMenu(cachedPitMenu);
                if (status)
                {
                    Log.Commentary($"Fuel level set: {GetFuelLevelSetting()} litres");
                }
            }
            return status;
        }

        public int GetVirtualEnergy()
        {
            reReadPitMenu();
            if (!SmartSetCategory("VIRTUAL ENERGY:"))
            {
                return 0;
            }
            return (int)cachedPitMenu[PitMenuIndex].currentSetting;
        }

        public bool SetVirtualEnergy(int percentage)
        {
            bool status = reReadPitMenu();
            if (status)
            {
                if (!SmartSetCategory("VIRTUAL ENERGY:"))
                {
                    return false;
                }
                percentage = Math.Min(percentage, 100);
                Log.Commentary($"LMU Set Virtual Energy to {percentage} %");
                cachedPitMenu[PitMenuIndex].currentSetting = percentage;
                status = restAPI.postPitMenu(cachedPitMenu);
                if (status)
                {
                    Log.Commentary($"LMU Virtual Energy set: {percentage} %");
                }
            }
            return status;
        }

        public float GetFuelRatio()
        {
            reReadPitMenu();
            if (!SmartSetCategory("FUEL RATIO:"))
            {
                return -1f;
            }
            var _currentSetting = (int)cachedPitMenu[PitMenuIndex].currentSetting;
            var text = cachedPitMenu[PitMenuIndex].settings[_currentSetting].text;
            return float.Parse(text);
        }

        public float GetMaxFuelRatio()
        {
            reReadPitMenu();
            if (!SmartSetCategory("FUEL RATIO:"))
            {
                return -1f;
            }
            var text = cachedPitMenu[PitMenuIndex].settings[cachedPitMenu[PitMenuIndex].settings.Count - 1].text;
            return float.Parse(text);
        }

        public bool SetFuelRatio(int percentage)
        {
            bool status = reReadPitMenu();
            if (status)
            {
                if (!SmartSetCategory("FUEL RATIO:"))
                {
                    return false;
                }
                Log.Commentary($"LMU Set fuel ratio to {percentage} %");
                if (cachedPitMenu[PitMenuIndex].settings.Count < 0)
                {
                    Log.Warning("LMU Can't adjust fuel ratio further");
                    percentage = cachedPitMenu[PitMenuIndex].settings.Count - 1;
                }
                cachedPitMenu[PitMenuIndex].currentSetting = percentage - 1;  // 1-based?
                status = restAPI.postPitMenu(cachedPitMenu);
                if (status)
                {
                    Log.Commentary($"LMU Fuel ratio set: {percentage} %");
                }
            }
            return status;
        }


        public List<string> GetTyreTypeNames()
        {
            List<string> result = new List<string> { "NO_TYRE" };
            //foreach (var pitMenuItem in shadowPitMenu.items)
            //{
            //    if (pitMenuItem.category.Contains("TIRE"))
            //    {
            //        result = pitMenuItem.choices;
            //        break;
            //    }
            //}
            return result;
        }

        public List<string> GetTyreChangeCategories()
        {
            List<string> result = new List<string>();
            //if (shadowPitMenu.Count == 0)
            //{
            //    if (GetMenuContents().Count == 0)
            //    {
            //        return result;
            //    }
            //}
            //foreach (var pitMenuItem in shadowPitMenu.items)
            //{
            //    if (pitMenuItem.category.Contains("TIRE"))
            //    {
            //        result.Add(pitMenuItem.category);
            //    }
            //}
            return result;
        }

        public bool SetTyreType(string whichTyre, string requiredType)
        {
            if (reReadPitMenu())
            {
                var shadowTyreCategory = getShadowPitMenuItem(whichTyre);
                if (shadowTyreCategory.category != null)
                {
                    if (shadowTyreCategory.choices.Contains(requiredType))
                    {
                        if (GetCategory().Contains("TIRE"))
                        {
                            string current = GetChoice();
                            while (GetChoice() != requiredType)
                            {
                                string newType = ChoiceInc();
                                if (newType == current)
                                {
                                    Log.Error($"LMU '{whichTyre}' does not have '{requiredType}' even though in shadowPitMenu");
                                    return false;
                                }
                            }
                            return true;
                        }
                    }
                    else
                    {
                        Log.Error($"LMU '{whichTyre}' does not have '{requiredType}' in shadowPitMenu");
                    }
                }
                else
                {
                    Log.Error($"LMU '{whichTyre}' not in shadowPitMenu");
                }
            }
            return false;
        }

        private Rf2PitMenuController.PitMenuItem getShadowPitMenuItem(string category)
        {
            Rf2PitMenuController.PitMenuItem result = new Rf2PitMenuController.PitMenuItem();
            result.category = null;
            //if (shadowPitMenu.Categories.Contains(category))
            //{
            //    foreach (var cat in shadowPitMenu)
            //    {
            //        if (cat.category == category)
            //        {
            //            result = cat;
            //            break;
            //        }
            //    }
            //}
            return result;
        }

        private int ParseFuelLevelFromVirtualEnergySetting(string fuelMenu)
        {
            var percentSplit = fuelMenu.Split('%');
            if (percentSplit.Length > 0)
            {
                var percent = percentSplit[0];
                if (float.TryParse(percent, out float current))
                {
                    return (int)(current * GetFuelRatio());
                }
            }
            return -1;
        }
    }
}
