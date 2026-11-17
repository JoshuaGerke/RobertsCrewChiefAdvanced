using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Register der Funk-Abwechslung (Rob 17.11.2026): Jede feste Phrasenliste der Crew hat in jeder Sprache mindestens vier
    /// verschiedene Formulierungen. Neue Listen fallen automatisch unter diese Prüfung.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class RadioVarietyRegisterTests
    {
        // Feste Funkcodes (Flaggen sind indexbasiert) und Sammellisten aus mehreren Einzellisten.
        private static readonly HashSet<string> Exempt = new HashSet<string>(StringComparer.Ordinal)
        {
            "FlagWatch.Phrases", "CrewSpeechPolicy.IncidentPhrases", "CrewSpeechPolicy.TrackLimitPhrases", "CrewSpeechPolicy.WeatherPhrases",
            "LiveWarningDispatcher.MikPhrases", "RaceTrafficWatch.PreparedPhrases", "ThermalWatch.PreparedPhrases", "RaceTrafficWatch.DistancePhrases"
        };

        private static IEnumerable<PropertyInfo> PhraseLists() =>
            typeof(RadioVariants).Assembly.GetTypes()
                .Where(t => t.Namespace == "CrewChiefV4.AiTeam")
                .SelectMany(t => t.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(p => p.PropertyType == typeof(string[]) && p.GetIndexParameters().Length == 0 && p.Name.EndsWith("Phrases", StringComparison.Ordinal) &&
                    !Exempt.Contains(p.DeclaringType.Name + "." + p.Name));

        [Test]
        public void Every_fixed_phrase_list_has_at_least_four_variants_in_every_language()
        {
            var before = RadioLanguage.Current;
            var tooFew = new List<string>();
            try
            {
                foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
                {
                    RadioLanguage.Current = lang;
                    foreach (var p in PhraseLists())
                    {
                        string[] list;
                        try { list = (string[])p.GetValue(null); } catch (Exception) { continue; }
                        int n = list == null ? 0 : list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().Count();
                        if (n < RadioVariants.Minimum) tooFew.Add(lang + " " + p.DeclaringType.Name + "." + p.Name + " = " + n);
                    }
                }
            }
            finally { RadioLanguage.Current = before; }
            Assert.IsEmpty(tooFew, "Rob 17.11.: mindestens vier Formulierungen je Ansage:\n" + string.Join("\n", tooFew));
        }

        [Test]
        public void Random_choice_never_repeats_the_same_variant_twice_in_a_row()
        {
            bool was = RadioVariants.Deterministic;
            try
            {
                RadioVariants.Deterministic = false;
                string last = null; var seen = new HashSet<string>();
                for (int i = 0; i < 200; i++)
                {
                    string s = RadioVariants.Pick("register-test", "a", "b", "c", "d");
                    Assert.AreNotEqual(last, s, "keine direkte Wiederholung");
                    last = s; seen.Add(s);
                }
                Assert.AreEqual(4, seen.Count, "alle Formulierungen kommen vor");
                RadioVariants.Deterministic = true;
                Assert.AreEqual("a", RadioVariants.Pick("register-test", "a", "b", "c", "d"));
            }
            finally { RadioVariants.Deterministic = was; }
        }
    }
}
