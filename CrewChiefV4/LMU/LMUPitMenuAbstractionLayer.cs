/*
Set the LMU Pit Menu using the REST API

Crew Chief wants to refer to tyres as Soft, Hard, Wet etc. but LMU uses
names that are defined in the vehicle data files (the *.tbc file).
This handles the translation both ways

Author: Tony Whitley (crewchiefTony@outlook.com)
*/
using CrewChiefV4;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.LMU;
using CrewChiefV4.RF2;
using CrewChiefV4.PitManager;

//namespace UnitTest
//{
//    public static partial class UnitTest
//    {
//        public static PitMenuController.PitMenuContents pitMenu
//        {
//            get { return inst.menuLayout.menuDict; }
//            set
//            {
//                inst.menuLayout = new MenuLayout();
//                inst.menuLayout.Set(value);
//            }
//        }
//    }
//}
namespace PitMenuAPI
{
    /// <summary>
    /// PitMenuAPI consists PitMenuAbstractionLayer : PitMenuController : PitMenu
    /// </summary>
    public class LMUPitMenuAbstractionLayer : IPitMenuAbstractionLayer //: PitMenuController
    {
        #region Private Fields

        /// <summary>
        /// All the Pit Menu categories of tyres that LMU selects from
        /// </summary>
        private readonly string[] tyreCategories = {
            "RR TIRE:",
            "RL TIRE:",
            "FR TIRE:",
            "FL TIRE:",
            "R TIRES:",
            "F TIRES:",
            "RT TIRES:",
            "LF TIRES:",
            "TIRES:"
        };

        /// <summary>
        /// The Pit Menu categories of tyres that LMU uses to select compounds,
        /// the remainder sometimes only choose this compound or NO CHANGE
        /// </summary>
        private readonly string[] frontTyreCategories = {
            "FR TIRE:",
            "FL TIRE:",
            "F TIRES:",
            "RT TIRES:",
            "LF TIRES:",
            "TIRES:"
        };

        private readonly string[] leftTyreCategories = {
            "FL TIRE:",
            "RL TIRE:",
            "LF TIRES:",
        };

        private readonly string[] rightTyreCategories = {
            "FR TIRE:",
            "RR TIRE:",
            "RT TIRES:",
        };

        internal MenuLayout menuLayout;
        #endregion Private Fields

        #region Public Methods

        internal static LMUPitMenuAbstractionLayer inst { get; private set; }
        public IPitMenuController pmc { get; set; }

        public LMUPitMenuAbstractionLayer()
        {
            inst = this;
            pmc = new LMUPitMenuController();
        }

        /// <summary>
        /// Dummy pit menu used when replaying a trace
        /// </summary>
        private static readonly Rf2PitMenuController.PitMenuContents tracePitMenuContents = new Rf2PitMenuController.PitMenuContents();
        /// <summary>
        /// Connect to the Shared Memory running in LMU
        /// </summary>
        public bool Connect()
        {
            menuLayout = new MenuLayout();
            if (!UnitTest.UnitTest.Active)
            {
                if (MainWindow.playingBackTrace)
                {   // Set up a dummy pit menu so commands in the trace can be carried out in some fashion without crashing
                    Rf2PitMenuController.PitMenuItem row = new Rf2PitMenuController.PitMenuItem("TIRES:");
                    row.choices = new List<string> { "Hypersoft", "Soft" };
                    tracePitMenuContents.Add(row);
                    row = new Rf2PitMenuController.PitMenuItem("VIRTUAL ENERGY:");
                    row.choices = new List<string>();
                    tracePitMenuContents.Add(row);
                    PitManagerEventHandlers_LMU.Pmal.SetMenuDict(tracePitMenuContents);
                }
                else
                {
                    menuLayout.NewCar();
                }
            }
            return pmc.Connect();
        }

        public void Disconnect()
        {
            pmc.Disconnect();
        }

        public string GetChoice()
        {
            return pmc.GetChoice();
        }

        public bool SetChoice(string choice)
        {
            return pmc.SetChoice(choice);
        }

        public bool SoftMatchCategory(string category)
        {
            return pmc.SoftMatchCategory(category);
        }

        public bool RelativeFuelStrategy()
        {
            return pmc.RelativeFuelStrategy();
        }

        public string CategoryDown()
        {
            return pmc.CategoryDown();
        }

        public string CategoryUp()
        {
            return pmc.CategoryUp();
        }

        public string ChoiceInc()
        {
            return pmc.ChoiceInc();
        }

        public string ChoiceDec()
        {
            return pmc.ChoiceDec();
        }

        public void RereadPitMenu()
        {
            menuLayout.NewCar();
            menuLayout.GetKeys();
        }

        public bool MfdPage(string Mfd)
        {
            return pmc.switchMFD(Mfd);
        }

        public List<string> GetCategories()
        {
            return menuLayout.GetKeys();
        }
        public bool SmartSetCategory(string category)
        {
            return pmc.SmartSetCategory(category);
        }

        public Rf2PitMenuController.PitMenuContents GetMenuContents()
        {
            return pmc.GetMenuContents();
        }

        /// <summary>
        /// Get a list of the front tyre changes provided for this vehicle.  Fronts
        /// sometimes have to be changed before the rears will be given the same
        /// set of compounds available
        /// </summary>
        /// <returns>
        /// A sorted list of the front tyre changes provided for this vehicle
        /// </returns>
        public List<string> GetFrontTyreCategories()
        {
            List<string> result =
                frontTyreCategories.Intersect(menuLayout.GetKeys()).ToList();
            result.Sort();
            Log.Debug(() => "Front tyre categories in menu: " + string.Join(", ", result.Select(s => $"'{s}'")));
            return result;
        }

        /// <summary>
        /// Get a list of all the tyre changes provided for this vehicle.
        /// </summary>
        /// <returns>
        /// A sorted list of all the tyre changes provided for this vehicle
        /// </returns>
        public List<string> GetAllTyreCategories()
        {
            List<string> result =
                tyreCategories.Intersect(menuLayout.GetKeys()).ToList();
            result.Sort();
            return result;
        }

        public string GetCurrentTyreType()
        {
            string result;
            foreach (string category in GetFrontTyreCategories())
            {
                pmc.SmartSetCategory(category);
                result = pmc.GetChoice();
                if (result != "No Change")
                {
                    return result;
                }
            }
            foreach (string category in GetRearTyreCategories())
            {
                pmc.SmartSetCategory(category);
                result = pmc.GetChoice();
                if (result != "No Change")
                {
                    return result;
                }
            }
            return "No Change";
        }

        /// <summary>
        /// Get a list of the rear tyre changes provided for this vehicle.
        /// </summary>
        /// <returns>
        /// A list of the rear tyre changes provided for this vehicle
        /// </returns>
        public List<string> GetRearTyreCategories()
        {
            // There are simpler ways to do this but...
            return tyreCategories.Except(frontTyreCategories)
              .Intersect(menuLayout.GetKeys()).ToList();
        }

        public List<string> GetLeftTyreCategories()
        {
            return leftTyreCategories.Intersect(menuLayout.GetKeys()).ToList();
        }

        public List<string> GetRightTyreCategories()
        {
            return rightTyreCategories.Intersect(menuLayout.GetKeys()).ToList();
        }

        public List<string> GetTyreTypeNames()
        {
            string tyre = GetFrontTyreCategories()[0];
            var result = menuLayout.Get(tyre);
            Log.Debug(() => "Tyre type names " + string.Join(", ", result.Select(s => $"'{s}'")));
            return result;
        }

        /// <summary>
        /// Set the tyre compound selection in the Pit Menu.
        /// Set the front tyres first as the rears may may depend on what is
        /// selected for the fronts
        /// Having changed them all, the client can then set specific tyres to
        /// NO CHANGE
        /// </summary>
        /// <param name="tyreType">Name of actual tyre type or NO CHANGE</param>
        /// <returns>true all tyres changed</returns>
        public bool SetAllTyreTypes(string tyreType)
        {
            bool response = true;

            foreach (string whichTyre in GetFrontTyreCategories())
            {
                if (response)
                {
                    response = pmc.SmartSetCategory(whichTyre);
                }
                if (response)
                {
                    response = pmc.SetTyreType(whichTyre, tyreType);
                }
            }
            foreach (string whichTyre in GetRearTyreCategories())
            {
                if (response)
                {
                    response = SmartSetCategory(whichTyre);
                }
                if (response)
                {
                    response = pmc.SetTyreType(whichTyre, tyreType);
                }
            }
            return response;
        }

        public bool SetCategoryAndChoice(string category, string choice)
        {
            int tryNo = 5;
            bool response;
            while (tryNo-- > 0)
            {
                response = SmartSetCategory(category);
                if (response)
                {
                    response = pmc.SetChoice(choice);
                    if (response)
                    {
                        return true;
                    }
                    pmc.startUsingPitMenu();
                }
            }
            return false;
        }

        public bool SetFuelLevel(int fuelLevel)
        {
            FuelVoiceCommand.Level = fuelLevel;
            return pmc.SetFuelLevel(fuelLevel);
        }

        public int GetVirtualEnergy()
        {
            return pmc.GetVirtualEnergy();
        }
        public bool SetVirtualEnergy(int percentage)
        {
            return pmc.SetVirtualEnergy(percentage);
        }

        public float GetFuelRatio()
        {
            return pmc.GetFuelRatio();
        }
        public float GetMaxFuelRatio()
        {
            return pmc.GetMaxFuelRatio();
        }
        public bool SetFuelRatio(int percentage)
        {
            return pmc.SetFuelRatio(percentage);
        }

        // Unit Test
        public void SetMenuDict(Rf2PitMenuController.PitMenuContents dict)
        {
            if (menuLayout == null)
            {
                menuLayout = new MenuLayout();
            }
            menuLayout.Set(dict);
        }

        #endregion Public Methods

        #region Private Classes

        /// <summary>
        /// Virtualisation of the menu layout for the current vehicle
        /// </summary>
        internal class MenuLayout
        {
            #region Private Fields

            private Rf2PitMenuController.PitMenuContents menuDict =
                new Rf2PitMenuController.PitMenuContents();

            private static readonly IPitMenuController pmc = new LMUPitMenuController();

            #endregion Private Fields

            #region Public Methods

            public void NewCar()
            {
                menuDict = new Rf2PitMenuController.PitMenuContents();
            }

            internal List<string> Get(string key)
            {
                if (menuDict.Count == 0)
                {
                    menuDict = pmc.GetMenuContents();
                }
                if (menuDict.Count > 0 && menuDict.Contains(key))
                {
                    return menuDict.GetChoices(key);
                }
                return new List<string>();
            }

            internal List<string> GetKeys()
            {
                if (menuDict.Count == 0)
                {
                    menuDict = pmc.GetMenuContents();
                }
                return new List<string>(menuDict.Categories);
            }

            internal void Set(Rf2PitMenuController.PitMenuContents unitTestDict)
            {
                menuDict = unitTestDict;
            }

            #endregion Public Methods
        }

        #endregion Private Classes
    }
}