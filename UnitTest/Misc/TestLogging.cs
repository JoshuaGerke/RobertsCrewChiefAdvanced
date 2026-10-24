using CrewChiefV4;

using FluentAssertions;

using NUnit.Framework;

using NUnit.Framework;

namespace UnitTest.Misc.Logging
{
    [TestFixture]
    public class TestLogging
    {
        [SetUp] // nunit
        public void Init()
        {
            Log.setLogLevel(Log.LogType.Exception);
        }
        [Test]
        public void TestDontSpam()
        {
            string log = "";
            log += Log.DontSpam("hello ", 4); // "hello"
            log += Log.DontSpam("world ", 4); // "world"
            log += Log.DontSpam("hello ", 1); // null 
            System.Threading.Thread.Sleep(1010);
            log += Log.DontSpam("hello ", 2); // "hello" again
            System.Threading.Thread.Sleep(1010);
            log += Log.DontSpam("hello ", 1); // null (still within 2 S)
            System.Threading.Thread.Sleep(1010);
            log += Log.DontSpam("hello ", 1); // "hello" (3 S passed, entry expired)
            log.Should().BeEquivalentTo($"hello world hello hello ");
            log += Log.DontSpam("world ", 4); // null
            System.Threading.Thread.Sleep(1010);
            log += Log.DontSpam("world ", 4); // "world"
            log.Should().BeEquivalentTo($"hello world hello hello world ");
        }
        [Test]
        public void TestFluentDontSpam()
        {
            var log = "Warning!";
            Log.DontSpam(log).Warning();
            ConsoleCapture.Contents.Should().Contain(log);
            ConsoleCapture.Clear();
            System.Threading.Thread.Sleep(1010);
            Log.DontSpam(log).Warning();
            ConsoleCapture.Contents.Should().NotContain(log);
        }
        [Test]
        public void TestLazyEvaluation()
        {
            ConsoleCapture.Clear();
            Log.setLogLevel(Log.LogType.Commentary);
            Log.Verbose(() => "hello ");
            ConsoleCapture.Contents.Should().NotContain("hello ");
            Log.setLogLevel(Log.LogType.Verbose);
            Log.Verbose(() => "hello ");
            ConsoleCapture.Contents.Should().Contain("hello ");
        }
    }
}
