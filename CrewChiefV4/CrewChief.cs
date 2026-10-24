using System;
using System.Collections.Generic;
using System.Threading;

using CrewChiefV4.RaceRoom;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using CrewChiefV4.PCars;
using CrewChiefV4.Audio;
using CrewChiefV4.commands;
using CrewChiefV4.LMU;
using CrewChiefV4.Overlay;
using CrewChiefV4.SharedMemory;
using CrewChiefV4.PitManager;
using CrewChiefV4.UserInterface.Models;
using CrewChiefV4.UserInterface.TopicWindows;

using static CrewChiefV4.GameDefinition;
using CrewChiefV4.Events.PubSub;

namespace CrewChiefV4
{
    public class CrewChief : IDisposable
    {
        public enum RacingType
        {
            Undefined,
            Circuit,
            Rally
        }

        /// <summary>
        /// The class instance
        /// </summary>
        public static Debugging Debug;

        // speechRecognizer and audioPlayer are shared by many threads.  They should be disposed after root threads stopped, in GlobalResources.Dispose.
        public SpeechRecogniser speechRecogniser;
        public AudioPlayer audioPlayer;

        readonly int timeBetweenProcConnectCheckMillis = 1000;
        readonly int timeBetweenProcDisconnectCheckMillis = 2000;
        readonly int maxEventFailuresBeforeDisabling = 10;
        DateTime nextProcessStateCheck = DateTime.MinValue;
        bool isGameProcessRunning = false;

        public static Boolean loadDataFromFile = false;
        public static SpeechTrace SpeechTrace;
        public static string gameExeParentDirectory = null;

        public static Boolean readOpponentDeltasForEveryLap = false;
        // initial state from properties but can be overridden during a session:
        // No need to initialise here, it's done in reloadSettings()
        public static Boolean yellowFlagMessagesEnabled;

        public static Boolean enableDriverNames;

        public static Utilities.CommandLineParametersReader CommandLine =
            new Utilities.CommandLineParametersReader();

        public static GameDefinition gameDefinition;  // Init to Undefined
        private static GameDefinition traceGameDefinition;  // The game that was used to record a trace
        public static Rf2ChatTransceiver rf2ChatTransceiver;

        private const int IRACING_INTERVAL = 16;               // always use 60Hz for iracing
        private const int DEFAULT_START_LIGHTS_INTERVAL = 10;  // default 10ms during race countdown
        private static int startLightsInterval;
        public static int timeInterval;

        private static int spotterInterval;

        private Boolean displaySessionLapTimes;

        public static Boolean forceSingleClass;
        public static int maxUnknownClassesForAC;

        private Boolean enableR3eWebsocket;
        private Boolean enableR3eGameDataWebsocket;

        private Boolean turnSpotterOffImmediatelyOnFCY;

        public static bool recordChartTelemetryDuringRace;

        private static int intervalWhenCollectionTelemetry;

        public static bool enableSharedMemory;

        private Boolean autoEnablePacenotesInPractice;

        internal static Dictionary<String, AbstractEvent> eventsList = new Dictionary<String, AbstractEvent>(); // Used by command buttons as well

        private Object lastSpotterState;
        private Object currentSpotterState;

        private Boolean stateCleared = false; // TODO: this is probably local to runGame()

        public Boolean running = false;

        // This value is set to false when we re-create main run thread, and is set to true
        // once we get past file loading phase (which can be lenghty).
        public Boolean dataFileReadDone = false;
        public Boolean dataFileDumpDone = false;

        private TimeSpan minimumSessionParticipationTime = TimeSpan.FromSeconds(6);

        private Dictionary<String, String> faultingEvents = new Dictionary<String, String>();

        private Dictionary<String, int> faultingEventsCount = new Dictionary<String, int>();

        private Boolean sessionHasFailingEvent = false;

        private Spotter spotter;

        private Boolean spotterIsRunning = false;

        private Boolean runSpotterThread = false;

        private Thread spotterThread = null;

        private GameDataReader gameDataReader;

        // hmm....
        public static volatile GameStateData currentGameState = null;

        public GameStateData previousGameState = null;

        /// <summary>
        /// Data is available from a game
        /// </summary>
        public Boolean mapped = false;

        private SessionEndMessages sessionEndMessages;

        public static AlarmClock alarmClock;
        // used for the pace notes recorder - need to separate out from the currentGameState so we can
        // set these even when viewing replays
        public static String trackName = "";
        public static int raceroomTrackId = -1;
        public static CarData.CarClassEnum carClass = CarData.CarClassEnum.UNKNOWN_RACE;
        public static Boolean viewingReplay = false;
        public static float distanceRoundTrack = -1;

        public static int playbackIntervalMilliseconds = 0;

        // when an FCY period starts, don't turn the spotter off immediately. Wait until the speed has reduced
        // or we've crossed the line
        // 10 seconds after the FCY we turn the spotter off as soon as the speed < 40m/s
        private Boolean waitingToPauseSpotter = false;
        private DateTime minTurnSpotterOffForFCYTime = DateTime.MaxValue;
        private DateTime maxTurnSpotterOffForFCYTime = DateTime.MaxValue;
        private TimeSpan minTimeToWaitToTurnSpotterOffInFCY = TimeSpan.FromSeconds(10);
        private TimeSpan maxTimeToWaitToTurnSpotterOffInFCY = TimeSpan.FromSeconds(30);
        private float fcySpeedToTurnSpotterOffOnOvals = 40;
        private float fcySpeedToTurnSpotterOffOnRoadCourses = 50;

        private ControllerConfiguration controllerConfiguration;

        public static SharedMemory.SharedMemoryManager sharedMemoryManager = null;

        private Object latestRawGameData;
        private static long _ticksWhenRead = 0;
        public static long ticksWhenRead
        {
            get { return _ticksWhenRead; }
            set { _ticksWhenRead = value; }
        }
        
        private string _latestTrackName;
        public string LatestTrackName
        {
            get
            {
                return _latestTrackName != null ? _latestTrackName : "unknown";
            } 
            set
            {
                if (value != "unknown") _latestTrackName = value;
            }}

        private string _latestCarName;
        public string LatestCarName
        {
            get
            {
                return _latestCarName != null ? _latestCarName : "unknown";
            }
            set
            {
                if (value != null) _latestCarName = value;
            }
        }
        private readonly bool traceRaceSessionsOnly = UserSettings.GetUserSettings().getBoolean("trace_race_sessions_only");

        #region Fuel Multiplier
        /// <summary>
        /// Deal with the ways different games handle fuel consumption
        /// and the different settings within games
        /// </summary>
        public class FuelMultiplier
        {
            public bool showMultiplier { get; private set; }
            private readonly FuelMultiplierType _fuelMultiplierType;

            public FuelMultiplier(FuelMultiplierType fuelMultiplierType)
            {
                _fuelMultiplierType = fuelMultiplierType;
                _fuelMultiplier = 0; // to initialise numericUpDownfuelMultiplier
                multiplier = 1; // until we know otherwise
                switch (fuelMultiplierType)
                {
                    case FuelMultiplierType.None:
                        showMultiplier = false;
                        break;
                    case FuelMultiplierType.Fixed:
                        showMultiplier = false;
                        break;
                    case FuelMultiplierType.SharedByGame:
                    case FuelMultiplierType.Variable:
                        showMultiplier = true;
                        break;
                }
            }
            private static volatile int _fuelMultiplier;
            public int multiplier
            {
                get
                {
                    int _multiplier;
                    switch (_fuelMultiplierType)
                    {
                        case FuelMultiplierType.None:
                        default:
                            _multiplier = 0;
                            break;
                        case FuelMultiplierType.Fixed:
                            _multiplier = 1;
                            break;
                        case FuelMultiplierType.SharedByGame:
                            _multiplier = _fuelMultiplier;
                            break;
                        case FuelMultiplierType.Variable:
                            _multiplier = int.TryParse(MainWindow.instance.numericUpDownfuelMultiplier.Text, out _multiplier) ? _multiplier : 0;
                            break;
                    }
                    return _multiplier;
                }
                set
                {
                    if (_fuelMultiplier != value)
                    {
                        switch (_fuelMultiplierType)
                        {
                            case FuelMultiplierType.None:
                                _fuelMultiplier = 0;
                                break;
                            case FuelMultiplierType.Fixed:
                                _fuelMultiplier = 1;
                                break;
                            case FuelMultiplierType.SharedByGame:
                                MainWindow.instance.numericUpDownfuelMultiplier.Text = value.ToString();
                                _fuelMultiplier = value;
                                break;
                            case FuelMultiplierType.Variable:
                                if (value == 0)
                                {   // Game says fuel consumption is now inactive
                                    MainWindow.instance.numericUpDownfuelMultiplier.Text = "0";
                                    _fuelMultiplier = 0;
                                }
                                else
                                {   // Game says fuel consumption is now active (but doesn't provide the multiplier)
                                    MainWindow.instance.numericUpDownfuelMultiplier.Text = "1";
                                    _fuelMultiplier = 1;
                                }
                                break;
                        }
                    }
                }
            }
            /// <summary>
            /// Fuel consumption is active in the game
            /// </summary>
            public bool active => fuelMultiplier.multiplier > 0;
            /// <summary>
            /// If the user entered the fuel multiplier and did it wrongly it
            /// will mess up the the data
            /// </summary>
            public bool dontSaveFuelData
            {
                get
                {
                    return _fuelMultiplierType == FuelMultiplierType.Variable &&
                           !UserSettings.GetUserSettings().getBoolean("save_all_fuel_data");
                }
            }
        }
        public static FuelMultiplier fuelMultiplier;
        #endregion Fuel Multiplier

        public CrewChief(ControllerConfiguration controllerConfiguration)
        {
            speechRecogniser = new SpeechRecogniser(this);
            audioPlayer = new AudioPlayer();
            enableSharedMemory = UserSettings.GetUserSettings().getBoolean("enable_shared_memory");
            if (enableSharedMemory)
            {
                sharedMemoryManager = new SharedMemoryManager();
            }
            this.controllerConfiguration = controllerConfiguration;

            GlobalResources.speechRecogniser = speechRecogniser;

            audioPlayer.initialise();
            clearAndReloadEvents();

            DriverNameHelper.ReadDriverNameMappings(AudioPlayer.soundFilesPath);
        }

        private void reloadSettings()
        {
            // Class vars
            this.enableR3eWebsocket = Game.RACE_ROOM && UserSettings.GetUserSettings().getBoolean("enable_websocket");
            this.enableR3eGameDataWebsocket = Game.RACE_ROOM && UserSettings.GetUserSettings().getBoolean("enable_game_data_websocket");
            this.displaySessionLapTimes = UserSettings.GetUserSettings().getBoolean("display_session_lap_times");
            this.turnSpotterOffImmediatelyOnFCY = UserSettings.GetUserSettings().getBoolean("fcy_stop_spotter_immediately");
            this.autoEnablePacenotesInPractice = UserSettings.GetUserSettings().getBoolean("auto_enable_pacenotes_in_practice");
            // Static vars
            CrewChief.yellowFlagMessagesEnabled = UserSettings.GetUserSettings().getBoolean("enable_yellow_flag_messages");
            CrewChief.enableDriverNames = UserSettings.GetUserSettings().getBoolean("enable_driver_names");
            CrewChief.timeInterval = Game.IRACING ? IRACING_INTERVAL : UserSettings.GetUserSettings().getInt("update_interval");
            CrewChief.startLightsInterval = Math.Min(CrewChief.timeInterval, CrewChief.DEFAULT_START_LIGHTS_INTERVAL);
            CrewChief.spotterInterval = Game.IRACING ? IRACING_INTERVAL : UserSettings.GetUserSettings().getInt("spotter_update_interval");
            CrewChief.forceSingleClass = UserSettings.GetUserSettings().getBoolean("force_single_class");
            CrewChief.maxUnknownClassesForAC = UserSettings.GetUserSettings().getInt("max_unknown_car_classes_for_assetto");
            CrewChief.intervalWhenCollectionTelemetry = UserSettings.GetUserSettings().getInt("update_interval_when_collecting_telemetry");
            CrewChief.recordChartTelemetryDuringRace = UserSettings.GetUserSettings().getBoolean("enable_chart_telemetry_in_race_session");
        }

        public Dictionary<String, AbstractEvent> createListOfAllEvents()
        {
            foreach (AbstractEvent ev in eventsList.Values)
            {
                ev.Dispose();
            }
            eventsList.Clear();
            eventsList.Add("Position", new Position(audioPlayer));
            eventsList.Add("LapCounter", new LapCounter(audioPlayer, null));
            if (UserSettings.GetUserSettings().getBoolean("revert_to_legacy_version_of_refactored_code"))
            {
                eventsList.Add("TyreMonitor", new TyreMonitor_legacy(audioPlayer));
            }
            else
            {
                eventsList.Add("TyreMonitor", new TyreMonitor(audioPlayer));
            }
            eventsList.Add("Timings", new Timings(audioPlayer));
            eventsList.Add("LapTimes", new LapTimes(audioPlayer));
            eventsList.Add("Opponents", new Opponents(audioPlayer));
            eventsList.Add("Penalties", new Penalties(audioPlayer));
            eventsList.Add("PitStops", new PitStops(audioPlayer));
            eventsList.Add("Fuel", new Fuel(audioPlayer));
            eventsList.Add("Battery", new Battery(audioPlayer));
            eventsList.Add("WatchedOpponents", new WatchedOpponents(audioPlayer));
            eventsList.Add("Strategy", new Strategy(audioPlayer));
            eventsList.Add("RaceTime", new RaceTime(audioPlayer));
            eventsList.Add("EngineMonitor", new EngineMonitor(audioPlayer));
            eventsList.Add("DamageReporting", new DamageReporting(audioPlayer));
            eventsList.Add("PushNow", new PushNow(audioPlayer));
            eventsList.Add("FlagsMonitor", new FlagsMonitor(audioPlayer));
            eventsList.Add("ConditionsMonitor", new ConditionsMonitor(audioPlayer));
            eventsList.Add("OvertakingAidsMonitor", new OvertakingAidsMonitor(audioPlayer));
            eventsList.Add("FrozenOrderMonitor", new FrozenOrderMonitor(audioPlayer));
            eventsList.Add("IRacingBroadcastMessageEvent", new IRacingBroadcastMessageEvent(audioPlayer));
            eventsList.Add("Ratings", new Ratings(audioPlayer));
            eventsList.Add("MulticlassWarnings", new MulticlassWarnings(audioPlayer));
            eventsList.Add("DriverSwaps", new DriverSwaps(audioPlayer));
            eventsList.Add("CommonActions", new CommonActions(audioPlayer));
            eventsList.Add("OverlayController", new OverlayController(audioPlayer));
            eventsList.Add("VROverlayController", new VROverlayController(audioPlayer));
            eventsList.Add("Mqtt", new Mqtt(audioPlayer));
            eventsList.Add("CoDriver", new CoDriver(audioPlayer));
            eventsList.Add("PitManagerVoiceCmds", new PitManager.PitManagerVoiceCmds(audioPlayer));
            eventsList.Add("AlarmClock", new AlarmClock(audioPlayer));
            return eventsList;
        }

        private void clearAndReloadEvents()
        {   // All events, less those removed below
            createListOfAllEvents();
            if (!Game.IRACING && !UnitTest.UnitTest.Active)
            {
                eventsList.Remove("IRacingBroadcastMessageEvent");
            }
            if (!Game.IRACING && !Game.RACE_ROOM && !UnitTest.UnitTest.Active)
            {
                eventsList.Remove("Ratings");
            }
            if (GlobalBehaviourSettings.racingType != CrewChief.RacingType.Rally)
            {
                eventsList.Remove("CoDriver");
            }
            if (!Game.RF2_LMU && !UnitTest.UnitTest.Active)
            {
                eventsList.Remove("PitManagerVoiceCmds");
            }
            if (!AlarmClock.Active)
            {
                eventsList.Remove("AlarmClock");
            }

            sessionEndMessages = new SessionEndMessages(audioPlayer);
        }

        /// <summary>
        /// Set the active gameDefinition and load its plugin if necessary
        /// Also sets UserSetting "last_game_definition"
        /// </summary>
        public void setGameDefinition(in GameDefinition gameDefinition)
        {
            spotter = null;
            mapped = false;
            if (gameDefinition == null)
            {
                Console.WriteLine("No game definition selected");
            }
            else
            {
                Console.WriteLine("Using game definition " + gameDefinition.friendlyName);
                UserSettings.GetUserSettings().setProperty("last_game_definition", gameDefinition.commandLineName);
                UserSettings.GetUserSettings().saveUserSettings();
                CrewChief.gameDefinition = gameDefinition;
                Game.game = gameDefinition.gameEnum;
                CrewChief.rf2ChatTransceiver = new Rf2ChatTransceiver();
                if (UserSettings.GetUserSettings().getBoolean("enable_automatic_plugin_update"))
                {
                    PluginInstaller pluginInstaller = new PluginInstaller();
                    pluginInstaller.InstallOrUpdatePlugins(gameDefinition);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (enableSharedMemory)
            {
                CrewChief.sharedMemoryManager.Dispose();
            }

        }

        ~CrewChief()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public static AbstractEvent getEvent(String eventName, Dictionary<String, AbstractEvent> _eventsList = null)
        {
            AbstractEvent abstractEvent;
            _eventsList = _eventsList ?? eventsList;
            if (_eventsList.TryGetValue(eventName, out abstractEvent))
            {
                return abstractEvent;
            }

            return new NullEvent();
        }

        public static string getEventName(AbstractEvent _event)
        {
            string foundKey = null;

            foreach (var kvp in eventsList)
            {
                if (kvp.Value == _event)
                {
                    foundKey = kvp.Key;
                    break;
                }
            }
            return foundKey;
        }


        /// <summary>
        /// The event handler is known, process the command
        /// </summary>
        public static void HandleEvent(string eventName, string recognisedText)
        {
            AbstractEvent abstractEvent = CrewChief.getEvent(eventName);
            var cmd = abstractEvent.HandlesEvent(recognisedText);
            if (cmd != SpeechCommands.ID.NO_COMMAND)
            {
                abstractEvent.respond(recognisedText, cmd);
            }
            else
            {
                Log.Error($"Event {eventName} does not handle command {recognisedText}");
                return;
            }
        }

        public void toggleSpotterMode()
        {
            if (GlobalBehaviourSettings.spotterEnabled)
            {
                disableSpotter();
            }
            else
            {
                enableSpotter();
            }
        }

        public void enableSpotter()
        {
            if (spotter == null)
            {
                Console.WriteLine("No spotter configured for this game");
            }
            else
            {
                GlobalBehaviourSettings.spotterEnabled = true;
                spotter.enableSpotter();
            }
        }

        public void disableSpotter()
        {
            if (spotter != null)
            {
                GlobalBehaviourSettings.spotterEnabled = false;
                spotter.disableSpotter();
            }
        }

        public void youWot(Boolean detectedSomeSpeech)
        {
            if (!running)
            {
                return;
            }
            SpeechRecogniser.waitingForSpeech = false;
            if (detectedSomeSpeech)
            {
                Console.WriteLine("Detected speech input but nothing was recognised");
            }
            else
            {
                Console.WriteLine("No speech input was detected");
            }

            if (SpeechWizard.Active)
            {
                audioPlayer.playListeningEndBeep();
                SpeechRecogniser.Timeout();
                return;
            }

            if (DamageReporting.waitingForDriverIsOKResponse)
            {
                ((DamageReporting)CrewChief.getEvent("DamageReporting")).cancelWaitingForDriverIsOK(
                    detectedSomeSpeech ? DamageReporting.DriverOKResponseType.NOT_UNDERSTOOD : DamageReporting.DriverOKResponseType.NO_SPEECH);
            }
            else
            {
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDidntUnderstand, 0));
            }
        }

        private void startSpotterThread()
        {
            if (spotter != null)
            {
                if (spotterThread != null)
                {
                    // This is the corner case when spotter was disabled during runtime.
                    stopSpotterThread();
                    spotterThread = null;
                }
                System.Diagnostics.Debug.Assert(spotterThread == null);
                lastSpotterState = null;
                currentSpotterState = null;
                spotterIsRunning = true;
                ThreadStart work = spotterWork;

                // Thread owned and managed by CrewChief.Run thread.
                spotterThread = new Thread(work);

                runSpotterThread = true;
                spotterThread.Start();
            }
        }

        private void stopSpotterThread()
        {
            if (spotter != null && spotterThread != null)
            {
                runSpotterThread = false;

                if (spotterThread.IsAlive)
                {
                    Console.WriteLine("Waiting for spotter thread to stop...");
                    if (!spotterThread.Join(5000))
                    {
                        Console.WriteLine("Warning: Timed out waiting for spotter thread to stop to stop");
                    }
                    Console.WriteLine("Spotter thread stopped");
                }

                spotterThread = null;
            }
        }

        private static bool noSpotterInQualifying; // Initialized later as it blows up unit testing
        public static bool qualifyingAndNoSpotterWhenQualifying { 
            get
            {
                return noSpotterInQualifying &&
                       (currentGameState.SessionData.SessionType == SessionType.Qualify ||
                        currentGameState.SessionData.SessionType == SessionType.PrivateQualify);
                }
            }
        private void spotterWork()
        {
            noSpotterInQualifying = UserSettings.GetUserSettings().getBoolean("spotter_off_during_qualifying");
            Console.WriteLine("Invoking spotter every " + spotterInterval);
            try
            {
                while (runSpotterThread)
                {
                    if (spotter != null && gameDataReader.hasNewSpotterData())
                    {
                        currentSpotterState = gameDataReader.ReadGameData(true);
                        if (lastSpotterState != null && currentSpotterState != null)
                        {
                            try
                            {
                                if (!qualifyingAndNoSpotterWhenQualifying)
                                {
                                    spotter.trigger(lastSpotterState, currentSpotterState, currentGameState);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.Exception(e, "Spotter failed: ");
                                runSpotterThread = false;
                            }
                        }
                        lastSpotterState = currentSpotterState;
                    }
                    Thread.Sleep(spotterInterval);
                }
            }
            catch (Exception)  // Exceptions can happen on Stop and DisconnectFromProcess.
            {
                Console.WriteLine("Spotter thread terminated.");
            }
            spotterIsRunning = false;
        }

        public Tuple<GridSide, Dictionary<int, GridSide>> getGridSide()
        {
            return this.spotter.getGridSide(this.latestRawGameData);
        }

        /// <summary>
        /// Run Crew Chief
        /// </summary>
        /// <param name="filenameToRun">if running from trace data, otherwise null</param>
        /// <param name="dumpToFile">if saving trace data</param>
        /// <returns>True: Game ran and closed down
        /// False: CC attempted to run game itself (because
        /// gameDefinition.gameStartEnabledProperty) but failed</returns>
        public Boolean Run(String filenameToRun, Boolean dumpToFile)
        {
            GameOrVoipVolume.SetVolumes();
            clearAndReloadEvents();
            reloadSettings();
            GlobalBehaviourSettings.reloadSettings();
            controllerConfiguration.assignButtonEventInstances();
            try
            {
                if (enableR3eWebsocket)
                {
                    Utilities.startCCDataWebsocketServer(audioPlayer);
                }
                if (Game.RACE_ROOM)
                {
                    //CarData.checkR3eDataJson();
                }

                PlaybackModerator.SetCrewChief(this);

                loadDataFromFile = false;
                audioPlayer.mute = false;
                GameStateMapper gameStateMapper = GameStateReaderFactory.getInstance().getGameStateMapper(gameDefinition);
                GameStateMapper traceGameStateMapper;
                if (filenameToRun != null)
                {
                    loadDataFromFile = true;
                    GlobalBehaviourSettings.spotterEnabled = Game.F1_20S;
                    dumpToFile = false;
                    SpeechTrace = new SpeechTrace();
                    traceGameDefinition = GameDefinition.getGameDefinitionForCommandLineName(filenameToRun.Split('_')[0]) ?? gameDefinition;
                    traceGameStateMapper = GameStateReaderFactory.getInstance().getGameStateMapper(traceGameDefinition);
                }
                else
                {
                    dataFileReadDone = true;  // Don't block UI as we won't be loading from the file.
                    traceGameStateMapper = gameStateMapper;
                    traceGameDefinition = gameDefinition;
                }
                gameDataReader = GameStateReaderFactory.getInstance().getGameStateReader(traceGameDefinition);
                gameDataReader.ResetGameDataFromFile();

                SpeechRecogniser.waitingForSpeech = false;
                SpeechRecogniser.gotRecognitionResult = false;
                SpeechRecogniser.keepRecognisingInHoldMode = false;
                gameStateMapper.setSpeechRecogniser(speechRecogniser);

                gameDataReader.dumpToFile = dumpToFile;
                if (dumpToFile)
                {
                    SpeechTrace = new SpeechTrace();
                }

                if (enableR3eGameDataWebsocket)
                {
                    // TODO: version handling is a bit hooky here. The version data are in shared memory but if we just pass this
                    // through to the JSON there's a risk the game version will advance (so the client expects new data) but CC isn't
                    // actually sending this data. So we'll hard-code it here for now
                    Utilities.startGameDataWebsocketServer("/r3e", gameDataReader, new R3ESerializer(true, 3, 4, 10));
                }

                if (gameDefinition.spotterName != null)
                {
                    spotter = (Spotter)Activator.CreateInstance(Type.GetType(gameDefinition.spotterName),
                        audioPlayer, GlobalBehaviourSettings.spotterEnabled);
                }
                else
                {
                    Console.WriteLine("No spotter defined for game " + gameDefinition.friendlyName);
                    spotter = null;
                }
                // force pcars3 to be single class because it probably is, i don't own it and it's not very good
                if (Game.PCARS3)
                {
                    CrewChief.forceSingleClass = true;
                }
                
                running = true; // AI says: Even though we may be about to return false, we need to set this to true so that the UI can update

                if (!audioPlayer.initialised)
                {
                    Console.WriteLine("Failed to initialise audio player");
                    return false;
                }
                // mute the audio player for anything < 10ms
                audioPlayer.mute = loadDataFromFile && CrewChief.playbackIntervalMilliseconds < 10;
                if (loadDataFromFile)
                {
                    Utilities.queuedMessageIds.Clear();
                    Utilities.includesRaceSession = false;
                }
                audioPlayer.startMonitor(!Game.NONE);
                // Boolean attemptedToRunGame = false; this is now local to runGame()
                if (UserSettings.GetUserSettings().getBoolean("enable_overlay_window"))
                {
                    OverlayDataSource.loadChartSubscriptions();
                    if (speechRecogniser != null && !speechRecogniser.disableOverlayVoiceCommands)
                    {
                        speechRecogniser.addOverlayGrammar();
                    }
                }

                bool useTelemetryIntervalWhereApplicable = !Game.IRACING
                                                           && UserSettings.GetUserSettings().getBoolean("enable_overlay_window");
                if (!Game.NONE &&
                    !Game.PCARS_NETWORK &&
                    !Game.F1_20S)
                {
                    Console.WriteLine("Polling for shared data every " + timeInterval + "ms");
                }

                if (!runGame(filenameToRun, dumpToFile, traceGameStateMapper, gameStateMapper, useTelemetryIntervalWhereApplicable))
                {
                    return false;
                }

                afterRunning();
            }
            finally  // (refactor: this is unchanged)
            {
                // Thread cleanup.

                speechRecogniser?.stop();

                // Wait on child threads and release owned resources here.
                Console.WriteLine("Stopping queue monitor");
                if (audioPlayer != null)
                {
                    audioPlayer.stopMonitor();
                    PlaybackModerator.SetCrewChief(null);
                    audioPlayer.disablePearlsOfWisdom = false;
                }
                SoundCache.saveVarietyData();

                stopSpotterThread();

                // Release thread resources:
                if (gameDataReader != null)
                {
                    gameDataReader.Dispose();
                    gameDataReader = null;
                }
                if (Debug.RunningUnderDebugger)
                {
                    Utilities.checkPlaybackCounts();
                }
            }

            return true;
        }

        /// <returns>True: Game ran and closed down
        /// False: CC attempted to run game itself (because
        /// gameDefinition.gameStartEnabledProperty) but failed</returns>
        private bool runGame(
            string filenameToRun,
            bool dumpToFile, 
            GameStateMapper traceGameStateMapper,
            GameStateMapper gameStateMapper,
            bool useTelemetryIntervalWhereApplicable)
        {
            Boolean attemptedToRunGame = false;
            Boolean sessionFinished = false;
            Boolean playedSessionFinished = false;
            GameStateEventDetector gameStateEventDetector = new GameStateEventDetector(null);

            while (running)
            {
                DateTime now = DateTime.UtcNow;
                //GameStateData.CurrentTime = now;

                alarmClock?.trigger(null, null);

                if (!loadDataFromFile)
                { // Get real data from game, set "mapped" if it's available
                    // Turns our checking for running process by name is an expensive system call.  So don't do that on every tick.
                    if (now > nextProcessStateCheck && gameDefinition.processName != null)
                    {
                        nextProcessStateCheck = now.Add(
                            TimeSpan.FromMilliseconds(isGameProcessRunning ? timeBetweenProcDisconnectCheckMillis : timeBetweenProcConnectCheckMillis));
                        isGameProcessRunning = Utilities.IsGameRunning(gameDefinition.processName, gameDefinition.alternativeProcessNames, out CrewChief.gameExeParentDirectory);
                    }

                    if (mapped
                        && !isGameProcessRunning
                        && gameDefinition.HasAnyProcessNameAssociated())
                    {
                        CrewChief.gameExeParentDirectory = null;
                        gameDataReader.DisconnectFromProcess();
                        mapped = false;
                    }

                    if (!gameDefinition.HasAnyProcessNameAssociated()  // Network data case.
                        || isGameProcessRunning)
                    {
                        if (!mapped)
                        { // The game has started
                            mapped = gameDataReader.Initialise();

                            // Instead of stressing process to death on failed mapping,
                            // give a it a break.
                            if (!mapped)
                            {
                                Thread.Sleep(1000);
                            }
                            else if (GameOrVoipVolume.AudioDuckingEnabled)
                            {
                                GameOrVoipVolume.UnDuckGameAudio(); // Set the saved game volume
                            }
                        }
                    }
                    else if (UserSettings.GetUserSettings().getBoolean(gameDefinition.gameStartEnabledProperty) && !attemptedToRunGame)
                    {
                        if (Utilities.runGame(UserSettings.GetUserSettings().getPath(gameDefinition.gameStartCommandProperty),
                                UserSettings.GetUserSettings().getString(gameDefinition.gameStartCommandOptionsProperty)))
                        {
                            attemptedToRunGame = true;
                        }
                        else
                        {
                            return false;   // runGame() has logged the error
                        }
                    }
                }

                if (loadDataFromFile || mapped)
                {
                    stateCleared = false;

                    if (loadDataFromFile)
                    {
                        GameDataReader.TracedGameData tracedGameData = null;
                        try
                        {
                            tracedGameData = gameDataReader.ReadGameDataFromFile(filenameToRun, 3000);
                            if (tracedGameData != null)
                            {
                                latestRawGameData = tracedGameData.GameData;
                                ticksWhenRead = tracedGameData.TicksWhenRead;
                                var recognisedText = SpeechTrace.Get(ticksWhenRead);
                                if (recognisedText != null)
                                {
                                    speechRecogniser.TracePlayback(recognisedText);
                                }
                            }
                        }
                        catch (Exception e) when (Debug.LogException(e))
                        {
                            Log.Exception(e, "Error reading game trace data: ");
                        }
                        finally
                        {
                            dataFileReadDone = true;
                        }
                        if (tracedGameData == null)
                        {
                            MainWindow.autoScrollConsole = true;
                            Console.WriteLine("Reached the end of the data file, sleeping to clear queued messages");
                            Utilities.InterruptedSleep(5000 /*totalWaitMillis*/, 500 /*waitWindowMillis*/, () => running /*keepWaitingPredicate*/);
                            try
                            {
                                audioPlayer.purgeQueues();
                            }
                            catch (Exception)
                            {
                                // ignore
                            }
                            running = false;
                            continue; // while(running)
                        }
                    }
                    else // mapped (real data is available)
                    {
                        try
                        {
#if FAKE_GAME
                                gameDataReader.currentlyTracingGameData = dumpToFile;
#else
                            gameDataReader.currentlyTracingGameData = dumpToFile && 
                                (Game.IRACING ||          // iRacing already saves sessions separately
                                 !traceRaceSessionsOnly ||
                                 (currentGameState != null &&
                                  currentGameState.SessionData.SessionType == SessionType.Race));
#endif

                            latestRawGameData = gameDataReader.ReadGameData(false);
                            ticksWhenRead = DateTime.UtcNow.Ticks; // Not exact but close enough for speech trace
                        }
                        catch (GameDataReadException e) when (Debug.LogException(e))
                        {
                            Log.Exception(e, "Error reading live game data " + e.Message);
                            continue; // while(running)
                        }
                    }
                    // another Thread may have stopped the app - check here before processing the game data
                    if (!running)
                    {
                        continue; // while(running)
                    }
                    try
                    {
                        traceGameStateMapper.versionCheck(latestRawGameData);
                    }
                    catch (Exception e)
                    {
                        // Prevent CC crash.  Individual mappers should still make sure they don't throw indefinitely.
                        Log.Exception(e, "Error verifying raw data version: ");
                    }

                    GameStateData nextGameState = null;
                    try
                    {
#if FAKE_GAME
#else
                        nextGameState = traceGameStateMapper.mapToGameStateData(latestRawGameData, currentGameState);
                        if (nextGameState != null)
                        {
                            LatestCarName = nextGameState.carName;
                            if (Game.LMU)
                            {
                                try
                                {
                                    fuelMultiplier.multiplier = SessionSettings.FuelMultiplier;
                                    if (currentGameState != null &&
                                        currentGameState.SessionData.IsNewSession)
                                    {
                                        SessionSettings.NewSession();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning($"LMU fuel multiplier read exception '{ex.Message}");
                                }
                            }
                            else
                            {
                                fuelMultiplier.multiplier = nextGameState.FuelData.FuelMultiplier; // Update if the game has changed it
                            }
                        }
#endif
                    }
                    catch (Exception e) when (Debug.LogException(e))
                    {
                        Log.Exception(e, "Error mapping game data: ");
                    }
                    // if we're paused or viewing another car, the mapper will just return the previous game state so we don't lose all the
                    // persistent state information. If this is the case, don't process any stuff
                    if (nextGameState != null && (nextGameState.SessionData.AbruptSessionEndDetected || nextGameState != currentGameState))
                    {
                        previousGameState = currentGameState;
                        currentGameState = nextGameState;
                        if (currentGameState.SessionData.SessionType == SessionType.Race)
                        {
                            gameStateMapper.populateDerivedRaceSessionData(currentGameState);
                            // tell the utils class that we've had a race session - used when debugging traces to check expectations
                            Utilities.includesRaceSession = true;
                        }
                        else
                        {
                            gameStateMapper.populateDerivedNonRaceSessionData(currentGameState);
                        }
                        if (!sessionFinished && GameEventBool.SessionFinished)
                        {
                            string positionMsg;
                            if (currentGameState.SessionData.IsDisqualified)
                            {
                                positionMsg = "Disqualified";
                            }
                            else if (currentGameState.SessionData.IsDNF)
                            {
                                positionMsg = "DNF";
                            }
                            else
                            {
                                positionMsg = currentGameState.SessionData.ClassPosition.ToString();
                            }
                            Console.WriteLine("Session finished, position = " + positionMsg);
                            audioPlayer.purgeQueues();
                            if (displaySessionLapTimes)
                            {
                                if (currentGameState.SessionData.formattedPlayerLapTimes.Count > 0)
                                {
                                    Console.WriteLine("Session lap times:");
                                    Console.WriteLine(String.Join(";    ", currentGameState.SessionData.formattedPlayerLapTimes));
                                }
                                else
                                {
                                    Console.WriteLine("No valid lap times were set.");
                                }
                            }

                            try
                            {
                                if (previousGameState != null &&
                                    previousGameState.SessionData != null)
                                {
                                    var classPosition = previousGameState.SessionData.ClassPosition;
                                    if (Game.IRACING)
                                    {
                                        // workaround: in iRacing the position is often wrong prior to crossing the line
                                        classPosition = currentGameState.SessionData.ClassPosition;
                                    }
                                    sessionEndMessages.trigger(previousGameState.SessionData.SessionRunningTime, previousGameState.SessionData.SessionType, currentGameState.SessionData.SessionPhase,
                                        previousGameState.SessionData.SessionStartClassPosition, classPosition,
                                        previousGameState.SessionData.NumCarsInPlayerClassAtStartOfSession, previousGameState.SessionData.CompletedLaps, currentGameState.SessionData.expectedFinishingPosition,
                                        currentGameState.SessionData.IsDisqualified, currentGameState.SessionData.IsDNF, currentGameState.Now);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Error triggering session end messages 'Object reference not set to an instance of an object'?\n{ex.Message}");
                            }

                            audioPlayer.holdChannelOpen = false;    // clear the 'hold open' state here before waking the monitor
                            audioPlayer.wakeMonitorThreadForRegularMessages(currentGameState.Now);
                            sessionFinished = true;
                            audioPlayer.disablePearlsOfWisdom = false;

                            if (loadDataFromFile)
                            {
                                Utilities.InterruptedSleep(2000 /*totalWaitMillis*/, 500 /*waitWindowMillis*/, () => running /*keepWaitingPredicate*/);
                            }
                        }

                        // this is a hack because it doesn't fit anywhere else. In iRacing there is a point after the race when
                        // the session officially ends. It is useful to know when this is because people will tend to
                        // continue grinding for safety points. It is typically indicated by the session counter jumping
                        // but on some tracks the timer jumps to the magical value 604800 as soon as the player crosses the line
                        // (which we also see in the gridwalk).
                        //
                        // We don't have a dedicated voice message for this, so we just repeat the session end message.
                        if (Game.IRACING && sessionFinished && previousGameState != null
                            && !playedSessionFinished
                            && !currentGameState.SessionData.IsNewSession
                            && currentGameState.SessionData.SessionType == SessionType.Race
                            && sessionEndMessages.enableSessionEndMessages
                            && ((previousGameState.SessionData.CarsStillToFinish != 0 && currentGameState.SessionData.CarsStillToFinish == 0)
                                || previousGameState.SessionData.SessionTimeRemaining < currentGameState.SessionData.SessionTimeRemaining))
                        {
                            playedSessionFinished = true;
                            Log.Debug("looks like the session is over");
                            audioPlayer.playMessage(new QueuedMessage("session_over", 0, messageFragments: AbstractEvent.MessageContents(SessionEndMessages.folderEndOfSession), priority: 10));
                        }

                        // not used float prevTime = previousGameState == null ? 0 : previousGameState.SessionData.SessionRunningTime;
                        if (currentGameState.SessionData.IsNewSession)
                        {
                            Console.WriteLine("New session");
                            PlaybackModerator.ClearVerbosityData();
                            PlaybackModerator.lastBlockedMessageId = -1;
                            audioPlayer.disablePearlsOfWisdom = false;
                            displayNewSessionInfo(currentGameState);
                            sessionFinished = false;
                            playedSessionFinished = false;
                            TopicWindowBrakes.SessionConstants(currentGameState);
                            TopicWindowTyres.SessionConstants(currentGameState);
                            if (!stateCleared)
                            {
                                Console.WriteLine("Clearing game state...");
                                audioPlayer.purgeQueues();

                                var _eventsList = eventsList;
                                foreach (KeyValuePair<String, AbstractEvent> entry in _eventsList)
                                {
                                    entry.Value.clearState();
                                }
                                spotter?.clearState();
                                faultingEvents.Clear();
                                faultingEventsCount.Clear();
                                sessionHasFailingEvent = false;
                                stateCleared = true;
                                PCarsGameStateMapper.FIRST_VIEWED_PARTICIPANT_NAME = null;
                                PCarsGameStateMapper.WARNED_ABOUT_MISSING_STEAM_ID = false;
                                PCarsGameStateMapper.FIRST_VIEWED_PARTICIPANT_INDEX = -1;
                            }
                            if (enableDriverNames)
                            {
                                List<String> rawDriverNames = currentGameState.getRawDriverNames();
                                if (currentGameState.SessionData.DriverRawName != null && currentGameState.SessionData.DriverRawName.Length > 0 &&
                                    !rawDriverNames.Contains(currentGameState.SessionData.DriverRawName))
                                {
                                    rawDriverNames.Add(currentGameState.SessionData.DriverRawName);
                                }
                                if (rawDriverNames.Count > 0)
                                {
                                    // load all the sound files for this set of driver names. Note this will recreate all their cleaned up and
                                    // mapped versions, and sounds which previously failed to match won't be in this set (we won't attempt to
                                    // match them again)
                                    SoundCache.loadDriverNameSounds(DriverNameHelper.getUsableDriverNameSounds(rawDriverNames));
                                    // if the SRE is active, load the appropriate phrases
                                    if (speechRecogniser != null && speechRecogniser.initialised)
                                    {
                                        speechRecogniser.addOpponentsSpeechRecognition(
                                            DriverNameHelper.getUsableDriverNamesForSRE(rawDriverNames), currentGameState.getCarNumbers());
                                    }
                                }
                            }
                            audioPlayer.wakeMonitorThreadForRegularMessages(currentGameState.Now);
                        }
                        else if (shouldTriggerEvents(previousGameState, currentGameState))
                        {
                            if (!sessionFinished)
                            {
                                if (spotter != null)
                                {
                                    if (DamageReporting.waitingForDriverIsOKResponse)
                                    {
                                        spotter.pause();
                                    }
                                    else if (currentGameState.FlagData.isFullCourseYellow)
                                    {
                                        if (turnSpotterOffImmediatelyOnFCY)
                                        {
                                            spotter.pause();
                                        }
                                        // in fcy, if the spotter's running wait a while before pausing it
                                        else if (!spotter.isPaused())
                                        {
                                            float speedThreshold = GlobalBehaviourSettings.useOvalLogic ? fcySpeedToTurnSpotterOffOnOvals : fcySpeedToTurnSpotterOffOnRoadCourses;
                                            if (!waitingToPauseSpotter)
                                            {
                                                waitingToPauseSpotter = true;
                                                minTurnSpotterOffForFCYTime = currentGameState.Now.Add(minTimeToWaitToTurnSpotterOffInFCY);
                                                maxTurnSpotterOffForFCYTime = currentGameState.Now.Add(maxTimeToWaitToTurnSpotterOffInFCY);
                                            }
                                            // if we've started a new lap, turn off the spotter.
                                            // if we've passed the max time to wait until turning him off, just turn him off. If we're between min and max, turn him
                                            // off but only if the speed is low *and* there's no overlap
                                            else if (currentGameState.SessionData.IsNewLap
                                                     || currentGameState.Now > maxTurnSpotterOffForFCYTime
                                                     || (currentGameState.Now > minTurnSpotterOffForFCYTime && currentGameState.PositionAndMotionData.CarSpeed < speedThreshold && !spotter.hasOverlap()))
                                            {
                                                waitingToPauseSpotter = false;
                                                spotter.pause();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        spotter.unpause();
                                    }
                                }
                                if (currentGameState.SessionData.IsNewLap)
                                {
                                    currentGameState.display();
                                }
                                stateCleared = false;
                            }
                            // update the auto-verbosity
                            PlaybackModerator.UpdateAutoVerbosity(currentGameState);

                            // increment the driver training service recording lap counter when we're recording and we start a new lap
                            if (currentGameState.SessionData.IsNewLap && DriverTrainingService.isRecordingPaceNotes && currentGameState.PositionAndMotionData.CarSpeed > 0.5)
                            {
                                DriverTrainingService.incrementPaceNotesRecordingLapCounter();
                            }
                            // increment the driver training service lap playback counter when we're playing back and we start a new lap
                            if (currentGameState.SessionData.IsNewLap && DriverTrainingService.isPlayingPaceNotes && currentGameState.PositionAndMotionData.CarSpeed > 0.5)
                            {
                                DriverTrainingService.incrementPaceNotesPlaybackLapCounter();
                            }

                            // Allow events to be processed after session finish.  Event should use applicableSessionPhases/applicableSessionTypes to opt in/out.
                            // for now, don't trigger any events for F1 20xx as there's no game mapping
                            if (!Game.F1_20S)
                            {
                                Boolean isPractice = currentGameState.SessionData.SessionType == SessionType.Practice || currentGameState.SessionData.SessionType == SessionType.LonePractice;
                                // before triggering events, see if we need to enable pace notes automatically.
                                if (this.autoEnablePacenotesInPractice && CrewChief.gameDefinition.racingType == RacingType.Circuit
                                    && currentGameState != null && previousGameState != null
                                    && !DriverTrainingService.isRecordingPaceNotes
                                    && isPractice)
                                {
                                    // trigger for stopping pace notes automatically - we've quit to pit or entered the pitlane
                                    Boolean enteredPit = (!previousGameState.PitData.IsInGarage && currentGameState.PitData.IsInGarage)
                                        || (!previousGameState.PitData.InPitlane && currentGameState.PitData.InPitlane);
                                    // trigger for automatically enabling pace notes in practice. Triggers when we leave the garage, we're handed control from the AI
                                    // or we're in the pits and our speed increases to 0.5 m/s. This is to (hopefully) catch cases where the game doesn't use AI
                                    // control in the pit and doesn't have a transition from garage to pitlane.
                                    Boolean exitedGarage = (previousGameState.PitData.IsInGarage && currentGameState.PitData.InPitlane)
                                        || (GameEventBool.PlayerTookControl)
                                        || (currentGameState.PitData.InPitlane && previousGameState.PositionAndMotionData.CarSpeed < 0.5 && currentGameState.PositionAndMotionData.CarSpeed >= 0.5);

                                    if (DriverTrainingService.isPlayingPaceNotes && enteredPit)
                                    {
                                        DriverTrainingService.stopPlayingPaceNotes();
                                    }
                                    else if (!DriverTrainingService.isPlayingPaceNotes && exitedGarage)
                                    {
                                        if (!DriverTrainingService.loadPaceNotes(CrewChief.gameDefinition.gameEnum,
                                                currentGameState.SessionData.TrackDefinition.name, currentGameState.carClass.carClassEnum, audioPlayer))
                                        {
                                            Console.WriteLine("Attempted to auto-start pace notes, but none are available for this circuit");
                                        }
                                    }
                                }

                                // Check for any GameEvents and GameBooleans that
                                // are triggered by a change in game state, and
                                // trigger them before we check the other events
                                // which may use the GameEvents or GameBooleans.
                                gameStateEventDetector?.trigger(previousGameState, currentGameState);

                                // Exception "Collection was modified; enumeration operation may not execute."
                                // reported by user, avoid it.
                                // A race condition? clearAndReloadEvents() is called from another thread?
                                // That's the only place it's modified.
                                var _eventsList = eventsList;
                                foreach (KeyValuePair<String, AbstractEvent> entry in _eventsList)
                                {
                                    if (entry.Value.isApplicableForCurrentSessionAndPhase(currentGameState.SessionData.SessionType, currentGameState.SessionData.SessionPhase))
                                    {
                                        // special case - if we've crashed heavily and are waiting for a response from the driver, don't trigger other events
                                        if (entry.Key.Equals("DamageReporting") || !DamageReporting.waitingForDriverIsOKResponse)
                                        {
                                            triggerEvent(entry.Key, entry.Value, previousGameState, currentGameState);
                                        }
                                    }
                                }
                                if (eventsList != _eventsList)
                                {
                                    Log.Warning($"eventsList was modified? Count: {eventsList.Count}");
                                }
                                audioPlayer.wakeMonitorThreadForRegularMessages(currentGameState.Now);
                            }
                            if (!sessionFinished)
                            {
                                if (DriverTrainingService.isPlayingPaceNotes)
                                {
                                    DriverTrainingService.checkValidAndPlayIfNeeded(currentGameState.Now,
                                        currentGameState.PositionAndMotionData.CarSpeed, currentGameState.PositionAndMotionData.Orientation.Yaw,
                                        previousGameState.PositionAndMotionData.DistanceRoundTrack,
                                        currentGameState.PositionAndMotionData.DistanceRoundTrack,
                                        currentGameState.PitData.InPitlane,
                                        audioPlayer);
                                }
                                if (spotter != null && GlobalBehaviourSettings.spotterEnabled && !spotterIsRunning &&
                                    (Game.F1_20S || Debug.PlaybackSpotter))
                                {
                                    Console.WriteLine("********** starting spotter***********");
                                    spotter.clearState();
                                    startSpotterThread();
                                }
                                else if (spotterIsRunning && !GlobalBehaviourSettings.spotterEnabled)
                                {
                                    runSpotterThread = false;
                                }
                            }
                        }
                        else
                        {
                            spotter?.pause();
                        }
                    }
                }

                if (filenameToRun != null)
                {
                    // mute the audio player for anything < 10ms

                    audioPlayer.mute = CrewChief.playbackIntervalMilliseconds < 10;
                    if (CrewChief.playbackIntervalMilliseconds > 0)
                    {
                        Thread.Sleep(CrewChief.playbackIntervalMilliseconds);
                        if (enableSharedMemory)
                        {
                            sharedMemoryManager.UpdateVariable("phraseIsPlaying", new bool[1] { audioPlayer.isChannelOpen() });
                            sharedMemoryManager.Tick(playbackIntervalMilliseconds);
                        }
                    }
                }
                else
                {
                    // iracing runs at 60Hz anyway, but for other games if we're collecting telemetry for charting, use the
                    // appropriate time interval
                    int interval = timeInterval;
                    if (CrewChief.currentGameState != null)
                    {
                        // TODO: this may be applicable to other games but limit it to R3E for now
                        if (Game.RACE_ROOM
                            && CrewChief.currentGameState.SessionData.SessionType == SessionType.Race
                            && CrewChief.currentGameState.SessionData.SessionPhase == SessionPhase.Countdown)
                        {
                            interval = CrewChief.startLightsInterval;
                        }
                        else if (useTelemetryIntervalWhereApplicable
                                 && (recordChartTelemetryDuringRace || CrewChief.currentGameState.SessionData.SessionType != SessionType.Race))
                        {
                            interval = CrewChief.intervalWhenCollectionTelemetry;
                        }
                    }
                    if (enableSharedMemory)
                    {
                        sharedMemoryManager.UpdateVariable("phraseIsPlaying", new bool[1] { audioPlayer.isChannelOpen() });
                        sharedMemoryManager.Tick(interval);
                    }
                    Thread.Sleep(interval);
                }
            }
            return true;
        }

        private void afterRunning()
        {
            GameOrVoipVolume.RestoreVolumes();

            var __eventsList = eventsList;
            foreach (KeyValuePair<String, AbstractEvent> entry in __eventsList)
            {
                // don't clear the overlay controller here - temporary hack
                if (entry.Key != "OverlayController")
                {
                    entry.Value.teardownState();
                }
            }
            spotter?.clearState();

            if (enableR3eWebsocket || enableR3eGameDataWebsocket)
            {
                Utilities.stopWebsocketServers();
            }

            stateCleared = true;
            currentGameState = null;
            previousGameState = null;
            // local to runGame() sessionFinished = false;
            faultingEvents.Clear();
            faultingEventsCount.Clear();
            PlaybackModerator.ClearVerbosityData();
            PlaybackModerator.lastBlockedMessageId = -1;

            if (audioPlayer != null)
            {
                audioPlayer.disablePearlsOfWisdom = false;
            }

            sessionHasFailingEvent = false;

            if (gameDataReader != null)
            {
                if (gameDataReader.dumpToFile)
                {
                    try
                    {
                        gameDataReader.DumpRawGameData();
                    }
                    finally
                    {
                        dataFileDumpDone = true;
                    }
                }
                dataFileDumpDone = true;
                try
                {
                    CrewChief.gameExeParentDirectory = null;
                    gameDataReader.stop();
                    gameDataReader.DisconnectFromProcess();
                }
                catch (Exception)
                {
                    //ignore
                }
            }

            if (SoundCache.dumpListOfUnvocalizedNames)
            {
                DriverNameHelper.dumpUnvocalizedNames();
            }
            mapped = false;
        }

        private bool shouldTriggerEvents(GameStateData previousGameState, GameStateData currentGameState)
        {
            // basic checks:
            if (previousGameState == null)
            {
                return false;
            }
            // time has advanced or session phase has changed so the session is running
            if (currentGameState.SessionData.SessionRunningTime > previousGameState.SessionData.SessionRunningTime
                || previousGameState.SessionData.SessionPhase != currentGameState.SessionData.SessionPhase)
            {
                return true;
            }
            // the AccGameClock has ticked forwards so the session is running (ACC only, obviously)
            if (previousGameState.AccGameClock > 0 && previousGameState.AccGameClock < currentGameState.AccGameClock)
            {
                return true;
            }

            // game-specific workarounds where the session is advancing and we want to trigger our events, but the game time / clock isn't advancing
            switch (gameDefinition.gameEnum)
            {
                case GameEnum.F1_2018:
                case GameEnum.F1_2019:
                case GameEnum.F1_2020:
                case GameEnum.F1_2021:
                case GameEnum.F1_2022:
                case GameEnum.F1_2023:
                    // F1 games have no session timer data so we have to allow the events to process:
                    return true;
                case GameEnum.PCARS2:
                case GameEnum.AMS2:
                case GameEnum.PCARS3:
                case GameEnum.PCARS2_NETWORK:
                case GameEnum.PCARS_64BIT:
                case GameEnum.PCARS_32BIT:
                    // undocumented hack from previous impl recreated here for consistency:
                    return currentGameState.SessionData.SessionPhase == SessionPhase.Countdown
                        || currentGameState.SessionData.SessionHasFixedTime && currentGameState.SessionData.SessionTotalRunTime == -1;
                case GameEnum.ACC:
                    // for ACC the game timer doesn't tick until we start and the clock doesn't tick until countdown, but we need data during formation laps. It never ticks in hotlap mode:
                    return currentGameState.SessionData.SessionPhase == SessionPhase.Formation
                        || currentGameState.SessionData.SessionType == SessionType.HotLap;
                case GameEnum.RF2_64BIT:
                case GameEnum.LMU:
                    // Need to process warnings during rF2's gridwalk
                    return currentGameState.SessionData.SessionPhase == SessionPhase.Gridwalk;
                default:
                    return false;
            }
        }

        private void triggerEvent(String eventName, AbstractEvent abstractEvent, GameStateData previousGameState, GameStateData currentGameState)
        {
            try
            {
                int failureCount;
                if (!sessionHasFailingEvent || !faultingEventsCount.TryGetValue(eventName, out failureCount) || failureCount < maxEventFailuresBeforeDisabling)
                {
                    abstractEvent.trigger(previousGameState, currentGameState);
                }
            }
            catch (Exception e)
            {
                int failureCount = 0;
                if (faultingEventsCount.TryGetValue(eventName, out failureCount))
                {
                    faultingEventsCount[eventName] = ++failureCount;
                    if (failureCount >= maxEventFailuresBeforeDisabling)
                    {
                        sessionHasFailingEvent = true;
                        Console.WriteLine("Event " + eventName +
                            " has failed " + maxEventFailuresBeforeDisabling + " times in this session and will be disabled");
                    }
                }
                if (!faultingEvents.ContainsKey(eventName))
                {
                    Log.Exception(e, "Event " + eventName + " threw exception ");
                    Console.WriteLine("This is the first time this event has failed in this session");
                    try
                    {
                        faultingEvents.Add(eventName, e.Message);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Race condition? 'An item with the same key has already been added'" + ex.Message);
                    }
                    faultingEventsCount.Add(eventName, 1);
                }
                else if (faultingEvents[eventName] != e.Message)
                {
                    Log.Exception(e, "Event " + eventName + " threw a different exception: ");
                    faultingEvents[eventName] = e.Message;
                }
            }
        }

        public void stop()
        {
            running = false;
            runSpotterThread = false;
            if (audioPlayer != null)
            {
                audioPlayer.monitorRunning = false;
            }
            // set status of shared mem to connected
            if (enableSharedMemory)
            {
                sharedMemoryManager.UpdateVariable("updateStatus", new int[1] { (int)UpdateStatus.connected });
                sharedMemoryManager.Tick(0, UpdateStatus.connected);
            }
        }

        private void displayNewSessionInfo(GameStateData currentGameState)
        {
            Console.WriteLine("New session details...");
            Console.WriteLine("SessionType: " + currentGameState.SessionData.SessionType);
            Console.WriteLine("EventIndex: " + currentGameState.SessionData.EventIndex);
            Console.WriteLine("SessionIteration: " + currentGameState.SessionData.SessionIteration);
            String trackName = currentGameState.SessionData.TrackDefinition == null ? "unknown" : currentGameState.SessionData.TrackDefinition.name;
            LatestTrackName = trackName;
            Console.WriteLine("TrackName: \"" + trackName + "\"");

            if (currentGameState.SessionData.TrackDefinition != null)
                Console.WriteLine($"TrackLength: {currentGameState.SessionData.TrackDefinition.trackLength.ToString("0.000")}m");
        }

        // This has to be called before starting man Chief thread (runApp).
        public void onRestart()
        {
            dataFileReadDone = false;
            dataFileDumpDone = false;
        }

        public Spotter getSpotter()
        {
            return spotter;
        }
    }
}
