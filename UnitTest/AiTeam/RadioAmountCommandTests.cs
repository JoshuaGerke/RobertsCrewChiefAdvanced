using System.Linq;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Rob 23.11.: Funkmenge und Spotter per Funk. „Ruhe bitte“, „mehr Infos“, „viel mehr Infos“, „Funk normal“, „Spotter aus“,
    /// „Spotter ein“ in Deutsch, Englisch und Spanisch. Fragen und längere Sätze ändern nichts. Die Befehle hängen an keiner
    /// Simulation; sie laufen in AMS2, RaceRoom, LMU, AC EVO und iRacing gleich.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class RadioAmountCommandTests
    {
        [TestCase("Ruhe bitte", RadioAmount.Quiet), TestCase("Ben, Ruhe bitte!", RadioAmount.Quiet), TestCase("bitte Ruhe", RadioAmount.Quiet),
         TestCase("Funkruhe", RadioAmount.Quiet), TestCase("weniger Infos", RadioAmount.Quiet), TestCase("Quiet please", RadioAmount.Quiet),
         TestCase("less radio", RadioAmount.Quiet), TestCase("Silencio, por favor", RadioAmount.Quiet), TestCase("menos información", RadioAmount.Quiet),
         TestCase("mehr Infos", RadioAmount.More), TestCase("Mehr Infos bitte.", RadioAmount.More), TestCase("Crew, mehr Funk", RadioAmount.More),
         TestCase("more info please", RadioAmount.More), TestCase("más información", RadioAmount.More), TestCase("mas radio", RadioAmount.More),
         TestCase("viel mehr Infos", RadioAmount.MuchMore), TestCase("Ben, viel mehr Infos bitte", RadioAmount.MuchMore), TestCase("noch mehr Infos", RadioAmount.MuchMore),
         TestCase("much more info", RadioAmount.MuchMore), TestCase("lots more radio", RadioAmount.MuchMore), TestCase("mucha más información", RadioAmount.MuchMore),
         TestCase("Funk normal", RadioAmount.Normal), TestCase("wieder normal", RadioAmount.Normal), TestCase("Infos normal", RadioAmount.Normal),
         TestCase("radio normal", RadioAmount.Normal), TestCase("back to normal", RadioAmount.Normal), TestCase("vuelve a normal", RadioAmount.Normal)]
        public void Radio_amount_commands_are_recognised_in_three_languages(string said, RadioAmount expected)
        {
            Assert.AreEqual(expected, RadioAmountCommand.Parse(said));
        }

        [TestCase("Gibt es mehr Infos?"), TestCase("Ruhe?"), TestCase("Wie viel Sprit habe ich noch?"), TestCase("Ruhe vor dem Sturm, oder"),
         TestCase("Mehr Infos zum Reifenverschleiß vorne links und hinten rechts bitte, und wie lange halten die noch"),
         TestCase("Ich brauche nicht mehr Infos"), TestCase("Wie weit ist der Vordermann"), TestCase(""), TestCase(null)]
        public void Questions_and_other_sentences_change_nothing(string said)
        {
            Assert.IsNull(RadioAmountCommand.Parse(said));
            Assert.IsNull(SpotterCommand.Parse(said));
        }

        [TestCase("Spotter aus", false), TestCase("Sara, Spotter aus bitte", false), TestCase("Spotter off", false), TestCase("turn the spotter off", false),
         TestCase("kein Spotter", false), TestCase("desactiva el spotter", false), TestCase("spotter apagado", false),
         TestCase("Spotter ein", true), TestCase("Spotter an", true), TestCase("Spotter wieder an", true), TestCase("spotter on", true),
         TestCase("turn the spotter back on", true), TestCase("activa el spotter", true), TestCase("spotter encendido", true),
         TestCase("Spotter activado", true), TestCase("spotter desactivado", false)]
        public void Spotter_commands_are_recognised_in_three_languages(string said, bool on)
        {
            Assert.AreEqual(on, SpotterCommand.Parse(said));
            Assert.IsNull(RadioAmountCommand.Parse(said));
        }

        [Test]
        public void Every_confirmation_has_four_or_more_wordings_in_every_language()
        {
            string was = RadioLanguage.Current;
            try
            {
                foreach (string language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
                {
                    RadioLanguage.Current = language;
                    foreach (RadioAmount amount in new[] { RadioAmount.Quiet, RadioAmount.Normal, RadioAmount.More, RadioAmount.MuchMore })
                        Assert.GreaterOrEqual(Enumerable.Range(0, 6).Select(i => RadioAmountCommand.Reply(amount, i)).Distinct().Count(), 4, language + " " + amount);
                    foreach (bool on in new[] { true, false })
                        Assert.GreaterOrEqual(Enumerable.Range(0, 6).Select(i => SpotterCommand.Reply(on, i)).Distinct().Count(), 4, language + " spotter " + on);
                }
                // Spotter aus nennt, was bleibt: Gelb, Unfälle, Safety Car.
                RadioLanguage.Current = RadioLanguage.German;
                Assert.IsTrue(Enumerable.Range(0, 5).Select(i => SpotterCommand.Reply(false, i)).Any(r => r.Contains("Gelb") || r.Contains("Gefahr") || r.Contains("Safety Car")));
            }
            finally { RadioLanguage.Current = was; }
        }

        [TestCase("Spotter aus", TeamMember.Sara), TestCase("Spotter ein", TeamMember.Sara), TestCase("Ruhe bitte", TeamMember.Ben),
         TestCase("viel mehr Infos", TeamMember.Ben), TestCase("Funk normal", TeamMember.Ben)]
        public void Commands_reach_the_right_crew_member_even_after_talking_to_someone_else(string said, TeamMember expected)
        {
            Assert.AreEqual(expected, TeamRouter.Route(said, TeamMember.Mik).Member);
            Assert.AreEqual(expected, TeamRouter.Route(said, TeamMember.Sara).Member);
        }

        [Test]
        public void Radio_amount_maps_to_the_cockpit_setting_both_ways()
        {
            foreach (RadioAmount amount in new[] { RadioAmount.Quiet, RadioAmount.Normal, RadioAmount.More, RadioAmount.MuchMore })
                Assert.AreEqual(amount, AutoRadioPlan.FromMode(AutoRadioPlan.ToMode(amount)));
            Assert.AreEqual(CrewChiefV4.AiTeam.AiRadioMode.MINIMAL, AutoRadioPlan.ToMode(RadioAmount.Quiet));
            Assert.AreEqual(-1, RadioLoad.AmountDensity(RadioAmount.MuchMore, 1));
            Assert.AreEqual(0, RadioLoad.AmountDensity(RadioAmount.More, 1));
            Assert.AreEqual(2, RadioLoad.AmountDensity(RadioAmount.Normal, 2), "Normal behält den älteren Funkwunsch");
        }
    }
}
