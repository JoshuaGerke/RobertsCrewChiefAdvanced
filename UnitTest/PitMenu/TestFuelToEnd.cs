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
namespace UnitTest.PitMenu
{
    [NUnit.Framework.Ignore("Ain't running")]
    [TestFixture]
    public class TestFuelToEnd
    {
        private static MainWindow hwnd;
        private static CrewChief ccObj;

        [OneTimeSetUp]
        public static void ClassInitialize()
        {
            new CrewChiefV4.UserSettings(); // Avoids null pointer errors all over the shop
            hwnd = new MainWindow();
            ccObj = hwnd.crewChief;
            MainWindow.instance = hwnd;
            MainWindow.instance.crewChief = ccObj;
            CrewChief.gameDefinition.gameEnum = GameEnum.LMU;
        }
        [TestCase(50, 5, 1000, 45)]
        public void TestMethod1(float fuelCapacity, float currentFuel, float maxVE, int litresNeeded)
        {
            CrewChief.gameDefinition = new GameDefinition();
            CrewChief.gameDefinition.gameEnum = GameEnum.LMU;

            var result = PitFuelling.fuelToEnd(fuelCapacity, currentFuel, maxVE);
            //result.Should().Be(litresNeeded);
        }
    }
}
