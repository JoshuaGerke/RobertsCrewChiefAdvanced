using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using DarkModeForms;
using MathNet.Numerics;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using WebSocketSharp;
using WebSocketSharp.Server;

namespace CrewChiefV4
{
    /// <summary>
    /// value object for message expectations
    /// </summary>
    public class ExpectedMessage
    {
        string[] messageNames;
        public int minCount;
        public int maxCount;

        // expect a specific message to be played the range of times - note that the DELAYED_ and COMPOUND_ prefixes are also considered here
        public ExpectedMessage(string messageName, int minCount, int maxCount)
        {
            this.messageNames = new string[] { messageName };
            this.minCount = minCount;
            this.maxCount = maxCount;
        }

        // expect one of the messageNames to be played the range of times - note that the DELAYED_ and COMPOUND_ prefixes are also considered here
        public ExpectedMessage(string[] messageNames, int minCount, int maxCount)
        {
            this.messageNames = messageNames;
            this.minCount = minCount;
            this.maxCount = maxCount;
        }
        // expect a specific message to be played the exact number of times - note that the DELAYED_ and COMPOUND_ prefixes are also considered here
        public ExpectedMessage(string messageName, int exactCount)
        {
            this.messageNames = new string[] { messageName };
            this.minCount = exactCount;
            this.maxCount = exactCount;
        }

        // expect one of the messageNames to be played the exact number of times - note that the DELAYED_ and COMPOUND_ prefixes are also considered here
        public ExpectedMessage(string[] messageNames, int exactCount)
        {
            this.messageNames = messageNames;
            this.minCount = exactCount;
            this.maxCount = exactCount;
        }
        override public string ToString()
        {
            return string.Join(", ", messageNames) + " expected >= " + minCount + " and <= " + maxCount;
        }
        // check that this expectation is met - i.e. a message was queued with one of the expected names >= min and <= max times
        public int getMatchCount()
        {
            int matchCount = 0;
            foreach (KeyValuePair<string, int> entry in Utilities.queuedMessageIds)
            {
                foreach (string messageName in messageNames)
                {
                    if (messageName == entry.Key || ("DELAYED_" + messageName) == entry.Key || ("COMPOUND_" + messageName) == entry.Key)
                    {
                        matchCount += entry.Value;
                    }
                }
            }
            return matchCount;
        }
    }

    public static class Utilities
    {
        public static Boolean includesRaceSession = false;

        public static Dictionary<string, int> queuedMessageIds = new Dictionary<string, int>();

        // some noddy hard-coded expectations for race session trace playback
        // TODO make this something that can be saved with the trace so each trace can define its own set of expectations

        // TODO: move the hard-coded Strings to a messageNames class and reference these in all the events instead of using random
        // magic Strings everywhere
        private static ExpectedMessage[] defaultExpectedMessagesForRaceSessions = new ExpectedMessage[]
        {
            new ExpectedMessage("lap_counter/get_ready", 1),
            new ExpectedMessage("lap_counter/green_green_green", 1),
            new ExpectedMessage("position", 1, 1000), // expect at least *some* position messages
            new ExpectedMessage(new string[] {"Timings/gap_behind", "Timings/gap_in_front"}, 1, 1000), // expect at least *some* gap messages
            new ExpectedMessage(new string[] {"fuel/half_distance_good_fuel", "fuel/half_distance_low_fuel"}, 0, 1),    // won't always get this, but should never have > 1
            new ExpectedMessage(new string[] {"lap_counter/two_to_go", "lap_counter/two_to_go_top_three", "lap_counter/two_to_go_leading",
                "race_time/five_minutes_left_podium", "race_time/five_minutes_left_leading", "race_time/five_minutes_left"}, 1),    // should always get 1 2-to-go or 5-mins-to-go
            new ExpectedMessage(new string[] {"lap_counter/last_lap", "lap_counter/white_flag_last_lap", "lap_counter/last_lap_leading",
                "lap_counter/last_lap_top_three", "race_time/last_lap", "race_time/last_lap_leading", "race_time/last_lap_top_three"}, 1),  // should always get 1 last-lap
            new ExpectedMessage("SESSION_END", 1)   // should always get 1 session end
        };

        public static Random random = (CrewChief.Debug.RunningUnderDebugger ||
                                       CrewChief.Debug.DebugWithPlaybackMode ||
                                       UnitTest.UnitTest.Active) ?
            new FixedRandomForDebug() : new Random();

        /// <summary>
        /// False: always return the min value
        /// True: always return the max value
        /// </summary>
        public static bool DebugUseMaxRandomValue;
        private class FixedRandomForDebug : Random
        {
            public override int Next()
            {
                return Next(int.MaxValue);
            }
            public override int Next(int maxValue)
            {
                return Next(0, maxValue);
            }
            public override int Next(int minValue, int maxValue)
            {
                return DebugUseMaxRandomValue ? maxValue - 1 : minValue;
            }
            protected override double Sample()
            {
                return DebugUseMaxRandomValue ? 0.999 : 0.0;
            }
        }


        private static WebSocketServer ccDataWebSocketServer;

        private static WebSocketServer gameDataWebSocketServer;

        private static object websocketServerLock = new object();

        public static AudioPlayer audioPlayer;

        private static readonly int ccDataWebsocketPort = UserSettings.GetUserSettings().getInt("websocket_port");

        private static readonly int gameDataWebsocketPort = UserSettings.GetUserSettings().getInt("game_data_websocket_port");

        public static GameDataReader gameDataReader;

        public static GameDataSerializer gameDataSerializer;

        public static void checkPlaybackCounts()
        {
            if (includesRaceSession)
            {
                Console.WriteLine("Playback counts: \n" + string.Join("\n", queuedMessageIds.Select(x => x.Key + " : " + x.Value)));
                checkMessageCounts();
            }
            else
            {
                Console.WriteLine("Skipping expectations as we've not had a race session");
            }
        }

        private static Boolean checkMessageCounts()
        {
            Boolean pass = true;
            foreach (ExpectedMessage expected in defaultExpectedMessagesForRaceSessions)
            {
                int matchCount = expected.getMatchCount();
                if (matchCount < expected.minCount || matchCount > expected.maxCount)
                {
                    Console.WriteLine("***** match count check failed " + expected.ToString() + " got " + matchCount + " matches");
                    pass = false;
                }
            }
            if (pass)
            {
                Console.WriteLine("Message expectations passed");
            }
            else
            {
                Console.WriteLine("Message expectations failed");
            }
            return pass;
        }

        public static void startCCDataWebsocketServer(AudioPlayer audioPlayer)
        {
            Utilities.audioPlayer = audioPlayer;
            stopCCWebsocketServer();
            try
            {
                lock (websocketServerLock)
                {
                    ccDataWebSocketServer = new WebSocketServer(ccDataWebsocketPort);
                    ccDataWebSocketServer.AddWebSocketService<WebsocketData>("/crewchief");
                    ccDataWebSocketServer.Start();
                    Console.WriteLine("Successfully started Crew Chief data WebSocket server");
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Unable to start Crew Chief websocket: ");
            }
        }

        public static void startGameDataWebsocketServer(String endpoint, GameDataReader gameDataReader, GameDataSerializer serializer)
        {
            stopGameDataWebsocketServer();
            GameDataWebsocketData.init(gameDataReader, serializer);
            try
            {
                lock (websocketServerLock)
                {
                    gameDataWebSocketServer = new WebSocketServer(gameDataWebsocketPort);
                    gameDataWebSocketServer.AddWebSocketService<GameDataWebsocketData>(endpoint);
                    gameDataWebSocketServer.Start();
                    Console.WriteLine("Successfully started game data WebSocket server");
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Unable to start game data websocket: ");
            }
        }

        public static void stopWebsocketServers()
        {
            stopCCWebsocketServer();
            stopGameDataWebsocketServer();
        }

        private static void stopCCWebsocketServer()
        {
            try
            {
                lock (websocketServerLock)
                {
                    if (ccDataWebSocketServer != null)
                    {
                        ccDataWebSocketServer.Stop();
                        ccDataWebSocketServer = null;
                        Console.WriteLine("Stopped CC data WebSocket server");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Unable to stop CC data websocket: ");
            }
        }

        private static void stopGameDataWebsocketServer()
        {
            GameDataWebsocketData.reset();
            try
            {
                lock (websocketServerLock)
                {
                    if (gameDataWebSocketServer != null)
                    {
                        gameDataWebSocketServer.Stop();
                        gameDataWebSocketServer = null;
                        Console.WriteLine("Stopped game data WebSocket server");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Unable to stop game data websocket: ");
            }
        }

        public static bool IsGameRunning(String processName, String[] alternateProcessNames, out String parentDir)
        {
            parentDir = null;
#if FAKE_GAME
            return true;
#else

            var proc = Process.GetProcessesByName(processName);
            if (proc.Length > 0)
            {
                if (!Game.IRACING)
                {
                    try
                    {
                        parentDir = Path.GetDirectoryName(proc[0].MainModule.FileName);
                    }
                    catch (Win32Exception) { /*Ignore - anti cheat protection?*/ }
                    catch (Exception e) { Log.Exception(e); }
                }
                return true;
            }
            else if (alternateProcessNames != null && alternateProcessNames.Length > 0)
            {
                foreach (String alternateProcessName in alternateProcessNames)
                {
                    if (Process.GetProcessesByName(alternateProcessName).Length > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
#endif
        }

        /// <summary>
        /// Launch the game
        /// </summary>
        /// <param name="launchExe"></param>
        /// <param name="launchParams"></param>
        /// <returns>true: the game started
        /// false: there was a problem running the game</returns>
        public static bool runGame(String launchExe, String launchParams)
        {
            string exception;
            string exMsg;
            // Inconsistent handling of spaces in paths
            // ProcessStartInfo() is happy with the escaped " version: "\"c:\pa th\game.exe\""
            // GetDirectoryName() wants "c:\pa th\game.exe"
            // Neither is happy if the user enters "c:\pa th\game.exe" with quotes
            launchExe = launchExe.Trim().Trim('"').Trim('\'').Trim();
            if (launchExe.IsNullOrEmpty())
            {
                // user wants to launch the game but hasn't specified a path. Bloody users.
                Console.WriteLine("Skipping game launch because no path was provided");
                return true;
            }
            try
            {
                Console.WriteLine("Attempting to run game using " + launchExe + " " + launchParams);
                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(launchExe);
                    startInfo.Arguments = launchParams;
                    startInfo.WorkingDirectory = Path.GetDirectoryName(launchExe);
                    process.StartInfo = startInfo;
                    process.Start();
                    return true;
                }
            }
            catch (InvalidOperationException e)
            {
                exception = "InvalidOperationException";
                exMsg = e.Message;
            }
            catch (Exception e)
            {
                exception = "Exception";
                exMsg = e.Message;
            }
            string error = String.IsNullOrEmpty(launchParams) ?
                $"{exception} starting game with path '{launchExe}' : {exMsg}" :
                $"{exception} starting game with path '{launchExe}' and params '{launchParams}' : {exMsg}";
            Log.Error(error);
            return false;
        }

        /*
         * For tyre life estimates we want to know how long the tyres will last, so we're asking for a time prediction
         * given a wear amount (100% wear). So y_data is the y-axis which may be time points (session running time) or
         * number of sectors since session start incrementing +1 for each sector. When we change tyres we clear these
         * data sets but the y-axis time / sector counts will start at however long into the session (time or total
         * sectors) we are.
         * x_data is the tyre wear at that y point (a percentage).
         * the x_point is the point you want to predict the life - wear amount. So we pass 100% in here to give us
         * a time / sector count estimate.
         * order is the polynomial fit order - 1 for linear, 2 for quadratic etc. > 3 does not give a suitable
         * curve and will produce nonsense. Use 2 for tyre wear.
         */
        public static double getYEstimate(double[] x_data, double[] y_data, double x_point, int order)
        {
            // get the polynomial from the Numerics library:
            double[] curveParams = Fit.Polynomial(x_data, y_data, order);

            // solve for x_point:
            double y_point = 0;
            for (int power = 0; power < curveParams.Length; power++)
            {
                if (power == 0)
                {
                    y_point = y_point + curveParams[power];
                }
                else if (power == 1)
                {
                    y_point = y_point + curveParams[power] * x_point;
                }
                else
                {
                    y_point = y_point + curveParams[power] * Math.Pow(x_point, power);
                }
            }
            return y_point;
        }

        public static void TraceEventClass(GameStateData gsd)
        {
            if (gsd == null || gsd.carClass == null)
                return;

            var eventCarClasses = new Dictionary<string, CarData.CarClassEnum>();
            eventCarClasses.Add(gsd.carClass.getClassIdentifier(), gsd.carClass.carClassEnum);

            if (gsd.OpponentData != null)
            {
                foreach (var opponent in gsd.OpponentData)
                {
                    if (opponent.Value.CarClass != null
                        && !eventCarClasses.ContainsKey(opponent.Value.CarClass.getClassIdentifier()))
                    {
                        eventCarClasses.Add(opponent.Value.CarClass.getClassIdentifier(), opponent.Value.CarClass.carClassEnum);
                    }
                }
            }

            if (eventCarClasses.Count == 1)
                Console.WriteLine("Single-Class event:\"" + eventCarClasses.Keys.First() + "\" "
                    + Utilities.GetCarClassMappingHint(eventCarClasses.Values.First()));
            else
            {
                Console.WriteLine("Multi-Class event:");
                foreach (var carClass in eventCarClasses)
                {
                    Console.WriteLine("\t\"" + carClass.Key + "\" "
                        + Utilities.GetCarClassMappingHint(carClass.Value));
                }
                if (!GameStateData.Multiclass)
                {
                    Console.WriteLine("Insufficient car class data, so dropping back to single class racing");
                }
            }
        }

        private static string GetCarClassMappingHint(CarData.CarClassEnum cce)
        {
            if (cce == CarData.CarClassEnum.UNKNOWN_RACE)
                return "(unmapped)";
            else if (cce == CarData.CarClassEnum.USER_CREATED)
                return "(user defined)";

            return "(built-in)";
        }

        // Moved to DataFiles, but that's a diff in 19 files so keep this stub for now
        public static string ResolveDataFile(string dataFilesPath, string fileNameToResolve)
        {
            return DataFiles.ResolveDataFile(fileNameToResolve);
        }

        // tries to use an optimal recording for 1 or 2 dp precision
        public static List<MessageFragment> WholeAndFractionalMessage(float number, int fractions = 1, bool rounding = true)
        {
            Debug.Assert(fractions > 0 && fractions < 3);
            var message = new List<MessageFragment>();
            var (whole, frac) = Utilities.WholeAndFractionalPart(number, fractions, rounding);

            bool prefixed = false;
            var prefix = "";

            if (fractions == 2)
            {
                if (frac < 10)
                {
                    // makes sure that "3.01" is reported as "3 point zero 1" not "3 point 1"
                    prefixed = true;
                    prefix = "0";
                }
                else if (frac % 10 == 0)
                {
                    // allows "3.10" to be reported as "3 point 1"
                    frac = frac / 10;
                }
            }

            var ideal = $"numbers/{whole}point{prefix}{frac}";
            if (SoundCache.availableSounds.Contains(ideal))
            {
                message.Add(MessageFragment.Text(ideal));
            }
            else
            {
                message.Add(MessageFragment.Integer(whole));
                message.Add(MessageFragment.Text(NumberReader.folderPoint));
                if (prefixed)
                {
                    message.Add(MessageFragment.Integer(0));
                }
                message.Add(MessageFragment.Integer(frac));
            }
            return message;
        }

        public static Tuple<int, int> WholeAndFractionalPart(float realNumber, int fractions = 1, bool rounding = false)
        {
            Debug.Assert(fractions > 0);
            Debug.Assert(!float.IsNaN(realNumber));
            Debug.Assert(!float.IsInfinity(realNumber));

            if (rounding)
            {
                // rounding is disabled by default for historical reasons, where the caller would do it, so is opt-in
                realNumber = (float)(realNumber + 0.5 * Math.Pow(0.1, fractions));
            }

            // adapted from https://stackoverflow.com/questions/53928162
            // this will for sure break down for anything that needs scientific notation
            double whole = Math.Truncate(realNumber);
            double fractional = (realNumber - whole) * Math.Pow(10, fractions);

            return new Tuple<int, int>((int)whole, (int)fractional);
        }

        /// <summary>
        /// Restart CC with edited args
        /// </summary>
        /// <returns>true if app restarted</returns>
        public static bool RestartApp(
            bool app_restart = false,
            bool removeSkipUpdates = false,
            bool removeProfile = false,
            bool removeGame = false)
        {
            if (!CrewChief.Debug.RunningUnderDebugger)
            {
                var newArgs = RestartAppCommandLine(app_restart,
                                                    removeSkipUpdates,
                                                    removeProfile,
                                                    removeGame);
                System.Diagnostics.Process.Start(    // to start new instance of application
                    System.Windows.Forms.Application.ExecutablePath,
                    String.Join(" ", newArgs.ToArray()));
                return true;
            }
            // If debugging then carry on regardless
            return false;
        }
        /// <summary>
        /// Edit the current command line
        /// </summary>
        /// <param name="app_restart"></param>
        /// <param name="removeSkipUpdates">We're restarting because the 'force update check'</param>
        /// <param name="removeProfile">-profile [profile name]</param>
        /// <param name="removeGame">=game [game name]</param>
        /// <returns></returns>
        // (Extracted so it can be tested)
        internal static List<string> RestartAppCommandLine(
            bool app_restart = false,
            bool removeSkipUpdates = false,
            bool removeProfile = false,
            bool removeGame = false)
        {
            if (app_restart)
            {
                CrewChief.CommandLine.Add("app_restart", "");
            }
            if (removeSkipUpdates)
            {
                CrewChief.CommandLine.Remove("skip_updates");
                CrewChief.CommandLine.Remove("SKIP_UPDATES");
            }
            if (removeProfile)
            {
                CrewChief.CommandLine.Remove("profile");
            }
            if (removeGame)
            {
                CrewChief.CommandLine.Remove("game");
            }
            // Always have to add "-multi" to the start args so the app can restart
            CrewChief.CommandLine.Add("multi", "");

            // Translate the dict back into a command line
            var newArgs = new List<string>();
            foreach (var arg in CrewChief.CommandLine._dict)
            {
                newArgs.Add("-" + arg.Key);
                newArgs.Add(arg.Value);
            }
            return newArgs;
        }

        public static void AddLinesToFile(string filePath, List<string> lines)
        {
            try
            {
                // Create the file if it doesn't exist
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Close();
                }

                // Add the lines to the file
                using (StreamWriter writer = File.AppendText(filePath))
                {
                    foreach (string line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }
            } 
            catch (Exception ex)
            { 
                Log.Error($"Error adding lines to file {filePath}: {ex.Message}");
            }
        }

        public class Strings
        {

            /// <summary>
            /// If 'text' is longer than 'maxLength' insert a newline near
            /// the middle after a word break
            /// </summary>
            /// <param name="text"></param>
            /// <param name="maxLength"></param>
            /// <returns></returns>
            public static string SplitString(string text, int maxLength)
            {
                if (text.Length <= maxLength)
                {
                    return text;
                }
                //Degenerate case with only 1 word
                if (!text.Any(Char.IsWhiteSpace))
                {
                    return text;
                }

                int mid = text.Length / 2;
                if (!Char.IsWhiteSpace(text[mid]))
                {
                    for (int i = 1; i < mid; i += i)
                    {
                        if (Char.IsWhiteSpace(text[mid + i]))
                        {
                            mid = mid + i;
                            break;
                        }
                        if (Char.IsWhiteSpace(text[mid - i]))
                        {
                            mid = mid - i;
                            break;
                        }
                    }
                }

                return text.Substring(0, mid)
                       + Environment.NewLine + text.Substring(mid + 1);
            }
            internal static string FirstLetterToUpper(string str)
            {
                if (str == null)
                    return null;

                if (str.Length > 1)
                    return char.ToUpper(str[0]) + str.Substring(1);

                return str.ToUpper();
            }
            /// <summary>
            /// Put newlines into a long string e.g. for tooltips
            /// A \ in the string is treated as a hard newline
            /// e.g. Recorded name pairs\Select then...
            /// </summary>
            public static string NewlinesInLongString(string longString, int maxLength = 44)
            {
                string result = string.Empty;
                var markedLines = longString.Split('\\');

                foreach (var line in markedLines)
                {
                    string _line = line;
                    while (_line.Length > maxLength)
                    {
                        int splitIndex = _line.Substring(0, maxLength).LastIndexOf(" ");
                        if (splitIndex == -1) // no space found
                            splitIndex = maxLength; // split at max length anyway

                        result += (_line.Substring(0, splitIndex)) + Environment.NewLine;
                        _line = _line.Substring(splitIndex + 1);
                    }
                    result += _line + Environment.NewLine; // add the last remaining part of the line
                }

                return result;
            }
            public static string GetHostFromUrl(string url)
            {
                try
                {
                    Uri uri = new Uri(url);
                    return $"{uri.Scheme}://{uri.Host}";
                }
                catch (Exception e)
                {
                    Log.Warning($"Failed to extract host from URL {url}: {e.Message}");
                    return null;
                }
            }
        }

        public class Conversions
        {
            const float litresPerUSgallon = 3.78541f;
            const float kpaPerPsi = 6.89476f;

            public static int convertGallonsToLitres(float gallons)
            {
                return (int)Math.Ceiling(gallons * litresPerUSgallon);
            }

            public static float convertLitresToGallons(float litres, Boolean roundTo1dp = false)
            {
                if (litres <= 0)
                {
                    return 0f;
                }
                float gallons = litres / litresPerUSgallon;
                if (roundTo1dp)
                { // Mainly used for reporting so round up to 1/10 gallon
                    return ((float)Math.Ceiling(gallons * 10f)) / 10f;
                }
                return gallons;
            }

            public static int convertPSItoKPA(int psi)
            {
                return (int)Math.Round(psi * kpaPerPsi);
            }
        }
        public static class FileNames
        {
            public static readonly String TimeStamp      = "yyyy-MM-dd__HH-mm";  // ISO-8601 modified for Windows/DOS
            public static readonly string TimeStampMatch = "????-??-??__??-??";  // Regex to match
            static readonly String TimeStampSeconds = "yyyy-MM-dd__HH-mm-ss";  // ISO-8601 modified for Windows/DOS

            static string TimeStampedFileName(string prefix, string extension, bool seconds = false)
            {
                String filename = prefix + DateTime.Now.ToString(seconds? TimeStampSeconds: TimeStamp) + extension;
                return filename;
            }

            /// <summary>
            /// Return a unique new file path
            /// Usually path/prefix"yyyy-MM-dd__HH-mm".extension but if that
            /// already exists then seconds will be added: "yyyy-MM-dd__HH-mm-ss"
            /// </summary>
            /// <param name="path"></param>
            /// <param name="prefix"></param>
            /// <param name="extension"></param>
            /// <returns></returns>
            public static string TimeStampedFilePath(string path, string prefix, string extension)
            {
                string filePath = Path.Combine(path, TimeStampedFileName(prefix, extension));
                if (File.Exists(filePath))
                {   // Add seconds to differentiate
                    filePath = Path.Combine(path, TimeStampedFileName(prefix, extension, true));
                }
                return filePath;
            }
            public static String MakeValidForPathName(String text)
            {
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    text = text.Replace(c, '_');
                }
                return text;
            }
        }
        /// <summary>
        /// Read the command line arguments into a dictionary
        /// </summary>
        public class CommandLineParametersReader
        {
            private string[] _args
            {
                get;
            }
            public Dictionary<string, string> _dict
            {
                get;
            }

            private bool CaseSensitive
            {
                get;
            }

            public CommandLineParametersReader(string[] args = null, bool isCaseSensitive = false)
            {
                if (args == null)
                {
                    args = Environment.GetCommandLineArgs();
                }
                _args = args;
                CaseSensitive = isCaseSensitive;
                _dict = new Dictionary<string, string>();
                Process();
            }

            // Process Arguments into KeyPairs
            private void Process()
            {
                string currentKey = null;
                foreach (var arg in _args)
                {
                    var s = arg.Trim();
                    if (s.StartsWith("-"))
                    {
                        currentKey = s.Substring(1);
                        if (!CaseSensitive)
                        {
                            currentKey = currentKey.ToLower();
                        }
                        _dict[currentKey] = "";
                    }
                    else
                    {
                        if (currentKey != null)
                        {
                            _dict[currentKey] = s;
                            currentKey = null;
                        }
                    }
                }
            }

            // Return the Key with a default value
            public string Get(string key, string defaultvalue = null)
            {
                if (!CaseSensitive)
                {
                    key = key.ToLower();
                }
                return _dict.ContainsKey(key) ? _dict[key] : defaultvalue;
            }

            public void Add(string key, string value)
            {
                _dict[key] = value;
            }
            public void Remove(string key)
            {
                if (_dict.ContainsKey(key))
                {
                    _dict.Remove(key);
                }
            }
            /// <summary>
            /// Return a -c_[command] argument
            /// </summary>
            /// <returns>
            /// The command or "" if none
            /// </returns>
            public string GetCommandArg()
            {
                string cmd = "";
                foreach (var arg in _dict)
                {
                    if (arg.Key.StartsWith("c_"))
                    {
                        cmd = "-" + arg.Key;
                    }
                }
                return cmd;
            }
        }

#region ReportException
        internal static void ReportException(Exception e, string msg, bool needReport)
        {
            // States:
            // Need report: yes/no
            // Also
            // During start up (or shutdown):
            //   no forms, so don't try to copy to the clipboard, but the
            //   MessageBoxWithCopy has a button for that
            // During normal operation but some game details are available:
            //   copy to the clipboard but also ask user for missing game details
            // During normal operation and game details are available:
            //   copy to the clipboard
            bool clippy = Application.OpenForms.Count > 0; // Whether the clipboard is available
            String errorReport = null;
            MainWindow.MajorSettings.Load();
            errorReport += "Error report:\n";
            errorReport += msg + "\n";
            errorReport += e.Message + "\nStack trace: " + String.Join(",", e.StackTrace);
            int innerExceptionCount = 0;
            int maxReportableInnerExceptions = 5;   // in case we have a circular set of inner exception references
            Exception innerException = e.InnerException;
            while (innerException != null && innerExceptionCount < maxReportableInnerExceptions)
            {
                errorReport += "\n\nInner exception " + innerExceptionCount + " message: " + e.InnerException.Message +
                    "\nInner exception " + innerExceptionCount + " stack trace: " + String.Join(",", e.InnerException.StackTrace);
                innerException = innerException.InnerException;
                innerExceptionCount++;
            }
            // Write it to the console window if it's live
            Console.WriteLine(
                "==================================================================" + Environment.NewLine
                );
            Console.WriteLine(errorReport);
            Console.WriteLine(
                "==================================================================" + Environment.NewLine
            );

            if (needReport)
            {
                requestReport(clippy, errorReport);
            }
        }

        private static void requestReport(bool clippy, string errorReport)
        {
            string consoleLogFilename = null;
            try
            {
                consoleLogFilename = MainWindow.instance.saveConsoleOutputText(false);
            }
            catch (Exception ex)
            {
            }

            string requestInfo = "Faults like this are often difficult to track down and only limited\n" + 
                "information is available so please provide the following additional details:\n\n" +
                "What were you doing with Crew Chief at the time?\nDoes it happen most times you run Crew Chief or just occasionally?\n";
            // Ask for any major settings that are not set
            foreach (var majorSetting in MainWindow.MajorSettings.majorSettings)
            {
                if (majorSetting.Value.Setting == null)
                {
                    requestInfo += majorSetting.Value.QueryIfNotSet + "\n";
                }
            }
            requestInfo += "\n";

            requestInfo += StatusReport();

            // To avoid threading issues with the clipboard, ensure
            // that all clipboard operations are performed on a
            // thread running in STA (Single Thread Apartment) mode.
            if (clippy)
            {
                var mainForm = Application.OpenForms[0];
                mainForm.Invoke((Action)(() => Clipboard.SetText(requestInfo + errorReport)));
            }
            //else
            //{
            //    MessageBox.Show("Couldn't copy to the clipboard because no UI thread is available.", "Error");
            //}

            if (consoleLogFilename == null)
            {
                // Console window not live yet
                MessageBoxWithCopy.Show((clippy ? "The following text should have been copied to the clipboard.\n" : "")
                    + (clippy ? "Please paste this report (or a screen capture of this window)" : "Please click the 'Copy' button (or a screen capture of this window)\n")
                    + "and paste it to the Crew Chief team via the forum or Discord.",
                    requestInfo + errorReport,
                    "Fatal error during start up",
                    MessageBoxButtons.OK);
            }
            else
            {
                MessageBox.Show($"The following text should be found in {consoleLogFilename}\n"
                    + "but should have been copied to the clipboard too.\n"
                    + "Please SEND THE LOG FILE which will give more information or paste this report to the Crew Chief team via the forum or Discord."
                    + "\n\n" + requestInfo + errorReport,
                    "Fatal error when running",
                    MessageBoxButtons.OK);
            }
        }

        public static string StatusReport()
        {
            string requestInfo = "Information about the current state of Crew Chief that is available:\n";
            // Report all major settings that are set
            foreach (var majorSetting in MainWindow.MajorSettings.majorSettings)
            {
                if (majorSetting.Value.Setting != null)
                {
                    requestInfo += majorSetting.Key + ": " + majorSetting.Value.Setting + "\n";
                }
            }
            if (MainWindow.instance != null)
            {
                requestInfo += "MainWindow minimised/maximised/normal: " + MainWindow.instance.WindowState.ToString() + "\n";
            }
            else
            {
                requestInfo += "MainWindow not active\n";
            }
            requestInfo += "\n";
            return requestInfo;
        }
#endregion ReportException

        internal static bool InterruptedSleep(int totalWaitMillis, int waitWindowMillis, Func<bool> keepWaitingPredicate)
        {
            Debug.Assert(totalWaitMillis > 0 && waitWindowMillis > 0);
            var waitSoFar = 0;
            while (waitSoFar < totalWaitMillis)
            {
                if (!keepWaitingPredicate())
                    return false;

                Thread.Sleep(waitWindowMillis);
                waitSoFar += waitWindowMillis;
            }

            return true;
        }

        internal static bool IsFlagOn<E, F>(E value, F flag)
        {
            return (Convert.ToInt32(value) & Convert.ToInt32(flag)) != 0;
        }

        internal static bool IsFlagOff<E, F>(E value, F flag)
        {
            return !Utilities.IsFlagOn(value, flag);
        }

        internal static bool TryBackupBrokenFile(string filePath, string backupExt, string msg)
        {
            try
            {
                var brokenFilePath = Path.ChangeExtension(filePath, backupExt);
                Console.WriteLine($"{msg} - renaming \"{filePath}\" to \"{brokenFilePath}\"");
                File.Delete(brokenFilePath);
                File.Move(filePath, brokenFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File.Move failed for {filePath} exception: {ex.Message}");
                return false;
            }

            return true;
        }

        public static int SizeOf<T>()
        {
            return Marshal.SizeOf(typeof(T));
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern long GetTickCount64();

        public static IEnumerable<Enum> GetEnumFlags(Enum input)
        {
            foreach (Enum value in Enum.GetValues(input.GetType()))
            {
                if (input.HasFlag(value))
                    yield return value;
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(string section, string key,
            string defaultValue, StringBuilder value, int size, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WritePrivateProfileString(string section, string key,
            string value, string filePath);

        public static string ReadIniValue(string section, string key, string filePath, string defaultValue = "")
        {
            var value = new StringBuilder(512);
            GetPrivateProfileString(section, key, defaultValue, value, value.Capacity, filePath);
            return value.ToString();
        }

        public static bool WriteIniValue(string section, string key, string value, string filePath)
        {
            bool result = WritePrivateProfileString(section, key, value, filePath);
            return result;
        }

        public static bool IsWindows11OrLater()
        {
            var currentVersion = Environment.OSVersion.Version;
            var win11Version = new Version(10, 0, 22000, 0);

            return currentVersion >= win11Version;
        }

        public static DialogResult MessageBoxShow(string text)
        {
            if (MainWindow.darkModeCS != null)
                return Messenger.MessageBox(text);

            return MessageBox.Show(text);
        }

        public static DialogResult MessageBoxShow(string text, string caption, MessageBoxButtons buttons)
        {
            if (MainWindow.darkModeCS != null)
                return Messenger.MessageBox(text, caption, buttons);

            return MessageBox.Show(text, caption, buttons);
        }

        public static DialogResult MessageBoxShow(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (MainWindow.darkModeCS != null)
                return Messenger.MessageBox(text, caption, buttons, icon);

            return MessageBox.Show(text, caption, buttons, icon);
        }

        public static DialogResult MessageBoxShow(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
        {
            if (MainWindow.darkModeCS != null)
                return Messenger.MessageBox(text, caption, buttons, icon/*, defaultButton, options*/); // TODO_DT: not exact mapping.

            return MessageBox.Show(text, caption, buttons, icon, defaultButton, options);
        }
    }

    /// <summary>
    /// Get source file and line number info for logging purposes
    /// </summary>
    public static class SourceInfoHelpers
    {
        public static string SourceFile(
            [CallerFilePath] string callerFile = "")
        {
            var fileName = Path.GetFileName(callerFile);
            return fileName;
        }
        public static int SourceLineNumber(
            [CallerLineNumber] int callerLine = 0)
        {
            return callerLine;
        }
    }

    public class WebsocketData : WebSocketBehavior
    {
        private String channelOpenStringResponse = "{\"channelOpen\": true}";
        private String channelClosedStringResponse = "{\"channelOpen\": false}";
        protected override void OnMessage(MessageEventArgs e)
        {
            Send(Utilities.audioPlayer.isChannelOpen() ? channelOpenStringResponse : channelClosedStringResponse);
        }
    }

    public class GameDataWebsocketData : WebSocketBehavior
    {
        private static GameDataReader gameDataReader;
        private static GameDataSerializer gameDataSerializer;

        public static void init(GameDataReader gameDataReader, GameDataSerializer gameDataSerializer)
        {
            GameDataWebsocketData.gameDataReader = gameDataReader;
            GameDataWebsocketData.gameDataSerializer = gameDataSerializer;
        }

        public static void reset()
        {
            GameDataWebsocketData.gameDataReader = null;
            GameDataWebsocketData.gameDataSerializer = null;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Send(GameDataWebsocketData.gameDataSerializer.Serialize(GameDataWebsocketData.gameDataReader.getLatestGameData(), e.Data));
        }
    }
}
