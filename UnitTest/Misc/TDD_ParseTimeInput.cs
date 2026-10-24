using CrewChiefV4;
using CrewChiefV4.Audio;
using CrewChiefV4.Events;

using NUnit.Framework;

using System;
using System.Threading;

using Assert = NUnit.Framework.Assert;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TDD_ParseTimeInput
    {
        [Test]
        [TestCase("9:45PM", 21, 45)]
        [TestCase("9:45 PM", 21, 45)]
        [TestCase("9:45AM", 9, 45)]
        [TestCase("9:45  AM", 9, 45)]
        [TestCase("21:25", 21, 25)]
        [TestCase("9:45", 9, 45)]
        [TestCase("945", 0, 0)]
        public void test_ParseTimeInput(string input, int hour, int minute)
        {
            DateTime dt = AlarmClock.ParseTimeInput(input);
            Assert.AreEqual(hour, dt.Hour);
            Assert.AreEqual(minute, dt.Minute);
        }
    }

    [TestFixture]
    public class TDD_ColloquialTime
    {
        [Test]
        [TestCase(3, 0, "three o'clock")]
        [TestCase(3, 2, "three o'clock")]
        [TestCase(3, 3, "five past three")]
        [TestCase(3, 28, "half past three")]
        [TestCase(3, 46, "quarter to four")]
        [TestCase(11, 46, "quarter to twelve")]
        [TestCase(11, 56, "five to twelve")]
        [TestCase(11, 57, "nearly twelve")]
        [TestCase(12, 2, "twelve o'clock")]
        [TestCase(12, 16, "quarter past twelve")]
        [TestCase(13, 46, "quarter to two")]
        [TestCase(23, 16, "quarter past eleven")]
        [TestCase(23, 46, "quarter to midnight")]
        [TestCase(23, 48, "ten to midnight")]
        [TestCase(23, 57, "nearly midnight")]
        [TestCase(0, 0, "midnight")]
        [TestCase(0, 16, "quarter past midnight")]
        public void test_ColloquialTimeFormat(int hour, int minute, string expected)
        {
            System.DateTime tod = new System.DateTime(
                1999, 1, 13, hour, minute, 32, 11);
            var ct = new ColloquialTime();
            string res = ct.Format(tod);

            Assert.AreEqual(expected, res);
        }
        [Test]
        [Ignore("Couldn't get it to play the sounds")]
        [TestCase(3, 0, "three o'clock")]
        public void test_ColloquialTimeMessage(int hour, int minute, string expected)
        {
            System.DateTime tod = new System.DateTime(
                1999, 1, 13, hour, minute, 32, 11);
            var ct = new ColloquialTime();
            string res = ct.Format(tod);

            var ap = new AudioPlayer();
            //var ctEvent = new ColloquialTimeEvent(ap);
            //AudioPlayer.ttsOption = AudioPlayer.TTS_OPTION.ANY_TIME;
            //SoundCache.SetUpTTS();
            //ctEvent.sayTime(tod);
            //ap.startMonitor(true);
            //ap.wakeMonitorThreadForRegularMessages(tod);
            //Thread.Sleep(10000); // wait for TTS to complete
            //ap.stopMonitor();
        }
    }
}
