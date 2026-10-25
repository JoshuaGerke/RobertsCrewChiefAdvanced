using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Fertig gerenderte KI-Funksprüche, bis die Crew-Chief-Audioengine sie abspielt.
    /// Die QueuedMessage verweist nur über eine ID darauf (SoundCache.AI_AUDIO_IDENTIFIER + id).
    /// </summary>
    public static class AiAudioStore
    {
        private class Entry
        {
            public byte[] Wav;
            public string Subtitle;
            public DateTime CreatedUtc;
        }

        private static readonly ConcurrentDictionary<string, Entry> clips = new ConcurrentDictionary<string, Entry>();

        public static string Put(byte[] wav, string subtitle)
        {
            Cleanup();
            string id = Guid.NewGuid().ToString("N");
            clips[id] = new Entry { Wav = wav, Subtitle = subtitle, CreatedUtc = DateTime.UtcNow };
            return id;
        }

        public static bool TryGet(string id, out byte[] wav, out string subtitle)
        {
            Entry e;
            if (id != null && clips.TryGetValue(id, out e))
            {
                wav = e.Wav;
                subtitle = e.Subtitle;
                return true;
            }
            wav = null;
            subtitle = null;
            return false;
        }

        public static void Remove(string id)
        {
            Entry ignored;
            if (id != null)
            {
                clips.TryRemove(id, out ignored);
            }
        }

        public static int Count { get { return clips.Count; } }

        // Nie abgespielte Clips (verworfen, Session-Ende) nach zwei Minuten freigeben
        private static void Cleanup()
        {
            DateTime limit = DateTime.UtcNow.AddMinutes(-2);
            foreach (var key in clips.Where(kv => kv.Value.CreatedUtc < limit).Select(kv => kv.Key).ToList())
            {
                Remove(key);
            }
        }
    }

    public static class AiAudio
    {
        public const int OpenAiPcmSampleRate = 24000;

        public static byte[] PcmToWav(byte[] pcm, int sampleRate, short channels = 1, short bitsPerSample = 16)
        {
            using (var ms = new MemoryStream(44 + pcm.Length))
            using (var w = new BinaryWriter(ms))
            {
                int byteRate = sampleRate * channels * bitsPerSample / 8;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + pcm.Length);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write(channels);
                w.Write(sampleRate);
                w.Write(byteRate);
                w.Write((short)(channels * bitsPerSample / 8));
                w.Write(bitsPerSample);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(pcm.Length);
                w.Write(pcm);
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>MP3 beliebiger Abtastrate → PCM 16 Bit mono mit der gewünschten Rate.</summary>
        public static byte[] Mp3ToPcm(byte[] mp3, int targetSampleRate)
        {
            using (var reader = new Mp3FileReader(new MemoryStream(mp3)))
            {
                ISampleProvider samples = reader.ToSampleProvider();
                if (samples.WaveFormat.Channels == 2)
                {
                    samples = new StereoToMonoSampleProvider(samples);
                }
                if (samples.WaveFormat.SampleRate != targetSampleRate)
                {
                    samples = new WdlResamplingSampleProvider(samples, targetSampleRate);
                }
                var pcm16 = new SampleToWaveProvider16(samples);
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[16384];
                    int read;
                    while ((read = pcm16.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                    }
                    return output.ToArray();
                }
            }
        }

        private static readonly object directSync = new object();
        private static WaveOutEvent directOut;

        /// <summary>
        /// Spielt einen Clip direkt ab. Nur für Tests im KI-Team-Fenster, solange Crew Chief nicht gestartet ist
        /// (dann verwirft die Crew-Chief-Audioengine alle Ansagen). Im Rennen läuft alles über Crew Chief.
        /// </summary>
        public static void PlayDirect(byte[] wav, int deviceNumber)
        {
            lock (directSync)
            {
                if (directOut != null)
                {
                    directOut.Stop();
                }
                var reader = new WaveFileReader(new MemoryStream(wav));
                var output = new WaveOutEvent { DeviceNumber = deviceNumber };
                output.PlaybackStopped += (s, e) =>
                {
                    output.Dispose();
                    reader.Dispose();
                };
                output.Init(reader);
                output.Play();
                directOut = output;
            }
        }

        /// <summary>
        /// Funkgerät-Klang für 16-Bit-Mono-PCM: Bandpass 300–3400 Hz, leichte Sättigung, Pegel auf -1 dBFS.
        /// Damit passen Ben, Mik und Sara klanglich zu den Crew-Chief-Funksprüchen.
        /// </summary>
        public static byte[] ApplyRadioEffect(byte[] pcm16Mono, int sampleRate)
        {
            int count = pcm16Mono.Length / 2;
            if (count == 0)
            {
                return pcm16Mono;
            }
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                samples[i] = BitConverter.ToInt16(pcm16Mono, i * 2) / 32768f;
            }
            var highPass = BiQuadFilter.HighPassFilter(sampleRate, 300, 0.707f);
            var lowPass = BiQuadFilter.LowPassFilter(sampleRate, 3400, 0.707f);
            float peak = 0;
            for (int i = 0; i < count; i++)
            {
                float s = lowPass.Transform(highPass.Transform(samples[i]));
                s = (float)Math.Tanh(s * 1.8f);
                samples[i] = s;
                peak = Math.Max(peak, Math.Abs(s));
            }
            float gain = peak > 0.0001f ? 0.89f / peak : 1f;
            var output = new byte[count * 2];
            for (int i = 0; i < count; i++)
            {
                int v = (int)Math.Round(samples[i] * gain * 32767f);
                short clamped = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, v));
                output[i * 2] = (byte)(clamped & 0xFF);
                output[i * 2 + 1] = (byte)((clamped >> 8) & 0xFF);
            }
            return output;
        }
    }

    /// <summary>
    /// Lokale Windows-Sprachausgabe (offline). Rückfall, wenn keine Cloud-Stimme verfügbar ist.
    /// Eigene Instanz, damit Crew Chiefs eigene TTS-Nutzung nicht gestört wird.
    /// </summary>
    public static class WindowsTts
    {
        private static readonly object sync = new object();
        private static SpeechSynthesizer synthesizer;

        public static string[] InstalledVoices()
        {
            lock (sync)
            {
                EnsureSynth();
                return synthesizer.GetInstalledVoices().Where(v => v.Enabled).Select(v => v.VoiceInfo.Name + " (" + v.VoiceInfo.Culture + ")").ToArray();
            }
        }

        /// <summary>Rendert Text zu PCM 24 kHz/16 Bit/mono.</summary>
        public static byte[] Render(string text, string preferredVoice, TeamMember member)
        {
            lock (sync)
            {
                EnsureSynth();
                var voices = synthesizer.GetInstalledVoices().Where(v => v.Enabled).ToList();
                InstalledVoice voice = null;
                if (!string.IsNullOrWhiteSpace(preferredVoice))
                {
                    voice = voices.FirstOrDefault(v => v.VoiceInfo.Name.IndexOf(preferredVoice, StringComparison.OrdinalIgnoreCase) >= 0);
                }
                if (voice == null)
                {
                    var german = voices.Where(v => v.VoiceInfo.Culture.TwoLetterISOLanguageName == "de").ToList();
                    VoiceGender wanted = member == TeamMember.Sara ? VoiceGender.Female : VoiceGender.Male;
                    voice = german.FirstOrDefault(v => v.VoiceInfo.Gender == wanted) ?? german.FirstOrDefault() ?? voices.FirstOrDefault();
                }
                if (voice == null)
                {
                    throw new InvalidOperationException("Keine Windows-Stimme installiert.");
                }
                synthesizer.SelectVoice(voice.VoiceInfo.Name);
                // gleiche Stimme für mehrere Rollen? Dann wenigstens im Tempo unterscheiden
                synthesizer.Rate = member == TeamMember.Sara ? 2 : member == TeamMember.Mik ? 1 : 0;
                synthesizer.Volume = 100;
                using (var ms = new MemoryStream())
                {
                    synthesizer.SetOutputToAudioStream(ms, new SpeechAudioFormatInfo(AiAudio.OpenAiPcmSampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                    try
                    {
                        synthesizer.Speak(text);
                    }
                    finally
                    {
                        synthesizer.SetOutputToNull();
                    }
                    return ms.ToArray();
                }
            }
        }

        private static void EnsureSynth()
        {
            if (synthesizer == null)
            {
                synthesizer = new SpeechSynthesizer();
            }
        }
    }
}
