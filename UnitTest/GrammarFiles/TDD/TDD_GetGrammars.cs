using CrewChiefV4.GrammarFiles;
using NUnit.Framework;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Assert = NUnit.Framework.Assert;
using TestContext = NUnit.Framework.TestContext;

namespace UnitTest.GrammarFiles.TDD
{
    /// <summary>
    /// Summary description for GetGrammars
    /// </summary>
    [TestFixture]
    public class TDD_GetGrammars
    {
        /*static string TempGrammarsPath = Path.Combine(Environment.GetFolderPath(
                   Environment.SpecialFolder.MyDocuments), "CrewChiefV4",
                   "SREtemporaryGrammars");
        */
        /*
        public GetGrammars()
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
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion
        */

        // Use ClassCleanup to run code after all tests in a class have run
        [ClassCleanup()]
        public static void MyClassCleanup() 
        {
            string path = WorkingFiles.grammarFilePath("UnitTest_GrammarList.xml ");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        [Test]
        public void TestSomethingReturned()
        {
            var files = WorkingFiles.GetGrammarFiles("RF2_64BIT");
            Assert.That(files, Has.Count.GreaterThan(0));
        }
        [Test]
        public void TestNoSuchGame()
        {
            List<string> expected = new List<string>
            {
                @"EN-GB\GameCommands\Pitstop_template.grxml"
            };
            var files = WorkingFiles.GetGrammarFiles("NO_SUCH_GAME");
            Assert.That(expected, Is.EquivalentTo(files));
        }
        [Test]
        //[ExpectedException(typeof(TypeInitializationException),"Log exception")]
        public void _CreateXMLfile()
        {
            List<string> expected = new List<string>
            {
                @"EN-GB\GameCommands\Pitstop_template.grxml"
            };
            System.Xml.Serialization.XmlSerializer writer =
                new System.Xml.Serialization.XmlSerializer(typeof(List<string>));

            string path = WorkingFiles.grammarFilePath("UnitTest_GrammarList.xml ");
            System.IO.FileStream file = System.IO.File.Create(path);

            writer.Serialize(file, expected);
            file.Close();
        }
        [Test]
        public void TestDummyGame()
        {
            List<string> expected = new List<string>
            {
                @"EN-GB\GameCommands\Pitstop_template.grxml"
            };
            WorkingFiles.CopyGrammarFiles("EN-GB");
            var files = WorkingFiles.GetGrammarFiles("UnitTest");
            Assert.That(expected, Is.EquivalentTo(files));
        }
        [Test]
        public void TestActualGame()
        {
            List<string> expected = new List<string>
            {
                @"EN-GB\GameCommands\Pitstop_template.grxml"
            };
            WorkingFiles.CopyGrammarFiles("EN-GB");
            var files = WorkingFiles.GetGrammarFiles("RF2_64BIT");
            Assert.That(expected, Is.EquivalentTo(files));
        }

    }
}
