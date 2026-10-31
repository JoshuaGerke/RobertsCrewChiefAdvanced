//#define EXCEPTION_TEST
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using AutoUpdaterDotNET;
using System.Net;
using CrewChiefV4.Audio;
using System.Diagnostics;
using CrewChiefV4.commands;
using CrewChiefV4.GameState;
using CrewChiefV4.Events;
using CrewChiefV4.Overlay;
using Valve.VR;
using CrewChiefV4.ScreenCapture;
using CrewChiefV4.VirtualReality;
using CrewChiefV4.UserInterface;
using CrewChiefV4.UserInterface.Models;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace CrewChiefV4
{
    public partial class MainWindow : Form, IMainWindow
    {
        #region Interface items
        public CheckBox RecordSession
        {
            get => recordSession;
            set => recordSession = value;
        }
        public TextBox FilenameTextbox {
            get => filenameTextbox;
            set => filenameTextbox = value;
        } 
        public void CloseCC()
        {
            this.Close();
        }

        public Boolean DoRestart(String warningMessage, String warningTitle, Boolean removeSkipUpdates = false, Boolean mandatory = false, Boolean saveUserSettings = false)
        {
            return doRestart(warningMessage,warningTitle,removeSkipUpdates, mandatory,saveUserSettings);
        }
        public void OpponentNames()
        {
            var win = new OpponentNames_V(this);
            win.ShowDialog(this);
        }
        public void ShowHelp()
        {
            var form = new HelpWindow(this, "index");
            form.ShowDialog(this);
        }
        public void SpeechWizard()
        {
            speechWizard_V = new SpeechWizard_V(IsAppRunning);
            speechWizard_V.ShowDialog(this);
        }
        public void TracePlayback()
        {
            var traceWindow = new PlaybackTraceWindow(filenameTextbox.Text);
            traceWindow.Show();
        }
        public void TraceNowSend()
        {
            if (!String.IsNullOrEmpty(GameDataReader.dumpedFilePath))
            {
                var logTraceDialog = new SendLogTraceDialog();
                logTraceDialog.Log_TraceSend(GameDataReader.dumpedFilePath);
            }
            else
            {
                MessageBox.Show($"'No trace recorded for this session", "No current trace", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        public void TraceSavedSend()
        {
            var logTraceDialog = new SendLogTraceDialog();
            logTraceDialog.TraceFileChoose();
        }
        #endregion Interface items


        private const int PREFERRED_X_SIZE = 1170;
        private const int PREFERRED_Y_SIZE = 730;
        private static int X_SIZE = Math.Min(PREFERRED_X_SIZE, Screen.PrimaryScreen.WorkingArea.Width);
        private static int Y_SIZE = Math.Min(PREFERRED_Y_SIZE, Screen.PrimaryScreen.WorkingArea.Height);
        private static bool NEED_H_SCROLL = X_SIZE < PREFERRED_X_SIZE;
        private static bool NEED_V_SCROLL = Y_SIZE < PREFERRED_Y_SIZE;

        private Boolean newSoundPackAvailable = false;
        private Boolean newDriverNamesAvailable = false;
        private Boolean newPersonalisationsAvailable = false;

        private StartButton startButton;

        public struct ControllerUiEntry
        {
            public string uiText;
            public bool isConnected;

            public ControllerUiEntry(string uiText, bool isConnected)
            {
                this.uiText = uiText;
                this.isConnected = isConnected;
            }

            public override string ToString()
            {
                return uiText;
            }
        }

        public class CoDriverStyleEntry
        {
            public string uiText = "";
            public CoDriver.CornerCallStyle style = CoDriver.CornerCallStyle.UNKNOWN;

            public override string ToString()
            {
                return uiText;
            }
        }

        // Shared with worker thread and Properties UI.  This should be disposed after root threads stopped, in GlobalResources.Dispose.
        public ControllerConfiguration controllerConfiguration;

        public CrewChief crewChief;

        private Boolean isAssigningButton = false;

        private Boolean runListenForChannelOpenThread = false;

        private Boolean runListenForButtonPressesThread = false;

        private TimeSpan buttonCheckInterval = TimeSpan.FromMilliseconds(50);

        public static VoiceOptionEnum voiceOption;

        #region Updates
        // Sources of updates:
        // 1) thecrewchief.org for installer and auto_update_data_files 
        // 2) VPS for the existing, out of date installer and auto_update_data_files
        // 3) GitLab as a backup for the installer and auto_update_data_files 
        // auto_update_data_files specify the URLs for each soundpack
        // 4) thecrewchief.org for new and existing soundpacks
        // 5) VPS for existing soundpacks
        // 6) GitLab as a backup for new soundpacks(if they're < 100MB) 


        // the new update stuff all hosted on the CrewChief website
        // this is the physical file:
        // private static String autoUpdateXMLURL1 = "https://thecrewchief.org/downloads/auto_update_data_primary.xml";
        // this is the file accessed via the PHP download script:
        private static String autoUpdateXMLURLTheCrewChiefOrg = "https://thecrewchief.org/downloads.php?do=downloadxml";
        //Test version private static String autoUpdateXMLURLTheCrewChiefOrg = "https://thecrewchief.org/downloads/auto_update_data4.8.3.99.xml";

        private static String additionalDataURL = "https://thecrewchief.org/downloads.php?do=getadditionaldata";

        // secondary on our VPS:
        private static String autoUpdateXMLURLVPS = "http://167.235.144.28/auto_update_data.xml";
        // finally on GitLab:
        private static String autoUpdateXMLURL_GitLab = "https://gitlab.com/mr_belowski/CrewChiefV4/-/raw/gitlab-update-files/auto_update_data_files/secondary/auto_update_data.xml?ref_type=heads";

        // a copy on Cloudfront, not used at the moment due to AWS' insane egress prices:
        //private static String autoUpdateXMLURL2 = "https://d1o4ya81faadx8.cloudfront.net/auto_update_data.xml";

        private readonly Boolean preferAlternativeDownloadSite = UserSettings.GetUserSettings().getBoolean("prefer_alternative_download_site");
        #endregion Updates

        private readonly Boolean allowCompositePersonalisations = UserSettings.GetUserSettings().getBoolean("allow_composite_personalisations");
        private readonly Boolean minimizeToTray = UserSettings.GetUserSettings().getBoolean("minimize_to_tray");
        private readonly Boolean rejectMessagesWhenTalking = UserSettings.GetUserSettings().getBoolean("reject_message_when_talking");
        public static Boolean forceMinWindowSize;
        private readonly int holdButtonPollFrequency = UserSettings.GetUserSettings().getInt("hold_button_poll_frequency");

        // 2 SRE delays here, sreWaitTime is the time we allow the SRE to get its shit together after invoking
        // recognizeAsync, and another delay between releasing the button and calling recognizeAsync
        private readonly int sreWaitTime = UserSettings.GetUserSettings().getInt("sre_wait_time");

        public static float currentMessageVolume = -1;
        private NotifyIcon notificationTrayIcon;
        private ToolStripItem contextMenuStartItem;
        private ToolStripItem contextMenuStopItem;
        private ToolStripMenuItem contextMenuGamesMenu;
        private ToolStripItem contextMenuPreferencesItem;

        // instance
        public static MainWindow instance = null;

        // Do not .Invoke under this lock.  Either .Post, or .BeginInvoke only.
        public static object instanceLock = new object();

        // True, while we are in a constructor.
        private bool constructingWindow = false;

        private Thread youWotThread = null;
        private Thread assignButtonThread = null;
        private Thread loadSREGrammarThread = null;
        public bool formClosed = false;

        public static bool soundTestMode = false;
        public static bool shouldSaveTrace = false;

        private AutoResetEvent controllerRescanThreadWakeUpEvent = new AutoResetEvent(false);
        private bool controllerRescanThreadRunning = false;

        // This lock must be held while we are updating controller devices or updating assignments.
        private object controllerWriteLock = new object();

        public static bool disableControllerReacquire = false;

        private static Boolean isMuted = false;
        private float messageVolumeToRestore = -1;

        private const int WM_DEVICECHANGE = 0x219;
        private const int DBT_DEVNODES_CHANGED = 0x0007;

        private bool internalMessageAudioRefresh = false;
        private bool internalBackgroundAudioRefresh = false;
        private bool internalSpeechRecognitionRefresh = false;
        public bool closedByCmdLineCommand = false;

        public CrewChiefOverlayWindow overlay = null;
        public SubtitleOverlay subtitleOverlay = null;

        // Allow trace playback on Release build.
        internal static bool playingBackTrace = false;

        public VROverlaySettings vrOverlayForm = null;
        VROverlayConfiguration _VRConfig = VROverlayConfiguration.FromFile();

        private DeviceManager deviceManager = null;
        private Direct3D11CaptureSource captureSource = null;
        private Thread vrUpdateThread = null;

        // Used to set the font size in the menu which otherwise varies with DPI
        public readonly Font exemplarFont;

        private readonly Regex autoStartRegex = new Regex(@"\.exe$", RegexOptions.IgnoreCase);

        public void killChief()
        {
            crewChief.stop();
        }

        protected override void WndProc(ref Message m)
        {
            if (!MainWindow.disableControllerReacquire && this.controllerRescanThreadRunning)
            {
                if (m.Msg == WM_DEVICECHANGE)
                {
                    if ((int)m.WParam == DBT_DEVNODES_CHANGED)
                    {
                        if (!this.controllerConfiguration.scanInProgress)
                        {
                            this.scanControllers.Enabled = false;
#if DEBUG
                            var watch = System.Diagnostics.Stopwatch.StartNew();
#endif
                            this.reacquireControllerList();
#if DEBUG
                            watch.Stop();
                            Debug.WriteLine("Controller re-acquisition took: " + watch.ElapsedTicks * 1000 / System.Diagnostics.Stopwatch.Frequency + "ms to shutdown");
#endif
                            if (!this.IsAppRunning)
                            {
                                this.scanControllers.Enabled = true;
                            }
                        }
                    }
                }
            }
            base.WndProc(ref m);
        }
        private Size setMainWindowSize()
        {
            Screen currentScreen = Screen.FromControl(this);
            Size screenSize = currentScreen.Bounds.Size;
            Size size = new Size(Math.Min(Properties.Settings.Default.main_window_size.Width, screenSize.Width),
                                 Math.Min(Properties.Settings.Default.main_window_size.Height, screenSize.Height));
            size = checkMainWindowSize(size);

            return size;
        }

        private static Size checkMainWindowSize(Size size)
        {
            if (forceMinWindowSize)
            {
                size.Width = Math.Max(size.Width, X_SIZE);
                size.Height = Math.Max(size.Height, Y_SIZE);
            }

            return size;
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            // Restore window position and size.
            try
            {
                Size size = setMainWindowSize();
                Rectangle windowRect = new Rectangle(Properties.Settings.Default.main_window_position.X,
                                                     Properties.Settings.Default.main_window_position.Y,
                                                     size.Width,
                                                     size.Height);
                if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(windowRect)))
                {
                    StartPosition = FormStartPosition.Manual;
                    DesktopBounds = windowRect;
                    WindowState = FormWindowState.Normal;
                }
            }
            catch (Exception)
            {
                // ignore
            }

            // Set up console update thread.  We need this because we call Console.WriteLine from random threads.
            ConsoleUpdateThreadStart();

            CommandManager.StartCommandListeners();

            ThreadStart ts;
            // Set up Controller Rescan thread
            ts = controllerRescanThreadWorker;
            var controllerRescanThread = new Thread(ts);
            controllerRescanThread.Name = "MainWindow.controllerRescanThreadWorker";
            controllerRescanThreadRunning = true;
            ThreadManager.RegisterResourceThread(controllerRescanThread);
            controllerRescanThread.Start();

            if (UserSettings.GetUserSettings().getBoolean("enable_vr_overlay_windows") &&
                OpenVR.IsRuntimeInstalled())
            {
                ts = vrOverlaysUpdateThreadWorker;
                vrUpdateThread = new Thread(ts);
                vrUpdateThread.Name = "MainWindow.vrOverlaysUpdateThreadWorker";
                VROverlayController.vrUpdateThreadRunning = true;

                if (!UserSettings.GetUserSettings().getBoolean("vr_overlays_enabled_on_startup"))
                {
                    // Start suspended.
                    VROverlayController.suspendVROverlayRenderThread();
                }

                ThreadManager.RegisterResourceThread(vrUpdateThread);
                vrUpdateThread.Start();
            }

            // Run immediately if requested.
            // Note that it is not safe to run immidiately from the constructor, becasue form handle
            // is created on a message pump, at undefined moment, which prevents Invoke from
            // working while constructor is running.
            Debug.Assert(this.IsHandleCreated);
            this.controllersList.DrawItem += this.ControllersList_DrawItem;
            this.controllersList.MeasureItem += this.ControllersList_MeasureItem;
            this.controllersList.DrawMode = DrawMode.OwnerDrawVariable;
            this.reacquireControllerList();

            try
            {
            if (UserSettings.GetUserSettings().getBoolean("run_immediately") &&
                !UserSettings.GetUserSettings().getBoolean("enable_auto_detect") &&
                GameDefinition.getGameDefinitionForFriendlyName(gameDefinitionList.Text) != null)
            {
                doStartAppStuff();

                // Will wait for threads to start, possible file load and enable the button.
                ThreadManager.DoWatchStartup(crewChief);
            }
            }
            catch (Exception)
            {
                Log.Warning($"gameDefinitionList.SelectedItem {SourceInfoHelpers.SourceFile()}:{SourceInfoHelpers.SourceLineNumber() - 10}");
            }

            this.Size = setMainWindowSize();
            if (NEED_H_SCROLL || NEED_V_SCROLL)
            {
                this.Size = new System.Drawing.Size(X_SIZE, Y_SIZE);
            }

            #region Updates
            // do the auto updating stuff in a separate Threads
            if (!CrewChief.Debug.RunningUnderDebugger)
            {
                string commentSeparator = "//";
                var updateAdditionalData = new Thread(() =>
                {
                    try
                    {
                        string base64EncodedData = new WebClient().DownloadString(additionalDataURL);
                        string decodedData = Base64Decode(base64EncodedData);
                        string[] splitData = decodedData.Split(',');
                        foreach (var nameWithComment in splitData)
                        {
                            string[] nameWithCommentSplitData = nameWithComment.Split(new String[] { commentSeparator }, StringSplitOptions.None);
                            if (nameWithCommentSplitData.Length > 0)
                            {
                                string s = nameWithCommentSplitData[0].Trim();
                                if (!AdditionalDataProvider.additionalData.Contains(s))
                                {
                                    string cleanedData = s.Trim('\r', '\n');
                                    if (cleanedData.Length > 0)
                                    {
                                        AdditionalDataProvider.additionalData.Add(cleanedData);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // don't really care
                    }
                });
                updateAdditionalData.Name = "MainWindow.updateAdditionalData";
                ThreadManager.RegisterResourceThread(updateAdditionalData);
                updateAdditionalData.Start();
            }
            // Some update test code - uncomment this to allow the app to process an update .zip file in the root of the sound pack
            /*
            ZipFile.ExtractToDirectory(AudioPlayer.soundFilesPath + @"\" + soundPackTempFileName, AudioPlayer.soundFilesPath + @"\sounds_temp");
            UpdateHelper.ProcessFileUpdates(AudioPlayer.soundFilesPath + @"\sounds_temp");
            UpdateHelper.MoveDirectory(AudioPlayer.soundFilesPath + @"\sounds_temp", AudioPlayer.soundFilesPath);
            */
#if SOUNDPACK_TEST
            if (true)
#else
            if (!CrewChief.Debug.RunningUnderDebugger ||
                SoundPackVersionsHelper.currentSoundPackVersion <= 0 || SoundPackVersionsHelper.currentPersonalisationsVersion <= 0 || SoundPackVersionsHelper.currentDriverNamesVersion <= 0)
#endif
            {
                mainWindowLayout.showSoundUpdate();
                var checkForUpdatesThread = new Thread(() =>
                {
                    try
                    {
                        Console.WriteLine("Checking for updates");

                        String firstUpdate = preferAlternativeDownloadSite ? autoUpdateXMLURLVPS : autoUpdateXMLURLTheCrewChiefOrg;
                        String secondUpdate = preferAlternativeDownloadSite ? autoUpdateXMLURLTheCrewChiefOrg : autoUpdateXMLURLVPS;

                        Thread.CurrentThread.IsBackground = true;
                        // now the sound packs
                        downloadSoundPackButton.Text = Configuration.getUIString("checking_sound_pack_version");
                        downloadDriverNamesButton.Text = Configuration.getUIString("checking_driver_names_version");
                        downloadPersonalisationsButton.Text = Configuration.getUIString("checking_personalisations_version");

                        Boolean appRestarted = CrewChief.CommandLine.Get("app_restart") != null;
                        Boolean skipAppUpdates = CrewChief.CommandLine.Get("skip_updates") != null;
                        if (skipAppUpdates)
                        {
                            Console.WriteLine("Skipping application update check. To enable this check, run the app *without* the '-skip_updates' command line argument");
                        }
                        Boolean gotSoundpackUpdateData;
                        if ((gotSoundpackUpdateData = GotSoundpackUpdateData(skipAppUpdates, appRestarted, ref firstUpdate)))
                        {
                            Console.WriteLine("Got soundpack update data from primary URL: " + Utilities.Strings.GetHostFromUrl(firstUpdate));
                        }
                        else
                        {
                            Console.WriteLine("Unable to get update data with primary URL, trying secondary");
                            if ((gotSoundpackUpdateData = GotSoundpackUpdateData(skipAppUpdates, appRestarted, ref secondUpdate)))
                            {
                                Console.WriteLine("Got soundpack update data from secondary URL: " + Utilities.Strings.GetHostFromUrl(secondUpdate));
                            }
                            else
                            {
                                Console.WriteLine("No update data from secondary URL, finally checking GitLab");
                                if ((gotSoundpackUpdateData = GotSoundpackUpdateData(skipAppUpdates, appRestarted, ref autoUpdateXMLURL_GitLab)))
                                {
                                    Console.WriteLine("Got soundpack update data from GitLab: " + Utilities.Strings.GetHostFromUrl(autoUpdateXMLURL_GitLab));
                                }
                            }
                        }
                        if (formClosed)
                        {
                            return;
                        }
                        if (gotSoundpackUpdateData)
                        {
#if SOUNDPACK_TEST
                            SoundPackVersionsHelper.currentSoundPackVersion--;
                            SoundPackVersionsHelper.currentPersonalisationsVersion--;
                            SoundPackVersionsHelper.currentDriverNamesVersion--;
#endif
                            soundPackUpdateCheck();
                        }
                        else
                        {
                            Console.WriteLine("Unable to get update data");
                        }
                    }
                    catch (Exception)
                    {
                        // This can throw on form close.
                    }
                });
                checkForUpdatesThread.Name = "MainWindow.checkForUpdatesThread";
                ThreadManager.RegisterResourceThread(checkForUpdatesThread);
                checkForUpdatesThread.Start();
            }
            else
            {
                Console.WriteLine("Skipping update check in debug mode");
            }
#endregion Updates

            if (UserSettings.GetUserSettings().getBoolean("show_splash_screen"))
            {
                Program.LoadingScreen.Close();
                Console.WriteLine("Loading screen closed");
            }

            if (UserSettings.GetUserSettings().getBoolean("minimize_on_startup"))
            {
                if (this.minimizeToTray)
                    this.HideToTray();
                else
                    this.WindowState = FormWindowState.Minimized;
            }

        }

        /// <returns>true if an update is available</returns>
        private bool soundPackUpdateCheck()
        {
            initialiseSoundPackButtons();
            (newSoundPackAvailable, newPersonalisationsAvailable, newDriverNamesAvailable) = SoundPackUpdate();

            if (newSoundPackAvailable || newPersonalisationsAvailable || newDriverNamesAvailable)
            {
                // Ok, we have something available for download (any of the buttons is green).
                // Restore CC once so that user gets higher chance of noticing.
                // I am not sure what is the best approach, we could also have text in the context menu,
                // but I definitely dislike Balloons and other distracting methods.  But, basically if we choose
                // to do anything, do it here.
                // This has limitation if, say, we have sound pack available, and at next startup we have driver pack
                // available, one property is not enough.  But this is ultra rare and not worth complications.

                if (!UserSettings.GetUserSettings().getBoolean("update_notify_attempted"))
                {
                    // Do this once per update availability.
                    UserSettings.GetUserSettings().setProperty("update_notify_attempted", true);
                    UserSettings.GetUserSettings().saveUserSettings();

                    // Slight race with minimize on startup :D
                    this.Invoke((MethodInvoker)delegate
                    {
                        this.RestoreFromTray();
                    });
                }
                return true;
            }
            else
            {
                mainWindowLayout.hideSoundUpdate();
                // If there are no updates available, clear the update_notify_attempted flag if it is set.
                if (UserSettings.GetUserSettings().getBoolean("update_notify_attempted"))
                {
                    UserSettings.GetUserSettings().setProperty("update_notify_attempted", false);
                    UserSettings.GetUserSettings().saveUserSettings();
                }
                if (UserSettings.GetUserSettings().getBoolean("FIRST_RUN"))
                {
                    buttonMyName_Click(this, null);
                    var form = new PropertiesForm(this, firstRun: true);
                    form.ShowDialog(this);
                    UserSettings.GetUserSettings().setProperty("FIRST_RUN", false);
                    // console_flush is set by default to track any problems when first installed
                    // but we don't want to keep it on forever (unless debugging)
                    UserSettings.GetUserSettings().setProperty("console_flush", false);
                    UserSettings.GetUserSettings().saveUserSettings();
                }
            }

            Console.WriteLine("Check for updates completed");
            return false;
        }

        private bool GotSoundpackUpdateData(bool skipAppUpdates, bool appRestarted, ref string updateURL)
        {
            Boolean gotSoundpackUpdateData = false;
            if (!formClosed)
            {
                try
                {
                    if (!skipAppUpdates)
                    {
                        AutoUpdater.Start(updateURL);
                    }
                    if (updateURL.Contains("downloadxml") && CrewChief.gameDefinition != null)
                    {
                        updateURL += "&lastplayed=" + (appRestarted ? "-app_restart" : CrewChief.gameDefinition.gameEnum.ToString());
                    }
                    string xml = new WebClient().DownloadString(updateURL);
                    string languageToCheck = AudioPlayer.soundPackLanguage == null ? "en" : AudioPlayer.soundPackLanguage;
                    gotSoundpackUpdateData = SoundPackVersionsHelper.parseUpdateData(xml, languageToCheck);
                }
                catch (WebException we)
                {
                    Log.Warning(we.Message);
                    Log.Warning("Unable to get update data from " + Utilities.Strings.GetHostFromUrl(updateURL));
                }
                catch (Exception ee)
                {
                    Log.Exception(ee);
                }
            }
            return gotSoundpackUpdateData;
        }

        public static SpeechWizard_V speechWizard_V;

        private void ControllersList_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= this.controllersList.Items.Count)
            {
                // WTF?
                return;
            }
            var entry = (MainWindow.ControllerUiEntry)this.controllersList.Items[e.Index];

            // Measure the string.
            var txtSize = e.Graphics.MeasureString(entry.uiText, this.Font);

            // Set the required size.
            e.ItemHeight = (int)txtSize.Height;
            e.ItemWidth = (int)txtSize.Width;
        }

        private void ControllersList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= this.controllersList.Items.Count)
            {
                // WTF?
                return;
            }

            // Draw the background of the ListBox control for each item.
            e.DrawBackground();

            var entry = (MainWindow.ControllerUiEntry)this.controllersList.Items[e.Index];

            var brush = entry.isConnected 
                ? (MainWindow.darkModeCS != null ? Brushes.White : Brushes.Black) 
                : Brushes.Gray;

            if (e.State.HasFlag(DrawItemState.Selected))
            {
                brush = Brushes.White;
            }

            SizeF txt_size = e.Graphics.MeasureString(entry.uiText, e.Font);

            e.Graphics.DrawString(this.controllersList.Items[e.Index].ToString(),
                e.Font, brush, e.Bounds, StringFormat.GenericDefault);

            // If the ListBox has focus, draw a focus rectangle around the selected item.
            e.DrawFocusRectangle();
        }

        private void HideToTray()
        {
            if (!this.minimizeToTray)
                return;

            this.ShowInTaskbar = false;
            this.Hide();
            this.notificationTrayIcon.Visible = true;

            // Do not mess with WindowState here, causes weirdest problems.
        }

        private void RestoreFromTray()
        {
            if (!this.minimizeToTray)
                return;

            this.ShowInTaskbar = true;
            this.notificationTrayIcon.Visible = false;
            this.Show();

            // This is necessary to bring window to the foreground.  Why ffs BringToFront doesn't work is beyound me.
            this.WindowState = FormWindowState.Normal;

            this.ReAppyTheme();

            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled
                && !Utilities.IsWindows11OrLater())
            {
                // Workaround for Win10 not coloring titlebar on restore.
                this.Hide();
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.Activate();
            }
        }

        /*
         changes the current message playback volume. If saveChange is true the change is written to the properties file,
         if updateSlider is true the slider is moved to reflect the new volume
         */
        public void updateMessagesVolume(float messagesVolume, Boolean saveChange, Boolean updateSlider)
        {
            if (messagesVolume < 0)
            {
                currentMessageVolume = 0;
            }
            else if (messagesVolume > 1)
            {
                currentMessageVolume = 1;
            }
            else
            {
                currentMessageVolume = messagesVolume;
            }
            // no point in setting output channel volume with nAudio - the sound file volumes are scaled separately
            if (!UserSettings.GetUserSettings().getBoolean("use_naudio"))
            {
                setOuputChannelVolume(currentMessageVolume);
            }
            if (saveChange)
            {
                UserSettings.GetUserSettings().setProperty("messages_volume", currentMessageVolume);
                UserSettings.GetUserSettings().saveUserSettings();
            }
            if (updateSlider)
            {
                messagesVolumeSlider.Value = (int)(currentMessageVolume * 100f);
            }
        }

        private void messagesVolumeSlider_Scroll(object sender, EventArgs e)
        {
            float volFloat = (float)messagesVolumeSlider.Value / 100;
            // no point in setting output channel volume with nAudio - the sound file volumes are scaled separately
            if (!UserSettings.GetUserSettings().getBoolean("use_naudio"))
            {
                setOuputChannelVolume(volFloat);
            }
            currentMessageVolume = volFloat;
            UserSettings.GetUserSettings().setProperty("messages_volume", volFloat);
            UserSettings.GetUserSettings().saveUserSettings();
        }

        // update the output channel volume - not used for nAudio
        private void setOuputChannelVolume(float vol)
        {
            int NewVolume = (int)(((float)ushort.MaxValue) * vol);
            // Set the same volume for both the left and the right channels
            uint NewVolumeAllChannels = (((uint)NewVolume & 0x0000ffff) | ((uint)NewVolume << 16));
            // Set the volume
            NativeMethods.waveOutSetVolume(IntPtr.Zero, NewVolumeAllChannels);
        }

        private void backgroundVolumeSlider_Scroll(object sender, EventArgs e)
        {
            float volFloat = (float)backgroundVolumeSlider.Value / 100;
            UserSettings.GetUserSettings().setProperty("background_volume", volFloat);
            UserSettings.GetUserSettings().saveUserSettings();
        }

        /// <summary>
        /// Control the appearance of the start button
        /// </summary>
        class StartButton
        {
            enum StartButtonState
            {
                START,
                AUTO_START,
                STOP,
                STOP_AND_SAVE
            }
            StartButtonState startButtonState;
            private bool crewChiefIsRunning;
            readonly Dictionary<StartButtonState, string> startButtonText = new Dictionary<StartButtonState, string>
            {
                {StartButtonState.START,  Configuration.getUIString("start_application")},
                {StartButtonState.AUTO_START,  Configuration.getUIString("auto_detect_active_tooltip")},
                {StartButtonState.STOP,  Configuration.getUIString("stop")},
                {StartButtonState.STOP_AND_SAVE,  Configuration.getUIString("stop_and_save")},
            };

            internal StartButton()
            {
                startButtonState = initState();
                crewChiefIsRunning = false;
                update(crewChiefIsRunning);
            }

            StartButtonState initState()
            {
                if (UserSettings.GetUserSettings().getBoolean("enable_auto_detect") ||
                    UserSettings.GetUserSettings().getBoolean("enable_auto_detect_exit_cc"))
                {
                    instance.autoDetectTimer.Enabled = true;
                    return StartButtonState.AUTO_START;
                }
                return StartButtonState.START;
            }

            internal void update(bool _crewChiefIsRunning)
            {
                if (crewChiefIsRunning != _crewChiefIsRunning)
                {
                    Log.Verbose(
                        $"startButtonState change from showing {startButtonState.ToString()}");
                    switch (startButtonState)
                    {
                        case StartButtonState.START:
                        case StartButtonState.AUTO_START:
                        {
                            startButtonState = CrewChief.Debug.UserTraceLogging ?
                                StartButtonState.STOP_AND_SAVE :
                                StartButtonState.STOP;
                        }
                            break;
                        case StartButtonState.STOP:
                        case StartButtonState.STOP_AND_SAVE:
                        {
                            startButtonState = initState();
                        }
                            break;
                    }
                    Log.Verbose(() => $"to showing {startButtonState.ToString()}");
                }
                crewChiefIsRunning = _crewChiefIsRunning;
                Log.Verbose(() => $"crewChiefIsRunning now {_crewChiefIsRunning}");
                instance.startApplicationButton.Text = startButtonText[startButtonState];
                instance.startApplicationButton.ForeColor = crewChiefIsRunning ? 
                    Color.OrangeRed: 
                    Color.Green;

            }
        }

        private bool _isAppRunning;
        /// <summary>
        /// Is Crew Chief running
        /// </summary>
        public bool IsAppRunning
        {
            get
            {
                return _isAppRunning;
            }
            set
            {
                _isAppRunning = value;
                startButton.update(_isAppRunning);
                downloadDriverNamesButton.Enabled = !value && newDriverNamesAvailable;
                downloadSoundPackButton.Enabled = !value && newSoundPackAvailable;
                downloadPersonalisationsButton.Enabled = !value && newPersonalisationsAvailable;
            }
        }

        /// <summary>
        /// Set gameDefinitionList.Text
        /// from command line or the one used last time CC ran
        /// (failing that, the first one in the list)
        /// </summary>
        private void setSelectedGameDefinition()
        {
            Boolean setFromCommandLine = false;

            string game = CrewChief.CommandLine.Get("game");
            if (game != null)
            {
                game = game.ToUpper();
                foreach (var gameDef in GameDefinition.AllGameDefinitions)
                {
                    if (gameDef.commandLineName == game)
                    {
                        if (GameDefinition.getAllAvailableGameDefinitions(false).Contains(gameDef))
                        {
                            Console.WriteLine($"Set {gameDef.friendlyName} mode from command line");
                            this.gameDefinitionList.Text = gameDef.friendlyName;
                        }
                        else
                        {
                            Log.Error($"Command line -game selection '{game}' is not present in current profile");
                        }
                        setFromCommandLine = true; // Even if not present in current profile
                        break;
                    }
                    Log.Verbose(() => $"Enum {gameDef.gameEnum.ToString()}: command line name {gameDef.commandLineName}");
                }
            }
            if (!setFromCommandLine)
            {
                if (game != null)
                {
                    Log.Error($"Command line -game selection '{game}' is not valid");
                }
                string lastDef = UserSettings.GetUserSettings().getString("last_game_definition");
                if (lastDef != null && lastDef.Length > 0)
                {
                    try
                    {
                        GameDefinition gameDefinition = GameDefinition.getGameDefinitionForCommandLineName(lastDef);
                        if (gameDefinition != null)
                        {
                            Console.WriteLine("Set " + gameDefinition.friendlyName + " mode from previous launch");
                            this.gameDefinitionList.Text = gameDefinition.friendlyName;
                        }
                    }
                    catch (Exception)
                    {
                        Log.Warning($"gameDefinitionList.SelectedItem {SourceInfoHelpers.SourceFile()}:{SourceInfoHelpers.SourceLineNumber() - 5}");
                        //ignore, just don't set the value in the list
                    }
                }
            }

            int gamesCount = this.gameDefinitionList.Items.Count;
            if (this.gameDefinitionList.Text.Length == 0 && gamesCount > 0)
            {   // Nothing selected, pick a random one that doesn't have a plugin
                // or is a rally game because that can confuse new users
                int selectedGame = 0;
                int tries;
                for (tries = 0; tries < 100; tries++)   // We *really* want to find one that's suitable
                {
                    selectedGame = Utilities.random.Next(0, gamesCount-1);
                    try
                    {
                        var gameDefinition = GameDefinition.getGameDefinitionForFriendlyName(gameDefinitionList.Items[selectedGame].ToString());
                        if (!gameDefinition.hasPlugin && 
                            gameDefinition.racingType != CrewChief.RacingType.Rally)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex);
                    }
                }
                if (tries >= gamesCount)
                {
                    selectedGame = gamesCount - 1;
                }
                try
                {
                    this.gameDefinitionList.SelectedIndex = selectedGame;
                }
                catch (Exception)
                {
                    Log.Warning($"gameDefinitionList.SelectedItem {SourceInfoHelpers.SourceFile()}:{SourceInfoHelpers.SourceLineNumber() - 4}");
                }
            }

            if (this.gameDefinitionList.Text.Length > 0)
            {
                try
                {
                    CrewChief.gameDefinition = GameDefinition.getGameDefinitionForFriendlyName(this.gameDefinitionList.Text);
                }
                catch (Exception e) {Log.Exception(e);}
            }
            sizeComboBoxDropDown(gameDefinitionList);
            sizeComboBox(gameDefinitionList, 300);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!MainWindow.shouldSaveTrace
                && CrewChief.Debug.UserTraceLogging
                && !this.closedByCmdLineCommand)  // Don't save trace if we're closed by script.
            {
                // Message box with y/n to save?
                var dialogResult = MessageBox.Show("A trace was enabled, would you like to save this trace?", "Save trace?", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, (MessageBoxOptions)0x40000 /*MB_TOPMOST*/);

                if (dialogResult == DialogResult.Yes)
                    MainWindow.shouldSaveTrace = true;
            }
            overlay?.Dispose();
            subtitleOverlay?.Dispose();
            base.OnFormClosing(e);
            MacroManager.stop();
            Log.Debug(() => $"{this.WindowState} {FormWindowState.Maximized} {this.Width} {this.Height}");
            ConsoleWindowClose();

            controllerRescanThreadRunning = false;
            this.controllerConfiguration.cancelScan();
            controllerRescanThreadWakeUpEvent.Set();

            if(VROverlayController.vrUpdateThreadRunning)
            {
                VROverlayController.vrUpdateThreadRunning = false;
                VROverlayController.resumeVROverlayRenderThread();
            }

            try
            {
                Properties.Settings.Default["main_window_position"] = new Point(DesktopBounds.X, DesktopBounds.Y);
                if (this.WindowState != FormWindowState.Maximized)
                { // Don't save the size if MainWindow is maximized
                    Properties.Settings.Default["main_window_size"] = new Size(this.Width, this.Height);
                }
                Properties.Settings.Default.Save();
            }
            catch (Exception ee) { Log.Exception(ee); }

        }

        private void SetupNotificationTrayIcon()
        {
            Debug.Assert(notificationTrayIcon == null, "Supposed to be called once");

            notificationTrayIcon = new NotifyIcon();

            // Load the icon.
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            notificationTrayIcon.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));

            notificationTrayIcon.DoubleClick += NotifyIcon_DoubleClick;

            // Setup the context menu.
            var cms = new ContextMenuStrip();

            // Restore item.
            var cmi = cms.Items.Add(Configuration.getUIString("restore_context_menu"));
            cmi.Click += NotifyIcon_DoubleClick;

            // Start/Stop items.
            cms.Items.Add(new ToolStripSeparator());
            contextMenuStartItem = cms.Items.Add(Configuration.getUIString("start_application"), null, this.startApplicationButton_Click);
            contextMenuStopItem = cms.Items.Add(!CrewChief.Debug.UserTraceLogging ? Configuration.getUIString("stop") : Configuration.getUIString("stop_and_save"), null, this.startApplicationButton_Click);
            cms.Items.Add(new ToolStripSeparator());

            // Form Game context submenu.
            cmi = cms.Items.Add(Configuration.getUIString("game"));
            contextMenuGamesMenu = cmi as ToolStripMenuItem;
            foreach (var game in this.gameDefinitionList.Items)
            {
                var ddi = contextMenuGamesMenu.DropDownItems.Add(game.ToString());
                ddi.Click += (sender, e) =>
                {
                    var gameSelected = sender as ToolStripMenuItem;
                    if (gameSelected == null)
                        return;

                    this.gameDefinitionList.Text = gameSelected.Text;
                };
            }

            contextMenuGamesMenu.DropDownOpening += (sender, e) =>
            {
                var currGameFriendlyName = this.gameDefinitionList.Text;
                foreach (var game in contextMenuGamesMenu.DropDownItems)
                {
                    var tsmi = game as ToolStripMenuItem;
                    tsmi.Checked = tsmi.Text == currGameFriendlyName;
                }
            };

            // Preferences and Close items
            contextMenuPreferencesItem = cms.Items.Add(Configuration.getUIString("properties"), null, this.editPropertiesButtonClicked);
            cms.Items.Add(new ToolStripSeparator());
            cmi = cms.Items.Add(Configuration.getUIString("close_context_menu"));
            cmi.Click += (sender, e) =>
            {
                this.notificationTrayIcon.Visible = false;
                this.Close();
            };

            cms.Opening += (sender, e) =>
            {
                this.contextMenuStartItem.Enabled = !this.IsAppRunning;
                this.contextMenuStopItem.Enabled = this.IsAppRunning;

                this.contextMenuStartItem.Text = string.IsNullOrWhiteSpace(this.gameDefinitionList.Text)
                    ? Configuration.getUIString("start_application")
                    : string.Format(Configuration.getUIString("start_context_menu"), this.gameDefinitionList.Text);

                // Only allow game selection if we're in a Stopped state.
                foreach (var game in this.contextMenuGamesMenu.DropDownItems)
                    (game as ToolStripMenuItem).Enabled = !this.IsAppRunning;
                // Why not just  this.contextMenuGamesMenu.Enabled = !this.IsAppRunning;  ?
            };


            notificationTrayIcon.ContextMenuStrip = cms;
            
            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                MainWindow.darkModeCS.ThemeControl(cms);

            notificationTrayIcon.Text = Configuration.getUIString("idling_context_menu");
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            this.RestoreFromTray();
        }

        readonly Dictionary<CoDriver.CornerCallStyle, string> coDriverStyles =
            new Dictionary<CoDriver.CornerCallStyle, string>()
            {
                { CoDriver.CornerCallStyle.NUMBER_FIRST, Configuration.getUIString("codriver_style_number_first") },
                { CoDriver.CornerCallStyle.DIRECTION_FIRST, Configuration.getUIString("codriver_style_direction_first") },
                { CoDriver.CornerCallStyle.DESCRIPTIVE, Configuration.getUIString("codriver_style_descriptive") },
                { CoDriver.CornerCallStyle.NUMBER_FIRST_REVERSED, Configuration.getUIString("codriver_style_number_first_reversed") },
                { CoDriver.CornerCallStyle.DIRECTION_FIRST_REVERSED, Configuration.getUIString("codriver_style_direction_first_reversed") },
                { CoDriver.CornerCallStyle.MUTED, Configuration.getUIString("codriver_style_muted") }
            };

        private void InitializeUiText()
        {
            this.scanControllers.Text = Configuration.getUIString("scan_for_controllers");
            this.assignButtonToAction.Text = Configuration.getUIString("assign_control");
            this.deleteAssigmentButton.Text = Configuration.getUIString("delete_assignment");
            this.buttonEditCommandMacros.Text = Configuration.getUIString("edit_macro_commands");
            this.groupBoxAvailableControllers.Text = Configuration.getUIString("available_controllers");
            this.groupBoxAssignedActions.Text = Configuration.getUIString("available_actions");
            this.propertiesButton.Text = Configuration.getUIString("properties");
            this.groupBoxVoiceRecognitionMode.Text = Configuration.getUIString("voice_recognition_mode");
            SetButtonMyNameText();
            this.mainWindowTooltip.SetToolTip(this.groupBoxVoiceRecognitionMode,
                Configuration.getUIString("voice_recognition_mode_help"));
            this.alwaysOnButton.Text = Configuration.getUIString("always_on");
            this.mainWindowTooltip.SetToolTip(this.alwaysOnButton, Configuration.getUIString("voice_recognition_always_on_help"));
            this.toggleButton.Text = Configuration.getUIString("toggle_button");
            this.mainWindowTooltip.SetToolTip(this.toggleButton,
                Configuration.getUIString("voice_recognition_toggle_button_help"));
            this.holdButton.Text = Configuration.getUIString("hold_button");
            this.mainWindowTooltip.SetToolTip(this.holdButton, Configuration.getUIString("voice_recognition_hold_button_help"));
            this.listenIfNotPressedButton.Text = Configuration.getUIString("voice_recognition_listen_if_not_pressed");
            this.mainWindowTooltip.SetToolTip(this.listenIfNotPressedButton, 
                Configuration.getUIString("voice_recognition_release_button_help"));

            this.voiceDisableButton.Text = Configuration.getUIString("disabled");
            this.mainWindowTooltip.SetToolTip(this.voiceDisableButton, Configuration.getUIString("voice_recognition_disabled_help"));
            this.triggerWordButton.Text = Configuration.getUIString("trigger_word") + " (\"" + UserSettings.GetUserSettings().getString("trigger_word_for_always_on_sre") + "\")";
            this.mainWindowTooltip.SetToolTip(this.triggerWordButton, 
                Configuration.getUIString("voice_recognition_trigger_word_help"));
            this.messagesVolumeSliderLabel.Text = Configuration.getUIString("messages_volume");
            this.backgroundVolumeSliderLabel.Text = Configuration.getUIString("background_volume");
            this.filenameLabel.Text = "File &name to run";
            this.app_version.Text = Configuration.getUIString("app_version");
            this.forceVersionCheckButton.Text = Configuration.getUIString("check_for_updates");
            this.downloadSoundPackButton.Text = Configuration.getUIString("sound_pack_is_up_to_date");
            this.downloadDriverNamesButton.Text = Configuration.getUIString("driver_names_are_up_to_date");
            this.downloadPersonalisationsButton.Text = Configuration.getUIString("personalisations_are_up_to_date");
            this.mainWindowTooltip.SetToolTip(this.buttonMyName, Configuration.getUIString("personalisation_tooltip"));
            this.chiefNameLabel.Text = Configuration.getUIString("chief_name_label");
            this.mainWindowTooltip.SetToolTip(this.chiefNameLabel, Configuration.getUIString("chief_name_tooltip"));
            this.mainWindowTooltip.SetToolTip(this.chiefNameBox, Configuration.getUIString("chief_name_tooltip"));
            this.spotterNameLabel.Text = Configuration.getUIString("spotter_name_label");
            this.mainWindowTooltip.SetToolTip(this.spotterNameLabel, Configuration.getUIString("spotter_name_tooltip"));
            this.mainWindowTooltip.SetToolTip(this.spotterNameBox, Configuration.getUIString("spotter_name_tooltip"));
            this.codriverNameLabel.Text = Configuration.getUIString("codriver_name_label");
            this.mainWindowTooltip.SetToolTip(this.codriverNameLabel, Configuration.getUIString("codriver_name_tooltip"));
            this.mainWindowTooltip.SetToolTip(this.codriverNameBox, Configuration.getUIString("codriver_name_tooltip"));
            this.codriverStyleLabel.Text = Configuration.getUIString("codriver_style_label");
            this.donateLink.Text = Configuration.getUIString("donate_link_text");
            this.messagesAudioDeviceLabel.Text = Configuration.getUIString("messages_audio_device_label");
            this.backgroundAudioDeviceLabel.Text = Configuration.getUIString("background_audio_device_label");
            this.AddRemoveActions.Text = Configuration.getUIString("add_remove_actions");
            this.buttonVRWindowSettings.Text = Configuration.getUIString("vr_window_settings");
            this.fuelMultiplierLabel.Text = Configuration.getUIString("fuel_multiplier");
            this.gameDefinitionList.Items.Clear();
            this.gameDefinitionList.Items.AddRange(GameDefinition.getGameDefinitionFriendlyNames());
            this.mainWindowTooltip.SetToolTip(this.fuelMultiplierLabel, Configuration.getUIString("fuel_multiplier_help"));
            this.mainWindowTooltip.SetToolTip(this.numericUpDownfuelMultiplier, Configuration.getUIString("fuel_multiplier_help"));
            this.mainWindowTooltip.SetToolTip(this.gameDefinitionList, Configuration.getUIString("game_definition_help"));
            this.mainWindowTooltip.SetToolTip(this.groupBoxGame, Configuration.getUIString("game_definition_help"));
            
            foreach (var style in coDriverStyles)
            {
                this.codriverStyleBox.Items.Add(new MainWindow.CoDriverStyleEntry()
                {
                    uiText = style.Value,
                    style = style.Key
                });
            }
 
            if (MainWindow.soundTestMode)
            {
                this.SuspendLayout();
                mainWindowLayout.showSoundTest();
                this.ResumeLayout(false);
            }
        }

        // MainWindow layout controls
        internal MainWindowLayout mainWindowLayout;
        public static DarkModeForms.DarkModeCS darkModeCS = null;
        public MainWindow()
        {
            Program.BootTrace("MW 0");
            lock (MainWindow.instanceLock)
            {
                MainWindow.instance = this;
            }

            this.constructingWindow = true;

            InitializeComponent();

            DarkModeForms.DarkModeCS.IsDarkModeCSEnabled = UserSettings.GetUserSettings().getBoolean("enable_dark_mode");
            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                MainWindow.darkModeCS = new DarkModeForms.DarkModeCS(this);
			
			Program.BootTrace("MW 1");
            forceMinWindowSize = UserSettings.GetUserSettings().getBoolean("force_min_window_size");
            // Run time font size twiddling to deal with DPI issues
            exemplarFont = this.buttonEditCommandMacros.Font;
            bool tracelogToolStripMenuItem = UserSettings.GetUserSettings().getBoolean("crewchief_record_trace_checkbox") ||
                                             CrewChief.Debug.RunningUnderDebugger || CrewChief.Debug.ProfileMode;
            bool mTracePlaybackToolStripMenuItem = CrewChief.Debug.RunningUnderDebugger || CrewChief.Debug.DebugWithPlaybackMode;
            menuStrip = new MainMenuStrip(this,
                exemplarFont,
                tracelogToolStripMenuItem,
                mTracePlaybackToolStripMenuItem); // Add the menu strip to the main window
            Controls.Add(menuStrip);
            menuStrip.Width = this.Width;
            menuStrip.ResumeLayout(false);
            menuStrip.PerformLayout();
            this.tableLayoutPanelMain.Controls.Add(this.menuStrip, 0, 0);
            this.tableLayoutPanelMain.SetColumnSpan(this.menuStrip, 2);
            var bigFontSize = this.exemplarFont.SizeInPoints * 1.2f;
            foreach (var label in new List<Label>{
                        this.messagesAudioDeviceLabel,
                        this.speechRecognitionDeviceLabel
                     })
            {
                label.Font = new System.Drawing.Font("Microsoft Sans Serif", bigFontSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            }

            mainWindowLayout = new MainWindowLayout(this);
            InitializeUiText();
            startButton = new StartButton();
            Program.BootTrace("MW 2");

            this.SuspendLayout();
            Application.DoEvents();
            SetFrameHeading();

            SetupNotificationTrayIcon();

            if (CrewChief.Debug.RunningUnderDebugger || CrewChief.Debug.ProfileMode)
            {
                // Restore last saved trace file name.
                filenameTextbox.Text = UserSettings.GetUserSettings().getString("last_trace_file_name");
                filenameTextbox.TextChanged += MainWindow_TextChanged;
            }

            CheckForIllegalCrossThreadCalls = false;
            ConsoleWindowOpen(menuStrip.consoleContextMenuStrip);
            Console.WriteLine("Loading screen opened"); // The first point at which we can do that, the screen is already loaded.

            string folderError = DataFiles.MakeFolders();
            Console.WriteLine($"BaseFolder: {Log.AnonymisePath(DataFiles.BaseFolder)}");
            Console.WriteLine($"UserConfigFolder: {Log.AnonymisePath(DataFiles.UserConfigFolder)}");
            Console.WriteLine($"LocalApplicationDataFolder: {Log.AnonymisePath(DataFiles.LocalApplicationDataFolder)}");
            if (folderError != null)
            {
                Program.BootTrace("DataFiles !" + folderError);
                // Could not create one of the folders.
                // throw a new exception to be shown in the "oh shit" popup message
                throw new Exception($"Unable to create folder '{folderError}'\n Please exit Crew Chief and investigate");
            }

            // Folders exist
            if (!UserSettings.GetUserSettings().initFailed)
            {
                Console.WriteLine(UserSettings.GetUserSettings().initMessage);
            }
            else
			{
                // if we can't init the UserSettings Crew Chief will basically be fucked. So try to nuke the Britton_IT_Ltd directory from
                // orbit (it's the only way to be sure) then restart Crew Chief. This shit is comically flakey but what else can we do here?

                Program.BootTrace("DataFiles !!");
                String path = DataFiles.startupError;
                try
                {
                    File.WriteAllText(path, "Message: " + UserSettings.GetUserSettings().initMessage + "\n" +
                        UserSettings.GetUserSettings().initFailedExceptionMessage + "\nStack" + UserSettings.GetUserSettings().initFailedStack);
                }
                catch (Exception e)
                {
                    Program.BootTrace("DataFiles !!!");
                    // oh dear, we can't write the startup error log file.
                    // throw a new exception to be shown in the "oh shit" popup message
                    throw new Exception("Unable to write startup error logs: " + e.Message + "\n please ensure CrewChief can access " + path +
                        "\nStartup error is " + UserSettings.GetUserSettings().initFailedExceptionMessage + "\nStack" + UserSettings.GetUserSettings().initFailedStack);
                }

                Console.WriteLine(UserSettings.GetUserSettings().initMessage);
                Console.WriteLine("Unable to read settings or upgrade settings from previous version, settings will be reset to default");

                try
                {
                    Program.BootTrace("DataFiles 2");
                    UserSettings.ForciblyDeleteConfigDirectory();
                    // note we can't load these from the UI settings because loading stuff will be broken at this point
                    doRestart("Failed to load user settings. Crew Chief will automatically restart in order to recreate this file. " +
                              "Once Crew Chief has restarted, please restart it again manually.", "Failed to load user settings");
                }
                catch (Exception e)
                {
                    Program.BootTrace("DataFiles !!!!!");
                    // oh dear, now we are in a pickle.
                    // throw a new exception to be shown in the "oh shit" popup message
                    throw new Exception("Unable to remove broken app settings file: " + e.Message +
                        "\n Please exit Crew Chief and manually delete folder " + DataFiles.UserConfigFolder);
                }
            }

            Debug.Assert(CrewChief.gameDefinition == null, "CrewChief.gameDefinition not null");
            CrewChief.gameDefinition = new GameDefinition();
            setSelectedGameDefinition();

            this.app_version.Text = Configuration.getUIString("version") + ": " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Console.WriteLine("Starting app.  " + this.app_version.Text);
            this.filenameLabel.Visible = CrewChief.Debug.RunningUnderDebugger || CrewChief.Debug.ProfileMode;
            this.filenameTextbox.Visible = CrewChief.Debug.RunningUnderDebugger || CrewChief.Debug.ProfileMode;
            this.playbackInterval.Visible = CrewChief.Debug.RunningUnderDebugger || CrewChief.Debug.ProfileMode;

            if (MainWindow.soundTestMode)
            {
                Console.WriteLine("Sound-test enabled");
                this.buttonSmokeTest.Visible = true;
                this.smokeTestTextBox.Visible = true;
            }
            if (CrewChief.Debug.RunningUnderDebugger)
            {
                this.recordSession.Visible = true;
                mainWindowLayout.showTraceWindow();
                Log.setLogLevel(Log.LogType.Debug);
            }
            else if (UserSettings.GetUserSettings().getBoolean("crewchief_record_trace_checkbox"))
            {
                this.recordSession.Visible = true;
                this.recordSession.Checked = false; // it's visible, but you still need to opt-in
                this.labelPlaybackSpeed.Visible = false;
                mainWindowLayout.showTraceWindow();
            }
            else
            {
                this.recordSession.Visible = false;
                if (CrewChief.Debug.DebugCommandLineOption)
                {
                    Console.WriteLine("Dump-to-file enabled");
                    this.recordSession.Visible = true;
                    // Don't start logging as soon as CC starts
                    this.recordSession.Checked = !UserSettings.GetUserSettings().getBoolean("run_immediately");
                    Log.enableLogTypes(Log.LogType.Debug |
                                       Log.LogType.SoundDebug |
                                       Log.LogType.Spotter);
                }
                if (CrewChief.Debug.DebugWithPlaybackMode)
                {
                    Console.WriteLine("Dump-to-file and playback controls enabled");
                    this.recordSession.Visible = true;
                    // Don't start logging as soon as CC starts
                    this.recordSession.Checked = !UserSettings.GetUserSettings().getBoolean("run_immediately");
                    this.playbackInterval.Visible = true;
                    this.filenameLabel.Visible = true;
                    this.filenameTextbox.Visible = true;
                }
            }

            if (UserSettings.GetUserSettings().getBoolean("use_naudio"))
            {
                this.messagesAudioDeviceBox.Enabled = true;
                this.messagesAudioDeviceBox.Visible = true;
                this.messagesAudioDeviceLabel.Visible = true;
                this.messagesAudioDeviceBox.Items.AddRange(AudioPlayer.playbackDevices.Keys.ToArray());
                sizeComboBoxDropDown(messagesAudioDeviceBox);
                // only register the value changed listener after loading the available values
                string messagesPlaybackGuid = UserSettings.GetUserSettings().getString("NAUDIO_DEVICE_GUID_MESSAGES");
                Boolean foundMessagesDeviceGuid = false;
                if (messagesPlaybackGuid != null)
                {
                    foreach (KeyValuePair<string, Tuple<string, int>> entry in AudioPlayer.playbackDevices)
                    {
                        if (messagesPlaybackGuid.Equals(entry.Value.Item1))
                        {
                            this.messagesAudioDeviceBox.Text = entry.Key;
                            sizeComboBox(messagesAudioDeviceBox);
                            AudioPlayer.naudioMessagesPlaybackDeviceId = entry.Value.Item2;
                            AudioPlayer.naudioMessagesPlaybackDeviceGuid = entry.Value.Item1;
                            foundMessagesDeviceGuid = true;
                            break;
                        }
                    }
                }
                if (!foundMessagesDeviceGuid && AudioPlayer.playbackDevices.Count > 0)
                {
                    foreach (KeyValuePair<string, Tuple<string, int>> entry in AudioPlayer.playbackDevices)
                    {
                        if (entry.Value.Item2 == 0) //Default device
                        {
                            this.messagesAudioDeviceBox.Text = entry.Key;
                            sizeComboBox(messagesAudioDeviceBox);
                            AudioPlayer.naudioMessagesPlaybackDeviceId = entry.Value.Item2;
                            AudioPlayer.naudioMessagesPlaybackDeviceGuid = entry.Value.Item1;
                            // Note: caching on device change won't work in this case because we check for saved device.  However, not saving device
                            // here has advantage if user reconnects his preferred device eventually.
                        }
                    }
                }
                this.messagesAudioDeviceBox.SelectedValueChanged += new System.EventHandler(this.messagesAudioDeviceSelected);

                this.backgroundAudioDeviceBox.Enabled = true;
                this.mainWindowLayout.showBackgroundDeviceWindow();
                this.backgroundAudioDeviceBox.Items.AddRange(AudioPlayer.playbackDevices.Keys.ToArray());
                sizeComboBoxDropDown(backgroundAudioDeviceBox);
                string backgroundPlaybackGuid = UserSettings.GetUserSettings().getString("NAUDIO_DEVICE_GUID_BACKGROUND");
                // only register the value changed listener after loading the available values
                Boolean foundBackgroundDeviceGuid = false;
                if (backgroundPlaybackGuid != null)
                {
                    foreach (KeyValuePair<string, Tuple<string, int>> entry in AudioPlayer.playbackDevices)
                    {
                        if (backgroundPlaybackGuid.Equals(entry.Value.Item1))
                        {
                            this.backgroundAudioDeviceBox.Text = entry.Key;
                            sizeComboBox(backgroundAudioDeviceBox);
                            AudioPlayer.naudioBackgroundPlaybackDeviceId = entry.Value.Item2;
                            AudioPlayer.naudioBackgroundPlaybackDeviceGuid = entry.Value.Item1;
                            foundBackgroundDeviceGuid = true;
                            break;
                        }
                    }
                }
                if (!foundBackgroundDeviceGuid && AudioPlayer.playbackDevices.Count > 0)
                {
                    foreach (KeyValuePair<string, Tuple<string, int>> entry in AudioPlayer.playbackDevices)
                    {
                        if (entry.Value.Item2 == 0) //Default device
                        {
                            this.backgroundAudioDeviceBox.Text = entry.Key;
                            AudioPlayer.naudioBackgroundPlaybackDeviceId = entry.Value.Item2;
                            AudioPlayer.naudioBackgroundPlaybackDeviceGuid = entry.Value.Item1;
                            // Note: caching on device change won't work in this case because we check for saved device.  However, not saving device
                            // here has advantage if user reconnects his preferred device eventually.
                        }
                    }
                }
                this.backgroundAudioDeviceBox.SelectedValueChanged += new System.EventHandler(this.backgroundAudioDeviceSelected);
            }

            if (UserSettings.GetUserSettings().getBoolean("use_naudio_for_speech_recognition"))
            {
                this.mainWindowLayout.showInputDeviceSelector();
                this.speechRecognitionDeviceBox.Items.AddRange(SpeechRecogniser.speechRecognitionDevices.Keys.ToArray());
                sizeComboBoxDropDown(speechRecognitionDeviceBox);
                // only register the value changed listener after loading the available values
                string speechRecognitionDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_RECORDING_DEVICE_GUID");
                Boolean foundspeechRecognitionDeviceGuid = false;
                if (speechRecognitionDeviceGuid != null)
                {
                    foreach (KeyValuePair<string, Tuple<string, int>> entry in SpeechRecogniser.speechRecognitionDevices)
                    {
                        if (speechRecognitionDeviceGuid.Equals(entry.Value.Item1))
                        {
                            this.speechRecognitionDeviceBox.Text = entry.Key;
                            sizeComboBox(speechRecognitionDeviceBox);
                            SpeechRecogniser.speechInputDeviceIndex = entry.Value.Item2;
                            foundspeechRecognitionDeviceGuid = true;
                            break;
                        }
                    }
                }
                if (!foundspeechRecognitionDeviceGuid && SpeechRecogniser.speechRecognitionDevices.Count > 0)
                {
                    foreach (KeyValuePair<string, Tuple<string, int>> entry in SpeechRecogniser.speechRecognitionDevices)
                    {
                        if (entry.Value.Item2 == 0) //Default device
                        {
                            this.speechRecognitionDeviceBox.Text = entry.Key;
                        }
                    }
                }
                this.speechRecognitionDeviceBox.SelectedValueChanged += new System.EventHandler(this.speechRecognitionDeviceSelected);
            }
            else
            {
                this.mainWindowLayout.hideInputDeviceSelector();
            }

            // NOTE: if you ever move this construction, please make sure controller rescan thread is not running yet.
            Debug.Assert(!this.controllerRescanThreadRunning);
            controllerConfiguration = new ControllerConfiguration(this);

            // NOTE: important to keep this instantiation here to avoid race between DirectInput and WMP initialization.
            crewChief = new CrewChief(controllerConfiguration);

            controllerConfiguration.initialize();
            GlobalResources.controllerConfiguration = controllerConfiguration;

            HashSet<string> availablePersonalisations = new HashSet<string>(this.crewChief.audioPlayer.personalisationsArray);
            if (allowCompositePersonalisations)
            {
                availablePersonalisations.UnionWith(new HashSet<string>(SoundCache.availableDriverNamesForUI));
            }

            this.chiefNameBox.Items.AddRange(AudioPlayer.availableChiefVoices.ToArray());
            this.spotterNameBox.Items.AddRange(NoisyCartesianCoordinateSpotter.availableSpotters.ToArray());
            this.codriverNameBox.Items.AddRange(CoDriver.availableCodrivers.ToArray());

            string savedChief = UserSettings.GetUserSettings().getString("chief_name");
            if (!String.IsNullOrWhiteSpace(savedChief) && AudioPlayer.availableChiefVoices.Contains(savedChief))
            {
                this.chiefNameBox.Text = savedChief;
            }
            else
            {
                this.chiefNameBox.Text = AudioPlayer.defaultChiefId;
            }

            string savedSpotter = UserSettings.GetUserSettings().getString("spotter_name");
            if (!String.IsNullOrWhiteSpace(savedSpotter) && NoisyCartesianCoordinateSpotter.availableSpotters.Contains(savedSpotter))
            {
                this.spotterNameBox.Text = savedSpotter;
            }
            else
            {
                this.spotterNameBox.Text = NoisyCartesianCoordinateSpotter.defaultSpotterId;
            }

            string savedCodriver = UserSettings.GetUserSettings().getString("codriver_name");
            if (!String.IsNullOrWhiteSpace(savedCodriver) && CoDriver.availableCodrivers.Contains(savedCodriver))
            {
                this.codriverNameBox.Text = savedCodriver;
            }
            else
            {
                this.codriverNameBox.Text = CoDriver.defaultCodriverId;
            }

            try
            {
                codriverStyleBox.Text = coDriverStyles[(CoDriver.CornerCallStyle)UserSettings.GetUserSettings().getInt("codriver_style")];
            }
            catch (Exception e)
            { // defensive code for strange error reported once.
            }


            // only register the value changed listener after loading the saved values
            this.chiefNameBox.SelectedValueChanged += new System.EventHandler(this.chiefNameSelected);
            this.spotterNameBox.SelectedValueChanged += new System.EventHandler(this.spotterNameSelected);
            this.codriverNameBox.SelectedValueChanged += new System.EventHandler(this.codriverNameSelected);
            this.codriverStyleBox.SelectedValueChanged += new System.EventHandler(this.codriverStyleSelected);

            float messagesVolume = UserSettings.GetUserSettings().getFloat("messages_volume");
            float backgroundVolume = UserSettings.GetUserSettings().getFloat("background_volume");
            updateMessagesVolume(messagesVolume, false, true);
            backgroundVolumeSlider.Value = (int)(backgroundVolume * 100f);

            Console.WriteLine("Loading controller settings");
            string customDeviceGuid = UserSettings.GetUserSettings().getString("custom_device_guid");
            if (customDeviceGuid != null && customDeviceGuid.Length > 0)
            {
                try
                {
                    Guid guid;
                    if (Guid.TryParse(customDeviceGuid, out guid))
                    {
                        controllerConfiguration.addCustomController(guid);
                    }
                    else
                    {
                        Console.WriteLine("Failed to add custom device, unable to process GUID");
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Failed to add custom device, message: ");
                }
            }
            Console.WriteLine("Load controller settings complete");
            voiceOption = getVoiceOptionEnum(UserSettings.GetUserSettings().getString("VOICE_OPTION"));
            switch (voiceOption)
            {
                case VoiceOptionEnum.DISABLED:
                    voiceDisableButton.Checked = true;
                    break;
                case VoiceOptionEnum.ALWAYS_ON:
                    alwaysOnButton.Checked = true;
                    break;
                case VoiceOptionEnum.HOLD:
                    holdButton.Checked = true;
                    break;
                case VoiceOptionEnum.TOGGLE:
                    toggleButton.Checked = true;
                    break;
                case VoiceOptionEnum.TRIGGER_WORD:
                    triggerWordButton.Checked = true;
                    break;
                case VoiceOptionEnum.NOT_PRESSED:
                    listenIfNotPressedButton.Checked = true;
                    break;
            }

            // don't allow trigger word or toggle if using nAudio
            if (UserSettings.GetUserSettings().getBoolean("use_naudio_for_speech_recognition"))
            {
                if (voiceOption == VoiceOptionEnum.TOGGLE || voiceOption == VoiceOptionEnum.TRIGGER_WORD)
                {
                    Console.WriteLine("Voice option " + voiceOption.ToString() + " not compatible with nAudio input");
                    this.voiceDisableButton.Checked = true;
                }
                this.tableLayoutPanelVoiceRecognitionModes.SetRow(this.alwaysOnButton, 2);
                this.tableLayoutPanelVoiceRecognitionModes.SetRow(this.listenIfNotPressedButton, 3);
                this.triggerWordButton.Enabled = false;
                this.triggerWordButton.Visible = false;
                this.toggleButton.Enabled = false;
                this.toggleButton.Visible = false;
            }
            this.assignButtonToAction.Enabled = false;
            this.deleteAssigmentButton.Enabled = false;

            this.ResumeLayout();

            bool forceHScrollbar = UserSettings.GetUserSettings().getBoolean("scroll_bars_on_main_window") || NEED_H_SCROLL;
            bool forceVScrollbar = UserSettings.GetUserSettings().getBoolean("scroll_bars_on_main_window") || NEED_V_SCROLL;

            this.AutoScroll = forceHScrollbar || forceVScrollbar;
            this.HScroll = forceHScrollbar;
            this.VScroll = forceVScrollbar;

            this.Resize += MainWindow_Resize;
            this.KeyPreview = true;
            this.KeyDown += MainWindow_KeyDown;

            this.constructingWindow = false;
            if (UserSettings.GetUserSettings().getBoolean("enable_overlay_window"))
            {
                overlay = new CrewChiefOverlayWindow();
                overlay.Run();
            }
            if (UserSettings.GetUserSettings().getBoolean("enable_subtitle_overlay"))
            {
                subtitleOverlay = new SubtitleOverlay();
                subtitleOverlay.Run();
            }
        }

        private void SetFrameHeading(string game=null)
        {
            var currProfileName = UserSettings.currentUserProfileFileName;
            if (currProfileName.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase))
                currProfileName = currProfileName.Substring(0, currProfileName.Length - ".json".Length);

            if (game != null)
            {
                currProfileName += $" - Running: {game}";
            }
            this.Text = $"{Configuration.getUIString("main_window_title_prefix")} {currProfileName}";
        }

        private void ReAppyTheme()
        {
            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                this.consoleTextBox.BackColor = this.messagesVolumeSlider.BackColor = this.backgroundVolumeSlider.BackColor = this.BackColor;
        }

        private bool isSteamVrRunning()
        {
            return Win32Stuff.FindWindowsWithText("SteamVR").FirstOrDefault() != IntPtr.Zero;
        }

        private bool initSteamVR()
        {
            // if the VR config file creation has failed _VRConfig will be null here
            if (SteamVR.instance != null && _VRConfig != null)
            {
                try
                {
                    deviceManager = new DeviceManager(OpenVR.System);
                    captureSource = new Direct3D11CaptureSource(deviceManager, _VRConfig, OpenVR.System);

                    this.Invoke(() =>
                    {
                        if (MainWindow.instance != null)
                        {
                            vrOverlayForm = new VROverlaySettings(_VRConfig);
                            buttonVRWindowSettings.Enabled = true;
                        }
                    });

                    return true;
                }
                catch (Exception e)
                {
                    VROverlayController.vrUpdateThreadRunning = false;
                    Log.Exception(e, "Failed to init Overlays = ");
                }
            }
            else
            {
                VROverlayController.vrUpdateThreadRunning = false;
                return false;
            }
            return false;
        }
        private bool waitForSteamVR(int preSleep = 0)
        {
            if (preSleep > 0)
                Utilities.InterruptedSleep(preSleep, 50, keepWaitingPredicate: () => VROverlayController.vrUpdateThreadRunning);

            while (VROverlayController.vrUpdateThreadRunning
                && !this.isSteamVrRunning())
            {
                Thread.Sleep(1000);
            }

            if (VROverlayController.vrUpdateThreadRunning
                && !this.initSteamVR())
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void vrOverlaysUpdateThreadWorker()
        {
            bool vrOverlayForceDisabledDrawing = false;
            uint vrEventSize = (uint)SharpDX.Utilities.SizeOf<VREvent_t>();
            try
            {
                if (UserSettings.GetUserSettings().getBoolean("start_steam_vr_if_detected"))
                {
                    if (!initSteamVR())
                        return;
                }
                else
                {
                    if (!waitForSteamVR())
                        return;
                }

                while (VROverlayController.vrUpdateThreadRunning)
                {
                    try
                    {
                        if (VROverlayController.vrOverlayRenderThreadSuspended && !vrOverlayForceDisabledDrawing)  // This is to avoid locking most of the time.
                        {
                            lock (VROverlayController.suspendStateLock)
                            {
                                if (VROverlayController.vrOverlayRenderThreadSuspended)
                                {
                                    // Hide the layers.
                                    VROverlayWindow[] currentItemsToHide = null;
                                    lock (VROverlaySettings.instanceLock)
                                    {
                                        currentItemsToHide = vrOverlayForm.listBoxWindows.Items.OfType<VROverlayWindow>().ToArray();
                                    }
                                    currentItemsToHide.Where(wnd => wnd.enabled).ToList().ForEach(w => w.SetOverlayEnabled(false));
                                    vrOverlayForceDisabledDrawing = true;
                                }
                            }
                        }
                        else
                        {
                            vrOverlayForceDisabledDrawing = false;
                        }

                        if (!VROverlayController.vrUpdateThreadRunning)
                            return;

                        var vrEvent = new VREvent_t();
                        bool reinitialize = false;
                        while (OpenVR.System != null && OpenVR.System.PollNextEvent(ref vrEvent, vrEventSize))
                        {
                            switch ((EVREventType)vrEvent.eventType)
                            {
                                case EVREventType.VREvent_Quit:
                                    {
                                        this.handleVRQuit();
                                        reinitialize = true;
                                        break;
                                    }
                                default:
                                    break;
                            }
                        }
                        if (reinitialize)
                            waitForSteamVR(10000); // give svr process some time to shut down before we start monitoring again.

                        if (!VROverlayController.vrUpdateThreadRunning)
                            return;

                        if (!vrOverlayForceDisabledDrawing && OpenVR.System != null)
                        {
                            // update poses(matix, velocity) for supported devices.
                            TrackedDevices.UpdatePoses();
                            TrackedDevices.GetHeadPose(out SharpDX.Matrix hmdMatrix, out _, out _);

                            VROverlayWindow[] currentItems = null;
                            lock (VROverlaySettings.instanceLock)
                            {
                                currentItems = vrOverlayForm.listBoxWindows.Items.OfType<VROverlayWindow>().ToArray();
                            }

                            foreach (var wnd in currentItems)
                            {
                                wnd.HandleToggleKey();
                            }

                            var windowBatch = currentItems.Where(wnd => wnd.enabled).ToList();

                            captureSource.Capture(windowBatch);
                            foreach (var wnd in windowBatch)
                            {
                                wnd.hmdMatrix = hmdMatrix;
                                wnd.Draw();
                            }
                        }
                        Thread.Sleep(11);
                    }
                    catch (Exception ex)
                    {
                        // Treat exception as VRQuit.
                        this.handleVRQuit();
                        waitForSteamVR(10000); // give svr process some time to shut down before we start monitoring again.

                        Utilities.ReportException(ex, "vrOverlaysUpdateThreadWorker exception.", needReport: false);
                    }
                }
            }
            finally
            {

                this.handleVRQuit();

                SteamVR.enabled = false;
                Debug.WriteLine("Exiting VR Overlays Render thread.");
            }
        }

        private void handleVRQuit()
        {
            try
            {
                try
                {
                    if (VROverlayController.vrUpdateThreadRunning  // Shutting down.
                        && MainWindow.instance != null)
                    {
                        this.Invoke(() =>
                        {
                            if (MainWindow.instance != null)
                            {
                                buttonVRWindowSettings.Enabled = false;
                            }
                        });
                    }
                }
                catch (Exception)
                {
                    // Shutdown.
                }

                Application.OpenForms.OfType<VROverlaySettings>()
                                    .ToList()
                                    .ForEach(v => v.Close());

                OpenVR.System?.AcknowledgeQuit_Exiting();

                captureSource?.Dispose();
                captureSource = null;

                deviceManager?.Dispose();
                deviceManager = null;

                vrOverlayForm?.Dispose();
                vrOverlayForm = null;

                SteamVR.SafeDispose();
            }
            catch (Exception ex)
            {
                Utilities.ReportException(ex, "handleVRQuit exited with exception.", needReport: false);
            }
        }

        public void resetSteamVRTrackingPose()
        {
            if (!VROverlayController.vrUpdateThreadRunning ||
                OpenVR.Chaperone == null)
                return;

            try
            {
                if (UserSettings.GetUserSettings().getBoolean("force_seated_on_vr_view_reset"))
                    OpenVR.Chaperone.ResetZeroPose(ETrackingUniverseOrigin.TrackingUniverseSeated);
                else
                    OpenVR.Chaperone.ResetZeroPose(OpenVR.Compositor.GetTrackingSpace());

                Log.Commentary("Reset VR view");
            }
            catch (Exception ex)
            {
                // What happens is for some reason CC loses connection to SVR objects, in particularly while alt-tabbing.
                // It then recovers nicely (although slowly), but in between, OVR references are invalid.
                // One of the reasons I know for sure is security: for example, one window running elevated while SVR is not. Etc.
                //
                // So just ignore, but log until better times when we understand the problem and the correct solution.
                Utilities.ReportException(ex, "resetSteamVRTrackingPose exception.", needReport: false);
            }
        }

        private void controllerRescanThreadWorker()
        {
            while (controllerRescanThreadRunning && !disableControllerReacquire)
            {
                controllerRescanThreadWakeUpEvent.WaitOne();
                if (!controllerRescanThreadRunning)
                {
                    Debug.WriteLine("Exiting controller rescan thread.");
                    return;
                }

                if (MainWindow.instance != null)
                {
                    try
                    {
                        lock (this.controllerWriteLock)
                        {
                            this.refreshControllerList();
                        }
                    }
                    catch (Exception)
                    {
                        // Possible shutdown.
                    }
                }
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if ( e.KeyCode == Keys.F1)
            {
                menuStrip.helpToolStripMenuItem_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.F2)
            {
                var dialog = new TrackSpline.TrackSplineManager.SplineViewerDialog();
                dialog.ShowDialog(this);
                //AllGames.TrackSplineManager.DrawSpline(this);
                e.Handled = true;
            }
        }

        private void MainWindow_TextChanged(object sender, EventArgs e)
        {
            UserSettings.GetUserSettings().setProperty("last_trace_file_name", filenameTextbox.Text);

            // It's awful to save on each character entered, but alternatives are far hairier, so let it be (debug only stuff anyway).
            UserSettings.GetUserSettings().saveUserSettings();
        }

        private void MainWindow_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.HideToTray();
            }
            else
            {
                this.Size = checkMainWindowSize(this.Size);
            }
        }

        private void thread_listenForChannelOpen()
        {
            Boolean channelOpen = false;
            if (crewChief.speechRecogniser != null &&
                crewChief.speechRecogniser.initialised &&
                (voiceOption == VoiceOptionEnum.HOLD ||
                voiceOption == VoiceOptionEnum.NOT_PRESSED))
            {
                string mode = (voiceOption == VoiceOptionEnum.HOLD) ? "held" : "released";
                Console.WriteLine($"Running speech recognition in 'while button {mode}' mode");
                crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.HOLD;
                while (runListenForChannelOpenThread)
                {
                    // open means button is pressed in HOLD mode
                    // or released in NOT_PRESSED mode
                    var open = controllerConfiguration.isChannelOpen() ^ 
                                    voiceOption == VoiceOptionEnum.NOT_PRESSED;
                    Thread.Sleep(this.holdButtonPollFrequency);
                    if (!channelOpen && open)
                    {
                        Tracepoints.MainWindow.talkToChief();
                        channelOpen = true;
                        PlaybackModerator.holdModeTalkingToChief = true;
                        // if we reject messages while we're talking to the chief, attempt to interrupt any sound currently playing
                        if (PlaybackModerator.rejectMessagesWhenTalking)
                        {
                            SoundCache.InterruptCurrentlyPlayingSound(true);
                        }
                        // for pace notes recording, start SRE *after* the beep. For voice commands, start SRE *before* the beep
                        if (DriverTrainingService.isRecordingPaceNotes)
                        {
                            if (CrewChief.distanceRoundTrack > 0)
                            {
                                Console.WriteLine("Recording pace note...");
                                DriverTrainingService.startRecordingMessage((int)CrewChief.distanceRoundTrack, crewChief.audioPlayer);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Listening for voice command...");
                            crewChief.speechRecogniser.recognizeAsync();
                            crewChief.audioPlayer.playStartListeningBeep();
                        }
                        if (this.rejectMessagesWhenTalking)
                        {
                            crewChief.audioPlayer.purgeQueues();
                            muteVolumes();
                        }
                        if (GameOrVoipVolume.AudioDuckingWhileTalkingToCcEnabled)
                        {
                            GameOrVoipVolume.DuckGameAudioTalkingToCc();
                        }
                    }
                    else if (channelOpen && !open)
                    {
                        Tracepoints.MainWindow.endTalkToChief();
                        if (this.rejectMessagesWhenTalking)
                        {
                            // Drop any outstanding messages queued while user was talking, this should prevent weird half phrases.
                            crewChief.audioPlayer.purgeQueues(SpeechRecogniser.sreSessionId);
                            // unmute
                            unmuteVolumes();

                            crewChief.audioPlayer.playChiefEndSpeakingBeep();
                        }

                        if (DriverTrainingService.isRecordingPaceNotes)
                        {
                            Console.WriteLine("Saving recorded pace note");
                            DriverTrainingService.stopRecordingMessage(crewChief.audioPlayer);
                        }
                        else
                        {
                            // button released, if we're waiting for speech here (i.e. the SRE hasn't unilaterally
                            // decided we've finished talking and has gone off on some ill-advised recognition adventure)
                            // then we might want to sleep a bit before triggering the SRE just in case some cack-handed
                            // user has let go of the button too soon
                            int delayBeforeRecognising = UserSettings.GetUserSettings().getInt("sre_button_release_delay");
                            if (SpeechRecogniser.waitingForSpeech && delayBeforeRecognising > 0)
                            {
                                Utilities.InterruptedSleep(totalWaitMillis: delayBeforeRecognising, waitWindowMillis: 50, keepWaitingPredicate: () => crewChief.running);
                            }
                            Console.WriteLine("Invoking speech recognition...");
                            crewChief.speechRecogniser.recognizeAsyncCancel();
                            if (youWotThread == null
                                || !youWotThread.IsAlive)
                            {
                                ThreadManager.UnregisterTemporaryThread(youWotThread);
                                youWotThread = new Thread(() =>
                                {
                                    Utilities.InterruptedSleep(totalWaitMillis: this.sreWaitTime, waitWindowMillis: 50, keepWaitingPredicate: () => crewChief.running);

                                    PlaybackModerator.holdModeTalkingToChief = false;
                                    if (!channelOpen && !SpeechRecogniser.gotRecognitionResult)
                                    {
                                        crewChief.youWot(false);
                                    }
                                });
                                youWotThread.Name = "MainWindow.youWotThread";
                                ThreadManager.RegisterTemporaryThread(youWotThread);
                                youWotThread.Start();
                            }
                            else
                            {
                                Console.WriteLine("Skipping new instance of youWot thread because previous is still running.");
                            }
                        }
                        if (GameOrVoipVolume.AudioDuckingWhileTalkingToCcEnabled)
                        {
                            GameOrVoipVolume.UnDuckGameAudio();
                        }
                        channelOpen = false;
                    }
                }
            }
        }

        private void thread_listenForButtons()
        {
            DateTime lastButtoncheck = DateTime.UtcNow;
            if (crewChief.speechRecogniser.initialised && voiceOption == VoiceOptionEnum.TOGGLE)
            {
                Console.WriteLine("Running speech recognition in 'toggle button' mode");
            }
            while (runListenForButtonPressesThread)
            {
                Thread.Sleep(50);
                DateTime now = DateTime.UtcNow;
                controllerConfiguration.PollForButtonClicks();
                if (now > lastButtoncheck.Add(buttonCheckInterval)) // (50mS also)
                {
                    lastButtoncheck = now;
                    // Only process one button at a time
                    // because it's always been like that
                    var _ = controllerConfiguration.ExecuteSpecialClickedButton() ||
                            controllerConfiguration.ExecuteClickedButton();
                }
            }
        }

        #region ConcreteControllerActions
        public bool volumeUp()
        {
            bool status = false;
            if (currentMessageVolume == -1)
            {
                Console.WriteLine("Initial volume not set, ignoring");
            }
            else if (currentMessageVolume >= 1)
            {
                Console.WriteLine("Volume at max");
            }
            else
            {
                Console.WriteLine("Increasing volume");
                updateMessagesVolume(currentMessageVolume + 0.05f, true, true);
                status = true;
            }
            return status;
        }

        public bool volumeDown()
        {
            bool status = false;
            if (currentMessageVolume == -1)
            {
                Console.WriteLine("Initial volume not set, ignoring");
            }
            else if (currentMessageVolume <= 0)
            {
                Console.WriteLine("Volume at min");
            }
            else
            {
                Console.WriteLine("Decreasing volume");
                updateMessagesVolume(currentMessageVolume - 0.05f, true, true);
                status = true;
            }
            return status;
        }

        public void channelOpen()
        {
            if (crewChief.speechRecogniser != null && crewChief.speechRecogniser.initialised && voiceOption == VoiceOptionEnum.TOGGLE)
            {
                // JB: no idea why we're setting this enum option here. Will leave it in just in case
                crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.TOGGLE;
                if (SpeechRecogniser.waitingForSpeech)
                {
                    Console.WriteLine("Cancelling...");
                    SpeechRecogniser.waitingForSpeech = false;
                    crewChief.speechRecogniser.recognizeAsyncCancel();
                }
                else
                {
                    // if we reject messages while we're talking to the chief, attempt to interrupt any sound currently playing
                    if (PlaybackModerator.rejectMessagesWhenTalking)
                    {
                        SoundCache.InterruptCurrentlyPlayingSound(true);
                    }
                    Console.WriteLine("Listening...");
                    crewChief.speechRecogniser.recognizeAsync();
                    crewChief.audioPlayer.playStartListeningBeep();
                }
            }
        }

        public void toggleSpotter()
        {
            Console.WriteLine("Toggling spotter mode");
            crewChief.toggleSpotterMode();
        }

        public void toggleMute()
        {
            if (!isMuted)
            {
                //crewChief.audioPlayer.playMuteBeep();
                muteVolumes();
            }
            else
            {
                unmuteVolumes();
                //crewChief.audioPlayer.playUnMuteBeep();
            }
            isMuted = !isMuted;
        }
        #endregion ConcreteControllerActions

        private void unmuteVolumes()
        {
            updateMessagesVolume(messageVolumeToRestore, false, false);
            crewChief.audioPlayer.muteBackgroundPlayer(false);
            messagesVolumeSlider.Enabled = true;
            backgroundVolumeSlider.Enabled = true;
        }

        private void muteVolumes()
        {
            // save the volume level to restore later
            messageVolumeToRestore = currentMessageVolume;
            updateMessagesVolume(0, false, false);
            crewChief.audioPlayer.muteBackgroundPlayer(true);
            messagesVolumeSlider.Enabled = false;
            backgroundVolumeSlider.Enabled = false;
        }

        public void startApplicationButton_Click(object sender, EventArgs e)
        {
            if (!IsAppRunning)
            {
                runCrewChief();
            }
            else 
            { 
                stopCrewChief();
            }
#if EXCEPTION_TEST
            Utilities.ReportException(new Exception("Exception test"), "Testing the Exception handler when MainWindow is loaded", true);
#endif
        }
        private void runCrewChief()
        {
            MainWindow.shouldSaveTrace = false;  // i.e. shouldSaveTrace if CC is NOT running (now)
            doStartAppStuff();
            if (!IsAppRunning) return;
            ThreadManager.DoWatchStartup(crewChief);
            overlay?.OnStartApplication(this, new OverlayElementClicked(null));
        }
        private void stopCrewChief()
        {
            MainWindow.shouldSaveTrace = true;  // i.e. shouldSaveTrace if CC is NOT running (now)
            doStopAppStuff();
            ThreadManager.DoWatchStop(crewChief);
            MainWindow.playingBackTrace = false;
            overlay?.OnStartApplication(this, new OverlayElementClicked(null));
        }

        private void uiSyncAppStart()
        {
            this.runListenForButtonPressesThread = controllerConfiguration.listenForButtons(voiceOption == VoiceOptionEnum.TOGGLE);
            this.assignButtonToAction.Enabled = false;
            this.deleteAssigmentButton.Enabled = false;
            this.groupBoxVoiceRecognitionMode.Enabled = false;
            this.propertiesButton.Enabled = false;

            // TODO_DT: This is a workaround for name box not editable if minimized on startup.  Workaround keeps it always enabled.
            // This also solves back themeing of disabled combo box in dark mode.  Note that editability issue is some sort of regression,
            // 4.16.3.5 does not have this bug.  Theming bug is still there, fuck it.
            //this.personalisationBox.Enabled = false;
            //this.personalisationBox.ForeColor = this.personalisationBox.BackColor = this.BackColor;

            this.chiefNameBox.Enabled = false;
            this.spotterNameBox.Enabled = false;
            this.codriverNameBox.Enabled = false;
            this.codriverStyleBox.Enabled = false;
            this.recordSession.Enabled = false;
            this.gameDefinitionList.Enabled = false;
            this.contextMenuPreferencesItem.Enabled = false;
            this.notificationTrayIcon.Text = string.Format(Configuration.getUIString("running_context_menu"), this.gameDefinitionList.Text);
            this.scanControllers.Enabled = false;
        }

        public void uiSyncAppStop()
        {
            this.deleteAssigmentButton.Enabled = this.buttonActionSelect.SelectedIndex > -1 &&
                this.controllerConfiguration.buttonAssignments[this.buttonActionSelect.SelectedIndex].controller != null;

            this.assignButtonToAction.Enabled = this.buttonActionSelect.SelectedIndex > -1 && this.controllersList.SelectedIndex > -1 && ((MainWindow.ControllerUiEntry)this.controllersList.Items[this.controllersList.SelectedIndex]).isConnected;
            this.propertiesButton.Enabled = true;
            this.groupBoxVoiceRecognitionMode.Enabled = true;
            this.chiefNameBox.Enabled = true;
            this.spotterNameBox.Enabled = true;
            this.codriverNameBox.Enabled = true;
            this.codriverStyleBox.Enabled = true;
            this.recordSession.Enabled = true;
            this.gameDefinitionList.Enabled = true;
            this.contextMenuPreferencesItem.Enabled = true;
            this.notificationTrayIcon.Text = Configuration.getUIString("idling_context_menu");
            this.scanControllers.Enabled = true;
        }

        private void loadSREGrammarAndStartListening()
        {
            bool loadedCommands = false;
            try
            {
                crewChief.speechRecogniser.loadSRECommands();
                loadedCommands = true;
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to load voice commands into speech recogniser: ");
            }
            try
            {
                crewChief.speechRecogniser.loadMacroVoiceTriggers(MacroManager.voiceTriggeredMacros);
                loadedCommands = true;
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to load command macros into speech recogniser: ");
            }

            // once the grammars are loaded successfully, we can start listening for commands
            // post this back to the main thread so we're kicking off the button listener from our root thread
            // and running always-on SRE on the root thread
            if (loadedCommands)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    bool holdOrReleaseButtonToListen = (voiceOption == VoiceOptionEnum.HOLD ||
                                voiceOption == VoiceOptionEnum.NOT_PRESSED) &&
                                crewChief.speechRecogniser != null &&
                                crewChief.speechRecogniser.initialised;
                    
                    runListenForChannelOpenThread = controllerConfiguration.listenForChannelOpen() && holdOrReleaseButtonToListen;
                    if (runListenForChannelOpenThread && holdOrReleaseButtonToListen)
                    {
                        Console.WriteLine("Listening on default audio input device");
                        ThreadStart channelOpenButtonListenerWork = thread_listenForChannelOpen;
                        Thread channelOpenButtonListenerThread = new Thread(channelOpenButtonListenerWork);

                        channelOpenButtonListenerThread.Name = "MainWindow.listenForChannelOpen";
                        ThreadManager.RegisterRootThread(channelOpenButtonListenerThread);

                        channelOpenButtonListenerThread.Start();
                    }
                    else if ((voiceOption == VoiceOptionEnum.ALWAYS_ON || voiceOption == VoiceOptionEnum.TRIGGER_WORD) &&
                        crewChief.speechRecogniser != null && crewChief.speechRecogniser.initialised)
                    {
                        Console.WriteLine("Running speech recognition in 'always on' mode");
                        crewChief.speechRecogniser.voiceOptionEnum = voiceOption;
                        crewChief.speechRecogniser.startContinuousListening();
                    }
                    if (runListenForButtonPressesThread)
                    {
                        Console.WriteLine("Listening for buttons");
                        ThreadStart buttonPressesListenerWork = thread_listenForButtons;
                        Thread buttonPressesListenerThread = new Thread(buttonPressesListenerWork);

                        buttonPressesListenerThread.Name = "MainWindow.listenForButtons";
                        ThreadManager.RegisterRootThread(buttonPressesListenerThread);

                        buttonPressesListenerThread.Start();
                    }
                });
            }
        }

        private void doStartAppStuff()
        {
            var ai = AiTeam.AiTeamService.Instance;
            var aiSettings = ai.Settings;
            var user = UserSettings.GetUserSettings();
            var ready = AiTeam.CrewVoiceReadiness.Evaluate(aiSettings,
                !aiSettings.SaraEnabled || !aiSettings.Enabled || ai.SaraSpotterPackCurrent,
                user.getString("spotter_name"), NoisyCartesianCoordinateSpotter.LoadedCarLeftFolder,
                AiTeam.CrewVoiceReadiness.LoadedSaraVoiceKey, user.getBoolean("enable_spotter"), AudioPlayer.playWithNAudio);
            if (!ready.Ready)
            {
                Console.WriteLine("[KI-Team] Start angehalten: " + ready.Message);
                MessageBox.Show(ready.Message, "KI-Team – Stimmen vorbereiten", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            IsAppRunning = true;
            startApplicationButton.Enabled = false;

            ConsoleWindowAppStart();
            GameDefinition gameDefinition = GameDefinition.getGameDefinitionForFriendlyName(gameDefinitionList.Text);
            if (gameDefinition != null)
            {
                crewChief.setGameDefinition(gameDefinition);
                MacroManager.initialise(crewChief.audioPlayer, crewChief.speechRecogniser, this.controllerConfiguration);
                uiSyncAppStart();
                CarData.loadCarClassData();
                TrackData.loadTrackLandmarksData();
                SetFrameHeading(gameDefinition.friendlyName);
                ThreadStart crewChiefWork = runApp;
                Thread crewChiefThread = new Thread(crewChiefWork);
                crewChiefThread.Name = "MainWindow.runApp";
                ThreadManager.RegisterRootThread(crewChiefThread);

                // this call is not part of the standard AutoUpdater API - I added a 'stopped' flag to prevent the auto updater timer
                // or other Threads firing when the game is running. It's not needed 99% of the time, it just stops that edge case where
                // the AutoUpdater triggers and steals focus while the player is racing
                AutoUpdater.Stop();

                crewChief.onRestart();
                crewChiefThread.Start();

                if (crewChief.speechRecogniser != null)
                {
                    ThreadStart loadSREGrammarWork = loadSREGrammarAndStartListening;
                    ThreadManager.UnregisterTemporaryThread(loadSREGrammarThread);
                    loadSREGrammarThread = new Thread(loadSREGrammarWork);
                    loadSREGrammarThread.Name = "MainWindow.loadSREGrammarThread";
                    ThreadManager.RegisterTemporaryThread(loadSREGrammarThread);
                    loadSREGrammarThread.Start();
                }
            }
            else
            {
                MessageBox.Show(Configuration.getUIString("please_choose_a_game_option"), Configuration.getUIString("no_game_selected"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                IsAppRunning = false;
                return;
            }
        }

        private void doStopAppStuff()
        {
            IsAppRunning = false;
            startApplicationButton.Enabled = false;
            SetFrameHeading();
            Console.WriteLine("Resuming console scrolling");
            MainWindow.autoScrollConsole = true;
            MacroManager.stop();
            if ((voiceOption == VoiceOptionEnum.ALWAYS_ON || voiceOption == VoiceOptionEnum.TOGGLE) && 
                crewChief.speechRecogniser != null && 
                crewChief.speechRecogniser.initialised)
            {
                Console.WriteLine("Stopping listening...");
                try
                {
                    SpeechRecogniser.waitingForSpeech = false;
                    crewChief.speechRecogniser.recognizeAsyncCancel();
                    crewChief.speechRecogniser.stopTriggerRecogniser();
                }
                catch (Exception e) { Log.Exception(e); }
            }
            stopApp();
            Console.WriteLine("Application stopped");
            DriverTrainingService.completeRecordingPaceNotes();
            DriverTrainingService.stopPlayingPaceNotes();
        }


        // called from the close callback on the main form
        private void stopApp(object sender, FormClosedEventArgs e)
        {
            lock (MainWindow.instanceLock)
            {
                MainWindow.instance = null;
                formClosed = true;
            }

            // Shutdown long running threads:
            CommandManager.StopCommandListeners();

            // SoundCache spawns a Thread to lazy-load the sound data. Cancel this:
            SoundCache.cancelLazyLoading = true;

            // Make sure we quit button assignment listener.
            controllerConfiguration.listenForAssignment = false;

            stopApp();
        }

        private void runApp()   // thread
        {
            String filenameToRun = null;
            Boolean record = false;
            if (!String.IsNullOrWhiteSpace(filenameTextbox.Text))
            {
                filenameToRun = filenameTextbox.Text;
                MainWindow.playingBackTrace = true;
                initialiseSpeechEngine(); // For SpeechTrace playback
                if (this.playbackInterval.Text.Length > 0)
                {
                    CrewChief.playbackIntervalMilliseconds = int.Parse(playbackInterval.Text);
                }
            }
            else
            {
                MainWindow.playingBackTrace = false;
            }
            if (recordSession.Checked)
            {
                record = true;
                mainWindowLayout.showTraceWindow();
            }
            if (!crewChief.Run(filenameToRun, record))
            {
                this.deleteAssigmentButton.Enabled = this.buttonActionSelect.SelectedIndex > -1 &&
                    this.controllerConfiguration.buttonAssignments[this.buttonActionSelect.SelectedIndex].controller != null;
                this.assignButtonToAction.Enabled = this.buttonActionSelect.SelectedIndex > -1 && this.controllersList.SelectedIndex > -1 && ((MainWindow.ControllerUiEntry)this.controllersList.Items[this.controllersList.SelectedIndex]).isConnected;
                stopApp();
                this.propertiesButton.Enabled = true;
                IsAppRunning = false;
            }
        }

        private void stopApp()
        {
            if (isMuted)
            {
                unmuteVolumes();
            }
            runListenForChannelOpenThread = false;
            runListenForButtonPressesThread = false;
            crewChief.stop();
            ConsoleWindowAppStop();
        }

        private void playbackIntervalChanged(object sender, EventArgs e)
        {
            if (this.playbackInterval.Text.Length > 0)
            {
                try
                {
                    CrewChief.playbackIntervalMilliseconds = int.Parse(playbackInterval.Text);
                }
                catch (Exception)
                {
                    // swallow - not much we can do here
                }
            }
            else
            {
                CrewChief.playbackIntervalMilliseconds = 0;
            }
        }

        private void buttonActionSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.deleteAssigmentButton.Enabled = this.buttonActionSelect.SelectedIndex > -1 && !crewChief.running;
            this.assignButtonToAction.Enabled = this.buttonActionSelect.SelectedIndex > -1 && this.controllersList.SelectedIndex > -1 && !crewChief.running && ((MainWindow.ControllerUiEntry)this.controllersList.Items[this.controllersList.SelectedIndex]).isConnected;
        }

        private void controllersList_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.deleteAssigmentButton.Enabled = this.buttonActionSelect.SelectedIndex > -1 && !crewChief.running;
            this.assignButtonToAction.Enabled = this.buttonActionSelect.SelectedIndex > -1 && this.controllersList.SelectedIndex > -1 && !crewChief.running && ((MainWindow.ControllerUiEntry)this.controllersList.Items[this.controllersList.SelectedIndex]).isConnected;
        }

        public void updateControllersUi()
        {
            Debug.Assert(controllerConfiguration.knownControllers != null);
            Debug.Assert(!this.InvokeRequired);

            this.controllersList.Items.Clear();

            // First, add active controllers to the list:
            // NOTE: it is important that connected controllers go first, because their index in the UI list is used to access controllerConfiguration.controllers list.
            foreach (ControllerConfiguration.ControllerData configData in controllerConfiguration.controllers)
            {
                this.controllersList.Items.Add(new MainWindow.ControllerUiEntry(configData.deviceName, isConnected: true));
            }

            // Now, add grayed out (inactive) controllers
            foreach (ControllerConfiguration.ControllerData configData in controllerConfiguration.knownControllers)
            {
                if (controllerConfiguration.controllers.Exists(c => c.guid == configData.guid))
                {
                    continue;
                }
                this.controllersList.Items.Add(new MainWindow.ControllerUiEntry(configData.deviceName, isConnected: false));
            }
        }

        public void updateActions()
        {
            Debug.Assert(!this.InvokeRequired);
            this.buttonActionSelect.Items.Clear();

            List<ControllerConfiguration.ButtonAssignment> assignedTo = new List<ControllerConfiguration.ButtonAssignment>();
            try
            {
                // Sort the available actions then move ones that are assigned to the top
                controllerConfiguration.buttonAssignments.Sort((x, y) => x.getInfo().CompareTo(y.getInfo()));
                assignedTo = new List<ControllerConfiguration.ButtonAssignment>(
                    controllerConfiguration.buttonAssignments.
                        Where(r => r.getInfo().Contains(Configuration.getUIString("assigned_to"))));
                assignedTo.Reverse(); // to keep them in order
            }
            catch (Exception e)
            {
                Log.Exception(e, "Sorting available actions");
            }

            try
            {
                foreach (var assigned in assignedTo)
                {
                    int assignedIndex = controllerConfiguration.buttonAssignments.FindIndex(r => r.action == assigned.action);
                    controllerConfiguration.buttonAssignments.Insert(0, controllerConfiguration.buttonAssignments[assignedIndex]);
                    controllerConfiguration.buttonAssignments.RemoveAt(assignedIndex + 1);
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Moving available actions to the top");
            }

            // Then move Talk to Crew Chief back to the top
            string talkToCrewChief = "Undef";
            int talkToCrewChiefIndex = -1;
            try
            {
                talkToCrewChief = Configuration.getUIString("talk_to_crew_chief");
                talkToCrewChiefIndex = controllerConfiguration.buttonAssignments.FindIndex(r => r.getInfo().StartsWith(talkToCrewChief));
                if (talkToCrewChiefIndex > 0)
                {
                    controllerConfiguration.buttonAssignments.Insert(0, controllerConfiguration.buttonAssignments[talkToCrewChiefIndex]);
                    controllerConfiguration.buttonAssignments.RemoveAt(talkToCrewChiefIndex + 1);
                }
                else if (talkToCrewChiefIndex != 0) // (it's not already at the top)
                {
                     Log.Warning($"Did not find 'Talk to Crew Chief' in button assignments, index: {talkToCrewChiefIndex}");
                }
            }
            catch (Exception e)
            {
                Log.Warning("Failed to move 'Talk to Crew Chief' back to the top");
                Log.Warning($"talkToCrewChief: '{talkToCrewChief}' talkToCrewChiefIndex: {talkToCrewChiefIndex}");
                Log.Warning($"controllerConfiguration.buttonAssignments.Count: {controllerConfiguration?.buttonAssignments?.Count}");
            }

            foreach (ControllerConfiguration.ButtonAssignment assignment in controllerConfiguration.buttonAssignments)
            {
                this.buttonActionSelect.Items.Add(Utilities.Strings.FirstLetterToUpper(assignment.getInfo()));
            }
        }

        private void assignButtonToActionClick(object sender, EventArgs e)
        {
            if (!isAssigningButton)
            {
                if (this.controllersList.SelectedIndex >= 0 && this.buttonActionSelect.SelectedIndex >= 0)
                {
                    isAssigningButton = true;
                    this.assignButtonToAction.Text = Configuration.getUIString("waiting_for_button_click_to_cancel");
                    ThreadStart assignButtonWork = assignButton;
                    ThreadManager.UnregisterTemporaryThread(assignButtonThread);
                    assignButtonThread = new Thread(assignButtonWork);
                    assignButtonThread.Name = "MainWindow.assignButtonThread";
                    ThreadManager.RegisterTemporaryThread(assignButtonThread);
                    assignButtonThread.Start();
                }
            }
            else
            {
                isAssigningButton = false;
                controllerConfiguration.listenForAssignment = false;
                this.assignButtonToAction.Text = Configuration.getUIString("assign");
            }
        }

        private bool initialiseSpeechEngine()
        {
            try
            {
                if (crewChief.speechRecogniser != null && !crewChief.speechRecogniser.initialised)
                {
                    crewChief.speechRecogniser.initialiseSpeechEngine();
                    Console.WriteLine("Attempted to initialise speech engine - success = " + crewChief.speechRecogniser.initialised);
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Unable to create speech engine, error message: ");
                runListenForChannelOpenThread = false;
            }
            //make sure we disable everything that might have been enabled in case speech engine fails
            if (!crewChief.speechRecogniser.initialised)
            {

                voiceDisableButton.Checked = true;
                runListenForChannelOpenThread = false;
                runListenForButtonPressesThread = controllerConfiguration.listenForButtons(false);
                voiceOption = VoiceOptionEnum.DISABLED;

                // Turns out saving prefs takes 5% of main thread time on startup, so don't do it
                // as we just read this from prefs.
                if (!this.constructingWindow)
                {
                    UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                    UserSettings.GetUserSettings().saveUserSettings();
                }
            }
            return crewChief.speechRecogniser.initialised;
        }

        private void assignButton()
        {
            lock (this.controllerWriteLock)
            {
                if (controllerConfiguration.assignButton(this, this.controllersList.SelectedIndex, this.buttonActionSelect.SelectedIndex))
                {
                    isAssigningButton = false;
                    controllerConfiguration.saveSettings();
                    runListenForChannelOpenThread = controllerConfiguration.listenForChannelOpen() && voiceOption != VoiceOptionEnum.DISABLED;
                    if (runListenForChannelOpenThread)
                    {
                        if (initialiseSpeechEngine())
                        {
                            runListenForButtonPressesThread = controllerConfiguration.listenForButtons(voiceOption == VoiceOptionEnum.TOGGLE);
                        }
                    }
                }
                else
                {
                    isAssigningButton = false;
                }
            }

            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    if (MainWindow.instance != null)
                    {
                        this.updateActions();
                        this.assignButtonToAction.Text = Configuration.getUIString("assign");
                    }
                });
            }
            catch (Exception)
            {
                // Shutdown.
            }
        }

        private void deleteAssignmentButtonClicked(object sender, EventArgs e)
        {
            if (this.buttonActionSelect.SelectedIndex >= 0)
            {
                this.controllerConfiguration.buttonAssignments[this.buttonActionSelect.SelectedIndex].unassign();
                updateActions();
                runListenForChannelOpenThread = controllerConfiguration.listenForChannelOpen();
                runListenForButtonPressesThread = controllerConfiguration.listenForButtons(voiceOption == VoiceOptionEnum.TOGGLE);
            }
            controllerConfiguration.saveSettings();
        }

        private void editPropertiesButtonClicked(object sender, EventArgs e)
        {
            // If minized to tray, hide tray icon while properties dialog is shown,
            // and it again when dialog is gone.  The goal is to prevent weird scenarios while
            // option dialog is visible.
            var minimizedToTray = this.notificationTrayIcon.Visible;
            if (minimizedToTray)
                this.notificationTrayIcon.Visible = false;

            try
            {
                var form = new PropertiesForm(this);
                form.ShowDialog(this);
            }
            finally
            {
                if (minimizedToTray)
                    this.notificationTrayIcon.Visible = true;
            }
        }

        private void forceVersionCheckButtonClicked(object sender, EventArgs e)
        {
            try
            {
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree("Software\\Britton IT Ltd");
            }
            catch
            {
            }
            doRestart(Configuration.getUIString("the_application_must_be_restarted_to_check_for_updates"), Configuration.getUIString("check_for_updates_title"), true);
        }

        public static string VersionInfo()
        {
            return MainWindow.instance != null ? $"{MainWindow.instance.app_version.Text}" : "V" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private bool hasNetwork =>
            this.gameDefinitionList.Text.Equals(GameDefinition.pCarsNetwork.friendlyName) || 
            this.gameDefinitionList.Text.Equals(GameDefinition.pCars2Network.friendlyName) ||
            this.gameDefinitionList.Text.Equals(GameDefinition.ams2Network.friendlyName);

        private void reacquireControllerList()
        {
            Debug.Assert(!this.InvokeRequired);
            this.controllerConfiguration.reacquireControllers();

            if (MainWindow.instance != null)
            {
                if (hasNetwork)
                {
                    controllerConfiguration.addNetworkControllerToList();
                }

                this.updateControllersUi();

                runListenForChannelOpenThread = controllerConfiguration.listenForChannelOpen()
                    && voiceOption == VoiceOptionEnum.HOLD && crewChief.speechRecogniser != null && crewChief.speechRecogniser.initialised;

                updateActions();
            }
        }

        private void refreshControllerList()
        {
            Debug.Assert(this.InvokeRequired);
            this.controllerConfiguration.scanControllers();

            try
            {
                // VL: I can't come up with a deadlock scenario, but if this dealocks we could move it out of this.controllerWriteLock.
                this.Invoke((MethodInvoker)delegate
                {
                    if (MainWindow.instance != null)
                    {
                        if (hasNetwork)
                        {
                            controllerConfiguration.addNetworkControllerToList();
                        }

                        this.updateControllersUi();

                        runListenForChannelOpenThread = controllerConfiguration.listenForChannelOpen()
                            && voiceOption == VoiceOptionEnum.HOLD && crewChief.speechRecogniser != null && crewChief.speechRecogniser.initialised;

                        updateActions();

                        this.controllerConfiguration.scanInProgress = false;
                        this.scanControllers.Text = Configuration.getUIString("scan_for_controllers");
                        this.scanControllers.Enabled = true;
                    }
                });
            }
            catch (Exception)
            {
                // Shutdown.
            }
        }

        private void voiceDisableButton_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                runListenForChannelOpenThread = false;
                runListenForButtonPressesThread = controllerConfiguration.listenForButtons(false);
                voiceOption = VoiceOptionEnum.DISABLED;
                // Turns out saving prefs takes 5% of main thread time on startup, so don't do it
                // as we just read this from prefs.
                if (!this.constructingWindow)
                {
                    UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                    UserSettings.GetUserSettings().saveUserSettings();
                }
            }
        }

        private void triggerWordButton_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                try
                {
                    if (initialiseSpeechEngine())
                    {
                        runListenForChannelOpenThread = false;
                        runListenForButtonPressesThread = controllerConfiguration.listenForButtons(false);
                        crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.TRIGGER_WORD;
                        voiceOption = VoiceOptionEnum.TRIGGER_WORD;
                        UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                        UserSettings.GetUserSettings().saveUserSettings();
                    }
                    else
                    {
                        ((RadioButton)sender).Checked = false;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to initialise speech engine, message = " + ex.Message);
                }
            }
        }

        private void holdButton_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                try
                {
                    if(initialiseSpeechEngine())
                    {
                        runListenForButtonPressesThread = controllerConfiguration.listenForButtons(false);
                        crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.HOLD;
                        voiceOption = VoiceOptionEnum.HOLD;
                        runListenForChannelOpenThread = true;
                        UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                        UserSettings.GetUserSettings().saveUserSettings();
                    }
                    else
                    {
                        ((RadioButton)sender).Checked = false;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to initialise speech engine, message = " + ex.Message);
                }
            }
        }
        private void toggleButton_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                try
                {
                    if(initialiseSpeechEngine())
                    {
                        runListenForButtonPressesThread = true;
                        runListenForChannelOpenThread = false;
                        crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.TOGGLE;
                        voiceOption = VoiceOptionEnum.TOGGLE;
                        UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                        UserSettings.GetUserSettings().saveUserSettings();
                    }
                    else
                    {
                        ((RadioButton)sender).Checked = false;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to initialise speech engine, message = " + ex.Message);
                }
            }
        }
        private void alwaysOnButton_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                try
                {
                    if(initialiseSpeechEngine())
                    {
                        runListenForChannelOpenThread = false;
                        runListenForButtonPressesThread = controllerConfiguration.listenForButtons(false);
                        crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.ALWAYS_ON;
                        voiceOption = VoiceOptionEnum.ALWAYS_ON;
                        UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                        UserSettings.GetUserSettings().saveUserSettings();
                    }
                    else
                    {
                        ((RadioButton)sender).Checked = false;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to initialise speech engine, message = " + ex.Message);
                }
            }
        }

        private void listenIfNotPressed_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                try
                {
                    if (initialiseSpeechEngine())
                    {
                        runListenForChannelOpenThread = false;
                        runListenForButtonPressesThread = controllerConfiguration.listenForButtons(false);
                        crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.NOT_PRESSED;
                        voiceOption = VoiceOptionEnum.NOT_PRESSED;
                        UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                        UserSettings.GetUserSettings().saveUserSettings();
                    }
                    else
                    {
                        ((RadioButton)sender).Checked = false;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to initialise speech engine, message = " + ex.Message);
                }
            }
        }
        private void messagesAudioDeviceSelected(object sender, EventArgs e)
        {
            if(internalMessageAudioRefresh)
            {
                return;
            }
            Tuple<string, int> device = null;
            if (AudioPlayer.playbackDevices.TryGetValue(this.messagesAudioDeviceBox.Text, out device))
            {
                int deviceId = device.Item2;
                AudioPlayer.naudioMessagesPlaybackDeviceId = deviceId;
                AudioPlayer.naudioMessagesPlaybackDeviceGuid = device.Item1;

                UserSettings.GetUserSettings().setProperty("NAUDIO_DEVICE_GUID_MESSAGES",
                    AudioPlayer.playbackDevices[this.messagesAudioDeviceBox.Text].Item1);
                UserSettings.GetUserSettings().saveUserSettings();
            }
            sizeComboBox(messagesAudioDeviceBox);
        }

        public void refreshMessageAudioDeviceBox()
        {
            internalMessageAudioRefresh = true;
            this.messagesAudioDeviceBox.Items.Clear();
            this.messagesAudioDeviceBox.Items.AddRange(AudioPlayer.playbackDevices.Keys.ToArray());
            foreach (var dev in AudioPlayer.playbackDevices)
            {
                if (dev.Value.Item2 == AudioPlayer.naudioMessagesPlaybackDeviceId)
                {
                    this.messagesAudioDeviceBox.Text = dev.Key;
                }
            }
            internalMessageAudioRefresh = false;
        }

        private void speechRecognitionDeviceSelected(object sender, EventArgs e)
        {
            if(internalSpeechRecognitionRefresh)
            {
                return;
            }
            Tuple<string, int> device = null;
            if (SpeechRecogniser.speechRecognitionDevices.TryGetValue(this.speechRecognitionDeviceBox.Text, out device))
            {
                int deviceId = device.Item2;
                crewChief.speechRecogniser.changeInputDevice(deviceId);
                UserSettings.GetUserSettings().setProperty("NAUDIO_RECORDING_DEVICE_GUID",
                    SpeechRecogniser.speechRecognitionDevices[this.speechRecognitionDeviceBox.Text].Item1);
                UserSettings.GetUserSettings().saveUserSettings();
            }
            sizeComboBox(speechRecognitionDeviceBox);
        }

        public void refreshSpeechRecognitionDeviceBox()
        {
            internalSpeechRecognitionRefresh = true;
            this.speechRecognitionDeviceBox.Items.Clear();
            this.speechRecognitionDeviceBox.Items.AddRange(SpeechRecogniser.speechRecognitionDevices.Keys.ToArray());
            foreach (var dev in SpeechRecogniser.speechRecognitionDevices)
            {
                if (dev.Value.Item2 == SpeechRecogniser.speechInputDeviceIndex)
                {
                    this.speechRecognitionDeviceBox.Text = dev.Key;
                }
            }
            internalSpeechRecognitionRefresh = false;
        }

        private void backgroundAudioDeviceSelected(object sender, EventArgs e)
        {
            if (internalBackgroundAudioRefresh)
            {
                return;
            }
            Tuple<string, int> device = null;
            if (AudioPlayer.playbackDevices.TryGetValue(this.backgroundAudioDeviceBox.Text, out device))
            {
                int deviceId = device.Item2;
                AudioPlayer.naudioBackgroundPlaybackDeviceId = deviceId;
                AudioPlayer.naudioBackgroundPlaybackDeviceGuid = device.Item1;
                UserSettings.GetUserSettings().setProperty("NAUDIO_DEVICE_GUID_BACKGROUND",
                    AudioPlayer.playbackDevices[this.backgroundAudioDeviceBox.Text].Item1);
                UserSettings.GetUserSettings().saveUserSettings();
            }
            sizeComboBox(backgroundAudioDeviceBox);
        }

        public void refreshBackgroundAudioDeviceBox()
        {
            internalBackgroundAudioRefresh = true;
            this.backgroundAudioDeviceBox.Items.Clear();
            this.backgroundAudioDeviceBox.Items.AddRange(AudioPlayer.playbackDevices.Keys.ToArray());
            foreach (var dev in AudioPlayer.playbackDevices)
            {
                if (dev.Value.Item2 == AudioPlayer.naudioBackgroundPlaybackDeviceId)
                {
                    this.backgroundAudioDeviceBox.Text = dev.Key;
                }
            }
            internalBackgroundAudioRefresh = false;
        }
        private void chiefNameSelected(object sender, EventArgs e)
        {
            if (!UserSettings.GetUserSettings().getString("chief_name").Equals(this.chiefNameBox.Text))
            {
                UserSettings.GetUserSettings().setProperty("chief_name", this.chiefNameBox.Text);
                UserSettings.GetUserSettings().saveUserSettings();
                doRestart(Configuration.getUIString("the_application_must_be_restarted_to_load_the_new_sounds"), Configuration.getUIString("load_new_sounds"));
            }
        }

        private void spotterNameSelected(object sender, EventArgs e)
        {
            if (!UserSettings.GetUserSettings().getString("spotter_name").Equals(this.spotterNameBox.Text))
            {
                UserSettings.GetUserSettings().setProperty("spotter_name", this.spotterNameBox.Text);
                UserSettings.GetUserSettings().saveUserSettings();
                doRestart(Configuration.getUIString("the_application_must_be_restarted_to_load_the_new_sounds"), Configuration.getUIString("load_new_sounds"));
            }
        }

        private void codriverNameSelected(object sender, EventArgs e)
        {
            if (!UserSettings.GetUserSettings().getString("codriver_name").Equals(this.codriverNameBox.Text))
            {
                UserSettings.GetUserSettings().setProperty("codriver_name", this.codriverNameBox.Text);
                UserSettings.GetUserSettings().saveUserSettings();
                doRestart(Configuration.getUIString("the_application_must_be_restarted_to_load_the_new_sounds"), Configuration.getUIString("load_new_sounds"));
            }
        }

        private void codriverStyleSelected(object sender, EventArgs e)
        {
            var cs = (MainWindow.CoDriverStyleEntry)this.codriverStyleBox.SelectedItem;
            if (UserSettings.GetUserSettings().getInt("codriver_style") != (int)cs.style)
            {
                UserSettings.GetUserSettings().setProperty("codriver_style", (int)cs.style);
                UserSettings.GetUserSettings().saveUserSettings();
            }
        }

        private VoiceOptionEnum getVoiceOptionEnum(String enumStr)
        {
            VoiceOptionEnum enumVal = VoiceOptionEnum.DISABLED;
            if (enumStr != null && enumStr.Length > 0)
            {
                enumVal = (VoiceOptionEnum)VoiceOptionEnum.Parse(typeof(VoiceOptionEnum), enumStr, true);
            }
            return enumVal;
        }

        private String getVoiceOptionString()
        {
            return voiceOption.ToString();
        }

        public enum VoiceOptionEnum
        {
            DISABLED, HOLD, TOGGLE, ALWAYS_ON, TRIGGER_WORD, NOT_PRESSED
        }

        private void populateControlListUI()
        {
            if (controllerConfiguration != null)
            {
                if (hasNetwork)
                {
                    controllerConfiguration.addNetworkControllerToList();
                }
                else
                {
                    controllerConfiguration.removeNetworkControllerFromList();
                }
                updateControllersUi();
            }
        }
        /// <summary>
        /// Side effect: sets GlobalBehaviourSettings.racingType
        /// Side effect: restarts CC if switching between race and rally game
        /// </summary>
        private void updateSelectedGameDefinition(object sender, EventArgs e)
        {
            if (this.gameDefinitionList.Text.Length > 0)
            {
                try
                {
                    var prevRacingType = CrewChief.gameDefinition.racingType;
                    var prevGameDefinition = CrewChief.gameDefinition;
                    sizeComboBox(gameDefinitionList, 300);
                    CrewChief.gameDefinition = GameDefinition.getGameDefinitionForFriendlyName(this.gameDefinitionList.Text);

                    if (prevRacingType != CrewChief.RacingType.Undefined &&
                        prevRacingType != CrewChief.gameDefinition.racingType)
                    {
                        if (doRestart(Configuration.getUIString("the_application_must_be_restarted_to_switch_between_circuit_and_rally_racing"),
                            Configuration.getUIString("switch_racing_type"), removeSkipUpdates: false, mandatory: false, saveUserSettings: true))
                        {    // The user cancelled, bounce back to previous game
                            CrewChief.gameDefinition = prevGameDefinition;
                            this.gameDefinitionList.Text = CrewChief.gameDefinition.friendlyName;
                            return;
                        }
                    }

                    GlobalBehaviourSettings.racingType = CrewChief.gameDefinition.racingType;

                    if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
                    {
                        this.chiefNameLabel.Visible = true;
                        this.chiefNameBox.Visible = true;
                        this.spotterNameLabel.Visible = true;
                        this.spotterNameBox.Visible = true;
                        this.codriverNameLabel.Visible = false;
                        this.codriverNameBox.Visible = false;
                        this.codriverStyleLabel.Visible = false;
                        this.codriverStyleBox.Visible = false;
                        this.backgroundVolumeSlider.Visible = true;
                        this.backgroundVolumeSliderLabel.Visible = true;
                    }
                    else
                    {
                        this.mainWindowLayout.showRallyMode();
                        this.backgroundVolumeSlider.Visible = false;
                        this.backgroundVolumeSliderLabel.Visible = false;
                    }

                    CrewChief.fuelMultiplier = new CrewChief.FuelMultiplier(CrewChief.gameDefinition.fuelMultiplierType);

                    if (CrewChief.fuelMultiplier.showMultiplier)
                    {
                        mainWindowLayout.showFuelMultiplier();
                    }
                    else
                    {
                        mainWindowLayout.hideFuelMultiplier();
                    }
                }
                catch (Exception ee) {Log.Exception(ee);}
            }
            populateControlListUI();
        }

        /// <summary>
        /// Warn the user then restart CC
        /// </summary>
        /// <param name="warningMessage"></param>
        /// <param name="warningTitle"></param>
        /// <param name="removeSkipUpdates"></param>
        /// <param name="mandatory">Switching between race and rally modes</param>
        /// <returns>User cancelled</returns>
        internal Boolean doRestart(String warningMessage, String warningTitle, Boolean removeSkipUpdates = false, Boolean mandatory = false, Boolean saveUserSettings = false)
        {
            Boolean userCancelled = false;

            Log.Error(warningMessage);
            if (CrewChief.Debug.RunningUnderDebugger)
            {
                warningMessage = "The app must be restarted manually";
            }

            // Make app visible first.
            this.RestoreFromTray();

            if (MessageBox.Show(warningMessage, warningTitle,
                mandatory ? MessageBoxButtons.OK : MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                if (saveUserSettings)
                {
                    UserSettings.GetUserSettings().setProperty("last_game_definition", CrewChief.gameDefinition.commandLineName);
                    UserSettings.GetUserSettings().saveUserSettings();
                }
                if (Utilities.RestartApp(app_restart: true,
                                         removeSkipUpdates: removeSkipUpdates,
                                         removeProfile: mandatory,
                                         removeGame: mandatory))
                {
                    this.Close(); //to turn off current app
                }
            }
            else
            {
                userCancelled = true;
            }
            return userCancelled;
        }

        private void internetPanHandler(object sender, EventArgs e)
        {
            Process.Start("https://thecrewchief.org/misc.php?do=donate");
        }

        private void playSmokeTestSounds(object sender, EventArgs e)
        {
            if (crewChief.audioPlayer != null)
            {
                new SmokeTest(crewChief.audioPlayer).soundTestPlay(this.smokeTestTextBox.Lines);
            }
        }

        private void ScanControllers_Click(object sender, System.EventArgs e)
        {
            if (!this.controllerConfiguration.scanInProgress)
            {
                this.controllerConfiguration.scanInProgress = true;
                this.controllerRescanThreadWakeUpEvent.Set();
                this.scanControllers.Text = Configuration.getUIString("cancel_scan");
            }
            else
            {
                this.scanControllers.Enabled = false;
                this.controllerConfiguration.cancelScan();
            }
        }
        private void editCommandMacroButtonClicked(object sender, EventArgs e)
        {
            var form = new MacroEditor(this, this.controllerConfiguration);
            form.ShowDialog(this);
        }

        private void AddRemoveActions_Click(object sender, EventArgs e)
        {
            var form = new ActionEditor(this);
            form.ShowDialog(this);
        }

        public void buttonVRWindowSettings_Click(object sender, EventArgs e)
        {
            vrOverlayForm.ShowDialog(this);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        private void buttonMyName_Click(object sender, EventArgs e)
        {
            var win = new MyName_V(this, MyName.myName);
            win.ShowDialog(this);
        }
        public void SetButtonMyNameText()
        {
            this.buttonMyName.Text = $"{Configuration.getUIString("personalisation_label")} {MyName.myName}";
        }

        private string _processName;
        private GameDefinition _autoStartedGame;
        /// <summary>
        /// If "Enable game auto detect" is set this runs every 3 seconds to 
        /// automatically detect the active game process and start/stop CC accordingly.
        /// </summary>
        /// <remarks>This monitors the foreground process to detect when a
        /// game starts or exits. If a new game starts, CC starts.
        /// If the auto-detected game exits and 
        /// "Exit Crew Chief when auto-detected game exits" is set, CC also shuts
        /// down automatically.
        /// </remarks>
        private async void autoDetectTimer_Tick(object sender, EventArgs e)
        {
            if (!await WaitAppReady())
            {
                return;
            }
            try
            {
                // 1) Check if auto-started game has shut down
                if (IsAppRunning && _autoStartedGame != null && _processName != null)
                {
                    Process[] processes = Process.GetProcessesByName(_autoStartedGame.processName);
                    if (!processes.Any(p => p.ProcessName == _autoStartedGame.processName))
                    {
                        Log.Commentary($"Auto-started game {_autoStartedGame.friendlyName} has shut down");
                        stopCrewChief();
                        _autoStartedGame = null;
                        if (UserSettings.GetUserSettings().getBoolean("enable_auto_detect_exit_cc"))
                        {
                            Log.Commentary($"Auto detected game exited, shutting down Crew Chief");
                            Application.DoEvents(); // Allow log to be written to
                            CloseCC();
                        }
                    }
                }
                // 2) Check if new game has started
                var fWinPtr = NativeMethods.GetForegroundWindow();
                if (fWinPtr != IntPtr.Zero)
                {
                    NativeMethods.GetWindowThreadProcessId(fWinPtr, out var pid);
                    var p = Process.GetProcessById((int)pid);
                    if (p != null)
                    {
                        string name;
                        try
                        {
                            name = p.MainModule.ModuleName;
                            // Ignore CC itself and Visual Studio (devenv)
                            var CC = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                            if (name.Contains(CC) || name.Contains("devenv"))
                                return;
                        }
                        catch (Win32Exception ex )
                        {
                            //  iRacing often throws an error so use ProcessName as a back up
                            try
                            {
                                name = p.ProcessName;
                            }
                            catch (Exception exception)
                            {
                                //fail to get process , try next time
                                return;
                            }
                        }
                        if (name != _processName)
                        {
                            _processName = name;
                            Log.Debug(() => $"Auto start foreground process changed to {name}");
                        }
                        var pName = autoStartRegex.Replace(name, string.Empty);
                        var defs = GameDefinition.getAllAvailableGameDefinitions(false);
                        var currentApplication = defs.Where(x => x.processName == pName).FirstOrDefault();
                        if (currentApplication != null)
                        { // A game is the foreground process
                            var currentSelected = GameDefinition.getGameDefinitionForFriendlyName(gameDefinitionList.Text);
                            if (currentSelected == null || currentSelected.processName != currentApplication.processName)
                            { // 3) The game has just been detected that is not the one selected
                                if (await WaitAppReady())
                                {
                                    if (IsAppRunning)
                                    { // 3a) Stop running CC on the game
                                        Log.Commentary($"Stop {currentSelected.friendlyName} and auto start {currentApplication.friendlyName}");
                                        stopCrewChief();
                                    }
                                    if (await WaitAppReady())
                                    {
                                        try
                                        {
                                            gameDefinitionList.SelectedItem = currentApplication.friendlyName;
                                        }
                                        catch (Exception)
                                        {
                                            Log.Warning($"gameDefinitionList.SelectedItem {SourceInfoHelpers.SourceFile()}:{SourceInfoHelpers.SourceLineNumber() - 4}");
                                        }
                                        Log.Commentary($"Auto start {currentApplication.friendlyName}");
                                        runCrewChief();  // 3b) Start running CC on the new game
                                        _autoStartedGame = currentApplication;
                                    }
                                }
                            }
                            else if (!IsAppRunning && currentSelected?.processName == currentApplication.processName)
                            { // 4) The selected game started
                                if (await WaitAppReady())
                                {
                                    Log.Commentary($"Auto start {currentApplication.friendlyName}");
                                    runCrewChief();
                                    _autoStartedGame = currentApplication;
                                }
                            }
                            // else 5) Focus returned to existing game 
                        }
                    }
                }
            }
            // catch anything here, log and shutdown the timer
            catch (Exception exception)
            {
                Console.WriteLine("Unable to auto-detect running game: " + exception.Message + " " + exception.StackTrace);
                ((System.Windows.Forms.Timer)sender).Stop();
            }

            /// <summary>
            /// Wait for the app to be ready before trying to start/stop CC, 
            /// otherwise we can end up with multiple auto-detect timers 
            /// running at the same time and stepping on each other.
            /// </summary>
            async Task<bool> WaitAppReady()
            {
                var i = 0;
                while (!startApplicationButton.Enabled && i++ < 5)
                {
                    await Task.Delay(500);
                }
                return startApplicationButton.Enabled;
            }
        }

        private void recordSession_CheckedChanged(object sender, EventArgs e)
        {
            CrewChief.Debug.UserTraceLogging = recordSession.CheckState == CheckState.Checked;
        }

        /// <summary>
        /// This button is enabled if there is a sound update.
        /// It's used in debug mode to demonstrate how the sound update is shown/hidden in the
        /// console window space
        /// </summary>
        private bool demoToggle;
        private void buttonSoundPackUpdate_Click(object sender, EventArgs e)
        {
            if (CrewChief.Debug.RunningUnderDebugger)
            {
                demoToggle = !demoToggle;
                if (demoToggle)
                {
                    mainWindowLayout.showSoundUpdate();
                }
                else
                {
                    mainWindowLayout.hideSoundUpdate();
                }
            }
        }

        private void numericUpDownfuelMultiplier_ValueChanged(object sender, EventArgs e)
        {
            // Set focus to another control otherwise it's not readable
            startApplicationButton.Focus();
        }
    }


    static class NativeMethods
    {
        [DllImport("winmm.dll")]
        public static extern int waveOutGetVolume(IntPtr hwo, out uint dwVolume);

        [DllImport("winmm.dll")]
        public static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, out uint ProcessId);
    }
    class ForegroundWindowManager
    {
        private IntPtr _savedWindow;

        public void SaveForegroundWindow()
        {
            _savedWindow = NativeMethods.GetForegroundWindow();
        }

        public bool RestoreForegroundWindow()
        {
            if (_savedWindow == IntPtr.Zero || !NativeMethods.IsWindow(_savedWindow))
                return false;

            return NativeMethods.SetForegroundWindow(_savedWindow);
        }
    }
}

namespace CrewChiefV4 // has to be CrewChiefV4.Tracepoints for automated discovery
{
    public partial class Tracepoints
    {
        public class MainWindow
        {
            public static void talkToChief()
            {
                if (TracepointIsChecked("Tracepoints/MainWindow/talkToChief"))
                {
                    Log.Debug("Talk to chief");
                }
            }
            public static void endTalkToChief()
            {
                if (TracepointIsChecked("Tracepoints/MainWindow/endTalkToChief"))
                {
                    Log.Debug("End talk to chief");
                }
            }
        }
    }
}
