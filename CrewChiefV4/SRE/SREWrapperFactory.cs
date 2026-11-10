using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CrewChiefV4.SRE
{
    class SREWrapperFactory
    {
        // RRCA liefert Microsoft.Speech.dll nicht mit (Weitergaberecht ungeklärt). Ohne die DLL bleibt es bei der
        // Windows-Spracherkennung. Jeder Code mit Microsoft.Speech-Typen steht in eigenen NoInlining-Methoden,
        // damit der JIT ihn nur kompiliert, wenn die DLL tatsächlich geladen werden kann.
        public static readonly bool microsoftSpeechAvailable = IsMicrosoftSpeechAvailable();

        public static bool useSystem = UserSettings.GetUserSettings().getBoolean("prefer_system_sre") || !microsoftSpeechAvailable;

        private static readonly bool writeDebugData = UserSettings.GetUserSettings().getBoolean("save_sre_debug_data");

        private static bool IsMicrosoftSpeechAvailable()
        {
            try
            {
                System.Reflection.Assembly.Load(new System.Reflection.AssemblyName("Microsoft.Speech, Version=11.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"));
                return true;
            }
            catch (Exception)
            {
                Console.WriteLine("Microsoft.Speech.dll nicht vorhanden, Sprachbefehle laufen über die Windows-Spracherkennung");
                return false;
            }
        }

        // try to create the preferred SRE impl, fall back to the other type if this isn't available
        // if endSilenceTimeoutAmbiguous is not null we override the default endSilenceTimeoutAmbiguous in the SRE impl
        public static SREWrapper createNewSREWrapper(CultureInfo culture, TimeSpan? endSilenceTimeoutAmbiguous, Boolean log = false)
        {
            SREWrapper sreWrapper = null;
            if (useSystem || !microsoftSpeechAvailable)
            {
                useSystem = true;
                sreWrapper = createSystemSREWrapper(culture, endSilenceTimeoutAmbiguous);
                if (sreWrapper == null)
                {
                    if (microsoftSpeechAvailable)
                    {
                        if (log) Console.WriteLine("Unable to create a System SRE, trying with Microsoft SRE");
                        sreWrapper = createMicrosoftSREWrapper(culture, endSilenceTimeoutAmbiguous);
                        if (sreWrapper != null)
                        {
                            useSystem = false;
                            if (log) Console.WriteLine("Falling back to Microsoft SRE");
                        }
                    }
                    else if (log)
                    {
                        Console.WriteLine("Unable to create a System SRE");
                    }
                }
                else
                {
                    if (log) Console.WriteLine("Successfully initialised preferred System SRE");
                }
            }
            else
            {
                sreWrapper = createMicrosoftSREWrapper(culture, endSilenceTimeoutAmbiguous);
                if (sreWrapper == null)
                {
                    if (log) Console.WriteLine("Unable to create a Microsoft SRE, trying with System SRE");
                    sreWrapper = createSystemSREWrapper(culture, endSilenceTimeoutAmbiguous);
                    if (sreWrapper != null)
                    {
                        useSystem = true;
                        if (log) Console.WriteLine("Falling back to System SRE");
                    }
                }
                else
                {
                    if (log) Console.WriteLine("Successfully initialised preferred Microsoft SRE");
                }
            }
            return sreWrapper;
        }

        public static GrammarWrapper CreateChatDictationGrammarWrapper()
        {
            System.Speech.Recognition.DictationGrammar dictationGrammar = new System.Speech.Recognition.DictationGrammar();
            dictationGrammar.Name = "default dictation";
            dictationGrammar.Enabled = true;
            return new SystemGrammarWrapper(dictationGrammar);
        }

        public static void LoadChatDictationGrammar(SREWrapper sreWrapper, GrammarWrapper dictationGrammarWrapper, String dictationContextStart, String dictationContextEnd)
        {
            System.Speech.Recognition.DictationGrammar dictationGrammar = (System.Speech.Recognition.DictationGrammar)dictationGrammarWrapper.GetInternalGrammar();
            dictationGrammar.Weight = 0.2f;
            ((System.Speech.Recognition.SpeechRecognitionEngine)sreWrapper.GetInternalSRE()).LoadGrammar(dictationGrammar);
            dictationGrammar.SetDictationContext(dictationContextStart, dictationContextEnd);
        }

        private static SREWrapper createMicrosoftSREWrapper(CultureInfo culture, TimeSpan? endSilenceTimeoutAmbiguous)
        {
            if (!microsoftSpeechAvailable)
            {
                return null;
            }
            try
            {
                return newMicrosoftSREWrapper(culture, endSilenceTimeoutAmbiguous);
            }
            catch (Exception)
            {
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static SREWrapper newMicrosoftSREWrapper(CultureInfo culture, TimeSpan? endSilenceTimeoutAmbiguous)
        {
            return new MicrosoftSREWrapper(culture, endSilenceTimeoutAmbiguous, writeDebugData);
        }

        private static SREWrapper createSystemSREWrapper(CultureInfo culture, TimeSpan? endSilenceTimeoutAmbiguous)
        {
            try
            {
                return new SystemSREWrapper(culture, endSilenceTimeoutAmbiguous, writeDebugData);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static GrammarWrapper createNewGrammarWrapper(GrammarBuilderWrapper grammarBuilderWrapper, string grammarName)
        {
            if (useSystem)
            {
                return new SystemGrammarWrapper(grammarBuilderWrapper, grammarName);
            }
            else
            {
                return MicrosoftOnly.GrammarWrapper(grammarBuilderWrapper, grammarName);
            }
        }

        public static GrammarBuilderWrapper createNewGrammarBuilderWrapper()
        {
            if (useSystem)
            {
                return new SystemGrammarBuilderWrapper();
            }
            else
            {
                return MicrosoftOnly.GrammarBuilderWrapper();
            }
        }

        public static GrammarBuilderWrapper createNewGrammarBuilderWrapper(ChoicesWrapper choicesWrapper)
        {
            if (useSystem)
            {
                return new SystemGrammarBuilderWrapper(choicesWrapper);
            }
            else
            {
                return MicrosoftOnly.GrammarBuilderWrapper(choicesWrapper);
            }
        }

        public static ChoicesWrapper createNewChoicesWrapper()
        {
            if (useSystem)
            {
                return new SystemChoicesWrapper();
            }
            else
            {
                return MicrosoftOnly.ChoicesWrapper();
            }
        }

        public static ChoicesWrapper createNewChoicesWrapper(string[] choices)
        {
            if (choices.Length == 0)
            {
                return createNewChoicesWrapper();
            }
            if (useSystem)
            {
                return new SystemChoicesWrapper(choices);
            }
            else
            {
                return MicrosoftOnly.ChoicesWrapper(choices);
            }
        }

        public static CultureInfo GetCultureInfo(String langAndCountryToUse, String langToUse, Boolean log = false)
        {
            try
            {
                if (useSystem)
                {
                    // first check we can get the system installed recognisers
                    ReadOnlyCollection<System.Speech.Recognition.RecognizerInfo> systemRecognisers = null;
                    try
                    {
                        systemRecognisers = System.Speech.Recognition.SpeechRecognitionEngine.InstalledRecognizers();
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Unable to get System (Windows) speech recogniser");
                        return null;
                    }
                    if (langAndCountryToUse != null && langAndCountryToUse.Length == 5)
                    {
                        if (log) Console.WriteLine("Attempting to get recogniser for " + langAndCountryToUse);
                        foreach (System.Speech.Recognition.RecognizerInfo ri in systemRecognisers)
                        {
                            if (ri.Culture.Name.Equals(langAndCountryToUse))
                            {
                                return ri.Culture;
                            }
                        }
                    }
                    if (log && langAndCountryToUse != null && langAndCountryToUse.Length == 5)
                    {
                        Console.WriteLine("Failed to get recogniser for " + langAndCountryToUse);
                    }
                    if (log) Console.WriteLine("Attempting to get recogniser for " + langToUse);
                    foreach (System.Speech.Recognition.RecognizerInfo ri in systemRecognisers)
                    {
                        if (ri.Culture.TwoLetterISOLanguageName.Equals(langToUse))
                        {
                            return ri.Culture;
                        }
                    }
                }
                else if (microsoftSpeechAvailable)
                {
                    return MicrosoftOnly.GetCultureInfo(langAndCountryToUse, langToUse, log);
                }
            }
            catch (Exception e)
            {
                // the engine may return a null InstalledRecognizers List
                Log.Exception(e, "Unable to get a SRE CultureInfo object: ");
            }
            return null;
        }

        public static float GetCallbackConfidence(object recognitionCallback)
        {
            if (recognitionCallback is System.Speech.Recognition.SpeechRecognizedEventArgs)
            {
                return ((System.Speech.Recognition.SpeechRecognizedEventArgs)recognitionCallback).Result.Confidence;
            }
            return MicrosoftOnly.Confidence(recognitionCallback);
        }

        public static string GetCallbackText(object recognitionCallback)
        {
            if (recognitionCallback is System.Speech.Recognition.SpeechRecognizedEventArgs)
            {
                return ((System.Speech.Recognition.SpeechRecognizedEventArgs)recognitionCallback).Result.Text;
            }
            return MicrosoftOnly.Text(recognitionCallback);
        }

        /// <summary>
        /// Gets the words generated by a speech recognizer from recognized input.
        /// </summary>
        /// <returns>
        /// The list of words generated by a speech recognizer for recognized input.
        /// </returns>
        public static string[] GetCallbackWordsList(object recognitionCallback)
        {
            if (recognitionCallback is System.Speech.Recognition.SpeechRecognizedEventArgs)
            {
                return ((System.Speech.Recognition.SpeechRecognizedEventArgs)recognitionCallback).Result.Words.Select(x => x.Text).ToArray();
            }
            return MicrosoftOnly.Words(recognitionCallback);
        }

        /// <summary>
        /// Gets the Recognition.Grammar that the speech recognizer
        /// used to return the Recognition.RecognizedPhrase.
        /// </summary>
        /// <returns>
        /// The grammar object that the speech recognizer used to identify the input.
        /// </returns>
        public static object GetCallbackGrammar(object recognitionCallback)
        {
            if (recognitionCallback is System.Speech.Recognition.SpeechRecognizedEventArgs)
            {
                return ((System.Speech.Recognition.SpeechRecognizedEventArgs)recognitionCallback).Result.Grammar;
            }
            return MicrosoftOnly.Grammar(recognitionCallback);
        }

        public static List<string> WriteSREDebugData(object recognitionCallback, Stream stream, SREWrapper sreWrapper)
        {
            List<string> metadata = new List<string>();
            bool gotResult = false;
            string recognisedText = "";
            float confidence = 0;
            if (recognitionCallback is System.Speech.Recognition.RecognizeCompletedEventArgs)
            {
                var callbackArgs = (System.Speech.Recognition.RecognizeCompletedEventArgs)recognitionCallback;
                if (callbackArgs.Result != null)
                {
                    gotResult = true;
                    var recognisedAudio = callbackArgs.Result.Audio;
                    recognisedText = callbackArgs.Result.Text;
                    confidence = callbackArgs.Result.Confidence;
                    recognisedAudio.GetRange(TimeSpan.FromSeconds(0), recognisedAudio.Duration).WriteToWaveStream(stream);
                }
            }
            else if (recognitionCallback is System.Speech.Recognition.SpeechRecognitionRejectedEventArgs)
            {
                var callbackArgs = (System.Speech.Recognition.SpeechRecognitionRejectedEventArgs)recognitionCallback;
                if (callbackArgs.Result != null)
                {
                    gotResult = true;
                    var recognisedAudio = callbackArgs.Result.Audio;
                    recognisedText = callbackArgs.Result.Text;
                    confidence = callbackArgs.Result.Confidence;
                    recognisedAudio.GetRange(TimeSpan.FromSeconds(0), recognisedAudio.Duration).WriteToWaveStream(stream);
                }
            }
            else if (microsoftSpeechAvailable)
            {
                gotResult = MicrosoftOnly.WriteDebugAudio(recognitionCallback, stream, out recognisedText, out confidence);
            }
            if (!gotResult)
            {
                metadata.Add("No SRE result");
            }
            else
            {
                metadata.Add("Recognised text: " + recognisedText);
                metadata.Add("Confidence: " + confidence);
            }
            metadata.Add("Max audio level: " + sreWrapper.GetMaxAudioLevelForLastOperation());
            metadata.Add("Reported problems: " + string.Join(",", sreWrapper.GetReportedProblemsForLastOperation()));
            return metadata;
        }

        public static string GetCallBackErrorText(object recognitionRejected)
        {
            if (recognitionRejected is System.Speech.Recognition.SpeechRecognitionRejectedEventArgs)
            {
                return ((System.Speech.Recognition.SpeechRecognitionRejectedEventArgs)recognitionRejected).Result.Text;
            }
            return MicrosoftOnly.ErrorText(recognitionRejected);
        }

        /// <summary>
        /// Alles, was Microsoft.Speech-Typen anfasst. Wird nur aufgerufen, wenn die DLL geladen werden konnte.
        /// </summary>
        private static class MicrosoftOnly
        {
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static GrammarWrapper GrammarWrapper(GrammarBuilderWrapper builder, string name) => new MicrosoftGrammarWrapper(builder, name);

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static GrammarBuilderWrapper GrammarBuilderWrapper() => new MicrosoftGrammarBuilderWrapper();

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static GrammarBuilderWrapper GrammarBuilderWrapper(ChoicesWrapper choices) => new MicrosoftGrammarBuilderWrapper(choices);

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static ChoicesWrapper ChoicesWrapper() => new MicrosoftChoicesWrapper();

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static ChoicesWrapper ChoicesWrapper(string[] choices) => new MicrosoftChoicesWrapper(choices);

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static float Confidence(object callback) => ((Microsoft.Speech.Recognition.SpeechRecognizedEventArgs)callback).Result.Confidence;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string Text(object callback) => ((Microsoft.Speech.Recognition.SpeechRecognizedEventArgs)callback).Result.Text;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string[] Words(object callback) => ((Microsoft.Speech.Recognition.SpeechRecognizedEventArgs)callback).Result.Words.Select(x => x.Text).ToArray();

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static object Grammar(object callback) => ((Microsoft.Speech.Recognition.SpeechRecognizedEventArgs)callback).Result.Grammar;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static string ErrorText(object callback) => ((Microsoft.Speech.Recognition.SpeechRecognitionRejectedEventArgs)callback).Result.Text;

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static bool WriteDebugAudio(object recognitionCallback, Stream stream, out string recognisedText, out float confidence)
            {
                recognisedText = "";
                confidence = 0;
                Microsoft.Speech.Recognition.RecognitionResult result = null;
                if (recognitionCallback is Microsoft.Speech.Recognition.RecognizeCompletedEventArgs)
                {
                    result = ((Microsoft.Speech.Recognition.RecognizeCompletedEventArgs)recognitionCallback).Result;
                }
                else if (recognitionCallback is Microsoft.Speech.Recognition.SpeechRecognitionRejectedEventArgs)
                {
                    result = ((Microsoft.Speech.Recognition.SpeechRecognitionRejectedEventArgs)recognitionCallback).Result;
                }
                if (result == null)
                {
                    return false;
                }
                recognisedText = result.Text;
                confidence = result.Confidence;
                result.Audio.GetRange(TimeSpan.FromSeconds(0), result.Audio.Duration).WriteToWaveStream(stream);
                return true;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static CultureInfo GetCultureInfo(String langAndCountryToUse, String langToUse, Boolean log)
            {
                // first check we can get the microsoft installed recognisers
                ReadOnlyCollection<Microsoft.Speech.Recognition.RecognizerInfo> microsoftRecognisers = null;
                try
                {
                    microsoftRecognisers = Microsoft.Speech.Recognition.SpeechRecognitionEngine.InstalledRecognizers();
                }
                catch (Exception)
                {
                    Console.WriteLine("Unable to get Microsoft speech recogniser. Is SpeechPlatformRuntime.msi installed?");
                    return null;
                }
                if (langAndCountryToUse != null && langAndCountryToUse.Length == 5)
                {
                    if (log) Console.WriteLine("Attempting to get recogniser for " + langAndCountryToUse + " package name MSSpeech_SR_" + langAndCountryToUse + "_TELE.msi");
                    foreach (Microsoft.Speech.Recognition.RecognizerInfo ri in microsoftRecognisers)
                    {
                        if (ri.Culture.Name.Equals(langAndCountryToUse))
                        {
                            return ri.Culture;
                        }
                    }
                }
                if (log && langAndCountryToUse != null && langAndCountryToUse.Length == 5)
                {
                    Console.WriteLine("Failed to get recogniser for " + langAndCountryToUse);
                }
                if (log) Console.WriteLine("Attempting to get recogniser for " + langToUse + " package name MSSpeech_SR_" + langToUse + "-XX_TELE.msi");
                foreach (Microsoft.Speech.Recognition.RecognizerInfo ri in microsoftRecognisers)
                {
                    if (ri.Culture.TwoLetterISOLanguageName.Equals(langToUse))
                    {
                        return ri.Culture;
                    }
                }
                return null;
            }
        }
    }
}
