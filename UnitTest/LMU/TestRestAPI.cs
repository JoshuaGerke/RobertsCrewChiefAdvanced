using CrewChiefV4;
using CrewChiefV4.LMU;

using FluentAssertions;

using NUnit.Framework;

using UnitTest.Utils;

using static CrewChiefV4.LMU.LMU_REST_API_classes;

namespace UnitTest.Misc.WillThrowExceptionsIfDebugging
{
    [TestFixture]
    public class TestRestAPI
    {
        [Test]
        public void TestReadVElive()
        {
            QuitIf.LMUnotRunning();
            Game.game = GameEnum.LMU;
            UnitTest.Active = false;
            var result = VirtualEnergy.Read();
            result.Should().BeLessThanOrEqualTo((100));
        }

        [Test]
        public void TestReadVEcanned()
        {
            QuitIf.rF2orLMUrunning();
            Game.game = GameEnum.LMU;
            var result = VirtualEnergy.Read();
        }

        [Test]
        public void TestReadSessionLive()
        {
            QuitIf.LMUnotRunning();
            UnitTest.Active = false;
            var settings = new RestAPI();
            var _settings = settings.get<OptionsSettings.Root>(OptionsSettings.endPoint);
            Assert.IsNotNull(_settings);

            var _fuel_Usage = settings.get<Sessions.Root>(Sessions.endPoint);
            Assert.IsNotNull(_fuel_Usage);

            Game.game = GameEnum.LMU;
            bool carDamageEnabled = SessionSettings.NotLMUorLMUcarDamageEnabled;
            int fuelMultiplier = SessionSettings.FuelMultiplier;
        }

        [Test]
        public void TestReadPitMenuLive()
        {
            QuitIf.LMUnotRunning();
            UnitTest.Active = false;
            var pitMenuAPI = new LMUPitMenuAPI();
            var choice = pitMenuAPI.GetChoice();
            choice.Should().NotBeNull();
        }
        [Test]
        public void TestWritePitMenuLive()
        {
            QuitIf.LMUnotRunning();
            UnitTest.Active = false;
            var pitMenuAPI = new LMUPitMenuAPI();
            var choice = pitMenuAPI.GetChoice();
            choice.Should().NotBeNull();
            var category = "WING";
            if (pitMenuAPI.SoftMatchCategory(category))
            {
                choice = pitMenuAPI.GetChoice();
                choice.Should().NotBe("N/A");
                var _choice = choice;
                pitMenuAPI.ChoiceInc();
                pitMenuAPI.ChoiceInc();
                pitMenuAPI.ChoiceInc();
                pitMenuAPI.SetChoice(_choice);
                choice = pitMenuAPI.GetChoice();
                choice.Should().Be(_choice);
            }
            else
            {
                Assert.Inconclusive($"No category {category} in Pit Menu");
            }
        }

        [Ignore("Canned responses to Pit Menu only")]
        [Test]
        public void TestReadSessionCanned()
        {
            QuitIf.rF2orLMUrunning();
            var settings = new RestAPI();
            var _settings = settings.get<OptionsSettings.Root>(OptionsSettings.endPoint);
            Assert.IsNotNull(_settings);

            var _fuel_Usage = settings.get<Sessions.Root>(Sessions.endPoint);
            Assert.IsNotNull(_fuel_Usage);
        }

    }
}
