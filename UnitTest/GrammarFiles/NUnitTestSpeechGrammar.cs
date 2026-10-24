using NUnit.Framework;
using SpeechGrammar;
using System;
using System.IO;

using CrewChiefV4.GrammarFiles;

namespace UnitTestSpeechGrammar
{
    [TestFixture]
    public class TestPitGrammar
    {
        static string cwd = Directory.GetCurrentDirectory();// Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        static string grammarFile = Path.Combine(cwd, @"CrewChiefV4\ui_text\SpeechGrammars\EN-GB\GameCommands\Pitstop_template.grxml");
        static string NumberGrammarFile = Path.Combine(cwd, @"UnitTest\cc_sample_grammar.xml");
        static string NumberTestGrammarFile = Path.Combine(cwd, @"CrewChiefV4\ui_text\SpeechGrammars\EN-GB\GameCommands\number_test_grammar.xml");
        static string TestStringsFile = Path.Combine(cwd, @"CrewChiefV4\ui_text\SpeechGrammars\EN-GB\GameCommands\TestStrings.txt");
        [Test]
        [TestCase("Pitstop clear tyres", "PIT_STOP_CLEAR_TYRES")]
        [TestCase("Box clear tyres", "PIT_STOP_CLEAR_TYRES")]
        [TestCase("Pitstop soft tyres", "PIT_STOP_SOFT_TYRES")]
        [TestCase("Pitstop soft", "PIT_STOP_SOFT_TYRES")]
        [TestCase("Pit stop wet tyres", "PIT_STOP_WET_TYRES")]
        [TestCase("Pit stop add five", "PIT_STOP_ADD_LITRES 5")]
        [TestCase("Pit stop add twelve litres", "PIT_STOP_ADD_LITRES 12")]
        [TestCase("Pit stop add one", "PIT_STOP_ADD_LITRES 1")]
        [TestCase("Pit stop clear fuel", "PIT_STOP_CLEAR_FUEL")]
        public void TestSampleGrammar(string cmd, string semantic)
        {
            var srO = new TestSpeechGrammar.SR();
            SystemGrammarFiles.LoadGrammar(srO.sr, grammarFile, new string[] { "RF2", "METRIC" });
            var response = srO.testGrammar(cmd);
            Assert.AreEqual(semantic, response);
        }
        [Test]
        [TestCase("prime tyres", "Pitstop_PrimeTyres")]
        [TestCase("tyre set", "Pitstop_TyreSet")] // RACE_ROOM only
        [TestCase("refuel", "Pitstop_Refuel")] // RACE_ROOM only
        [TestCase("dont refuel", "Pitstop_DontRefuel")] // RACE_ROOM only
        [TestCase("Pit stop add one point five gallons", "PIT_STOP_ADD_GALLONS 01.05")] // not METRIC
        [TestCase("Pit stop add five gallons", "PIT_STOP_ADD_GALLONS 5")] // not METRIC
        [TestCase("Pit stop add five litres", "PIT_STOP_ADD_GALLONS 5", "US")] // not METRIC
        public void TestSampleGrammarFails(string cmd, string semantic, string units = "METRIC")
        {
            var srO = new TestSpeechGrammar.SR();
            SystemGrammarFiles.LoadGrammar(srO.sr, grammarFile, new string[] { "RF2", units });
            var response = srO.testGrammar(cmd);
            Assert.AreNotEqual(semantic, response);
        }

        [Test]
        [TestCase("Pit stop add fifteen", "PIT_STOP_ADD_GALLONS 15")]
        [TestCase("Pit stop add one oh five", "PIT_STOP_ADD_GALLONS 105")]
        [TestCase("Pit stop add oh oh seven", "PIT_STOP_ADD_GALLONS 007")]
        [TestCase("Pit stop add five point oh seven", "PIT_STOP_ADD_GALLONS 5.07")]
        [TestCase("Pit stop add five point oh oh seven", "PIT_STOP_ADD_GALLONS 5.007")]
        [TestCase("Pit stop add five point oh oh oh seven", "PIT_STOP_ADD_GALLONS 5.0007")]
        [TestCase("Pit stop add five zero three point oh oh seven", "PIT_STOP_ADD_GALLONS 503.007")]
        [TestCase("Pit stop add point oh seven", "PIT_STOP_ADD_GALLONS 0.07")]
        public void TestSampleGrammarNumber(string cmd, string semantic)
        {
            var srO = new TestSpeechGrammar.SR();
            SystemGrammarFiles.LoadGrammar(srO.sr, grammarFile, new string[] { "RF2", "US" });
            var response = srO.testGrammar(cmd);
            Assert.AreEqual(semantic, response);
        }
        [Test]
        [TestCase("Pit stop refuel", "PIT_STOP_REFUEL")]
        public void TestSampleGrammarFilter(string cmd, string semantic)
        {
            var srO = new TestSpeechGrammar.SR();
            SystemGrammarFiles.LoadGrammar(srO.sr, grammarFile, new string[] { "RACE_ROOM", "METRIC" });
            var response = srO.testGrammar(cmd);
            Assert.AreEqual(semantic, response);
        }
        [Test]
        [TestCase("Pitstop_template.grxml", "RF2_PM_TestStrings.txt", new string[] { "RF2", "METRIC", "CELSIUS" })]
        [TestCase("Pitstop_template.grxml", "RF2_PM_TestStrings_US.txt", new string[] { "RF2", "US", "FAHRENHEIT" })]
        [TestCase("RF2.grxml", "RF2_TestStrings.txt", new string[] { "RF2", "METRIC", "CELSIUS" })]
        [TestCase("Pitstop_template.grxml", "IR_PM_TestStrings.txt", new string[] { "IRACING", "METRIC", "CELCIUS" })]
        [TestCase("Pitstop_template.grxml", "ACC_PM_TestStrings.txt", new string[] { "ACC", "METRIC", "CELCIUS" })]
        [TestCase("Pitstop_template.grxml", "R3E_PM_TestStrings.txt", new string[] { "RACE_ROOM", "METRIC", "CELCIUS" })]
        [TestCase("Pitstop_template.grxml", "IR_PM_WrongUnits_TestStrings.txt", new string[] { "IRACING", "METRIC", "CELCIUS", "ALLOW_WRONG_UNITS" })]
        public void TestPitGrammarCommandList(string grammarFile, string testStrings, string[] filters)
        {
            testStrings = Path.Combine(cwd, @"CrewChiefV4\ui_text\SpeechGrammars\EN-GB\GameCommands", testStrings);
            grammarFile = Path.Combine(cwd, @"CrewChiefV4\ui_text\SpeechGrammars\EN-GB\GameCommands", grammarFile);
            Assert.IsFalse(TestSampleGrammarCommandList(grammarFile, testStrings, filters));
        }
        internal bool TestSampleGrammarCommandList(string grammarFile, string testStrings, string[] filters)
        {
            bool failed = false;
            var srO = new TestSpeechGrammar.SR();
            SystemGrammarFiles.LoadGrammar(srO.sr, grammarFile, filters);
            //var srM = new TestSpeechGrammar.SR();
            // won't build   MicrosoftGrammarFiles.LoadGrammar(srM.sr, grammarFile, filters);
            using (var sr = new StreamReader(testStrings))
            {
                string line = sr.ReadLine(); ;
                while (line != null)
                {
                    var words = line.Split(',');
                    if (words.Length == 2)
                    {
                        var semantic = words[0];
                        if (semantic == "_NO_RESPONSE_EXPECTED_")
                        {
                            semantic = null;
                        }
                        var cmd = words[1];

                        var response = srO.testGrammar(cmd);
                        //Assert.AreEqual(semantic, response);
                        if (semantic != response)
                        {
                            Console.WriteLine($"Expected '{semantic}' got '{response}' to '{cmd}'");
                            failed = true;
                        }
                    }
                    line = sr.ReadLine();
                }
            }
            return failed;
        }
    }
    [TestFixture]
    public class TestQueryGrammar
    {
        static string cwd = Directory.GetCurrentDirectory();
        [Test]
        [TestCase("Queries_template.grxml", "GameQueries_TestStrings.txt", new string[] { "Blah" })]
        public void TestSampleGrammarCommandList(string grammarFile, string testStrings, string[] filters)
        {
            testStrings = Path.Combine(cwd, @"CrewChiefV4\ui_text\SpeechGrammars\EN-GB\GameQueries", testStrings);
            grammarFile = Path.Combine(cwd, @"CrewChiefV4\ui_text\SpeechGrammars\EN-GB\GameQueries", grammarFile);
        }
    }
}
