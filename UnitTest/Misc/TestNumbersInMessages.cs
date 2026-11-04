using CrewChiefV4;
using CrewChiefV4.Audio;
using CrewChiefV4.NumberProcessing;

using FluentAssertions;
using NUnit.Framework;

namespace UnitTest.Misc
{
    [TestFixture, NonParallelizable]
    public class TestNumbersInMessages
    {
        // Utilities may have been initialized by NUnit before MSTest's assembly initializer.
        // These tests explicitly require min/max choices, not a random hundred-and variant.
        private System.Random originalRandom;
        private bool originalMax;
        private sealed class NumberChoice : System.Random
        { public override double NextDouble() { return Utilities.DebugUseMaxRandomValue ? .999 : 0; } }
        [SetUp]
        public void UseDeterministicNumberChoices()
        { originalRandom=Utilities.random;originalMax=Utilities.DebugUseMaxRandomValue;Utilities.random=new NumberChoice(); }
        [TearDown]
        public void RestoreNumberChoices()
        { Utilities.random=originalRandom;Utilities.DebugUseMaxRandomValue=originalMax; }

        [Test]
        public void TestPortugueseTwocmd()
        {
            UserSettings.GetUserSettings().setProperty("use_naudio", false); // otherwise AudioPlayer Debug.Asserts
            AudioPlayer ap = new AudioPlayer();
            AudioPlayer.soundPackLanguage = "pt-br";
            ap.Dispose();
            NumberReaderFactory.NewNumberReader();
            NumberReader numberReader = NumberReaderFactory.GetNumberReader();
            //var x = MessageFragment.Integer(2, MessageFragment.Genders("pt-br", NumberReader.ARTICLE_GENDER.FEMALE));
            var duas = numberReader.GetIntegerSounds(2, true, false, NumberReader.ARTICLE_GENDER.FEMALE);
            duas.Count.Should().Be(1);
            duas[0].Should().NotBeNull();
            duas[0].Should().Be("numbers/duas", "Because two in Portuguese is 'oneHundred20'");
        }
        [Test]
        public void TestHundredsAnd()
        {
            UserSettings.GetUserSettings().setProperty("use_naudio", false); // otherwise AudioPlayer Debug.Asserts
            Configuration.setSoundConfigOption("prefix_hundred_and_thousand_with_one", "true").Should().BeTrue();
            Configuration.setSoundConfigOption("say_and_between_hundred_and_units", "true").Should().BeTrue();
            Configuration.setSoundConfigOption("say_and_between_thousand_and_units", "true").Should().BeTrue();
            Configuration.setSoundConfigOption("allow_short_hundreds", "true").Should().BeTrue();
            Configuration.setSoundConfigOption("always_use_thousands", "true").Should().BeTrue();
            Utilities.DebugUseMaxRandomValue = false; // to avoid random use of "hundred and"

            AudioPlayer ap = new AudioPlayer();
            AudioPlayer.soundPackLanguage = "en";
            ap.Dispose();
            NumberReaderFactory.NewNumberReader();
            NumberReader numberReader = NumberReaderFactory.GetNumberReader();
            var oneHundred20 = numberReader.GetIntegerSounds(120, true, false);
            var result = string.Join(", ", oneHundred20);
            oneHundred20.Count.Should().Be(2, result);
            oneHundred20[1].Should().NotBeNull();
            oneHundred20[0].Should().Be("numbers/1", "One twenty");

            Utilities.DebugUseMaxRandomValue = true; // to ensure random use of "hundred and"
            oneHundred20 = numberReader.GetIntegerSounds(120, false, false);
            result = string.Join(", ", oneHundred20);
            oneHundred20.Count.Should().Be(3, result);
            oneHundred20[1].Should().NotBeNull();
            oneHundred20[1].Should().Be("numbers/hundred_and", "One hundred and twenty");
        }
        [Test]
        public void TestHundreds()
        {
            UserSettings.GetUserSettings().setProperty("use_naudio", false); // otherwise AudioPlayer Debug.Asserts
            Configuration.setSoundConfigOption("prefix_hundred_and_thousand_with_one", "true").Should().BeTrue();
            Configuration.setSoundConfigOption("say_and_between_hundred_and_units", "false").Should().BeTrue();
            Configuration.setSoundConfigOption("say_and_between_thousand_and_units", "true").Should().BeTrue();
            Configuration.setSoundConfigOption("allow_short_hundreds", "true").Should().BeTrue();
            Configuration.setSoundConfigOption("always_use_thousands", "true").Should().BeTrue();
            Utilities.DebugUseMaxRandomValue = false; // to avoid random use of "hundred and"

            AudioPlayer ap = new AudioPlayer();
            AudioPlayer.soundPackLanguage = "en";
            ap.Dispose();
            NumberReaderFactory.NewNumberReader();
            NumberReader numberReader = NumberReaderFactory.GetNumberReader();
            var oneHundred20 = numberReader.GetIntegerSounds(120, false, false);
            var result = string.Join(", ", oneHundred20);
            oneHundred20.Count.Should().Be(3, result);
            oneHundred20[1].Should().NotBeNull();
            oneHundred20[1].Should().Be("numbers/hundred", "One hundred twenty");
        }
    }
}
