using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CrewChiefV4.SRE;
using CrewChiefV4.UserInterface.Models;

namespace CrewChiefV4
{
    // Wizard diagnostics for SpeechRecogniser; the other partial contains recognition itself.
    public partial class SpeechRecogniser
    {
        private readonly object microphoneLevelLock = new object();
        private float microphonePeak;
        private DateTime microphoneSampleTime = DateTime.MinValue;
        private byte[] microphoneRecording;
        private int microphoneRecordingNext;
        private int microphoneRecordingCount;

        private volatile bool wizardListening;
        private int wizardListeningSession;
        public bool WizardListening { get { return wizardListening; } }
        public int WizardListeningSession { get { return Volatile.Read(ref wizardListeningSession); } }

        public bool UsesNAudioSpeechInput { get { return useNAudio; } }

        public bool HasMicrophoneRecording
        {
            get { lock (microphoneLevelLock) { return microphoneRecordingCount > 0; } }
        }

        public void ClearMicrophoneRecording()
        {
            lock (microphoneLevelLock)
            {
                microphoneRecording = null;
                microphoneRecordingNext = microphoneRecordingCount = 0;
            }
        }

        public byte[] GetMicrophoneRecording(out NAudio.Wave.WaveFormat format)
        {
            lock (microphoneLevelLock)
            {
                format = new NAudio.Wave.WaveFormat(nAudioWaveInSampleRate, nAudioWaveInChannelCount);
                var result = new byte[microphoneRecordingCount];
                if (result.Length == 0) return result;
                int start = (microphoneRecordingNext - microphoneRecordingCount +
                    microphoneRecording.Length) % microphoneRecording.Length;
                int first = Math.Min(result.Length, microphoneRecording.Length - start);
                Array.Copy(microphoneRecording, start, result, 0, first);
                Array.Copy(microphoneRecording, 0, result, first, result.Length - first);
                return result;
            }
        }

        // A bounded, in-memory copy of the same PCM supplied to recognition.
        private void RecordMicrophoneAudio(byte[] samples, int count)
        {
            if (!SpeechWizard.Active)
            {
                microphoneRecording = null;
                microphoneRecordingNext = microphoneRecordingCount = 0;
                return;
            }
            int capacity = nAudioWaveInSampleRate * nAudioWaveInChannelCount * 2 * 10;
            if (microphoneRecording == null || microphoneRecording.Length != capacity)
                microphoneRecording = new byte[capacity];
            // A new PTT/listening interval replaces the previous recording.
            if ((DateTime.UtcNow - microphoneSampleTime).TotalMilliseconds > 500)
                microphoneRecordingNext = microphoneRecordingCount = 0;
            int offset = Math.Max(0, count - capacity);
            count -= offset;
            int first = Math.Min(count, capacity - microphoneRecordingNext);
            Array.Copy(samples, offset, microphoneRecording, microphoneRecordingNext, first);
            Array.Copy(samples, offset + first, microphoneRecording, 0, count - first);
            microphoneRecordingNext = (microphoneRecordingNext + count) % capacity;
            microphoneRecordingCount = Math.Min(capacity, microphoneRecordingCount + count);
        }


        // Called by the wizard on its UI timer; never opens another capture device.
        public float ReadMicrophonePeak(out bool receivingAudio)
        {
            lock (microphoneLevelLock)
            {
                receivingAudio = useNAudio &&
                    (DateTime.UtcNow - microphoneSampleTime).TotalMilliseconds < 500;
                float peak = receivingAudio ? microphonePeak : 0;
                microphonePeak = 0;
                return peak;
            }
        }

        private void UpdateMicrophonePeak(byte[] samples, int count)
        {
            // WaveFormat(sampleRate, channels) used below is signed 16-bit PCM.
            int peak = 0;
            for (int i = 0; i + 1 < count; i += 2)
            {
                int sample = (short)(samples[i] | (samples[i + 1] << 8));
                peak = Math.Max(peak, Math.Abs(sample));
            }
            lock (microphoneLevelLock)
            {
                RecordMicrophoneAudio(samples, count);
                microphonePeak = Math.Max(microphonePeak, peak / 32768f);
                microphoneSampleTime = DateTime.UtcNow;
            }
        }


        private void StartWizardListening()
        {
            if (!wizardListening)
            {
                ClearMicrophoneRecording();
                Interlocked.Increment(ref wizardListeningSession);
                wizardListening = true;
            }
        }

        private void StopWizardListening()
        {
            wizardListening = false;
        }

        private void UpdateWizardRecognitionStatus(string recognisedText, float recognitionConfidence)
        {
            if (SpeechWizard.Active)
            {
                string[] status = new string[3];
                status[0] = $"Recognised text: {recognisedText}";
                status[1] = $"Volume: {sreWrapper.GetMaxAudioLevelForLastOperation()}";
                status[2] = $"Confidence: {recognitionConfidence}";
                MainWindow.speechWizard_V.textBoxStatus.Text = String.Join(Environment.NewLine, status);
            }

        }

        public static void Timeout()
        {
            if (SpeechWizard.Active)
            {
                string[] status = new string[3];
                status[0] = "Nothing recognised";
                MainWindow.speechWizard_V.textBoxStatus.Text = String.Join(Environment.NewLine, status);
            }
        }

        void saveAudio(object sender, object e, bool successful = true)
        {
            if (SpeechWizard.Active)
            {
                if (!successful)
                {
                    string[] status = new string[3];
                    status[0] = $"Recognised text: {SREWrapperFactory.GetCallBackErrorText(e)}";
                    status[1] = $"Volume: {sreWrapper.GetMaxAudioLevelForLastOperation()}";
                    //status[2] = $"Length: {SREWrapperFactory.GetCallBackErrorLength(e)} seconds";
                    MainWindow.speechWizard_V.textBoxStatus.Text = String.Join(Environment.NewLine, status);
                }

                string wavFilename = "wizard.wav";
                string textFilename = "wizard.txt";
                // Keep recognition diagnostics, but playback is now explicitly
                // controlled by the wizard's Play back button.
                saveSREDebug(e, wavFilename, textFilename, out string wavFileFullPath);
            }
            else if (CrewChief.Debug.SaveSREDebugData)
            {
                DateTime now = DateTime.Now;
                string wavFilename = now.ToString("MM-dd-yyyy_hh-mm-ss") + ".wav";
                string textFilename = now.ToString("MM-dd-yyyy_hh-mm-ss") + ".txt";
                saveSREDebug(e, wavFilename, textFilename, out string wavFileFullPath);
            }
        }

        bool saveSREDebug(object e, string wavFilename, string textFilename, out string wavFileFullPath)
        {
            wavFileFullPath = null;
            bool saved = false;
            try
            {
                wavFileFullPath = Path.Combine(debugDataPath, wavFilename);
                string textFileFullPath = Path.Combine(debugDataPath, textFilename);
                List<string> result;
                using (Stream outputStream = new FileStream(wavFileFullPath, FileMode.Create))
                {
                    result = SREWrapperFactory.WriteSREDebugData(e, outputStream, this.sreWrapper);
                    saved = outputStream.Length != 0;
                    outputStream.Close();
                }
                if (!saved)
                {
                    File.Delete(wavFileFullPath);
                }
                File.WriteAllText(textFileFullPath, string.Join(Environment.NewLine, result));
            }
            catch (Exception ex)
            {
                // log n swallow, this-is-the-way
                Console.WriteLine("Failed to write SRE debug data");
                Log.Exception(ex);
            }
            return saved;
        }
    }
}
