using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CrewChiefV4.Audio
{
    internal abstract class NAudioOut
    {
        internal static NAudioOut CreateOutput()
        {
            if (AudioPlayer.nAudioOutputInterface == AudioPlayer.NAUDIO_OUTPUT_INTERFACE.WAVEOUT)
                return new NAudioOutWaveOut();
            else
                return new NAudioOutWasapi();
        }

        internal abstract NAudio.Wave.PlaybackState PlaybackState { get; }

        internal abstract void SubscribePlaybackStopped(EventHandler<StoppedEventArgs> eventHandler);
        internal abstract void UnsubscribePlaybackStopped(EventHandler<StoppedEventArgs> eventHandler);
        internal abstract void Stop();
        internal abstract void Dispose();
        internal abstract void Play();
        internal abstract void Init(IWaveProvider waveProvider);
        internal abstract void Init(ISampleProvider sampleProvider, bool convertTo16Bit = false);
    }

    internal class NAudioOutWaveOut : NAudioOut
    {
        private NAudio.Wave.WaveOutEvent waveOut = null;

        public NAudioOutWaveOut()
        {
            this.waveOut = new NAudio.Wave.WaveOutEvent();
            this.waveOut.DeviceNumber = AudioPlayer.naudioMessagesPlaybackDeviceId;

            SoundCache.activeSoundPlayerObjects++;
        }

        internal override PlaybackState PlaybackState => this.waveOut != null ? this.waveOut.PlaybackState : PlaybackState.Stopped;

        internal override void Dispose()
        {
            if (this.waveOut != null)
            {
                SoundCache.activeSoundPlayerObjects--;
                this.waveOut.Dispose();
                this.waveOut = null;
            }
        }

        internal override void Init(IWaveProvider waveProvider) => this.waveOut?.Init(waveProvider);
        internal override void Init(ISampleProvider sampleProvider, bool convertTo16Bit = false) => this.waveOut?.Init(sampleProvider, convertTo16Bit);
        internal override void Play() => this.waveOut?.Play();
        internal override void Stop() => this.waveOut?.Stop();

        internal override void SubscribePlaybackStopped(EventHandler<StoppedEventArgs> eventHandler)
        {
            if (this.waveOut != null)
                this.waveOut.PlaybackStopped += eventHandler;
        }

        internal override void UnsubscribePlaybackStopped(EventHandler<StoppedEventArgs> eventHandler)
        {
            if (this.waveOut != null)
                this.waveOut.PlaybackStopped -= eventHandler;
        }
    }

    internal class NAudioOutWasapi : NAudioOut
    {
        public static readonly int wasapiLatency = UserSettings.GetUserSettings().getInt("naudio_wasapi_latency");

        // Cache MMDevice as creating it over and over is costly.
        private static MMDevice cachedDevice = null;
        private static string naudioDeviceGuidWhenCached = "";

        private NAudio.Wave.WasapiOut wasapiOut = null;

        public NAudioOutWasapi()
        {
            if (!NAudioOutWasapi.naudioDeviceGuidWhenCached.Equals(AudioPlayer.naudioMessagesPlaybackDeviceGuid))
            {
                NAudioOutWasapi.cachedDevice?.Dispose();

                NAudioOutWasapi.naudioDeviceGuidWhenCached = AudioPlayer.naudioMessagesPlaybackDeviceGuid;
                NAudioOutWasapi.cachedDevice = new MMDeviceEnumerator().GetDevice(AudioPlayer.naudioMessagesPlaybackDeviceGuid);

                if (NAudioOutWasapi.cachedDevice != null)
                    Console.WriteLine($"Creating new WASAPI output device: {NAudioOutWasapi.cachedDevice.FriendlyName}");
            }

            // Don't allow latency of 0 as it causes CPU spike.  Probably because such low latency is achieved via busy wait. 
            this.wasapiOut = new WasapiOut(NAudioOutWasapi.cachedDevice, AudioClientShareMode.Shared, false, Math.Max(NAudioOutWasapi.wasapiLatency, 1));

            SoundCache.activeSoundPlayerObjects++;
        }

        internal override PlaybackState PlaybackState => this.wasapiOut != null ? this.wasapiOut.PlaybackState : PlaybackState.Stopped;

        internal override void Dispose()
        {
            if (this.wasapiOut != null)
            {
                SoundCache.activeSoundPlayerObjects--;
                this.wasapiOut.Dispose();
                this.wasapiOut = null;
            }
        }

        internal override void Init(IWaveProvider waveProvider) => this.wasapiOut?.Init(waveProvider);
        internal override void Init(ISampleProvider sampleProvider, bool convertTo16Bit = false) => this.wasapiOut?.Init(sampleProvider, convertTo16Bit);
        internal override void Play() => this.wasapiOut?.Play();
        internal override void Stop() => this.wasapiOut?.Stop();

        internal override void SubscribePlaybackStopped(EventHandler<StoppedEventArgs> eventHandler)
        {
            if (this.wasapiOut != null)
                this.wasapiOut.PlaybackStopped += eventHandler;
        }

        internal override void UnsubscribePlaybackStopped(EventHandler<StoppedEventArgs> eventHandler)
        {
            if (this.wasapiOut != null)
                this.wasapiOut.PlaybackStopped -= eventHandler;
        }
    }
}
