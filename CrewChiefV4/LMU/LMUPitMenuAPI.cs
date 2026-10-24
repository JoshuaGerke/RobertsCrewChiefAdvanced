using System;
using System.Collections.Generic;

using static CrewChiefV4.LMU.LMU_REST_API_classes;

namespace CrewChiefV4.LMU
{
    public class LMUPitMenuAPI : IPitMenu
    {
        public class PitMenu : PitMenuRoot { }
        public class Setting : LMU_REST_API_classes.Setting { }
        private List<PitMenu> _cachedPitMenu;
        private readonly RestAPI restAPI = new RestAPI();

        /// <summary>
        /// Read the cached Pit menu from LMU.
        /// </summary>
        internal List<PitMenu> cachedPitMenu
        {
            get
            {
                if (_cachedPitMenu == null || _cachedPitMenu.Count == 0)
                {
                    _cachedPitMenu = restAPI.get<List<PitMenu>>(ReceivePitMenu.endPoint) ?? new List<PitMenu>();
                }
                return _cachedPitMenu;
            }
            private set => _cachedPitMenu = value;
        }

        /// <summary>
        /// Re-reads the pit menu which may have been updated by the game.
        /// </summary>
        /// <returns>Read successfully from LMU</returns>
        protected bool reReadPitMenu()
        {
            cachedPitMenu = null;
            return (cachedPitMenu != null && cachedPitMenu.Count != 0);
        }

        // The category currently selected in the pit menu
        protected int PitMenuIndex;

        public LMUPitMenuAPI()
        {
        }

        private bool Connected = false;

        public bool Connect()
        {
            Connected = true;
            return Connected;
        }

        public void Disconnect()
        {
            Connected = false;
        }

        public bool switchMFD(string display = "MFDB")
        {
            // Implement MFD switching logic for LMU
            return Connected;
        }

        public bool startUsingPitMenu()
        {
            return reReadPitMenu();
        }

        public void setDelay(int mS, int initialDelay)
        {
            // Implement delay setting logic for LMU
        }

        public bool PitRequest()
        {
            // Implement pit request logic for LMU
            return Connected;
        }

        public string GetCategory(bool log = false)
        {
            return cachedPitMenu[PitMenuIndex].name;
        }

        public string CategoryUp()
        {
            PitMenuIndex = Math.Max(--PitMenuIndex, 0);
            return cachedPitMenu[PitMenuIndex].name;
        }

        public string CategoryDown()
        {
            PitMenuIndex = Math.Min(++PitMenuIndex, cachedPitMenu.Count - 1);
            return cachedPitMenu[PitMenuIndex].name;
        }

        public string ChoiceInc()
        {
            if (reReadPitMenu())
            {
                cachedPitMenu[PitMenuIndex].currentSetting++;
                if (restAPI.postPitMenu(cachedPitMenu))
                {
                    return cachedPitMenu[PitMenuIndex].currentSetting.ToString();
                }
            }
            return "N/A";
        }

        public string ChoiceDec()
        {
            if (reReadPitMenu())
            {
                cachedPitMenu[PitMenuIndex].currentSetting--;
                if (restAPI.postPitMenu(cachedPitMenu))
                {
                    return cachedPitMenu[PitMenuIndex].currentSetting.ToString();
                }
            }
            return "N/A";
        }

        public string GetChoice()
        {
            if (reReadPitMenu())
            {
                int setting = (int)cachedPitMenu[PitMenuIndex].currentSetting;
                if (setting < 0 || setting >= cachedPitMenu[PitMenuIndex].settings.Count)
                {
                    return "N/A";
                }
                return cachedPitMenu[PitMenuIndex].settings[setting].text;
            }
            return "N/A";
        }

        public bool SetCategory(string category)
        {
            for (PitMenuIndex = 0; PitMenuIndex < cachedPitMenu.Count; PitMenuIndex++)
            {
                if (cachedPitMenu[PitMenuIndex].name.Equals(category))
                {
                    return true;
                }
            }
            return false;
        }

        public bool SoftMatchCategory(string category)
        {
            for (PitMenuIndex = 0; PitMenuIndex < cachedPitMenu.Count; PitMenuIndex++)
            {
                if (cachedPitMenu[PitMenuIndex].name.Contains(category))
                {
                    return true;
                }
            }
            return false;
        }

        public bool SetChoice(string choice)
        {
            // Straight match first
            for (var c = 0; c < cachedPitMenu[PitMenuIndex].settings.Count; c++)
            {
                if (cachedPitMenu[PitMenuIndex].settings[c].text == choice)
                {
                    cachedPitMenu[PitMenuIndex].currentSetting = c;
                    return restAPI.postPitMenu(cachedPitMenu);
                }
            }
            var wordIndex = 0;
            const char delimiter = ' ';
            foreach (var word in choice.Split(delimiter))
            {
                for (var c = 0; c < cachedPitMenu[PitMenuIndex].settings.Count; c++)
                {
                    var choiceWords = cachedPitMenu[PitMenuIndex].settings[c].text.Split(delimiter);
                    if (choiceWords.Length > wordIndex &&
                        choiceWords[wordIndex] == choice)
                    {
                        cachedPitMenu[PitMenuIndex].currentSetting = c;
                        return restAPI.postPitMenu(cachedPitMenu);
                    }
                }
                wordIndex++;
            }
            return false;
        }
    }
}
