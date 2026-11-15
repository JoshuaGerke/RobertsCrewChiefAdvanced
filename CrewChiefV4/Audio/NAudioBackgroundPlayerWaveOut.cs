using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrewChiefV4.Audio
{
    class NAudioBackgroundPlayerWaveOut : BackgroundPlayer
    {
        private Boolean playing = false;

        // will be re-used and only disposed when we stop the app or switch background sounds
        private NAudio.Wave.WaveFileReader reader = null;
        private NAudio.Wave.WaveOutEvent waveOut = null;

        private int deviceIdWhenCached = 0;
        private volatile float volumeWhenCached;
        private NAudioLoopStream loop;
        public override void refreshVolume()
        {
            lock (this)
            {
                volumeWhenCached = getBackgroundVolume();
            }
        }

        private TimeSpan backgroundLength = TimeSpan.Zero;

        public NAudioBackgroundPlayerWaveOut(SynchronizationContext mainThreadContext, String backgroundFilesPath, String defaultBackgroundSound)
        {
            this.backgroundFilesPath = backgroundFilesPath;
            this.defaultBackgroundSound = defaultBackgroundSound;
        }

        public override void mute(bool doMute)
        {
            lock (this)
            {
                muted = doMute;
                if (playing && doMute) stop();
            }
        }

        public override void play()
        {
            lock (this)
            {
                float volume = getBackgroundVolume();
                if (playing || muted || volume <= 0)
                {
                    return;
                }
                if (!initialised || volume != this.volumeWhenCached || this.deviceIdWhenCached != AudioPlayer.naudioBackgroundPlaybackDeviceId)
                {
                    initialised = false;
                    initialise(this.defaultBackgroundSound);
                }
                playing = true;
                int backgroundOffset = Utilities.random.Next(0, Math.Max(1, (int)backgroundLength.TotalSeconds - backgroundLeadout));
                this.reader.CurrentTime = TimeSpan.FromSeconds(backgroundOffset);
                this.waveOut.Play();
            }
        }

        public override void stop()
        {
            lock (this)
            {
                if (initialised && this.waveOut != null)
                {
                    this.waveOut.Pause();
                }
                playing = false;
            }
        }

        private void initReader(String backgroundSoundName)
        {
            if (this.reader != null)
            {
                this.reader.Dispose();
            }
            this.reader = new NAudio.Wave.WaveFileReader(Path.Combine(backgroundFilesPath, backgroundSoundName));
            backgroundLength = reader.TotalTime;
        }

        private void initWaveOut()
        {
            if (this.waveOut != null)
            {
                this.waveOut.Dispose();
            }
            this.volumeWhenCached = getBackgroundVolume();
            this.deviceIdWhenCached = AudioPlayer.naudioBackgroundPlaybackDeviceId;
            this.waveOut = new NAudio.Wave.WaveOutEvent();
            this.waveOut.DeviceNumber = this.deviceIdWhenCached;
            loop = new NAudioLoopStream(reader);
            this.waveOut.Init(new NAudio.Wave.SampleProviders.SampleToWaveProvider(BuildSamples(loop)));
        }

        private NAudio.Wave.ISampleProvider BuildSamples(NAudio.Wave.WaveStream source) =>
            new RadioGainSampleProvider(new NAudio.Wave.SampleProviders.SampleChannel(source), () => volumeWhenCached);

        public override void initialise(string initialBackgroundSound)
        {
            lock (this)
            {
                if (!initialised) LoadBackground(initialBackgroundSound);
            }
        }

        public override void setBackgroundSound(string backgroundSoundName)
        {
            lock (this) LoadBackground(backgroundSoundName);
        }

        private void LoadBackground(string name)
        {
            ReleaseBackground();
            try
            {
                initReader(name);
                initWaveOut();
                initialised = true;
            }
            catch
            {
                ReleaseBackground();
                throw;
            }
        }

        private void ReleaseBackground()
        {
            playing = false;
            initialised = false;
            var output = waveOut;
            waveOut = null;
            try
            {
                if (output != null)
                {
                    try { output.Stop(); }
                    finally { output.Dispose(); }
                }
            }
            finally
            {
                loop?.Dispose();
                loop = null;
                reader?.Dispose();
                reader = null;
            }
        }

        public override void dispose()
        {
            lock (this)
            {
                try { ReleaseBackground(); }
                catch (Exception e) { Log.Exception(e); }
            }
        }
    }
}
