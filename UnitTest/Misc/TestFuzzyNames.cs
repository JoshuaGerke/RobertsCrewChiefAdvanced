using NUnit.Framework;

using Assert = NUnit.Framework.Assert;
using System.IO;
using System.Collections.Generic;
using System;
using System.Linq;

using CrewChiefV4;
using static CrewChiefV4.DriverNameHelper;
using CrewChiefV4.Audio;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TestFuzzyNames
    {
        string[] availableDriverNames = null;
        [OneTimeSetUp]
        public void InitSoundCache()
        {
            SoundCache.prepareDriverNamesWithoutLoading(new DirectoryInfo(@"../../../CrewChiefV4/sounds/driver_names"), false,
                SoundCache.singleSounds,
                SoundCache.availableDriverNames,
                SoundCache.availableDriverNamesForUI
                );
            SoundCache.availableDriverNamesForUIAsArray = SoundCache.availableDriverNamesForUI.ToArray<string>();
        }
        [SetUp]
        public void Init()
        {
            useFuzzyDrivernameMatching = true;
            allowFirstNames = false;
            preferFirstNames = false;
            StreamReader sr = new StreamReader(@"../../misc/driverNameFiles.txt");
            var line = sr.ReadLine();
            List<string> aDN = new List<string>();
            while (line != null) 
            {
                if (line.Contains(".wav"))
                { // aachban.wav -> Aachban
                    line = line.Substring(0,line.Length - ".wav".Length);
                    line = char.ToUpper(line[0]) + line.Substring(1).ToLower();
                    aDN.Add(line);
                }
                line = sr.ReadLine();
            }
            availableDriverNames = aDN.ToArray();
            readRawNamesToUsableNamesFile(@"../../../CrewChiefV4/sounds/driver_names", "names.txt", lowerCaseRawNameToUsableName);
        }

        #region Test_FuzzyMatches
        // good matches...
        [TestCase("waissman", "weissmann")]
        [TestCase("gwilherme", "guilherme")]
        [TestCase("gwilherm", "guilherme")]
        [TestCase("Klemenz", "klemenz")]    // the following are taken from
        [TestCase("Holmes", "holmes")]      // sounds\driver_names\names.txt with small edits
        [TestCase("Gómez", "g�mez")]
        [TestCase("Mueller", "muller")]
        [TestCase("Clarke", "clark")]
        [TestCase("Fischer", "fischer")]
        [TestCase("Anderson", "Andersson")]
        [TestCase("Chaves", "chavez")]
        [TestCase("Christoph", "christophe")]
        [TestCase("Christopherssen", "christopherson")]
        [TestCase("Christoffersen", "christofferson")]
        [TestCase("D]Alessandro", "dalessandro")]
        [TestCase("Frank", "franke")]
        [TestCase("Low", "louw")]
        [TestCase("Laughton", "lawton")]
        [TestCase("Leigh", "lee")]
        [TestCase("McDonald", "macdonald")]
        [TestCase("Moor", "moore")]
        [TestCase("Sergent", "sargent")]
        [TestCase("Sunn", "sun")]
        [TestCase("Tomas", "thomas")]
        [TestCase("Webber", "weber")]
        [TestCase("Hundt", "hunt")]
        [TestCase("Bryan", "brien")]
        [TestCase("Clement", "clemente")]
        [TestCase("De Campos", "decampos")]
        [TestCase("Foulds", "faulds")]
        [TestCase("Folett", "follett")]
        [TestCase("Graeme", "graham")]
        [TestCase("Grey", "gray")]
        [TestCase("Healey", "haley")]
        [TestCase("Hugues", "hughes")]
        [TestCase("Larsson", "larsen")]
        [TestCase("Manson", "mansson")]
        [TestCase("Mellor", "meller")]
        [TestCase("Prince", "prinz")]
        [TestCase("Stephenson", "stevenson")]
        [TestCase("Villa", "vila")]
        [TestCase("Yung", "young")]
        [TestCase("Holtz", "holts")]
        [TestCase("Holtze", "holts")]
        [TestCase("webber", "weber")]
        [TestCase("olley", "olly")]
        [TestCase("plocke", "pollock")]
        [TestCase("müslie", "mosley")]
        [TestCase("morrie", "mori")]
        [TestCase("rahal", "rahhal")]
        [TestCase("rezende", "resende")]
        [TestCase("bouille", "boule")]
        [TestCase("senna", "senna")]

        // not so good...
        [TestCase("rits", "raats")]
        [TestCase("ride", "reid")]
        [TestCase("piper", "pepper")]
        [TestCase("burdon", "burton")]
        [TestCase("marrazzo", "merazzi")]
        [TestCase("pallotti", "pauletta")]
        [TestCase("matter", "mader")]
        [TestCase("bowles", "blues")]
        [TestCase("wilhelm", "wilhelmsson")]
        [TestCase("koep", "koop")]
        [TestCase("moura", "moreau")]
        [TestCase("manuel", "muanle")]
        [TestCase("marín", "morn�")]
        [TestCase("goerg", "gourg")]
        [TestCase("dearman", "dorman")]
        [TestCase("neubauer", "neber")]
        [TestCase("francani", "frankonia")]

        // no matches
        [TestCase("saridis", null)]
        [TestCase("snow", null)]
        [TestCase("dobal", null)]
        [TestCase("canaille", null)]
        [TestCase("staveley", null)]
        [TestCase("iusan", null)]
        [TestCase("sancio", null)]
        [TestCase("trundle", null)]
        [TestCase("etoo", null)]
        [TestCase("ditte", null)]
        [TestCase("messina", null)]
        [TestCase("tarta", null)]
        [TestCase("bekesi", null)]
        [TestCase("de pauw", null)]
        [TestCase("sachse", null)]
        [TestCase("fenstermacher", null)]
        [TestCase("boltzmann", null)]
        [TestCase("berenger", null)]
        [TestCase("prunes", null)]

        public void test_FuzzyMatches(string driverName, string wavFile)
        {
            var result = DriverNameHelper.MatchForOpponentName(driverName, availableDriverNames);
            if (wavFile == null && result.matched)
            {
                Assert.Fail($"expected no match for {driverName} but got {result.driverNameMatches[0]} " +
                    $"with confidence {result.fuzzyConfidence} from phonic match count {result.matchLevel}");
            }
            else if (result.matched)
            {
                var filename = result.driverNameMatches[0];
                if (driverName.ToLower() != filename.ToLower())
                {
                    Assert.AreEqual(wavFile.ToLower(), filename.ToLower());
                }
            }
            else if (wavFile != null)
            {
                Assert.Fail($"{driverName} returned no match, expected {wavFile}");
            }
        }
        [TestCase("foulds", "faulds")]
        [TestCase("waissman", "weissmann")]
        public void test_PhonixFuzzyMultipleMatches(string driverName, string wavFile)
        {
            var result = DriverNameHelper.PhonixFuzzyMatches(driverName, availableDriverNames, 10);
            var filename = result.driverNameMatches[0];
            if (driverName.ToLower() != filename.ToLower())
            {
                Assert.AreEqual(wavFile.ToLower(), filename.ToLower());
            }
            Assert.Greater(result.driverNameMatches.Count, 0);
            for (int i = 0; i < result.driverNameMatches.Count; i++)
            {
                driverName = result.driverNameMatches[i];
                Log.Commentary(driverName);
            }
        }
        [Test]
        [Ignore("Takes forever!")]
        public void test_unvocalisedNames()
        {
            StreamReader sr = new StreamReader(@"../../misc/unvocalisedNames.h");
            var line = sr.ReadLine();
            while (line != null)
            {
                if (line.Contains(","))
                { // "a h", null
                    var uvn = line.Split(',')[0].Trim(new char[] { ' ', '"' });
                    var result = DriverNameHelper.MatchForOpponentName(uvn, availableDriverNames);
                    if (result.matched)
                    {
                        Console.WriteLine($"'{uvn}' fuzzy matched with {result.driverNameMatches[0]}");
                    }
                    else
                    {
                        Console.WriteLine($"'{uvn}' didn't match");
                    }
                }
                line = sr.ReadLine();
            }
        }
        #endregion Test_FuzzyMatches

        #region Test_getUsableDriverName
        [TestCase("J1mBr1tt0n", "brltton", null)] // note 1 is substituted for L (not I)
        [TestCase("JimBri5tol", "bristol", "bristol")]
        [TestCase("JimBritton", "britton", "britton")]
        [TestCase("jimBRITTON", "britton", "britton")]
        [TestCase("jim BRITTON", "britton", "britton")]
        [TestCase("jim BRITTON UK", "britton", "britton")]
        [TestCase("JIM BRITTON", "britton", "britton")]
        [TestCase("JIM_BRITTON!!!", "britton", "britton")]
        [TestCase("BRITTON", "britton", "britton")]
        [TestCase("JimBritton!!!!", "britton", "britton")]
        [TestCase("Jim Britton!!!!", "britton", "britton")]
        [TestCase("jim vanderbritton", "vanderbritton", null)]
        [TestCase("Jim_Van_Der_Britton", "van der britton", "van der drift")]   // !!!!
        [TestCase("jim_von_britton", "von britton", null)]
        [TestCase("Jim Van Der Britton", "van der britton", "van der drift")]   // !!!!
        [TestCase("JimVanDerBritton", "van der britton", "van der drift")]  // !!!!
        [TestCase("Jim McShit", "mc shit", null)]
        [TestCase("Jim Mcshit", "mcshit", null)]
        [TestCase("Jim MacShit", "mac shit", null)]
        [TestCase("Dave Mackay", "mackay", "mackay")]
        [TestCase("bobbyMoore", "moore", "moore")]
        [TestCase("Jim Britton uk", "britton", "britton")]
        [TestCase("JimBritton UK", "britton", "britton")]
        [TestCase("Jim Britton [UK]", "britton", "britton")]
        [TestCase("Jim Britt0n 69 UK", "britton", "britton")]
        [TestCase("Jim Britton 69", "britton", "britton")]
        [TestCase("Jim Britton Junior UK", "britton", "britton")]
        [TestCase("Jim Britton Junior", "britton", "britton")]
        [TestCase("Jim Britton jr", "britton", "britton")]
        [TestCase("Jim Junior", "junior", "junior")]
        [TestCase("Jim LeClerc", "le clerc", "le clercq")]
        [TestCase("Jim le Clerc", "le clerc", "le clercq")]
        [TestCase("Charles Leclerc", "leclerc", "leclerc")]
        [TestCase("Jim Ng", "ng", "ng")]
        [TestCase("jim", "jim", "jm")]
        [TestCase("a", null, null)]
        [TestCase("ba", "ba", "bao")]
        [TestCase("aaaa", "aaaa", null)]
        [TestCase("345hf9237f", "hff", "hoff")]
        [TestCase("9999", null, null)]
        [TestCase("Jim Britton DIV 2", "britton", "britton")]
        [TestCase("jimBritton DIV 2", "britton", "britton")]
        [TestCase("jimBritton pro", "britton", "britton")]
        [TestCase("jimBritton proam", "britton", "britton")]
        [TestCase("jim DIV 2", "jim", "jm")]
        [TestCase("Jim Britton division 2", "britton", "britton")]
        [TestCase("Jim [da man] Britton", "britton", "britton")]
        [TestCase("JimBritton {some nonsense} UK", "britton", "britton")]
        [TestCase("Britton {some other nonsense} UK", "britton", "britton")]
        [TestCase("Jim <boss> Britton (is ace)", "britton", "britton")]
        // [TestCase("Jim <boss> Britton [][]<>{} ()", "britton")]  fails because it removes everything between the first < and the last >
        [TestCase("Jim Britton [dude]", "britton", "britton")]
        [TestCase("<ejit> JimBritton", "britton", "britton")]
        [TestCase("<ejit> Jim Britton [dipstick]{smelly}", "britton", "britton")]
        [TestCase("Jim   ARG", "jim", "jm")]
        public void Test_getUsableDriverName(string rawDriverName,
            string usableDriverNameForSRE, string usableDriverNameForAudio)
        {
            var driverNameForAudio = getUsableDriverName(rawDriverName);
            var driverNameForSRE = getUsableDriverNameForSRE(rawDriverName);
            Assert.AreEqual(usableDriverNameForAudio, driverNameForAudio);
            Assert.AreEqual(usableDriverNameForSRE, driverNameForSRE);
        }
        #endregion Test_getUsableDriverName

        #region Test_getUsableDriverNameCaching
        // Ignore [TestCase("michael holtz", "holts", "holts", 1)]
        [TestCase("Patricio Javier Alzamora", "alzamora", "alzamora", 1)] // Clause 1: A straight match
        // (Clause 2 is an error condition I can't see a way to generate)
        [TestCase("webber232", "weber", "weber", 1)]         // Clause 3: Using mapped driver name for cleaned up driver name
        [TestCase("andy weber", "weber", "weber", 1)]        // Clause 4: We have a sound file for the driver last name
        // Ignore [TestCase("jim whatshisname", "whats his name", "whats his name", 1)]       // Clause 6: Using mapped driver name for cleaned up driver (last) name
        [TestCase("andy wexxxer", "wexxxer", null, 0)]    // Clause 7a: Using unmapped driver last name for raw driver name
        [TestCase("weyyyer", "weyyyer", null, 0)]         // Clause 7b: Using unmapped driver name for raw driver name

        // Rerun the same cases and no new names should be added
        // ...assuming the tests are run in this order
        // Ignore [TestCase("michael holtz", "holts", "holts", 0)]
        [TestCase("Patricio Javier Alzamora", "alzamora", "alzamora", 0)] // Clause 1: A straight match
        // (Clause 2 is an error condition I can't see a way to generate)
        //[TestCase("webber232", "weber", "webber", 0)]         // Clause 3: Using mapped driver name for cleaned up driver name
        [TestCase("andy weber", "weber", "weber", 0)]        // Clause 4: We have a sound file for the driver last name
        // Ignore [TestCase("jim whatshisname", "whats his name", "whats his name", 0)]       // Clause 6: Using mapped driver name for cleaned up driver (last) name
        [TestCase("andy wexxxer", "wexxxer", null, 0)]    // Clause 7a: Using unmapped driver last name for raw driver name
        [TestCase("weyyyer", "weyyyer", null, 0)]         // Clause 7b: Using unmapped driver name for raw driver name

        public void Test_getUsableDriverNameCaching(string rawDriverName,
            string usableDriverNameForSRE,
            string usableDriverNameForAudio,
            int adds1to_usableNamesForSession)
        {
            int initialCacheSize = GetSize_usableNamesForSession();
            var driverName = getUsableDriverName(rawDriverName);
            Assert.AreEqual(usableDriverNameForAudio, driverName);
            var driverSurname = getUsableDriverNameForSRE(rawDriverName);
            Assert.AreEqual(usableDriverNameForSRE, driverSurname);
            Assert.AreEqual(initialCacheSize + adds1to_usableNamesForSession,
                GetSize_usableNamesForSession());
        }
        #endregion Test_getUsableDriverNameCaching
    }
}
