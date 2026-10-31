using CrewChiefV4.ACC.accData;
using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using System;
using System.Collections.Generic;
using System.Linq;

/**
 * Maps memory mapped file to a local game-agnostic representation.
 */

namespace CrewChiefV4.ACC
{
    public class ACCGameStateMapper : GameStateMapper
    {
        public static Boolean versionChecked = false;
        public static int numberOfSectorsOnTrack = 3;

        private class AcTyres
        {
            public List<CornerData.EnumWithThresholds> tyreWearThresholdsForAC = new List<CornerData.EnumWithThresholds>();
            public List<CornerData.EnumWithThresholds> tyreTempThresholdsForAC = new List<CornerData.EnumWithThresholds>();
            public float tyreWearMinimumValue;

            public AcTyres(List<CornerData.EnumWithThresholds> tyreWearThresholds, List<CornerData.EnumWithThresholds> tyreTempThresholds, float tyreWearMinimum)
            {
                tyreWearThresholdsForAC = tyreWearThresholds;
                tyreTempThresholdsForAC = tyreTempThresholds;
                tyreWearMinimumValue = tyreWearMinimum;
            }
        }

        List<CornerData.EnumWithThresholds> tyreTempThresholds = new List<CornerData.EnumWithThresholds>();
        private static Dictionary<string, AcTyres> acTyres = new Dictionary<string, AcTyres>();

        // these are set when we start a new session, from the car name / class
        private TyreType defaultTyreTypeForPlayersCar = TyreType.Unknown_Race;

        private float[] loggedSectorStart = new float[] { -1f, -1f };

        private List<CornerData.EnumWithThresholds> brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(CarData.getCarClassFromEnum(CarData.CarClassEnum.GT3));

        // next track conditions sample due after:
        private DateTime nextConditionsSampleDue = DateTime.MinValue;

        private List<CornerData.EnumWithThresholds> suspensionDamageThresholds = new List<CornerData.EnumWithThresholds>();

        private float trivialSuspensionDamageThreshold = 0.03f;
        private float minorSuspensionDamageThreshold = 0.08f;
        private float severeSuspensionDamageThreshold = 0.25f;
        private float destroyedSuspensionDamageThreshold = 0.60f;

        private float trivialEngineDamageThreshold = 900.0f;
        private float minorEngineDamageThreshold = 600.0f;
        private float severeEngineDamageThreshold = 350.0f;
        private float destroyedEngineDamageThreshold = 25.0f;

        private float trivialAeroDamageThreshold = 20.0f;
        private float minorAeroDamageThreshold = 40.0f;
        private float severeAeroDamageThreshold = 130.0f;
        private float destroyedAeroDamageThreshold = 250.0f;

        private AC_SESSION_TYPE sessionTypeOnPreviousTick = AC_SESSION_TYPE.AC_UNKNOWN;
        private DateTime ignoreUnknownSessionTypeUntil = DateTime.MinValue;
        private Boolean waitingForUnknownSessionTypeToSettle = false;

        private DateTime startedWaitingForValidGridDataAt = DateTime.MinValue;
        private Boolean waitingForValidFormationCarData = false;

        private Dictionary<string, int> opponentDisconnectionCounter = new Dictionary<string, int>();

        // workaround for shit sector flag data
        TimeSpan timeForYellowToGreenToSettle = TimeSpan.FromSeconds(3);
        TimeSpan timeForGreenToYellowToSettle = TimeSpan.FromSeconds(2);
        TimeSpan timeForYellowToYellowToSettle = TimeSpan.FromSeconds(4);
        private DateTime pendingChangesCreatedTime = DateTime.MinValue;
        private int[] acceptedYellowSectors = new int[] { 0, 0, 0 };
        private int[] pendingYellowSectors = new int[] { 0, 0, 0 };
        private bool hasPendingChanges = false;

        // we have the player's sector number but not the opponents, so derive the sector points as we go
        private float[] sectorSplinePointsFromGame = new float[] {0, -1, -1 };

        private bool getReadyTriggeredForThisSession = false;

        private void updateFlagSectors(int sector1, int sector2, int sector3, DateTime now)
        {
            bool isYellow = sector1 == 1 || sector2 == 1 || sector3 == 1;
            bool wasYellow = acceptedYellowSectors[0] == 1 || acceptedYellowSectors[1] == 1 || acceptedYellowSectors[2] == 1;
            if (sector1 != acceptedYellowSectors[0] || sector2 != acceptedYellowSectors[1] || sector3 != acceptedYellowSectors[2])
            {
                // some changes, only accept them if they've been stable for a while. Here we use different settling times
                // depending on the type of change - we want to report green -> yellow fairly quickly, yellow -> green might be
                // noise in the data so wait a bit longer, yellow -> yellow (change of sector) is often just noise
                if (!hasPendingChanges ||
                    sector1 != pendingYellowSectors[0] || sector2 != pendingYellowSectors[1] || sector3 != pendingYellowSectors[2])
                {
                    // update the pending changes and start the timer ticking
                    // Console.WriteLine("new sector flags at " + now.ToLongTimeString() + ": " + sector1 + " " + sector2 + " " + sector3);
                    pendingYellowSectors[0] = sector1; pendingYellowSectors[1] = sector2; pendingYellowSectors[2] = sector3;
                    pendingChangesCreatedTime = now;
                    hasPendingChanges = true;
                }
                else if (hasPendingChanges)
                {
                    // our pending changes are still valid, if they're old enough apply them
                    TimeSpan settleTime = isYellow && !wasYellow ? timeForGreenToYellowToSettle : isYellow && wasYellow ? timeForYellowToYellowToSettle : timeForYellowToGreenToSettle;
                    if (now > pendingChangesCreatedTime.Add(settleTime))
                    {
                        Console.WriteLine("accepted sector flags at " + now.ToLongTimeString() + ": " + sector1 + " " + sector2 + " " + sector3);
                        acceptedYellowSectors[0] = sector1; acceptedYellowSectors[1] = sector2; acceptedYellowSectors[2] = sector3;
                        hasPendingChanges = false;
                    }
                }
            }
            else
            {
                // the game agrees with our accepted state so reset the timer and pending changes
                hasPendingChanges = false;
                pendingChangesCreatedTime = DateTime.MinValue;
            }
        }
        
        public static float clockMultiplierGuess = 1.0f;
        private DateTime realTimeAtLastClockMultiplierGuess = DateTime.MinValue;
        private DateTime nextClockGuessDue = DateTime.MinValue;
        private float clockAtLastClockMultiplierGuess = -1f;
        private void updateClockMultiplierGuess(float currentClock, DateTime now)
        {
            if (now > nextClockGuessDue)
            {
                if (clockAtLastClockMultiplierGuess > 0 && realTimeAtLastClockMultiplierGuess != DateTime.MinValue)
                {
                    float clockDiff = currentClock - clockAtLastClockMultiplierGuess;
                    float realtimeDiff = (float) (now - realTimeAtLastClockMultiplierGuess).TotalSeconds;
                    clockMultiplierGuess = clockDiff / realtimeDiff;
                }
                nextClockGuessDue = now.AddSeconds(10);
                realTimeAtLastClockMultiplierGuess = now;
                clockAtLastClockMultiplierGuess = currentClock;
            }
        }

        #region WaYToManyTyres
        public ACCGameStateMapper()
        {
            acTyres.Clear();
            suspensionDamageThresholds.Clear();

            CornerData.EnumWithThresholds suspensionDamageNone = new CornerData.EnumWithThresholds(DamageLevel.NONE, -10000, trivialSuspensionDamageThreshold);
            CornerData.EnumWithThresholds suspensionDamageTrivial = new CornerData.EnumWithThresholds(DamageLevel.TRIVIAL, trivialSuspensionDamageThreshold, minorSuspensionDamageThreshold);
            CornerData.EnumWithThresholds suspensionDamageMinor = new CornerData.EnumWithThresholds(DamageLevel.MINOR, trivialSuspensionDamageThreshold, severeSuspensionDamageThreshold);
            CornerData.EnumWithThresholds suspensionDamageMajor = new CornerData.EnumWithThresholds(DamageLevel.MAJOR, severeSuspensionDamageThreshold, destroyedSuspensionDamageThreshold);
            CornerData.EnumWithThresholds suspensionDamageDestroyed = new CornerData.EnumWithThresholds(DamageLevel.DESTROYED, destroyedSuspensionDamageThreshold, 10000);
            suspensionDamageThresholds.Add(suspensionDamageNone);
            suspensionDamageThresholds.Add(suspensionDamageTrivial);
            suspensionDamageThresholds.Add(suspensionDamageMinor);
            suspensionDamageThresholds.Add(suspensionDamageMajor);
            suspensionDamageThresholds.Add(suspensionDamageDestroyed);

            //GTE Classes
            List<CornerData.EnumWithThresholds> tyreWearThresholdsSlickHard = new List<CornerData.EnumWithThresholds>();
            tyreWearThresholdsSlickHard.Add(new CornerData.EnumWithThresholds(TyreCondition.NEW, -10000f, 0.000f));
            tyreWearThresholdsSlickHard.Add(new CornerData.EnumWithThresholds(TyreCondition.SCRUBBED, 0.000f, 14.96601f));
            tyreWearThresholdsSlickHard.Add(new CornerData.EnumWithThresholds(TyreCondition.MINOR_WEAR, 14.96601f, 22.68041f));
            tyreWearThresholdsSlickHard.Add(new CornerData.EnumWithThresholds(TyreCondition.MAJOR_WEAR, 22.68041f, 30.55553f));
            tyreWearThresholdsSlickHard.Add(new CornerData.EnumWithThresholds(TyreCondition.WORN_OUT, 30.55553f, 1000f));

            List<CornerData.EnumWithThresholds> tyreTempsThresholdsHardSlick = new List<CornerData.EnumWithThresholds>();
            tyreTempsThresholdsHardSlick.Add(new CornerData.EnumWithThresholds(TyreTemp.COLD, -10000f, 75f));
            tyreTempsThresholdsHardSlick.Add(new CornerData.EnumWithThresholds(TyreTemp.WARM, 75f, 100f));
            tyreTempsThresholdsHardSlick.Add(new CornerData.EnumWithThresholds(TyreTemp.HOT, 100f, 180f));
            tyreTempsThresholdsHardSlick.Add(new CornerData.EnumWithThresholds(TyreTemp.COOKING, 180f, 10000f));
            acTyres.Add("dry_compound", new AcTyres(tyreWearThresholdsSlickHard, tyreTempsThresholdsHardSlick, 88f));
        }
        #endregion

        public override void versionCheck(Object memoryMappedFileStruct)
        {
            //AssettoCorsaShared shared = ((ACCSharedMemoryReader.ACCStructWrapper)memoryMappedFileStruct).data;
            //String currentVersion = shared.acsStatic.smVersion;
            //String currentPluginVersion = getNameFromBytes(shared.acsChief.pluginVersion);
            //if (currentVersion.Length != 0 && currentPluginVersion.Length != 0 && versionChecked == false)
            //{
            //    System.Diagnostics.Debug.WriteLine("Shared Memory Version: " + shared.acsStatic.smVersion);
            //    if (!currentVersion.Equals(expectedVersion, StringComparison.Ordinal))
            //    {
            //        throw new GameDataReadException("Expected shared data version " + expectedVersion + " but got version " + currentVersion);
            //    }
            //    System.Diagnostics.Debug.WriteLine("Plugin Version: " + currentPluginVersion);
            //    if (!currentPluginVersion.Equals(expectedPluginVersion, StringComparison.Ordinal))
            //    {

            //        throw new GameDataReadException("Expected python plugin version " + expectedPluginVersion + " but got version " + currentPluginVersion);
            //    }
            versionChecked = true;
            //}
        }

        public static OpponentData getOpponentForName(GameStateData gameState, String nameToFind)
        {
            if (gameState.OpponentData == null || gameState.OpponentData.Count == 0 || nameToFind == null || nameToFind.Length == 0)
            {
                return null;
            }

            OpponentData od = null;
            if (gameState.OpponentData.TryGetValue(nameToFind, out od))
            {
                return od;
            }
            return null;
        }

        public override GameStateData mapToGameStateData(Object memoryMappedFileStruct, GameStateData previousGameState)
        {
            ACCSharedMemoryReader.ACCStructWrapper wrapper = (ACCSharedMemoryReader.ACCStructWrapper)memoryMappedFileStruct;
            GameStateData currentGameState = new GameStateData(wrapper.ticksWhenRead);
            ACCShared shared = wrapper.data;

            if (shared == null || shared.accChief == null)
            {
                return null;
            }

            // If this is empty then it is because we haven't loaded the players car yet
            if (shared.accChief.vehicle.Length == 0)
            {
                // no participant data
                return null;
            }
            accVehicleInfo playerVehicle = shared.accChief.vehicle[0];

            if (String.IsNullOrEmpty(playerVehicle.driverName))
                return null;

            AC_STATUS status = shared.accGraphic.status;
            if (status == AC_STATUS.AC_REPLAY)
            {
                CrewChief.trackName = shared.accStatic.track + ":" + shared.accStatic.NOT_SET_trackConfiguration;
                CrewChief.carClass = CarData.getCarClassForClassNameOrCarName(playerVehicle.carModel).carClassEnum;
                CrewChief.viewingReplay = true;
                CrewChief.distanceRoundTrack = spLineLengthToDistanceRoundTrack(shared.accChief.trackLength, playerVehicle.spLineLength);
            }

            if (status == AC_STATUS.AC_REPLAY || status == AC_STATUS.AC_OFF)
            {
                return previousGameState;
            }
            // note this doesn't tick in hotlap / hotstint and during Formation phase
            currentGameState.AccGameClock = shared.accGraphic.Clock;

            Boolean isOnline = shared.accStatic.isOnline == 1;
            Boolean isSinglePlayerPracticeSession = shared.accChief.vehicle.Length == 1 && !isOnline && shared.accGraphic.session == AC_SESSION_TYPE.AC_PRACTICE;
            float distanceRoundTrack = spLineLengthToDistanceRoundTrack(shared.accChief.trackLength, playerVehicle.spLineLength);

            currentGameState.SessionData.TrackDefinition = TrackData.getTrackDefinition(shared.accStatic.track + ":" + shared.accStatic.NOT_SET_trackConfiguration, shared.accChief.trackLength);

            AdditionalDataProvider.validate(playerVehicle.driverName);
            AC_SESSION_TYPE sessionTypeAsSentByGame = shared.accGraphic.session;

            SessionPhase lastSessionPhase = SessionPhase.Unavailable;
            SessionType lastSessionType = SessionType.Unavailable;
            float lastSessionRunningTime = 0;
            int lastSessionLapsCompleted = 0;
            TrackDefinition lastSessionTrack = null;
            Boolean lastSessionHasFixedTime = false;
            int lastSessionNumberOfLaps = 0;
            float lastSessionTotalRunTime = 0;
            float lastSessionTimeRemaining = 0;

            updateClockMultiplierGuess(shared.accGraphic.Clock, currentGameState.Now);

            currentGameState.SessionData.EventIndex = shared.accGraphic.sessionIndex;
            if (previousGameState != null)
            {
                lastSessionPhase = previousGameState.SessionData.SessionPhase;
                lastSessionType = previousGameState.SessionData.SessionType;
                lastSessionRunningTime = previousGameState.SessionData.SessionRunningTime;
                lastSessionHasFixedTime = previousGameState.SessionData.SessionHasFixedTime;
                lastSessionTrack = previousGameState.SessionData.TrackDefinition;
                lastSessionLapsCompleted = previousGameState.SessionData.CompletedLaps;
                lastSessionNumberOfLaps = previousGameState.SessionData.SessionNumberOfLaps;
                lastSessionTotalRunTime = previousGameState.SessionData.SessionTotalRunTime;
                lastSessionTimeRemaining = previousGameState.SessionData.SessionTimeRemaining;
                currentGameState.carClass = previousGameState.carClass;
                currentGameState.carName = previousGameState.carName;

                currentGameState.SessionData.PlayerLapTimeSessionBest = previousGameState.SessionData.PlayerLapTimeSessionBest;
                currentGameState.SessionData.PlayerLapTimeSessionBestPrevious = previousGameState.SessionData.PlayerLapTimeSessionBestPrevious;
                currentGameState.SessionData.OpponentsLapTimeSessionBestOverall = previousGameState.SessionData.OpponentsLapTimeSessionBestOverall;
                currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass = previousGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass;
                currentGameState.SessionData.OverallSessionBestLapTime = previousGameState.SessionData.OverallSessionBestLapTime;
                currentGameState.SessionData.PlayerClassSessionBestLapTime = previousGameState.SessionData.PlayerClassSessionBestLapTime;
                currentGameState.SessionData.CurrentLapIsValid = previousGameState.SessionData.CurrentLapIsValid;
                currentGameState.SessionData.PreviousLapWasValid = previousGameState.SessionData.PreviousLapWasValid;
                currentGameState.readLandmarksForThisLap = previousGameState.readLandmarksForThisLap;

                // preserve the tyre set usage data from the previous tick unless we've decremented the event index counter.
                // Note that this will retain usage if we restart a session, which is definitely *not* what we want but will do for now
                if (currentGameState.SessionData.EventIndex >= previousGameState.SessionData.EventIndex)
                {
                    currentGameState.TyreData.lapsPerSet = previousGameState.TyreData.lapsPerSet;
                }
                else
                {
                    Console.WriteLine("Resetting tyre tracking data");
                }
            }

            if (currentGameState.carClass.carClassEnum == CarData.CarClassEnum.UNKNOWN_RACE)
            {
                CarData.CarClass newClass = CarData.getCarClassForClassNameOrCarName(playerVehicle.carModel);
                CarData.CLASS_ID = shared.accStatic.carModel;
                if (!CarData.IsCarClassEqual(newClass, currentGameState.carClass, true))
                {
                    currentGameState.carClass = newClass;
                    GlobalBehaviourSettings.UpdateFromCarClass(currentGameState.carClass);
                    System.Diagnostics.Debug.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier());
                    brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(currentGameState.carClass);
                    // no tyre data in the block so get the default tyre types for this car
                    defaultTyreTypeForPlayersCar = CarData.getDefaultTyreType(currentGameState.carClass);
                }
            }

            // this is the corrected AC_SESSION_TYPE, ignoring occasional shit data. I wonder what else is set to "shit" in shared memory every few seconds?
            AC_SESSION_TYPE sessionType;
            if (sessionTypeAsSentByGame != AC_SESSION_TYPE.AC_UNKNOWN)
            {
                waitingForUnknownSessionTypeToSettle = false;
                sessionTypeOnPreviousTick = sessionTypeAsSentByGame;
                sessionType = sessionTypeAsSentByGame;
            }
            else
            {
                if (!waitingForUnknownSessionTypeToSettle)
                {
                    // transitioned to UNKNOWN session type. ACC sends this semi-randomly in shared memory during a running session, no idea why
                    waitingForUnknownSessionTypeToSettle = true;
                    ignoreUnknownSessionTypeUntil = currentGameState.Now.AddSeconds(1);
                    sessionType = sessionTypeOnPreviousTick;
                }
                else if (currentGameState.Now > ignoreUnknownSessionTypeUntil)
                {
                    // transition to UNKNOWN has settled
                    waitingForUnknownSessionTypeToSettle = false;
                    sessionTypeOnPreviousTick = sessionTypeAsSentByGame;
                    sessionType = sessionTypeAsSentByGame;
                }
                else
                {
                    // transition to UNKNOWN has not settled
                    sessionType = sessionTypeOnPreviousTick;
                }
            }

            currentGameState.SessionData.SessionType = mapToSessionState(sessionType);

            Boolean leaderHasFinished = previousGameState != null && previousGameState.SessionData.LeaderHasFinishedRace;
            currentGameState.SessionData.LeaderHasFinishedRace = leaderHasFinished;

            int numberOfLapsInSession = (int)shared.accGraphic.numberOfLaps;

            float gameSessionTimeLeft = 0.0f;
            if (!Double.IsInfinity(shared.accGraphic.sessionTimeLeft))
            {
                gameSessionTimeLeft = shared.accGraphic.sessionTimeLeft / 1000f;
            }

            float sessionTimeRemaining = -1;
            //if (sessionType != AC_SESSION_TYPE.AC_PRACTICE && (numberOfLapsInSession == 0 || shared.accStatic.isTimedRace == 1))
            if ((numberOfLapsInSession == 0 && shared.accGraphic.sessionTimeLeft != -1) || shared.accStatic.SET_FROM_UDP_isTimedRace == 1)
            {
                currentGameState.SessionData.SessionHasFixedTime = true;
                sessionTimeRemaining = gameSessionTimeLeft;
            }

            Boolean isCountDown = false;
            TimeSpan countDown = TimeSpan.FromSeconds(gameSessionTimeLeft);

            if (sessionType == AC_SESSION_TYPE.AC_RACE || sessionType == AC_SESSION_TYPE.AC_DRIFT || sessionType == AC_SESSION_TYPE.AC_DRAG)
            {
                //Make sure to check for both numberOfLapsInSession and isTimedRace as latter sometimes tells lies!
                if (shared.accStatic.SET_FROM_UDP_isTimedRace == 1 || numberOfLapsInSession == 0)
                {
                    isCountDown = playerVehicle.currentLapTimeMS <= 0 && playerVehicle.lapCount <= 0;
                }
                else
                {
                    isCountDown = countDown.TotalMilliseconds >= 0.25;
                }
            }

            currentGameState.SessionData.IsDisqualified = shared.accGraphic.flag == AC_FLAG_TYPE.AC_BLACK_FLAG;
            bool isInPits = shared.accGraphic.isInPit == 1;
            int lapsCompleted = playerVehicle.lapCount;
            ACCGameStateMapper.numberOfSectorsOnTrack = shared.accStatic.sectorCount;

            Boolean raceFinished = lapsCompleted == numberOfLapsInSession || (previousGameState != null && previousGameState.SessionData.LeaderHasFinishedRace && previousGameState.SessionData.IsNewLap);

            currentGameState.SessionData.SessionPhase = shared.accChief.SessionPhase;
            if (raceFinished && shared.accChief.SessionPhase == SessionPhase.Checkered)
            {
                currentGameState.SessionData.SessionPhase = SessionPhase.Finished;
            }

            // use leaderboard position in non-race sessions, and for the first 10 seconds of a race
            Boolean useLeaderboardPosition = sessionType == AC_SESSION_TYPE.AC_PRACTICE
                || sessionType == AC_SESSION_TYPE.AC_QUALIFY
                || raceFinished
                || (previousGameState != null && previousGameState.SessionData.SessionRunningTime < 10 && shared.accGraphic.session == AC_SESSION_TYPE.AC_RACE);

            currentGameState.SessionData.DriverRawName = playerVehicle.driverName;
            int positionFromGame = useLeaderboardPosition ? playerVehicle.carLeaderboardPosition : playerVehicle.carRealTimeLeaderboardPosition;
            currentGameState.SessionData.OverallPosition = currentGameState.SessionData.SessionType == SessionType.Race && previousGameState != null 
                && currentGameState.SessionData.SessionPhase == SessionPhase.Green
                ? getRacePosition(currentGameState.SessionData.DriverRawName, previousGameState.SessionData.OverallPosition, positionFromGame, currentGameState.Now)
                : positionFromGame;

            currentGameState.SessionData.TrackDefinition = TrackData.getTrackDefinition(shared.accStatic.track + ":" + shared.accStatic.NOT_SET_trackConfiguration, shared.accChief.trackLength, shared.accStatic.sectorCount);
            currentGameState.SessionData.Clock = shared.accGraphic.Clock;
            Boolean sessionOfSameTypeRestarted = ((currentGameState.SessionData.SessionType == SessionType.Race && lastSessionType == SessionType.Race) ||
                (currentGameState.SessionData.SessionType == SessionType.Practice && lastSessionType == SessionType.Practice) ||
                (currentGameState.SessionData.SessionType == SessionType.Qualify && lastSessionType == SessionType.Qualify)) 
                && (previousGameState != null && previousGameState.SessionData.Clock - currentGameState.SessionData.Clock > 2);

            if (sessionOfSameTypeRestarted ||
                (currentGameState.SessionData.SessionType != SessionType.Unavailable &&
                 currentGameState.SessionData.SessionPhase != SessionPhase.Finished &&
                    (lastSessionType != currentGameState.SessionData.SessionType ||
                        lastSessionTrack == null || lastSessionTrack.name != currentGameState.SessionData.TrackDefinition.name ||
                            (currentGameState.SessionData.SessionHasFixedTime && sessionTimeRemaining > lastSessionTimeRemaining + 1))))
            {
                opponentDisconnectionCounter.Clear();
                getReadyTriggeredForThisSession = false;
                ACCSharedMemoryReader.clearSyncData();
                sectorSplinePointsFromGame = new float[] { 0, -1, -1 };
                Console.WriteLine("New session, trigger...");
                if (sessionOfSameTypeRestarted)
                {
                    Console.WriteLine("Session of same type (" + lastSessionType + ") restarted (previous clock = " + previousGameState.SessionData.Clock + " current clock = " + currentGameState.SessionData.Clock + ")");
                }
                if (lastSessionType != currentGameState.SessionData.SessionType)
                {
                    Console.WriteLine("lastSessionType = " + lastSessionType + " currentGameState.SessionData.SessionType = " + currentGameState.SessionData.SessionType);
                }
                else if (lastSessionTrack != currentGameState.SessionData.TrackDefinition)
                {
                    String lastTrackName = lastSessionTrack == null ? "unknown" : lastSessionTrack.name;
                    String currentTrackName = currentGameState.SessionData.TrackDefinition == null ? "unknown" : currentGameState.SessionData.TrackDefinition.name;
                    Console.WriteLine("lastSessionTrack = " + lastTrackName + " currentGameState.SessionData.Track = " + currentTrackName);
                    if (currentGameState.SessionData.TrackDefinition.unknownTrack)
                    {
                        Console.WriteLine("Track is unknown, setting virtual sectors");
                        currentGameState.SessionData.TrackDefinition.setSectorPointsForUnknownTracks();
                    }

                }
                else if (currentGameState.SessionData.SessionHasFixedTime && sessionTimeRemaining > lastSessionTimeRemaining + 1)
                {
                    Console.WriteLine("sessionTimeRemaining = " + sessionTimeRemaining.ToString("0.000") + " lastSessionTimeRemaining = " + lastSessionTimeRemaining.ToString("0.000"));
                }
                waitingForValidFormationCarData = false;
                currentGameState.SessionData.IsNewSession = true;
                currentGameState.SessionData.SessionNumberOfLaps = numberOfLapsInSession;
                currentGameState.SessionData.LeaderHasFinishedRace = false;
                currentGameState.SessionData.SessionStartTime = currentGameState.Now;

                if (currentGameState.SessionData.SessionHasFixedTime)
                {
                    currentGameState.SessionData.SessionTotalRunTime = sessionTimeRemaining;
                    currentGameState.SessionData.SessionTimeRemaining = sessionTimeRemaining;
                    currentGameState.SessionData.ExtraLapsAfterTimedSessionComplete = shared.accStatic.NOT_SET_hasExtraLap == 1 ? 1 : 0;
                    if (currentGameState.SessionData.SessionTotalRunTime == 0)
                    {
                        Console.WriteLine("Setting session run time to 0");
                    }
                    Console.WriteLine("Time in this new session = " + sessionTimeRemaining.ToString("0.000"));
                }
                currentGameState.PitData.IsRefuellingAllowed = true;

                //add carclasses for assetto corsa.
                currentGameState.carClass = CarData.getCarClassForClassNameOrCarName(playerVehicle.carModel);
                currentGameState.carName = playerVehicle.carModel;
                GlobalBehaviourSettings.UpdateFromCarClass(currentGameState.carClass);
                CarData.CLASS_ID = shared.accStatic.carModel;

                if (acTyres.Count > 0 && !acTyres.ContainsKey(shared.accGraphic.tyreCompound))
                {
                    Console.WriteLine("Tyre information is disabled. Player is using unknown Tyre Type " + shared.accGraphic.tyreCompound);
                }
                else
                {
                    Console.WriteLine("Player is using Tyre Type " + shared.accGraphic.tyreCompound);
                }
                currentGameState.TyreData.TyreTypeName = shared.accGraphic.tyreCompound;
                tyreTempThresholds = getTyreTempThresholds(currentGameState.carClass, currentGameState.TyreData.TyreTypeName);
                brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(currentGameState.carClass);
                // no tyre data in the block so get the default tyre types for this car
                defaultTyreTypeForPlayersCar = CarData.getDefaultTyreType(currentGameState.carClass);

                // 0th element is always the player - this is set in the ACCSharedMemoryReader code
                for (int i = 1; i < shared.accChief.vehicle.Length; i++)
                {
                    accVehicleInfo participantStruct = shared.accChief.vehicle[i];
                    if (participantStruct.isConnected == 1)
                    {
                        String participantName = participantStruct.driverName;
                        if (i != 0 && participantName != null && participantName.Length > 0)
                        {
                            CarData.CarClass opponentCarClass = CarData.getCarClassForClassNameOrCarName(participantStruct.carModel);
                            addOpponentForName(participantName, createOpponentData(participantStruct, false, opponentCarClass, shared.accChief.trackLength, false), currentGameState);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier());
                // don't trace the car classes at this point because we might not have been told about them all, giving a misleading debug statement
                // Utilities.TraceEventClass(currentGameState);

                currentGameState.SessionData.PlayerLapTimeSessionBest = -1;
                currentGameState.SessionData.OpponentsLapTimeSessionBestOverall = -1;
                currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass = -1;
                currentGameState.SessionData.OverallSessionBestLapTime = -1;
                currentGameState.SessionData.PlayerClassSessionBestLapTime = -1;
                TrackDataContainer tdc = TrackData.TRACK_LANDMARKS_DATA.getTrackDataForTrackName(currentGameState.SessionData.TrackDefinition.name, shared.accChief.trackLength);
                currentGameState.SessionData.TrackDefinition.trackLandmarks = tdc.trackLandmarks;
                currentGameState.SessionData.TrackDefinition.isOval = false;  // ACC doesn't have oval racing
                currentGameState.SessionData.TrackDefinition.setGapPoints();
                GlobalBehaviourSettings.UpdateFromTrackDefinition(currentGameState.SessionData.TrackDefinition);
                if (previousGameState != null && previousGameState.SessionData.TrackDefinition != null)
                {
                    if (previousGameState.SessionData.TrackDefinition.name.Equals(currentGameState.SessionData.TrackDefinition.name))
                    {
                        if (previousGameState.hardPartsOnTrackData.hardPartsMapped)
                        {
                            currentGameState.hardPartsOnTrackData = previousGameState.hardPartsOnTrackData;
                        }
                    }
                }
                currentGameState.SessionData.DeltaTime = new DeltaTime(currentGameState.SessionData.TrackDefinition.trackLength, distanceRoundTrack,
                    playerVehicle.speedMS, currentGameState.Now);
                currentGameState.SessionData.StartedRaceFromPitLane = currentGameState.SessionData.SessionType == SessionType.Race && playerVehicle.isCarInPitlane == 1;
            }
            else
            {
                if (lastSessionPhase != currentGameState.SessionData.SessionPhase)
                {
                    if (currentGameState.SessionData.SessionPhase == SessionPhase.Green || currentGameState.SessionData.SessionPhase == SessionPhase.FullCourseYellow)
                    {
                        // just gone green, so get the session data.
                        if (currentGameState.SessionData.SessionType == SessionType.Race)
                        {
                            currentGameState.SessionData.JustGoneGreen = true;
                            if (currentGameState.SessionData.SessionHasFixedTime)
                            {
                                currentGameState.SessionData.SessionTotalRunTime = sessionTimeRemaining;
                                currentGameState.SessionData.SessionTimeRemaining = sessionTimeRemaining;
                                if (currentGameState.SessionData.SessionTotalRunTime == 0)
                                {
                                    System.Diagnostics.Debug.WriteLine("Setting session run time to 0");
                                }
                            }
                            currentGameState.SessionData.SessionStartTime = currentGameState.Now;
                            currentGameState.SessionData.SessionNumberOfLaps = numberOfLapsInSession;
                            currentGameState.SessionData.StartedRaceFromPitLane = playerVehicle.isCarInPitlane == 1;
                        }
                        currentGameState.SessionData.LeaderHasFinishedRace = false;
                        sectorSplinePointsFromGame = new float[] { 0, -1, -1 };
                        currentGameState.SessionData.NumCarsOverallAtStartOfSession = shared.accChief.vehicle.Length;
                        currentGameState.SessionData.TrackDefinition = TrackData.getTrackDefinition(shared.accStatic.track + ":" + shared.accStatic.NOT_SET_trackConfiguration, shared.accChief.trackLength, shared.accStatic.sectorCount);
                        if (currentGameState.SessionData.TrackDefinition.unknownTrack)
                        {
                            currentGameState.SessionData.TrackDefinition.setSectorPointsForUnknownTracks();
                        }

                        if (currentGameState.SessionData.TrackDefinition.sectorsOnTrack < shared.accStatic.sectorCount)
                        {
                            Console.WriteLine("Track definition has " + shared.accStatic.sectorCount +
                                " sectors - these will be combined into " + currentGameState.SessionData.TrackDefinition.sectorsOnTrack + " equal sectors");
                        }

                        TrackDataContainer tdc = TrackData.TRACK_LANDMARKS_DATA.getTrackDataForTrackName(currentGameState.SessionData.TrackDefinition.name, shared.accChief.trackLength);
                        currentGameState.SessionData.TrackDefinition.trackLandmarks = tdc.trackLandmarks;
                        currentGameState.SessionData.TrackDefinition.isOval = tdc.isOval;
                        currentGameState.SessionData.TrackDefinition.setGapPoints();
                        GlobalBehaviourSettings.UpdateFromTrackDefinition(currentGameState.SessionData.TrackDefinition);
                        if (previousGameState != null && previousGameState.SessionData.TrackDefinition != null)
                        {
                            if (previousGameState.SessionData.TrackDefinition.name.Equals(currentGameState.SessionData.TrackDefinition.name))
                            {
                                if (previousGameState.hardPartsOnTrackData.hardPartsMapped)
                                {
                                    currentGameState.hardPartsOnTrackData = previousGameState.hardPartsOnTrackData;
                                }
                            }
                        }
                        currentGameState.SessionData.DeltaTime = new DeltaTime(currentGameState.SessionData.TrackDefinition.trackLength, distanceRoundTrack,
                            playerVehicle.speedMS, currentGameState.Now);

                        currentGameState.carClass = CarData.getCarClassForClassNameOrCarName(playerVehicle.carModel);
                        currentGameState.carName = playerVehicle.carModel;
                        CarData.CLASS_ID = shared.accStatic.carModel;
                        GlobalBehaviourSettings.UpdateFromCarClass(currentGameState.carClass);
                        System.Diagnostics.Debug.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier());
                        brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(currentGameState.carClass);
                        // no tyre data in the block so get the default tyre types for this car
                        defaultTyreTypeForPlayersCar = CarData.getDefaultTyreType(currentGameState.carClass);

                        currentGameState.TyreData.TyreTypeName = shared.accGraphic.tyreCompound;
                        tyreTempThresholds = getTyreTempThresholds(currentGameState.carClass, currentGameState.TyreData.TyreTypeName);

                        if (previousGameState != null)
                        {
                            currentGameState.OpponentData = previousGameState.OpponentData;
                            currentGameState.PitData.IsRefuellingAllowed = previousGameState.PitData.IsRefuellingAllowed;
                            if (currentGameState.SessionData.SessionType != SessionType.Race)
                            {
                                currentGameState.SessionData.SessionStartTime = previousGameState.SessionData.SessionStartTime;
                                currentGameState.SessionData.SessionTotalRunTime = previousGameState.SessionData.SessionTotalRunTime;
                                currentGameState.SessionData.SessionTimeRemaining = previousGameState.SessionData.SessionTimeRemaining;
                                currentGameState.SessionData.SessionNumberOfLaps = previousGameState.SessionData.SessionNumberOfLaps;
                            }
                        }

                        System.Diagnostics.Debug.WriteLine("Just gone green, session details...");
                        System.Diagnostics.Debug.WriteLine("SessionType " + currentGameState.SessionData.SessionType);
                        System.Diagnostics.Debug.WriteLine("SessionPhase " + currentGameState.SessionData.SessionPhase);
                        if (previousGameState != null)
                        {
                            System.Diagnostics.Debug.WriteLine("previous SessionPhase " + previousGameState.SessionData.SessionPhase);
                        }
                        System.Diagnostics.Debug.WriteLine("EventIndex " + currentGameState.SessionData.EventIndex);
                        System.Diagnostics.Debug.WriteLine("SessionIteration " + currentGameState.SessionData.SessionIteration);
                        System.Diagnostics.Debug.WriteLine("NumCarsAtStartOfSession " + currentGameState.SessionData.NumCarsOverallAtStartOfSession);
                        System.Diagnostics.Debug.WriteLine("SessionNumberOfLaps " + currentGameState.SessionData.SessionNumberOfLaps);
                        System.Diagnostics.Debug.WriteLine("SessionRunTime " + currentGameState.SessionData.SessionTotalRunTime.ToString("0.000"));
                        System.Diagnostics.Debug.WriteLine("SessionStartTime " + currentGameState.SessionData.SessionStartTime.ToString("0.000"));
                        String trackName = currentGameState.SessionData.TrackDefinition == null ? "unknown" : currentGameState.SessionData.TrackDefinition.name;
                        System.Diagnostics.Debug.WriteLine("TrackName " + trackName);
                    }
                }
                if (!currentGameState.SessionData.JustGoneGreen && previousGameState != null)
                {
                    currentGameState.SessionData.SessionStartTime = previousGameState.SessionData.SessionStartTime;
                    currentGameState.SessionData.SessionTotalRunTime = previousGameState.SessionData.SessionTotalRunTime;
                    currentGameState.SessionData.SessionNumberOfLaps = previousGameState.SessionData.SessionNumberOfLaps;
                    currentGameState.SessionData.ExtraLapsAfterTimedSessionComplete = previousGameState.SessionData.ExtraLapsAfterTimedSessionComplete;
                    currentGameState.SessionData.NumCarsOverallAtStartOfSession = previousGameState.SessionData.NumCarsOverallAtStartOfSession;
                    currentGameState.SessionData.TrackDefinition = previousGameState.SessionData.TrackDefinition;
                    currentGameState.SessionData.SessionIteration = previousGameState.SessionData.SessionIteration;
                    currentGameState.SessionData.PositionAtStartOfCurrentLap = previousGameState.SessionData.PositionAtStartOfCurrentLap;
                    currentGameState.SessionData.SessionStartClassPosition = previousGameState.SessionData.SessionStartClassPosition;
                    currentGameState.SessionData.ClassPositionAtStartOfCurrentLap = previousGameState.SessionData.ClassPositionAtStartOfCurrentLap;
                    currentGameState.SessionData.CompletedLaps = previousGameState.SessionData.CompletedLaps;
                    currentGameState.SessionData.LapCount = previousGameState.SessionData.LapCount;

                    currentGameState.OpponentData = previousGameState.OpponentData;
                    currentGameState.PitData.IsRefuellingAllowed = previousGameState.PitData.IsRefuellingAllowed;
                    currentGameState.PitData.MaxPermittedDistanceOnCurrentTyre = previousGameState.PitData.MaxPermittedDistanceOnCurrentTyre;
                    currentGameState.PitData.MinPermittedDistanceOnCurrentTyre = previousGameState.PitData.MinPermittedDistanceOnCurrentTyre;
                    currentGameState.PitData.OnInLap = previousGameState.PitData.OnInLap;
                    currentGameState.PitData.OnOutLap = previousGameState.PitData.OnOutLap;
                    // the other properties of PitData are updated each tick, and shouldn't be copied over here. Nasty...
                    currentGameState.SessionData.SessionTimesAtEndOfSectors = previousGameState.SessionData.SessionTimesAtEndOfSectors;
                    currentGameState.PenaltiesData.CutTrackWarnings = previousGameState.PenaltiesData.CutTrackWarnings;
                    currentGameState.SessionData.formattedPlayerLapTimes = previousGameState.SessionData.formattedPlayerLapTimes;
                    currentGameState.SessionData.GameTimeAtLastPositionFrontChange = previousGameState.SessionData.GameTimeAtLastPositionFrontChange;
                    currentGameState.SessionData.GameTimeAtLastPositionBehindChange = previousGameState.SessionData.GameTimeAtLastPositionBehindChange;
                    currentGameState.SessionData.OpponentKeyInFront = previousGameState.SessionData.OpponentKeyInFront;
                    currentGameState.SessionData.OpponentKeyBehind = previousGameState.SessionData.OpponentKeyBehind;
                    currentGameState.SessionData.LastSector1Time = previousGameState.SessionData.LastSector1Time;
                    currentGameState.SessionData.LastSector2Time = previousGameState.SessionData.LastSector2Time;
                    currentGameState.SessionData.LastSector3Time = previousGameState.SessionData.LastSector3Time;
                    currentGameState.SessionData.PlayerBestSector1Time = previousGameState.SessionData.PlayerBestSector1Time;
                    currentGameState.SessionData.PlayerBestSector2Time = previousGameState.SessionData.PlayerBestSector2Time;
                    currentGameState.SessionData.PlayerBestSector3Time = previousGameState.SessionData.PlayerBestSector3Time;
                    currentGameState.SessionData.PlayerBestLapSector1Time = previousGameState.SessionData.PlayerBestLapSector1Time;
                    currentGameState.SessionData.PlayerBestLapSector2Time = previousGameState.SessionData.PlayerBestLapSector2Time;
                    currentGameState.SessionData.PlayerBestLapSector3Time = previousGameState.SessionData.PlayerBestLapSector3Time;
                    currentGameState.SessionData.LapTimePrevious = previousGameState.SessionData.LapTimePrevious;
                    currentGameState.Conditions.samples = previousGameState.Conditions.samples;
                    currentGameState.Conditions.CurrentConditions = previousGameState.Conditions.CurrentConditions;
                    currentGameState.SessionData.trackLandmarksTiming = previousGameState.SessionData.trackLandmarksTiming;
                    currentGameState.TyreData.TyreTypeName = previousGameState.TyreData.TyreTypeName;

                    currentGameState.SessionData.DeltaTime = previousGameState.SessionData.DeltaTime;
                    currentGameState.hardPartsOnTrackData = previousGameState.hardPartsOnTrackData;

                    currentGameState.SessionData.PlayerLapData = previousGameState.SessionData.PlayerLapData;

                    currentGameState.TimingData = previousGameState.TimingData;

                    currentGameState.SessionData.JustGoneGreenTime = previousGameState.SessionData.JustGoneGreenTime;
                    currentGameState.SessionData.StartedRaceFromPitLane = previousGameState.SessionData.StartedRaceFromPitLane;

                    currentGameState.FrozenOrderData = previousGameState.FrozenOrderData;
                }

                //------------------- Variable session data ---------------------------

                if (currentGameState.SessionData.SessionHasFixedTime)
                {
                    // when the race timer reaches zero, this will make SessionRunningTime 'stick' at the SessionTotalRunTime
                    if (sessionTimeRemaining == 0)
                    {
                        currentGameState.SessionData.SessionRunningTime = (float)(currentGameState.Now - currentGameState.SessionData.SessionStartTime).TotalSeconds;
                    }
                    else
                    {
                        currentGameState.SessionData.SessionRunningTime = currentGameState.SessionData.SessionTotalRunTime - sessionTimeRemaining;
                    }
                    currentGameState.SessionData.SessionTimeRemaining = sessionTimeRemaining;
                }
                else
                {
                    currentGameState.SessionData.SessionRunningTime = (float)(currentGameState.Now - currentGameState.SessionData.SessionStartTime).TotalSeconds;
                }

                // need to be careful with shared.accGraphic.currentSectorIndex - it's not sync'ed to the player and opponent lap end data
                // so we use the spline position which is sync'ed for sector 1 so this aligns with lap start events
                int sectorIndex = playerVehicle.spLineLength < 0.07 ? 0 : playerVehicle.spLineLength > 0.93 ? 2 : shared.accGraphic.currentSectorIndex;
                currentGameState.SessionData.SectorNumber = sectorIndex + 1;

                // if we've started a new sector and we don't have the sector point, record it from the player's splineLength
                if (playerVehicle.isCarInPitlane == 0
                    && (sectorIndex != 2 || playerVehicle.spLineLength < 0.93) // additional sanity check for sector3 start position
                    && sectorIndex > 0 && sectorSplinePointsFromGame[sectorIndex] == -1 && playerVehicle.spLineLength > sectorSplinePointsFromGame[sectorIndex - 1])
                {
                    sectorSplinePointsFromGame[sectorIndex] = playerVehicle.spLineLength;
                }
                if (currentGameState.SessionData.OverallPosition == 1)
                {
                    currentGameState.SessionData.LeaderSectorNumber = currentGameState.SessionData.SectorNumber;
                }
                currentGameState.SessionData.IsNewSector = previousGameState == null || currentGameState.SessionData.SectorNumber != previousGameState.SessionData.SectorNumber;

                currentGameState.SessionData.LapTimeCurrent = convertMillisecondsToSeconds(playerVehicle.currentLapTimeMS);

                currentGameState.SessionData.CurrentLapIsValid = playerVehicle.currentLapInvalid == 0;
                bool hasCrossedSFLine = currentGameState.SessionData.IsNewSector && currentGameState.SessionData.SectorNumber == 1;
                float lastLapTime = convertMillisecondsToSeconds(playerVehicle.lastLapTimeMS);
                currentGameState.SessionData.IsNewLap = (playerVehicle.isCarInPitlane == 0 && hasCrossedSFLine)
                    || ((lastSessionPhase == SessionPhase.Countdown)
                    && (currentGameState.SessionData.SessionPhase == SessionPhase.Green || currentGameState.SessionData.SessionPhase == SessionPhase.FullCourseYellow));

                currentGameState.TyreData.fittedSet = shared.accGraphic.currentTyreSet - 1; // 1-indexed
                if (currentGameState.SessionData.IsNewLap)
                {
                    currentGameState.TyreData.incrementLapsPerSet();
                    currentGameState.SessionData.CurrentLapIsValid = true;
                    Boolean lapWasValid = previousGameState != null && previousGameState.SessionData.CurrentLapIsValid;
                    // invalidate non-race outlaps
                    if (currentGameState.SessionData.SessionType != SessionType.Race && currentGameState.PitData.OnOutLap)
                    {
                        lapWasValid = false;
                    }
                    currentGameState.readLandmarksForThisLap = false;
                    currentGameState.SessionData.IsNewSector = true;
                    currentGameState.SessionData.CompletedLaps = playerVehicle.lapCount;
                    currentGameState.SessionData.LapCount = currentGameState.SessionData.CompletedLaps + 1;

                    currentGameState.SessionData.playerCompleteLapWithProvidedLapTime(currentGameState.SessionData.OverallPosition,
                        currentGameState.SessionData.SessionRunningTime,
                        lastLapTime, lapWasValid,
                        currentGameState.PitData.InPitlane,
                        shared.accChief.rainLevel > 0.05,
                        shared.accPhysics.roadTemp,
                        shared.accPhysics.airTemp,
                        currentGameState.SessionData.SessionHasFixedTime,
                        currentGameState.SessionData.SessionTimeRemaining,
                        ACCGameStateMapper.numberOfSectorsOnTrack,
                        currentGameState.TimingData,
                        (float)playerVehicle.lastSplit1TimeMS / 1000f,
                        (float)playerVehicle.lastSplit2TimeMS / 1000f);
                    currentGameState.SessionData.playerStartNewLap(currentGameState.SessionData.CompletedLaps + 1,
                        currentGameState.SessionData.OverallPosition, currentGameState.PitData.InPitlane, currentGameState.SessionData.SessionRunningTime);
                }

                //Sector
                if (previousGameState != null && currentGameState.SessionData.IsNewSector && !currentGameState.SessionData.IsNewLap &&
                    previousGameState.SessionData.SectorNumber != 0 && currentGameState.SessionData.SessionRunningTime > 10)
                {
                    currentGameState.SessionData.playerAddCumulativeSectorData(
                        previousGameState.SessionData.SectorNumber,
                        currentGameState.SessionData.OverallPosition,
                        currentGameState.SessionData.LapTimeCurrent,
                        currentGameState.SessionData.SessionRunningTime,
                        currentGameState.SessionData.CurrentLapIsValid,
                        shared.accChief.rainLevel > 0.05,
                        shared.accPhysics.roadTemp,
                        shared.accPhysics.airTemp);
                }

                currentGameState.SessionData.Flag = mapToFlagEnum(
                    shared.accGraphic.GlobalChequered == 1,
                    shared.accGraphic.GlobalGreen == 1,
                    shared.accGraphic.GlobalRed == 1,
                    shared.accGraphic.GlobalWhite == 1,
                    shared.accGraphic.GlobalYellow == 1,
                    shared.accGraphic.flag == AC_FLAG_TYPE.AC_BLACK_FLAG,
                    shared.accGraphic.flag == AC_FLAG_TYPE.AC_BLUE_FLAG);
                if (currentGameState.SessionData.Flag == FlagEnum.YELLOW && previousGameState != null && previousGameState.SessionData.Flag != FlagEnum.YELLOW)
                {
                    currentGameState.SessionData.YellowFlagStartTime = currentGameState.Now;
                }
                updateFlagSectors(shared.accGraphic.GlobalYellow1, shared.accGraphic.GlobalYellow2, shared.accGraphic.GlobalYellow3, currentGameState.Now);
                currentGameState.FlagData.sectorFlags[0] = acceptedYellowSectors[0] == 1 ? FlagEnum.YELLOW : FlagEnum.GREEN;
                currentGameState.FlagData.sectorFlags[1] = acceptedYellowSectors[1] == 1 ? FlagEnum.YELLOW : FlagEnum.GREEN;
                currentGameState.FlagData.sectorFlags[2] = acceptedYellowSectors[2] == 1 ? FlagEnum.YELLOW : FlagEnum.GREEN;
                currentGameState.FlagData.isLocalYellow = currentGameState.FlagData.sectorFlags[currentGameState.SessionData.SectorNumber - 1] == FlagEnum.YELLOW;

                // unfortunately the sector yellow data are noisy as hell. If a car goes off in S1, the game reports S1 as yellow mostly, with occasional periods of 
                // a second or so when S1 goes green but S2 is yellow, then back for a few tenths and so on. So like much of the ACC data it's completely unusable horseshit
                // Console.WriteLine("Local yellow = " + currentGameState.FlagData.isLocalYellow + " sector = " + (currentGameState.SessionData.SectorNumber - 1) + " flags " + String.Join(",", currentGameState.FlagData.sectorFlags));
                // stick to improvised flag calling
                currentGameState.FlagData.useImprovisedIncidentCalling = false;

                currentGameState.SessionData.NumCarsOverall = shared.accChief.vehicle.Length;

                /*previousGameState != null && previousGameState.SessionData.IsNewLap == false &&
                    (shared.acsGraphic.completedLaps == previousGameState.SessionData.CompletedLaps + 1 || ((lastSessionPhase == SessionPhase.Countdown)
                    && (currentGameState.SessionData.SessionPhase == SessionPhase.Green || currentGameState.SessionData.SessionPhase == SessionPhase.FullCourseYellow)));
                */

                currentGameState.SessionData.DeltaTime.SetNextDeltaPoint(distanceRoundTrack, currentGameState.SessionData.CompletedLaps, playerVehicle.speedMS, currentGameState.Now, !currentGameState.PitData.InPitlane);

                if (previousGameState != null)
                {
                    String stoppedInLandmark = currentGameState.SessionData.trackLandmarksTiming.updateLandmarkTiming(currentGameState.SessionData.TrackDefinition,
                        currentGameState.SessionData.SessionRunningTime, previousGameState.PositionAndMotionData.DistanceRoundTrack, distanceRoundTrack, playerVehicle.speedMS, currentGameState.carClass);
                    currentGameState.SessionData.stoppedInLandmark = shared.accGraphic.isInPitLane == 1 ? null : stoppedInLandmark;
                    if (currentGameState.SessionData.IsNewLap)
                    {
                        currentGameState.SessionData.trackLandmarksTiming.cancelWaitingForLandmarkEnd();
                    }
                }

                if (currentGameState.SessionData.SessionFastestLapTimeFromGamePlayerClass == -1 ||
                    (playerVehicle.bestLapMS > 0 && currentGameState.SessionData.SessionFastestLapTimeFromGamePlayerClass > convertMillisecondsToSeconds(playerVehicle.bestLapMS)))
                {
                    currentGameState.SessionData.SessionFastestLapTimeFromGamePlayerClass = convertMillisecondsToSeconds(playerVehicle.bestLapMS);
                }

                HashSet<string> driversWhoMayHaveDisconnected = new HashSet<string>();
                if (previousGameState != null)
                {
                    // start with the entire opponent set on the previous tick:
                    driversWhoMayHaveDisconnected.UnionWith(previousGameState.OpponentData.Keys);
                }

                for (int i = 1; i < shared.accChief.vehicle.Length; i++)
                {
                    accVehicleInfo participantStruct = shared.accChief.vehicle[i];

                    String participantName = participantStruct.driverName;
                    OpponentData currentOpponentData = getOpponentForName(currentGameState, participantName);
                    if (currentGameState.SessionData.SessionPhase == SessionPhase.Countdown
                        && participantStruct.carLeaderboardPosition == 1
                        && !getReadyTriggeredForThisSession
                        && currentGameState.SessionData.TrackDefinition != null && currentGameState.SessionData.TrackDefinition.trackLength > 0
                        && currentGameState.SessionData.TrackDefinition.trackLength - participantStruct.spLineLength * currentGameState.SessionData.TrackDefinition.trackLength < 125)
                    {
                        getReadyTriggeredForThisSession = true;
                        currentGameState.SessionData.TriggerStartWarning = true;
                    }
                    if (participantName != null && participantName.Length > 0)
                    {
                        // there's a driver in the game data so remove him from the set who may have left the game
                        driversWhoMayHaveDisconnected.Remove(participantName);
                        opponentDisconnectionCounter[participantName] = 0;

                        if (currentOpponentData != null)
                        {
                            currentOpponentData.IsReallyDisconnectedCounter = 0;
                            if (previousGameState != null)
                            {
                                int previousOpponentSectorNumber = 1;
                                int previousOpponentCompletedLaps = 0;
                                int previousOpponentPosition = 0;
                                int currentOpponentSector = 0;
                                Boolean previousOpponentIsEnteringPits = false;

                                float[] previousOpponentWorldPosition = new float[] { 0, 0, 0 };
                                float previousOpponentSpeed = 0;
                                float previousDistanceRoundTrack = 0;
                                int currentOpponentRacePosition = 0;
                                OpponentData previousOpponentData = getOpponentForName(previousGameState, participantName);
                                // store some previous opponent data that we'll need later
                                if (previousOpponentData != null)
                                {
                                    previousOpponentSectorNumber = previousOpponentData.CurrentSectorNumber;
                                    previousOpponentCompletedLaps = previousOpponentData.CompletedLaps;
                                    previousOpponentPosition = previousOpponentData.OverallPosition;
                                    previousOpponentIsEnteringPits = previousOpponentData.isEnteringPits();
                                    previousOpponentWorldPosition = previousOpponentData.WorldPosition;
                                    previousOpponentSpeed = previousOpponentData.Speed;
                                    previousDistanceRoundTrack = previousOpponentData.DistanceRoundTrack;

                                    currentOpponentData.ClassPositionAtPreviousTick = previousOpponentData.ClassPosition;
                                    currentOpponentData.OverallPositionAtPreviousTick = previousOpponentData.OverallPosition;
                                }
                                float currentOpponentLapDistance = spLineLengthToDistanceRoundTrack(shared.accChief.trackLength, participantStruct.spLineLength);
                                currentOpponentSector = getCurrentSector(currentGameState.SessionData.TrackDefinition, currentOpponentLapDistance, participantStruct.spLineLength);

                                currentOpponentData.DeltaTime.SetNextDeltaPoint(currentOpponentLapDistance, participantStruct.lapCount,
                                    participantStruct.speedMS, currentGameState.Now, participantStruct.isCarInPitlane != 1);

                                int currentOpponentLapsCompleted = participantStruct.lapCount;

                                Boolean finishedAllottedRaceLaps = false;
                                Boolean finishedAllottedRaceTime = false;
                                if (currentGameState.SessionData.SessionType == SessionType.Race)
                                {
                                    if (!currentGameState.SessionData.SessionHasFixedTime)
                                    {
                                        finishedAllottedRaceLaps = currentGameState.SessionData.SessionNumberOfLaps > 0 && currentGameState.SessionData.SessionNumberOfLaps == currentOpponentLapsCompleted;
                                    }
                                    else if (currentGameState.SessionData.SessionTotalRunTime > 0 && currentGameState.SessionData.SessionTimeRemaining <= 0)
                                    {
                                        if (previousOpponentCompletedLaps < currentOpponentLapsCompleted)
                                        {
                                            // timed session, he's started a new lap after the time has reached zero. Where there's no extra lap this means we've finished. If there's 1 or more
                                            // extras he's finished when he's started more than the extra laps number
                                            currentOpponentData.LapsStartedAfterRaceTimeEnd++;
                                        }
                                        finishedAllottedRaceTime = currentOpponentData.LapsStartedAfterRaceTimeEnd > currentGameState.SessionData.ExtraLapsAfterTimedSessionComplete;
                                    }
                                }
                                int opponentPositionFromGame = useLeaderboardPosition || finishedAllottedRaceLaps || finishedAllottedRaceTime
                                    ? participantStruct.carLeaderboardPosition
                                    : participantStruct.carRealTimeLeaderboardPosition;
                                currentOpponentRacePosition = getRacePosition(participantName, previousOpponentPosition, opponentPositionFromGame, currentGameState.Now);

                                if (currentOpponentRacePosition == 1 && (finishedAllottedRaceTime || finishedAllottedRaceLaps))
                                {
                                    currentGameState.SessionData.LeaderHasFinishedRace = true;
                                }

                                updateOpponentData(currentOpponentData,
                                    previousOpponentData,
                                    currentOpponentRacePosition,
                                    currentOpponentLapsCompleted,
                                    currentOpponentSector,
                                    convertMillisecondsToSeconds(participantStruct.currentLapTimeMS),
                                    convertMillisecondsToSeconds(participantStruct.lastLapTimeMS),
                                    participantStruct.isCarInPitlane == 1,
                                    participantStruct.currentLapInvalid == 0,
                                    currentGameState.SessionData.SessionRunningTime,
                                    new float[] { participantStruct.worldPosition.x, participantStruct.worldPosition.z },
                                    participantStruct.speedMS,
                                    currentOpponentLapDistance,
                                    currentGameState.SessionData.SessionHasFixedTime,
                                    currentGameState.SessionData.SessionTimeRemaining,
                                    shared.accPhysics.airTemp,
                                    shared.accPhysics.roadTemp,
                                    currentGameState.SessionData.SessionType == SessionType.Race,
                                    currentGameState.SessionData.TrackDefinition.distanceForNearPitEntryChecks,
                                    currentGameState.TimingData,
                                    currentGameState.carClass,
                                    participantStruct.raceNumber,
                                    participantStruct.lastSplit1TimeMS,
                                    participantStruct.lastSplit2TimeMS,
                                    participantStruct.lastSplit3TimeMS);

                                if (previousOpponentData != null)
                                {
                                    currentOpponentData.trackLandmarksTiming = previousOpponentData.trackLandmarksTiming;
                                    String stoppedInLandmark = currentOpponentData.trackLandmarksTiming.updateLandmarkTiming(
                                        currentGameState.SessionData.TrackDefinition, currentGameState.SessionData.SessionRunningTime,
                                        previousDistanceRoundTrack, currentOpponentData.DistanceRoundTrack, currentOpponentData.Speed, currentOpponentData.CarClass);
                                    currentOpponentData.stoppedInLandmark = participantStruct.isCarInPitlane == 1 ? null : stoppedInLandmark;
                                }
                                if (currentGameState.SessionData.JustGoneGreen)
                                {
                                    currentOpponentData.trackLandmarksTiming = new TrackLandmarksTiming();
                                }
                                if (currentGameState.SessionData.SessionFastestLapTimeFromGamePlayerClass == -1 ||
                                        (participantStruct.bestLapMS > 0 && currentGameState.SessionData.SessionFastestLapTimeFromGamePlayerClass > convertMillisecondsToSeconds(participantStruct.bestLapMS)))
                                {
                                    currentGameState.SessionData.SessionFastestLapTimeFromGamePlayerClass = convertMillisecondsToSeconds(participantStruct.bestLapMS);
                                }
                                if (currentOpponentData.IsNewLap)
                                {
                                    currentOpponentData.trackLandmarksTiming.cancelWaitingForLandmarkEnd();
                                    if (currentOpponentData.CurrentBestLapTime > 0)
                                    {
                                        if (currentGameState.SessionData.OpponentsLapTimeSessionBestOverall == -1 ||
                                            currentOpponentData.CurrentBestLapTime < currentGameState.SessionData.OpponentsLapTimeSessionBestOverall)
                                        {
                                            currentGameState.SessionData.OpponentsLapTimeSessionBestOverall = currentOpponentData.CurrentBestLapTime;
                                            if (currentGameState.SessionData.OverallSessionBestLapTime == -1 ||
                                                currentGameState.SessionData.OverallSessionBestLapTime > currentOpponentData.CurrentBestLapTime)
                                            {
                                                currentGameState.SessionData.OverallSessionBestLapTime = currentOpponentData.CurrentBestLapTime;
                                            }
                                        }
                                        if (CarData.IsCarClassEqual(currentOpponentData.CarClass, currentGameState.carClass, true))
                                        {
                                            if (currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass == -1 ||
                                                currentOpponentData.CurrentBestLapTime < currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass)
                                            {
                                                currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass = currentOpponentData.CurrentBestLapTime;
                                                if (currentGameState.SessionData.PlayerClassSessionBestLapTime == -1 ||
                                                    currentGameState.SessionData.PlayerClassSessionBestLapTime > currentOpponentData.CurrentBestLapTime)
                                                {
                                                    currentGameState.SessionData.PlayerClassSessionBestLapTime = currentOpponentData.CurrentBestLapTime;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (participantName != null && participantName.Length > 0)
                        {
                            addOpponentForName(participantName, createOpponentData(participantStruct,
                                true,
                                CarData.getCarClassForClassNameOrCarName(participantStruct.carModel),
                                shared.accChief.trackLength,
                                currentGameState.SessionData.SessionType == SessionType.Race),
                                currentGameState);
                        }
                    }
                }

                // now process the disconnected drivers
                foreach (String name in driversWhoMayHaveDisconnected)
                {
                    int disconnectionCount;
                    if (opponentDisconnectionCounter.TryGetValue(name, out disconnectionCount))
                    {
                        if (disconnectionCount > 4)
                        {
                            // this guy has been missing for 5 or more ticks, so we assume he's gone and remove him
                            currentGameState.OpponentData.Remove(name);
                            opponentDisconnectionCounter.Remove(name);
                        }
                        else
                        {
                            opponentDisconnectionCounter[name] = disconnectionCount + 1;
                        }
                    }
                    else
                    {
                        opponentDisconnectionCounter.Add(name, 1);
                    }
                }

                currentGameState.sortClassPositions();
                currentGameState.setPracOrQualiDeltas();
            }

            // engine/transmission data
            currentGameState.EngineData.EngineRpm = shared.accPhysics.rpms;
            currentGameState.EngineData.MaxEngineRpm = shared.accStatic.maxRpm;
            currentGameState.EngineData.MinutesIntoSessionBeforeMonitoring = 2;

            currentGameState.FuelData.FuelCapacity = shared.accStatic.maxFuel;
            currentGameState.FuelData.FuelLeft = shared.accPhysics.fuel;
            currentGameState.FuelData.FuelUseActive = shared.accStatic.aidFuelRate > 0;
            currentGameState.FuelData.FuelMultiplier = (int)shared.accStatic.aidFuelRate;

            currentGameState.TransmissionData.Gear = shared.accPhysics.gear - 1;

            currentGameState.ControlData.BrakePedal = shared.accPhysics.brake;
            currentGameState.ControlData.ThrottlePedal = shared.accPhysics.gas;
            currentGameState.ControlData.ClutchPedal = shared.accPhysics.clutch;

            // penalty data
            PenatiesData.DetailedPenaltyType previousPenaltyType = previousGameState == null ? PenatiesData.DetailedPenaltyType.NONE :
                previousGameState.PenaltiesData.PenaltyType;
            if (shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_DriveThrough_Cutting
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_DriveThrough_IgnoredDriverStint
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_DriveThrough_PitSpeeding
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_DriveThrough_False_Start)
            {
                currentGameState.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.DRIVE_THROUGH;
                currentGameState.PenaltiesData.NumOutstandingPenalties++;
                currentGameState.PenaltiesData.HasDriveThrough = true;
            }
            else if (shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_10_Cutting
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_10_PitSpeeding
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_20_Cutting
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_20_PitSpeeding
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_30_Cutting
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_30_PitSpeeding)
            {
                currentGameState.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.STOP_AND_GO;
                currentGameState.PenaltiesData.NumOutstandingPenalties++;
                currentGameState.PenaltiesData.HasStopAndGo = true;
            }
            else
            {
                currentGameState.PenaltiesData.PenaltyType = PenatiesData.DetailedPenaltyType.NONE;
            }

            currentGameState.PenaltiesData.HasTimeDeduction = shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_PostRaceTime;

            if (shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_DriveThrough_PitSpeeding
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_Disqualified_PitSpeeding
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_10_PitSpeeding
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_20_PitSpeeding
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_30_PitSpeeding)
            {
                currentGameState.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.SPEEDING_IN_PITLANE;
            }
            else if (shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_Disqualified_Cutting
                            || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_DriveThrough_Cutting
                            || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_10_Cutting
                            || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_20_Cutting
                            || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_StopAndGo_30_Cutting)
            {
                currentGameState.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.CUT_TRACK;
            }
            else if (shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_Disqualified_ExceededDriverStintLimit)
            {
                currentGameState.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.EXCEEDED_TOTAL_DRIVER_STINT_LIMIT;
            }
            else if (shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_Disqualified_IgnoredDriverStint
                || shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_DriveThrough_IgnoredDriverStint)
            {
                currentGameState.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.EXCEEDED_SINGLE_DRIVER_STINT_LIMIT;
            }
            else if (shared.accGraphic.penalty == AC_PENALTY_TYPE.ACC_DriveThrough_False_Start)
            {
                currentGameState.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.FALSE_START;
            }
            else
            {
                currentGameState.PenaltiesData.PenaltyCause = PenatiesData.DetailedPenaltyCause.NONE;
            }

            // motion data
            currentGameState.PositionAndMotionData.CarSpeed = shared.accPhysics.speedKmh / 3.6f;
            currentGameState.PositionAndMotionData.DistanceRoundTrack = distanceRoundTrack;

            currentGameState.SessionData.PlayerCarNr = playerVehicle.raceNumber.ToString();

            //------------------------ Pit stop data -----------------------
            currentGameState.PitData.InPitlane = shared.accGraphic.isInPitLane == 1;

            if (currentGameState.PitData.InPitlane)
            {
                if (previousGameState != null && !previousGameState.PitData.InPitlane)
                {
                    if (currentGameState.SessionData.SessionRunningTime > 30 && currentGameState.SessionData.SessionType == SessionType.Race)
                    {
                        currentGameState.PitData.NumPitStops++;
                    }
                    currentGameState.PitData.OnInLap = true;
                    currentGameState.PitData.OnOutLap = false;
                }
                else if (currentGameState.SessionData.IsNewLap)
                {
                    currentGameState.PitData.OnInLap = false;
                    currentGameState.PitData.OnOutLap = true;
                }
            }
            else if (previousGameState != null && previousGameState.PitData.InPitlane)
            {
                currentGameState.PitData.OnInLap = false;
                currentGameState.PitData.OnOutLap = true;
                currentGameState.PitData.IsAtPitExit = true;
            }
            else if (currentGameState.SessionData.IsNewLap)
            {
                // starting a new lap while not in the pitlane so clear the in / out lap flags
                currentGameState.PitData.OnInLap = false;
                currentGameState.PitData.OnOutLap = false;
            }

            if (previousGameState != null && currentGameState.PitData.OnOutLap && previousGameState.PitData.InPitlane && !currentGameState.PitData.InPitlane)
            {
                currentGameState.PitData.IsAtPitExit = true;
            }

            //damage data
            if (shared.accChief.isInternalMemoryModuleLoaded == 1)
            {
                currentGameState.CarDamageData.DamageEnabled = true;

                currentGameState.CarDamageData.OverallEngineDamage = DamageLevel.UNKNOWN; // mapToEngineDamageLevel(playerVehicle.engineLifeLeft);

                currentGameState.CarDamageData.OverallAeroDamage = mapToAeroDamageLevel(shared.accPhysics.carDamage[0] +
                    shared.accPhysics.carDamage[1] +
                    shared.accPhysics.carDamage[2] +
                    shared.accPhysics.carDamage[3]);

                currentGameState.CarDamageData.SuspensionDamageStatus = CornerData.getCornerData(suspensionDamageThresholds,
                    shared.accPhysics.NOT_SET_suspensionDamage[0], shared.accPhysics.NOT_SET_suspensionDamage[1],
                    shared.accPhysics.NOT_SET_suspensionDamage[2], shared.accPhysics.NOT_SET_suspensionDamage[3]);
            }
            else
            {
                currentGameState.CarDamageData.DamageEnabled = false;
            }
            currentGameState.EngineData.EngineWaterTemp = shared.accPhysics.waterTemp;

            //tyre data
            currentGameState.TyreData.HasMatchedTyreTypes = true;
            currentGameState.TyreData.TyreWearActive = shared.accStatic.aidTireRate > 0;

            currentGameState.TyreData.FrontLeftPressure = shared.accPhysics.wheelsPressure[0] * 6.894f;
            currentGameState.TyreData.FrontRightPressure = shared.accPhysics.wheelsPressure[1] * 6.894f;
            currentGameState.TyreData.RearLeftPressure = shared.accPhysics.wheelsPressure[2] * 6.894f;
            currentGameState.TyreData.RearRightPressure = shared.accPhysics.wheelsPressure[3] * 6.894f;

            currentGameState.TyreData.BrakeTempStatus = CornerData.getCornerData(brakeTempThresholdsForPlayersCar, shared.accPhysics.brakeTemp[0], shared.accPhysics.brakeTemp[1], shared.accPhysics.brakeTemp[2], shared.accPhysics.brakeTemp[3]);
            currentGameState.TyreData.LeftFrontBrakeTemp = shared.accPhysics.brakeTemp[0];
            currentGameState.TyreData.RightFrontBrakeTemp = shared.accPhysics.brakeTemp[1];
            currentGameState.TyreData.LeftRearBrakeTemp = shared.accPhysics.brakeTemp[2];
            currentGameState.TyreData.RightRearBrakeTemp = shared.accPhysics.brakeTemp[3];
            // this appears to be zero-indexed
            currentGameState.TyreData.selectedSet = shared.accGraphic.mfdTyreSet;

            // specific fields for manuipulating tyre pressure in ACC:
            currentGameState.TyreData.ACCFrontLeftPressureMFD = shared.accGraphic.mfdTyrePressureLF;
            currentGameState.TyreData.ACCFrontRightPressureMFD = shared.accGraphic.mfdTyrePressureRF;
            currentGameState.TyreData.ACCRearLeftPressureMFD = shared.accGraphic.mfdTyrePressureLR;
            currentGameState.TyreData.ACCRearRightPressureMFD = shared.accGraphic.mfdTyrePressureRR;

            String currentTyreCompound = shared.accGraphic.tyreCompound;

            // Only middle tire temperature is available
            if (previousGameState != null && !previousGameState.TyreData.TyreTypeName.Equals(currentTyreCompound))
            {
                tyreTempThresholds = getTyreTempThresholds(currentGameState.carClass, currentTyreCompound);
                currentGameState.TyreData.TyreTypeName = currentTyreCompound;
            }

            // NOTE only a single tyre core temp value per corner is available. Shit shit data.

            //Front Left
            currentGameState.TyreData.FrontLeft_CenterTemp = shared.accPhysics.tyreCoreTemperature[0];
            currentGameState.TyreData.FrontLeft_LeftTemp = shared.accPhysics.tyreCoreTemperature[0];
            currentGameState.TyreData.FrontLeft_RightTemp = shared.accPhysics.tyreCoreTemperature[0];
            currentGameState.TyreData.FrontLeftTyreType = shared.accGraphic.rainTyres == 1 ? TyreType.Wet : defaultTyreTypeForPlayersCar;
            if (currentGameState.SessionData.IsNewLap || currentGameState.TyreData.PeakFrontLeftTemperatureForLap == 0)
            {
                currentGameState.TyreData.PeakFrontLeftTemperatureForLap = currentGameState.TyreData.FrontLeft_CenterTemp;
            }
            else if (previousGameState == null || currentGameState.TyreData.FrontLeft_CenterTemp > previousGameState.TyreData.PeakFrontLeftTemperatureForLap)
            {
                currentGameState.TyreData.PeakFrontLeftTemperatureForLap = currentGameState.TyreData.FrontLeft_CenterTemp;
            }
            //Front Right
            currentGameState.TyreData.FrontRight_CenterTemp = shared.accPhysics.tyreCoreTemperature[1];
            currentGameState.TyreData.FrontRight_LeftTemp = shared.accPhysics.tyreCoreTemperature[1];
            currentGameState.TyreData.FrontRight_RightTemp = shared.accPhysics.tyreCoreTemperature[1];
            currentGameState.TyreData.FrontRightTyreType = shared.accGraphic.rainTyres == 1 ? TyreType.Wet : defaultTyreTypeForPlayersCar;
            if (currentGameState.SessionData.IsNewLap || currentGameState.TyreData.PeakFrontRightTemperatureForLap == 0)
            {
                currentGameState.TyreData.PeakFrontRightTemperatureForLap = currentGameState.TyreData.FrontRight_CenterTemp;
            }
            else if (previousGameState == null || currentGameState.TyreData.FrontRight_CenterTemp > previousGameState.TyreData.PeakFrontRightTemperatureForLap)
            {
                currentGameState.TyreData.PeakFrontRightTemperatureForLap = currentGameState.TyreData.FrontRight_CenterTemp;
            }
            //Rear Left
            currentGameState.TyreData.RearLeft_CenterTemp = shared.accPhysics.tyreCoreTemperature[2];
            currentGameState.TyreData.RearLeft_LeftTemp = shared.accPhysics.tyreCoreTemperature[2];
            currentGameState.TyreData.RearLeft_RightTemp = shared.accPhysics.tyreCoreTemperature[2];
            currentGameState.TyreData.RearLeftTyreType = shared.accGraphic.rainTyres == 1 ? TyreType.Wet : defaultTyreTypeForPlayersCar;
            if (currentGameState.SessionData.IsNewLap || currentGameState.TyreData.PeakRearLeftTemperatureForLap == 0)
            {
                currentGameState.TyreData.PeakRearLeftTemperatureForLap = currentGameState.TyreData.RearLeft_CenterTemp;
            }
            else if (previousGameState == null || currentGameState.TyreData.RearLeft_CenterTemp > previousGameState.TyreData.PeakRearLeftTemperatureForLap)
            {
                currentGameState.TyreData.PeakRearLeftTemperatureForLap = currentGameState.TyreData.RearLeft_CenterTemp;
            }
            //Rear Right
            currentGameState.TyreData.RearRight_CenterTemp = shared.accPhysics.tyreCoreTemperature[3];
            currentGameState.TyreData.RearRight_LeftTemp = shared.accPhysics.tyreCoreTemperature[3];
            currentGameState.TyreData.RearRight_RightTemp = shared.accPhysics.tyreCoreTemperature[3];
            currentGameState.TyreData.RearRightTyreType = shared.accGraphic.rainTyres == 1 ? TyreType.Wet : defaultTyreTypeForPlayersCar;
            if (currentGameState.SessionData.IsNewLap || currentGameState.TyreData.PeakRearRightTemperatureForLap == 0)
            {
                currentGameState.TyreData.PeakRearRightTemperatureForLap = currentGameState.TyreData.RearRight_CenterTemp;
            }
            else if (previousGameState == null || currentGameState.TyreData.RearRight_CenterTemp > previousGameState.TyreData.PeakRearRightTemperatureForLap)
            {
                currentGameState.TyreData.PeakRearRightTemperatureForLap = currentGameState.TyreData.RearRight_CenterTemp;
            }

            currentGameState.TyreData.TyreTempStatus = CornerData.getCornerData(tyreTempThresholds, currentGameState.TyreData.PeakFrontLeftTemperatureForLap,
                    currentGameState.TyreData.PeakFrontRightTemperatureForLap, currentGameState.TyreData.PeakRearLeftTemperatureForLap,
                    currentGameState.TyreData.PeakRearRightTemperatureForLap);

            // tyre wear is always 0.0 in the shared memory data

            if (previousGameState != null && previousGameState.SessionData.CurrentLapIsValid && !currentGameState.SessionData.CurrentLapIsValid)
            {
                currentGameState.PenaltiesData.CutTrackWarnings++;
            }
            if (playerVehicle.speedMS > 7 && currentGameState.carClass != null)
            {
                float minRotatingSpeed = (float)Math.PI * playerVehicle.speedMS / currentGameState.carClass.maxTyreCircumference;
                currentGameState.TyreData.LeftFrontIsLocked = Math.Abs(shared.accPhysics.wheelAngularSpeed[0]) < minRotatingSpeed;
                currentGameState.TyreData.RightFrontIsLocked = Math.Abs(shared.accPhysics.wheelAngularSpeed[1]) < minRotatingSpeed;
                currentGameState.TyreData.LeftRearIsLocked = Math.Abs(shared.accPhysics.wheelAngularSpeed[2]) < minRotatingSpeed;
                currentGameState.TyreData.RightRearIsLocked = Math.Abs(shared.accPhysics.wheelAngularSpeed[3]) < minRotatingSpeed;

                // '3' here is a magic number - we want to allow the wheel to spin significantly faster than the ideal we we're not always grumbling about it
                float maxRotatingSpeed = 3 * (float)Math.PI * playerVehicle.speedMS / currentGameState.carClass.minTyreCircumference;
                // all the cars are RWD (so far), so don't bother checking front wheelspin
                //currentGameState.TyreData.LeftFrontIsSpinning = Math.Abs(shared.accPhysics.wheelAngularSpeed[0]) > maxRotatingSpeed;
                //currentGameState.TyreData.RightFrontIsSpinning = Math.Abs(shared.accPhysics.wheelAngularSpeed[1]) > maxRotatingSpeed;
                currentGameState.TyreData.LeftRearIsSpinning = Math.Abs(shared.accPhysics.wheelAngularSpeed[2]) > maxRotatingSpeed;
                currentGameState.TyreData.RightRearIsSpinning = Math.Abs(shared.accPhysics.wheelAngularSpeed[3]) > maxRotatingSpeed;
            }

            // conditions
            // sometimes the game sends zeros, so don't take a sample if the temps are zero
            currentGameState.Conditions.UpdateLiveSample(currentGameState.Now, currentGameState.SessionData.CompletedLaps, currentGameState.SessionData.SectorNumber,
                shared.accPhysics.airTemp, shared.accPhysics.roadTemp, (float)shared.accGraphic.rainIntensity / 5f,
                0, 0, 0, 0, currentGameState.SessionData.IsNewLap, (ConditionsMonitor.TrackStatus)shared.accGraphic.trackGripStatus);
            if (currentGameState.Now > nextConditionsSampleDue && (shared.accPhysics.airTemp != 0 || shared.accPhysics.roadTemp != 0))
            {
                float currentRainLevel = (float)shared.accGraphic.rainIntensity / 5f;   // 5 enum levels for rain from 0 (none) to 1 (monsoon)
                nextConditionsSampleDue = currentGameState.Now.Add(ConditionsMonitor.ConditionsSampleFrequency);

                currentGameState.Conditions.addSample(currentGameState.Now, currentGameState.SessionData.CompletedLaps, currentGameState.SessionData.SectorNumber,
                    shared.accPhysics.airTemp, shared.accPhysics.roadTemp, currentRainLevel, 0, 0, 0, 0, currentGameState.SessionData.IsNewLap,
                    (ConditionsMonitor.TrackStatus)shared.accGraphic.trackGripStatus /* our track status maps directly to ACC grip status */);
            }
            currentGameState.Conditions.rainLevelNow = (ConditionsMonitor.RainLevel)shared.accGraphic.rainIntensity;
            currentGameState.Conditions.rainLevelIn10Mins = (ConditionsMonitor.RainLevel)shared.accGraphic.rainIntensityIn10min;
            currentGameState.Conditions.rainLevelIn30Mins = (ConditionsMonitor.RainLevel)shared.accGraphic.rainIntensityIn30min;
            currentGameState.Conditions.RainForecastAvailable = ((int)shared.accGraphic.rainIntensityIn10min >= 0 && (int)shared.accGraphic.rainIntensityIn10min <= 5) ||
                ((int)shared.accGraphic.rainIntensityIn30min >= 0 && (int)shared.accGraphic.rainIntensityIn30min <= 5);
            currentGameState.Conditions.RainForecastReadUtc = DateTime.SpecifyKind(currentGameState.Now, DateTimeKind.Utc);
            currentGameState.Conditions.RainForecastSource = "ACC Shared Memory – Spiel-/Servervorhersage";
            currentGameState.Conditions.RainChanceIn10Mins = currentGameState.Conditions.RainChanceIn30Mins = -1;

            if (currentGameState.SessionData.TrackDefinition != null)
            {
                CrewChief.trackName = currentGameState.SessionData.TrackDefinition.name;
            }
            if (currentGameState.carClass != null)
            {
                CrewChief.carClass = currentGameState.carClass.carClassEnum;
            }
            CrewChief.distanceRoundTrack = currentGameState.PositionAndMotionData.DistanceRoundTrack;
            CrewChief.viewingReplay = false;

            currentGameState.PositionAndMotionData.Orientation.Pitch = shared.accPhysics.pitch;
            currentGameState.PositionAndMotionData.Orientation.Roll = shared.accPhysics.roll;
            currentGameState.PositionAndMotionData.Orientation.Yaw = shared.accPhysics.heading;

            if (currentGameState.SessionData.IsNewLap)
            {
                if (currentGameState.hardPartsOnTrackData.updateHardPartsForNewLap(currentGameState.SessionData.LapTimePrevious))
                {
                    currentGameState.SessionData.TrackDefinition.adjustGapPoints(currentGameState.hardPartsOnTrackData.processedHardPartsForBestLap);
                }
            }
            else if (!currentGameState.PitData.OnOutLap && !currentGameState.SessionData.TrackDefinition.isOval &&
                !(currentGameState.SessionData.SessionType == SessionType.Race &&
                   (currentGameState.SessionData.CompletedLaps < 1 || (GameStateData.useManualFormationLap && currentGameState.SessionData.CompletedLaps < 2))))
            {
                currentGameState.hardPartsOnTrackData.mapHardPartsOnTrack(currentGameState.ControlData.BrakePedal, currentGameState.ControlData.ThrottlePedal,
                    currentGameState.PositionAndMotionData.DistanceRoundTrack, currentGameState.SessionData.CurrentLapIsValid, currentGameState.SessionData.TrackDefinition.trackLength);
            }

            currentGameState.PositionAndMotionData.WorldPosition = new float[] { playerVehicle.worldPosition.x, playerVehicle.worldPosition.y, playerVehicle.worldPosition.z };

            // note on the window stuff... It only matches what's in the session options (offline) if you skip P and Q. Otherwise it's based on the last session length prior to the
            // race.
            currentGameState.PitData.PitWindow = PitWindow.Unavailable;
            currentGameState.PitData.PitWindowStart = -1;
            currentGameState.PitData.PitWindowEnd = -1;
            // where there's no pit window, the game sends data where start is after end so this will be negative
            // Note that the length is generally correct but the start / end values are not
            float windowLength = (shared.accStatic.PitWindowEnd - shared.accStatic.PitWindowStart) / 60000f;
            if (currentGameState.SessionData.SessionType == SessionType.Race
                && shared.accGraphic.missingMandatoryPits < 255 /* 255 means nothing to miss, so no mandatory stop */
                && (shared.accGraphic.missingMandatoryPits > 0 || shared.accGraphic.MandatoryPitDone > 0)) /* 0 missing and 0 done means no mandatory stop */
            {
                // 'has' actually means 'the session has a mandatory stop', we might have completed it but this will remain true
                currentGameState.PitData.HasMandatoryPitStop = true;
                if (windowLength > 0
                    && !currentGameState.SessionData.StartedRaceFromPitLane) /* When starting a race from the pit lane we can't know how long the race was originally for so can't get the pit window */
                {
                    // use the session run time and pit window length to figure out the correct start and end times. The middle of the window should be the middle of the race
                    int sessionLengthMinutes = (int)Math.Ceiling(currentGameState.SessionData.SessionTotalRunTime / 60f);
                    if (sessionLengthMinutes > windowLength)
                    {
                        currentGameState.PitData.PitWindowStart = (sessionLengthMinutes - windowLength) / 2;
                        currentGameState.PitData.PitWindowEnd = currentGameState.PitData.PitWindowStart + windowLength;
                    }
                }
                currentGameState.PitData.MandatoryPitStopCompleted = shared.accGraphic.missingMandatoryPits == 0;

                // note that the game sometimes sends shared.accGraphic.MandatoryPitDone == 1 all through the session when there's no mandatory stop. In this
                // case we'll immediately map to Closed. It should really be Unavailable but there's no sane way to tell the difference. Closed is probably OK anyway
                if (currentGameState.SessionData.SessionHasFixedTime && currentGameState.SessionData.SessionRunningTime < currentGameState.SessionData.SessionTotalRunTime)
                {
                    bool beforeWindow = currentGameState.PitData.PitWindowStart > 0 && currentGameState.SessionData.SessionRunningTime < currentGameState.PitData.PitWindowStart * 60;
                    bool afterWindow = currentGameState.PitData.PitWindowEnd > 0 && currentGameState.SessionData.SessionRunningTime > currentGameState.PitData.PitWindowEnd * 60;
                    if ((beforeWindow || afterWindow) && !currentGameState.PitData.MandatoryPitStopCompleted)
                    {
                        currentGameState.PitData.PitWindow = PitWindow.Closed;
                    }
                    else if (currentGameState.PitData.HasMandatoryPitStop && !beforeWindow && !afterWindow)
                    {
                        currentGameState.PitData.PitWindow = currentGameState.PitData.InPitlane ? PitWindow.StopInProgress : PitWindow.Open;
                    }
                    else if (currentGameState.PitData.MandatoryPitStopCompleted)
                    {
                        currentGameState.PitData.PitWindow = PitWindow.Completed;
                    }
                }
            }

            // Frozen order stuff
            if (previousGameState != null && currentGameState.SessionData.SessionType == SessionType.Race)
            {
                int playerPosition = currentGameState.SessionData.OverallPosition;
                if (currentGameState.SessionData.SessionPhase == SessionPhase.Formation)
                {
                    // need to capture the grid side data before the cars start moving but we must have a non-zero heading first. Without the
                    // heading check the spotter will see a heading of 0 for the first few ticks and get the side calculation wrong
                    if (currentGameState.FrozenOrderData.Phase == FrozenOrderPhase.None && shared.accPhysics.heading != 0)
                    {
                        // because of the unsynchronized data, we don't know how many cars we'll have at this point. The game will start sending
                        // car data at the start of the formation phase, it should be fairly quick, but it's may not be finished by the time we get a non-zero heading
                        Tuple<GridSide, Dictionary<int, GridSide>> gridSides = MainWindow.instance.crewChief.getGridSide();
                        // check this is valid - it takes time for the opponent position and player heading data to settle
                        bool gridSideDataValid = gridSideDataIsValid(gridSides.Item1, gridSides.Item2, playerPosition, shared.accGraphic.carCount);                        

                        if (!gridSideDataValid && !waitingForValidFormationCarData)
                        {
                            waitingForValidFormationCarData = true;
                            startedWaitingForValidGridDataAt = currentGameState.Now;
                        }
                        else if (gridSideDataValid || currentGameState.Now > startedWaitingForValidGridDataAt.AddSeconds(6))
                        {
                            // note that we don't set an 'action' here - this comes when we hit the countdown phase (it's single file until then)
                            currentGameState.FrozenOrderData.Action = FrozenOrderAction.None;
                            currentGameState.FrozenOrderData.Phase = FrozenOrderPhase.Rolling;
                            if (gridSideDataValid)
                            {
                                Console.WriteLine("Waited " + (currentGameState.Now - startedWaitingForValidGridDataAt).TotalSeconds + " for valid grid side data");
                                currentGameState.FrozenOrderData.AssignedColumn = gridSides.Item1 == GridSide.LEFT ? FrozenOrderColumn.Left : FrozenOrderColumn.Right;
                            }
                            else
                            {
                                Console.WriteLine("Waited " + (currentGameState.Now - startedWaitingForValidGridDataAt).TotalSeconds + " but didn't get valid side data");
                            }
                            currentGameState.FrozenOrderData.AssignedPosition = playerPosition;
                            bool leaderCol = playerPosition % 2 != 0;
                            currentGameState.FrozenOrderData.AssignedGridPosition = leaderCol ? (playerPosition / 2) + 1 : playerPosition / 2;
                            if (playerPosition > 1)
                            {
                                // special case for P2 - no safety car to follow so just tell him to follow the leader
                                OpponentData carFront = currentGameState.getOpponentAtOverallPosition(playerPosition == 2 ? 1 : playerPosition - 2);
                                currentGameState.FrozenOrderData.CarNumberToFollowRaw = carFront.CarNumber;
                                currentGameState.FrozenOrderData.DriverToFollowRaw = carFront.DriverRawName;
                            }
                            foreach (OpponentData opponent in currentGameState.OpponentData.Values)
                            {
                                currentGameState.FrozenOrderData.OpponentPositionsAtStartOfFormationLap[opponent.OverallPosition] = opponent.DriverRawName;
                            }
                            waitingForValidFormationCarData = false;
                        }
                    }
                }
                else if (playerVehicle.isCarInPitlane == 0
                    && currentGameState.SessionData.SessionPhase == SessionPhase.Countdown
                    && currentGameState.FrozenOrderData.Phase == FrozenOrderPhase.Rolling
                    && currentGameState.FrozenOrderData.AssignedPosition > 0)
                {
                    // if a car has been removed during the formation lap the grid positioning will be reshuffled. If a car is removed during the countdown phase there'll be a gap
                    // but the grid slots won't change. So for online races, compare the opponents list at the start of Countdown to see if we need update our grid data
                    if (previousGameState.SessionData.SessionPhase == SessionPhase.Formation
                        && shared.accStatic.isOnline == 1)
                    {
                        // check if any opponents have gone missing
                        bool opponentsHaveDisconnected = false;
                        foreach (var formationDriver in currentGameState.FrozenOrderData.OpponentPositionsAtStartOfFormationLap)
                        {
                            if (!currentGameState.OpponentData.ContainsKey(formationDriver.Value))
                            {
                                opponentsHaveDisconnected = true;
                                break;
                            }
                        }
                        // someone's missing, see if we need to reshuffle
                        if (opponentsHaveDisconnected)
                        {
                            correctFrozenOrderDataForDisconnectedOpponents(currentGameState);
                        }
                    }
                    // suppress the 'catch up to and 'allow to pass' until they work reliably (or remove them)
                    /*if (playerPosition > currentGameState.FrozenOrderData.AssignedPosition)
                    {
                        currentGameState.FrozenOrderData.Action = playerPosition == 1 ? FrozenOrderAction.MoveToPole : FrozenOrderAction.CatchUp;
                    }
                    else if (playerPosition < currentGameState.FrozenOrderData.AssignedPosition)
                    {
                        currentGameState.FrozenOrderData.Action = FrozenOrderAction.AllowToPass;
                    }
                    else*/ if (previousGameState.SessionData.SessionPhase == SessionPhase.Formation)
                    {
                        // only allow the follow action to be trigger on transition from formation to countdown (when we have to form up)
                        currentGameState.FrozenOrderData.Action = currentGameState.FrozenOrderData.AssignedPosition == 1 ? FrozenOrderAction.StayInPole : FrozenOrderAction.Follow;
                    }
                }
                else
                {
                    currentGameState.FrozenOrderData.Phase = FrozenOrderPhase.None;
                    currentGameState.FrozenOrderData.Action = FrozenOrderAction.None;
                }
            }
            else
            {
                currentGameState.FrozenOrderData.Phase = FrozenOrderPhase.None;
                currentGameState.FrozenOrderData.Action = FrozenOrderAction.None;
            }
            return currentGameState;
        }

        private bool gridSideDataIsValid(GridSide playerGridSide, Dictionary<int, GridSide> gridSides, int playerPosition, int carCount)
        {
            int carsOnLeft = 0;
            int carsOnRight = 0;
            bool nextOrPrevCarIsOnOtherSide = false;
            for (int i=1; i<carCount; i++)
            {
                if (gridSides.TryGetValue(i, out GridSide gridSide))
                {
                    if (gridSide == GridSide.LEFT)
                    {
                        carsOnLeft++;
                    }
                    else if (gridSide == GridSide.RIGHT)
                    {
                        carsOnRight++;
                    }
                    if ((playerPosition == 1 && i == 2) || (i == playerPosition - 1))
                    {
                        nextOrPrevCarIsOnOtherSide = gridSide != playerGridSide;
                    }
                }
            }
            // we have valid grid data when the car immediately ahead (or behind if we're pole) is on the opposite side, and
            // there's a reasonable variety of left / right starting positions
            return nextOrPrevCarIsOnOtherSide && Math.Abs(carsOnLeft - carsOnRight) < 3;
        }

        private void correctFrozenOrderDataForDisconnectedOpponents(GameStateData currentGameState)
        {
            int numMissingAhead = 0;
            bool carToFollowHasChanged = false;
            foreach (var formationDriver in currentGameState.FrozenOrderData.OpponentPositionsAtStartOfFormationLap)
            {
                if (formationDriver.Key < currentGameState.FrozenOrderData.AssignedPosition)
                {
                    bool stillHasDriver = false;
                    foreach (OpponentData opponent in currentGameState.OpponentData.Values)
                    {
                        if (opponent.DriverRawName == formationDriver.Value)
                        {
                            stillHasDriver = true;
                            break;
                        }
                    }
                    if (!stillHasDriver)    // someone ahead has dropped out so might need a reshuffle
                    {
                        numMissingAhead++;
                        // swap columns each time a driver has dropped out ahead of us
                        currentGameState.FrozenOrderData.swapSides();
                        // if the driver who's dropped out is 1 or 2 places ahead of us, we'll need to follow a different driver
                        carToFollowHasChanged = currentGameState.FrozenOrderData.AssignedPosition - formationDriver.Key <= 2;
                    }
                }
            }
            if (numMissingAhead > 0)
            {
                if (carToFollowHasChanged)
                {
                    int originalPositionOfNewCarToFollow = currentGameState.FrozenOrderData.AssignedPosition - 3;
                    for (; originalPositionOfNewCarToFollow > 0; originalPositionOfNewCarToFollow--)
                    {
                        if (currentGameState.OpponentData.TryGetValue(currentGameState.FrozenOrderData.OpponentPositionsAtStartOfFormationLap[originalPositionOfNewCarToFollow], out var opponent))
                        {
                            Console.WriteLine("grid reshuffle, we're now following car " + opponent.DriverRawName);
                            currentGameState.FrozenOrderData.CarNumberToFollowRaw = opponent.CarNumber;
                            currentGameState.FrozenOrderData.DriverToFollowRaw = opponent.DriverRawName;
                            break;
                        }
                    }
                }
                currentGameState.FrozenOrderData.AssignedPosition = currentGameState.FrozenOrderData.AssignedPosition - numMissingAhead;
                if (currentGameState.FrozenOrderData.AssignedPosition < 3)
                {
                    // moved to front row
                    currentGameState.FrozenOrderData.CarNumberToFollowRaw = "";
                    currentGameState.FrozenOrderData.DriverToFollowRaw = "";
                }
                bool leaderCol = currentGameState.FrozenOrderData.AssignedPosition % 2 != 0;
                currentGameState.FrozenOrderData.AssignedGridPosition = leaderCol ? (currentGameState.FrozenOrderData.AssignedPosition / 2) + 1 : currentGameState.FrozenOrderData.AssignedPosition / 2;
                Console.WriteLine(numMissingAhead + " cars have dropped out ahead of us during the formation phase, new grid side is now " + currentGameState.FrozenOrderData.AssignedColumn);
            }
        }

        private List<CornerData.EnumWithThresholds> getTyreTempThresholds(CarData.CarClass carClass, string currentTyreCompound)
        {
            List<CornerData.EnumWithThresholds> tyreTempThresholds = new List<CornerData.EnumWithThresholds>();
            CarData.TyreTypeData tyreTypeData = null;
            AcTyres acTyre = null;
            if (carClass.acTyreTypeData.TryGetValue(currentTyreCompound, out tyreTypeData))
            {
                tyreTempThresholds.Add(new CornerData.EnumWithThresholds(TyreTemp.COLD, -10000f, tyreTypeData.maxColdTyreTemp));
                tyreTempThresholds.Add(new CornerData.EnumWithThresholds(TyreTemp.WARM, tyreTypeData.maxColdTyreTemp, tyreTypeData.maxWarmTyreTemp));
                tyreTempThresholds.Add(new CornerData.EnumWithThresholds(TyreTemp.HOT, tyreTypeData.maxWarmTyreTemp, tyreTypeData.maxHotTyreTemp));
                tyreTempThresholds.Add(new CornerData.EnumWithThresholds(TyreTemp.COOKING, tyreTypeData.maxHotTyreTemp, 10000f));
                System.Diagnostics.Debug.WriteLine("Using user defined temperature thresholds for TyreType: " + currentTyreCompound);
            }
            else if (acTyres.TryGetValue(currentTyreCompound, out acTyre))
            {
                tyreTempThresholds = acTyre.tyreTempThresholdsForAC;
                System.Diagnostics.Debug.WriteLine("Using buildin defined temperature thresholds for TyreType: " + currentTyreCompound);
            }
            else
            {
                tyreTempThresholds = CarData.getTyreTempThresholds(carClass);
                System.Diagnostics.Debug.WriteLine("Using temperature thresholds for TyreType: " + carClass.defaultTyreType.ToString() +
                    " maxColdTyreTemp: " + tyreTempThresholds[0].upperThreshold + " maxWarmTyreTemp: " + tyreTempThresholds[1].upperThreshold +
                    " maxHotTyreTemp: " + tyreTempThresholds[2].upperThreshold);
            }
            return tyreTempThresholds;
        }

        private void updateOpponentData(OpponentData opponentData, OpponentData previousOpponentData, int realtimeRacePosition, int completedLaps, int sector,
            float completedLapTime, float lastLapTime, Boolean isInPits, Boolean lapIsValid, float sessionRunningTime,
            float[] currentWorldPosition, float speed, float distanceRoundTrack, Boolean sessionLengthIsTime, float sessionTimeRemaining,
            float airTemperature, float trackTemperature, Boolean isRace, float nearPitEntryPointDistance,
            TimingData timingData, CarData.CarClass playerCarClass, int raceNumber, int lastLapS1TimeMs, int lastLapS2TimeMs, int lastLapS3TimeMs)
        {
            if (opponentData.CurrentSectorNumber == 0)
            {
                opponentData.CurrentSectorNumber = sector;
            }
            float previousDistanceRoundTrack = opponentData.DistanceRoundTrack;
            opponentData.DistanceRoundTrack = distanceRoundTrack;
            opponentData.CarNumber = raceNumber.ToString();

            opponentData.Speed = speed;
            if (opponentData.OverallPosition != realtimeRacePosition)
            {
                opponentData.SessionTimeAtLastPositionChange = sessionRunningTime;
            }
            opponentData.OverallPosition = realtimeRacePosition;
            if (previousDistanceRoundTrack < nearPitEntryPointDistance && opponentData.DistanceRoundTrack > nearPitEntryPointDistance)
            {
                opponentData.PositionOnApproachToPitEntry = opponentData.OverallPosition;
            }
            opponentData.WorldPosition = currentWorldPosition;
            opponentData.IsNewLap = false;
            opponentData.JustEnteredPits = !opponentData.InPits && isInPits;
            if (opponentData.JustEnteredPits)
            {
                opponentData.NumPitStops++;
            }
            opponentData.InPits = isInPits;

            bool hasCrossedSFline = opponentData.CurrentSectorNumber == ACCGameStateMapper.numberOfSectorsOnTrack && sector == 1;

            if (opponentData.CurrentSectorNumber == ACCGameStateMapper.numberOfSectorsOnTrack && sector == ACCGameStateMapper.numberOfSectorsOnTrack && !lapIsValid)
            {
                // special case for s3 - need to invalidate lap immediately
                opponentData.InvalidateCurrentLap();
            }
            if (opponentData.CurrentSectorNumber != sector || hasCrossedSFline)
            {
                if (hasCrossedSFline)
                {
                    opponentData.CurrentSectorNumber = 1;
                    if (opponentData.OpponentLapData.Count > 0)
                    {
                        // special case here: if there's only 1 lap in the list, and it's marked as an in-lap, and we don't have a laptime, remove it.
                        // This is because we might have created a new LapData entry to hold a partially completed in-lap if we join mid-session, but
                        // this also results in each opponent having a spurious 'empty' LapData element.
                        if (opponentData.OpponentLapData.Count == 1 && opponentData.OpponentLapData[0].InLap && lastLapTime == 0)
                        {
                            opponentData.OpponentLapData.Clear();
                        }
                        else
                        {
                            opponentData.CompleteLapWithProvidedLapTime(realtimeRacePosition, sessionRunningTime, lastLapTime, isInPits,
                                false, trackTemperature, airTemperature, sessionLengthIsTime, sessionTimeRemaining, ACCGameStateMapper.numberOfSectorsOnTrack,
                                timingData, CarData.IsCarClassEqual(opponentData.CarClass, playerCarClass, true), (float)lastLapS1TimeMs / 1000f, (float)lastLapS2TimeMs / 1000f);
                        }
                    }

                    opponentData.StartNewLap(completedLaps + 1, realtimeRacePosition, isInPits, sessionRunningTime, false, trackTemperature, airTemperature);
                    opponentData.IsNewLap = true;
                    opponentData.CompletedLaps = completedLaps;
                    // recheck the car class here?
                }
                else if (((opponentData.CurrentSectorNumber == 1 && sector == 2) || (opponentData.CurrentSectorNumber == 2 && sector == 3)))
                {
                    // if we've not yet logged the position of the current sector start, make the time invalid:
                    opponentData.AddCumulativeSectorData(opponentData.CurrentSectorNumber, realtimeRacePosition, completedLapTime, sessionRunningTime,
                        lapIsValid && sectorSplinePointsFromGame[sector -1] > 0, false, trackTemperature, airTemperature);
                }
                opponentData.CurrentSectorNumber = sector;
            }
            if (sector == ACCGameStateMapper.numberOfSectorsOnTrack && isInPits)
            {
                opponentData.setInLap();
            }
        }

        private OpponentData createOpponentData(accVehicleInfo participantStruct, Boolean loadDriverName, CarData.CarClass carClass, float trackLength, bool raceSessionIsUnderway)
        {
            OpponentData opponentData = new OpponentData();
            String participantName = participantStruct.driverName;
            opponentData.DriverRawName = participantName;
            opponentData.DriverNameSet = true;
            // note that in AC, drivers may be added to the session during the race - we don't want to load these driver names
            if (participantName != null && participantName.Length > 0 && loadDriverName && CrewChief.enableDriverNames && !raceSessionIsUnderway)
            {
                if (speechRecogniser != null) speechRecogniser.addNewOpponentName(opponentData.DriverRawName, "-1");
                SoundCache.loadDriverNameSound(DriverNameHelper.getUsableDriverName(opponentData.DriverRawName));
            }

            // when we first create an opponent use the game-provided leadboard position. Subsequent updates will use the realtime position
            opponentData.OverallPosition = participantStruct.carLeaderboardPosition;
            opponentData.CompletedLaps = participantStruct.lapCount;
            opponentData.CurrentSectorNumber = 0;
            opponentData.WorldPosition = new float[] { participantStruct.worldPosition.x, participantStruct.worldPosition.z };
            opponentData.DistanceRoundTrack = spLineLengthToDistanceRoundTrack(trackLength, participantStruct.spLineLength);
            opponentData.DeltaTime = new DeltaTime(trackLength, opponentData.DistanceRoundTrack, participantStruct.speedMS, DateTime.UtcNow);
            opponentData.CarClass = carClass;
            opponentData.IsActive = true;
            opponentData.CarNumber = participantStruct.raceNumber.ToString();
            String nameToLog = opponentData.DriverRawName == null ? "unknown" : opponentData.DriverRawName;
            System.Diagnostics.Debug.WriteLine("New driver " + nameToLog + " is using car class " + opponentData.CarClass.getClassIdentifier());
            return opponentData;
        }

        public static void addOpponentForName(String name, OpponentData opponentData, GameStateData gameState)
        {
            if (name == null || name.Length == 0)
            {
                return;
            }
            if (gameState.OpponentData == null)
            {
                gameState.OpponentData = new Dictionary<string, OpponentData>();
            }
            gameState.OpponentData.Remove(name);
            gameState.OpponentData.Add(name, opponentData);
        }

        public float convertMillisecondsToSeconds(int timeInMilliseconds)
        {
            return timeInMilliseconds < 0 ? -1f : timeInMilliseconds / 1000f;
        }

        private FlagEnum mapToFlagEnum(bool checkered, bool green, bool red, bool white, bool yellow, bool black, bool blue)
        {
            if (checkered)
            {
                return FlagEnum.CHEQUERED;
            }
            else if (red)
            {
                return FlagEnum.RED;
            }
            else if (blue)
            {
                return FlagEnum.BLUE;
            }
            else if (black)
            {
                return FlagEnum.BLACK;
            }
            // note that white flag in ACC is the American version (last lap) for some odd reason
            /*else if (white)
            {
                return FlagEnum.WHITE;
            }*/
            else if (yellow)
            {
                return FlagEnum.YELLOW;
            }
            else if (green)
            {
                return FlagEnum.GREEN;
            }
            return FlagEnum.UNKNOWN;
        }

        private SessionType mapToSessionState(AC_SESSION_TYPE sessionState)
        {
            if (sessionState == AC_SESSION_TYPE.AC_RACE || sessionState == AC_SESSION_TYPE.AC_DRIFT || sessionState == AC_SESSION_TYPE.AC_DRAG)
            {
                return SessionType.Race;
            }
            else if (sessionState == AC_SESSION_TYPE.AC_PRACTICE)
            {
                return SessionType.Practice;
            }
            else if (sessionState == AC_SESSION_TYPE.AC_QUALIFY || sessionState == AC_SESSION_TYPE.ACC_HOTSTINTSUPERPOLE)
            {
                return SessionType.Qualify;
            }
            else if (sessionState == AC_SESSION_TYPE.AC_TIME_ATTACK || sessionState == AC_SESSION_TYPE.AC_HOTLAP || sessionState == AC_SESSION_TYPE.ACC_HOTSTINT)
            {
                return SessionType.HotLap;
            }
            else
            {
                return SessionType.Unavailable;
            }
        }

        private DamageLevel mapToEngineDamageLevel(float engineDamage)
        {
            if (engineDamage >= 1000.0)
            {
                return DamageLevel.NONE;
            }
            else if (engineDamage <= destroyedEngineDamageThreshold)
            {
                return DamageLevel.DESTROYED;
            }
            else if (engineDamage <= severeEngineDamageThreshold)
            {
                return DamageLevel.MAJOR;
            }
            else if (engineDamage <= minorEngineDamageThreshold)
            {
                return DamageLevel.MINOR;
            }
            else if (engineDamage <= trivialEngineDamageThreshold)
            {
                return DamageLevel.TRIVIAL;
            }
            return DamageLevel.NONE;
        }

        private DamageLevel mapToAeroDamageLevel(float aeroDamage)
        {

            if (aeroDamage >= destroyedAeroDamageThreshold)
            {
                return DamageLevel.DESTROYED;
            }
            else if (aeroDamage >= severeAeroDamageThreshold)
            {
                return DamageLevel.MAJOR;
            }
            else if (aeroDamage >= minorAeroDamageThreshold)
            {
                return DamageLevel.MINOR;
            }
            else if (aeroDamage >= trivialAeroDamageThreshold)
            {
                return DamageLevel.TRIVIAL;
            }
            else
            {
                return DamageLevel.NONE;
            }

        }
        public Boolean isBehindWithinDistance(float trackLength, float minDistance, float maxDistance, float playerTrackDistance, float opponentTrackDistance)
        {
            float difference = playerTrackDistance - opponentTrackDistance;
            if (difference > 0)
            {
                return difference < maxDistance && difference > minDistance;
            }
            else
            {
                difference = (playerTrackDistance + trackLength) - opponentTrackDistance;
                return difference < maxDistance && difference > minDistance;
            }
        }

        private int getCurrentSector(TrackDefinition trackDef, float distanceRoundtrack, float splinePoint)
        {
            int ret = 3;
            if (sectorSplinePointsFromGame[1] > 0 && splinePoint < sectorSplinePointsFromGame[1])
            {
                ret = 1;
            }
            else if (sectorSplinePointsFromGame[2] > 0 && splinePoint < sectorSplinePointsFromGame[2])
            {
                ret = 2;
            }
            else if (distanceRoundtrack >= 0 && distanceRoundtrack < trackDef.sectorPoints[0])
            {
                ret = 1;
            }
            else if (distanceRoundtrack >= trackDef.sectorPoints[0] && (trackDef.sectorPoints[1] == 0 || distanceRoundtrack < trackDef.sectorPoints[1]))
            {
                ret = 2;
            }
            return ret;
        }
        void logSectorsForUnknownTracks(TrackDefinition trackDef, float distanceRoundTrack, int currentSector)
        {
            if (loggedSectorStart[0] == -1 && currentSector == 2)
            {
                loggedSectorStart[0] = distanceRoundTrack;
            }
            if (loggedSectorStart[1] == -1 && currentSector == 3)
            {
                loggedSectorStart[1] = distanceRoundTrack;
            }
            if (trackDef.sectorsOnTrack == 2 && loggedSectorStart[0] != -1)
            {
                System.Diagnostics.Debug.WriteLine("new TrackDefinition(\"" + trackDef.name + "\", " + trackDef.trackLength + ", " + trackDef.sectorsOnTrack + ", new float[] {" + loggedSectorStart[0] + "f, " + 0 + "f})");
            }
            else if (trackDef.sectorsOnTrack == 3 && loggedSectorStart[0] != -1 && loggedSectorStart[1] != -1)
            {
                System.Diagnostics.Debug.WriteLine("new TrackDefinition(\"" + trackDef.name + "\", " + trackDef.trackLength + "f, " + trackDef.sectorsOnTrack + ", new float[] {" + loggedSectorStart[0] + "f, " + loggedSectorStart[1] + "f})");
            }
        }

        private float spLineLengthToDistanceRoundTrack(float trackLength, float spLine)
        {
            if (spLine < 0.0f)
            {
                spLine -= 1f;
            }
            return spLine * trackLength;
        }
    }
}
