/*
 * Subtitles ...
 * 
 * Official website: thecrewchief.org 
 * License: MIT
 */
using CrewChiefV4.SharedMemory;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CircularBuffer;


namespace CrewChiefV4.Audio
{
    internal static class SubtitleManager
    {
        private static readonly string spotterName = UserSettings.GetUserSettings().getString("spotter_name");
        private static readonly String nameToUseForSubtitles = CrewChief.gameDefinition != null && CrewChief.gameDefinition.racingType == CrewChief.RacingType.Rally ? UserSettings.GetUserSettings().getString("codriver_name") : UserSettings.GetUserSettings().getString("chief_name");
        public static bool enableSubtitles = UserSettings.GetUserSettings().getBoolean("enable_subtitle_overlay") || UserSettings.GetUserSettings().getBoolean("enable_shared_memory");
        public static CircularBuffer<Phrase> phraseBuffer = new CircularBuffer<Phrase>(10);

        static SubtitleManager()
        {

            if (nameToUseForSubtitles.Contains("default"))
            {
                nameToUseForSubtitles = nameToUseForSubtitles.Remove(nameToUseForSubtitles.IndexOf(" (default)"));
            }            
        }
        internal static void AddPhraseForSpeech(string phrase)
        {
            Console.WriteLine("[Subtitle] You: " + phrase);
            lock(phraseBuffer)
            {
                phraseBuffer.PushFront(new Phrase(0, "You", phrase, (int)PhraseVoiceType.you));
                if (CrewChief.enableSharedMemory)
                {
                    CrewChief.sharedMemoryManager.WritePhrases(phraseBuffer.ToArray());
                    CrewChief.sharedMemoryManager.UpdateVariable("numTotalPhrases", new int[1] { phraseBuffer.Size });
                    CrewChief.sharedMemoryManager.UpdateVariable("lastPhraseIndex", new int[1] { 0 });
                }
            }
        }
        internal static void AddPhrase(List<SingleSound> singleSoundsToPlay, SoundMetadata soundMetadata)
        {
            int numberStringsCount = 0;
            int numbersAdded = 0;
            string subtitleFullString = "";
            foreach (var singleSound in singleSoundsToPlay)
            {
                if (singleSound.isNumber)
                {
                    numberStringsCount++;
                }
            }

            string voiceName = "";
            if (!singleSoundsToPlay[0].isPause && !singleSoundsToPlay[0].isBleep)
            {
                voiceName = soundMetadata.type == SoundType.SPOTTER ? spotterName : nameToUseForSubtitles;
            }           
            for (int i = 0; i < singleSoundsToPlay.Count(); i++)
            {
                //singleSoundsToPlay[i].loadSubtitleBeforePlaying = true; //TBD SpeechTrace hack
                string subtitle = singleSoundsToPlay[i].GetSubtitle();
                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    if (i == singleSoundsToPlay.Count() - 1)
                    {
                        subtitleFullString += subtitle;
                    }
                    else
                    {
                        if (singleSoundsToPlay[i].isNumber)
                        {
                            if (numbersAdded == 0 && numberStringsCount > 1 && !subtitle.Contains(':') && !subtitle.Contains('.'))
                            {
                                subtitleFullString += subtitle + ':';
                                numbersAdded++;
                            }
                            else
                            {
                                if (i <= singleSoundsToPlay.Count() - 2 && !singleSoundsToPlay[i+1].isNumber)
                                {
                                    subtitleFullString += subtitle + " ";
                                }
                                else
                                {
                                    subtitleFullString += subtitle;
                                }
                            }
                        }
                        else
                        {
                            subtitleFullString += subtitle + " ";
                        }                        
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(subtitleFullString))
            {
                Console.WriteLine("[Subtitle] " + voiceName + ": "+ subtitleFullString);
                lock(phraseBuffer)
                {
                    phraseBuffer.PushFront(new Phrase(0, voiceName, subtitleFullString, soundMetadata.type == SoundType.SPOTTER ? (int)PhraseVoiceType.spotter : (int)PhraseVoiceType.chief));
                    if (CrewChief.enableSharedMemory)
                    {
                        CrewChief.sharedMemoryManager.WritePhrases(phraseBuffer.ToArray());
                        CrewChief.sharedMemoryManager.UpdateVariable("numTotalPhrases", new int[1] { phraseBuffer.Size });
                        CrewChief.sharedMemoryManager.UpdateVariable("lastPhraseIndex", new int[1] { 0 });
                    }
                }
            }
        }

        internal static string LoadSubtitleForSound(string fullPath)
        {
            string subtitle = "";
            string firstSubtitle = "";
            bool gotFirstLine = false;
            bool gotSubtitle = false;
            string directoryName = Path.GetDirectoryName(fullPath);
            string soundFileName = Path.GetFileName(fullPath);
            string subtitleFileName = "subtitles.csv";
            string subtitleFile = Path.Combine(directoryName, subtitleFileName);

            if (File.Exists(subtitleFile))
            {
                using (TextFieldParser csvParser = new TextFieldParser(subtitleFile))
                {
                    csvParser.CommentTokens = new string[] { "#" };
                    csvParser.SetDelimiters(new string[] { "," });
                    csvParser.HasFieldsEnclosedInQuotes = true;
                    while (!csvParser.EndOfData)
                    {
                        // Read current line fields, pointer moves to the next line.
                        string[] fields = csvParser.ReadFields();
                        if (fields.Length < 2)
                        {
                            continue;
                        }
                        string filename = fields[0];
                        string text = fields[1];
                        // cache the first line for cases where I'm too bone-idle to add to the subtitles file
                        if (!gotFirstLine)
                        {
                            gotFirstLine = true;
                            firstSubtitle = text;
                        }
                        if (Path.GetFileName(soundFileName).Equals(filename))
                        {
                            subtitle = text;
                            gotSubtitle = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                // no subtitle file, fall back to the folder name with some magic exception(s)
                string folderName = new DirectoryInfo(fullPath).Parent.Name;
                if (!"fx".Equals(folderName) && !"breath_in".Equals(folderName))
                {
                    return folderName.Replace("_", " ");
                }
            }
            if (gotFirstLine && !gotSubtitle)
            {
                return firstSubtitle;
            }
            return subtitle;
        }
        internal static string ParseSubtitleForPersonalisation(string fullPath)
        {
            string subtitle = "";
            var rawPersonalisation = new DirectoryInfo(Path.GetDirectoryName(fullPath)).Name;
            var personalisationName = Directory.GetParent(fullPath).Parent.Parent.Name;

            String[] sep = { "_" };
            Int32 count = 2;

            // using the method 
            String[] strlist = rawPersonalisation.Split(sep, count, StringSplitOptions.RemoveEmptyEntries);
            // only sound i found that's not a direct translatioon of folder name.

            // for more personalisations there's a predictable order to the sound file names, but this isn't always
            // the case. Assume the order is as expected but only if the file appears in the expected folder. This 
            // will catch most cases
            if (rawPersonalisation == "oh_dear")
            {
                if (fullPath.EndsWith("3.wav"))
                {
                    subtitle = "oh bad luck " + personalisationName;
                }
                else if (fullPath.EndsWith("9.wav"))
                {
                    subtitle = "oh no " + personalisationName;
                }
                else if (fullPath.EndsWith("10.wav"))
                {
                    subtitle = "oh " + personalisationName;
                }
                else if (fullPath.EndsWith("sweary_11.wav"))
                {
                    subtitle = "fuck's sake " + personalisationName;
                }
                else if (fullPath.EndsWith("sweary_12.wav"))
                {
                    subtitle = "shit " + personalisationName;
                }
                else if (fullPath.Contains("sweary"))
                {
                    subtitle = "fuck's sake " + personalisationName;
                }
            }            
            else if (fullPath.EndsWith("22.wav") && rawPersonalisation == "well_done")
            {
                subtitle = "nice one " + personalisationName;
            }
            else if (fullPath.EndsWith("16.wav") && rawPersonalisation == "ok")
            {
                subtitle = "alright " + personalisationName;
            }
            else if (strlist.Length == 1)
            {
                subtitle = strlist[0] + " " + personalisationName;
            }
            else if(strlist.Length == 2)
            {
                subtitle = strlist[0] + " " + strlist[1] + " " + personalisationName;
            }
            return subtitle;
        }

        internal static string ParseSubtitleForNumber(string directoryName, SingleSound ss)
        {
            string subtitle = "";
            var rawNumberString = new DirectoryInfo(Path.GetDirectoryName(directoryName)).Name;
            if (rawNumberString.Equals("point"))
            {
                ss.isNumber = true;
                return ".";
            }
            else if (rawNumberString.Equals("oh"))
            {
                ss.isNumber = true;
                return "0";
            }
            else if (rawNumberString.Equals("zerozero"))
            {
                ss.isNumber = true;
                return "00";
            }
            else if (rawNumberString.Equals("seconds") || rawNumberString.Equals("second"))
            {
                return rawNumberString;
            }

            String[] sep = { "_", "point", "seconds" };
            Int32 count = 3;

            // using the method 
            String[] strlist = rawNumberString.Split(sep, count, StringSplitOptions.RemoveEmptyEntries);

            string outString = "";
            if (strlist.Length == 1)
            {
                if (int.TryParse(strlist[0], out int number))
                {
                    ss.isNumber = true;
                }
                if (rawNumberString.Contains("point"))
                {
                    //Console.WriteLine("index = " + rawNumberString.IndexOf("point"));
                    if (rawNumberString.IndexOf("point") == 0)
                    {
                        outString = '.' + strlist[0];
                        if (rawNumberString.Contains("seconds"))
                        {
                            outString += " seconds";
                        }
                    }
                    else
                    {
                        outString = strlist[0] + '.';
                        if (rawNumberString.Contains("seconds"))
                        {
                            outString += " seconds";
                        }
                    }
                }
                else
                {
                    outString = strlist[0];
                }
            }
            else if (strlist.Length == 2)
            {
                if (int.TryParse(strlist[0], out int firstNumber) || int.TryParse(strlist[1], out int secondNumber))
                {
                    ss.isNumber = true;
                }
                if (rawNumberString.Contains("point"))
                {
                    outString = strlist[0] + '.' + strlist[1];
                    if (rawNumberString.Contains("seconds"))
                    {
                        outString += " seconds";
                    }
                }
                else if (rawNumberString.Contains("_") && ss.isNumber)
                {
                    outString = strlist[0] + ':' + strlist[1];
                }
                else
                {
                    outString = strlist[0] + ' ' + strlist[1];
                }
            }
            subtitle = outString;
            return subtitle;
        }
    }
}
