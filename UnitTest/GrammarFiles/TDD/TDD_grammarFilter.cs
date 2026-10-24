using NUnit.Framework;
using System.IO;

using CrewChiefV4.GrammarFiles;

namespace TDD_grammarFilter
{
    [TestFixture]
    public class TDD_grammarFilter
    {
        static string cwd = Directory.GetCurrentDirectory();
        static string grammarFile = Path.Combine(cwd, @"..\..\..\CrewChiefV4\ui_text\SpeechGrammars\EN-GB\GameCommands\Pitstop_template.grxml");
        const string testGrammar =
@"<?gamefilter IRACING RF2 ACC
IR_RF2_ACC_1
?>
";
        public TDD_grammarFilter()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [Test]
        public void TestgrammarFilter()
        {
            string result = WorkingFiles.GrammarFilter(testGrammar, new string[] {"RF2"});
            Assert.AreEqual("\nIR_RF2_ACC_1\n\n", result);
            result = WorkingFiles.GrammarFilter(testGrammar, new string[] { "ACS" });
            Assert.IsTrue(result.Contains("<?gamefilter IRACING RF2 ACC"));
        }
        [Test]
        public void TestgrammarFilterRealFile()
        {
            // Simple check it doesn't blow up on a real file
            try
            {
                string realGrammar = File.ReadAllText(grammarFile);
                string result = WorkingFiles.GrammarFilter(realGrammar, new string[] { "ACC" });
                // Check this has been filtered out
                Assert.IsFalse(result.Contains("<?gamefilter RACE_ROOM ACC"));

                // Check this isn't filtered out
                result = WorkingFiles.GrammarFilter(realGrammar, new string[] { "IRACING", "RF2" });
                Assert.IsTrue(result.Contains("<?gamefilter RACE_ROOM ACC"));
            }
            catch
            {
                Assert.Fail();
            }
        }
    }
}
