using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

using CrewChiefV4.Audio;
using CrewChiefV4.Events.PubSub;
using CrewChiefV4.UserInterface.TopicWindows;
using CrewChiefV4.UserInterface;

namespace CrewChiefV4
{
    /// <summary>
    /// The idea was to be able to use the VS designer on bits of the main
    /// Window without an enormous diff and messed up layout.  Didn't really
    /// work out but this does allow the menu strip to be edited without
    /// affecting the rest of the main window.
    /// This also provides the console context menu with the same options
    /// as the Console menu item.
    /// It may still be possible to prototype changes using a designer and copy
    /// the code into here.
    /// </summary>
    public sealed class MainMenuStrip : MenuStrip
    {
        public readonly ContextMenuStrip consoleContextMenuStrip;
        private readonly ToolStripMenuItem namesToolStripMenuItem;
        private readonly ToolStripMenuItem helpToolStripMenuItem;
        private readonly ToolStripMenuItem speechWizardToolStripMenuItem;
        private readonly ToolStripMenuItem controllerButtonNamesToolStripMenuItem;

        private readonly ToolStripMenuItem topicWindowsToolStripMenuItem;
        private readonly ToolStripMenuItem topicBrakesToolStripMenuItem;
        private readonly ToolStripMenuItem topicFuelToolStripMenuItem;
        private readonly ToolStripMenuItem topicTyresToolStripMenuItem;

        private readonly IMainWindow mainWindow;
        private TopicWindowBrakes brakesWindow;
        private TopicWindowFuel fuelWindow;
        private TopicWindowTyres tyresWindow;

        public MainMenuStrip(IMainWindow mw, Font exemplarFont, bool tracelogToolStripMenuItem = true, bool tracePlaybackToolStripMenuItem = true)
        {
            mainWindow = mw;
            namesToolStripMenuItem = new ToolStripMenuItem();
            speechWizardToolStripMenuItem = new ToolStripMenuItem();
            controllerButtonNamesToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            // Add Topic Windows menu
            topicWindowsToolStripMenuItem = new ToolStripMenuItem("Topic Windows");
            topicBrakesToolStripMenuItem = new ToolStripMenuItem("Brakes") { CheckOnClick = true };
            topicFuelToolStripMenuItem = new ToolStripMenuItem("Fuel") { CheckOnClick = true };
            topicTyresToolStripMenuItem = new ToolStripMenuItem("Tyres") { CheckOnClick = true };
            topicWindowsToolStripMenuItem.DropDownItems.Add(topicBrakesToolStripMenuItem);
            topicWindowsToolStripMenuItem.DropDownItems.Add(topicTyresToolStripMenuItem);
            topicWindowsToolStripMenuItem.DropDownItems.Add(topicFuelToolStripMenuItem);
            topicBrakesToolStripMenuItem.CheckedChanged += TopicBrakesToolStripMenuItemCheckedChanged;
            topicTyresToolStripMenuItem.CheckedChanged += TopicTyresToolStripMenuItemCheckedChanged;
            topicFuelToolStripMenuItem.CheckedChanged += TopicFuelToolStripMenuItemCheckedChanged;
            SuspendLayout();
            BackColor = DefaultBackColor;
            Location = new Point(0, 0);
            TabIndex = 503;
            Text = "menuStrip";
            Font = exemplarFont;
            Padding = new Padding(3, 1, 0, 1);

            var fileMenu = new FileMenu(mainWindow);
            var consoleMenu = new ConsoleMenu(mainWindow);
            consoleContextMenuStrip = consoleMenu.GetContextMenuStrip();
            var problemsMenu = new ProblemsMenu(mainWindow);
            if (tracelogToolStripMenuItem)
            {
                problemsMenu.tracelogMenu();
            }
            if (tracePlaybackToolStripMenuItem)
            {
                problemsMenu.tracePlaybackMenu();
            }
#if DEBUG
            problemsMenu.gameEventDebugMenu();
#endif
            Items.AddRange(new ToolStripItem[]
            {
                fileMenu.GetMenuItem(),
                consoleMenu.GetMenuItem(),
                namesToolStripMenuItem,
                speechWizardToolStripMenuItem,
                controllerButtonNamesToolStripMenuItem,
                topicWindowsToolStripMenuItem,
                problemsMenu.GetMenuItem(),
                helpToolStripMenuItem
            });

            namesToolStripMenuItem.Text = Configuration.getUIString("Opponent Names");
            namesToolStripMenuItem.Click += mOpponentNamesToolStripMenuItem_Click;

            speechWizardToolStripMenuItem.Text = Configuration.getUIString("Speech Wizard");
            speechWizardToolStripMenuItem.Click += mSpeechWizardToolStripMenuItem_Click;

            controllerButtonNamesToolStripMenuItem.Text = "Controller names";
            controllerButtonNamesToolStripMenuItem.Click += controllerButtonNamesToolStripMenuItem_Click;

            helpToolStripMenuItem.Text = Configuration.getUIString("help_menu");
            helpToolStripMenuItem.ShortcutKeyDisplayString = "F1";
            helpToolStripMenuItem.ShortcutKeys = Keys.F1;
            helpToolStripMenuItem.Click += helpToolStripMenuItem_Click;
        }

        private void mOpponentNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mainWindow.OpponentNames();
        }

        private void mSpeechWizardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mainWindow.SpeechWizard();
        }

        private void controllerButtonNamesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (ControllerNamesDialog dialog = new ControllerNamesDialog())
            {
                dialog.ShowDialog(MainWindow.instance);
            }
        }

        internal void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            mainWindow.ShowHelp();
        }


        public class FileMenu
        {
            private readonly ToolStripMenuItem fileToolStripMenuItem;
            private readonly ToolStripMenuItem mDataFolderToolStripMenuItem;
            private readonly ToolStripMenuItem mVoiceFolderToolStripMenuItem;
            private readonly ToolStripMenuItem mDeleteDebugFoldersToolStripMenuItem;
            private readonly ToolStripMenuItem mExitToolStripMenuItem;
            private readonly IMainWindow mainWindow;

            public FileMenu(IMainWindow mw)
            {
                mainWindow = mw;
                fileToolStripMenuItem = new ToolStripMenuItem();
                mDataFolderToolStripMenuItem = new ToolStripMenuItem();
                mVoiceFolderToolStripMenuItem = new ToolStripMenuItem();
                mDeleteDebugFoldersToolStripMenuItem = new ToolStripMenuItem();
                mExitToolStripMenuItem = new ToolStripMenuItem();

                InitializeMenuItems();
            }

            private void InitializeMenuItems()
            {
                fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { mDataFolderToolStripMenuItem, mVoiceFolderToolStripMenuItem, mExitToolStripMenuItem });
                if (CrewChief.Debug.UseDebugFilePaths)
                {
                    fileToolStripMenuItem.DropDownItems.Add(mDeleteDebugFoldersToolStripMenuItem);
                }
                fileToolStripMenuItem.ShortcutKeyDisplayString = "";
                fileToolStripMenuItem.Text = Configuration.getUIString("file_menu");
                mExitToolStripMenuItem.Text = Configuration.getUIString("exit_menu_item");

                mDataFolderToolStripMenuItem.Text = Configuration.getUIString("Open data files folder");
                mDataFolderToolStripMenuItem.Click += mDataFolderToolStripMenuItem_Click;

                mVoiceFolderToolStripMenuItem.Text = Configuration.getUIString("Open voice files folder");
                mVoiceFolderToolStripMenuItem.Click += mVoiceFolderToolStripMenuItem_Click;

                mDeleteDebugFoldersToolStripMenuItem.Text = Configuration.getUIString("(Debug builds only) Nuke all data folders");
                mDeleteDebugFoldersToolStripMenuItem.Click += mDeleteDebugFoldersToolStripMenuItem_Click;

                mExitToolStripMenuItem.ShortcutKeyDisplayString = "Alt+F4";
                mExitToolStripMenuItem.ShortcutKeys = (Keys.Alt | Keys.F4);
                mExitToolStripMenuItem.Text = Configuration.getUIString("Exit");
                mExitToolStripMenuItem.Click += mExitToolStripMenuItem_Click;
            }

            public ToolStripMenuItem GetMenuItem()
            {
                return fileToolStripMenuItem;
            }

            private void mDataFolderToolStripMenuItem_Click(object sender, EventArgs e)
            {
                Process.Start(DataFiles.BaseFolder);
            }

            private void mVoiceFolderToolStripMenuItem_Click(object sender, EventArgs e)
            {
                Process.Start(AudioPlayer.soundFilesPath);
            }

            private void mDeleteDebugFoldersToolStripMenuItem_Click(object sender, EventArgs e)
            {
                DataFiles.DeleteDebugFolders();
                CrewChief.gameDefinition.commandLineName = ""; // Equivalent to a new install
                mainWindow.DoRestart( /*msg overwritten in debug mode*/"", "Debug mode data delete", saveUserSettings: false);
            }

            private void mExitToolStripMenuItem_Click(object sender, EventArgs e)
            {
                mainWindow.CloseCC();
            }
        }

        public class ConsoleMenu
        {
            private ToolStripMenuItem consoleToolStripMenuItem;
            private ContextMenuStrip consoleContextMenuStrip;
            private readonly ToolStripMenuItem mcCopyConsoleToolStripMenuItem;
            private readonly ToolStripMenuItem mcClearConsoleToolStripMenuItem;
            private readonly ToolStripMenuItem mSaveConsoleToolStripMenuItem;
            private readonly ToolStripMenuItem mSaveConsoleAsToolStripMenuItem;
            private readonly ToolStripMenuItem mCopySelectedConsoleTextToolStripMenuItem;
            private readonly ToolStripMenuItem mCopyCrewChiefSettingsToolStripMenuItem;
            private readonly IMainWindow mainWindow;

            public ConsoleMenu(IMainWindow mw)
            {
                mainWindow = mw;
                consoleToolStripMenuItem = new ToolStripMenuItem();
                consoleContextMenuStrip = new ContextMenuStrip();
                mcCopyConsoleToolStripMenuItem = new ToolStripMenuItem();
                mcClearConsoleToolStripMenuItem = new ToolStripMenuItem();
                mSaveConsoleToolStripMenuItem = new ToolStripMenuItem();
                mSaveConsoleAsToolStripMenuItem = new ToolStripMenuItem();
                mCopySelectedConsoleTextToolStripMenuItem = new ToolStripMenuItem();
                mCopyCrewChiefSettingsToolStripMenuItem = new ToolStripMenuItem();

                InitializeMenuItems();
            }

            private void InitializeMenuItems()
            {
                consoleContextMenuStrip.Opening += consoleContextMenuStrip_Opening;

                consoleToolStripMenuItem.ShortcutKeyDisplayString = "";
                consoleToolStripMenuItem.Text = Configuration.getUIString("console_menu");

                consoleToolStripMenuItem.Click += ConsoleToolStripMenuItem_Click;

                mcCopyConsoleToolStripMenuItem.Text = Configuration.getUIString("copy_console_text");
                mcCopyConsoleToolStripMenuItem.Click += cCopyConsoleToolStripMenuItem_Click;

                mcClearConsoleToolStripMenuItem.Text = Configuration.getUIString("clear_console");
                mcClearConsoleToolStripMenuItem.Click += clearConsole_Click;

                mSaveConsoleToolStripMenuItem.Text = Configuration.getUIString("save_console_output");
                mSaveConsoleToolStripMenuItem.Click += saveConsoleOutputText;

                mSaveConsoleAsToolStripMenuItem.Text = Configuration.getUIString("save_console_output_as");
                mSaveConsoleAsToolStripMenuItem.Click += saveConsoleOutputTextAs;

                mCopySelectedConsoleTextToolStripMenuItem.Text = Configuration.getUIString("copy_selected_text");
                mCopySelectedConsoleTextToolStripMenuItem.Click += saveSelectedConsoleText_Click;

                mCopyCrewChiefSettingsToolStripMenuItem.Text = Configuration.getUIString("copy_crew_chief_settings");
                mCopyCrewChiefSettingsToolStripMenuItem.Click += copyCrewChiefSettings_Click;

                consoleToolStripMenuItem.DropDownItems.Add(new ToolStripMenuItem(mcCopyConsoleToolStripMenuItem.Text, null, cCopyConsoleToolStripMenuItem_Click));
                consoleToolStripMenuItem.DropDownItems.Add(new ToolStripMenuItem(mCopySelectedConsoleTextToolStripMenuItem.Text, null, saveSelectedConsoleText_Click));
                consoleToolStripMenuItem.DropDownItems.Add(new ToolStripMenuItem(mcClearConsoleToolStripMenuItem.Text, null, clearConsole_Click));
                consoleToolStripMenuItem.DropDownItems.Add(new ToolStripMenuItem(mSaveConsoleToolStripMenuItem.Text, null, saveConsoleOutputText));
                consoleToolStripMenuItem.DropDownItems.Add(new ToolStripMenuItem(mSaveConsoleAsToolStripMenuItem.Text, null, saveConsoleOutputTextAs));
                consoleToolStripMenuItem.DropDownItems.Add(new ToolStripMenuItem(mCopyCrewChiefSettingsToolStripMenuItem.Text, null, copyCrewChiefSettings_Click));
                consoleToolStripMenuItem.DropDownItems[4].ToolTipText = Configuration.getUIString("copy_crew_chief_settings_tooltip");

                consoleContextMenuStrip.Items.Add(new ToolStripMenuItem(mcCopyConsoleToolStripMenuItem.Text, null, cCopyConsoleToolStripMenuItem_Click));
                consoleContextMenuStrip.Items.Add(new ToolStripMenuItem(mCopySelectedConsoleTextToolStripMenuItem.Text, null, saveSelectedConsoleText_Click));
                consoleContextMenuStrip.Items.Add(new ToolStripMenuItem(mcClearConsoleToolStripMenuItem.Text, null, clearConsole_Click));
                consoleContextMenuStrip.Items.Add(new ToolStripMenuItem(mSaveConsoleToolStripMenuItem.Text, null, saveConsoleOutputText));
                consoleContextMenuStrip.Items.Add(new ToolStripMenuItem(mSaveConsoleAsToolStripMenuItem.Text, null, saveConsoleOutputTextAs));
                consoleContextMenuStrip.Items.Add(new ToolStripMenuItem(mCopyCrewChiefSettingsToolStripMenuItem.Text, null, copyCrewChiefSettings_Click));
                consoleContextMenuStrip.Items[4].ToolTipText = Configuration.getUIString("copy_crew_chief_settings_tooltip");
            }

            public ToolStripMenuItem GetMenuItem()
            {
                return consoleToolStripMenuItem;
            }

            public ContextMenuStrip GetContextMenuStrip()
            {
                return consoleContextMenuStrip;
            }

            private void ConsoleToolStripMenuLoad()
            {
                void SetEnabledRange(List<int> indices, bool enabled)
                {
                    foreach (int i in indices)
                    {
                        consoleToolStripMenuItem.DropDownItems[i].Enabled = enabled;
                        consoleContextMenuStrip.Items[i].Enabled = enabled;
                    }
                }

                SetEnabledRange(new List<int>() { 0, 1, 2, 3, 4 }, false);

                if (!string.IsNullOrWhiteSpace(mainWindow.ConsoleTextBox.SelectedText))
                {
                    SetEnabledRange(new List<int>() { 1 }, true);
                }
                if (!string.IsNullOrWhiteSpace(mainWindow.ConsoleTextBox.Text))
                {
                    SetEnabledRange(new List<int>() { 0, 2, 3 }, true);
                }
                SetEnabledRange(new List<int>() { 4 }, true);
            }

            private void ConsoleToolStripMenuItem_Click(object sender, EventArgs e)
            {
                ConsoleToolStripMenuLoad();
            }

            /// <summary>
            /// Console context menu entries offered according to Console contents
            /// </summary>
            private void consoleContextMenuStrip_Opening(object sender, CancelEventArgs e)
            {
                ConsoleToolStripMenuLoad();
                // Always show the context menu
                e.Cancel = false;
            }

            private void cCopyConsoleToolStripMenuItem_Click(object sender, EventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(mainWindow.ConsoleTextBox.Text))
                {
                    Clipboard.SetText(mainWindow.PrefixLogfile() + mainWindow.ConsoleTextBox.Text);
                }
            }

            private void clearConsole_Click(object sender, EventArgs e)
            {
                mainWindow.ClearConsole();
            }

            private void saveConsoleOutputText(object sender, EventArgs e)
            {
                mainWindow.SaveConsoleOutputText(false);
            }

            private void saveConsoleOutputTextAs(object sender, EventArgs e)
            {
                mainWindow.SaveConsoleOutputText(true);
            }

            private void saveSelectedConsoleText_Click(object sender, EventArgs e)
            {
                if (!string.IsNullOrWhiteSpace(mainWindow.ConsoleTextBox.SelectedText))
                {
                    Clipboard.SetText(mainWindow.PrefixLogfile() + mainWindow.ConsoleTextBox.SelectedText);
                }
            }

            private void copyCrewChiefSettings_Click(object sender, EventArgs e)
            {
                Clipboard.SetText(mainWindow.PrefixLogfile());
            }
        }

        public class ProblemsMenu
        {
            private readonly ToolStripMenuItem traceToolStripMenuItem;
            private readonly ToolStripMenuItem mLogNowToolStripMenuItem;
            private readonly ToolStripMenuItem mLogSavedToolStripMenuItem;
            private readonly ToolStripMenuItem mTraceCheckboxToolStripMenuItem;
            private readonly ToolStripMenuItem mTraceNowToolStripMenuItem;
            private readonly ToolStripMenuItem mTraceSavedToolStripMenuItem;
            private readonly ToolStripMenuItem mTracePlaybackToolStripMenuItem;
            private readonly ToolStripMenuItem mLogDebugToolStripMenuItem;
            private readonly ToolStripMenuItem mLogVerboseToolStripMenuItem;
            private readonly ToolStripMenuItem mLogTyresToolStripMenuItem;
            private readonly ToolStripMenuItem mLogBrakesToolStripMenuItem;
            private readonly ToolStripMenuItem mLogFuelToolStripMenuItem;
            private readonly ToolStripMenuItem mGameEventDebugToolStripMenuItem;
            private readonly IMainWindow mainWindow;

            public ProblemsMenu(IMainWindow mw)
            {
                mLogNowToolStripMenuItem = new ToolStripMenuItem();
                mLogSavedToolStripMenuItem = new ToolStripMenuItem();
                mTracePlaybackToolStripMenuItem = new ToolStripMenuItem();
                mTraceCheckboxToolStripMenuItem = new ToolStripMenuItem();
                mTraceNowToolStripMenuItem = new ToolStripMenuItem();
                mTraceSavedToolStripMenuItem = new ToolStripMenuItem();
                mLogDebugToolStripMenuItem = new ToolStripMenuItem();
                mLogVerboseToolStripMenuItem = new ToolStripMenuItem();
                mLogTyresToolStripMenuItem = new ToolStripMenuItem();
                mLogBrakesToolStripMenuItem = new ToolStripMenuItem();
                mLogFuelToolStripMenuItem = new ToolStripMenuItem();
                mGameEventDebugToolStripMenuItem = new ToolStripMenuItem();
                mainWindow = mw;
                traceToolStripMenuItem = new ToolStripMenuItem();
                InitializeMenuItems();
            }

            private void InitializeMenuItems()
            {
                traceToolStripMenuItem.Text = Configuration.getUIString("problems_menu");
                traceToolStripMenuItem.ForeColor = Color.Red;
                traceToolStripMenuItem.Font = new Font(traceToolStripMenuItem.Font, FontStyle.Bold);

                traceToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    mLogDebugToolStripMenuItem,
                    mLogVerboseToolStripMenuItem,
                    mLogTyresToolStripMenuItem,
                    mLogBrakesToolStripMenuItem,
                    mLogFuelToolStripMenuItem,
                    mLogNowToolStripMenuItem,
                    mLogSavedToolStripMenuItem
                });

                mLogDebugToolStripMenuItem.Text = Configuration.getUIString("log_type_debug");
                mLogDebugToolStripMenuItem.Font = new Font(mLogDebugToolStripMenuItem.Font, FontStyle.Regular);
                mLogDebugToolStripMenuItem.CheckOnClick = true;
                mLogDebugToolStripMenuItem.CheckedChanged += LogDebugToolStripMenuItem_CheckedChanged;
                mLogDebugToolStripMenuItem.Checked = Log.logDebug;
                mLogDebugToolStripMenuItem.ToolTipText = Configuration.getUIString("log_type_debug_help");

                mLogVerboseToolStripMenuItem.Text = Configuration.getUIString("log_type_verbose");
                mLogVerboseToolStripMenuItem.Font = new Font(mLogVerboseToolStripMenuItem.Font, FontStyle.Regular);
                mLogVerboseToolStripMenuItem.CheckOnClick = true;
                mLogVerboseToolStripMenuItem.CheckedChanged += LogVerboseToolStripMenuItem_CheckedChanged;
                mLogVerboseToolStripMenuItem.Checked = Log.logVerbose;
                mLogVerboseToolStripMenuItem.ToolTipText = Configuration.getUIString("log_type_verbose_help");

                mLogTyresToolStripMenuItem.Text = Configuration.getUIString("log_type_tyres");
                mLogTyresToolStripMenuItem.Font = new Font(mLogTyresToolStripMenuItem.Font, FontStyle.Regular);
                mLogTyresToolStripMenuItem.CheckOnClick = true;
                mLogTyresToolStripMenuItem.CheckedChanged += LogTyresToolStripMenuItem_CheckedChanged;
                mLogTyresToolStripMenuItem.Checked = Log.logTyres;
                mLogTyresToolStripMenuItem.ToolTipText = Configuration.getUIString("log_type_tyres_help");

                mLogBrakesToolStripMenuItem.Text = Configuration.getUIString("log_type_brakes");
                mLogBrakesToolStripMenuItem.Font = new Font(mLogBrakesToolStripMenuItem.Font, FontStyle.Regular);
                mLogBrakesToolStripMenuItem.CheckOnClick = true;
                mLogBrakesToolStripMenuItem.CheckedChanged += LogBrakesToolStripMenuItem_CheckedChanged;
                mLogBrakesToolStripMenuItem.Checked = Log.logBrakes;
                mLogBrakesToolStripMenuItem.ToolTipText = Configuration.getUIString("log_type_brakes_help");

                mLogFuelToolStripMenuItem.Text = Configuration.getUIString("log_type_fuel");
                mLogFuelToolStripMenuItem.Font = new Font(mLogFuelToolStripMenuItem.Font, FontStyle.Regular);
                mLogFuelToolStripMenuItem.CheckOnClick = true;
                mLogFuelToolStripMenuItem.CheckedChanged += LogFuelToolStripMenuItem_CheckedChanged;
                mLogFuelToolStripMenuItem.Checked = Log.logFuel;
                mLogFuelToolStripMenuItem.ToolTipText = Configuration.getUIString("log_type_fuel_help");

                mLogNowToolStripMenuItem.Text = Configuration.getUIString("log_now");
                mLogNowToolStripMenuItem.Font = new Font(mLogNowToolStripMenuItem.Font, FontStyle.Regular);
                mLogNowToolStripMenuItem.Click += logNow_Click;
                mLogNowToolStripMenuItem.ToolTipText = Configuration.getUIString("log_now_tooltip");

                mLogSavedToolStripMenuItem.Text = Configuration.getUIString("log_saved");
                mLogSavedToolStripMenuItem.Font = new Font(mLogSavedToolStripMenuItem.Font, FontStyle.Regular);
                mLogSavedToolStripMenuItem.Click += logSaved_Click;
                mLogSavedToolStripMenuItem.ToolTipText = Configuration.getUIString("log_saved_tooltip");

                mTraceCheckboxToolStripMenuItem.Text = Configuration.getUIString("record_trace");
                mTraceCheckboxToolStripMenuItem.Font = new Font(mTraceCheckboxToolStripMenuItem.Font, FontStyle.Regular);
                mTraceCheckboxToolStripMenuItem.Click += trace_Click;
                mTraceCheckboxToolStripMenuItem.ToolTipText = Configuration.getUIString("record_trace_tooltip");

                mTraceNowToolStripMenuItem.Text = Configuration.getUIString("trace_now");
                mTraceNowToolStripMenuItem.Font = new Font(mTraceNowToolStripMenuItem.Font, FontStyle.Regular);
                mTraceNowToolStripMenuItem.Click += TraceNowClick;
                mTraceNowToolStripMenuItem.ToolTipText = Configuration.getUIString("trace_now_tooltip");

                mTraceSavedToolStripMenuItem.Text = Configuration.getUIString("trace_saved");
                mTraceSavedToolStripMenuItem.Font = new Font(mTraceSavedToolStripMenuItem.Font, FontStyle.Regular);
                mTraceSavedToolStripMenuItem.Click += TraceSavedClick;
                mTraceSavedToolStripMenuItem.ToolTipText = Configuration.getUIString("trace_saved_tooltip");

                mTracePlaybackToolStripMenuItem.Text = Configuration.getUIString("playback_trace");
                mTracePlaybackToolStripMenuItem.Font = new Font(mTracePlaybackToolStripMenuItem.Font, FontStyle.Regular);
                mTracePlaybackToolStripMenuItem.Click += tracePlayback_Click;
                mTracePlaybackToolStripMenuItem.ToolTipText = Configuration.getUIString("playback_trace_tooltip");

                mGameEventDebugToolStripMenuItem.Text = "(Debug builds only) Game Event Debug";
                mGameEventDebugToolStripMenuItem.Font = new Font(mGameEventDebugToolStripMenuItem.Font, FontStyle.Regular);
                mGameEventDebugToolStripMenuItem.Click += GameEventDebugToolStripMenuItem_Click;
            }

            private void LogDebugToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
            {
                Log.logDebug = mLogDebugToolStripMenuItem.Checked;
            }

            private void LogTyresToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
            {
                Log.logTyres = mLogTyresToolStripMenuItem.Checked;
            }

            private void LogBrakesToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
            {
                Log.logBrakes = mLogBrakesToolStripMenuItem.Checked;
            }

            private void LogVerboseToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
            {
                Log.logVerbose = mLogVerboseToolStripMenuItem.Checked;
            }

            private void LogFuelToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
            {
                Log.logFuel = mLogFuelToolStripMenuItem.Checked;
            }

            public void tracelogMenu()
            {
                traceToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                {
                    mTraceCheckboxToolStripMenuItem,
                    mTraceNowToolStripMenuItem,
                    mTraceSavedToolStripMenuItem,
                });
            }

            public void tracePlaybackMenu()
            {
                traceToolStripMenuItem.DropDownItems.Add(mTracePlaybackToolStripMenuItem);
            }

            public void gameEventDebugMenu()
            {
                traceToolStripMenuItem.DropDownItems.Add(mGameEventDebugToolStripMenuItem);
            }

            public ToolStripMenuItem GetMenuItem()
            {
                return traceToolStripMenuItem;
            }

            /// <summary>
            /// Save the console output to a file and upload it to file.io
            /// </summary>
            private void logNow_Click(object sender, EventArgs e)
            {
                mainWindow.LogNowSend();
            }

            /// <summary>
            /// Browse for a saved console log and upload it to file.io
            /// </summary>
            private void logSaved_Click(object sender, EventArgs e)
            {
                mainWindow.LogSavedSend();
            }

            private void trace_Click(object sender, EventArgs e)
            {
                mTraceCheckboxToolStripMenuItem.Checked = !mTraceCheckboxToolStripMenuItem.Checked;
                mainWindow.RecordSession.Checked = mTraceCheckboxToolStripMenuItem.Checked; // wire it back to existing process
            }

            private void TraceNowClick(object sender, EventArgs e)
            {
                mainWindow.TraceNowSend();
            }

            private void TraceSavedClick(object sender, EventArgs e)
            {
                mainWindow.TraceSavedSend();
            }

            public static GameEventDebugForm gameEventDebugForm;
            private void GameEventDebugToolStripMenuItem_Click(object sender, EventArgs e)
            {
                if (gameEventDebugForm == null || gameEventDebugForm.IsDisposed)
                {
                    gameEventDebugForm = new GameEventDebugForm();
                    gameEventDebugForm.Show(MainWindow.instance);
                }
                else
                {
                    gameEventDebugForm.BringToFront();
                }
            }

            private void tracePlayback_Click(object sender, EventArgs e)
            {
                mainWindow.TracePlayback();
            }
        }

        private void TopicBrakesToolStripMenuItemCheckedChanged(object sender, EventArgs e)
        {
            if (topicBrakesToolStripMenuItem.Checked)
            {
                if (brakesWindow == null || brakesWindow.IsDisposed)
                {
                    brakesWindow = new TopicWindowBrakes();
                    brakesWindow.FormClosed += (s, args) => topicBrakesToolStripMenuItem.Checked = false;
                }
                brakesWindow.Show();
                brakesWindow.BringToFront();
            }
            else
            {
                if (brakesWindow != null && !brakesWindow.IsDisposed)
                {
                    brakesWindow.Close();
                }
            }
        }

        private void TopicTyresToolStripMenuItemCheckedChanged(object sender, EventArgs e)
        {
            if (topicTyresToolStripMenuItem.Checked)
            {
                if (tyresWindow == null || tyresWindow.IsDisposed)
                {
                    tyresWindow = new TopicWindowTyres();
                    tyresWindow.FormClosed += (s, args) => topicTyresToolStripMenuItem.Checked = false;
                }
                tyresWindow.Show();
                tyresWindow.BringToFront();
            }
            else
            {
                if (tyresWindow != null && !tyresWindow.IsDisposed)
                {
                    tyresWindow.Close();
                }
            }
        }

        private void TopicFuelToolStripMenuItemCheckedChanged(object sender, EventArgs e)
        {
            if (topicFuelToolStripMenuItem.Checked)
            {
                if (fuelWindow == null || fuelWindow.IsDisposed)
                {
                    fuelWindow = new TopicWindowFuel();
                    fuelWindow.FormClosed += (s, args) => topicFuelToolStripMenuItem.Checked = false;
                }
                fuelWindow.Show();
                fuelWindow.BringToFront();
            }
            else
            {
                if (fuelWindow != null && !fuelWindow.IsDisposed)
                {
                    fuelWindow.Close();
                }
            }
        }
    }

    /// <summary>
    /// Interface to allow unit test to clone a main window and add the menu strip
    /// </summary>
    public interface IMainWindow
    {
        RichTextBox ConsoleTextBox { get; }
        CheckBox RecordSession { get; set; }
        void ClearConsole();
        void CloseCC();
        bool DoRestart(string warningMessage, string warningTitle, bool removeSkipUpdates = false, bool mandatory = false, bool saveUserSettings = false);
        void OpponentNames();
        string PrefixLogfile(string path = null);
        string SaveConsoleOutputText(bool saveAs);
        void ShowHelp();
        void SpeechWizard();
        void LogNowSend();
        void LogSavedSend();
        void TracePlayback();
        void TraceNowSend();
        void TraceSavedSend();
    }
}
