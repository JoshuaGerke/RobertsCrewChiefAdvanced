using System;
using System.Linq;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Funksprache Deutsch/Englisch/Spanisch und Versionsvergleich des Updates.</summary>
    [TestFixture, Category("AiTeam")]
    public class RadioLanguageTests
    {
        [TearDown]
        public void BackToGerman() { RadioLanguage.Current = RadioLanguage.German; }

        [TestCase("de", "de")] [TestCase("en", "en")] [TestCase("es", "es")] [TestCase("EN", "en")]
        public void Explicit_language_is_kept(string setting, string expected)
        {
            Assert.AreEqual(expected, RadioLanguage.Resolve(setting));
        }

        [Test]
        public void Text_and_numbers_follow_the_language()
        {
            RadioLanguage.Current = RadioLanguage.English;
            Assert.AreEqual("left", RadioLanguage.T("links", "left", "izquierda"));
            Assert.AreEqual("0.8", RadioLanguage.Num(0.8, "0.0"));
            RadioLanguage.Current = RadioLanguage.Spanish;
            Assert.AreEqual("izquierda", RadioLanguage.T("links", "left", "izquierda"));
            Assert.AreEqual("0,8", RadioLanguage.Num(0.8, "0.0"));
            RadioLanguage.Current = RadioLanguage.German;
            Assert.AreEqual("0,8", RadioLanguage.Num(0.8, "0.0"));
        }

        [TestCase("en")] [TestCase("es")] [TestCase("de")]
        public void Every_language_has_a_complete_phrase_catalog(string language)
        {
            RadioLanguage.Current = language;
            var catalog = RadioPhraseCatalog.Current;
            string line = catalog.Format("fuel-range", 0, "18", "8");
            StringAssert.Contains("18", line);
            StringAssert.Contains("8", line);
            Assert.AreEqual(RadioPhraseCatalogFor(RadioLanguage.German).Count("pass-first"), catalog.Count("pass-first"));
        }

        private static RadioPhraseCatalog RadioPhraseCatalogFor(string language)
        {
            string before = RadioLanguage.Current;
            RadioLanguage.Current = language;
            var catalog = RadioPhraseCatalog.Current;
            RadioLanguage.Current = before;
            return catalog;
        }

        [Test]
        public void Prompts_ask_for_the_radio_language_but_stay_german_by_default()
        {
            string german = AiPersonas.SystemPrompt(TeamMember.Mik, "Rob", false);
            StringAssert.Contains("Sprich durchgehend Deutsch", german);
            StringAssert.DoesNotContain("AUSGABESPRACHE", german);

            RadioLanguage.Current = RadioLanguage.English;
            string english = AiPersonas.SystemPrompt(TeamMember.Mik, "Rob", false);
            StringAssert.Contains("Sprich durchgehend Englisch", english);
            StringAssert.Contains("AUSGABESPRACHE", english);
            StringAssert.DoesNotContain("Sprich durchgehend Deutsch", english);
            StringAssert.Contains("ausschließlich auf Englisch", AiPersonas.ProactivePrompt);
            Assert.AreEqual(1, CountOf(english, "AUSGABESPRACHE"), "Sprachvorgabe nur einmal");

            RadioLanguage.Current = RadioLanguage.Spanish;
            StringAssert.Contains("ausschließlich auf Spanisch", AiPersonas.SessionBriefingPrompt);
        }

        private static int CountOf(string text, string part) => (text.Length - text.Replace(part, "").Length) / part.Length;

        [Test]
        public void Radio_clearance_by_the_ai_is_removed_in_every_language()
        {
            Assert.AreEqual("", AiPersonas.CleanForRadio("Clear on the left.", 200, true));
            Assert.AreEqual("", AiPersonas.CleanForRadio("Libre por la izquierda.", 200, true));
            Assert.AreEqual("", AiPersonas.CleanForRadio("Links frei.", 200, true));
            Assert.AreEqual("Gap ahead is 0.8 seconds.", AiPersonas.CleanForRadio("Gap ahead is 0.8 seconds.", 200, true));
        }

        [Test]
        public void Sara_pack_exists_in_every_language_with_the_same_clips()
        {
            var german = SaraSpotterPack.Phrases;
            foreach (var language in new[] { RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                var pack = SaraSpotterPack.Phrases;
                CollectionAssert.AreEquivalent(german.Keys, pack.Keys);
                Assert.IsFalse(pack.Values.SelectMany(v => v).Any(p => p.Contains("Links") || p.Contains("Rechts")), language);
            }
        }

        [Test]
        public void German_numbers_are_only_spelled_out_for_german_radio()
        {
            StringAssert.Contains("Komma", GermanRadioText.Prepare("Noch 12,5 Liter."));
            RadioLanguage.Current = RadioLanguage.English;
            Assert.AreEqual("12.5 litres left.", GermanRadioText.Prepare("12.5 litres left."));
        }

        [TestCase("v0.9.1", "v0.9-alpha3", true)]
        [TestCase("v0.9.1", "v0.9.1", false)]
        [TestCase("v0.9.1", "v0.10.0", false)]
        [TestCase("v0.10.0", "v0.9.9", true)]
        [TestCase("v1.0.0", "v1.0.0-beta", true)]
        [TestCase("aktuell", "v0.9.1", false)]
        public void Update_is_offered_only_for_a_newer_version(string remote, string installed, bool expected)
        {
            Assert.AreEqual(expected, RrcaUpdater.IsNewer(remote, installed));
        }
    }
}
