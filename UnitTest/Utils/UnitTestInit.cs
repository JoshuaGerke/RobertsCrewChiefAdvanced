using CrewChiefV4;

using NUnit.Framework;

using NUnit.Framework;

using System;
using System.IO;
using System.Text;
using System.Web.UI.WebControls;
using System.Windows.Forms;

// Assembly-wide: legacy fixtures also live outside the UnitTest namespace.
    [SetUpFixture]
    public sealed class CrewChiefRuntimeSetup
    {
        [OneTimeSetUp]
        public void InitializeBeforeAnyFixture()
        {
            if (CrewChiefV4.CrewChief.Debug == null)
                CrewChiefV4.CrewChief.Debug = new CrewChiefV4.Debugging();
            PrepareHeadlessProfile();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void PrepareHeadlessProfile()
        {
            // Same headless audio choice as the legacy test setup, before any AI fixture runs.
            // WINDOWS_V06_TEST supplies RCCA_DATA_ROOT to keep the driver's profile isolated.
            CrewChiefV4.DataFiles.MakeFolders();
            CrewChiefV4.UserSettings.GetUserSettings().setProperty("use_naudio", false);
            // Legacy speech tests exercise Crew Chief messages. AI fixtures opt in explicitly.
            CrewChiefV4.UserSettings.GetUserSettings().setProperty("enable_ai_team", false);
            // Tests prüfen deutsche Funktexte; unabhängig von der Windows-Sprache des Testrechners
            CrewChiefV4.UserSettings.GetUserSettings().setProperty("ai_team_radio_language", "de");
            CrewChiefV4.AiTeam.RadioLanguage.Current = CrewChiefV4.AiTeam.RadioLanguage.German;
        }
    }
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

