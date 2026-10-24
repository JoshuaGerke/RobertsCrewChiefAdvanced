using CrewChiefV4.Events;
using CrewChiefV4.rFactor2;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

using System.Collections.Generic;

namespace UnitTest.LMU
{
    [TestFixture]
    public class TestWeatherForecast
    {
        private static string node(int sky, float rainChance, float temperature) =>
            $"{{\"WNV_SKY\":{{\"currentValue\":{sky}}},\"WNV_RAIN_CHANCE\":{{\"currentValue\":{rainChance}}},\"WNV_TEMPERATURE\":{{\"currentValue\":{temperature}}}}}";

        // Shape of rest/sessions/weather (trimmed, the real one has more WNV_ values)
        private static readonly JObject response = JObject.Parse(
            "{\"PRACTICE\":{" +
                $"\"START\":{node(0, 0, 20)},\"NODE_25\":{node(1, 0, 20)},\"NODE_50\":{node(2, 0, 21)},\"NODE_75\":{node(3, 0, 21)},\"FINISH\":{node(4, 0, 21)}}}," +
            "\"RACE\":{" +
                $"\"START\":{node(1, 0, 20)},\"NODE_25\":{node(5, 80, 19)},\"NODE_50\":{node(8, 100, 18)},\"NODE_75\":{node(9, 30, 18)},\"FINISH\":{node(0, 0, 19)}}}}}");

        [Test]
        public void TestParse()
        {
            List<RF2WeatherForecast.ForecastNode> nodes = RF2WeatherForecast.parse(response, "RACE");
            Assert.AreEqual(5, nodes.Count);
            Assert.AreEqual(ConditionsMonitor.RainLevel.NONE, nodes[0].rainLevel);
            Assert.AreEqual(ConditionsMonitor.RainLevel.DRIZZLE, nodes[1].rainLevel);
            Assert.AreEqual(ConditionsMonitor.RainLevel.MID, nodes[2].rainLevel);
            Assert.AreEqual(ConditionsMonitor.RainLevel.NONE, nodes[3].rainLevel, "rain chance is below 50%");
            Assert.AreEqual(18, nodes[2].temperature);
        }

        [Test]
        public void TestParseMissingSession()
        {
            Assert.IsNull(RF2WeatherForecast.parse(response, "QUALIFY"));
            Assert.IsNull(RF2WeatherForecast.parse(null, "RACE"));
        }

        [Test]
        public void TestRainLevelAt()
        {
            List<RF2WeatherForecast.ForecastNode> nodes = RF2WeatherForecast.parse(response, "RACE");
            const double endET = 60 * 60; // 1 hour race, nodes are 12 minutes apart
            Assert.AreEqual(ConditionsMonitor.RainLevel.NONE, RF2WeatherForecast.getRainLevelAt(nodes, 0, endET));
            Assert.AreEqual(ConditionsMonitor.RainLevel.NONE, RF2WeatherForecast.getRainLevelAt(nodes, 11 * 60, endET));
            Assert.AreEqual(ConditionsMonitor.RainLevel.DRIZZLE, RF2WeatherForecast.getRainLevelAt(nodes, 12 * 60, endET));
            Assert.AreEqual(ConditionsMonitor.RainLevel.MID, RF2WeatherForecast.getRainLevelAt(nodes, 30 * 60, endET));
            // Past the end of the session uses the last node
            Assert.AreEqual(ConditionsMonitor.RainLevel.NONE, RF2WeatherForecast.getRainLevelAt(nodes, 90 * 60, endET));
        }

        [Test]
        public void TestSessionType()
        {
            Assert.AreEqual("PRACTICE", RF2WeatherForecast.getForecastSessionType(0));
            Assert.AreEqual("PRACTICE", RF2WeatherForecast.getForecastSessionType(4));
            Assert.AreEqual("QUALIFY", RF2WeatherForecast.getForecastSessionType(5));
            Assert.AreEqual("RACE", RF2WeatherForecast.getForecastSessionType(9));
            Assert.AreEqual("RACE", RF2WeatherForecast.getForecastSessionType(13));
        }
    }
}
