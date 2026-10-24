using System;
using System.Collections.Generic;
using System.Globalization;
using System.Speech.AudioFormat;
using System.Speech.Recognition;

namespace CrewChiefV4.SRE
{
    class SystemSREWrapper : SREWrapper
    {
        private SpeechRecognitionEngine internalSRE;

        private int maxAudioLevel = 0;

        private List<string> lastProblems = new List<string>();

        private bool debugRecognitionAttempt = false;

        private bool writeDebugData;

        public SystemSREWrapper(CultureInfo culture, TimeSpan? endSilenceTimeoutAmbiguous, bool writeSREDebugData)
        {
            // if the culture is null we won't be able to use the SRE - this is checked later in the initialisation code.
            // We still want to check if the engine is available so we know which warning to display to the user
            if (culture == null)
            {
                this.internalSRE = new SpeechRecognitionEngine();
            }
            else
            {
                this.internalSRE = new SpeechRecognitionEngine(culture);
            }
            if (endSilenceTimeoutAmbiguous != null)
            {
                this.internalSRE.EndSilenceTimeoutAmbiguous = endSilenceTimeoutAmbiguous.Value;
            }
            if (writeSREDebugData)
            {
                this.internalSRE.AudioLevelUpdated += new EventHandler<AudioLevelUpdatedEventArgs>(recognizer_AudioLevelUpdated); ;
                this.internalSRE.AudioSignalProblemOccurred += new EventHandler<AudioSignalProblemOccurredEventArgs>(recognizer_AudioSignalProblemOccurred);
            }
            this.writeDebugData = writeSREDebugData;
        }

        public void AddSpeechRecognizedCallback(object callback)
        {
            internalSRE.SpeechRecognized += (EventHandler<SpeechRecognizedEventArgs>)callback;
        }

        public void AddRecognitionCompleteCallback(object callback)
        {
            internalSRE.RecognizeCompleted += (EventHandler<RecognizeCompletedEventArgs>)callback;
        }

        public void AddRecognitionRejectedCallback(object callback)
        {
            internalSRE.SpeechRecognitionRejected += (EventHandler<SpeechRecognitionRejectedEventArgs>)callback;
        }

        public void LoadGrammar(GrammarWrapper grammarWrapper)
        {
            internalSRE.LoadGrammar((Grammar)grammarWrapper.GetInternalGrammar());
        }

        public void RecognizeAsync()
        {
            if (writeDebugData)
            {
                this.maxAudioLevel = 0;
                this.lastProblems.Clear();
                this.debugRecognitionAttempt = false;
            }
            internalSRE.RecognizeAsync(RecognizeMode.Multiple);
        }

        public void EmulateRecognize(string command)
        {
            internalSRE.EmulateRecognize(command);
        }

        public void RecognizeAsyncCancel()
        {
            try
            {
                internalSRE.RecognizeAsyncCancel();
            }
            catch (Exception ex)
            {
                Log.DontSpam("Error occurred while cancelling recognition: " + ex.Message).Warning();
            }
            if (this.debugRecognitionAttempt)
            {
                Console.WriteLine("Max audio level for recogniser operation = " + this.maxAudioLevel);
                Console.WriteLine("Reported audio signal problems: " + string.Join(", ", this.lastProblems));
            }
        }

        public void RecognizeAsyncStop()
        {
            internalSRE.RecognizeAsyncStop();
            if (this.debugRecognitionAttempt)
            {
                Console.WriteLine("Max audio level for recogniser operation = " + this.maxAudioLevel);
                Console.WriteLine("Reported audio signal problems: " + string.Join(", ", this.lastProblems));
            }
        }

        public int GetMaxAudioLevelForLastOperation()
        {
            return maxAudioLevel;
        }

        public List<string> GetReportedProblemsForLastOperation()
        {
            return this.lastProblems;
        }

        public void SetInitialSilenceTimeout(TimeSpan timeSpan)
        {
            internalSRE.InitialSilenceTimeout = timeSpan;
        }

        public void SetInputToAudioStream(RingBufferStream.RingBufferStream stream, int rate, int depth, int channelCount)
        {
            SpeechAudioFormatInfo safi = new SpeechAudioFormatInfo(rate,
                            depth == 16 ? AudioBitsPerSample.Sixteen : AudioBitsPerSample.Eight,
                            channelCount == 2 ? AudioChannel.Stereo : AudioChannel.Mono);
            internalSRE.SetInputToAudioStream(stream, safi);
        }

        public void SetInputToDefaultAudioDevice()
        {
            try
            {
                internalSRE.SetInputToDefaultAudioDevice();
            }
            catch (Exception e)
            {
                Log.Error("Error setting input to default audio device: no microphone?");
            }
        }

        public void SetInputToNull()
        {
            internalSRE.SetInputToNull();
        }

        public void UnloadAllGrammars()
        {
            internalSRE.UnloadAllGrammars();
        }

        public void UnloadGrammar(GrammarWrapper grammarWrapper)
        {
            internalSRE.UnloadGrammar((Grammar)grammarWrapper.GetInternalGrammar());
        }

        public object GetInternalSRE()
        {
            return internalSRE;
        }

        void recognizer_AudioLevelUpdated(object sender, AudioLevelUpdatedEventArgs e)
        {
            if (this.writeDebugData && this.maxAudioLevel < e.AudioLevel)
            {
                this.maxAudioLevel = e.AudioLevel;
            }
        }

        // Gather information when the AudioSignalProblemOccurred event is raised.  
        private void recognizer_AudioSignalProblemOccurred(object sender, AudioSignalProblemOccurredEventArgs e)
        {
            if (this.writeDebugData)
            {
                this.lastProblems.Add(e.AudioSignalProblem.ToString());
                this.debugRecognitionAttempt = true;
            }
        }
    }
}
