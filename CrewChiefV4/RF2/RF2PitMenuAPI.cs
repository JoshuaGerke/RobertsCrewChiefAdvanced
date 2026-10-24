/* Pit Menu API using TheIronWolf's rF2 Shared Memory Map plugin
https://github.com/TheIronWolfModding/rF2SharedMemoryMapPlugin

It provides functions to directly control the Pit Menu (selecting it,
moving up and down, changing the menu choice) and "smart" controls that
move to a specified category or a specified choice.

Author: Tony Whitley (crewchiefTony@outlook.com)
*/

using System;
using System.Text;

using CrewChiefV4;

using rF2SharedMemory;
using rF2SharedMemory.rFactor2Data;
namespace UnitTest
{
    public static partial class UnitTest
    {
        public static string PitMenuCategory { get; set; }
        public static string PitMenuChoice { get; set; }
        /// <summary>
        /// Running RF2 unit tests when RF2 is running
        /// </summary>
        private static bool _Rf2LiveSet;

        private static bool _Rf2Live;

        public static bool Rf2Live
        {
            get
            {
                if (!_Rf2LiveSet)
                {
                    var gameDefinition =
                        GameDefinition.getGameDefinitionForCommandLineName("RF2");
                    _Rf2Live = Utilities.IsGameRunning(
                        gameDefinition.processName,
                        gameDefinition.alternativeProcessNames,
                        out CrewChief.gameExeParentDirectory);
                }
                return _Rf2Live;
            }
        }
    }
}
namespace CrewChiefV4.RF2
{
    /// <summary>
    /// PitMenuAPI consists PitMenuAbstractionLayer : PitMenuController : PitMenu
    /// </summary>
    public class RF2PitMenu : IPitMenu
    {
        #region Private Fields

        private static
            SendrF2HWControl sendHWControl = new SendrF2HWControl();
        private MappedBuffer<rF2PitInfo> pitInfoBuffer = null;

        private static rF2PitInfo pitInfo;
        private bool Connected = false;

        // Shared memory scans slowly until the first control is received. It
        // returns to scanning slowly when it hasn't received a control for a while.
        private int initialDelay = 230;

        // Delay in mS after sending a HW control to rFactor before sending another,
        // set by experiment
        // 20 works for category selection and tyres but fuel needs it slower
        private int delay = 30;

        #endregion Private Fields

        #region Public Methods

        ///////////////////////////////////////////////////////////////////////////
        /// Setup
        ///
        /// <summary>
        /// Connect to the Shared Memory running in rFactor
        /// </summary>
        /// <returns>
        /// true if connected
        /// </returns>
        public bool Connect()
        {
            if (!this.Connected)
            {
                Connected = sendHWControl.Connect();
                if (Connected)
                {
                    pitInfoBuffer = new MappedBuffer<rF2PitInfo>(
                        rFactor2Constants.MM_PITINFO_FILE_NAME,
                        partial: false,
                        skipUnchanged: true);
                    pitInfoBuffer.Connect();
                }
            }
            return Connected;
        }

        /// <summary>
        /// Disconnect from the Shared Memory running in rFactor
        /// tbd: :No references???
        /// </summary>
        public void Disconnect()
        {
            pitInfoBuffer.Disconnect();
            sendHWControl.Disconnect();
            Connected = false;
        }

        /// <summary>
        /// Switch the MFD to
        /// MFDA Standard (Sectors etc.)
        /// MFDB Pit Menu
        /// MFDC Vehicle Status (Tyres etc.)
        /// MFDD Driving Aids
        /// MFDE Extra Info (RPM, temps)
        /// MFDF Race Info (Clock, leader etc.)
        /// MFDG Standings (Race position)
        /// MFDH Penalties
        ///
        /// Shared memory is normally scanning slowly until a control is received
        /// so send the first control with a longer delay
        /// </summary>
        /// <param name="display"></param>
        /// <returns></returns>
        private static string currentDisplay = "";
        public bool switchMFD(string display = "MFDB")
        {
            if (!Connected)
            {
                Connected = Connect();
            }
            if (Connected)
            {
                if (currentDisplay != display)
                {
                    // To select MFDB screen for example:
                    // If the MFD is off ToggleMFDA will turn it on then ToggleMFDB will switch
                    // to the Pit Menu
                    // If it is showing MFDA ToggleMFDA will turn it off then ToggleMFDB
                    // will show the Pit Menu
                    // If it is showing MFD"x" ToggleMFDA will show MFDA then ToggleMFDB
                    // will show the Pit Menu
                    string notDisplay = display == "MFDA" ? "ToggleMFDB" : "ToggleMFDA";
                    sendControl(notDisplay, true);
                    sendControl("Toggle" + display); // Select required MFD
                }
                currentDisplay = display;
            }
            return Connected;
        }

        public bool startUsingPitMenu()
        {
            int countDown = 20; // Otherwise it can lock up here
            do
            {
                switchMFD("MFDB");
            } while (!(iSoftMatchCategory("TIRE", "FUEL")) && countDown-- > 0);
            return countDown > 0;
        }

        /// <summary>
        /// Set the delay between sending each control
        /// After sending the first control in sequence the delay should be longer
        /// as the Shared Memory takes up to 200 mS to switch to its higher update
        /// rate.  After 200 mS without receiving any controls it returns to a
        /// 200 mS update.
        /// </summary>
        /// <param name="mS"></param>
        public void setDelay(int mS, int _initialDelay)
        {
            delay = mS;
            initialDelay = _initialDelay;
        }

        /// <summary>
        /// Send a Pit Request (which toggles)
        /// </summary>
        /// <returns>Successful</returns>
        public bool PitRequest()
        {
            if (!Connected)
            {
                Connected = Connect();
            }
            if (Connected)
            {
                sendControl("PitRequest", true);
                Log.Commentary("PitRequest sent");
            }
            return Connected;
        }

        //////////////////////////////////////////////////////////////////////////
        /// Direct menu control
        //////////////////////////////////////////////////////////////////////////
        
        /// <summary>
        /// Operate the menu control until something changes
        /// or we give up because something is wrong
        /// </summary>
        private string changeMenu(string control, bool choiceVsCategory)
        {
            int countDown = 5;
            string choiceOrCategory = choiceVsCategory ? GetChoice() : GetCategory();
            sendControl(control);
            string choiceNow = choiceVsCategory ? GetChoice() : GetCategory();
            while (choiceNow == choiceOrCategory && countDown-- > 0)
            {
                sendControl(control, true);
                choiceNow = choiceVsCategory ? GetChoice() : GetCategory();
            }

            if (countDown == 0)
            {
                startUsingPitMenu();
            }
            return choiceNow;
        }
        // Menu Categories
        /// <summary>
        /// Get the current Pit Menu category
        /// </summary>
        /// <returns>
        /// Name of the category
        /// </returns>
        public string GetCategory(bool log = false)
        {
            string catName;
            if (!CrewChief.loadDataFromFile && !UnitTest.UnitTest.Active)
            {
                pitInfoBuffer.GetMappedData(ref pitInfo);
                catName = GetStringFromBytes(pitInfo.mPitMneu.mCategoryName);
            }
            else
            {
                catName = UnitTest.UnitTest.PitMenuCategory;
            }

            if (log)
            {
                Log.Verbose(() => $"Pit menu category '{catName}'");
            }
            return catName;
        }

        /// <summary>
        /// Move up to the next category
        /// </summary>
        public string CategoryUp()
        {
            string newCategory = changeMenu("PitMenuUp", false);
            Log.Verbose("Pit menu category up");
            System.Threading.Thread.Sleep(1); // Delay (yield?) improves reliability
            return newCategory;
        }

        /// <summary>
        /// Move down to the next category
        /// </summary>
        public string CategoryDown()
        {
            string newCategory = changeMenu("PitMenuDown", false);
            Log.Verbose("Pit menu category down");
            System.Threading.Thread.Sleep(1);// Delay (yield?) improves reliability
            return newCategory;
        }

        //////////////////////////////////////////////////////////////////////////
        // Menu Choices
        /// <summary>
        /// Increment the current choice
        /// </summary>
        public string ChoiceInc()
        {
            string newCategory = changeMenu("PitMenuIncrementValue", true);
            Log.Verbose("Pit menu value inc");
            return newCategory;
        }

        /// <summary>
        /// Decrement the current choice
        /// </summary>
        public string ChoiceDec()
        {
            string newCategory = changeMenu("PitMenuDecrementValue", true);
            Log.Verbose("Pit menu value dec");
            return newCategory;
        }

        /// <summary>
        /// Get the text of the current choice
        /// </summary>
        /// <returns>string</returns>
        public string GetChoice()
        {
            string choiceStr;
            if (!UnitTest.UnitTest.Active)
            {
                pitInfoBuffer.GetMappedData(ref pitInfo);
                choiceStr = GetStringFromBytes(pitInfo.mPitMneu.mChoiceString);
                if (CrewChief.Debug.RunningUnderDebugger || UnitTest.UnitTest.Active)
                {
                    Log.Commentary($"Pit menu choice '{choiceStr}'");
                }
            }
            else
            {
                choiceStr = UnitTest.UnitTest.PitMenuChoice;
            }
            return choiceStr;
        }

        //////////////////////////////////////////////////////////////////////////
        /// "Smart" menu control - specify which category or choice and it will be
        /// selected
        //////////////////////////////////////////////////////////////////////////
        // Menu Categories
        /// <summary>
        /// Set the Pit Menu category
        /// </summary>
        /// <param name="category">string</param>
        /// <returns>
        /// false: Category not found
        /// </returns>
        public bool SetCategory(string category)
        {
            string InitialCategory = GetCategory();
            int tryNo = 5;
            while (GetCategory(true) != category)
            {
                string newCategory = CategoryDown();
                if (newCategory == InitialCategory)
                {  // Wrapped around, category not found
                    if (tryNo-- < 0)
                    {
                        return false;
                    }
                    startUsingPitMenu();
                }
            }
            return true;
        }

        /// <summary>
        /// Select a category that includes "category"
        /// </summary>
        /// <param name="category"></param>
        /// <returns>
        /// True: category found
        /// </returns>
        public bool SoftMatchCategory(string category)
        {
            return iSoftMatchCategory(category);
        }

        /// <summary>
        /// Select a category that includes "category"
        /// </summary>
        /// <param name="cat1">category to match</param>
        /// <param name="cat2">optional other category to match</param>
        /// <returns></returns>
        private bool iSoftMatchCategory(string cat1, string cat2 = null)
        {
            string InitialCategory = GetCategory();
            if (InitialCategory == null)
            {
                return false;
            }
            int tryNo = 3;
            while (!(GetCategory(true).Contains(cat1) ||
                     (cat2 != null && GetCategory().Contains(cat2))))
            {
                string newCategory = CategoryDown();
                if (newCategory == InitialCategory)
                {  // Wrapped around, category not found
#pragma warning disable S1066
                    if (tryNo-- < 0)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// SetChoice first tries an exact match, if that fails it accepts an
        /// entry that starts with the choice.  This extracts that complexity
        /// (Not certain it's necessary, an exact match looks OK but it was
        /// written with StartsWith...)
        /// </summary>
        /// <param name="choice"></param>
        /// <param name="startsWith"></param>
        /// <returns>
        /// false: Choice not found using the current comparison
        /// </returns>
        bool choiceCompare(string choice, bool startsWith)
        {
            return ((startsWith && GetChoice().StartsWith(choice)) ||
                    (!startsWith && GetChoice() == choice));
        }
        /// <summary>
        /// Set the current choice
        /// </summary>
        /// <param name="choice">string</param>
        /// <returns>
        /// false: Choice not found
        /// </returns>
        public bool SetChoice(string choice)
        {
            string LastChoice = GetChoice();
            string newChoice;
            bool inc = true;
            bool startsWith = false;
            while (!choiceCompare(choice, startsWith))
            {
                if (inc)
                {
                    newChoice = ChoiceInc();
                }
                else
                {
                    newChoice = ChoiceDec();
                }
                if (GetChoice() == LastChoice)
                {
                    if (inc)
                    { // Go the other way
                        inc = false;
                    }
                    else
                    {
#pragma warning disable S2583 // Conditionally executed code should be reachable
                        if (startsWith)
#pragma warning restore S2583 // Conditionally executed code should be reachable
                        {
                            return false;
                        }
                        startsWith = true;
                        inc = false;
                    }
                }
                LastChoice = newChoice;
            }
            return true;
        }
        #endregion Public Methods

        #region Private Methods

        //////////////////////////////////////////////////////////////////////////
        // Utils
        private static string GetStringFromBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                return "";
            }

            var nullIdx = Array.IndexOf(bytes, (byte)0);

            return nullIdx >= 0
                ? Encoding.Default.GetString(bytes, 0, nullIdx)
                : Encoding.Default.GetString(bytes);
        }

        public void sendControl(string control, bool firstPress = false)
        {
            int downDelay = firstPress ? initialDelay : delay;
            sendHWControl.SendHWControl(control, true);
            System.Threading.Thread.Sleep(downDelay);
            sendHWControl.SendHWControl(control, false);
            System.Threading.Thread.Sleep(delay);
            // Doesn't seem to be necessary to do "retVal false" too
        }

        #endregion Private Methods
    }
}
