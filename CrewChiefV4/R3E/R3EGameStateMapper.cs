using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrewChiefV4.GameState;
using CrewChiefV4.Events;
using CrewChiefV4.RaceRoom.RaceRoomData;
using System.Diagnostics;
using CrewChiefV4.R3E;
using CrewChiefV4.Audio;

/**
 * Maps memory mapped file to a local game-agnostic representation.
 */
namespace CrewChiefV4.RaceRoom
{
    public class R3EGameStateMapper : GameStateMapper
    {
        private Boolean versionChecked = false;
        // this is set when we first join a practice or qual session (or when the session first starts). All participants
        // car classes are recalculated every tick for 5 seconds
        private DateTime recheckCarClassesUntil = DateTime.MinValue;
        private TimeSpan recheckCarClassesForSeconds = TimeSpan.FromSeconds(5);

        private TimeSpan minimumSessionParticipationTime = TimeSpan.FromSeconds(6);

        private List<CornerData.EnumWithThresholds> suspensionDamageThresholds = new List<CornerData.EnumWithThresholds>();
        private List<CornerData.EnumWithThresholds> tyreWearThresholds = new List<CornerData.EnumWithThresholds>();
        private List<CornerData.EnumWithThresholds> brakeDamageThresholds = new List<CornerData.EnumWithThresholds>();
        private List<CornerData.EnumWithThresholds> tyreDirtPickupThresholds = new List<CornerData.EnumWithThresholds>();

        // recent r3e changes to tyre wear levels / rates - the data in the block appear to 
        // have changed recently, with about 0.94 representing 'worn out' - these start at (or close to) 1
        // and drop, so 0.06 worth of wear means "worn out". 
        private float wornOutTyreWearLevel = 0f;

        private float scrubbedTyreWearPercent = 2f;
        private float minorTyreWearPercent = 25f;
        private float majorTyreWearPercent = 55f;
        private float wornOutTyreWearPercent = 85f;

        private float trivialAeroDamageThreshold = 0.99995f;
        private float trivialEngineDamageThreshold = 0.995f;
        private float trivialTransmissionDamageThreshold = 0.99f;

        private float minorTransmissionDamageThreshold = 0.97f;
        private float minorEngineDamageThreshold = 0.99f;
        private float minorAeroDamageThreshold = 0.995f;

        private float severeTransmissionDamageThreshold = 0.4f;
        private float severeEngineDamageThreshold = 0.6f;
        private float severeAeroDamageThreshold = 0.95f;

        private float destroyedTransmissionThreshold = 0.1f;
        private float destroyedEngineThreshold = 0.1f;
        private float destroyedAeroThreshold = 0.8f;

        private float trivialSuspensionDamageThresholdPercent = 4f;
        private float minorSuspensionDamageThresholdPercent = 14f;
        private float severeSuspensionDamageThresholdPercent = 20f;
        private float destroyedSuspensionThresholdPercent = 50f;

        private float severeTyreDirtPickupThreshold = 0.6f;

        private List<CornerData.EnumWithThresholds> brakeTempThresholdsForPlayersCar = null;
        // blue flag zone for improvised blues when the 'full flag rules' are disabled
        private Boolean useImprovisedBlueFlagDetection = true;
        private readonly int blueFlagDetectionDistance = UserSettings.GetUserSettings().getInt("r3e_blue_flag_detection_distance");

        Dictionary<string, DateTime> lastActiveTimeForOpponents = new Dictionary<string, DateTime>();
        DateTime nextOpponentCleanupTime = DateTime.MinValue;
        TimeSpan opponentCleanupInterval = TimeSpan.FromSeconds(2);

        HashSet<int> positionsFilledForThisTick = new HashSet<int>();
        List<String> opponentDriverNamesProcessedForThisTick = new List<String>();

        private DateTime lastTimeEngineWasRunning = DateTime.MaxValue;
        private HashSet<string> ghostOpponents = new HashSet<string>();

        // True while on HotLap/Qualifying flying start lap.
        private bool approachingFirstFlyingLap = false;

        private bool chequeredFlagShownInThisSession = false;

        // update the expected finishing position regularly in non-race sessions
        private DateTime nextExpectedFinishingPositionUpdateDue = DateTime.MinValue;

        class PendingRacePositionChange
        {
            public int newPosition;
            public DateTime positionChangeTime;
            public PendingRacePositionChange(int newPosition, DateTime positionChangeTime)
            {
                this.newPosition = newPosition;
                this.positionChangeTime = positionChangeTime;
            }
        }

        public R3EGameStateMapper()
        {
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.NEW, -10000, scrubbedTyreWearPercent));
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.SCRUBBED, scrubbedTyreWearPercent, minorTyreWearPercent));
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.MINOR_WEAR, minorTyreWearPercent, majorTyreWearPercent));
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.MAJOR_WEAR, majorTyreWearPercent, wornOutTyreWearPercent));
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.WORN_OUT, wornOutTyreWearPercent, 10000));

            CornerData.EnumWithThresholds suspensionDamageNone = new CornerData.EnumWithThresholds(DamageLevel.NONE, -10000, trivialSuspensionDamageThresholdPercent);
            CornerData.EnumWithThresholds suspensionDamageTrivial = new CornerData.EnumWithThresholds(DamageLevel.TRIVIAL, trivialSuspensionDamageThresholdPercent, minorSuspensionDamageThresholdPercent);
            CornerData.EnumWithThresholds suspensionDamageMinor = new CornerData.EnumWithThresholds(DamageLevel.MINOR, trivialSuspensionDamageThresholdPercent, severeSuspensionDamageThresholdPercent);
            CornerData.EnumWithThresholds suspensionDamageMajor = new CornerData.EnumWithThresholds(DamageLevel.MAJOR, severeSuspensionDamageThresholdPercent, destroyedSuspensionThresholdPercent);
            CornerData.EnumWithThresholds suspensionDamageDestroyed = new CornerData.EnumWithThresholds(DamageLevel.DESTROYED, destroyedSuspensionThresholdPercent, 10000);
            suspensionDamageThresholds.Add(suspensionDamageNone);
            suspensionDamageThresholds.Add(suspensionDamageTrivial);
            suspensionDamageThresholds.Add(suspensionDamageMinor);
            suspensionDamageThresholds.Add(suspensionDamageMajor);
            suspensionDamageThresholds.Add(suspensionDamageDestroyed);

            tyreDirtPickupThresholds.Add(new CornerData.EnumWithThresholds(TyreDirtPickupState.NONE, 0.0f, severeTyreDirtPickupThreshold));
            tyreDirtPickupThresholds.Add(new CornerData.EnumWithThresholds(TyreDirtPickupState.MAJOR, severeTyreDirtPickupThreshold, 1.1f));
        }

        public override void versionCheck(Object memoryMappedFileStruct)
        {
            if (!versionChecked)
            {
                versionChecked = true;
                R3ESharedMemoryReader.R3EStructWrapper wrapper = (R3ESharedMemoryReader.R3EStructWrapper)memoryMappedFileStruct;
                string versionString = wrapper.data.VersionMajor + "." + wrapper.data.VersionMinor;
                Log.Info(() => "Shared memory version " + versionString);
                if (wrapper.data.VersionMajor != (int)RaceRoomConstant.VersionMajor.R3E_VERSION_MAJOR
                    || wrapper.data.VersionMinor != (int)RaceRoomConstant.VersionMinor.R3E_VERSION_MINOR)
                {
                    Log.Warning("Expected shared data version "
                        + (int)RaceRoomConstant.VersionMajor.R3E_VERSION_MAJOR + "." + (int)RaceRoomConstant.VersionMinor.R3E_VERSION_MINOR
                        + " but got version " + versionString);
                }
            }
        }

        public override GameStateData mapToGameStateData(Object memoryMappedFileStruct, GameStateData previousGameState)
        {
            CrewChiefV4.RaceRoom.R3ESharedMemoryReader.R3EStructWrapper wrapper = (CrewChiefV4.RaceRoom.R3ESharedMemoryReader.R3EStructWrapper)memoryMappedFileStruct;
            GameStateData currentGameState = new GameStateData(wrapper.ticksWhenRead);
            RaceRoomData.RaceRoomShared shared = wrapper.data;
            currentGameState.rawGameData = wrapper;
            useImprovisedBlueFlagDetection = shared.Flags.Blue == -1;
            currentGameState.FlagData.useImprovisedIncidentCalling = shared.Flags.Yellow == -1;

            if (shared.ControlType == (int)RaceRoomConstant.Control.Replay)
            {
                CrewChief.trackName = getNameFromBytes(shared.TrackName);
                CrewChief.carClass = CarData.getCarClassForRaceRoomId(shared.VehicleInfo.ClassId).carClassEnum;
                CrewChief.viewingReplay = true;
                CrewChief.distanceRoundTrack = shared.LapDistance;
                CrewChief.raceroomTrackId = shared.LayoutId;
            }

            if (shared.Player.GameSimulationTime <= 0 || shared.VehicleInfo.SlotId < 0 ||
                shared.ControlType == (int)RaceRoomConstant.Control.Remote || shared.ControlType == (int)RaceRoomConstant.Control.Replay)
            {
                return previousGameState;
            }
            Boolean isCarRunning = CheckIsCarRunning(shared);
            SessionPhase lastSessionPhase = SessionPhase.Unavailable;
            float lastSessionRunningTime = 0;
            if (previousGameState != null)
            {
                lastSessionPhase = previousGameState.SessionData.SessionPhase;
                lastSessionRunningTime = previousGameState.SessionData.SessionRunningTime;
                //Console.WriteLine("Raw: " + shared.CarDamage.TireRearLeft + ", calc:" + previousGameState.TyreData.RearLeftPercentWear);

                // belt n braces checks to ensure we have our static data loaded
                String trackNameFromGame = getNameFromBytes(shared.TrackName);
                if (trackNameFromGame != null && trackNameFromGame.Length > 0 && (previousGameState.SessionData.TrackDefinition == null ||
                    previousGameState.SessionData.TrackDefinition.name == null || previousGameState.SessionData.TrackDefinition.name.Length == 0 ||
                    !previousGameState.SessionData.TrackDefinition.name.Equals(trackNameFromGame)))
                {
                    currentGameState.SessionData.TrackDefinition = TrackData.getTrackDefinition(getNameFromBytes(shared.TrackName), shared.LayoutId, shared.LayoutLength);
                    TrackDataContainer tdc = TrackData.TRACK_LANDMARKS_DATA.getTrackDataForTrackLayoutId(shared.LayoutId);
                    currentGameState.SessionData.TrackDefinition.trackLandmarks = tdc.trackLandmarks;
                    currentGameState.SessionData.TrackDefinition.isOval = tdc.isOval;
                    currentGameState.SessionData.TrackDefinition.raceroomRollingStartLapDistance = tdc.raceroomRollingStartLapDistance;
                    currentGameState.SessionData.TrackDefinition.pitApproachPoint = tdc.pitApproachPoint;
                    currentGameState.SessionData.TrackDefinition.setGapPoints();
                    GlobalBehaviourSettings.UpdateFromTrackDefinition(currentGameState.SessionData.TrackDefinition);
                }
                currentGameState.readLandmarksForThisLap = previousGameState.readLandmarksForThisLap;

                currentGameState.SessionData.NumCarsOverallAtStartOfSession = previousGameState.SessionData.NumCarsOverallAtStartOfSession;
            }
            else
            {
                currentGameState.SessionData.TrackDefinition = TrackData.getTrackDefinition(getNameFromBytes(shared.TrackName), shared.LayoutId, shared.LayoutLength);
                TrackDataContainer tdc = TrackData.TRACK_LANDMARKS_DATA.getTrackDataForTrackLayoutId(shared.LayoutId);
                currentGameState.SessionData.TrackDefinition.trackLandmarks = tdc.trackLandmarks;
                currentGameState.SessionData.TrackDefinition.isOval = tdc.isOval;
                currentGameState.SessionData.TrackDefinition.raceroomRollingStartLapDistance = tdc.raceroomRollingStartLapDistance;
                currentGameState.SessionData.TrackDefinition.pitApproachPoint = tdc.pitApproachPoint;
                currentGameState.SessionData.TrackDefinition.setGapPoints();
                GlobalBehaviourSettings.UpdateFromTrackDefinition(currentGameState.SessionData.TrackDefinition);

                // Used by mapToSessionType.
                currentGameState.SessionData.NumCarsOverallAtStartOfSession = shared.NumCars;
                chequeredFlagShownInThisSession = false;
            }

            currentGameState.SessionData.SessionType = mapToSessionType(shared, previousGameState);

            if (previousGameState != null && currentGameState.SessionData.SessionType != previousGameState.SessionData.SessionType)
            {
                Console.WriteLine("Session type changed from: " + previousGameState.SessionData.SessionType + "  to: " + currentGameState.SessionData.SessionType
                    + "  Last session phase: " + previousGameState.SessionData.SessionPhase + "  New session phase: " + currentGameState.SessionData.SessionPhase);
            }

            currentGameState.SessionData.SessionRunningTime = (float)shared.Player.GameSimulationTime;
            currentGameState.ControlData.ControlType = mapToControlType(shared.ControlType);

            // in some cases, the session start trigger gets missed and we don't have a driver name
            currentGameState.SessionData.DriverRawName = getNameFromBytes(shared.PlayerName);

            DriverData playerDriverData = new DriverData();
            int playerDriverDataIndex = 0;
            // In hotlap/leaderboard challenge, this contains duplicate entries.  Do we want this?
            string[] driverNames = new string[shared.DriverData.Length];
            for (int i = 0; i < shared.DriverData.Length; i++)
            {
                DriverData participantStruct = shared.DriverData[i];
                String driverName = getNameFromBytes(participantStruct.DriverInfo.Name).Trim();
                driverNames[i] = driverName;
                if (driverName.Equals(currentGameState.SessionData.DriverRawName))
                {
                    playerDriverData = participantStruct;
                    playerDriverDataIndex = i;
                }
            }
            if (!R3ERatings.gotPlayerRating)
            {
                R3ERatings.getRatingForPlayer(playerDriverData.DriverInfo.UserId);
            }
            currentGameState.PositionAndMotionData.WorldPosition = new float[] { (float)playerDriverData.Position.X, (float)playerDriverData.Position.Y, (float)playerDriverData.Position.Z };
            int previousLapsCompleted = previousGameState == null ? 0 : previousGameState.SessionData.CompletedLaps;
            currentGameState.SessionData.SessionPhase = mapToSessionPhase(lastSessionPhase, currentGameState.SessionData.SessionType, lastSessionRunningTime,
                currentGameState.SessionData.SessionRunningTime, shared.SessionPhase, currentGameState.ControlData.ControlType,
                previousLapsCompleted, shared.CompletedLaps, isCarRunning, chequeredFlagShownInThisSession, shared.StartLights);

            // yuk, another session end hack. Catch the tick when q and p session timer reaches zero (but not when it's reset to zero as a result of quitting the session)
            if ((currentGameState.SessionData.SessionType == SessionType.Qualify || currentGameState.SessionData.SessionType == SessionType.Practice)
                && previousGameState != null && previousGameState.SessionData.SessionPhase != SessionPhase.Finished
                && previousGameState.SessionData.SessionTimeRemaining > 0 && previousGameState.SessionData.SessionTimeRemaining < 0.2)
            {
                currentGameState.SessionData.SessionPhase = SessionPhase.Finished;
            }

            if ((lastSessionPhase != currentGameState.SessionData.SessionPhase && (lastSessionPhase == SessionPhase.Unavailable || lastSessionPhase == SessionPhase.Finished)) ||
                ((lastSessionPhase == SessionPhase.Checkered || lastSessionPhase == SessionPhase.Finished || lastSessionPhase == SessionPhase.Green || lastSessionPhase == SessionPhase.FullCourseYellow) &&
                    currentGameState.SessionData.SessionPhase == SessionPhase.Countdown) ||
                lastSessionRunningTime > currentGameState.SessionData.SessionRunningTime)
            {
                R3EPitMenuManager.hasStateForCurrentSession = false;
                currentGameState.SessionData.IsNewSession = true;
                chequeredFlagShownInThisSession = false;
                // if this is a new prac / qual session, we might have just joined a multiclass session so we need to keep
                // updating the car class until it settles.
                // Also allow this during race start countdown
                if (currentGameState.SessionData.SessionType == SessionType.Qualify
                    || currentGameState.SessionData.SessionType == SessionType.Practice
                    || (currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.SessionPhase != SessionPhase.Green))
                {
                    recheckCarClassesUntil = currentGameState.Now.Add(recheckCarClassesForSeconds);
                }
                else
                {
                    // probably don't need to reset this but it does no harm
                    recheckCarClassesUntil = DateTime.MinValue;
                }
                Console.WriteLine("New session, trigger data:");
                Console.WriteLine("lastSessionPhase = " + lastSessionPhase);
                Console.WriteLine("lastSessionRunningTime = " + lastSessionRunningTime);
                Console.WriteLine("currentSessionPhase = " + currentGameState.SessionData.SessionPhase);
                Console.WriteLine("rawSessionPhase = " + shared.SessionPhase);
                Console.WriteLine("currentSessionRunningTime = " + currentGameState.SessionData.SessionRunningTime);

                if ((currentGameState.SessionData.SessionType == SessionType.Practice || currentGameState.SessionData.SessionType == SessionType.Qualify)
                    && shared.InPitlane == 1)
                {
                    currentGameState.PitData.PitBoxPositionEstimate = shared.LapDistance;
                    currentGameState.PitData.PitBoxLocationEstimate = new float[] { playerDriverData.Position.X, playerDriverData.Position.Y, playerDriverData.Position.Z };
                    Console.WriteLine("Pit box position = " + currentGameState.PitData.PitBoxPositionEstimate.ToString("0.000"));
                }
                else if (previousGameState != null)
                {
                    // if we're entering a race session or rolling qually, copy the value from the previous field
                    currentGameState.PitData.PitBoxPositionEstimate = previousGameState.PitData.PitBoxPositionEstimate;
                    currentGameState.PitData.PitBoxLocationEstimate = previousGameState.PitData.PitBoxLocationEstimate;
                }


                currentGameState.SessionData.NumCarsOverallAtStartOfSession = shared.NumCars;
                currentGameState.SessionData.EventIndex = shared.EventIndex;
                // correct session iteration - it starts at 1 in the game data but we expect it to be zero-indexed
                currentGameState.SessionData.SessionIteration = shared.SessionIteration <= 0 ? 0 : shared.SessionIteration - 1;
                currentGameState.SessionData.SessionStartTime = currentGameState.Now;
                currentGameState.OpponentData.Clear();
                ghostOpponents.Clear();
                currentGameState.SessionData.TrackDefinition = TrackData.getTrackDefinition(getNameFromBytes(shared.TrackName), shared.LayoutId, shared.LayoutLength);
                TrackDataContainer tdc = TrackData.TRACK_LANDMARKS_DATA.getTrackDataForTrackLayoutId(shared.LayoutId);
                currentGameState.SessionData.TrackDefinition.trackLandmarks = tdc.trackLandmarks;
                currentGameState.SessionData.TrackDefinition.isOval = tdc.isOval;
                currentGameState.SessionData.TrackDefinition.raceroomRollingStartLapDistance = tdc.raceroomRollingStartLapDistance;
                currentGameState.SessionData.TrackDefinition.pitApproachPoint = tdc.pitApproachPoint;
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
                currentGameState.PitData.IsRefuellingAllowed = true;

                lastActiveTimeForOpponents.Clear();
                nextOpponentCleanupTime = currentGameState.Now + opponentCleanupInterval;

                if (shared.SessionLengthFormat == 0 || shared.SessionLengthFormat == 2 || shared.SessionTimeRemaining > 0)
                {
                    currentGameState.SessionData.SessionTotalRunTime = shared.SessionTimeRemaining;
                    currentGameState.SessionData.SessionHasFixedTime = true;
                }

                lastTimeEngineWasRunning = DateTime.MaxValue;
                opponentDriverNamesProcessedForThisTick.Clear();
                for (int i = 0; i < shared.DriverData.Length; i++)
                {
                    DriverData participantStruct = shared.DriverData[i];
                    String driverName = driverNames[i];
                    if (i == playerDriverDataIndex)
                    {
                        currentGameState.SessionData.IsNewSector = previousGameState == null || participantStruct.TrackSector != previousGameState.SessionData.SectorNumber;
                        currentGameState.SessionData.SectorNumber = participantStruct.TrackSector;
                        if (driverName.Length > 0)
                        {
                            AdditionalDataProvider.validate(driverName);
                        }
                        currentGameState.PitData.InPitlane = participantStruct.InPitlane == 1;
                        currentGameState.PositionAndMotionData.DistanceRoundTrack = participantStruct.LapDistance;
                        // sanity check the car class data before using it - retain the previous state's class if we need to
                        if (participantStruct.DriverInfo.ClassId <= 0 && previousGameState != null)
                        {
                            currentGameState.carClass = previousGameState.carClass;
                            currentGameState.SessionData.PlayerCarNr = previousGameState.SessionData.PlayerCarNr;
                            Console.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier() + " copied from previous game state");
                        }
                        else
                        {
                            // Note that the R3E SafetyCar is car class ID 10743
                            currentGameState.carClass = CarData.getCarClassForRaceRoomId(participantStruct.DriverInfo.ClassId);
                            currentGameState.carClass.ClassPerformanceIndex = participantStruct.DriverInfo.ClassPerformanceIndex;
                            currentGameState.SessionData.PlayerCarNr = participantStruct.DriverInfo.CarNumber.ToString();
                            CarData.RACEROOM_CLASS_ID = participantStruct.DriverInfo.ClassId;
                            // car length / width now added to shared memory
                            currentGameState.carClass.spotterVehicleLength = playerDriverData.DriverInfo.CarLength;
                            currentGameState.carClass.spotterVehicleWidth = playerDriverData.DriverInfo.CarWidth;
                            GlobalBehaviourSettings.UpdateFromCarClass(currentGameState.carClass);
                            Console.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier() + " (class ID " + participantStruct.DriverInfo.ClassId + ")");
                        }
                        brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(currentGameState.carClass);

                        if ((currentGameState.SessionData.SessionType == SessionType.Qualify || currentGameState.SessionData.SessionType == SessionType.HotLap) &&
                            currentGameState.SessionData.SectorNumber != 1 && !currentGameState.PitData.InPitlane)
                        {
                            // Assume that Qualify/HotLap starts with flying lap.  This is cleared out when we begin the new lap.
                            approachingFirstFlyingLap = true;
                        }
                        else
                        {
                            approachingFirstFlyingLap = false;
                        }
                    }
                    else
                    {
                        if (driverName.Length > 0 && currentGameState.SessionData.DriverRawName != driverName)
                        {
                            if (!opponentDriverNamesProcessedForThisTick.Contains(driverName))
                            {
                                opponentDriverNamesProcessedForThisTick.Add(driverName);
                                var opponentData = createOpponentData(participantStruct, driverName,
                                    false, currentGameState.SessionData.TrackDefinition.trackLength);
                                opponentData.CarClass.ClassPerformanceIndex = participantStruct.DriverInfo.ClassPerformanceIndex;
                                currentGameState.OpponentData.Add(driverName, opponentData);
                            }
                        }
                    }
                }
                currentGameState.SessionData.DeltaTime = new DeltaTime(currentGameState.SessionData.TrackDefinition.trackLength,
                    currentGameState.PositionAndMotionData.DistanceRoundTrack, currentGameState.PositionAndMotionData.CarSpeed, currentGameState.Now);
            }
            else
            {
                if (lastSessionPhase != currentGameState.SessionData.SessionPhase)
                {
                    Console.WriteLine("New session phase, was " + lastSessionPhase + " now " + currentGameState.SessionData.SessionPhase);
                    if (currentGameState.SessionData.SessionPhase == SessionPhase.Green)
                    {
                        currentGameState.SessionData.JustGoneGreen = true;
                        chequeredFlagShownInThisSession = false;
                        // just gone green, so get the session data
                        if (shared.SessionLengthFormat == 0 || shared.SessionLengthFormat == 2 || shared.SessionTimeRemaining > 0)
                        {
                            currentGameState.SessionData.SessionTotalRunTime = shared.SessionTimeRemaining;
                            if (shared.SessionLengthFormat == 2)
                            {
                                currentGameState.SessionData.ExtraLapsAfterTimedSessionComplete = 1;
                            }
                            currentGameState.SessionData.SessionHasFixedTime = true;
                        }
                        else if (shared.NumberOfLaps > 0)
                        {
                            currentGameState.SessionData.SessionNumberOfLaps = shared.NumberOfLaps;
                            currentGameState.SessionData.SessionHasFixedTime = false;
                        }

                        lastActiveTimeForOpponents.Clear();
                        nextOpponentCleanupTime = currentGameState.Now + opponentCleanupInterval;

                        currentGameState.SessionData.NumCarsOverallAtStartOfSession = shared.NumCars;
                        currentGameState.SessionData.SessionStartTime = currentGameState.Now;
                        if (shared.VehicleInfo.ClassId <= 0 && previousGameState != null)
                        {
                            currentGameState.carClass = previousGameState.carClass;
                            Console.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier() + "(copied from previous game state)");
                        }
                        else
                        {
                            currentGameState.carClass = CarData.getCarClassForRaceRoomId(shared.VehicleInfo.ClassId);
                            currentGameState.carClass.ClassPerformanceIndex = shared.VehicleInfo.ClassPerformanceIndex;
                            CarData.RACEROOM_CLASS_ID = shared.VehicleInfo.ClassId;
                            // car length / width now added to shared memory
                            currentGameState.carClass.spotterVehicleLength = playerDriverData.DriverInfo.CarLength;
                            currentGameState.carClass.spotterVehicleWidth = playerDriverData.DriverInfo.CarWidth;
                            GlobalBehaviourSettings.UpdateFromCarClass(currentGameState.carClass);
                            Console.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier());
                        }
                        brakeTempThresholdsForPlayersCar = getBrakeTempThresholds(shared.BrakeTemp);
                        if (previousGameState != null)
                        {
                            currentGameState.PitData.IsRefuellingAllowed = previousGameState.PitData.IsRefuellingAllowed;
                            currentGameState.OpponentData = previousGameState.OpponentData;
                            currentGameState.SessionData.TrackDefinition = previousGameState.SessionData.TrackDefinition;
                            currentGameState.SessionData.DriverRawName = previousGameState.SessionData.DriverRawName;
                            currentGameState.PositionAndMotionData.DistanceRoundTrack = playerDriverData.LapDistance;
                            currentGameState.PitData.PitBoxPositionEstimate = previousGameState.PitData.PitBoxPositionEstimate;
                            currentGameState.PitData.PitBoxLocationEstimate = previousGameState.PitData.PitBoxLocationEstimate;
                        }

                        // get the SoF once at the race start, so it's fixed for the duration of the session. Note that this will
                        // be inaccurate if opponents disconnect while on the grid but the alternative is too fiddly:
                        currentGameState.SessionData.StrengthOfField = R3ERatings.getAverageRatingForParticipants(currentGameState.OpponentData);

                        currentGameState.PitData.PitWindowStart = shared.PitWindowStart;
                        currentGameState.PitData.PitWindowEnd = shared.PitWindowEnd;
                        currentGameState.PitData.HasMandatoryPitStop = currentGameState.PitData.PitWindowStart > 0 && currentGameState.PitData.PitWindowEnd > 0;
                        if (currentGameState.PitData.HasMandatoryPitStop)
                        {
                            if (currentGameState.carClass.carClassEnum == CarData.CarClassEnum.DTM_2014 ||
                                currentGameState.carClass.carClassEnum == CarData.CarClassEnum.DTM_2015 || currentGameState.carClass.carClassEnum == CarData.CarClassEnum.DTM_2016)
                            {
                                // iteration 1 of the DTM 2015 doesn't have a mandatory tyre change, but this means the pit window stuff won't be set, so we're (kind of) OK here...
                                currentGameState.PitData.HasMandatoryTyreChange = true;
                            }
                            if (currentGameState.PitData.HasMandatoryTyreChange && currentGameState.PitData.MandatoryTyreChangeRequiredTyreType == TyreType.Unknown_Race)
                            {
                                if (currentGameState.carClass.carClassEnum == CarData.CarClassEnum.DTM_2014)
                                {
                                    double halfRaceDistance = currentGameState.SessionData.SessionNumberOfLaps / 2d;
                                    if (mapToTyreType(shared.TireTypeFront, shared.TireSubtypeFront, shared.TireTypeRear, shared.TireSubtypeFront,
                                        currentGameState.carClass.carClassEnum) == TyreType.Option)
                                    {
                                        currentGameState.PitData.MandatoryTyreChangeRequiredTyreType = TyreType.Prime;
                                        currentGameState.PitData.MaxPermittedDistanceOnCurrentTyre = ((int)Math.Floor(halfRaceDistance)) - 1;
                                    }
                                    else
                                    {
                                        currentGameState.PitData.MandatoryTyreChangeRequiredTyreType = TyreType.Option;
                                        currentGameState.PitData.MinPermittedDistanceOnCurrentTyre = (int)Math.Ceiling(halfRaceDistance);
                                    }
                                }
                                else if (currentGameState.carClass.carClassEnum == CarData.CarClassEnum.DTM_2015 || currentGameState.carClass.carClassEnum == CarData.CarClassEnum.DTM_2016)
                                {
                                    currentGameState.PitData.MandatoryTyreChangeRequiredTyreType = TyreType.Prime;
                                    // the mandatory change must be completed by the end of the pit window
                                    currentGameState.PitData.MaxPermittedDistanceOnCurrentTyre = (int)currentGameState.PitData.PitWindowEnd;
                                }
                            }
                        }

                        currentGameState.SessionData.DeltaTime = new DeltaTime(currentGameState.SessionData.TrackDefinition.trackLength,
                            currentGameState.PositionAndMotionData.DistanceRoundTrack, currentGameState.PositionAndMotionData.CarSpeed, currentGameState.Now);

                        Console.WriteLine("Just gone green, session details...");

                        Console.WriteLine("SessionType " + currentGameState.SessionData.SessionType);
                        Console.WriteLine("SessionPhase " + currentGameState.SessionData.SessionPhase);
                        Console.WriteLine("EventIndex " + currentGameState.SessionData.EventIndex);
                        Console.WriteLine("SessionIteration " + currentGameState.SessionData.SessionIteration);
                        Console.WriteLine("HasMandatoryPitStop " + currentGameState.PitData.HasMandatoryPitStop);
                        Console.WriteLine("PitWindowStart " + currentGameState.PitData.PitWindowStart);
                        Console.WriteLine("PitWindowEnd " + currentGameState.PitData.PitWindowEnd);
                        Console.WriteLine("NumCarsAtStartOfSession " + currentGameState.SessionData.NumCarsOverallAtStartOfSession);
                        Console.WriteLine("SessionNumberOfLaps " + currentGameState.SessionData.SessionNumberOfLaps);
                        Console.WriteLine("SessionRunTime " + currentGameState.SessionData.SessionTotalRunTime);
                        Console.WriteLine("SessionStartTime " + currentGameState.SessionData.SessionStartTime);
                        String trackName = currentGameState.SessionData.TrackDefinition == null ? "unknown" : currentGameState.SessionData.TrackDefinition.name;
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
                        Console.WriteLine("TrackName " + trackName);
                        Console.WriteLine("TrackLayoutID " + shared.LayoutId);
                        // recalculate the expected finish position on race start to account for drop-outs
                        currentGameState.SessionData.expectedFinishingPosition = R3ERatings.calculateExpectedFinishPosition(currentGameState.OpponentData, currentGameState.carClass);
                    }
                }
                if (!currentGameState.SessionData.JustGoneGreen && previousGameState != null)
                {
                    currentGameState.SessionData.SessionStartTime = previousGameState.SessionData.SessionStartTime;
                    currentGameState.SessionData.SessionTotalRunTime = previousGameState.SessionData.SessionTotalRunTime;
                    currentGameState.SessionData.SessionNumberOfLaps = previousGameState.SessionData.SessionNumberOfLaps;
                    currentGameState.SessionData.SessionHasFixedTime = previousGameState.SessionData.SessionHasFixedTime;
                    currentGameState.SessionData.ExtraLapsAfterTimedSessionComplete = previousGameState.SessionData.ExtraLapsAfterTimedSessionComplete;
                    currentGameState.SessionData.NumCarsOverallAtStartOfSession = previousGameState.SessionData.NumCarsOverallAtStartOfSession;
                    currentGameState.SessionData.NumCarsInPlayerClassAtStartOfSession = previousGameState.SessionData.NumCarsInPlayerClassAtStartOfSession;
                    currentGameState.SessionData.EventIndex = previousGameState.SessionData.EventIndex;
                    currentGameState.SessionData.SessionIteration = previousGameState.SessionData.SessionIteration;
                    currentGameState.SessionData.PositionAtStartOfCurrentLap = previousGameState.SessionData.PositionAtStartOfCurrentLap;
                    currentGameState.SessionData.SessionStartClassPosition = previousGameState.SessionData.SessionStartClassPosition;

                    currentGameState.SessionData.ClassPositionAtStartOfCurrentLap = previousGameState.SessionData.ClassPositionAtStartOfCurrentLap;
                    currentGameState.PitData.PitWindowStart = previousGameState.PitData.PitWindowStart;
                    currentGameState.PitData.PitWindowEnd = previousGameState.PitData.PitWindowEnd;
                    currentGameState.PitData.HasMandatoryPitStop = previousGameState.PitData.HasMandatoryPitStop;
                    currentGameState.PitData.HasMandatoryTyreChange = previousGameState.PitData.HasMandatoryTyreChange;
                    currentGameState.PitData.MandatoryTyreChangeRequiredTyreType = previousGameState.PitData.MandatoryTyreChangeRequiredTyreType;
                    currentGameState.PitData.IsRefuellingAllowed = previousGameState.PitData.IsRefuellingAllowed;
                    currentGameState.PitData.MaxPermittedDistanceOnCurrentTyre = previousGameState.PitData.MaxPermittedDistanceOnCurrentTyre;
                    currentGameState.PitData.MinPermittedDistanceOnCurrentTyre = previousGameState.PitData.MinPermittedDistanceOnCurrentTyre;
                    currentGameState.PitData.OnInLap = previousGameState.PitData.OnInLap;
                    currentGameState.PitData.OnOutLap = previousGameState.PitData.OnOutLap;
                    currentGameState.PitData.NumPitStops = previousGameState.PitData.NumPitStops;
                    currentGameState.PitData.PitBoxPositionEstimate = previousGameState.PitData.PitBoxPositionEstimate;
                    currentGameState.PitData.PitBoxLocationEstimate = previousGameState.PitData.PitBoxLocationEstimate;
                    currentGameState.SessionData.TrackDefinition = previousGameState.SessionData.TrackDefinition;
                    currentGameState.SessionData.formattedPlayerLapTimes = previousGameState.SessionData.formattedPlayerLapTimes;
                    currentGameState.SessionData.PlayerLapTimeSessionBest = previousGameState.SessionData.PlayerLapTimeSessionBest;
                    currentGameState.SessionData.PlayerLapTimeSessionBestPrevious = previousGameState.SessionData.PlayerLapTimeSessionBestPrevious;
                    currentGameState.SessionData.OpponentsLapTimeSessionBestOverall = previousGameState.SessionData.OpponentsLapTimeSessionBestOverall;
                    currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass = previousGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass;
                    currentGameState.carClass = previousGameState.carClass;
                    currentGameState.SessionData.PlayerClassSessionBestLapTimeByTyre = previousGameState.SessionData.PlayerClassSessionBestLapTimeByTyre;
                    currentGameState.SessionData.PlayerBestLapTimeByTyre = previousGameState.SessionData.PlayerBestLapTimeByTyre;
                    currentGameState.SessionData.DriverRawName = previousGameState.SessionData.DriverRawName;
                    currentGameState.SessionData.SessionTimesAtEndOfSectors = previousGameState.SessionData.SessionTimesAtEndOfSectors;
                    currentGameState.SessionData.LapTimePreviousEstimateForInvalidLap = previousGameState.SessionData.LapTimePreviousEstimateForInvalidLap;
                    currentGameState.SessionData.OverallSessionBestLapTime = previousGameState.SessionData.OverallSessionBestLapTime;
                    currentGameState.SessionData.PlayerClassSessionBestLapTime = previousGameState.SessionData.PlayerClassSessionBestLapTime;
                    currentGameState.SessionData.GameTimeAtLastPositionFrontChange = previousGameState.SessionData.GameTimeAtLastPositionFrontChange;
                    currentGameState.SessionData.GameTimeAtLastPositionBehindChange = previousGameState.SessionData.GameTimeAtLastPositionBehindChange;
                    currentGameState.SessionData.OpponentKeyInFront = previousGameState.SessionData.OpponentKeyInFront;
                    currentGameState.SessionData.OpponentKeyBehind = previousGameState.SessionData.OpponentKeyBehind;
                    currentGameState.SessionData.CurrentLapIsValid = previousGameState.SessionData.CurrentLapIsValid;
                    currentGameState.SessionData.PreviousLapWasValid = previousGameState.SessionData.PreviousLapWasValid;
                    currentGameState.SessionData.LapTimePrevious = previousGameState.SessionData.LapTimePrevious;
                    currentGameState.SessionData.LastSector1Time = previousGameState.SessionData.LastSector1Time;
                    currentGameState.SessionData.LastSector2Time = previousGameState.SessionData.LastSector2Time;
                    currentGameState.SessionData.LastSector3Time = previousGameState.SessionData.LastSector3Time;
                    currentGameState.SessionData.PlayerBestSector1Time = previousGameState.SessionData.PlayerBestSector1Time;
                    currentGameState.SessionData.PlayerBestSector2Time = previousGameState.SessionData.PlayerBestSector2Time;
                    currentGameState.SessionData.PlayerBestSector3Time = previousGameState.SessionData.PlayerBestSector3Time;
                    currentGameState.SessionData.PlayerBestLapSector1Time = previousGameState.SessionData.PlayerBestLapSector1Time;
                    currentGameState.SessionData.PlayerBestLapSector2Time = previousGameState.SessionData.PlayerBestLapSector2Time;
                    currentGameState.SessionData.PlayerBestLapSector3Time = previousGameState.SessionData.PlayerBestLapSector3Time;
                    currentGameState.SessionData.trackLandmarksTiming = previousGameState.SessionData.trackLandmarksTiming;
                    currentGameState.SessionData.PlayerCarNr = previousGameState.SessionData.PlayerCarNr;

                    currentGameState.FlagData.useImprovisedIncidentCalling = previousGameState.FlagData.useImprovisedIncidentCalling;

                    currentGameState.SessionData.DeltaTime = previousGameState.SessionData.DeltaTime;

                    currentGameState.retriedDriverNames = previousGameState.retriedDriverNames;
                    currentGameState.disqualifiedDriverNames = previousGameState.disqualifiedDriverNames;

                    currentGameState.hardPartsOnTrackData = previousGameState.hardPartsOnTrackData;

                    currentGameState.SessionData.PlayerLapData = previousGameState.SessionData.PlayerLapData;

                    currentGameState.TimingData = previousGameState.TimingData;

                    currentGameState.SessionData.JustGoneGreenTime = previousGameState.SessionData.JustGoneGreenTime;
                    currentGameState.SessionData.StrengthOfField = previousGameState.SessionData.StrengthOfField;

                    currentGameState.SessionData.expectedFinishingPosition = previousGameState.SessionData.expectedFinishingPosition;

                    currentGameState.Conditions.CurrentConditions = previousGameState.Conditions.CurrentConditions;
                    currentGameState.Conditions.samples = previousGameState.Conditions.samples;
                }
            }

            currentGameState.ControlData.ThrottlePedal = shared.Throttle;
            currentGameState.ControlData.ClutchPedal = shared.Clutch;
            currentGameState.ControlData.BrakePedal = shared.Brake;
            currentGameState.ControlData.BrakeBias = shared.BrakeBias;
            currentGameState.TransmissionData.Gear = shared.Gear;

            //------------------------ Session data -----------------------
            currentGameState.SessionData.Flag = FlagEnum.UNKNOWN;

            // only allow flag data when the session's been running for a couple of seconds - hopefully this will reduce the spurious
            // yellow flags right at the start of race sessions
            if (currentGameState.SessionData.SessionType != SessionType.Race || shared.Player.GameSimulationTime > 3)
            {
                // Mark Yellow sectors.
                // note that any yellow flag info will switch off improvised incident calling for the remainder of the session
                if (shared.Flags.SectorYellow.Sector1 == 1)
                {
                    currentGameState.FlagData.sectorFlags[0] = FlagEnum.YELLOW;
                }
                if (shared.Flags.SectorYellow.Sector2 == 1)
                {
                    currentGameState.FlagData.sectorFlags[1] = FlagEnum.YELLOW;
                }
                if (shared.Flags.SectorYellow.Sector3 == 1)
                {
                    currentGameState.FlagData.sectorFlags[2] = FlagEnum.YELLOW;
                }
                filterBadFlags(shared.TrackId, previousGameState == null ? null : previousGameState.FlagData.sectorFlags, currentGameState.FlagData.sectorFlags);
                currentGameState.FlagData.isLocalYellow = shared.Flags.Yellow == 1;

                if (shared.Flags.White == 1 && !currentGameState.FlagData.isLocalYellow)
                {
                    currentGameState.SessionData.Flag = FlagEnum.WHITE;
                }
                else if (shared.Flags.Black == 1)
                {
                    currentGameState.SessionData.Flag = FlagEnum.BLACK;
                }

                if (shared.Flags.Checkered == 1 && currentGameState.SessionData.SessionPhase == SessionPhase.Green)
                {
                    chequeredFlagShownInThisSession = true;
                }

                currentGameState.FlagData.numCarsPassedIllegally = shared.Flags.YellowPositionsGained;
                if (shared.Flags.YellowOvertake == 1)
                {
                    currentGameState.FlagData.canOvertakeCarInFront = PassAllowedUnderYellow.YES;
                }
                else if (shared.Flags.YellowOvertake == 0)
                {
                    currentGameState.FlagData.canOvertakeCarInFront = PassAllowedUnderYellow.NO;
                }

                // closestYellowLapDistance is the distance roundn the lap from the player to the incident
                if (shared.Flags.ClosestYellowDistanceIntoTrack > 0)
                {
                    currentGameState.FlagData.distanceToNearestIncident = shared.Flags.ClosestYellowDistanceIntoTrack;
                }

                if (shared.Flags.Blue == 1)
                {
                    currentGameState.SessionData.Flag = FlagEnum.BLUE;
                }
            }

            currentGameState.SessionData.SessionTimeRemaining = shared.SessionTimeRemaining;
            currentGameState.SessionData.CompletedLaps = shared.CompletedLaps;
            currentGameState.SessionData.LapCount = currentGameState.SessionData.CompletedLaps + 1;
            currentGameState.SessionData.MaxIncidentCount = shared.MaxIncidentPoints;
            currentGameState.SessionData.HasLimitedIncidents = shared.MaxIncidentPoints > 0;

            currentGameState.SessionData.LapTimeCurrent = shared.LapTimeCurrentSelf;
            currentGameState.SessionData.NumCarsOverall = shared.NumCars;

            currentGameState.SessionData.OverallPosition = currentGameState.SessionData.SessionType == SessionType.Race && previousGameState != null ?
                getRacePosition(currentGameState.SessionData.DriverRawName, previousGameState.SessionData.OverallPosition, shared.Position, currentGameState.Now)
                : shared.Position;
            // currentGameState.SessionData.Position = shared.Position;
            currentGameState.SessionData.TimeDeltaBehind = shared.TimeDeltaBehind;
            currentGameState.SessionData.TimeDeltaFront = shared.TimeDeltaFront;

            currentGameState.SessionData.SessionFastestLapTimeFromGame = shared.LapTimeBestLeader;
            currentGameState.SessionData.SessionFastestLapTimeFromGamePlayerClass = shared.LapTimeBestLeaderClass;
            if (currentGameState.SessionData.OverallSessionBestLapTime == -1 ||
                currentGameState.SessionData.OverallSessionBestLapTime > shared.LapTimeBestLeader)
            {
                currentGameState.SessionData.OverallSessionBestLapTime = shared.LapTimeBestLeader;
            }
            if (currentGameState.SessionData.PlayerClassSessionBestLapTime == -1 ||
                currentGameState.SessionData.PlayerClassSessionBestLapTime > shared.LapTimeBestLeaderClass)
            {
                currentGameState.SessionData.PlayerClassSessionBestLapTime = shared.LapTimeBestLeaderClass;
            }

            if (previousGameState != null && !currentGameState.SessionData.IsNewSession)
            {
                currentGameState.OpponentData = previousGameState.OpponentData;
                currentGameState.SessionData.SectorNumber = previousGameState.SessionData.SectorNumber;
            }
            if (currentGameState.SessionData.OverallPosition == 1)
            {
                currentGameState.SessionData.LeaderSectorNumber = currentGameState.SessionData.SectorNumber;
            }

            currentGameState.SessionData.CurrentIncidentCount = shared.IncidentPoints;

            foreach (DriverData participantStruct in shared.DriverData)
            {
                if (participantStruct.DriverInfo.SlotId == shared.VehicleInfo.SlotId)
                {
                    if (currentGameState.carClass.carClassEnum == CarData.CarClassEnum.UNKNOWN_RACE && participantStruct.DriverInfo.ClassId > 0)
                    {
                        CarData.CarClass newClass = CarData.getCarClassForRaceRoomId(participantStruct.DriverInfo.ClassId);
                        if (newClass.carClassEnum != currentGameState.carClass.carClassEnum)
                        {
                            currentGameState.carClass = newClass;
                            currentGameState.carClass.ClassPerformanceIndex = participantStruct.DriverInfo.ClassPerformanceIndex;
                            Console.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier() + " (class ID " + participantStruct.DriverInfo.ClassId + ")");
                            brakeTempThresholdsForPlayersCar = getBrakeTempThresholds(shared.BrakeTemp); ;
                        }
                    }
                    if (currentGameState.SessionData.CurrentLapIsValid && (participantStruct.CurrentLapValid != 1 || participantStruct.LapTimeCurrentSelf == -1) && !approachingFirstFlyingLap)
                    {
                        currentGameState.SessionData.CurrentLapIsValid = false;
                    }

                    // Note that the participantStruct.TrackSector does NOT get updated if the participant exits to the pits. If he does this,
                    // participantStruct.TrackSector will remain at whatever it was when he exited until he starts he next flying lap
                    currentGameState.SessionData.IsNewSector = participantStruct.TrackSector != 0 && currentGameState.SessionData.SectorNumber > 0
                        && currentGameState.SessionData.SectorNumber != participantStruct.TrackSector;
                    currentGameState.PitData.InPitlane = participantStruct.InPitlane == 1;
                    currentGameState.SessionData.IsNewLap = previousGameState != null && previousGameState.SessionData.IsNewLap == false &&
                        (shared.CompletedLaps == previousGameState.SessionData.CompletedLaps + 1 ||
                         (participantStruct.TrackSector == 1 && currentGameState.SessionData.IsNewSector) ||
                        ((lastSessionPhase == SessionPhase.Countdown || lastSessionPhase == SessionPhase.Formation || lastSessionPhase == SessionPhase.Garage)
                        && (currentGameState.SessionData.SessionPhase == SessionPhase.Green || currentGameState.SessionData.SessionPhase == SessionPhase.FullCourseYellow)));
                    if (currentGameState.SessionData.IsNewLap)
                    {
                        currentGameState.readLandmarksForThisLap = false;
                        currentGameState.SessionData.PreviousLapWasValid = currentGameState.SessionData.CurrentLapIsValid && !approachingFirstFlyingLap;
                        currentGameState.SessionData.CurrentLapIsValid = true;

                        currentGameState.SessionData.playerCompleteLapWithProvidedLapTime(currentGameState.SessionData.OverallPosition, currentGameState.SessionData.SessionRunningTime,
                            shared.LapTimePreviousSelf, currentGameState.SessionData.CurrentLapIsValid, currentGameState.PitData.InPitlane, false,
                            30, 25, currentGameState.SessionData.SessionHasFixedTime, currentGameState.SessionData.SessionTimeRemaining, 3, currentGameState.TimingData, null, null);
                        currentGameState.SessionData.playerStartNewLap(currentGameState.SessionData.CompletedLaps + 1,
                            currentGameState.SessionData.OverallPosition, currentGameState.PitData.InPitlane, currentGameState.SessionData.SessionRunningTime);
                    }
                    else if (currentGameState.SessionData.IsNewSector)
                    {
                        // at this point in the mapper, the sector number is the sector we just left
                        float sectorTime = currentGameState.SessionData.SectorNumber == 1 ?
                            participantStruct.SectorTimeCurrentSelf.Sector1 : participantStruct.SectorTimeCurrentSelf.Sector2;
                        currentGameState.SessionData.playerAddCumulativeSectorData(currentGameState.SessionData.SectorNumber, currentGameState.SessionData.OverallPosition, sectorTime,
                            currentGameState.SessionData.SessionRunningTime, currentGameState.SessionData.CurrentLapIsValid, false, 30, 25);
                    }

                    if (currentGameState.SessionData.SectorNumber == 1)
                    {
                        // Don't consider this lap as flying as soon as we reach sector 1.  Ideally this should be done on new lap, but it seems that we disregard sector number in check above.
                        approachingFirstFlyingLap = false;
                    }

                    currentGameState.SessionData.SectorNumber = participantStruct.TrackSector;

                    currentGameState.PositionAndMotionData.DistanceRoundTrack = participantStruct.LapDistance;

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
                            R3EPitMenuManager.outstandingPitstopRequest = false;
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
                    currentGameState.SessionData.DeltaTime.SetNextDeltaPoint(currentGameState.PositionAndMotionData.DistanceRoundTrack,
                        currentGameState.SessionData.CompletedLaps, shared.CarSpeed, currentGameState.Now, !currentGameState.PitData.InPitlane && !currentGameState.PitData.InPitlane);

                    if (previousGameState != null)
                    {
                        String stoppedInLandmark = currentGameState.SessionData.trackLandmarksTiming.updateLandmarkTiming(currentGameState.SessionData.TrackDefinition,
                            currentGameState.SessionData.SessionRunningTime, previousGameState.PositionAndMotionData.DistanceRoundTrack,
                            participantStruct.LapDistance, shared.CarSpeed, currentGameState.carClass);
                        currentGameState.SessionData.stoppedInLandmark = participantStruct.InPitlane == 1 ? null : stoppedInLandmark;
                    }
                    break;
                }
            }

            if (currentGameState.SessionData.IsNewLap)
            {
                // quick n dirty hack here - if the current car class is unknown, try and get it again
                if (currentGameState.carClass.carClassEnum == CarData.CarClassEnum.UNKNOWN_RACE && shared.VehicleInfo.ClassId > 0)
                {
                    currentGameState.carClass = CarData.getCarClassForRaceRoomId(shared.VehicleInfo.ClassId);
                    currentGameState.carClass.ClassPerformanceIndex = shared.VehicleInfo.ClassPerformanceIndex;
                    brakeTempThresholdsForPlayersCar = getBrakeTempThresholds(shared.BrakeTemp); ;
                }
                currentGameState.SessionData.trackLandmarksTiming.cancelWaitingForLandmarkEnd();
            }

            opponentDriverNamesProcessedForThisTick.Clear();
            opponentDriverNamesProcessedForThisTick.Add(currentGameState.SessionData.DriverRawName);
            positionsFilledForThisTick.Clear();
            positionsFilledForThisTick.Add(currentGameState.SessionData.OverallPosition);
            for (int i = 0; i < shared.DriverData.Length; i++)
            {
                if (i == playerDriverDataIndex)
                {
                    continue;
                }
                DriverData participantStruct = shared.DriverData[i];
                /*
                // don't discard opponents with duplicate positions
                if (positionsFilledForThisTick.Contains(participantStruct.Place))
                {
                    // discard this participant element because the race position is already occupied
                    continue;
                }*/
                String driverName = driverNames[i];
                if (driverName.Length == 0 || driverName == currentGameState.SessionData.DriverRawName || opponentDriverNamesProcessedForThisTick.Contains(driverName) ||
                    participantStruct.Place < 1 || participantStruct.FinishStatus == (int)CrewChiefV4.RaceRoom.RaceRoomConstant.FinishStatus.DNS)
                {
                    // allow these drivers be pruned from the set if we continue to receive no data for them
                    continue;
                }
                else if (participantStruct.FinishStatus == (int)CrewChiefV4.RaceRoom.RaceRoomConstant.FinishStatus.DNF)
                {
                    // remove this driver from the set immediately
                    if (!currentGameState.retriedDriverNames.ContainsKey(driverName))
                    {
                        Console.WriteLine("Opponent " + driverName + " has retired");
                        currentGameState.retriedDriverNames.Add(driverName, null);
                    }
                    currentGameState.OpponentData.Remove(driverName);
                    continue;
                }
                else if (participantStruct.FinishStatus == (int)CrewChiefV4.RaceRoom.RaceRoomConstant.FinishStatus.DQ)
                {
                    // remove this driver from the set immediately
                    if (!currentGameState.disqualifiedDriverNames.ContainsKey(driverName))
                    {
                        Console.WriteLine("Opponent " + driverName + " has been disqualified");
                        currentGameState.disqualifiedDriverNames.Add(driverName, null);
                    }
                    currentGameState.OpponentData.Remove(driverName);
                    continue;
                }
                lastActiveTimeForOpponents[driverName] = currentGameState.Now;
                positionsFilledForThisTick.Add(participantStruct.Place);
                opponentDriverNamesProcessedForThisTick.Add(driverName);
                OpponentData currentOpponentData = null;
                if (currentGameState.OpponentData.TryGetValue(driverName, out currentOpponentData))
                {
                    if (previousGameState != null)
                    {
                        OpponentData previousOpponentData = null;
                        Boolean newOpponentLap = false;
                        int previousOpponentSectorNumber = 1;
                        int previousOpponentCompletedLaps = 0;
                        int previousOpponentPosition = 0;
                        Boolean previousOpponentIsEnteringPits = false;
                        float[] previousOpponentWorldPosition = new float[] { 0, 0, 0 };
                        float previousOpponentSpeed = 0;
                        float previousDistanceRoundTrack = 0;
                        Boolean previousOpponentInPits = false;
                        if (previousGameState.OpponentData.TryGetValue(driverName, out previousOpponentData))
                        {
                            previousOpponentSectorNumber = previousOpponentData.CurrentSectorNumber;
                            previousOpponentCompletedLaps = previousOpponentData.CompletedLaps;
                            previousOpponentPosition = previousOpponentData.OverallPosition;
                            previousOpponentIsEnteringPits = previousOpponentData.isEnteringPits();
                            previousOpponentInPits = previousOpponentData.InPits;
                            previousOpponentWorldPosition = previousOpponentData.WorldPosition;
                            previousOpponentSpeed = previousOpponentData.Speed;
                            newOpponentLap = previousOpponentData.CurrentSectorNumber == 3 && participantStruct.TrackSector == 1;
                            previousDistanceRoundTrack = previousOpponentData.DistanceRoundTrack;
                            currentOpponentData.ClassPositionAtPreviousTick = previousOpponentData.ClassPosition;
                            currentOpponentData.OverallPositionAtPreviousTick = previousOpponentData.OverallPosition;
                        }

                        float sectorTime = -1;
                        if (participantStruct.TrackSector == 1)
                        {
                            sectorTime = participantStruct.SectorTimeCurrentSelf.Sector3;
                        }
                        else if (participantStruct.TrackSector == 2)
                        {
                            sectorTime = participantStruct.SectorTimeCurrentSelf.Sector1;
                        }
                        else if (participantStruct.TrackSector == 3)
                        {
                            sectorTime = participantStruct.SectorTimeCurrentSelf.Sector2;
                        }

                        int currentOpponentRacePosition = currentGameState.SessionData.SessionType == SessionType.Race && previousOpponentPosition > 0 ?
                            getRacePosition(driverName, previousOpponentPosition, participantStruct.Place, currentGameState.Now)
                            : participantStruct.Place;
                        //int currentOpponentRacePosition = participantStruct.place;
                        int currentOpponentLapsCompleted = participantStruct.CompletedLaps;
                        int currentOpponentSector = participantStruct.TrackSector;
                        if (currentOpponentSector == 0)
                        {
                            currentOpponentSector = previousOpponentSectorNumber;
                        }
                        float currentOpponentLapDistance = participantStruct.LapDistance;

                        Boolean finishedAllottedRaceLaps = currentGameState.SessionData.SessionNumberOfLaps > 0 && currentGameState.SessionData.SessionNumberOfLaps == currentOpponentLapsCompleted;
                        Boolean finishedAllottedRaceTime = false;

                        if (currentGameState.SessionData.SessionType == SessionType.Race
                            && currentGameState.SessionData.SessionTotalRunTime > 0 && currentGameState.SessionData.SessionTimeRemaining <= 0)
                        {
                            if (previousOpponentCompletedLaps < currentOpponentLapsCompleted)
                            {
                                // timed session, we've started a new lap after the time has reached zero. Where there's no extra lap this means we've finished. If there's 1 or more
                                // extras he's finished when he's started more than the extra laps number
                                currentOpponentData.LapsStartedAfterRaceTimeEnd++;
                            }
                            finishedAllottedRaceTime = currentOpponentData.LapsStartedAfterRaceTimeEnd > currentGameState.SessionData.ExtraLapsAfterTimedSessionComplete;
                        }

                        if (currentOpponentRacePosition == 1 && (finishedAllottedRaceTime || finishedAllottedRaceLaps))
                        {
                            currentGameState.SessionData.LeaderHasFinishedRace = true;
                        }
                        Boolean isEnteringPits = participantStruct.InPitlane == 1 && !previousOpponentInPits;
                        Boolean isLeavingPits = previousOpponentInPits && participantStruct.InPitlane != 1;

                        float secondsSinceLastUpdate = (float)new TimeSpan(currentGameState.Ticks - previousGameState.Ticks).TotalSeconds;

                        // lap invalidation: at the start of the lap the laptime will be 0, so only check if the time is zero if
                        // we've actually start this lap. For this we use LapDistance > 1 metre. Bit of an abitrary choice but
                        // that's we roll at the CCMC
                        Boolean lapInvalidated = participantStruct.CurrentLapValid != 1 || participantStruct.LapTimeCurrentSelf == -1 ||
                            (participantStruct.LapDistance > 1 && participantStruct.LapTimeCurrentSelf == 0);

                        upateOpponentData(currentOpponentData, currentOpponentRacePosition,
                                participantStruct.Place, currentOpponentLapsCompleted,
                                currentOpponentSector, sectorTime, participantStruct.SectorTimePreviousSelf.Sector3,
                                participantStruct.InPitlane == 1, !lapInvalidated,
                                currentGameState.SessionData.SessionRunningTime, secondsSinceLastUpdate,
                                new float[] { participantStruct.Position.X, participantStruct.Position.Z }, previousOpponentWorldPosition,
                                participantStruct.LapDistance, participantStruct.TireTypeFront, participantStruct.TireSubtypeFront,
                                participantStruct.TireTypeRear, participantStruct.TireSubtypeRear,
                                currentGameState.SessionData.SessionHasFixedTime, currentGameState.SessionData.SessionTimeRemaining,
                                currentGameState.SessionData.SessionType == SessionType.Race,
                                currentGameState.SessionData.TrackDefinition.distanceForNearPitEntryChecks,
                                participantStruct.CarSpeed,
                                participantStruct.LapTimeCurrentSelf,
                                participantStruct.DriverInfo.ClassId, currentGameState.Now,
                                currentGameState.TimingData,
                                currentGameState.carClass);

                        currentOpponentData.DeltaTime.SetNextDeltaPoint(currentOpponentLapDistance, currentOpponentData.CompletedLaps, currentOpponentData.Speed, currentGameState.Now, participantStruct.InPitlane != 1);

                        if (previousOpponentData != null)
                        {
                            currentOpponentData.trackLandmarksTiming = previousOpponentData.trackLandmarksTiming;
                            String stoppedInLandmark = currentOpponentData.trackLandmarksTiming.updateLandmarkTiming(
                                currentGameState.SessionData.TrackDefinition, currentGameState.SessionData.SessionRunningTime,
                                previousDistanceRoundTrack, currentOpponentData.DistanceRoundTrack, currentOpponentData.Speed, currentOpponentData.CarClass);
                            currentOpponentData.stoppedInLandmark = currentOpponentData.InPits ? null : stoppedInLandmark;
                        }
                        if (currentGameState.SessionData.JustGoneGreen)
                        {
                            currentOpponentData.trackLandmarksTiming = new TrackLandmarksTiming();
                        }
                        if (newOpponentLap)
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
                                if (CarData.IsCarClassEqual(currentOpponentData.CarClass, currentGameState.carClass))
                                {
                                    float playerClassSessionBestByTyre = -1.0f;
                                    if (currentOpponentData.LastLapTime > 0 && currentOpponentData.LastLapValid &&
                                        (!currentGameState.SessionData.PlayerClassSessionBestLapTimeByTyre.TryGetValue(currentOpponentData.CurrentTyres, out playerClassSessionBestByTyre) ||
                                        playerClassSessionBestByTyre > currentOpponentData.LastLapTime))
                                    {
                                        currentGameState.SessionData.PlayerClassSessionBestLapTimeByTyre[currentOpponentData.CurrentTyres] = currentOpponentData.LastLapTime;
                                    }
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

                        // improvised blue flag calculation when we have full flag rules disabled
                        if (useImprovisedBlueFlagDetection)
                        {
                            Boolean isInSector1OnOutlap = currentOpponentData.CurrentSectorNumber == 1 &&
                                (currentOpponentData.getCurrentLapData() != null && currentOpponentData.getCurrentLapData().OutLap);
                            if (currentGameState.SessionData.SessionType == SessionType.Race && currentOpponentData.OverallPosition == participantStruct.Place &&
                                !isEnteringPits && !isLeavingPits && currentGameState.PositionAndMotionData.DistanceRoundTrack != 0 &&
                                participantStruct.LapDistance != 0 && currentOpponentData.OverallPosition + 1 < shared.Position && !isInSector1OnOutlap &&
                                isBehindWithinDistance(shared.LayoutLength, 8, blueFlagDetectionDistance, currentGameState.PositionAndMotionData.DistanceRoundTrack,
                                participantStruct.LapDistance))
                            {
                                currentGameState.SessionData.Flag = FlagEnum.BLUE;
                            }
                        }
                    }
                }
                else
                {
                    Boolean skipGhost = false;
                    if (currentGameState.OpponentData.Count() == 0
                        && (currentGameState.SessionData.SessionType == SessionType.HotLap
                             || currentGameState.SessionData.SessionType == SessionType.LonePractice))
                    {
                        if (!ghostOpponents.Contains(driverName))
                        {
                            // Not sure if this is valid check.  We need a trace of the online session with folks joining in.
                            if (participantStruct.InPitlane == 0
                                && participantStruct.CarSpeed == 0.0f
                                && Math.Abs(participantStruct.LapDistance) < 50.0)
                            {
                                ghostOpponents.Add(driverName);
                                Console.WriteLine("Added opponent: " + driverName + " into the set of ghost vehicles.  At lapdist: " + participantStruct.LapDistance.ToString("0.000"));
                                skipGhost = true;
                            }
                        }
                        else
                        {
                            skipGhost = true;
                        }
                    }

                    if (!skipGhost)
                    {
                        var opponentData = createOpponentData(participantStruct, driverName,
                            false, currentGameState.SessionData.TrackDefinition.trackLength);
                        opponentData.CarClass.ClassPerformanceIndex = participantStruct.DriverInfo.ClassPerformanceIndex;
                        currentGameState.OpponentData.Add(driverName, opponentData);
                    }
                }
            }

            if (currentGameState.Now > nextOpponentCleanupTime)
            {
                nextOpponentCleanupTime = currentGameState.Now + opponentCleanupInterval;
                DateTime oldestAllowedUpdate = currentGameState.Now - opponentCleanupInterval;
                List<string> inactiveOpponents = new List<string>();
                foreach (string opponentName in currentGameState.OpponentData.Keys)
                {
                    DateTime lastActiveTime = DateTime.MinValue;
                    if (!lastActiveTimeForOpponents.TryGetValue(opponentName, out lastActiveTime) || lastActiveTime < oldestAllowedUpdate)
                    {
                        inactiveOpponents.Add(opponentName);
                        Console.WriteLine("Opponent " + opponentName + " has been inactive for " + opponentCleanupInterval + ", removing him");
                    }
                }
                foreach (String inactiveOpponent in inactiveOpponents)
                {
                    currentGameState.OpponentData.Remove(inactiveOpponent);
                }
            }
            // Sort class positions
            currentGameState.sortClassPositions();
            currentGameState.setPracOrQualiDeltas();

            // garage phase has nonsense data
            if (currentGameState.SessionData.JustGoneGreen || (currentGameState.SessionData.IsNewSession && currentGameState.SessionData.SessionPhase != SessionPhase.Garage))
            {
                Utilities.TraceEventClass(currentGameState);
            }
            if (currentGameState.SessionData.IsNewLap && currentGameState.SessionData.PreviousLapWasValid &&
                currentGameState.SessionData.LapTimePrevious > 0)
            {
                float playerClassBestTimeByTyre = -1.0f;
                if (!currentGameState.SessionData.PlayerClassSessionBestLapTimeByTyre.TryGetValue(currentGameState.TyreData.FrontLeftTyreType, out playerClassBestTimeByTyre) ||
                    playerClassBestTimeByTyre > currentGameState.SessionData.LapTimePrevious)
                {
                    currentGameState.SessionData.PlayerClassSessionBestLapTimeByTyre[currentGameState.TyreData.FrontLeftTyreType] = currentGameState.SessionData.LapTimePrevious;
                }
                float playerBestLapTimeByTyre = -1.0f;
                if (!currentGameState.SessionData.PlayerBestLapTimeByTyre.TryGetValue(currentGameState.TyreData.FrontLeftTyreType, out playerBestLapTimeByTyre) ||
                    playerBestLapTimeByTyre > currentGameState.SessionData.LapTimePrevious)
                {
                    currentGameState.SessionData.PlayerBestLapTimeByTyre[currentGameState.TyreData.FrontLeftTyreType] = currentGameState.SessionData.LapTimePrevious;
                }
            }

            if (shared.SessionType == (int)RaceRoomConstant.Session.Race && shared.SessionPhase == (int)RaceRoomConstant.SessionPhase.Checkered &&
                previousGameState != null && (previousGameState.SessionData.SessionPhase == SessionPhase.Green || previousGameState.SessionData.SessionPhase == SessionPhase.Green))
            {
                Console.WriteLine("Leader has finished race, player has done " + shared.CompletedLaps + " laps, session time = " + shared.Player.GameSimulationTime);
                currentGameState.SessionData.LeaderHasFinishedRace = true;
            }

            //------------------------ Car damage data -----------------------
            TelemetryMeasurements.HealthDamage(currentGameState,shared.CarDamage.Aerodynamics,shared.CarDamage.Engine,
                shared.CarDamage.Transmission,"RaceRoom.API",DateTime.SpecifyKind(currentGameState.Now,DateTimeKind.Utc));
            currentGameState.CarDamageData.DamageEnabled = shared.CarDamage.Aerodynamics != -1 &&
                shared.CarDamage.Transmission != -1 && shared.CarDamage.Engine != -1;
            if (currentGameState.CarDamageData.DamageEnabled)
            {
                currentGameState.CarDamageData.AeroDamagePercent = (1 - shared.CarDamage.Aerodynamics) * 100;
                currentGameState.CarDamageData.EngineDamagePercent = (1 - shared.CarDamage.Engine) * 100;
                currentGameState.CarDamageData.TransmissionDamagePercent = (1 - shared.CarDamage.Transmission) * 100;
                if (shared.CarDamage.Aerodynamics < destroyedAeroThreshold)
                {
                    currentGameState.CarDamageData.OverallAeroDamage = DamageLevel.DESTROYED;
                }
                else if (shared.CarDamage.Aerodynamics < severeAeroDamageThreshold)
                {
                    currentGameState.CarDamageData.OverallAeroDamage = DamageLevel.MAJOR;
                }
                else if (shared.CarDamage.Aerodynamics < minorAeroDamageThreshold)
                {
                    currentGameState.CarDamageData.OverallAeroDamage = DamageLevel.MINOR;
                }
                else if (shared.CarDamage.Aerodynamics < trivialAeroDamageThreshold)
                {
                    currentGameState.CarDamageData.OverallAeroDamage = DamageLevel.TRIVIAL;
                }
                else
                {
                    currentGameState.CarDamageData.OverallAeroDamage = DamageLevel.NONE;
                }

                if (shared.CarDamage.Engine < destroyedEngineThreshold)
                {
                    currentGameState.CarDamageData.OverallEngineDamage = DamageLevel.DESTROYED;
                }
                else if (shared.CarDamage.Engine < severeEngineDamageThreshold)
                {
                    currentGameState.CarDamageData.OverallEngineDamage = DamageLevel.MAJOR;
                }
                else if (shared.CarDamage.Engine < minorEngineDamageThreshold)
                {
                    currentGameState.CarDamageData.OverallEngineDamage = DamageLevel.MINOR;
                }
                else if (shared.CarDamage.Engine < trivialEngineDamageThreshold)
                {
                    currentGameState.CarDamageData.OverallEngineDamage = DamageLevel.TRIVIAL;
                }
                else
                {
                    currentGameState.CarDamageData.OverallEngineDamage = DamageLevel.NONE;
                }

                if (shared.CarDamage.Transmission < destroyedTransmissionThreshold)
                {
                    currentGameState.CarDamageData.OverallTransmissionDamage = DamageLevel.DESTROYED;
                }
                else if (shared.CarDamage.Transmission < severeTransmissionDamageThreshold)
                {
                    currentGameState.CarDamageData.OverallTransmissionDamage = DamageLevel.MAJOR;
                }
                else if (shared.CarDamage.Transmission < minorTransmissionDamageThreshold)
                {
                    currentGameState.CarDamageData.OverallTransmissionDamage = DamageLevel.MINOR;
                }
                else if (shared.CarDamage.Transmission < trivialTransmissionDamageThreshold)
                {
                    currentGameState.CarDamageData.OverallTransmissionDamage = DamageLevel.TRIVIAL;
                }
                else
                {
                    currentGameState.CarDamageData.OverallTransmissionDamage = DamageLevel.NONE;
                }
                float suspensionDamageLevel = shared.CarDamage.Suspension == -1 ? 0 : (1 - shared.CarDamage.Suspension) * 100;
                currentGameState.CarDamageData.SuspensionDamageStatus = CornerData.getCornerData(suspensionDamageThresholds,
                    suspensionDamageLevel, suspensionDamageLevel, suspensionDamageLevel, suspensionDamageLevel);
            }

            //------------------------ Engine data -----------------------            
            currentGameState.EngineData.EngineOilPressure = shared.EngineOilPressure;
            currentGameState.EngineData.EngineRpm = shared.EngineRps * (60 / (2 * (Single)Math.PI));
            currentGameState.EngineData.MaxEngineRpm = shared.MaxEngineRps * (60 / (2 * (Single)Math.PI));
            currentGameState.EngineData.MinutesIntoSessionBeforeMonitoring = 5;

            currentGameState.EngineData.EngineOilTemp = shared.EngineOilTemp;
            currentGameState.EngineData.EngineWaterTemp = shared.EngineWaterTemp;

            //------------------------ Fuel data -----------------------
            currentGameState.FuelData.FuelUseActive = shared.FuelUseActive > 0;    // there seems to be some issue with this...
            currentGameState.FuelData.FuelMultiplier = shared.FuelUseActive;
            currentGameState.FuelData.FuelPressure = shared.FuelPressure;
            currentGameState.FuelData.FuelCapacity = shared.FuelCapacity;
            currentGameState.FuelData.FuelLeft = shared.FuelLeft;


            //------------------------ Penalties data -----------------------     
            // these two are deprecated:
            //currentGameState.PenaltiesData.HasDriveThrough = shared.Penalties.DriveThrough > 0;
            //currentGameState.PenaltiesData.HasStopAndGo = shared.Penalties.StopAndGo > 0;
            currentGameState.PenaltiesData.CutTrackWarnings = shared.CutTrackWarnings;
            currentGameState.PenaltiesData.HasSlowDown = shared.Penalties.SlowDown > 0;
            currentGameState.PenaltiesData.HasPitStop = shared.Penalties.PitStop > 0;
            currentGameState.PenaltiesData.HasTimeDeduction = shared.Penalties.TimeDeduction > 0;   // "time deduction" is actually "servable time penalty"
            currentGameState.PenaltiesData.NumOutstandingPenalties = shared.NumPenalties;

            // new penalties data, incomplete mapping using same event logic as RF2
            currentGameState.PenaltiesData.PenaltyType = getDetailedPenaltyType(playerDriverData.PenaltyType);
            currentGameState.PenaltiesData.PenaltyCause = getDetailedPenaltyCause(currentGameState.PenaltiesData.PenaltyType, playerDriverData.PenaltyReason);
            currentGameState.PenaltiesData.HasStopAndGo = currentGameState.PenaltiesData.PenaltyType == PenatiesData.DetailedPenaltyType.STOP_AND_GO;

            //------------------------ Pit stop data -----------------------            
            currentGameState.PitData.PitWindow = mapToPitWindow(shared.PitWindowStatus);
            currentGameState.PitData.IsMakingMandatoryPitStop = (currentGameState.PitData.PitWindow == PitWindow.Open || currentGameState.PitData.PitWindow == PitWindow.StopInProgress) &&
               (currentGameState.PitData.OnInLap || currentGameState.PitData.OnOutLap);
            if (previousGameState != null)
            {
                currentGameState.PitData.MandatoryPitStopCompleted = previousGameState.PitData.MandatoryPitStopCompleted || shared.PitWindowStatus == (int)RaceRoomConstant.PitWindow.Completed;
            }

            currentGameState.PitData.limiterStatus = (PitData.LimiterStatus)shared.PitLimiter + 1;
            currentGameState.PitData.HasRequestedPitStop = shared.PitState == (Int32)RaceRoomConstant.PitStates.Requested;

            if (shared.GameInMenus == 0  // BS data in menu.
                && currentGameState.SessionData.SessionType == SessionType.Race)  // Limit to race only.  There's also no real critical rush in quali or practice to stress about.
            {
                currentGameState.PitData.IsPitCrewDone = shared.PitState == (Int32)RaceRoomConstant.PitStates.Exiting;
            }

            currentGameState.PitData.MandatoryPitMinDurationLeft = shared.PitMinDurationLeft;
            currentGameState.PitData.MandatoryPitMinDurationTotal = shared.PitMinDurationTotal;

            // See if it looks like we're entering the pits.  Use TrackDefinition.pitApproachPoint if available.
            var pitApproachPoint = currentGameState.SessionData.TrackDefinition.pitApproachPoint;
            if (pitApproachPoint != null
                && currentGameState.PitData.HasRequestedPitStop
                && Math.Abs(currentGameState.PositionAndMotionData.DistanceRoundTrack - pitApproachPoint[0]) < 30.0f)  // Within 30 meters of anchor pt by lapdist.
            {
                var distToPitApproachPt = Math.Sqrt(
                   (double)((currentGameState.PositionAndMotionData.WorldPosition[0] - pitApproachPoint[1]) * (currentGameState.PositionAndMotionData.WorldPosition[0] - pitApproachPoint[1])
                   + (currentGameState.PositionAndMotionData.WorldPosition[2] - pitApproachPoint[2]) * (currentGameState.PositionAndMotionData.WorldPosition[2] - pitApproachPoint[2])));

                currentGameState.PitData.IsApproachingPitlane = distToPitApproachPt < 4.0;  // Within 4 meters by world pos.
                Console.WriteLine($"Pit approach detection: approaching - {currentGameState.PitData.IsApproachingPitlane}    dist to point - {distToPitApproachPt.ToString("0.000")}");
            }

            currentGameState.PitData.PitSpeedLimit = shared.SessionPitSpeedLimit;

            //------------------------ Pit menu -----------------------------
            R3EPitMenuManager.map(shared.PitMenuSelection, shared.PitMenuState, shared.PitState);

            //------------------------ Car position / motion data -----------------------
            currentGameState.PositionAndMotionData.CarSpeed = shared.CarSpeed;


            //------------------------ Transmission data -----------------------
            currentGameState.TransmissionData.Gear = shared.Gear;


            //------------------------ Tyre data -----------------------
            // no way to have unmatched tyre types in R3E
            currentGameState.TyreData.HasMatchedTyreTypes = true;
            currentGameState.TyreData.TyreWearActive = shared.TireWearActive > 0;
            TyreType tyreType = mapToTyreType(shared.TireTypeFront, shared.TireSubtypeFront, shared.TireTypeRear, shared.TireSubtypeFront, currentGameState.carClass.carClassEnum);
            var key = new Tuple<CarData.CarClassEnum, TyreType>(currentGameState.carClass.carClassEnum, tyreType);
            if (!CarData.optimalTempsFromGame.ContainsKey(key))
            {
                CarData.AddTempThresholdsFromGame(key, shared.TireTemp.FrontLeft.ColdTemp, shared.TireTemp.FrontLeft.OptimalTemp, shared.TireTemp.FrontLeft.HotTemp);
            }
            currentGameState.TyreData.FrontLeft_CenterTemp = shared.TireTemp.FrontLeft.CurrentTemp.Center;
            currentGameState.TyreData.FrontLeft_LeftTemp = shared.TireTemp.FrontLeft.CurrentTemp.Left;
            currentGameState.TyreData.FrontLeft_RightTemp = shared.TireTemp.FrontLeft.CurrentTemp.Right;
            float frontLeftTemp = (currentGameState.TyreData.FrontLeft_CenterTemp + currentGameState.TyreData.FrontLeft_LeftTemp + currentGameState.TyreData.FrontLeft_RightTemp) / 3;
            currentGameState.TyreData.FrontLeftTyreType = tyreType;
            currentGameState.TyreData.FrontLeftPressure = shared.TirePressure.FrontLeft;
            currentGameState.TyreData.FrontLeftPercentWear = getTyreWearPercentage(shared.TireWear.FrontLeft);
            if (currentGameState.SessionData.IsNewLap || currentGameState.TyreData.PeakFrontLeftTemperatureForLap == 0)
            {
                currentGameState.TyreData.PeakFrontLeftTemperatureForLap = frontLeftTemp;
            }
            else if (previousGameState == null || frontLeftTemp > previousGameState.TyreData.PeakFrontLeftTemperatureForLap)
            {
                currentGameState.TyreData.PeakFrontLeftTemperatureForLap = frontLeftTemp;
            }

            currentGameState.TyreData.FrontRight_CenterTemp = shared.TireTemp.FrontRight.CurrentTemp.Center;
            currentGameState.TyreData.FrontRight_LeftTemp = shared.TireTemp.FrontRight.CurrentTemp.Left;
            currentGameState.TyreData.FrontRight_RightTemp = shared.TireTemp.FrontRight.CurrentTemp.Right;
            float frontRightTemp = (currentGameState.TyreData.FrontRight_CenterTemp + currentGameState.TyreData.FrontRight_LeftTemp + currentGameState.TyreData.FrontRight_RightTemp) / 3;
            currentGameState.TyreData.FrontRightTyreType = tyreType;
            currentGameState.TyreData.FrontRightPressure = shared.TirePressure.FrontRight;
            currentGameState.TyreData.FrontRightPercentWear = getTyreWearPercentage(shared.TireWear.FrontRight);
            if (currentGameState.SessionData.IsNewLap || currentGameState.TyreData.PeakFrontRightTemperatureForLap == 0)
            {
                currentGameState.TyreData.PeakFrontRightTemperatureForLap = frontRightTemp;
            }
            else if (previousGameState == null || frontRightTemp > previousGameState.TyreData.PeakFrontRightTemperatureForLap)
            {
                currentGameState.TyreData.PeakFrontRightTemperatureForLap = frontRightTemp;
            }

            currentGameState.TyreData.RearLeft_CenterTemp = shared.TireTemp.RearLeft.CurrentTemp.Center;
            currentGameState.TyreData.RearLeft_LeftTemp = shared.TireTemp.RearLeft.CurrentTemp.Left;
            currentGameState.TyreData.RearLeft_RightTemp = shared.TireTemp.RearLeft.CurrentTemp.Right;
            float rearLeftTemp = (currentGameState.TyreData.RearLeft_CenterTemp + currentGameState.TyreData.RearLeft_LeftTemp + currentGameState.TyreData.RearLeft_RightTemp) / 3;
            currentGameState.TyreData.RearLeftTyreType = tyreType;
            currentGameState.TyreData.RearLeftPressure = shared.TirePressure.RearLeft;
            currentGameState.TyreData.RearLeftPercentWear = getTyreWearPercentage(shared.TireWear.RearLeft);
            if (currentGameState.SessionData.IsNewLap || currentGameState.TyreData.PeakRearLeftTemperatureForLap == 0)
            {
                currentGameState.TyreData.PeakRearLeftTemperatureForLap = rearLeftTemp;
            }
            else if (previousGameState == null || rearLeftTemp > previousGameState.TyreData.PeakRearLeftTemperatureForLap)
            {
                currentGameState.TyreData.PeakRearLeftTemperatureForLap = rearLeftTemp;
            }

            currentGameState.TyreData.RearRight_CenterTemp = shared.TireTemp.RearRight.CurrentTemp.Center;
            currentGameState.TyreData.RearRight_LeftTemp = shared.TireTemp.RearRight.CurrentTemp.Left;
            currentGameState.TyreData.RearRight_RightTemp = shared.TireTemp.RearRight.CurrentTemp.Right;
            float rearRightTemp = (currentGameState.TyreData.RearRight_CenterTemp + currentGameState.TyreData.RearRight_LeftTemp + currentGameState.TyreData.RearRight_RightTemp) / 3;
            currentGameState.TyreData.RearRightTyreType = tyreType;
            currentGameState.TyreData.RearRightPressure = shared.TirePressure.RearRight;
            currentGameState.TyreData.RearRightPercentWear = getTyreWearPercentage(shared.TireWear.RearRight);
            if (currentGameState.SessionData.IsNewLap || currentGameState.TyreData.PeakRearRightTemperatureForLap == 0)
            {
                currentGameState.TyreData.PeakRearRightTemperatureForLap = rearRightTemp;
            }
            else if (previousGameState == null || rearRightTemp > previousGameState.TyreData.PeakRearRightTemperatureForLap)
            {
                currentGameState.TyreData.PeakRearRightTemperatureForLap = rearRightTemp;
            }

            currentGameState.TyreData.TyreConditionStatus = CornerData.getCornerData(tyreWearThresholds, currentGameState.TyreData.FrontLeftPercentWear,
                currentGameState.TyreData.FrontRightPercentWear, currentGameState.TyreData.RearLeftPercentWear, currentGameState.TyreData.RearRightPercentWear);

            currentGameState.TyreData.DirtPickupEmulationActive = true;
            currentGameState.TyreData.TyreDirtPickupStatus = CornerData.getCornerData(tyreDirtPickupThresholds, shared.TireDirt.FrontLeft,
                shared.TireDirt.FrontRight, shared.TireDirt.RearLeft, shared.TireDirt.RearRight);

            var tyreTempThresholds = CarData.getTyreTempThresholds(currentGameState.carClass, tyreType);
            currentGameState.TyreData.TyreTempStatus = CornerData.getCornerData(tyreTempThresholds,
                currentGameState.TyreData.PeakFrontLeftTemperatureForLap, currentGameState.TyreData.PeakFrontRightTemperatureForLap,
                currentGameState.TyreData.PeakRearLeftTemperatureForLap, currentGameState.TyreData.PeakRearRightTemperatureForLap);

            if (brakeTempThresholdsForPlayersCar != null)
            {
                currentGameState.TyreData.BrakeTempStatus = CornerData.getCornerData(brakeTempThresholdsForPlayersCar, shared.BrakeTemp.FrontLeft.CurrentTemp,
                    shared.BrakeTemp.FrontRight.CurrentTemp, shared.BrakeTemp.RearLeft.CurrentTemp, shared.BrakeTemp.RearRight.CurrentTemp);
            }

            currentGameState.TyreData.LeftFrontBrakeTemp = shared.BrakeTemp.FrontLeft.CurrentTemp;
            currentGameState.TyreData.RightFrontBrakeTemp = shared.BrakeTemp.FrontRight.CurrentTemp;
            currentGameState.TyreData.LeftRearBrakeTemp = shared.BrakeTemp.RearLeft.CurrentTemp;
            currentGameState.TyreData.RightRearBrakeTemp = shared.BrakeTemp.RearRight.CurrentTemp;

            // some simple locking / spinning checks
            if (shared.CarSpeed > 7)
            {
                float minRotatingSpeed = (float)Math.PI * shared.CarSpeed / currentGameState.carClass.maxTyreCircumference;
                currentGameState.TyreData.LeftFrontIsLocked = Math.Abs(shared.TireRps.FrontLeft) < minRotatingSpeed;
                currentGameState.TyreData.RightFrontIsLocked = Math.Abs(shared.TireRps.FrontRight) < minRotatingSpeed;
                currentGameState.TyreData.LeftRearIsLocked = Math.Abs(shared.TireRps.RearLeft) < minRotatingSpeed;
                currentGameState.TyreData.RightRearIsLocked = Math.Abs(shared.TireRps.RearRight) < minRotatingSpeed;

                float maxRotatingSpeed = 3 * (float)Math.PI * shared.CarSpeed / currentGameState.carClass.minTyreCircumference;
                currentGameState.TyreData.LeftFrontIsSpinning = Math.Abs(shared.TireRps.FrontLeft) > maxRotatingSpeed;
                currentGameState.TyreData.RightFrontIsSpinning = Math.Abs(shared.TireRps.FrontRight) > maxRotatingSpeed;
                currentGameState.TyreData.LeftRearIsSpinning = Math.Abs(shared.TireRps.RearLeft) > maxRotatingSpeed;
                currentGameState.TyreData.RightRearIsSpinning = Math.Abs(shared.TireRps.RearRight) > maxRotatingSpeed;
            }
            currentGameState.OvertakingAids = getOvertakingAids(shared, currentGameState.carClass.carClassEnum, currentGameState.SessionData.CompletedLaps,
                currentGameState.SessionData.SessionNumberOfLaps, currentGameState.SessionData.SessionTimeRemaining,
                currentGameState.SessionData.SessionType);

            if (currentGameState.SessionData.TrackDefinition != null)
            {
                CrewChief.trackName = currentGameState.SessionData.TrackDefinition.name;
            }
            if (currentGameState.carClass != null)
            {
                CrewChief.carClass = currentGameState.carClass.carClassEnum;
            }
            CrewChief.distanceRoundTrack = currentGameState.PositionAndMotionData.DistanceRoundTrack;
            CrewChief.raceroomTrackId = shared.LayoutId;
            CrewChief.viewingReplay = false;

            currentGameState.PositionAndMotionData.Orientation.Pitch = shared.CarOrientation.Pitch;
            currentGameState.PositionAndMotionData.Orientation.Roll = shared.CarOrientation.Roll;
            currentGameState.PositionAndMotionData.Orientation.Yaw = shared.CarOrientation.Yaw;

            if (currentGameState.EngineData.EngineRpm > 5)
            {
                lastTimeEngineWasRunning = currentGameState.Now;
            }
            // don't check for stalled engine in qualify because this will be triggered when starting a lap in rolling-start qual
            if (!currentGameState.PitData.InPitlane &&
                currentGameState.SessionData.SessionType != SessionType.Qualify &&
                previousGameState != null && !previousGameState.EngineData.EngineStalledWarning &&
                currentGameState.SessionData.SessionRunningTime > 60 && currentGameState.EngineData.EngineRpm < 5 &&
                lastTimeEngineWasRunning < currentGameState.Now.Subtract(TimeSpan.FromSeconds(2)) &&
                shared.GameInMenus == 0)  // Don't play engine stall message in menu.
            {
                currentGameState.EngineData.EngineStalledWarning = true;
                lastTimeEngineWasRunning = DateTime.MaxValue;
            }

            // hack to work around delayed car class data joining online multiclass sessions
            if (currentGameState.Now < recheckCarClassesUntil && playerDriverData.DriverInfo.ClassId > 0)
            {
                CarData.CarClass correctedCarClass = CarData.getCarClassForRaceRoomId(playerDriverData.DriverInfo.ClassId);
                if (!CarData.IsCarClassEqual(correctedCarClass, currentGameState.carClass, false))
                {
                    Console.WriteLine("Player car class in game data has changed. Updating to " + correctedCarClass.getClassIdentifier());
                    currentGameState.carClass = correctedCarClass;
                    CarData.RACEROOM_CLASS_ID = playerDriverData.DriverInfo.ClassId;
                    // car length / width now added to shared memory
                    currentGameState.carClass.spotterVehicleLength = playerDriverData.DriverInfo.CarLength;
                    currentGameState.carClass.spotterVehicleWidth = playerDriverData.DriverInfo.CarWidth;
                    GlobalBehaviourSettings.UpdateFromCarClass(correctedCarClass);
                }
            }

            if (currentGameState.SessionData.IsNewLap)
            {
                if (currentGameState.hardPartsOnTrackData.updateHardPartsForNewLap(currentGameState.SessionData.LapTimePrevious))
                {
                    currentGameState.SessionData.TrackDefinition.adjustGapPoints(currentGameState.hardPartsOnTrackData.processedHardPartsForBestLap);
                }
            }
            else if (!currentGameState.PitData.OnOutLap &&
                 !(currentGameState.SessionData.SessionType == SessionType.Race &&
                   (currentGameState.SessionData.CompletedLaps < 1 || (GameStateData.useManualFormationLap && currentGameState.SessionData.CompletedLaps < 2))))
            {
                currentGameState.hardPartsOnTrackData.mapHardPartsOnTrack(currentGameState.ControlData.BrakePedal, currentGameState.ControlData.ThrottlePedal,
                    currentGameState.PositionAndMotionData.DistanceRoundTrack, currentGameState.SessionData.CurrentLapIsValid, currentGameState.SessionData.TrackDefinition.trackLength);
            }

            // race sessions length
            currentGameState.SessionData.RaceSessionsLengthLaps[0] = shared.RaceSessionLaps.Race1;
            currentGameState.SessionData.RaceSessionsLengthLaps[1] = shared.RaceSessionLaps.Race2;
            currentGameState.SessionData.RaceSessionsLengthLaps[2] = shared.RaceSessionLaps.Race3;

            currentGameState.SessionData.RaceSessionsLengthMinutes[0] = shared.RaceSessionMinutes.Race1;
            currentGameState.SessionData.RaceSessionsLengthMinutes[1] = shared.RaceSessionMinutes.Race2;
            currentGameState.SessionData.RaceSessionsLengthMinutes[2] = shared.RaceSessionMinutes.Race3;

            if (previousGameState != null
                && (previousGameState.SessionData.SessionType == SessionType.Practice || previousGameState.SessionData.SessionType == SessionType.Qualify)
                && (previousGameState.SessionData.SessionPhase != SessionPhase.Unavailable)
                && (currentGameState.Now > nextExpectedFinishingPositionUpdateDue
                    || (previousGameState.SessionData.SessionTimeRemaining > 0 && currentGameState.SessionData.SessionTimeRemaining <= 0)))
            {
                // during practice and qual sessions, update this every 30 seconds and again at the end of the session. Here we use the previous game state's opponent data 
                // as it gets cleared at session end
                currentGameState.SessionData.expectedFinishingPosition = R3ERatings.calculateExpectedFinishPosition(previousGameState.OpponentData, previousGameState.carClass);
                nextExpectedFinishingPositionUpdateDue = currentGameState.Now.AddSeconds(30);
            }
            return currentGameState;
        }

        private TyreType mapToTyreType(int tire_type_front, int tire_sub_type_front, int tire_type_rear, int tire_sub_type_rear, CarData.CarClassEnum carClass)
        {
            // F Junior
            if (carClass == CarData.CarClassEnum.FF)
            {
                return TyreType.R3E_2017_F5;
            }
            // bias ply cars - Note that procar and gr5 are using the newer tyre model and have multiple compounds - mapping to a single
            // 'ply' tyre isn't sufficient here. The game-provided thresholds should be OK, so it should be safe to allow these to map
            // to the 2017 tyres
            if (carClass == CarData.CarClassEnum.GROUP4)
            {
                return TyreType.Bias_Ply;
            }
            // indycar
            else if ((int)RaceRoomConstant.TireSubtype.Alternate == tire_sub_type_front)
            {
                return TyreType.Alternate;
            }
            else if ((int)RaceRoomConstant.TireSubtype.Primary == tire_sub_type_front)
            {
                return TyreType.Primary;
            }
            // modern DTM
            else if ((carClass == CarData.CarClassEnum.DTM_2014 || carClass == CarData.CarClassEnum.DTM_2015 || carClass == CarData.CarClassEnum.DTM_2016) &&
                (int)RaceRoomConstant.TireType.Option == tire_type_front)
            {
                return TyreType.Option;
            }
            else if ((carClass == CarData.CarClassEnum.DTM_2014 || carClass == CarData.CarClassEnum.DTM_2015 || carClass == CarData.CarClassEnum.DTM_2016) &&
                (int)RaceRoomConstant.TireType.Prime == tire_type_front)
            {
                return TyreType.Prime;
            }
            // V5 tyre model
            else if ((int)RaceRoomConstant.TireSubtype.Hard == tire_sub_type_front)
            {
                return TyreType.R3E_2017_HARD;
            }
            else if ((int)RaceRoomConstant.TireSubtype.Soft == tire_sub_type_front)
            {
                return TyreType.R3E_2017_SOFT;
            }
            return TyreType.R3E_2017_MEDIUM;
        }

        private PitWindow mapToPitWindow(int r3ePitWindow)
        {
            if ((int)RaceRoomConstant.PitWindow.Closed == r3ePitWindow)
            {
                return PitWindow.Closed;
            }
            if ((int)RaceRoomConstant.PitWindow.Completed == r3ePitWindow)
            {
                return PitWindow.Completed;
            }
            else if ((int)RaceRoomConstant.PitWindow.Disabled == r3ePitWindow)
            {
                return PitWindow.Disabled;
            }
            else if ((int)RaceRoomConstant.PitWindow.Open == r3ePitWindow)
            {
                return PitWindow.Open;
            }
            else if ((int)RaceRoomConstant.PitWindow.Stopped == r3ePitWindow)
            {
                return PitWindow.StopInProgress;
            }
            else
            {
                return PitWindow.Unavailable;
            }
        }

        /**
         * Gets the current session phase. If the transition is valid this is returned, otherwise the
         * previous phase is returned
         */
        private SessionPhase mapToSessionPhase(SessionPhase lastSessionPhase, SessionType currentSessionType, float lastSessionRunningTime, float thisSessionRunningTime,
            int r3eSessionPhase, ControlType controlType, int previousLapsCompleted, int currentLapsCompleted, Boolean isCarRunning, Boolean chequeredFlagShownInThisSession,
            int lights)
        {
            /* prac and qual sessions go chequered after the allotted time. They never go 'finished'. If we complete a lap
             * during this period we can detect the session end and trigger the finish message. Otherwise we just can't detect
             * this period end - hence the 'isCarRunning' hack...
            */
            if ((int)RaceRoomConstant.SessionPhase.Checkered == r3eSessionPhase || chequeredFlagShownInThisSession)
            {
                if (lastSessionPhase == SessionPhase.Green || lastSessionPhase == SessionPhase.FullCourseYellow)
                {
                    // only allow a transition to checkered if the last state was green
                    Console.WriteLine("Checkered - completed " + currentLapsCompleted + " laps, session running time = " + thisSessionRunningTime);
                    return SessionPhase.Checkered;
                }
                else if (SessionPhase.Checkered == lastSessionPhase)
                {
                    if (previousLapsCompleted != currentLapsCompleted || controlType == ControlType.AI ||
                        ((currentSessionType == SessionType.Qualify || currentSessionType == SessionType.Practice) && !isCarRunning))
                    {
                        Console.WriteLine("Finished - completed " + currentLapsCompleted + " laps (was " + previousLapsCompleted + "), session running time = " +
                            thisSessionRunningTime + " control type = " + controlType);
                        return SessionPhase.Finished;
                    }
                }
            }
            else if ((int)RaceRoomConstant.SessionPhase.Countdown == r3eSessionPhase)
            {
                if (lights == 0)
                {
                    return SessionPhase.Gridwalk;
                }
                else
                {
                    return SessionPhase.Countdown;
                }
            }
            else if ((int)RaceRoomConstant.SessionPhase.Formation == r3eSessionPhase)
            {
                return SessionPhase.Formation;
            }
            else if ((int)RaceRoomConstant.SessionPhase.Garage == r3eSessionPhase)
            {
                return SessionPhase.Garage;
            }
            else if ((int)RaceRoomConstant.SessionPhase.Green == r3eSessionPhase)
            {
                if (controlType == ControlType.AI && thisSessionRunningTime < 30)
                {
                    return SessionPhase.Formation;
                }
                else
                {
                    return SessionPhase.Green;
                }
            }
            else if ((int)RaceRoomConstant.SessionPhase.Gridwalk == r3eSessionPhase)
            {
                return SessionPhase.Gridwalk;
            }
            else if ((int)RaceRoomConstant.SessionPhase.Terminated == r3eSessionPhase)
            {
                return SessionPhase.Finished;
            }
            return lastSessionPhase;
        }

        // we use the previous gamestate here as we call this method before mapping the opponent data.
        // If the previousGameState opponentData is empty, but the NumCars is > 1, then all driverData 
        // sent by the game have the same name. For practice and qual this is a hint that we're looking
        // at ghost lap data
        public SessionType mapToSessionType(Object memoryMappedFileStruct, GameStateData previousGameState)
        {
            RaceRoomData.RaceRoomShared shared = (RaceRoomData.RaceRoomShared)memoryMappedFileStruct;
            RaceRoomConstant.Session r3eSessionType = (RaceRoomConstant.Session)shared.SessionType;

            if (RaceRoomConstant.Session.Practice == r3eSessionType)
            {
                if (previousGameState != null && previousGameState.OpponentData.Count == 0)
                {
                    return SessionType.LonePractice;
                }
                else
                {
                    return SessionType.Practice;
                }
            }
            else if (RaceRoomConstant.Session.Warmup == r3eSessionType)
            {
                return SessionType.Practice;
            }
            else if (RaceRoomConstant.Session.Qualify == r3eSessionType)
            {
                if (previousGameState != null && previousGameState.SessionData.SessionType == SessionType.HotLap)
                {
                    // check if we need to revert to regular qual
                    if (previousGameState.PitData.InPitlane && previousGameState.PositionAndMotionData.CarSpeed > 5 &&
                        shared.InPitlane != 1 && shared.CarSpeed > 5)
                    {
                        // regular pit exit in this session, so revert to regular qual
                        return SessionType.Qualify;
                    }
                    else
                    {
                        return SessionType.HotLap;
                    }
                }

                if (((previousGameState != null && previousGameState.PositionAndMotionData.CarSpeed < 1) || previousGameState == null)
                    && shared.InPitlane != 1
                    && shared.CarSpeed > 10)
                {
                    // we're in a qual session and we've gone from the pit to the racing surface in an instant, going from a standstill
                    // to > 10 m/s. 
                    return SessionType.HotLap;
                }
                return SessionType.Qualify;
            }
            else if (RaceRoomConstant.Session.Race == r3eSessionType)
            {
                return SessionType.Race;
            }
            else
            {
                return SessionType.Unavailable;
            }
        }

        private ControlType mapToControlType(int r3eControlType)
        {
            if ((int)RaceRoomConstant.Control.AI == r3eControlType)
            {
                return ControlType.AI;
            }
            else if ((int)RaceRoomConstant.Control.Player == r3eControlType)
            {
                return ControlType.Player;
            }
            else if ((int)RaceRoomConstant.Control.Remote == r3eControlType)
            {
                return ControlType.Remote;
            }
            else if ((int)RaceRoomConstant.Control.Replay == r3eControlType)
            {
                return ControlType.Replay;
            }
            else
            {
                return ControlType.Unavailable;
            }
        }

        private float getTyreWearPercentage(float wearLevel)
        {
            if (wearLevel == -1)
            {
                return -1;
            }
            return Math.Min(100, ((1 - wearLevel) / (1 - wornOutTyreWearLevel)) * 100);
        }

        private Boolean CheckIsCarRunning(RaceRoomData.RaceRoomShared shared)
        {
            return shared.Gear > 0 || shared.CarSpeed > 0.001;
        }

        private TyreCondition getTyreCondition(float percentWear)
        {
            if (percentWear <= -1)
            {
                return TyreCondition.UNKNOWN;
            }
            if (percentWear >= wornOutTyreWearPercent)
            {
                return TyreCondition.WORN_OUT;
            }
            else if (percentWear >= majorTyreWearPercent)
            {
                return TyreCondition.MAJOR_WEAR;
            }
            if (percentWear >= minorTyreWearPercent)
            {
                return TyreCondition.MINOR_WEAR;
            }
            if (percentWear >= scrubbedTyreWearPercent)
            {
                return TyreCondition.SCRUBBED;
            }
            else
            {
                return TyreCondition.NEW;
            }
        }

        private PenatiesData.DetailedPenaltyType getDetailedPenaltyType(Int32 penaltyType)
        {
            // note that Pitstop, Time, Slowdown and DQ aren't mapped here
            switch (penaltyType)
            {
                case 0:
                    return PenatiesData.DetailedPenaltyType.DRIVE_THROUGH;
                case 1:
                case 3:/* "servable time penalty" */
                    return PenatiesData.DetailedPenaltyType.STOP_AND_GO;
                default:
                    return PenatiesData.DetailedPenaltyType.NONE;
            }
        }

        private PenatiesData.DetailedPenaltyCause getDetailedPenaltyCause(PenatiesData.DetailedPenaltyType penaltyType, Int32 penaltyReason)
        {
            // note this is very incomplete
            if (penaltyType == PenatiesData.DetailedPenaltyType.DRIVE_THROUGH)
            {
                switch (penaltyReason)
                {
                    case 1:
                        return PenatiesData.DetailedPenaltyCause.CUT_TRACK;
                    case 2:
                        return PenatiesData.DetailedPenaltyCause.SPEEDING_IN_PITLANE;
                    case 3:
                        return PenatiesData.DetailedPenaltyCause.FALSE_START;
                }
            }
            else if (penaltyType == PenatiesData.DetailedPenaltyType.STOP_AND_GO)
            {
                switch (penaltyReason)
                {
                    case 1:
                    case 2:
                        return PenatiesData.DetailedPenaltyCause.CUT_TRACK;
                }
            }
            return PenatiesData.DetailedPenaltyCause.NONE;
        }

        private OvertakingAids getOvertakingAids(RaceRoomShared shared, CarData.CarClassEnum carClassEnum,
            int lapsCompleted, int lapsInSession, float sessionTimeRemaining, SessionType sessionType)
        {
            OvertakingAids overtakingAids = new OvertakingAids();
            overtakingAids.DrsAvailable = shared.Drs.Available == 1;
            overtakingAids.DrsEngaged = shared.Drs.Engaged == 1;
            overtakingAids.DrsActivationsRemaining = shared.DrsNumActivationsTotal; // only used for DTM 2020
            // DTM rule sets are kinda deprecated and not really that clear any more. Generally the DRS is enabled at the end of lap 1 and disabled near
            // the end of the race (but I'm not sure exactly when). We can't infer the rule set from the selected car so have a one-size-fits-all hack
            if (carClassEnum == CarData.CarClassEnum.DTM_2013 || carClassEnum == CarData.CarClassEnum.DTM_2014
                || carClassEnum == CarData.CarClassEnum.DTM_2015 || carClassEnum == CarData.CarClassEnum.DTM_2016 || carClassEnum == CarData.CarClassEnum.DTM_2020)
            {
                // No race end check - TODO: find out if there's a predictable point where DRS is disabled near the race end
                overtakingAids.DrsEnabled = sessionType == SessionType.Race && lapsCompleted > 0;
                overtakingAids.DrsRange = 1;
            }
            // use shared.PtPNumActivationsTotal if available
            overtakingAids.PushToPassActivationsRemaining = shared.PtPNumActivationsTotal == -1 ? shared.PushToPass.AmountLeft : shared.PtPNumActivationsTotal;
            overtakingAids.PushToPassAvailable = shared.PushToPass.Available == 1;
            overtakingAids.PushToPassEngaged = shared.PushToPass.Engaged == 1;
            overtakingAids.PushToPassEngagedTimeLeft = shared.PushToPass.EngagedTimeLeft;
            overtakingAids.PushToPassWaitTimeLeft = shared.PushToPass.WaitTimeLeft;

            // work-around for DTM 2020, which has a limited number of DRS activations which don't
            // depend on opponent proximity. Setting this to -1 disables the DRS range-to-opponent calls / 'you missed DRS' calls.
            if (carClassEnum == CarData.CarClassEnum.DTM_2020)
            {
                overtakingAids.DrsRange = -1;
            }
            return overtakingAids;
        }

        private void upateOpponentData(OpponentData opponentData, int racePosition, int unfilteredRacePosition, int completedLaps, int sector, float sectorTime,
            float completedLapTime, Boolean isInPits, Boolean lapIsValid, float sessionRunningTime, float secondsSinceLastUpdate, float[] currentWorldPosition,
            float[] previousWorldPosition, float distanceRoundTrack, int tire_type_front, int tyre_sub_type_front, int tire_type_rear, int tyre_sub_type_rear,
            Boolean sessionLengthIsTime, float sessionTimeRemaining, Boolean isRace, float nearPitEntryPointDistance, float speed,
            /* currentLapTime is used only to correct the game time at lap start */float currentLapTime,
            /* may need to recalculate car classes for a short time at session start */int carClassId, DateTime now, TimingData timingData, CarData.CarClass playerCarClass)
        {
            // hack to work around delayed car class data in online sessions
            if (now < recheckCarClassesUntil && carClassId > 0)
            {
                CarData.CarClass correctedCarClass = CarData.getCarClassForRaceRoomId(carClassId);
                if (!CarData.IsCarClassEqual(opponentData.CarClass, correctedCarClass, false))
                {
                    // note we're not correcting the tyre types here but this shouldn't matter as we don't announce them in 
                    // practice and qually anyway
                    Console.WriteLine("Correcting driver " + opponentData.DriverRawName + "'s car class to " + correctedCarClass.getClassIdentifier());
                    opponentData.CarClass = correctedCarClass;
                }
            }

            Boolean isPlayerCarClass = CarData.IsCarClassEqual(opponentData.CarClass, playerCarClass);
            float previousDistanceRoundTrack = opponentData.DistanceRoundTrack;
            opponentData.DistanceRoundTrack = distanceRoundTrack;
            opponentData.Speed = speed;

            if (opponentData.OverallPosition != racePosition)
            {
                opponentData.SessionTimeAtLastPositionChange = sessionRunningTime;
            }
            opponentData.OverallPosition = racePosition;
            if (previousDistanceRoundTrack < nearPitEntryPointDistance && opponentData.DistanceRoundTrack > nearPitEntryPointDistance)
            {
                opponentData.PositionOnApproachToPitEntry = opponentData.OverallPosition;
            }
            opponentData.WorldPosition = currentWorldPosition;
            opponentData.IsNewLap = false;
            Boolean wasInPits = opponentData.InPits;
            if (sessionRunningTime > 30 && !wasInPits && isInPits)
            {
                opponentData.InvalidateCurrentLap();
                opponentData.setInLap();
                if (isRace)
                {
                    opponentData.NumPitStops++;
                }
            }
            opponentData.JustEnteredPits = !wasInPits && isInPits;
            opponentData.InPits = isInPits;
            TyreType previousTyreType = opponentData.CurrentTyres;
            opponentData.hasJustChangedToDifferentTyreType = false;
            if (opponentData.InPits)
            {
                opponentData.CurrentTyres = mapToTyreType(tire_type_front, tyre_sub_type_front, tire_type_rear, tyre_sub_type_rear, opponentData.CarClass.carClassEnum);
                if (opponentData.CurrentTyres != previousTyreType)
                {
                    opponentData.TyreChangesByLap[opponentData.OpponentLapData.Count] = opponentData.CurrentTyres;
                    opponentData.hasJustChangedToDifferentTyreType = true;
                }
            }

            // special case for S3 - invalidate immediately because we won't get a chance to invalidate once the lap is finished
            LapData currentLapData = opponentData.getCurrentLapData();
            if (opponentData.CurrentSectorNumber == 3 && sector == 3 && currentLapData != null && currentLapData.IsValid && !lapIsValid)
            {
                opponentData.InvalidateCurrentLap();
            }
            if (opponentData.CurrentSectorNumber != sector)
            {
                if (opponentData.CurrentSectorNumber == 3 && sector == 1)
                {
                    // correct the game time at lap start - if we're 0.1 into the current lap, the game time when we started
                    // this lap is sessionRunningTime - 0.1
                    if (currentLapTime > 0)
                    {
                        sessionRunningTime = sessionRunningTime - currentLapTime;
                    }
                    if (currentLapData != null)
                    {
                        // the game-provided sector3 times appear to be nonsense in the participant data array, so for sector3 we use
                        // the built-in timer (based on the GameTime reported by the game).
                        if (currentLapData.GameTimeAtLapStart > 0)
                        {
                            completedLapTime = sessionRunningTime - currentLapData.GameTimeAtLapStart;
                        }
                        opponentData.CompleteLapWithProvidedLapTime(racePosition, sessionRunningTime, currentLapData.IsValid ? completedLapTime : -1,
                            isInPits, false, 20, 20, sessionLengthIsTime, sessionTimeRemaining, 3, timingData, isPlayerCarClass, null, null);
                    }
                    opponentData.StartNewLap(completedLaps + 1, racePosition, isInPits, sessionRunningTime, false, 20, 20);
                    opponentData.IsNewLap = true;
                }
                else if (opponentData.CurrentSectorNumber == 1 && sector == 2 || opponentData.CurrentSectorNumber == 2 && sector == 3)
                {
                    opponentData.AddCumulativeSectorData(opponentData.CurrentSectorNumber, racePosition, sectorTime, sessionRunningTime, lapIsValid, false, 20, 20);
                    if (sector == 2)
                    {
                        opponentData.CurrentTyres = mapToTyreType(tire_type_front, tyre_sub_type_front, tire_type_rear, tyre_sub_type_rear, opponentData.CarClass.carClassEnum);
                    }
                }
                opponentData.CurrentSectorNumber = sector;
            }
            opponentData.CompletedLaps = completedLaps;
            if (wasInPits && !isInPits)
            {
                opponentData.InvalidateCurrentLap();
            }
        }

        private OpponentData createOpponentData(DriverData participantStruct, String driverName, Boolean loadDriverName, float trackLength)
        {
            if (loadDriverName && CrewChief.enableDriverNames)
            {
                if (speechRecogniser != null) speechRecogniser.addNewOpponentName(driverName, participantStruct.DriverInfo.CarNumber.ToString());
                SoundCache.loadDriverNameSound(DriverNameHelper.getUsableDriverName(driverName));
            }
            OpponentData opponentData = new OpponentData();
            opponentData.DriverRawName = driverName;
            opponentData.CarNumber = participantStruct.DriverInfo.CarNumber.ToString();
            opponentData.OverallPosition = participantStruct.Place;
            opponentData.CompletedLaps = participantStruct.CompletedLaps;
            opponentData.CurrentSectorNumber = participantStruct.TrackSector;
            opponentData.WorldPosition = new float[] { participantStruct.Position.X, participantStruct.Position.Z };
            opponentData.DistanceRoundTrack = participantStruct.LapDistance;
            opponentData.DeltaTime = new DeltaTime(trackLength, opponentData.DistanceRoundTrack, participantStruct.CarSpeed, DateTime.UtcNow);
            opponentData.CarClass = CarData.getCarClassForRaceRoomId(participantStruct.DriverInfo.ClassId);
            opponentData.CurrentTyres = mapToTyreType(participantStruct.TireTypeFront, participantStruct.TireSubtypeFront,
                participantStruct.TireTypeRear, participantStruct.TireSubtypeRear, opponentData.CarClass.carClassEnum);
            Console.WriteLine("New driver " + driverName + " is using car class " +
                opponentData.CarClass.getClassIdentifier() + " (class ID " + participantStruct.DriverInfo.ClassId + ") with tyres " + opponentData.CurrentTyres);
            opponentData.TyreChangesByLap[0] = opponentData.CurrentTyres;
            opponentData.r3eUserId = participantStruct.DriverInfo.UserId;
            return opponentData;
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

        public static String getNameFromBytes(byte[] name)
        {
            int count = Array.IndexOf(name, (byte)0);
            return Encoding.UTF8.GetString(name, 0, count);
        }

        private void filterBadFlags(int trackId, FlagEnum[] previousTickFlags, FlagEnum[] currentTickFlags)
        {
            // previousTickFlags may be null
            if (trackId == 3870 /* Spa GP - sector 3 flags don't seem to clear properly, until the exact behaviour is understood, just filter them*/)
            {
                currentTickFlags[2] = FlagEnum.GREEN;
            }
        }

        private static List<CornerData.EnumWithThresholds> getBrakeTempThresholds(TireData<BrakeTempInformation> brakeTempDataFromGame)
        {
            // the game sends cold, optimal and hot
            var btt = new List<CornerData.EnumWithThresholds>();
            // make a range for optimal centred around optimal
            var maxCold = brakeTempDataFromGame.FrontLeft.ColdTemp + ((brakeTempDataFromGame.FrontLeft.OptimalTemp - brakeTempDataFromGame.FrontLeft.ColdTemp) / 2);
            var maxWarm = brakeTempDataFromGame.FrontLeft.HotTemp - ((brakeTempDataFromGame.FrontLeft.HotTemp - brakeTempDataFromGame.FrontLeft.OptimalTemp) / 2);
            btt.Add(new CornerData.EnumWithThresholds(BrakeTemp.COLD, -10000, maxCold));
            btt.Add(new CornerData.EnumWithThresholds(BrakeTemp.WARM, maxCold, maxWarm));
            btt.Add(new CornerData.EnumWithThresholds(BrakeTemp.HOT, maxWarm, brakeTempDataFromGame.FrontLeft.HotTemp));
            btt.Add(new CornerData.EnumWithThresholds(BrakeTemp.COOKING, brakeTempDataFromGame.FrontLeft.HotTemp, 10000));
            return btt;
        }
    }
}
