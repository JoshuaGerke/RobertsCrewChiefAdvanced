using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Rob 24.11.: Keine Funkstufen mehr („das bringt ihn nur durcheinander“). Die Crew funkt immer nach ihrer Lage-Logik. Alte
    /// Befehle wie „Ruhe bitte“ oder „mehr Infos“ ändern nichts, die Crew sagt das kurz. „Spotter ein“ und „Spotter aus“ bleiben
    /// und wirken während der Fahrt, auch beim Wechsel Training → Qualifying → Rennen und im Spottermodus Realistisch.
    /// Die Befehle hängen an keiner Simulation; sie laufen in AMS2, RaceRoom, LMU, AC EVO und iRacing gleich.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class RadioAmountCommandTests
    {
        [TestCase("Ruhe bitte"), TestCase("Ben, Ruhe bitte!"), TestCase("bitte Ruhe"), TestCase("Funkruhe"), TestCase("weniger Infos"),
         TestCase("Quiet please"), TestCase("less radio"), TestCase("Silencio, por favor"), TestCase("menos información"),
         TestCase("mehr Infos"), TestCase("Mehr Infos bitte."), TestCase("Crew, mehr Funk"), TestCase("more info please"), TestCase("más información"),
         TestCase("viel mehr Infos"), TestCase("Volle Informationen."), TestCase("Volles Programm"), TestCase("much more info"), TestCase("toda la información"),
         TestCase("Funk normal"), TestCase("wieder normal"), TestCase("Infos wieder an"), TestCase("radio back on"), TestCase("vuelve a normal"),
         TestCase("Routinefunk seltener"), TestCase("Routineberichte häufiger")]
        public void Old_radio_level_commands_are_recognised_but_change_nothing(string said)
        {
            Assert.IsTrue(RadioAmountCommand.IsAmountWish(said), said);
            Assert.IsNull(SpotterCommand.Parse(said));
            Assert.IsFalse(DriverRadioPreferences.IsCommand(said), "wird nicht als Funkwunsch gespeichert");
        }

        [TestCase("Gibt es mehr Infos?"), TestCase("Ruhe?"), TestCase("Wie viel Sprit habe ich noch?"), TestCase("Ruhe vor dem Sturm, oder"),
         TestCase("Mehr Infos zum Reifenverschleiß vorne links und hinten rechts bitte, und wie lange halten die noch"),
         TestCase("Ich brauche nicht mehr Infos"), TestCase("Wie weit ist der Vordermann"), TestCase("Ben, kommt von dir nichts mehr?"), TestCase(""), TestCase(null)]
        public void Questions_and_other_sentences_are_no_command(string said)
        {
            Assert.IsFalse(RadioAmountCommand.IsAmountWish(said));
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
            Assert.IsFalse(RadioAmountCommand.IsAmountWish(said));
        }

        [Test]
        public void Every_reply_has_four_or_more_wordings_in_every_language()
        {
            string was = RadioLanguage.Current;
            try
            {
                foreach (string language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
                {
                    RadioLanguage.Current = language;
                    Assert.GreaterOrEqual(Enumerable.Range(0, 6).Select(RadioAmountCommand.NoLevelsReply).Distinct().Count(), 4, language);
                    foreach (bool on in new[] { true, false })
                        Assert.GreaterOrEqual(Enumerable.Range(0, 6).Select(i => SpotterCommand.Reply(on, i)).Distinct().Count(), 4, language + " spotter " + on);
                }
                // Spotter aus nennt, was bleibt: Gelb, Unfälle, Safety Car.
                RadioLanguage.Current = RadioLanguage.German;
                Assert.IsTrue(Enumerable.Range(0, 5).Select(i => SpotterCommand.Reply(false, i)).Any(r => r.Contains("Gelb") || r.Contains("Gefahr") || r.Contains("Safety Car")));
                // Die Antwort auf alte Befehle verspricht keine Umstellung.
                foreach (var reply in Enumerable.Range(0, 4).Select(RadioAmountCommand.NoLevelsReply))
                    StringAssert.DoesNotContain("ab jetzt mehr", reply);
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
        public void The_radio_always_runs_at_the_crews_own_level()
        {
            Assert.AreEqual(AiRadioMode.NORMAL, new AiTeamSettings().RadioMode);
            Assert.AreEqual(1, DriverMemory.RadioDensity, "keine gespeicherte Funkdichte");
            Assert.AreEqual(1, RadioLoad.AmountDensity(RadioAmount.Normal, DriverMemory.RadioDensity));
        }

        private bool wasInitial, wasRealistic, wasEnabled, wasOval;
        private bool? wasChoice;

        [SetUp]
        public void KeepSpotterState()
        {
            wasInitial = GlobalBehaviourSettings.spotterEnabledInitialState; wasRealistic = GlobalBehaviourSettings.realisticMode;
            wasEnabled = GlobalBehaviourSettings.spotterEnabled; wasOval = GlobalBehaviourSettings.useOvalLogic; wasChoice = GlobalBehaviourSettings.driverSpotterChoice;
        }

        [TearDown]
        public void RestoreSpotterState()
        {
            GlobalBehaviourSettings.spotterEnabledInitialState = wasInitial; GlobalBehaviourSettings.realisticMode = wasRealistic;
            GlobalBehaviourSettings.spotterEnabled = wasEnabled; GlobalBehaviourSettings.useOvalLogic = wasOval; GlobalBehaviourSettings.driverSpotterChoice = wasChoice;
        }

        private static string StartReason() => SpotterStartCheck.Reason(true, false, GlobalBehaviourSettings.spotterEnabled,
            GlobalBehaviourSettings.spotterEnabledInitialState, GlobalBehaviourSettings.realisticMode, GlobalBehaviourSettings.useOvalLogic, true, false);

        /// <summary>
        /// LMU Sebring 24.11.: Spottermodus Realistisch, Rundstrecke. „Spotter ein“ muss Sara sofort spotten lassen und darf beim
        /// Wechsel vom Qualifying ins Rennen nicht wieder verloren gehen. „Spotter aus“ ebenso.
        /// </summary>
        [Test]
        public void Spotter_on_and_off_by_radio_work_while_driving_and_survive_the_switch_to_the_race()
        {
            var sebring = new TrackDefinition("Sebring International Raceway", 6019);
            GlobalBehaviourSettings.spotterEnabledInitialState = true;
            GlobalBehaviourSettings.realisticMode = true;
            GlobalBehaviourSettings.driverSpotterChoice = null;

            GlobalBehaviourSettings.UpdateFromTrackDefinition(sebring); // Qualifying beginnt
            Assert.IsFalse(GlobalBehaviourSettings.spotterEnabled, "Realistisch: auf der Rundstrecke zunächst aus");
            Assert.AreEqual("realistic_mode_on_road_course", StartReason());

            SpotterCommand.Apply(true); // „Spotter ein“ während der Fahrt
            Assert.IsTrue(GlobalBehaviourSettings.spotterEnabled);
            Assert.AreEqual("ready_to_start", StartReason(), "der Spielzyklus startet den Spotter beim nächsten Takt");

            GlobalBehaviourSettings.UpdateFromTrackDefinition(sebring); // Qualifying → Rennen
            Assert.IsTrue(GlobalBehaviourSettings.spotterEnabled, "„Spotter ein“ gilt auch im Rennen");
            Assert.AreEqual("ready_to_start", StartReason());

            SpotterCommand.Apply(false); // „Spotter aus“
            Assert.IsFalse(GlobalBehaviourSettings.spotterEnabled);
            GlobalBehaviourSettings.realisticMode = false;
            GlobalBehaviourSettings.UpdateFromTrackDefinition(sebring); // nächste Sitzung, Modus Normal
            Assert.IsFalse(GlobalBehaviourSettings.spotterEnabled, "„Spotter aus“ gilt bis zum Neustart, auch im Modus Normal");
        }

        [Test]
        public void Without_a_radio_command_the_spotter_mode_decides()
        {
            var track = new TrackDefinition("Spa-Francorchamps", 7004);
            GlobalBehaviourSettings.spotterEnabledInitialState = true;
            GlobalBehaviourSettings.driverSpotterChoice = null;
            GlobalBehaviourSettings.realisticMode = false;
            GlobalBehaviourSettings.UpdateFromTrackDefinition(track);
            Assert.IsTrue(GlobalBehaviourSettings.spotterEnabled, "Normal: Spotter auf jeder Strecke");
            GlobalBehaviourSettings.realisticMode = true;
            GlobalBehaviourSettings.UpdateFromTrackDefinition(track);
            Assert.IsFalse(GlobalBehaviourSettings.spotterEnabled, "Realistisch: nur auf Ovalen");
        }

        /// <summary>
        /// Der Spotter wird beim Start mit dem damaligen Zustand gebaut. War er da aus, muss „Spotter ein“ ihn trotzdem wecken; der
        /// Spielzyklus gleicht ihn dafür jeden Takt mit dem Schalter ab, ohne Crew Chiefs eigenen Bestätigungsclip.
        /// </summary>
        [Test]
        public void A_spotter_built_while_off_wakes_up_on_spotter_on()
        {
            var spotter = new CrewChiefV4.rFactor2.RF2Spotter(null, false);
            Assert.IsFalse(spotter.isEnabled);
            spotter.syncEnabled(true);
            Assert.IsTrue(spotter.isEnabled);
            spotter.syncEnabled(false);
            Assert.IsFalse(spotter.isEnabled);
        }
    }
}
