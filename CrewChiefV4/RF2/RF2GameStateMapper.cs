//#define SIMULATE_ONLINE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

using CrewChiefV4.GameState;
using CrewChiefV4.Events;
using rF2SharedMemory;
using static rF2SharedMemory.rFactor2Constants;
using rF2SharedMemory.rFactor2Data;
using CrewChiefV4.Audio;

/**
 * Maps memory mapped file to a local game-agnostic representation.
 */
namespace CrewChiefV4.rFactor2
{
    public class RF2GameStateMapper : GameStateMapper
    {
        // User preference constants.
        private bool enablePitStopPrediction;
        private bool enableFrozenOrderMessages;
        private bool enableCutTrackHeuristics;
        private bool enablePitLaneApproachHeuristics;
        private bool enableFCYPitStateMessages;
        private bool useRealWheelSizeForLockingAndSpinning;
        private bool enableWrongWayMessage;
        private bool disableRaceEndMessagesOnAbandon;

        private readonly string scrLuckyDogIsPrefix = "Lucky Dog: ";

        public static string playerName = null;

        private List<CornerData.EnumWithThresholds> suspensionDamageThresholds = new List<CornerData.EnumWithThresholds>();
        private List<CornerData.EnumWithThresholds> tyreWearThresholds = new List<CornerData.EnumWithThresholds>();

        // Numbers below are pretty good match for HUD colors, all series, tire types.  Yay for best in class physics ;)
        // Exact thresholds/ probably depend on tire type/series, even user preferences.  Maybe expose this via car class data in the future.
        private float scrubbedTyreWearPercent = 5.0f;
        private float minorTyreWearPercent = 20.0f;  // Turns Yellow in HUD.
        private float majorTyreWearPercent = 50.0f;  // Turns Red in HUD.
        private float wornOutTyreWearPercent = 75.0f;  // Still not black, but tires are usually almost dead.

        private List<CornerData.EnumWithThresholds> brakeTempThresholdsForPlayersCar = null;

        // At which point we consider that it is raining
        private const double minRainThreshold = 0.1;

        // Pit stop prediction constants.
        private const int minMinutesBetweenPredictedStops = 10;
        private const int minLapsBetweenPredictedStops = 5;

        // On 3930k@4.6 transitions sometimes take above 2 secs,
        // the issue is that if we leave to monitor, long delayed message is a bit annoying, might need to revisit.
        private const int waitForSessionEndMillis = 2500;

        // If we're running only against AI, force the pit window to open
        private bool isOfflineSession = true;

        // Private practice detection hacks.
        private int lastPracticeNumVehicles = -1;
        private int lastPracticeNumNonGhostVehicles = -1;

        // Keep track of opponents processed this time
        private List<string> opponentKeysProcessed = new List<string>();

        // Detect when approaching racing surface after being off track
        private float distanceOffTrack = 0.0f;

        // Pit stop detection tracking variables.
        private double minTrackWidth = -1;

        // Experimantal: If true, means we completed at least one full lap since exiting the pits (out lap excluded)
        // and minTrackWidth is ready to be used for pit lane approach detection.
        // However, it looks like depending on track authoring errors this might cause bad width stuck, so this is by default disabled currently.
        private DateTime timePitStopRequested = DateTime.MinValue;
        private bool isApproachingPitEntry = false;

        // Detect if there any changes in the the game data since the last update.
        private double lastPlayerTelemetryET = -1.0;
        private double lastScoringET = -1.0;

        // True if it looks like track has no DRS zones defined.
        private bool detectedTrackNoDRSZones = false;

        // Track landmarks cache.
        private string lastSessionTrackName = null;
        private TrackDataContainer lastSessionTrackDataContainer = null;
        private HardPartsOnTrackData lastSessionHardPartsOnTrackData = null;
        private double lastSessionTrackLength = -1.0;

        // Next track conditions sample due after:
        private DateTime nextConditionsSampleDue = DateTime.MinValue;

        // Weather forecast from the REST API
        private readonly RF2WeatherForecast weatherForecast = new RF2WeatherForecast();

        private DateTime lastTimeEngineWasRunning = DateTime.MaxValue;

        // Penalty state.
        private readonly TimeSpan lastPenaltyCheckWndow = TimeSpan.FromSeconds(2);
        private DateTime lastPenaltyTime = DateTime.MinValue;

        // Session caches:
        private Dictionary<string, TyreType> compoundNameToTyreType = new Dictionary<string, TyreType>();

        class CarInfo
        {
            public CarData.CarClass carClass = null;
            public string driverNameRawSanitized = null;
            public bool isGhost = false;
        }

        private Dictionary<long, CarInfo> idToCarInfoMap = new Dictionary<long, CarInfo>();

        // Message center stuff
        private Int64 lastHistoryMessageUpdatedTicks = 0L;
#if DEBUG
        private Int64 statusMessageUpdatedTicks = 0L;
#endif
        private Int64 LSIPitStateMessageUpdatedTicks = 0L;
        private Int64 LSIRulesInstructionMessageUpdatedTicks = 0L;
        private Int64 LSIOrderInstructionMessageUpdatedTicks = 0L;

        // Since some of MC messages disapper (Player Control: N, for example), we need to remember last message that
        // mattered from CC's standpoint, otherwise, same message could get applied multiple times.
        private string lastEffectiveHistoryMessage = string.Empty;

        // It is however sometimes valid for message to re-appear.  For example, Crew got ready in two separate pit
        // requests.  Allow message processed to expire.
        private readonly int effectiveMessageExpirySeconds = 120;
        private DateTime timeEffectiveMessageProcessed = DateTime.MinValue;
        private DateTime timeHistoryMessageIgnored = DateTime.MinValue;
        private DateTime timeLSIMessageIgnored = DateTime.MinValue;
        private int numFODetectPhaseAttempts = 0;
        private const int maxFormationStandingCheckAttempts = 5;
        private bool safetyCarLeft = false;

        public RF2GameStateMapper()
        {
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.NEW, -10000.0f, this.scrubbedTyreWearPercent));
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.SCRUBBED, this.scrubbedTyreWearPercent, this.minorTyreWearPercent));
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.MINOR_WEAR, this.minorTyreWearPercent, this.majorTyreWearPercent));
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.MAJOR_WEAR, this.majorTyreWearPercent, this.wornOutTyreWearPercent));
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.WORN_OUT, this.wornOutTyreWearPercent, 10000.0f));

            this.suspensionDamageThresholds.Add(new CornerData.EnumWithThresholds(DamageLevel.NONE, 0.0f, 1.0f));
            this.suspensionDamageThresholds.Add(new CornerData.EnumWithThresholds(DamageLevel.DESTROYED, 1.0f, 2.0f));

            if (Game.RF2_64BIT)
            {
                enablePitStopPrediction = UserSettings.GetUserSettings().getBoolean("enable_rf2_pit_stop_prediction");
                enableFrozenOrderMessages = UserSettings.GetUserSettings().getBoolean("enable_rf2_frozen_order_messages");
                enableCutTrackHeuristics = UserSettings.GetUserSettings().getBoolean("enable_rf2_cut_track_heuristics");
                enablePitLaneApproachHeuristics = UserSettings.GetUserSettings().getBoolean("enable_rf2_pit_lane_approach_heuristics");
                enableFCYPitStateMessages = UserSettings.GetUserSettings().getBoolean("enable_rf2_pit_state_during_fcy");
                useRealWheelSizeForLockingAndSpinning = UserSettings.GetUserSettings().getBoolean("use_rf2_wheel_size_for_locking_and_spinning");
                enableWrongWayMessage = UserSettings.GetUserSettings().getBoolean("enable_rf2_wrong_way_message");
                disableRaceEndMessagesOnAbandon = UserSettings.GetUserSettings().getBoolean("disable_rf2_race_end_messages_on_abandoned_sessions");
            }
            else // Game.LMU
            {
                enablePitStopPrediction = UserSettings.GetUserSettings().getBoolean("enable_lmu_pit_stop_prediction");
                enableFrozenOrderMessages = UserSettings.GetUserSettings().getBoolean("enable_lmu_frozen_order_messages");
                enableCutTrackHeuristics = UserSettings.GetUserSettings().getBoolean("enable_lmu_cut_track_heuristics");
                enablePitLaneApproachHeuristics = UserSettings.GetUserSettings().getBoolean("enable_lmu_pit_lane_approach_heuristics");
                enableFCYPitStateMessages = UserSettings.GetUserSettings().getBoolean("enable_lmu_pit_state_during_fcy");
                useRealWheelSizeForLockingAndSpinning = UserSettings.GetUserSettings().getBoolean("use_lmu_wheel_size_for_locking_and_spinning");
                enableWrongWayMessage = UserSettings.GetUserSettings().getBoolean("enable_lmu_wrong_way_message");
                disableRaceEndMessagesOnAbandon = UserSettings.GetUserSettings().getBoolean("disable_lmu_race_end_messages_on_abandoned_sessions");
            }
        }

        private static readonly bool useLegacyRF2SharedMemoryFilenames = UserSettings.GetUserSettings().
            getBoolean("use_legacy_rf2_shared_memory_filenames");

        private readonly int[] minimumSupportedVersionParts = useLegacyRF2SharedMemoryFilenames
            ? new int[] { 3, 7, 0, 0 }
            : new int[] { 3, 8, 0, 0 };
        private readonly int[] currentVersionParts = new int[] { 3, 8, 0, 1 };
        public static bool pluginVerified = false;
        private static int reinitWaitAttempts = 0;
        private MExpansionCache mExpansionCache = new MExpansionCache();
        public override void versionCheck(Object memoryMappedFileStruct)
        {
            if (RF2GameStateMapper.pluginVerified || CrewChief.loadDataFromFile)
                return;

            var shared = memoryMappedFileStruct as RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper;

            mExpansionCache.mExpansionDebug(shared);

            var versionStr = RF2GameStateMapper.GetStringFromBytes(shared.extended.mVersion);
            if (string.IsNullOrWhiteSpace(versionStr)
                && RF2GameStateMapper.reinitWaitAttempts < 500)
            {
                // SimHub (and possibly other tools) leaks the shared memory block, making us read the empty one.
                // Wait a bit before re-checking version string.
                ++RF2GameStateMapper.reinitWaitAttempts;
                Thread.Sleep(100);
                return;
            }

            // Only verify once.
            RF2GameStateMapper.pluginVerified = true;
            RF2GameStateMapper.reinitWaitAttempts = 0;

            var failureHelpMsg = ".\nMake sure you have \"Update game plugins on startup\" option enabled."
                + "\nAlternatively, visit https://forum.studio-397.com/index.php?threads/crew-chief-v4-5-with-rfactor-2-support.54421/ "
                + "to download and update plugin manually.";

            var versionParts = versionStr.Split('.');
            if (versionParts.Length != 4)
            {
                Log.Error($"Corrupt or leaked {CrewChief.gameDefinition.friendlyName} Shared Memory.  Version string: " + versionStr + failureHelpMsg);
                return;
            }

            int smVer = 0;
            int minVer = 0;
            int currentVer = 0;
            int partFactor = 1;
            for (int i = 3; i >= 0; --i)
            {
                int versionPart = 0;
                if (!int.TryParse(versionParts[i], out versionPart))
                {
                    Log.Error($"Corrupt or leaked {CrewChief.gameDefinition.friendlyName} Shared Memory version.  Version string: " + versionStr + failureHelpMsg);
                    return;
                }

                smVer += (versionPart * partFactor);
                minVer += (this.minimumSupportedVersionParts[i] * partFactor);
                currentVer += (this.currentVersionParts[i] * partFactor);
                partFactor *= 100;
            }

            if (shared.extended.is64bit == 0)
                Log.Error("Only 64bit version of rFactor 2 is supported.   Plugin version is:"  + versionStr + failureHelpMsg);
            else if (smVer < minVer)
            {
                var minVerStr = string.Join(".", this.minimumSupportedVersionParts);
                var msg1 = $"Unsupported {CrewChief.gameDefinition.friendlyName} Shared Memory version: " + versionStr;

                var msg2 = "Minimum supported version is: "
                    + minVerStr
                    + "\nPlease update rFactor2SharedMemoryMapPlugin64.dll" + failureHelpMsg;
                Log.Error(msg1 + " " + msg2);
                MessageBox.Show(msg2, msg1,
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            }
            else
            {
                if (smVer < currentVer)
                {
                    var currentVerStr = string.Join(".", this.currentVersionParts);
                    Log.Warning($"Outdated {CrewChief.gameDefinition.friendlyName} Shared Memory version is: {versionStr} but the current version is: {currentVerStr}.  Please update rFactor2SharedMemoryMapPlugin64.dll" + failureHelpMsg);
                }
                if (Utilities.IsFlagOn(shared.extended.mUnsubscribedBuffersMask, SubscribedBuffer.Telemetry))
                {
                    Log.Error($"Telemetry buffer updates are disabled, Crew Chief will not work correctly.  UnsubscribedBuffersMask: {shared.extended.mUnsubscribedBuffersMask}.");
                    return;
                }

                if (Utilities.IsFlagOn(shared.extended.mUnsubscribedBuffersMask, SubscribedBuffer.Scoring))
                {
                    Log.Error($"Scoring buffer updates are disabled, Crew Chief will not work correctly.  UnsubscribedBuffersMask: {shared.extended.mUnsubscribedBuffersMask}.");
                    return;
                }

                if (Utilities.IsFlagOn(shared.extended.mUnsubscribedBuffersMask, SubscribedBuffer.Rules))
                {
                    Log.Error($"Rules buffer updates are disabled, Crew Chief will not work correctly.  UnsubscribedBuffersMask: {shared.extended.mUnsubscribedBuffersMask}.");
                    return;
                }

                var msg = $"{CrewChief.gameDefinition.friendlyName} Shared Memory version: " + versionStr + " 64bit."
                    + (shared.extended.mDirectMemoryAccessEnabled != 0 && shared.extended.mSCRPluginEnabled != 0 ? ("  Stock Car Rules plugin enabled. (DFT:" + shared.extended.mSCRPluginDoubleFileType + ")")  : "")
                    + (shared.extended.mDirectMemoryAccessEnabled != 0 ? "  DMA enabled." : "")
                    + "  UBM: " + shared.extended.mUnsubscribedBuffersMask;
                Console.WriteLine(msg);
            }
        }

        /// <summary>
        /// Some bytes in shared.scoring.mVehicles[].mExpansion are non-zero,
        /// log them if -debug to see if they are interesting.
        /// Stored in Documents\CrewChiefV4\CrewChiefV4\RF2\mVehicles.mExpansion.txt
        /// or Documents\CrewChiefV4\CrewChiefV4\LMU\mVehicles.mExpansion.txt
        /// </summary>
        private class MExpansionCache
        {
            private readonly string cacheFile;
            private readonly HashSet<string> cache;

            public MExpansionCache()
            {
                var folder = Game.RF2_64BIT ? 
                    DataFiles.RF2Folder :
                    DataFiles.LMUFolder;
                cacheFile = Path.Combine(folder, "mVehicles.mExpansion.txt");
                cache = new HashSet<string>(StringComparer.Ordinal);

                if (File.Exists(cacheFile))
                {
                    foreach (var line in File.ReadAllLines(cacheFile))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            cache.Add(line.Trim());
                    }
                }
            }

            internal void mExpansionDebug(in RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper shared)
            {
                if (!CrewChief.Debug.DebugCommandLineOption)
                {
                    return;
                }
                // --- mExpansion cache logic ---
                for (var i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
                {
                    var expansion = shared.scoring.mVehicles[i].mExpansion;
                    if (expansion != null && expansion.Length > 8)
                    {
                        // Check if any bytes are non-zero
                        var hasNonZero = false;
                        foreach (var t in expansion)
                        {
                            if (t != 0)
                            {
                                hasNonZero = true;
                                break;
                            }
                        }
                        if (hasNonZero)
                        {
                            // Create a record (string representation)
                            var record = RF2GameStateMapper.GetStringFromBytes(shared.scoring.mVehicles[i].mExpansion.Skip(8).ToArray()) +
                                         ": " + BitConverter.ToString(expansion);

                            // Check if already in cache, if not add and write
                            if (!cache.Contains(record))
                            {
                                cache.Add(record);
                                File.AppendAllText(cacheFile, record + Environment.NewLine);
                            }
                        }
                    }
                }
            }
        }

        // Abrupt session detection variables.
        private bool waitingToTerminateSession = false;
        private long ticksWhenSessionEnded = DateTime.MinValue.Ticks;

        // Used to reduce number of "Waiting" messages on abrupt session end.
        private int sessionWaitMessageCounter = 0;

        private Int64 lastSessionEndTicks = -1;
        private bool lastInRealTimeState = false;

        private void ClearState()
        {
            this.lastPlayerTelemetryET = -1.0;
            this.lastScoringET = -1.0;

            this.waitingToTerminateSession = false;
            this.isOfflineSession = true;
            this.lastPracticeNumVehicles = -1;
            this.lastPracticeNumNonGhostVehicles = -1;
            this.distanceOffTrack = 0;
            this.detectedTrackNoDRSZones = false;
            this.minTrackWidth = -1.0;
            this.timePitStopRequested = DateTime.MinValue;
            this.isApproachingPitEntry = false;
            this.lastTimeEngineWasRunning = DateTime.MaxValue;
            this.compoundNameToTyreType.Clear();
            this.idToCarInfoMap.Clear();
            this.lastPenaltyTime = DateTime.MinValue;

            // Do not reset MC tracking variables as "Disqualified" messages seem to stick for a bit on restart.
            //this.lastEffectiveHistoryMessage = string.Empty;
            //this.timeEffectiveMessageProcessed = DateTime.MinValue;

            this.timeHistoryMessageIgnored = DateTime.MinValue;
            this.timeLSIMessageIgnored = DateTime.MinValue;
            this.numFODetectPhaseAttempts = 0;
            this.safetyCarLeft = false;
            this.lastHistoryMessageUpdatedTicks = 0L;
        }

        public override GameStateData mapToGameStateData(Object memoryMappedFileStruct, GameStateData previousGameState)
        {
            var pgs = previousGameState;
            var shared = memoryMappedFileStruct as RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper;
            var cgs = new GameStateData(shared.ticksWhenRead);
            cgs.rawGameData = shared;
            //
            // This block has two purposes:
            //
            // * If no session is active it just returns previous game state, except if abrupt session end detection is in progress.
            //
            // * Terminate game sessions that did not go to "Finished" state.  Most often this happens because user finishes session early
            //   by clicking "Next Session", any "Restart" button or leaves to the main menu.  However, we may end up in that situation as well,
            //   simply because we're reading shared memory, and we might miss some transitions.
            //   One particularly interesting case is that sometimes, game updates state between session ended/started states.
            //   This was observed, in particular, after qualification.  This code tries to extract most current position in such case.
            //
            // Note: if we're in progress of detecting session end (this.waitingToTerminateSession == true), we will skip first frame of the new session
            // which should be ok.
            //

            // Check if session has _just_ ended and we are possibly hanging in between.
            var sessionJustEnded = shared.extended.mTicksSessionEnded != 0 && this.lastSessionEndTicks != shared.extended.mTicksSessionEnded;

            this.lastSessionEndTicks = shared.extended.mTicksSessionEnded;
            var sessionStarted = shared.extended.mSessionStarted == 1;

            if (shared.scoring.mScoringInfo.mNumVehicles == 0  // No session data (game startup, new session or game shutdown).
                || sessionJustEnded  // Need to start the wait for the next session
                || this.waitingToTerminateSession  // Wait for the next session (or timeout) is in progress
                || !sessionStarted)  // We don't process game state updates outside of the active session
            {
                //
                // If we have a previous game state and it's in a valid phase here, update it to "Finished" and return it,
                // unless it looks like user clicked "Restart" button during the race.
                // Additionally, if user made no valid laps in a session, mark it as DNF, because position does not matter in that case
                // (and it isn't reported by the game, so whatever we announce is wrong).  Lastly, try updating end position to match
                // the one captured during last session transition.
                //
                if (pgs != null
                    && pgs.SessionData.SessionType != SessionType.Unavailable
                    && pgs.SessionData.SessionPhase != SessionPhase.Finished
                    && pgs.SessionData.SessionPhase != SessionPhase.Unavailable)
                {
                    // Begin the wait for session re-start or a run out of time
                    if (!this.waitingToTerminateSession && !sessionStarted)
                    {
                        Console.WriteLine("Abrupt Session End: start to wait for session end.");

                        // Start waiting for session end.
                        this.ticksWhenSessionEnded = DateTime.UtcNow.Ticks;
                        this.waitingToTerminateSession = true;
                        this.sessionWaitMessageCounter = 0;

                        return pgs;
                    }

                    var sessionEndWaitTimedOut = false;
                    if (!sessionStarted)
                    {
                        var timeSinceWaitStarted = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - this.ticksWhenSessionEnded);
                        if (timeSinceWaitStarted.TotalMilliseconds < RF2GameStateMapper.waitForSessionEndMillis)
                        {
                            if (this.sessionWaitMessageCounter % 10 == 0)
                                Console.WriteLine("Abrupt Session End: continue session end wait.");

                            this.sessionWaitMessageCounter++;

                            return pgs;
                        }
                        else
                        {
                            Console.WriteLine("Abrupt Session End: session end wait timed out.");
                            sessionEndWaitTimedOut = true;
                        }
                    }
                    else
                        Console.WriteLine("Abrupt Session End: new session just started, terminate previous session.");

                    // Wait is over.  Terminate the abrupt session.
                    this.waitingToTerminateSession = false;

                    if (this.lastInRealTimeState && pgs.SessionData.SessionType == SessionType.Race)
                    {
                        // Looks like race restart without exiting to monitor.  We can't reliably detect session end
                        // here, because it is timing affected (we might miss this between updates).  So better not do it.
                        Console.WriteLine("Abrupt Session End: suppressed due to restart during real time.");
                    }
                    else if (this.disableRaceEndMessagesOnAbandon && this.isOfflineSession && sessionEndWaitTimedOut)
                        Console.WriteLine("Abrupt Session End: suppressed due to session abandoned.");
                    else if (this.disableRaceEndMessagesOnAbandon && this.isOfflineSession
                        && !this.lastInRealTimeState && pgs.SessionData.SessionType == SessionType.Race)
                        Console.WriteLine("Abrupt Session End: suppressed due to race restart in the monitor.");
                    else
                    {
                        if (pgs.SessionData.PlayerLapTimeSessionBest < 0.0f && !pgs.SessionData.IsDisqualified)
                        {
                            // If user has not set any lap time during the session, mark it as DNF.
                            pgs.SessionData.IsDNF = true;

                            Console.WriteLine("Abrupt Session End: mark session as DNF due to no valid laps made.");
                        }

                        // Get the latest position info available.  Try to find player's vehicle.
                        int playerVehIdx = -1;
                        for (int i = 0; i < shared.extended.mSessionTransitionCapture.mNumScoringVehicles; ++i)
                        {
                            if (shared.extended.mSessionTransitionCapture.mScoringVehicles[i].mIsPlayer == 1)
                            {
                                playerVehIdx = i;
                                break;
                            }
                        }

                        if (playerVehIdx != -1)
                        {
                            var playerVehCapture = shared.extended.mSessionTransitionCapture.mScoringVehicles[playerVehIdx];
                            if (pgs.SessionData.OverallPosition != playerVehCapture.mPlace)
                            {
                                Console.WriteLine(string.Format("Abrupt Session End: player position was updated after session end, updating overall pos {0} to: {1}.",
                                    pgs.SessionData.OverallPosition, playerVehCapture.mPlace));
                                pgs.SessionData.OverallPosition = playerVehCapture.mPlace;

                                var cpBefore = pgs.SessionData.ClassPosition;
                                pgs.sortClassPositions();
                                Console.WriteLine($"Abrupt Session End: updating class position from {cpBefore} to: {pgs.SessionData.ClassPosition}.");
                            }
                        }
                        else
                            Console.WriteLine("Abrupt Session End: failed to locate player vehicle info capture.");

                        // While this detects the "Next Session" this still sounds a bit weird if user clicks
                        // "Leave Session" and goes to main menu.  60 sec delay (minSessionRunTimeForEndMessages) helps, but not entirely.
                        pgs.SessionData.SessionPhase = SessionPhase.Finished;
                        pgs.SessionData.AbruptSessionEndDetected = true;
                        Console.WriteLine("Abrupt Session End: ended SessionType: " + pgs.SessionData.SessionType);

                        return pgs;
                    }
                }

                // Session is not in progress and no abrupt session end detection is in progress, simply return pgs.
                Debug.Assert(!this.waitingToTerminateSession, "Previous abrupt session end detection hasn't ended correctly.");

                this.ClearState();

                if (pgs != null)
                {
                    pgs.SessionData.SessionType = SessionType.Unavailable;
                    pgs.SessionData.SessionPhase = SessionPhase.Unavailable;
                    pgs.SessionData.AbruptSessionEndDetected = false;
                }

                return pgs;
            }

            this.lastInRealTimeState = shared.extended.mInRealtimeFC == 1 || shared.scoring.mScoringInfo.mInRealtime == 1;
            cgs.inCar = this.lastInRealTimeState;

            // --------------------------------
            // session data
            // get player scoring info (usually index 0)
            // get session leader scoring info (usually index 1 if not player)
            var playerScoring = new rF2VehicleScoring();
            var leaderScoring = new rF2VehicleScoring();
            for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicle = shared.scoring.mVehicles[i];
                switch (this.MapToControlType((rF2Control)vehicle.mControl))
                {
                    case ControlType.AI:
                    case ControlType.Player:
                    case ControlType.Remote:
                        if (vehicle.mIsPlayer == 1)
                            playerScoring = vehicle;

                        if (vehicle.mPlace == 1)
                            leaderScoring = vehicle;
                        break;

                    default:
                        continue;
                }

                if (playerScoring.mIsPlayer == 1 && leaderScoring.mPlace == 1)
                    break;
            }

            // Can't find the player or session leader vehicle info (replay).  No useful data is available.
            if (playerScoring.mIsPlayer != 1 || leaderScoring.mPlace != 1)
            {
                if (pgs != null)
                    pgs.SessionData.AbruptSessionEndDetected = false;  // Not 100% sure how this happened, but I saw us entering inifinite session restart due to this sticking.

                return pgs;
            }

            if (RF2GameStateMapper.playerName == null)
            {
                var driverName = RF2GameStateMapper.GetStringFromBytes(playerScoring.mDriverName);
                AdditionalDataProvider.validate(driverName);
                RF2GameStateMapper.playerName = driverName;
            }

            // Get player and leader telemetry objects.
            // NOTE: Those are not available on first entry to the garage and likely in rare
            // cases during online races.  But using just zeroed structs are mostly ok.
            var playerTelemetry = new rF2VehicleTelemetry();

            // This is shaky part of the mapping, but here goes:
            // Telemetry and Scoring are updated separately by the game.  Therefore, one can be
            // ahead of another, sometimes in a significant way.  Particularly, this is possible with
            // online races, where people quit/join the game.
            //
            // For Crew Chief in rF2, our primary data structure is _Scoring_ (because it contains timings).
            // However, since Telemetry is updated more frequently (up to 90FPS vs 5FPS for Scoring), we
            // try to use Telemetry values whenever possible (position, speed, elapsed time, orientation).
            // In those rare cases where Scoring contains vehicle that is not in Telemetry set, use Scoring as a
            // fallback where possible.  For the rest of values, use zeroed out Telemetry object (playerTelemetry).
            bool playerTelemetryAvailable = true;

            var idsToTelIndicesMap = RF2GameStateMapper.getIdsToTelIndicesMap(ref shared.telemetry);
            int playerTelIdx = -1;
            if (idsToTelIndicesMap.TryGetValue(playerScoring.mID, out playerTelIdx))
                playerTelemetry = shared.telemetry.mVehicles[playerTelIdx];
            else
            {
                playerTelemetryAvailable = false;
                RF2GameStateMapper.InitEmptyVehicleTelemetry(ref playerTelemetry);

                // Exclude known situations when telemetry is not available, but log otherwise to get more
                // insights.
                if (shared.extended.mInRealtimeFC == 1
                    && shared.scoring.mScoringInfo.mInRealtime == 1
                    && shared.scoring.mScoringInfo.mGamePhase != (byte)rFactor2Constants.rF2GamePhase.GridWalk)
                {
                    Console.WriteLine("Failed to obtain player telemetry, falling back to scoring.");
                }
            }

            // See if there are meaningful updates to the data.
            var currPlayerTelET = playerTelemetry.mElapsedTime;
            var currScoringET = shared.scoring.mScoringInfo.mCurrentET;

            if (currPlayerTelET == this.lastPlayerTelemetryET
                && currScoringET == this.lastScoringET)
            {
                if (pgs != null)
                    pgs.SessionData.AbruptSessionEndDetected = false;  // Not 100% sure how this happened, but I saw us entering inifinite session restart due to this sticking.

                return pgs;  // Skip this update.
            }

            this.lastPlayerTelemetryET = currPlayerTelET;
            this.lastScoringET = currScoringET;

            // Get player vehicle track rules.
            var playerRulesIdx = -1;
#if !SIMULATE_ONLINE
            for (int i = 0; i < shared.rules.mTrackRules.mNumParticipants; ++i)
            {
                if (shared.rules.mParticipants[i].mID == playerScoring.mID)
                {
                    playerRulesIdx = i;
                    break;
                }
            }
#endif

            // these things should remain constant during a session
            var csd = cgs.SessionData;
            var psd = pgs != null ? pgs.SessionData : null;
            csd.EventIndex = shared.scoring.mScoringInfo.mSession;

            csd.SessionIteration
                = shared.scoring.mScoringInfo.mSession >= 1 && shared.scoring.mScoringInfo.mSession <= 4 ? shared.scoring.mScoringInfo.mSession - 1 :
                shared.scoring.mScoringInfo.mSession >= 5 && shared.scoring.mScoringInfo.mSession <= 8 ? shared.scoring.mScoringInfo.mSession - 5 :
                shared.scoring.mScoringInfo.mSession >= 10 && shared.scoring.mScoringInfo.mSession <= 13 ? shared.scoring.mScoringInfo.mSession - 10 : 0;

            csd.SessionType = this.MapToSessionType(shared);
            csd.SessionPhase = this.mapToSessionPhase((rF2GamePhase)shared.scoring.mScoringInfo.mGamePhase, csd.SessionType, ref playerScoring);

            csd.SessionNumberOfLaps = shared.scoring.mScoringInfo.mMaxLaps > 0 && shared.scoring.mScoringInfo.mMaxLaps < 1000 ? shared.scoring.mScoringInfo.mMaxLaps : 0;

            // default to 60:30 if both session time and number of laps undefined (test day)
            float defaultSessionTotalRunTime = 3630.0f;
            csd.SessionTotalRunTime
                = (float)shared.scoring.mScoringInfo.mEndET > 0.0f
                    ? (float)shared.scoring.mScoringInfo.mEndET
                    : csd.SessionNumberOfLaps > 0 ? 0.0f : defaultSessionTotalRunTime;

            // If any difference between current and previous states suggests it is a new session
            if (pgs == null
                || csd.SessionType != psd.SessionType
                || csd.EventIndex != psd.EventIndex
                || csd.SessionIteration != psd.SessionIteration)
            {
                csd.IsNewSession = true;
            }
            // Else, if any difference between current and previous phases suggests it is a new session
            else if ((psd.SessionPhase == SessionPhase.Checkered
                        || psd.SessionPhase == SessionPhase.Finished
                        || psd.SessionPhase == SessionPhase.Green
                        || psd.SessionPhase == SessionPhase.FullCourseYellow
                        || psd.SessionPhase == SessionPhase.Unavailable)
                    && (csd.SessionPhase == SessionPhase.Garage
                        || csd.SessionPhase == SessionPhase.Gridwalk
                        || csd.SessionPhase == SessionPhase.Formation
                        || csd.SessionPhase == SessionPhase.Countdown))
            {
                csd.IsNewSession = true;
            }

            if (csd.IsNewSession)
            {
                // Do not use previous game state if this is the new session.
                pgs = null;

                this.ClearState();

                // Initialize variables that persist for the duration of a session.
                var cci = this.GetCachedCarInfo(ref playerScoring);
                Debug.Assert(!cci.isGhost);

                cgs.carClass = cci.carClass;
                CarData.CLASS_ID = cgs.carClass.getClassIdentifier();
                this.brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(cgs.carClass);
                csd.DriverRawName = cci.driverNameRawSanitized;
                csd.TrackDefinition = TrackData.getTrackDefinition(RF2GameStateMapper.GetStringFromBytes(shared.scoring.mScoringInfo.mTrackName), (float)shared.scoring.mScoringInfo.mLapDist);

                GlobalBehaviourSettings.UpdateFromCarClass(cgs.carClass);

                this.ProcessTyreTypeClassMapping(cgs.carClass);

                // Initialize track landmarks for this session.
                TrackDataContainer tdc = null;
                if (this.lastSessionTrackDataContainer != null
                    && this.lastSessionTrackName == csd.TrackDefinition.name
                    && this.lastSessionTrackLength == shared.scoring.mScoringInfo.mLapDist)
                {
                    tdc = this.lastSessionTrackDataContainer;

                    if (this.lastSessionHardPartsOnTrackData != null
                        && this.lastSessionHardPartsOnTrackData.hardPartsMapped)
                        cgs.hardPartsOnTrackData = this.lastSessionHardPartsOnTrackData;

                    if (tdc.trackLandmarks.Count > 0)
                        Console.WriteLine(tdc.trackLandmarks.Count + " landmarks defined for this track");
                }
                else
                {
                    tdc = TrackData.TRACK_LANDMARKS_DATA.getTrackDataForTrackName(csd.TrackDefinition.name, (float)shared.scoring.mScoringInfo.mLapDist);

                    this.lastSessionTrackDataContainer = tdc;
                    this.lastSessionHardPartsOnTrackData = null;
                    this.lastSessionTrackName = csd.TrackDefinition.name;
                    this.lastSessionTrackLength = shared.scoring.mScoringInfo.mLapDist;
                }

                csd.TrackDefinition.trackLandmarks = tdc.trackLandmarks;
                csd.TrackDefinition.isOval = Game.HAS_OVALS ? false : tdc.isOval; // LMU doesn't have oval racing
                csd.TrackDefinition.setGapPoints();

                GlobalBehaviourSettings.UpdateFromTrackDefinition(csd.TrackDefinition);

                cgs.PitData.PitBoxPositionEstimate = playerScoring.mPitLapDist;
                Console.WriteLine("Pit box position = " + (cgs.PitData.PitBoxPositionEstimate < 0.0f ? "Unknown" : cgs.PitData.PitBoxPositionEstimate.ToString("0.000")));

                cgs.trackName = csd.TrackDefinition.name;
            }

            // Restore cumulative data.
            if (psd != null && !csd.IsNewSession)
            {
                cgs.carClass = pgs.carClass;
                csd.DriverRawName = psd.DriverRawName;
                csd.TrackDefinition = psd.TrackDefinition;

                csd.TrackDefinition.trackLandmarks = psd.TrackDefinition.trackLandmarks;
                csd.TrackDefinition.gapPoints = psd.TrackDefinition.gapPoints;

                cgs.PitData.NumPitStops = pgs.PitData.NumPitStops;
                cgs.PenaltiesData.CutTrackWarnings = pgs.PenaltiesData.CutTrackWarnings;

                csd.DeltaTime = psd.DeltaTime;
                cgs.readLandmarksForThisLap = previousGameState.readLandmarksForThisLap;

                cgs.retriedDriverNames = pgs.retriedDriverNames;
                cgs.disqualifiedDriverNames = pgs.disqualifiedDriverNames;

                cgs.FlagData.currentLapIsFCY = pgs.FlagData.currentLapIsFCY;
                cgs.FlagData.previousLapWasFCY = pgs.FlagData.previousLapWasFCY;

                cgs.Conditions.CurrentConditions = pgs.Conditions.CurrentConditions;
                cgs.Conditions.samples = pgs.Conditions.samples;
                
                cgs.PitData.PitBoxPositionEstimate = pgs.PitData.PitBoxPositionEstimate;

                cgs.hardPartsOnTrackData = pgs.hardPartsOnTrackData;

                csd.formattedPlayerLapTimes = psd.formattedPlayerLapTimes;
                cgs.TimingData = pgs.TimingData;
                csd.JustGoneGreenTime = psd.JustGoneGreenTime;

                csd.IsLastLap = psd.IsLastLap;
                csd.OverallLeaderIsOnLastLap = psd.OverallLeaderIsOnLastLap;

                cgs.trackName = pgs.trackName;
                cgs.carName = pgs.carName;

                cgs.SessionData.OpponentKeyInFront = pgs.SessionData.OpponentKeyInFront;
                cgs.SessionData.OpponentKeyBehind = pgs.SessionData.OpponentKeyBehind;
            }

            if (cgs.carName == null && playerTelemetryAvailable)
                cgs.carName = RF2GameStateMapper.GetStringFromBytes(playerTelemetry.mVehicleName);

            csd.SessionStartTime = csd.IsNewSession ? cgs.Now : psd.SessionStartTime;
            csd.SessionHasFixedTime = csd.SessionTotalRunTime > 0.0f;

            csd.SessionRunningTime = (float)(playerTelemetryAvailable ? playerTelemetry.mElapsedTime : shared.scoring.mScoringInfo.mCurrentET);
            csd.SessionTimeRemaining = csd.SessionHasFixedTime ? csd.SessionTotalRunTime - csd.SessionRunningTime : 0.0f;

            // hack for test day sessions running longer than allotted time
            csd.SessionTimeRemaining = csd.SessionTimeRemaining < 0.0f && shared.scoring.mScoringInfo.mSession == 0 ? defaultSessionTotalRunTime : csd.SessionTimeRemaining;

            csd.NumCarsOverall = shared.scoring.mScoringInfo.mNumVehicles;
            csd.NumCarsOverallAtStartOfSession = csd.IsNewSession ? csd.NumCarsOverall : psd.NumCarsOverallAtStartOfSession;
            csd.OverallPosition = playerScoring.mPlace;

            csd.SectorNumber = playerScoring.mSector == 0 ? 3 : playerScoring.mSector;
            csd.IsNewSector = csd.IsNewSession || csd.SectorNumber != psd.SectorNumber;
            csd.IsNewLap = csd.IsNewSession || (csd.IsNewSector && csd.SectorNumber == 1);

            if (csd.IsNewLap)
            {
                cgs.readLandmarksForThisLap = false;
                cgs.FlagData.previousLapWasFCY = pgs != null && pgs.FlagData.currentLapIsFCY;
                cgs.FlagData.currentLapIsFCY = cgs.FlagData.isFullCourseYellow;
            }

            csd.PositionAtStartOfCurrentLap = csd.IsNewLap ? csd.OverallPosition : psd.PositionAtStartOfCurrentLap;
            csd.IsDisqualified = (rF2FinishStatus)playerScoring.mFinishStatus == rFactor2Constants.rF2FinishStatus.Dq;
            csd.IsDNF = (rF2FinishStatus)playerScoring.mFinishStatus == rFactor2Constants.rF2FinishStatus.Dnf;

            // NOTE: Telemetry contains mLapNumber, which might be ahead of Scoring due to higher refresh rate.  However,
            // since we use Scoring fields for timing calculations, stick to Scoring here as well.
            csd.CompletedLaps = playerScoring.mTotalLaps;
            csd.LapCount = csd.CompletedLaps + 1;

            ////////////////////////////////////
            // motion data
            if (playerTelemetryAvailable)
            {
                cgs.PositionAndMotionData.CarSpeed = (float)RF2GameStateMapper.getVehicleSpeed(ref playerTelemetry);
                cgs.PositionAndMotionData.DistanceRoundTrack = (float)getEstimatedLapDist(shared, ref playerScoring, ref playerTelemetry);
                cgs.PositionAndMotionData.WorldPosition = new float[] { (float)playerTelemetry.mPos.x, (float)playerTelemetry.mPos.y, (float)playerTelemetry.mPos.z };
                if (playerTelemetry.mOri != null)
                    cgs.PositionAndMotionData.Orientation = RF2GameStateMapper.GetRotation(ref playerTelemetry.mOri);
            }
            else
            {
                cgs.PositionAndMotionData.CarSpeed = (float)RF2GameStateMapper.getVehicleSpeed(ref playerScoring);
                cgs.PositionAndMotionData.DistanceRoundTrack = (float)playerScoring.mLapDist;
                cgs.PositionAndMotionData.WorldPosition = new float[] { (float)playerScoring.mPos.x, (float)playerScoring.mPos.y, (float)playerScoring.mPos.z };

                if (playerScoring.mOri != null)  // Don't bother with corner case of no telemetry data if we're reading from a file.
                    cgs.PositionAndMotionData.Orientation = RF2GameStateMapper.GetRotation(ref playerScoring.mOri);
            }

            // During Gridwalk/Formation and Finished phases, distance close to S/F line is negative.  Fix it up.
            if (cgs.PositionAndMotionData.DistanceRoundTrack < 0.0f)
                cgs.PositionAndMotionData.DistanceRoundTrack += (float)shared.scoring.mScoringInfo.mLapDist;

            // Initialize DeltaTime.
            if (csd.IsNewSession)
                csd.DeltaTime = new DeltaTime(csd.TrackDefinition.trackLength, cgs.PositionAndMotionData.DistanceRoundTrack, cgs.PositionAndMotionData.CarSpeed, cgs.Now);

            // Is online session?
            for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                if ((rF2Control)shared.scoring.mVehicles[i].mControl == rFactor2Constants.rF2Control.Remote)
                    this.isOfflineSession = false;
            }

            ///////////////////////////////////
            // Pit Data
            cgs.PitData.IsRefuellingAllowed = cgs.carClass.isRefuelingAllowed;
            cgs.PitData.IsElectricVehicleSwapAllowed = cgs.carClass.isVehicleSwapAllowed;

            if (this.enablePitStopPrediction)
            {
                cgs.PitData.HasMandatoryPitStop = this.isOfflineSession
                    && playerTelemetry.mScheduledStops > 0
                    && playerScoring.mNumPitstops < playerTelemetry.mScheduledStops
                    && csd.SessionType == SessionType.Race;

                cgs.PitData.PitWindowStart = this.isOfflineSession && cgs.PitData.HasMandatoryPitStop ? 1 : 0;

                var pitWindowEndLapOrTime = 0;
                if (cgs.PitData.HasMandatoryPitStop)
                {
                    if (csd.SessionHasFixedTime)
                    {
                        var minutesBetweenStops = (int)(csd.SessionTotalRunTime / 60 / (playerTelemetry.mScheduledStops + 1));
                        if (minutesBetweenStops > RF2GameStateMapper.minMinutesBetweenPredictedStops)
                            pitWindowEndLapOrTime = minutesBetweenStops * (playerScoring.mNumPitstops + 1) + 1;
                    }
                    else
                    {
                        var lapsBetweenStops = (int)(csd.SessionNumberOfLaps / (playerTelemetry.mScheduledStops + 1));
                        if (lapsBetweenStops > RF2GameStateMapper.minLapsBetweenPredictedStops)
                            pitWindowEndLapOrTime = lapsBetweenStops * (playerScoring.mNumPitstops + 1) + 1;
                    }

                    // Force the MandatoryPit event to be re-initialsed if the window end has been recalculated.
                    cgs.PitData.ResetEvents = pgs != null && pitWindowEndLapOrTime > pgs.PitData.PitWindowEnd;
                }

                cgs.PitData.PitWindowEnd = pitWindowEndLapOrTime;
            }

            // mInGarageStall also means retired or before race start, but for now use it here.
            cgs.PitData.InPitlane = playerScoring.mInPits == 1 || playerScoring.mInGarageStall == 1;

            csd.DeltaTime.SetNextDeltaPoint(cgs.PositionAndMotionData.DistanceRoundTrack, csd.CompletedLaps, cgs.PositionAndMotionData.CarSpeed, cgs.Now, !cgs.PitData.InPitlane);

            if (csd.SessionType == SessionType.Race && csd.SessionRunningTime > 10
                && cgs.PitData.InPitlane && pgs != null && !pgs.PitData.InPitlane)
            {
                cgs.PitData.NumPitStops++;
            }

            cgs.PitData.IsAtPitExit = pgs != null && pgs.PitData.InPitlane && !cgs.PitData.InPitlane;
            cgs.PitData.OnOutLap = cgs.PitData.IsAtPitExit || playerScoring.mInGarageStall == 1;

            if (shared.extended.mInRealtimeFC == 0  // Mark pit limiter as unavailable if in Monitor (not real time).
                || shared.scoring.mScoringInfo.mInRealtime == 0
                || playerTelemetry.mSpeedLimiterAvailable == 0)
            {
                cgs.PitData.limiterStatus = PitData.LimiterStatus.NOT_AVAILABLE;
            }
            else
                cgs.PitData.limiterStatus = playerTelemetry.mSpeedLimiter > 0 ? PitData.LimiterStatus.ACTIVE : PitData.LimiterStatus.INACTIVE;

            if (pgs != null
                && csd.CompletedLaps == psd.CompletedLaps
                && pgs.PitData.OnOutLap)
            {
                // If current lap is pit out lap, keep it that way till lap completes.
                cgs.PitData.OnOutLap = true;
            }

            cgs.PitData.OnInLap = cgs.PitData.InPitlane && csd.SectorNumber == 3;

            cgs.PitData.IsMakingMandatoryPitStop = cgs.PitData.HasMandatoryPitStop
                && cgs.PitData.OnInLap
                && csd.CompletedLaps > cgs.PitData.PitWindowStart;

            cgs.PitData.PitWindow = cgs.PitData.IsMakingMandatoryPitStop
                ? PitWindow.StopInProgress : this.mapToPitWindow((rF2YellowFlagState)shared.scoring.mScoringInfo.mYellowFlagState);

            if (pgs != null)
                cgs.PitData.MandatoryPitStopCompleted = pgs.PitData.MandatoryPitStopCompleted || cgs.PitData.IsMakingMandatoryPitStop;

            cgs.PitData.HasRequestedPitStop = (rF2PitState)playerScoring.mPitState == rFactor2Constants.rF2PitState.Request;

            // Is this new pit request?
            if (pgs != null && !pgs.PitData.HasRequestedPitStop && cgs.PitData.HasRequestedPitStop)
                this.timePitStopRequested = cgs.Now;

            if (shared.extended.mDirectMemoryAccessEnabled == 0)
            {
                // If DMA is not enabled, check if it's time to mark pit crew as ready.
                if (pgs != null
                    && pgs.PitData.HasRequestedPitStop
                    && cgs.PitData.HasRequestedPitStop
                    && (cgs.Now - this.timePitStopRequested).TotalSeconds > cgs.carClass.pitCrewPreparationTime)
                        cgs.PitData.IsPitCrewReady = true;
            }

            if (shared.extended.mDirectMemoryAccessEnabled != 0)
                cgs.PitData.PitSpeedLimit = shared.extended.mCurrentPitSpeedLimit;

            // This sometimes fires under Countdown, so limit to phases when message might make sense.
            if ((csd.SessionPhase == SessionPhase.Green
                    || csd.SessionPhase == SessionPhase.FullCourseYellow
                    || csd.SessionPhase == SessionPhase.Formation)
                && shared.extended.mInRealtimeFC == 1 && shared.scoring.mScoringInfo.mInRealtime == 1  // Limit this to Realtime only.
                && csd.SessionType == SessionType.Race)  // Also, limit to race only, this helps with back and forth between returing to pits via exit to monitor.
            {                                            // There's also no real critical rush in quali or practice to stress about.
                cgs.PitData.IsPitCrewDone = (rF2PitState)playerScoring.mPitState == rFactor2Constants.rF2PitState.Exiting;
            }

            if (csd.IsNewLap)
            {
                this.minTrackWidth = -1.0;
                this.isApproachingPitEntry = false;
            }

            // Reset pit lane approach prediction variables on exiting the pits.  Also, do not collect widths in a first
            // sector of the out lap, as it may include pit exit lane values, which we don't need.
            if (cgs.PitData.OnOutLap && csd.SectorNumber == 1)
            {
                this.minTrackWidth = -1.0;
                this.isApproachingPitEntry = false;
            }
            else if (this.enablePitLaneApproachHeuristics)
            {
                if (cgs.PitData.InPitlane)
                {
                    this.minTrackWidth = -1.0;
                    this.isApproachingPitEntry = false;
                }
                else
                {
                    var estTrackWidth = Math.Abs(playerScoring.mTrackEdge) * 2.0;

                    if (this.minTrackWidth == -1.0 || estTrackWidth < this.minTrackWidth)
                    {
                        this.minTrackWidth = estTrackWidth;

                        var pitLaneStartDist = shared.rules.mTrackRules.mPitLaneStartDist;
                        if (cgs.SessionData.SessionType != SessionType.Race)
                        {
                            // Rules aren't updated in non-race sessions, so estimate pit lane start based on
                            // pit stall position.
                            pitLaneStartDist = playerScoring.mPitLapDist - 400.0f;
                            if (pitLaneStartDist < 0.0f)
                                pitLaneStartDist += (float)shared.scoring.mScoringInfo.mLapDist;
                        }

                        // See if it looks like we're entering the pits.
                        // The idea here is that if:
                        // - current DistanceRoundTrack is past the point where track forks into pits
                        // - this appears like narrowest part of a track surface (tracked for an entire lap)
                        // - and pit is requested, assume we're approaching pit entry.
                        if (cgs.PositionAndMotionData.DistanceRoundTrack > pitLaneStartDist
                            && cgs.PitData.HasRequestedPitStop)
                            this.isApproachingPitEntry = true;

                        if (cgs.SessionData.SectorNumber > 2)  // Only print in S3, that's the most interesting.
                        {
                            Console.WriteLine(string.Format("New min width: {0:0.000}    lapDist: {1:0.000}    pathLat: {2:0.000}    inPit: {3}    ps: {4}    appr: {5}    lap: {6}    pit lane: {7:0.000}",
                                this.minTrackWidth,
                                playerScoring.mLapDist,
                                playerScoring.mPathLateral,
                                cgs.PitData.InPitlane,
                                playerScoring.mPitState,
                                this.isApproachingPitEntry,
                                csd.CompletedLaps + 1,
                                shared.rules.mTrackRules.mPitLaneStartDist));
                        }
                    }
                }

                cgs.PitData.IsApproachingPitlane = this.isApproachingPitEntry;
            }

            // --------------------------------
            // MC warnings
            if (!csd.IsNewSession)  // Skip the very first session tick as events are not processed at this time.
                this.ProcessMCMessages(cgs, pgs, shared);

            // If player is DNF or DQ, do not send further updates.
            if (!csd.IsNewSession
                && psd != null
                && (psd.IsDisqualified || psd.IsDisqualified))
            {
                if (psd.SessionPhase != SessionPhase.Finished)
                {
                    Debug.Assert(csd.IsDNF || csd.IsDisqualified);

                    // Send one last update and finish the session.
                    cgs.SessionData.SessionPhase = SessionPhase.Finished;
                    return cgs;
                }
                else
                {
                    Debug.Assert(pgs.SessionData.SessionPhase == SessionPhase.Finished);

                    // No more updates until new session kicks in.
                    return pgs;
                }
            }

            ////////////////////////////////////
            // Timings
            if (psd != null && !csd.IsNewSession)
            {
                // Preserve current values.
                // Those values change on sector/lap change, otherwise stay the same between updates.
                psd.RestorePlayerTimings(csd);
            }

            this.processPlayerTimingData(ref shared.scoring, cgs, pgs, ref playerScoring);

            csd.SessionTimesAtEndOfSectors = pgs != null ? psd.SessionTimesAtEndOfSectors : new SessionData().SessionTimesAtEndOfSectors;

            if (csd.IsNewSector && !csd.IsNewSession)
            {
                // There's a slight delay due to scoring updating every 200 ms, so we can't use SessionRunningTime here.
                // NOTE: Telemetry contains mLapStartET as well, which is out of sync with Scoring mLapStartET (might be ahead
                // due to higher refresh rate).  However, since we're using Scoring for timings elsewhere, use Scoring here, for now at least.
                switch (csd.SectorNumber)
                {
                    case 1:
                        csd.SessionTimesAtEndOfSectors[3]
                            = playerScoring.mLapStartET > 0.0f ? (float)playerScoring.mLapStartET : -1.0f;
                        break;
                    case 2:
                        csd.SessionTimesAtEndOfSectors[1]
                            = playerScoring.mLapStartET > 0.0f && playerScoring.mCurSector1 > 0.0f
                                ? (float)(playerScoring.mLapStartET + playerScoring.mCurSector1)
                                : -1.0f;
                        break;
                    case 3:
                        csd.SessionTimesAtEndOfSectors[2]
                            = playerScoring.mLapStartET > 0 && playerScoring.mCurSector2 > 0.0f
                                ? (float)(playerScoring.mLapStartET + playerScoring.mCurSector2)
                                : -1.0f;
                        break;
                    default:
                        break;
                }
            }

            csd.LeaderHasFinishedRace = leaderScoring.mFinishStatus == (int)rFactor2Constants.rF2FinishStatus.Finished;
            csd.LeaderSectorNumber = leaderScoring.mSector == 0 ? 3 : leaderScoring.mSector;
            csd.TimeDeltaFront = (float)Math.Abs(playerScoring.mTimeBehindNext);
            if (shared.scoring.mScoringInfo.mEndET < 0)
            {   // Not a timed race, estimate race time based on leader's lap time
                // and number of laps remaining.
                // (Needed for weather forecast to identify weather slots) 
                shared.scoring.mScoringInfo.mEndET = 
                    shared.scoring.mScoringInfo.mMaxLaps * leaderScoring.mBestLapTime;
            }

            // --------------------------------
            // engine data
            cgs.EngineData.EngineRpm = (float)playerTelemetry.mEngineRPM;
            cgs.EngineData.MaxEngineRpm = (float)playerTelemetry.mEngineMaxRPM;
            cgs.EngineData.MinutesIntoSessionBeforeMonitoring = 5;
            cgs.EngineData.EngineOilTemp = (float)playerTelemetry.mEngineOilTemp;
            cgs.EngineData.EngineWaterTemp = (float)playerTelemetry.mEngineWaterTemp;

            // JB: stall detection hackery
            if (cgs.EngineData.EngineRpm > 5.0f)
                this.lastTimeEngineWasRunning = cgs.Now;

            if (!cgs.PitData.InPitlane
                && pgs != null && !pgs.EngineData.EngineStalledWarning
                && cgs.SessionData.SessionRunningTime > 60.0f
                && cgs.EngineData.EngineRpm < 5.0f
                && this.lastTimeEngineWasRunning < cgs.Now.Subtract(TimeSpan.FromSeconds(2))
                && shared.extended.mInRealtimeFC == 1 && shared.scoring.mScoringInfo.mInRealtime == 1)  // Realtime only.
            {
                cgs.EngineData.EngineStalledWarning = true;
                this.lastTimeEngineWasRunning = DateTime.MaxValue;
            }

            //HACK: there's probably a cleaner way to do this...
            if (playerTelemetry.mOverheating == 1)
            {
                cgs.EngineData.EngineWaterTemp += 50;
                cgs.EngineData.EngineOilTemp += 50;
            }

            // --------------------------------
            // transmission data
            cgs.TransmissionData.Gear = playerTelemetry.mGear;

            // controls
            cgs.ControlData.BrakePedal = (float)playerTelemetry.mUnfilteredBrake;
            cgs.ControlData.ThrottlePedal = (float)playerTelemetry.mUnfilteredThrottle;
            cgs.ControlData.ClutchPedal = (float)playerTelemetry.mUnfilteredClutch;

            // --------------------------------
            // damage
            cgs.CarDamageData.DamageEnabled = true;
            cgs.CarDamageData.LastImpactTime = (float)playerTelemetry.mLastImpactET;

            var playerDamageInfo = shared.extended.mTrackedDamages[playerScoring.mID % rFactor2Constants.MAX_MAPPED_IDS];

            if (shared.extended.mPhysics.mInvulnerable == 0)
            {
                const double MINOR_DAMAGE_THRESHOLD = 1500.0;
                const double MAJOR_DAMAGE_THRESHOLD = 4000.0;
                const double ACCUMULATED_THRESHOLD_FACTOR = 4.0;

                bool anyWheelDetached = false;
                foreach (var wheel in playerTelemetry.mWheels)
                    anyWheelDetached |= wheel.mDetached == 1;

                if (playerTelemetry.mDetached == 1
                    && anyWheelDetached)  // Wheel is not really aero damage, but it is bad situation.
                {
                    // Things are sad if we have both part and wheel detached.
                    cgs.CarDamageData.OverallAeroDamage = DamageLevel.DESTROYED;
                }
                else if (playerTelemetry.mDetached == 1  // If there are parts detached, consider damage major, and pit stop is necessary.
                    || playerDamageInfo.mMaxImpactMagnitude > MAJOR_DAMAGE_THRESHOLD)  // Also take max impact magnitude into consideration.
                {

                    cgs.CarDamageData.OverallAeroDamage = DamageLevel.MAJOR;
                }
                else if (playerDamageInfo.mMaxImpactMagnitude > MINOR_DAMAGE_THRESHOLD
                    || playerDamageInfo.mAccumulatedImpactMagnitude > MINOR_DAMAGE_THRESHOLD * ACCUMULATED_THRESHOLD_FACTOR)  // Also consider accumulated damage, if user grinds car against the wall, max won't be high, but car is still damaged.
                {
                    cgs.CarDamageData.OverallAeroDamage = DamageLevel.MINOR;
                }
                else if (playerDamageInfo.mMaxImpactMagnitude > 0.0)
                    cgs.CarDamageData.OverallAeroDamage = DamageLevel.TRIVIAL;
                else
                    cgs.CarDamageData.OverallAeroDamage = DamageLevel.NONE;
            }
            else  // shared.extended.mPhysics.mInvulnerable != 0
            {
                // roll over all you want - it's just a scratch.
                cgs.CarDamageData.OverallAeroDamage = playerDamageInfo.mMaxImpactMagnitude > 0.0 ? DamageLevel.TRIVIAL : DamageLevel.NONE;
            }

            // --------------------------------
            // control data
            cgs.ControlData.ControlType = MapToControlType((rF2Control)playerScoring.mControl);

            // --------------------------------
            // Tyre data
            // rF2 reports in Kelvin
            cgs.TyreData.TyreWearActive = true;

            // For now, all tyres will be reported as front compund.
            var tt = TyreType.Uninitialized;
            if (pgs != null)
            {
                // Restore previous tyre type.
                tt = pgs.TyreData.FrontLeftTyreType;

                // Re-evaluate on Countdown, Green and on pit exit:
                if ((csd.SessionPhase == SessionPhase.Countdown && psd.SessionPhase != SessionPhase.Countdown)
                    || (csd.SessionPhase == SessionPhase.Green && psd.SessionPhase != SessionPhase.Green)
                    || (!cgs.PitData.InPitlane && pgs.PitData.InPitlane))
                    tt = this.MapToTyreType(ref playerTelemetry);
            }

            // First time intialize.  Might stay like that until we get telemetry.
            if (tt == TyreType.Uninitialized)
            {
                tt = this.MapToTyreType(ref playerTelemetry);
                if (playerTelemetry.mFrontTireCompoundName != null)
                {
                    cgs.TyreData.TyreTypeName = RF2GameStateMapper.GetStringFromBytes(playerTelemetry.mFrontTireCompoundName);
                }
            }

            var wheelFrontLeft = playerTelemetry.mWheels[(int)rFactor2Constants.rF2WheelIndex.FrontLeft];
            cgs.TyreData.FrontLeftTyreType = tt;
            cgs.TyreData.LeftFrontAttached = wheelFrontLeft.mDetached == 0;
            cgs.TyreData.FrontLeft_LeftTemp = getTyreTempCelsius(ref wheelFrontLeft, 0);
            cgs.TyreData.FrontLeft_CenterTemp = getTyreTempCelsius(ref wheelFrontLeft, 1);
            cgs.TyreData.FrontLeft_RightTemp = getTyreTempCelsius(ref wheelFrontLeft, 2);

            var frontLeftTemp = (cgs.TyreData.FrontLeft_CenterTemp + cgs.TyreData.FrontLeft_LeftTemp + cgs.TyreData.FrontLeft_RightTemp) / 3.0f;
            cgs.TyreData.FrontLeftPressure = wheelFrontLeft.mFlat == 0 ? (float)wheelFrontLeft.mPressure : 0.0f;
            cgs.TyreData.FrontLeftPercentWear = (float)(1.0f - wheelFrontLeft.mWear) * 100.0f;

            if (csd.IsNewLap || cgs.TyreData.PeakFrontLeftTemperatureForLap == 0)
                cgs.TyreData.PeakFrontLeftTemperatureForLap = frontLeftTemp;
            else if (pgs == null || frontLeftTemp > pgs.TyreData.PeakFrontLeftTemperatureForLap)
                cgs.TyreData.PeakFrontLeftTemperatureForLap = frontLeftTemp;

            var wheelFrontRight = playerTelemetry.mWheels[(int)rFactor2Constants.rF2WheelIndex.FrontRight];
            cgs.TyreData.FrontRightTyreType = tt;
            cgs.TyreData.RightFrontAttached = wheelFrontRight.mDetached == 0;
            cgs.TyreData.FrontRight_LeftTemp = getTyreTempCelsius(ref wheelFrontRight, 0);
            cgs.TyreData.FrontRight_CenterTemp = getTyreTempCelsius(ref wheelFrontRight, 1);
            cgs.TyreData.FrontRight_RightTemp = getTyreTempCelsius(ref wheelFrontRight, 2);

            var frontRightTemp = (cgs.TyreData.FrontRight_CenterTemp + cgs.TyreData.FrontRight_LeftTemp + cgs.TyreData.FrontRight_RightTemp) / 3.0f;
            cgs.TyreData.FrontRightPressure = wheelFrontRight.mFlat == 0 ? (float)wheelFrontRight.mPressure : 0.0f;
            cgs.TyreData.FrontRightPercentWear = (float)(1.0f - wheelFrontRight.mWear) * 100.0f;

            if (csd.IsNewLap || cgs.TyreData.PeakFrontRightTemperatureForLap == 0)
                cgs.TyreData.PeakFrontRightTemperatureForLap = frontRightTemp;
            else if (pgs == null || frontRightTemp > pgs.TyreData.PeakFrontRightTemperatureForLap)
                cgs.TyreData.PeakFrontRightTemperatureForLap = frontRightTemp;

            var wheelRearLeft = playerTelemetry.mWheels[(int)rFactor2Constants.rF2WheelIndex.RearLeft];
            cgs.TyreData.RearLeftTyreType = tt;
            cgs.TyreData.LeftRearAttached = wheelRearLeft.mDetached == 0;
            cgs.TyreData.RearLeft_LeftTemp = getTyreTempCelsius(ref wheelRearLeft, 0);
            cgs.TyreData.RearLeft_CenterTemp = getTyreTempCelsius(ref wheelRearLeft, 1);
            cgs.TyreData.RearLeft_RightTemp = getTyreTempCelsius(ref wheelRearLeft, 2);

            var rearLeftTemp = (cgs.TyreData.RearLeft_CenterTemp + cgs.TyreData.RearLeft_LeftTemp + cgs.TyreData.RearLeft_RightTemp) / 3.0f;
            cgs.TyreData.RearLeftPressure = wheelRearLeft.mFlat == 0 ? (float)wheelRearLeft.mPressure : 0.0f;
            cgs.TyreData.RearLeftPercentWear = (float)(1.0f - wheelRearLeft.mWear) * 100.0f;

            if (csd.IsNewLap || cgs.TyreData.PeakRearLeftTemperatureForLap == 0)
                cgs.TyreData.PeakRearLeftTemperatureForLap = rearLeftTemp;
            else if (pgs == null || rearLeftTemp > pgs.TyreData.PeakRearLeftTemperatureForLap)
                cgs.TyreData.PeakRearLeftTemperatureForLap = rearLeftTemp;

            var wheelRearRight = playerTelemetry.mWheels[(int)rFactor2Constants.rF2WheelIndex.RearRight];
            cgs.TyreData.RearRightTyreType = tt;
            cgs.TyreData.RightRearAttached = wheelRearRight.mDetached == 0;
            cgs.TyreData.RearRight_LeftTemp = getTyreTempCelsius(ref wheelRearRight, 0);
            cgs.TyreData.RearRight_CenterTemp = getTyreTempCelsius(ref wheelRearRight, 1);
            cgs.TyreData.RearRight_RightTemp = getTyreTempCelsius(ref wheelRearRight, 2);

            logLMUTyreTemperatureReadDebug(cgs, ref wheelFrontLeft, ref wheelFrontRight, ref wheelRearLeft, ref wheelRearRight);

            var rearRightTemp = (cgs.TyreData.RearRight_CenterTemp + cgs.TyreData.RearRight_LeftTemp + cgs.TyreData.RearRight_RightTemp) / 3.0f;
            cgs.TyreData.RearRightPressure = wheelRearRight.mFlat == 0 ? (float)wheelRearRight.mPressure : 0.0f;
            cgs.TyreData.RearRightPercentWear = (float)(1.0f - wheelRearRight.mWear) * 100.0f;

            if (csd.IsNewLap || cgs.TyreData.PeakRearRightTemperatureForLap == 0)
                cgs.TyreData.PeakRearRightTemperatureForLap = rearRightTemp;
            else if (pgs == null || rearRightTemp > pgs.TyreData.PeakRearRightTemperatureForLap)
                cgs.TyreData.PeakRearRightTemperatureForLap = rearRightTemp;

            cgs.TyreData.TyreConditionStatus = CornerData.getCornerData(this.tyreWearThresholds, cgs.TyreData.FrontLeftPercentWear,
                cgs.TyreData.FrontRightPercentWear, cgs.TyreData.RearLeftPercentWear, cgs.TyreData.RearRightPercentWear);

            var tyreTempThresholds = CarData.getTyreTempThresholds(cgs.carClass, tt);
            cgs.TyreData.TyreTempStatus = CornerData.getCornerData(tyreTempThresholds,
                cgs.TyreData.PeakFrontLeftTemperatureForLap, cgs.TyreData.PeakFrontRightTemperatureForLap,
                cgs.TyreData.PeakRearLeftTemperatureForLap, cgs.TyreData.PeakRearRightTemperatureForLap);

            // some simple locking / spinning checks
            if (cgs.PositionAndMotionData.CarSpeed > 7.0f)
            {
                if (this.useRealWheelSizeForLockingAndSpinning && playerTelemetryAvailable)
                {
                    // TODO: remove.
                    float minRotatingSpeedOld = (float)Math.PI * cgs.PositionAndMotionData.CarSpeed / cgs.carClass.maxTyreCircumference;
                    float maxRotatingSpeedOld = 3 * (float)Math.PI * cgs.PositionAndMotionData.CarSpeed / cgs.carClass.minTyreCircumference;

                    // w = v/r
                    // https://www.lucidar.me/en/unit-converter/rad-per-second-to-meters-per-second/
                    float MAX_RADIUS = 3.6f;  // When making a left turn, right wheel spins faster, as if it was smaller.  Because of that, scale real radius up for lock detection.
                    var minFrontRotatingSpeedRadSec = cgs.PositionAndMotionData.CarSpeed / (wheelFrontLeft.mStaticUndeflectedRadius * 0.01f * MAX_RADIUS);
                    cgs.TyreData.LeftFrontIsLocked = Math.Abs(wheelFrontLeft.mRotation) < minFrontRotatingSpeedRadSec;
                    cgs.TyreData.RightFrontIsLocked = Math.Abs(wheelFrontRight.mRotation) < minFrontRotatingSpeedRadSec;

                    var minRearRotatingSpeedRadSec = cgs.PositionAndMotionData.CarSpeed / (wheelRearLeft.mStaticUndeflectedRadius * 0.01f * MAX_RADIUS);
                    cgs.TyreData.LeftRearIsLocked = Math.Abs(wheelRearLeft.mRotation) < minRearRotatingSpeedRadSec;
                    cgs.TyreData.RightRearIsLocked = Math.Abs(wheelRearRight.mRotation) < minRearRotatingSpeedRadSec;

                    float MIN_RADIUS = 0.5f;  // When making a left turn, right wheel spins faster, as if it was smaller.  Because of that, scale real radius down for spin detection.
                    var maxFrontRotatingSpeedRadSec = cgs.PositionAndMotionData.CarSpeed / (wheelFrontLeft.mStaticUndeflectedRadius * 0.01f * MIN_RADIUS);
                    cgs.TyreData.LeftFrontIsSpinning = Math.Abs(wheelFrontLeft.mRotation) > maxFrontRotatingSpeedRadSec;
                    cgs.TyreData.RightFrontIsSpinning = Math.Abs(wheelFrontRight.mRotation) > maxFrontRotatingSpeedRadSec;

                    var maxRearRotatingSpeedRadSec = cgs.PositionAndMotionData.CarSpeed / (wheelRearLeft.mStaticUndeflectedRadius * 0.01f * MIN_RADIUS);
                    cgs.TyreData.LeftRearIsSpinning = Math.Abs(wheelRearLeft.mRotation) > maxRearRotatingSpeedRadSec;
                    cgs.TyreData.RightRearIsSpinning = Math.Abs(wheelRearRight.mRotation) > maxRearRotatingSpeedRadSec;

#if DEBUG
                    RF2GameStateMapper.writeSpinningLockingDebugMsg(cgs, wheelFrontLeft.mRotation, wheelFrontRight.mRotation,
                        wheelRearLeft.mRotation, wheelRearRight.mRotation, minRotatingSpeedOld, maxRotatingSpeedOld, minFrontRotatingSpeedRadSec,
                        minRearRotatingSpeedRadSec, maxFrontRotatingSpeedRadSec, maxRearRotatingSpeedRadSec);
#endif
                }
                else
                {
                    float minRotatingSpeed = (float)Math.PI * cgs.PositionAndMotionData.CarSpeed / cgs.carClass.maxTyreCircumference;
                    cgs.TyreData.LeftFrontIsLocked = Math.Abs(wheelFrontLeft.mRotation) < minRotatingSpeed;
                    cgs.TyreData.RightFrontIsLocked = Math.Abs(wheelFrontRight.mRotation) < minRotatingSpeed;
                    cgs.TyreData.LeftRearIsLocked = Math.Abs(wheelRearLeft.mRotation) < minRotatingSpeed;
                    cgs.TyreData.RightRearIsLocked = Math.Abs(wheelRearRight.mRotation) < minRotatingSpeed;

                    float maxRotatingSpeed = 3 * (float)Math.PI * cgs.PositionAndMotionData.CarSpeed / cgs.carClass.minTyreCircumference;
                    cgs.TyreData.LeftFrontIsSpinning = Math.Abs(wheelFrontLeft.mRotation) > maxRotatingSpeed;
                    cgs.TyreData.RightFrontIsSpinning = Math.Abs(wheelFrontRight.mRotation) > maxRotatingSpeed;
                    cgs.TyreData.LeftRearIsSpinning = Math.Abs(wheelRearLeft.mRotation) > maxRotatingSpeed;
                    cgs.TyreData.RightRearIsSpinning = Math.Abs(wheelRearRight.mRotation) > maxRotatingSpeed;
                }
            }

            // use detached wheel status for suspension damage
            cgs.CarDamageData.SuspensionDamageStatus = CornerData.getCornerData(this.suspensionDamageThresholds,
                !cgs.TyreData.LeftFrontAttached ? 1 : 0,
                !cgs.TyreData.RightFrontAttached ? 1 : 0,
                !cgs.TyreData.LeftRearAttached ? 1 : 0,
                !cgs.TyreData.RightRearAttached ? 1 : 0);

            // --------------------------------
            // brake data
            // rF2 reports in Kelvin
            cgs.TyreData.LeftFrontBrakeTemp = (float)wheelFrontLeft.mBrakeTemp - 273.15f;
            cgs.TyreData.RightFrontBrakeTemp = (float)wheelFrontRight.mBrakeTemp - 273.15f;
            cgs.TyreData.LeftRearBrakeTemp = (float)wheelRearLeft.mBrakeTemp - 273.15f;
            cgs.TyreData.RightRearBrakeTemp = (float)wheelRearRight.mBrakeTemp - 273.15f;

            if (this.brakeTempThresholdsForPlayersCar != null)
            {
                cgs.TyreData.BrakeTempStatus = CornerData.getCornerData(this.brakeTempThresholdsForPlayersCar,
                    cgs.TyreData.LeftFrontBrakeTemp, cgs.TyreData.RightFrontBrakeTemp,
                    cgs.TyreData.LeftRearBrakeTemp, cgs.TyreData.RightRearBrakeTemp);
            }

            // --------------------------------
            // track conditions
            if (cgs.Now > nextConditionsSampleDue)
            {
                nextConditionsSampleDue = cgs.Now.Add(ConditionsMonitor.ConditionsSampleFrequency);
                cgs.Conditions.addSample(cgs.Now,
                                         csd.CompletedLaps,
                                         csd.SectorNumber,
                                         (float)shared.scoring.mScoringInfo.mAmbientTemp,
                                         (float)shared.scoring.mScoringInfo.mTrackTemp,
                                         (float)shared.scoring.mScoringInfo.mRaining,
                                         (float)Math.Sqrt((double)(shared.scoring.mScoringInfo.mWind.x * shared.scoring.mScoringInfo.mWind.x + shared.scoring.mScoringInfo.mWind.y * shared.scoring.mScoringInfo.mWind.y + shared.scoring.mScoringInfo.mWind.z * shared.scoring.mScoringInfo.mWind.z)),
                                         0,
                                         0,
                                         0,
                                         csd.IsNewLap,
                                         ConditionsMonitor.TrackStatus.UNKNOWN);
            }
            weatherForecast.update(cgs,
                                   shared.scoring.mScoringInfo.mSession,
                                   (float)shared.scoring.mScoringInfo.mRaining,
                                   shared.scoring.mScoringInfo.mCurrentET,
                                   shared.scoring.mScoringInfo.mEndET);

            // --------------------------------
            // DRS data
            cgs.OvertakingAids.DrsAvailable = playerTelemetry.mRearFlapLegalStatus == (int)rFactor2Constants.rF2RearFlapLegalStatus.Allowed;

            // Many of rF2 tracks have no DRS zones defined.  One of the symptoms is DRS alloweved immediately on race start.
            // Disallow DRS messages in such case.
            if (!this.detectedTrackNoDRSZones
                && csd.CompletedLaps == 0
                && csd.SessionRunningTime > 10.0f
                && cgs.OvertakingAids.DrsAvailable)
            {
                this.detectedTrackNoDRSZones = true;
                if (cgs.carClass.isDRSCapable)
                    Console.WriteLine("Track has no valid DRS zones defined, disabling DRS messages.");
            }

            cgs.OvertakingAids.DrsEngaged = playerTelemetry.mRearFlapActivated == 1;

            if (cgs.SessionData.SessionPhase != SessionPhase.FullCourseYellow)
            {
                // Doesn't look like game is providing info on when DRS is actually enabled in race, so guess.
                cgs.OvertakingAids.DrsEnabled = cgs.carClass.isDRSCapable
                    && csd.CompletedLaps > 2  // Hack of course.
                    && !this.detectedTrackNoDRSZones;
            }

            cgs.OvertakingAids.DrsRange = cgs.carClass.DRSRange;

            // --------------------------------
            // opponent data
            this.opponentKeysProcessed.Clear();

            // first check for duplicates:
            var driverNameCounts = new Dictionary<string, int>();
            var duplicatesCreated = new Dictionary<string, int>();
            for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicleScoring = shared.scoring.mVehicles[i];

                var cci = this.GetCachedCarInfo(ref vehicleScoring);
                if (cci.isGhost)
                    continue;  // Skip trainer.

                var driverName = cci.driverNameRawSanitized;

                var numNames = -1;
                if (driverNameCounts.TryGetValue(driverName, out numNames))
                    driverNameCounts[driverName] = ++numNames;
                else
                    driverNameCounts.Add(driverName, 1);
            }

            for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicleScoring = shared.scoring.mVehicles[i];
                if (vehicleScoring.mIsPlayer == 1)
                {
                    // Issue 515: player may be best
                    if (csd.PlayerLapTimeSessionBest > 0.0f)
                    {
                        if (csd.OverallSessionBestLapTime > 0)
                        {
                            csd.OverallSessionBestLapTime = Math.Min(
                                csd.OverallSessionBestLapTime,
                                csd.PlayerLapTimeSessionBest);
                        }
                        else
                        {
                            csd.OverallSessionBestLapTime = csd.PlayerLapTimeSessionBest;
                        }
                    }
                    csd.PlayerClassSessionBestLapTime = csd.PlayerLapTimeSessionBest > 0.0f ?
                        csd.PlayerLapTimeSessionBest : -1.0f;

                    if (csd.IsNewLap
                        && psd != null && !psd.IsNewLap
                        && csd.LapTimePrevious > 0.0f
                        && csd.PreviousLapWasValid)
                    {
                        float playerClassBestTimeByTyre = -1.0f;
                        if (!csd.PlayerClassSessionBestLapTimeByTyre.TryGetValue(cgs.TyreData.FrontLeftTyreType, out playerClassBestTimeByTyre)
                            || playerClassBestTimeByTyre > csd.LapTimePrevious)
                            csd.PlayerClassSessionBestLapTimeByTyre[cgs.TyreData.FrontLeftTyreType] = csd.LapTimePrevious;

                        float playerBestTimeByTyre = -1.0f;
                        if (!csd.PlayerBestLapTimeByTyre.TryGetValue(cgs.TyreData.FrontLeftTyreType, out playerBestTimeByTyre)
                            || playerBestTimeByTyre > csd.LapTimePrevious)
                            csd.PlayerBestLapTimeByTyre[cgs.TyreData.FrontLeftTyreType] = csd.LapTimePrevious;

                        // See if this looks like the last lap of a timed race.
                        if (cgs.SessionData.SessionType == SessionType.Race
                            && cgs.SessionData.SessionPhase != SessionPhase.Finished
                            && cgs.SessionData.SessionPhase != SessionPhase.Checkered
                            && csd.SessionHasFixedTime)
                        {
                            if (csd.OverallLeaderIsOnLastLap)
                                csd.IsLastLap = true;

                            if (vehicleScoring.mPlace == 1
                                && csd.PlayerLapTimeSessionBest > 0.0f
                                && csd.SessionTimeRemaining < csd.PlayerLapTimeSessionBest * 0.90f)
                                csd.IsLastLap = csd.OverallLeaderIsOnLastLap = true;
                        }
                    }

                    continue;
                }

                var ct = this.MapToControlType((rF2Control)vehicleScoring.mControl);
                if (ct == ControlType.Player || ct == ControlType.Replay || ct == ControlType.Unavailable)
                    continue;

                var vehicleCachedInfo = this.GetCachedCarInfo(ref vehicleScoring);
                if (vehicleCachedInfo.isGhost)
                    continue;  // Skip trainer.

                // Get telemetry for this vehicle.
                var vehicleTelemetry = new rF2VehicleTelemetry();
                bool vehicleTelemetryAvailable = true;
                int vehicleTelIdx = -1;
                if (idsToTelIndicesMap.TryGetValue(vehicleScoring.mID, out vehicleTelIdx))
                    vehicleTelemetry = shared.telemetry.mVehicles[vehicleTelIdx];
                else
                {
                    vehicleTelemetryAvailable = false;
                    RF2GameStateMapper.InitEmptyVehicleTelemetry(ref vehicleTelemetry);

                    // Exclude known situations when telemetry is not available, but log otherwise to get more
                    // insights.
                    if (shared.extended.mInRealtimeFC == 1
                        && shared.scoring.mScoringInfo.mInRealtime == 1
                        && shared.scoring.mScoringInfo.mGamePhase != (byte)rFactor2Constants.rF2GamePhase.GridWalk)
                    {
                        Console.WriteLine("Failed to obtain opponent telemetry, falling back to scoring.");
                    }
                }

                var driverName = vehicleCachedInfo.driverNameRawSanitized;
                OpponentData opponentPrevious = null;
                var duplicatesCount = driverNameCounts[driverName];
                string opponentKey = null;
                if (duplicatesCount > 1)
                {
                    if (!this.isOfflineSession)
                    {
                        // there shouldn't be duplicate driver names in online sessions. This is probably a temporary glitch in the shared memory data -
                        // don't panic and drop the existing opponentData for this key - just copy it across to the current state. This prevents us losing
                        // the historical data and repeatedly re-adding this name to the SpeechRecogniser (which is expensive)
                        if (pgs != null && pgs.OpponentData.ContainsKey(driverName)
                            && !cgs.OpponentData.ContainsKey(driverName))
                            cgs.OpponentData.Add(driverName, pgs.OpponentData[driverName]);

                        opponentKeysProcessed.Add(driverName);
                        continue;
                    }
                    else
                    {
                        // offline we can have any number of duplicates :(
                        opponentKey = this.GetOpponentKeyForVehicleInfo(ref vehicleScoring, ref vehicleTelemetry, pgs, csd.SessionRunningTime, driverName, duplicatesCount, vehicleTelemetryAvailable);

                        if (opponentKey == null)
                        {
                            // there's no previous opponent data record for this driver so create one
                            var numDuplicates = -1;
                            if (duplicatesCreated.TryGetValue(driverName, out numDuplicates))
                                duplicatesCreated[driverName] = ++numDuplicates;
                            else
                            {
                                numDuplicates = 1;
                                duplicatesCreated.Add(driverName, 1);
                            }

                            opponentKey = driverName + "_duplicate_" + numDuplicates;
                        }
                    }
                }
                else
                {
                    opponentKey = driverName;
                }

                var ofs = (rF2FinishStatus)vehicleScoring.mFinishStatus;
                if (ofs == rFactor2Constants.rF2FinishStatus.Dnf)
                {
                    // Note driver DNF and don't tack him anymore.
                    if (!cgs.retriedDriverNames.ContainsKey(driverName))
                    {
                        Console.WriteLine("Opponent " + driverName + " has retired");
                        cgs.retriedDriverNames.Add(driverName, null);
                    }
                    continue;
                }
                else if (ofs == rFactor2Constants.rF2FinishStatus.Dq)
                {
                    // Note driver DQ and don't tack him anymore.
                    if (!cgs.disqualifiedDriverNames.ContainsKey(driverName))
                    {
                        Console.WriteLine("Opponent " + driverName + " has been disqualified");
                        cgs.disqualifiedDriverNames.Add(driverName, null);
                    }
                    continue;
                }

                opponentPrevious = pgs == null || opponentKey == null || !pgs.OpponentData.TryGetValue(opponentKey, out opponentPrevious) ? null : opponentPrevious;
                var opponent = new OpponentData();

                opponent.CarClass = vehicleCachedInfo.carClass;

                tt = TyreType.Uninitialized;
                if (pgs != null && opponentPrevious != null)
                {
                    // Restore previous tyre type.
                    if (opponentPrevious != null)
                        tt = opponentPrevious.CurrentTyres;

                    // Re-evaluate on Countdown and on pit exit:
                    if ((csd.SessionPhase == SessionPhase.Countdown && psd.SessionPhase != SessionPhase.Countdown)
                        || (csd.SessionPhase == SessionPhase.Green && psd.SessionPhase != SessionPhase.Green)
                        || (vehicleScoring.mInPits != 1 && opponentPrevious.InPits))
                        tt = this.MapToTyreType(ref vehicleTelemetry);
                }

                // First time intialize.  Might stay like that until we get telemetry.
                if (tt == TyreType.Uninitialized)
                    tt = this.MapToTyreType(ref vehicleTelemetry);

                opponent.CurrentTyres = tt;
                opponent.DriverRawName = vehicleCachedInfo.driverNameRawSanitized;
                opponent.DriverNameSet = opponent.DriverRawName.Length > 0;
                opponent.OverallPosition = vehicleScoring.mPlace;

                // Telemetry isn't always available, initialize first tyre set 10 secs or more into race.
                if (csd.SessionType == SessionType.Race && csd.SessionRunningTime > 10.0f
                    && opponentPrevious != null
                    && opponentPrevious.TyreChangesByLap.Count == 0)  // If tyre for initial lap was never set.
                    opponent.TyreChangesByLap[0] = opponent.CurrentTyres;

                if (opponent.DriverNameSet && opponentPrevious == null && CrewChief.enableDriverNames)
                {
                    if (!csd.IsNewSession && this.speechRecogniser != null)
                        this.speechRecogniser.addNewOpponentName(opponent.DriverRawName, "-1");
                    SoundCache.loadDriverNameSound(DriverNameHelper.getUsableDriverName(opponent.DriverRawName));
                    Console.WriteLine("New driver \"" + driverName +
                        "\" is using car class " + opponent.CarClass.getClassIdentifier() +
                        " at position " + opponent.OverallPosition.ToString());
                }

                // Carry over state
                if (opponentPrevious != null)
                {
                    // Copy so that we can safely use previous state.
                    foreach (var old in opponentPrevious.OpponentLapData)
                        opponent.OpponentLapData.Add(old);

                    foreach (var old in opponentPrevious.TyreChangesByLap)
                        opponent.TyreChangesByLap.Add(old.Key, old.Value);

                    opponent.NumPitStops = opponentPrevious.NumPitStops;
                    opponent.OverallPositionAtPreviousTick = opponentPrevious.OverallPosition;
                    opponent.ClassPositionAtPreviousTick = opponentPrevious.ClassPosition;
                    opponent.IsLastLap = opponentPrevious.IsLastLap;
                }

                opponent.SessionTimeAtLastPositionChange
                    = opponentPrevious != null && opponentPrevious.OverallPosition != opponent.OverallPosition
                            ? csd.SessionRunningTime : -1.0f;

                opponent.CompletedLaps = vehicleScoring.mTotalLaps;
                opponent.CurrentSectorNumber = vehicleScoring.mSector == 0 ? 3 : vehicleScoring.mSector;
                var isNewSector = csd.IsNewSession || (opponentPrevious != null && opponentPrevious.CurrentSectorNumber != opponent.CurrentSectorNumber);
                opponent.IsNewLap = csd.IsNewSession || (isNewSector && opponent.CurrentSectorNumber == 1 && opponent.CompletedLaps > 0);

                if (vehicleTelemetryAvailable)
                {
                    opponent.Speed = (float)RF2GameStateMapper.getVehicleSpeed(ref vehicleTelemetry);
                    opponent.WorldPosition = new float[] { (float)vehicleTelemetry.mPos.x, (float)vehicleTelemetry.mPos.z };
                    opponent.DistanceRoundTrack = (float)RF2GameStateMapper.getEstimatedLapDist(shared, ref vehicleScoring, ref vehicleTelemetry);
                }
                else
                {
                    opponent.Speed = (float)RF2GameStateMapper.getVehicleSpeed(ref vehicleScoring);
                    opponent.WorldPosition = new float[] { (float)vehicleScoring.mPos.x, (float)vehicleScoring.mPos.z };
                    opponent.DistanceRoundTrack = (float)vehicleScoring.mLapDist;
                }

                if (opponentPrevious != null)
                {
                    // if we've just crossed the 'near to pit entry' mark, update our near-pit-entry position. Otherwise copy it from the previous state
                    if (opponentPrevious.DistanceRoundTrack < csd.TrackDefinition.distanceForNearPitEntryChecks
                        && opponent.DistanceRoundTrack > csd.TrackDefinition.distanceForNearPitEntryChecks)
                    {
                        opponent.PositionOnApproachToPitEntry = opponent.OverallPosition;
                    }
                    else
                    {
                        opponent.PositionOnApproachToPitEntry = opponentPrevious.PositionOnApproachToPitEntry;
                    }
                    // carry over the delta time - do this here so if we have to initalise it we have the correct distance data
                    opponent.DeltaTime = opponentPrevious.DeltaTime;
                }
                else
                {
                    opponent.DeltaTime = new DeltaTime(csd.TrackDefinition.trackLength, opponent.DistanceRoundTrack, opponent.Speed, DateTime.UtcNow);
                }
                opponent.DeltaTime.SetNextDeltaPoint(opponent.DistanceRoundTrack, opponent.CompletedLaps, opponent.Speed, cgs.Now, vehicleScoring.mInPits != 1);

                opponent.CurrentBestLapTime = vehicleScoring.mBestLapTime > 0.0f ? (float)vehicleScoring.mBestLapTime : -1.0f;

                if (opponent.IsNewLap)
                {
                    if (cgs.SessionData.SessionType == SessionType.Race
                        && cgs.SessionData.SessionPhase != SessionPhase.Finished
                        && cgs.SessionData.SessionPhase != SessionPhase.Checkered
                        && csd.SessionHasFixedTime)
                    {
                        if (opponent.CurrentBestLapTime > 0.0f)
                        {
                            if (csd.OverallLeaderIsOnLastLap)
                                opponent.IsLastLap = true;

                            if (opponent.OverallPosition == 1
                                && opponent.CurrentBestLapTime > 0.0f
                                && csd.SessionTimeRemaining < opponent.CurrentBestLapTime * 0.90f)
                                csd.OverallLeaderIsOnLastLap = opponent.IsLastLap = true;
                        }
                    }
                }

                opponent.PreviousBestLapTime = opponentPrevious != null && opponentPrevious.CurrentBestLapTime > 0.0f &&
                    opponentPrevious.CurrentBestLapTime > opponent.CurrentBestLapTime ? opponentPrevious.CurrentBestLapTime : -1.0f;
                float previousDistanceRoundTrack = opponentPrevious != null ? opponentPrevious.DistanceRoundTrack : 0;
                opponent.bestSector1Time = vehicleScoring.mBestSector1 > 0 ? (float)vehicleScoring.mBestSector1 : -1.0f;
                opponent.bestSector2Time = vehicleScoring.mBestSector2 > 0 && vehicleScoring.mBestSector1 > 0.0f ? (float)(vehicleScoring.mBestSector2 - vehicleScoring.mBestSector1) : -1.0f;
                opponent.bestSector3Time = vehicleScoring.mBestLapTime > 0 && vehicleScoring.mBestSector2 > 0.0f ? (float)(vehicleScoring.mBestLapTime - vehicleScoring.mBestSector2) : -1.0f;
                opponent.LastLapTime = vehicleScoring.mLastLapTime > 0 ? (float)vehicleScoring.mLastLapTime : -1.0f;

                var isInPits = vehicleScoring.mInPits == 1;

                if (csd.SessionType == SessionType.Race && csd.SessionRunningTime > 10.0f
                    && opponentPrevious != null && !opponentPrevious.InPits && isInPits)
                {
                    opponent.NumPitStops++;
                }

                opponent.InPits = isInPits;
                opponent.JustEnteredPits = opponentPrevious != null && !opponentPrevious.InPits && opponent.InPits;

                var wasInPits = opponentPrevious != null && opponentPrevious.InPits;
                opponent.hasJustChangedToDifferentTyreType = false;

                // It looks like compound type fluctuates while in pits.  So, check for it on pit exit only.
                if (wasInPits && !isInPits
                    && opponent.TyreChangesByLap.Count != 0)  // This should be initialized above
                {
                    var prevTyres = opponent.TyreChangesByLap.Last().Value;
                    if (opponent.CurrentTyres != prevTyres)
                    {
                        opponent.TyreChangesByLap[opponent.CompletedLaps] = opponent.CurrentTyres;
                        opponent.hasJustChangedToDifferentTyreType = true;
                    }
                }

                var lastSectorTime = this.getLastSectorTime(ref vehicleScoring, opponent.CurrentSectorNumber);

                bool lapValid = true;
                if (vehicleScoring.mCountLapFlag != 2)
                    lapValid = false;

                if (opponent.IsNewLap)
                {
                    if (lastSectorTime > 0.0f)
                    {
                        opponent.CompleteLapWithProvidedLapTime(
                            opponent.OverallPosition,
                            csd.SessionRunningTime,
                            opponent.LastLapTime,
                            lapValid,
                            vehicleScoring.mInPits == 1,
                            shared.scoring.mScoringInfo.mRaining > minRainThreshold,
                            (float)shared.scoring.mScoringInfo.mTrackTemp,
                            (float)shared.scoring.mScoringInfo.mAmbientTemp,
                            csd.SessionHasFixedTime,
                            csd.SessionTimeRemaining,
                            3, cgs.TimingData, CarData.IsCarClassEqual(opponent.CarClass, cgs.carClass));
                    }
                    opponent.StartNewLap(
                        opponent.CompletedLaps + 1,
                        opponent.OverallPosition,
                        vehicleScoring.mInPits == 1 || opponent.DistanceRoundTrack < 0.0f,
                        csd.SessionRunningTime,
                        shared.scoring.mScoringInfo.mRaining > minRainThreshold,
                        (float)shared.scoring.mScoringInfo.mTrackTemp,
                        (float)shared.scoring.mScoringInfo.mAmbientTemp);
                }
                else if (isNewSector && lastSectorTime > 0.0f)
                {
                    opponent.AddCumulativeSectorData(
                        opponentPrevious.CurrentSectorNumber,
                        opponent.OverallPosition,
                        lastSectorTime,
                        csd.SessionRunningTime,
                        lapValid,
                        shared.scoring.mScoringInfo.mRaining > minRainThreshold,
                        (float)shared.scoring.mScoringInfo.mTrackTemp,
                        (float)shared.scoring.mScoringInfo.mAmbientTemp);
                }

                if (vehicleScoring.mInPits == 1
                    && opponent.CurrentSectorNumber == 3
                    && opponentPrevious != null
                    && !opponentPrevious.isEnteringPits())
                {
                    opponent.setInLap();
                }

                //  Allow gaps in qual and prac, delta here is not on track delta but diff on fastest time.  Race gaps are set in populateDerivedRaceSessionData.
                if (opponent.ClassPosition == csd.ClassPosition + 1 && csd.SessionType != SessionType.Race)
                    csd.TimeDeltaBehind = Math.Abs(opponent.CurrentBestLapTime - csd.PlayerLapTimeSessionBest);

                if (opponent.ClassPosition == csd.ClassPosition - 1 && csd.SessionType != SessionType.Race)
                    csd.TimeDeltaFront = Math.Abs(csd.PlayerLapTimeSessionBest - opponent.CurrentBestLapTime);

                // session best lap times
                if (opponent.CurrentBestLapTime > 0.0f
                    && (opponent.CurrentBestLapTime < csd.OpponentsLapTimeSessionBestOverall
                        || csd.OpponentsLapTimeSessionBestOverall < 0.0f))
                {
                    csd.OpponentsLapTimeSessionBestOverall = opponent.CurrentBestLapTime;
                }

                if (opponent.CurrentBestLapTime > 0.0f
                    && (opponent.CurrentBestLapTime < csd.OpponentsLapTimeSessionBestPlayerClass
                        || csd.OpponentsLapTimeSessionBestPlayerClass < 0.0f)
                    && CarData.IsCarClassEqual(opponent.CarClass, cgs.carClass))
                {
                    csd.OpponentsLapTimeSessionBestPlayerClass = opponent.CurrentBestLapTime;

                    if (csd.PlayerClassSessionBestLapTime == -1.0f
                        || csd.PlayerClassSessionBestLapTime > csd.OpponentsLapTimeSessionBestPlayerClass)
                        csd.PlayerClassSessionBestLapTime = csd.OpponentsLapTimeSessionBestPlayerClass;

                    if (opponent.IsNewLap && opponentPrevious != null && !opponentPrevious.IsNewLap)
                    {
                        float playerClassSessionBestByTyre = -1.0f;
                        if (opponent.LastLapTime > 0.0
                            && opponent.LastLapValid
                            && (!csd.PlayerClassSessionBestLapTimeByTyre.TryGetValue(opponent.CurrentTyres, out playerClassSessionBestByTyre)
                                || playerClassSessionBestByTyre > opponent.LastLapTime))
                            csd.PlayerClassSessionBestLapTimeByTyre[opponent.CurrentTyres] = opponent.LastLapTime;
                    }
                }

                if (opponent.CurrentBestLapTime > 0.0f
                    && (opponent.CurrentBestLapTime < csd.OverallSessionBestLapTime
                        || csd.OverallSessionBestLapTime < 0.0f))
                    csd.OverallSessionBestLapTime = opponent.CurrentBestLapTime;

                if (opponentPrevious != null)
                {
                    opponent.trackLandmarksTiming = opponentPrevious.trackLandmarksTiming;
                    var stoppedInLandmark = opponent.trackLandmarksTiming.updateLandmarkTiming(csd.TrackDefinition,
                        csd.SessionRunningTime, previousDistanceRoundTrack, opponent.DistanceRoundTrack, opponent.Speed, opponent.CarClass);
                    opponent.stoppedInLandmark = opponent.InPits ? null : stoppedInLandmark;
                }

                if (opponent.IsNewLap)
                    opponent.trackLandmarksTiming.cancelWaitingForLandmarkEnd();

                // shouldn't have duplicates, but just in case
                if (!cgs.OpponentData.ContainsKey(opponentKey))
                    cgs.OpponentData.Add(opponentKey, opponent);

                if (cgs.PitData.HasRequestedPitStop
                    && csd.SessionType == SessionType.Race)
                {
                    // Detect if opponent occupies player's stall.
                    if (vehicleScoring.mPitState == (byte)rFactor2Constants.rF2PitState.Stopped)
                    {
                        if (Math.Abs(cgs.PitData.PitBoxPositionEstimate - opponent.DistanceRoundTrack) < 5.0)
                            cgs.PitData.PitStallOccupied = true;
                    }
                }
            }

            cgs.sortClassPositions();
            cgs.setPracOrQualiDeltas();

            if (pgs != null)
            {
                csd.trackLandmarksTiming = pgs.SessionData.trackLandmarksTiming;

                var stoppedInLandmark = csd.trackLandmarksTiming.updateLandmarkTiming(csd.TrackDefinition,
                    csd.SessionRunningTime, pgs.PositionAndMotionData.DistanceRoundTrack, cgs.PositionAndMotionData.DistanceRoundTrack,
                    cgs.PositionAndMotionData.CarSpeed, cgs.carClass);

                cgs.SessionData.stoppedInLandmark = cgs.PitData.InPitlane ? null : stoppedInLandmark;

                if (csd.IsNewLap)
                    csd.trackLandmarksTiming.cancelWaitingForLandmarkEnd();

                csd.SessionStartClassPosition = pgs.SessionData.SessionStartClassPosition;
                csd.ClassPositionAtStartOfCurrentLap = pgs.SessionData.ClassPositionAtStartOfCurrentLap;
                csd.NumCarsInPlayerClassAtStartOfSession = pgs.SessionData.NumCarsInPlayerClassAtStartOfSession;
            }

            // --------------------------------
            // fuel/battery data
            if (GameWorkArounds.LMU_FuelMultiplier) //tbd LMU hack
            {
                shared.extended.mPhysics.mFuelMult = 1;
            }
            cgs.FuelData.FuelUseActive = cgs.BatteryData.BatteryUseActive = shared.extended.mPhysics.mFuelMult > 0;
            cgs.FuelData.FuelMultiplier = shared.extended.mPhysics.mFuelMult;
            cgs.FuelData.FuelLeft = cgs.BatteryData.BatteryPercentageLeft = (float)playerTelemetry.mFuel;
            cgs.FuelData.FuelCapacity = (float)playerTelemetry.mFuelCapacity;
            if (Game.LMU)
            {
                LMU.VirtualEnergy.VE = (int)(playerTelemetry.mVirtualEnergy * 100);
            }

            // --------------------------------
            // flags data
            cgs.FlagData.useImprovisedIncidentCalling = false;

            cgs.FlagData.isFullCourseYellow = csd.SessionPhase == SessionPhase.FullCourseYellow
                || shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.Resume;

            if (shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.Resume)
            {
                // Special case for resume after FCY.  rF2 no longer has FCY set, but still has Resume sub phase set.
                cgs.FlagData.fcyPhase = FullCourseYellowPhase.RACING;
                cgs.FlagData.lapCountWhenLastWentGreen = cgs.SessionData.CompletedLaps;
            }
            else if (cgs.FlagData.isFullCourseYellow)
            {
                cgs.FlagData.currentLapIsFCY = true;
                if (shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.Pending)
                    cgs.FlagData.fcyPhase = FullCourseYellowPhase.PENDING;
                else if (shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.PitOpen
                    || shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.PitClosed)
                {
                    if (!this.enableFCYPitStateMessages)
                        cgs.FlagData.fcyPhase = FullCourseYellowPhase.IN_PROGRESS;
                    else if (playerRulesIdx != -1
                        && shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.PitClosed)
                    {
                        var allowedToPit = shared.rules.mParticipants[playerRulesIdx].mPitsOpen;
                        if (shared.extended.mDirectMemoryAccessEnabled == 1 && shared.extended.mSCRPluginEnabled == 1)
                        {
                            // Apparently, 0 and 2 means not allowed with SCR plugin.
                            var pitsClosedForPlayer = allowedToPit == 2 || allowedToPit == 0;
                            cgs.FlagData.fcyPhase = pitsClosedForPlayer ? FullCourseYellowPhase.PITS_CLOSED : FullCourseYellowPhase.PITS_OPEN;
                        }
                        else
                        {
                            // Core rules: always open, pit state == 3
                            cgs.FlagData.fcyPhase = FullCourseYellowPhase.PITS_OPEN;
                        }
                    }
                    else if (playerRulesIdx == -1  // Online case
                        && shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.PitClosed)
                    {
                        if (shared.extended.mDirectMemoryAccessEnabled == 1)
                        {
                            if (shared.extended.mTicksLSIPitStateMessageUpdated != this.LSIPitStateMessageUpdatedTicks)
                            {
                                this.LSIPitStateMessageUpdatedTicks = shared.extended.mTicksLSIPitStateMessageUpdated;
                                var pitStateMsg = RF2GameStateMapper.GetStringFromBytes(shared.extended.mLSIPitStateMessage);
                                if (!string.IsNullOrWhiteSpace(pitStateMsg))
                                    Console.WriteLine("LSI Message: pit state message updated - \"" + pitStateMsg + "\"");

                                if (pitStateMsg == "Pits Open")
                                    cgs.FlagData.fcyPhase = FullCourseYellowPhase.PITS_OPEN;
                                else if (pitStateMsg == "Pits Closed")
                                    cgs.FlagData.fcyPhase = FullCourseYellowPhase.PITS_CLOSED;
                                else
                                {
                                    if (pgs != null)
                                        cgs.FlagData.fcyPhase = pgs.FlagData.fcyPhase;

                                    if (!string.IsNullOrWhiteSpace(pitStateMsg))
                                    {
#if !DEBUG
                                        Log.DontSpam($"LSI Message: pit state ignored - \"{pitStateMsg}\"", 10).Commentary();
#else
                                        Log.DontSpam($"LSI Message: pit state ignored - \"{pitStateMsg}\"", 10).Warning();
#endif
                                    }
                                }
                            }
                            else if (pgs != null)
                                cgs.FlagData.fcyPhase = pgs.FlagData.fcyPhase;
                        }
                        else
                        {
                            // Core rules: always open, pit state == 3
                            cgs.FlagData.fcyPhase = FullCourseYellowPhase.PITS_OPEN;
                        }
                    }
                    else
                        cgs.FlagData.fcyPhase = FullCourseYellowPhase.PITS_OPEN;
                }
                else if (shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.PitLeadLap)
                    cgs.FlagData.fcyPhase = this.enableFCYPitStateMessages ? FullCourseYellowPhase.PITS_OPEN_LEAD_LAP_VEHICLES : FullCourseYellowPhase.IN_PROGRESS;
                else if (shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.LastLap)
                {
                    if (pgs != null)
                    {
                        if (pgs.FlagData.fcyPhase != FullCourseYellowPhase.LAST_LAP_NEXT && pgs.FlagData.fcyPhase != FullCourseYellowPhase.LAST_LAP_CURRENT)
                            // Initial last lap phase
                            cgs.FlagData.fcyPhase = FullCourseYellowPhase.LAST_LAP_NEXT;
                        else if (csd.CompletedLaps != psd.CompletedLaps && pgs.FlagData.fcyPhase == FullCourseYellowPhase.LAST_LAP_NEXT)
                            // Once we reach the end of current lap, and this lap is next last lap, switch to last lap current phase.
                            cgs.FlagData.fcyPhase = FullCourseYellowPhase.LAST_LAP_CURRENT;
                        else
                            // Keep previous FCY last lap phase.
                            cgs.FlagData.fcyPhase = pgs.FlagData.fcyPhase;

                    }
                }
            }

            if (csd.SessionPhase == SessionPhase.Green)
            {
                for (int i = 0; i < 3; ++i)
                {
                    // Mark Yellow sectors.
                    if (shared.scoring.mScoringInfo.mSectorFlag[i] == (int)rFactor2Constants.rF2YellowFlagState.Pending)
                        cgs.FlagData.sectorFlags[i] = FlagEnum.YELLOW;
                }
            }

            var currFlag = FlagEnum.UNKNOWN;

            if (GlobalBehaviourSettings.useAmericanTerms
                && !cgs.FlagData.isFullCourseYellow)  // Don't announce White flag under FCY.
            {
                // Only works correctly if race is not timed.
                if ((csd.SessionType == SessionType.Race || 
                     csd.SessionType == SessionType.Qualify ||
                     csd.SessionType == SessionType.PrivateQualify)
                    && csd.SessionPhase == SessionPhase.Green
                    && (playerScoring.mTotalLaps == csd.SessionNumberOfLaps - 1) || csd.LeaderHasFinishedRace)
                {
                    currFlag = FlagEnum.WHITE;
                }
            }

            if (playerScoring.mFlag == (byte)rFactor2Constants.rF2PrimaryFlag.Blue)
                currFlag = FlagEnum.BLUE;

            if (csd.IsDisqualified
                && pgs != null
                && !psd.IsDisqualified)
                currFlag = FlagEnum.BLACK;

            csd.Flag = currFlag;

            // --------------------------------
            // Stock Car Rules data
            if (shared.extended.mDirectMemoryAccessEnabled != 0
                && shared.extended.mSCRPluginEnabled != 0)
                cgs.StockCarRulesData = this.GetStockCarRulesData(cgs, ref shared);

            // --------------------------------
            // Frozen order data
            if (this.enableFrozenOrderMessages
                && pgs != null)
            {
                cgs.FrozenOrderData = playerRulesIdx != -1
                    ? this.GetFrozenOrderData(pgs.FrozenOrderData, ref playerScoring, ref shared.scoring, ref shared.rules.mParticipants[playerRulesIdx], ref shared.rules, ref shared.extended, cgs.PositionAndMotionData.CarSpeed)
                    : this.GetFrozenOrderOnlineData(cgs, pgs.FrozenOrderData, ref playerScoring, ref shared.scoring, ref shared.extended, cgs.PositionAndMotionData.CarSpeed);
            }

            // --------------------------------
            // penalties data
            cgs.PenaltiesData.NumOutstandingPenalties = playerScoring.mNumPenalties;
            if (pgs != null && cgs.PenaltiesData.NumOutstandingPenalties > pgs.PenaltiesData.NumOutstandingPenalties)
                this.lastPenaltyTime = cgs.Now;

            var cutTrackByInvalidLapDetected = false;
            // If lap state changed from valid to invalid, consider it due to cut track.
            if (!cgs.PitData.OnOutLap
                && pgs != null
                && pgs.SessionData.CurrentLapIsValid
                && !cgs.SessionData.CurrentLapIsValid
                && !cgs.PitData.InPitlane
                && !(cgs.SessionData.SessionType == SessionType.Race
                    && (cgs.SessionData.SessionPhase == SessionPhase.Countdown
                        || cgs.SessionData.SessionPhase == SessionPhase.Gridwalk)))
            {
                Console.WriteLine("Player off track: by an invalid lap.");
                cgs.PenaltiesData.CutTrackWarnings = pgs.PenaltiesData.CutTrackWarnings + 1;
                cutTrackByInvalidLapDetected = true;
            }

            // Improvised cut track warnings based on surface type.
            if (!cutTrackByInvalidLapDetected
                && !cgs.PitData.InPitlane
                && !cgs.PitData.OnOutLap)
            {
                var wfls = wheelFrontLeft.mSurfaceType;
                var wfrs = wheelFrontRight.mSurfaceType;
                var wrls = wheelRearLeft.mSurfaceType;
                var wrrs = wheelRearRight.mSurfaceType;

                cgs.PenaltiesData.IsOffRacingSurface =
                    wfls != (int)rFactor2Constants.rF2SurfaceType.Dry && wfls != (int)rFactor2Constants.rF2SurfaceType.Wet && wfls != (int)rFactor2Constants.rF2SurfaceType.Kerb
                    && wfrs != (int)rFactor2Constants.rF2SurfaceType.Dry && wfrs != (int)rFactor2Constants.rF2SurfaceType.Wet && wfrs != (int)rFactor2Constants.rF2SurfaceType.Kerb
                    && wrls != (int)rFactor2Constants.rF2SurfaceType.Dry && wrls != (int)rFactor2Constants.rF2SurfaceType.Wet && wrls != (int)rFactor2Constants.rF2SurfaceType.Kerb
                    && wrrs != (int)rFactor2Constants.rF2SurfaceType.Dry && wrrs != (int)rFactor2Constants.rF2SurfaceType.Wet && wrrs != (int)rFactor2Constants.rF2SurfaceType.Kerb;

                if (this.enableCutTrackHeuristics)
                {
                    if (pgs != null && !pgs.PenaltiesData.IsOffRacingSurface && cgs.PenaltiesData.IsOffRacingSurface)
                    {
                        Console.WriteLine("Player off track: by surface type.");
                        cgs.PenaltiesData.CutTrackWarnings = pgs.PenaltiesData.CutTrackWarnings + 1;
                    }
                }
            }

            if (pgs != null
                && cgs.PenaltiesData.CutTrackWarnings > pgs.PenaltiesData.CutTrackWarnings
                && this.lastPenaltyTime.Add(this.lastPenaltyCheckWndow) > cgs.Now)
            {
                Console.WriteLine("Ignoring player off track due to recent penalty.");
                --cgs.PenaltiesData.CutTrackWarnings;
            }

            // See if we're off track by distance.
            if (!cutTrackByInvalidLapDetected
                && !cgs.PenaltiesData.IsOffRacingSurface)
            {
                float lateralDistDiff = (float)(Math.Abs(playerScoring.mPathLateral) - Math.Abs(playerScoring.mTrackEdge));
                cgs.PenaltiesData.IsOffRacingSurface = !cgs.PitData.InPitlane && lateralDistDiff >= 2.5f;
                float offTrackDistanceDelta = lateralDistDiff - this.distanceOffTrack;
                this.distanceOffTrack = cgs.PenaltiesData.IsOffRacingSurface ? lateralDistDiff : 0.0f;

                if (this.enableCutTrackHeuristics)
                {
                    if (!cgs.PitData.OnOutLap && pgs != null
                        && !pgs.PenaltiesData.IsOffRacingSurface && cgs.PenaltiesData.IsOffRacingSurface
                        && !(cgs.SessionData.SessionType == SessionType.Race && cgs.SessionData.SessionPhase == SessionPhase.Countdown))
                    {
                        Console.WriteLine("Player off track: by distance.");
                        cgs.PenaltiesData.CutTrackWarnings = pgs.PenaltiesData.CutTrackWarnings + 1;
                    }
                }
            }

            // --------------------------------
            // console output
            if (csd.IsNewSession)
            {
                Console.WriteLine("New session, trigger data:");
                Console.WriteLine("SessionType: " + csd.SessionType);
                Console.WriteLine("SessionPhase: " + csd.SessionPhase);
                Console.WriteLine("HasMandatoryPitStop: " + cgs.PitData.HasMandatoryPitStop);
                Console.WriteLine("PitWindowStart: " + cgs.PitData.PitWindowStart);
                Console.WriteLine("PitWindowEnd: " + cgs.PitData.PitWindowEnd);
                Console.WriteLine("NumCarsAtStartOfSession: " + csd.NumCarsOverallAtStartOfSession);
                Console.WriteLine("SessionNumberOfLaps: " + csd.SessionNumberOfLaps);
                Console.WriteLine("SessionRunTime: " + csd.SessionTotalRunTime);
                Console.WriteLine("SessionStartTime: " + csd.SessionStartTime);
                Console.WriteLine("SessionIteration: " + csd.SessionIteration);
                Console.WriteLine("EventIndex: " + csd.EventIndex);
                Console.WriteLine("Player is using car class: \"" + cgs.carClass.getClassIdentifier() +
                    "\" at position: " + csd.OverallPosition.ToString());

                Utilities.TraceEventClass(cgs);
            }
            if (pgs != null && psd.SessionPhase != csd.SessionPhase)
            {
                Console.WriteLine("SessionPhase changed from " + psd.SessionPhase +
                    " to " + csd.SessionPhase);
                if (csd.SessionPhase == SessionPhase.Checkered ||
                    csd.SessionPhase == SessionPhase.Finished)
                    Console.WriteLine("Checkered - completed " + csd.CompletedLaps + " laps, session running time = " + csd.SessionRunningTime + "  (" + TimeSpan.FromSeconds(csd.SessionRunningTime).ToString(@"hh\:mm\:ss\:fff") + ")");
            }
            if (pgs != null && !psd.LeaderHasFinishedRace && csd.LeaderHasFinishedRace)
                Console.WriteLine("Leader has finished race, player has done " + csd.CompletedLaps + " laps, session time = " + csd.SessionRunningTime + "  (" + TimeSpan.FromSeconds(csd.SessionRunningTime).ToString(@"hh\:mm\:ss\:fff") + ")");

            CrewChief.trackName = csd.TrackDefinition.name;
            CrewChief.carClass = cgs.carClass.carClassEnum;
            CrewChief.distanceRoundTrack = cgs.PositionAndMotionData.DistanceRoundTrack;
            CrewChief.viewingReplay = false;

            if (pgs != null
                && csd.SessionType == SessionType.Race
                && csd.SessionPhase == SessionPhase.Green
                && (pgs.SessionData.SessionPhase == SessionPhase.Formation
                    || pgs.SessionData.SessionPhase == SessionPhase.Countdown))
                csd.JustGoneGreen = true;

            // ------------------------
            // Map difficult track parts.
            if (csd.IsNewLap)
            {
                if (cgs.hardPartsOnTrackData.updateHardPartsForNewLap(csd.LapTimePrevious))
                    csd.TrackDefinition.adjustGapPoints(cgs.hardPartsOnTrackData.processedHardPartsForBestLap);
            }
            else if (!cgs.PitData.OnOutLap && !csd.TrackDefinition.isOval &&
                !(csd.SessionType == SessionType.Race && (csd.CompletedLaps < 1 || (GameStateData.useManualFormationLap && csd.CompletedLaps < 2))))
                cgs.hardPartsOnTrackData.mapHardPartsOnTrack(cgs.ControlData.BrakePedal, cgs.ControlData.ThrottlePedal,
                    cgs.PositionAndMotionData.DistanceRoundTrack, csd.CurrentLapIsValid, csd.TrackDefinition.trackLength);

            this.lastSessionHardPartsOnTrackData = cgs.hardPartsOnTrackData;

            // ------------------------
            // Chart telemetry data.
            if (CrewChief.recordChartTelemetryDuringRace || csd.SessionType != SessionType.Race)
            {
                cgs.EngineData.Gear = playerTelemetry.mGear;

                cgs.TelemetryData.FrontDownforce = playerTelemetry.mFrontDownforce;
                cgs.TelemetryData.RearDownforce = playerTelemetry.mRearDownforce;

                cgs.TelemetryData.FrontLeftData.SuspensionDeflection = wheelFrontLeft.mSuspensionDeflection;
                cgs.TelemetryData.FrontRightData.SuspensionDeflection = wheelFrontRight.mSuspensionDeflection;
                cgs.TelemetryData.RearLeftData.SuspensionDeflection = wheelRearLeft.mSuspensionDeflection;
                cgs.TelemetryData.RearRightData.SuspensionDeflection = wheelRearRight.mSuspensionDeflection;

                cgs.TelemetryData.FrontLeftData.RideHeight = wheelFrontLeft.mRideHeight;
                cgs.TelemetryData.FrontRightData.RideHeight = wheelFrontRight.mRideHeight;
                cgs.TelemetryData.RearLeftData.RideHeight = wheelRearLeft.mRideHeight;
                cgs.TelemetryData.RearRightData.RideHeight = wheelRearRight.mRideHeight;
            }

            return cgs;
        }

        private void ProcessMCMessages(GameStateData cgs, GameStateData pgs, RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper shared)
        {
            if (shared.extended.mDirectMemoryAccessEnabled == 0)
                return;

#if DEBUG
            if (shared.extended.mTicksStatusMessageUpdated != this.statusMessageUpdatedTicks)
            {
                this.statusMessageUpdatedTicks = shared.extended.mTicksStatusMessageUpdated;
                Console.WriteLine("Status message: updated - \"" + RF2GameStateMapper.GetStringFromBytes(shared.extended.mStatusMessage) + "\"");
            }
#endif

            if (shared.extended.mTicksLastHistoryMessageUpdated == this.lastHistoryMessageUpdatedTicks)
                return;

            // Do not re-process this update.
            this.lastHistoryMessageUpdatedTicks = shared.extended.mTicksLastHistoryMessageUpdated;

            var msg = RF2GameStateMapper.GetStringFromBytes(shared.extended.mLastHistoryMessage);
            if (msg != this.lastEffectiveHistoryMessage
                || (cgs.Now - this.timeEffectiveMessageProcessed).TotalSeconds > this.effectiveMessageExpirySeconds)
            {
                var messageConsumed = true;
                if (msg == "Crew Is Ready For Pitstop")
                {
                    if (pgs != null
                        && pgs.PitData.HasRequestedPitStop
                        && cgs.PitData.HasRequestedPitStop)
                        cgs.PitData.IsPitCrewReady = true;
                }
                else if (msg == "Headlights Are Now Required")
                    cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.HEADLIGHTS_REQUIRED;
                else if (msg.StartsWith("Stop/Go Penalty: "))
                {
                    if (msg.EndsWith("Cut Track"))  // "Stop/Go Penalty: Cut Track"
                    {
                        cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.STOP_AND_GO;
                        cgs.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.CUT_TRACK;
                    }
                    else if (msg.EndsWith("Speeding In Pitlane"))  // "Stop/Go Penalty: Speeding In Pitlane"
                    {
                        cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.STOP_AND_GO;
                        cgs.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.SPEEDING_IN_PITLANE;
                    }
                    else if (msg.EndsWith("False Start"))  // "Stop/Go Penalty: False Start"
                    {
                        cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.STOP_AND_GO;
                        cgs.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.FALSE_START;
                    }
                    else if (msg.EndsWith("Exiting Pits Under Red"))  // "Stop/Go Penalty: Exiting Pits Under Red"
                    {
                        cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.STOP_AND_GO;
                        cgs.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.EXITING_PITS_UNDER_RED;
                    }
                    else if (msg.EndsWith("Illegally Passed Before Green"))  // "Stop/Go Penalty: Illegally Passed Before Green"
                    {
                        cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.STOP_AND_GO;
                        cgs.PenaltiesData.PenaltyCause = cgs.FlagData.previousLapWasFCY
                            ? PenatiesData.DetailedPenaltyCause.ILLEGAL_PASS_FCY_BEFORE_GREEN
                            : PenatiesData.DetailedPenaltyCause.ILLEGAL_PASS_ROLLING_BEFORE_GREEN;
                    }
                    else if (msg.EndsWith("Illegally Passed Before Start/Finish"))
                    {
                        cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.STOP_AND_GO;
                        cgs.PenaltiesData.PenaltyCause = cgs.FlagData.previousLapWasFCY
                            ? PenatiesData.DetailedPenaltyCause.ILLEGAL_PASS_FCY_BEFORE_GREEN
                            : PenatiesData.DetailedPenaltyCause.ILLEGAL_PASS_ROLLING_BEFORE_GREEN;
                    }
                    else
                        messageConsumed = false;
                }
                else if (msg.StartsWith("Warning: "))
                {
                    if (msg.EndsWith("Driving Too Slow"))  // "Warning: Driving Too Slow"
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.DRIVING_TOO_SLOW;
                    else if (msg.EndsWith("One Lap To Serve Drive-Thru Penalty"))  // "Warning: One Lap To Serve Drive-Thru Penalty"
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.ONE_LAP_TO_SERVE_DRIVE_THROUGH;
                    else if (msg.EndsWith("One Lap To Serve Stop/Go Penalty"))  // "Warning: One Lap To Serve Stop/Go Penalty"
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.ONE_LAP_TO_SERVE_STOP_AND_GO;
                    else if (msg.EndsWith("Unsportsmanlike Driving"))  // // "Warning: Unsportsmanlike Driving"
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.UNSPORTSMANLIKE_DRIVING;
                    else
                        messageConsumed = false;
                }
                else if (msg.StartsWith("Disqualified: "))
                {
                    if (msg.EndsWith(" Laps"))  // "Disqualified: 4 Laps"
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.DISQUALIFIED_EXCEEDING_ALLOWED_LAP_COUNT;
                    else if (msg.EndsWith("Driving In Dark Without Headlights"))  // "Disqualified: Driving In Dark Without Headlights"
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.DISQUALIFIED_DRIVING_WITHOUT_HEADLIGHTS_IN_DARK;
                    else if (msg.EndsWith("Ignored Stop/Go Penalty"))
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.DISQUALIFIED_IGNORED_STOP_AND_GO;
                    else if (msg.EndsWith("Ignored Drive-Thru Penalty"))
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.DISQUALIFIED_IGNORED_DRIVE_THROUGH;
                    else
                        messageConsumed = false;
                }
                else if (msg.StartsWith("Drive-Thru Penalty: "))
                {
                    if (msg.EndsWith("Speeding In Pitlane"))
                    {
                        cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.DRIVE_THROUGH;
                        cgs.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.SPEEDING_IN_PITLANE;
                    }
                    else if (msg.EndsWith(" False Start"))
                    {
                        cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.DRIVE_THROUGH;
                        cgs.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.FALSE_START;
                    }
                    else if (msg.EndsWith(" Ignored Blue Flags"))
                    {
                        cgs.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.DRIVE_THROUGH;
                        cgs.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.IGNORED_BLUE_FLAG;
                    }
                    else
                        messageConsumed = false;
                }
                else if (msg == "Enter Pits To Avoid Exceeding Lap Allowance")
                    cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.ENTER_PITS_TO_AVOID_EXCEEDING_LAPS;
                else if (msg == "Enter Pits This Lap To Serve Penalty")
                    cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.ENTER_PITS_TO_SERVE_PENALTY;
                else if (msg == "Wrong Way")
                {
                    if (this.enableWrongWayMessage)
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.WRONG_WAY;
                }
                else if (msg == "Blue Flag Warning: Move over soon or be penalized")
                {
                    cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.BLUE_MOVE_OR_BE_PENALIZED;
                }
                else
                {
                    messageConsumed = false;
                }

                if (messageConsumed)
                {
                    this.lastEffectiveHistoryMessage = msg;
                    this.timeEffectiveMessageProcessed = cgs.Now;
                    Console.WriteLine("MC Message: processed - \"" + msg + "\"");
                }
                else
                {
#if !DEBUG
                    Log.DontSpam($"MC Message: ignored - \"{msg}\"", 10).Commentary();
#else
                    Log.DontSpam($"MC Message: ignored - \"{msg}\"", 10).Warning();
#endif
                }
            }
            else
            {
#if !DEBUG
                Log.DontSpam($"MC Messages: message was already processed - \"" + msg + "\"    Elapsed seconds: " + (cgs.Now - this.timeEffectiveMessageProcessed).TotalSeconds.ToString("0.00"), 5).Commentary();
#else
                Log.Warning($"MC Messages: message was already processed - \"" + msg + "\"    Elapsed seconds: " + (cgs.Now - this.timeEffectiveMessageProcessed).TotalSeconds.ToString("0.00"));
#endif
            }
        }

        private StockCarRulesData GetStockCarRulesData(GameStateData currentGameState, ref RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper shared)
        {
            var scrData = new StockCarRulesData();
            scrData.stockCarRulesEnabled = shared.extended.mDirectMemoryAccessEnabled != 0 && shared.extended.mSCRPluginEnabled != 0;

            var cgs = currentGameState;

            if (!scrData.stockCarRulesEnabled
                || !GlobalBehaviourSettings.useAmericanTerms
                || cgs.SessionData.SessionPhase != SessionPhase.FullCourseYellow
                || cgs.SessionData.SessionType != SessionType.Race)
                return scrData;

            if (this.LSIRulesInstructionMessageUpdatedTicks == shared.extended.mTicksLSIRulesInstructionMessageUpdated)
                return scrData;

            this.LSIRulesInstructionMessageUpdatedTicks = shared.extended.mTicksLSIRulesInstructionMessageUpdated;

            var msg = RF2GameStateMapper.GetStringFromBytes(shared.extended.mLSIRulesInstructionMessage);
            if (string.IsNullOrWhiteSpace(msg))
                return scrData;

            var consumed = true;
            // New SCR plugin:
            if (msg == "Lucky Dog: Pass Field On Outside")
                scrData.stockCarRuleApplicable = StockCarRule.LUCKY_DOG_PASS_ON_OUTSIDE;
            else if (msg == "Allow Lucky Dog To Pass On Outside")
                scrData.stockCarRuleApplicable = StockCarRule.LUCKY_DOG_ALLOW_TO_PASS_ON_OUTSIDE;
            else if (msg == "Move to the Left Or Right to Choose a Lane")
                scrData.stockCarRuleApplicable = StockCarRule.MOVE_CHOOSE_LANE;
            else if (msg == "Wave Around: Catch Rear of Field")
                scrData.stockCarRuleApplicable = StockCarRule.WAVE_AROUND_CATCH_END_OF_FIELD;
            else if (msg == "Two To Green")
                scrData.stockCarRuleApplicable = StockCarRule.TWO_TO_GREEN;
            else if (msg == "Two To Green (You Were Lucky Dog)")
                scrData.stockCarRuleApplicable = StockCarRule.TWO_TO_GREEN_REMIND_LUCKY_DOG;
            else if (msg == "PENALTY: End Of Longest Line")
                scrData.stockCarRuleApplicable = StockCarRule.PENALTY_EOLL;
            else if (msg == "(You Were Lucky Dog)")
                scrData.stockCarRuleApplicable = StockCarRule.REMIND_LUCKY_DOG;
            // Old SCR plugin:
            else if (msg == "Lucky Dog: Pass Field On Left")
                scrData.stockCarRuleApplicable = StockCarRule.LUCKY_DOG_PASS_ON_LEFT;
            else if (msg.Length > this.scrLuckyDogIsPrefix.Length
                && msg.StartsWith(this.scrLuckyDogIsPrefix))
            {
                scrData.luckyDogNameRaw = msg.Substring(this.scrLuckyDogIsPrefix.Length);
                scrData.luckyDogNameRaw = RF2GameStateMapper.GetSanitizedDriverName(scrData.luckyDogNameRaw);
                scrData.stockCarRuleApplicable = StockCarRule.NEW_LUCKY_DOG;
            }
            else if (msg == "Allow Lucky Dog To Pass On Left")
                scrData.stockCarRuleApplicable = StockCarRule.LUCKY_DOG_ALLOW_TO_PASS_ON_LEFT;
            else if (msg == "Choose A Lane By Staying Left Or Right")
                scrData.stockCarRuleApplicable = StockCarRule.LEADER_CHOOSE_LANE;
            else if (msg == "Move To End Of Longest Line")
                scrData.stockCarRuleApplicable = StockCarRule.MOVE_TO_EOLL;
            // Note: "wave around" is a prefix message, because apparently ISI plugin says "on left",
            // but custom plugin leagues use says "on right".
            else if (msg.StartsWith("Wave Around: Pass Field On"))
                scrData.stockCarRuleApplicable = StockCarRule.WAVE_AROUND_PASS_ON_RIGHT;
            else
            {
                Log.Warning("Rule instruction messages: unrecognized message - " + msg);
                consumed = false;
            }

            if (consumed)
                Console.WriteLine("Rule instruction messages: processed message - \"" + msg + "\"");

            return scrData;
        }

        #region LMU_SUPPORT
        private static bool isValidKelvin(double tempKelvin)
        {
            return !double.IsNaN(tempKelvin) && !double.IsInfinity(tempKelvin) && tempKelvin > 0.0;
        }

        private static string getArrayTempAsCelsius(double[] tempsKelvin, int tempIndex)
        {
            if (tempsKelvin == null || tempsKelvin.Length <= tempIndex)
            {
                return "n/a";
            }
            var tempKelvin = tempsKelvin[tempIndex];
            return isValidKelvin(tempKelvin)
                ? ((float)tempKelvin - 273.15f).ToString("0.0")
                : "n/a";
        }

        private void logLMUTyreTemperatureReadDebug(GameStateData cgs, ref rF2Wheel wheelFrontLeft, ref rF2Wheel wheelFrontRight, ref rF2Wheel wheelRearLeft, ref rF2Wheel wheelRearRight)
        {
            if (!Game.LMU || cgs == null || cgs.SessionData == null || cgs.TyreData == null || !cgs.SessionData.IsNewSector)
            { // Only log for LMU and once per sector.
                return;
            }

            Log.Tyres("[LMU_TYRE_TEMP_READ]");
            Log.Tyres($"lap={cgs.SessionData.CompletedLaps} sector={cgs.SessionData.SectorNumber}");
            Log.Tyres($"FL centre surface={getArrayTempAsCelsius(wheelFrontLeft.mTemperature, 1)} C, inner={getArrayTempAsCelsius(wheelFrontLeft.mTireInnerLayerTemperature, 1)} C, carcase={(wheelFrontLeft.mTireCarcassTemperature - 273.15f):0.0} C; overall={cgs.TyreData.FrontLeft_CenterTemp:0.0} C");
            Log.Tyres($"FR centre surface={getArrayTempAsCelsius(wheelFrontRight.mTemperature, 1)} C, inner={getArrayTempAsCelsius(wheelFrontRight.mTireInnerLayerTemperature, 1)} C, carcase={(wheelFrontRight.mTireCarcassTemperature - 273.15f):0.0} C; overall={cgs.TyreData.FrontRight_CenterTemp:0.0} C");
            Log.Tyres($"RL centre surface={getArrayTempAsCelsius(wheelRearLeft.mTemperature, 1)} C, inner={getArrayTempAsCelsius(wheelRearLeft.mTireInnerLayerTemperature, 1)} C, carcase={(wheelRearLeft.mTireCarcassTemperature - 273.15f):0.0} C; overall={cgs.TyreData.RearLeft_CenterTemp:0.0} C");
            Log.Tyres($"RR centre surface={getArrayTempAsCelsius(wheelRearRight.mTemperature, 1)} C, inner={getArrayTempAsCelsius(wheelRearRight.mTireInnerLayerTemperature, 1)} C, carcase={(wheelRearRight.mTireCarcassTemperature - 273.15f):0.0} C; overall={cgs.TyreData.RearRight_CenterTemp:0.0} C");
        }

        private float getTyreTempCelsius(ref rF2Wheel wheel, int tempIndex)
        {
            // LMU: use inner rubber layer when available; fallback to surface temperatures.
            double tempKelvin = 0.0;
            if (Game.LMU
                && wheel.mTireInnerLayerTemperature != null
                && wheel.mTireInnerLayerTemperature.Length > tempIndex)
            {
                var innerLayerKelvin = (wheel.mTireInnerLayerTemperature[tempIndex] +
                    wheel.mTireCarcassTemperature) / 2.0;
                if (isValidKelvin(innerLayerKelvin))
                {
                    tempKelvin = innerLayerKelvin;
                }
            }

            if (tempKelvin <= 0.0
                && wheel.mTemperature != null
                && wheel.mTemperature.Length > tempIndex)
            {
                tempKelvin = wheel.mTemperature[tempIndex];
            }

            if (!isValidKelvin(tempKelvin))
            {
                return 0.0f;
            }

            return (float)tempKelvin - 273.15f;
        }
        #endregion LMU_SUPPORT

        private static double getVehicleSpeed(ref rF2VehicleTelemetry vehicleTelemetry)
        {
            return Math.Sqrt((vehicleTelemetry.mLocalVel.x * vehicleTelemetry.mLocalVel.x)
                + (vehicleTelemetry.mLocalVel.y * vehicleTelemetry.mLocalVel.y)
                + (vehicleTelemetry.mLocalVel.z * vehicleTelemetry.mLocalVel.z));
        }

        private static double getVehicleSpeed(ref rF2VehicleScoring vehicleScoring)
        {
            return Math.Sqrt((vehicleScoring.mLocalVel.x * vehicleScoring.mLocalVel.x)
                + (vehicleScoring.mLocalVel.y * vehicleScoring.mLocalVel.y)
                + (vehicleScoring.mLocalVel.z * vehicleScoring.mLocalVel.z));
        }

        private static double getEstimatedLapDist(RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper shared, ref rF2VehicleScoring vehicleScoring, ref rF2VehicleTelemetry vehicleTelemetry)
        {
            // Estimate lapdist
            // See how much ahead telemetry is ahead of scoring update
            var delta = vehicleTelemetry.mElapsedTime - shared.scoring.mScoringInfo.mCurrentET;
            var lapDistEstimated = vehicleScoring.mLapDist;
            if (delta > 0.0)
            {
                var localZAccelEstimated = vehicleScoring.mLocalAccel.z * delta;
                var localZVelEstimated = vehicleScoring.mLocalVel.z + localZAccelEstimated;

                lapDistEstimated = vehicleScoring.mLapDist - localZVelEstimated * delta;
            }

            return lapDistEstimated;
        }

        private void processPlayerTimingData(
            ref rF2Scoring scoring,
            GameStateData currentGameState,
            GameStateData previousGameState,
            ref rF2VehicleScoring playerScoring)
        {
            var cgs = currentGameState;
            var csd = cgs.SessionData;
            var psd = previousGameState != null ? previousGameState.SessionData : null;

            // Clear all the timings one new session.
            if (csd.IsNewSession)
                return;

            Debug.Assert(psd != null);

            /////////////////////////////////////
            // Current lap timings
            csd.LapTimeCurrent = csd.SessionRunningTime - (float)playerScoring.mLapStartET;
            csd.LapTimePrevious = playerScoring.mLastLapTime > 0.0f ? (float)playerScoring.mLastLapTime : -1.0f;

            // Last (most current) per-sector times:
            // NOTE: this logic still misses invalid sector handling.
            var lastS1Time = playerScoring.mLastSector1 > 0.0 ? playerScoring.mLastSector1 : -1.0;
            var lastS2Time = playerScoring.mLastSector1 > 0.0 && playerScoring.mLastSector2 > 0.0
                ? playerScoring.mLastSector2 - playerScoring.mLastSector1 : -1.0;
            var lastS3Time = playerScoring.mLastSector2 > 0.0 && playerScoring.mLastLapTime > 0.0
                ? playerScoring.mLastLapTime - playerScoring.mLastSector2 : -1.0;

            csd.LastSector1Time = (float)lastS1Time;
            csd.LastSector2Time = (float)lastS2Time;
            csd.LastSector3Time = (float)lastS3Time;

            // Check if we have more current values for S1 and S2.
            // S3 always equals to lastS3Time.
            if (playerScoring.mCurSector1 > 0.0)
                csd.LastSector1Time = (float)playerScoring.mCurSector1;

            if (playerScoring.mCurSector1 > 0.0 && playerScoring.mCurSector2 > 0.0)
                csd.LastSector2Time = (float)(playerScoring.mCurSector2 - playerScoring.mCurSector1);

            // Verify lap is valid
            // First, verify if previous sector has invalid time.
            if (((csd.SectorNumber == 2 && csd.LastSector1Time < 0.0f
                    || csd.SectorNumber == 3 && csd.LastSector2Time < 0.0f)
                // And, this is not an out/in lap
                && !cgs.PitData.OnOutLap && !cgs.PitData.OnInLap
                // And it's Race or Qualification
                && (csd.SessionType == SessionType.Race || 
                    csd.SessionType == SessionType.Qualify ||
                    csd.SessionType == SessionType.PrivateQualify)))
            {
                csd.CurrentLapIsValid = false;
            }
            // If current lap was marked as invalid, keep it that way.
            else if (psd.CompletedLaps == csd.CompletedLaps  // Same lap
                     && !psd.CurrentLapIsValid)
            {
                csd.CurrentLapIsValid = false;
            }
            // rF2 lap time or whole lap won't count
            else if (playerScoring.mCountLapFlag != (byte)rFactor2Constants.rF2CountLapFlag.CountLapAndTime
                // And, this is not an out/in lap
                && !cgs.PitData.OnOutLap && !cgs.PitData.OnInLap)
            {
                csd.CurrentLapIsValid = false;
            }

            // Check if timing update is needed.
            if (!csd.IsNewLap && !csd.IsNewSector)
                return;

            /////////////////////////////////////////
            // Update Sector/Lap timings.
            var lastSectorTime = this.getLastSectorTime(ref playerScoring, csd.SectorNumber);

            if (csd.IsNewLap)
            {
                if (lastSectorTime > 0.0f)
                {
                    csd.playerCompleteLapWithProvidedLapTime(
                        csd.OverallPosition,
                        csd.SessionRunningTime,
                        csd.LapTimePrevious,
                        csd.CurrentLapIsValid,
                        playerScoring.mInPits == 1,
                        scoring.mScoringInfo.mRaining > minRainThreshold,
                        (float)scoring.mScoringInfo.mTrackTemp,
                        (float)scoring.mScoringInfo.mAmbientTemp,
                        csd.SessionHasFixedTime,
                        csd.SessionTimeRemaining,
                        3, cgs.TimingData,
                        null, null);
                }

                csd.playerStartNewLap(
                    csd.CompletedLaps + 1,
                    csd.OverallPosition,
                    playerScoring.mInPits == 1 || currentGameState.PositionAndMotionData.DistanceRoundTrack < 0.0f,
                    csd.SessionRunningTime);
            }
            else if (csd.IsNewSector && lastSectorTime > 0.0f)
            {
                csd.playerAddCumulativeSectorData(
                    psd.SectorNumber,
                    csd.OverallPosition,
                    lastSectorTime,
                    csd.SessionRunningTime,
                    csd.CurrentLapIsValid,
                    scoring.mScoringInfo.mRaining > minRainThreshold,
                    (float)scoring.mScoringInfo.mTrackTemp,
                    (float)scoring.mScoringInfo.mAmbientTemp);
            }
        }

        private float getLastSectorTime(ref rF2VehicleScoring vehicle, int currSector)
        {
            var lastSectorTime = -1.0f;
            if (currSector == 1)
                lastSectorTime = vehicle.mLastLapTime > 0.0f ? (float)vehicle.mLastLapTime : -1.0f;
            else if (currSector == 2)
            {
                lastSectorTime = vehicle.mLastSector1 > 0.0f ? (float)vehicle.mLastSector1 : -1.0f;

                if (vehicle.mCurSector1 > 0.0)
                    lastSectorTime = (float)vehicle.mCurSector1;
            }
            else if (currSector == 3)
            {
                lastSectorTime = vehicle.mLastSector2 > 0.0f ? (float)vehicle.mLastSector2 : -1.0f;

                if (vehicle.mCurSector2 > 0.0)
                    lastSectorTime = (float)vehicle.mCurSector2;
            }

            return lastSectorTime;
        }

        private static void writeSpinningLockingDebugMsg(GameStateData cgs, double frontLeftRotation, double frontRightRotation,
            double rearLeftRotation, double rearRightRotation, float minRotatingSpeedOld, float maxRotatingSpeedOld, float minFrontRotatingSpeed, float minRearRotatingSpeed,
            float maxFrontRotatingSpeed, float maxRearRotatingSpeed)
        {
            if (cgs.TyreData.LeftFrontIsLocked)
                Log.Debug(string.Format("Left Front is locked.  mRotation: {0}  minFrontRotatingSpeed: {1}  minRotatingSpeedOld: {2}", frontLeftRotation.ToString("0.000"), minFrontRotatingSpeed.ToString("0.000"), minRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.RightFrontIsLocked)
                Log.Debug(string.Format("Right Front is locked.  mRotation: {0}  minFrontRotatingSpeed: {1}  minRotatingSpeedOld: {2}", frontRightRotation.ToString("0.000"), minFrontRotatingSpeed.ToString("0.000"), minRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.LeftRearIsLocked)
                Log.Debug(string.Format("Left Rear is locked.  mRotation: {0}  minRearRotatingSpeed: {1}  minRotatingSpeedOld: {2}", rearLeftRotation.ToString("0.000"), minRearRotatingSpeed.ToString("0.000"), minRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.RightRearIsLocked)
                Log.Debug(string.Format("Right Rear is locked.  mRotation: {0}  minRearRotatingSpeed: {1}  minRotatingSpeedOld: {2}", rearRightRotation.ToString("0.000"), minRearRotatingSpeed.ToString("0.000"), minRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.LeftFrontIsSpinning)
                Log.Debug(string.Format("Left Front is spinning.  mRotation: {0}  maxFrontRotatingSpeed: {1}  maxRotatingSpeedOld: {2}", frontLeftRotation.ToString("0.000"), maxFrontRotatingSpeed.ToString("0.000"), maxRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.RightFrontIsSpinning)
                Log.Debug(string.Format("Right Front is spinning.  mRotation: {0}  maxFrontRotatingSpeed: {1}  maxRotatingSpeedOld: {2}", frontRightRotation.ToString("0.000"), maxFrontRotatingSpeed.ToString("0.000"), maxRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.LeftRearIsSpinning)
                Log.Debug(string.Format("Left Rear is spinning.  mRotation: {0}  maxFronRotatingSpeed: {1}  maxRotatingSpeedOld: {2}", rearLeftRotation.ToString("0.000"), maxRearRotatingSpeed.ToString("0.000"), maxRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.RightRearIsSpinning)
                Log.Debug(string.Format("Right Rear is spinning.  mRotation: {0}  maxRearRotatingSpeed: {1}  maxRotatingSpeedOld: {2}", rearRightRotation.ToString("0.000"), maxRearRotatingSpeed.ToString("0.000"), maxRotatingSpeedOld.ToString("0.000")));
        }

        private PitWindow mapToPitWindow(rF2YellowFlagState pitWindow)
        {
            // it seems that the pit window is only truly open on multiplayer races?
            if (this.isOfflineSession)
            {
                return PitWindow.Open;
            }
            switch (pitWindow)
            {
                case rFactor2Constants.rF2YellowFlagState.PitClosed:
                    return PitWindow.Closed;
                case rFactor2Constants.rF2YellowFlagState.PitOpen:
                case rFactor2Constants.rF2YellowFlagState.PitLeadLap:
                    return PitWindow.Open;
                default:
                    return PitWindow.Unavailable;
            }
        }


        private SessionPhase mapToSessionPhase(
            rF2GamePhase sessionPhase,
            SessionType sessionType,
            ref rF2VehicleScoring player)
        {
            switch (sessionPhase)
            {
                case rFactor2Constants.rF2GamePhase.Countdown:
                    return SessionPhase.Countdown;
                // warmUp never happens, but just in case
                case rFactor2Constants.rF2GamePhase.WarmUp:
                case rFactor2Constants.rF2GamePhase.Formation:
                    return SessionPhase.Formation;
                case rFactor2Constants.rF2GamePhase.Garage:
                case rFactor2Constants.rF2GamePhase.PausedOrHeartbeat:
                    return SessionPhase.Garage;
                case rFactor2Constants.rF2GamePhase.GridWalk:
                    return SessionPhase.Gridwalk;
                // sessions never go to sessionStopped, they always go straight from greenFlag to sessionOver
                case rFactor2Constants.rF2GamePhase.SessionStopped:
                case rFactor2Constants.rF2GamePhase.SessionOver:
                    if (sessionType == SessionType.Race
                        && player.mFinishStatus == (sbyte)rFactor2Constants.rF2FinishStatus.None)
                    {
                        return SessionPhase.Checkered;
                    }
                    else
                    {
                        return SessionPhase.Finished;
                    }
                // fullCourseYellow will count as greenFlag since we'll call it out in the Flags separately anyway
                case rFactor2Constants.rF2GamePhase.FullCourseYellow:
                    return SessionPhase.FullCourseYellow;
                case rFactor2Constants.rF2GamePhase.GreenFlag:
                    return SessionPhase.Green;
                default:
                    return SessionPhase.Unavailable;
            }
        }

        // finds OpponentData key for given vehicle based on driver name, vehicle class, and world position
        private String GetOpponentKeyForVehicleInfo(ref rF2VehicleScoring vehicleScoring, ref rF2VehicleTelemetry vehicleTelemetry, GameStateData previousGameState, float sessionRunningTime, string driverName, int duplicatesCount, bool vehicleTelemetryAvailable)
        {
            if (previousGameState == null)
                return null;

            var possibleKeys = new List<string>();
            for (int i = 1; i <= duplicatesCount; ++i)
                possibleKeys.Add(driverName + "_duplicate_ " + i);

            float[] worldPos = null;
            if (vehicleTelemetryAvailable)
                worldPos = new float[] { (float)vehicleTelemetry.mPos.x, (float)vehicleTelemetry.mPos.z };
            else
                worldPos = new float[] { (float)vehicleScoring.mPos.x, (float)vehicleScoring.mPos.z };

            float minDistDiff = -1.0f;
            float timeDelta = sessionRunningTime - previousGameState.SessionData.SessionRunningTime;
            string bestKey = null;
            if (timeDelta >= 0.0f)
            {
                foreach (var possibleKey in possibleKeys)
                {
                    OpponentData o = null;
                    if (previousGameState.OpponentData.TryGetValue(possibleKey, out o))
                    {
                        var cci = this.GetCachedCarInfo(ref vehicleScoring);
                        Debug.Assert(!cci.isGhost);

                        var driverNameFromScoring = cci.driverNameRawSanitized;

                        if (o.DriverRawName != driverNameFromScoring
                            || !CarData.IsCarClassEqual(o.CarClass, cci.carClass)
                            || opponentKeysProcessed.Contains(possibleKey))
                            continue;

                        // distance from predicted position
                        float targetDist = o.Speed * timeDelta;
                        float dist = (float)Math.Abs(Math.Sqrt((double)((o.WorldPosition[0] - worldPos[0]) * (o.WorldPosition[0] - worldPos[0]) +
                            (o.WorldPosition[1] - worldPos[1]) * (o.WorldPosition[1] - worldPos[1]))) - targetDist);

                        if (minDistDiff < 0.0f || dist < minDistDiff)
                        {
                            minDistDiff = dist;
                            bestKey = possibleKey;
                        }
                    }
                }
            }

            if (bestKey != null)
                opponentKeysProcessed.Add(bestKey);

            return bestKey;
        }

        public SessionType MapToSessionType(Object wrapper)
        {
            var shared = wrapper as RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper;
            switch (shared.scoring.mScoringInfo.mSession)
            {
                // up to four possible practice sessions
                case 1:
                case 2:
                case 3:
                case 4:
                // test day and pre-race warm-up sessions are 'Practice' as well
                case 0:
                case 9:
                    // This might go from LonePractice to Practice without any nice state transition.  However,
                    // I am not aware of any horrible side effects.
                    return RealOpponents(shared)
                        ? SessionType.Practice
                        : SessionType.LonePractice;
                // up to four possible qualifying sessions
                case 5:
                case 6:
                case 7:
                case 8:
                    return RealOpponents(shared)
                        ? SessionType.Qualify
                        : SessionType.PrivateQualify;
                // up to four possible race sessions
                case 10:
                case 11:
                case 12:
                case 13:
                    return SessionType.Race;
                default:
                    Log.Fatal($"Invalid session type {shared.scoring.mScoringInfo.mSession}");
                    return SessionType.Unavailable;
            }
        }

        private bool RealOpponents(RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper shared)
        {
            if (this.lastPracticeNumVehicles < shared.scoring.mScoringInfo.mNumVehicles)
            {
                this.lastPracticeNumVehicles = shared.scoring.mScoringInfo.mNumVehicles;
                this.lastPracticeNumNonGhostVehicles = 0;
                // Populate cached car info.
                for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
                {
                    var vehicleScoring = shared.scoring.mVehicles[i];

                    var cci = this.GetCachedCarInfo(ref vehicleScoring);
                    if (cci.isGhost)
                        continue;  // Skip trainer.

                    ++this.lastPracticeNumNonGhostVehicles;
                }
            }

            return this.lastPracticeNumNonGhostVehicles > 1; // 1 means player only session.
        }

        private void ProcessTyreTypeClassMapping(CarData.CarClass carClass)
        {
            if (carClass.gameTyreToTyreType.Count == 0)
                return;

            Debug.Assert(this.compoundNameToTyreType.Count == 0);
            Console.WriteLine("Using custom tyre type mapping:");
            foreach (var entry in carClass.gameTyreToTyreType)
            {
                Console.WriteLine($"Compound: \"{entry.Key}\" mapped to: \"{entry.Value}\"");
                this.compoundNameToTyreType.Add(entry.Key.ToUpperInvariant(), entry.Value);
            }
        }

        private TyreType MapToTyreType(ref rF2VehicleTelemetry vehicleTelemetry)
        {
            // Do not cache tyre type if telemetry is not available yet.
            if (vehicleTelemetry.mFrontTireCompoundName == null)
                return TyreType.Uninitialized;

            // For now, use fronts.
            var frontCompound = RF2GameStateMapper.GetStringFromBytes(vehicleTelemetry.mFrontTireCompoundName).ToUpperInvariant();
            var tyreType = TyreType.Unknown_Race;
            if (this.compoundNameToTyreType.TryGetValue(frontCompound, out tyreType))
                return tyreType;

            tyreType = TyreType.Unknown_Race;

            if (string.IsNullOrWhiteSpace(frontCompound))
                tyreType = TyreType.Unknown_Race;
            else if (frontCompound.Contains("HARD"))
                tyreType = TyreType.Hard;
            else if (frontCompound.Contains("MEDIUM"))
                tyreType = TyreType.Medium;
            else if (frontCompound.Contains("SOFT"))
            {
                if (frontCompound.Contains("SUPER"))
                    tyreType = TyreType.Super_Soft;
                else if (frontCompound.Contains("ULTRA"))
                    tyreType = TyreType.Ultra_Soft;
                else if (frontCompound.Contains("HYPER"))
                    tyreType = TyreType.Hyper_Soft;
                else
                    tyreType = TyreType.Soft;
            }
            else if (frontCompound.Contains("WET") || frontCompound.Contains("RAIN"))
                tyreType = TyreType.Wet;
            else if (frontCompound.Contains("INTERMEDIATE"))
                tyreType = TyreType.Intermediate;
            else if (frontCompound.Contains("BIAS") && frontCompound.Contains("PLY"))
                tyreType = TyreType.Bias_Ply;
            else if (frontCompound.Contains("PRIME"))
                tyreType = TyreType.Prime;
            else if (frontCompound.Contains("OPTION"))
                tyreType = TyreType.Option;
            else if (frontCompound.Contains("ALTERNATE"))
                tyreType = TyreType.Alternate;
            else if (frontCompound.Contains("PRIMARY"))
                tyreType = TyreType.Primary;

            // Cache the tyre type.
            this.compoundNameToTyreType.Add(frontCompound, tyreType);

            return tyreType;
        }

        private ControlType MapToControlType(rF2Control controlType)
        {
            switch (controlType)
            {
                case rFactor2Constants.rF2Control.AI:
                    return ControlType.AI;
                case rFactor2Constants.rF2Control.Player:
                    return ControlType.Player;
                case rFactor2Constants.rF2Control.Remote:
                    return ControlType.Remote;
                case rFactor2Constants.rF2Control.Replay:
                    return ControlType.Replay;
                default:
                    return ControlType.Unavailable;
            }
        }

        public static string GetStringFromBytes(byte[] bytes)
        {
            if (bytes != null)
            {
                var nullIdx = Array.IndexOf(bytes, (byte)0);

                return nullIdx >= 0
                  ? Encoding.Default.GetString(bytes, 0, nullIdx)
                  : Encoding.Default.GetString(bytes);
            }
            // Defensive code
            return "";
        }

        public static string GetSanitizedDriverName(string nameFromGame)
        {
            var fwdSlashPos = nameFromGame.IndexOf('/');
            if (fwdSlashPos != -1)
                Console.WriteLine(string.Format(@"Detected pair name: ""{0}"" . Crew Chief does not currently support double names, first part will be used.", nameFromGame));

            var sanitizedName = fwdSlashPos == -1 ? nameFromGame : nameFromGame.Substring(0, fwdSlashPos);

            return sanitizedName;
        }


        // Since it is not clear if there's any guarantee around vehicle telemetry update order in rF2
        // I don't want to assume mID == i in telemetry.mVehicles.  Also, telemetry is updated separately from scoring,
        // so it is possible order changes in between updates.  Lastly, it is possible scoring to contain mID, but not
        // telemetry.  So, build a lookup map of mID -> i into telemetry mVehicles, for lookup between scoring.mVehicles[].mID
        // into telemetry.
        public static Dictionary<long, int> getIdsToTelIndicesMap(ref rF2Telemetry telemetry)
        {
            var idsToTelIndices = new Dictionary<long, int>();
            for (int i = 0; i < telemetry.mNumVehicles; ++i)
            {
                if (!idsToTelIndices.ContainsKey(telemetry.mVehicles[i].mID))
                    idsToTelIndices.Add(telemetry.mVehicles[i].mID, i);
            }

            return idsToTelIndices;
        }

        // Vehicle telemetry is not always available (before sesssion start).  Instead of
        // hardening code against this case, create and zero intialize arrays within passed in object.
        // This is equivalent of how V1 and rF1 works.
        // NOTE: not a complete initialization, just parts that were cause NRE.
        public static void InitEmptyVehicleTelemetry(ref rF2VehicleTelemetry vehicleTelemetry)
        {
            Debug.Assert(vehicleTelemetry.mWheels == null);

            vehicleTelemetry.mWheels = new rF2Wheel[4];
            for (int i = 0; i < 4; ++i)
                vehicleTelemetry.mWheels[i].mTemperature = new double[3];
        }

        private FrozenOrderData GetFrozenOrderData(FrozenOrderData prevFrozenOrderData, ref rF2VehicleScoring vehicle, ref rF2Scoring scoring,
            ref rF2TrackRulesParticipant vehicleRules, ref rF2Rules rules, ref rF2Extended extended, float vehicleSpeedMS)
        {
            var fod = new FrozenOrderData();

            // Only applies to formation laps and FCY.
            if (scoring.mScoringInfo.mGamePhase != (int)rFactor2Constants.rF2GamePhase.Formation
                && scoring.mScoringInfo.mGamePhase != (int)rFactor2Constants.rF2GamePhase.FullCourseYellow)
            {
                this.numFODetectPhaseAttempts = 0;
                this.safetyCarLeft = false;
                return fod;
            }

            var foStage = rules.mTrackRules.mStage;
            if (foStage == rF2TrackRulesStage.Normal)
                return fod; // Note, there's slight race between scoring and rules here, FO messages should have validation on them.

            // Figure out the phase:
            if (extended.mDirectMemoryAccessEnabled != 0)
            {
                if (prevFrozenOrderData == null || prevFrozenOrderData.Phase == FrozenOrderPhase.None)
                {
                    // Don't bother checking updated ticks, this showld allow catching multiple SC car phases.
                    var phase = RF2GameStateMapper.GetStringFromBytes(extended.mLSIPhaseMessage);

                    if (scoring.mScoringInfo.mGamePhase == (int)rFactor2Constants.rF2GamePhase.Formation
                      && string.IsNullOrWhiteSpace(phase))
                    {
                        if (this.numFODetectPhaseAttempts > RF2GameStateMapper.maxFormationStandingCheckAttempts)
                            fod.Phase = FrozenOrderPhase.FormationStanding;

                        ++this.numFODetectPhaseAttempts;
                    }
                    else if (!string.IsNullOrWhiteSpace(phase)
                      && phase == "Formation Lap")
                        fod.Phase = RF2GameStateMapper.GetSector(vehicle.mSector) == 3 && vehicleSpeedMS > 10.0f ? FrozenOrderPhase.FastRolling : FrozenOrderPhase.Rolling;
                    else if (!string.IsNullOrWhiteSpace(phase)
                      && (phase == "Full-Course Yellow" || phase == "One Lap To Go"))
                        fod.Phase = FrozenOrderPhase.FullCourseYellow;
                    else if (string.IsNullOrWhiteSpace(phase))
                        fod.Phase = prevFrozenOrderData.Phase;
                    else
                        Debug.Assert(false, "Unhandled FO phase");
                }
                else
                {
                    fod.Phase = prevFrozenOrderData.Phase;
                }
            }
            else
            {
                // rF2 currently does not expose what kind of race start is chosen.  For tracks with SC, I use presence of SC to distinguish between
                // Formation/Standing and Rolling starts.  However, if SC does not exist (Kart tracks), I used the fact that in Rolling start leader is
                // typically standing past S/F line (mLapDist is positive).  Obviously, there will be perverted tracks where that won't be true, but this
                // all I could come up with, and real problem is in game being shit in this area.
                var leaderLapDistAtFOPhaseStart = 0.0;
                var leaderSectorAtFOPhaseStart = -1;
                if (foStage != rF2TrackRulesStage.CautionInit && foStage != rF2TrackRulesStage.CautionUpdate  // If this is not FCY.
                  && (prevFrozenOrderData == null || prevFrozenOrderData.Phase == FrozenOrderPhase.None)  // And, this is first FO calculation.
                  && rules.mTrackRules.mSafetyCarExists == 0) // And, track has no SC.
                {
                    // Find where leader is relatively to F/S line.
                    for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                    {
                        var veh = scoring.mVehicles[i];
                        if (veh.mPlace == 1)
                        {
                            leaderLapDistAtFOPhaseStart = veh.mLapDist;
                            leaderSectorAtFOPhaseStart = RF2GameStateMapper.GetSector(veh.mSector);
                            break;
                        }
                    }
                }

                if (foStage == rF2TrackRulesStage.CautionInit || foStage == rF2TrackRulesStage.CautionUpdate)
                    fod.Phase = FrozenOrderPhase.FullCourseYellow;
                else if (foStage == rF2TrackRulesStage.FormationInit || foStage == rF2TrackRulesStage.FormationUpdate)
                {
                    // Check for signs of a rolling start.
                    if ((prevFrozenOrderData != null && prevFrozenOrderData.Phase == FrozenOrderPhase.Rolling)  // If FO started as Rolling, keep it as Rolling even after SC leaves the track
                      || (rules.mTrackRules.mSafetyCarExists == 1 && rules.mTrackRules.mSafetyCarActive == 1)  // Of, if SC exists and is active
                      || (rules.mTrackRules.mSafetyCarExists == 0 && leaderLapDistAtFOPhaseStart > 0.0 && leaderSectorAtFOPhaseStart == 1)) // Or, if SC is not present on a track, and leader started ahead of S/F line and is insector 1.  This will be problem on some tracks.
                        fod.Phase = FrozenOrderPhase.Rolling;
                    else
                    {
                        // Formation / Standing and Fast Rolling have no Safety Car.
                        fod.Phase = (rules.mTrackRules.mStage == rF2TrackRulesStage.FormationInit ||
                                     (Game.LMU && rules.mTrackRules.mStage == rF2TrackRulesStage.FormationUpdate))
                                    && RF2GameStateMapper.GetSector(vehicle.mSector) == 3
                          ? FrozenOrderPhase.FastRolling  // Fast rolling never goes into FormationUpdate and usually starts in S3.
                          : FrozenOrderPhase.FormationStanding;
                    }
                }
            }

            if (fod.Phase == FrozenOrderPhase.None)
                return fod;  // Wait a bit, there's a delay for string based phases.

            if (this.safetyCarLeft)
            {
                // Afer SC left, disable order messages.  This is not perfect, because there might be useful data, but it is an attempt
                // to suppress weird messages during Rolling start, when pit lane is long.  Still did not get exact repro, will have to revisit.
                Debug.Assert(fod.Action == FrozenOrderAction.None);
                return fod;
            }

            var useSCRules = GlobalBehaviourSettings.useAmericanTerms && extended.mDirectMemoryAccessEnabled != 0 && extended.mSCRPluginEnabled != 0;
            if (vehicleRules.mPositionAssignment != -1)
            {
                var gridOrder = false;
                var scrLastLapDoubleFile = useSCRules
                    && fod.Phase == FrozenOrderPhase.FullCourseYellow
                    && (extended.mSCRPluginDoubleFileType == 1 || extended.mSCRPluginDoubleFileType == 2)
                    && scoring.mScoringInfo.mYellowFlagState == (sbyte)rFactor2Constants.rF2YellowFlagState.LastLap;

                if (fod.Phase == FrozenOrderPhase.FullCourseYellow  // Core FCY does not use grid order.
                      && !scrLastLapDoubleFile)  // With SCR rules, however, last lap might be double file depending on DoubleFileType configuration var value.
                {
                    gridOrder = false;
                    fod.AssignedPosition = vehicleRules.mPositionAssignment + 1;  // + 1, because it is zero based with 0 meaning follow SC.
                }
                else if (Game.RF2_64BIT)  // This is not FCY, or last lap of Double File FCY with SCR plugin enabled.  The order reported is grid order, with columns specified.
                {
                    gridOrder = true;
                    fod.AssignedGridPosition = vehicleRules.mPositionAssignment + 1;

                    if (vehicleRules.mColumnAssignment == rF2TrackRulesColumn.LeftLane)
                        fod.AssignedColumn = FrozenOrderColumn.Left;
                    else if (vehicleRules.mColumnAssignment == rF2TrackRulesColumn.RightLane)
                        fod.AssignedColumn = FrozenOrderColumn.Right;

                    if (rules.mTrackRules.mPoleColumn == rF2TrackRulesColumn.LeftLane)
                    {
                        fod.AssignedPosition = (vehicleRules.mColumnAssignment == rF2TrackRulesColumn.LeftLane
                          ? vehicleRules.mPositionAssignment * 2
                          : vehicleRules.mPositionAssignment * 2 + 1) + 1;
                    }
                    else if (rules.mTrackRules.mPoleColumn == rF2TrackRulesColumn.RightLane)
                    {
                        fod.AssignedPosition = (vehicleRules.mColumnAssignment == rF2TrackRulesColumn.RightLane
                          ? vehicleRules.mPositionAssignment * 2
                          : vehicleRules.mPositionAssignment * 2 + 1) + 1;
                    }

                }
                else // LMU has different enum values, no mid-left and mid-right.
                {
                    {
                        gridOrder = true;
                        fod.AssignedGridPosition = vehicleRules.mPositionAssignment + 1;

                        if ((lmuTrackRulesColumn)vehicleRules.mColumnAssignment == lmuTrackRulesColumn.LeftLane)
                            fod.AssignedColumn = FrozenOrderColumn.Left;
                        else if ((lmuTrackRulesColumn)vehicleRules.mColumnAssignment == lmuTrackRulesColumn.RightLane)
                            fod.AssignedColumn = FrozenOrderColumn.Right;

                        if ((lmuTrackRulesColumn)rules.mTrackRules.mPoleColumn == lmuTrackRulesColumn.LeftLane)
                        {
                            fod.AssignedPosition = ((lmuTrackRulesColumn)vehicleRules.mColumnAssignment == lmuTrackRulesColumn.LeftLane
                              ? vehicleRules.mPositionAssignment * 2
                              : vehicleRules.mPositionAssignment * 2 + 1) + 1;
                        }
                        else if ((lmuTrackRulesColumn)rules.mTrackRules.mPoleColumn == lmuTrackRulesColumn.RightLane)
                        {
                            fod.AssignedPosition = ((lmuTrackRulesColumn)vehicleRules.mColumnAssignment == lmuTrackRulesColumn.RightLane
                              ? vehicleRules.mPositionAssignment * 2
                              : vehicleRules.mPositionAssignment * 2 + 1) + 1;
                        }
                    }
                }

                    // Figure out Driver Name to follow.
                    // NOTE: In Formation/Standing, game does not report those in UI, but we can.
                    var vehToFollowId = -1;
                bool followSC = true;
                if ((gridOrder && fod.AssignedPosition > 2)  // In grid order, first 2 vehicles are following SC.
                  || (!gridOrder && fod.AssignedPosition > 1))  // In non-grid order, 1st car is following SC.
                {
                    followSC = false;
                    // Find the mID of a vehicle in front of us by frozen order.
                    for (int i = 0; i < rules.mTrackRules.mNumParticipants; ++i)
                    {
                        var p = rules.mParticipants[i];
                        if ((!gridOrder  // Don't care about column in non-grid order case.
                            || (gridOrder && p.mColumnAssignment == vehicleRules.mColumnAssignment))  // Should be vehicle in the same column.
                          && p.mPositionAssignment == (vehicleRules.mPositionAssignment - 1))
                        {
                            vehToFollowId = p.mID;
                            break;
                        }
                    }
                }

                var playerDist = RF2GameStateMapper.GetDistanceCompleted(ref scoring, ref vehicle);
                var toFollowDist = -1.0;

                if (!followSC)
                {
                    // Now find the vehicle to follow from the scoring info.
                    for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                    {
                        var v = scoring.mVehicles[i];
                        if (v.mID == vehToFollowId)
                        {
                            var cci = this.GetCachedCarInfo(ref v);
                            Debug.Assert(!cci.isGhost);

                            fod.DriverToFollowRaw = cci.driverNameRawSanitized;

                            toFollowDist = RF2GameStateMapper.GetDistanceCompleted(ref scoring, ref v);
                            break;
                        }
                    }
                }
                else
                    toFollowDist = ((vehicle.mTotalLaps - vehicleRules.mRelativeLaps) * scoring.mScoringInfo.mLapDist) + rules.mTrackRules.mSafetyCarLapDist;

                if (fod.Phase == FrozenOrderPhase.Rolling
                    && followSC
                    && rules.mTrackRules.mSafetyCarExists == 0)
                {
                    // Find distance to car next to us if we're in pole.
                    var neighborDist = -1.0;
                    for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                    {
                        var veh = scoring.mVehicles[i];
                        if (veh.mPlace == (vehicle.mPlace == 1 ? 2 : 1))
                        {
                            neighborDist = RF2GameStateMapper.GetDistanceCompleted(ref scoring, ref veh);
                            break;
                        }
                    }

                    var distDelta = neighborDist - playerDist;
                    // Special case if we have to stay in pole row, but there's no SC on this track.
                    if (fod.AssignedColumn == FrozenOrderColumn.None)
                        fod.Action = distDelta > 70.0 ? FrozenOrderAction.MoveToPole : FrozenOrderAction.StayInPole;
                    else
                        fod.Action = distDelta > 70.0 ? FrozenOrderAction.MoveToPole : FrozenOrderAction.StayInPole;
                }
                else
                {
                    Debug.Assert(toFollowDist != -1.0);

                    fod.Action = FrozenOrderAction.Follow;

                    var distDelta = toFollowDist - playerDist;
                    if (distDelta < 0.0)
                        fod.Action = FrozenOrderAction.AllowToPass;
                    else if (distDelta > 70.0)
                        fod.Action = FrozenOrderAction.CatchUp;
                }
            }

            if (rules.mTrackRules.mSafetyCarActive == 1)
                fod.SafetyCarSpeed = rules.mTrackRules.mSafetyCarSpeed;

            if (prevFrozenOrderData != null
                && prevFrozenOrderData.SafetyCarSpeed != -1.0f
                && fod.SafetyCarSpeed == -1.0f)
            {
                fod.Action = FrozenOrderAction.None;
                this.safetyCarLeft = true;
            }

            return fod;
        }

        private FrozenOrderData GetFrozenOrderOnlineData(GameStateData cgs, FrozenOrderData prevFrozenOrderData, ref rF2VehicleScoring vehicle,
            ref rF2Scoring scoring, ref rF2Extended extended, float vehicleSpeedMS)
        {
            if (extended.mDirectMemoryAccessEnabled == 0)
                return null;

            var fod = new FrozenOrderData();

            // Only applies to formation laps and FCY.
            if (scoring.mScoringInfo.mGamePhase != (int)rFactor2Constants.rF2GamePhase.Formation
              && scoring.mScoringInfo.mGamePhase != (int)rFactor2Constants.rF2GamePhase.FullCourseYellow)
            {
                this.numFODetectPhaseAttempts = 0;
                return fod;
            }

            if (prevFrozenOrderData != null)
            {
                // Carry old state over.
                fod.Action = prevFrozenOrderData.Action;
                fod.AssignedColumn = prevFrozenOrderData.AssignedColumn;
                fod.AssignedPosition = prevFrozenOrderData.AssignedPosition;
                fod.AssignedGridPosition = prevFrozenOrderData.AssignedGridPosition;
                fod.DriverToFollowRaw = prevFrozenOrderData.DriverToFollowRaw;
                fod.Phase = prevFrozenOrderData.Phase;
                fod.SafetyCarSpeed = prevFrozenOrderData.SafetyCarSpeed;
            }

            if (fod.Phase == FrozenOrderPhase.None)
            {
                // Don't bother checking updated ticks, this showld allow catching multiple SC car phases.
                var phase = RF2GameStateMapper.GetStringFromBytes(extended.mLSIPhaseMessage);

                if (scoring.mScoringInfo.mGamePhase == (int)rFactor2Constants.rF2GamePhase.Formation
                  && string.IsNullOrWhiteSpace(phase))
                {
                    if (this.numFODetectPhaseAttempts > RF2GameStateMapper.maxFormationStandingCheckAttempts)
                        fod.Phase = FrozenOrderPhase.FormationStanding;

                    ++this.numFODetectPhaseAttempts;
                }
                else if (!string.IsNullOrWhiteSpace(phase)
                  && phase == "Formation Lap")
                    fod.Phase = RF2GameStateMapper.GetSector(vehicle.mSector) == 3 && vehicleSpeedMS > 10.0f ? FrozenOrderPhase.FastRolling : FrozenOrderPhase.Rolling;
                else if (!string.IsNullOrWhiteSpace(phase)
                  && (phase == "Full-Course Yellow" || phase == "One Lap To Go"))
                    fod.Phase = FrozenOrderPhase.FullCourseYellow;
                else if (string.IsNullOrWhiteSpace(phase))
                    fod.Phase = prevFrozenOrderData.Phase;
                else
                    Debug.Assert(false, "Unhandled FO phase");
            }

            if (fod.Phase == FrozenOrderPhase.None)
                return fod;  // Wait a bit, there's a delay for string based phases.

            // NOTE: for formation/standing capture order once.   For other phases, rely on LSI text.
            if ((fod.Phase == FrozenOrderPhase.FastRolling || fod.Phase == FrozenOrderPhase.Rolling || fod.Phase == FrozenOrderPhase.FullCourseYellow)
              && this.LSIOrderInstructionMessageUpdatedTicks != extended.mTicksLSIOrderInstructionMessageUpdated)
            {
                this.LSIOrderInstructionMessageUpdatedTicks = extended.mTicksLSIOrderInstructionMessageUpdated;

                var orderInstruction = RF2GameStateMapper.GetStringFromBytes(extended.mLSIOrderInstructionMessage);
                if (!string.IsNullOrWhiteSpace(orderInstruction))
                {
                    Console.WriteLine("LSI Message: order instruction updated - \"" + orderInstruction + "\"");

                    var followPrefix = @"Please Follow ";
                    var catchUpToPrefix = @"Please Catch Up To ";
                    var allowToPassPrefix = @"Please Allow ";

                    var action = FrozenOrderAction.None;

                    string prefix = null;
                    if (orderInstruction.StartsWith(followPrefix))
                    {
                        prefix = followPrefix;
                        action = FrozenOrderAction.Follow;
                    }
                    else if (orderInstruction.StartsWith(catchUpToPrefix))
                    {
                        prefix = catchUpToPrefix;
                        action = FrozenOrderAction.CatchUp;
                    }
                    else if (orderInstruction.StartsWith(allowToPassPrefix))
                    {
                        prefix = allowToPassPrefix;
                        action = FrozenOrderAction.AllowToPass;
                    }
                    else if (orderInstruction == "Please Pass The Safety Car")
                    {
                        // Special case - set action only.
                        fod.Action = FrozenOrderAction.PassSafetyCar;
                    }
                    else
                    {
                        Debug.Assert(false, "unhandled action");
#if !DEBUG
                        Log.DontSpam("LSI Message: unrecognized Frozen Order action - \"" + orderInstruction + "\"", 10).Commentary();
#else
                        Log.DontSpam("LSI Message: unrecognized Frozen Order action - \"" + orderInstruction + "\"", 10).Warning();
#endif
                    }

                    var SCassignedAhead = false;
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        var closingQuoteIdx = orderInstruction.LastIndexOf("\"");
                        string driverName = null;
                        try
                        {
                            if (closingQuoteIdx != -1)
                            {
                                driverName = orderInstruction.Substring(prefix.Length + 1, closingQuoteIdx - prefix.Length - 1);
                            }
                            else
                            {
                                driverName = "Safety Car";
                                SCassignedAhead = true;
                            }
                        }
                        catch (Exception e) { Log.Exception(e); }

                        // Remove [-0.2 laps] if it is there.
                        var lastOpenBckt = orderInstruction.LastIndexOf('[');
                        if (lastOpenBckt != -1)
                        {
                            try
                            {
                                orderInstruction = orderInstruction.Substring(0, lastOpenBckt - 1);
                            }
                            catch (Exception e) { Log.Exception(e); }
                        }

                        var column = FrozenOrderColumn.None;
                        if (orderInstruction.EndsWith("(In Right Line)") || orderInstruction.EndsWith("(In Outside Line)"))
                            column = FrozenOrderColumn.Right;
                        else if (orderInstruction.EndsWith("(In Left Line)") || orderInstruction.EndsWith("(In Inside Line)"))
                            column = FrozenOrderColumn.Left;
                        else if (!orderInstruction.EndsWith("\"") && action == FrozenOrderAction.Follow && !SCassignedAhead)
                        {
                            Debug.Assert(false, "unrecognized postfix");
#if !DEBUG
                            Log.DontSpam("LSI Message: unrecognized Frozen Order message postfix - \"" + orderInstruction + "\"", 10).Commentary();
#else
                            Log.DontSpam("LSI Message: unrecognized Frozen Order message postfix - \"" + orderInstruction + "\"", 10).Warning();
#endif
                        }

                        // Note: assigned Grid position only matters for Formation/Standing - don't bother figuring it out, just figure out assigned position (starting position).
                        var assignedPos = -1;
                        if (!string.IsNullOrWhiteSpace(driverName))
                        {
                            if (!SCassignedAhead)
                            {
                                for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                                {
                                    var veh = scoring.mVehicles[i];
                                    var driver = RF2GameStateMapper.GetStringFromBytes(veh.mDriverName);
                                    if (driver == driverName)
                                    {
                                        if (column == FrozenOrderColumn.None)
                                        {
                                            assignedPos = action == FrozenOrderAction.Follow || action == FrozenOrderAction.CatchUp
                                              ? veh.mPlace + 1
                                              : veh.mPlace - 1; // Might not be true
                                        }
                                        else
                                        {
                                            assignedPos = action == FrozenOrderAction.Follow || action == FrozenOrderAction.CatchUp
                                              ? veh.mPlace + 2
                                              : veh.mPlace - 2; // Might not be true
                                        }
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                assignedPos = vehicle.mPlace;
                            }
                        }

                        fod.Action = action;
                        fod.AssignedColumn = column;
                        fod.DriverToFollowRaw = driverName;
                        fod.AssignedPosition = assignedPos;
                    }
                }
            }
            else if ((prevFrozenOrderData == null || prevFrozenOrderData.Phase == FrozenOrderPhase.None)
              && fod.Phase == FrozenOrderPhase.FormationStanding)
            {
                // Just capture the starting position.
                fod.AssignedColumn = vehicle.mTrackEdge > 0.0 ? FrozenOrderColumn.Right : FrozenOrderColumn.Left;
                fod.AssignedPosition = vehicle.mPlace;

                // We need to know which side of a grid leader is here, gosh what a bullshit.
                // Find where leader is relatively to F/S line.
                var leaderCol = FrozenOrderColumn.None;
                for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                {
                    var veh = scoring.mVehicles[i];
                    if (veh.mPlace == 1)
                    {
                        leaderCol = veh.mTrackEdge > 0.0 ? FrozenOrderColumn.Right : FrozenOrderColumn.Left;
                        break;
                    }
                }

                if (fod.AssignedColumn == FrozenOrderColumn.Left)
                {
                    fod.AssignedGridPosition = leaderCol == FrozenOrderColumn.Left
                      ? (vehicle.mPlace / 2) + 1
                      : vehicle.mPlace / 2;
                }
                else if (fod.AssignedColumn == FrozenOrderColumn.Right)
                {
                    fod.AssignedGridPosition = leaderCol == FrozenOrderColumn.Right
                      ? (vehicle.mPlace / 2) + 1
                      : vehicle.mPlace / 2;
                }
            }

            return fod;
        }

        private static double GetDistanceCompleted(ref rF2Scoring scoring, ref rF2VehicleScoring vehicle)
        {
            // Note: Can be interpolated a bit.
            return vehicle.mTotalLaps * scoring.mScoringInfo.mLapDist + vehicle.mLapDist;
        }

        private static int GetSector(int rf2Sector)
        {
            return rf2Sector == 0 ? 3 : rf2Sector;
        }

        private static PositionAndMotionData.Rotation GetRotation(ref rF2Vec3[] orientation)
        {
            var rot = new PositionAndMotionData.Rotation()
            {
                Yaw = (float)Math.Atan2(orientation[rFactor2Constants.RowZ].x, orientation[rFactor2Constants.RowZ].z),

                Pitch = (float)Math.Atan2(-orientation[rFactor2Constants.RowY].z,
                    Math.Sqrt(orientation[rFactor2Constants.RowX].z * orientation[rFactor2Constants.RowX].z + orientation[rFactor2Constants.RowZ].z * orientation[rFactor2Constants.RowZ].z)),

                Roll = (float)Math.Atan2(orientation[rFactor2Constants.RowY].x,
                    Math.Sqrt(orientation[rFactor2Constants.RowX].x * orientation[rFactor2Constants.RowX].x + orientation[rFactor2Constants.RowZ].x * orientation[rFactor2Constants.RowZ].x))
            };

            return rot;
        }

        private CarInfo GetCachedCarInfo(ref rF2VehicleScoring vehicleScoring)
        {
            CarInfo ci = null;
            if (this.idToCarInfoMap.TryGetValue(vehicleScoring.mID, out ci))
                return ci;

            var driverName = RF2GameStateMapper.GetStringFromBytes(vehicleScoring.mDriverName);
            driverName = RF2GameStateMapper.GetSanitizedDriverName(driverName);

            var carClassId = RF2GameStateMapper.GetStringFromBytes(vehicleScoring.mVehicleClass);
            var carClass = CarData.getCarClassForClassNameOrCarName(carClassId);

            // Name does not appear to be localized in rF2, so hardcoding it is ok for now.
            var isGhost = string.Equals(driverName, "transparent trainer", StringComparison.InvariantCultureIgnoreCase);

            ci = new CarInfo()
            {
                carClass = carClass,
                driverNameRawSanitized = driverName,
                isGhost = isGhost
            };

            this.idToCarInfoMap.Add(vehicleScoring.mID, ci);

            return ci;
        }
    }
}
