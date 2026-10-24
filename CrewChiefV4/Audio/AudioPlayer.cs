using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Media;
using CrewChiefV4.Events;
using System.Windows.Media;
using System.Collections.Specialized;
using CrewChiefV4.GameState;
using System.Collections;
using System.Runtime.Remoting.Contexts;
using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;
using System.Runtime.InteropServices;
using CrewChiefV4.UserInterface.Models;

namespace CrewChiefV4.Audio
{
    public class AudioPlayer
    {
        public const String PAUSE_ID = "insert_pause";

        public Boolean disablePearlsOfWisdom = false;   // used for the last 2 laps / 3 minutes of a race session only
        public Boolean mute = false;

        public static readonly Boolean playWithNAudio = UserSettings.GetUserSettings().getBoolean("use_naudio");

        public static Boolean delayMessagesInHardParts = UserSettings.GetUserSettings().getBoolean("enable_delayed_messages_on_hardparts");
        private readonly Boolean allowImportantMessagesInKeepQuietMode = UserSettings.GetUserSettings().getBoolean("allow_important_messages_even_when_silenced");
        private readonly Boolean channelCloseBeepEnabled = UserSettings.GetUserSettings().getBoolean("enable_on_hold_close_channel_beep");

        public enum TTS_OPTION { NEVER, ONLY_WHEN_NECESSARY, ANY_TIME }
        public static TTS_OPTION ttsOption = TTS_OPTION.ONLY_WHEN_NECESSARY;

        public enum NAUDIO_OUTPUT_INTERFACE { WAVEOUT, WASAPI };

        public static int naudioMessagesPlaybackDeviceId = 0;
        public static int naudioBackgroundPlaybackDeviceId = 0;

        public static string naudioMessagesPlaybackDeviceGuid = "";
        public static string naudioBackgroundPlaybackDeviceGuid = "";

        public static Dictionary<string, Tuple<string, int>> playbackDevices = new Dictionary<string, Tuple<string, int>>();

        public const String folderAcknowlegeOK = "acknowledge/OK";
        public const String folderYellowEnabled = "acknowledge/yellowEnabled";
        public const String folderYellowDisabled = "acknowledge/yellowDisabled";
        public const String folderAcknowlegeEnableKeepQuiet = "acknowledge/keepQuietEnabled";
        public const String folderAcknowlegeDisableKeepQuiet = "acknowledge/keepQuietDisabled";
        public const String folderDidntUnderstand = "acknowledge/didnt_understand";
        public const String folderNoData = "acknowledge/no_data";
        public const String folderNotOnTrack = "acknowledge/not_on_track";
        public const String folderNotInRace = "acknowledge/not_in_race";
        public const String folderNotInRaceAction = "acknowledge/not_in_race_action";
        public const String folderNoMoreData = "acknowledge/no_more_data";
        public const String folderYes = "acknowledge/yes";
        public const String folderNo = "acknowledge/no";
        public const String folderDeltasEnabled = "acknowledge/deltasEnabled";
        public const String folderDeltasDisabled = "acknowledge/deltasDisabled";
        public const String folderRadioCheckResponse = "acknowledge/radio_check";
        public const String folderStandBy = "acknowledge/stand_by";
        public const String folderFuelToEnd = "acknowledge/fuel_to_end";
        public const String folderCutWarningsDisabled = "acknowledge/cut_warnings_disabled";
        public const String folderCutWarningsEnabled = "acknowledge/cut_warnings_enabled";

        public const String folderBreathIn = "acknowledge/breath_in";

        public const String folderAcknowledgeEnableDelayInHardParts = "acknowledge/keep_quiet_in_corners_enabled";
        public const String folderAcknowledgeDisableDelayInHardParts = "acknowledge/keep_quiet_in_corners_disabled";

        private const String folderRants = "rants/general";
        private Boolean playedRantInThisSession = false;
        public static Boolean rantWaitingToPlay = false;
        public static readonly Boolean enableRants = UserSettings.GetUserSettings().getBoolean("enable_rants");
        private static float rantLikelihood = enableRants ? 0.1f : 0;

        public static readonly Boolean useAlternateBeeps = UserSettings.GetUserSettings().getBoolean("use_alternate_beeps");
        public static readonly float pauseBetweenMessages = UserSettings.GetUserSettings().getFloat("pause_between_messages");
        public static readonly Boolean enableRadioBeeps = UserSettings.GetUserSettings().getBoolean("enable_radio_beeps");
        
        private QueuedMessage lastMessagePlayed = null;

        private Boolean allowPearlsOnNextPlay = true;
        private Dictionary<String, int> playedMessagesCount = new Dictionary<String, int>();

        public Boolean monitorRunning = false;

        private Boolean keepQuiet = false;
        private Boolean channelOpen = false;

        // hack... this is set to 'false' on session end to ensure a pending spotter message doesn't clog the audio player
        public Boolean holdChannelOpen = false;
        private Boolean useShortBeepWhenOpeningChannel = false;

        private TimeSpan maxTimeToHoldEmptyChannelOpen = TimeSpan.FromSeconds(UserSettings.GetUserSettings().getInt("spotter_hold_repeat_frequency") + 1);
        private DateTime timeOfLastMessageEnd = DateTime.MinValue;

        private readonly Boolean useListenBeep = UserSettings.GetUserSettings().getBoolean("use_listen_beep");
        private readonly Boolean useListenEndBeep = UserSettings.GetUserSettings().getBoolean("use_listen_end_beep");

        private readonly TimeSpan minTimeBetweenPearlsOfWisdom = TimeSpan.FromSeconds(UserSettings.GetUserSettings().getInt("minimum_time_between_pearls_of_wisdom"));

        private readonly Boolean sweary = UserSettings.GetUserSettings().getBoolean("use_sweary_messages");
        private Boolean useMaleSounds = !UserSettings.GetUserSettings().getBoolean("block_male_sounds");
        private readonly Boolean allowCaching = UserSettings.GetUserSettings().getBoolean("cache_sounds");

        private OrderedDictionary queuedClips = new OrderedDictionary();

        private OrderedDictionary immediateClips = new OrderedDictionary();

        private BackgroundPlayer backgroundPlayer;

        public static String soundFilesPath;
        private static String soundFilesPathTraineeChief;
        public static bool TraineeChiefAvailable { get; private set; }

        // Default voice pack root path.
        // Spotter files location does not depend on Chief voice location, and comes from the default voice pack, for now.
        public static String soundFilesPathNoChiefOverride;

        private String backgroundFilesPath;

        public const String dtmPitWindowOpenBackground = "dtm_pit_window_open.wav";

        public const String dtmPitWindowClosedBackground = "dtm_pit_window_closed.wav";

        private PearlsOfWisdom pearlsOfWisdom = new PearlsOfWisdom();

        DateTime timeLastPearlOfWisdomPlayed = DateTime.UtcNow;

        public Boolean initialised = false;

        public static String soundPackLanguage = null;

        private String lastImmediateMessageName = null;
        private DateTime lastImmediateMessageTime = DateTime.MinValue;

        private Boolean regularQueuePaused = false;

        private SoundCache soundCache;

        public const String NO_PERSONALISATION_SELECTED = "(non selected)";
        public String[] personalisationsArray = new String[] { NO_PERSONALISATION_SELECTED };

        public String selectedPersonalisation = NO_PERSONALISATION_SELECTED;

        private SynchronizationContext mainThreadContext = null;

        public const String defaultChiefId = "Jim (default)";
        public static List<String> availableChiefVoices = new List<String>();
        public static String folderChiefRadioCheck = null;
        private Thread monitorQueueThread = null;

        private AutoResetEvent monitorQueueWakeUpEvent = new AutoResetEvent(false);
        private AutoResetEvent hangingChannelCloseWakeUpEvent = new AutoResetEvent(false);
        private DateTime nextWakeupCheckTime = DateTime.MinValue;
        private Thread playDelayedImmediateMessageThread = null;
        private Thread pauseQueueThread = null;
        private Thread hangingChannelCloseThread = null;

        private MMDeviceEnumerator deviceEnum = null;
        private NotificationClientImplementation notificationClient = null;
        public static NAUDIO_OUTPUT_INTERFACE nAudioOutputInterface = NAUDIO_OUTPUT_INTERFACE.WAVEOUT;

        // this is used to determine whether we need to take a breath.
        private DateTime breathDueAt = DateTime.MaxValue;
        private const int maxSecondsBeforeTakingABreath = 3;

        // any messages with this magic string in their ID will not be purged on session end
        public const string RETAIN_ON_SESSION_END = "retain_on_session_end";

        private static Thread disposeAndRecreateThread = null;

        public struct WaveDevice
        {
            public int WaveDeviceId;
            public string FullName;
            public string EndpointGuid;
        }

        public const int DRV_QUERYFUNCTIONINSTANCEID = (0x0800 + 17);
        public const int DRV_QUERYFUNCTIONINSTANCEIDSIZE = (0x0800 + 18);

        [DllImport("winmm.dll")]
        static extern Int32 waveOutGetNumDevs();
        [DllImport("winmm.dll")]
        static extern Int32 waveOutMessage(IntPtr hWaveOut, int uMsg, out int dwParam1, IntPtr dwParam2);
        [DllImport("winmm.dll")]
        static extern Int32 waveOutMessage(IntPtr hWaveOut, int uMsg, IntPtr dwParam1, int dwParam2);

        public static string GetWaveOutEndpointId(int devNumber)
        {
            int cbEndpointId;
            string result = string.Empty;
            waveOutMessage((IntPtr)devNumber, DRV_QUERYFUNCTIONINSTANCEIDSIZE, out cbEndpointId, IntPtr.Zero);
            IntPtr strPtr = Marshal.AllocHGlobal(cbEndpointId);
            waveOutMessage((IntPtr)devNumber, DRV_QUERYFUNCTIONINSTANCEID, strPtr, cbEndpointId);
            result = Marshal.PtrToStringAuto(strPtr);
            Marshal.FreeHGlobal(strPtr);
            return result;
        }

        public static List<WaveDevice> GetWaveOutDevices()
        {
            List<WaveDevice> retVal = new List<WaveDevice>();
            try
            {
                var devEnum = new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var dev in devEnum)
                {
                    try
                    {
                        WaveDevice di = new WaveDevice()
                        {
                            EndpointGuid = dev.ID,
                            FullName = dev.FriendlyName,
                            WaveDeviceId = -1,
                        };

                        for (int waveOutIdx = 0; waveOutIdx < devEnum.Count; waveOutIdx++)
                        {
                            string guid = GetWaveOutEndpointId(waveOutIdx);
                            if (guid == di.EndpointGuid)
                            {
                                di.WaveDeviceId = waveOutIdx;
                                break;
                            }
                        }
                        retVal.Add(di);
                    }
                    catch (Exception)
                    {
                        // ignore - we'll log an error later if we can't get any devices
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Error enumerating nAudio wave out devices: " + e.ToString());
            }
            if (retVal.Count == 0)
            {
                Log.Error("Unable to initialise any nAudio playback devices, try setting \" nAudio Playback\" to false in the properties screen -" +
                    "the app will use the Windows Sounds default playback device instead");
            }
            return retVal;
        }
        public static string GetDefaultOutputDeviceName()
        {
            Debug.Assert(!MainWindow.instance.InvokeRequired);
            foreach (var device in playbackDevices)
            {
                if (device.Value.Item2 == 0)
                {
                    return device.Key;
                }
            }
            return null;
        }

        public static string GetDefaultOutputDeviceID()
        {
            Debug.Assert(!MainWindow.instance.InvokeRequired);
            foreach (var device in playbackDevices)
            {
                if (device.Value.Item2 == 0)
                {
                    return device.Value.Item1;
                }
            }
            return null;
        }

        public static string GetDefaultInputDeviceName()
        {
            Debug.Assert(MainWindow.instance == null || !MainWindow.instance.InvokeRequired); // Null check required for unit tests
            foreach (var device in SpeechRecogniser.speechRecognitionDevices)
            {
                if (device.Value.Item2 == 0)
                {
                    return device.Key;
                }
            }
            return null;
        }

        public static string GetDefaultInputDeviceId()
        {
            Debug.Assert(MainWindow.instance == null || !MainWindow.instance.InvokeRequired); // Null check required for unit tests
            foreach (var device in SpeechRecogniser.speechRecognitionDevices)
            {
                if (device.Value.Item2 == 0)
                {
                    return device.Value.Item1;
                }
            }
            return null;
        }

        public static void UpdateUI()
        {
            if (MainWindow.instance != null)
            { // (Not running unit tests)
                Debug.Assert(!MainWindow.instance.InvokeRequired);
                if (UserSettings.GetUserSettings().getBoolean("use_naudio"))
                {
                    MainWindow.instance.refreshMessageAudioDeviceBox();
                    MainWindow.instance.refreshBackgroundAudioDeviceBox();
                }
                if (UserSettings.GetUserSettings().getBoolean("use_naudio_for_speech_recognition"))
                {
                    MainWindow.instance.refreshSpeechRecognitionDeviceBox();
                }
                else if (GlobalResources.audioPlayer != null)
                {
                    GlobalResources.audioPlayer.notificationClient.GetSpeechRecognitionDevices();
                    MainWindow.instance.mainWindowLayout.refreshDefaultSpeechRecognitionDevice(GetDefaultInputDeviceName());
                }
            }
        }

        /// <summary>
        /// Handle changes in input/output sound devices while CC is running
        /// </summary>
        class NotificationClientImplementation : IMMNotificationClient
        {
            private AudioPlayer audioPlayer;
            private bool playbackEnabled = false;
            private bool speechEnabled = false;

            private bool processingDeviceChanges = false;

            // Windows is stupid, if default device is changed the index in WaveOut devices changes, so we need to update the indecies(I think it's called) in playbackDevices
            // Find our currently selected devices for both backgoundplayer and messageplayer.
            private void ReloadBackgroundPlayer()
            {
                Debug.Assert(!MainWindow.instance.InvokeRequired);

                if (nAudioOutputInterface == NAUDIO_OUTPUT_INTERFACE.WAVEOUT)
                    audioPlayer.backgroundPlayer = new NAudioBackgroundPlayerWaveOut(audioPlayer.mainThreadContext, audioPlayer.backgroundFilesPath, dtmPitWindowClosedBackground);
                else if (nAudioOutputInterface == NAUDIO_OUTPUT_INTERFACE.WASAPI)
                    audioPlayer.backgroundPlayer = new NAudioBackgroundPlayerWasapi(audioPlayer.mainThreadContext, audioPlayer.backgroundFilesPath, dtmPitWindowClosedBackground);
                else
                    Debug.Assert(false);

                try
                {
                    audioPlayer.backgroundPlayer.initialise(dtmPitWindowClosedBackground);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Unable to initialise nAudio background player: ");
                    Log.Warning("Using WindowsMediaPlayer for background sounds");
                    audioPlayer.backgroundPlayer = new MediaPlayerBackgroundPlayer(audioPlayer.mainThreadContext, audioPlayer.backgroundFilesPath, dtmPitWindowClosedBackground);
                    audioPlayer.backgroundPlayer.initialise(dtmPitWindowClosedBackground);
                }
            }

            void ReIndexAudioOutputDevices()
            {
                Debug.Assert(MainWindow.instance.InvokeRequired);

                Log.Sound("Re-indexing audio output devices...");
                lock (MainWindow.instanceLock)
                {
                    if (MainWindow.instance != null)
                    {
                        audioPlayer.mainThreadContext.Post(delegate
                        {
                            string messageDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_DEVICE_GUID_MESSAGES");
                            string backgroundDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_DEVICE_GUID_BACKGROUND");

                            playbackDevices.Clear();
                            foreach (var dev in GetWaveOutDevices())
                            {
                                int disambiguator = 2;
                                string disambiguatedFullName = dev.FullName;
                                while (playbackDevices.ContainsKey(disambiguatedFullName))
                                {
                                    disambiguatedFullName = dev.FullName + "(" + disambiguator + ")";
                                    disambiguator++;
                                }
                                playbackDevices[disambiguatedFullName] = new Tuple<string, int>(dev.EndpointGuid, dev.WaveDeviceId);
                                if (dev.EndpointGuid == messageDeviceGuid)
                                {
                                    naudioMessagesPlaybackDeviceId = dev.WaveDeviceId;
                                    naudioMessagesPlaybackDeviceGuid = dev.EndpointGuid;
                                }
                                if (dev.EndpointGuid == backgroundDeviceGuid)
                                {
                                    naudioBackgroundPlaybackDeviceId = dev.WaveDeviceId;
                                    naudioBackgroundPlaybackDeviceGuid = dev.EndpointGuid;
                                }
                            }
                            UpdateUI();
                        }, null);
                    }
                }
            }

            void ReIndexAudioInputDevices()
            {
                Debug.Assert(MainWindow.instance.InvokeRequired);
                Log.Sound("Re-indexing audio input devices...");
                lock (MainWindow.instanceLock)
                {
                    if (MainWindow.instance != null)
                    {
                        audioPlayer.mainThreadContext.Post(delegate
                        {
                            string recordingDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_RECORDING_DEVICE_GUID");

                            var devices = GetSpeechRecognitionDevices();
                            foreach (var dev in devices)
                                if (dev.EndpointGuid == recordingDeviceGuid)
                                {
                                    MainWindow.instance.crewChief.speechRecogniser.changeInputDevice(dev.WaveDeviceId);
                                    SpeechRecogniser.speechInputDeviceIndex = dev.WaveDeviceId;
                                }
                            UpdateUI();
                        }, null);
                    }
                }
            }

            internal List<AudioPlayer.WaveDevice> GetSpeechRecognitionDevices()
            {
                var devices = SpeechRecogniser.GetWaveInDevices();
                SpeechRecogniser.speechRecognitionDevices.Clear();
                foreach (var dev in devices)
                {
                    int disambiguator = 2;
                    string disambiguatedFullName = dev.FullName;
                    while (playbackDevices.ContainsKey(disambiguatedFullName))
                    {
                        disambiguatedFullName = dev.FullName + "(" + disambiguator + ")";
                        disambiguator++;
                    }
                    SpeechRecogniser.speechRecognitionDevices[disambiguatedFullName] = new Tuple<string, int>(dev.EndpointGuid, dev.WaveDeviceId);
                }
                return devices;
            }

            public void OnDefaultDeviceChanged(DataFlow dataFlow, Role deviceRole, string defaultDeviceId)
            {
                if (dataFlow == DataFlow.Render && playbackEnabled)
                {
                    ReIndexAudioOutputDevices();
                }
                else if(dataFlow == DataFlow.Capture && speechEnabled)
                {
                    ReIndexAudioInputDevices();
                }
            }

            public void OnDeviceAdded(string deviceId)
            {
                Log.Sound("OnDeviceAdded -->");
            }

            public void OnDeviceRemoved(string deviceId)
            {
                Log.Sound("OnDeviceRemoved -->");
            }

            public void OnDeviceStateChanged(string deviceId, DeviceState newState)
            {
                // this will be invoked once for each device in the audio device list. For me, this means 4 or 5 invocations when I start SteamVR.
                // It's OK to update the UI for each callback but we only want to recreate the sounds cache once. We can't know when the state change
                // callbacks will be completed, so just wait a short time after we get one before triggering the sound cache work
                if (!processingDeviceChanges)
                {
                    ThreadManager.UnregisterTemporaryThread(disposeAndRecreateThread);
                    processingDeviceChanges = true;
                    disposeAndRecreateThread = new Thread(() =>
                    {
                        Log.Sound("Sound cache will be reinitialised");
                        Thread.Sleep(300);
                        disposeAndRecreateSoundCache();
                        processingDeviceChanges = false;
                    });
                    ThreadManager.RegisterTemporaryThread(disposeAndRecreateThread);
                    disposeAndRecreateThread.Start();
                }

                // TODO_REMOVE: on first time we hit this and CC does not hang.
                Debug.Assert(MainWindow.instance.InvokeRequired);
                Log.Sound(() => $"Processing audio device state changes...");
                if (this.speechEnabled)
                {
                    this.ReIndexAudioInputDevices();
                    lock (MainWindow.instanceLock)
                    {
                        if (MainWindow.instance != null)
                        {
                            if (MainWindow.instance.InvokeRequired)
                            {
                                this.audioPlayer.mainThreadContext.Post(delegate
                                {
                                    this.ProcessInputDeviceStateChange(deviceId, newState);
                                }, null);
                            }
                            else
                            {
                                this.ProcessInputDeviceStateChange(deviceId, newState);
                            }
                        }
                    }
                }

                if (this.playbackEnabled)
                {
                    this.ReIndexAudioOutputDevices();
                    lock (MainWindow.instanceLock)
                    {
                        if (MainWindow.instance != null)
                        {
                            if (MainWindow.instance.InvokeRequired)
                            {
                                this.audioPlayer.mainThreadContext.Post(delegate
                                {
                                    this.ProcessOutputDeviceStateChange(deviceId, newState);
                                }, null);
                            }
                            else
                            {
                                this.ProcessOutputDeviceStateChange(deviceId, newState);
                            }
                        }
                    }
                }
            }

            private void disposeAndRecreateSoundCache()
            {
                lock (MainWindow.instanceLock)
                {
                    if (MainWindow.instance != null)
                    {
                        audioPlayer.mainThreadContext.Post(delegate
                        {
                            // clean up and dispose the current cache:

                            // Those will be allowed again when we re-create the SoundCache.
                            SoundCache.cancelLazyLoading = true;
                            SoundCache.cancelDriverNameLoading = true;
                            if (MainWindow.instance.crewChief.running)
                                audioPlayer.stopMonitor();

                            // Safe to check here because we are on the main thread.
                            if (SoundCache.cacheSoundsThread != null)
                            {
                                Log.Sound("Waiting for sound caching thread to stop...");
                                if (!SoundCache.cacheSoundsThread.Join(30000))
                                    Log.Warning($"Failed to wait for cacheSoundsThread while re-indexing audio devices.  Hide and hope for the best.");
                                else
                                    Log.Sound(() => $"Sound caching thread stopped.");
                            }

                            // Wait for various short lived caching threads to exit.
                            ThreadManager.WaitForTemporaryThreadsExit(waitMillis: 5000);

                            Log.Sound("Clearing sound cache");
                            if (audioPlayer.backgroundPlayer != null)
                                audioPlayer.backgroundPlayer.dispose();

                            audioPlayer.disposeSoundCache();

                            // create a new cache:
                            SoundCache.cancelLazyLoading = false;
                            SoundCache.cancelDriverNameLoading = false;
                            Log.Sound("Recreating sound cache");
                            audioPlayer.soundCache = new SoundCache(new DirectoryInfo(soundFilesPath),
                                soundFilesPathTraineeChief != null ? new DirectoryInfo(soundFilesPathTraineeChief) : null,
                                new DirectoryInfo(soundFilesPathNoChiefOverride),
                                new String[] { "spotter", "acknowledge" }, audioPlayer.sweary, audioPlayer.useMaleSounds, audioPlayer.allowCaching, audioPlayer.selectedPersonalisation, false);
                            ReloadBackgroundPlayer();

                            if (MainWindow.instance.crewChief.running)
                                audioPlayer.startMonitor(smokeTest: false);
                        }, null);
                    }
                }
            }

            private void ProcessOutputDeviceStateChange(string deviceId, DeviceState newState)
            {
                string messageDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_DEVICE_GUID_MESSAGES");
                string backgroundDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_DEVICE_GUID_BACKGROUND");

                if (backgroundDeviceGuid == deviceId || deviceId == messageDeviceGuid)
                {
                    if (newState == DeviceState.Disabled || newState == DeviceState.Unplugged || newState == DeviceState.NotPresent)
                    {
                        if (backgroundDeviceGuid == deviceId)
                        {
                            naudioBackgroundPlaybackDeviceId = 0;
                            naudioBackgroundPlaybackDeviceGuid = GetDefaultOutputDeviceID();
                            Log.Warning($"Selected background audio device removed, setting background playback to default device--> {GetDefaultOutputDeviceName()}");
                        }
                        if (messageDeviceGuid == deviceId)
                        {
                            naudioMessagesPlaybackDeviceId = 0;
                            naudioMessagesPlaybackDeviceGuid = GetDefaultOutputDeviceID();
                            Log.Warning($"Selected message audio device removed, setting voice playback to default device--> {GetDefaultOutputDeviceName()}");
                        }
                    }
                    else if (newState == DeviceState.Active)
                    {
                        foreach (var device in playbackDevices)
                        {
                            if (backgroundDeviceGuid == device.Value.Item1)
                            {
                                Log.Sound(() => $"Saved background audio device added, setting sound playback to saved device--> {device.Key}");
                                naudioBackgroundPlaybackDeviceId = device.Value.Item2;
                                naudioBackgroundPlaybackDeviceGuid = device.Value.Item1;
                            }
                            if (messageDeviceGuid == device.Value.Item1)
                            {
                                Log.Sound(() => $"Saved message audio device added, setting sound playback to saved device--> {device.Key}");
                                naudioMessagesPlaybackDeviceId = device.Value.Item2;
                                naudioMessagesPlaybackDeviceGuid = device.Value.Item1;
                            }
                        }
                    }
                    UpdateUI();
                }
                else
                {
                    Log.Sound("Output audio device state change is ignored.");
                }
            }

            private void ProcessInputDeviceStateChange(string deviceId, DeviceState newState)
            {
                string recordingDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_RECORDING_DEVICE_GUID");

                if (recordingDeviceGuid == deviceId)
                {
                    if (newState == DeviceState.Disabled || newState == DeviceState.Unplugged || newState == DeviceState.NotPresent)
                    {
                        MainWindow.instance.crewChief.speechRecogniser.changeInputDevice(0);
                        SpeechRecogniser.speechInputDeviceIndex = 0;
                        Log.Warning($"Selected speech input device removed, setting sound input to default device--> {GetDefaultInputDeviceName()}");
                    }
                    else if (newState == DeviceState.Active)
                    {
                        foreach (var device in SpeechRecogniser.speechRecognitionDevices)
                        {
                            if (recordingDeviceGuid == device.Value.Item1)
                            {
                                MainWindow.instance.crewChief.speechRecogniser.changeInputDevice(device.Value.Item2);
                                SpeechRecogniser.speechInputDeviceIndex = device.Value.Item2;
                                Log.Warning($"Saved speech input device added, setting sound input to saved device--> {device.Key}");
                            }
                        }
                    }
                    UpdateUI();
                }
                else
                {
                    Log.Sound("Input audio device state change is ignored.");
                }
            }

            public NotificationClientImplementation(AudioPlayer audioPlayer, bool playbackEnabled, bool speechEnabled)
            {
                this.audioPlayer = audioPlayer;
                this.playbackEnabled = playbackEnabled;
                this.speechEnabled = speechEnabled;
                if (System.Environment.OSVersion.Version.Major < 6)
                {
                    throw new NotSupportedException("This functionality is only supported on Windows Vista or newer.");
                }
            }

            public void OnPropertyValueChanged(string deviceId, PropertyKey propertyKey)
            {}
        }

        /// <summary>
        /// Check if a sound file exists, and return the default if it doesn't.
        /// Can be used if a phrase would be useful but hasn't been recorded yet.
        /// </summary>
        /// <param name="defaultFileName">defaults to folderAcknowlegeOK</param>
        public static string phraseIfItHasBeenRecorded(String soundFileName, string defaultFileName = folderAcknowlegeOK)
        {
            return File.Exists(Path.Combine(soundFilesPath, soundFileName)) ?
                soundFileName : 
                defaultFileName;
        }
        static AudioPlayer()
        {
            // Inintialize sound file paths.  Handle user specified override, or pick default.
            string soundPackLocationOverride = UserSettings.GetUserSettings().getString("override_default_sound_pack_location");
            soundFilesPath = soundFilesPathNoChiefOverride = DataFiles.SoundFilesFolder;
            if (!string.IsNullOrWhiteSpace(soundPackLocationOverride))
            {
                try
                {
                    if (Directory.Exists(soundPackLocationOverride))
                    {
                        soundFilesPath = soundFilesPathNoChiefOverride = soundPackLocationOverride;
                    }
                    else
                    {
                        Log.Error("Specified sound pack override folder " + soundPackLocationOverride + " doesn't exist, using default");
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Unable to set override sound folder ");
                }
            }

            // Process alternative Chief voice locations.
            availableChiefVoices.Clear();
            availableChiefVoices.Add(defaultChiefId);
            try
            {
                DirectoryInfo altVoicePackDirectory = new DirectoryInfo(AudioPlayer.soundFilesPath + "/alt/");
                if (altVoicePackDirectory.Exists)
                {
                    DirectoryInfo[] directories = altVoicePackDirectory.GetDirectories();
                    foreach (DirectoryInfo folder in directories)
                    {
                        if (!availableChiefVoices.Contains(folder.Name))
                        {
                            availableChiefVoices.Add(folder.Name);
                        }
                        else
                        {
                            Log.Error("Skipping alternative voice pack directory, because it already exists in a list: " + folder.FullName);
                        }
                    }
                }

                string selectedChief = UserSettings.GetUserSettings().getString("chief_name");
                if (!String.IsNullOrWhiteSpace(selectedChief) && !defaultChiefId.Equals(selectedChief))
                {
                    if (Directory.Exists(AudioPlayer.soundFilesPath + "/alt/" + selectedChief))
                    {
                        Log.Commentary("Using Chief voice: " + selectedChief);
                        AudioPlayer.soundFilesPath = AudioPlayer.soundFilesPath + "/alt/" + selectedChief;

                        // Prefer test_chief folder, and fall back to test if it doesn't exist.
                        if (Directory.Exists(AudioPlayer.soundFilesPathNoChiefOverride + "/voice/radio_check_" + selectedChief + "/test_chief"))
                        {
                            folderChiefRadioCheck = "radio_check_" + selectedChief + "/test_chief";
                        }
                        else if (Directory.Exists(AudioPlayer.soundFilesPathNoChiefOverride + "/voice/radio_check_" + selectedChief + "/test"))
                        {
                            folderChiefRadioCheck = "radio_check_" + selectedChief + "/test";
                        }
                    }
                    else
                    {
                        Log.Error("No Chief called " + selectedChief + " exists, dropping back to the default (Jim)");
                        UserSettings.GetUserSettings().setProperty("chief_name", defaultChiefId);
                        UserSettings.GetUserSettings().saveUserSettings();
                    }
                }
                else
                {
                    // Prefer test_chief folder, and fall back to test if it doesn't exist.
                    if (Directory.Exists(AudioPlayer.soundFilesPathNoChiefOverride + "/voice/radio_check/test_chief"))
                    {
                        folderChiefRadioCheck = "radio_check/test_chief";
                    }
                    else if (Directory.Exists(AudioPlayer.soundFilesPathNoChiefOverride + "/voice/radio_check/test"))
                    {
                        folderChiefRadioCheck = "radio_check/test";
                    }
                }

                // Trainee chief sound files path is "LocalApplicationData"/CrewChiefV4/Sounds/alt/"trainee name",
                // typically C:\Users\"user"\AppData\Local\CrewChiefV4\Sounds\alt\Jerry
                // but may be overwritten by Property "Trainee chief". If that is blank or "Jim" then the Sounds folder is used.
                string traineeChiefPath = UserSettings.GetUserSettings().getString("trainee_chief_path");
                TraineeChiefAvailable = false;
                if (String.IsNullOrWhiteSpace(traineeChiefPath)
                    || traineeChiefPath.ToLower().Equals("jim")
                    || (!String.IsNullOrWhiteSpace(selectedChief) && traineeChiefPath.ToLower().Equals(selectedChief.ToLower())))
                {   // default
                }
                else if (Directory.Exists(traineeChiefPath))
                {   // full path
                    soundFilesPathTraineeChief = traineeChiefPath;
                    TraineeChiefAvailable = true;
                }
                else
                {  // e.g. "Jerry"
                    soundFilesPathTraineeChief = Path.Combine(soundFilesPathNoChiefOverride, "alt", traineeChiefPath);
                    if (Directory.Exists(soundFilesPathTraineeChief))
                    {
                        TraineeChiefAvailable = true;
                    }
                }
                if (TraineeChiefAvailable)
                {
                    Log.Commentary("Using 2nd Chief voice: " + soundFilesPathTraineeChief);
                }
                else
                {
                    soundFilesPathTraineeChief = null;
                }
            }
            catch (Exception)
            {
                Log.Error("No Chief voice sound folders available");
            }

            Enum.TryParse(UserSettings.GetUserSettings().getString("tts_setting_listprop"), out ttsOption);
            Debug.Assert(Enum.IsDefined(typeof(TTS_OPTION), ttsOption));

            // Initialize optional nAudio playback.
            if (UserSettings.GetUserSettings().getBoolean("use_naudio"))
            {
                Enum.TryParse(UserSettings.GetUserSettings().getString("naudio_output_interface_listprop"), out nAudioOutputInterface);

                string messageDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_DEVICE_GUID_MESSAGES");
                string backgroundDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_DEVICE_GUID_BACKGROUND");
                bool foundMessagePlayBackDevice = false;
                bool foundBackgroundAudioDevice = false;
                //Debug.Assert(!MainWindow.instance.InvokeRequired);
                playbackDevices.Clear();
                List<WaveDevice> devices = GetWaveOutDevices();
                foreach (var dev in devices)
                {
                    try
                    {
                        NAudio.Wave.WaveOutCapabilities capabilities = NAudio.Wave.WaveOut.GetCapabilities(dev.WaveDeviceId);
                        // Update legacy audio device "GUID" to MMdevice guid which does does contain a unique GUID
                        if (messageDeviceGuid.Contains(capabilities.ProductName))
                        {
                            UserSettings.GetUserSettings().setProperty("NAUDIO_DEVICE_GUID_MESSAGES", dev.EndpointGuid);
                            UserSettings.GetUserSettings().saveUserSettings();
                        }
                        if (backgroundDeviceGuid.Contains(capabilities.ProductName))
                        {
                            UserSettings.GetUserSettings().setProperty("NAUDIO_DEVICE_GUID_BACKGROUND", dev.EndpointGuid);
                            UserSettings.GetUserSettings().saveUserSettings();
                        }
                        int disambiguator = 2;
                        string disambiguatedFullName = dev.FullName;
                        while (playbackDevices.ContainsKey(disambiguatedFullName))
                        {
                            disambiguatedFullName = dev.FullName + "(" + disambiguator + ")";
                            disambiguator++;
                        }
                        Log.Sound(() => $"Device name: {disambiguatedFullName} Guid: {dev.EndpointGuid} DeviceWaveId {dev.WaveDeviceId}");
                        playbackDevices[disambiguatedFullName] = new Tuple<string, int>(dev.EndpointGuid, dev.WaveDeviceId);
                    }
                    catch (Exception ex)
                    {
                        Log.DontSpam("Error occurred while calling GetCapabilities: " + ex.Message).Warning();
                    }
                }
                // Check that A device has been selected
                if (String.IsNullOrEmpty(messageDeviceGuid))
                {
                    messageDeviceGuid = GetDefaultOutputDeviceID();
                    UserSettings.GetUserSettings().setProperty("NAUDIO_DEVICE_GUID_MESSAGES", messageDeviceGuid);
                    UserSettings.GetUserSettings().saveUserSettings();
                    AudioPlayer.UpdateUI();
                    Log.Error($"No message audio output device selected, setting to default: {GetDefaultOutputDeviceName()}");
                }
                if (String.IsNullOrEmpty(backgroundDeviceGuid))
                {
                    backgroundDeviceGuid = GetDefaultOutputDeviceID();
                    UserSettings.GetUserSettings().setProperty("NAUDIO_DEVICE_GUID_BACKGROUND", backgroundDeviceGuid);
                    UserSettings.GetUserSettings().saveUserSettings();
                    AudioPlayer.UpdateUI();
                    Log.Error($"No background audio output device selected, setting to default: {GetDefaultOutputDeviceName()}");
                }
                foreach (var dev in playbackDevices)
                {
                    if (dev.Value.Item1 == messageDeviceGuid)
                    {
                        Log.Sound(() => $"Detected saved message audio output device: {dev.Key}");
                        foundMessagePlayBackDevice = true;
                    }
                    if (dev.Value.Item1 == backgroundDeviceGuid)
                    {
                        foundBackgroundAudioDevice = true;
                        Log.Sound(() => $"Detected saved background audio output device: {dev.Key}");
                    }
                }
                if (!foundMessagePlayBackDevice)
                {
                    Log.Error($"Unable to find saved message audio output device, using default: {GetDefaultOutputDeviceName()}.  Note: this could be caused by a driver reinstall or an OS feature update.  If you keep seeing this message, re-select the desired device in the UI.");
                }
                if (!foundBackgroundAudioDevice)
                {
                    Log.Error($"Unable to find saved background audio output device, using default: {GetDefaultOutputDeviceName()}.  Note: this could be caused by a driver reinstall or an OS feature update.  If you keep seeing this message, re-select the desired device in the UI.");
                }
            }
        }

        public AudioPlayer()
        {
            GlobalResources.audioPlayer = this;
            this.mainThreadContext = SynchronizationContext.Current;
            deviceEnum = new MMDeviceEnumerator();
            notificationClient = new NotificationClientImplementation(this, UserSettings.GetUserSettings().getBoolean("use_naudio"), UserSettings.GetUserSettings().getBoolean("use_naudio_for_speech_recognition"));
            deviceEnum.RegisterEndpointNotificationCallback(notificationClient);

            // Only update main pack for now?
            DirectoryInfo soundDirectory = initialiseSoundPackVersions();
            if (!soundDirectory.Exists)
            {
                soundDirectory.Create();
            }

            soundDirectory = new DirectoryInfo(soundFilesPath);
            if (soundDirectory.Exists)
            {
                // Pick language from possibly overriden voice location.
                soundPackLanguage = getSoundPackLanguage(soundDirectory);
            }
            if (soundPackLanguage == null)
            {
                soundPackLanguage = "en";  // Default to Queen's English, or Northern?
            }

            // populate the personalisations list
            DirectoryInfo personalisationsDirectory = new DirectoryInfo(soundFilesPath + @"\personalisations");
            if (personalisationsDirectory.Exists)
            {
                List<String> personalisationsList = new List<string>();
                personalisationsList.Add(NO_PERSONALISATION_SELECTED);
                foreach (DirectoryInfo folderInPersonalisationsDirectory in personalisationsDirectory.GetDirectories())
                {
                    personalisationsList.Add(folderInPersonalisationsDirectory.Name);
                }
                personalisationsArray = personalisationsList.ToArray();
            }
            if (MyName.myName != null && MyName.myName.Length > 0)
            {
                selectedPersonalisation = MyName.myName;
            }
            if (!UserSettings.GetUserSettings().getBoolean("use_naudio_for_speech_recognition"))
            {
                UpdateUI();
            }
        }

        public DirectoryInfo initialiseSoundPackVersions()
        {
            DirectoryInfo soundDirectory = new DirectoryInfo(soundFilesPathNoChiefOverride);
            if (soundDirectory.Exists)
            {
                SoundPackVersionsHelper.currentSoundPackVersion = getSoundPackVersion(soundDirectory);
                SoundPackVersionsHelper.currentDriverNamesVersion = getDriverNamesVersion(soundDirectory);
                SoundPackVersionsHelper.currentPersonalisationsVersion = getPersonalisationsVersion(soundDirectory);
            }
            return soundDirectory;
        }

        // if it's more than 200ms since the last call, and message have been queued, wake the monitor thread
        public void wakeMonitorThreadForRegularMessages(DateTime now)
        {
            if (now > nextWakeupCheckTime && queuedClips.Count > 0)
            {
                Boolean doHardPartsCheck = delayMessagesInHardParts &&
                            CrewChief.currentGameState != null &&
                            CrewChief.currentGameState.PositionAndMotionData.CarSpeed > 5 &&
                            (CrewChief.currentGameState.SessionData.SessionPhase == SessionPhase.Green || CrewChief.currentGameState.SessionData.SessionPhase == SessionPhase.Checkered) &&
                            !GameStateData.onManualFormationLap;
                if (!doHardPartsCheck ||
                    !CrewChief.currentGameState.hardPartsOnTrackData.isInHardPart(CrewChief.currentGameState.PositionAndMotionData.DistanceRoundTrack))
                {
                    monitorQueueWakeUpEvent.Set();
                }
                nextWakeupCheckTime = now.AddMilliseconds(200);
            }
        }

        // for debugging the moderator message block process
        public String getMessagesBlocking(SoundType blockLevel)
        {
            List<String> blockingMessages = new List<string>();
            lock (immediateClips)
            {
                foreach (Object entry in immediateClips.Values)
                {
                    QueuedMessage message = (QueuedMessage)entry;
                    if (message.metadata.type <= blockLevel)
                    {
                        blockingMessages.Add(message.messageName + "(" + message.metadata.type + ")");
                    }
                }
            }
            return String.Join(", ", blockingMessages);
        }

        public void initialise()
        {
            DirectoryInfo soundDirectory = new DirectoryInfo(soundFilesPath);
            backgroundFilesPath = Path.Combine(soundFilesPathNoChiefOverride, "background_sounds");

            if (UserSettings.GetUserSettings().getBoolean("use_naudio"))
            {
                Log.Sound(() => $"nAudio output interface: {nAudioOutputInterface}" + (nAudioOutputInterface == NAUDIO_OUTPUT_INTERFACE.WASAPI ? $"  Latency: {NAudioOutWasapi.wasapiLatency}ms" : ""));
                if (nAudioOutputInterface == NAUDIO_OUTPUT_INTERFACE.WAVEOUT)
                    this.backgroundPlayer = new NAudioBackgroundPlayerWaveOut(mainThreadContext, backgroundFilesPath, dtmPitWindowClosedBackground);
                else if (nAudioOutputInterface == NAUDIO_OUTPUT_INTERFACE.WASAPI)
                    this.backgroundPlayer = new NAudioBackgroundPlayerWasapi(mainThreadContext, backgroundFilesPath, dtmPitWindowClosedBackground);
                else
                    Debug.Assert(false);

                try
                {
                    this.backgroundPlayer.initialise(dtmPitWindowClosedBackground);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Unable to initialise nAudio background player: ");
                    Log.Warning("Using WindowsMediaPlayer for background sounds");
                    this.backgroundPlayer = new MediaPlayerBackgroundPlayer(mainThreadContext, backgroundFilesPath, dtmPitWindowClosedBackground);
                    this.backgroundPlayer.initialise(dtmPitWindowClosedBackground);
                }
            }
            else
            {
                this.backgroundPlayer = new MediaPlayerBackgroundPlayer(mainThreadContext, backgroundFilesPath, dtmPitWindowClosedBackground);
                this.backgroundPlayer.initialise(dtmPitWindowClosedBackground);
            }

            if (!soundDirectory.Exists)
            {
                Log.Error("Unable to find sound directory " + soundDirectory.FullName);
                return;
            }
            if (soundFilesPathTraineeChief != null && !Directory.Exists(soundFilesPathTraineeChief))
            {
                Log.Error("Unable to find sound directory (2nd crew chief) " + soundFilesPathTraineeChief);
                return;
            }

            if (SoundPackVersionsHelper.currentSoundPackVersion == -1)
            {
                Log.Error("Unable to get sound pack version");
            }
            else
            {
                Log.Sound(() => "Using sound pack version " + SoundPackVersionsHelper.currentSoundPackVersion +
                    ", driver names version " + SoundPackVersionsHelper.currentDriverNamesVersion +
                    " and personalisations version " + SoundPackVersionsHelper.currentPersonalisationsVersion);
            }
            if (this.soundCache == null)
            {
                soundCache = new SoundCache(new DirectoryInfo(soundFilesPath),
                    soundFilesPathTraineeChief != null ? new DirectoryInfo(soundFilesPathTraineeChief) : null,
                    new DirectoryInfo(soundFilesPathNoChiefOverride),
                    new String[] { "spotter", "acknowledge" }, sweary, useMaleSounds, allowCaching, selectedPersonalisation, true);
            }
            initialised = true;
            PlaybackModerator.SetAudioPlayer(this);
        }

        public SoundCache getSoundCache()
        {
            return soundCache;
        }

        public void startMonitor(bool smokeTest = true)
        {
            if (monitorRunning)
            {
                Log.Sound("Monitor is already running");
            }
            else
            {
                Log.Sound("Starting queue monitor");
                this.monitorRunning = true;
                // spawn a Thread to monitor the queue
                Debug.Assert(monitorQueueThread == null);

                // This thread is managed by the Chief Run thread directly.
                monitorQueueThread = new Thread(monitorQueue);
                monitorQueueThread.Name = "AudioPlayer.monitorQueueThread";
                monitorQueueThread.Start();
            }

            if (smokeTest)
            {
                new SmokeTest(this).trigger(new GameStateData(DateTime.UtcNow.Ticks), new GameStateData(DateTime.UtcNow.Ticks));
            }

        }

        public void stopMonitor()
        {
            monitorRunning = false;
            monitorQueueWakeUpEvent.Set();
            stopHangingChannelCloseThread();
            // Wait for monitor queue thread to exit.
            if (monitorQueueThread != null)
            {
                if (monitorQueueThread.IsAlive)
                {
                    Log.Sound("Waiting for queue monitor to stop...");
                    if (!monitorQueueThread.Join(5000))
                    {
                        var msg = "Warning: Timed out waiting for queue monitor to stop";
                        Log.Sound(msg);
                        Debug.WriteLine(msg);
                    }
                }
                monitorQueueThread = null;
                Log.Sound("Monitor queue stopped");
            }
            channelOpen = false;
        }

        public float getSoundPackVersion(DirectoryInfo soundDirectory)
        {
            FileInfo[] filesInSoundDirectory = soundDirectory.GetFiles();
            float version = -1;
            foreach (FileInfo fileInSoundDirectory in filesInSoundDirectory)
            {
                if (fileInSoundDirectory.Name == "sound_pack_version_info.txt")
                {
                    String[] lines = File.ReadAllLines(fileInSoundDirectory.FullName/*Path.Combine(soundFilesPath, fileInSoundDirectory.Name)*/);
                    foreach (String line in lines)
                    {
                        if (float.TryParse(line, out version))
                        {
                            return version;
                        }
                    }
                    break;
                }
            }
            return version;
        }

        public String getSoundPackLanguage(DirectoryInfo soundDirectory)
        {
            FileInfo[] filesInSoundDirectory = soundDirectory.GetFiles();
            foreach (FileInfo fileInSoundDirectory in filesInSoundDirectory)
            {
                if (fileInSoundDirectory.Name == "sound_pack_language.txt")
                {
                    String[] lines = File.ReadAllLines(Path.Combine(soundFilesPath, fileInSoundDirectory.Name));
                    if (lines.Length > 0)
                    {
                        return lines[0];
                    }
                }
            }
            return null;
        }

        public float getDriverNamesVersion(DirectoryInfo soundDirectory)
        {
            DirectoryInfo[] directories = soundDirectory.GetDirectories();
            float version = -1;

            foreach (DirectoryInfo folderInSoundDirectory in directories)
            {
                if (folderInSoundDirectory.Name == "driver_names")
                {
                    FileInfo[] filesInDriverNamesDirectory = folderInSoundDirectory.GetFiles();
                    foreach (FileInfo fileInDriverNameDirectory in filesInDriverNamesDirectory)
                    {
                        if (fileInDriverNameDirectory.Name == "driver_names_version_info.txt")
                        {
                            String[] lines = File.ReadAllLines(fileInDriverNameDirectory.FullName);
                            foreach (String line in lines)
                            {
                                if (float.TryParse(line, out version))
                                {
                                    return version;
                                }
                            }
                            break;
                        }
                    }
                    break;
                }
            }
            return version;
        }

        public float getPersonalisationsVersion(DirectoryInfo soundDirectory)
        {
            DirectoryInfo[] directories = soundDirectory.GetDirectories();
            float version = -1;

            foreach (DirectoryInfo folderInSoundDirectory in directories)
            {
                if (folderInSoundDirectory.Name == "personalisations")
                {
                    FileInfo[] filesInDriverNamesDirectory = folderInSoundDirectory.GetFiles();
                    foreach (FileInfo fileInDriverNameDirectory in filesInDriverNamesDirectory)
                    {
                        if (fileInDriverNameDirectory.Name == "personalisations_version_info.txt")
                        {
                            String[] lines = File.ReadAllLines(fileInDriverNameDirectory.FullName);
                            foreach (String line in lines)
                            {
                                if (float.TryParse(line, out version))
                                {
                                    return version;
                                }
                            }
                            break;
                        }
                    }
                    break;
                }
            }
            return version;
        }

        public void setBackgroundSound(String backgroundSoundName)
        {
            this.backgroundPlayer.setBackgroundSound(backgroundSoundName);
        }

        public void muteBackgroundPlayer(bool doMute)
        {
            this.backgroundPlayer.mute(doMute);
        }

        public void disposeBackgroundPlayer()
        {
            if (this.backgroundPlayer != null)
            {
                backgroundPlayer.dispose();
            }
        }

        private void monitorQueue()
        {
            Log.Sound("Monitor starting");
            while (monitorRunning)
            {
                int waitTimeout = -1;
                DateTime now = CrewChief.currentGameState == null ? DateTime.UtcNow : CrewChief.currentGameState.Now;
                if (channelOpen && !SoundCache.IS_PLAYING && (!holdChannelOpen || now > timeOfLastMessageEnd + maxTimeToHoldEmptyChannelOpen))
                {
                    if (!queueHasDueMessages(queuedClips, false) && !queueHasDueMessages(immediateClips, true))
                    {
                        holdChannelOpen = false;
                        stopHangingChannelCloseThread();
                        closeRadioInternalChannel();
                    }
                }
                if (immediateClips.Count > 0)
                {
                    if (!SpeechRecogniser.waitingForSpeech || SpeechRecogniser.respondWhileChannelIsStillOpen || MainWindow.voiceOption != MainWindow.VoiceOptionEnum.HOLD)
                    {
                        try
                        {
                            playQueueContents(immediateClips, true);
                        }
                        catch (Exception e)
                        {
                            Log.Exception(e, "Exception processing immediate clips: ");
                            lock (immediateClips)
                            {
                                immediateClips.Clear();
                            }
                        }
                    }
                    waitTimeout = 10;
                }
                else if (!regularQueuePaused && queuedClips.Count > 0 && !holdChannelOpen /* don't allow regular messages to play if we're holding the channel open*/)
                {
                    try
                    {
                        playQueueContents(queuedClips, false);
                        allowPearlsOnNextPlay = true;
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Exception processing queued clips: ");
                        lock (queuedClips)
                        {
                            queuedClips.Clear();
                        }
                    }
                    waitTimeout = 10;
                }
                // -1 timeout means wait-till-notified. 10 timeout kicks the loop off again almost immediately. This
                // should happen once after playing messages, to allow the channel to be closed. After the channel is closed
                // we're back to a -1 timeout
                monitorQueueWakeUpEvent.WaitOne(waitTimeout);
            }

            lock (queuedClips)
            {
                queuedClips.Clear();
            }
            lock (immediateClips)
            {
                immediateClips.Clear();
            }

            playedMessagesCount.Clear();

            try
            {
                // This can throw on device disconnect.
                this.backgroundPlayer.stop();
            }
            catch (Exception e) { Log.Exception(e); }
        }

        private void writeMessagePlayedStats()
        {
            Log.Sound("Count, event name");
            foreach (KeyValuePair<String, int> entry in playedMessagesCount)
            {
                Log.Sound(() => entry.Value + " instances of event " + entry.Key);
            }
        }

        public void enableKeepQuietMode()
        {
            playMessageImmediately(new QueuedMessage(folderAcknowlegeEnableKeepQuiet, 0));
            keepQuiet = true;
        }

        public void disableKeepQuietMode()
        {
            playMessageImmediately(new QueuedMessage(folderAcknowlegeDisableKeepQuiet, 0));
            keepQuiet = false;
        }

        private bool messageFunctionIsEmptyOrTrue(QueuedMessage queuedMessage)
        {
            // if we have a function, execute it and return the resut, otherwise return true
            if (CrewChief.currentGameState != null && queuedMessage.triggerFunction != null)
            {
                return queuedMessage.triggerFunction.Invoke(CrewChief.currentGameState);
            }
            else
            {
                return true;
            }
        }

        private void playQueueContents(OrderedDictionary queueToPlay, Boolean isImmediateMessages)
        {
            long milliseconds = GameStateData.CurrentTime.Ticks / TimeSpan.TicksPerMillisecond;
            List<String> keysToPlay = new List<String>();
            List<String> soundsProcessed = new List<String>();

            Boolean oneOrMoreEventsEnabled = false;

            QueuedMessage higherPriorityDelayedMessage = null;

            lock (queueToPlay)
            {
                int willBePlayedCount = queueToPlay.Count;
                String firstMovableEventWithPrefixOrSuffix = null;
                foreach (String key in queueToPlay.Keys)
                {
                    QueuedMessage queuedMessage = (QueuedMessage)queueToPlay[key];
                    if (isImmediateMessages || queuedMessage.dueTime <= milliseconds)
                    {
                        Boolean blockedByKeepQuietMode;
                        if (allowImportantMessagesInKeepQuietMode)
                        {
                            blockedByKeepQuietMode = keepQuiet && !queuedMessage.playEvenWhenSilenced && !isImmediateMessages;
                        }
                        else
                        {
                            blockedByKeepQuietMode = keepQuiet && !queuedMessage.playEvenWhenSilenced &&
                                (queuedMessage.metadata == null ||
                                (queuedMessage.metadata.type != SoundType.SPOTTER && queuedMessage.metadata.type != SoundType.VOICE_COMMAND_RESPONSE));
                        }

                        Boolean blockedByDelayedHigherPriorityMessage = false;
                        if (higherPriorityDelayedMessage != null)
                        {
                            Debug.Assert(!AudioPlayer.delayMessagesInHardParts);
                            if (higherPriorityDelayedMessage.metadata.priority > queuedMessage.metadata.priority)
                            {
                                blockedByDelayedHigherPriorityMessage = true;
                            }
                        }

                        Boolean messageHasExpired = queuedMessage.expiryTime != 0 && queuedMessage.expiryTime < milliseconds;
                        Boolean messageIsStillValid = queuedMessage.isMessageStillValid(key, CrewChief.currentGameState);
                        Boolean queueTooLongForMessage = queuedMessage.maxPermittedQueueLengthForMessage != 0 && willBePlayedCount > queuedMessage.maxPermittedQueueLengthForMessage;
                        Boolean hasJustPlayedAsAnImmediateMessage = !isImmediateMessages && lastImmediateMessageName != null &&
                            key == lastImmediateMessageName && GameStateData.CurrentTime - lastImmediateMessageTime < TimeSpan.FromSeconds(5);
                        Boolean messageFunctionIsEmptyOrTrue = this.messageFunctionIsEmptyOrTrue(queuedMessage);
                        Boolean isAlreadyQueuedToPlay = keysToPlay.Contains(key);
                        // can we actually play the message?
                        if (!blockedByKeepQuietMode
                            && queuedMessage.canBePlayed
                            && !blockedByDelayedHigherPriorityMessage
                            && messageIsStillValid
                            && !isAlreadyQueuedToPlay
                            && !queueTooLongForMessage
                            && !messageHasExpired
                            && !hasJustPlayedAsAnImmediateMessage
                            && messageFunctionIsEmptyOrTrue)
                        {
                            // special case for 'get ready' event here - we don't want to move this to the top of the queue because
                            // it makes it sound shit. Bit of a hack, needs a better solution
                            if (firstMovableEventWithPrefixOrSuffix == null && key != LapCounter.folderGetReady && soundCache.eventHasPersonalisedPrefixOrSuffix(key))
                            {
                                firstMovableEventWithPrefixOrSuffix = key;
                            }
                            else
                            {
                                keysToPlay.Add(key);
                            }
                        }
                        else
                        {
                            if (blockedByKeepQuietMode)
                            {
                                Log.Sound(() => "Clip " + key + " will not be played because we're in 'keep quiet' mode");
                                soundsProcessed.Add(key);   // add this to the set processed so it's removed from the queue
                            }
                            else if (!messageIsStillValid)
                            {
                                Log.SoundDebug(() => "Clip " + key + " is no longer valid");
                                soundsProcessed.Add(key);   // add this to the set processed so it's removed from the queue
                            }
                            else if (messageHasExpired)
                            {
                                Log.SoundDebug(() => "Clip " + key + " has expired after being queued for " + queuedMessage.getAge() + " milliseconds");
                                soundsProcessed.Add(key);   // add this to the set processed so it's removed from the queue
                            }
                            else if (queueTooLongForMessage)
                            {
                                List<String> keysToDisplay = new List<string>();
                                foreach (String keyToDisplay in queueToPlay.Keys)
                                {
                                    keysToDisplay.Add(keyToDisplay);
                                }
                                Log.Warning("Queue is too long to play clip " + key + " max permitted items for this message = "
                                    + queuedMessage.maxPermittedQueueLengthForMessage + " queue: " + String.Join(", ", keysToDisplay));
                                soundsProcessed.Add(key);   // add this to the set processed so it's removed from the queue
                            }
                            else if (!queuedMessage.canBePlayed)
                            {
                                Log.Warning("Clip " + key + " has some missing sound files");
                                soundsProcessed.Add(key);   // add this to the set processed so it's removed from the queue
                            }
                            else if (hasJustPlayedAsAnImmediateMessage)
                            {
                                Log.SoundDebug(() => "Clip " + key + " has just been played in response to a voice command, skipping");
                                soundsProcessed.Add(key);   // add this to the set processed so it's removed from the queue
                            }
                            else if (blockedByDelayedHigherPriorityMessage)
                            {
                                Log.SoundDebug(() => "Clip " + key + "will not be played because higher priority message is waiting to be played: " + higherPriorityDelayedMessage.messageName);
                                soundsProcessed.Add(key);   // add this to the set processed so it's removed from the queue - TODO: can we leave this in the queue for the next iteration?
                            }
                            else if (isAlreadyQueuedToPlay)
                            {
                                Log.SoundDebug(() => "Clip " + key + " will not be played because it's already about to be played");
                                soundsProcessed.Add(key);   // add this to the set processed so it's removed from the queue
                            }
                            else if (!messageFunctionIsEmptyOrTrue)
                            {
                                // special case for messages which would otherwise be ready to play, but that have
                                // an associated function which hasn't (yet) evaluated to true
                                // Log.Sound("wait...");
                            }
                            willBePlayedCount--;
                        }
                    }
                    else if (queuedMessage.dueTime > milliseconds)
                    {
                        if (!AudioPlayer.delayMessagesInHardParts  // Do not delay messages based on priority if hard parts feature is in use.
                            && (higherPriorityDelayedMessage == null
                                || higherPriorityDelayedMessage.metadata.priority < queuedMessage.metadata.priority))
                        {
                            higherPriorityDelayedMessage = queuedMessage;
                        }
                    }

                    // if we've just processed a 'rant' here, set the flag to false
                    if (queuedMessage.isRant)
                    {
                        AudioPlayer.rantWaitingToPlay = false;
                    }
                }
                if (firstMovableEventWithPrefixOrSuffix != null)
                {
                    keysToPlay.Insert(0, firstMovableEventWithPrefixOrSuffix);
                }
                if (keysToPlay.Count > 0)
                {
                    if (keysToPlay.Count == 1 && clipIsPearlOfWisdom(keysToPlay[0]))
                    {
                        if (hasPearlJustBeenPlayed())
                        {
                            Log.SoundDebug(() => "Rejecting pearl of wisdom " + keysToPlay[0] +
                                " because one has been played in the last " + minTimeBetweenPearlsOfWisdom + " seconds");
                            soundsProcessed.Add(keysToPlay[0]);
                        }
                        else if (disablePearlsOfWisdom)
                        {
                            Log.SoundDebug(() => "Rejecting pearl of wisdom " + keysToPlay[0] +
                                   " because pearls have been disabled for the last phase of the race");
                            soundsProcessed.Add(keysToPlay[0]);
                        }
                    }
                    else
                    {
                        oneOrMoreEventsEnabled = true;
                    }
                }
            }

            Boolean wasInterrupted = false;
            if (oneOrMoreEventsEnabled)
            {
                PlaybackModerator.PreProcessAddedKeys(keysToPlay);

                openRadioChannelInternal();
                soundsProcessed.AddRange(playSounds(keysToPlay, isImmediateMessages, out wasInterrupted));
            }
            else
            {
                soundsProcessed.AddRange(keysToPlay);
            }
            if (soundsProcessed.Count > 0)
            {
                lock (queueToPlay)
                {
                    foreach (String key in soundsProcessed)
                    {
                        if (queueToPlay.Contains(key))
                        {
                            queueToPlay.Remove(key);
                        }
                    }
                }
            }

            // now we go back and play anything else that's been inserted into the queue since we started, but only if
            // we've not been interrupted
            if (queueHasDueMessages(queueToPlay, isImmediateMessages) && (isImmediateMessages || !wasInterrupted) && this.monitorRunning)
            {
                Log.SoundDebug(() => "There are " + queueToPlay.Count + " more events in the queue, playing them...");
                playQueueContents(queueToPlay, isImmediateMessages);
            }
        }

        private Boolean queueHasDueMessages(OrderedDictionary queueToCheck, Boolean isImmediateMessages)
        {
            if (isImmediateMessages)
            {
                // immediate messages can't be delayed so no point in checking their due times
                return queueToCheck.Count > 0;
            }
            else if (queueToCheck.Count == 0)
            {
                return false;
            }
            else
            {
                long milliseconds = GameStateData.CurrentTime.Ticks / TimeSpan.TicksPerMillisecond;
                lock (queueToCheck)
                {
                    foreach (String key in queueToCheck.Keys)
                    {
                        QueuedMessage message = (QueuedMessage)queueToCheck[key];
                        if (message.dueTime <= milliseconds && messageFunctionIsEmptyOrTrue(message))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        private List<String> playSounds(List<String> eventNames, Boolean isImmediateMessages, out Boolean wasInterrupted)
        {
            var _events = String.Join(", ", eventNames);
            if (_events.StartsWith("spotter_"))
            {
                Log.Spotter(_events);
            }
            else
            {
                Log.SoundDebug(() => "Playing sounds, events: " + _events);
            }
            List<String> soundsProcessed = new List<String>();

            OrderedDictionary thisQueue = isImmediateMessages ? immediateClips : queuedClips;
            wasInterrupted = false;
            int playedEventCount = 0;
            foreach (String eventName in eventNames)
            {
                if (!this.monitorRunning)
                {
                    return soundsProcessed;
                }

                // if there's anything in the immediateClips queue, stop processing
                if (isImmediateMessages || immediateClips.Count == 0)
                {
                    if (thisQueue.Contains(eventName))
                    {
                        QueuedMessage thisMessage = (QueuedMessage)thisQueue[eventName];
                        if (!isImmediateMessages && playedEventCount > 0 && pauseBetweenMessages > 0)
                        {
                            Log.SoundDebug(() => "Pausing before " + eventName);
                            Utilities.InterruptedSleep((int)Math.Round(pauseBetweenMessages * 1000.0f) /*totalWaitMillis*/, 10 /*waitWindowMillis*/, () => monitorRunning /*keepWaitingPredicate*/);
                        }
                        //  now double check this is still valid
                        if (!isImmediateMessages)
                        {
                            Boolean messageHasExpired = thisMessage.expiryTime != 0 && thisMessage.expiryTime < GameStateData.CurrentTime.Ticks / TimeSpan.TicksPerMillisecond; ;
                            Boolean messageIsStillValid = thisMessage.isMessageStillValid(eventName, CrewChief.currentGameState);
                            Boolean hasJustPlayedAsAnImmediateMessage = lastImmediateMessageName != null &&
                                eventName == lastImmediateMessageName && GameStateData.CurrentTime - lastImmediateMessageTime < TimeSpan.FromSeconds(5);
                            if (messageHasExpired || !messageIsStillValid || hasJustPlayedAsAnImmediateMessage)
                            {
                                Log.SoundDebug(() => "Skipping message " + eventName +
                                    ", messageHasExpired:" + messageHasExpired + ", messageIsStillValid:" + messageIsStillValid + ", hasJustPlayedAsAnImmediateMessage:" + hasJustPlayedAsAnImmediateMessage);
                                soundsProcessed.Add(eventName);
                                continue;
                            }
                        }
                        if (clipIsPearlOfWisdom(eventName))
                        {
                            soundsProcessed.Add(eventName);
                            if (hasPearlJustBeenPlayed())
                            {
                                Log.SoundDebug(() => "Rejecting pearl of wisdom " + eventName +
                                    " because one has been played in the last " + minTimeBetweenPearlsOfWisdom + " seconds");
                                continue;
                            }
                            else if (!allowPearlsOnNextPlay)
                            {
                                Log.SoundDebug(() => "Rejecting pearl of wisdom " + eventName +
                                    " because they've been temporarily disabled");
                                continue;
                            }
                            else if (disablePearlsOfWisdom)
                            {
                                Log.SoundDebug(() => "Rejecting pearl of wisdom " + eventName +
                                        " because pearls have been disabled for the last phase of the race");
                                continue;
                            }
                            else
                            {
                                timeLastPearlOfWisdomPlayed = GameStateData.CurrentTime;
                                String messageStringContent = thisMessage.ToString();
                                if (messageStringContent != "")
                                {
                                    Log.Sound(messageStringContent);
                                }
                                if (!mute || CrewChief.Debug.RunningUnderDebugger)
                                {
                                    soundCache.Play(eventName, thisMessage.metadata, mute);
                                    timeOfLastMessageEnd = GameStateData.CurrentTime;
                                }
                                else if (!MainWindow.playingBackTrace)
                                {
                                    Log.SoundDebug(() => "Skipping message " + eventName + " because we're muted");
                                }
                            }
                        }
                        else
                        {
                            if (thisMessage.messageName != AudioPlayer.folderDidntUnderstand &&
                                thisMessage.messageName != AudioPlayer.folderStandBy && thisMessage.metadata.type != SoundType.SPOTTER)
                            {
                                // only cache the last message for repeat if it's an actual message
                                lastMessagePlayed = thisMessage;
                            }
                            if (thisMessage.delayMessageResolution)
                            {
                                thisMessage.resolveDelayedContents();
                                if (!thisMessage.canBePlayed)
                                {
                                    Log.SoundDebug(() => "Delayed message " + eventName + " cannot be played (was invalidated while waiting)");
                                }
                            }
                            String messageStringContent = thisMessage.ToString();
                            if (!messageStringContent.Equals("") && !messageStringContent.Equals("()"))
                            {
                                Log.Sound(() => eventName + "=" + messageStringContent);
                            }
                            if (!mute || CrewChief.Debug.RunningUnderDebugger)
                            {
                                if (GlobalBehaviourSettings.enableBreathIn && !DriverTrainingService.isPlayingPaceNotes)
                                {
                                    if (thisMessage.metadata.type == SoundType.SPOTTER)
                                    {
                                        // reset the breath timer
                                        breathDueAt = DateTime.MaxValue;
                                    }
                                    else if (breathDueAt == DateTime.MaxValue)
                                    {
                                        breathDueAt = DateTime.UtcNow.AddSeconds(maxSecondsBeforeTakingABreath);
                                    }
                                    else
                                    {
                                        DateTime now = DateTime.UtcNow;
                                        if (now > breathDueAt)
                                        {
                                            Log.SoundDebug("Taking a breath");
                                            soundCache.Play(folderBreathIn, thisMessage.metadata, mute);
                                            breathDueAt = now.AddSeconds(maxSecondsBeforeTakingABreath);
                                        }
                                    }
                                }
                                if (thisMessage.canBePlayed)
                                {
                                    soundCache.Play(thisMessage.messageFolders, thisMessage.metadata, mute);
                                    timeOfLastMessageEnd = GameStateData.CurrentTime;
                                }
                            }
                            else if (!MainWindow.playingBackTrace)
                            {
                                Log.SoundDebug(() => "Skipping message " + eventName + " because we're muted");
                            }
                            int playedCount = -1;
                            if(playedMessagesCount.TryGetValue(eventName, out playedCount))
                            {
                                playedMessagesCount[eventName] = ++playedCount;
                            }
                            else
                            {
                                playedMessagesCount.Add(eventName, 1);
                            }
                            soundsProcessed.Add(eventName);
                        }
                        playedEventCount++;
                    }
                    else
                    {
                        Log.SoundDebug(() => "Event " + eventName + " is no longer in the queue");
                        if (CrewChief.Debug.RunningUnderDebugger)
                        {
                            string[] keysToDebug = new string[thisQueue.Keys.Count];
                            thisQueue.Keys.CopyTo(keysToDebug, 0);
                            Log.SoundDebug(() => "The " + (isImmediateMessages ? "immediate" : "regular") + " queue contains "
                                + String.Join(", ", keysToDebug));
                        }
                    }
                }
                else
                {
                    Log.SoundDebug(() => "We've been interrupted after playing " + playedEventCount + " events");
                    wasInterrupted = true;
                    break;
                }
            }
            return soundsProcessed;
        }

        private void openRadioChannelInternal()
        {
            if (!channelOpen)
            {
                breathDueAt = DateTime.MaxValue;
                channelOpen = true;
                // only play the radio beep and background sound if we're not in driver training mode
                if (CrewChief.currentGameState == null
                    || !(CrewChief.currentGameState.SessionData.SessionType == SessionType.Practice || CrewChief.currentGameState.SessionData.SessionType == SessionType.LonePractice)
                    || !PlaybackModerator.paceNotesMuteOtherMessages
                    || !DriverTrainingService.isPlayingPaceNotes)
                {
                    if (!mute)
                    {
                        try
                        {
                            this.backgroundPlayer.play();
                        }
                        catch (Exception)
                        {
                            // ignore
                        }
                    }
                    // beeps don't play in oval spotter mode
                    if (!GlobalBehaviourSettings.ovalSpotterMode)
                    {
                        if (useShortBeepWhenOpeningChannel)
                        {
                            playShortStartSpeakingBeep();
                        }
                        else
                        {
                            playStartSpeakingBeep();
                        }
                    }
                }
            }
        }

        private void closeRadioInternalChannel()
        {
            if (channelOpen)
            {
                breathDueAt = DateTime.MaxValue;
                // only play the radio beep and background sound if we're not in driver training mode
                if (CrewChief.currentGameState == null
                    || !(CrewChief.currentGameState.SessionData.SessionType == SessionType.Practice || CrewChief.currentGameState.SessionData.SessionType == SessionType.LonePractice)
                    || !PlaybackModerator.paceNotesMuteOtherMessages
                    || !DriverTrainingService.isPlayingPaceNotes)
                {
                    // beeps don't play in oval spotter mode
                    if (!GlobalBehaviourSettings.ovalSpotterMode)
                    {
                        playEndSpeakingBeep();
                    }
                    if (!mute)
                    {
                        try
                        {
                            this.backgroundPlayer.stop();
                        }
                        catch (Exception)
                        {
                            // ignore
                        }
                    }
                }
                if (soundCache != null)
                {
                    soundCache.ExpireCachedSounds();
                }
            }
            useShortBeepWhenOpeningChannel = false;
            channelOpen = false;
        }

        public void playStartSpeakingBeep()
        {
            if (!mute && enableRadioBeeps && GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
            {
                var soundToPlay = PlaybackModerator.GetSuggestedBleepStart();
                soundCache.Play(soundToPlay, SoundMetadata.beep);
            }
        }

        public void playPaceNoteRecordingStartStopBeep()
        {
            if (!mute)
            {
                soundCache.Play("pace_notes_recording_start_stop_bleep", SoundMetadata.beep);
            }
        }

        public void playStartListeningBeep()
        {
            if (useListenBeep)
            {
                if (!mute)
                {
                    soundCache.Play("listen_start_sound", SoundMetadata.listenStartBeep);
                }
            }
        }

        public void playListeningEndBeep()
        {
            if (useListenEndBeep)
            {
                if (!mute)
                {
                    soundCache.Play("listen_end_sound", SoundMetadata.listenStartBeep);
                }
            }
        }

        public void playShortStartSpeakingBeep()
        {
            if (!mute && enableRadioBeeps && GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
            {
                var soundToPlay = PlaybackModerator.GetSuggestedBleepShorStart();
                soundCache.Play(soundToPlay, SoundMetadata.beep);
            }
        }

        public void playEndSpeakingBeep()
        {
            if (!mute && enableRadioBeeps && GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
            {
                var soundToPlay = PlaybackModerator.GetSuggestedBleepEnd();
                soundCache.Play(soundToPlay, SoundMetadata.beep);
            }
        }

        public void playChiefEndSpeakingBeep()
        {
            if (!mute && enableRadioBeeps && channelCloseBeepEnabled && GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
            {
                var soundToPlay = PlaybackModerator.GetSuggestedBleepEnd(forceChief: true);
                soundCache.Play(soundToPlay, SoundMetadata.beep);
            }
        }

        public void playMuteBeep()
        {
            if (enableRadioBeeps && GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
            {
                var soundToPlay = PlaybackModerator.GetSuggestedBleepEnd();
                soundCache.Play(soundToPlay, SoundMetadata.beep);
            }
        }

        public void playUnMuteBeep()
        {
            if (enableRadioBeeps && GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
            {
                var soundToPlay = PlaybackModerator.GetSuggestedBleepShorStart();
                soundCache.Play(soundToPlay, SoundMetadata.beep);
            }
        }

        public int purgeQueues(int retainMessagesWithSreSessionId = -1)
        {
            return purgeQueue(queuedClips, false) + purgeQueue(immediateClips, true, retainMessagesWithSreSessionId);
        }

        private int purgeQueue(OrderedDictionary queue, bool isImmediateQueue, int retainMessagesWithSreSessionId = -1)
        {
            Log.SoundDebug(() => "Purging " + (isImmediateQueue ? "immediate" : "regular") + " queue" );
            int purged = 0;
            lock (queue)
            {
                ArrayList keysToPurge = new ArrayList(queue.Keys);
                foreach (String keyStr in keysToPurge)
                {
                    try
                    {
                        if (retainMessagesWithSreSessionId != -1 && isImmediateQueue)
                        {
                            QueuedMessage message = (QueuedMessage)queue[keyStr];
                            if (message.metadata != null && message.metadata.type == SoundType.VOICE_COMMAND_RESPONSE
                                && retainMessagesWithSreSessionId == message.metadata.speechRecognitionSessionID)
                            {
                                continue;
                            }
                        }

                        if (!keyStr.Contains(SessionEndMessages.sessionEndMessageIdentifier) &&
                            !keyStr.Contains(SmokeTest.SMOKE_TEST) &&
                            !keyStr.Contains(SmokeTest.SMOKE_TEST_SPOTTER) &&
                            !keyStr.Contains(AudioPlayer.RETAIN_ON_SESSION_END))
                        {
                            queue.Remove(keyStr);
                            purged++;
                        }
                    }
                    catch (Exception)
                    {
                        // ignore - not sure why I'm try-catching here :)
                    }
                }
            }
            return purged;
        }

        public Boolean isChannelOpen()
        {
            return channelOpen;
        }

        /// <summary>
        /// Play a message if the sound is available, otherwise play an acknowledgement bleep.
        /// </summary>
        public void playMessageOrFallbackToBleep(string sound)
        {
            if (SoundCache.availableSounds.Contains(sound))
            {
                playMessageImmediately(new QueuedMessage(sound, 0));
            }
            else
            {
                getSoundCache()?.Play("drs_detected_bleep", SoundMetadata.beep);
            }
        }

        /// <summary>
        /// Play a message if the sound is available, otherwise play a fallback message.
        /// </summary>
        public void playMessageOrFallback(string sound, string fallback, bool immediately = true,
            int priority = 0, AbstractEvent abstractEvent = null)
        {
            var message = new QueuedMessage(
                SoundCache.availableSounds.Contains(sound) ? sound : fallback,
                0,
                abstractEvent: abstractEvent,
                priority: priority);
            if (immediately)
            {
                playMessageImmediately(message);
            }
            else
            {
                playMessage(message);
            }
        }

        public void playMessage(QueuedMessage queuedMessage)
        {
            if (GlobalBehaviourSettings.enabledMessageTypes.Contains(MessageTypes.NONE))
            {
                Log.Sound(() => "All messages disabled for this car class. Message " + queuedMessage.messageName + " will not be played");
            }
            else
            {
                playMessage(queuedMessage, PearlsOfWisdom.PearlType.NONE, 0);
            }
        }

        // WIP... sometimes the chief loses his shit
        public Boolean playRant(String messageIdentifier, List<MessageFragment> messagesToPlayBeforeRanting)
        {
            if (sweary && !playedRantInThisSession && Utilities.random.NextDouble() < rantLikelihood)
            {
                playedRantInThisSession = true;
                AudioPlayer.rantWaitingToPlay = true;
                List<MessageFragment> messageContents = new List<MessageFragment>();
                if (messagesToPlayBeforeRanting != null)
                {
                    messageContents.AddRange(messagesToPlayBeforeRanting);
                }
                messageContents.Add(MessageFragment.Text(folderRants));
                QueuedMessage rant = new QueuedMessage(messageIdentifier, 0, messageFragments: messageContents);
                rant.isRant = true;
                playMessage(rant, PearlsOfWisdom.PearlType.NONE, 0);
                return true;
            }
            return false;
        }

        public Boolean getDelayedRant(List<MessageFragment> messageFragments)
        {
            if (sweary && !playedRantInThisSession && Utilities.random.NextDouble() < rantLikelihood)
            {
                playedRantInThisSession = true;
                AudioPlayer.rantWaitingToPlay = true;
                messageFragments.Add(MessageFragment.Text(folderRants));
                return true;
            }
            return false;
        }

        // this should only be called in response to a voice message, following a 'standby' request. We want to play the
        // message via the 'immediate' mechanism, but not until the secondsDelay has expired.
        public void playDelayedImmediateMessage(QueuedMessage queuedMessage)
        {
            ThreadManager.UnregisterTemporaryThread(playDelayedImmediateMessageThread);
            playDelayedImmediateMessageThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                if (queuedMessage.secondsDelay > 0)
                {
                    Utilities.InterruptedSleep(queuedMessage.secondsDelay * 1000 /*totalWaitMillis*/, 500 /*waitWindowMillis*/, () => monitorRunning /*keepWaitingPredicate*/);
                }
                playMessageImmediately(queuedMessage);
            });
            playDelayedImmediateMessageThread.Name = "AudioPlayer.playDelayedImmediateMessageThread";
            playDelayedImmediateMessageThread.Start();
            ThreadManager.RegisterTemporaryThread(playDelayedImmediateMessageThread);
        }

        // when we keep the channel open for long running spotter repeat calls, there's a chance that
        // it'll remain open indefinitely (if the last spotter call was an overlap and no clear was
        // received, e.g. the game was closed). So when openning the channel in 'hold' mode, we spawn
        // a Thread that waits for 6 seconds and cleans up if necessary
        private void startHangingChannelCloseThread()
        {
            // ensure an existing thread is stopped properly - can one be created while another is waiting on the monitor?
            hangingChannelCloseWakeUpEvent.Set();
            if (hangingChannelCloseThread != null)
            {
                if (!hangingChannelCloseThread.Join(3000))
                {
                    Log.SoundDebug(() => "Warning: Timed out waiting for thread: " + hangingChannelCloseThread.Name);
                }
            }
            ThreadManager.UnregisterTemporaryThread(hangingChannelCloseThread);
            // reset the wait monitor after the .Set call
            hangingChannelCloseWakeUpEvent.Reset();
            hangingChannelCloseThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                if (!hangingChannelCloseWakeUpEvent.WaitOne(6000) && !SoundCache.IS_PLAYING)
                {
                    // if we timeout here it means the channel was left open, so close it
                    closeRadioInternalChannel();
                }
            });
            hangingChannelCloseThread.Name = "AudioPlayer.hangingChannelCloseThread";
            hangingChannelCloseThread.Start();
            ThreadManager.RegisterTemporaryThread(hangingChannelCloseThread);
        }

        private void stopHangingChannelCloseThread()
        {
            hangingChannelCloseWakeUpEvent.Set();
        }

        public SoundType getPriortyOfFirstWaitingImmediateMessage()
        {
            QueuedMessage message = getFirstWaitingImmediateMessage(SoundType.OTHER);
            return message == null ? SoundType.OTHER : message.metadata.type;
        }

        public QueuedMessage getFirstWaitingImmediateMessage(SoundType minType)
        {
            lock (immediateClips)
            {
                foreach (Object value in immediateClips.Values)
                {
                    QueuedMessage message = (QueuedMessage)value;
                    if (message.metadata.type <= minType)
                    {
                        return message;
                    }
                }
            }
            return null;
        }

        public void playMessageImmediately(QueuedMessage queuedMessage, Boolean keepChannelOpen = false)
        {
            if (CrewChief.Debug.RunningUnderDebugger && queuedMessage.messageName != null)
            {
                int count;
                if (Utilities.queuedMessageIds.TryGetValue(queuedMessage.messageName, out count))
                {
                    Utilities.queuedMessageIds[queuedMessage.messageName] = count++;
                }
                else
                {
                    Utilities.queuedMessageIds.Add(queuedMessage.messageName, 1);
                }
                Log.SoundDebug($"playMessageImmediately: Queued message IDs: {string.Join(", ", Utilities.queuedMessageIds.Select(kvp => kvp.Key + "=" + kvp.Value))}");
            }
            if (queuedMessage.canBePlayed)
            {
                if (queuedMessage.metadata.type == SoundType.AUTO)
                {
                    queuedMessage.metadata.type = SoundType.VOICE_COMMAND_RESPONSE;
                    queuedMessage.metadata.speechRecognitionSessionID = SpeechRecogniser.sreSessionId;
                }
                lock (immediateClips)
                {
                    if (immediateClips.Contains(queuedMessage.messageName))
                    {
                        Log.SoundDebug(() => "Clip for event " + queuedMessage.messageName + " is already queued, ignoring");
                        return;
                    }
                    else if (PlaybackModerator.ImmediateMessageCanBeQueued(queuedMessage))
                    {
                        lastImmediateMessageName = queuedMessage.messageName;
                        lastImmediateMessageTime = GameStateData.CurrentTime;
                        this.useShortBeepWhenOpeningChannel = false;
                        this.holdChannelOpen = keepChannelOpen;
                        if (this.holdChannelOpen)
                        {
                            startHangingChannelCloseThread();
                        }

                        // sanity check...
                        if (queuedMessage.metadata.type == SoundType.REGULAR_MESSAGE)
                        {
                            // a regular message will not play from the immediate queue
                            Log.SoundDebug(() => "Message " + queuedMessage.messageName + " is in the immediate queue but is type 'regular' - this will not play. Setting the type to 'important'");
                            queuedMessage.metadata.type = SoundType.IMPORTANT_MESSAGE;
                        }
                        immediateClips.Insert(getInsertionIndex(immediateClips, queuedMessage), queuedMessage.messageName, queuedMessage);

                        // wake up the monitor thread immediately
                        monitorQueueWakeUpEvent.Set();
                    }
                }
            }
        }

        public void playSpotterMessage(QueuedMessage queuedMessage, Boolean keepChannelOpen)
        {
            if (queuedMessage.canBePlayed)
            {
                // always override the type here
                queuedMessage.metadata.type = SoundType.SPOTTER;
                queuedMessage.metadata.priority = 20;
                lock (immediateClips)
                {
                    if (immediateClips.Contains(queuedMessage.messageName))
                    {
                        Log.SoundDebug(() => "Clip for event " + queuedMessage.messageName + " is already queued, ignoring");
                        return;
                    }
                    else
                    {
                        this.useShortBeepWhenOpeningChannel = true;
                        this.holdChannelOpen = keepChannelOpen;
                        if (this.holdChannelOpen)
                        {
                            startHangingChannelCloseThread();
                        }
                        // default spotter priority is 10
                        immediateClips.Insert(getInsertionIndex(immediateClips, queuedMessage), queuedMessage.messageName, queuedMessage);

                        // attempt to interrupt whatever sound is currently playing when the spotter interrupts the chief (only works with nAudio)
                        // don't interrupt if the currently playing sound is a beep
                        if (!PlaybackModerator.lastSoundWasSpotter)
                        {
                            SoundCache.InterruptCurrentlyPlayingSound(false);
                        }

                        // wake up the monitor thread immediately
                        monitorQueueWakeUpEvent.Set();
                    }
                }
            }
        }

        private int getInsertionIndex(OrderedDictionary queue, QueuedMessage queuedMessage)
        {
            int index = 0;
            foreach (Object value in queue.Values)
            {
                int existingMessagePriority = ((QueuedMessage)value).metadata.priority;
                if (queuedMessage.metadata.priority > existingMessagePriority)
                {
                    // the existing message is lower priorty than the one we're adding so we've found the index we want
                    break;
                }
                index++;
            }
            return index;
        }

        public void playMessage(QueuedMessage queuedMessage, PearlsOfWisdom.PearlType pearlType, double pearlMessageProbability)
        {
            if (CrewChief.Debug.RunningUnderDebugger && queuedMessage.messageName != null)
            {
                int count;
                if (Utilities.queuedMessageIds.TryGetValue(queuedMessage.messageName, out count))
                {
                    Utilities.queuedMessageIds[queuedMessage.messageName] = count++;
                }
                else
                {
                    Utilities.queuedMessageIds.Add(queuedMessage.messageName, 1);
                }
                Log.SoundDebug($"playMessage: Queued message IDs: {string.Join(", ", Utilities.queuedMessageIds.Select(kvp => kvp.Key + "=" + kvp.Value))}");
            }
            if (queuedMessage.canBePlayed)
            {
                if (queuedMessage.metadata.type == SoundType.AUTO)
                {
                    queuedMessage.metadata.type = SoundType.REGULAR_MESSAGE;
                }
                lock (queuedClips)
                {
                    if (queuedClips.Contains(queuedMessage.messageName))
                    {
                        Log.SoundDebug(() => "Clip for event " + queuedMessage.messageName + " is already queued, ignoring");
                        return;
                    }
                    else
                    {
                        DateTime now = CrewChief.currentGameState == null ? DateTime.UtcNow : CrewChief.currentGameState.Now;
                        if (PlaybackModerator.MessageCanBeQueued(queuedMessage, queuedClips.Count, now))
                        {
                            PearlsOfWisdom.PearlMessagePosition pearlPosition = PearlsOfWisdom.PearlMessagePosition.NONE;
                            if (pearlType != PearlsOfWisdom.PearlType.NONE && checkPearlOfWisdomValid(pearlType))
                            {
                                pearlPosition = pearlsOfWisdom.getMessagePosition(pearlMessageProbability);
                            }

                            int insertionIndex = getInsertionIndex(queuedClips, queuedMessage);
                            if (pearlPosition == PearlsOfWisdom.PearlMessagePosition.BEFORE)
                            {
                                QueuedMessage pearlQueuedMessage = new QueuedMessage(queuedMessage.abstractEvent);
                                pearlQueuedMessage.metadata = queuedMessage.metadata;
                                pearlQueuedMessage.dueTime = queuedMessage.dueTime;
                                pearlQueuedMessage.triggerFunction = queuedMessage.triggerFunction;
                                queuedClips.Insert(insertionIndex, PearlsOfWisdom.getMessageFolder(pearlType), pearlQueuedMessage);
                                insertionIndex++;
                            }
                            queuedClips.Insert(insertionIndex, queuedMessage.messageName, queuedMessage);
                            if (pearlPosition == PearlsOfWisdom.PearlMessagePosition.AFTER)
                            {
                                QueuedMessage pearlQueuedMessage = new QueuedMessage(queuedMessage.abstractEvent);
                                pearlQueuedMessage.dueTime = queuedMessage.dueTime;
                                pearlQueuedMessage.metadata = queuedMessage.metadata;
                                pearlQueuedMessage.triggerFunction = queuedMessage.triggerFunction;
                                insertionIndex++;
                                queuedClips.Insert(insertionIndex, PearlsOfWisdom.getMessageFolder(pearlType), pearlQueuedMessage);
                            }

                            // note that we don't wake the monitor Thread here - we wait until all the events have completed on this tick
                            // then check the queue
                        }
                    }
                }
            }
        }

        public Boolean removeQueuedMessage(String eventName)
        {
            lock (queuedClips)
            {
                if (queuedClips.Contains(eventName))
                {
                    queuedClips.Remove(eventName);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Removes the specified event names from the collection of immediate
        /// clips if there are any
        /// </summary>
        public void removeImmediateMessages(List<String> eventNames)
        {
            lock (immediateClips)
            {
                foreach (String eventName in eventNames)
                {
                    if (immediateClips.Contains(eventName))
                    {
                        Log.SoundDebug(() => "Removing immediate clip " + eventName);
                        immediateClips.Remove(eventName);
                    }
                }
            }
        }

        // checks that another pearl isn't already queued. If one of the same type is already
        // in the queue this method just returns false. If a conflicting pearl is in the queue
        // this method removes it and returns false, so we don't end up with, for example,
        // a 'keep it up' message in a block that contains a 'your lap times are worsening' message
        private Boolean checkPearlOfWisdomValid(PearlsOfWisdom.PearlType newPearlType)
        {
            if (GlobalBehaviourSettings.justTheFacts)
            {
                return false;
            }
            if (newPearlType == PearlsOfWisdom.PearlType.BAD)
            {
                if (GlobalBehaviourSettings.complaintsCountInThisSession >= GlobalBehaviourSettings.maxComplaintsPerSession)
                {
                    return false;
                }
            }
            Boolean isValid = true;
            if (queuedClips != null && queuedClips.Count > 0)
            {
                List<String> pearlsToPurge = new List<string>();
                foreach (String eventName in queuedClips.Keys)
                {
                    if (clipIsPearlOfWisdom(eventName))
                    {
                        Log.SoundDebug("There's already a pearl in the queue, can't add another");
                        isValid = false;
                        if (eventName != PearlsOfWisdom.getMessageFolder(newPearlType))
                        {
                            pearlsToPurge.Add(eventName);
                        }
                    }
                }
                foreach (String pearlToPurge in pearlsToPurge)
                {
                    queuedClips.Remove(pearlToPurge);
                    Log.SoundDebug(() => "Queue contains a pearl " + pearlToPurge + " which conflicts with " + newPearlType);
                }
            }
            if (isValid && newPearlType == PearlsOfWisdom.PearlType.BAD)
            {
                GlobalBehaviourSettings.complaintsCountInThisSession++;
            }
            return isValid;
        }

        private Boolean clipIsPearlOfWisdom(String eventName)
        {
            foreach (PearlsOfWisdom.PearlType pearlType in Enum.GetValues(typeof(PearlsOfWisdom.PearlType)))
            {
                if (pearlType != PearlsOfWisdom.PearlType.NONE && PearlsOfWisdom.getMessageFolder(pearlType) == eventName)
                {
                    return true;
                }
            }
            return false;
        }

        private Boolean hasPearlJustBeenPlayed()
        {
            return timeLastPearlOfWisdomPlayed.Add(minTimeBetweenPearlsOfWisdom) > GameStateData.CurrentTime;
        }

        public void suspendPearlsOfWisdom()
        {
            allowPearlsOnNextPlay = false;
        }

        public void repeatLastMessage()
        {
            if (lastMessagePlayed != null)
            {
                // clear the validation, expiry and other data
                lastMessagePlayed.prepareToBeRepeated();
                playMessageImmediately(lastMessagePlayed);
            }
        }

        public void Dispose()
        {
            if (UserSettings.GetUserSettings().getBoolean("use_naudio") && deviceEnum != null && notificationClient != null)
            {
                try
                {
                    deviceEnum.UnregisterEndpointNotificationCallback(notificationClient);
                    deviceEnum.Dispose();
                }
                catch (Exception e) {Log.Exception(e);}
                deviceEnum = null;
                notificationClient = null;
            }
            backgroundPlayer?.dispose();

            disposeSoundCache();
            GlobalResources.audioPlayer = null;
        }

        private void disposeSoundCache()
        {
            if (soundCache != null)
            {
                try
                {
                    soundCache.StopAndUnloadAll();
                }
                catch (Exception e) {Log.Exception(e);}
                soundCache = null;
            }
        }

        public void pauseQueue(int seconds)
        {
            if (!regularQueuePaused)
            {
                regularQueuePaused = true;

                ThreadManager.UnregisterTemporaryThread(pauseQueueThread);
                pauseQueueThread = new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    if (seconds > 0)
                    {
                        Utilities.InterruptedSleep(seconds * 1000 /*totalWaitMillis*/, 500 /*waitWindowMillis*/, () => monitorRunning /*keepWaitingPredicate*/);
                    }
                    regularQueuePaused = false;
                    // wake the monitor thread as soon as the pause has expired
                    this.monitorQueueWakeUpEvent.Set();
                });
                pauseQueueThread.Name = "AudioPlayer.pauseQueueThread";
                ThreadManager.RegisterTemporaryThread(pauseQueueThread);
                pauseQueueThread.Start();
            }
        }

        public void unpauseQueue()
        {
            regularQueuePaused = false;
        }

        public static Boolean canReadName(String rawName, bool allowTTSNameInOnlyWhenNecessaryMode = true)
        {
            if (String.IsNullOrWhiteSpace(rawName)) return false;

            bool allowTTSName = SoundCache.hasSuitableTTSVoice
                && (ttsOption == TTS_OPTION.ANY_TIME || (ttsOption == TTS_OPTION.ONLY_WHEN_NECESSARY && allowTTSNameInOnlyWhenNecessaryMode));
            var usableName = DriverNameHelper.getUsableDriverName(rawName);
            
            return CrewChief.enableDriverNames &&
                (allowTTSName
                || (SoundCache.trainee_active() && SoundCache.availableDriverNamesForTrainee.Contains(usableName)
                || !SoundCache.trainee_active() && SoundCache.availableDriverNames.Contains(usableName)));
        }

        internal void pauseQueueAndPlayDelayedImmediateMessage(QueuedMessage queuedMessage, int lowerDelayBoundInclusive, int upperDelayBound)
        {
            this.playMessageImmediately(new QueuedMessage(AudioPlayer.folderStandBy, 0));

            var secondsDelay = Utilities.random.Next(lowerDelayBoundInclusive, upperDelayBound);
            this.pauseQueue(secondsDelay);

            queuedMessage.dueTime = queuedMessage.creationTime + (secondsDelay * 1000) + queuedMessage.updateInterval;
            queuedMessage.secondsDelay = secondsDelay;
            queuedMessage.expiryTime = queuedMessage.creationTime + (secondsDelay + 10) * 1000;

            this.playDelayedImmediateMessage(queuedMessage);
        }
    }
}
