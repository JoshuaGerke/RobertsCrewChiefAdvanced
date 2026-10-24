using CrewChiefV4;

using NUnit.Framework;

using NUnit.Framework;

using System;
using System.IO;
using System.Text;
using System.Web.UI.WebControls;
using System.Windows.Forms;

namespace UnitTest.Misc
{
    /// <summary>
    /// Capture Console.WriteLines and Logs in ConsoleCapture.Contents
    /// </summary>
    static class ConsoleCapture
    {
        private static StringBuilder ConsoleOutput { get; set; } = new StringBuilder();

        public static void Clear()
        {
            ConsoleOutput.Clear();
        }

        public static string Contents => ConsoleOutput.ToString();

        static ConsoleCapture()
        {
            Console.SetOut(new StringWriter(ConsoleOutput)); // Associate StringBuilder with StdOut
        }
    }
    /// <summary>
    /// Universal function to set up Crew Chief so that tests can run
    /// </summary>
    class setUp
    {
        internal void unitTests()
        {
#if DEBUG
            //Application.SetCompatibleTextRenderingDefault(false);
            ConsoleCapture.Clear(); // Clear text from any previous text runs
            UnitTest.CrewChiefInitialise();
            UnitTest.Active = true;
            UnitTest.Debugging = System.Diagnostics.Debugger.IsAttached;
            CrewChief.gameDefinition = new GameDefinition();
            UserSettings.GetUserSettings().setProperty("use_naudio", false);
#else
            NUnit.Framework.Assert.IsTrue(false, "\n\n!!! MUST BE BUILT FOR DEBUG !!!\n\n");
#endif
        }
    }

    [SetUpFixture]
    public class NUnitSetUpClass
    {
        [OneTimeSetUp]
        /// <summary>
        /// Set up Crew Chief before all NUnit tests
        /// </summary>
        public void RunBeforeAnyTests()
        {
            var s = new setUp();
            s.unitTests();
        }
    }
    //[TestFixture]
    ///// <summary>
    ///// Set up Crew Chief before all (Microsoft) Unit tests
    ///// </summary>
    //public class UnitSetUpClass
    //{
    //    [AssemblyInitialize]
    //    public static void AssemblyInitialise(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext testContext)
    //    {
    //        var s = new setUp();
    //        s.unitTests();
    //    }
    //}
}

