using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using CrewChiefV4.GameState;
using CrewChiefV4.UserInterface;

namespace CrewChiefV4
{
    public partial class MainWindow
    {
        #region Interface items
        public RichTextBox ConsoleTextBox => consoleTextBox;
        public void ClearConsole()
        {
            clearConsole();
        }
        public string PrefixLogfile(string path = null)
        {
            return prefixLogfile();
        }
        public string SaveConsoleOutputText(bool saveAs)
        {
            return saveConsoleOutputText(saveAs);
        }
        public void LogNowSend()
        {
            string LogFilePath = saveConsoleOutputText(true);
            var logTraceDialog = new SendLogTraceDialog();
            logTraceDialog.Log_TraceSend(LogFilePath);
        }
        public void LogSavedSend()
        {
            var logTraceDialog = new SendLogTraceDialog();
            logTraceDialog.LogFileChoose();
        }
        #endregion Interface items
        private readonly Boolean saveConsoleAfterEachRun = UserSettings.GetUserSettings().getBoolean("save_console_after_each_run");
        private ControlWriter consoleWriter = null;
        public static bool autoScrollConsole = true;
        private AutoResetEvent consoleUpdateThreadWakeUpEvent = new AutoResetEvent(false);
        private bool consoleUpdateThreadRunning = false;


        private class ControlWriter : TextWriter
        {
            private int repetitionCount = 0;
            private String previousMessage = null;

            public Boolean enable = true;
            public StringBuilder builder = new StringBuilder();

            public StringBuilder newMessagesBuilder = new StringBuilder();
            public static object controlWriterLock = new object();
            private AutoResetEvent consoleUpdateThreadWakeUpEvent = null;
            public ControlWriter(RichTextBox textbox, AutoResetEvent consoleUpdateThreadWakeUpEvent)
            {
                this.consoleUpdateThreadWakeUpEvent = consoleUpdateThreadWakeUpEvent;
            }

            public override void WriteLine(string value)
            {
                try
                {
                    if (MainWindow.instance != null && (enable || CrewChief.Debug.UserTraceLogging))
                    {
                        if (value == previousMessage)
                        {
                            repetitionCount++;
                        }
                        else
                        {
                            if (repetitionCount > 0 && repetitionCount < 20)
                            {
                                writeMessage("Skipped " + repetitionCount + " copies of previous message\n");
                            }
                            else if (repetitionCount >= 20 && MainWindow.instance.crewChief.mapped && !value.Contains("ValidationException"))
                            {
                                writeMessage("++++++++++++ Skipped " + repetitionCount + " copies of previous message. Please report this error to the CC dev team ++++++++++++\n");
                            }
                            repetitionCount = 0;
#if !DEBUG  // Do not swallow duplicates in the debug build.
                            previousMessage = value;
#endif
                            Boolean gotDateStamp = false;
                            StringBuilder sb = new StringBuilder();
                            DateTime now = DateTime.Now;
                            if (CrewChief.loadDataFromFile)
                            {
                                if (CrewChief.currentGameState != null)
                                {
                                    if (CrewChief.currentGameState.CurrentTimeStr == null || CrewChief.currentGameState.CurrentTimeStr == "")
                                    {
                                        CrewChief.currentGameState.CurrentTimeStr = GameStateData.CurrentTime.ToString("HH:mm:ss.fff");
                                    }
                                    sb.Append(now.ToString("HH:mm:ss.fff")).Append(" (").Append(CrewChief.currentGameState.CurrentTimeStr).Append(")");
                                    gotDateStamp = true;
                                }
                            }
                            if (!gotDateStamp)
                            {
                                sb.Append(now.ToString("HH:mm:ss.fff"));
                            }
                            sb.Append(" : ").Append(value).AppendLine();
                            writeMessage(sb.ToString());
                        }
                    }
                }
                catch (Exception e)
                {
                    // not much we can do here
                }
            }

            private void writeMessage(String message)
            {
                if (enable)
                {
                    lock (ControlWriter.controlWriterLock)
                    {
                        newMessagesBuilder.Append(message);
                        Debug.Write(message);
                    }
                    consoleUpdateThreadWakeUpEvent.Set();
                }
                else
                {
                    lock (ControlWriter.controlWriterLock)
                    {
                        builder.Append(message);
                    }
                }
            }

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }
        }

        public void clearConsole()
        {
            if (!consoleTextBox.IsDisposed)
            {
                try
                {
                    lock (this)
                    {
                        consoleTextBox.Text = "";
                        consoleWriter.builder.Clear();
                    }
                }
                catch (Exception)
                {
                    // swallow - nothing to log it to
                }
            }
        }

        /// <summary>
        /// Save the console window text in a timestamped log file.
        /// Keep the last 25 log files, don't delete any that have been renamed.
        /// </summary>
        internal string saveConsoleOutputText(Boolean saveAs)
        {
            String prefix = "console_";
            String matchString = prefix + Utilities.FileNames.TimeStampMatch + ".txt";
            int numberFilesToKeep = 25;
            try
            {
                if (consoleTextBox.Text.Length > 0)
                {
                    String path = DataFiles.UserLogsFolder;
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    // Get the list of console_*.txt log files in the log folder
                    // then DateTime.TryParse the timestamp bit to filter the
                    // ones with just the timestamp CC gave them (i.e. not
                    // renamed by user because they want to keep them). Sort
                    // into LastWriteTime order and keep the most recent 'numberFilesToKeep'
                    DateTime __; // (To discard result of DateTime.TryParse)
                    CultureInfo enGB = new CultureInfo("en-GB");
                    foreach (var fi in new DirectoryInfo(path).GetFiles(matchString)
                                 .Where(file => DateTime.TryParseExact(
                                     file.Name.Substring(prefix.Length, 
                                         file.Name.Length - file.Extension.Length - prefix.Length),
                                     Utilities.FileNames.TimeStamp,
                                     enGB,
                                     DateTimeStyles.None,
                                     out __) )
                                 .OrderByDescending(x => x.LastWriteTime)
                                 .Skip(numberFilesToKeep))
                    {
                        fi.Delete();
                    }
                    path = Utilities.FileNames.TimeStampedFilePath(path, prefix, ".txt");
                    if (saveAs)
                    {
                        SaveFileDialog saveFileDialog = new SaveFileDialog();
                        saveFileDialog.Filter = "Console log files (console_*.txt)|console_*.txt|All files (*.*)|*.*";
                        saveFileDialog.FilterIndex = 1;
                        saveFileDialog.RestoreDirectory = true;
                        saveFileDialog.FileName = Path.GetFileName(path);
                        saveFileDialog.InitialDirectory = Path.GetDirectoryName(path);
                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            path = saveFileDialog.FileName;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    File.WriteAllText(path, (prefixLogfile(path) + consoleTextBox.Text).Replace("\n", Environment.NewLine));
                    Console.WriteLine("Console output saved to " + path);
                    return path;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to save console output, message = " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Returns the log file prefix - path, VOICE_OPTION, non-default Properties
        /// </summary>
        /// <param name="path"></param>
        /// <returns>String with \n</returns>
        public static string prefixLogfile(string path = null)
        {
            if (path == null)
            {
                path = DataFiles.UserLogsFolder;
            }
            MajorSettings.Load();
            return $"{Log.AnonymisePath(path)}\n{Utilities.StatusReport()}" +
                   "\n\nNon-default Properties:\n" +
                   UserSettings.getNonDefaultUserSettings() + "\n";
        }

        /// <summary>
        /// Convoluted way to store a setting and its default value, 
        /// so we can display the default value in the error report if the setting is null
        /// </summary>
        public class MajorSetting
        {
            public string Setting { get; set; } // may be null
            public string QueryIfNotSet { get; set; }

            public MajorSetting(string defaultValue = null)
            {
                Setting = null;
                QueryIfNotSet = defaultValue;
            }
        }

        public static class MajorSettings
        {
            /// <summary>
            /// All the major settings we want to display in the error report, with their query text if not set.
            /// Key is the title, value is the MajorSetting object with Value and DefaultValue
            /// </summary>
            public static Dictionary<string, MajorSetting> majorSettings = new Dictionary<string, MajorSetting>
            {
                { "Crew Chief",        new MajorSetting("Version unknown") },
                { "Main Window Title", new MajorSetting( "Not set") },
                { "Game",              new MajorSetting( "What game were you playing?") },
                { "Last Track Name",   new MajorSetting( "What track were you driving?") },
                { "Last Car Name",     new MajorSetting( "What car were you driving?") },
                { "Voice Option",      new MajorSetting( "") },
                { "Fuel Multiplier",   new MajorSetting( "Not set") },
                { "Dark Mode",         new MajorSetting( "Disabled") }
            };

            /// <summary>
            /// Load the major settings from the current state of the application
            /// where possible, so we can display them in the error report.
            /// </summary>
            public static void Load()
            {
                majorSettings["Main Window Title"].Setting = $"{Configuration.getUIString("main_window_title_prefix")}" +
                       UserSettings.currentUserProfileFileName;
                majorSettings["Last Track Name"].Setting = null;
                majorSettings["Last Car Name"].Setting = null;
                majorSettings["Game"].Setting = CrewChief.gameDefinition != null ? CrewChief.gameDefinition.friendlyName : null;
                majorSettings["Fuel Multiplier"].Setting = CrewChief.fuelMultiplier != null ? CrewChief.fuelMultiplier.multiplier.ToString() : "Not set";
                if (instance != null && instance.crewChief != null)
                {
                    if (instance.crewChief.LatestTrackName != "unknown")
                    {
                        majorSettings["Last Track Name"].Setting = instance.crewChief.LatestTrackName;
                    }
                    if (instance.crewChief.LatestCarName != "unknown")
                    {
                        majorSettings["Last Car Name"].Setting = instance.crewChief.LatestCarName;
                    }
                }
                majorSettings["Crew Chief"].Setting = MainWindow.VersionInfo();
                majorSettings["Voice Option"].Setting = UserSettings.GetUserSettings().getString("VOICE_OPTION");
                majorSettings["Dark Mode"].Setting = DarkModeForms.DarkModeCS.IsDarkModeCSEnabled.ToString();
            }
        }

        /// <summary>
        /// Get the log prefix informations and the last lines of the
        /// Console window as context before an error report
        /// </summary>
        /// <param name="path">the log file</param>
        /// <param name="linesOfContext"></param>
        /// <returns></returns>
        public static List<string> ErrorContext(string path, int linesOfContext)
        {
            List<string> TakeLastLines(string text, int count)
            {   // Search backwards, more efficient than splitting the whole
                // Console window text
                List<string> lines = new List<string>();
                Match match = Regex.Match(text, "^.*$", RegexOptions.Multiline | RegexOptions.RightToLeft);

                while (match.Success && lines.Count < count)
                {
                    lines.Insert(0, match.Value);
                    match = match.NextMatch();
                }

                return lines;
            }

            if (instance != null)
            {
                List<string> context = prefixLogfile(path).Split('\n').ToList();
                context.AddRange(TakeLastLines(instance.consoleTextBox.Text, linesOfContext));
                return context;
            }
            return new List<string>{"No console log available"};
        }

        public static string getConsoleLog()
        {
            string consoleLog = null;
            lock (MainWindow.instanceLock)
            {
                if (MainWindow.instance != null)
                {
                    consoleLog = MainWindow.instance.consoleWriter.enable ?
                        MainWindow.instance.consoleTextBox.Text :
                        MainWindow.instance.consoleWriter.builder.ToString();
                    // MainWindow.instance.clearConsole();
                }
            }
            return consoleLog;
        }

        /// <summary>
        /// If Console flush is checked continuously flush the console output to 
        /// MY DOCUMENTS\CrewChief\CrewChiefV4\DebugLogs\console_flush.txt.
        /// The same file is used each time.
        /// </summary>
        class ConsoleFlush
        {
            static bool consoleFlush = UserSettings.GetUserSettings().getBoolean("console_flush");
            static readonly string consoleFlushFile = Path.Combine(DataFiles.UserLogsFolder, "console_flush.txt");
            internal static void write(string message)
            {
                if (consoleFlush)
                {
                    try
                    {
                        File.AppendAllText(consoleFlushFile, message);
                    }
                    catch (Exception ex)
                    {
                        consoleFlush = false;
                        Log.Warning($"Couldn't write console to {consoleFlushFile}");
                    }
                }
            }
            internal static void deleteOldFile()
            {
                if (consoleFlush)
                {
                    if (File.Exists(consoleFlushFile))
                    {
                        try
                        {
                            File.Delete(consoleFlushFile);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"Couldn't delete {consoleFlushFile} {ex.Message}");
                        }
                    }
                    write(Utilities.FileNames.TimeStampedFilePath("", "", "") + "\n");
                    write(prefixLogfile(consoleFlushFile));
                }
            }
        }
        private void consoleUpdateThreadWorker()
        {
            while (consoleUpdateThreadRunning)
            {
                consoleUpdateThreadWakeUpEvent.WaitOne();
                if (!consoleUpdateThreadRunning)
                {
                    Debug.WriteLine("Exiting console update thread.");
                    return;
                }

                if (!consoleWriter.enable)
                {
                    try
                    {
                        Debug.WriteLine("Exiting console update thread, console output disabled.");
                    }
                    catch (Exception e) { Log.Exception(e); }
                    return;
                }

                Debug.Assert(consoleTextBox.InvokeRequired);

                string messages = null;
                // Pick up the new messages.
                lock (ControlWriter.controlWriterLock)
                {
                    messages = consoleWriter.newMessagesBuilder.ToString();
                    consoleWriter.newMessagesBuilder.Clear();
                }

                ConsoleFlush.write(messages);

                if (MainWindow.instance != null
                    && consoleTextBox != null
                    && !consoleTextBox.IsDisposed
                    && !string.IsNullOrWhiteSpace(messages)
                    && consoleUpdateThreadRunning)
                {
                    try
                    {
                        consoleTextBox.Invoke((MethodInvoker)delegate
                        {
                            if (MainWindow.instance != null
                                && consoleTextBox != null
                                && !consoleTextBox.IsDisposed)
                            {
                                try
                                {
                                    consoleTextBox.AppendText(messages);
                                    if (MainWindow.autoScrollConsole)
                                    {
                                        consoleTextBox.ScrollToCaret();
                                    }
                                }
                                catch (Exception)
                                {
                                    // swallow - nothing to log it to
                                }
                            }
                        });
                    }
                    catch (Exception)
                    {
                        // Possible shutdown.
                    }
                }
            }
        }

        private void TextBoxConsole_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                consoleTextBox.SelectAll();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                consoleTextBox.DeselectAll();
            }
            else if (e.Alt && e.KeyCode == Keys.E)
            {
                menuStrip.consoleContextMenuStrip.Show(consoleTextBox.PointToScreen(new Point(250, 100)));
            }
        }

        // Set up console update thread.  We need this because we call Console.WriteLine from random threads.
        private void ConsoleUpdateThreadStart()
        {
            ConsoleFlush.deleteOldFile();
            ThreadStart ts = consoleUpdateThreadWorker;
            var consoleUpdateThread = new Thread(ts);
            consoleUpdateThread.Name = "MainWindow.consoleUpdateThreadWorker";
            consoleUpdateThreadRunning = true;
            ThreadManager.RegisterResourceThread(consoleUpdateThread);
            consoleUpdateThread.Start();
        }
        
        private void ConsoleWindowOpen(ContextMenuStrip consoleContextMenuStrip)
        {
            consoleTextBox.WordWrap = false;
            var _fontSize = Math.Max(UserSettings.GetUserSettings().getInt("console_font_size"), 8);
            consoleTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", _fontSize);
            consoleWriter = new ControlWriter(consoleTextBox, consoleUpdateThreadWakeUpEvent);
            consoleTextBox.KeyDown += TextBoxConsole_KeyDown;

            Console.SetOut(consoleWriter);

            if (!UserSettings.GetUserSettings().getBoolean("enable_console_logging"))
            {
                Console.WriteLine("Console logging has been disabled ('enable_console_logging' property)");
            }
            consoleWriter.enable = UserSettings.GetUserSettings().getBoolean("enable_console_logging");
            ConsoleTextBox.ContextMenuStrip = consoleContextMenuStrip;
        }

        private void ConsoleWindowClose()
        {
            saveConsoleOutputText(false);

            consoleUpdateThreadRunning = false;
            consoleUpdateThreadWakeUpEvent.Set();

            lock (consoleWriter)
            {
                consoleWriter.Dispose();
            }
        }

        private static void ConsoleWindowAppStart()
        {
#if !DEBUG
            // Don't disable auto scroll in Debug builds and in Profile mode.
            if (!UserSettings.GetUserSettings().getBoolean("enable_console_autoscroll"))
            {
                Console.WriteLine("Pausing console scrolling");
                MainWindow.autoScrollConsole = CrewChief.Debug.ProfileMode;
            }
#endif
        }

        private void ConsoleWindowAppStop()
        {
            if (saveConsoleAfterEachRun)
            {
                saveConsoleOutputText(false);
                clearConsole();
            }
        }
    }
}