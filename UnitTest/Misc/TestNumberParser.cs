using NUnit.Framework;

using Assert = NUnit.Framework.Assert;
using CrewChiefV4;
using CrewChiefV4.PitManager;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TestNumberParser
    {
        [OneTimeSetUp]
        public void InitSpeechRecogniser()
        {
            SpeechRecogniser x = new SpeechRecogniser(null);
            //Debug.Assert(SpeechRecogniser.POINT.Length != 0);
        }

        [SetUp]
        public void Init()
        {
            //SpeechRecogniser x = new SpeechRecogniser(null);
        }

        [TestCase("box change tyre pressures twenty one point four five bar", 21.45f)]
        [TestCase("box change tyre pressures twenty one point fourty five psi", 21.45f)]
        [TestCase("box change tyre pressures zero point four five bar", 0.45f)]
        [TestCase("box change tyre pressures point four five bar", 0.45f)]
        [TestCase("box change tyre pressures nought point zero psi", 0.0f)]
        [TestCase("box change tyre pressures thirty point four five bar", 30.45f)]
        [TestCase("box change tyre pressures thirty point twenty bar", 30.20f)]
        [TestCase("box change tyre pressures thirty point bar", 30.0f)]
        [TestCase("box change tyre pressures oh point zero psi", 0.0f)]
        [TestCase("pit stop change tyre pressures thirty five psi", 35.0f)]
        [TestCase("pit stop change tyre pressures one hundred and thirty five psi", 135.0f)]
        [TestCase("pit stop change tyre pressures hundred psi", 100.0f)]
        [TestCase("pitstop add ten liters", 10.0f)]
        [TestCase("pitstop add eleven liters", 11.0f)]
        [TestCase("pitstop add fifteen liters", 15.0f)]
        [TestCase("pitstop add nineteen liters", 19.0f)]
        [TestCase("pitstop add sixty one liters", 61.0f)]
        [TestCase("pitstop add seventy four liters", 74.0f)]
        [TestCase("pitstop add eighty seven liters", 87.0f)]
        [TestCase("pit stop clear tyres", -1.0f)]
        //[TestCase("twenty twenty two", 2022.0f)]
        //[TestCase("point twenty twenty two", 0.2022f)]
        [TestCase("twenty", 20.0f)]
        [TestCase("point twenty percent", 0.20f)]
        [TestCase("point eleven percent", 0.11f)]
        [TestCase("pit stop fuel ratio fifty", 50f)]
        [TestCase("pit stop fuel ratio fifty five percent", 55f)]
        [TestCase("pit stop fuel ratio fifty-three percent", 53f)]
        [TestCase("pit stop fuel ratio fourty-three percent", 43f)]
        [TestCase("pit stop fuel ratio fifteen", 15f)]
        [TestCase("pit stop virtual energy one-hundred", 100f)]
        [TestCase("something one-hundred and nine", 109f)]
        [TestCase("something hundred and nine", 109f)]
        [TestCase("something one-hundred and ninety-nine", 199f)]
        [TestCase("something one-hundred and twelve", 112f)]
        public void TestFloatsInStrings(string text, float result)
        {
            float parsed = CrewChiefV4.NumberProcessing.SpokenNumberParser.Parse(text);
            Assert.AreEqual(result, parsed);
        }

        [TestCase("pitstop add ten liters", 10)]
        [TestCase("pitstop add twelve liters", 12)]
        [TestCase("pitstop add sixteen liters", 16)]
        [TestCase("pitstop add eighteen liters", 18)]
        [TestCase("pitstop add twenty three liters", 23)]
        [TestCase("pitstop add thirty six liters", 36)]
        [TestCase("pitstop add fifty eight liters", 58)]
        [TestCase("pitstop fill to twenty gallons", 20)]
        public void TestPitNumberHandling_processNumber(string text, int result)
        {
            int parsed = PitNumberHandling.processNumber(text);
            Assert.AreEqual(result, parsed);
        }
    }
}
