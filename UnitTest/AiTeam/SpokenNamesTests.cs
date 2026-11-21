using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class SpokenNamesTests
    {
        [Test]
        public void Shouted_field_name_is_spoken_as_a_name()
        {
            // RaceRoom Mantorp 21.11.: „Du hast 7,3 Sekunden Vorsprung auf JAH TESÏ.“ wurde buchstabiert.
            var field = new[] { "JAH TESÏ", "Max Becker" };
            Assert.AreEqual("Du hast 7,3 Sekunden Vorsprung auf Jah Tesï.",
                SpokenNames.Prepare("Du hast 7,3 Sekunden Vorsprung auf JAH TESÏ.", field));
            Assert.AreEqual("Gelb! Langsam, nicht überholen. Das ist Jah Tesï.",
                SpokenNames.Prepare("Gelb! Langsam, nicht überholen. Das ist JAH TESÏ.", field));
        }

        [Test]
        public void Mixed_names_keep_their_normal_words()
        {
            Assert.AreEqual("Max Verstappen", SpokenNames.Speakable("Max VERSTAPPEN"));
            Assert.AreEqual("O'Neil Müller-Lüdenscheidt", SpokenNames.Speakable("O'NEIL MÜLLER-LÜDENSCHEIDT"));
            Assert.AreEqual("Henry Ford III", SpokenNames.Speakable("HENRY FORD III"));
            Assert.IsNull(SpokenNames.Speakable("Max Becker"), "Normal geschriebene Namen bleiben unberührt.");
            Assert.IsNull(SpokenNames.Speakable("J. Becker"));
        }

        [Test]
        public void Only_known_names_change_never_other_capitals()
        {
            // Klassen und Abkürzungen im Funk bleiben, wie sie sind.
            string text = "GT3 vorne, BMW von hinten. Das ist JAH TESÏ.";
            Assert.AreEqual("GT3 vorne, BMW von hinten. Das ist Jah Tesï.", SpokenNames.Prepare(text, new[] { "JAH TESÏ" }));
            Assert.AreEqual(text, SpokenNames.Prepare(text, new string[0]));
            Assert.AreEqual(text, SpokenNames.Prepare(text, null));
            Assert.AreEqual("TESÏNGER vorne.", SpokenNames.Prepare("TESÏNGER vorne.", new[] { "TESÏ" }), "Nur ganze Namen.");
        }

        [Test]
        public void Longer_name_wins_over_a_shorter_one_inside_it()
        {
            Assert.AreEqual("Jah Tesï und Tesï.", SpokenNames.Prepare("JAH TESÏ und TESÏ.", new[] { "TESÏ", "JAH TESÏ" }));
        }
    }
}
