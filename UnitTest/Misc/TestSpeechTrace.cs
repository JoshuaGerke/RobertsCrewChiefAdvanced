using System.Collections.Generic;
using System.IO;
using CrewChiefV4;
using FluentAssertions;
using NUnit.Framework;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TestSpeechTrace
    {
        [Test]
        public void Instantiate()
        {
            CrewChief.SpeechTrace = new SpeechTrace();
        }

        [Test]
        public void Tracing()
        {
            string recognisedText = "How's my fuel";
            CrewChief.SpeechTrace = new SpeechTrace();
            CrewChief.SpeechTrace.Add(recognisedText);
        }

        string filename = "test.cct";
        [Test]
        public void Dumping()
        {
            long ticksWhenRead = 638634829861140000; // the initial value
            CrewChief.ticksWhenRead = ticksWhenRead;
            string recognisedText = "How's my fuel";
            CrewChief.SpeechTrace = new SpeechTrace();
            CrewChief.SpeechTrace.Add(recognisedText);
        }

        [Test]
        public void Playback()
        {
            long ticksWhenRead = 638634829861140000; // the initial value
            CrewChief.ticksWhenRead = ticksWhenRead;
            string recognisedText = "How's my fuel";
            CrewChief.SpeechTrace = new SpeechTrace();
            CrewChief.SpeechTrace.Add(recognisedText);
            var dict = CrewChief.SpeechTrace.Dictionary;

            // Not reached the tick
            CrewChief.SpeechTrace.LoadDictionary(dict);
            var resultText = CrewChief.SpeechTrace.Get(40000);
            Assert.IsNull(resultText);

            resultText = CrewChief.SpeechTrace.Get(ticksWhenRead);
            Assert.AreEqual(recognisedText, resultText);

            // Can't read it twice
            resultText = CrewChief.SpeechTrace.Get(ticksWhenRead);
            Assert.IsNull(resultText);
        }
    }

    [TestFixture]
    public class TestTraceFile
    {
        string fileName = "testTraceDump.zip";
        string consoleLogPrefix = "consoleLogPrefix....";
        string consoleLog = "consoleLog.....";
        string serializableObject = "serializableObject....";
        Dictionary<long, string> speechTrace = new Dictionary<long, string> { { 123456, "voice command" } };
        [Test]
        public void TestDump()
        {
            GameDataReader.DumpTrace<string>(serializableObject, fileName, speechTrace, consoleLogPrefix, consoleLog);
        }

        [Test]
        public void TestLoad()
        {
            //fileName = @"d:\Users\Tony\Documents\CrewChiefV4\debugLogs\fred.zip";
            var traceContents = GameDataReader.UnzipTraceContents<string>(fileName);
            traceContents.ConsoleLogPrefix[0].Should().Be(consoleLogPrefix);
            traceContents.ConsoleLog[0].Should().Be(consoleLog);
            traceContents.SpeechTrace[123456].Should().Be(speechTrace[123456]);
            traceContents.RawGameData.Should().Be(serializableObject);
            File.Delete(fileName);  // tidy up
        }
    }
}
