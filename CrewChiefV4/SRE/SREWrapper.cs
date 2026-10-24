using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.SRE
{
    interface SREWrapper
    {
        // need to cast these in impl
        void UnloadGrammar(GrammarWrapper grammar);
        void LoadGrammar(GrammarWrapper grammar);
        void AddSpeechRecognizedCallback(Object callback); // internalRealSRE += (CAST to EventHandler<SpeechRecognizedEventArgs>) new EventHandler<SpeechRecognizedEventArgs>(sre_SpeechRecognized);
        void AddRecognitionCompleteCallback(Object callback);
        void AddRecognitionRejectedCallback(Object callback);
        void RecognizeAsync();
        void SetInputToAudioStream(RingBufferStream.RingBufferStream stream, int rate, int depth, int channelCount);

        void RecognizeAsyncCancel();
        void SetInputToDefaultAudioDevice();
        void SetInputToNull();
        /// <summary>
        /// Use text to emulate speech input
        /// Triggers sre_SpeechRecognized() just as if it was speech
        /// (except that confidence is 100%)
        /// </summary>
        /// <param name="command"></param>
        void EmulateRecognize(string command);

        void SetInitialSilenceTimeout(TimeSpan timeSpan);   // internalRealSRE.InitialSilenceTimeout = timeSpan;

        void RecognizeAsyncStop();
        void UnloadAllGrammars();

        object GetInternalSRE();

        int GetMaxAudioLevelForLastOperation();
        List<string> GetReportedProblemsForLastOperation();
    }
}
