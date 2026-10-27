using System;
using System.IO;
using CrewChiefV4;
using FluentAssertions;
using NUnit.Framework;
using UnitTest.Utils;

namespace UnitTest.Misc.Volume
{
    [TestFixture]
    public class TDD_VolumeDucking
    {
        [SetUp]
        public void init()
        {
            Log.LogMask |= Log.LogType.Debug;
            if (!Processes.vlcCheck())
            {
                return;
            }
            string processName = "vlc";
            int duckPercent = 20;
            Func<ControlVolumeOfProcess> cvp = () => new ControlVolumeOfProcess(processName);
            cvp.Should().NotThrow<ArgumentException>();
            cvp.Should().NotBeNull($"{processName} is not running (and playing a file)");
        }

        [Test]
        public void TestDuckingVolumeOfVLC_mustBePlaying()
        {
            if (!Processes.vlcCheck())
            {
                return;
            }
            string processName = "vlc";

            int duckPercent = 20;
            var cvp = new ControlVolumeOfProcess(processName);
            Console.WriteLine($"Volume before {cvp.GetVolume()}");
            cvp.SetVolume(1.0f); // Set it to max (1.0)
            Console.WriteLine($"Volume after setting to max (1.0) {cvp.GetVolume()}");
            cvp.GetVolume().Should().Be(1.0f);
            float newVolume = cvp.DuckVolume(-duckPercent);
            Console.WriteLine($"Volume after ducking {duckPercent}% {cvp.GetVolume()}");
            newVolume.Should().Be(0.8f);
            cvp.TheProcess.ProcessName.Should().Be(processName);
            Console.WriteLine($"Process: {processName}, {cvp.TheProcess.MainWindowTitle}");
        }

        [Test]
        public void TestMaxMinVolumeOfVLC_mustBePlaying()
        {
            if (!Processes.vlcCheck())
            {
                return;
            }
            string processName = "vlc";
            var cvp = new ControlVolumeOfProcess(processName);
            if (cvp.GetVolume() < 0) Assert.Ignore("VLC hat keine aktive, zugängliche Windows-Audiositzung.");
            cvp.SetVolume(1.01f);
            Console.WriteLine($"Volume after setting to (invalid) 1.01 is {cvp.GetVolume()}");
            cvp.GetVolume().Should().Be(1.0f);

            cvp.SetVolume(-0.01f);
            Console.WriteLine($"Volume after setting to (invalid) -0.01 is {cvp.GetVolume()}");
            cvp.GetVolume().Should().Be(0.0f);
        }
    }
    [TestFixture]
    public class TestVolumeControlOfGame
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void MissingProcessNameHasNoAudioSession(string processName)
        {
            var control = new ControlVolumeOfProcess(processName);
            Assert.IsNull(control.TheProcess);
            Assert.AreEqual(-1, control.GetVolume());
        }

        [SetUp]
        public void Initialize()
        {
            Log.LogMask |= Log.LogType.Debug;
        }
        [Test]
        public void TestNoProcess()
        {
            string processName = "NoSuchProcess";
            Func<ControlVolumeOfProcess> cvp = () => new ControlVolumeOfProcess(processName);
        }
        [Test]
        public void TestDuckingVolumeOfRF2()
        {
            GameOrVoipVolume cvg;
            string processName = "rFactor2";
            if (!Processes.runningCheck(processName))
            {
                return;
            }
            CrewChief.gameDefinition.processName = processName;
            GameOrVoipVolume.AudioDuckingEnabled = true;
            cvg = new GameOrVoipVolume(false);

            File.Exists(DataFiles.game_volumes).Should().BeTrue($"{DataFiles.game_volumes} has not been created");

            Console.WriteLine($"Volume before {cvg.GameProcess.GetVolume()}");
            cvg.GameProcess.SetVolume(1.0f); // Set it to max (1.0)
            Console.WriteLine($"Volume after setting to max (1.0) {cvg.GameProcess.GetVolume()}");
            cvg.GameProcess.GetVolume().Should().Be(1.0f);
            GameOrVoipVolume.DuckGameAudio();
            cvg.GameProcess.GetVolume().Should().BeApproximately(0.2f, 0.01f);
            GameOrVoipVolume.UnDuckGameAudio();
            cvg.GameProcess.GetVolume().Should().Be(1.0f);
            cvg.GameProcess.TheProcess.ProcessName.Should().Be(processName);
            Console.WriteLine($"Process: {processName}, {cvg.GameProcess.TheProcess.MainWindowTitle}");
        }
        [Test]
        public void TestDuckingVolumeOfAMS2_notRunning()
        {
            GameOrVoipVolume cvg;
            string processName = "AMS2AVX";
            CrewChief.gameDefinition.processName = processName;
            GameOrVoipVolume.AudioDuckingEnabled = true;
            cvg = new GameOrVoipVolume(false);
            cvg.GameProcess.SetVolume(1.0f); // Not enabled
            GameOrVoipVolume.DuckGameAudio();
            GameOrVoipVolume.UnDuckGameAudio();
            GameOrVoipVolume.AudioDuckingEnabled = false;
            cvg = new GameOrVoipVolume(false);
            GameOrVoipVolume.DuckGameAudio();
            GameOrVoipVolume.UnDuckGameAudio();
            GameOrVoipVolume.VolumeControlEnabled = true;
            cvg = new GameOrVoipVolume(false);
            cvg.GameProcess.SetVolume(1.0f); // Not running so no effect
        }
        [Test]
        public void TestMaxMinVolumeOfDiscord_process()
        {
            string processName = "Discord";
            if (!Processes.runningCheck(processName))
            {
                return;
            }
            var cvp = new ControlVolumeOfProcess(processName);
            if (cvp.GetVolume() < 0) Assert.Ignore("Discord hat keine aktive, zugängliche Windows-Audiositzung.");
            cvp.SetVolume(1.01f);
            Console.WriteLine($"Volume after setting to (invalid) 1.01 is {cvp.GetVolume()}");
            cvp.GetVolume().Should().Be(1.0f);

            cvp.SetVolume(-0.01f);
            Console.WriteLine($"Volume after setting to (invalid) -0.01 is {cvp.GetVolume()}");
            cvp.GetVolume().Should().Be(0.0f);
        }
        [Test]
        public void TestMaxMinVolumeOfDiscord()
        {
            GameOrVoipVolume cvg;
            string processName = "Discord";
            if (!Processes.runningCheck(processName))
            {
                return;
            }
            GameOrVoipVolume.VolumeControlEnabled = true;
            cvg = new GameOrVoipVolume(true);
            if (cvg.VoipProcess.GetVolume() < 0) Assert.Ignore("Discord hat keine aktive, zugängliche Windows-Audiositzung.");

            File.Exists(DataFiles.game_volumes).Should().BeTrue($"{DataFiles.game_volumes} has not been created");

            Console.WriteLine($"Volume before {cvg.VoipProcess.GetVolume()}");
            cvg.VoipProcess.SetVolume(1.0f); // Set it to max (1.0)
            Console.WriteLine($"Volume after setting to max (1.0) {cvg.VoipProcess.GetVolume()}");
            cvg.VoipProcess.GetVolume().Should().Be(1.0f);
            GameOrVoipVolume.DecreaseVoipVolume();
            cvg.VoipProcess.GetVolume().Should().Be(0.9f);
            GameOrVoipVolume.IncreaseVoipVolume();
            cvg.VoipProcess.GetVolume().Should().Be(1.0f);
        }
    }
}
