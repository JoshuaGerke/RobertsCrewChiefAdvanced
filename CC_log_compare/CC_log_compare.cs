using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

using File = System.IO.File;

namespace CC_log_compare
{
    internal class CC_log_compare
    {
        public string FilePath { get; private set; }
        public List<string> FileContents { get; private set; } = new List<string>();
        public CC_log_compare(string _filePath)
        {
            FilePath = _filePath;
        }

        public List<string> ReadFile()
        {
            Console.WriteLine($"Reading {FilePath}");
            foreach (string line in File.ReadLines(FilePath))
            {
                FileContents.Add(line);
            }
            return FileContents;
        }

        public void WriteFile(List<string> contents)
        {
            string fp = FilePath + ".timeZeroed";
            File.WriteAllLines(fp, contents);
            Console.WriteLine($"Log file with timestamps zeroed written to {fp}");
        }
        public void WriteFuelFile(List<string> contents)
        {
            string fp = FilePath + ".fuel.CSV";
            File.WriteAllLines(fp, contents);
            Console.WriteLine($"Fuel parsed written to {fp}");
        }

        /// <summary>
        /// Zero the start timestamp in the contents of a log file.
        /// </summary>
        /// <returns></returns>
        public List<string> ParseContents(List<string> contents)
        {
            List<string> result = new List<string>();
            DateTime? startTime = null;
            var culture = CultureInfo.CreateSpecificCulture("en-GB");
            var styles = DateTimeStyles.None;
            foreach (string line in contents)
            {
                string[] sep = { " : " };
                string[] parts = line.Split(sep, System.StringSplitOptions.None);
                if (parts.Length > 1 && DateTime.TryParse(parts[0], culture, styles, out DateTime dateResult))
                {
                    if (startTime == null)
                    {
                        startTime = dateResult;
                    }
                    TimeSpan newDate = (TimeSpan)(dateResult - startTime);
                    result.Add($"{newDate.Hours:D2}:{newDate.Minutes:D2}:{newDate.Seconds:D2} : {parts[1]}");
                }
                else
                {
                    result.Add(line);
                }
            }
            return result;
        }
        public struct FuelParameters
        {
            public int LapsCompleted;
            public float InitialFuelLevel;
            public float FuelLeft;
            public float FuelPerMin;
            public float FuelPerMinWindowed;
            public float FuelPerMinMaxWindowed;
            public float FuelPerLap;
            public float FuelPerLapWindowed;
            public float FuelPerLapMaxWindowed;

            public FuelParameters(
             float _InitialFuelLevel = 0.0f,
             float _FuelLeft = 0.0f,
             float _FuelPerMin = 0.0f,
             float _FuelPerMinWindowed = 0.0f,
             float _FuelPerMinMaxWindowed = 0.0f,
             float _FuelPerLap = 0.0f,
             float _FuelPerLapWindowed = 0.0f,
             float _FuelPerLapMaxWindowed = 0.0f,
             int _LapsCompleted = 0
                )
            {
                InitialFuelLevel = _InitialFuelLevel;
                FuelLeft = _FuelLeft;
                FuelPerMin = _FuelPerMin;
                FuelPerMinWindowed = _FuelPerMinWindowed;
                FuelPerMinMaxWindowed = _FuelPerMinMaxWindowed;
                FuelPerLap = _FuelPerLap;
                FuelPerLapWindowed = _FuelPerLapWindowed;
                FuelPerLapMaxWindowed = _FuelPerLapMaxWindowed;
                LapsCompleted = _LapsCompleted;
            }
            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }
            public static bool operator ==(FuelParameters c1, FuelParameters c2)
            {
                return c1.Equals(c2);
            }
            public static bool operator !=(FuelParameters c1, FuelParameters c2)
            {
                return !c1.Equals(c2);
            }
            public static string FieldNames(FuelParameters fuelParameters)
            {
                string fieldNames = "Elapsed time,";
                foreach (var field in fuelParameters.GetType().GetMembers())
                {
                    if (field.MemberType == MemberTypes.Field)
                    {
                        fieldNames += field.Name + ",";
                    }
                }
                return fieldNames;
            }

            // These methods just to eliminate warnings
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
            public override string ToString()
            {
                return base.ToString();
            }
        }
        
        /// <summary>
        /// Parse a log line for Fuel content
        /// </summary>
        /// <param name="line"></param>
        /// <param name="FuelParameters">Running tally of entries</param>
        /// <returns>FuelParameters </returns>
        public static FuelParameters ParseFuel(string line, FuelParameters fuelParameters)
        {
            float parseFloat(string number)
            {
                if (!float.TryParse(number, out float result))
                {
                    result = 0; // gives somewhere to breakpoint
                }
                return result;
            }

            if (string.IsNullOrEmpty(line))
            {
                return fuelParameters;
            }
            string pattern;
            Regex rg;
            Match matchedNumbers;

            if (line.Contains("Fuel:"))
            {
                pattern = @"initialFuelLevel *= *(.*)(L|gal)";
                rg = new Regex(pattern);
                matchedNumbers = rg.Match(line);
                if (matchedNumbers.Success)
                {
                    fuelParameters.InitialFuelLevel = parseFloat(matchedNumbers.Groups[1].Value);
                }

                pattern = @"fuel left *= *(.*)(L|gal)";
                rg = new Regex(pattern);
                matchedNumbers = rg.Match(line);
                if (matchedNumbers.Success)
                {
                    fuelParameters.FuelLeft = parseFloat(matchedNumbers.Groups[1].Value);
                }

                pattern = @"Fuel use per minute \(basic calc\) *= *(.*?)(L|gal)";
                rg = new Regex(pattern);
                matchedNumbers = rg.Match(line);
                if (matchedNumbers.Success)
                {
                    fuelParameters.FuelPerMin = parseFloat(matchedNumbers.Groups[1].Value);
                }

                pattern = @"Fuel use per minute: windowed calc *= *(.*?)(L|gal), max per min calc *= *(.*?)(L|gal)";
                rg = new Regex(pattern);
                matchedNumbers = rg.Match(line);
                if (matchedNumbers.Success)
                {
                    fuelParameters.FuelPerMinWindowed = parseFloat(matchedNumbers.Groups[1].Value);
                    fuelParameters.FuelPerMinMaxWindowed = parseFloat(matchedNumbers.Groups[3].Value);
                }

                pattern = @"Fuel use per lap \(basic calc\) *= *(.*?)(L|gal)";
                rg = new Regex(pattern);
                matchedNumbers = rg.Match(line);
                if (matchedNumbers.Success)
                {
                    fuelParameters.FuelPerLap = parseFloat(matchedNumbers.Groups[1].Value);
                }

                pattern = @"Fuel use per lap: windowed calc *= *(.*?)(L|gal), max per lap *= *(.*?)(L|gal) left=(.*)(L|gal)";
                rg = new Regex(pattern);
                matchedNumbers = rg.Match(line);
                if (matchedNumbers.Success)
                {
                    fuelParameters.FuelPerLapWindowed = parseFloat(matchedNumbers.Groups[1].Value);
                    fuelParameters.FuelPerLapMaxWindowed = parseFloat(matchedNumbers.Groups[3].Value);
                    fuelParameters.FuelLeft = parseFloat(matchedNumbers.Groups[5].Value);
                }
            }
            else
            {
                pattern = @"Laps completed = (.*)";
                rg = new Regex(pattern);
                matchedNumbers = rg.Match(line);
                if (matchedNumbers.Success)
                {
                    if (!int.TryParse(matchedNumbers.Groups[1].Value, out fuelParameters.LapsCompleted))
                        fuelParameters.LapsCompleted = 0;
                }
            }

            return fuelParameters;
        }

        /// <summary>
        /// Zero the start time in a log file (and remove the seconds decimal)
        /// to make it easier to compare log files.
        /// Also parse the fuel content into a CSV file
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var obj = new CC_log_compare(args[0]);
                var contents = obj.ReadFile();
                var result = obj.ParseContents(contents);
                obj.WriteFile(result);

                FuelParameters fuelParameters = new FuelParameters();
                FuelParameters fuelParametersResult;
                var csv = new List<string> { };
                string fuelCalc;

                csv.Add(FuelParameters.FieldNames(fuelParameters));
                foreach (var line in result)
                {
                    fuelParametersResult = ParseFuel(line, fuelParameters);
                    if (fuelParametersResult != fuelParameters)
                    {
                        fuelParameters = fuelParametersResult;
                        string newLine = formatCSVline(fuelParametersResult, line);
                        csv.Add(newLine);
                    }
                    fuelCalc = MatchFuelCalculation(line);
                    if (fuelCalc.Length > 0)
                    {
                        string newLine = formatCSVline(fuelParameters, line);
                        newLine += $", {fuelCalc}";
                        csv.Add(newLine);
                    }
                }
                obj.WriteFuelFile(csv);
            }
            Console.WriteLine("Press Enter to continue:");
            Console.ReadLine();
        }

        internal static string MatchFuelCalculation(string line)
        {
            string pattern;
            Regex rg;
            Match match;
            pattern = @"Fuel: Use per minute *= *(.*?)(L|gal) estimated minutes to go \(including final lap\) *= *(.*) current fuel *= *(.*?)(L|gal) additional fuel needed *= *(.*?)(L|gal)";

            rg = new Regex(pattern);
            match = rg.Match(line);
            return match.Value;
        }

        private static string formatCSVline(FuelParameters fuelParametersResult, string line)
        {
            string[] sep = { " : " };
            string[] parts = line.Split(sep, System.StringSplitOptions.None);
            string newLine = parts[0];  // Time
            newLine += $", {fuelParametersResult.LapsCompleted}";
            newLine += $", {fuelParametersResult.InitialFuelLevel}";
            newLine += $", {fuelParametersResult.FuelLeft}";
            newLine += $", {fuelParametersResult.FuelPerMin}";
            newLine += $", {fuelParametersResult.FuelPerMinWindowed}";
            newLine += $", {fuelParametersResult.FuelPerMinMaxWindowed}";
            newLine += $", {fuelParametersResult.FuelPerLap}";
            newLine += $", {fuelParametersResult.FuelPerLapWindowed}";
            newLine += $", {fuelParametersResult.FuelPerLapMaxWindowed}";
            return newLine;
        }
    }
}
