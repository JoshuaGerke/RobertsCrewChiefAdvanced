using System.Collections.Generic;

using CrewChiefV4;

using NUnit.Framework;

using Assert = NUnit.Framework.Assert;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TestChatText
    {
        [TestCase("chat nice race berenger", "berenger", "[AutoChat]Nice race Berenger")]
        [TestCase("chat nice race berenger", null, "[AutoChat]Nice race berenger")]

        public void test_CapitaliseOpponentName(string chatText, string driverName, string result)
        {
            HashSet<string>driverNames = new HashSet<string>();
            if (driverName != null)
            {
                driverNames.Add(driverName);
            }

            string output = SpeechRecogniser.TidyChatText(chatText, "chat", driverNames);
            Assert.AreEqual(result, output);
        }
        [TestCase("chat nice race berenger", "bertie", "Berenger", "[AutoChat]Nice race Berenger")]
        [TestCase("chat nice race berenger bertie", "bertie", "berenger", "[AutoChat]Nice race Berenger Bertie")]

        public void test_CapitaliseOpponentName2(string chatText, string driverName, string driverName2, string result)
        {
            HashSet<string> driverNames = new HashSet<string>();
            if (driverName != null)
            {
                driverNames.Add(driverName);
            }
            if (driverName2 != null)
            {
                driverNames.Add(driverName2);
            }

            string output = SpeechRecogniser.TidyChatText(chatText, "chat", driverNames);
            Assert.AreEqual(result, output);
        }
    }
}
