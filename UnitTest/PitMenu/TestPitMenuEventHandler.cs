using NUnit.Framework;
using CrewChiefV4;
using CrewChiefV4.PitManager;
using System.Windows.Forms;
using System.Globalization;
using System.Threading;
using System.Collections.Generic;
using CrewChiefV4.RF2;
using CrewChiefV4.UserInterface.Models;
using UnitTest.Utils;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using TestContext = Microsoft.VisualStudio.TestTools.UnitTesting.TestContext;

// NONE of these actually run, TestPitManager does allow stepping through the
// Pit Menu event handler
namespace UnitTestPitMenuEventHandler
{
    [TestFixture]
    public class TestPitManagerRF2
    {
        private static Rf2PitMenuController.PitMenuContents testPitMenuContents = new Rf2PitMenuController.PitMenuContents();

        [OneTimeSetUp]
        public static void ClassInitialize()
        {
            new CrewChiefV4.UserSettings(); // Avoids null pointer errors all over the shop
            CrewChief.gameDefinition.gameEnum = GameEnum.RF2_64BIT;
            Rf2PitMenuController.PitMenuItem row = new Rf2PitMenuController.PitMenuItem("TIRES:");
            row.choices = new List<string> { "Hypersoft", "Soft" };
            testPitMenuContents.Add( row);
            row = new Rf2PitMenuController.PitMenuItem("FUEL:");
            row.choices = new List<string>();
            testPitMenuContents.Add(row);
            PitManagerEventHandlers_RF2.Pmal.SetMenuDict(testPitMenuContents);
        }
        [Test]
        /// RUNS OK AS LONG AS RF2 IS NOT LOADED
        /// can be used to create Documents\CrewChiefV4\rF2\TyreDictionary.json
        public void Test_EventHandler()
        {
            bool result;
            QuitIf.rF2orLMUrunning();
            var pmh = new PitManager();

            result = pmh.HandleEvent(PitManagerEvent.TyreCompoundWet, "");
            Assert.IsTrue(result);
            Thread.Sleep(100);

            result = pmh.HandleEvent(PitManagerEvent.AeroFrontSetToX, "");
            Assert.IsFalse(result);
            Thread.Sleep(100);
            result = pmh.HandleEvent(PitManagerEvent.RepairFast, "");
            Assert.IsFalse(result);
            Thread.Sleep(100);
        }

        [Test]
        public void Test_EventHandlerFuel()
        {
            bool result;
            QuitIf.rF2notRunning();
            var pmh = new PitManager();
            result = pmh.HandleEvent(PitManagerEvent.FuelAddXlitres, "box add five liters");
            Assert.IsFalse(result); // rF2 does not operate the menu
            Thread.Sleep(100);
        }
        [Test]
        public void Test_EventHandlerTyrePressure()
        {
            bool result;
            QuitIf.rF2notRunning();
            var pmh = new PitManager();
            result = pmh.HandleEvent(PitManagerEvent.TyrePressure, "box change tyre pressures one point four five bar");
            Assert.IsFalse(result); // rF2 does not operate the menu
            Thread.Sleep(100);
        }
        [Test]
        public void Test_EventHandlerUndo()
        {
            bool result;
            QuitIf.rF2notRunning();
            var pmh = new PitManager();

            result = pmh.HandleEvent(PitManagerEvent.TyreChangeNone, "");
            Assert.IsTrue(result); // rF2 does not operate the menu
            Thread.Sleep(100);

            result = pmh.HandleEvent(PitManagerEvent.FuelAddXlitres, "one gallon");
            Assert.IsFalse(result); // rF2 does not operate the menu
            Thread.Sleep(100);
        }
    }

    [TestFixture]
    public class TestPitManagerLMU
    {
        private static Rf2PitMenuController.PitMenuContents testPitMenuContents = new Rf2PitMenuController.PitMenuContents();

        [OneTimeSetUp]
        public static void ClassInitialize()
        {
            new CrewChiefV4.UserSettings(); // Avoids null pointer errors all over the shop
            CrewChief.gameDefinition.gameEnum = GameEnum.LMU;
        }
        [Test]
        /// RUNS OK AS LONG AS LMU IS NOT LOADED
        public void Test_EventHandler()
        {
            bool result;
            QuitIf.rF2orLMUrunning();
            var pmh = new PitManager();

            result = pmh.HandleEvent(PitManagerEvent.TyreCompoundWet, "");
            Thread.Sleep(100);

            result = pmh.HandleEvent(PitManagerEvent.AeroFrontSetToX, "");
            Thread.Sleep(100);
            result = pmh.HandleEvent(PitManagerEvent.RepairFast, "");
            Thread.Sleep(100);
        }


        [Test]
        public void Test_EventHandlerFuel()
        {
            bool result;
            var pmh = new PitManager();
            result = pmh.HandleEvent(PitManagerEvent.FuelFillToXlitres, "box fill to fifteen liters");
            Assert.IsTrue(result);
            Thread.Sleep(100);
        }

        [Test]
        public void Test_EventHandlerVirtualEnergy()
        {
            bool result;
            var pmh = new PitManager();
            UnitTest.UnitTest.LmuVirtualEnergy = true;
            result = pmh.HandleEvent(PitManagerEvent.VirtualEnergy, "box virtual energy twenty five");
            Assert.IsTrue(result);
            result = pmh.HandleEvent(PitManagerEvent.VirtualEnergy, "box virtual energy one hundred");
            Assert.IsTrue(result);
            Thread.Sleep(10);
        }

        [Test]
        public void Test_EventHandlerTyrePressure()
        {
            bool result;
            //CrewChief.gameDefinition.gameEnum = GameEnum.IRACING;
            var pmh = new PitManager();
            result = pmh.HandleEvent(PitManagerEvent.TyrePressureFront, "box change front tyre pressures one point four five bar");
            Assert.IsTrue(result);
            Thread.Sleep(10);
        }
        [Test]
        public void Test_EventHandlerUndo()
        {
            bool result;
            var pmh = new PitManager();

            result = pmh.HandleEvent(PitManagerEvent.TyreChangeNone, "");
            Assert.IsTrue(result);

            result = pmh.HandleEvent(PitManagerEvent.FuelAddXlitres, "one gallon"); // May not have FUEL:
            result = pmh.HandleEvent(PitManagerEvent.FuelRatio, "seventy percent"); // May not have FUEL RATION:
            result = pmh.HandleEvent(PitManagerEvent.VirtualEnergy, "pitstop virtual energy one hundred percent");  // May not have VIRTUAL ENERGY::
            Thread.Sleep(10);
        }
    }

    [TestFixture]
    public class TestPitManagerInCC
    {
        private static MainWindow hwnd;
        public static CrewChief ccObj;
        [OneTimeSetUp]
        public static void TestFixtureSetup()
        {
            // Set Invariant Culture for all threads as default.
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            // Set Invariant Culture for current thead.
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            Application.EnableVisualStyles();

            MyName.myName = "UnitTest"; // So the "MyName" dialog is not activated.
            CrewChief.gameDefinition = null;
            hwnd = new MainWindow();
            ccObj = hwnd.crewChief;//    new CrewChief(controllerConfiguration);
        }

        [OneTimeTearDown]
        public static void TextFixtureCleanup()
        {
            hwnd.Dispose();
        }
        [Test]
        public void CCobject()
        {
            ; // Do nothing, just tests that CC MainWindow has been created
        }
#if false
        [Test]
        public void Test1()
        {
            var pmh = new PitManager_dev();
            pmh.method();
            pmh.HandleEvent(PitManager_devEvent.AeroFrontPlusMinusX);

        }
        [Test]
        public void Test2()
        {
            bool result;
            var pmh = new PitManager_dev();
            pmh.method();
            result = pmh.EventHandler2(PitManager_devEvent.AeroFrontPlusMinusX);
            Assert.IsTrue(result);
            result = pmh.EventHandler2(PitManager_devEvent.FenderL);
            Assert.IsTrue(result);
        }
#endif

        [Test]
        public void TestVoice()
        {
            bool result;
            var pmh = new PitManager();
            PitManagerVoiceCmds pmvc = null;
            try
            {
                pmvc = new PitManagerVoiceCmds(ccObj.audioPlayer);
            }
            catch
            {
                if (pmvc == null)
                {
                    pmvc = new PitManagerVoiceCmds(ccObj.audioPlayer);
                }
            }

            pmvc.respond("pitstop change all tyres");
            Application.DoEvents();
            Thread.Sleep(200);
        }

    }
    [TestFixture]
    public class TestTranslateTyreTypes
    {
        /// <summary>
        /// inMenu is the list of tyre types in the game's pit menu
        /// Test the result list to see if each Pit Manager tyre compound
        /// ("Hypersoft", "Ultrasoft" etc.) matches the corresponding name
        /// used by the pit menu.
        /// </summary>
        [Test]
        public void Test_TTT_FormulaISI()
        {
            List<string> inMenu = new List<string>();
            inMenu.Add("Intermediate");
            inMenu.Add("Super Soft");
            inMenu.Add("Soft");
            inMenu.Add("Medium");
            inMenu.Add("Hard");
            inMenu.Add("Wet");

            Dictionary<string, string> result =
                PitManagerEventHandlers_RF2.TranslateTyreTypes(
                    PitManagerEventHandlers_RF2.SampleTyreTranslationDict,
                    inMenu);
            Assert.IsNotNull(result);
            //               In menu              In Pit Manager
            Assert.AreEqual("Super Soft", result["Hypersoft"]);
            Assert.AreEqual("Super Soft", result["Ultrasoft"]);
            Assert.AreEqual("Super Soft", result["Supersoft"]);
            Assert.AreEqual("Soft", result["Soft"]);
            Assert.AreEqual("Medium", result["Medium"]);
            Assert.AreEqual("Hard", result["Hard"]);
            Assert.AreEqual("Intermediate", result["Intermediate"]);
            Assert.AreEqual("Wet", result["Wet"]);
            Assert.AreEqual("Wet", result["Monsoon"]);

        }
        [Test]
        public void Test_TTT_S397_Oreca()
        {
            List<string> inMenu = new List<string>();
            inMenu.Add("Soft");
            inMenu.Add("Medium");
            inMenu.Add("Hard");
            inMenu.Add("Inter");
            inMenu.Add("Rain");

            Dictionary<string, string> result =
                PitManagerEventHandlers_RF2.TranslateTyreTypes(
                    PitManagerEventHandlers_RF2.SampleTyreTranslationDict,
                    inMenu);
            Assert.IsNotNull(result);
            Assert.AreEqual("Soft", result["Hypersoft"]);
            Assert.AreEqual("Soft", result["Ultrasoft"]);
            Assert.AreEqual("Soft", result["Supersoft"]);
            Assert.AreEqual("Soft", result["Soft"]);
            Assert.AreEqual("Medium", result["Medium"]);
            Assert.AreEqual("Hard", result["Hard"]);
            Assert.AreEqual("Inter", result["Intermediate"]);
            Assert.AreEqual("Rain", result["Wet"]);
            Assert.AreEqual("Rain", result["Monsoon"]);

        }
        [Test]
        public void Test_TTT_DallaraIndy()
        {
            List<string> inMenu = new List<string>();
            inMenu.Add("Alternates");    // Soft
            inMenu.Add("Primary");       // Hard
            inMenu.Add("Rain");

            Dictionary<string, string> result =
                PitManagerEventHandlers_RF2.TranslateTyreTypes(
                    PitManagerEventHandlers_RF2.SampleTyreTranslationDict,
                    inMenu);
            Assert.IsNotNull(result);
            Assert.AreEqual("Alternates", result["Hypersoft"]);
            Assert.AreEqual("Alternates", result["Ultrasoft"]);
            Assert.AreEqual("Alternates", result["Supersoft"]);
            Assert.AreEqual("Alternates", result["Soft"]);
            Assert.AreEqual("Alternates", result["Medium"]);
            Assert.AreEqual("Primary", result["Hard"]);
            Assert.AreEqual("Rain", result["Intermediate"]);
            Assert.AreEqual("Rain", result["Wet"]);
            Assert.AreEqual("Rain", result["Monsoon"]);

        }
        [Test]
        public void Test_TTT_S397_AMGT3()
        {
            List<string> inMenu = new List<string>();
            inMenu.Add("Soft");
            inMenu.Add("Medium");
            inMenu.Add("Hard");
            inMenu.Add("Rain");

            Dictionary<string, string> result =
                PitManagerEventHandlers_RF2.TranslateTyreTypes(
                    PitManagerEventHandlers_RF2.SampleTyreTranslationDict,
                    inMenu);
            Assert.IsNotNull(result);
            Assert.AreEqual("Soft", result["Hypersoft"]);
            Assert.AreEqual("Soft", result["Ultrasoft"]);
            Assert.AreEqual("Soft", result["Supersoft"]);
            Assert.AreEqual("Soft", result["Soft"]);
            Assert.AreEqual("Medium", result["Medium"]);
            Assert.AreEqual("Hard", result["Hard"]);
            Assert.AreEqual("Rain", result["Intermediate"]);
            Assert.AreEqual("Rain", result["Wet"]);
            Assert.AreEqual("Rain", result["Monsoon"]);

        }
        [Test]
        public void Test_TTT_S397_Megane()
        {
            List<string> inMenu = new List<string>();
            inMenu.Add("Dry");
            inMenu.Add("Rain");

            Dictionary<string, string> result =
                PitManagerEventHandlers_RF2.TranslateTyreTypes(
                    PitManagerEventHandlers_RF2.SampleTyreTranslationDict,
                    inMenu);
            Assert.IsNotNull(result);
            Assert.AreEqual("Dry", result["Hypersoft"]);
            Assert.AreEqual("Dry", result["Ultrasoft"]);
            Assert.AreEqual("Dry", result["Supersoft"]);
            Assert.AreEqual("Dry", result["Soft"]);
            Assert.AreEqual("Dry", result["Medium"]);
            Assert.AreEqual("Dry", result["Hard"]);
            Assert.AreEqual("Rain", result["Intermediate"]);
            Assert.AreEqual("Rain", result["Wet"]);
            Assert.AreEqual("Rain", result["Monsoon"]);

        }
        [Test]
        public void Test_TTT_F1_1998()
        {
            List<string> inMenu = new List<string>();
            inMenu.Add("Wet");
            inMenu.Add("Soft");
            inMenu.Add("Medium");
            inMenu.Add("Hard");
            inMenu.Add("Intermediate");

            Dictionary<string, string> result =
                PitManagerEventHandlers_RF2.TranslateTyreTypes(
                    PitManagerEventHandlers_RF2.SampleTyreTranslationDict,
                    inMenu);
            Assert.IsNotNull(result);
            Assert.AreEqual("Soft", result["Hypersoft"]);
            Assert.AreEqual("Soft", result["Ultrasoft"]);
            Assert.AreEqual("Soft", result["Supersoft"]);
            Assert.AreEqual("Soft", result["Soft"]);
            Assert.AreEqual("Medium", result["Medium"]);
            Assert.AreEqual("Hard", result["Hard"]);
            Assert.AreEqual("Intermediate", result["Intermediate"]);
            Assert.AreEqual("Wet", result["Wet"]);
            Assert.AreEqual("Wet", result["Monsoon"]);

        }
        [Test]
        public void Test_TTT_F1_Cs()
        {
            List<string> inMenu = new List<string>();
            inMenu.Add("C1");
            inMenu.Add("C2");
            inMenu.Add("C3");
            inMenu.Add("C4");
            inMenu.Add("C5");
            inMenu.Add("Intermediate");
            inMenu.Add("Wet");

            Dictionary<string, string> result =
                PitManagerEventHandlers_RF2.TranslateTyreTypes(
                    PitManagerEventHandlers_RF2.SampleTyreTranslationDict,
                    inMenu);
            Assert.IsNotNull(result);
            //             In menu      In Pit Manager
            Assert.AreEqual("C1", result["Hypersoft"]);
            Assert.AreEqual("C1", result["Ultrasoft"]);
            Assert.AreEqual("C2", result["Supersoft"]);
            Assert.AreEqual("C3", result["Soft"]);
            Assert.AreEqual("C4", result["Medium"]);
            Assert.AreEqual("C5", result["Hard"]);
            Assert.AreEqual("Intermediate", result["Intermediate"]);
            Assert.AreEqual("Wet", result["Wet"]);
            Assert.AreEqual("Wet", result["Monsoon"]);
        }
        [Test]
        public void Test_TTT_VanwallVandervellLMH()
        {
            List<string> inMenu = new List<string>();
            inMenu.Add("Soft cold");
            inMenu.Add("Soft hot");
            inMenu.Add("Medium");
            inMenu.Add("Wet");

            Dictionary<string, string> result =
                PitManagerEventHandlers_RF2.TranslateTyreTypes(
                    PitManagerEventHandlers_RF2.SampleTyreTranslationDict,
                    inMenu);
            Assert.IsNotNull(result);
            //             In menu      In Pit Manager
            Assert.AreEqual("Soft cold", result["Hypersoft"]);
            Assert.AreEqual("Soft hot", result["Soft"]);
            Assert.AreEqual("Medium", result["Medium"]);
            Assert.AreEqual("Wet", result["Wet"]);
            Assert.AreEqual("Wet", result["Monsoon"]);
        }
        [Test]
        public void Test_TTT_AcuraARX_05_DPI()
        {
            List<string> inMenu = new List<string>();
            inMenu.Add("S7M - Soft");
            inMenu.Add("S8M - Medium");
            inMenu.Add("S9M - Hard");
            inMenu.Add("P2M - Wet");

            Dictionary<string, string> result =
                PitManagerEventHandlers_RF2.TranslateTyreTypes(
                    PitManagerEventHandlers_RF2.SampleTyreTranslationDict,
                    inMenu);
            Assert.IsNotNull(result);
            //             In menu      In Pit Manager
            Assert.AreEqual("S7M - Soft", result["Soft"]);
            Assert.AreEqual("S8M - Medium", result["Medium"]);
            Assert.AreEqual("S9M - Hard", result["Hard"]);
            Assert.AreEqual("P2M - Wet", result["Wet"]);
        }
    }
    [TestFixture]
    public class TestFuelMenuParsing
    {
        [TestCase("+10.0/5", 38)]
        [TestCase("+9.7/5", 37)]
        [TestCase("65/25", 65)]
        [TestCase("+65/25", 65)]
        [TestCase("+ 5/25", 5)]
        // LMU
        [TestCase("10.0gal/5 laps", 38)]
        [TestCase("9.7gal/15 laps", 37)]
        [TestCase("65L/25 laps", 65)]
        [TestCase(" 5L/0 laps", 5)]
        public void Test_Parse(string text, int expectedFuelLevel)
        {
            var result = DataFiles.MakeFolders();
            CrewChief.Debug.UseDebugFilePaths = true;
            int fuelLevel;
            fuelLevel = Rf2PitMenuController.ParseFuelLevel(text);
            Assert.AreEqual(expectedFuelLevel, fuelLevel); // Litres
        }
    }
}
