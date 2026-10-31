using System;
using System.Threading;
using System.Threading.Tasks;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class AudioDeliveryAccuracyTests
    {
        [Test]
        public void Removing_before_playback_calls_only_discard_once()
        {
            int started=0, discarded=0, backend=0;
            string id=AiAudioStore.Put(new byte[44],"pending",null,()=>started++,()=>discarded++);
            AiAudioStore.Remove(id); AiAudioStore.Remove(id);
            Assert.IsFalse(AiAudioStore.TryStart(id,()=>backend++));
            Assert.AreEqual(0,started); Assert.AreEqual(0,backend); Assert.AreEqual(1,discarded);
        }
        [Test]
        public void Backend_failure_discards_without_counting_as_delivered()
        {
            int started=0, discarded=0;
            string id=AiAudioStore.Put(new byte[44],"failure",null,()=>started++,()=>discarded++);
            Assert.Throws<InvalidOperationException>(()=>AiAudioStore.TryStart(id,()=>{throw new InvalidOperationException();}));
            AiAudioStore.Remove(id);
            Assert.AreEqual(0,started); Assert.AreEqual(1,discarded);
        }
        [Test]
        public void Late_invalidity_is_checked_at_backend_submission()
        {
            bool valid=true; int started=0, discarded=0, backend=0;
            string id=AiAudioStore.Put(new byte[44],"late",()=>valid,()=>started++,()=>discarded++);
            byte[] wav; string subtitle; Assert.IsTrue(AiAudioStore.TryGet(id,out wav,out subtitle));
            valid=false;
            Assert.IsFalse(AiAudioStore.TryStart(id,()=>backend++));
            Assert.AreEqual(0,started); Assert.AreEqual(0,backend); Assert.AreEqual(1,discarded);
        }
        [Test]
        public void Concurrent_queue_removal_during_backend_start_never_retries_the_same_clip()
        {
            int started=0, discarded=0;
            using(var entered=new ManualResetEventSlim())
            using(var finish=new ManualResetEventSlim())
            {
                string id=AiAudioStore.Put(new byte[44],"race",null,()=>Interlocked.Increment(ref started),()=>Interlocked.Increment(ref discarded));
                var task=Task.Run(()=>AiAudioStore.TryStart(id,()=>{entered.Set(); if(!finish.Wait(3000)) throw new TimeoutException();}));
                try
                {
                    Assert.IsTrue(entered.Wait(3000));
                    AiAudioStore.Remove(id); // Must return without waiting for the audio driver.
                    Assert.AreEqual(0,discarded);
                }
                finally { finish.Set(); }
                Assert.IsTrue(task.GetAwaiter().GetResult());
                Assert.AreEqual(1,started); Assert.AreEqual(0,discarded);
            }
        }
        [Test]
        public void Competing_starts_submit_only_once()
        {
            int started=0, backend=0, discarded=0;
            string id=AiAudioStore.Put(new byte[44],"once",null,()=>Interlocked.Increment(ref started),()=>discarded++);
            Parallel.For(0,32,_=>AiAudioStore.TryStart(id,()=>Interlocked.Increment(ref backend)));
            AiAudioStore.Remove(id);
            Assert.AreEqual(1,backend); Assert.AreEqual(1,started); Assert.AreEqual(0,discarded);
        }
        [TestCase("ai_team_reply_")] [TestCase("ai_team_info_")]
        public void Queue_expiry_releases_a_pending_clip(string prefix)
        {
            int discarded=0;string id=AiAudioStore.Put(new byte[44],"expired",null,null,()=>discarded++);
            AiAudioStore.DiscardMessage(prefix+id);
            Assert.AreEqual(1,discarded);
        }
    }

    [TestFixture, Category("AiTeam"), NonParallelizable, Apartment(System.Threading.ApartmentState.STA)]
    public class AudioLevelControlTests
    {
        [TestCase(0)] [TestCase(37)] [TestCase(100)]
        public void Classic_message_and_background_controls_stay_in_sync(int percent)
        {
            var type = typeof(CrewChiefV4.MainWindow);
            var main = (CrewChiefV4.MainWindow)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            var message = new System.Windows.Forms.TrackBar { Maximum = 100 };
            var background = new System.Windows.Forms.TrackBar { Maximum = 100 };
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            type.GetField("messagesVolumeSlider", flags).SetValue(main, message);
            type.GetField("backgroundVolumeSlider", flags).SetValue(main, background);
            try
            {
                main.updateMessagesVolume(percent / 100f, true, true);
                main.updateBackgroundVolume(percent / 100f, true, true);
                Assert.AreEqual(percent, message.Value); Assert.AreEqual(percent, background.Value);
                Assert.AreEqual(percent / 100f, CrewChiefV4.MainWindow.currentMessageVolume, .001);
            }
            finally { message.Dispose(); background.Dispose(); }
        }

        [TestCase("NAudioBackgroundPlayerWaveOut", 0f)] [TestCase("NAudioBackgroundPlayerWaveOut", .4f)]
        [TestCase("NAudioBackgroundPlayerWasapi", 0f)] [TestCase("NAudioBackgroundPlayerWasapi", .4f)]
        public void Running_background_sample_channel_applies_new_gain_without_restarting(string backend, float level)
        {
            var asm = typeof(CrewChiefV4.Audio.AudioPlayer).Assembly;
            var userType = asm.GetType("CrewChiefV4.UserSettings"); var user = userType.GetMethod("GetUserSettings").Invoke(null, null);
            userType.GetMethod("setProperty").Invoke(user, new object[] { "background_volume", level });
            var global = asm.GetType("CrewChiefV4.GameState.GlobalBehaviourSettings").GetField("racingType");
            var previous = global.GetValue(null); global.SetValue(null, CrewChiefV4.CrewChief.RacingType.Circuit);
            var audioAsm = System.Reflection.Assembly.Load("NAudio.Core");
            var pcm = new byte[480]; for (int i = 0; i < pcm.Length; i += 2) { pcm[i] = 0; pcm[i + 1] = 64; }
            var reader = (IDisposable)Activator.CreateInstance(audioAsm.GetType("NAudio.Wave.WaveFileReader"),
                new object[] { new System.IO.MemoryStream(AiAudio.PcmToWav(pcm, 24000)) });
            try
            {
                var channelType = audioAsm.GetType("NAudio.Wave.SampleProviders.SampleChannel");
                var channel = Activator.CreateInstance(channelType, new object[] { reader, false });
                var backendType = asm.GetType("CrewChiefV4.Audio." + backend);
                var player = Activator.CreateInstance(backendType, new object[] { new SynchronizationContext(), "", "" });
                backendType.GetField("volumeChannel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(player, channel);
                backendType.GetMethod("refreshVolume").Invoke(player, null);
                Assert.AreEqual(level, (float)channelType.GetProperty("Volume").GetValue(channel), .001);
                var samples = new float[20];
                int count = (int)channelType.GetMethod("Read").Invoke(channel, new object[] { samples, 0, samples.Length });
                Assert.Greater(count, 0); Assert.AreEqual(.5f * level, samples[0], .001, "Real sample gain, no audio hardware or playback.");
            }
            finally { reader.Dispose(); global.SetValue(null, previous); }
        }
    }
}
