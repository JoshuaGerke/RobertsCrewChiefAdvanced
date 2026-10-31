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
}
