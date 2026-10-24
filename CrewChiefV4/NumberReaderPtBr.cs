using CrewChiefV4.Audio;
using System;
using System.Collections.Generic;

namespace CrewChiefV4.NumberProcessing
{
    /**
     * This is the Portuguese (BR) number reader implementation. 
     *  
     */
    public class NumberReaderPtBr : NumberReader
    {
        // this folder contains lots of subfolders, one for each number from 0 to 99, so we can add a folder to the 
        // list to play called "numbers/[number]" - i.e. numbers/45 or numbers/1. This is used a lot in the implementations below.
        private static String folderNumbersStub = "numbers/";

        // special cases for 1 and 2 which change according to article gender
        private static String folderMasculine_1 = folderNumbersStub +"um";
        private static String folderFeminine_1 = folderNumbersStub + "uma";
        private static String folderMasculine_2 = folderNumbersStub + "dois";
        private static String folderFeminine_2 = folderNumbersStub + "duas";
        private static String folder100Only = folderNumbersStub + "cem"; // special case for one hundred

        private static String folder_100 = folderNumbersStub + "cento"; // one hundred and something
        private static String folderMasuline_200 = folderNumbersStub + "duzentos";
        private static String folderFeminine_200 = folderNumbersStub + "duzentas";
        private static String folderMasuline_300 = folderNumbersStub + "trezentos";
        private static String folderFeminine_300 = folderNumbersStub + "trezentas";
        private static String folderMasuline_400 = folderNumbersStub + "quatrocentos";
        private static String folderFeminine_400 = folderNumbersStub + "quatrocentas";
        private static String folderMasuline_500 = folderNumbersStub + "quinhentos";
        private static String folderFeminine_500 = folderNumbersStub + "quinhentos";
        private static String folderMasuline_600 = folderNumbersStub + "seiscentos";
        private static String folderFeminine_600 = folderNumbersStub + "seiscentas";
        private static String folderMasuline_700 = folderNumbersStub + "setecentos";
        private static String folderFeminine_700 = folderNumbersStub + "setecentas";
        private static String folderMasuline_800 = folderNumbersStub + "oitocentos";
        private static String folderFeminine_800 = folderNumbersStub + "oitocentas";
        private static String folderMasuline_900 = folderNumbersStub + "novecentos";
        private static String folderFeminine_900 = folderNumbersStub + "novecentas";

        private static String folderThousand = folderNumbersStub + "mil";

        private static String folderAndPrefix = "and_"; // 'e' in Portuguese, i.e. vinte e um/uma
        
        private static String folderMinutes = folderNumbersStub + "minutes";
        private static String folderAMinute = folderNumbersStub + "a_minute";
        private static String folderASecond = folderNumbersStub + "a_second";
        private static String folderSeconds = folderNumbersStub + "seconds";
        public static String folderAnd = folderNumbersStub + "and";
        
        private enum Unit { HOUR, MINUTE, SECOND, AND_TENTH, JUST_TENTH, NONE }
        
        protected override String getLocale()
        {
            return "pt-br";
        }
        
        /**
         * Get Portuguese sound for a whole number of hours. Long form. I have no idea whether this should be masculin or
         * feminine so we'll default to masculin here
         */
        protected override List<String> GetHoursSounds(int hours, int minutes, int seconds, int tenths,
            Boolean messageHasContentAfterTime, Precision precision)
        {
            List<String> messages = new List<String>();
            if (hours > 0)
            {
                messages.AddRange(resolveNumberSounds(false, hours, Unit.HOUR, !messageHasContentAfterTime, ARTICLE_GENDER.FEMALE));
            }
            return messages;
        }

        /**
         * Get a Portuguese sound for a whole number of minutes. Long form.
         */
        protected override List<String> GetMinutesSounds(int hours, int minutes, int seconds, int tenths,
            Boolean messageHasContentAfterTime, Precision precision)
        {
            List<String> messages = new List<String>();
            if (minutes > 0)
            {
                if (hours > 0)
                {
                    // skip seconds and tenths, use 'and' if we can
                    messages.AddRange(resolveNumberSounds(true, minutes, Unit.MINUTE, messageHasContentAfterTime, ARTICLE_GENDER.MALE));
                }
                else
                {
                    // no hours, so we may be reading seconds / tenths as well.
                    if (seconds == 0 && tenths == 0)
                    {
                        messages.AddRange(resolveNumberSounds(false, minutes, Unit.MINUTE, !messageHasContentAfterTime, ARTICLE_GENDER.MALE));
                    }
                    else
                    {
                        // when we're here, we know that there are seconds or tenths to come.
                        messages.AddRange(resolveNumberSounds(false, minutes, Unit.MINUTE, !messageHasContentAfterTime && seconds == 0, ARTICLE_GENDER.MALE));
                    }
                }
            }
            return messages;
        }

        /**
         * Get a Portuguese sound for a whole number of seconds. Long form.
         */
        protected override List<String> GetSecondsSounds(int hours, int minutes, int seconds, int tenths,
            Boolean messageHasContentAfterTime, Precision precision)
        {
            List<String> messages = new List<String>();
            // special case here - if we're reading a time which has hours, the seconds aren't significant so ignore them
            if (hours == 0)
            {
                if (seconds > 0)
                {
                    if (tenths == 0)
                    {
                        messages.AddRange(resolveNumberSounds(minutes > 0, seconds, Unit.SECOND, !messageHasContentAfterTime, ARTICLE_GENDER.MALE));
                    }
                    else
                    {
                        messages.AddRange(resolveNumberSounds(false, seconds, Unit.NONE, !messageHasContentAfterTime, ARTICLE_GENDER.MALE));
                    }
                }
            }
            return messages;
        }

        /**
         * Get a Portuguese sound for a whole number of tenths of a second.
         */
        protected override List<String> GetTenthsSounds(int hours, int minutes, int seconds, int tenths,
            Boolean messageHasContentAfterTime, Precision precision)
        {
            List<String> messages = new List<String>();
            if (tenths > 0)
            {
                Boolean haveMinutesOrSeconds = minutes > 0 || seconds > 0;
                if (haveMinutesOrSeconds)
                {
                    messages.Add(folderPoint);
                }
                messages.AddRange(resolveNumberSounds(false, tenths, 
                    haveMinutesOrSeconds ? Unit.AND_TENTH : Unit.JUST_TENTH, messageHasContentAfterTime));
            }
            return messages;
        }

        /**
         * Not implemented for Portuguese number reader.
         * */
        protected override String GetSecondsWithTenths(int seconds, int tenths)
        {
            return null;
        }

        /**
         * fraction is String so we can pass "01" etc - we don't know if it's tenths or hundredths so it may need zero padding.
         */
        protected override List<String> GetMinutesAndSecondsWithFraction(int minutes, int seconds, String fraction, Boolean messageHasContentAfterTime)
        {
            // there will always be some seconds here
            String combinedMinutesAndSecondsSoundFolder;
            List<String> separateMinutesAndSecondsSoundFolders = new List<string>();
            String fractionsFolder = null;
            Boolean usePoint = false;

            if (minutes > 0)
            {
                separateMinutesAndSecondsSoundFolders.Add(folderNumbersStub + minutes.ToString());
                separateMinutesAndSecondsSoundFolders.Add(folderMinutes);
                separateMinutesAndSecondsSoundFolders.Add(folderNumbersStub + seconds.ToString());

                String paddedSeconds = seconds < 10 ? "_0" + seconds : "_" + seconds;
                combinedMinutesAndSecondsSoundFolder = folderNumbersStub + minutes + paddedSeconds;
            }
            else
            {
                combinedMinutesAndSecondsSoundFolder = folderNumbersStub + seconds.ToString();
            }
            if (fraction != "0" && fraction != "00")
            {
                fractionsFolder = folderNumbersStub + fraction;
            }
            List<String> messages = new List<String>();

            Boolean addCombined = true;
            if (!SoundCache.availableSounds.Contains(combinedMinutesAndSecondsSoundFolder))
            {
                addCombined = false;
            }
            if (addCombined) 
            {
                messages.Add(combinedMinutesAndSecondsSoundFolder);
            }
            else
            {
                Console.WriteLine("Unable to find number sound: " + combinedMinutesAndSecondsSoundFolder);
                messages.AddRange(separateMinutesAndSecondsSoundFolders);
            }
            if (usePoint)
            {
                messages.Add(folderPoint);
            }
            if (fractionsFolder != null)
            {
                messages.Add(fractionsFolder);
            }
            Console.WriteLine("Reading short form with sounds " + String.Join(", ", messages));
            return messages;
        }

        /**
         * Not implemented for Portuguese number reader.
         * */
        protected override List<String> GetSecondsWithHundredths(int seconds, int hundredths)
        {
            return null;
        }

        /**
         * Not implemented for Portuguese number reader.
         * */
        protected override List<String> GetSeconds(int seconds)
        {
            return null;
        }

        /**
         * Get a Portuguese sound for an Integer from 0 to 99999.
         */
        protected override List<String> GetIntegerSounds(char[] digits, Boolean allowShortHundredsForThisNumber, Boolean messageHasContentAfterNumber,
            ARTICLE_GENDER gender = ARTICLE_GENDER.NA)
        {
            List<String> messages = new List<String>();
            // if this is just zero, return a list with just "zero"
            if (digits.Length == 0 || (digits.Length == 1 && digits[0] == '0'))
            {
                messages.Add(folderNumbersStub + 0);
            }
            else
            {
                // work out what to say for the thousands, hundreds, and tens / units
                String tensAndUnits = null;
                String hundreds = null;
                String thousands = null;

                if (digits.Length == 1 || (digits[digits.Length - 2] == '0' && digits[digits.Length - 1] != '0'))
                {
                    // if we have just 1 digit, or we have a number that ends with 01, 02, 03, etc, then the 
                    // number of tensAndUnits is the final character
                    tensAndUnits = digits[digits.Length - 1].ToString();
                }
                else if (digits[digits.Length - 2] != '0' || digits[digits.Length - 1] != '0')
                {
                    // if we have just multiple digits, and one or both of the last 2 are non-zero
                    tensAndUnits = digits[digits.Length - 2].ToString() + digits[digits.Length - 1].ToString();
                }
                if (digits.Length >= 3)
                {
                    if (digits[digits.Length - 3] != '0')
                    {
                        // there's a non-zero number of hundreds
                        hundreds = digits[digits.Length - 3].ToString();
                    }
                    if (digits.Length == 4)
                    {
                        // there's a non-zero number of thousands
                        thousands = digits[0].ToString();
                    }
                    else if (digits.Length == 5)
                    {
                        // there's a non-zero number of thousands - 10 or more
                        thousands = digits[0].ToString() + digits[1].ToString();
                    }
                }
                if (thousands != null)
                {
                    if (thousands == "2")
                    {
                        messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_2 : folderMasculine_2);
                    }
                    else if (thousands != "1")
                    {
                        messages.Add(folderNumbersStub + thousands);
                    }
                    messages.Add(folderThousand);
                }
                if (hundreds != null)
                {
                    switch (hundreds)
                    {
                        case "1":
                            if (tensAndUnits == null)
                            {
                                messages.Add(folder100Only);
                            }
                            else
                            {
                                messages.Add(folder_100);
                            }
                            break;
                        case "2":
                            messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_200 : folderMasuline_200);
                            break;
                        case "3":
                            messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_300 : folderMasuline_300);
                            break;
                        case "4":
                            messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_400 : folderMasuline_400);
                            break;
                        case "5":
                            messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_500 : folderMasuline_500);
                            break;
                        case "6":
                            messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_600 : folderMasuline_600);
                            break;
                        case "7":
                            messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_700 : folderMasuline_700);
                            break;
                        case "8":
                            messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_800 : folderMasuline_800);
                            break;
                        case "9":
                            messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_900 : folderMasuline_900);
                            break;
                        default:
                            break;
                    }
                }
                if (tensAndUnits != null)
                {
                    if (hundreds != null || thousands != null)
                    {
                        // insert the 'e' between the hundreds / thousands
                        messages.Add(folderAnd);
                    }
                    if (tensAndUnits == "1")
                    {
                        messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_1 : folderMasculine_1);
                    }
                    else if (tensAndUnits == "2")
                    {
                        messages.Add(gender == ARTICLE_GENDER.FEMALE ? folderFeminine_2 : folderMasculine_2);
                    }
                    else
                    {
                        messages.Add(folderNumbersStub + tensAndUnits);
                    }
                }
            }            
            return messages;
        }

        private String getSuffixForUnit(Unit unit)
        {
            switch (unit)
            {
                case Unit.HOUR:
                    return "_hours";
                case Unit.MINUTE:
                    return "_minutes";
                case Unit.SECOND:
                    return "_seconds";
                case Unit.JUST_TENTH:
                     return "_tenths";
                default:
                    // used for 'and_X' sounds
                    return "";
            }
        }

        private String getFolderForUnit(Unit unit, int number)
        {
            switch (unit)
            {
                case Unit.HOUR:
                    return NumberReaderPtBr.folderNumbersStub + (number == 1 ? "hour" : "hours");
                case Unit.MINUTE:
                    return NumberReaderPtBr.folderNumbersStub + (number == 1 ? "minute" : "minutes");
                case Unit.SECOND:
                    return NumberReaderPtBr.folderNumbersStub + (number == 1 ? "second" : "seconds");
                case Unit.AND_TENTH:
                    return NumberReaderPtBr.folderNumbersStub + "seconds";
                case Unit.JUST_TENTH:
                    return NumberReaderPtBr.folderNumbersStub + (number == 1 ? "tenth" : "tenths");
                default:
                    return "";
            }
        }

        private List<String> resolveNumberSounds(Boolean startWithAnd, int number, Unit unitEnum, Boolean useMoreInflection, 
            ARTICLE_GENDER gender = ARTICLE_GENDER.NA)
        {
            String unitSuffix = getSuffixForUnit(unitEnum);
            String unitFolder = getFolderForUnit(unitEnum, number);
            List<String> sounds = new List<string>();
            if (startWithAnd)
            {
                String sound = folderNumbersStub + folderAndPrefix + number + unitSuffix;
                Console.WriteLine("looking for sound " + sound);
                if (SoundCache.availableSounds.Contains(sound))
                {
                    // this is the best case - the full sound is available
                    Console.WriteLine("Got sound for all parameters: " + sound);
                    sounds.Add(sound);
                    return sounds;
                }
                sound = folderNumbersStub + number + unitSuffix;
                if (SoundCache.availableSounds.Contains(sound))
                {
                    sounds.Add(folderAnd);
                    sounds.Add(sound);
                    Console.WriteLine("Got sound for parameters without 'and': " + String.Join(", ", sounds));
                    return sounds;
                }
                sounds.Add(folderAnd);
                sounds.Add(folderNumbersStub + number);
                if (unitFolder.Length > 0)
                {
                    sounds.Add(unitFolder);
                }
                Console.WriteLine("Returning individual sounds: " + String.Join(", ", sounds));
                return sounds;
            }
            else
            {
                String sound = folderNumbersStub + number + unitSuffix;
                Console.WriteLine("looking for sound " + sound);
                if (SoundCache.availableSounds.Contains(sound))
                {
                    // this is the best case - the full sound is available
                    Console.WriteLine("Got sound for all parameters: " + sound);
                    sounds.Add(sound);
                    return sounds;
                }
                sounds.Add(folderNumbersStub + number);
                if (unitFolder.Length > 0)
                {
                    sounds.Add(unitFolder);
                }
                Console.WriteLine("Returning individual sounds: " + String.Join(", ", sounds));
                return sounds;
            }
        }
    }
}
