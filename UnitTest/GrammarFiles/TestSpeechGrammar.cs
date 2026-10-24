using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Speech.Recognition;
using System.Speech.Recognition.SrgsGrammar;
using System.Threading;
using System.Windows.Forms;

using CrewChiefV4.GrammarFiles;


namespace SpeechGrammar
{
    /// <summary>
    /// Hacked-together ASR class for testing grammar files
    /// See also testWavFile()
    /// </summary>
    public class TestSpeechGrammar
    {
        public static bool recognised = false;

        public static SpeechRecognizedEventArgs speechRecognizedEventResult = null;

        static void sr_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string str = e.Result.Text;
            string subj, obj;
            bool errorCondition = false;
            speechRecognizedEventResult = e;
            if (e.Result != null)
            {
                if (e.Result.Semantics != null) // && e.Result.Semantics.Count != 0)
                {
                    SemanticValue semantics = e.Result.Semantics;
                    if (e.Result.Semantics.ContainsKey("hows_my"))
                    {
                        subj = "hows_my (" + e.Result.Semantics["hows_my"].Value.ToString() + ")";
                    }
                    else errorCondition = true;

                    if (e.Result.Semantics.ContainsKey("hows_my_objects"))
                    {
                        obj = e.Result.Semantics["hows_my_objects"].Value.ToString();
                    }
                    else errorCondition = true;
                    /*Console.WriteLine(String.Format(
                              "Query: {0}\nObject: {1}",
                              subj, obj));*/
                    return;
                    if (!errorCondition)
                    {
                        MessageBox.Show(String.Format(
                              "Reco string: {0}\nQuery: {1}\nObject: {2}",
                              str, subj, obj));
                    }
                    else
                    {
                        MessageBox.Show(
                              "e.Result.Semantics is null or e.Result.Semantics.Count == 0");
                    }
                }
                else
                {
                    MessageBox.Show("e.Result is null");
                }
            }
        }
        static bool completed;
        // Send emulated input to the recognizer for asynchronous  
        // recognition.  
        private static void TestRecognizeAsync(
          SpeechRecognitionEngine recognizer, string input)
        {
            completed = false;

            Console.WriteLine("TestRecognizeAsync(\"{0}\")...", input);
            recognizer.EmulateRecognizeAsync(input);

            // Wait for the operation to complete.  
            while (!completed)
            {
                Thread.Sleep(333);
            }

            Console.WriteLine(" Done.");
            Console.WriteLine();
        }
        static void SpeechDetectedHandler(
          object sender, SpeechDetectedEventArgs e)
        {
            recognised = true; // Console.WriteLine(" SpeechDetected event raised.");
        }

        static void SpeechHypothesizedHandler(
          object sender, SpeechHypothesizedEventArgs e)
        {
            //Console.WriteLine(" SpeechHypothesized event raised.");
            if (e.Result != null)
            {
                // Console.WriteLine("  Grammar = {0}; Text = {1}",
                //  e.Result.Grammar.Name ?? "<none>", e.Result.Text);
            }
            else
            {
                Console.WriteLine("  No recognition result available.");
            }
        }

        // Handle events.  
        static void SpeechRecognitionRejectedHandler(
          object sender, SpeechRecognitionRejectedEventArgs e)
        {
            //Console.WriteLine(" SpeechRecognitionRejected event raised.");
            if (e.Result != null)
            {
                string grammarName;
                if (e.Result.Grammar != null)
                {
                    grammarName = e.Result.Grammar.Name ?? "<none>";
                }
                else
                {
                    grammarName = "<not available>";
                }
                Console.WriteLine("  Grammar = {0}; Text = {1}",
                  grammarName, e.Result.Text);
            }
            else
            {
                Console.WriteLine("  No recognition result available.");
            }
        }

        static void SpeechRecognizedHandler(
          object sender, SpeechRecognizedEventArgs e)
        {
            //Console.WriteLine(" SpeechRecognized event raised.");
            speechRecognizedEventResult = e;
            if (e.Result != null)
            {
                /*Console.WriteLine("  Grammar = {0}; Text = {1}",
                  e.Result.Grammar.Name ?? "<none>", e.Result.Text);*/
            }
            else
            {
                Console.WriteLine("  No recognition result available.");
            }
        }

        static void EmulateRecognizeCompletedHandler(
          object sender, EmulateRecognizeCompletedEventArgs e)
        {
            Console.WriteLine(" EmulateRecognizeCompleted event raised.");

            if (e.Error != null)
            {
                Console.WriteLine("  {0} exception encountered: {1}:",
                  e.Error.GetType().Name, e.Error.Message);
            }
            else if (e.Cancelled)
            {
                Console.WriteLine("  Operation cancelled.");
            }
            else if (e.Result != null)
            {
                Console.WriteLine("  Grammar = {0}; Text = {1}",
                  e.Result.Grammar.Name ?? "<none>", e.Result.Text);
            }
            else
            {
                Console.WriteLine("  No recognition result available.");
            }

            completed = true;
        }

        static int maxAudioLevel = 0;
        static List <string> lastProblems = new List<string>();
        static void recognizer_AudioLevelUpdated(object sender, AudioLevelUpdatedEventArgs e)
        {
            if (maxAudioLevel < e.AudioLevel)
            {
                maxAudioLevel = e.AudioLevel;
            }
        }

        // Gather information when the AudioSignalProblemOccurred event is raised.  
        static void recognizer_AudioSignalProblemOccurred(object sender, AudioSignalProblemOccurredEventArgs e)
        {
                lastProblems.Add(e.AudioSignalProblem.ToString());
        }
        
        public class SR
        {
            public SpeechRecognitionEngine sr = new SpeechRecognitionEngine(new CultureInfo("en-GB"));

            SrgsDocument document;

            // Create a Grammar object, initializing it with the root rule.
            Grammar grammarObj;
            public SR()
            {
                // Add event handlers for the events raised by the  
                // EmulateRecognizeAsync method.  
                sr.SpeechDetected +=
                  new EventHandler<SpeechDetectedEventArgs>(
                    SpeechDetectedHandler);
                sr.SpeechHypothesized +=
                  new EventHandler<SpeechHypothesizedEventArgs>(
                    SpeechHypothesizedHandler);
                sr.SpeechRecognitionRejected +=
                  new EventHandler<SpeechRecognitionRejectedEventArgs>(
                    SpeechRecognitionRejectedHandler);
                sr.SpeechRecognized +=
                  new EventHandler<SpeechRecognizedEventArgs>(
                    SpeechRecognizedHandler);
                sr.EmulateRecognizeCompleted +=
                  new EventHandler<EmulateRecognizeCompletedEventArgs>(
                    EmulateRecognizeCompletedHandler);

                // Attach a handler for the SpeechRecognized event.
                sr.SpeechRecognized +=
                  new EventHandler<SpeechRecognizedEventArgs>(sr_SpeechRecognized);

                sr.AudioLevelUpdated += new EventHandler<AudioLevelUpdatedEventArgs>(recognizer_AudioLevelUpdated); ;
                sr.AudioSignalProblemOccurred += new EventHandler<AudioSignalProblemOccurredEventArgs>(recognizer_AudioSignalProblemOccurred);
                maxAudioLevel = 0;
                lastProblems = new List<string>();

                sr.SetInputToNull();
            }
             public string testGrammar(string cmd)
            {
                string semantic = null;
                var result = sr.EmulateRecognize(cmd);
                if (result != null)
                {
                    semantic = result.Semantics.Value.ToString();
                }
                else
                {
                    Console.WriteLine("No recognition result");
                }
                return semantic;
            }

            public List<string> testWavFile(string wavFileName)
            {
                string semantic = null;
                string confidence = null;
                var srO = new SR();
                srO.sr.SetInputToWaveFile(wavFileName);
                SystemGrammarFiles.LoadGrammar(srO.sr, "../../../cc_sample_grammar.xml", null);
                Thread.Sleep(333);  // give grammar time to load
                speechRecognizedEventResult = null;
                recognised = false;
                srO.sr.RecognizeAsync();
                var maxCount = 10;
                while (!recognised && maxCount-- > 0)
                {
                    Thread.Sleep(100);
                }
                var result = speechRecognizedEventResult;
                if (result != null)
                {
                    semantic = result.Result.Semantics.Value.ToString();
                    confidence = result.Result.Confidence.ToString();
                }
                else
                {
                    Console.WriteLine("No recognition result");
                }
                List <string> results = new List <string> { "GRXML result", semantic, confidence, maxAudioLevel.ToString()};
                if (lastProblems != null)
                {
                    foreach (var prob in lastProblems)
                    {
                        results.Add(prob);
                    }
                }
                return results;
            }

        }
        static void Main(string[] args)
        {
            // Class version
            var srO = new SR();
            SystemGrammarFiles.LoadGrammar(srO.sr, "../../../../pitstop_template.grxml", null);
            Thread.Sleep(333);  // give grammar time to load
            string command;
            command ="How are my bodywork";
            //Console.WriteLine(command + ":\t" + srO.testGrammar(command));
            command ="Chat how are you today";
            //Console.WriteLine(command + ":\t" + srO.testGrammar(command));
            command ="Pitstop clear tyres";
            Console.WriteLine(command + ":\t" + srO.testGrammar(command));
            command ="Box fuel to the end";
            Console.WriteLine(command + ":\t" + srO.testGrammar(command));
            command ="Pit stop fuel to the end of the race";
            Console.WriteLine(command + ":\t" + srO.testGrammar(command));
            command ="Box prime";
            Console.WriteLine(command + ":\t" + srO.testGrammar(command));
            command = "Pit crew next tyre compound";
            Console.WriteLine(command + ":\t" + srO.testGrammar(command));
            command ="Box change left side tyres";
            Console.WriteLine(command + ":\t" + srO.testGrammar(command));
            command ="Box change front tyre pressures";
            Console.WriteLine(command + ":\t" + srO.testGrammar(command));
            command ="Box change left rear tyre pressure";
            Console.WriteLine(command + ":\t" + srO.testGrammar(command));

            Thread.Sleep(333);
            Console.WriteLine("press any key to exit...");
            Console.ReadKey(true);

        }
    }
}
