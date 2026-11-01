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
            public Func<bool> IsValid;
            public Action Started;
            public Action BeforeStart;
            public Action Discarded;
            public int State; // 0 pending, 1 starting, 2 started, 3 discarded
        }

        private static readonly ConcurrentDictionary<string, Entry> clips = new ConcurrentDictionary<string, Entry>();

        public static string Put(byte[] wav, string subtitle, Func<bool> isValid = null, Action started = null, Action discarded = null)
        {
            if (wav == null || wav.Length == 0 || wav.Length > 16 * 1024 * 1024) throw new ArgumentException("Ungültiger Audioclip.");
            Cleanup();
            string id = Guid.NewGuid().ToString("N");
            clips[id] = new Entry { Wav = wav, Subtitle = subtitle, CreatedUtc = DateTime.UtcNow, IsValid = isValid, Started = started, Discarded = discarded };
            // Bound unplayed audio even if a backend rejects its queue entry without requesting it.
            foreach (var old in clips.OrderBy(kv => kv.Value.CreatedUtc).ToArray())
            {
                if (clips.Count <= 64 && clips.Values.Sum(e => (long)e.Wav.Length) <= 16L * 1024 * 1024) break;
                Remove(old.Key);
            }
            return id;
        }

        public static bool TryGet(string id, out byte[] wav, out string subtitle)
        {
            Entry e;
            if (id != null && clips.TryGetValue(id, out e))
            {
                bool valid;
                try { valid = e.IsValid == null || e.IsValid(); }
                catch (Exception) { valid = false; }
                if (valid)
                {
                    wav = e.Wav;
                    subtitle = e.Subtitle;
                    return true;
                }
                LiveDiagnostics.Audio("invalidated",id,"ai",e.Subtitle);
                Remove(id);
            }
            wav = null;
            subtitle = null;
            return false;
        }

        /// <summary>Atomically owns the transition from queued to submitted. The backend action starts audio;
        /// removing the queue entry concurrently cannot also mark this clip as discarded/retryable.</summary>
        public static bool TryStart(string id, Action startPlayback)
        {
            Entry entry;
            if (id == null || !clips.TryGetValue(id, out entry) ||
                System.Threading.Interlocked.CompareExchange(ref entry.State, 1, 0) != 0) return false;
            try
            {
                if (entry.IsValid != null && !entry.IsValid())
                {
                    System.Threading.Interlocked.Exchange(ref entry.State, 3);
                    InvokeOnce(ref entry.Discarded);
                    Remove(id);
                    return false;
                }
                InvokeOnce(ref entry.BeforeStart);
                startPlayback();
                System.Threading.Interlocked.Exchange(ref entry.State, 2);
                InvokeOnce(ref entry.Started);
                return true;
            }
            catch
            {
                System.Threading.Interlocked.Exchange(ref entry.State, 3);
                InvokeOnce(ref entry.Discarded);
                Remove(id);
                throw;
            }
        }

        public static void MarkStarted(string id) { TryStart(id, () => { }); }

        private static void InvokeOnce(ref Action callback)
        {
            var action = System.Threading.Interlocked.Exchange(ref callback, null);
            try { if (action != null) action(); } catch (Exception) { }
        }

        public static void Remove(string id)
        {
            Entry removed;
            if (id != null && clips.TryRemove(id, out removed) &&
                System.Threading.Interlocked.CompareExchange(ref removed.State, 3, 0) == 0)
                InvokeOnce(ref removed.Discarded);
        }

        public static void DiscardMessage(string messageName)
        {
            string id;
            if (TryMessageId(messageName, false, out id)) Remove(id);
        }

        // The radio channel opens only after the AI clip passes its backend-start validity check.
        // If a queued clip expires, no standalone beep/background is started for it.
        public static bool PrepareRadio(string messageName, Action openRadio)
        {
            string id; byte[] wav; string text; Entry entry;
            if(!TryMessageId(messageName,false,out id) || !TryGet(id,out wav,out text) || !clips.TryGetValue(id,out entry) ||
                System.Threading.Volatile.Read(ref entry.State)!=0) return false;
            System.Threading.Interlocked.Exchange(ref entry.BeforeStart,openRadio);
            return true;
        }

        public static bool IsMessage(string messageName, bool replyOnly = false)
        {
            string id;
            return TryMessageId(messageName, replyOnly, out id);
        }

        private static bool TryMessageId(string name, bool replyOnly, out string id)
        {
            id = null;
            if (name == null) return false;
            // QueuedMessage adds this prefix when the AI clip is a message fragment.
            if (name.StartsWith("COMPOUND_", StringComparison.Ordinal)) name = name.Substring(9);
            const string reply = "ai_team_reply_", info = "ai_team_info_";
            string prefix = name.StartsWith(reply, StringComparison.Ordinal) ? reply :
                !replyOnly && name.StartsWith(info, StringComparison.Ordinal) ? info : null;
            Guid parsed;
            if (prefix == null || !Guid.TryParseExact(name.Substring(prefix.Length), "N", out parsed)) return false;
            id = parsed.ToString("N");
            return true;
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

        /// <summary>
        /// Schneidet Stille am Anfang und Ende ab (Schwelle etwa -34 dBFS, 20 ms Rand bleiben stehen).
        /// Für Spotteransagen wichtig: Die Warnung soll ohne Vorlauf beginnen.
        /// </summary>
        public static bool HasSpeechSignal(byte[] pcm16Mono)
        {
            if(pcm16Mono==null || pcm16Mono.Length<48 || pcm16Mono.Length%2!=0)return false;
            int audible=0;
            for(int i=0;i<pcm16Mono.Length;i+=2)
                if(Math.Abs((int)BitConverter.ToInt16(pcm16Mono,i))>16 && ++audible>=24)return true;
            return false;
        }

        public static byte[] TrimSilence(byte[] pcm16Mono, int sampleRate, short threshold = 650)
        {
            int count = pcm16Mono.Length / 2;
            int first = -1, last = -1;
            for (int i = 0; i < count; i++)
            {
                if (Math.Abs((int)BitConverter.ToInt16(pcm16Mono, i * 2)) > threshold)
                {
                    if (first < 0) first = i;
                    last = i;
                }
            }
            if (first < 0)
            {
                return pcm16Mono;
            }
            int margin = sampleRate / 50;
            first = Math.Max(0, first - margin);
            last = Math.Min(count - 1, last + margin);
            var result = new byte[(last - first + 1) * 2];
            Buffer.BlockCopy(pcm16Mono, first * 2, result, 0, result.Length);
            return result;
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
        private static float DirectPlaybackVolume()
        {
            float volume = MainWindow.currentMessageVolume >= 0 ? MainWindow.currentMessageVolume :
                UserSettings.GetUserSettings().getFloat("messages_volume");
            return float.IsNaN(volume) || float.IsInfinity(volume) ? 0 : Math.Max(0, Math.Min(1, volume));
        }

        public static void RefreshDirectVolume()
        {
            lock (directSync) { if (directOut != null) directOut.Volume = DirectPlaybackVolume(); }
        }

        public static void PlayDirect(byte[] wav, int deviceNumber)
        {
            lock (directSync)
            {
                if (directOut != null)
                {
                    directOut.Stop();
                }
                var reader = new WaveFileReader(new MemoryStream(wav));
                var output = new WaveOutEvent { DeviceNumber = deviceNumber, Volume = DirectPlaybackVolume() };
                output.PlaybackStopped += (s, e) =>
                {
                    lock (directSync)
                    {
                        if (ReferenceEquals(directOut, output)) directOut = null;
                        output.Dispose();
                        reader.Dispose();
                    }
                };
                output.Init(reader);
                directOut = output;
                output.Play();
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
    /// Lokale Windows-Sprachausgabe (offline), nur bei ausdrücklich gewählter Windows-Quelle. Kein Cloud-Rückfall.
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
