using NUnit.Framework;
using CrewChiefV4;

namespace UnitTest.Misc
{
    [TestFixture]
    public class CommandLineParametersReader
    {
        [Test]
        public void TestMethod1()
        {
            string[] args = { "-game", "F1_2020" };
            Utilities.CommandLineParametersReader param = new Utilities.CommandLineParametersReader(args);
            string input = param.Get("game");
            Assert.AreEqual("F1_2020", input);
        }
        [Test]
        public void TestMethod2()
        {
            string[] args = { "-GAME", "PCARS2_NETWORK", "-c_exit" };
            Utilities.CommandLineParametersReader param = new Utilities.CommandLineParametersReader(args);
            string input = param.Get("game");
            Assert.AreEqual("PCARS2_NETWORK", input);
            input = param.Get("c_exit");
            Assert.AreEqual("", input);
        }
        [Test]
        public void TestMethodProfileName()
        {
            string[] args = { "-profile", "my favourite game my awesome profile" };
            Utilities.CommandLineParametersReader param = new Utilities.CommandLineParametersReader(args);
            string input = param.Get("profile");
            Assert.AreEqual("my favourite game my awesome profile", input);
        }
        [Test]
        public void TestMethod3()
        {
            string[] args = { "-cpu1", "-profile", "ams", "-game", "automobilista", "-nodevicescan", "-skip_updates", "-debug" };
            Utilities.CommandLineParametersReader param = new Utilities.CommandLineParametersReader(args);
            Assert.AreEqual("", param.Get("cpu1"));
            Assert.AreEqual("ams", param.Get("profile"));
            Assert.AreEqual("automobilista", param.Get("game"));
            Assert.AreEqual("", param.Get("nodevicescan"));
            Assert.AreEqual("", param.Get("skip_updates"));
            Assert.AreEqual("", param.Get("debug"));
        }
        [Test]
        public void TestMethodCmd()
        {
            string[] args = { "-c_exit", "-profile", "ams", "-game", "automobilista", "-nodevicescan", "-skip_updates", "-debug" };
            Utilities.CommandLineParametersReader param = new Utilities.CommandLineParametersReader(args);
            Assert.AreEqual("-c_exit", param.GetCommandArg());

            string[] args2 = { "-profile", "ams", "-game", "automobilista", "-nodevicescan", "-skip_updates", "-debug" };
            param = new Utilities.CommandLineParametersReader(args2);
            Assert.AreEqual("", param.GetCommandArg());
        }
        [Test]
        /// Check that -profile and  -game are stripped from command args
        public void TestMethodRestart()
        {
            string[] args = { "-c_exit", "-profile", "ams", "-game", "automobilista", "-nodevicescan", "-skip_updates", "-debug" };
            CrewChief.CommandLine = new Utilities.CommandLineParametersReader(args);
            var newArgs = Utilities.RestartAppCommandLine(app_restart: true,
                                                    removeSkipUpdates: true,
                                                    removeProfile: true,
                                                    removeGame: true);

            Assert.AreEqual(-1, newArgs.IndexOf("-profile"));
            Assert.AreEqual(-1, newArgs.IndexOf("ams"));
            Assert.AreEqual(-1, newArgs.IndexOf("-game"));
            Assert.AreEqual(-1, newArgs.IndexOf("automobilista"));
            Assert.AreNotEqual(-1, newArgs.IndexOf("-app_restart"));
        }
    }
}
