//#define SIMULATE_ONLINE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

using CrewChiefV4.GameState;
using CrewChiefV4.Events;
using GTR2SharedMemory;
using static GTR2SharedMemory.GTR2Constants;
using GTR2SharedMemory.GTR2Data;
using CrewChiefV4.Audio;

/**
 * Maps memory mapped file to a local game-agnostic representation.
 */
namespace CrewChiefV4.GTR2
{
    public class GTR2GameStateMapper : GameStateMapper
    {
        // User preference constants.
        private readonly bool enablePitStopPrediction = UserSettings.GetUserSettings().getBoolean("enable_gtr2_pit_stop_prediction");
        private readonly bool enableFrozenOrderMessages = true; // UserSettings.GetUserSettings().getBoolean("enable_gtr2_frozen_order_messages");
        private readonly bool enableCutTrackHeuristics = UserSettings.GetUserSettings().getBoolean("enable_gtr2_cut_track_heuristics");
        private readonly bool enablePitLaneApproachHeuristics = UserSettings.GetUserSettings().getBoolean("enable_gtr2_pit_lane_approach_heuristics");
        //private readonly bool useRealWheelSizeForLockingAndSpinning = UserSettings.GetUserSettings().getBoolean("use_gtr2_wheel_size_for_locking_and_spinning");
        //private readonly bool enableWrongWayMessage = UserSettings.GetUserSettings().getBoolean("enable_gtr2_wrong_way_message");
        private readonly bool disableRaceEndMessagesOnAbandon = true; // UserSettings.GetUserSettings().getBoolean("disable_gtr2_race_end_messages_on_abandoned_sessions");

        public static string playerName = null;

        private List<CornerData.EnumWithThresholds> suspensionDamageThresholds = new List<CornerData.EnumWithThresholds>();
        private List<CornerData.EnumWithThresholds> tyreWearThresholds = new List<CornerData.EnumWithThresholds>();
        private List<CornerData.EnumWithThresholds> tyreFlatSpotThresholds = new List<CornerData.EnumWithThresholds>();
        private List<CornerData.EnumWithThresholds> tyreDirtPickupThresholds = new List<CornerData.EnumWithThresholds>();

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
            public string carName = null;
            public string teamName = null;
            public int carNumber = -1;
            public string carNumberStr = null;
            public int year = -1;
            public OpponentData opponentData = null;
        }

        private Dictionary<long, CarInfo> idToCarInfoMap = new Dictionary<long, CarInfo>();

        // barebones lazy ass fallback if someone disables DMA
        private Dictionary<string, CarInfo> driverNameToCarInfoMap = new Dictionary<string, CarInfo>();

        // Message center stuff
        private Int64 lastFirstHistoryMessageUpdatedTicks = 0L;
        private Int64 lastSecondHistoryMessageUpdatedTicks = 0L;
        private Int64 lastThirdHistoryMessageUpdatedTicks = 0L;
#if DEBUG
        private Int64 statusMessageUpdatedTicks = 0L;
#endif
        private Int64 LSIPitStateMessageUpdatedTicks = 0L;
        private Int64 LSIRulesInstructionMessageUpdatedTicks = 0L;
        private Int64 firstHistoryMessageUpdatedTicks = 0L;

        // Frozen order processing.
        private Int64 firstHistoryMessageUpdatedFOTicks = 0L;
        private Int64 secondHistoryMessageUpdatedFOTicks = 0L;
        private Int64 thirdHistoryMessageUpdatedFOTicks = 0L;
        private string firstHistoryMessage = "";
        private string secondHistoryMessage = "";
        private string thirdHistoryMessage = "";
        private Int64 lastFOChangeTicks = DateTime.MinValue.Ticks;
        private int lastKnownVehicleToFollowID = -1;
        private const int SPECIAL_MID_NONE = -2;
        private const int SPECIAL_MID_SAFETY_CAR = -1;

        // Since some of MC messages disapper (Player Control: N, for example), we need to remember last message that
        // mattered from CC's standpoint, otherwise, same message could get applied multiple times.
        private string lastEffectiveHistoryMessage = string.Empty;

        // It is however sometimes valid for message to re-appear.  For example, Crew got ready in two separate pit
        // requests.  Allow message processed to expire.
        private readonly int effectiveMessageExpirySeconds = 25;
        private DateTime timeEffectiveMessageProcessed = DateTime.MinValue;
        private DateTime timeHistoryMessageIgnored = DateTime.MinValue;
        private DateTime timeLSIMessageIgnored = DateTime.MinValue;
        private bool nonRaceSessionDurationLogged = false;
        public GTR2GameStateMapper()
        {
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.NEW, -10000.0f, this.scrubbedTyreWearPercent));
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.SCRUBBED, this.scrubbedTyreWearPercent, this.minorTyreWearPercent));
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.MINOR_WEAR, this.minorTyreWearPercent, this.majorTyreWearPercent));
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.MAJOR_WEAR, this.majorTyreWearPercent, this.wornOutTyreWearPercent));
            this.tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.WORN_OUT, this.wornOutTyreWearPercent, 10000.0f));

            this.suspensionDamageThresholds.Add(new CornerData.EnumWithThresholds(DamageLevel.NONE, 0.0f, 0.5f));
            this.suspensionDamageThresholds.Add(new CornerData.EnumWithThresholds(DamageLevel.MAJOR, 0.5f, 1.0f));
            this.suspensionDamageThresholds.Add(new CornerData.EnumWithThresholds(DamageLevel.DESTROYED, 1.0f, 2.0f));

            this.tyreFlatSpotThresholds.Add(new CornerData.EnumWithThresholds(TyreFlatSpotState.NONE, 0.0f, Single.Epsilon));
            this.tyreFlatSpotThresholds.Add(new CornerData.EnumWithThresholds(TyreFlatSpotState.MINOR, Single.Epsilon, 0.3f));

            // Secondary lockups start at around 0.2, that's when shit hits the fan really.
            // Weird - 1.0 + Single.Epsilon does not work.
            this.tyreFlatSpotThresholds.Add(new CornerData.EnumWithThresholds(TyreFlatSpotState.MAJOR, 0.3f, 1.1f));

            this.tyreDirtPickupThresholds.Add(new CornerData.EnumWithThresholds(TyreDirtPickupState.NONE, 0.0f, 0.7f));
            this.tyreDirtPickupThresholds.Add(new CornerData.EnumWithThresholds(TyreDirtPickupState.MAJOR, 0.7f, 1.1f));
        }

        private int[] minimumSupportedVersionParts = new int[] { 2, 4, 2, 0 };
        public static bool pluginVerified = false;
        private static int reinitWaitAttempts = 0;
        public override void versionCheck(Object memoryMappedFileStruct)
        {
            if (GTR2GameStateMapper.pluginVerified || GTR2GameStateMapper.reinitWaitAttempts > 500 || CrewChief.loadDataFromFile)
                return;

            try
            {
                var shared = memoryMappedFileStruct as GTR2SharedMemoryReader.GTR2StructWrapper;
                var versionStr = GTR2GameStateMapper.GetStringFromBytes(shared.extended.mVersion);
                if (string.IsNullOrWhiteSpace(versionStr)
                    && GTR2GameStateMapper.reinitWaitAttempts < 500)
                {
                    // SimHub (and possibly other tools) leaks the shared memory block, making us read the empty one.
                    // Wait a bit before re-checking version string.
                    ++GTR2GameStateMapper.reinitWaitAttempts;
                    Thread.Sleep(100);
                    return;
                }

                // Only verify once.  If user is super unlucky, string may still be incorrect due to timing.  However, in GTR2 this
                // check is informational only, it won't stop mapper from running.
                GTR2GameStateMapper.pluginVerified = true;
                GTR2GameStateMapper.reinitWaitAttempts = 0;

                var failureHelpMsg = ".\nMake sure you have \"Update game plugins on startup\" option enabled."
                    + "\nFor manual setup instructions, visit https://thecrewchief.org/showthread.php?2012-GTR2-Setup-Instructions-and-Known-Issues.";

                var versionParts = versionStr.Split('.');
                if (versionParts.Length != 4)
                {
                    Console.WriteLine("Corrupt or leaked GTR 2 Shared Memory.  Version string: " + versionStr + failureHelpMsg);
                    return;
                }

                int smVer = 0;
                int minVer = 0;
                int partFactor = 1;
                for (int i = 3; i >= 0; --i)
                {
                    int versionPart = 0;
                    if (!int.TryParse(versionParts[i], out versionPart))
                    {
                        Console.WriteLine("Corrupt or leaked GTR 2 Shared Memory version.  Version string: " + versionStr + failureHelpMsg);
                        return;
                    }

                    smVer += (versionPart * partFactor);
                    minVer += (this.minimumSupportedVersionParts[i] * partFactor);
                    partFactor *= 100;
                }

                if (smVer < minVer)
                {
                    var minVerStr = string.Join(".", this.minimumSupportedVersionParts);
                    var msg1 = "Unsupported GTR 2 Shared Memory version: " + versionStr;

                    var msg2 = "Minimum supported version is: "
                        + minVerStr
                        + "\nPlease update the ccgep.dll" + failureHelpMsg;
                    Console.WriteLine(msg1 + " " + msg2);
                    MessageBox.Show(msg2, msg1,
                        //Configuration.getUIString("install_plugin_popup_enable_text"),
                        //Configuration.getUIString("install_plugin_popup_enable_title"),
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                }
                else
                {
                    var msg = "GTR2 Shared Memory version: " + versionStr;
                    Console.WriteLine(msg);
                }
            }
            catch (Exception)
            {
                // If we're very unlucky, SM might be dirty and string parsing would crash.
                if (GTR2GameStateMapper.reinitWaitAttempts < 500)
                {
                    ++GTR2GameStateMapper.reinitWaitAttempts;
                    Thread.Sleep(100);
                }
            }
        }

        // Abrupt session detection variables.
        private bool waitingToTerminateSession = false;
        private long ticksWhenSessionEnded = DateTime.MinValue.Ticks;

        // Used to reduce number of "Waiting" messages on abrupt session end.
        private int sessionWaitMessageCounter = 0;

        private Int64 lastSessionEndTicks = -1;
        private int lastGameSession = -1;
        private bool lastInRealTimeState = false;

        private void ClearState()
        {
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
            this.idToCarInfoMap.Clear();
            this.driverNameToCarInfoMap.Clear();
            this.compoundNameToTyreType.Clear();
            this.lastPenaltyTime = DateTime.MinValue;

            this.lastEffectiveHistoryMessage = string.Empty;
            this.timeEffectiveMessageProcessed = DateTime.MinValue;

            this.timeHistoryMessageIgnored = DateTime.MinValue;
            this.timeLSIMessageIgnored = DateTime.MinValue;
            this.lastFirstHistoryMessageUpdatedTicks = 0L;
            this.lastSecondHistoryMessageUpdatedTicks = 0L;
            this.lastThirdHistoryMessageUpdatedTicks = 0L;

            this.lastGameSession = -1;

            this.rspwState = RollingStateWorkaroundState.Done;
            this.rspwIDToRSWData = null;

            this.lastFOChangeTicks = DateTime.MinValue.Ticks;
            this.lastKnownVehicleToFollowID = GTR2GameStateMapper.SPECIAL_MID_NONE;

            this.nonRaceSessionDurationLogged = false;
        }

        public override GameStateData mapToGameStateData(Object memoryMappedFileStruct, GameStateData previousGameState)
        {
            var pgs = previousGameState;
            var shared = memoryMappedFileStruct as GTR2SharedMemoryReader.GTR2StructWrapper;
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

            if (!sessionJustEnded
                && this.lastGameSession != -1 // There was a session before.
                && shared.scoring.mScoringInfo.mSession != 0 // Exclude 0 because we can't tell empty from Session 0
                && this.lastGameSession != shared.scoring.mScoringInfo.mSession)
            {
                Console.WriteLine($"Abrupt Session End: consider session end by transition from '{this.lastGameSession}' to '{shared.scoring.mScoringInfo.mSession}'");
                this.lastGameSession = shared.scoring.mScoringInfo.mSession;
                sessionJustEnded = true;
            }

            this.lastSessionEndTicks = shared.extended.mTicksSessionEnded;
            var sessionStarted = shared.extended.mSessionStarted == 1;/*
                || (pgs != null && pgs.SessionData.SessionType == SessionType.Practice  // For some reason, no Started signal on second practice in GTR2.
                    || cgs.SessionData.SessionType == SessionType.Practice);*/

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
                        if (timeSinceWaitStarted.TotalMilliseconds < GTR2GameStateMapper.waitForSessionEndMillis)
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
            var playerScoring = new GTR2VehicleScoring();
            var leaderScoring = new GTR2VehicleScoring();
            for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicle = shared.scoring.mVehicles[i];
                switch (this.MapToControlType((GTR2Control)vehicle.mControl))
                {
                    case ControlType.AI:
                    case ControlType.Player:
                    case ControlType.Remote:
                        if (vehicle.mIsPlayer == 1)
                            playerScoring = vehicle;

                        if (this.GetPreprocessedPlace(ref vehicle) == 1)
                            leaderScoring = vehicle;
                        break;

                    default:
                        continue;
                }

                if (playerScoring.mIsPlayer == 1 && this.GetPreprocessedPlace(ref leaderScoring) == 1)
                    break;
            }

            // Can't find the player or session leader vehicle info (replay).  No useful data is available.
            if (playerScoring.mIsPlayer != 1 || this.GetPreprocessedPlace(ref leaderScoring) != 1)
            {
                if (pgs != null)
                    pgs.SessionData.AbruptSessionEndDetected = false;  // Not 100% sure how this happened, but I saw us entering inifinite session restart due to this sticking.

                return pgs;
            }

            var playerExtendedScoring = shared.extended.mExtendedVehicleScoring[playerScoring.mID % GTR2Constants.MAX_MAPPED_IDS];

            if (GTR2GameStateMapper.playerName == null)
            {
                var driverName = GTR2GameStateMapper.GetStringFromBytes(playerScoring.mDriverName);
                AdditionalDataProvider.validate(driverName);
                GTR2GameStateMapper.playerName = driverName;
            }

            // Get player and leader telemetry objects.
            // NOTE: Those are not available on first entry to the garage and likely in rare
            // cases during online races.  But using just zeroed structs are mostly ok.
            var playerTelemetry = new GTR2VehicleTelemetry();

            // This is shaky part of the mapping, but here goes:
            // Telemetry and Scoring are updated separately by the game.  Therefore, one can be
            // ahead of another, sometimes in a significant way.  Particularly, this is possible with
            // online races, where people quit/join the game.
            //
            // For Crew Chief in GTR2, our primary data structure is _Scoring_ (because it contains timings).
            // However, since Telemetry is updated more frequently (up to 90FPS vs 5FPS for Scoring), we
            // try to use Telemetry values whenever possible (position, speed, elapsed time, orientation).
            // In those rare cases where Scoring contains vehicle that is not in Telemetry set, use Scoring as a
            // fallback where possible.  For the rest of values, use zeroed out Telemetry object (playerTelemetry).

            playerTelemetry = shared.telemetry.mPlayerTelemetry;
            /*else
            {
                playerTelemetryAvailable = false;
                GTR2GameStateMapper.InitEmptyVehicleTelemetry(ref playerTelemetry);

                // Exclude known situations when telemetry is not available, but log otherwise to get more
                // insights.
                if (shared.extended.mInRealtimeFC == 1
                    && shared.scoring.mScoringInfo.mInRealtime == 1
                    && shared.scoring.mScoringInfo.mGamePhase != (byte)GTR2Constants.GTR2GamePhase.GridWalk)
                {
                    Console.WriteLine("Failed to obtain player telemetry, falling back to scoring.");
                }
            }*/

            // See if there are meaningful updates to the data.
            var currScoringET = shared.scoring.mScoringInfo.mCurrentET;

            if (currScoringET == this.lastScoringET)
            {
                if (pgs != null)
                    pgs.SessionData.AbruptSessionEndDetected = false;  // Not 100% sure how this happened, but I saw us entering inifinite session restart due to this sticking.

                return pgs;  // Skip this update.
            }

            this.lastScoringET = currScoringET;

            // Get player vehicle track rules.
            // these things should remain constant during a session
            var csd = cgs.SessionData;
            var psd = pgs != null ? pgs.SessionData : null;
            csd.EventIndex = shared.scoring.mScoringInfo.mSession;

            csd.SessionIteration
                = shared.scoring.mScoringInfo.mSession >= 1 && shared.scoring.mScoringInfo.mSession <= 2 ? shared.scoring.mScoringInfo.mSession - 1 :  // Practice
                shared.scoring.mScoringInfo.mSession >= 3 && shared.scoring.mScoringInfo.mSession <= 4 ? shared.scoring.mScoringInfo.mSession - 3 :  // Qualification
                shared.scoring.mScoringInfo.mSession == 5 ? shared.scoring.mScoringInfo.mSession - 5 :  // Warmup (Practice)
                shared.scoring.mScoringInfo.mSession >= 6 && shared.scoring.mScoringInfo.mSession <= 6 ? shared.scoring.mScoringInfo.mSession - 6 : 0;  // Race

            csd.SessionType = this.MapToSessionType(shared);
            csd.SessionPhase = this.MapToSessionPhase((GTR2GamePhase)shared.scoring.mScoringInfo.mGamePhase, csd.SessionType, ref playerScoring);

            float defaultSessionTotalRunTime = 3630.0f;
            if (csd.SessionType == SessionType.Race
                && cgs.inCar)
            {
                // mTimedRaceTotalSeconds is only meaningful:
                // * In Race.
                // * During the rolling start.
                //
                // After rolling start goes Green we need to use endET, because it is re-calculated
                // by the game to include rolling lap time.
                if (shared.extended.mTimedRaceTotalSeconds < 108000.0f)
                {
                    csd.SessionNumberOfLaps = 0;

                    if (csd.SessionPhase == SessionPhase.Gridwalk
                        || csd.SessionPhase == SessionPhase.Countdown
                        || csd.SessionPhase == SessionPhase.Formation)
                        csd.SessionTotalRunTime = shared.extended.mTimedRaceTotalSeconds;
                    else
                        csd.SessionTotalRunTime = shared.scoring.mScoringInfo.mEndET;
                }
                else
                {
                    csd.SessionNumberOfLaps = shared.scoring.mScoringInfo.mMaxLaps;
                    csd.SessionTotalRunTime = 0.0f;
                }
            }
            else
            {
                csd.SessionNumberOfLaps = shared.scoring.mScoringInfo.mMaxLaps > 0 && shared.scoring.mScoringInfo.mMaxLaps < 999 ? shared.scoring.mScoringInfo.mMaxLaps : 0;

                // default to 60:30 if both session time and number of laps undefined (test day)
                csd.SessionTotalRunTime
                    = (float)shared.scoring.mScoringInfo.mEndET != float.MaxValue
                        ? (float)shared.scoring.mScoringInfo.mEndET
                        : csd.SessionNumberOfLaps > 0 ? 0.0f : defaultSessionTotalRunTime;
            }

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

                this.lastGameSession = shared.scoring.mScoringInfo.mSession;

                // Initialize variables that persist for the duration of a session.
                var cci = this.GetCachedCarInfo(ref playerScoring, ref shared.extended);
                Debug.Assert(!cci.isGhost);

                cgs.carClass = cci.carClass;
                CarData.CLASS_ID = cgs.carClass.getClassIdentifier();
                this.brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(cgs.carClass);
                csd.DriverRawName = cci.driverNameRawSanitized;
                csd.TrackDefinition = TrackData.getTrackDefinition(GTR2GameStateMapper.GetStringFromBytes(shared.scoring.mScoringInfo.mTrackName), (float)shared.scoring.mScoringInfo.mLapDist);

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
                csd.TrackDefinition.isOval = tdc.isOval;
                csd.TrackDefinition.setGapPoints();

                GlobalBehaviourSettings.UpdateFromTrackDefinition(csd.TrackDefinition);

                cgs.PitData.PitBoxPositionEstimate = playerExtendedScoring.mPitLapDist;
                Console.WriteLine("Pit box position = " + (cgs.PitData.PitBoxPositionEstimate < 0.0f ? "Unknown" : cgs.PitData.PitBoxPositionEstimate.ToString("0.000")));
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
                csd.GameTimeAtLastPositionFrontChange = psd.GameTimeAtLastPositionFrontChange;
                csd.GameTimeAtLastPositionBehindChange = psd.GameTimeAtLastPositionBehindChange;
                csd.OpponentKeyInFront = psd.OpponentKeyInFront;
                csd.OpponentKeyBehind = psd.OpponentKeyBehind;
            }

            csd.SessionStartTime = csd.IsNewSession ? cgs.Now : psd.SessionStartTime;
            csd.SessionHasFixedTime = csd.SessionTotalRunTime > 0.0f;
            csd.SessionRunningTime = (float)shared.scoring.mScoringInfo.mCurrentET;

            if (csd.SessionType == SessionType.Race
                && csd.SessionHasFixedTime)
            {
                if (csd.SessionPhase == SessionPhase.Formation
                    || csd.SessionPhase == SessionPhase.Countdown
                    || csd.SessionPhase == SessionPhase.Gridwalk)
                {
                    // Ignore running time for the above cases.
                    csd.SessionTimeRemaining = csd.SessionTotalRunTime;
                }
                else
                    csd.SessionTimeRemaining = csd.SessionHasFixedTime ? csd.SessionTotalRunTime - csd.SessionRunningTime : 0.0f;
            }
            else // Non Race case.
                csd.SessionTimeRemaining = csd.SessionHasFixedTime ? csd.SessionTotalRunTime - csd.SessionRunningTime : 0.0f;

            // hack for test day sessions running longer than allotted time
            csd.SessionTimeRemaining = csd.SessionTimeRemaining < 0.0f && shared.scoring.mScoringInfo.mSession == 0 ? defaultSessionTotalRunTime : csd.SessionTimeRemaining;

            csd.NumCarsOverall = shared.scoring.mScoringInfo.mNumVehicles;
            csd.NumCarsOverallAtStartOfSession = csd.IsNewSession ? csd.NumCarsOverall : psd.NumCarsOverallAtStartOfSession;
            csd.OverallPosition = this.GetPreprocessedPlace(ref playerScoring);

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
            csd.IsDisqualified = (GTR2FinishStatus)playerScoring.mFinishStatus == GTR2Constants.GTR2FinishStatus.Dq;
            csd.IsDNF = (GTR2FinishStatus)playerScoring.mFinishStatus == GTR2Constants.GTR2FinishStatus.Dnf;

            // NOTE: Telemetry contains mLapNumber, which might be ahead of Scoring due to higher refresh rate.  However,
            // since we use Scoring fields for timing calculations, stick to Scoring here as well.
            csd.CompletedLaps = playerScoring.mTotalLaps;
            csd.LapCount = csd.CompletedLaps + 1;

            ////////////////////////////////////
            // motion data
            cgs.PositionAndMotionData.CarSpeed = (float)GTR2GameStateMapper.getVehicleSpeed(ref playerTelemetry);
            cgs.PositionAndMotionData.DistanceRoundTrack = (float)GetEstimatedLapDist(shared, ref playerScoring, ref playerTelemetry);
            cgs.PositionAndMotionData.WorldPosition = new float[] { (float)playerTelemetry.mPos.x, (float)playerTelemetry.mPos.y, (float)playerTelemetry.mPos.z };
            cgs.PositionAndMotionData.Orientation = GTR2GameStateMapper.GetRotation(ref playerTelemetry.mOriX, ref playerTelemetry.mOriY, ref playerTelemetry.mOriZ);

            // During Gridwalk/Formation and Finished phases, distance close to S/F line is negative.  Fix it up.
            if (cgs.PositionAndMotionData.DistanceRoundTrack < 0.0f)
                cgs.PositionAndMotionData.DistanceRoundTrack += (float)shared.scoring.mScoringInfo.mLapDist;

            // Initialize DeltaTime.
            if (csd.IsNewSession)
                csd.DeltaTime = new DeltaTime(csd.TrackDefinition.trackLength, cgs.PositionAndMotionData.DistanceRoundTrack, cgs.PositionAndMotionData.CarSpeed, cgs.Now);

            // Is online session?
            for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                if ((GTR2Control)shared.scoring.mVehicles[i].mControl == GTR2Constants.GTR2Control.Remote)
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
                        if (minutesBetweenStops > GTR2GameStateMapper.minMinutesBetweenPredictedStops)
                            pitWindowEndLapOrTime = minutesBetweenStops * (playerScoring.mNumPitstops + 1) + 1;
                    }
                    else
                    {
                        var lapsBetweenStops = (int)(csd.SessionNumberOfLaps / (playerTelemetry.mScheduledStops + 1));
                        if (lapsBetweenStops > GTR2GameStateMapper.minLapsBetweenPredictedStops)
                            pitWindowEndLapOrTime = lapsBetweenStops * (playerScoring.mNumPitstops + 1) + 1;
                    }

                    // Force the MandatoryPit event to be re-initialsed if the window end has been recalculated.
                    cgs.PitData.ResetEvents = pgs != null && pitWindowEndLapOrTime > pgs.PitData.PitWindowEnd;
                }

                cgs.PitData.PitWindowEnd = pitWindowEndLapOrTime;
            }

            // mInGarageStall also means retired or before race start, but for now use it here.
            cgs.PitData.InPitlane = playerScoring.mInPits == 1/* || playerScoring.mInGarageStall == 1*/;

            csd.DeltaTime.SetNextDeltaPoint(cgs.PositionAndMotionData.DistanceRoundTrack, csd.CompletedLaps, cgs.PositionAndMotionData.CarSpeed, cgs.Now, !cgs.PitData.InPitlane);

            if (csd.SessionType == SessionType.Race && csd.SessionRunningTime > 10
                && cgs.PitData.InPitlane && pgs != null && !pgs.PitData.InPitlane)
            {
                cgs.PitData.NumPitStops++;
            }

            cgs.PitData.IsAtPitExit = pgs != null && pgs.PitData.InPitlane && !cgs.PitData.InPitlane;
            cgs.PitData.OnOutLap = cgs.PitData.IsAtPitExit/* || playerScoring.mInGarageStall == 1*/;

            if (shared.extended.mInRealtimeFC == 0  // Mark pit limiter as unavailable if in Monitor (not real time).
                || shared.scoring.mScoringInfo.mInRealtime == 0
                || playerExtendedScoring.mSpeedLimiterAvailable == 0)
                cgs.PitData.limiterStatus = PitData.LimiterStatus.NOT_AVAILABLE;
            else
                cgs.PitData.limiterStatus = playerExtendedScoring.mSpeedLimiter > 0 ? PitData.LimiterStatus.ACTIVE : PitData.LimiterStatus.INACTIVE;

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
                ? PitWindow.StopInProgress : this.mapToPitWindow((GTR2YellowFlagState)shared.scoring.mScoringInfo.mYellowFlagState);

            if (pgs != null)
                cgs.PitData.MandatoryPitStopCompleted = pgs.PitData.MandatoryPitStopCompleted || cgs.PitData.IsMakingMandatoryPitStop;

            cgs.PitData.HasRequestedPitStop = (GTR2PitState)playerExtendedScoring.mPitState == GTR2Constants.GTR2PitState.Request;

            // Is this new pit request?
            if (pgs != null && !pgs.PitData.HasRequestedPitStop && cgs.PitData.HasRequestedPitStop)
                this.timePitStopRequested = cgs.Now;

            // If DMA is not enabled, check if it's time to mark pit crew as ready.
            if (pgs != null
                && pgs.PitData.HasRequestedPitStop
                && cgs.PitData.HasRequestedPitStop
                && (cgs.Now - this.timePitStopRequested).TotalSeconds > cgs.carClass.pitCrewPreparationTime)
                cgs.PitData.IsPitCrewReady = true;

            cgs.PitData.PitSpeedLimit = shared.extended.mCurrentPitSpeedLimit;

            // This sometimes fires under Countdown, so limit to phases when message might make sense.
            if ((csd.SessionPhase == SessionPhase.Green
                    || csd.SessionPhase == SessionPhase.FullCourseYellow
                    || csd.SessionPhase == SessionPhase.Formation)
                && shared.extended.mInRealtimeFC == 1 && shared.scoring.mScoringInfo.mInRealtime == 1  // Limit this to Realtime only.
                && csd.SessionType == SessionType.Race)  // Also, limit to race only, this helps with back and forth between returing to pits via exit to monitor.
            {                                            // There's also no real critical rush in quali or practice to stress about.
                cgs.PitData.IsPitCrewDone = (GTR2PitState)playerExtendedScoring.mPitState == GTR2Constants.GTR2PitState.Exiting;

                //cgs.PitData.IsPitCrewDone = (GTR2PitState)shared.extended.mPlayerPitState == GTR2Constants.GTR2PitState.Exiting;
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

                        var pitLaneStartDist = shared.extended.mPitApproachLapDist;
                        if (cgs.SessionData.SessionType != SessionType.Race)
                        {
                            // Rules aren't updated in non-race sessions, so estimate pit lane start based on
                            // pit stall position.
                            pitLaneStartDist = playerExtendedScoring.mPitLapDist - 400.0f;
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
                                playerExtendedScoring.mPitState,
                                this.isApproachingPitEntry,
                                csd.CompletedLaps + 1,
                                shared.extended.mPitApproachLapDist));
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

            this.ProcessPlayerTimingData(ref shared.scoring, cgs, pgs, ref playerScoring, ref playerExtendedScoring);

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

            csd.LeaderHasFinishedRace = leaderScoring.mFinishStatus == (int)GTR2Constants.GTR2FinishStatus.Finished;
            csd.LeaderSectorNumber = leaderScoring.mSector == 0 ? 3 : leaderScoring.mSector;
            csd.TimeDeltaFront = (float)Math.Abs(playerScoring.mTimeBehindNext);

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

            // --------------------------------
            // damage
            // not 100% certain on this mapping but it should be reasonably close
            // Investigate if impact is ever not 0 and dents ever not 0.
            if (shared.extended.mInvulnerable == 0)
            {
                var mf = (GTR2MechanicalFailure)playerExtendedScoring.mMechanicalFailureID;
                if (mf == GTR2MechanicalFailure.Gearbox)
                    cgs.CarDamageData.OverallTransmissionDamage = DamageLevel.DESTROYED;
                else if (mf == GTR2MechanicalFailure.Engine)
                    cgs.CarDamageData.OverallEngineDamage = DamageLevel.DESTROYED;
                else
                {
                    bool anyWheelDetached = false;
                    foreach (var wheel in playerTelemetry.mWheel)
                        anyWheelDetached |= wheel.mDetached == 1;

                    if (playerTelemetry.mDetached == 1
                        && anyWheelDetached)  // Wheel is not really aero damage, but it is bad situation.
                    {
                        // Things are sad if we have both part and wheel detached.
                        cgs.CarDamageData.OverallAeroDamage = DamageLevel.DESTROYED;
                    }
                    else if (playerTelemetry.mDetached == 1)  // If there are parts detached, consider damage major, and pit stop is necessary.
                        cgs.CarDamageData.OverallAeroDamage = DamageLevel.MAJOR;

                    if (shared.extended.mTotalGearboxDamage == 1.0)
                        cgs.CarDamageData.OverallTransmissionDamage = DamageLevel.DESTROYED;
                    else if (shared.extended.mTotalGearboxDamage > 0.499)
                        cgs.CarDamageData.OverallTransmissionDamage = DamageLevel.MAJOR;
                    else if (shared.extended.mTotalGearboxDamage > 0.1)
                        cgs.CarDamageData.OverallTransmissionDamage = DamageLevel.MINOR; // Something to remember: if, say, aero is at MAJOR, this won't get called out.
                    else if (shared.extended.mTotalGearboxDamage > 0.02)
                        cgs.CarDamageData.OverallTransmissionDamage = DamageLevel.TRIVIAL;
                    else
                        cgs.CarDamageData.OverallTransmissionDamage = DamageLevel.NONE;
                }
            }

            // --------------------------------
            // control data
            cgs.ControlData.ControlType = MapToControlType((GTR2Control)playerScoring.mControl);

            // --------------------------------
            // Tyre data
            // GTR2 reports in Kelvin
            cgs.TyreData.TyreWearActive = shared.extended.mTireMult > 0;
            cgs.TyreData.FlatSpotEmulationActive = shared.extended.mFlatSpotEmulationEnabled != 0;
            cgs.TyreData.DirtPickupEmulationActive = shared.extended.mDirtPickupEmulationEnabled != 0;

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
                    tt = this.MapToTyreType(ref shared.extended, ref playerExtendedScoring);
            }

            // First time intialize.  Might stay like that until we get telemetry.
            if (tt == TyreType.Uninitialized)
                cgs.TyreData.TyreTypeName = this.MapToTyreType(ref shared.extended, ref playerExtendedScoring).ToString();

            var wheelFrontLeft = playerTelemetry.mWheel[(int)GTR2Constants.GTR2WheelIndex.FrontLeft];
            cgs.TyreData.FrontLeftTyreType = tt;
            cgs.TyreData.LeftFrontAttached = wheelFrontLeft.mDetached == 0;
            cgs.TyreData.FrontLeft_LeftTemp = (float)wheelFrontLeft.mTemperature[0] - 273.15f;
            cgs.TyreData.FrontLeft_CenterTemp = (float)wheelFrontLeft.mTemperature[1] - 273.15f;
            cgs.TyreData.FrontLeft_RightTemp = (float)wheelFrontLeft.mTemperature[2] - 273.15f;
            cgs.TyreData.FrontLeftFlatSpotSeverity = (float)shared.extended.mWheels[0].mFlatSpotSeverity;
            cgs.TyreData.FrontLeftDirtPickupSeverity = (float)shared.extended.mWheels[0].mDirtPickupSeverity;

            var frontLeftTemp = (cgs.TyreData.FrontLeft_CenterTemp + cgs.TyreData.FrontLeft_LeftTemp + cgs.TyreData.FrontLeft_RightTemp) / 3.0f;
            cgs.TyreData.FrontLeftPressure = wheelFrontLeft.mFlat == 0 ? (float)wheelFrontLeft.mPressure : 0.0f;
            const double MAX_WEAR = 0.6875;
            cgs.TyreData.FrontLeftPercentWear = (float)((1.0 - MAX_WEAR - Math.Max(wheelFrontLeft.mWear - MAX_WEAR, 0.0)) * (100.0 / (1.0 - MAX_WEAR)));

            if (csd.IsNewLap || cgs.TyreData.PeakFrontLeftTemperatureForLap == 0)
                cgs.TyreData.PeakFrontLeftTemperatureForLap = frontLeftTemp;
            else if (pgs == null || frontLeftTemp > pgs.TyreData.PeakFrontLeftTemperatureForLap)
                cgs.TyreData.PeakFrontLeftTemperatureForLap = frontLeftTemp;

            var wheelFrontRight = playerTelemetry.mWheel[(int)GTR2Constants.GTR2WheelIndex.FrontRight];
            cgs.TyreData.FrontRightTyreType = tt;
            cgs.TyreData.RightFrontAttached = wheelFrontRight.mDetached == 0;
            cgs.TyreData.FrontRight_LeftTemp = (float)wheelFrontRight.mTemperature[0] - 273.15f;
            cgs.TyreData.FrontRight_CenterTemp = (float)wheelFrontRight.mTemperature[1] - 273.15f;
            cgs.TyreData.FrontRight_RightTemp = (float)wheelFrontRight.mTemperature[2] - 273.15f;
            cgs.TyreData.FrontRightFlatSpotSeverity = (float)shared.extended.mWheels[1].mFlatSpotSeverity;
            cgs.TyreData.FrontRightDirtPickupSeverity = (float)shared.extended.mWheels[1].mDirtPickupSeverity;

            var frontRightTemp = (cgs.TyreData.FrontRight_CenterTemp + cgs.TyreData.FrontRight_LeftTemp + cgs.TyreData.FrontRight_RightTemp) / 3.0f;
            cgs.TyreData.FrontRightPressure = wheelFrontRight.mFlat == 0 ? (float)wheelFrontRight.mPressure : 0.0f;
            cgs.TyreData.FrontRightPercentWear = (float)((1.0 - MAX_WEAR - Math.Max(wheelFrontRight.mWear - MAX_WEAR, 0.0)) * (100.0 / (1.0 - MAX_WEAR)));

            if (csd.IsNewLap || cgs.TyreData.PeakFrontRightTemperatureForLap == 0)
                cgs.TyreData.PeakFrontRightTemperatureForLap = frontRightTemp;
            else if (pgs == null || frontRightTemp > pgs.TyreData.PeakFrontRightTemperatureForLap)
                cgs.TyreData.PeakFrontRightTemperatureForLap = frontRightTemp;

            var wheelRearLeft = playerTelemetry.mWheel[(int)GTR2Constants.GTR2WheelIndex.RearLeft];
            cgs.TyreData.RearLeftTyreType = tt;
            cgs.TyreData.LeftRearAttached = wheelRearLeft.mDetached == 0;
            cgs.TyreData.RearLeft_LeftTemp = (float)wheelRearLeft.mTemperature[0] - 273.15f;
            cgs.TyreData.RearLeft_CenterTemp = (float)wheelRearLeft.mTemperature[1] - 273.15f;
            cgs.TyreData.RearLeft_RightTemp = (float)wheelRearLeft.mTemperature[2] - 273.15f;
            cgs.TyreData.RearLeftFlatSpotSeverity = (float)shared.extended.mWheels[2].mFlatSpotSeverity;
            cgs.TyreData.RearLeftDirtPickupSeverity = (float)shared.extended.mWheels[2].mDirtPickupSeverity;

            var rearLeftTemp = (cgs.TyreData.RearLeft_CenterTemp + cgs.TyreData.RearLeft_LeftTemp + cgs.TyreData.RearLeft_RightTemp) / 3.0f;
            cgs.TyreData.RearLeftPressure = wheelRearLeft.mFlat == 0 ? (float)wheelRearLeft.mPressure : 0.0f;
            cgs.TyreData.RearLeftPercentWear = (float)((1.0 - MAX_WEAR - Math.Max(wheelRearLeft.mWear - MAX_WEAR, 0.0)) * (100.0 / (1.0 - MAX_WEAR)));

            if (csd.IsNewLap || cgs.TyreData.PeakRearLeftTemperatureForLap == 0)
                cgs.TyreData.PeakRearLeftTemperatureForLap = rearLeftTemp;
            else if (pgs == null || rearLeftTemp > pgs.TyreData.PeakRearLeftTemperatureForLap)
                cgs.TyreData.PeakRearLeftTemperatureForLap = rearLeftTemp;

            var wheelRearRight = playerTelemetry.mWheel[(int)GTR2Constants.GTR2WheelIndex.RearRight];
            cgs.TyreData.RearRightTyreType = tt;
            cgs.TyreData.RightRearAttached = wheelRearRight.mDetached == 0;
            cgs.TyreData.RearRight_LeftTemp = (float)wheelRearRight.mTemperature[0] - 273.15f;
            cgs.TyreData.RearRight_CenterTemp = (float)wheelRearRight.mTemperature[1] - 273.15f;
            cgs.TyreData.RearRight_RightTemp = (float)wheelRearRight.mTemperature[2] - 273.15f;
            cgs.TyreData.RearRightFlatSpotSeverity = (float)shared.extended.mWheels[3].mFlatSpotSeverity;
            cgs.TyreData.RearRightDirtPickupSeverity = (float)shared.extended.mWheels[3].mDirtPickupSeverity;

            var rearRightTemp = (cgs.TyreData.RearRight_CenterTemp + cgs.TyreData.RearRight_LeftTemp + cgs.TyreData.RearRight_RightTemp) / 3.0f;
            cgs.TyreData.RearRightPressure = wheelRearRight.mFlat == 0 ? (float)wheelRearRight.mPressure : 0.0f;
            cgs.TyreData.RearRightPercentWear = (float)((1.0 - MAX_WEAR - Math.Max(wheelRearRight.mWear - MAX_WEAR, 0.0)) * (100.0 / (1.0 - MAX_WEAR)));

            if (csd.IsNewLap || cgs.TyreData.PeakRearRightTemperatureForLap == 0)
                cgs.TyreData.PeakRearRightTemperatureForLap = rearRightTemp;
            else if (pgs == null || rearRightTemp > pgs.TyreData.PeakRearRightTemperatureForLap)
                cgs.TyreData.PeakRearRightTemperatureForLap = rearRightTemp;

            cgs.TyreData.TyreConditionStatus = CornerData.getCornerData(this.tyreWearThresholds, cgs.TyreData.FrontLeftPercentWear,
                cgs.TyreData.FrontRightPercentWear, cgs.TyreData.RearLeftPercentWear, cgs.TyreData.RearRightPercentWear);

            cgs.TyreData.TyreFlatSpotStatus = CornerData.getCornerData(this.tyreFlatSpotThresholds, cgs.TyreData.FrontLeftFlatSpotSeverity,
                cgs.TyreData.FrontRightFlatSpotSeverity, cgs.TyreData.RearLeftFlatSpotSeverity, cgs.TyreData.RearRightFlatSpotSeverity);

            cgs.TyreData.TyreDirtPickupStatus = CornerData.getCornerData(this.tyreDirtPickupThresholds, cgs.TyreData.FrontLeftDirtPickupSeverity,
                cgs.TyreData.FrontRightDirtPickupSeverity, cgs.TyreData.RearLeftDirtPickupSeverity, cgs.TyreData.RearRightDirtPickupSeverity);

            var tyreTempThresholds = CarData.getTyreTempThresholds(cgs.carClass);
            cgs.TyreData.TyreTempStatus = CornerData.getCornerData(tyreTempThresholds,
                cgs.TyreData.PeakFrontLeftTemperatureForLap, cgs.TyreData.PeakFrontRightTemperatureForLap,
                cgs.TyreData.PeakRearLeftTemperatureForLap, cgs.TyreData.PeakRearRightTemperatureForLap);

            // some simple locking / spinning checks
            if (cgs.PositionAndMotionData.CarSpeed > 7.0f)
            {
                if (shared.extended.mFlatSpotEmulationEnabled != 0) // For now, flat spot emulation has to be on for this to work.
                {
                    //float minRotatingSpeedOld = (float)Math.PI * cgs.PositionAndMotionData.CarSpeed / cgs.carClass.maxTyreCircumference;
                    //float maxRotatingSpeedOld = 3 * (float)Math.PI * cgs.PositionAndMotionData.CarSpeed / cgs.carClass.minTyreCircumference;

                    // w = v/r
                    // https://www.lucidar.me/en/unit-converter/rad-per-second-to-meters-per-second/
                    float MAX_RADIUS = 3.6f;  // When making a left turn, right wheel spins faster, as if it was smaller.  Because of that, scale real radius up for lock detection.
                    var minFrontRotatingSpeedRadSec = cgs.PositionAndMotionData.CarSpeed / (shared.extended.mWheels[0].mRadiusMeters * MAX_RADIUS);
                    cgs.TyreData.LeftFrontIsLocked = Math.Abs(wheelFrontLeft.mRotation) < minFrontRotatingSpeedRadSec;
                    cgs.TyreData.RightFrontIsLocked = Math.Abs(wheelFrontRight.mRotation) < minFrontRotatingSpeedRadSec;

                    var minRearRotatingSpeedRadSec = cgs.PositionAndMotionData.CarSpeed / (shared.extended.mWheels[1].mRadiusMeters * MAX_RADIUS);
                    cgs.TyreData.LeftRearIsLocked = Math.Abs(wheelRearLeft.mRotation) < minRearRotatingSpeedRadSec;
                    cgs.TyreData.RightRearIsLocked = Math.Abs(wheelRearRight.mRotation) < minRearRotatingSpeedRadSec;

                    float MIN_RADIUS = 0.5f;  // When making a left turn, right wheel spins faster, as if it was smaller.  Because of that, scale real radius down for spin detection.
                    var maxFrontRotatingSpeedRadSec = cgs.PositionAndMotionData.CarSpeed / (shared.extended.mWheels[2].mRadiusMeters * MIN_RADIUS);
                    cgs.TyreData.LeftFrontIsSpinning = Math.Abs(wheelFrontLeft.mRotation) > maxFrontRotatingSpeedRadSec;
                    cgs.TyreData.RightFrontIsSpinning = Math.Abs(wheelFrontRight.mRotation) > maxFrontRotatingSpeedRadSec;

                    var maxRearRotatingSpeedRadSec = cgs.PositionAndMotionData.CarSpeed / (shared.extended.mWheels[3].mRadiusMeters * MIN_RADIUS);
                    cgs.TyreData.LeftRearIsSpinning = Math.Abs(wheelRearLeft.mRotation) > maxRearRotatingSpeedRadSec;
                    cgs.TyreData.RightRearIsSpinning = Math.Abs(wheelRearRight.mRotation) > maxRearRotatingSpeedRadSec;

                    /*#if DEBUG
                                        GTR2GameStateMapper.writeSpinningLockingDebugMsg(cgs, wheelFrontLeft.mRotation, wheelFrontRight.mRotation,
                                            wheelRearLeft.mRotation, wheelRearRight.mRotation, minRotatingSpeedOld, maxRotatingSpeedOld, minFrontRotatingSpeedRadSec,
                                            minRearRotatingSpeedRadSec, maxFrontRotatingSpeedRadSec, maxRearRotatingSpeedRadSec);
                    #endif*/
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

            var suspDmgThresh = playerExtendedScoring.mMechanicalFailureID == (int)GTR2MechanicalFailure.Suspension ? 0.5f : 0.0f;

            // use detached wheel status for destroyed suspension damage
            cgs.CarDamageData.SuspensionDamageStatus = CornerData.getCornerData(this.suspensionDamageThresholds,
                !cgs.TyreData.LeftFrontAttached ? 1.0f : suspDmgThresh,
                !cgs.TyreData.RightFrontAttached ? 1.0f : suspDmgThresh,
                !cgs.TyreData.LeftRearAttached ? 1.0f : suspDmgThresh,
                !cgs.TyreData.RightRearAttached ? 1.0f : suspDmgThresh);

            // --------------------------------
            // brake data
            // GTR2 reports in Kelvin
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
                cgs.Conditions.addSample(cgs.Now, csd.CompletedLaps, csd.SectorNumber,
                    (float)shared.scoring.mScoringInfo.mAmbientTemp, (float)shared.scoring.mScoringInfo.mTrackTemp, (float)shared.scoring.mScoringInfo.mRaining,
                    (float)Math.Sqrt((double)(shared.scoring.mScoringInfo.mWind.x * shared.scoring.mScoringInfo.mWind.x + shared.scoring.mScoringInfo.mWind.y * shared.scoring.mScoringInfo.mWind.y + shared.scoring.mScoringInfo.mWind.z * shared.scoring.mScoringInfo.mWind.z)),
                    0, 0, 0, csd.IsNewLap, ConditionsMonitor.TrackStatus.UNKNOWN);
            }

            // --------------------------------
            // DRS data
            if (shared.extended.mActiveDRSRuleSet == (char)GTR2DRSRuleSet.DTM18 || shared.extended.mActiveDRSRuleSet == (char)GTR2DRSRuleSet.DTM13)
            {
                cgs.OvertakingAids.DrsEnabled = shared.extended.mCurrDRSSystemState == (char)GTR2DRSSystemState.Enabled;
                var ledState = (GTR2DTM18DRSState)shared.extended.mCurrDRSLEDState;

                cgs.OvertakingAids.DrsAvailable = ledState == GTR2DTM18DRSState.Available1
                    || ledState == GTR2DTM18DRSState.Available2
                    || ledState == GTR2DTM18DRSState.Available3;

                cgs.OvertakingAids.DrsEngaged = ledState == GTR2DTM18DRSState.Active1
                    || ledState == GTR2DTM18DRSState.Active2
                    || ledState == GTR2DTM18DRSState.Active3;

                cgs.OvertakingAids.DrsRange = shared.extended.mActiveDRSActivationThresholdSeconds;

                if (cgs.SessionData.SessionType == SessionType.Race && shared.extended.mActiveDRSRuleSet == (char)GTR2DRSRuleSet.DTM18)
                    cgs.OvertakingAids.DrsActivationsRemaining = shared.extended.mActiveDRSDTM18ActivationsPerRace - shared.extended.mCurrActivationsInRace;
                else
                    cgs.OvertakingAids.DrsActivationsRemaining = -1;
            }
            else if (shared.extended.mActiveDRSRuleSet == (char)GTR2DRSRuleSet.F1_2011 || shared.extended.mActiveDRSRuleSet == (char)GTR2DRSRuleSet.F1_2013)
            {
                cgs.OvertakingAids.DrsEnabled = shared.extended.mCurrDRSSystemState == (char)GTR2DRSSystemState.Enabled;

                var ledState = (GTR2F1DRSState)shared.extended.mCurrDRSLEDState;
                cgs.OvertakingAids.DrsAvailable = ledState == GTR2F1DRSState.Available;
                cgs.OvertakingAids.DrsDetected = ledState == GTR2F1DRSState.Eligible;
                cgs.OvertakingAids.DrsEngaged = ledState == GTR2F1DRSState.Active;
                cgs.OvertakingAids.DrsRange = shared.extended.mActiveDRSActivationThresholdSeconds;
            }

            // --------------------------------
            // KERS data
            if (shared.extended.mActiveKERSRuleSet == (char)GTR2KERSRuleSet.F1_2009)
            {
                cgs.OvertakingAids.KersEnabled = true;
                cgs.OvertakingAids.KersAvailable = shared.extended.mCurrKERSLEDState == (char)GTR2F109KERSState.Available 
                    && csd.SessionPhase == SessionPhase.Green; // Don't nag if not actually racing.
                cgs.OvertakingAids.KersEngaged = shared.extended.mCurrKERSLEDState == (char)GTR2F109KERSState.Active;
            }

            // --------------------------------
            // opponent data
            this.opponentKeysProcessed.Clear();

            // first check for duplicates:
            var driverNameCounts = new Dictionary<string, int>();
            var duplicatesCreated = new Dictionary<string, int>();
            for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicleScoring = shared.scoring.mVehicles[i];

                var cci = this.GetCachedCarInfo(ref vehicleScoring, ref shared.extended);
                if (cci.isGhost)
                    continue;  // Skip trainer.

                var driverName = cci.driverNameRawSanitized;

                var numNames = -1;
                if (driverNameCounts.TryGetValue(driverName, out numNames))
                    driverNameCounts[driverName] = ++numNames;
                else
                    driverNameCounts.Add(driverName, 1);
            }

            OpponentData leaderOppoentData = null;
            for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicleScoring = shared.scoring.mVehicles[i];
                var vehicleExtendedScoring = shared.extended.mExtendedVehicleScoring[vehicleScoring.mID % GTR2Constants.MAX_MAPPED_IDS];

                if (vehicleScoring.mIsPlayer == 1)
                {
                    csd.OverallSessionBestLapTime = csd.PlayerLapTimeSessionBest > 0.0f ?
                        csd.PlayerLapTimeSessionBest : -1.0f;

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

                            if (this.GetPreprocessedPlace(ref vehicleScoring) == 1
                                && csd.PlayerLapTimeSessionBest > 0.0f
                                && csd.SessionTimeRemaining < csd.PlayerLapTimeSessionBest * 0.90f)
                                csd.IsLastLap = csd.OverallLeaderIsOnLastLap = true;
                        }
                    }

                    continue;
                }

                var ct = this.MapToControlType((GTR2Control)vehicleScoring.mControl);
                if (ct == ControlType.Player || ct == ControlType.Replay || ct == ControlType.Unavailable)
                    continue;

                var vehicleCachedInfo = this.GetCachedCarInfo(ref vehicleScoring, ref shared.extended);
                if (vehicleCachedInfo.isGhost)
                    continue;  // Skip trainer.

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
                        opponentKey = this.GetOpponentKeyForVehicleInfo(ref vehicleScoring, pgs, csd.SessionRunningTime, driverName, duplicatesCount, ref shared.extended);

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

                var ofs = (GTR2FinishStatus)vehicleScoring.mFinishStatus;
                if (ofs == GTR2Constants.GTR2FinishStatus.Dnf)
                {
                    // Note driver DNF and don't tack him anymore.
                    if (!cgs.retriedDriverNames.ContainsKey(driverName))
                    {
                        Console.WriteLine("Opponent " + driverName + " has retired");
                        cgs.retriedDriverNames.Add(driverName, null);
                        vehicleCachedInfo.opponentData = null;
                    }
                    continue;
                }
                else if (ofs == GTR2Constants.GTR2FinishStatus.Dq)
                {
                    // Note driver DQ and don't tack him anymore.
                    if (!cgs.disqualifiedDriverNames.ContainsKey(driverName))
                    {
                        Console.WriteLine("Opponent " + driverName + " has been disqualified");
                        cgs.disqualifiedDriverNames.Add(driverName, null);
                        vehicleCachedInfo.opponentData = null;
                    }
                    continue;
                }

                opponentPrevious = pgs == null || opponentKey == null || !pgs.OpponentData.TryGetValue(opponentKey, out opponentPrevious) ? null : opponentPrevious;
                var opponent = new OpponentData();

                if (leaderScoring.mID == vehicleScoring.mID)
                {
                    Debug.Assert(leaderOppoentData == null);
                    leaderOppoentData = opponent;
                }

                vehicleCachedInfo.opponentData = opponent;

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
                        tt = this.MapToTyreType(ref shared.extended, ref vehicleExtendedScoring);
                }

                // First time intialize.  Might stay like that until we get telemetry.
                if (tt == TyreType.Uninitialized)
                    tt = this.MapToTyreType(ref shared.extended, ref vehicleExtendedScoring);

                opponent.CurrentTyres = tt;
                opponent.DriverRawName = vehicleCachedInfo.driverNameRawSanitized;
                opponent.DriverNameSet = opponent.DriverRawName.Length > 0;
                opponent.OverallPosition = this.GetPreprocessedPlace(ref vehicleScoring);
                opponent.CarNumber = vehicleCachedInfo.carNumberStr;

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
                opponent.IsNewLap = csd.IsNewSession || (isNewSector && opponent.CurrentSectorNumber == 1/* && opponent.CompletedLaps > 0*/);  // Why last condition opponent but not player?

                opponent.Speed = (float)GTR2GameStateMapper.getVehicleSpeed(ref vehicleScoring);
                opponent.WorldPosition = new float[] { (float)vehicleScoring.mPos.x, (float)vehicleScoring.mPos.z };
                opponent.DistanceRoundTrack = (float)vehicleScoring.mLapDist;

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

                var lastSectorTime = this.GetLastSectorTime(ref vehicleScoring, opponent.CurrentSectorNumber);

                bool lapValid = true;
                if (vehicleExtendedScoring.mCountLapFlag != 2)
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
                    if (vehicleExtendedScoring.mPitState == (byte)GTR2Constants.GTR2PitState.Stopped)
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
                csd.trackLandmarksTiming = previousGameState.SessionData.trackLandmarksTiming;

                var stoppedInLandmark = csd.trackLandmarksTiming.updateLandmarkTiming(csd.TrackDefinition,
                    csd.SessionRunningTime, previousGameState.PositionAndMotionData.DistanceRoundTrack, cgs.PositionAndMotionData.DistanceRoundTrack,
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
            cgs.FuelData.FuelUseActive = cgs.BatteryData.BatteryUseActive = shared.extended.mFuelMult > 0;
            cgs.FuelData.FuelMultiplier = shared.extended.mFuelMult;
            cgs.FuelData.FuelLeft = cgs.BatteryData.BatteryPercentageLeft = (float)playerTelemetry.mFuel;

            cgs.FuelData.FuelCapacity = playerExtendedScoring.mFuelCapacityLiters;

            // --------------------------------
            // flags data
            cgs.FlagData.useImprovisedIncidentCalling = false;

            // Note that mYellowFlagState goes to 7 (Halt) after FCY is cleared and until player hits s/f.
            // It's possible there's something special going on during 7 (no passing?).
            cgs.FlagData.isFullCourseYellow = csd.SessionPhase == SessionPhase.FullCourseYellow;

            if (cgs.FlagData.isFullCourseYellow)
            {
                cgs.FlagData.currentLapIsFCY = true;
                if (shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)GTR2Constants.GTR2YellowFlagState.Pending)
                    cgs.FlagData.fcyPhase = FullCourseYellowPhase.PENDING;
                else if (shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)GTR2Constants.GTR2YellowFlagState.PitOpen
                    || shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)GTR2Constants.GTR2YellowFlagState.PitClosed
                    || shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)GTR2Constants.GTR2YellowFlagState.PitLeadLap)
                    cgs.FlagData.fcyPhase = FullCourseYellowPhase.IN_PROGRESS;
                else if (shared.scoring.mScoringInfo.mYellowFlagState == (sbyte)GTR2Constants.GTR2YellowFlagState.Resume)
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
                    //if (shared.scoring.mScoringInfo.mSectorFlag[i] == (int)GTR2Constants.GTR2YellowFlagState.Pending)
                    if (shared.scoring.mScoringInfo.mSectorFlag[i] == (int)GTR2Constants.GTR2YellowFlagState.PitClosed)
                    {
                        var sector = i == 0 ? 2 : i - 1;
                        cgs.FlagData.sectorFlags[sector] = FlagEnum.YELLOW;
                    }
                }
            }

            var currFlag = FlagEnum.UNKNOWN;

            if (GlobalBehaviourSettings.useAmericanTerms
                && !cgs.FlagData.isFullCourseYellow)  // Don't announce White flag under FCY.
            {
                // Only works correctly if race is not timed.
                if ((csd.SessionType == SessionType.Race || csd.SessionType == SessionType.Qualify)
                    && csd.SessionPhase == SessionPhase.Green
                    && (playerScoring.mTotalLaps == csd.SessionNumberOfLaps - 1) || csd.LeaderHasFinishedRace)
                {
                    currFlag = FlagEnum.WHITE;
                }
            }

            if (playerExtendedScoring.mBlueFlag != 0)
                currFlag = FlagEnum.BLUE;

            if (csd.IsDisqualified
                && pgs != null
                && !psd.IsDisqualified)
                currFlag = FlagEnum.BLACK;

            csd.Flag = currFlag;

            // --------------------------------
            // Frozen order data
            if (this.enableFrozenOrderMessages
                && pgs != null)
                cgs.FrozenOrderData = this.GetFrozenOrderData(cgs, pgs, pgs.FrozenOrderData, ref playerScoring, ref shared.scoring, ref shared.extended, cgs.PositionAndMotionData.CarSpeed);

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
                    wfls != (int)GTR2Constants.GTR2SurfaceType.Dry && wfls != (int)GTR2Constants.GTR2SurfaceType.Wet && wfls != (int)GTR2Constants.GTR2SurfaceType.Kerb
                    && wfrs != (int)GTR2Constants.GTR2SurfaceType.Dry && wfrs != (int)GTR2Constants.GTR2SurfaceType.Wet && wfrs != (int)GTR2Constants.GTR2SurfaceType.Kerb
                    && wrls != (int)GTR2Constants.GTR2SurfaceType.Dry && wrls != (int)GTR2Constants.GTR2SurfaceType.Wet && wrls != (int)GTR2Constants.GTR2SurfaceType.Kerb
                    && wrrs != (int)GTR2Constants.GTR2SurfaceType.Dry && wrrs != (int)GTR2Constants.GTR2SurfaceType.Wet && wrrs != (int)GTR2Constants.GTR2SurfaceType.Kerb;

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
                Console.WriteLine("=====================================================");
                Console.WriteLine("New session, trigger data:");
                Console.WriteLine("=====================================================");
                Console.WriteLine("SessionType: " + csd.SessionType);
                Console.WriteLine("SessionPhase: " + csd.SessionPhase);
                Console.WriteLine("HasMandatoryPitStop: " + cgs.PitData.HasMandatoryPitStop);
                Console.WriteLine("PitWindowStart: " + cgs.PitData.PitWindowStart);
                Console.WriteLine("PitWindowEnd: " + cgs.PitData.PitWindowEnd);
                Console.WriteLine("NumCarsAtStartOfSession: " + csd.NumCarsOverallAtStartOfSession);
                Console.WriteLine("SessionStartTime: " + csd.SessionStartTime); 
                Console.WriteLine("EventIndex: " + csd.EventIndex);
                Console.WriteLine("Player is using car class: \"" + cgs.carClass.getClassIdentifier() +
                    "\" at position: " + csd.OverallPosition.ToString());
                Console.WriteLine("=====================================================");

                Utilities.TraceEventClass(cgs);
            }
            if (pgs != null && psd.SessionPhase != csd.SessionPhase)
            {
                if (!csd.SessionHasFixedTime)
                    Console.WriteLine($"SessionPhase changed from '{psd.SessionPhase}' to '{csd.SessionPhase}'");
                else
                    Console.WriteLine($"SessionPhase changed from '{psd.SessionPhase}' to '{csd.SessionPhase}'.  SessionTimeRemaining: {TimeSpan.FromSeconds(csd.SessionTimeRemaining).ToString(@"hh\:mm\:ss\:fff")}.");

                if (csd.SessionPhase == SessionPhase.Checkered
                     || csd.SessionPhase == SessionPhase.Finished)
                    Console.WriteLine("Checkered - completed " + csd.CompletedLaps + " laps, session running time = " + csd.SessionRunningTime + "  (" + TimeSpan.FromSeconds(csd.SessionRunningTime).ToString(@"hh\:mm\:ss\:fff") + ")");

                if (csd.SessionType == SessionType.Race
                    && psd.SessionPhase == SessionPhase.Gridwalk)
                    this.PrintSessionDurationDetails(csd);
            }

            if (csd.SessionType != SessionType.Race
                && !this.nonRaceSessionDurationLogged
                && cgs.inCar)
            {
                this.PrintSessionDurationDetails(csd);

                this.nonRaceSessionDurationLogged = true;
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

                //cgs.TelemetryData.FrontDownforce = playerTelemetry.mFrontDownforce;
                //cgs.TelemetryData.RearDownforce = playerTelemetry.mRearDownforce;

                cgs.TelemetryData.FrontLeftData.SuspensionDeflection = wheelFrontLeft.mSuspensionDeflection;
                cgs.TelemetryData.FrontRightData.SuspensionDeflection = wheelFrontRight.mSuspensionDeflection;
                cgs.TelemetryData.RearLeftData.SuspensionDeflection = wheelRearLeft.mSuspensionDeflection;
                cgs.TelemetryData.RearRightData.SuspensionDeflection = wheelRearRight.mSuspensionDeflection;

                cgs.TelemetryData.FrontLeftData.RideHeight = wheelFrontLeft.mRideHeight;
                cgs.TelemetryData.FrontRightData.RideHeight = wheelFrontRight.mRideHeight;
                cgs.TelemetryData.RearLeftData.RideHeight = wheelRearLeft.mRideHeight;
                cgs.TelemetryData.RearRightData.RideHeight = wheelRearRight.mRideHeight;
            }

            // ------------------------
            // Apply rolling start position data workaround.
            this.ApplyRollingStartPosWorkaround(cgs, pgs, ref shared.scoring, ref shared.extended, leaderScoring.mID, leaderOppoentData, csd.IsNewLap, playerScoring.mID);

            return cgs;
        }

        private void PrintSessionDurationDetails(SessionData csd)
        {
            Console.WriteLine("=====================================================");
            Console.WriteLine("Session duration details:");
            Console.WriteLine("=====================================================");
            Console.WriteLine("SessionHasFixedTime: " + csd.SessionHasFixedTime);
            if (csd.SessionHasFixedTime)
            {
                if (csd.SessionType == SessionType.Race)
                    Console.WriteLine("SessionRunningTime: {0} minutes", csd.SessionTotalRunTime / 60);
                else
                    Console.WriteLine("SessionRunningTime: {0} minutes", (csd.SessionTotalRunTime - 30.0f) / 60);  // For now subtract 30 secs of red light, but eventually extract the exact delay.
            }
            else
                Console.WriteLine("SessionNumberOfLaps: " + csd.SessionNumberOfLaps);

            Console.WriteLine("=====================================================");
        }

        private int GetPreprocessedPlace(ref GTR2VehicleScoring vehScoring)
        {
            if (this.rspwState == RollingStateWorkaroundState.Done)
                return vehScoring.mPlace;
            else
            {
                if (this.rspwIDToRSWData.TryGetValue(vehScoring.mID, out var rswvd))
                    return rswvd.frozenPosition;
            }

            return vehScoring.mPlace;
        }

        // Rolling start is f-ed up slightly.  As soon as leader crosses s/f line, standings reported by the game are incorrect.
        // They get back order only when all vehicles cross s/f line.  So, we need to cache last valid standings and use them
        // during that time window.
        enum RollingStateWorkaroundState
        {
            Done,
            PhaseWentGreen,
            LeaderCrossedStartFinishLine
        }
        RollingStateWorkaroundState rspwState = RollingStateWorkaroundState.Done;

        public class RollingStateWorkaroundVehicleData
        {
            public bool crossedSFLine = false;
            public int frozenPosition = -1;
#if DEBUG
            public string driverName = "";
#endif  // DEBUG
        }

        Dictionary<int, RollingStateWorkaroundVehicleData> rspwIDToRSWData = null;

        private void ApplyRollingStartPosWorkaround(
            GameStateData cgs,
            GameStateData pgs,
            ref GTR2Scoring scoring,
            ref GTR2Extended extended,
            int leaderVehID,
            OpponentData leaderOpponentData,
            bool playerOnNewLap,
            int playerVehID)
        {
            var csd = cgs.SessionData;
            if (csd.SessionType != SessionType.Race
                || pgs == null)
                return;

            var psd = pgs.SessionData;

            if (this.rspwState == RollingStateWorkaroundState.Done
                && psd.SessionPhase == SessionPhase.Formation
                && csd.SessionPhase == SessionPhase.Green)
            {
                Console.WriteLine("Rolling Start position workaround: just went green.");
                this.rspwState = RollingStateWorkaroundState.PhaseWentGreen;
                this.rspwIDToRSWData = new Dictionary<int, RollingStateWorkaroundVehicleData>();
            }

            if (this.rspwState == RollingStateWorkaroundState.PhaseWentGreen)
            {
                // Check if anyone crossed s/f line - leader below is wrong.
                if ((leaderOpponentData != null && leaderOpponentData.IsNewLap)  // leaderOpponentData == null means that player is leading.
                    || playerOnNewLap)
                {
                    Console.WriteLine("Rolling Start position workaround: leader crossed s/f line, freeze the position order.");
                    this.rspwState = RollingStateWorkaroundState.LeaderCrossedStartFinishLine;

                    if (this.rspwIDToRSWData.TryGetValue(leaderVehID, out var rswvd))
                        rswvd.crossedSFLine = true;
#if DEBUG
                    Console.WriteLine("Rolling Start position workaround: frozen grid order:");
                    foreach (var rswvdOrder in this.rspwIDToRSWData)
                    {
                        Console.WriteLine($"Driver: {rswvdOrder.Value.driverName}  Assigned pos: {rswvdOrder.Value.frozenPosition}");
                    }
#endif  // DEBUG
                }
                else
                {
                    for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                    {
                        var vehicleScoring = scoring.mVehicles[i];
                        var vehicleExtendedScoring = extended.mExtendedVehicleScoring[vehicleScoring.mID % GTR2Constants.MAX_MAPPED_IDS];

                        if (this.rspwIDToRSWData.TryGetValue(vehicleScoring.mID, out var rswvd))
                            rswvd.frozenPosition =  vehicleScoring.mPlace;
                        else
                        {
                            this.rspwIDToRSWData.Add(vehicleScoring.mID, new RollingStateWorkaroundVehicleData()
                            {
                                frozenPosition = vehicleScoring.mPlace
#if DEBUG
                                ,
                                driverName = GTR2GameStateMapper.GetStringFromBytes(vehicleScoring.mDriverName)
#endif  // DEBUG
                            });
                        }
                    }
                }
            }
            else if (this.rspwState == RollingStateWorkaroundState.LeaderCrossedStartFinishLine)
            {
                var allVehCrossedSF = true;
                for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                {
                    if (playerOnNewLap && this.rspwIDToRSWData.TryGetValue(playerVehID, out var rswvd))
                    {
                        rswvd.crossedSFLine = true;
                        allVehCrossedSF = allVehCrossedSF && rswvd.crossedSFLine;
                    }

                    var vehicleScoring = scoring.mVehicles[i];
                    var cci = this.GetCachedCarInfo(ref vehicleScoring, ref extended);
                    if (cci.opponentData != null  // DNF/DQ are nulled out.
                        && this.rspwIDToRSWData.TryGetValue(vehicleScoring.mID, out var rswvdOpponent))
                    {
                        if (cci.opponentData.IsNewLap)
                            rswvdOpponent.crossedSFLine = true;

                        allVehCrossedSF = allVehCrossedSF && rswvdOpponent.crossedSFLine;
                    }
                }

                if (allVehCrossedSF)
                {
                    Console.WriteLine("Rolling Start position workaround: all vehicles crossed s/f line.  Workaround is done.");
                    this.rspwState = RollingStateWorkaroundState.Done;
                }
            }
        }

        private void ProcessMCMessages(GameStateData cgs, GameStateData pgs, GTR2SharedMemoryReader.GTR2StructWrapper shared)
        {
            if (shared.extended.mTicksFirstHistoryMessageUpdated == this.lastFirstHistoryMessageUpdatedTicks
                && shared.extended.mTicksSecondHistoryMessageUpdated == this.lastSecondHistoryMessageUpdatedTicks
                && shared.extended.mTicksThirdHistoryMessageUpdated == this.lastThirdHistoryMessageUpdatedTicks)
                return;

            if (this.lastFirstHistoryMessageUpdatedTicks != shared.extended.mTicksFirstHistoryMessageUpdated)
            {
                this.lastFirstHistoryMessageUpdatedTicks = shared.extended.mTicksFirstHistoryMessageUpdated;

                var msg = GTR2GameStateMapper.GetStringFromBytes(shared.extended.mFirstHistoryMessage);
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    Log.Info("First history message changed.");
                    this.ProcessMCMesagesHelper(cgs, pgs, msg);
                }
            }

            if (this.lastSecondHistoryMessageUpdatedTicks != shared.extended.mTicksSecondHistoryMessageUpdated)
            {
                this.lastSecondHistoryMessageUpdatedTicks = shared.extended.mTicksSecondHistoryMessageUpdated;

                var msg = GTR2GameStateMapper.GetStringFromBytes(shared.extended.mSecondHistoryMessage);
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    Log.Info("Second history message changed.");
                    this.ProcessMCMesagesHelper(cgs, pgs, msg);
                }
            }

            if (this.lastThirdHistoryMessageUpdatedTicks != shared.extended.mTicksThirdHistoryMessageUpdated)
            {
                this.lastThirdHistoryMessageUpdatedTicks = shared.extended.mTicksThirdHistoryMessageUpdated;

                var msg = GTR2GameStateMapper.GetStringFromBytes(shared.extended.mThirdHistoryMessage);
                if (!string.IsNullOrWhiteSpace(msg))
                {
                    Log.Info("Third history message changed.");
                    this.ProcessMCMesagesHelper(cgs, pgs, msg);
                }
            }
        }

        private void ProcessMCMesagesHelper(GameStateData cgs, GameStateData pgs, string msg)
        {
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
                else if (msg == "Headlights Are Required When Raining")
                    cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.HEADLIGHTS_REQUIRED_WHEN_RAINING;
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
                    else if (msg.EndsWith("Driving Without Headlights") || msg.EndsWith("Driving In Rain Without Headlights"))
                        cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.DISQUALIFIED_DRIVING_WITHOUT_HEADLIGHTS;
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
                    //if (this.enableWrongWayMessage)
                    cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.WRONG_WAY;
                }
                else if (msg == "Blue Flag Warning: Move over soon or be penalized")
                {
                    cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.BLUE_MOVE_OR_BE_PENALIZED;
                }
                else if (msg == "Points will be awarded this lap!")
                {
                    cgs.PenaltiesData.Warning = PenatiesData.WarningMessage.POINTS_WILL_BE_AWARDED_THIS_LAP;
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
                    // Avoid spamming console too aggressively.
                    if ((cgs.Now - this.timeHistoryMessageIgnored).TotalSeconds > 10)
                    {
                        this.timeHistoryMessageIgnored = cgs.Now;
                        Console.WriteLine("MC Message: ignored - \"" + msg + "\"");
                    }
#else
                    Console.WriteLine("MC Message: ignored - \"" + msg + "\"");
#endif
                }
            }
            else
            {
#if !DEBUG
                if ((cgs.Now - this.timeHistoryMessageIgnored).TotalSeconds > 5)
                {
                    this.timeHistoryMessageIgnored = cgs.Now;
                    Console.WriteLine("MC Messages: message was already processed - \"" + msg + "\"    Elapsed seconds: " + (cgs.Now - this.timeEffectiveMessageProcessed).TotalSeconds.ToString("0.00"));
                }
#else
                Console.WriteLine("MC Messages: message was already processed - \"" + msg + "\"    Elapsed seconds: " + (cgs.Now - this.timeEffectiveMessageProcessed).TotalSeconds.ToString("0.00"));
#endif
            }
        }

        private static double getVehicleSpeed(ref GTR2VehicleTelemetry vehicleTelemetry)
        {
            return Math.Sqrt((vehicleTelemetry.mLocalVel.x * vehicleTelemetry.mLocalVel.x)
                + (vehicleTelemetry.mLocalVel.y * vehicleTelemetry.mLocalVel.y)
                + (vehicleTelemetry.mLocalVel.z * vehicleTelemetry.mLocalVel.z));
        }

        private static double getVehicleSpeed(ref GTR2VehicleScoring vehicleScoring)
        {
            return Math.Sqrt((vehicleScoring.mLocalVel.x * vehicleScoring.mLocalVel.x)
                + (vehicleScoring.mLocalVel.y * vehicleScoring.mLocalVel.y)
                + (vehicleScoring.mLocalVel.z * vehicleScoring.mLocalVel.z));
        }

        private static double GetEstimatedLapDist(GTR2SharedMemoryReader.GTR2StructWrapper shared, ref GTR2VehicleScoring vehicleScoring, ref GTR2VehicleTelemetry vehicleTelemetry)
        {
            // Estimate lapdist
            // See how much ahead telemetry is ahead of scoring update
            /*var delta = vehicleTelemetry.mDeltaTime - shared.scoring.mScoringInfo.mCurrentET;
            var lapDistEstimated = vehicleScoring.mLapDist;
            if (delta > 0.0)
            {
                var localZAccelEstimated = vehicleScoring.mLocalAccel.z * delta;
                var localZVelEstimated = vehicleScoring.mLocalVel.z + localZAccelEstimated;

                lapDistEstimated = vehicleScoring.mLapDist - localZVelEstimated * delta;
            }*/

            // TODO: we could track time since scoring update and interpolate.  Just add TickCounts to both tel and scoring.
                    return vehicleScoring.mLapDist;
        }

        private void ProcessPlayerTimingData(
            ref GTR2Scoring scoring,
            GameStateData currentGameState,
            GameStateData previousGameState,
            ref GTR2VehicleScoring playerScoring,
            ref GTR2ExtendedVehicleScoring playerExtendedScoring)
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
                    || csd.SectorNumber == 3 && csd.LastSector2Time < 0.0f
                    /*|| csd.IsNewLap && csd.LastSector3Time < 0.0f*/)
                // Make sure that's not after rolling start
                && csd.CompletedLaps > 0
                // And, this is not an out/in lap
                && !cgs.PitData.OnOutLap && !cgs.PitData.OnInLap
                // And it's Race or Qualification
                && (csd.SessionType == SessionType.Race || csd.SessionType == SessionType.Qualify)))
            {
                csd.CurrentLapIsValid = false;
            }
            // If current lap was marked as invalid, keep it that way.
            else if (psd.CompletedLaps == csd.CompletedLaps  // Same lap
                     && !psd.CurrentLapIsValid)
            {
                csd.CurrentLapIsValid = false;
            }
            // GTR2 lap time or whole lap won't count
            else if (playerExtendedScoring.mCountLapFlag != (byte)GTR2Constants.GTR2CountLapFlag.CountLapAndTime
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
            var lastSectorTime = this.GetLastSectorTime(ref playerScoring, csd.SectorNumber);

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

        private float GetLastSectorTime(ref GTR2VehicleScoring vehicle, int currSector)
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

        // NOTE: This can be made generic for all sims, but I am not sure if anyone needs this but me
        private static void writeDebugMsg(string msg)
        {
            Console.WriteLine("DEBUG_MSG: " + msg);
        }

        private static void writeSpinningLockingDebugMsg(GameStateData cgs, double frontLeftRotation, double frontRightRotation,
            double rearLeftRotation, double rearRightRotation, float minRotatingSpeedOld, float maxRotatingSpeedOld, float minFrontRotatingSpeed, float minRearRotatingSpeed,
            float maxFrontRotatingSpeed, float maxRearRotatingSpeed)
        {
            if (cgs.TyreData.LeftFrontIsLocked)
                GTR2GameStateMapper.writeDebugMsg(string.Format("Left Front is locked.  mRotation: {0}  minFrontRotatingSpeed: {1}  minRotatingSpeedOld: {2}", frontLeftRotation.ToString("0.000"), minFrontRotatingSpeed.ToString("0.000"), minRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.RightFrontIsLocked)
                GTR2GameStateMapper.writeDebugMsg(string.Format("Right Front is locked.  mRotation: {0}  minFrontRotatingSpeed: {1}  minRotatingSpeedOld: {2}", frontRightRotation.ToString("0.000"), minFrontRotatingSpeed.ToString("0.000"), minRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.LeftRearIsLocked)
                GTR2GameStateMapper.writeDebugMsg(string.Format("Left Rear is locked.  mRotation: {0}  minRearRotatingSpeed: {1}  minRotatingSpeedOld: {2}", rearLeftRotation.ToString("0.000"), minRearRotatingSpeed.ToString("0.000"), minRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.RightRearIsLocked)
                GTR2GameStateMapper.writeDebugMsg(string.Format("Right Rear is locked.  mRotation: {0}  minRearRotatingSpeed: {1}  minRotatingSpeedOld: {2}", rearRightRotation.ToString("0.000"), minRearRotatingSpeed.ToString("0.000"), minRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.LeftFrontIsSpinning)
                GTR2GameStateMapper.writeDebugMsg(string.Format("Left Front is spinning.  mRotation: {0}  maxFrontRotatingSpeed: {1}  maxRotatingSpeedOld: {2}", frontLeftRotation.ToString("0.000"), maxFrontRotatingSpeed.ToString("0.000"), maxRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.RightFrontIsSpinning)
                GTR2GameStateMapper.writeDebugMsg(string.Format("Right Front is spinning.  mRotation: {0}  maxFrontRotatingSpeed: {1}  maxRotatingSpeedOld: {2}", frontRightRotation.ToString("0.000"), maxFrontRotatingSpeed.ToString("0.000"), maxRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.LeftRearIsSpinning)
                GTR2GameStateMapper.writeDebugMsg(string.Format("Left Rear is spinning.  mRotation: {0}  maxFronRotatingSpeed: {1}  maxRotatingSpeedOld: {2}", rearLeftRotation.ToString("0.000"), maxRearRotatingSpeed.ToString("0.000"), maxRotatingSpeedOld.ToString("0.000")));
            if (cgs.TyreData.RightRearIsSpinning)
                GTR2GameStateMapper.writeDebugMsg(string.Format("Right Rear is spinning.  mRotation: {0}  maxRearRotatingSpeed: {1}  maxRotatingSpeedOld: {2}", rearRightRotation.ToString("0.000"), maxRearRotatingSpeed.ToString("0.000"), maxRotatingSpeedOld.ToString("0.000")));
        }

        private PitWindow mapToPitWindow(GTR2YellowFlagState pitWindow)
        {
            // it seems that the pit window is only truly open on multiplayer races?
            if (this.isOfflineSession)
            {
                return PitWindow.Open;
            }
            switch (pitWindow)
            {
                case GTR2Constants.GTR2YellowFlagState.PitClosed:
                    return PitWindow.Closed;
                case GTR2Constants.GTR2YellowFlagState.PitOpen:
                case GTR2Constants.GTR2YellowFlagState.PitLeadLap:
                    return PitWindow.Open;
                default:
                    return PitWindow.Unavailable;
            }
        }


        private SessionPhase MapToSessionPhase(
            GTR2GamePhase sessionPhase,
            SessionType sessionType,
            ref GTR2VehicleScoring player)
        {
            switch (sessionPhase)
            {
                case GTR2Constants.GTR2GamePhase.Countdown:
                    return SessionPhase.Countdown;
                // warmUp never happens, but just in case
                case GTR2Constants.GTR2GamePhase.WarmUp:
                case GTR2Constants.GTR2GamePhase.Formation:
                    return SessionPhase.Formation;
                case GTR2Constants.GTR2GamePhase.Garage:
                    return SessionPhase.Garage;
                case GTR2Constants.GTR2GamePhase.GridWalk:
                    return SessionPhase.Gridwalk;
                // sessions never go to sessionStopped, they always go straight from greenFlag to sessionOver
                case GTR2Constants.GTR2GamePhase.SessionStopped:
                case GTR2Constants.GTR2GamePhase.SessionOver:
                    if (sessionType == SessionType.Race
                        && player.mFinishStatus == (sbyte)GTR2Constants.GTR2FinishStatus.None)
                    {
                        return SessionPhase.Checkered;
                    }
                    else
                    {
                        return SessionPhase.Finished;
                    }
                // fullCourseYellow will count as greenFlag since we'll call it out in the Flags separately anyway
                case GTR2Constants.GTR2GamePhase.FullCourseYellow:
                    return SessionPhase.FullCourseYellow;
                case GTR2Constants.GTR2GamePhase.GreenFlag:
                    return SessionPhase.Green;
                default:
                    return SessionPhase.Unavailable;
            }
        }

        // finds OpponentData key for given vehicle based on driver name, vehicle class, and world position
        // TODO: is this even needed if we have mID?
        private String GetOpponentKeyForVehicleInfo(
            ref GTR2VehicleScoring vehicleScoring,
            GameStateData previousGameState,
            float sessionRunningTime,
            string driverName,
            int duplicatesCount,
            ref GTR2Extended extended)
        {
            if (previousGameState == null)
                return null;

            var possibleKeys = new List<string>();
            for (int i = 1; i <= duplicatesCount; ++i)
                possibleKeys.Add(driverName + "_duplicate_ " + i);

            float[] worldPos = new float[] { (float)vehicleScoring.mPos.x, (float)vehicleScoring.mPos.z };

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
                        var cci = this.GetCachedCarInfo(ref vehicleScoring, ref extended);
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
            var shared = wrapper as GTR2SharedMemoryReader.GTR2StructWrapper;
            switch (shared.scoring.mScoringInfo.mSession)
            {
                case 0:  // Applies to open practice, private practice, time trial.
                         // This might be problematic - memory may be simply empty.
                         // up to four possible practice sessions (seems 2 in GTR2)
                    if (shared.extended.mGameMode == (int)GTR2GameMode.DrivingSchool)
                        return SessionType.DrivingSchool;

                    goto case 1;

                case 1:
                case 2:
                    // This might go from LonePractice to Practice without any nice state transition.  However,
                    // I am not aware of any horrible side effects.
                    if (this.lastPracticeNumVehicles < shared.scoring.mScoringInfo.mNumVehicles)
                    {
                        this.lastPracticeNumVehicles = shared.scoring.mScoringInfo.mNumVehicles;
                        this.lastPracticeNumNonGhostVehicles = 0;
                        // Populate cached car info.
                        for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
                        {
                            var vehicleScoring = shared.scoring.mVehicles[i];
                            var vehicleExtendedScoring = shared.extended.mExtendedVehicleScoring[vehicleScoring.mID % GTR2Constants.MAX_MAPPED_IDS];
                            var cci = this.GetCachedCarInfo(ref vehicleScoring, ref shared.extended);
                            if (cci.isGhost)
                                continue;  // Skip trainer.

                            ++this.lastPracticeNumNonGhostVehicles;
                        }
                    }

                    return this.lastPracticeNumNonGhostVehicles > 1 // 1 means player only session.
                        ? SessionType.Practice
                        : SessionType.LonePractice;
                // up to four possible qualifying sessions (seems 2 in GTR2)
                case 3:
                case 4:
                    return SessionType.Qualify;
                case 5:
                    return SessionType.Practice;  // Warmup really.
                case 6:
                    return SessionType.Race;
                // up to four possible race sessions
                case 10:
                case 11:
                case 12:
                case 13:
                default:
                    return SessionType.Unavailable;
            }
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

        private TyreType MapToTyreType(ref GTR2Extended extended, ref GTR2ExtendedVehicleScoring vehicleExtendedScoring)
        {
            // Do not cache tyre type if telemetry is not available yet.
            if (vehicleExtendedScoring.mCurrCompoundName == null)
                return TyreType.Uninitialized;

            // For now, use fronts.
            var frontCompound = GTR2GameStateMapper.GetStringFromBytes(vehicleExtendedScoring.mCurrCompoundName).ToUpperInvariant();
            var tyreType = TyreType.Unknown_Race;
            if (this.compoundNameToTyreType.TryGetValue(frontCompound, out tyreType))
                return tyreType;

            tyreType = TyreType.Unknown_Race;
            if (string.IsNullOrWhiteSpace(frontCompound))
                tyreType = TyreType.Unknown_Race;
            // TODO_RF2: this is broken in rF2, adjust order in rF2.
            else if (frontCompound.Contains("WET") || frontCompound.Contains("RAIN") || frontCompound.Contains("MONSOON"))
                tyreType = TyreType.Wet;
            else if (frontCompound.Contains("INTERMEDIATE"))
                tyreType = TyreType.Intermediate;
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

        private ControlType MapToControlType(GTR2Control controlType)
        {
            switch (controlType)
            {
                case GTR2Constants.GTR2Control.AI:
                    return ControlType.AI;
                case GTR2Constants.GTR2Control.Player:
                    return ControlType.Player;
                case GTR2Constants.GTR2Control.Remote:
                    return ControlType.Remote;
                default:
                    return ControlType.Unavailable;
            }
        }

        public static string GetStringFromBytes(byte[] bytes)
        {
            var nullIdx = Array.IndexOf(bytes, (byte)0);

            return nullIdx >= 0
              ? Encoding.Default.GetString(bytes, 0, nullIdx)
              : Encoding.Default.GetString(bytes);
        }

        public static string GetSanitizedDriverName(string nameFromGame)
        {
            var fwdSlashPos = nameFromGame.IndexOf('/');
            if (fwdSlashPos != -1)
                Console.WriteLine(string.Format(@"Detected pair name: ""{0}"" . Crew Chief does not currently support double names, first part will be used.", nameFromGame));

            var sanitizedName = fwdSlashPos == -1 ? nameFromGame : nameFromGame.Substring(0, fwdSlashPos);

            return sanitizedName;
        }

        // Vehicle telemetry is not always available (before sesssion start).  Instead of
        // hardening code against this case, create and zero intialize arrays within passed in object.
        // This is equivalent of how V1 and rF1 works.
        // NOTE: not a complete initialization, just parts that were cause NRE.
        public static void InitEmptyVehicleTelemetry(ref GTR2VehicleTelemetry vehicleTelemetry)
        {
            Debug.Assert(vehicleTelemetry.mWheel == null);

            vehicleTelemetry.mWheel = new GTR2Wheel[4];
            for (int i = 0; i < 4; ++i)
                vehicleTelemetry.mWheel[i].mTemperature = new float[3];
        }

        private FrozenOrderData GetFrozenOrderData(GameStateData cgs, GameStateData pgs, FrozenOrderData prevFrozenOrderData, ref GTR2VehicleScoring playerVehicle,
            ref GTR2Scoring scoring, ref GTR2Extended extended, float vehicleSpeedMS)
        {
            var fod = new FrozenOrderData();

            // Only applies to formation laps and FCY.
            if (scoring.mScoringInfo.mGamePhase != (int)GTR2Constants.GTR2GamePhase.Formation
              && scoring.mScoringInfo.mGamePhase != (int)GTR2Constants.GTR2GamePhase.FullCourseYellow)
            {
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
                fod.CarNumberToFollowRaw = prevFrozenOrderData.CarNumberToFollowRaw;
            }
            else
                this.lastKnownVehicleToFollowID = SPECIAL_MID_NONE;

            if (fod.Phase == FrozenOrderPhase.None)
            {
                // Don't bother checking updated ticks, this showld allow catching multiple SC car phases.
                var fhm = GTR2GameStateMapper.GetStringFromBytes(extended.mFirstHistoryMessage);
                if (!string.IsNullOrWhiteSpace(fhm)
                  && fhm == "Begin Formation Lap")
                {
                    fod.Phase = GTR2GameStateMapper.GetSector(playerVehicle.mSector) == 3 && vehicleSpeedMS > 10.0f ? FrozenOrderPhase.FastRolling : FrozenOrderPhase.Rolling;
                    this.firstHistoryMessageUpdatedFOTicks = extended.mTicksFirstHistoryMessageUpdated;
                }
                else if (fhm == "Begin Formation/Standing Lap")
                {
                    fod.Phase = FrozenOrderPhase.FormationStanding;
                    this.firstHistoryMessageUpdatedFOTicks = extended.mTicksFirstHistoryMessageUpdated;
                    fod.SafetyCarSpeed = extended.mFormationLapSpeeed;
                }
                else if (!string.IsNullOrWhiteSpace(fhm)
                  && (fhm == "Full-Course Yellow" || fhm == "One Lap To Go"))
                {
                    fod.Phase = FrozenOrderPhase.FullCourseYellow;
                    this.firstHistoryMessageUpdatedFOTicks = extended.mTicksFirstHistoryMessageUpdated;
                }
                else if (string.IsNullOrWhiteSpace(fhm))
                    fod.Phase = prevFrozenOrderData.Phase;
            }

            if (fod.Phase == FrozenOrderPhase.None)
                return fod;  // Wait a bit, there's a delay for string based phases.

            // NOTE: for formation/standing capture order once.  The monitor uses these fields to
            // announce the player's starting position and grid stall.
            if ((prevFrozenOrderData == null || prevFrozenOrderData.Phase == FrozenOrderPhase.None)
                && fod.Phase == FrozenOrderPhase.FormationStanding)
            {
                // FormationStanding announcements are gated on a non-empty action.  RF2 uses
                // Follow for the captured starting-order state even though no driver is followed.
                fod.Action = FrozenOrderAction.Follow;
                fod.AssignedColumn = this.GetGTR2GridColumn(ref playerVehicle, ref scoring);
                fod.AssignedPosition = playerVehicle.mPlace;

                // Determine which side the pole sitter occupies so the row number can be derived
                // from GTR2's single-file place value.
                var leaderCol = FrozenOrderColumn.None;
                for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                {
                    var veh = scoring.mVehicles[i];
                    if (veh.mPlace == 1)
                    {
                        leaderCol = this.GetGTR2GridColumn(ref veh, ref scoring);
                        break;
                    }
                }

                if (fod.AssignedColumn == FrozenOrderColumn.Left)
                {
                    fod.AssignedGridPosition = leaderCol == FrozenOrderColumn.Left
                        ? (playerVehicle.mPlace / 2) + 1
                        : playerVehicle.mPlace / 2;
                }
                else if (fod.AssignedColumn == FrozenOrderColumn.Right)
                {
                    fod.AssignedGridPosition = leaderCol == FrozenOrderColumn.Right
                        ? (playerVehicle.mPlace / 2) + 1
                        : playerVehicle.mPlace / 2;
                }
            }

            // For Rolling, assign initial action, which is to either follow car ahead or SC.
            if (fod.Phase == FrozenOrderPhase.Rolling
                && fod.Action == FrozenOrderAction.None)
            {
                fod.SafetyCarSpeed = extended.mFormationLapSpeeed;
                fod.Action = FrozenOrderAction.Follow;
                var carNumberToFollow = "-1";
                var driverNameToFollow = "Safety Car";
                this.lastKnownVehicleToFollowID = GTR2GameStateMapper.SPECIAL_MID_SAFETY_CAR;
                fod.AssignedColumn = FrozenOrderColumn.None;

                // If we are the first vehicle, check assigned column by inspecting the previous vehicle on the grid.
                // Otherwise, check the next vehicle.
                var columnCheckVehPlace = playerVehicle.mPlace == 1 ? 2 : playerVehicle.mPlace - 1;
                var vehToFollowFound = false;
                var vehForColumnCheckFound = false;

                // Just capture the starting position.
                fod.AssignedPosition = playerVehicle.mPlace;
                for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                {
                    var veh = scoring.mVehicles[i];
                    if (veh.mPlace == playerVehicle.mPlace - 2)
                    {
                        this.lastKnownVehicleToFollowID = veh.mID;
                        var cci = this.GetCachedCarInfo(ref veh, ref extended);
                        driverNameToFollow = cci.driverNameRawSanitized;
                        carNumberToFollow = cci.carNumberStr;
                        // Team, make etc.
                        vehToFollowFound = true;
                    }

                    if (veh.mPlace == columnCheckVehPlace)
                    {
                        GTR2Spotter spotter = null;
                        lock (MainWindow.instanceLock)
                        {
                            if (MainWindow.instance != null)
                            {
                                try
                                {
                                    spotter = (GTR2Spotter)MainWindow.instance.crewChief.getSpotter();
                                }
                                catch (Exception) { }
                            }
                        }

                        var internalSpotter = spotter.getInternalSpotter();
                        if (internalSpotter != null)
                        {
                            var playerRotation = (float)(Math.Atan2(playerVehicle.mOriZ.x, playerVehicle.mOriZ.z));
                            if (playerRotation < 0.0f)
                                playerRotation = (float)(2.0f * Math.PI) + playerRotation;

                            var coordsAligned = internalSpotter.getAlignedXZCoordinates(
                                playerRotation,
                                playerVehicle.mPos.x,
                                playerVehicle.mPos.z,
                                veh.mPos.x,
                                veh.mPos.z);

                             fod.AssignedColumn = coordsAligned[0] > 0.0f ? FrozenOrderColumn.Right : FrozenOrderColumn.Left;
                        }

                        vehForColumnCheckFound = true;
                    }

                    if (vehToFollowFound && vehForColumnCheckFound)
                        break;
                }

                fod.DriverToFollowRaw = driverNameToFollow;
                fod.CarNumberToFollowRaw = carNumberToFollow;
                return fod;
            }

            // NOTE: for formation/standing capture order once.   For other phases, rely on MC text.
            // TODO: For Rolling, find who should we folow from start order.
            if ((fod.Phase == FrozenOrderPhase.FastRolling || fod.Phase == FrozenOrderPhase.Rolling || fod.Phase == FrozenOrderPhase.FullCourseYellow)
                && TimeSpan.FromTicks(cgs.Now.Ticks - this.lastFOChangeTicks).TotalMilliseconds > 2000)  // Since text fluctuates, we need throttle state changes.
            {
                var anyMsgChanged = false;
                if (this.firstHistoryMessageUpdatedFOTicks != extended.mTicksFirstHistoryMessageUpdated)
                {
                    this.firstHistoryMessageUpdatedFOTicks = extended.mTicksFirstHistoryMessageUpdated;
                    this.firstHistoryMessage = GTR2GameStateMapper.GetStringFromBytes(extended.mFirstHistoryMessage);
                    anyMsgChanged = true;
                }

                if (this.secondHistoryMessageUpdatedFOTicks != extended.mTicksSecondHistoryMessageUpdated)
                {
                    this.secondHistoryMessageUpdatedFOTicks = extended.mTicksSecondHistoryMessageUpdated;
                    this.secondHistoryMessage = GTR2GameStateMapper.GetStringFromBytes(extended.mSecondHistoryMessage);
                    anyMsgChanged = true;
                }

                if (this.thirdHistoryMessageUpdatedFOTicks != extended.mTicksThirdHistoryMessageUpdated)
                {
                    this.thirdHistoryMessageUpdatedFOTicks = extended.mTicksThirdHistoryMessageUpdated;
                    this.thirdHistoryMessage = GTR2GameStateMapper.GetStringFromBytes(extended.mThirdHistoryMessage);
                    anyMsgChanged = true;
                }

                if (anyMsgChanged)
                    this.ProcessOrderMessages(fod, ref scoring, ref extended, cgs);

                if (fod.Action == FrozenOrderAction.Follow
                    && this.lastKnownVehicleToFollowID != SPECIAL_MID_NONE
                    && this.lastKnownVehicleToFollowID != GTR2GameStateMapper.SPECIAL_MID_SAFETY_CAR)  // FUTURE: We don't have SC location, yet.
                {
                    // See if we need to catch up the vehicle we have to be following.
                    for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                    {
                        var veh = scoring.mVehicles[i];
                        if (veh.mID == this.lastKnownVehicleToFollowID)
                        {
                            var plrDistTotal = GTR2GameStateMapper.GetDistanceCompleted(ref scoring, ref playerVehicle);
                            var toFollowDistTotal = GTR2GameStateMapper.GetDistanceCompleted(ref scoring, ref veh);
                            var distDelta = toFollowDistTotal - plrDistTotal;

                            // FUTURE:
                            // if (distDelta < 0.0)
                            //   fod.Action = FrozenOrderAction.AllowToPass;
                            if (distDelta > 100.0)
                            {
                                fod.Action = FrozenOrderAction.CatchUp;
                                var cci = this.GetCachedCarInfo(ref veh, ref extended);
                                fod.DriverToFollowRaw = cci.driverNameRawSanitized;
                                fod.CarNumberToFollowRaw = cci.carNumberStr;
                                // Team, make etc.
                                break;
                            }
                        }
                    }
                }

                if (pgs != null
                    && fod.Phase == FrozenOrderPhase.FullCourseYellow
                    && pgs.FlagData.fcyPhase == FullCourseYellowPhase.PENDING
                    && cgs.FlagData.fcyPhase == FullCourseYellowPhase.IN_PROGRESS)
                    fod.SafetyCarSpeed = extended.mFormationLapSpeeed;

                return fod;
            }

            // CCGEP drops the speed when the SC leaves.  Action is intentionally left non-None:
            // FrozenOrderMonitor gates the FormationStanding pre-start reminder on it.
            if (fod.SafetyCarSpeed != extended.mFormationLapSpeeed)
                fod.SafetyCarSpeed = -1.0f;

            return fod;
        }

        private FrozenOrderColumn GetGTR2GridColumn(ref GTR2VehicleScoring vehicle, ref GTR2Scoring scoring)
        {
            var columnCheckVehPlace = vehicle.mPlace == 1 ? 2 : vehicle.mPlace - 1;
            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var adjacentVehicle = scoring.mVehicles[i];
                if (adjacentVehicle.mPlace != columnCheckVehPlace)
                    continue;

                GTR2Spotter spotter = null;
                lock (MainWindow.instanceLock)
                {
                    if (MainWindow.instance != null)
                    {
                        try
                        {
                            spotter = (GTR2Spotter)MainWindow.instance.crewChief.getSpotter();
                        }
                        catch (Exception) { }
                    }
                }

                var internalSpotter = spotter?.getInternalSpotter();
                if (internalSpotter != null)
                {
                    var vehicleRotation = (float)Math.Atan2(vehicle.mOriZ.x, vehicle.mOriZ.z);
                    if (vehicleRotation < 0.0f)
                        vehicleRotation = (float)(2.0f * Math.PI) + vehicleRotation;

                    var coordsAligned = internalSpotter.getAlignedXZCoordinates(
                        vehicleRotation,
                        vehicle.mPos.x,
                        vehicle.mPos.z,
                        adjacentVehicle.mPos.x,
                        adjacentVehicle.mPos.z);

                    return coordsAligned[0] > 0.0f
                        ? FrozenOrderColumn.Right
                        : FrozenOrderColumn.Left;
                }

                break;
            }

            return vehicle.mTrackEdge < 0.0
                ? FrozenOrderColumn.Right
                : FrozenOrderColumn.Left;
        }

        private bool ProcessOrderMessages(FrozenOrderData fod, ref GTR2Scoring scoring, ref GTR2Extended extended, GameStateData cgs)
        {
            if (!string.IsNullOrWhiteSpace(this.firstHistoryMessage))
                Console.WriteLine("MC Message: order instruction 1 - \"" + this.firstHistoryMessage + "\"");

            if (!string.IsNullOrWhiteSpace(this.secondHistoryMessage))
                Console.WriteLine("MC Message: order instruction 2 - \"" + this.secondHistoryMessage + "\"");

            if (!string.IsNullOrWhiteSpace(this.thirdHistoryMessage))
                Console.WriteLine("MC Message: order instruction 3 - \"" + this.thirdHistoryMessage + "\"");

            var followPrefix = @"Stay Behind ";

            var action = FrozenOrderAction.None;

            string prefix = null;
            string driverName = null;  // FUTURE: Hack "Allow To Pass" to use nameless version for now, can change in the future.
            string orderInstruction = null;

            var msgPassSC = "Please Pass The Safety Car";
            var msgAllowToPass = "Warning: You Are Ahead Of The Car You Should Be Following";
            var cautionPrefix = "Caution Lap: ";

            if (this.firstHistoryMessage == msgPassSC
                || this.secondHistoryMessage == msgPassSC
                || this.thirdHistoryMessage == msgPassSC)
            {
                fod.Action = FrozenOrderAction.PassSafetyCar;
            }
            if (this.firstHistoryMessage == msgAllowToPass
                || this.secondHistoryMessage == msgAllowToPass
                || this.thirdHistoryMessage == msgAllowToPass)
            {
                // FUTURE: It is possible that we could use contents of another message on the stack to figure out
                // the driver name.
                action = FrozenOrderAction.AllowToPass;
            }
            else if (fod.Phase == FrozenOrderPhase.FullCourseYellow
                && this.firstHistoryMessage.StartsWith(cautionPrefix))
            {
                orderInstruction = this.firstHistoryMessage.Substring(cautionPrefix.Length);
                if (orderInstruction.StartsWith(followPrefix))
                {
                    prefix = followPrefix;
                    action = FrozenOrderAction.Follow;
                }
            }
            else if (fod.Phase == FrozenOrderPhase.FullCourseYellow
                && this.secondHistoryMessage.StartsWith(cautionPrefix))
            {
                orderInstruction = this.secondHistoryMessage.Substring(cautionPrefix.Length);
                if (orderInstruction.StartsWith(followPrefix))
                {
                    prefix = followPrefix;
                    action = FrozenOrderAction.Follow;
                }
            }
            else if (fod.Phase == FrozenOrderPhase.FullCourseYellow
                && this.thirdHistoryMessage.StartsWith(cautionPrefix))
            {
                orderInstruction = this.thirdHistoryMessage.Substring(cautionPrefix.Length);
                if (orderInstruction.StartsWith(followPrefix))
                {
                    prefix = followPrefix;
                    action = FrozenOrderAction.Follow;
                }
            }
            else if (this.firstHistoryMessage.StartsWith(followPrefix))
            {
                orderInstruction = this.firstHistoryMessage;
                prefix = followPrefix;
                action = FrozenOrderAction.Follow;
            }
            else if (this.secondHistoryMessage.StartsWith(followPrefix))
            {
                orderInstruction = this.secondHistoryMessage;
                prefix = followPrefix;
                action = FrozenOrderAction.Follow;
            }
            else if (this.thirdHistoryMessage.StartsWith(followPrefix))
            {
                orderInstruction = this.thirdHistoryMessage;
                prefix = followPrefix;
                action = FrozenOrderAction.Follow;
            }
            else
            {
                ///Debug.Assert(false, "unhandled action");
#if !DEBUG
                        // Avoid spamming console too aggressively.
                        /*if ((cgs.Now - this.timeLSIMessageIgnored).TotalSeconds > 10)
                        {
                            this.timeLSIMessageIgnored = cgs.Now;
                            Console.WriteLine("LSI Message: unrecognized Frozen Order action - \"" + orderInstruction + "\"");
                        }*/
#else
                Console.WriteLine("Ignoring MC FO Messages, nothing is recognized.");
#endif
                return false;
            }

            string carNumberStr = null;
            if (action == FrozenOrderAction.Follow)
            {
                Debug.Assert(!string.IsNullOrWhiteSpace(orderInstruction));
                if (string.IsNullOrWhiteSpace(orderInstruction))
                    return false;

                var vehNumberHash = orderInstruction.LastIndexOf("#");
                try
                {
                    if (vehNumberHash != -1)
                    {
                        var spaceAfterNumber = orderInstruction.IndexOf(" ", vehNumberHash);
                        carNumberStr = orderInstruction.Substring(vehNumberHash + 1, spaceAfterNumber - vehNumberHash - 1);

                        if (int.TryParse(carNumberStr, out var carNumber))
                        {
                            for (int i = 0; i < scoring.mScoringInfo.mNumVehicles; ++i)
                            {
                                var veh = scoring.mVehicles[i];
                                var vehExtended = extended.mExtendedVehicleScoring[veh.mID % GTR2Constants.MAX_MAPPED_IDS];
                                if (carNumber == vehExtended.mYearAndCarNumber % 1000)
                                {
                                    this.lastKnownVehicleToFollowID = veh.mID;

                                    var cci = this.GetCachedCarInfo(ref veh, ref extended);
                                    driverName = cci.driverNameRawSanitized;
                                    carNumberStr = cci.carNumberStr;
                                    // Team, make etc.
                                    break;
                                }
                            }
                        }

                        // Find the actual car.
                    }
                    else
                    {
                        driverName = "Safety Car";
                        this.lastKnownVehicleToFollowID = GTR2GameStateMapper.SPECIAL_MID_SAFETY_CAR;
                    }
                    //                    SCassignedAhead = true;
                }
                catch (Exception e) { Log.Exception(e); }
            }

            this.lastFOChangeTicks = cgs.Now.Ticks;

            fod.Action = action;
            fod.CarNumberToFollowRaw = carNumberStr;
            fod.DriverToFollowRaw = driverName;
            return true;
        }

        private static double GetDistanceCompleted(ref GTR2Scoring scoring, ref GTR2VehicleScoring vehicle)
        {
            // Note: Can be interpolated a bit.
            return vehicle.mTotalLaps * scoring.mScoringInfo.mLapDist + vehicle.mLapDist;
        }

        private static int GetSector(int GTR2Sector)
        {
            return GTR2Sector == 0 ? 3 : GTR2Sector;
        }

        private static PositionAndMotionData.Rotation GetRotation(ref GTR2Vec3 oriX, ref GTR2Vec3 oriY, ref GTR2Vec3 oriZ)
        {
            var rot = new PositionAndMotionData.Rotation()
            {
                Yaw = (float)Math.Atan2(oriZ.x, oriZ.z),

                Pitch = (float)Math.Atan2(-oriY.z,
                    Math.Sqrt(oriX.z * oriX.z + oriZ.z * oriZ.z)),

                Roll = (float)Math.Atan2(oriY.x,
                    Math.Sqrt(oriX.x * oriX.x + oriZ.x * oriZ.x))
            };

            return rot;
        }

        private CarInfo GetCachedCarInfo(ref GTR2VehicleScoring vehicleScoring, ref GTR2Extended extended)
        {
            CarInfo ci = null;
            if (this.idToCarInfoMap.TryGetValue(vehicleScoring.mID, out ci))
                return ci;

            var driverName = GTR2GameStateMapper.GetStringFromBytes(vehicleScoring.mDriverName);
            driverName = GTR2GameStateMapper.GetSanitizedDriverName(driverName);

            var vehicleExtendedScoring = extended.mExtendedVehicleScoring[vehicleScoring.mID % GTR2Constants.MAX_MAPPED_IDS];

            var carClassId = GTR2GameStateMapper.GetStringFromBytes(vehicleExtendedScoring.mCarClass);
            var carClass = CarData.getCarClassForClassNameOrCarName(carClassId);

            // Name does not appear to be localized in GTR2, so hardcoding it is ok for now.
            var isGhost = string.Equals(driverName, "transparent trainer", StringComparison.InvariantCultureIgnoreCase);

            var carNumber = vehicleExtendedScoring.mYearAndCarNumber % 1000;
            string carNumberStr = $"{carNumber}";
            ci = new CarInfo()
            {
                carClass = carClass,
                driverNameRawSanitized = driverName,
                isGhost = isGhost,
                carName = GTR2GameStateMapper.GetStringFromBytes(vehicleExtendedScoring.mCarModelName),
                teamName = GTR2GameStateMapper.GetStringFromBytes(vehicleExtendedScoring.mTeamName),
                carNumber = carNumber,
                carNumberStr = carNumberStr,
                year = vehicleExtendedScoring.mYearAndCarNumber / 1000
            };

            this.idToCarInfoMap.Add(vehicleScoring.mID, ci);

            return ci;
        }
    }
}
