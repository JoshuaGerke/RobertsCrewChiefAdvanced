using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("UnitTest")]

namespace CrewChiefV4
{
    /// <summary>
    /// Class to encapsulate Console.Write, with LogType added to each call.<br/>
    /// Each type can be turned on or off.<br/>
    /// Each log has its type prepended before it is written to the console.
    /// </summary>
    static class Log
    {
        [Flags]
        internal enum LogType
        {
            FatalError = 1 << 0,
            Error = 1 << 1,
            Warning = 1 << 2,
            Commentary = 1 << 3,
            Sound = 1 << 4,
            Subtitle = 1 << 5,   // All here and up show in release builds
            Spotter = 1 << 6,
            Info = 1 << 7,
            Fuel = 1 << 8,
            Tyres = 1 << 9,
            Brakes = 1 << 10,
            SoundDebug = 1 << 11,
            DebugError = 1 << 12,// cf. Debug.Assert
            Event = 1 << 13,     // EventBus events
            Debug = 1 << 14,     // All here and up show in debug builds, also if log_type_debug is set
            Verbose = 1 << 15,   // Shown if log_type_verbose is set
            Exception = 1 << 16
        };

        private static readonly Dictionary<LogType, string> logPrefixes = new
            Dictionary<LogType, string>
        {
            { LogType.FatalError, "FATAL ERROR: " },
            { LogType.Error     , "ERROR: " },
            { LogType.Warning   , "Warn: " },
            { LogType.Commentary, "Cmnt: " },
            { LogType.Sound     , "Sound: " },
            { LogType.Subtitle  , "Subt: " },
            { LogType.Spotter   , "Spot: " },
            { LogType.Info      , "Info: " },
            { LogType.Fuel      , "Fuel: " },
            { LogType.Tyres     , "Tyres: " },
            { LogType.Brakes    , "Brakes: " },
            { LogType.SoundDebug, "SnDbug: " },
            { LogType.DebugError, "DEBUG ERROR: " },
            { LogType.Event     , "Event: " },
            { LogType.Debug     , "Dbug: " },
            { LogType.Verbose   , "Verb: " },
            { LogType.Exception , "EXCEPTION: " }
        };

        private static LogType _logMask = setLogLevel(LogType.Subtitle);
        
        static Log()
        {
            var logTypesThatHavePropertiesToSetLevels = new List<LogType>
            {
                {LogType.Spotter},
                {LogType.Debug},
                {LogType.Verbose}
            };
            foreach (var logType in logTypesThatHavePropertiesToSetLevels)
            {
                var property = "log_type_" + logType.ToString().ToLower();
                if (UserSettings.GetUserSettings().getBoolean(property))
                {
                    enableLogTypes(logType);
                }
            }

            var logTypesThatHavePropertiesToSetThem = new List<LogType>
            {
                {LogType.Fuel},
                {LogType.Tyres},
                {LogType.Brakes},
                {LogType.Exception}
            };
            foreach (var logType in logTypesThatHavePropertiesToSetThem)
            {
                var property = "log_type_" + logType.ToString().ToLower();
                if (UserSettings.GetUserSettings().getBoolean(property))
                {
                    enableLogTypes(logType);
                }
            }

        }
        /// <summary>
        /// Set the log mask so all logs up to "logLevel" are shown
        /// </summary>
        /// <param name="logLevel"> log level</param>
        /// <returns></returns>
        public static LogType setLogLevel(LogType logLevel)
        {
            _logMask = 0;
            while (logLevel != 0)
            {
                enableLogTypes(logLevel);
                logLevel = (LogType)((int)logLevel / 2);
            }
            return _logMask;
        }

        /// <summary>
        /// Turn on specific log types
        /// </summary>
        /// <param name="logTypes"> log mask</param>
        /// <returns></returns>
        public static void enableLogTypes(LogType logTypes) => _logMask |= logTypes;

        /// <summary>
        /// Is specific log type enabled
        /// </summary>
        /// <param name="logTypes"> log mask</param>
        public static bool LogTypeEnabled(LogType logType)
        {
            return (_logMask & logType) != 0;
        }
        public static bool logDebug
        {
            get => (_logMask & LogType.Debug) != 0;
            set
            {
                UserSettings.GetUserSettings().setProperty("log_type_debug", value);
                if (value)
                {
                    _logMask |= LogType.Debug;
                }
                else
                {
                    _logMask &= ~LogType.Debug;
                }
                UserSettings.GetUserSettings().saveUserSettings();
            }
        }
        public static bool logVerbose
        {
            get => (_logMask & LogType.Verbose) != 0;
            set
            {
                UserSettings.GetUserSettings().setProperty("log_type_verbose", value);
                if (value)
                {
                    _logMask |= LogType.Verbose;
                }
                else
                {
                    _logMask &= ~LogType.Verbose;
                }
                UserSettings.GetUserSettings().saveUserSettings();
            }
        }
        public static bool logFuel
        {
            get => (_logMask & LogType.Fuel) != 0;
            set
            {
                UserSettings.GetUserSettings().setProperty("log_type_fuel", value);
                if (value)
                {
                    _logMask |= LogType.Fuel;
                }
                else
                {
                    _logMask &= ~LogType.Fuel;
                }
                UserSettings.GetUserSettings().saveUserSettings();
            }
        }
        public static bool logTyres
        {
            get => (_logMask & LogType.Tyres) != 0;
            set
            {
                UserSettings.GetUserSettings().setProperty("log_type_tyres", value);
                if (value)
                {
                    _logMask |= LogType.Tyres;
                }
                else
                {
                    _logMask &= ~LogType.Tyres;
                }
                UserSettings.GetUserSettings().saveUserSettings();
            }
        }
        public static bool logBrakes
        {
            get => (_logMask & LogType.Brakes) != 0;
            set
            {
                UserSettings.GetUserSettings().setProperty("log_type_brakes", value);
                if (value)
                {
                    _logMask |= LogType.Brakes;
                }
                else
                {
                    _logMask &= ~LogType.Brakes;
                }
                UserSettings.GetUserSettings().saveUserSettings();
            }
        }

        public static LogType LogMask
        {
            get
            {
                return _logMask;
            }
            /// <summary>
            /// Set the log mask so all logs matching "logMask" are shown
            /// Can set individual types of log, for example
            ///   enableLogTypes(LogType.Error | LogType.Subtitle);
            /// </summary>
            set
            {
                _logMask = value;
                CrewChief.Debug.LoggingDebug = (_logMask & LogType.Debug) != 0;
            }
        }

        public delegate void LogSink(string log);

        public static LogSink logSink { get; set; } = Console.WriteLine;

        /// <summary>
        /// We ask users to send log files so replace c:\users\ NAME \My Documents with MY DOCUMENTS
        /// </summary>
        public static string AnonymisePath(string filePath) => 
            filePath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MY DOCUMENTS");
        /// <summary>
        /// Write "log" to Console if logType is enabled
        /// "log" has its type prepended before it is written.
        /// </summary>
        private static void _Log(LogType logType, string log)
        {
            if (string.IsNullOrEmpty(log))
            {   // Probably a DontSpam() that returned null
                return;
            }
            if ((logType & _logMask) != 0)
            {
                log = AnonymisePath(log);
                switch (logType)
                {
                    case LogType.FatalError:
                        {
                            logSink(new string('*', (logPrefixes[logType] + log).Length));
                            logSink(logPrefixes[logType] + log);
                            logSink(new string('*', (logPrefixes[logType] + log).Length));
                            writeToErrorLog(log);
                        }
                        break;
                    case LogType.Exception:
                        {
                            logSink(logPrefixes[logType] + log);
                            ToastManager.Exception(MainWindow.instance, log);
                            writeToErrorLog(log);
                        }
                        break;
                    case LogType.Error:
                        {
                            logSink(logPrefixes[logType] + log);
                            ToastManager.Error(MainWindow.instance, log);
                            writeToErrorLog(log);
                        }
                        break;
                    case LogType.Warning:
                        {
                            logSink(logPrefixes[logType] + log);
                            ToastManager.Warning(MainWindow.instance, log);
                        }
                        break;
                    default:
                        {
                            logSink(logPrefixes[logType] + log);
                        }
                        break;
                }
            }
            return;

            void writeToErrorLog(string _log)
            {
                if (UnitTest.UnitTest.Active)
                {
                    return;
                }
                try
                {
                    const string divider = "------------------";
                    string path = Path.Combine(DataFiles.UserLogsFolder, "ErrorLog.txt");
                    string currentErrors = File.Exists(path) ? File.ReadAllText(path) : "";
                    string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                    string anonymisedPath = AnonymisePath(path);
                    if (currentErrors.Contains(_log))
                    {
                        int count = 0;
                        int index = 0;
                        const int maxDuplicates = 10;

                        // Don't spam the log with the same error repeatedly
                        while ((index = currentErrors.IndexOf(_log, index, StringComparison.OrdinalIgnoreCase)) != -1)
                        {
                            count++;
                            index += _log.Length; // Move past the current match

                            if (count == maxDuplicates)
                            {
                                Log.DontSpam($"Duplicate {logType.ToString()} _log already exists {maxDuplicates} times in {anonymisedPath}, not adding another").Commentary();
                                return;
                            }
                        }
                        writeErrorReport_discardIfFileLocked(path, $"\n{timeStamp}: ");
                        writeErrorReport_discardIfFileLocked(path, $"Repeat of {logPrefixes[logType]}  {_log.Split('\n')[0]}");
                        Log.Commentary($"Duplicate {logType.ToString()} _log added to {anonymisedPath}");
                    }
                    else
                    {
                        List<string> errorReport = new List<string> { divider };
                        errorReport.Add($"{timeStamp}");
                        errorReport.AddRange(MainWindow.ErrorContext(path, 10));
                        errorReport.Add(logPrefixes[logType] + _log);
                        errorReport.Add(divider);
                        writeErrorReport_discardIfFileLocked(path, string.Join(Environment.NewLine, errorReport));
                        Log.Commentary($"{logType.ToString()} _log added to {anonymisedPath}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                return;
            }
            void writeErrorReport_discardIfFileLocked(string filePath, string _log)
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                {
                    try
                    {
                        fileStream.Lock(0, fileStream.Length);
                        fileStream.Seek(0, SeekOrigin.End);

                        using (StreamWriter writer = new StreamWriter(fileStream))
                        {
                            writer.WriteLine(_log);
                        }
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"{filePath} still locked");
                    }
                    finally
                    {
                        try
                        {
                            fileStream.Unlock(0, fileStream.Length);
                        }
                        catch (Exception)
                        {
                            // ignore
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Lazy evaluation version of _Log that takes a Func<string>
        /// that will only be evaluated if the logType is enabled.
        /// e.g. Log.Debug(() => $"Complex message {ExpensiveOperation()}");
        /// Only used (only necessary) for log types lower than Commentary
        /// </summary>
        /// <param name="logType"></param>
        /// <param name="messageFunc"></param>
        private static void _Log(LogType logType, Func<string> messageFunc)
        {
            if ((logType & _logMask) != 0 &&
                messageFunc != null)
            {
                _Log(logType, messageFunc());
            }
        }

        private static readonly Dictionary<string, (DateTime Time, int deSpamSeconds)> _lastSeen = new Dictionary<string, (DateTime Time, int Cooldown)>();
        /// <summary>
        /// Return "log" if it hasn't been seen in the last "deSpamSeconds" seconds, otherwise return null.
        /// Example usage: Log.Warning(Log.DontSpam("iRacing SDK is not connected", 5));
        /// or fluent version: Log.DontSpam("iRacing SDK is not connected", 5).Warning();
        /// </summary>
        /// <returns></returns>
        public static string DontSpam(string log, int deSpamSeconds = 5)
        {
            return DontSpam(log, "", deSpamSeconds);
        }

        /// <summary>
        /// Return "significantText + restOfLog" if "significantText" hasn't 
        /// been seen in the last "deSpamSeconds" seconds, otherwise return null.
        /// </summary>
        /// <param name="significantText"></param>
        /// <param name="restOfLog"></param>
        /// <returns></returns>
        public static string DontSpam(string significantText, string restOfLog, int deSpamSeconds = 5)
        {
            var now = DateTime.UtcNow;

            lock (_lastSeen)
            {
                // Cleanup expired entries
                var expiredKeys = _lastSeen
                    .Where(kvp => (now - kvp.Value.Time).TotalSeconds >= kvp.Value.Item2)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _lastSeen.Remove(key);
                }

                // Check if the significant text was seen recently
                if (_lastSeen.TryGetValue(significantText, out var entry))
                {
                    if ((now - entry.Time).TotalSeconds < deSpamSeconds)
                    {
                        return null; // Suppress duplicate
                    }
                }

                // Update last seen time
                _lastSeen[significantText] = (now, deSpamSeconds);
            }

            return significantText + restOfLog;
        }
        /// <summary>
        /// Write "log" with object to Console if LogType is enabled
        /// Example:
        ///   logLog(LogType.Warning, "Warning log {0}", 1.5f);
        /// </summary>
        /// <param name="logType"></param>
        /// <param name="log">format string containing {0}</param>
        /// <param name="arg0">the object to replace {0}</param>
        private static void _Log(LogType logType, string log, object arg0) => _Log(logType, String.Format(log, arg0));
        private static void _Log(LogType logType, string log, object arg0, object arg1) => _Log(logType, String.Format(log, arg0, arg1));
        private static void _Log(LogType logType, string log, object arg0, object arg1, object arg2) => _Log(logType, String.Format(log, arg0, arg1, arg2));
        private static void _Log(LogType logType, string log, object arg0, object arg1, object arg2, object arg3) => _Log(logType, String.Format(log, arg0, arg1, arg2, arg3));
        private static void _Log(LogType logType, string log, object arg0, object arg1, object arg2, object arg3, object arg4, object arg5) => _Log(logType, String.Format(log, arg0, arg1, arg2, arg3, arg4, arg5));

        #region Shorthand calls
        /// <summary>
        /// Write "log" to Console if logType.FatalError is enabled
        /// </summary>
        public static void Fatal(string log) => _Log(LogType.FatalError, log);
        /// <summary>
        /// Write "log" to Console if logType.Error is enabled
        /// </summary>
        public static void Error(string log) => _Log(LogType.Error, log);
        /// <summary>
        /// Write "log" to Console if logType.Warning is enabled
        /// </summary>
        public static void Warning(string log) => _Log(LogType.Warning, log);
        public static void Warning(string log, object arg1, object arg2) => _Log(LogType.Warning, log, arg1, arg2);
        /// <summary>
        /// Write "log" to Console if logType.Commentary is enabled
        /// </summary>
        public static void Commentary(string log) => _Log(LogType.Commentary, log);
        public static void Commentary(string log, object arg1) => _Log(LogType.Commentary, log, arg1);
        public static void Commentary(string log, object arg1, object arg2) => _Log(LogType.Commentary, log, arg1, arg2);
        public static void Commentary(string log, object arg1, object arg2, object arg3) => _Log(LogType.Commentary, log, arg1, arg2, arg3);
        public static void Commentary(string log, object arg1, object arg2, object arg3, object arg4) => _Log(LogType.Commentary, log, arg1, arg2, arg3, arg4);
        public static void Commentary(string log, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6) => _Log(LogType.Commentary, log, arg1, arg2, arg3, arg4, arg5, arg6);
        /// <summary>
        /// Write "log" to Console if logType.Sound is enabled
        /// </summary>
        public static void Sound(string log) => _Log(LogType.Sound, log);
        public static void Sound(Func<string> messageFunc) => _Log(LogType.Sound, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.Subtitle is enabled
        /// </summary>
        public static void Subtitle(string log) => _Log(LogType.Subtitle, log);
        public static void Subtitle(Func<string> messageFunc) => _Log(LogType.Subtitle, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.Spotter is enabled
        /// </summary>
        public static void Spotter(string log) => _Log(LogType.Spotter, log);
        public static void Spotter(Func<string> messageFunc) => _Log(LogType.Spotter, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.Info is enabled
        /// </summary>
        public static void Info(string log) => _Log(LogType.Info, log);
        public static void Info(Func<string> messageFunc) => _Log(LogType.Info, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.Fuel is enabled
        /// </summary>
        public static void Fuel(string log) => _Log(LogType.Fuel, log);
        public static void Fuel(Func<string> messageFunc) => _Log(LogType.Fuel, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.SoundDebug is enabled
        /// </summary>
        public static void SoundDebug(string log) => _Log(LogType.SoundDebug, log);
        public static void SoundDebug(Func<string> messageFunc) => _Log(LogType.SoundDebug, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.DebugError is enabled, cf. Debug.Assert
        /// </summary>
        public static void DebugError(string log) => _Log(LogType.DebugError, log);
        public static void DebugError(Func<string> messageFunc) => _Log(LogType.DebugError, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.Event is enabled
        /// </summary>
        public static void Event(string log) => _Log(LogType.Event, log);
        public static void Event(Func<string> messageFunc) => _Log(LogType.Event, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.Debug is enabled
        /// </summary>
        public static void Debug(string log) => _Log(LogType.Debug, log);
        public static void Debug(Func<string> messageFunc) => _Log(LogType.Debug, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.Tyres is enabled
        /// </summary>
        public static void Tyres(string log) => _Log(LogType.Tyres, log);
        public static void Tyres(Func<string> messageFunc) => _Log(LogType.Tyres, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.Brakes is enabled
        /// </summary>
        public static void Brakes(string log) => _Log(LogType.Brakes, log);
        public static void Brakes(Func<string> messageFunc) => _Log(LogType.Brakes, messageFunc);
        /// <summary>
        /// Write "log" to Console if logType.Verbose is enabled
        /// </summary>
        public static void Verbose(string log) => _Log(LogType.Verbose, log);
        public static void Verbose(Func<string> messageFunc) => _Log(LogType.Verbose, messageFunc);
        /// <summary>
        /// Write Exception details to Console if logType.Exception is enabled
        /// Precede with optionalText if the arg is included
        /// </summary>
        public static void Exception(Exception exception, string optionalText="")
        {
            if (!string.IsNullOrEmpty(optionalText))
            {
                optionalText += Environment.NewLine;
            }
            _Log(LogType.Exception, optionalText + exception.Message + Environment.NewLine + exception.StackTrace);
        }
        #endregion Shorthand calls
    }
}
