using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrewChiefV4.GameState;
using CrewChiefV4.Audio;
using CrewChiefV4.NumberProcessing;
using System.IO;
using Newtonsoft.Json;

namespace CrewChiefV4.Events
{
    class Strategy : AbstractEvent
    {
        public static readonly Boolean enablePitExitPositionEstimates = UserSettings.GetUserSettings().getBoolean("enable_pit_exit_position_estimates");
        private readonly Boolean warnAboutOpponentsExitingCloseToPlayer = UserSettings.GetUserSettings().getBoolean("enable_opponent_pit_exit_estimates");
        private static readonly Boolean rf1WarnAboutOpponentsSharingBox = UserSettings.GetUserSettings().getBoolean("enable_rf1_box_sharing_warnings");

        // if this is enabled, don't play the pit position estimates on pit entry. This is only a fallback in case
        // we haven't made a pit request
        private readonly Boolean pitBoxPositionCountdown = UserSettings.GetUserSettings().getBoolean("pit_box_position_countdown");

        public static readonly Boolean persistBenchmarks = UserSettings.GetUserSettings().getBoolean("persist_pitstop_benchmark_times");
        public static volatile Boolean naggedAboutBenchmarks = false;

        // less than 70m => 'just ahead' or 'just behind'
        private static float distanceBehindToBeConsideredVeryClose = 70;
        private static float distanceAheadToBeConsideredVeryClose = 70;

        // assume speed at pit exit is 70m/s (mad, I know), so any distance > 200 and < 500 is a few seconds.
        // < 200 means we'll give a simple non-commital ahead / behind
        private static float minDistanceBehindToBeConsideredAFewSeconds= 200;
        private static float minDistanceAheadToBeConsideredAFewSeconds = 200;
        private static float maxDistanceBehindToBeConsideredAFewSeconds = 500;
        private static float maxDistanceAheadToBeConsideredAFewSeconds = 500;

        private const String folderExpectTrafficOnPitExit = "strategy/expect_traffic_on_pit_exit";
        public const String folderClearTrackOnPitExit = "strategy/expect_clear_track_on_pit_exit";
        public const String folderWeShouldEmergeInPosition = "strategy/we_should_emerge_in_position";
        private const String folderCloseBetween = "strategy/close_between";    // stuff like "right with" and "really close to..."
        public const String folderBetween = "strategy/between";
        public const String folderAnd = "strategy/and";
        private const String folderJustAheadOf = "strategy/just_ahead_of";
        private const String folderJustBehind = "strategy/just_behind";
        private const String folderAheadOf = "strategy/ahead_of";
        private const String folderBehind = "strategy/behind";

        // these are a bit tricky as we only know the separation distance, not time
        private const String folderAFewSecondsAheadOf = "strategy/a_few_seconds_ahead_of";
        private const String folderAFewSecondsBehind = "strategy/a_few_seconds_behind";

        private const String folderPitStopCostsUsAbout = "strategy/a_pitstop_costs_us_about";
        // stuff like: "ok, we'll time this stop", "understood, we'll use this stop as a benchmark, push until sector2" or something
        private const String folderTimePitstopAcknowledge = "strategy/acknowledge_time_pitstop";
        // used when we request a benchmark pitstop timing in practice, but we have no best lap data
        //public const String folderNeedMoreLapData = "strategy/set_benchmark_laptime_first";
        //public const String folderNeedFlyingLap = "strategy/will_calculate_time_loss_from_next_lap";

        private const String folderIsPittingFromPosition = "strategy/is_pitting_from_position";
        private const String folderHeWillComeOutJustInFront = "strategy/he_will_come_out_just_in_front";
        private const String folderHeWillComeOutJustBehind = "strategy/he_will_come_out_just_behind";

        // TODO: record this and wire it up - maybe trigger it as part of the session end message after Q
        //public const String folderWeAreSharingOurPitboxWith = "strategy/we_are_sharing_our_pit_box_with";
        // may be timed during practice.
        // Need to be careful here to ensure this is applicable to the session we've actually entered
        private float playerTimeLostForStop = -1;
        private float gameTimeOnPitEntry = -1;
        private float playerTimeSpentInPitLane = -1;
        private float playerFuelLitresOnEntry = -1;
        private float playerFuelLitresAdded = -1;
        private CarData.CarClass carClassForLastPitstopTiming;
        private String trackNameForLastPitstopTiming;
        private Dictionary<String, float> opponentsTimeLostForStop = new Dictionary<string, float>();
        private Dictionary<String, float> opponentsTimeSpentInPitlane = new Dictionary<string, float>();

        private Boolean isTimingPracticeStop = false;
        private Boolean hasPittedDuringPracticeStopProcess = false;
        private float gameTimeWhenEnteringLastSectorInPractice = -1;
        private float lastAndFirstSectorTimesOnStop = -1;

        // an extra check for practice sessions. We only allow the pit benchmark calculation if we've completed
        // a valid lap that's not our first lap and is not a lap where we visited the pits. This prevents the app
        // using the line lap or outlap for its comparison data
        private Boolean hasPracticeLapForComparison = false;

        // this is set by the box-this-lap macro (eeewwww)
        // and Fuel window open message (uuuuhhhhhh)
        // and primes this event to play the position
        // estimates when we hit the final sector
        public static Boolean playPitPositionEstimates = false;
        private Boolean pitPositionEstimatesRequested = false;

        private Boolean playedPitPositionEstimatesForThisLap = false;

        private string opponentAheadAtLastSector, opponentBehindAtLastSector;

        // for timing opponent stops
        private Boolean timeOpponentStops = true;
        private HashSet<String> opponentsInPitCycle = new HashSet<string>();
        private DateTime nextPitTimingCheckDue = DateTime.MinValue;

        // for tracking opponent pitting
        private DateTime nextOpponentFinalSectorTimingCheckDue = DateTime.MinValue;
        // just used to track when an opponent transitions from to final sector
        private HashSet<String> opponentsInPenultimateSector = new HashSet<string>();
        // these are static because the opponents event needs to check them:
        public static HashSet<String> opponentsWhoWillExitCloseInFront = new HashSet<string>();
        public static HashSet<String> opponentsWhoWillExitCloseBehind = new HashSet<string>();

        private int sectorCount = 3;

        public static String opponentFrontToWatchForPitting = null;
        public static String opponentBehindToWatchForPitting = null;

        public static DateTime nextOpponentPitExitWarningDue = DateTime.MinValue;

        private Boolean printS1Positions = false;

        private Dictionary<String, float> opponentsInPitLane = new Dictionary<String, float>();

        private Boolean waitingForValidDataForBenchmark = false;

        // data for player pitbox position and opponents we think may be sharing that pitbox
        private static String trackAndCarNameForPitBoxPositionData = null; // record the car and track name so reduce the amount of stale data being reused
                                                                           // (note that we can't clear the pit box data between sessions)
        private static float playerPitBoxLapDistance = -1;
        private static float[] playerPitBoxLocation = new float[] { 0, 0 }; // note opponent's array is x,z. Player is x,y,z
        private static HashSet<string> opponentKeysSharingPitLocation = new HashSet<string>();
        private static DateTime nextPitBoxBlockedCheckDue = DateTime.MinValue;
        private HashSet<string> namesBlockingPitBoxAtLastCheck = new HashSet<string>();
        public static bool pitStallIsBlocked = false;

        public override List<SessionPhase> applicableSessionPhases
        {
            get { return new List<SessionPhase> { SessionPhase.Green, SessionPhase.FullCourseYellow }; }
        }

        public Strategy(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
        }

        public override void clearState()
        {
            // don't wipe the time-lost-on-stop that may have been derived in a previous session
            opponentsTimeLostForStop.Clear();
            opponentsTimeSpentInPitlane.Clear();
            isTimingPracticeStop = false;
            gameTimeWhenEnteringLastSectorInPractice = -1;
            nextPitTimingCheckDue = DateTime.MinValue;
            nextOpponentFinalSectorTimingCheckDue = DateTime.MinValue;
            opponentsInPitCycle.Clear();
            Strategy.playPitPositionEstimates = false;
            playedPitPositionEstimatesForThisLap = false;
            timeOpponentStops = true;
            opponentsInPenultimateSector.Clear();
            Strategy.opponentsWhoWillExitCloseBehind.Clear();
            hasPittedDuringPracticeStopProcess = false;
            Strategy.opponentsWhoWillExitCloseInFront.Clear();
            pitPositionEstimatesRequested = false;
            Strategy.opponentFrontToWatchForPitting = null;
            Strategy.opponentBehindToWatchForPitting = null;
            printS1Positions = false;
            opponentsInPitLane.Clear();
            hasPracticeLapForComparison = false;
            namesBlockingPitBoxAtLastCheck.Clear();
            pitStallIsBlocked = false;

            playerTimeLostForStop = -1;
            gameTimeOnPitEntry = -1;
            playerTimeSpentInPitLane = -1;
            playerFuelLitresOnEntry = -1;
            playerFuelLitresAdded = -1;

            opponentAheadAtLastSector = null;
            opponentBehindAtLastSector = null;

            naggedAboutBenchmarks = false;
            if (persistBenchmarks)
            {
                BenchmarkHelper.loadBenchmarks();
            }
        }

        private void setTimeLossFromBenchmark(GameStateData currentGameState)
        {
            if (sectorCount == 2)
            {
                playerTimeLostForStop = lastAndFirstSectorTimesOnStop - (currentGameState.SessionData.PlayerBestLapSector2Time + currentGameState.SessionData.PlayerBestLapSector1Time);
            }
            else
            {
                playerTimeLostForStop = lastAndFirstSectorTimesOnStop - (currentGameState.SessionData.PlayerBestLapSector3Time + currentGameState.SessionData.PlayerBestLapSector1Time);
            }
            // record the data
            if (persistBenchmarks)
            {
                BenchmarkHelper.updatePersistedBenchmark(playerTimeLostForStop, playerFuelLitresAdded, currentGameState.Conditions.CurrentConditions.TrackWetness);
            }
        }

        private Boolean hasValidComparisonForBenchmark(GameStateData currentGameState)
        {
            switch (currentGameState.SessionData.SessionType)
            {
                case SessionType.Qualify:
                case SessionType.PrivateQualify:
                    return false;
                case SessionType.Practice:
                case SessionType.LonePractice:
                    return hasPracticeLapForComparison &&
                           ((sectorCount == 2 && currentGameState.SessionData.PlayerBestLapSector1Time > 0 && currentGameState.SessionData.PlayerBestLapSector2Time > 0) ||
                           (sectorCount == 3 && currentGameState.SessionData.PlayerBestLapSector1Time > 0 && currentGameState.SessionData.PlayerBestLapSector3Time > 0));
                case SessionType.Race:
                    return currentGameState.SessionData.IsNewLap && currentGameState.SessionData.CompletedLaps > 1 && currentGameState.SessionData.PreviousLapWasValid;
                default:
                    return false;
            }
        }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState) 
        {
            // can't be arsed to keep checking this:
            if (previousGameState == null || currentGameState.SessionData.TrackDefinition == null)
            {
                // no track data
                return;
            }

            if (rf1WarnAboutOpponentsSharingBox && Game.RF1
                && currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.Now > nextPitBoxBlockedCheckDue)
            {
                nextPitBoxBlockedCheckDue = currentGameState.Now.AddSeconds(1);
                HashSet<string> opponentsCurrentlyBlockingBox = Strategy.getOpponentKeysBlockingBox(currentGameState);
                bool pitStallWasBlocked = Strategy.pitStallIsBlocked;
                Strategy.pitStallIsBlocked = opponentsCurrentlyBlockingBox.Count() > 0;
                // remove elements not present in the current list of blocking opponents
                namesBlockingPitBoxAtLastCheck.IntersectWith(opponentsCurrentlyBlockingBox);
                // now trigger a warning for each new opponent
                foreach (string currentOpponentBlocking in opponentsCurrentlyBlockingBox)
                {
                    if (namesBlockingPitBoxAtLastCheck.Add(currentOpponentBlocking))
                    {
                        Console.WriteLine("Opponent " + currentOpponentBlocking + " appears to be, or will be, blocking our pit stall");
                    }
                }
                if (pitStallWasBlocked && !Strategy.pitStallIsBlocked)
                {
                    audioPlayer.playMessage(new QueuedMessage(PitStops.folderPitStallOccupied, 6, abstractEvent: this, priority: 10));
                }
                else if (!pitStallIsBlocked && Strategy.pitStallIsBlocked)
                {
                    audioPlayer.playMessage(new QueuedMessage(PitStops.folderPitStallAvailable, 6, abstractEvent: this, priority: 10));
                }
            }

            if (Game.ASSETTO1)
            {
                // tracks may not have 2 or 3 sectors
                sectorCount = CrewChiefV4.assetto.ACSGameStateMapper.numberOfSectorsOnTrack;
                if (sectorCount < 2 || sectorCount > 3)
                {
                    return;
                }
            }

            if (waitingForValidDataForBenchmark && (currentGameState.SessionData.IsNewLap ||
                (currentGameState.SessionData.IsNewSector && previousGameState.SessionData.SectorNumber == 1)))
            {
                if (currentGameState.SessionData.TrackDefinition.name != trackNameForLastPitstopTiming ||
                    !CarData.IsCarClassEqual(currentGameState.carClass, carClassForLastPitstopTiming))
                {
                    // wrong car class or track, cancel waiting
                    waitingForValidDataForBenchmark = false;
                }
                else if (hasValidComparisonForBenchmark(currentGameState))
                {
                    setTimeLossFromBenchmark(currentGameState);
                    Console.WriteLine("Completing pit benchmark in " + currentGameState.SessionData.SessionType +
                        " session, practice pitstop has cost us " + playerTimeLostForStop + " seconds");
                    // only notify about this if we're in a practice session
                    if (currentGameState.SessionData.SessionType == SessionType.Practice ||
                        currentGameState.SessionData.SessionType == SessionType.LonePractice)
                    {
                        audioPlayer.playMessage(new QueuedMessage("pit_stop_cost_estimate", 0,
                            messageFragments: MessageContents(folderPitStopCostsUsAbout, TimeSpanWrapper.FromSeconds(playerTimeLostForStop, Precision.SECONDS)),
                            abstractEvent: this, priority: 10));
                    }
                    waitingForValidDataForBenchmark = false;
                }
            }
            if (currentGameState.SessionData.SessionType == SessionType.Practice ||
                currentGameState.SessionData.SessionType == SessionType.LonePractice)
            {
                if (currentGameState.SessionData.IsNewLap && !currentGameState.PitData.InPitlane &&
                    !currentGameState.PitData.OnOutLap &&
                    currentGameState.SessionData.PreviousLapWasValid)
                {
                    hasPracticeLapForComparison = true;
                }
                // always log the game time at start of final sector, in case we make a late decision to time a stop
                if (enteredLastSector(previousGameState, currentGameState))
                {
                    gameTimeWhenEnteringLastSectorInPractice = currentGameState.SessionData.SessionRunningTime;
                    hasPittedDuringPracticeStopProcess = false;
                }
                if (isTimingPracticeStop)
                {
                    if (!previousGameState.PitData.InPitlane && currentGameState.PitData.InPitlane)
                    {   // Entering pits
                        gameTimeOnPitEntry = currentGameState.SessionData.SessionRunningTime;
                        playerFuelLitresOnEntry = currentGameState.FuelData.FuelLeft;
                        playerTimeSpentInPitLane = -1;
                        // sanity check
                        if (currentGameState.PositionAndMotionData.CarSpeed > 1)
                        {
                            Console.WriteLine("Pitting - this stop will be used as a benchmark. Game time = " + gameTimeWhenEnteringLastSectorInPractice);
                            hasPittedDuringPracticeStopProcess = true;
                        }
                        else
                        {
                            Console.WriteLine("Pitted during practice pitstop window but we appear to have quit-to-pit, cancelling");
                            gameTimeWhenEnteringLastSectorInPractice = -1;
                            isTimingPracticeStop = false;
                            hasPittedDuringPracticeStopProcess = false;
                            gameTimeOnPitEntry = -1;
                            playerFuelLitresOnEntry = -1;
                        }
                    }
                    else if (previousGameState.PitData.InPitlane && !currentGameState.PitData.InPitlane && gameTimeOnPitEntry != -1)
                    {   // Exiting pits
                        playerTimeSpentInPitLane = currentGameState.SessionData.SessionRunningTime - gameTimeOnPitEntry;
                        gameTimeOnPitEntry = -1;
                        playerFuelLitresAdded = currentGameState.FuelData.FuelLeft - playerFuelLitresOnEntry;
                        playerFuelLitresOnEntry = -1;
                    }
                    else if (currentGameState.SessionData.SectorNumber == 2 && previousGameState.SessionData.SectorNumber == 1)
                    {
                        if (hasPittedDuringPracticeStopProcess && gameTimeWhenEnteringLastSectorInPractice != -1)
                        {   // we've completed a pitstop and exited sector 1 so we can calculate the time lost for this stop
                            lastAndFirstSectorTimesOnStop = currentGameState.SessionData.SessionRunningTime - gameTimeWhenEnteringLastSectorInPractice;
                            gameTimeWhenEnteringLastSectorInPractice = -1;
                            isTimingPracticeStop = false;
                            hasPittedDuringPracticeStopProcess = false;
                            carClassForLastPitstopTiming = currentGameState.carClass;
                            trackNameForLastPitstopTiming = currentGameState.SessionData.TrackDefinition.name;

                            if (hasValidComparisonForBenchmark(currentGameState))
                            {
                                waitingForValidDataForBenchmark = false;
                                setTimeLossFromBenchmark(currentGameState);
                                audioPlayer.playMessage(new QueuedMessage("pit_stop_cost_estimate", 0,
                                    messageFragments: MkPitStopTimeLoss(playerTimeLostForStop),
                                    abstractEvent: this, priority: 10));
                            }
                            else
                            {
                                waitingForValidDataForBenchmark = true;
                            }
                        }
                    }
                }
                if (rf1WarnAboutOpponentsSharingBox && Game.RF1 &&
                    currentGameState.PitData.InPitlane && currentGameState.PositionAndMotionData.CarSpeed < 0.1 && currentGameState.PositionAndMotionData.CarSpeed > 0)
                {
                    Strategy.setPlayerPitLocationData(currentGameState);
                }
                // nothing else to do unless we're in race mode
            }
            else if (currentGameState.SessionData.SessionType == SessionType.Race && !currentGameState.PitData.IsTeamRacing)
            {
                // record the time each opponent entered the pitlane
                foreach (KeyValuePair<String, OpponentData> entry in currentGameState.OpponentData)
                {
                    // if this guy hasn't completed a lap, ignore him
                    if (entry.Value.CompletedLaps < 1)
                    {
                        continue;
                    }
                    if (entry.Value.IsNewLap)
                    {
                        Strategy.opponentsWhoWillExitCloseBehind.Remove(entry.Value.DriverRawName);
                        Strategy.opponentsWhoWillExitCloseInFront.Remove(entry.Value.DriverRawName);
                    }
                    if (entry.Value.InPits)
                    {
                        if (!opponentsInPitLane.ContainsKey(entry.Value.DriverRawName))
                        {
                            opponentsInPitLane.Add(entry.Value.DriverRawName, currentGameState.SessionData.SessionRunningTime);
                            Boolean warnedAboutOpponent = false;
                            if (warnAboutOpponentsExitingCloseToPlayer && currentGameState.Now > nextOpponentPitExitWarningDue)
                            {
                                if (Strategy.opponentsWhoWillExitCloseInFront.Contains(entry.Value.DriverRawName))
                                {
                                    // this guy has just entered the pit and we predict he'll exit just in front of us
                                    Console.WriteLine("Opponent " + entry.Value.DriverRawName + " will exit the pit close in front of us");
                                    // suppress this message during FCY phase as it'll spam:
                                    if (entry.Value.ClassPosition >= 1 && currentGameState.SessionData.SessionPhase != SessionPhase.FullCourseYellow)
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("opponent_exiting_in_front", 10,
                                            messageFragments: MessageContents(entry.Value, folderIsPittingFromPosition, entry.Value.ClassPosition, folderHeWillComeOutJustInFront),
                                            abstractEvent: this, priority: 10));
                                    }
                                    // only allow one of these every 10 seconds. When an opponent crosses the start line he's 
                                    // removed from this set anyway
                                    nextOpponentPitExitWarningDue = currentGameState.Now.AddSeconds(10);
                                    Strategy.opponentsWhoWillExitCloseInFront.Remove(entry.Value.DriverRawName);
                                    warnedAboutOpponent = true;
                                }
                                else if (Strategy.opponentsWhoWillExitCloseBehind.Contains(entry.Value.DriverRawName))
                                {
                                    // this guy has just entered the pit and we predict he'll exit just behind us
                                    Console.WriteLine("Opponent " + entry.Value.DriverRawName + " will exit the pit close behind us");
                                    // suppress this message during FCY phase as it'll spam:
                                    if (entry.Value.ClassPosition >= 1 && currentGameState.SessionData.SessionPhase != SessionPhase.FullCourseYellow)
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("opponent_exiting_behind", 10,
                                            messageFragments: MessageContents(entry.Value, folderIsPittingFromPosition, entry.Value.ClassPosition, folderHeWillComeOutJustBehind),
                                            abstractEvent: this, priority: 10));
                                    }
                                    // only allow one of these every 10 seconds. When an opponent crosses the start line he's 
                                    // removed from this set anyway
                                    nextOpponentPitExitWarningDue = currentGameState.Now.AddSeconds(10);
                                    Strategy.opponentsWhoWillExitCloseBehind.Remove(entry.Value.DriverRawName);
                                    warnedAboutOpponent = true;
                                }
                            }
                            if (!warnedAboutOpponent && WatchedOpponents.watchedOpponentKeys.Contains(entry.Key))
                            {
                                // this guy has entered the pits but won't exit close to us, but he's on the watch list
                                if (AudioPlayer.canReadName(entry.Value.DriverRawName, false))
                                {
                                    audioPlayer.playMessage(new QueuedMessage("watched_opponent_pitting", 10,
                                            messageFragments: MessageContents(entry.Value, folderIsPittingFromPosition, entry.Value.ClassPosition),
                                            abstractEvent: this, priority: 10));
                                }
                                else if (WatchedOpponents.teamMates.Contains(entry.Key))
                                {
                                    audioPlayer.playMessage(new QueuedMessage("watched_opponent_pitting", 10,
                                            messageFragments: MessageContents(WatchedOpponents.folderTeamMateIsPittingFromPosition, entry.Value.ClassPosition),
                                            abstractEvent: this, priority: 10));
                                }
                                else if (WatchedOpponents.rivals.Contains(entry.Key))
                                {
                                    audioPlayer.playMessage(new QueuedMessage("watched_opponent_pitting", 10,
                                            messageFragments: MessageContents(WatchedOpponents.folderRivalIsPittingFromPosition, entry.Value.ClassPosition),
                                            abstractEvent: this, priority: 10));
                                }
                                else if (entry.Value.CarNumber != "-1")
                                {
                                    audioPlayer.playMessage(new QueuedMessage("watched_opponent_pitting", 10,
                                            messageFragments: MessageContents(Opponents.folderCarNumber, new CarNumber(entry.Value.CarNumber), folderIsPittingFromPosition, entry.Value.ClassPosition),
                                            abstractEvent: this, priority: 10));
                                }
                            }
                        }
                    }
                    else
                    {
                        if (opponentsInPitLane.ContainsKey(entry.Key))
                        {
                            // he's just left the pit lane
                            opponentsTimeSpentInPitlane[entry.Key] = currentGameState.SessionData.SessionRunningTime - opponentsInPitLane[entry.Key];
                        }
                        opponentsInPitLane.Remove(entry.Value.DriverRawName);
                    }
                }

                if (printS1Positions && previousGameState.SessionData.SectorNumber == 1 && currentGameState.SessionData.SectorNumber == 2)
                {
                    printS1Positions = false;
                    Console.WriteLine("After exiting pit, we're in P " + currentGameState.SessionData.ClassPosition);
                }
                if (previousGameState.PitData.InPitlane && !currentGameState.PitData.InPitlane)
                {
                    // just left the pits, clear our hack
                    Strategy.opponentBehindToWatchForPitting = null;
                    Strategy.opponentFrontToWatchForPitting = null;
                }

                // if we've timed our pitstop in practice, don't search for opponent stop times
                if (currentGameState.SessionData.JustGoneGreen)
                {
                    // don't time opponent stops for iracing because they are so variable - only make estimates for the player,
                    // and when he's done a benchmark stop.
                    timeOpponentStops = !Game.IRACING &&
                        getExpectedPlayerTimeLoss(currentGameState.carClass, currentGameState.SessionData.TrackDefinition.name) == -1;
                }

                if (previousGameState != null && previousGameState.SessionData.SectorNumber != currentGameState.SessionData.SectorNumber)
                {
                    // used by the iracing pit exit predictor to watch opponents for fuel
                    opponentAheadAtLastSector = currentGameState.getOpponentInFront(currentGameState.carClass)?.DriverRawName;
                    opponentBehindAtLastSector = currentGameState.getOpponentBehind(currentGameState.carClass)?.DriverRawName;
                }

                if (currentGameState.SessionData.IsNewLap)
                {
                    playedPitPositionEstimatesForThisLap = false;
                    pitPositionEstimatesRequested = false;
                    Strategy.playPitPositionEstimates = false;
                }
                // if we've just requested a pit stop (and the game has this data), trigger the strategy data when we next hit the final sector
                else if (!previousGameState.PitData.HasRequestedPitStop && currentGameState.PitData.HasRequestedPitStop && !playedPitPositionEstimatesForThisLap)
                {
                    Strategy.playPitPositionEstimates = true;
                    pitPositionEstimatesRequested = false;
                }
                // if we've just entered the pitlane and the pit countdown is disabled, and we don't have a penalty, trigger
                // the strategy stuff
                else if (!pitBoxPositionCountdown && !currentGameState.PenaltiesData.HasDriveThrough && !currentGameState.PenaltiesData.HasStopAndGo &&
                    !playedPitPositionEstimatesForThisLap && !previousGameState.PitData.InPitlane && currentGameState.PitData.InPitlane)
                {
                    Strategy.playPitPositionEstimates = true;
                    pitPositionEstimatesRequested = false;
                }
                if (enablePitExitPositionEstimates &&
                    Strategy.playPitPositionEstimates &&
                    (pitPositionEstimatesRequested || (inFinalSector(currentGameState) && !playedPitPositionEstimatesForThisLap)))
                {
                    // we requested a stop and we're in the final sector, or we requested data, so gather up the data we'll need and report it
                    //
                    // Note that we need to derive the position estimates here before we start slowing for pit entry
                    float bestLapTime;
                    if (sectorCount == 2)
                    {
                        bestLapTime = currentGameState.SessionData.PlayerBestLapSector1Time + currentGameState.SessionData.PlayerBestLapSector2Time;
                    }
                    else
                    {
                        bestLapTime = currentGameState.SessionData.PlayerBestLapSector1Time + currentGameState.SessionData.PlayerBestLapSector2Time + currentGameState.SessionData.PlayerBestLapSector3Time;
                    }
                    Strategy.PostPitRacePosition postRacePositions = getPostPitPositionData(false, currentGameState.SessionData.ClassPosition, currentGameState.SessionData.CompletedLaps,
                            currentGameState.carClass, currentGameState.OpponentData, currentGameState.SessionData.DeltaTime, currentGameState.Now,
                            currentGameState.SessionData.TrackDefinition.name, currentGameState.SessionData.TrackDefinition.trackLength,
                            currentGameState.PositionAndMotionData.DistanceRoundTrack, bestLapTime, currentGameState.SessionData.SessionRunningTime,
                            currentGameState.PitData.HasMandatoryPitStop, currentGameState.PitData.PitWindowEnd, currentGameState.SessionData.SessionHasFixedTime);
                    if (currentGameState.SessionData.SessionPhase != SessionPhase.FullCourseYellow)
                    {
                        reportPostPitData(postRacePositions);
                        playedPitPositionEstimatesForThisLap = true;
                    }
                    pitPositionEstimatesRequested = false;
                    Strategy.playPitPositionEstimates = false;
                }



                //--------------------------------------
                // opponent pit exit position estimation
                if (warnAboutOpponentsExitingCloseToPlayer && currentGameState.Now > nextOpponentFinalSectorTimingCheckDue)
                {
                    float expectedPlayerTimeLoss = -1;
                    DateTime nowMinusExpectedLoss = DateTime.MinValue;
                    Boolean doneTimeLossCalc = false;
                    nextOpponentFinalSectorTimingCheckDue = currentGameState.Now.AddSeconds(5);
                    foreach (KeyValuePair<String, OpponentData> entry in currentGameState.OpponentData)
                    {
                        if (entry.Value.CompletedLaps < 1)
                        {
                            continue;
                        }
                        if (((sectorCount == 3 && entry.Value.CurrentSectorNumber == 2) || (sectorCount == 2 && entry.Value.CurrentSectorNumber == 1)) &&
                            !opponentsInPenultimateSector.Contains(entry.Key))
                        {
                            opponentsInPenultimateSector.Add(entry.Key);
                        }
                        else
                        {
                            if (opponentsInPenultimateSector.Contains(entry.Key) &&
                                ((sectorCount == 3 && entry.Value.CurrentSectorNumber == 3) || (sectorCount == 2 && entry.Value.CurrentSectorNumber == 2)))
                            {
                                opponentsInPenultimateSector.Remove(entry.Key);
                                // this guy has just hit final sector, so do the count-back

                                // lazily obtain the expected time loss
                                if (!doneTimeLossCalc)
                                {
                                    expectedPlayerTimeLoss = getTimeLossEstimate(currentGameState.carClass, currentGameState.SessionData.TrackDefinition.name,
                                        currentGameState.OpponentData, currentGameState.SessionData.ClassPosition);

                                    if (expectedPlayerTimeLoss < 5) // 5 is a number i pulled out my arse, less than 5 seconds lost for a stop means something's wrong
                                    {
                                        // no loss data, can't continue
                                        break;
                                    }
                                    // get his track distanceRoundTrack at this point in history
                                    int fullLapsLost = (int)(expectedPlayerTimeLoss / currentGameState.SessionData.PlayerLapTimeSessionBest);

                                    // need to allow for losing 1 or more complete laps. This adjustment is required because the DeltaTimes stuff only
                                    // holds the last lap's worth of data
                                    nowMinusExpectedLoss = currentGameState.Now.AddSeconds((fullLapsLost * currentGameState.SessionData.PlayerLapTimeSessionBest) - expectedPlayerTimeLoss);
                                    doneTimeLossCalc = true;
                                }

                                TimeSpan closestDeltapointTimeDelta = TimeSpan.MaxValue;
                                float closestDeltapointPosition = -1;
                                int index = 0;
                                foreach (DateTime deltaPointEntry in entry.Value.DeltaTime.deltaPoints)
                                {
                                    TimeSpan timeDelta = (nowMinusExpectedLoss - deltaPointEntry).Duration();
                                    if (timeDelta < closestDeltapointTimeDelta)
                                    {
                                        closestDeltapointTimeDelta = timeDelta;
                                        closestDeltapointPosition = index * entry.Value.DeltaTime.spacing;
                                    }
                                    index++;
                                }
                                // this is the gap we expect to this guy when he leaves the pits. Negative gap means he'll be in front
                                float expectedDistanceToPlayerOnPitExit = currentGameState.PositionAndMotionData.DistanceRoundTrack - closestDeltapointPosition;
                                float absGap = Math.Abs(expectedDistanceToPlayerOnPitExit);

                                if (expectedDistanceToPlayerOnPitExit < 0 && absGap < minDistanceAheadToBeConsideredAFewSeconds && AudioPlayer.canReadName(entry.Value.DriverRawName, false))
                                {
                                    // he'll come out of the pits right in front of us if he pits
                                    Strategy.opponentsWhoWillExitCloseInFront.Add(entry.Value.DriverRawName);
                                }
                                else if (expectedDistanceToPlayerOnPitExit > 0 && absGap < minDistanceBehindToBeConsideredAFewSeconds && AudioPlayer.canReadName(entry.Value.DriverRawName, false))
                                {
                                    // he'll come out right behind us if he pits
                                    Strategy.opponentsWhoWillExitCloseBehind.Add(entry.Value.DriverRawName);
                                }
                            }
                        }
                    }
                }
                //------------------------------


                if (timeOpponentStops && currentGameState.Now > nextPitTimingCheckDue)
                {
                    // check for opponent pit timings every 10 seconds if we don't have our own
                    nextPitTimingCheckDue = currentGameState.Now.AddSeconds(10);
                    // update opponent time lost
                    foreach (KeyValuePair<String, OpponentData> entry in currentGameState.OpponentData)
                    {
                        if (entry.Value.CompletedLaps < 1)
                        {
                            continue;
                        }
                        // only interested in opponent pit times for our class
                        if (CarData.IsCarClassEqual(entry.Value.CarClass, currentGameState.carClass) && entry.Value.CompletedLaps > 2)
                        {
                            if (opponentsInPitCycle.Contains(entry.Key) && entry.Value.CurrentSectorNumber == 2)
                            {
                                // he's just pitted and has entered the finished the first sector since we last checked, so calculate how much time he's lost pitting
                                float bestLastAndFirstSectorTime;
                                if (sectorCount == 3)
                                {
                                    bestLastAndFirstSectorTime = entry.Value.bestSector3Time + entry.Value.bestSector1Time;
                                }
                                else
                                {
                                    bestLastAndFirstSectorTime = entry.Value.bestSector2Time + entry.Value.bestSector1Time;
                                }

                                // note that these are zero-indexed - we want the game time at the end of sector2 on the previous lap and s1 on this lap
                                float lastLapPenultimateSectorEndTime = -1;
                                float thisLapS1EndTime = -1;
                                if (entry.Value.getLastLapData() != null)
                                {
                                    lastLapPenultimateSectorEndTime = entry.Value.getLastLapData().GameTimeAtSectorEnd[sectorCount == 3 ? 1 : 0];
                                }
                                if (entry.Value.getCurrentLapData() != null)
                                {
                                    thisLapS1EndTime = entry.Value.getCurrentLapData().GameTimeAtSectorEnd[0];
                                }
                                // only insert data here if we have sane times
                                if (lastLapPenultimateSectorEndTime > 0 && thisLapS1EndTime > 0)
                                {
                                    float lastPenultimateSectorAndS1Time = thisLapS1EndTime - lastLapPenultimateSectorEndTime;
                                    float timeLost = lastPenultimateSectorAndS1Time - bestLastAndFirstSectorTime;
                                    // if this opponent has lost less than 5 seconds due to pitting, something has gone wrong in the calculations
                                    if (timeLost > 5)
                                    {
                                        opponentsTimeLostForStop[entry.Key] = timeLost;
                                    }
                                }
                                opponentsInPitCycle.Remove(entry.Key);
                            }
                            else if (!opponentsInPitCycle.Contains(entry.Key) && entry.Value.InPits)
                            {
                                opponentsInPitCycle.Add(entry.Key);
                            }
                        }
                    }
                }
            }
        }
        
        // only reports the actual recorded time loss, not an estimate
        public static List<MessageFragment> MkPitStopTimeLoss(float timeLostForStop)
        {
            Console.WriteLine("Practice pitstop has cost us " + timeLostForStop + " seconds");
            return MessageContents(folderPitStopCostsUsAbout, TimeSpanWrapper.FromSeconds(timeLostForStop, Precision.SECONDS));
        }

        // fallbacks that are not dependent on looking at opponents
        public static float getExpectedPlayerTimeLossFallback(float fuelToTheEnd)
        {
            if (Game.IRACING)
            {
                // massive hack, but this is accurate to a few seconds for IMSA/GT3/NASCAR etc...
                // some data points: porsche 963 (gtp) is 1.8 litres / second
                //                   porsche 992 (gt3) is 2.1 litres / second
                return 20f + Math.Max(0f, fuelToTheEnd) / 2f;
            }
            return -1;
        }

        private float getExpectedPlayerTimeLoss(CarData.CarClass carClass, String trackName)
        {
            // this is almost never set, but hey, use it if it's there.
            if (CarData.IsCarClassEqual(carClass, carClassForLastPitstopTiming) && trackName == trackNameForLastPitstopTiming && playerTimeLostForStop > 0 && playerTimeLostForStop < 120)
            {
                return playerTimeLostForStop;
            }

            float litres = Math.Min(CrewChief.currentGameState.FuelData.FuelCapacity, Fuel.GetAdditionalFuelToEndOfRace(true, verbose: false));
            // if we have stopped already, this is probably wrong, so consult how much fuel we previously got
            if (litres < 5)
            {
                litres = Fuel.FuelAddedInLastStop;
            }

            if (persistBenchmarks)
            {
                var benchmark = BenchmarkHelper.findPersistedBenchmarkForCombo(litres);
                if (benchmark != null)
                {
                    return benchmark.timeLoss;
                }
            }

            return getExpectedPlayerTimeLossFallback(litres);
        }

        private float getTimeLossEstimate(CarData.CarClass carClass, String trackName, Dictionary<String, OpponentData> opponents, int currentRacePosition)
        {
            float timeLossEstimate = getExpectedPlayerTimeLoss(carClass, trackName);
            if (timeLossEstimate == -1)
            {
                if (opponentsTimeLostForStop.Count != 0)
                {                    
                    int pittedOpponentPositionDiff = int.MaxValue;
                    // select the best opponent to compare with
                    foreach (KeyValuePair<String, float> entry in opponentsTimeLostForStop)
                    {
                        OpponentData opponentData;
                        if (opponents.TryGetValue(entry.Key, out opponentData))
                        {
                            int positionDiff = Math.Abs(opponentData.ClassPosition - currentRacePosition);
                            if (positionDiff < pittedOpponentPositionDiff)
                            {
                                timeLossEstimate = entry.Value;
                                pittedOpponentPositionDiff = positionDiff;
                                float opponentTimeSpentInPitlane;
                                if (opponentsTimeSpentInPitlane.TryGetValue(entry.Key, out opponentTimeSpentInPitlane))
                                {
                                    playerTimeSpentInPitLane = opponentTimeSpentInPitlane;
                                }
                            }
                        }
                    }
                }
            }
            return timeLossEstimate;
        }

        /**
         * Get the track position (distance round lap) and the total distance covered. Allow for whole laps difference
         * by assuming we'd have been travelling at bestLap pace if we were not stopping
         */
        private Tuple<float, float> getPositionAndTotalDistanceForTimeLoss(float expectedPlayerTimeLoss, float trackLength,
            float currentDistanceRoundTrack, float bestLapTime, int currentLapsCompleted, DateTime now, DeltaTime playerDeltaTime)
        {
            int fullLapsLost = (int) (expectedPlayerTimeLoss / bestLapTime);

            // need to allow for losing 1 or more complete laps. This adjustment is required because the DeltaTimes stuff only
            // holds the last lap's worth of data
            DateTime nowMinusExpectedLoss = now.AddSeconds((fullLapsLost * bestLapTime) - expectedPlayerTimeLoss);
            // get the track distanceRoundTrack at this point in history
            TimeSpan closestDeltapointTimeDelta = TimeSpan.MaxValue;
            float closestDeltapointPosition = float.MaxValue;
            int index = 0;
            foreach (DateTime entry in playerDeltaTime.deltaPoints)
            {
                TimeSpan timeDelta = (nowMinusExpectedLoss - entry).Duration();
                if (timeDelta < closestDeltapointTimeDelta)
                {
                    closestDeltapointTimeDelta = timeDelta;
                    closestDeltapointPosition = index * playerDeltaTime.spacing;
                }
                index++;
            }

            // work out how far we'd have travelled if we were expectedPlayerTimeLoss seconds behind where we are now
            float totalRaceDistanceAtExpectedLoss;
            // also need to allow for this deltapoint position being in front of us
            if (closestDeltapointPosition > currentDistanceRoundTrack)
            {
                totalRaceDistanceAtExpectedLoss = ((currentLapsCompleted - fullLapsLost - 1) * trackLength) + closestDeltapointPosition;
            }
            else
            {
                totalRaceDistanceAtExpectedLoss = ((currentLapsCompleted - fullLapsLost) * trackLength) + closestDeltapointPosition;
            }
            return new Tuple<float, float>(closestDeltapointPosition, totalRaceDistanceAtExpectedLoss);
        }

        // all the nasty logic is in this method - refactor?
        private Strategy.PostPitRacePosition getPostPitPositionData(Boolean fromVoiceCommand, int currentRacePosition, int lapsCompleted,
            CarData.CarClass playerClass, Dictionary<String, OpponentData> opponents, DeltaTime playerDeltaTime, DateTime now,
            String trackName, float trackLength, float currentDistanceRoundTrack, float bestLapTime, float sessionRunningTime,
            Boolean hasMandatoryPitStop, float pitWindowEnd, Boolean fixedTimeSession)
        {
            if (Game.IRACING)
            {
                // to avoid potentially breaking things for everybody else, this entire algorithm is rewritten just for iRacing and
                // may be expanded at some point. In iRacing there's no concept of a mandatory stop. We could guess if an opponent is
                // pitting based on if we're running out and our relative stop counts, but that's super flakey so just assume that when
                // pitting we are the only car pitting.
                //
                // We estimate our pit exit position based on simply deducting the expected pitstop time from the absolute delta to
                // every other car. Multiclass cars are ignored, and are not even considered traffic.
                float estimatedLoss = getTimeLossEstimate(playerClass, trackName, opponents, currentRacePosition);
                if (estimatedLoss < 10 || estimatedLoss > 120)
                {
                    return null; // clearly junk
                }
                var rivals = opponents.Values.ToList().Where(o => CarData.IsCarClassEqual(playerClass, o.CarClass)).ToList();

                // using full deltas allows us to handle shorter tracks where the pitstop is longer than the lap
                // and generally simplifies the code. This is to estimate who will be behind us on the leaderboard
                // after we stop (but nobody else does).
                var lap_time = CrewChief.currentGameState.SessionData.EstimatedLapTime;
                var racing = rivals.ConvertAll(o => (o, playerDeltaTime.GetRelativeFullDelta(o.DeltaTime, lap_time)));
                racing = racing.OrderBy(kv => kv.Item2).ToList();
                int estimated_position = racing.FindIndex(kv => kv.Item2 > estimatedLoss);

                // then we do it again for track position. Short tracks are probably broken,
                // but we try to rewrap times that go too positive (behind) to be negative (ahead).
                int laps = (int) (estimatedLoss / lap_time);
                float estimatedLossModulo = estimatedLoss - laps * lap_time;
                var ahead = new List<OpponentPositionAtPlayerPitExit>();
                var behind = new List<OpponentPositionAtPlayerPitExit>();
                foreach (var o in rivals)
                {
                    var relative = playerDeltaTime.GetRelativeDelta(o.DeltaTime).Item2 + estimatedLossModulo;
                    if (relative < -lap_time / 2 || relative > lap_time / 2)
                    {
                        int laps_ = (int)(relative / lap_time);
                        relative = relative - laps_ * lap_time; // remove laps
                        if (relative < 0)
                        {
                            relative = relative + lap_time; // moves behind
                        }
                        else
                        {
                            relative = relative - lap_time; // moves ahead
                        }
                    }

                    // converts seconds into distance, using the same hacky number that the legacy code uses
                    var distance = Math.Abs(relative * 70);
                    var pos = new OpponentPositionAtPlayerPitExit(distance, true, o.CarClass.carClassEnumString, o);
                    if (relative < 0)
                    {
                        ahead.Add(pos);
                    }
                    else
                    {
                        behind.Add(pos);
                    }
                }

                return new PostPitRacePosition(ahead, behind, estimated_position);
            }

            float halfTrackLength = trackLength / 2;
            // check we have deltapoints first
            if (playerDeltaTime == null || playerDeltaTime.deltaPoints == null || playerDeltaTime.deltaPoints.Length == 0)
            {
                Console.WriteLine("No usable deltapoints object, can't derive post-pit positions");
                return null;
            }

            float expectedPlayerTimeLoss = getTimeLossEstimate(playerClass, trackName, opponents, currentRacePosition);
            if (expectedPlayerTimeLoss >= 5) // 5 is a number i pulled out my arse - less than 5 and surely something's gone wrong in the calculations
            {
                // now we have a sensible value for the time lost due to the stop, estimate where we'll emerge
                // in order to do this we need to know the total race distance at the point where we'd have been
                // expectedPlayerTimeLoss seconds ago
                Tuple<float, float> positionAndTotalDistanceForTimeLoss = getPositionAndTotalDistanceForTimeLoss(
                    expectedPlayerTimeLoss, trackLength, currentDistanceRoundTrack, bestLapTime, lapsCompleted, now, playerDeltaTime);

                float closestDeltapointPosition = positionAndTotalDistanceForTimeLoss.Item1;

                // this will be scaled
                float baseDistanceCorrectionForPittingOpponents = (((trackLength * lapsCompleted) + currentDistanceRoundTrack) - positionAndTotalDistanceForTimeLoss.Item2);

                List<OpponentPositionAtPlayerPitExit> opponentsAhead = new List<OpponentPositionAtPlayerPitExit>();
                List<OpponentPositionAtPlayerPitExit> opponentsBehind = new List<OpponentPositionAtPlayerPitExit>();
                List<Tuple<OpponentData, float>> totalRaceDistances = new List<Tuple<OpponentData, float>>();

                // for fixed time races, see if we'd woudl expect this guy to stop on this lap:
                Boolean expectMandatoryStopRaceTime = hasMandatoryPitStop && fixedTimeSession && pitWindowEnd > 1 && pitWindowEnd * 60 < sessionRunningTime + bestLapTime;

                foreach (OpponentData opponent in opponents.Values)
                {
                    String opponentCarClassId = opponent.CarClass.getClassIdentifier();
                    Boolean isPlayerClass = CarData.IsCarClassEqual(opponent.CarClass, playerClass);

                    float correction = 0;

                    Boolean willNeedToStop = opponent.NumPitStops == 0 &&
                        (expectMandatoryStopRaceTime || (!fixedTimeSession && hasMandatoryPitStop && pitWindowEnd > 1 && opponent.CompletedLaps == pitWindowEnd - 1));

                    if (opponent.InPits || willNeedToStop)
                    {
                        // this makes things awkward. He'll be some distance behind where we predicted him to be.
                        // The best we can do here is move him back some proportion of the distance we'd expect to lose by pitting.
                        // The exact amount is based on how far we think his is through his pit stop process.
                        float timeOpponentEnteredPitlane;
                        if (opponentsInPitLane.TryGetValue(opponent.DriverRawName, out timeOpponentEnteredPitlane))
                        {
                            // proportion of stop completed is the proportion of the amount of in-pitlane time this guy has already spent, minus a small correction for the time 
                            // he'll lose on exit (the part of the pit process he's not completed yet), plus a small correction for the time he's already spent on entry (the part
                            // of the pit process he *as* completed). Because these two corrections are the same (we assume entry loss and exit loss are identical),
                            // these will cancel each other out, so we're only interested in the proportion of in-pitlane time he's spent
                            float proportionOfPitstopCompleted = (sessionRunningTime - timeOpponentEnteredPitlane) / playerTimeSpentInPitLane;
                            correction = baseDistanceCorrectionForPittingOpponents * (1 - (Math.Min(1, Math.Max(0, proportionOfPitstopCompleted))));
                        }
                        else if (willNeedToStop)
                        {
                            // he's not in the pits but probably will be
                            correction = baseDistanceCorrectionForPittingOpponents;
                        }
                        else
                        {
                            // errr...
                            correction = baseDistanceCorrectionForPittingOpponents * 0.5f;
                        }
                    }

                    if (isPlayerClass)
                    {
                        // get this guy's predicted total distance travelled.
                        //
                        // If this car is in the pits, he will be further back due to this stop. Move him back by distance we'd expect him to lose
                        float opponentTotalRaceDistance = ((opponent.CompletedLaps) * trackLength) + opponent.DistanceRoundTrack - correction;
                        totalRaceDistances.Add(new Tuple<OpponentData, float>(opponent, opponentTotalRaceDistance));                        
                    }
                    
                    // want to know how far the opponent is from this closestDeltapointPosition right now
                    // fuck me this is a retarded way to do this, but it's late and my brain has given up.

                    // additional hack here - if he's in the pit we need to correct this position
                    float opponentPositionDelta = opponent.DistanceRoundTrack - closestDeltapointPosition - correction;
                    if (opponentPositionDelta < halfTrackLength * -1)
                    {
                        opponentPositionDelta = (trackLength - closestDeltapointPosition) + opponent.DistanceRoundTrack;
                    }
                    else if (opponentPositionDelta > halfTrackLength)
                    {
                        opponentPositionDelta = -1 * ((trackLength - opponent.DistanceRoundTrack) + closestDeltapointPosition);
                    }

                    float absDelta = Math.Abs(opponentPositionDelta);

                    // all this logic is so messed up that sometimes we end up calculating the leaders as being behind us. If the opponent
                    // has done more laps than us, then just assume they will always come out ahead of us. This will probably break in ovals
                    // and in all kinds of other situations, but this is at least a safety margin.
                    if (opponentPositionDelta > 0 || opponent.CompletedLaps > lapsCompleted)
                    {
                        // he'll be ahead
                        opponentsAhead.Add(new OpponentPositionAtPlayerPitExit(absDelta, isPlayerClass, opponentCarClassId, opponent));
                    }
                    else
                    {
                        opponentsBehind.Add(new OpponentPositionAtPlayerPitExit(absDelta, isPlayerClass, opponentCarClassId, opponent));
                    }                    
                }

                // now work out the race position of the player, from the expected distance covered of all the drivers
                totalRaceDistances.Sort(delegate(Tuple<OpponentData, float> t1, Tuple<OpponentData, float> t2)
                {
                    return t2.Item2.CompareTo(t1.Item2);
                });
                int expectedPlayerRacePosition = 1;
                OpponentData opponentAheadExpected = null;
                foreach (Tuple<OpponentData, float> opponentAndDistance in totalRaceDistances)
                {                    
                    if (opponentAndDistance.Item2 < positionAndTotalDistanceForTimeLoss.Item2)
                    {
                        // we'll be ahead of this car, and he's the car who'll have travelled the furthest
                        // of all the cars we'll be ahead of
                        opponentAheadExpected = opponentAndDistance.Item1;
                        break;
                    }
                    expectedPlayerRacePosition++;
                }
                if (opponentAheadExpected == null)
                {
                    // we're going to be last
                    opponentAheadExpected = totalRaceDistances.Last().Item1;
                }

                if (opponentAheadExpected != null)
                {
                    Console.WriteLine("Derived expected race position P" + expectedPlayerRacePosition + " from opponent " +
                        opponentAheadExpected.DriverRawName + " who's currently in P" + opponentAheadExpected.ClassPosition + 
                        ". Completed laps player: " + lapsCompleted + " opponent: " +
                        opponentAheadExpected.CompletedLaps + " track position player: " + currentDistanceRoundTrack + " opponent: " +
                        opponentAheadExpected.DistanceRoundTrack);
                }

                printS1Positions = true;

                // sort each of these by the delta, smallest first
                opponentsAhead.Sort(delegate(OpponentPositionAtPlayerPitExit d1, OpponentPositionAtPlayerPitExit d2)
                {
                    return d1.predictedDistanceGap.CompareTo(d2.predictedDistanceGap);
                });
                opponentsBehind.Sort(delegate(OpponentPositionAtPlayerPitExit d1, OpponentPositionAtPlayerPitExit d2)
                {
                    return d1.predictedDistanceGap.CompareTo(d2.predictedDistanceGap);
                });

                // phew... now we know who will be in front and who will be behind when we emerge from the pitlane. We also
                // now the expected distance between us and them (in metres) when we emerge.
                return new Strategy.PostPitRacePosition(opponentsAhead, opponentsBehind, expectedPlayerRacePosition);
            }
            else
            {
                Console.WriteLine("Unable to get pitstop prediction - no pitstop time loss data available");
            }
            // oh dear
            return null;
        }

        /**
         * value object containing car class data, predicted post-pit distance gap, and opponent
         */
        public class OpponentPositionAtPlayerPitExit
        {
            // always positive:
            public float predictedDistanceGap;
            public Boolean isPlayerClass;
            public String carClassId;
            public OpponentData opponentData;
            public OpponentPositionAtPlayerPitExit(float predictedDistanceGap, Boolean isPlayerClass, String carClassId, OpponentData opponentData)
            {
                this.predictedDistanceGap = predictedDistanceGap;
                this.isPlayerClass = isPlayerClass;
                this.carClassId = carClassId;
                this.opponentData = opponentData;
            }
        }

        /**
         * Helpfully organised post-pit position data.
         */
        public class PostPitRacePosition
        {
            public List<OpponentPositionAtPlayerPitExit> opponentsFrontAfterStop;
            public List<OpponentPositionAtPlayerPitExit> opponentsBehindAfterStop;
            public int expectedRacePosition;

            public OpponentPositionAtPlayerPitExit opponentClosestAheadAfterStop = null;
            public OpponentPositionAtPlayerPitExit opponentClosestBehindAfterStop = null;

            // this is based on any class
            public float numCarsVeryCloseBehindAfterStop = 0;
            public float numCarsVeryCloseAheadAfterStop = 0;

            public float numCarsCloseBehindAfterStop = 0;
            public float numCarsCloseAheadAfterStop = 0;

            public void print()
            {
                Console.WriteLine("Pitstop predition: position " + expectedRacePosition + ", " + numCarsVeryCloseAheadAfterStop + " cars very close ahead, " + 
                    numCarsVeryCloseBehindAfterStop + " cars very close behind, " + numCarsCloseAheadAfterStop + " cars close ahead, " + 
                    numCarsCloseBehindAfterStop + " cars close behind");
                if (opponentClosestAheadAfterStop != null)
                {
                    Console.WriteLine("opponent " + opponentClosestAheadAfterStop.opponentData.DriverRawName + " will be " + opponentClosestAheadAfterStop.predictedDistanceGap + " metres ahead");
                }
                if (opponentClosestBehindAfterStop != null)
                {
                    Console.WriteLine("opponent " + opponentClosestBehindAfterStop.opponentData.DriverRawName + " will be " + opponentClosestBehindAfterStop.predictedDistanceGap + " metres behind");
                }
            }

            public PostPitRacePosition(List<OpponentPositionAtPlayerPitExit> opponentsFrontAfterStop, List<OpponentPositionAtPlayerPitExit> opponentsBehindAfterStop,
                int expectedRacePosition)
            {
                this.expectedRacePosition = expectedRacePosition;
                this.opponentsBehindAfterStop = opponentsBehindAfterStop;
                this.opponentsFrontAfterStop = opponentsFrontAfterStop;

                if (opponentsFrontAfterStop != null && opponentsFrontAfterStop.Count > 0)
                {
                    // note these are sorted by distance (closest first)
                    foreach (OpponentPositionAtPlayerPitExit opponent in opponentsFrontAfterStop)
                    {
                        if (opponent.predictedDistanceGap < Strategy.distanceAheadToBeConsideredVeryClose)
                        {
                            numCarsVeryCloseAheadAfterStop++;
                        }
                        if (opponent.predictedDistanceGap < Strategy.minDistanceAheadToBeConsideredAFewSeconds)
                        {
                            numCarsCloseAheadAfterStop++;
                        }
                        if (opponent.isPlayerClass) 
                        {
                            if (opponentClosestAheadAfterStop == null)
                            {
                                opponentClosestAheadAfterStop = opponent;
                            }
                        }
                    }
                }
                if (opponentsBehindAfterStop != null && opponentsBehindAfterStop.Count > 0)
                {
                    // note these are sorted by distance (closest first)
                    foreach (OpponentPositionAtPlayerPitExit opponent in opponentsBehindAfterStop)
                    {
                        if (opponent.predictedDistanceGap < Strategy.distanceBehindToBeConsideredVeryClose)
                        {
                            numCarsVeryCloseBehindAfterStop++;                            
                        }
                        if (opponent.predictedDistanceGap < Strategy.minDistanceBehindToBeConsideredAFewSeconds)
                        {
                            numCarsCloseBehindAfterStop++;
                        }
                        if (opponent.isPlayerClass) 
                        {
                            if (opponentClosestBehindAfterStop == null)
                            {
                                opponentClosestBehindAfterStop = opponent;
                            }
                        }
                    }
                }
            }

            public Boolean willBeInTraffic()
            {
                return numCarsCloseAheadAfterStop > 0 && numCarsCloseBehindAfterStop > 0;
            }
        }

        private void reportPostPitData(Strategy.PostPitRacePosition postPitData)
        {
            List<MessageFragment> fragments = new List<MessageFragment>();
            if (postPitData != null)
            {
                postPitData.print();

                if (Strategy.persistBenchmarks && !Strategy.naggedAboutBenchmarks && BenchmarkHelper.findPersistedBenchmarkForCombo(-1) == null)
                {
                    Strategy.naggedAboutBenchmarks = true;
                    fragments.Add(MessageFragment.Text(LapCounter.folderNoPitstopEstimatesPosition));
                }
                if (postPitData.willBeInTraffic())
                {
                    fragments.Add(MessageFragment.Text(folderExpectTrafficOnPitExit));
                }
                else if (postPitData.numCarsCloseAheadAfterStop == 0 && postPitData.numCarsVeryCloseBehindAfterStop == 0)
                {
                    fragments.Add(MessageFragment.Text(folderClearTrackOnPitExit));
                }
                if (postPitData.expectedRacePosition != -1)
                {
                    fragments.Add(MessageFragment.Text(folderWeShouldEmergeInPosition));
                    fragments.Add(MessageFragment.Integer(postPitData.expectedRacePosition));
                }
                if (Game.IRACING)
                {
                    // the legacy behaviour here is super weird, it's unclear why we would ever want to watch the closest opponent ahead/behind
                    // AFTER the pit... it is almost always the case that we want to watch the opponents who we are racing. Maybe there's a
                    // situational switch we could do here.
                    Strategy.opponentFrontToWatchForPitting = opponentAheadAtLastSector;
                    Strategy.opponentBehindToWatchForPitting = opponentBehindAtLastSector;
                }
                else
                {
                    if (postPitData.opponentClosestAheadAfterStop != null && !postPitData.opponentClosestAheadAfterStop.opponentData.InPits)
                    {
                        Strategy.opponentFrontToWatchForPitting = postPitData.opponentClosestAheadAfterStop.opponentData.DriverRawName;
                    }
                    if (postPitData.opponentClosestBehindAfterStop != null && !postPitData.opponentClosestBehindAfterStop.opponentData.InPits)
                    {
                        Strategy.opponentBehindToWatchForPitting = postPitData.opponentClosestBehindAfterStop.opponentData.DriverRawName;
                    }
                }
                // figure out what to read here
                if (postPitData.opponentClosestAheadAfterStop != null)
                {
                    Boolean canReadOpponentAhead = AudioPlayer.canReadName(postPitData.opponentClosestAheadAfterStop.opponentData.DriverRawName, false);
                    float gapFront = postPitData.opponentClosestAheadAfterStop.predictedDistanceGap;
                    if (postPitData.opponentClosestBehindAfterStop != null)
                    {
                        Boolean canReadOpponentBehind = AudioPlayer.canReadName(postPitData.opponentClosestBehindAfterStop.opponentData.DriverRawName, false);
                        // can read 2 driver names, so decide which to read (or both)
                        float gapBehind = postPitData.opponentClosestBehindAfterStop.predictedDistanceGap;
                        if (gapFront < distanceAheadToBeConsideredVeryClose)
                        {
                            if (gapBehind < distanceBehindToBeConsideredVeryClose)
                            {
                                // both cars very close
                                fragments.Add(MessageFragment.Text(folderCloseBetween));
                                fragments.Add(canReadOpponentBehind ? MessageFragment.Opponent(postPitData.opponentClosestBehindAfterStop.opponentData) :
                                    MessageFragment.Text(Position.folderStub + postPitData.opponentClosestBehindAfterStop.opponentData.ClassPosition));
                                fragments.Add(MessageFragment.Text(folderAnd));
                                fragments.Add(canReadOpponentAhead ? MessageFragment.Opponent(postPitData.opponentClosestAheadAfterStop.opponentData) :
                                    MessageFragment.Text(Position.folderStub + postPitData.opponentClosestAheadAfterStop.opponentData.ClassPosition));
                            }
                            else
                            {
                                // car in front very close
                                fragments.Add(MessageFragment.Text(folderJustBehind));
                                fragments.Add(canReadOpponentAhead ? MessageFragment.Opponent(postPitData.opponentClosestAheadAfterStop.opponentData) :
                                     MessageFragment.Text(Position.folderStub + postPitData.opponentClosestAheadAfterStop.opponentData.ClassPosition));
                            }
                        }
                        else if (gapBehind < distanceBehindToBeConsideredVeryClose)
                        {
                            // car behind very close
                            fragments.Add(MessageFragment.Text(folderJustAheadOf));
                            fragments.Add(canReadOpponentBehind ? MessageFragment.Opponent(postPitData.opponentClosestBehindAfterStop.opponentData) :
                                    MessageFragment.Text(Position.folderStub + postPitData.opponentClosestBehindAfterStop.opponentData.ClassPosition));
                        }
                        else if (gapFront < minDistanceAheadToBeConsideredAFewSeconds)
                        {
                            if (gapBehind < minDistanceBehindToBeConsideredAFewSeconds)
                            {
                                // both cars quite close. 
                                // Additional check here. "you'll emerge in P4 between P5 and P3" is going to sound particularly shit, so if we have neither name,
                                // don't announce this
                                if (canReadOpponentAhead || canReadOpponentBehind)
                                {
                                    fragments.Add(MessageFragment.Text(folderBetween));
                                    fragments.Add(canReadOpponentBehind ? MessageFragment.Opponent(postPitData.opponentClosestBehindAfterStop.opponentData) :
                                        MessageFragment.Text(Position.folderStub + postPitData.opponentClosestBehindAfterStop.opponentData.ClassPosition));
                                    fragments.Add(MessageFragment.Text(folderAnd));
                                    fragments.Add(canReadOpponentAhead ? MessageFragment.Opponent(postPitData.opponentClosestAheadAfterStop.opponentData) :
                                        MessageFragment.Text(Position.folderStub + postPitData.opponentClosestAheadAfterStop.opponentData.ClassPosition));
                                }
                            }
                            else
                            {
                                // car in front quite close
                                // Only announce this if we can use the name
                                if (canReadOpponentAhead)
                                {
                                    fragments.Add(MessageFragment.Text(folderBehind));
                                    fragments.Add(MessageFragment.Opponent(postPitData.opponentClosestAheadAfterStop.opponentData));
                                }
                            }
                        }
                        else if (gapBehind < minDistanceBehindToBeConsideredAFewSeconds)
                        {
                            // car behind quite close
                            // Only announce this if we can use the name
                            if (canReadOpponentBehind)
                            {
                                fragments.Add(MessageFragment.Text(folderAheadOf));
                                fragments.Add(MessageFragment.Opponent(postPitData.opponentClosestBehindAfterStop.opponentData));
                            }
                        }
                        else if (gapFront < maxDistanceAheadToBeConsideredAFewSeconds)
                        {
                            // car in front a few seconds away
                            fragments.Add(MessageFragment.Text(folderAFewSecondsBehind));
                            fragments.Add(canReadOpponentAhead ? MessageFragment.Opponent(postPitData.opponentClosestAheadAfterStop.opponentData) :
                                    MessageFragment.Text(Position.folderStub + postPitData.opponentClosestAheadAfterStop.opponentData.ClassPosition));
                        }
                        else if (gapBehind < maxDistanceBehindToBeConsideredAFewSeconds)
                        {
                            // car behind a few seconds away
                            fragments.Add(MessageFragment.Text(folderAFewSecondsAheadOf));
                            fragments.Add(canReadOpponentBehind ? MessageFragment.Opponent(postPitData.opponentClosestBehindAfterStop.opponentData) :
                                    MessageFragment.Text(Position.folderStub + postPitData.opponentClosestBehindAfterStop.opponentData.ClassPosition));
                        }
                    }
                    else
                    {
                        // only have a car in front here
                        if (gapFront < distanceAheadToBeConsideredVeryClose)
                        {
                            // car in front very close
                            fragments.Add(MessageFragment.Text(folderJustBehind));
                            fragments.Add(canReadOpponentAhead ? MessageFragment.Opponent(postPitData.opponentClosestAheadAfterStop.opponentData) :
                                 MessageFragment.Text(Position.folderStub + postPitData.opponentClosestAheadAfterStop.opponentData.ClassPosition));
                        }
                        else if (gapFront < minDistanceAheadToBeConsideredAFewSeconds)
                        {
                            // car in front quite close
                            // Only announce this if we have a name
                            if (canReadOpponentAhead)
                            {
                                fragments.Add(MessageFragment.Text(folderBehind));
                                fragments.Add(MessageFragment.Opponent(postPitData.opponentClosestAheadAfterStop.opponentData));
                            }
                        }
                        else if (gapFront < maxDistanceAheadToBeConsideredAFewSeconds)
                        {
                            // car in front a few seconds away
                            fragments.Add(MessageFragment.Text(folderAFewSecondsBehind));
                            fragments.Add(canReadOpponentAhead ? MessageFragment.Opponent(postPitData.opponentClosestAheadAfterStop.opponentData) :
                                    MessageFragment.Text(Position.folderStub + postPitData.opponentClosestAheadAfterStop.opponentData.ClassPosition));
                        }
                    }
                }
                else if (postPitData.opponentClosestBehindAfterStop != null)
                {
                    // only have a car behind here
                    Boolean canReadOpponentBehind = AudioPlayer.canReadName(postPitData.opponentClosestBehindAfterStop.opponentData.DriverRawName, false);
                    // can read 2 driver names, so decide which to read (or both)
                    float gapBehind = postPitData.opponentClosestBehindAfterStop.predictedDistanceGap;
                    if (gapBehind < distanceBehindToBeConsideredVeryClose)
                    {
                        // car behind very close
                        fragments.Add(MessageFragment.Text(folderJustAheadOf));
                        fragments.Add(canReadOpponentBehind ? MessageFragment.Opponent(postPitData.opponentClosestBehindAfterStop.opponentData) :
                                MessageFragment.Text(Position.folderStub + postPitData.opponentClosestBehindAfterStop.opponentData.ClassPosition));
                    }
                    else if (gapBehind < minDistanceBehindToBeConsideredAFewSeconds)
                    {
                        // car behind quite close
                        // Only announce this if we have a name
                        if (canReadOpponentBehind)
                        {
                            fragments.Add(MessageFragment.Text(folderAheadOf));
                            fragments.Add(MessageFragment.Opponent(postPitData.opponentClosestBehindAfterStop.opponentData));
                        }
                    }
                    else if (gapBehind < maxDistanceBehindToBeConsideredAFewSeconds)
                    {
                        // car behind a few seconds away
                        fragments.Add(MessageFragment.Text(folderAFewSecondsAheadOf));
                        fragments.Add(canReadOpponentBehind ? MessageFragment.Opponent(postPitData.opponentClosestBehindAfterStop.opponentData) :
                                MessageFragment.Text(Position.folderStub + postPitData.opponentClosestBehindAfterStop.opponentData.ClassPosition));
                    }
                }
            }
            if (fragments.Count > 0)
            {
                if (pitPositionEstimatesRequested)
                {
                    audioPlayer.playMessageImmediately(new QueuedMessage("pit_stop_position_prediction", 0, messageFragments: fragments));
                }
                else
                {
                    audioPlayer.playMessage(new QueuedMessage("pit_stop_position_prediction", 0, messageFragments: fragments, abstractEvent: this, priority: 10));
                }
            }
            else if (pitPositionEstimatesRequested)
            {
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
            }
        }

        private Boolean enteredLastSector(GameStateData previousGameState, GameStateData currentGameState)
        {
            return currentGameState.SessionData.IsNewSector && currentGameState.SessionData.SectorNumber == sectorCount;
        }

        private Boolean inFinalSector(GameStateData currentGameState)
        {
            if (sectorCount == 2)
            {
                return currentGameState.SessionData.SectorNumber == 2;
            }
            else
            {
                return currentGameState.SessionData.SectorNumber == 3;
            }
        }

        public void respondPracticeStop()
        {
            if (CrewChief.currentGameState == null || CrewChief.currentGameState.SessionData.SessionType != SessionType.Practice &&
                CrewChief.currentGameState.SessionData.SessionType != SessionType.LonePractice)
            {
                // there's no real reason why we can't record the pitstop in a race (e.g. an AI race)
                // but the code is structured to only capture this during test sessions so at least
                // let the player know that we're not doing what they asked.
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
            }
            else
            {
                isTimingPracticeStop = true;
                audioPlayer.playMessageImmediately(new QueuedMessage(folderTimePitstopAcknowledge, 0));
            }
        }

        public void respondRace()
        {            
            if (CrewChief.currentGameState == null || CrewChief.currentGameState.SessionData.TrackDefinition == null)
            {
                Console.WriteLine("No data for pit estimate");
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
            }
            else
            {
                Strategy.playPitPositionEstimates = true;
                pitPositionEstimatesRequested = true;
            }
        }

        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.PLAY_POST_PIT_POSITION_ESTIMATE,
            SpeechCommands.ID.PRACTICE_PIT_STOP,
        };
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            return SpeechCommands.SpeechToCommand(Commands, voiceMessage);
        }

        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID cmd)
        {
            switch (cmd)
            {
                // if voice message is 'practice pitstop' or something, set the boolean flag that makes the
                // trigger-loop calculate the time loss
                case SpeechCommands.ID.PRACTICE_PIT_STOP:
                    respondPracticeStop();
                    break;
                // if the voice message is 'where will I emerge' or something, get the PostPitRacePosition object
                // and report some data from it, then set the playedPitPositionEstimatesForThisLap to true
                case SpeechCommands.ID.PLAY_POST_PIT_POSITION_ESTIMATE:
                    respondRace();
                    break;
            }                
        }

        private static string getTrackNameAndCarClass(GameStateData currentGameState)
        {
            if (currentGameState != null && currentGameState.SessionData.TrackDefinition != null)
            {
                return currentGameState.carClass.getClassIdentifier() + "_" + currentGameState.SessionData.TrackDefinition.name;
            }
            return null;
        }

        public static float getPlayerPitBoxLapDistance(GameStateData currentGameState)
        {
            string trackNameAndCarClass = getTrackNameAndCarClass(currentGameState);
            if (trackNameAndCarClass != null && trackNameAndCarClass == Strategy.trackAndCarNameForPitBoxPositionData)
            {
                return Strategy.playerPitBoxLapDistance;
            }
            return -1;
        }
        
        public static float[] getPlayerPitBoxLocation(GameStateData currentGameState)
        {
            string trackNameAndCarClass = getTrackNameAndCarClass(currentGameState);
            if (trackNameAndCarClass != null && trackNameAndCarClass == Strategy.trackAndCarNameForPitBoxPositionData)
            {
                return Strategy.playerPitBoxLocation;
            }
            return null;
        }

        // RF1 calls this during prac or qual when we do our practice stop
        private static void setPlayerPitLocationData(GameStateData currentGameState)
        {
            string trackNameAndCarClass = getTrackNameAndCarClass(currentGameState);
            bool newTrackNameAndCarClass = trackNameAndCarClass != Strategy.trackAndCarNameForPitBoxPositionData;
            if (trackNameAndCarClass != null)
            {
                if (currentGameState.PositionAndMotionData.DistanceRoundTrack > 0)
                {
                    Strategy.trackAndCarNameForPitBoxPositionData = trackNameAndCarClass;
                    Strategy.playerPitBoxLapDistance = currentGameState.PositionAndMotionData.DistanceRoundTrack;
                }
                if (currentGameState.PositionAndMotionData.WorldPosition != null
                    && locationIsValid(currentGameState.PositionAndMotionData.WorldPosition))
                {
                    Strategy.trackAndCarNameForPitBoxPositionData = trackNameAndCarClass;
                    Strategy.playerPitBoxLocation[0] = currentGameState.PositionAndMotionData.WorldPosition[0];
                    Strategy.playerPitBoxLocation[1] = currentGameState.PositionAndMotionData.WorldPosition[2];
                }
            }
            if (trackNameAndCarClass == null || newTrackNameAndCarClass)
            {
                Strategy.opponentKeysSharingPitLocation.Clear();
            }
        }

        private static bool locationIsValid(float[] location)
        {
            return location != null && !(location[0] == 0 && location[1] == 0);
        }
        
        // only checks the position - call this when we know the opponent has just stopped in the pit lane
        public static void checkIfOpponentSharesPlayerPitBox(string opponentKey, float opponentLapDistance, float[] opponentWorldLocation)
        {
            if (Strategy.trackAndCarNameForPitBoxPositionData == null || !rf1WarnAboutOpponentsSharingBox)
            {
                // no data for the player's box or feature disabled, so no point in adding any data for the opponent
                return;
            }
            if ((Strategy.playerPitBoxLapDistance > 0 && opponentLapDistance > 0 && Math.Abs(opponentLapDistance - Strategy.playerPitBoxLapDistance) < 3)
                || (locationIsValid(Strategy.playerPitBoxLocation) && locationIsValid(opponentWorldLocation)
                     && Math.Abs(Strategy.playerPitBoxLocation[0] - opponentWorldLocation[0]) < 3
                     && Math.Abs(Strategy.playerPitBoxLocation[1] - opponentWorldLocation[1]) < 3))
            {
                Console.WriteLine("Opponent " + opponentKey + " appears to be sharing our pitbox at distance " + opponentLapDistance + " world location " + String.Join(",", opponentWorldLocation));
                Strategy.opponentKeysSharingPitLocation.Add(opponentKey);
            }
        }
        
        // only call this every few seconds
        private static HashSet<string> getOpponentKeysBlockingBox(GameStateData currentGameState)
        {
            HashSet<string> blocking = new HashSet<string>();
            if (currentGameState.SessionData.SessionType == SessionType.Race && getTrackNameAndCarClass(currentGameState) == Strategy.trackAndCarNameForPitBoxPositionData)
            {
                foreach (string opponentKey in Strategy.opponentKeysSharingPitLocation)
                {
                    OpponentData opponentData;
                    if (currentGameState.OpponentData.TryGetValue(opponentKey, out opponentData) && opponentData.InPits)
                    {
                        // this opponent shares our box, he's pitting so report it if we haven't already
                        blocking.Add(opponentKey);
                    }
                }
            }
            return blocking;
        }
    }

    // code to persist and load benchmark data
    class PersistedBenchmark
    {
        public String game { get; set; }
        public String carClassId { get; set; }
        public String trackName { get; set; }
        public float timeLoss { get; set; }

        // added in later schemas, must be prepared to read default values
        public int fuelLitres { get; set; }
        public string carName { get; set; }
        public ConditionsMonitor.TrackWetness conditions { get; set; }

        public Boolean isForThisCombo(String game, String carClassId, String trackName)
        {
            return this.game != null && this.game.Equals(game) &&
                this.carClassId != null && this.carClassId.Equals(carClassId) &&
                this.trackName != null && this.trackName.Equals(trackName);
        }

        public Boolean isForThisExactCombo(String game, String carName, String trackName)
        {
            return this.game != null && this.game.Equals(game) &&
                this.carName != null && this.carName.Equals(carName) &&
                this.trackName != null && this.trackName.Equals(trackName);
        }

    }

    class BenchmarkHelper
    {
        private static String fullPath = DataFiles.pit_benchmarks;
        private static List<PersistedBenchmark> persistedBenchmarks;

        // a cache of all the benchmarks that are relevant for the current session. This should be accessed through
        // findPersistedBenchmarkForCombo.
        private volatile static List<PersistedBenchmark> benchmarksForThisCombo = null;

        private static void saveBenchmarks()
        {
            try
            {
                string jsonRaw = JsonConvert.SerializeObject(BenchmarkHelper.persistedBenchmarks, Formatting.Indented);
                if (BenchmarkHelper.persistedBenchmarks.Count > 0 && (!File.Exists(fullPath) || !jsonRaw.Equals(File.ReadAllText(fullPath))))
                {
                    Console.WriteLine("Saving " + BenchmarkHelper.persistedBenchmarks.Count + " pit benchmarks");
                    File.WriteAllText(fullPath, jsonRaw);
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Unable to save pit benchmark : ");
            }
        }

        public static void loadBenchmarks()
        {
            BenchmarkHelper.persistedBenchmarks = null;
            BenchmarkHelper.benchmarksForThisCombo = null;
            if (File.Exists(fullPath))
            {
                try
                {
                    BenchmarkHelper.persistedBenchmarks = JsonConvert.DeserializeObject<List<PersistedBenchmark>>(File.ReadAllText(fullPath));
                    if (BenchmarkHelper.persistedBenchmarks != null)
                    {
                        Console.WriteLine("Loaded " + BenchmarkHelper.persistedBenchmarks.Count() + " saved pitstop time loss benchmarks");
                        return;
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine("Unable to load existing benchmarkdata");
                }
            }
            BenchmarkHelper.persistedBenchmarks = new List<PersistedBenchmark>();
        }

        // the estimatedFuel allows us to have multiple benchmarks that we can query to find
        // the one that is the most accurate for the given stop. Of course fuel isn't the
        // only thing that can be done in a pitstop but (after damage repair) is usually
        // the most dominant part of a green flag pitstop (stock car drivers often take alternating
        // left / right tyres, with most other series getting a free tyre change on a full tank).
        //
        // If fuel is not relevant then it may be left out and the most recent persisted benchmark
        // will be used.
        public static PersistedBenchmark findPersistedBenchmarkForCombo(float estimatedFuelLitres)
        {
            if (CrewChief.currentGameState == null)
            {
                return null;
            }
            if (benchmarksForThisCombo == null)
            {
                // the only way this can be null is if a new Strategy instance was created, which means we need to recreate the cache
                BenchmarkHelper.benchmarksForThisCombo = new List<PersistedBenchmark>();

                var game = CrewChief.gameDefinition.gameEnum.ToString();
                var carClassId = CrewChief.currentGameState.carClass.getClassIdentifier();
                var carName = CrewChief.currentGameState.carName;
                var trackName = CrewChief.currentGameState.SessionData.TrackDefinition.name;
                // first try for the exact car
                foreach (PersistedBenchmark persistedBenchmark in BenchmarkHelper.persistedBenchmarks)
                {
                    if (persistedBenchmark.isForThisExactCombo(game, carName, trackName))
                    {
                        BenchmarkHelper.benchmarksForThisCombo.Add(persistedBenchmark);
                    }
                }
                // then fall back to the car class, which won't be ideal for series with a big fuel difference
                if (BenchmarkHelper.benchmarksForThisCombo.Count == 0)
                {
                    foreach (PersistedBenchmark persistedBenchmark in BenchmarkHelper.persistedBenchmarks)
                    {
                        if (persistedBenchmark.isForThisCombo(game, carClassId, trackName))
                        {
                            BenchmarkHelper.benchmarksForThisCombo.Add(persistedBenchmark);
                        }
                    }
                }
                Console.WriteLine("Found " + benchmarksForThisCombo.Count + " persisted benchmark(s) for car / track / game combo");
            }

            // now we can return the one that is the most relevant
            if (BenchmarkHelper.benchmarksForThisCombo.Count < 1)
            {
                return null;
            }
            else if (estimatedFuelLitres < 0 || estimatedFuelLitres == int.MaxValue)
            {
                // the quickest one
                PersistedBenchmark quickest = null;
                foreach (var b in BenchmarkHelper.benchmarksForThisCombo)
                {
                    if (quickest == null || b.timeLoss < quickest.timeLoss)
                    {
                        quickest = b;
                    }
                }
                return quickest;
            }
            else
            {
                // there is an opportunity here to interpolate between pitstops, but for now
                // we just pick the benchmark with the smallest absolute difference in fuel.
                float nearestFuelDiff = -1;
                PersistedBenchmark nearest = null;

                foreach (var b in BenchmarkHelper.benchmarksForThisCombo)
                {
                    float fuelDiff = Math.Abs(estimatedFuelLitres - b.fuelLitres);
                    if (nearest == null || fuelDiff < nearestFuelDiff)
                    {
                        nearestFuelDiff = fuelDiff;
                        nearest = b;
                    }
                }
                Console.Write($"pit estimate differs from the benchmark by {nearestFuelDiff} litres");
                return nearest;
            }
        }

        public static void updatePersistedBenchmark(float timeLoss, float fuelLitres, ConditionsMonitor.TrackWetness conditions)
        {
            var game = CrewChief.gameDefinition.gameEnum.ToString();
            var carClassId = CrewChief.currentGameState.carClass.getClassIdentifier();
            var trackName = CrewChief.currentGameState.SessionData.TrackDefinition.name;
            var carName = CrewChief.currentGameState.carName;

            Console.WriteLine("Saving new time loss benchmark");
            PersistedBenchmark newBenchmark = new PersistedBenchmark();
            newBenchmark.game = game;
            newBenchmark.carClassId = carClassId;
            newBenchmark.trackName = trackName;
            newBenchmark.timeLoss = timeLoss;
            newBenchmark.conditions = conditions;
            if (fuelLitres < 0)
            {
                fuelLitres = 0;
            }
            newBenchmark.fuelLitres = (int)Math.Ceiling(fuelLitres);
            newBenchmark.carName = carName;

            // we might want to think about truncating the DB, but since this is always a result of a user request and the data
            // is small we might want to just allow the user to manually curate their data. Last thing we want to do is delete
            // data that they wanted to keep.
            BenchmarkHelper.persistedBenchmarks.Add(newBenchmark);
            if (BenchmarkHelper.benchmarksForThisCombo != null)
            {
                BenchmarkHelper.benchmarksForThisCombo.Add(newBenchmark);
            }
            saveBenchmarks();
        }
    }
}
