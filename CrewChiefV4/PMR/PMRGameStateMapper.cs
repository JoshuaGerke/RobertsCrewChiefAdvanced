using CrewChiefV4.AMS2;
using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using CrewChiefV4.iRacing;
using CrewChiefV4.RaceRoom.RaceRoomData;
using CrewChiefV4.rFactor1.rFactor1Data;
using System;
using System.Collections.Generic;

namespace CrewChiefV4.PMR
{
    /// <summary>
    /// Maps the PMR UDP data (PMRUDPReader.PMRStructWrapper) into CrewChief's GameStateData.
    /// </summary>
    class PMRGameStateMapper : GameStateMapper
    {
        // Simple session-start tracking so we can produce a reasonable SessionRunningTime
        private long sessionStartTicks = 0;
        private string lastSessionKey = null;

        // Next time we're allowed to take a conditions sample
        private DateTime nextConditionsSampleDue = DateTime.MinValue;

        // these are set when we start a new session, from the car name / class
        private TyreType defaultTyreTypeForPlayersCar = TyreType.Unknown_Race;
        private List<CornerData.EnumWithThresholds> brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(new CarData.CarClass());

        private Dictionary<string, float> waitingForCarsToFinish = new Dictionary<string, float>();
        private DateTime nextDebugCheckeredToFinishMessageTime = DateTime.MinValue;

        private static int lastGuessAtPlayerIndex = -1;
        Dictionary<string, DateTime> lastActiveTimeForOpponents = new Dictionary<string, DateTime>();
        DateTime nextOpponentCleanupTime = DateTime.MinValue;
        TimeSpan opponentCleanupInterval = TimeSpan.FromSeconds(4);

        private Boolean collisionOnThisLap = false;


        private DateTime lastTimeEngineWasRunning = DateTime.MaxValue;

        List<String> opponentDriverNamesProcessedForThisTick = new List<String>();
        HashSet<uint> positionsFilledForThisTick = new HashSet<uint>();

        private Boolean loggedPossibleTrackLimitViolationOnThisLap = false;
        private Boolean loggedTrackLimitViolationOnThisLap = false;


        private static string BuildSessionKey(UDPRaceInfo raceInfo)
        {
            if (raceInfo == null)
                return null;

            // Track + layout + session name uniquely identify a "session" for our purposes
            return string.Format("{0}:{1}:{2}",
                raceInfo.m_track ?? "",
                raceInfo.m_layout ?? "",
                raceInfo.m_session ?? "");
        }

        private static SessionType MapSessionType(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName))
                return SessionType.Unavailable;

            var s = sessionName.Trim().ToLowerInvariant();

            if (s.Contains("prac") || s == "free practice" || s == "practice")
                return SessionType.Practice;

            if (s.Contains("qual") || s == "quali" || s == "qualification")
                return SessionType.Qualify;

            if (s.Contains("race"))
                return SessionType.Race;

            if (s.Contains("test") || s.Contains("hotlap") || s.Contains("time trial"))
                return SessionType.HotLap;

            return SessionType.Unavailable;
        }

        private SessionPhase MapSessionPhase(SessionType sessionType, UDPRaceSessionState state, int numParticipants, SessionPhase previousSessionPhase,
            float sessionTimeRemaining, float sessionRunTime, Dictionary<string, OpponentData> opponentData, float playerSpeed, DateTime now)
        {
            if(numParticipants < 1 || state == UDPRaceSessionState.Inactive)
            {
                return SessionPhase.Unavailable;
            }

            // PMR currently exposes only Inactive / Active / Complete in UDPRaceSessionState
            switch (state)
            {
                case UDPRaceSessionState.Active:
                    return SessionPhase.Green;
                case UDPRaceSessionState.Complete:
                    return SessionPhase.Finished;
                default:
                    return previousSessionPhase;
            }
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

        public override void versionCheck(object memoryMappedFileStruct)
        {
            // Version checking is already handled in UDPProtocol.decode()
            // via the *_expectedPacketVersion constants.
        }

        public override GameStateData mapToGameStateData(object memoryMappedFileStruct, GameStateData previousGameState)
        {
            var wrapper = memoryMappedFileStruct as PMRUDPReader.PMRStructWrapper;
            if (wrapper == null)
            {
                // No valid data to map, just keep the previous state
                return previousGameState;
            }

            UDPRaceInfo raceInfo = wrapper.RaceInfo;
            List<UDPParticipantRaceState> leaderboard = wrapper.Leaderboard;
            UDPParticipantRaceState playerState = wrapper.PlayerRaceState;
            UDPVehicleTelemetry playerTelemetry = wrapper.PlayerTelemetry;
            List<UDPVehicleTelemetry> vehicleTelemetries = wrapper.AllTelemetry;
            long ticksWhenRead = wrapper.ticksWhenRead;

            // Basic sanity checks
            if (raceInfo == null ||
                leaderboard == null || leaderboard.Count == 0 ||
                playerState == null ||
                playerTelemetry == null ||
                raceInfo.m_state == UDPRaceSessionState.Inactive)
            {
                return previousGameState;
            }

            // Create the game state and attach the raw UDP data object for trace / debug
            GameStateData currentGameState = new GameStateData(ticksWhenRead);
            currentGameState.rawGameData = wrapper;
            var sData = currentGameState.SessionData;
            var pm = currentGameState.PositionAndMotionData;
            var pitData = currentGameState.PitData;
            var pData = currentGameState.PenaltiesData;
            var con = currentGameState.Conditions;
            var ctrl = currentGameState.ControlData;
            var trans = currentGameState.TransmissionData;
            var cd = currentGameState.CarDamageData;

            sData.SectorNumber = playerState.m_currentSector + 1; // stored as 0-based in UDP


            AdditionalDataProvider.validate(playerState.m_driverName);
            currentGameState.carName = playerState.m_vehicleName;     
            sData.CompletedLaps = playerState.m_currentLap - 1;
            sData.LapCount = playerState.m_currentLap; 
            sData.OverallPosition = playerState.m_racePos;
            if (sData.OverallPosition == 1)
            {
                sData.LeaderSectorNumber = sData.SectorNumber;
            }
            sData.IsNewSector = previousGameState == null || playerState.m_currentSector + 1 != previousGameState.SessionData.SectorNumber;

            string trackId = string.Format("{0}:{1}",
                raceInfo.m_track ?? "",
                raceInfo.m_layout ?? "");

            currentGameState.trackName = trackId;
            sData.TrackDefinition =
                  TrackData.getTrackDefinition(trackId, raceInfo.m_layoutLength);
            float trackLength =
                (sData.TrackDefinition != null &&
                 sData.TrackDefinition.trackLength > 0f)
                    ? sData.TrackDefinition.trackLength
                    : raceInfo.m_layoutLength;
            pm.DistanceRoundTrack = trackLength * playerState.m_lapProgress; // 0..1 * trackLength

            // previous session data to check if we've started an new session
            SessionPhase lastSessionPhase = SessionPhase.Unavailable;
            SessionType lastSessionType = SessionType.Unavailable;
            float lastSessionRunningTime = 0;
            int lastSessionLapsCompleted = 0;
            TrackDefinition lastSessionTrack = null;
            Boolean lastSessionHasFixedTime = false;
            int lastSessionNumberOfLaps = 0;
            float lastSessionTotalRunTime = 0;
            float lastSessionTimeRemaining = 0;
            // Carry over conditions from previous state so the samples list is continuous
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

                sData.PlayerLapTimeSessionBest = previousGameState.SessionData.PlayerLapTimeSessionBest;
                sData.PlayerLapTimeSessionBestPrevious = previousGameState.SessionData.PlayerLapTimeSessionBestPrevious;
                sData.OpponentsLapTimeSessionBestOverall = previousGameState.SessionData.OpponentsLapTimeSessionBestOverall;
                sData.OpponentsLapTimeSessionBestPlayerClass = previousGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass;
                sData.OverallSessionBestLapTime = previousGameState.SessionData.OverallSessionBestLapTime;
                sData.PlayerClassSessionBestLapTime = previousGameState.SessionData.PlayerClassSessionBestLapTime;
                currentGameState.readLandmarksForThisLap = previousGameState.readLandmarksForThisLap;

            }

            if (currentGameState.carClass.carClassEnum == CarData.CarClassEnum.UNKNOWN_RACE)
            {

                CarData.CarClass newClass =
                  CarData.getCarClassForClassNameOrCarName(playerState.m_vehicleClass, playerState.m_vehicleName);
                CarData.CLASS_ID = playerState.m_vehicleClass;
                if (!CarData.IsCarClassEqual(newClass, currentGameState.carClass, false))
                {
                    currentGameState.carClass = newClass;
                    GlobalBehaviourSettings.UpdateFromCarClass(currentGameState.carClass);
                    Console.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier());
                    brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(currentGameState.carClass);
                    // tyre types mapped at each sector point
                    defaultTyreTypeForPlayersCar = CarData.getDefaultTyreType(currentGameState.carClass);
                    Utilities.TraceEventClass(currentGameState);
                }
            }

            // current session data
            sData.SessionType = MapSessionType(raceInfo.m_session);
            Boolean leaderHasFinished = previousGameState != null && previousGameState.SessionData.LeaderHasFinishedRace;
            sData.LeaderHasFinishedRace = leaderHasFinished;
            sData.IsDisqualified = playerState.m_dq;
            int numberOfLapsInSession = 0;
            // Session length info
            if (raceInfo.m_isLaps && sData.SessionType == SessionType.Race)
            {
                numberOfLapsInSession = (int)raceInfo.m_duration; // duration == lap count 
                sData.SessionTotalRunTime = 0;                         
            }
            else
            {
                sData.SessionHasFixedTime = true; 
                sData.SessionTotalRunTime = raceInfo.m_duration;        // seconds
            }

            sData.SessionPhase = MapSessionPhase(sData.SessionType
                , raceInfo.m_state, raceInfo.m_numParticipants, lastSessionPhase, lastSessionTimeRemaining,
                lastSessionRunningTime, previousGameState == null ? null : previousGameState.OpponentData,
                playerTelemetry.m_chassis.m_overallSpeed, currentGameState.Now);

            // now check if this is a new session...

            Boolean sessionOfSameTypeRestarted = ((sData.SessionType == SessionType.Race && lastSessionType == SessionType.Race) ||
                (sData.SessionType == SessionType.Practice && lastSessionType == SessionType.Practice) ||
                (sData.SessionType == SessionType.Qualify && lastSessionType == SessionType.Qualify)) &&
                (lastSessionPhase == SessionPhase.Green || lastSessionPhase == SessionPhase.Checkered || lastSessionPhase == SessionPhase.FullCourseYellow ||
                    lastSessionPhase == SessionPhase.Finished) &&
                sData.SessionPhase == SessionPhase.Countdown &&
                (sData.SessionType == SessionType.Race ||
                    sData.SessionHasFixedTime && sData.SessionTimeRemaining > lastSessionTimeRemaining + 1);

          
            UDPRaceSessionState rawRaceState = raceInfo.m_state;
            Boolean ignoreFinishedStatus = previousGameState != null &&
                ((previousGameState.SessionData.SessionType == SessionType.Practice && sData.SessionType == SessionType.Qualify) ||
                 (previousGameState.SessionData.SessionType == SessionType.Qualify && sData.SessionType == SessionType.Race))
                 && rawRaceState == UDPRaceSessionState.Complete;
          
            if (ignoreFinishedStatus)
            {
                // don't allow the session type to be updated here
                sData.SessionType = previousGameState.SessionData.SessionType;
            }
            if (!ignoreFinishedStatus &&
                (sessionOfSameTypeRestarted ||
                 (sData.SessionType != SessionType.Unavailable &&
                     (lastSessionType != sData.SessionType ||
                         lastSessionTrack == null || lastSessionTrack.name != sData.TrackDefinition.name ||
                             (sData.SessionHasFixedTime && sData.SessionTimeRemaining > lastSessionTimeRemaining + 1))))
                )
            {
                lastGuessAtPlayerIndex = -1;
                lastActiveTimeForOpponents.Clear();
                nextOpponentCleanupTime = currentGameState.Now + opponentCleanupInterval;
                Console.WriteLine("New session, trigger...");
                if (sessionOfSameTypeRestarted)
                {
                    Console.WriteLine("Session of same type (" + lastSessionType + ") restarted (green / finished -> countdown)");
                }
                if (lastSessionType != sData.SessionType)
                {
                    Console.WriteLine("lastSessionType = " + lastSessionType + " currentGameState.SessionData.SessionType = " + sData.SessionType);
                }
                else if (lastSessionTrack != sData.TrackDefinition)
                {
                    String lastTrackName = lastSessionTrack == null ? "unknown" : lastSessionTrack.name;
                    String currentTrackName = sData.TrackDefinition == null ? "unknown" : sData.TrackDefinition.name;
                    Console.WriteLine("lastSessionTrack = " + lastTrackName + " currentGameState.SessionData.Track = " + currentTrackName);
                }
                else if (sData.SessionHasFixedTime && sData.SessionTimeRemaining > lastSessionTimeRemaining + 1)
                {
                    Console.WriteLine("sessionTimeRemaining = " + sData.SessionTimeRemaining + " lastSessionTimeRemaining = " + lastSessionTimeRemaining);
                }
                sData.IsNewSession = true;
                sData.SessionNumberOfLaps = numberOfLapsInSession;
                sData.LeaderHasFinishedRace = false;
                sData.SessionStartTime = currentGameState.Now;
                if (sData.SessionHasFixedTime)
                {
                    sData.SessionTotalRunTime = raceInfo.m_duration;
                    sData.SessionTimeRemaining = raceInfo.m_duration;
                    sData.SessionRunningTime = 0;
                    Console.WriteLine("Time in this new session = " + sData.SessionTimeRemaining);
                }
                else
                {
                    sData.SessionNumberOfLaps = numberOfLapsInSession;
                }
                sData.DriverRawName = playerState.m_driverName;
                pitData.IsRefuellingAllowed = true;

                if ((currentGameState.SessionData.SessionType == SessionType.Practice || currentGameState.SessionData.SessionType == SessionType.Qualify)
                    && playerState.m_inPits)
                {
                    currentGameState.PitData.PitBoxPositionEstimate = playerState.m_lapProgress * sData.TrackDefinition.trackLength;
                    currentGameState.PitData.PitBoxLocationEstimate = new float[] { playerTelemetry.m_chassis.m_posWS.x, playerTelemetry.m_chassis.m_posWS.y, playerTelemetry.m_chassis.m_posWS.z };
                    Console.WriteLine("Pit box position = " + currentGameState.PitData.PitBoxPositionEstimate.ToString("0.000"));
                }
                else if (previousGameState != null)
                {
                    // if we're entering a race session or rolling qually, copy the value from the previous field
                    currentGameState.PitData.PitBoxPositionEstimate = previousGameState.PitData.PitBoxPositionEstimate;
                    currentGameState.PitData.PitBoxLocationEstimate = previousGameState.PitData.PitBoxLocationEstimate;
                }

                string carClassId = playerState.m_vehicleClass;
                currentGameState.carClass = CarData.getCarClassForClassNameOrCarName(carClassId);
                GlobalBehaviourSettings.UpdateFromCarClass(currentGameState.carClass);
                CarData.CLASS_ID = carClassId;

                Console.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier());
                brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(currentGameState.carClass);
                // tyre types mapped at each sector point
                defaultTyreTypeForPlayersCar = CarData.getDefaultTyreType(currentGameState.carClass);

                lastTimeEngineWasRunning = DateTime.MaxValue;

                opponentDriverNamesProcessedForThisTick.Clear();
                opponentDriverNamesProcessedForThisTick.Add(playerState.m_driverName);
                positionsFilledForThisTick.Clear();
                positionsFilledForThisTick.Add((uint)sData.OverallPosition);
                for (int i = 0; i < leaderboard.Count; i++)
                {
                    UDPParticipantRaceState participantStuct = leaderboard[i];
                    UDPVehicleTelemetry participantTelemetry = GetVehicleTelemetryByID(vehicleTelemetries, participantStuct.m_vehicleId);
                    String participantName = participantStuct.m_driverName;
                    if (participantStuct.m_vehicleId != playerState.m_vehicleId && participantName != null && participantName.Length > 0
                        && !opponentDriverNamesProcessedForThisTick.Contains(participantName) && !positionsFilledForThisTick.Contains((uint)participantStuct.m_racePos)) 
                    {
                        CarData.CarClass opponentCarClass = CarData.getCarClassForClassNameOrCarName(participantStuct.m_vehicleClass);
                        if (participantTelemetry != null)
                        {
                            addOpponentForName(participantName, createOpponentData(participantStuct, participantTelemetry, false,
                                opponentCarClass, participantName != null && participantName.Length != 0, sData.TrackDefinition.trackLength), currentGameState);
                            opponentDriverNamesProcessedForThisTick.Add(participantName);
                            positionsFilledForThisTick.Add((uint)participantStuct.m_racePos);
                        }                       
                    }
                }

                sData.PlayerLapTimeSessionBest = -1;
                sData.OpponentsLapTimeSessionBestOverall = -1;
                sData.OpponentsLapTimeSessionBestPlayerClass = -1;
                sData.OverallSessionBestLapTime = -1;
                sData.PlayerClassSessionBestLapTime = -1;
                TrackDataContainer tdc = TrackData.TRACK_LANDMARKS_DATA.getTrackDataForTrackName(sData.TrackDefinition.name, trackLength);
                sData.TrackDefinition.trackLandmarks = tdc.trackLandmarks;
                sData.TrackDefinition.isOval = tdc.isOval;
                sData.TrackDefinition.setGapPoints();
                GlobalBehaviourSettings.UpdateFromTrackDefinition(sData.TrackDefinition);
                if (previousGameState != null && previousGameState.SessionData.TrackDefinition != null)
                {
                    if (previousGameState.SessionData.TrackDefinition.name.Equals(sData.TrackDefinition.name))
                    {
                        if (previousGameState.hardPartsOnTrackData.hardPartsMapped)
                        {
                            currentGameState.hardPartsOnTrackData = previousGameState.hardPartsOnTrackData;
                        }
                    }
                }
                sData.DeltaTime = new DeltaTime(sData.TrackDefinition.trackLength,
                    pm.DistanceRoundTrack, pm.CarSpeed, currentGameState.Now);

                pitData.MandatoryPitStopCompleted = false;
                pitData.PitWindow = PitWindow.Unavailable;
            }
            else
            {
                if (lastSessionPhase != sData.SessionPhase)
                {
                    if (sData.SessionPhase == SessionPhase.Green)
                    {
                        // just gone green, so get the session data.
                        lastActiveTimeForOpponents.Clear();
                        nextOpponentCleanupTime = currentGameState.Now + opponentCleanupInterval;
                        if (sData.SessionType == SessionType.Race)
                        {
                            sData.JustGoneGreen = true;
                            // ensure that we track the car we're in at the point when the lights change
                            if (sData.SessionHasFixedTime)
                            {
                                sData.SessionTotalRunTime = raceInfo.m_duration;
                                sData.SessionTimeRemaining = raceInfo.m_duration;
                                sData.SessionRunningTime = 0;
                            }
                            sData.SessionStartTime = currentGameState.Now;
                            sData.SessionNumberOfLaps = numberOfLapsInSession;
                        }
                        sData.LeaderHasFinishedRace = false;
                        sData.NumCarsOverallAtStartOfSession = raceInfo.m_numParticipants;
                        trackId = string.Format("{0}:{1}",
                        raceInfo.m_track ?? "",
                         raceInfo.m_layout ?? "");
                        sData.TrackDefinition = TrackData.getTrackDefinition(trackId, raceInfo.m_layoutLength);
                        TrackDataContainer tdc = TrackData.TRACK_LANDMARKS_DATA.getTrackDataForTrackName(sData.TrackDefinition.name, raceInfo.m_layoutLength);
                        sData.TrackDefinition.trackLandmarks = tdc.trackLandmarks;
                        sData.TrackDefinition.isOval = tdc.isOval;
                        sData.TrackDefinition.setGapPoints();
                        GlobalBehaviourSettings.UpdateFromTrackDefinition(sData.TrackDefinition);
                        if (previousGameState != null && previousGameState.SessionData.TrackDefinition != null)
                        {
                            if(previousGameState.SessionData.TrackDefinition.name.Equals(sData.TrackDefinition.name))
                            {
                                if (previousGameState.hardPartsOnTrackData.hardPartsMapped)
                                {
                                    currentGameState.hardPartsOnTrackData = previousGameState.hardPartsOnTrackData;
                                }
                            }
                        }
                        String carClassId = playerState.m_vehicleClass;
                        currentGameState.carClass = CarData.getCarClassForClassNameOrCarName(carClassId);
                        GlobalBehaviourSettings.UpdateFromCarClass(currentGameState.carClass);
                        CarData.CLASS_ID = carClassId;

                        Console.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier());
                        brakeTempThresholdsForPlayersCar = CarData.getBrakeTempThresholds(currentGameState.carClass);
                        defaultTyreTypeForPlayersCar = CarData.getDefaultTyreType(currentGameState.carClass);
                        if (previousGameState != null)
                        {
                            currentGameState.OpponentData = previousGameState.OpponentData;
                            pitData.IsRefuellingAllowed = previousGameState.PitData.IsRefuellingAllowed;
                            if (sData.SessionType != SessionType.Race)
                            {
                                sData.SessionStartTime = previousGameState.SessionData.SessionStartTime;
                                sData.SessionTotalRunTime = previousGameState.SessionData.SessionTotalRunTime;
                                sData.SessionTimeRemaining = previousGameState.SessionData.SessionTimeRemaining;
                                sData.SessionNumberOfLaps = previousGameState.SessionData.SessionNumberOfLaps;
                            }
                        }

                        pitData.MandatoryPitStopCompleted = false;
                        pitData.PitWindow = PitWindow.Unavailable;

                        sData.DeltaTime = new DeltaTime(sData.TrackDefinition.trackLength,
                            pm.DistanceRoundTrack, pm.CarSpeed, currentGameState.Now);

                        Console.WriteLine("Just gone green, session details...");
                        Console.WriteLine("SessionType " + sData.SessionType);
                        Console.WriteLine("SessionPhase " + sData.SessionPhase);
                        if (previousGameState != null)
                        {
                            Console.WriteLine("previous SessionPhase " + previousGameState.SessionData.SessionPhase);
                        }
                        Console.WriteLine("EventIndex " + sData.EventIndex);
                        Console.WriteLine("SessionIteration " + sData.SessionIteration);
                        Console.WriteLine("HasMandatoryPitStop " + pitData.HasMandatoryPitStop);
                        Console.WriteLine("PitWindowStart " + pitData.PitWindowStart);
                        Console.WriteLine("PitWindowEnd " + pitData.PitWindowEnd);
                        Console.WriteLine("NumCarsAtStartOfSession " + sData.NumCarsOverallAtStartOfSession);
                        Console.WriteLine("SessionNumberOfLaps " + sData.SessionNumberOfLaps);
                        Console.WriteLine("SessionRunTime " + sData.SessionTotalRunTime);
                        Console.WriteLine("SessionStartTime " + sData.SessionStartTime);
                        String trackName = sData.TrackDefinition == null ? "unknown" : sData.TrackDefinition.name;
                        Console.WriteLine("TrackName " + trackName);
                    }
                }
                // copy peristent data from the previous game state

                if (!sData.JustGoneGreen && previousGameState != null)
                {
                    sData.SessionStartTime = previousGameState.SessionData.SessionStartTime;
                    sData.SessionTotalRunTime = previousGameState.SessionData.SessionTotalRunTime;
                    sData.SessionNumberOfLaps = previousGameState.SessionData.SessionNumberOfLaps;
                    sData.NumCarsOverallAtStartOfSession = previousGameState.SessionData.NumCarsOverallAtStartOfSession;
                    sData.NumCarsInPlayerClassAtStartOfSession = previousGameState.SessionData.NumCarsInPlayerClassAtStartOfSession;
                    sData.TrackDefinition = previousGameState.SessionData.TrackDefinition;
                    sData.EventIndex = previousGameState.SessionData.EventIndex;
                    sData.SessionIteration = previousGameState.SessionData.SessionIteration;
                    sData.PositionAtStartOfCurrentLap = previousGameState.SessionData.PositionAtStartOfCurrentLap;
                    sData.SessionStartClassPosition = previousGameState.SessionData.SessionStartClassPosition;
                    sData.ClassPositionAtStartOfCurrentLap = previousGameState.SessionData.ClassPositionAtStartOfCurrentLap;
                    sData.LapTimePrevious = previousGameState.SessionData.LapTimePrevious;
                    currentGameState.OpponentData = previousGameState.OpponentData;

                    pitData.PitWindowStart = previousGameState.PitData.PitWindowStart;
                    pitData.PitWindowEnd = previousGameState.PitData.PitWindowEnd;
                    pitData.PitWindow = previousGameState.PitData.PitWindow;
                    pitData.HasMandatoryPitStop = previousGameState.PitData.HasMandatoryPitStop;
                    pitData.HasMandatoryTyreChange = previousGameState.PitData.HasMandatoryTyreChange;
                    pitData.MandatoryTyreChangeRequiredTyreType = previousGameState.PitData.MandatoryTyreChangeRequiredTyreType;
                    pitData.IsRefuellingAllowed = previousGameState.PitData.IsRefuellingAllowed;
                    pitData.MaxPermittedDistanceOnCurrentTyre = previousGameState.PitData.MaxPermittedDistanceOnCurrentTyre;
                    pitData.MinPermittedDistanceOnCurrentTyre = previousGameState.PitData.MinPermittedDistanceOnCurrentTyre;
                    pitData.OnInLap = previousGameState.PitData.OnInLap;
                    pitData.OnOutLap = previousGameState.PitData.OnOutLap;
                    pitData.HasRequestedPitStop = previousGameState.PitData.HasRequestedPitStop;
                    pitData.PitStallOccupied = previousGameState.PitData.PitStallOccupied;
                    pitData.IsPitCrewReady = previousGameState.PitData.IsPitCrewReady;

                    sData.SessionTimesAtEndOfSectors = previousGameState.SessionData.SessionTimesAtEndOfSectors;
                    pData.CutTrackWarnings = previousGameState.PenaltiesData.CutTrackWarnings;
                    sData.formattedPlayerLapTimes = previousGameState.SessionData.formattedPlayerLapTimes;
                    sData.GameTimeAtLastPositionFrontChange = previousGameState.SessionData.GameTimeAtLastPositionFrontChange;
                    sData.GameTimeAtLastPositionBehindChange = previousGameState.SessionData.GameTimeAtLastPositionBehindChange;
                    sData.OpponentKeyInFront = previousGameState.SessionData.OpponentKeyInFront;
                    sData.OpponentKeyBehind = previousGameState.SessionData.OpponentKeyBehind;
                    sData.LastSector1Time = previousGameState.SessionData.LastSector1Time;
                    sData.LastSector2Time = previousGameState.SessionData.LastSector2Time;
                    sData.LastSector3Time = previousGameState.SessionData.LastSector3Time;
                    sData.PlayerBestSector1Time = previousGameState.SessionData.PlayerBestSector1Time;
                    sData.PlayerBestSector2Time = previousGameState.SessionData.PlayerBestSector2Time;
                    sData.PlayerBestSector3Time = previousGameState.SessionData.PlayerBestSector3Time;
                    sData.PlayerBestLapSector1Time = previousGameState.SessionData.PlayerBestLapSector1Time;
                    sData.PlayerBestLapSector2Time = previousGameState.SessionData.PlayerBestLapSector2Time;
                    sData.PlayerBestLapSector3Time = previousGameState.SessionData.PlayerBestLapSector3Time;
                    con.CurrentConditions = previousGameState.Conditions.CurrentConditions;
                    con.samples = previousGameState.Conditions.samples;
                    sData.trackLandmarksTiming = previousGameState.SessionData.trackLandmarksTiming;
                    sData.PlayerLapData = previousGameState.SessionData.PlayerLapData;
                    sData.CurrentLapIsValid = previousGameState.SessionData.CurrentLapIsValid;
                    sData.PreviousLapWasValid = previousGameState.SessionData.PreviousLapWasValid;

                    sData.DeltaTime = previousGameState.SessionData.DeltaTime;

                    currentGameState.disqualifiedDriverNames = previousGameState.disqualifiedDriverNames;
                    currentGameState.retriedDriverNames = previousGameState.retriedDriverNames;

                    currentGameState.hardPartsOnTrackData = previousGameState.hardPartsOnTrackData;

                    currentGameState.TimingData = previousGameState.TimingData;

                    sData.JustGoneGreenTime = previousGameState.SessionData.JustGoneGreenTime;
                }
            }

            ctrl.ThrottlePedal = playerTelemetry.m_input.m_accelerator;
            ctrl.ClutchPedal = playerTelemetry.m_input.m_clutch;
            trans.Gear = playerTelemetry.m_input.m_gear;
            ctrl.BrakePedal = playerTelemetry.m_input.m_brake;
            ctrl.HandBrake = playerTelemetry.m_input.m_handbrake;
            ctrl.SteeringWheelAngle = playerTelemetry.m_general.m_steeringWheelAngle;


            //------------------- Variable session data ----------------------------------
            var elapsedSinceStart = (float)(currentGameState.Now - sData.SessionStartTime).TotalSeconds;
            if (sData.SessionHasFixedTime)
            {
               
                if (sData.SessionTimeRemaining <= 0 && previousGameState != null && previousGameState.SessionData.SessionRunningTime > 0)
                {
                    sData.SessionRunningTime = previousGameState.SessionData.SessionRunningTime +
                        (float)(currentGameState.Now - previousGameState.Now).TotalSeconds;
                }
                else
                {
                    sData.SessionRunningTime = elapsedSinceStart; 
                }
                sData.SessionTimeRemaining = raceInfo.m_duration - sData.SessionRunningTime;
            }
            else
            {
                sData.SessionRunningTime = elapsedSinceStart;
            }
            sData.ExtraLapsAfterTimedSessionComplete = 0;

            pitData.InPitlane = playerState.m_inPits;

            sData.Flag = mapToFlagEnum(playerState.m_flags);
            sData.NumCarsOverall = raceInfo.m_numParticipants;
            sData.IsNewLap = previousGameState != null &&
                 (sData.CompletedLaps == previousGameState.SessionData.CompletedLaps + 1 ||
                  (sData.SessionType == SessionType.Practice &&
                      sData.SectorNumber == 1 && previousGameState.SessionData.SectorNumber == 3));

            bool isRaining = raceInfo.m_weather.Contains("Rain");

            if (sData.IsNewLap)
            {
                currentGameState.readLandmarksForThisLap = false;
                loggedPossibleTrackLimitViolationOnThisLap = false;
                loggedTrackLimitViolationOnThisLap = false;
                collisionOnThisLap = false;

               

                sData.playerCompleteLapWithProvidedLapTime( sData.OverallPosition, sData.SessionRunningTime,
                    previousGameState.SessionData.LapTimeCurrent, sData.CurrentLapIsValid, pitData.InPitlane, isRaining,
                    raceInfo.m_trackTemperature, raceInfo.m_ambientTemperature, sData.SessionHasFixedTime,
                    sData.SessionTimeRemaining, 3, currentGameState.TimingData, null, null);
                sData.playerStartNewLap(sData.CompletedLaps + 1,
                    sData.OverallPosition, pitData.InPitlane, sData.SessionRunningTime);
                currentGameState.SessionData.IsLastLap = sData.Flag == FlagEnum.WHITE;
            }
            else if (sData.IsNewSector)
            {
                if (sData.SectorNumber == 2)
                {
                    sData.playerAddCumulativeSectorData(1, sData.OverallPosition, playerState.m_sectorTimes[0],
                        sData.SessionRunningTime, sData.CurrentLapIsValid, isRaining,
                        raceInfo.m_trackTemperature, raceInfo.m_ambientTemperature);
                }
                else if(sData.SectorNumber == 3)
                {
                    sData.playerAddCumulativeSectorData(2, sData.OverallPosition, playerState.m_sectorTimes[1] + playerState.m_sectorTimes[0],
                       sData.SessionRunningTime, sData.CurrentLapIsValid, isRaining,
                       raceInfo.m_trackTemperature, raceInfo.m_ambientTemperature);
                }
            }
            sData.DeltaTime.SetNextDeltaPoint(pm.DistanceRoundTrack,
                sData.CompletedLaps, playerTelemetry.m_chassis.m_overallSpeed, currentGameState.Now, !pitData.InPitlane);
            if (previousGameState != null)
            {
                String stoppedInLandmark = sData.trackLandmarksTiming.updateLandmarkTiming(sData.TrackDefinition,
                    sData.SessionRunningTime, previousGameState.PositionAndMotionData.DistanceRoundTrack,
                    pm.DistanceRoundTrack, playerTelemetry.m_chassis.m_overallSpeed, currentGameState.carClass);
                sData.stoppedInLandmark = pitData.InPitlane ? null : stoppedInLandmark;
                if (sData.IsNewLap)
                {
                    sData.trackLandmarksTiming.cancelWaitingForLandmarkEnd();
                }
            }

            sData.LapTimeCurrent = playerState.m_currentLapTime;

            opponentDriverNamesProcessedForThisTick.Clear();
            opponentDriverNamesProcessedForThisTick.Add(playerState.m_driverName);
            positionsFilledForThisTick.Clear();
            positionsFilledForThisTick.Add((uint)sData.OverallPosition);
            for ( int i = 0; i < leaderboard.Count; i++)
            {
                if (!leaderboard[i].m_isPlayer)
                {
                    UDPParticipantRaceState participantStruct = leaderboard[i];
                    UDPVehicleTelemetry participantTelemetry = GetVehicleTelemetryByID(vehicleTelemetries, participantStruct.m_vehicleId);
                    if(participantStruct.m_driverName == null || participantStruct.m_driverName[0] == 0)
                    {
                        // first character of name is null - this means the game regards this driver as inactive or missing for this update
                        continue;
                    }
                    if (positionsFilledForThisTick.Contains((uint)participantStruct.m_racePos))
                    {
                        // discard this participant element because the race position is already occupied
                        continue;
                    }
                    String participantName = participantStruct.m_driverName;
                    if (participantName != null && participantName.Length > 0 && !opponentDriverNamesProcessedForThisTick.Contains(participantName))
                    {
                        opponentDriverNamesProcessedForThisTick.Add(participantName);
                        positionsFilledForThisTick.Add((uint)participantStruct.m_racePos);
                        if (participantStruct.m_dq)
                        {
                            if (!currentGameState.disqualifiedDriverNames.ContainsKey(participantName))
                            {
                                Console.WriteLine("Opponent " + participantName + " has been disqualified");
                                currentGameState.disqualifiedDriverNames.Add(participantName, null);
                            }
                            // remove this driver from the set immediately
                            currentGameState.OpponentData.Remove(participantName);
                            continue;
                        }

                        OpponentData currentOpponentData = null;
                        if (currentGameState.OpponentData.TryGetValue(participantName, out currentOpponentData))
                        {
                            lastActiveTimeForOpponents[participantName] = currentGameState.Now;
                            if (previousGameState != null)
                            {
                                int previousOpponentSectorNumber = 1;
                                int previousOpponentCompletedLaps = 0;
                                int previousOpponentPosition = 0;
                                Boolean previousOpponentIsEnteringPits = false;

                                float[] previousOpponentWorldPosition = new float[] { 0, 0, 0 };
                                float previousDistanceRoundTrack = 0;
                                OpponentData previousOpponentData = null;
                                if (previousGameState.OpponentData.TryGetValue(participantName, out previousOpponentData))
                                {
                                    previousOpponentSectorNumber = previousOpponentData.CurrentSectorNumber;
                                    previousOpponentCompletedLaps = previousOpponentData.CompletedLaps;
                                    previousOpponentPosition = previousOpponentData.OverallPosition;
                                    previousOpponentIsEnteringPits = previousOpponentData.isEnteringPits();
                                    previousOpponentWorldPosition = previousOpponentData.WorldPosition;
                                    previousDistanceRoundTrack = previousOpponentData.DistanceRoundTrack;
                                    currentOpponentData.ClassPositionAtPreviousTick = previousOpponentData.ClassPosition;
                                    currentOpponentData.OverallPositionAtPreviousTick = previousOpponentData.OverallPosition;
                                }

                                int currentOpponentRacePosition = participantStruct.m_racePos;
                                int currentOpponentLapsCompleted = (int)participantStruct.m_currentLap - 1;
                                int currentOpponentSector = (int)participantStruct.m_currentSector + 1;  // zero-indexed
                                if (currentOpponentSector == 0)
                                {
                                    currentOpponentSector = previousOpponentSectorNumber;
                                }
                                float currentOpponentLapDistance = participantStruct.m_lapProgress * sData.TrackDefinition.trackLength;

                                Boolean finishedAllottedRaceLaps = sData.SessionNumberOfLaps > 0 && sData.SessionNumberOfLaps == currentOpponentLapsCompleted;
                                Boolean finishedAllotedRaceTime = false;

                                if (sData.SessionTotalRunTime > 0 && sData.SessionTimeRemaining < 1)
                                {
                                    if (previousOpponentCompletedLaps < currentOpponentLapsCompleted)
                                    {
                                        currentOpponentData.LapsStartedAfterRaceTimeEnd++;
                                    }
                                    finishedAllotedRaceTime = currentOpponentData.LapsStartedAfterRaceTimeEnd > sData.ExtraLapsAfterTimedSessionComplete;
                                }

                                if (currentOpponentRacePosition == 1 && (finishedAllotedRaceTime || finishedAllottedRaceLaps))
                                {
                                    sData.LeaderHasFinishedRace = true;
                                }
                                Boolean isInPits = participantStruct.m_inPits;

                                float secondsSinceLastUpdate = (float)new TimeSpan(currentGameState.Ticks - previousGameState.Ticks).TotalSeconds;
                                float lastSectorTime = -1;
                                if (participantStruct.m_sectorTimes.Count > participantStruct.m_currentSector &&
                                    participantStruct.m_currentSector > 0)
                                {
                                    lastSectorTime = participantStruct.m_sectorTimes[participantStruct.m_currentSector];
                                }
                                else
                                {
                                    Log.Warning(Log.DontSpam($"participantStruct.m_sectorTimes.Count {participantStruct.m_sectorTimes.Count} participantStruct.m_currentSector {participantStruct.m_currentSector}", 5));
                                }
                                updateOpponentData(currentOpponentData, currentOpponentRacePosition, currentOpponentLapsCompleted,
                                    currentOpponentSector, isInPits, sData.SessionRunningTime,
                                    new float[] {participantTelemetry.m_chassis.m_posWS.x, participantTelemetry.m_chassis.m_posWS.z },
                                    participantTelemetry.m_chassis.m_overallSpeed, participantStruct.m_lapProgress * sData.TrackDefinition.trackLength,
                                    isRaining, raceInfo.m_trackTemperature, raceInfo.m_ambientTemperature,
                                    sData.SessionHasFixedTime, sData.SessionTimeRemaining,
                                    lastSectorTime, false, sData.TrackDefinition.distanceForNearPitEntryChecks,
                                    currentGameState.TimingData, currentGameState.carClass);

                                currentOpponentData.DeltaTime.SetNextDeltaPoint(currentOpponentLapDistance, currentOpponentData.CompletedLaps,
                                    currentOpponentData.Speed, currentGameState.Now);

                                if (previousOpponentData != null)
                                {
                                    currentOpponentData.trackLandmarksTiming = previousOpponentData.trackLandmarksTiming;
                                    String stoppedInLandmark = currentOpponentData.trackLandmarksTiming.updateLandmarkTiming(
                                        sData.TrackDefinition, sData.SessionRunningTime,
                                        previousDistanceRoundTrack, currentOpponentData.DistanceRoundTrack, currentOpponentData.Speed,
                                         currentOpponentData.CarClass);
                                    currentOpponentData.stoppedInLandmark = currentOpponentData.InPits ? null : stoppedInLandmark;
                                }
                                if (sData.JustGoneGreen)
                                {
                                    currentOpponentData.trackLandmarksTiming = new TrackLandmarksTiming();
                                }
                                if (currentOpponentData.IsNewLap)
                                {
                                    currentOpponentData.trackLandmarksTiming.cancelWaitingForLandmarkEnd();
                                    currentOpponentData.CarClass = CarData.getCarClassForClassNameOrCarName(participantStruct.m_vehicleClass);
                                }
                                if (currentOpponentData.IsNewLap && currentOpponentData.CurrentBestLapTime > 0)
                                {
                                    if (sData.OpponentsLapTimeSessionBestOverall == -1 ||
                                         currentOpponentData.CurrentBestLapTime < sData.OpponentsLapTimeSessionBestOverall)
                                    {
                                        sData.OpponentsLapTimeSessionBestOverall = currentOpponentData.CurrentBestLapTime;
                                        if (sData.OverallSessionBestLapTime == -1 ||
                                            sData.OverallSessionBestLapTime > currentOpponentData.CurrentBestLapTime)
                                        {
                                            sData.OverallSessionBestLapTime = currentOpponentData.CurrentBestLapTime;
                                        }
                                    }
                                    if (CarData.IsCarClassEqual(currentOpponentData.CarClass, currentGameState.carClass))
                                    {
                                        if (sData.OpponentsLapTimeSessionBestPlayerClass == -1 ||
                                            currentOpponentData.CurrentBestLapTime < sData.OpponentsLapTimeSessionBestPlayerClass)
                                        {
                                            sData.OpponentsLapTimeSessionBestPlayerClass = currentOpponentData.CurrentBestLapTime;
                                            if (sData.PlayerClassSessionBestLapTime == -1 ||
                                                sData.PlayerClassSessionBestLapTime > currentOpponentData.CurrentBestLapTime)
                                            {
                                                sData.PlayerClassSessionBestLapTime = currentOpponentData.CurrentBestLapTime;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (participantStruct != null && participantName.Length > 0)
                            {
                                addOpponentForName(participantName, createOpponentData(participantStruct, participantTelemetry, true, CarData.getCarClassForClassNameOrCarName(participantStruct.m_vehicleClass),
                                    participantStruct.m_driverName != null && participantStruct.m_driverName[0] != 0, sData.TrackDefinition.trackLength), currentGameState);
                            }
                        }
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
                    DateTime lastTimeActive = DateTime.MinValue;
                    if (!lastActiveTimeForOpponents.TryGetValue(opponentName, out lastTimeActive) || lastTimeActive < oldestAllowedUpdate)
                    {
                        inactiveOpponents.Add(opponentName);
                        Console.WriteLine("Opponent " + opponentName + " has been inactive for " + opponentCleanupInterval + ", removing them");
                    }
                }
                foreach (String inactiveOpponent in inactiveOpponents)
                {
                    currentGameState.OpponentData.Remove(inactiveOpponent);
                }
            }

            currentGameState.sortClassPositions();
            currentGameState.setPracOrQualiDeltas();

            if (pitData.InPitlane)
            {
                if(previousGameState != null && !previousGameState.PitData.InPitlane)
                {
                    if (sData.SessionRunningTime > 30 && sData.SessionType == SessionType.Race)
                    {
                        pitData.NumPitStops++;
                    }
                    pitData.OnInLap = true;
                    pitData.OnOutLap = false;
                }
                else if(sData.IsNewLap)
                {
                    pitData.OnInLap = false;
                    pitData.OnOutLap = true;
                }
            }
            else if (previousGameState != null && previousGameState.PitData.InPitlane)
            {
                pitData.OnInLap = false;
                pitData.OnOutLap = true;
                pitData.IsAtPitExit = true;
            }
            else if (sData.IsNewLap)
            {
                // starting a new lap while not in the pitlane so clear the in / out lap flags
                pitData.OnInLap = false;
                pitData.OnOutLap = false;
            }

            pitData.limiterStatus = playerTelemetry.m_drivetrain.m_speedLimiterActive ? PitData.LimiterStatus.ACTIVE : PitData.LimiterStatus.INACTIVE;

            pitData.HasRequestedPitStop = false;
            pitData.PitStallOccupied = false;
            pitData.IsPitCrewReady = false;

            pitData.IsPitCrewDone = sData.SessionType == SessionType.Race &&
                !playerState.m_inPits &&
                previousGameState != null && previousGameState.PositionAndMotionData.CarSpeed < 1;
            if (pitData.IsPitCrewDone)
            {
                pitData.IsPitCrewReady = false;
            }

            // See if it looks like we're entering the pits.  Use TrackDefinition.pitApproachPoint if available.
            var pitApproachPoint = sData.TrackDefinition.pitApproachPoint;
            if (pitApproachPoint != null
                && pitData.HasRequestedPitStop
                && Math.Abs(pm.DistanceRoundTrack - pitApproachPoint[0]) < 30.0f)  // Within 30 meters of anchor pt by lapdist.
            {
                var distToPitApproachPt = Math.Sqrt(
                   (double)((pm.WorldPosition[0] - pitApproachPoint[1]) * (pm.WorldPosition[0] - pitApproachPoint[1])
                   + (pm.WorldPosition[2] - pitApproachPoint[2]) * (pm.WorldPosition[2] - pitApproachPoint[2])));

                pitData.IsApproachingPitlane = distToPitApproachPt < 4.0;  // Within 4 meters by world pos.
                Console.WriteLine($"Pit approach detection: approaching - {pitData.IsApproachingPitlane}    dist to point - {distToPitApproachPt.ToString("0.000")}");
            }

            if (sData.SessionType == SessionType.Race)
            {
                // PMR has no mandatory pitstops
                pitData.MandatoryPitStopCompleted = true;
                pitData.PitWindow = PitWindow.Completed;
            }

            cd.DamageEnabled = true;

            // Engine
            var eng = currentGameState.EngineData;
            eng.EngineOilPressure = playerTelemetry.m_drivetrain.m_engineOilPressure;
            eng.EngineOilTemp = playerTelemetry.m_drivetrain.m_engineOilTemperature;
            eng.EngineWaterTemp = playerTelemetry.m_drivetrain.m_engineCoolantTemperature; 
            eng.MaxEngineRpm = playerTelemetry.m_constant.m_engineMaxRPM;
            eng.MinutesIntoSessionBeforeMonitoring = 2;

            // Fuel
            var fuel = currentGameState.FuelData;
            fuel.FuelCapacity = playerTelemetry.m_constant.m_fuelCapacity;
            fuel.FuelLeft = playerTelemetry.m_drivetrain.m_fuelRemaining;
            fuel.FuelUseActive = true;
            // No fuel pressure provided

            pData.HasDriveThrough = false;
            pData.HasStopAndGo = false;

            pm.CarSpeed = playerTelemetry.m_chassis.m_overallSpeed;          // m/s

            //------------------------ Tyre data -----------------------
            var tyres = currentGameState.TyreData;
            tyres.HasMatchedTyreTypes = true;
            tyres.TyreWearActive = true;

            // only map to tyre type every sector or on pit exit
            TyreType tyreType;
            if (previousGameState == null || sData.IsNewSector || pitData.IsAtPitExit || sData.JustGoneGreen)
            {
                // PMR does not tell us tyre type
                tyreType = defaultTyreTypeForPlayersCar;
            }
            else
            {
                tyreType = previousGameState.TyreData.FrontLeftTyreType;
            }

            tyres.LeftFrontAttached = playerTelemetry.m_wheels[0].m_contactMaterialHash != 0;
            tyres.RightFrontAttached = playerTelemetry.m_wheels[1].m_contactMaterialHash != 0;
            tyres.LeftRearAttached = playerTelemetry.m_wheels[2].m_contactMaterialHash != 0;
            tyres.RightRearAttached = playerTelemetry.m_wheels[3].m_contactMaterialHash != 0;

            tyres.FrontLeft_CenterTemp = playerTelemetry.m_wheels[0].m_tread.y;
            tyres.FrontLeft_LeftTemp = playerTelemetry.m_wheels[0].m_tread.x;
            tyres.FrontLeft_RightTemp = playerTelemetry.m_wheels[0].m_tread.z;
            tyres.FrontLeftTyreType = tyreType;
            tyres.FrontLeftPressure = playerTelemetry.m_wheels[0].m_pressure;
            // No Tyre Wear data
            tyres.FrontLeftPercentWear = 0;
            if (sData.IsNewLap || tyres.PeakFrontLeftTemperatureForLap == 0)
            {
                tyres.PeakFrontLeftTemperatureForLap = playerTelemetry.m_wheels[0].m_carcass;
            }
            else if (previousGameState == null || playerTelemetry.m_wheels[0].m_carcass > previousGameState.TyreData.PeakFrontLeftTemperatureForLap)
            {
                tyres.PeakFrontLeftTemperatureForLap = playerTelemetry.m_wheels[0].m_carcass;
            }

            tyres.FrontRight_CenterTemp = playerTelemetry.m_wheels[1].m_tread.y;
            tyres.FrontRight_LeftTemp = playerTelemetry.m_wheels[1].m_tread.x;
            tyres.FrontRight_RightTemp = playerTelemetry.m_wheels[1].m_tread.z;
            tyres.FrontRightTyreType = tyreType;
            tyres.FrontRightPressure = playerTelemetry.m_wheels[1].m_pressure;
            // No Tyre Wear data
            tyres.FrontRightPercentWear = 0;
            if (sData.IsNewLap || tyres.PeakFrontRightTemperatureForLap == 0)
            {
                tyres.PeakFrontRightTemperatureForLap = playerTelemetry.m_wheels[1].m_carcass;
            }
            else if (previousGameState == null || playerTelemetry.m_wheels[1].m_carcass > previousGameState.TyreData.PeakFrontRightTemperatureForLap)
            {
                tyres.PeakFrontRightTemperatureForLap = playerTelemetry.m_wheels[1].m_carcass;
            }

            tyres.RearLeft_CenterTemp = playerTelemetry.m_wheels[2].m_tread.y;
            tyres.RearLeft_LeftTemp = playerTelemetry.m_wheels[2].m_tread.x;
            tyres.RearLeft_RightTemp = playerTelemetry.m_wheels[2].m_tread.z;
            tyres.RearLeftTyreType = tyreType;
            tyres.RearLeftPressure = playerTelemetry.m_wheels[2].m_pressure;
            // No Tyre Wear data
            tyres.RearLeftPercentWear = 0;
            if (sData.IsNewLap || tyres.PeakRearLeftTemperatureForLap == 0)
            {
                tyres.PeakRearLeftTemperatureForLap = playerTelemetry.m_wheels[2].m_carcass;
            }
            else if (previousGameState == null || playerTelemetry.m_wheels[2].m_carcass > previousGameState.TyreData.PeakRearLeftTemperatureForLap)
            {
                tyres.PeakRearLeftTemperatureForLap = playerTelemetry.m_wheels[2].m_carcass;
            }

            tyres.RearRight_CenterTemp = playerTelemetry.m_wheels[3].m_tread.y;
            tyres.RearRight_LeftTemp = playerTelemetry.m_wheels[3].m_tread.x;
            tyres.RearRight_RightTemp = playerTelemetry.m_wheels[3].m_tread.z;
            tyres.RearRightTyreType = tyreType;
            tyres.RearRightPressure = playerTelemetry.m_wheels[3].m_pressure;
            // No Tyre Wear data
            tyres.RearRightPercentWear = 0;
            if (sData.IsNewLap || tyres.PeakRearRightTemperatureForLap == 0)
            {
                tyres.PeakRearRightTemperatureForLap = playerTelemetry.m_wheels[3].m_carcass;
            }
            else if (previousGameState == null || playerTelemetry.m_wheels[3].m_carcass > previousGameState.TyreData.PeakRearRightTemperatureForLap)
            {
                tyres.PeakRearRightTemperatureForLap = playerTelemetry.m_wheels[3].m_carcass;
            }

            // Can't set tyre condition status because we get no tyre wear status

            var tyreTempThresholds = CarData.getTyreTempThresholds(currentGameState.carClass, tyreType);
            tyres.TyreTempStatus = CornerData.getCornerData(tyreTempThresholds,
                tyres.PeakFrontLeftTemperatureForLap, tyres.PeakFrontRightTemperatureForLap,
                tyres.PeakRearLeftTemperatureForLap, tyres.PeakRearRightTemperatureForLap);

            tyres.LeftFrontBrakeTemp = playerTelemetry.m_wheels[0].m_brake;
            tyres.RightFrontBrakeTemp = playerTelemetry.m_wheels[1].m_brake; 
            tyres.LeftRearBrakeTemp = playerTelemetry.m_wheels[2].m_brake; 
            tyres.RightRearBrakeTemp = playerTelemetry.m_wheels[3].m_brake; 
            tyres.BrakeTempStatus = CornerData.getCornerData(brakeTempThresholdsForPlayersCar,
                tyres.LeftFrontBrakeTemp, tyres.RightFrontBrakeTemp, tyres.LeftRearBrakeTemp, tyres.RightRearBrakeTemp);

            // Can't even improvise cut track warnings because we currently don't know enough about track surface.

            if (!pitData.OnOutLap && previousGameState != null && previousGameState.SessionData.CurrentLapIsValid && !sData.CurrentLapIsValid &&
                !(sData.SessionType == SessionType.Race && raceInfo.m_state == UDPRaceSessionState.Inactive))
            {
                pData.CutTrackWarnings = previousGameState.PenaltiesData.CutTrackWarnings + 1;
            }

            var carClass = currentGameState.carClass;
            var chassis = playerTelemetry.m_chassis;

            // Only check when we're actually moving and have a sensible car class
            if (chassis.m_overallSpeed > 7.0f &&
                carClass != null &&
                carClass.carClassEnum != CarData.CarClassEnum.KART_1 &&
                carClass.carClassEnum != CarData.CarClassEnum.KART_2 &&
                carClass.minTyreCircumference > 0.0f &&
                carClass.maxTyreCircumference > 0.0f)
            {
                float speed = chassis.m_overallSpeed; // m/s

                
                float minRotatingSpeed = (float)Math.PI * speed / carClass.maxTyreCircumference;
                float maxRotatingSpeed = 3f * (float)Math.PI * speed / carClass.minTyreCircumference;

                var wheels = playerTelemetry.m_wheels;

                // Lock-ups (very low angular speed for the car speed)
                tyres.LeftFrontIsLocked = Math.Abs(wheels[0].m_angVel) < minRotatingSpeed;
                tyres.RightFrontIsLocked = Math.Abs(wheels[1].m_angVel) < minRotatingSpeed;
                tyres.LeftRearIsLocked = Math.Abs(wheels[2].m_angVel) < minRotatingSpeed;
                tyres.RightRearIsLocked = Math.Abs(wheels[3].m_angVel) < minRotatingSpeed;

                // Wheelspin (angular speed way above what the car speed implies)
                tyres.LeftFrontIsSpinning = Math.Abs(wheels[0].m_angVel) > maxRotatingSpeed;
                tyres.RightFrontIsSpinning = Math.Abs(wheels[1].m_angVel) > maxRotatingSpeed;
                tyres.LeftRearIsSpinning = Math.Abs(wheels[2].m_angVel) > maxRotatingSpeed;
                tyres.RightRearIsSpinning = Math.Abs(wheels[3].m_angVel) > maxRotatingSpeed;
            }
            else
            {
                // Too slow or no valid class -> clear flags
                tyres.LeftFrontIsLocked = tyres.RightFrontIsLocked =
                    tyres.LeftRearIsLocked = tyres.RightRearIsLocked =
                    tyres.LeftFrontIsSpinning = tyres.RightFrontIsSpinning =
                    tyres.LeftRearIsSpinning = tyres.RightRearIsSpinning = false;
            }

            //PMR doesn't provide much in the way of conditions
            if (currentGameState.Now > nextConditionsSampleDue)
            {
                nextConditionsSampleDue = currentGameState.Now.Add(ConditionsMonitor.ConditionsSampleFrequency);
                con.addSample(currentGameState.Now, sData.CompletedLaps, sData.SectorNumber,
                    raceInfo.m_ambientTemperature, raceInfo.m_trackTemperature, isRaining ? 1 : 0, 0, 0, 0, 0,
                    sData.IsNewLap, ConditionsMonitor.TrackStatus.UNKNOWN);
            }

            currentGameState.RainDensity = isRaining ? 1 : 0;

            if (sData.TrackDefinition != null)
            {
                CrewChief.trackName = sData.TrackDefinition.name;
            }
            if (currentGameState.carClass != null)
            {
                CrewChief.carClass = currentGameState.carClass.carClassEnum;
            }
            CrewChief.distanceRoundTrack = pm.DistanceRoundTrack;
            CrewChief.viewingReplay = false;

            // Currently no way of setting possible track limits violations as we get no terrain material data

            eng.EngineRpm = playerTelemetry.m_drivetrain.m_engineRPM;
            if (eng.EngineRpm > 5)
            {
                lastTimeEngineWasRunning = currentGameState.Now;
            }
            if (!pitData.InPitlane &&
                previousGameState != null && !previousGameState.EngineData.EngineStalledWarning &&
                sData.SessionRunningTime > 60 && eng.EngineRpm < 5 &&
                lastTimeEngineWasRunning < currentGameState.Now.Subtract(TimeSpan.FromSeconds(2)))
            {
                eng.EngineStalledWarning = true;
                lastTimeEngineWasRunning = DateTime.MaxValue;
            }

            eng.Gear = playerTelemetry.m_input.m_gear;

            // Battery / hybrid
            var battery = currentGameState.BatteryData;
            battery.BatteryCapacity = playerTelemetry.m_constant.m_batteryCapacity;
            if (battery.BatteryCapacity > 0f)
            {
                battery.BatteryPercentageLeft =
                    (playerTelemetry.m_drivetrain.m_batteryRemaining / battery.BatteryCapacity) * 100f;
            }
            battery.BatteryUseActive = playerTelemetry.m_drivetrain.m_batteryUseRate > 0f;

            // Controls
            ctrl.BrakeBias = playerTelemetry.m_setup.m_brakeBias;
            ctrl.BrakePedal = playerTelemetry.m_input.m_brake;
            ctrl.ThrottlePedal = playerTelemetry.m_input.m_accelerator;
            ctrl.ClutchPedal = playerTelemetry.m_input.m_clutch;
            ctrl.HandBrake = playerTelemetry.m_input.m_handbrake;
            ctrl.SteeringWheelAngle = playerTelemetry.m_input.m_steering;

            if (sData.IsNewLap)
            {
                if (currentGameState.hardPartsOnTrackData.updateHardPartsForNewLap(sData.LapTimePrevious))
                {
                    sData.TrackDefinition.adjustGapPoints(currentGameState.hardPartsOnTrackData.processedHardPartsForBestLap);
                }
            }
            else if(!pitData.OnOutLap && !sData.TrackDefinition.isOval &&
                !(sData.SessionType == SessionType.Race && 
                (sData.CompletedLaps < 1 || (GameStateData.useManualFormationLap && sData.CompletedLaps < 2))))
            {
                currentGameState.hardPartsOnTrackData.mapHardPartsOnTrack(ctrl.BrakePedal, ctrl.ThrottlePedal,
                    pm.DistanceRoundTrack, sData.CurrentLapIsValid, sData.TrackDefinition.trackLength);
            }

            pm.WorldPosition = new float[]
            {
                playerTelemetry.m_chassis.m_posWS.x,
                playerTelemetry.m_chassis.m_posWS.y,
                playerTelemetry.m_chassis.m_posWS.z
            };

            setRotationFromQuaternion(pm, playerTelemetry);

            mapFrozenOrderData(currentGameState, previousGameState);

            return currentGameState;
        }

        private void mapFrozenOrderData(GameStateData currentGameState, GameStateData previousGameState)
        {
            if (previousGameState == null)
            {
                return;
            }

            FlagData cfd = currentGameState.FlagData;
            FlagData pfd = previousGameState.FlagData;
            FrozenOrderData cfod = currentGameState.FrozenOrderData;
            FrozenOrderData pfod = previousGameState.FrozenOrderData;

            cfd.fcyPhase = pfd.fcyPhase;
            cfd.lapCountWhenLastWentGreen = pfd.lapCountWhenLastWentGreen;
            cfd.isFullCourseYellow = pfd.isFullCourseYellow;

            cfod.Action = pfod.Action;
            cfod.AssignedColumn = pfod.AssignedColumn;
            cfod.AssignedGridPosition = pfod.AssignedGridPosition;
            cfod.AssignedPosition = pfod.AssignedPosition;
            cfod.CarNumberToFollowRaw = pfod.CarNumberToFollowRaw;
            cfod.DriverToFollowRaw = pfod.DriverToFollowRaw;

            if (currentGameState.SessionData.LapCount > previousGameState.SessionData.LapCount)
            {
                foreach (OpponentData opponentData in currentGameState.OpponentData.Values)
                {
                    cfod.OpponentPositionsAtStartOfFormationLap[opponentData.OverallPosition] = opponentData.DriverRawName;
                }
                cfd.previousLapWasFCY = pfd.currentLapIsFCY;
            }
            else
            {
                cfod.OpponentPositionsAtStartOfFormationLap = pfod.OpponentPositionsAtStartOfFormationLap;
                cfd.previousLapWasFCY = pfd.previousLapWasFCY;
            }

            cfod.Phase = pfod.Phase;

            // PMR does not have any full course yellows
            cfd.fcyPhase = FullCourseYellowPhase.RACING;
            cfd.isFullCourseYellow = false;
            cfod.Action = FrozenOrderAction.None;
            cfod.Phase = FrozenOrderPhase.None;
            if (pfd.fcyPhase != FullCourseYellowPhase.RACING)
            {
                // restarted
                cfd.lapCountWhenLastWentGreen = currentGameState.SessionData.LapCount;
            }

            // note that pitting (opponent or player) breaks the 'car to follow' - we no longer know who to follow, so clear it
            if (currentGameState.PitData.CarInFrontIsPitting || currentGameState.PitData.InPitlane)
            {
                cfod.CarNumberToFollowRaw = "";
                cfod.DriverToFollowRaw = "";
                cfod.AssignedPosition = -1;
            }
            if (cfd.fcyPhase != FullCourseYellowPhase.RACING)
            {
                cfd.useImprovisedIncidentCalling = false;
            }
            else
            {
                cfd.useImprovisedIncidentCalling = pfd.useImprovisedIncidentCalling;
            }
        }

        private void setRotationFromQuaternion(PositionAndMotionData pm, UDPVehicleTelemetry playerTelemetry)
        {
            var rot = pm.Orientation;
            UDPQuat q = playerTelemetry.m_chassis.m_quat;

            float qx = q.x;
            float qy = q.y;
            float qz = q.z;
            float qw = q.w;

            float sinr_cosp = 2f * (qw * qx + qy * qz);
            float cosr_cosp = 1f - 2f * (qx * qx + qy * qy);
            float roll = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            float sinp = 2f * (qw * qy - qz * qx);
            float pitch;
            if (Math.Abs(sinp) >= 1f)
            {
                pitch = (float)((sinp >= 0f ? 1f : -1f) * (Math.PI / 2.0));
            }
            else
            {
                pitch = (float)Math.Asin(sinp);
            }

            float siny_cosp = 2f * (qw * qz + qx * qy);
            float cosy_cosp = 1f - 2f * (qy * qy + qz * qz);
            float yaw = (float)Math.Atan2(siny_cosp, cosy_cosp);

            yaw -= 0.5f * (float)Math.PI; // -90 degrees

            float pi = (float)Math.PI;
            if (yaw > pi)
            {
                yaw -= 2f * pi;
            }
            else if (yaw < -pi)
            {
                yaw += 2f * pi;
            }

            // For now, don’t trust PMR pitch/roll in CC’s frame
            rot.Pitch = 0f;
            rot.Roll = 0f;
            rot.Yaw = yaw;
        }

        private OpponentData createOpponentData(UDPParticipantRaceState participantStruct, UDPVehicleTelemetry participantTelemetry , Boolean loadDriverName, CarData.CarClass carClass, Boolean canUseName, float trackLength)
        {
            OpponentData opponentData = new OpponentData();
            String participantName = participantStruct.m_driverName;
            opponentData.DriverRawName = participantName;
            opponentData.DriverNameSet = true;
            if (participantName != null && participantName.Length > 0 && loadDriverName && CrewChief.enableDriverNames)
            {
                if (speechRecogniser != null) speechRecogniser.addNewOpponentName(opponentData.DriverRawName, "-1");
                SoundCache.loadDriverNameSound(DriverNameHelper.getUsableDriverName(opponentData.DriverRawName));
            }
            opponentData.OverallPosition = (int)participantStruct.m_racePos;
            opponentData.CompletedLaps = (int)participantStruct.m_currentLap - 1;
            opponentData.CurrentSectorNumber = (int)participantStruct.m_currentSector + 1;   // zero indexed
            opponentData.WorldPosition = new float[] { participantTelemetry.m_chassis.m_posWS.x, participantTelemetry.m_chassis.m_posWS.z };
            opponentData.DistanceRoundTrack = participantStruct.m_lapProgress * trackLength;
            opponentData.DeltaTime = new DeltaTime(trackLength, opponentData.DistanceRoundTrack, opponentData.Speed, DateTime.UtcNow);
            opponentData.CarClass = carClass;
            opponentData.IsActive = true;
            String nameToLog = opponentData.DriverRawName == null ? "unknown" : opponentData.DriverRawName;
            Console.WriteLine("New driver " + nameToLog + " is using car class " + opponentData.CarClass.carClassEnum);
            opponentData.CanUseName = canUseName;

            return opponentData;
        }

        private UDPVehicleTelemetry GetVehicleTelemetryByID(List<UDPVehicleTelemetry> vehicleTelemetries, int vehicleID)
        {
            foreach (var vehicle in vehicleTelemetries)
            {
                if (vehicleID == vehicle.m_vehicleId)
                {
                    return vehicle;
                }
            }
            return null;
        }

        private FlagEnum mapToFlagEnum(uint highestFlagColour)
        {
            if (highestFlagColour == 1)
            {
                return FlagEnum.CHEQUERED;
            }
            else if (highestFlagColour == 4)
            {
                return FlagEnum.WHITE;
            }
            else if (highestFlagColour == 8)
            {
                return FlagEnum.BLUE;
            }
            else if (highestFlagColour == 0)
            {
                return FlagEnum.GREEN;
            }
            else if (highestFlagColour == 3)
            {
                return FlagEnum.YELLOW;
            }
            
            return FlagEnum.UNKNOWN;
        }

        private void updateOpponentData(OpponentData opponentData, int racePosition, int completedLaps, int sector, Boolean isInPits,
            float sessionRunningTime, float[] currentWorldPosition, float speed, float distanceRoundTrack,
            Boolean isRaining, float trackTemp, float airTemp, Boolean sessionLengthIsTime, float sessionTimeRemaining,
            float lastSectorTime, Boolean lapInvalidated, float nearPitEntryPointDistance, TimingData timingData,
            CarData.CarClass playerCarClass)
        {
            float previousDistanceRoundTrack = opponentData.DistanceRoundTrack;

            opponentData.DistanceRoundTrack = distanceRoundTrack;
            opponentData.Speed = speed;
            opponentData.OverallPosition = racePosition;
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
            if (opponentData.CurrentSectorNumber != sector)
            {
                if (opponentData.CurrentSectorNumber == 3 && sector == 1)
                {
                    if (opponentData.OpponentLapData.Count > 0)
                    {
                        if (lastSectorTime <= 0)
                        {
                            lastSectorTime = -1;
                            lapInvalidated = true;
                        }
                        opponentData.CompleteLapWithLastSectorTime(racePosition, lastSectorTime, sessionRunningTime,
                            !lapInvalidated, isRaining, trackTemp, airTemp, sessionLengthIsTime, sessionTimeRemaining, 3, timingData,
                            CarData.IsCarClassEqual(opponentData.CarClass, playerCarClass));
                    }
                    opponentData.StartNewLap(completedLaps + 1, racePosition, isInPits, sessionRunningTime, isRaining, trackTemp, airTemp);
                    opponentData.IsNewLap = true;
                }
                else if (opponentData.CurrentSectorNumber == 1 && sector == 2 || opponentData.CurrentSectorNumber == 2 && sector == 3)
                {
                    if (lastSectorTime <= 0)
                    {
                        lastSectorTime = -1;
                        lapInvalidated = true;
                    }
                    opponentData.AddSectorData(opponentData.CurrentSectorNumber, racePosition, lastSectorTime, sessionRunningTime, !lapInvalidated, isRaining, trackTemp, airTemp);
                }
                opponentData.CurrentSectorNumber = sector;
            }
            if (sector == 3 && isInPits)
            {
                opponentData.setInLap();
            }
            opponentData.CompletedLaps = completedLaps;
        }
    }
}
