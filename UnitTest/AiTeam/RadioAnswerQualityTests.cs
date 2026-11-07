using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class RadioAnswerQualityTests
    {
        [Test]
        public void Incomplete_lowercase_non_German_tail_is_removed_after_complete_answer()
        {
            Assert.AreEqual("Die Temperaturen liegen bei 70 bis 79 Grad.",
                RadioAnswerQuality.Clean("Die Temperaturen liegen bei 70 bis 79 Grad. eel parem"));
        }

        [TestCase("Vorsicht. sofort an die Box")]
        [TestCase("Reifen werden heiß. schone die Vorderreifen")]
        [TestCase("Das passt. weiter so")]
        [TestCase("Noch 18,5 Liter")]
        [TestCase("Der Abstand beträgt 0.5 Sekunden")]
        [TestCase("K. Testov liegt vor dir.")]
        [TestCase("Vorsicht. bitte sofort stoppen")]
        [TestCase("Boxeneinfahrt. limiter einschalten")]
        public void Valid_instructions_numbers_and_initials_are_preserved(string text)
        { Assert.AreEqual(text, RadioAnswerQuality.Clean(text)); }

        [Test]
        public void Foreign_script_driver_name_requires_actual_roster_evidence()
        {
            const string name = "Тестов";
            Assert.AreEqual(name + " liegt vor dir.", RadioAnswerQuality.Clean(name + " liegt vor dir.", new[] { name }));
            Assert.IsNull(RadioAnswerQuality.Clean(name + " liegt vor dir."));
            Assert.AreEqual("Die Reifen sind warm.", RadioAnswerQuality.Clean("Die Reifen sind warm. བཀྲ་ཤིས"));
        }

        [Test]
        public void German_accents_and_known_lowercase_names_are_preserved()
        {
            Assert.AreEqual("Öl, Kühlung und Aufhängung.", RadioAnswerQuality.Clean("Öl, Kühlung und Aufhängung."));
            Assert.AreEqual("Vorn. testfahrer", RadioAnswerQuality.Clean("Vorn. testfahrer", new[] { "testfahrer" }));
        }
    }
}
