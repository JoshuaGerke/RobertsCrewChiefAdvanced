using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;

namespace CrewChiefV4.NumberProcessing
{
    public static class SpokenNumberParser
    {
        enum states {
            TEXT,
            DIGIT,
            DECADE,
            HUNDRED
        }
        /// <summary>
        /// Parse a string for a number
        /// </summary>
        /// <param name="text">e.g. "box change tyre pressures twenty one point four five psi"</param>
        /// <returns>the number, or -1 if not able to parse one</returns>
        public static float Parse(string text)
        {
            float result;
            string stringToParse = "";
            states state = states.TEXT;
            // State machine depends on there being a word after the number so add "suffix"
            string[] words = (text + " suffix").Split(new Char[] { ' ', '-' });
            foreach (string word in words)
            {
                if (word == "and")
                {
                    continue;
                }
                int num = ExtractInt(word);
                switch (state)
                {
                    case states.TEXT:
                        if (SpeechRecogniser.POINT[0] == word)
                        {
                            stringToParse = "0.";
                        }
                        else if (num >= 0)
                        {
                            if (num > 99)
                            { 
                                num /= 100;
                                state = states.HUNDRED;
                            }
                            else if (num > 19)
                            { // e.g. "twenty one point two" or "point twenty five"
                                num /= 10;
                                state = states.DECADE;
                            }
                            else
                            {
                                state = states.DIGIT;
                            }

                            stringToParse += num.ToString();
                        }

                        break;
                    case states.DIGIT:
                        if (SpeechRecogniser.POINT[0] == word)
                        {
                            stringToParse += ".";
                        }
                        else if (num >= 0)
                        {
                            if (num > 99)
                            {
                                num /= 100;
                                state = states.HUNDRED;
                            }
                            else
                            {
                                if (num > 19)
                                { // e.g. "twenty one point two" or "point twenty five"
                                    num /= 10;
                                    state = states.DECADE;
                                }
                                else
                                {
                                    state = states.DIGIT;
                                }

                                stringToParse += num.ToString();
                            }
                        }
                        else
                        {
                            state = states.TEXT;
                        }

                        break;
                    case states.DECADE:
                        if (SpeechRecogniser.POINT[0] == word)
                        {
                            stringToParse += "0.";
                            state = states.DIGIT;
                        }
                        else if (num >= 0)
                        {
                            if (num > 19)
                            { // e.g. "twenty twenty two"
                                stringToParse += "0";
                                num /= 10;
                                state = states.DECADE;
                            }
                            else
                            {
                                state = states.DIGIT;
                            }

                            stringToParse += num.ToString();
                        }
                        else
                        {
                            stringToParse += "0";
                            state = states.TEXT;
                        }

                        break;
                    case states.HUNDRED:
                        if (SpeechRecogniser.POINT[0] == word)
                        {
                            stringToParse += "0.";
                            state = states.DIGIT;
                        }
                        else if (num >= 0)
                        {
                            if (num > 19)
                            { // e.g. "twenty twenty two"
                                num /= 10;
                                state = states.DECADE;
                            }
                            else if (num > 9)
                            {
                                state = states.DIGIT;
                            }
                            else
                            {
                                stringToParse += "0";
                                state = states.DIGIT;
                            }

                            stringToParse += num.ToString();
                        }
                        else
                        {
                            stringToParse += "00";
                            state = states.TEXT;
                        }

                        break;
                }
            }

            if (state == states.DECADE)
            {
                stringToParse += "0";
            }
            if (!float.TryParse(stringToParse, out result))
            {
                result = -1;
            }
            return result;
        }

        private static int ExtractInt(String word)
        {
            foreach (KeyValuePair<String[], int> entry in SpeechRecogniser.numbers0_199)
            {
                foreach (String numberStr in entry.Key)
                {
                    if (word == numberStr)
                    {
                        return entry.Value;
                    }
                }
            }
            return -1;
        }
    }
}
