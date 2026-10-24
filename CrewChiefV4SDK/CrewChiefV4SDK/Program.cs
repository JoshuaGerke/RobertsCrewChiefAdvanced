using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace CrewChiefV4SharedMemory
{
    class Program
    {        
        static void Main(string[] args)
        {
            CrewChiefV4SDK sdk = new CrewChiefV4SDK();
            Int64 lastPhraseTime = 0;
            try
            {
                while (true)
                {                    
                    if(!sdk.IsConnected())
                    {
                        sdk.initialize();
                        // uncomment to dump a C# class with all available variable
                        // sdk.GenerateCSharpDataClass();
                    }
                    if(sdk.IsUpdating()) // Read some data
                    {
                        int? numTotalPhrases = (int?)sdk.GetData("numTotalPhrases");                       
                        if (numTotalPhrases != null)
                        {
                            Int64[] phraseFileTimes = (Int64[])sdk.GetData("phraseFileTimes");
                            int? lastPhraseIndex = (int?)sdk.GetData("lastPhraseIndex");
                            int Index = lastPhraseIndex.Value;
                            if (phraseFileTimes[Index] != lastPhraseTime)
                            {
                                string[] phraseVoiceNames = (string[])sdk.GetData("phraseVoiceNames");
                                string[] phrasePhrases = (string[])sdk.GetData("phrasePhrases");
                                if (!string.IsNullOrWhiteSpace(phraseVoiceNames[Index]) && !string.IsNullOrWhiteSpace(phrasePhrases[Index]))
                                {
                                    Console.WriteLine("[Subtitle]" + phraseVoiceNames[Index] + ": " + phrasePhrases[Index]);
                                    lastPhraseTime = phraseFileTimes[Index];
                                }
                            }
                        }
                        sdk.Tick();                       
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e .Message + "Try Running CrewChiefV4 first.");
            }

        }
    }
}
