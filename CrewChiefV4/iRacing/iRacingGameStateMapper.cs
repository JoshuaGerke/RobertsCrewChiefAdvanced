using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrewChiefV4.GameState;
using CrewChiefV4.Events;
using System.Diagnostics;
using CrewChiefV4.Overlay;
using CrewChiefV4.Audio;

namespace CrewChiefV4.iRacing
{
    class iRacingGameStateMapper : GameStateMapper
    {
        public static String playerName = null;

        // these are very car and tyre compound specific, and the data is not available, so we have very conservative thresholds
        static public List<CornerData.EnumWithThresholds> tyreWearThresholds = new List<CornerData.EnumWithThresholds>();
        static iRacingGameStateMapper() {
            int scrubbed = 10;
            int minor = 30;
            int major = 60;
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.NEW, 0, 1));
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.SCRUBBED, 1, scrubbed));
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.MINOR_WEAR, scrubbed, minor));
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.MAJOR_WEAR, minor, major));
            tyreWearThresholds.Add(new CornerData.EnumWithThresholds(TyreCondition.WORN_OUT, major, 101));
        }

        public Driver playerCar = null;
        public Driver leaderCar = null;

        public iRacingGameStateMapper()
        {

        }

        public override void versionCheck(Object memoryMappedFileStruct)
        {
            // no version number in iRacing shared data so this is a no-op
        }

        Dictionary<string, DateTime> lastActiveTimeForOpponents = new Dictionary<string, DateTime>();
        DateTime nextOpponentCleanupTime = DateTime.MinValue;
        TimeSpan opponentCleanupInterval = TimeSpan.FromSeconds(3);

        // next track conditions sample due after:
        private DateTime nextConditionsSampleDue = DateTime.MinValue;
        private DateTime lastTimeEngineWasRunning = DateTime.MaxValue;
        private DateTime lastTimeEngineWaterTempWarning = DateTime.MaxValue;
        private DateTime lastTimeEngineOilPressureWarning = DateTime.MaxValue;
        private DateTime lastTimeEngineFuelPressureWarning = DateTime.MaxValue;

        private readonly bool enableFCYPitStateMessages = UserSettings.GetUserSettings().getBoolean("enable_iracing_pit_state_during_fcy");
        private readonly Boolean invalidateCutTrackLaps = UserSettings.GetUserSettings().getBoolean("iracing_invalidate_cut_track_laps");
        private readonly Boolean iracing_overrides_units = UserSettings.GetUserSettings().getBoolean("iracing_overrides_units");
        private DateTime lastCutTrackTime = DateTime.MaxValue;
        TimeSpan cutTrackValidaitonTime = TimeSpan.FromSeconds(3);


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

        TrackSurfaces prevTrackSurface;
        public override GameStateData mapToGameStateData(Object memoryMappedFileStruct, GameStateData previousGameState)
        {
            if (memoryMappedFileStruct == null)
            {
                return null;
            } 
            CrewChiefV4.iRacing.iRacingSharedMemoryReader.iRacingStructWrapper wrapper = (CrewChiefV4.iRacing.iRacingSharedMemoryReader.iRacingStructWrapper)memoryMappedFileStruct;
            GameStateData currentGameState = new GameStateData(wrapper.ticksWhenRead);
            currentGameState.rawGameData = wrapper;
            Sim shared = wrapper.data;

            if (shared.Telemetry.IsReplayPlaying)
            {
                CrewChief.trackName = shared.SessionData.Track.CodeName;
                CrewChief.carClass = CarData.getCarClassForIRacingId(shared.Driver.Car.CarClassId, shared.Driver.Car.CarId).carClassEnum;
                CrewChief.viewingReplay = true;
                CrewChief.distanceRoundTrack = shared.Driver.Live.CorrectedLapDistance * ((float)shared.SessionData.Track.Length * 1000);
                //return previousGameState;
            }
            updateOvertakingAids(currentGameState, shared.Telemetry.DRS_Status);

            SessionPhase lastSessionPhase = SessionPhase.Unavailable;
            SessionType lastSessionType = SessionType.Unavailable;
            int? previousSessionNumber = -1;
            int previousSessionId = -1;
            float lastSessionRunningTime = 0;
            float previousPitEntranceDistanceRoundTrack = -1.0f;
            if (previousGameState != null)
            {
                lastSessionPhase = previousGameState.SessionData.SessionPhase;
                lastSessionRunningTime = previousGameState.SessionData.SessionRunningTime;
                lastSessionType = previousGameState.SessionData.SessionType;
                currentGameState.readLandmarksForThisLap = previousGameState.readLandmarksForThisLap;
                previousSessionNumber = previousGameState.SessionData.SessionIteration;
                previousSessionId = previousGameState.SessionData.SessionId;
                previousPitEntranceDistanceRoundTrack = previousGameState.SessionData.TrackDefinition.iracingPitEntranceDistanceRoundTrack;
            }
            currentGameState.SessionData.SessionType = mapToSessionType(shared.SessionData.SessionType);
            currentGameState.SessionData.iRacingStandingStart = shared.SessionData.StandingStart;
            if (currentGameState.SessionData.SessionType == SessionType.PrivateQualify)
            {
                // see comments on iRacingLoneQualify to explain this hack
                currentGameState.SessionData.SessionType = SessionType.Qualify;
                currentGameState.SessionData.iRacingLoneQualify = true;
            }
            currentGameState.SessionData.SessionRunningTime = (float)shared.Telemetry.SessionTime;
            currentGameState.SessionData.SessionTimeRemaining = (float)shared.Telemetry.SessionTimeRemain;

            int previousLapsCompleted = previousGameState == null ? 0 : previousGameState.SessionData.CompletedLaps;
            int PlayerCarIdx = shared.Telemetry.PlayerCarIdx;

            if (shared.Driver != null)
            {
                playerCar = shared.Driver;
                playerName = playerCar.Name;
            }
            // only used for auto fuelling check
            currentGameState.ControlData.ControlType = shared.Telemetry.IsReplayPlaying ? ControlType.Replay : ControlType.Player;
            leaderCar = shared.Drivers.Where(d => d.CurrentResults.Position > 0).OrderBy(d => d.CurrentResults.Position).FirstOrDefault();

            if(leaderCar == null)
            {
                leaderCar = playerCar;
            }
            AdditionalDataProvider.validate(playerName);

            currentGameState.SafetyCarData = GetSafetyCarData(previousGameState == null ? null : previousGameState.SafetyCarData, shared.PaceCar, 
                (float)(shared.SessionData.Track.Length * 1000), shared.PaceCarPresent);
           
            currentGameState.SessionData.SessionPhase = mapToSessionPhase(lastSessionPhase, shared.Telemetry.SessionState, currentGameState.SessionData.SessionType, 
                (float)shared.Telemetry.SessionTime, previousLapsCompleted, playerCar.Live.LiveLapsCompleted, (SessionFlags)shared.Telemetry.SessionFlags, 
                shared.SessionData.hasFullCourseCautions,currentGameState.SafetyCarData, previousGameState == null ? null : previousGameState.SafetyCarData, 
                shared.SessionData.Track.FormationLapCount, previousPitEntranceDistanceRoundTrack);
           
            if (iracing_overrides_units)
            {
                // iRacing only allows metric or American units (which they call "English" units, ha!), no mixing of celcius and miles.
                GlobalBehaviourSettings.useMetric = shared.Telemetry.DisplayUnits == DisplayUnits.Metric;
                GlobalBehaviourSettings.useFahrenheit = !GlobalBehaviourSettings.useMetric;
                GlobalBehaviourSettings.useAmericanTerms = GlobalBehaviourSettings.useAmericanTerms || !GlobalBehaviourSettings.useMetric;
            }

            currentGameState.SessionData.NumCarsOverallAtStartOfSession = shared.PaceCarPresent ? shared.Drivers.Count - 1 : shared.Drivers.Count;
            
            int sessionNumber = shared.Telemetry.SessionNum;

            currentGameState.SessionData.SessionIteration = sessionNumber;
            currentGameState.SessionData.SessionId = shared.SessionData.SessionId;

            if (currentGameState.SessionData.SessionType != SessionType.Unavailable && shared.Telemetry.IsNewSession)
            {
                CarData.clearCachedIRacingClassData();
                currentGameState.SessionData.IsNewSession = true;
                Console.WriteLine("New session, trigger data:");
                Console.WriteLine("SessionType = " + currentGameState.SessionData.SessionType);
                Console.WriteLine("lastSessionPhase = " + lastSessionPhase);
                Console.WriteLine("lastSessionRunningTime = " + lastSessionRunningTime);
                Console.WriteLine("currentSessionPhase = " + currentGameState.SessionData.SessionPhase);
                Console.WriteLine("rawSessionPhase = " + shared.Telemetry.SessionState);
                Console.WriteLine("currentSessionRunningTime = " + currentGameState.SessionData.SessionRunningTime);
                Console.WriteLine("NumCarsAtStartOfSession = " + currentGameState.SessionData.NumCarsOverallAtStartOfSession);
                Console.WriteLine("Category = " + shared.SessionData.Category);
                Console.WriteLine("TrackType = " + shared.SessionData.Track.TrackType);
                Console.WriteLine("TrackPitSpeedLimit = " + shared.SessionData.Track.TrackPitSpeedLimit);
                Console.WriteLine("CourseCautions = " + shared.SessionData.CourseCautions);
                Console.WriteLine("Restarts = " + shared.SessionData.Restarts);
                Console.WriteLine("StartingGrid = " + shared.SessionData.StartingGrid);
                Console.WriteLine("Start on Left = " + shared.SessionData.StartOnLeft);
                Console.WriteLine("Restart on Left = " + shared.SessionData.RestartOnLeft);
                Console.WriteLine("TrackCodeName " + shared.SessionData.Track.CodeName + " Track Reported Length " + (float)shared.SessionData.Track.Length * 1000);

                currentGameState.SessionData.DriverRawName = playerName;
                currentGameState.OpponentData.Clear();
                currentGameState.SessionData.PlayerLapData.Clear();
                currentGameState.SessionData.SessionStartTime = currentGameState.Now;
                
                currentGameState.SessionData.TrackDefinition = TrackData.getTrackDefinition(shared.SessionData.Track.CodeName, (float)shared.SessionData.Track.Length * 1000);
                currentGameState.SessionData.TrackDefinition.iracingTrackNorthOffset = shared.SessionData.Track.TrackNorthOffset;
                if (previousGameState != null && previousGameState.SessionData.TrackDefinition != null)
                {
                    if (previousGameState.SessionData.TrackDefinition.name.Equals(currentGameState.SessionData.TrackDefinition.name))
                    {
                        if (previousGameState.hardPartsOnTrackData.hardPartsMapped)
                        {
                            currentGameState.hardPartsOnTrackData.processedHardPartsForBestLap = previousGameState.hardPartsOnTrackData.processedHardPartsForBestLap;
                            currentGameState.hardPartsOnTrackData.isAlreadyBraking = previousGameState.hardPartsOnTrackData.isAlreadyBraking;
                            currentGameState.hardPartsOnTrackData.hardPartStart = previousGameState.hardPartsOnTrackData.hardPartStart;
                            currentGameState.hardPartsOnTrackData.hardPartsMapped = previousGameState.hardPartsOnTrackData.hardPartsMapped;
                        }
                    }
                }
                TrackDataContainer tdc = TrackData.TRACK_LANDMARKS_DATA.getTrackDataForTrackName(shared.SessionData.Track.CodeName, currentGameState.SessionData.TrackDefinition.trackLength);
                currentGameState.SessionData.TrackDefinition.trackLandmarks = tdc.trackLandmarks;
                if (tdc.isDefinedInTracklandmarksData)
                {
                    currentGameState.SessionData.TrackDefinition.iracingPitEntranceDistanceRoundTrack = tdc.iracingPitEntranceDistanceRoundTrack;
                }
                currentGameState.SessionData.TrackDefinition.isOval = shared.SessionData.Track.IsOval;
                currentGameState.SessionData.TrackDefinition.setGapPoints();
                GlobalBehaviourSettings.UpdateFromTrackDefinition(currentGameState.SessionData.TrackDefinition);


                currentGameState.SessionData.LeaderHasFinishedRace = false;
                currentGameState.PitData.IsRefuellingAllowed = true;
                currentGameState.CarDamageData.DamageEnabled = true;
                currentGameState.SessionData.SessionTimeRemaining = (float)shared.Telemetry.SessionTimeRemain;

                if (!shared.SessionData.IsLimitedSessionLaps)
                {
                    currentGameState.SessionData.SessionHasFixedTime = true;
                    currentGameState.SessionData.SessionTotalRunTime = (float)shared.SessionData.RaceTime;
                    Console.WriteLine("Treating this as a time limited race");
                }
                else
                {
                    currentGameState.SessionData.SessionHasFixedTime = false;
                    currentGameState.SessionData.SessionNumberOfLaps = Parser.ParseInt(shared.SessionData.RaceLaps);
                    Console.WriteLine("Treating this as a lap limited race");
                }
                

                currentGameState.SessionData.MaxIncidentCount = shared.SessionData.IncidentLimit;
                currentGameState.SessionData.CurrentIncidentCount = shared.Telemetry.PlayerCarMyIncidentCount;
                currentGameState.SessionData.CurrentDriverIncidentCount = shared.Telemetry.PlayerCarDriverIncidentCount;
                currentGameState.SessionData.CurrentTeamIncidentCount = shared.Telemetry.PlayerCarTeamIncidentCount;

                lastActiveTimeForOpponents.Clear();
                nextOpponentCleanupTime = currentGameState.Now + opponentCleanupInterval;

                currentGameState.PitData.InPitlane = shared.Telemetry.OnPitRoad;
                currentGameState.PositionAndMotionData.DistanceRoundTrack = Math.Abs(playerCar.Live.CorrectedLapDistance * currentGameState.SessionData.TrackDefinition.trackLength);

                currentGameState.carClass = CarData.getCarClassForIRacingId(playerCar.Car.CarClassId, playerCar.Car.CarId);
                if (currentGameState.SessionData.TrackDefinition.isOval)
                {
                    currentGameState.carClass.limiterAvailable = false;
                }
                CarData.IRACING_CLASS_ID = playerCar.Car.CarClassId;
                GlobalBehaviourSettings.UpdateFromCarClass(currentGameState.carClass, playerCar.Car.CarIsElectric);

                Console.WriteLine("Player is using car class " + currentGameState.carClass.getClassIdentifier() + " (car ID " + playerCar.Car.CarId + ")" + "Is Electric Vehicle = " + playerCar.Car.CarIsElectric);
                currentGameState.SessionData.PlayerCarNr = playerCar.CarNumber;
                
                currentGameState.SessionData.DeltaTime = new DeltaTime(currentGameState.SessionData.TrackDefinition.trackLength, 
                    currentGameState.PositionAndMotionData.DistanceRoundTrack, currentGameState.PositionAndMotionData.CarSpeed, currentGameState.Now);
                currentGameState.SessionData.SectorNumber = playerCar.Live.CurrentSector;
                foreach (Driver driver in shared.Drivers)
                {
                    if (driver.IsCurrentDriver || driver.CurrentResults.IsOut || driver.IsPaceCar || driver.IsSpectator)
                    {
                        continue;
                    }
                    else
                    {
                        currentGameState.OpponentData.Add(driver.Id.ToString(), createOpponentData(driver, false,
                            currentGameState.SessionData.TrackDefinition.trackLength));
                    }
                }
                // add a conditions sample when we first start a session so we're not using stale or default data in the pre-lights phase
                currentGameState.Conditions.addSample(currentGameState.Now, 0, 1, shared.Telemetry.AirTemp, shared.Telemetry.TrackTempCrew,
                    shared.Telemetry.Precipitation, shared.Telemetry.WindVel, 0, 0, 0, true, ConditionsMonitor.TrackStatus.UNKNOWN, TrackWetness: (object)shared.Telemetry.TrackWetness != null ? (ConditionsMonitor.TrackWetness)shared.Telemetry.TrackWetness : ConditionsMonitor.TrackWetness.UNKNOWN);

                //need to call this after adding opponents else we have nothing to compare against 
                GameStateData.Multiclass = shared.SessionData.NumCarClasses > 1;
                GameStateData.NumberOfClasses = shared.SessionData.NumCarClasses;
                //Utilities.TraceEventClass(currentGameState);
            }
            else
            {
                if (lastSessionPhase != currentGameState.SessionData.SessionPhase)
                {
                    Console.WriteLine("New session phase, was " + lastSessionPhase + " now " + currentGameState.SessionData.SessionPhase);
                    if (previousGameState != null && previousGameState.SessionData.TrackDefinition == null)
                    {
                        Console.WriteLine("New session phase without new session initialized previously.");
                    }

                    if (currentGameState.SessionData.SessionPhase == SessionPhase.Green && lastSessionPhase != SessionPhase.Finished && lastSessionPhase != SessionPhase.FullCourseYellow)
                    {
                        currentGameState.SessionData.JustGoneGreen = true;
                        currentGameState.SessionData.LeaderHasFinishedRace = false;
                        // just gone green, so get the session data

                        if (!shared.SessionData.IsLimitedSessionLaps)
                        {
                            currentGameState.SessionData.SessionHasFixedTime = true;
                            currentGameState.SessionData.SessionTotalRunTime = (float)shared.SessionData.RaceTime;
                            Console.WriteLine("Treating this as a time limited race");
                        }
                        else
                        {
                            currentGameState.SessionData.SessionHasFixedTime = false;
                            currentGameState.SessionData.SessionNumberOfLaps = Parser.ParseInt(shared.SessionData.RaceLaps);
                            Console.WriteLine("Treating this as a lap limited race");
                        }
                                                                    
                        if (previousGameState != null)
                        {
                            currentGameState.PitData.IsRefuellingAllowed = previousGameState.PitData.IsRefuellingAllowed;
                            currentGameState.OpponentData = previousGameState.OpponentData;
                            currentGameState.SessionData.TrackDefinition = previousGameState.SessionData.TrackDefinition;
                            currentGameState.SessionData.DriverRawName = previousGameState.SessionData.DriverRawName;
                            currentGameState.PitData.OnInLap = previousGameState.PitData.OnInLap;
                            currentGameState.PitData.OnOutLap = previousGameState.PitData.OnOutLap;
                            currentGameState.Conditions.CurrentConditions = previousGameState.Conditions.CurrentConditions;
                            currentGameState.Conditions.samples = previousGameState.Conditions.samples;
                            currentGameState.carClass = previousGameState.carClass;
                            currentGameState.SessionData.DeltaTime = previousGameState.SessionData.DeltaTime;
                            currentGameState.SessionData.PlayerCarNr = previousGameState.SessionData.PlayerCarNr;
                            currentGameState.SessionData.SectorNumber = previousGameState.SessionData.SectorNumber;
                            currentGameState.SessionData.IsStartLapInserted = previousGameState.SessionData.IsStartLapInserted;
                            if (previousGameState.SessionData.TrackDefinition != null && previousGameState.SessionData.TrackDefinition.name.Equals(currentGameState.SessionData.TrackDefinition.name))
                            {
                                if (previousGameState.hardPartsOnTrackData.hardPartsMapped)
                                {
                                    currentGameState.hardPartsOnTrackData.processedHardPartsForBestLap = previousGameState.hardPartsOnTrackData.processedHardPartsForBestLap;
                                    currentGameState.hardPartsOnTrackData.isAlreadyBraking = previousGameState.hardPartsOnTrackData.isAlreadyBraking;
                                    currentGameState.hardPartsOnTrackData.hardPartStart = previousGameState.hardPartsOnTrackData.hardPartStart;
                                    currentGameState.hardPartsOnTrackData.hardPartsMapped = previousGameState.hardPartsOnTrackData.hardPartsMapped;
                                }
                            }
                        }
                        currentGameState.SessionData.SessionStartTime = currentGameState.Now;


                        lastActiveTimeForOpponents.Clear();
                        nextOpponentCleanupTime = currentGameState.Now + opponentCleanupInterval;
                        lastTimeEngineWasRunning = DateTime.MaxValue;
                        lastTimeEngineWaterTempWarning = DateTime.MaxValue;
                        lastTimeEngineOilPressureWarning = DateTime.MaxValue;
                        lastTimeEngineFuelPressureWarning = DateTime.MaxValue;
                        //currentGameState.SessionData.CompletedLaps = shared.Driver.CurrentResults.LapsComplete;

                        Console.WriteLine("Just gone green, session details...");

                        Console.WriteLine("SessionType " + currentGameState.SessionData.SessionType);
                        Console.WriteLine("SessionPhase " + currentGameState.SessionData.SessionPhase);
                        Console.WriteLine("HasMandatoryPitStop " + currentGameState.PitData.HasMandatoryPitStop);
                        Console.WriteLine("NumCarsAtStartOfSession " + currentGameState.SessionData.NumCarsOverallAtStartOfSession);
                        Console.WriteLine("SessionNumberOfLaps " + currentGameState.SessionData.SessionNumberOfLaps);
                        Console.WriteLine("SessionRunTime " + currentGameState.SessionData.SessionTotalRunTime);
                        Console.WriteLine("SessionStartTime " + currentGameState.SessionData.SessionStartTime);
                        String trackName = currentGameState.SessionData.TrackDefinition == null ? "unknown" : currentGameState.SessionData.TrackDefinition.name;
                        Console.WriteLine("TrackName " + trackName + " Track Reported Length " + currentGameState.SessionData.TrackDefinition.trackLength);
                    }
                }
                if (!currentGameState.SessionData.JustGoneGreen && previousGameState != null)
                {
                    //Console.WriteLine("regular update, session type = " + currentGameState.SessionData.SessionType + " phase = " + currentGameState.SessionData.SessionPhase);

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
                    currentGameState.SessionData.LeaderHasFinishedRace = previousGameState.SessionData.LeaderHasFinishedRace;

                    currentGameState.PitData.PitWindowStart = previousGameState.PitData.PitWindowStart;
                    currentGameState.PitData.PitWindowEnd = previousGameState.PitData.PitWindowEnd;
                    currentGameState.PitData.HasMandatoryPitStop = previousGameState.PitData.HasMandatoryPitStop;
                    currentGameState.PitData.HasMandatoryTyreChange = previousGameState.PitData.HasMandatoryTyreChange;
                    currentGameState.PitData.MandatoryTyreChangeRequiredTyreType = previousGameState.PitData.MandatoryTyreChangeRequiredTyreType;
                    currentGameState.PitData.IsRefuellingAllowed = previousGameState.PitData.IsRefuellingAllowed;
                    currentGameState.CarDamageData.DamageEnabled = previousGameState.CarDamageData.DamageEnabled;
                    currentGameState.CarDamageData.OverallGeneralDamage = previousGameState.CarDamageData.OverallGeneralDamage;
                    currentGameState.CarDamageData.LastImpactTime = previousGameState.CarDamageData.LastImpactTime;
                    currentGameState.PitData.MaxPermittedDistanceOnCurrentTyre = previousGameState.PitData.MaxPermittedDistanceOnCurrentTyre;
                    currentGameState.PitData.MinPermittedDistanceOnCurrentTyre = previousGameState.PitData.MinPermittedDistanceOnCurrentTyre;
                    currentGameState.PitData.OnInLap = previousGameState.PitData.OnInLap;
                    currentGameState.PitData.OnOutLap = previousGameState.PitData.OnOutLap;
                    currentGameState.PitData.NumPitStops = previousGameState.PitData.NumPitStops;
                    currentGameState.PitData.PitBoxPositionEstimate = previousGameState.PitData.PitBoxPositionEstimate;
                    currentGameState.PitData.IsTeamRacing = previousGameState.PitData.IsTeamRacing;

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
                    currentGameState.SessionData.PlayerLapData = previousGameState.SessionData.PlayerLapData;
                    currentGameState.SessionData.trackLandmarksTiming = previousGameState.SessionData.trackLandmarksTiming;
                    currentGameState.SessionData.CompletedLaps = previousGameState.SessionData.CompletedLaps;
                    currentGameState.SessionData.LapCount = previousGameState.SessionData.LapCount;
                    currentGameState.SessionData.PlayerCarNr = previousGameState.SessionData.PlayerCarNr;
                    currentGameState.SessionData.IsLastLap = previousGameState.SessionData.IsLastLap;

                    currentGameState.OpponentData = previousGameState.OpponentData;
                    currentGameState.SessionData.SectorNumber = previousGameState.SessionData.SectorNumber;
                    currentGameState.SessionData.DeltaTime = previousGameState.SessionData.DeltaTime;
                    currentGameState.SessionData.MaxIncidentCount = previousGameState.SessionData.MaxIncidentCount;
                    currentGameState.SessionData.CurrentIncidentCount = previousGameState.SessionData.CurrentIncidentCount;
                    currentGameState.SessionData.CurrentDriverIncidentCount = previousGameState.SessionData.CurrentDriverIncidentCount;
                    currentGameState.SessionData.CurrentTeamIncidentCount = previousGameState.SessionData.CurrentTeamIncidentCount;
                    currentGameState.SessionData.HasLimitedIncidents = previousGameState.SessionData.HasLimitedIncidents;
                    currentGameState.SessionData.StrengthOfField = previousGameState.SessionData.StrengthOfField;
                    currentGameState.SessionData.expectedFinishingPosition = previousGameState.SessionData.expectedFinishingPosition;

                    currentGameState.Conditions.samples = previousGameState.Conditions.samples;
                    currentGameState.Conditions.CurrentConditions = previousGameState.Conditions.CurrentConditions;
                    currentGameState.PenaltiesData.CutTrackWarnings = previousGameState.PenaltiesData.CutTrackWarnings;
                    currentGameState.retriedDriverNames = previousGameState.retriedDriverNames;
                    currentGameState.disqualifiedDriverNames = previousGameState.disqualifiedDriverNames;
                    currentGameState.hardPartsOnTrackData = previousGameState.hardPartsOnTrackData;
                    currentGameState.TimingData = previousGameState.TimingData;
                    //currentGameState.FrozenOrderData = previousGameState.FrozenOrderData;
                    currentGameState.SessionData.JustGoneGreenTime = previousGameState.SessionData.JustGoneGreenTime;
                    currentGameState.FlagData.previousLapWasFCY = previousGameState.FlagData.previousLapWasFCY;
                    currentGameState.FlagData.currentLapIsFCY = previousGameState.FlagData.currentLapIsFCY;
                    currentGameState.FlagData.fcyPhase = previousGameState.FlagData.fcyPhase;
                    currentGameState.FlagData.lapCountWhenLastWentGreen = previousGameState.FlagData.lapCountWhenLastWentGreen;
                    currentGameState.SessionData.IsStartLapInserted = previousGameState.SessionData.IsStartLapInserted;
                }
            }

            currentGameState.ControlData.ThrottlePedal = shared.Telemetry.Throttle;
            currentGameState.ControlData.ClutchPedal = shared.Telemetry.Clutch;
            currentGameState.ControlData.BrakePedal = shared.Telemetry.Brake;
            currentGameState.ControlData.HandBrake = shared.Telemetry.HandBrake;
            currentGameState.ControlData.SteeringWheelAngle = shared.Telemetry.SteeringWheelAngle;
            currentGameState.ControlData.BrakeBias = shared.Telemetry.dcBrakeBias;
            currentGameState.TransmissionData.Gear = shared.Telemetry.Gear;

            if (shared.Telemetry.EngineWarnings != null) {
                if (shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.MandRepNeeded))
                {
                    if (currentGameState.CarDamageData.OverallGeneralDamage < DamageLevel.MAJOR)
                    {
                        currentGameState.CarDamageData.LastImpactTime = currentGameState.SessionData.SessionRunningTime;
                    }
                    currentGameState.CarDamageData.OverallGeneralDamage = DamageLevel.MAJOR;
                }
                else if (shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.OptRepNeeded))
                {
                    if (currentGameState.CarDamageData.OverallGeneralDamage < DamageLevel.MINOR)
                    {
                        currentGameState.CarDamageData.LastImpactTime = currentGameState.SessionData.SessionRunningTime;
                    }
                    currentGameState.CarDamageData.OverallGeneralDamage = DamageLevel.MINOR;
                }
                else
                {
                    currentGameState.CarDamageData.OverallGeneralDamage = DamageLevel.NONE;
                }
            }

            currentGameState.SessionData.LicenseLevel = playerCar.licensLevel;
            currentGameState.SessionData.iRating = playerCar.IRating;
            currentGameState.SessionData.EstimatedLapTime = playerCar.EstimatedLapTime;

            currentGameState.EngineData.EngineOilTemp = shared.Telemetry.OilTemp;
            currentGameState.EngineData.EngineWaterTemp = shared.Telemetry.WaterTemp;

            currentGameState.EngineData.MinutesIntoSessionBeforeMonitoring = 0;

            bool additionalEngineCheckFlags = shared.Telemetry.IsOnTrack && !shared.Telemetry.OnPitRoad && shared.Telemetry.Voltage > 0f;

            if (!shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.EngineStalled))
            {
                lastTimeEngineWasRunning = currentGameState.Now;
            }
            if (previousGameState != null && !previousGameState.EngineData.EngineStalledWarning &&
                currentGameState.SessionData.SessionRunningTime > 60 && shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.EngineStalled) &&
                lastTimeEngineWasRunning < currentGameState.Now.Subtract(TimeSpan.FromSeconds(2)) && additionalEngineCheckFlags)
            {
                currentGameState.EngineData.EngineStalledWarning = true;
                lastTimeEngineWasRunning = DateTime.MaxValue;

            }

            if (!shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.WaterTemperatureWarning))
            {
                lastTimeEngineWaterTempWarning = currentGameState.Now;
            }

            if (previousGameState != null && !previousGameState.EngineData.EngineWaterTempWarning && !shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.EngineStalled) &&
                currentGameState.SessionData.SessionRunningTime > 60 && shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.WaterTemperatureWarning) &&
                lastTimeEngineWaterTempWarning < currentGameState.Now.Subtract(TimeSpan.FromSeconds(2)) && additionalEngineCheckFlags)
            {
                currentGameState.EngineData.EngineWaterTempWarning = true;
                lastTimeEngineWaterTempWarning = DateTime.MaxValue;
            }

            if (!shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.OilPressureWarning))
            {
                lastTimeEngineOilPressureWarning = currentGameState.Now;
            }
            if (previousGameState != null && !previousGameState.EngineData.EngineWaterTempWarning && !shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.EngineStalled) &&
                currentGameState.SessionData.SessionRunningTime > 60 && shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.OilPressureWarning) &&
                lastTimeEngineOilPressureWarning < currentGameState.Now.Subtract(TimeSpan.FromSeconds(2)) && additionalEngineCheckFlags)
            {
                currentGameState.EngineData.EngineWaterTempWarning = true;
                lastTimeEngineOilPressureWarning = DateTime.MaxValue;
            }
            if (!shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.FuelPressureWarning))
            {
                lastTimeEngineFuelPressureWarning = currentGameState.Now;
            }
            if (previousGameState != null && !previousGameState.EngineData.EngineWaterTempWarning && !shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.EngineStalled) &&
                currentGameState.SessionData.SessionRunningTime > 60 && shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.FuelPressureWarning) &&
                lastTimeEngineFuelPressureWarning < currentGameState.Now.Subtract(TimeSpan.FromSeconds(2)) && additionalEngineCheckFlags)
            {
                currentGameState.EngineData.EngineWaterTempWarning = true;
                lastTimeEngineFuelPressureWarning = DateTime.MaxValue;
            }

            currentGameState.SessionData.CompletedLaps = playerCar.Live.LiveLapsCompleted >= 0 ? playerCar.Live.LiveLapsCompleted: 0;
            currentGameState.SessionData.LapCount = currentGameState.SessionData.CompletedLaps + 1;
            currentGameState.SessionData.LapTimeCurrent = shared.Telemetry.LapCurrentLapTime;
            currentGameState.FlagData.isFullCourseYellow = currentGameState.SessionData.SessionPhase == SessionPhase.FullCourseYellow;
            currentGameState.PitData.InPitlane = (shared.Telemetry.CarIdxOnPitRoad[PlayerCarIdx] || playerCar.Live.TrackSurface == TrackSurfaces.InPitStall) && currentGameState.SessionData.SessionPhase != SessionPhase.Formation;

            SessionFlags flags = (SessionFlags)shared.Telemetry.SessionFlags;

            if (currentGameState.SessionData.TrackDefinition != null && currentGameState.SessionData.TrackDefinition.isOval)
            {
                currentGameState.StockCarRulesData.stockCarRulesEnabled = true;

                if (currentGameState.FlagData.isFullCourseYellow && shared.Telemetry.CarIdxPaceFlags != null)
                {
                    // in real life series the lucky dog is asked to come to the pole lane when the pits are open, and then
                    // asked to pass when crossing the line (one to go). https://en.wikipedia.org/wiki/Beneficiary_rule
                    //
                    // In iRacing this tends to be implemented in a single step of passing the entire pack and pace car when
                    // there is one to go.
                    //
                    // But the iRacing paceflags telemetry appears before anything can be actioned, and doesn't disappear after,
                    // so there is no way to get the timing right here https://forums.iracing.com/discussion/60597
                    // and it's basically wrong information as far as we can tell.
                    for (int j = 0; j < shared.Drivers.Count; j++)
                    {
                        var pace_flag = shared.Telemetry.CarIdxPaceFlags[j];
                        if (j == shared.Telemetry.PlayerCarIdx)
                        {
                            if (pace_flag.HasFlag(PaceFlags.EndOfLine))
                            {
                                currentGameState.StockCarRulesData.stockCarRuleApplicable = StockCarRule.MOVE_TO_EOLL;
                            }
                            else if (pace_flag.HasFlag(PaceFlags.WavedAround))
                            {
                                currentGameState.StockCarRulesData.stockCarRuleApplicable = StockCarRule.WAVE_AROUND_CATCH_END_OF_FIELD;
                            }
                            else if (pace_flag.HasFlag(PaceFlags.FreePass))
                            {
                                // CarIdxPaceLine doesn't seem to differ from the rest of the pack, so we have to guess the side
                                currentGameState.StockCarRulesData.stockCarRuleApplicable = StockCarRule.LUCKY_DOG_PASS_ON_OUTSIDE;
                                currentGameState.StockCarRulesData.luckyDogNameRaw = shared.Driver.Name;
                                currentGameState.StockCarRulesData.luckyDogNumber = shared.Driver.CarNumber;
                            }
                        }
                        else if (pace_flag.HasFlag(PaceFlags.FreePass))
                        {
                            currentGameState.StockCarRulesData.stockCarRuleApplicable = StockCarRule.LUCKY_DOG_ALLOW_TO_PASS_ON_OUTSIDE;
                            currentGameState.StockCarRulesData.luckyDogNameRaw = shared.Drivers[j].Name;
                            currentGameState.StockCarRulesData.luckyDogNumber = shared.Drivers[j].CarNumber;
                        }
                    }
                }
            }

            if (previousGameState != null && !currentGameState.FlagData.isFullCourseYellow && previousGameState.FlagData.isFullCourseYellow 
                && currentGameState.SessionData.SessionPhase != SessionPhase.Checkered)
            {                
                currentGameState.FlagData.fcyPhase = FullCourseYellowPhase.RACING;
                currentGameState.FlagData.lapCountWhenLastWentGreen = currentGameState.SessionData.CompletedLaps;
            }
            else if (previousGameState != null && currentGameState.FlagData.isFullCourseYellow)
            {
                if (!currentGameState.SafetyCarData.fcySafetyCarCallsEnabled || !enableFCYPitStateMessages)
                {
                    if (previousGameState.FlagData.fcyPhase == FullCourseYellowPhase.RACING)
                    {
                        currentGameState.FlagData.fcyPhase = FullCourseYellowPhase.PENDING;
                    }
                    if (flags.HasFlag(SessionFlags.OneLapToGreen))
                    {
                        currentGameState.FlagData.fcyPhase = FullCourseYellowPhase.LAST_LAP_CURRENT;
                    }
                }
                else
                {
                    if (!currentGameState.SafetyCarData.isOnTrack)
                    {
                        currentGameState.FlagData.fcyPhase = FullCourseYellowPhase.PENDING;
                    }
                    else
                    {
                        if (!shared.Telemetry.PitsOpen)
                        {
                            currentGameState.FlagData.fcyPhase = FullCourseYellowPhase.PITS_CLOSED;
                        }
                        if (shared.Telemetry.PitsOpen)
                        {
                            currentGameState.FlagData.fcyPhase = FullCourseYellowPhase.PITS_OPEN;
                        }
                    }
                    if (flags.HasFlag(SessionFlags.OneLapToGreen))
                    {
                        currentGameState.FlagData.fcyPhase = FullCourseYellowPhase.LAST_LAP_CURRENT;
                    }
                }
            }

            currentGameState.SessionData.NumCarsOverall = shared.PaceCarPresent ? shared.Drivers.Count - 1 : shared.Drivers.Count;
            if (currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.SessionPhase == SessionPhase.Finished)
            {
                // LapDistance == 1 seems to suggest disconnected, but could also mean in that little section between the finish
                // and start lines so there are probably some corner cases here (especially multiclass), but hopefully not in
                // situations that really matter.
                var waiting_on = shared.Drivers.Where(d => !d.IsSpectator & !d.IsPaceCar && !d.CurrentResults.IsOut && d.Live.LapDistance < 1 && d.FinishStatus == Driver.FinishState.Unknown);
                if (waiting_on.Count() != previousGameState.SessionData.CarsStillToFinish)
                {
                    Log.Debug(() => $"waiting on ...");
                    foreach (var d in waiting_on) {
                        Log.Debug(() => $"  ... {d.Name} distance={d.Live.LapDistance} speed={d.Live.Speed}");
                    }
                }

                currentGameState.SessionData.CarsStillToFinish = waiting_on.Count();
            }
            else
            {
                currentGameState.SessionData.CarsStillToFinish = currentGameState.SessionData.NumCarsOverall;
            }

            //use qual position in race session position until we green and first lap has been started. 
            //Here i'm a bit blindfolded, but this might be a fix for wrong multiclass start position message.
            Boolean useQualifyingPosition = false;
            if ((currentGameState.SessionData.SessionPhase == SessionPhase.Formation || currentGameState.SessionData.SessionPhase == SessionPhase.Gridwalk ||
                currentGameState.SessionData.SessionPhase == SessionPhase.Countdown || playerCar.Live.Lap < 1) && currentGameState.SessionData.SessionType == SessionType.Race)
            {
                currentGameState.SessionData.OverallPosition = playerCar.CurrentResults.QualifyingPosition;
                currentGameState.SessionData.ClassPosition = playerCar.CurrentResults.ClassQualifyingPosition;
                useQualifyingPosition = true;
            }
            else
            {
                // TODO this doesn't take cars that have finished the race into account, and therefore positions are wrong when approaching the end.
                // we might need to keep a list of how many cars ahead of us have finished and exited.
                currentGameState.SessionData.OverallPosition = currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.SessionPhase != SessionPhase.Finished && previousGameState != null
                    ? getRacePosition(currentGameState.SessionData.DriverRawName, previousGameState.SessionData.OverallPosition, playerCar.Live.Position, currentGameState.Now)
                    : playerCar.Live.Position;
                currentGameState.SessionData.ClassPosition = currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.SessionPhase != SessionPhase.Finished && previousGameState != null
                    ? getRacePosition(currentGameState.SessionData.DriverRawName, previousGameState.SessionData.ClassPosition, playerCar.Live.ClassPosition, currentGameState.Now, true)
                    : playerCar.Live.ClassPosition;
            }
            if (currentGameState.FlagData.isFullCourseYellow)
            {
                currentGameState.SessionData.OverallPosition = playerCar.CurrentResults.Position;
            }
            if (previousGameState != null
                && previousGameState.SessionData.SessionPhase != SessionPhase.Finished
                && currentGameState.SessionData.SessionPhase == SessionPhase.Finished
                && currentGameState.SessionData.SessionType == SessionType.Race
                && previousGameState.SessionData.OverallPosition != playerCar.Live.Position)
            {
                // Note: resolved position at crossing the finish line will be incorrect if any of the cars ahead are disconnected.
                // Scoring position will be incorrect for ~2.5secs after crossing s/f, if it changed during the last lap.
                // Lastly, as long as we detect finish within the PositionChangeLag, finishing position will be correct (should be)
                // because disconnecters only affect race pos after s/f (due to lap dist falling behind player's).
                Console.WriteLine("Finished position ambigous:  prev overall: {0}  curr overall (delayed): {1}  results pos: {2}  curr resolved: {3}",
                    previousGameState.SessionData.OverallPosition,
                    currentGameState.SessionData.OverallPosition,
                    playerCar.CurrentResults.Position,
                    playerCar.Live.Position);
            }

            if (currentGameState.SessionData.SessionType != SessionType.Race)
            {
                currentGameState.SessionData.ClassPosition = playerCar.Live.ClassPosition;
            }

            if (shared.Telemetry.LapBestLapTime > 0 && (currentGameState.SessionData.OverallSessionBestLapTime <= 0 || shared.Telemetry.LapBestLapTime < currentGameState.SessionData.OverallSessionBestLapTime))
            {
                currentGameState.SessionData.OverallSessionBestLapTime = shared.Telemetry.LapBestLapTime;
            }

            if (currentGameState.SessionData.OverallPosition == 1)
            {
                currentGameState.SessionData.LeaderSectorNumber = currentGameState.SessionData.SectorNumber;
            }

            int currentSector = playerCar.Live.CurrentSector;

            if (playerCar.Car.DriverPitTrkPct != -1.0f && currentGameState.SessionData.TrackDefinition != null)
            {
                currentGameState.PitData.PitBoxPositionEstimate = currentGameState.SessionData.TrackDefinition.trackLength * playerCar.Car.DriverPitTrkPct;
                if ((previousGameState != null && currentGameState.PitData.PitBoxPositionEstimate != previousGameState.PitData.PitBoxPositionEstimate)
                    || previousGameState == null)
                {
                    Console.WriteLine("Pit box position = " + currentGameState.PitData.PitBoxPositionEstimate.ToString("0.000"));
                }
            }

            currentGameState.SessionData.TrackSurface = (int)playerCar.Live.TrackSurface;
            if (previousGameState != null && (playerCar.Live.TrackSurface == TrackSurfaces.AproachingPits ||
                (TrackSurfaces)previousGameState.SessionData.TrackSurface == TrackSurfaces.OnTrack || 
                (TrackSurfaces)previousGameState.SessionData.TrackSurface == TrackSurfaces.OffTrack))
            {
                
                currentGameState.PitData.IsApproachingPitlane = (playerCar.Live.TrackSurface == TrackSurfaces.AproachingPits && 
                    ((TrackSurfaces)previousGameState.SessionData.TrackSurface == TrackSurfaces.OnTrack || (TrackSurfaces)previousGameState.SessionData.TrackSurface == TrackSurfaces.OffTrack)) || 
                    (playerCar.Live.TrackSurface == TrackSurfaces.AproachingPits && !currentGameState.PitData.InPitlane && previousGameState.PitData.IsApproachingPitlane);            
            }
            if (previousGameState != null)
            {
                if (previousGameState.PitData.TowedToPits == PitData.TowDetection.TowOnTrack && playerCar.Live.TrackSurface == TrackSurfaces.NotInWorld)
                {
                    currentGameState.PitData.TowedToPits = PitData.TowDetection.TowNotInWorld;
                }
                else if (previousGameState.PitData.TowedToPits == PitData.TowDetection.TowNotInWorld && playerCar.Live.TrackSurface == TrackSurfaces.InPitStall)
                {
                    currentGameState.PitData.TowedToPits = PitData.TowDetection.TowInPitStall;
                }
                else
                {
                    currentGameState.PitData.TowedToPits = PitData.TowDetection.TowOnTrack;
                }               
            }
            currentGameState.PitData.JumpedToPits = previousGameState != null && currentGameState.PitData.TowedToPits == PitData.TowDetection.TowInPitStall && previousGameState.PitData.TowedToPits == PitData.TowDetection.TowNotInWorld;
      
            currentGameState.PitData.IsInGarage = shared.Telemetry.IsInGarage;

            currentGameState.PitData.IsTeamRacing = shared.SessionData.IsTeamRacing;
            bool insertStartLap = false;
            if (currentGameState.SessionData.SessionType == SessionType.Race)
            {
                insertStartLap = previousGameState != null && previousGameState.SessionData.IsStartLapInserted == false && currentGameState.SessionData.SessionPhase == SessionPhase.Gridwalk && shared.Telemetry.IsOnTrack;
            }
            else
            {
                insertStartLap = previousGameState != null && previousGameState.SessionData.IsStartLapInserted == false && shared.Telemetry.IsOnTrack && playerCar.Live.Lap == 0;
                
            }           
            if (insertStartLap)
            {
                currentGameState.SessionData.IsStartLapInserted = true;
                playerCar.Live.startLapInserted = true;
            }
                

            currentGameState.SessionData.IsNewLap = playerCar.Live.IsNewLap && !(currentGameState.PitData.JumpedToPits || playerCar.Live.TrackSurface == TrackSurfaces.NotInWorld) || insertStartLap;

            if (currentGameState.SessionData.IsNewLap)
            {
                Log.Debug(() => $"NEW LAP distance={playerCar.Live.CorrectedLapDistance}");
            }

            if (currentGameState.SessionData.IsNewLap)
            {
                currentGameState.FlagData.previousLapWasFCY = previousGameState != null && previousGameState.FlagData.currentLapIsFCY;
                currentGameState.FlagData.currentLapIsFCY = currentGameState.FlagData.isFullCourseYellow;

                currentGameState.SessionData.IsLastLap = flags.HasFlag(SessionFlags.White);
            }

            // check that the IsLastLap detection is good without requiring a trace
            if (previousGameState != null && currentGameState.SessionData.IsLastLap != previousGameState.SessionData.IsLastLap)
            {
                if (currentGameState.SessionData.IsLastLap)
                {
                    Log.Debug("we began the last lap");
                }
                else
                {
                    Log.Debug("it is not the last lap anymore"); // race finished for us
                }
            }

            // we could be a little bit more lenient here when the last lap was an FCY but we're past 1st sector, but would need testing.
            // we might also want to disable incident calling while in the 1st sector of the first lap, since that's kind of a given.
            currentGameState.FlagData.useImprovisedIncidentCalling = !shared.SessionData.Track.IsOval && !currentGameState.FlagData.previousLapWasFCY && !currentGameState.FlagData.currentLapIsFCY;

            currentGameState.SessionData.IsNewSector = (currentGameState.SessionData.SectorNumber != currentSector && currentSector != 1 || currentGameState.SessionData.IsNewLap) && !(currentGameState.PitData.JumpedToPits || playerCar.Live.TrackSurface == TrackSurfaces.NotInWorld);

            if (previousGameState != null)
            {
                currentGameState.SessionData.CurrentLapIsValid = (previousGameState.SessionData.CurrentLapIsValid && !currentGameState.PitData.JumpedToPits) || currentGameState.SessionData.IsNewLap;
            }

            currentGameState.SessionData.SectorNumber = currentSector;

            if (currentGameState.SessionData.IsNewSector || currentGameState.SessionData.IsNewLap)
            {
                Boolean lapValid = previousGameState != null && playerCar.Live.PreviousLapWasValid && previousGameState.SessionData.CurrentLapIsValid && !currentGameState.PitData.JumpedToPits;
                if (currentSector == 1 && currentGameState.SessionData.IsNewLap)
                {
                    currentGameState.SessionData.playerCompleteLapWithProvidedLapTime(currentGameState.SessionData.OverallPosition, currentGameState.SessionData.SessionRunningTime,
                        playerCar.Live.LapTimePrevious, lapValid, currentGameState.PitData.InPitlane || playerCar.Live.TrackSurface == TrackSurfaces.AproachingPits, false, shared.Telemetry.TrackTempCrew, shared.Telemetry.AirTemp,
                        currentGameState.SessionData.SessionHasFixedTime, currentGameState.SessionData.SessionTimeRemaining, 3, currentGameState.TimingData, null, null);

                    if (currentGameState.carClass.carClassEnum == CarData.CarClassEnum.UNKNOWN_RACE)
                    {
                        currentGameState.carClass = CarData.getCarClassForIRacingId(playerCar.Car.CarClassId, playerCar.Car.CarId);
                    }
                }
                else if ((currentSector == 2 || currentSector == 3) && playerCar.Live.Lap > 0 && !currentGameState.PitData.JumpedToPits)
                {
                    currentGameState.SessionData.playerAddCumulativeSectorData(currentSector - 1, currentGameState.SessionData.OverallPosition, shared.Telemetry.LapCurrentLapTime,
                        currentGameState.SessionData.SessionRunningTime, currentGameState.SessionData.CurrentLapIsValid && !currentGameState.PitData.JumpedToPits, false, shared.Telemetry.TrackTempCrew, shared.Telemetry.AirTemp);
                }
            }

            if (currentGameState.SessionData.IsNewLap)
            {
                currentGameState.readLandmarksForThisLap = false;
            }

            if (currentGameState.SessionData.IsNewLap)
            {
                currentGameState.SessionData.playerStartNewLap(currentGameState.SessionData.LapCount,
                    currentGameState.SessionData.OverallPosition, currentGameState.PitData.InPitlane || playerCar.Live.TrackSurface == TrackSurfaces.AproachingPits, playerCar.Live.GameTimeWhenLastCrossedSFLine);
            }

            currentGameState.PositionAndMotionData.DistanceRoundTrack = currentGameState.SessionData.TrackDefinition != null ? Math.Abs(currentGameState.SessionData.TrackDefinition.trackLength * playerCar.Live.CorrectedLapDistance) : -1.0f;
            currentGameState.PositionAndMotionData.CarSpeed = (float)shared.Telemetry.Speed;

            currentGameState.PositionAndMotionData.Orientation.Pitch = shared.Telemetry.Pitch;
            currentGameState.PositionAndMotionData.Orientation.Roll = shared.Telemetry.Roll;
            currentGameState.PositionAndMotionData.Orientation.Yaw = shared.Telemetry.Yaw;

            currentGameState.PositionAndMotionData.AccelerationVector.VertAccel = shared.Telemetry.VertAccel;
            currentGameState.PositionAndMotionData.AccelerationVector.LatAccel = shared.Telemetry.LatAccel;
            currentGameState.PositionAndMotionData.AccelerationVector.LongAccel = shared.Telemetry.LongAccel;

            //Console.WriteLine("LatAccel = " + shared.Telemetry.LatAccel.ToString());
            //Console.WriteLine("VertAccel = " + shared.Telemetry.VertAccel.ToString());
            //Console.WriteLine("LongAccel = " + shared.Telemetry.LongAccel.ToString());
       


            if (currentGameState.PitData.InPitlane)
            {
                if (currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.SessionRunningTime > 10 &&
                    previousGameState != null && !previousGameState.PitData.InPitlane)
                {
                    currentGameState.PitData.NumPitStops++;
                }
                if (previousGameState != null && previousGameState.PitData.IsApproachingPitlane)
                {
                    currentGameState.PitData.OnInLap = true;
                    currentGameState.PitData.OnOutLap = false;
                }
            }
            if ((previousGameState != null && previousGameState.PitData.InPitlane && !currentGameState.PitData.InPitlane) ||
                currentGameState.PitData.JumpedToPits || (currentGameState.PitData.InPitlane && currentGameState.SessionData.IsNewLap))
            {
                currentGameState.PitData.OnInLap = false;
                currentGameState.PitData.OnOutLap = true;
            }
            if (currentGameState.SessionData.IsNewLap && playerCar.Live.TrackSurface != TrackSurfaces.AproachingPits)
            {
                // starting a new lap while not in the pitlane so clear the in / out lap flags
                currentGameState.PitData.OnInLap = false;
                currentGameState.PitData.OnOutLap = false;
            }

            if (previousGameState != null && currentGameState.PitData.OnOutLap && previousGameState.PitData.InPitlane && !currentGameState.PitData.InPitlane)
            {
                currentGameState.PitData.IsAtPitExit = true;
            }
            //Console.WriteLine("Voltage: " + shared.Telemetry.Voltage);
            SessionFlags flag = (SessionFlags)shared.Telemetry.SessionFlags;
            if (flag.HasFlag(SessionFlags.Black) && !flag.HasFlag(SessionFlags.Furled))
            {
                currentGameState.PenaltiesData.HasPitStop = true;
            }
            if (flag.HasFlag(SessionFlags.Furled) && currentGameState.SessionData.SessionType != SessionType.Qualify)
            {
                currentGameState.PenaltiesData.HasSlowDown = true;
            }
            if (flag.HasFlag(SessionFlags.Repair))
            {
                currentGameState.PenaltiesData.HasMeatballFlag = true;
            }
            if (flag.HasFlag(SessionFlags.YellowWaving))
            {
                currentGameState.SessionData.Flag = FlagEnum.YELLOW;
            }
            if (flag.HasFlag(SessionFlags.Debris))
            {
                currentGameState.SessionData.Flag = FlagEnum.RED_YELLOW;
            }
            else if (previousGameState != null && !previousGameState.SessionData.Flag.HasFlag(FlagEnum.BLUE) && flag.HasFlag(SessionFlags.Blue) && !flag.HasFlag(SessionFlags.Green))
            {
                currentGameState.SessionData.Flag = FlagEnum.BLUE;
            }
            /*else if (flag.HasFlag(SessionFlags.CautionWaving))
            {
                currentGameState.SessionData.Flag = FlagEnum.DOUBLE_YELLOW;
            }*/
            if (flag.HasFlag(SessionFlags.White))
            {
                if (GlobalBehaviourSettings.useAmericanTerms)
                {
                    currentGameState.SessionData.Flag = FlagEnum.WHITE;
                }
            }
            currentGameState.PitData.PitSpeedLimit = shared.SessionData.Track.TrackPitSpeedLimit;
            float speed = (float)playerCar.Live.Speed;
            bool hasValidSpeed = speed > 0 && speed < 110;  // we get some wild speed data occasionally (>400m/s)
            currentGameState.SessionData.DeltaTime.SetNextDeltaPoint(currentGameState.PositionAndMotionData.DistanceRoundTrack, currentGameState.SessionData.CompletedLaps,
                speed, currentGameState.Now, 
                hasValidSpeed && !currentGameState.PitData.InPitlane && !currentGameState.PitData.IsApproachingPitlane && !currentGameState.PitData.OnOutLap && playerCar.Live.TrackSurface != TrackSurfaces.NotInWorld);

            if (previousGameState != null)
            {
                String stoppedInLandmark = currentGameState.SessionData.trackLandmarksTiming.updateLandmarkTiming(currentGameState.SessionData.TrackDefinition,
                    currentGameState.SessionData.SessionRunningTime, previousGameState.PositionAndMotionData.DistanceRoundTrack,
                    currentGameState.PositionAndMotionData.DistanceRoundTrack, currentGameState.PositionAndMotionData.CarSpeed, currentGameState.carClass);
                currentGameState.SessionData.stoppedInLandmark = currentGameState.PitData.InPitlane ? null : stoppedInLandmark;
            }
            if (playerCar.Live.HasCrossedSFLine)
            {
                currentGameState.SessionData.trackLandmarksTiming.cancelWaitingForLandmarkEnd();
            }

            GameStateData.Multiclass = shared.SessionData.NumCarClasses > 1;
            GameStateData.NumberOfClasses = shared.SessionData.NumCarClasses;

            List<double> combinedStrengthOfField = new List<double>();
            foreach (Driver driver in shared.Drivers)
            {
                String opponentDataKey = driver.Id.ToString();

                if (driver.IsPaceCar || driver.IsSpectator)
                {
                    continue;
                }
                if (!GlobalBehaviourSettings.sofIsPlayerClass || driver.Car.CarClassId == playerCar.Car.CarClassId)
                {
                    combinedStrengthOfField.Add(driver.IRating);
                }

                if (driver.IsCurrentDriver || currentGameState.disqualifiedDriverNames.ContainsKey(driver.Name) || currentGameState.retriedDriverNames.ContainsKey(driver.Name))
                {
                    continue;
                }

                if (driver.CurrentResults.OutReasonId == ReasonOutId.IDS_DISQUALIFIED)
                {
                    // remove this driver from the set immediately
                    if (!currentGameState.disqualifiedDriverNames.ContainsKey(driver.Name))
                    {
                        Console.WriteLine("Opponent " + driver.Name + " has been disqualified");
                        currentGameState.disqualifiedDriverNames.Add(driver.Name, driver.CarNumber);
                    }
                    currentGameState.OpponentData.Remove(opponentDataKey);
                    continue;
                }

                if (driver.CurrentResults.IsOut)
                {
                    // remove this driver from the set immediately
                    if (!currentGameState.retriedDriverNames.ContainsKey(driver.Name))
                    {
                        Console.WriteLine("Opponent " + driver.Name + " has retired");
                        currentGameState.retriedDriverNames.Add(driver.Name, driver.CarNumber);
                    }
                    currentGameState.OpponentData.Remove(opponentDataKey);
                    continue;
                }

                String driverName = driver.Name;
                lastActiveTimeForOpponents[opponentDataKey] = currentGameState.Now;
                Boolean createNewDriver = true;
                OpponentData currentOpponentData = null;
                if (currentGameState.OpponentData.TryGetValue(opponentDataKey, out currentOpponentData))
                {
                    // if we're team racing or the car number matches, update this driver.
                    if (shared.SessionData.IsTeamRacing || (driver.CarNumber.Equals(currentOpponentData.CarNumber)))
                    {
                        createNewDriver = false;
                        OpponentData previousOpponentData = null;
                        int previousOpponentSectorNumber = 0;
                        int previousOpponentCompletedLaps = 0;
                        int previousOpponentOverallPosition = 0;
                        TyreType previousOpponentTyreType = TyreType.Uninitialized;
                        Boolean previousOpponentIsEnteringPits = false;
                        float previousOpponentSpeed = 0;
                        float previousDistanceRoundTrack = 0;
                        bool previousIsInPits = false;
                        bool hasCrossedSFLine = false;
                        bool previousIsApporchingPits = false;
                        Boolean jumpedToPits = false;
                        TrackSurfaces previousTrackSurface = TrackSurfaces.NotInWorld;
                        float previousOpponentGameTimeWhenLastCrossedStartFinishLine = -1;
                        bool previousOpponentIsStartLapInserted = false;
                        OpponentData opponentPrevious = null;
                        if (previousGameState != null && previousGameState.OpponentData.TryGetValue(opponentDataKey, out opponentPrevious))
                        {
                            previousOpponentData = opponentPrevious;
                            previousOpponentSectorNumber = previousOpponentData.CurrentSectorNumber;
                            previousOpponentCompletedLaps = previousOpponentData.CompletedLaps;
                            previousOpponentOverallPosition = previousOpponentData.OverallPosition;
                            previousOpponentIsEnteringPits = previousOpponentData.isEnteringPits();
                            previousOpponentSpeed = previousOpponentData.Speed;
                            previousDistanceRoundTrack = previousOpponentData.DistanceRoundTrack;
                            previousIsInPits = previousOpponentData.InPits;
                            previousOpponentGameTimeWhenLastCrossedStartFinishLine = previousOpponentData.GameTimeWhenLastCrossedStartFinishLine;
                            previousTrackSurface = (TrackSurfaces)previousOpponentData.trackSurface;
                            previousIsApporchingPits = previousOpponentData.isApporchingPits;
                            previousOpponentTyreType = previousOpponentData.CurrentTyres;
                            currentOpponentData.ClassPositionAtPreviousTick = previousOpponentData.ClassPosition;
                            currentOpponentData.OverallPositionAtPreviousTick = previousOpponentData.OverallPosition;
                            previousOpponentIsStartLapInserted = previousOpponentData.IsStartLapInserted;
                        }
                        bool isInWorld = driver.Live.TrackSurface != TrackSurfaces.NotInWorld;
                        hasCrossedSFLine = driver.Live.HasCrossedSFLine;
                        int currentOpponentSector = isInWorld ? driver.Live.CurrentSector : 0;                            
                        currentOpponentData.IsActive = true;
                        currentOpponentData.trackSurface = (int)driver.Live.TrackSurface;
                        bool previousOpponentLapValid = driver.Live.PreviousLapWasValid;
                        currentOpponentData.isApporchingPits = false;
                        if (driver.Live.TrackSurface == TrackSurfaces.AproachingPits || previousTrackSurface == TrackSurfaces.OnTrack)
                        {
                            if((driver.Live.TrackSurface == TrackSurfaces.AproachingPits && previousTrackSurface == TrackSurfaces.OnTrack) || //allow only aproach to pit if previous state was on track
                                (driver.Live.TrackSurface == TrackSurfaces.AproachingPits && !shared.Telemetry.CarIdxOnPitRoad[driver.Id] && previousIsApporchingPits))
                            {
                                currentOpponentData.isApporchingPits = true;
                            }                                
                        }
                        bool insertOpponentStartLap = false;
                        if (currentGameState.SessionData.SessionType == SessionType.Race)
                        {
                            insertOpponentStartLap = previousOpponentIsStartLapInserted == false && currentGameState.SessionData.SessionPhase == SessionPhase.Gridwalk;
                        }
                        else
                        {
                            insertOpponentStartLap = previousOpponentIsStartLapInserted == false && driver.Live.TrackSurface != TrackSurfaces.NotInWorld && playerCar.Live.Lap == 0 && driver.Live.startLapInserted == false && driver.Live.IsNewLap == false;
                        }
                        if (insertOpponentStartLap)
                        {
                            driver.Live.startLapInserted = true;
                            currentOpponentData.IsStartLapInserted = true;
                        }
                        //currentOpponentData.isApporchingPits = driver.Live.TrackSurface == TrackSurfaces.AproachingPits && !shared.Telemetry.CarIdxOnPitRoad[driver.Id] && !previousIsInPits;
                        // TODO:
                        // reset to to pitstall
                        bool currentOpponentLapValid = true;
                        if ((previousOpponentSectorNumber == 1 || previousOpponentSectorNumber == 2) && !previousIsInPits && shared.Telemetry.CarIdxOnPitRoad[driver.Id])
                        {
                            currentOpponentLapValid = false;
                        }
                                                        
                        int currentOpponentOverallPosition = currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.SessionPhase != SessionPhase.Finished && previousOpponentOverallPosition > 0 ?
                            getRacePosition(opponentDataKey, previousOpponentOverallPosition, driver.Live.Position, currentGameState.Now)
                            : driver.Live.Position;
                        if(currentGameState.FlagData.isFullCourseYellow)
                        {
                            currentOpponentOverallPosition = driver.CurrentResults.Position;
                        }
                        if(useQualifyingPosition)
                        {
                            currentOpponentOverallPosition = driver.CurrentResults.QualifyingPosition;
                            currentOpponentData.ClassPosition = driver.CurrentResults.ClassQualifyingPosition;
                        }
                        int currentOpponentLapsCompleted = driver.Live.LiveLapsCompleted;

                        if (currentOpponentSector == 0)
                        {
                            currentOpponentSector = previousOpponentSectorNumber;
                        }
                        float currentOpponentLapDistance = isInWorld && currentGameState.SessionData.TrackDefinition != null ? currentGameState.SessionData.TrackDefinition.trackLength * driver.Live.CorrectedLapDistance : 0;
                        float currentOpponentSpeed = isInWorld ? (float)driver.Live.Speed : 0;
                        //Console.WriteLine("lapdistance:" + currentOpponentLapDistance);
                        bool opponentHasValidSpeed = currentOpponentSpeed > 7 && currentOpponentSpeed < 110;    // we get some wild speed data occasionally (>400m/s)
                        currentOpponentData.DeltaTime.SetNextDeltaPoint(currentOpponentLapDistance, currentOpponentLapsCompleted, currentOpponentSpeed, currentGameState.Now,
                            opponentHasValidSpeed && driver.Live.TrackSurface != TrackSurfaces.AproachingPits && !shared.Telemetry.CarIdxOnPitRoad[driver.Id] && driver.Live.TrackSurface != TrackSurfaces.NotInWorld);

                        Boolean finishedAllottedRaceLaps = currentGameState.SessionData.SessionNumberOfLaps > 0 && currentGameState.SessionData.SessionNumberOfLaps == currentOpponentLapsCompleted;
                        Boolean finishedAllottedRaceTime = false;

                        if (currentGameState.SessionData.IsLastLap)
                        {
                            if (previousOpponentCompletedLaps < currentOpponentLapsCompleted)
                            {
                                if (currentOpponentOverallPosition == 1)
                                {
                                    Log.Debug(() => $"{currentOpponentData} has passed the finish line when we were on our last lap");
                                }
                                finishedAllottedRaceTime = true;
                            }
                        }

                        if (currentOpponentLapsCompleted > 0 && currentOpponentOverallPosition == 1 && (finishedAllottedRaceTime || finishedAllottedRaceLaps) && !currentGameState.SessionData.LeaderHasFinishedRace)
                        {
                            Log.Debug("the leader has finished the race");
                            currentGameState.SessionData.LeaderHasFinishedRace = true;
                        }

                        currentOpponentData.LicensLevel = driver.licensLevel;
                        currentOpponentData.iRating = driver.IRating;
                        currentOpponentData.iFlair = driver.FlairName;
                        currentOpponentData.iDiv = driver.DivisionName;
                        currentOpponentData.EstimatedLapTime = driver.EstimatedLapTime;
                                                        
                        updateOpponentData(currentOpponentData, driverName, driver.CustId, currentOpponentOverallPosition, currentOpponentLapsCompleted,
                                    currentOpponentSector, (float)driver.Live.LapTimePrevious, driver.Live.Gear,driver.Live.Rpm,driver.Live.SteeringAngle,
                                    driver.Live.TrackSurface, driver.Live.IsInPilLane, previousIsApporchingPits,
                                    previousOpponentLapValid, currentOpponentLapValid, currentGameState.SessionData.SessionRunningTime, currentOpponentLapDistance,
                                    currentGameState.SessionData.SessionHasFixedTime, currentGameState.SessionData.SessionTimeRemaining,
                                    currentGameState.SessionData.SessionType == SessionType.Race, shared.Telemetry.TrackTempCrew,
                                    shared.Telemetry.AirTemp, currentOpponentSpeed, previousOpponentSpeed, driver.Live.GameTimeWhenLastCrossedSFLine, driver.Live.IsNewLap || insertOpponentStartLap,
                                    driver.Car.CarClassId, driver.Car.CarId, currentGameState.TimingData, currentGameState.carClass, shared.SessionData.IsTeamRacing, shared.Telemetry.Precipitation > 0);

                        //allow gaps in qual and prac, delta here is not on track delta but diff on fastest time 
                        if (currentGameState.SessionData.SessionType != SessionType.Race)
                        {
                            currentOpponentData.ClassPosition = driver.Live.ClassPosition;
                            if (currentOpponentData.ClassPosition == currentGameState.SessionData.ClassPosition + 1)
                            {
                                currentGameState.SessionData.TimeDeltaBehind = Math.Abs(currentOpponentData.CurrentBestLapTime - currentGameState.SessionData.PlayerLapTimeSessionBest);
                            }
                            if (currentOpponentData.ClassPosition == currentGameState.SessionData.ClassPosition - 1)
                            {
                                currentGameState.SessionData.TimeDeltaFront = Math.Abs(currentGameState.SessionData.PlayerLapTimeSessionBest - currentOpponentData.CurrentBestLapTime);
                            }
                        }

                        if (currentGameState.SessionData.SessionType == SessionType.Race && !useQualifyingPosition)
                        {
                            currentOpponentData.ClassPosition = driver.Live.ClassPosition;
                        }

                        if (previousOpponentData != null && !currentGameState.SessionData.JustGoneGreen)
                        {
                            currentOpponentData.trackLandmarksTiming = previousOpponentData.trackLandmarksTiming;
                            String stoppedInLandmark = currentOpponentData.trackLandmarksTiming.updateLandmarkTiming(
                                currentGameState.SessionData.TrackDefinition, currentGameState.SessionData.SessionRunningTime,
                                previousDistanceRoundTrack, currentOpponentData.DistanceRoundTrack, currentOpponentData.Speed, currentOpponentData.CarClass);
                            currentOpponentData.stoppedInLandmark = driver.Live.IsInPilLane || !isInWorld || finishedAllottedRaceTime || finishedAllottedRaceLaps || driver.Live.Lap <= 0 || driver.Live.TrackSurface == TrackSurfaces.AproachingPits ? null : stoppedInLandmark;
                        }
                        if (currentGameState.SessionData.JustGoneGreen)
                        {
                            currentOpponentData.trackLandmarksTiming = new TrackLandmarksTiming();
                        }
                        if (hasCrossedSFLine)
                        {
                            currentOpponentData.trackLandmarksTiming.cancelWaitingForLandmarkEnd();
                        }
                        currentOpponentData.hasJustChangedToDifferentTyreType = false;
                        if (shared.Telemetry.CarIdxTireCompound != null)
                        {
                            currentOpponentData.CurrentTyres = mapToTyreType(driver.Live.CurrentTireCompound, currentGameState.carClass.carClassEnum);
                            if (currentOpponentData.CurrentTyres != previousOpponentTyreType && currentOpponentData.CurrentTyres != TyreType.Uninitialized)
                            {
                                currentOpponentData.TyreChangesByLap[currentOpponentData.OpponentLapData.Count] = currentOpponentData.CurrentTyres;
                                currentOpponentData.hasJustChangedToDifferentTyreType = previousOpponentTyreType != TyreType.Uninitialized;
                            }
                        }

                        if (currentOpponentData.CurrentBestLapTime > 1)
                        {
                            if (currentGameState.SessionData.OpponentsLapTimeSessionBestOverall <= 0 ||
                                currentOpponentData.CurrentBestLapTime < currentGameState.SessionData.OpponentsLapTimeSessionBestOverall)
                            {
                                currentGameState.SessionData.OpponentsLapTimeSessionBestOverall = currentOpponentData.CurrentBestLapTime;

                                if (currentGameState.SessionData.OverallSessionBestLapTime <= 0 ||
                                    currentGameState.SessionData.OverallSessionBestLapTime > currentOpponentData.CurrentBestLapTime)
                                {
                                    currentGameState.SessionData.OverallSessionBestLapTime = currentOpponentData.CurrentBestLapTime;
                                }
                            }
                            if (CarData.IsCarClassEqual(currentOpponentData.CarClass, currentGameState.carClass))
                            {
                                if (currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass <= 0 ||
                                    currentOpponentData.CurrentBestLapTime < currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass)
                                {
                                    currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass = currentOpponentData.CurrentBestLapTime;
                                    if (currentGameState.SessionData.PlayerClassSessionBestLapTime <= 0 ||
                                        currentGameState.SessionData.PlayerClassSessionBestLapTime > currentOpponentData.CurrentBestLapTime)
                                    {
                                        currentGameState.SessionData.PlayerClassSessionBestLapTime = currentOpponentData.CurrentBestLapTime;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Removing driver " + currentOpponentData.DriverRawName + " (custId " + currentOpponentData.CostId +
                            ") and replacing with " + driverName + " (custId " + driver.CustId +")");
                        currentGameState.OpponentData.Remove(opponentDataKey);
                    }
                }
                if (createNewDriver && currentGameState.SessionData.TrackDefinition != null)
                {
                    if (!driver.CurrentResults.IsOut || !driver.IsPaceCar || !driver.IsSpectator)
                    {
                        currentGameState.OpponentData.Add(opponentDataKey, createOpponentData(driver,
                            true, currentGameState.SessionData.TrackDefinition.trackLength));
                    }
                }
            }
            if (currentGameState.Now > nextOpponentCleanupTime)
            {
                nextOpponentCleanupTime = currentGameState.Now + opponentCleanupInterval;
                DateTime oldestAllowedUpdate = currentGameState.Now - opponentCleanupInterval;
                foreach (KeyValuePair<string, OpponentData> entry in currentGameState.OpponentData)
                {
                    DateTime lastTimeForOpponent = DateTime.MinValue;
                    if (!lastActiveTimeForOpponents.TryGetValue(entry.Key, out lastTimeForOpponent) || (lastTimeForOpponent < oldestAllowedUpdate && entry.Value.IsActive))
                    {
                        entry.Value.IsActive = false;
                        entry.Value.InPits = true;
                        entry.Value.Speed = 0;
                        entry.Value.stoppedInLandmark = null;
                        entry.Value.trackLandmarksTiming.cancelWaitingForLandmarkEnd();
                        entry.Value.DeltaTime.SetNextDeltaPoint(0, entry.Value.CompletedLaps, 0, currentGameState.Now, !entry.Value.InPits);
                        Console.WriteLine("Opponent " + entry.Value.DriverRawName + "(index " + entry.Key + ") has been inactive for " + opponentCleanupInterval + ", sending him back to pits");
                    }
                }
            }

            //Sof calculations
            if (combinedStrengthOfField.Count > 0)
            {
                double baseSof = 1350 / Math.Log(2);
                double sofExpSum = 0;
                foreach (double ir in combinedStrengthOfField)
                {
                    sofExpSum += Math.Exp(-ir / baseSof);
                }
                currentGameState.SessionData.StrengthOfField = (int)Math.Round(Math.Floor(baseSof * Math.Log(combinedStrengthOfField.Count / sofExpSum)));

                if (!GameStateData.Multiclass || GlobalBehaviourSettings.sofIsPlayerClass)
                {
                    combinedStrengthOfField.Sort();
                    combinedStrengthOfField.Reverse();
                    int driver = playerCar.IRating;
                    int rated = combinedStrengthOfField.FindIndex(r => r <= driver) + 1;
                    int position = playerCar?.CurrentResults.ClassQualifyingPosition > 0 ? playerCar.CurrentResults.ClassQualifyingPosition : rated;
                    int expected = (position + rated) / 2;
                    currentGameState.SessionData.expectedFinishingPosition = new Tuple<int, int>(expected, combinedStrengthOfField.Count);
                }
            }

            if (currentGameState.SessionData.IsNewLap && currentGameState.SessionData.PreviousLapWasValid &&
                currentGameState.SessionData.LapTimePrevious > 1)
            {
                if (currentGameState.SessionData.PlayerLapTimeSessionBest == -1 ||
                     currentGameState.SessionData.LapTimePrevious < currentGameState.SessionData.PlayerLapTimeSessionBest)
                {
                    currentGameState.SessionData.PlayerLapTimeSessionBest = currentGameState.SessionData.LapTimePrevious;

                    if (currentGameState.SessionData.OverallSessionBestLapTime == -1 ||
                        currentGameState.SessionData.LapTimePrevious < currentGameState.SessionData.OverallSessionBestLapTime)
                    {
                        currentGameState.SessionData.OverallSessionBestLapTime = currentGameState.SessionData.LapTimePrevious;
                    }

                    if (currentGameState.SessionData.PlayerClassSessionBestLapTime == -1 ||
                        currentGameState.SessionData.LapTimePrevious < currentGameState.SessionData.PlayerClassSessionBestLapTime)
                    {
                        currentGameState.SessionData.PlayerClassSessionBestLapTime = currentGameState.SessionData.LapTimePrevious;
                    }
                }
            }


            if(playerCar.Car.CarIsElectric)
            {
                currentGameState.BatteryData.BatteryUseActive = true;
                currentGameState.BatteryData.BatteryCapacity = playerCar.Car.DriverCarFuelMaxKWh * playerCar.Car.DriverCarMaxFuelPct;
                currentGameState.BatteryData.BatteryPercentageLeft =  shared.Telemetry.FuelLevel;
                currentGameState.PitData.IsElectricVehicleSwapAllowed = true;
            }
            else
            {
                currentGameState.FuelData.FuelUseActive = true;
                currentGameState.FuelData.FuelPressure = shared.Telemetry.FuelPress;
                currentGameState.FuelData.FuelLeft = shared.Telemetry.FuelLevel;
                currentGameState.FuelData.FuelCapacity = playerCar.Car.DriverCarFuelMaxLtr * playerCar.Car.DriverCarMaxFuelPct;
            }
            currentGameState.FuelData.FuelMultiplier = 1; // iRacing does not have a fuel multiplier

            if (currentGameState.carClass.limiterAvailable)
            {
                currentGameState.PitData.limiterStatus = shared.Telemetry.EngineWarnings.HasFlag(EngineWarnings.PitSpeedLimiter) == true ? PitData.LimiterStatus.ACTIVE : PitData.LimiterStatus.INACTIVE;
            }

            //conditions
            currentGameState.Conditions.UpdateLiveSample(currentGameState.Now, currentGameState.SessionData.CompletedLaps, currentGameState.SessionData.SectorNumber,
                    shared.Telemetry.AirTemp, shared.Telemetry.TrackTempCrew, shared.Telemetry.Precipitation, shared.Telemetry.WindVel, 0, 0, 0,
                    currentGameState.SessionData.IsNewLap, ConditionsMonitor.TrackStatus.UNKNOWN, (object)shared.Telemetry.TrackWetness != null ? (ConditionsMonitor.TrackWetness)shared.Telemetry.TrackWetness : ConditionsMonitor.TrackWetness.UNKNOWN);
            if (currentGameState.Now > nextConditionsSampleDue)
            {
                nextConditionsSampleDue = currentGameState.Now.Add(ConditionsMonitor.ConditionsSampleFrequency);                    
                currentGameState.Conditions.addSample(currentGameState.Now, currentGameState.SessionData.CompletedLaps, currentGameState.SessionData.SectorNumber,
                    shared.Telemetry.AirTemp, shared.Telemetry.TrackTempCrew, shared.Telemetry.Precipitation, shared.Telemetry.WindVel, 0, 0, 0, 
                    currentGameState.SessionData.IsNewLap, ConditionsMonitor.TrackStatus.UNKNOWN, (object)shared.Telemetry.TrackWetness != null ? (ConditionsMonitor.TrackWetness)shared.Telemetry.TrackWetness : ConditionsMonitor.TrackWetness.UNKNOWN);
            }

            currentGameState.PenaltiesData.IsOffRacingSurface = shared.Telemetry.PlayerTrackSurface == TrackSurfaces.OffTrack;

            if (invalidateCutTrackLaps && !currentGameState.PitData.OnOutLap && previousGameState != null &&
                !(currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.SessionPhase == SessionPhase.Countdown) &&
                previousGameState.SessionData.CurrentIncidentCount + 1 == shared.Telemetry.PlayerCarMyIncidentCount /*&& 
                currentGameState.SessionData.TrackSurface == (int)TrackSurfaces.OffTrack*/)
            {
                currentGameState.PenaltiesData.CutTrackWarnings = previousGameState.PenaltiesData.CutTrackWarnings + 1;
                currentGameState.SessionData.CurrentLapIsValid = false;
            }

            currentGameState.SessionData.CurrentIncidentCount = shared.Telemetry.PlayerCarMyIncidentCount;
            currentGameState.SessionData.CurrentDriverIncidentCount = shared.Telemetry.PlayerCarDriverIncidentCount;
            currentGameState.SessionData.CurrentTeamIncidentCount = shared.Telemetry.PlayerCarTeamIncidentCount;
            currentGameState.SessionData.HasLimitedIncidents = shared.SessionData.IsLimitedIncidents;
            currentGameState.SessionData.MaxIncidentCount = shared.SessionData.IncidentLimit;
            if(currentGameState.PenaltiesData.HasSlowDown)
            {
                currentGameState.PenaltiesData.NumOutstandingPenalties++;
            }
            if (currentGameState.PenaltiesData.HasPitStop)
            {
                currentGameState.PenaltiesData.NumOutstandingPenalties++;
            }

            currentGameState.TyreData = new TyreData
            {
                FrontLeftPressure = shared.Telemetry.LFcoldPressure,
                FrontRightPressure = shared.Telemetry.RFcoldPressure,
                RearLeftPressure = shared.Telemetry.LRcoldPressure,
                RearRightPressure = shared.Telemetry.RRcoldPressure,

                FrontLeft_LeftTemp = shared.Telemetry.LFtempCL,
                FrontLeft_CenterTemp = shared.Telemetry.LFtempCM,
                FrontLeft_RightTemp = shared.Telemetry.LFtempCR,
                FrontRight_LeftTemp = shared.Telemetry.RFtempCL,
                FrontRight_CenterTemp = shared.Telemetry.RFtempCM,
                FrontRight_RightTemp = shared.Telemetry.RFtempCR,
                RearLeft_LeftTemp = shared.Telemetry.LRtempCL,
                RearLeft_CenterTemp = shared.Telemetry.LRtempCM,
                RearLeft_RightTemp = shared.Telemetry.LRtempCR,
                RearRight_LeftTemp = shared.Telemetry.RRtempCL,
                RearRight_CenterTemp = shared.Telemetry.RRtempCM,
                RearRight_RightTemp = shared.Telemetry.RRtempCR,

                TyreWearActive = true,
                // we're only tracking one general wear value. We take the lowest tread of I/M/O.
                // note that although iRacing calls it wear it is actually tread, so we need to reverse.
                FrontLeftPercentWear = 100 - 100 * shared.Telemetry.LFwear(),
                FrontRightPercentWear = 100 - 100 * shared.Telemetry.RFwear(),
                RearLeftPercentWear = 100 - 100 * shared.Telemetry.LRwear(),
                RearRightPercentWear = 100 - 100 * shared.Telemetry.RRwear()
            };

            // to indicate to the TyreMonitor when a tyre is changed, we briefly toggle the "attached" status of each wheel
            // when in the pit stall and the PitServiceFlag is set. This approach is prone to false positives
            // because it is possible that the user has manually changed their request and is also prone to false negatives
            // because the user may have exited to the garage while the work is happening.
            if (shared.Telemetry.PlayerCarInPitStall)
            {
                currentGameState.TyreData.iRacingChangesPossible = shared.Telemetry.PitRepairLeft <= 0 && flag.HasFlag(SessionFlags.Servicible);
                currentGameState.TyreData.LeftFrontAttached = !shared.Telemetry.PitSvFlags.HasFlag(PitServiceFlags.LFTireChange);
                currentGameState.TyreData.RightFrontAttached = !shared.Telemetry.PitSvFlags.HasFlag(PitServiceFlags.RFTireChange);
                currentGameState.TyreData.LeftRearAttached = !shared.Telemetry.PitSvFlags.HasFlag(PitServiceFlags.LRTireChange);
                currentGameState.TyreData.RightRearAttached = !shared.Telemetry.PitSvFlags.HasFlag(PitServiceFlags.RRTireChange);
            }
            else
            {
                currentGameState.TyreData.iRacingChangesPossible = false;
                currentGameState.TyreData.LeftFrontAttached = true;
                currentGameState.TyreData.RightFrontAttached = true;
                currentGameState.TyreData.LeftRearAttached = true;
                currentGameState.TyreData.RightRearAttached = true;
            }

        TyreType tyreType = mapToTyreType(shared.Telemetry.PlayerTireCompound, currentGameState.carClass.carClassEnum);
            currentGameState.TyreData.FrontLeftTyreType = tyreType;
            currentGameState.TyreData.FrontRightTyreType = tyreType;
            currentGameState.TyreData.RearLeftTyreType = tyreType;
            currentGameState.TyreData.RearRightTyreType = tyreType;

            if (previousGameState != null)
            {
                currentGameState.FrozenOrderData = GetFrozenOrderData(currentGameState, previousGameState, shared);
                currentGameState.SessionData.TriggerStartWarning = currentGameState.FrozenOrderData.Phase == FrozenOrderPhase.FormationStanding;
            }
            else
            {
                currentGameState.FrozenOrderData = new FrozenOrderData();
            }

            if (previousGameState != null
                && currentGameState.SessionData.SessionType == SessionType.Race
                && currentGameState.SessionData.SessionPhase == SessionPhase.Finished
                && previousGameState.SessionData.SessionPhase != SessionPhase.Finished)
            {
                var driverInfo = new List<Tuple<int, string, int, double, double, Driver.FinishState>>();
                foreach (var driver in shared.Drivers)
                {
                    if (driver.IsSpectator || driver.IsPaceCar || driver.CurrentResults.IsOut)
                    {
                        continue;
                    }

                    var delayedPosition = -1;
                    OpponentData od = null;
                    if (currentGameState.OpponentData.TryGetValue(driver.Id.ToString(), out od))
                    {
                        delayedPosition = od.OverallPosition;
                    }
                    else if (driver.Id == playerCar.Id)  // Player.
                    {
                        delayedPosition = currentGameState.SessionData.OverallPosition;
                    }
                    else  // Retired
                    {
                        delayedPosition = driver.Live.Position;
                    }

                    driverInfo.Add(new Tuple<int, string, int, double, double, Driver.FinishState>(
                        delayedPosition,
                        driver.Name,
                        driver.Live.LiveLapsCompleted,
                        driver.Live.TotalLapDistance,
                        driver.Live.TotalLapDistanceCorrected,
                        driver.FinishStatus));
                }

                Console.WriteLine("Estimated standings:");
                foreach (var driver in driverInfo.OrderBy(d => d.Item1))
                {
                    Console.WriteLine("P:{0}  Name:{1}  LLC:{2}  TLD:{3}  TLDC:{4}  FS:{5}",
                        driver.Item1,
                        driver.Item2,
                        driver.Item3,
                        driver.Item4,
                        driver.Item5,
                        driver.Item6);
                }
            }
            if (currentGameState.SessionData.TrackDefinition != null)
            {
                CrewChief.trackName = currentGameState.SessionData.TrackDefinition.name;
            }
            if (currentGameState.carClass != null)
            {
                CrewChief.carClass = currentGameState.carClass.carClassEnum;
            }
            currentGameState.carName = shared.Driver.Car.CarName;
            CrewChief.distanceRoundTrack = currentGameState.PositionAndMotionData.DistanceRoundTrack;
            CrewChief.viewingReplay = false;
            if (currentGameState.SessionData.IsNewLap)
            {
                if (currentGameState.hardPartsOnTrackData.updateHardPartsForNewLap(currentGameState.SessionData.LapTimePrevious))
                {
                    currentGameState.SessionData.TrackDefinition.adjustGapPoints(currentGameState.hardPartsOnTrackData.processedHardPartsForBestLap);
                }
            }
            else if (!currentGameState.SessionData.TrackDefinition.isOval &&
                !(currentGameState.SessionData.SessionType == SessionType.Race && !currentGameState.PitData.OnOutLap &&
                   (currentGameState.SessionData.CompletedLaps < 1 || (GameStateData.useManualFormationLap && currentGameState.SessionData.CompletedLaps < 2))))// if(!currentGameState.PitData.OnOutLap*/)
            {
                currentGameState.hardPartsOnTrackData.mapHardPartsOnTrack(currentGameState.ControlData.BrakePedal, currentGameState.ControlData.ThrottlePedal,
                    currentGameState.PositionAndMotionData.DistanceRoundTrack, currentGameState.SessionData.CurrentLapIsValid && !currentGameState.PitData.InPitlane,
                    currentGameState.SessionData.TrackDefinition.trackLength);
            }
                                           
            return currentGameState;
        }

        private void updateOvertakingAids(GameStateData currentGameState, DrsStatus drsStatus)
        {
            currentGameState.OvertakingAids.DrsDetected = drsStatus == DrsStatus.DrsDetected;
            currentGameState.OvertakingAids.DrsAvailable = drsStatus == DrsStatus.DrsAvailable;
            currentGameState.OvertakingAids.DrsEngaged = drsStatus == DrsStatus.DrsEnabled;
        }

        private void updateOpponentData(OpponentData opponentData, String driverName, int CostId, int racePosition, int completedLaps,
            int sector, float completedLapTime, int gear, float rpm,float steeringAngle, TrackSurfaces trackSurface, Boolean onPitRoad, bool previousIsApporchingPits,
            Boolean previousLapWasValid, Boolean currentLapValid, float sessionRunningTime,
            float distanceRoundTrack, Boolean sessionLengthIsTime, float sessionTimeRemaining,
            Boolean isRace, float airTemperature, float trackTempreture, float speed, float previousSpeed,
            float GameTimeWhenLastCrossedStartFinishLine,bool isNewLap, int carClassId, int carId, TimingData timingData, CarData.CarClass playerCarClass, Boolean IsTeamRacing, Boolean isRaining)
        {
            if (!opponentData.DriverRawName.Equals(driverName) && IsTeamRacing)
            {
                Console.WriteLine("Driver " + opponentData.DriverRawName + " has been swapped for " + driverName);
                opponentData.DriverRawName = driverName;
                opponentData.CostId = CostId;
                if (speechRecogniser != null) speechRecogniser.addNewOpponentName(driverName, opponentData.CarNumber);
                SoundCache.loadDriverNameSound(DriverNameHelper.getUsableDriverName(opponentData.DriverRawName));
            }
            Boolean validSpeed = true;
            if (speed > 500)
            {
                // faster than 500m/s (1000+mph) suggests the player has quit to the pit. Might need to reassess this as the data are quite noisy
                validSpeed = false;
                opponentData.Speed = 0;
                //Console.WriteLine(opponentData.DriverRawName + " invalidating lap based of car speed = " + speed + "m/s");
            }
            else if(speed < -20f)
            {
                opponentData.Speed = previousSpeed;
            }
            else
            {
                opponentData.Speed= speed;
            }
            if (opponentData.OverallPosition != racePosition)
            {
                opponentData.SessionTimeAtLastPositionChange = sessionRunningTime;
            }
            opponentData.IsNewLap = false;
            opponentData.OverallPosition = racePosition;
            opponentData.DistanceRoundTrack = distanceRoundTrack;
            opponentData.Gear = gear;
            opponentData.RPM = rpm;
            opponentData.SteeringWheelAngle = steeringAngle;
            //Check that previous state was IsApporchingPits, this includes the zone befor the pitlane(striped lines on track)
            opponentData.JustEnteredPits = previousIsApporchingPits && onPitRoad;
            
            if (sessionRunningTime > 10 && isRace && !opponentData.InPits && onPitRoad)
            {
                opponentData.NumPitStops++;
            }
            
            if (isNewLap)
            {
                if (opponentData.OpponentLapData.Count > 0)
                {
                    opponentData.CompleteLapThatMightHaveMissingSectorTimes(racePosition, sessionRunningTime, completedLapTime, 
                        completedLapTime > 1 && validSpeed, isRaining, trackTempreture, airTemperature, sessionLengthIsTime, sessionTimeRemaining, 3,
                        timingData, CarData.IsCarClassEqual(opponentData.CarClass, playerCarClass));
                    //Console.WriteLine(opponentData.DriverRawName + " New Lap: " + completedLaps + " time: " + TimeSpan.FromSeconds(completedLapTime).ToString(@"mm\:ss\.fff") + " lap valid: " + (completedLapTime > 1 && validSpeed) + " Updated From Live Data " + trackSurface); 
                }                
                opponentData.StartNewLap(completedLaps + 1, 
                    racePosition, onPitRoad || trackSurface == TrackSurfaces.InPitStall || trackSurface == TrackSurfaces.AproachingPits, // Tracks like brands hatch has the pit exit exactly where we transition from onPitRoad to AproachingPits
                    GameTimeWhenLastCrossedStartFinishLine, 
                    isRaining, 
                    trackTempreture, 
                    airTemperature);

                opponentData.IsNewLap = true;
            }
            if (opponentData.CurrentSectorNumber != sector)
            {
                if (opponentData.CurrentSectorNumber == 1 && sector == 2 || opponentData.CurrentSectorNumber == 2 && sector == 3)
                {               
                    if (opponentData.CurrentSectorNumber == 1 && opponentData.CarClass.carClassEnum == CarData.CarClassEnum.UNKNOWN_RACE)
                    {
                        // re-evaluate the car class
                        opponentData.CarClass = CarData.getCarClassForIRacingId(carClassId, carId);
                    }
                    opponentData.AddCumulativeSectorData(opponentData.CurrentSectorNumber, racePosition, -1, sessionRunningTime, currentLapValid && validSpeed, isRaining, trackTempreture, airTemperature);
                }
                opponentData.CurrentSectorNumber = sector;
            }
            opponentData.CompletedLaps = completedLaps;
            opponentData.InPits = onPitRoad || trackSurface == TrackSurfaces.InPitStall;
            if (opponentData.JustEnteredPits)
            {
                opponentData.setInLap();
            }
        }
         
        private static Dictionary<String, SessionType> sessionTypeMap = new Dictionary<String, SessionType>()
        {
            {"Offline Testing", SessionType.LonePractice},
            {"Practice", SessionType.Practice},
            {"Lone Practice", SessionType.LonePractice},
            {"Warmup", SessionType.Practice},
            {"Open Qualify", SessionType.Qualify},
            {"Lone Qualify", SessionType.PrivateQualify},
            {"Race", SessionType.Race}
        };

        public SessionType mapToSessionType(Object memoryMappedFileStruct)
        {
            String sessionString = (String)memoryMappedFileStruct;
            SessionType st = SessionType.Unavailable;
            if (sessionTypeMap.TryGetValue(sessionString, out st))
            {
                return st;
            }
            else
            {
                Console.WriteLine("Unrecognized SessionType: " + sessionString);
            }
            return SessionType.Unavailable;
        }
        SessionFlags previousSessionFlags;
        SessionStates previousSessionState;
        private SessionPhase mapToSessionPhase(SessionPhase lastSessionPhase, SessionStates sessionState, SessionType currentSessionType, float thisSessionRunningTime,
            int previousLapsCompleted, int laps, SessionFlags sessionFlags, bool IsFullCourseCautions, SafetyCarData currentSafetyCarData, SafetyCarData previousSafetyCarData, 
            int formationLapCount, float pitEntranceDistanceRoundTrack)
        {
           /* if (previousSessionFlags != sessionFlags)
            {
                Console.WriteLine("Previous sessionFlags: " + previousSessionFlags);
                Console.WriteLine("Current sessionFlags: " + sessionFlags);
                previousSessionFlags = sessionFlags; 
            }
            if (previousSessionState != sessionState)
            {
                Console.WriteLine("Previous sessionState: " + previousSessionState);
                Console.WriteLine("Current sessionState: " + sessionState);
                previousSessionState = sessionState;
            }*/
            if (currentSessionType == SessionType.Practice)
            {
                if (sessionState == SessionStates.CoolDown)
                {
                    return SessionPhase.Finished;
                }
                else if (sessionState.HasFlag(SessionStates.Checkered))
                {
                    return SessionPhase.Checkered;
                }
                else if (lastSessionPhase == SessionPhase.Unavailable)
                {
                    return SessionPhase.Countdown;
                }

                return SessionPhase.Green;
            }
            else if (currentSessionType == SessionType.LonePractice)
            {
                if (sessionState == SessionStates.CoolDown)
                {
                    return SessionPhase.Finished;
                }
                else if (sessionState.HasFlag(SessionStates.Checkered))
                {
                    return SessionPhase.Checkered;
                }
                else if (lastSessionPhase == SessionPhase.Unavailable)
                {
                    return SessionPhase.Countdown;
                }

                return SessionPhase.Green;
            }
            else if (currentSessionType == SessionType.Qualify)
            {
                if (sessionState == SessionStates.GetInCar)
                {
                    return SessionPhase.Unavailable;
                }
                if (sessionState == SessionStates.CoolDown)
                {
                    return SessionPhase.Finished;
                }
                else if (sessionState == SessionStates.Checkered)
                {
                    return SessionPhase.Checkered;
                }
                else if (!sessionFlags.HasFlag(SessionFlags.Green) && sessionFlags.HasFlag(SessionFlags.OneLapToGreen) && lastSessionPhase != SessionPhase.Green)
                {
                    return SessionPhase.Countdown;
                }
                else if (sessionFlags.HasFlag(SessionFlags.Green) || 
                    sessionState.HasFlag(SessionStates.Racing) /* covers mid-session join in hosted racing */)
                {
                    return SessionPhase.Green;
                }

                return lastSessionPhase;
            }
            else if (currentSessionType.HasFlag(SessionType.Race))
            {
                if (sessionState.HasFlag(SessionStates.Checkered) || sessionState.HasFlag(SessionStates.CoolDown))
                {
                    if (lastSessionPhase == SessionPhase.Green || lastSessionPhase == SessionPhase.FullCourseYellow
                         || lastSessionPhase == SessionPhase.Checkered)
                    {
                        if (playerCar.FinishStatus == Driver.FinishState.Finished)
                        {
                            Console.WriteLine("Finished - completed " + laps + " laps (was " + previousLapsCompleted + "), session running time = " +
                                thisSessionRunningTime);
                            return SessionPhase.Finished;
                        }
                        else if (sessionFlags.HasFlag(SessionFlags.Checkered))
                        {
                            if (lastSessionPhase != SessionPhase.Checkered)
                            {
                                Console.WriteLine("Checkered flag - completed " + laps + " laps (was " + previousLapsCompleted + "), session running time = " +
                                    thisSessionRunningTime);
                            }
                            return SessionPhase.Checkered;
                        }
                    }
                }
                else if (sessionState == SessionStates.GetInCar)
                {
                    if (playerCar.Live.TrackSurface != TrackSurfaces.NotInWorld)
                    {
                        return GameState.SessionPhase.Gridwalk;
                    }
                    return GameState.SessionPhase.Unavailable;
                }
                else if (sessionState == SessionStates.Warmup && !sessionFlags.HasFlag(SessionFlags.StartSet) && !sessionFlags.HasFlag(SessionFlags.StartGo)) 
                {
                    return SessionPhase.Gridwalk;
                }
                else if (sessionState.HasFlag(SessionStates.ParadeLaps) && !sessionFlags.HasFlag(SessionFlags.StartGo))                
                {
                    if ((lastSessionPhase == SessionPhase.Formation || lastSessionPhase == SessionPhase.Countdown) && 
                        (/*(currentSafetyCarData.Lap == formationLapCount && currentSafetyCarData.CurrentSector == 3 && pitEntranceDistanceRoundTrack == -1.0f) ||*/
                        (currentSafetyCarData.Lap == formationLapCount && currentSafetyCarData.DistanceRoundTrack >= pitEntranceDistanceRoundTrack && pitEntranceDistanceRoundTrack != -1.0f) ||
                         (previousSafetyCarData != null && (TrackSurfaces)currentSafetyCarData.TrackSurface == TrackSurfaces.AproachingPits && (TrackSurfaces)previousSafetyCarData.TrackSurface == TrackSurfaces.OnTrack)))
                    {
                        return SessionPhase.Countdown;                       
                    }
                    else if(lastSessionPhase != SessionPhase.Countdown)
                    {
                        return SessionPhase.Formation;
                    }                    
                }
                else if ((sessionFlags.HasFlag(SessionFlags.StartSet) && !sessionFlags.HasFlag(SessionFlags.StartGo)))
                {
                    return SessionPhase.Countdown;
                }
                else if ( IsFullCourseCautions && ((lastSessionPhase == SessionPhase.Green && sessionFlags.HasFlag(SessionFlags.CautionWaving)) || 
                    (lastSessionPhase == SessionPhase.FullCourseYellow && sessionFlags.HasFlag(SessionFlags.CautionWaving))))
                {
                    return SessionPhase.FullCourseYellow;
                }
                else if (sessionFlags.HasFlag(SessionFlags.Green) || 
                    sessionFlags.HasFlag(SessionFlags.StartGo) || SessionPhase.Unavailable == lastSessionPhase)
                {
                    return SessionPhase.Green;
                }
            }
            return lastSessionPhase;
        }

        private OpponentData createOpponentData(Driver driver, Boolean joinedMidSession, float trackLength)
        {
            String driverName = driver.Name;
            if (CrewChief.enableDriverNames)
            {
                if (speechRecogniser != null) 
                    speechRecogniser.addNewOpponentName(driverName, driver.CarNumber);
                SoundCache.loadDriverNameSound(DriverNameHelper.getUsableDriverName(driverName), joinedMidSession);
            }
            OpponentData opponentData = new OpponentData();
            opponentData.IsActive = true;
            opponentData.DriverRawName = driverName;
            opponentData.CostId = driver.CustId;
            opponentData.OverallPosition = driver.Live.PositionRaw;
            opponentData.CompletedLaps = driver.Live.LiveLapsCompleted;
            opponentData.DistanceRoundTrack = driver.Live.CorrectedLapDistance * trackLength;
            opponentData.DeltaTime = new DeltaTime(trackLength, opponentData.DistanceRoundTrack, (float)driver.Live.Speed, DateTime.UtcNow);
            opponentData.CarClass = CarData.getCarClassForIRacingId(driver.Car.CarClassId, driver.Car.CarId);
            opponentData.CurrentSectorNumber = driver.Live.CurrentSector;
            opponentData.CarNumber = driver.CarNumber;
            opponentData.CurrentTyres = mapToTyreType(driver.Live.CurrentTireCompound, opponentData.CarClass.carClassEnum);
            var tire_part = "";
            if (opponentData.CurrentTyres != TyreType.Uninitialized)
            {
                tire_part = " Tires " + opponentData.CurrentTyres + "(TireID )" + driver.Live.CurrentTireCompound;
            }
            Console.WriteLine("New driver " + driverName + " is using car class " +
                opponentData.CarClass.getClassIdentifier() + " (car ID " + driver.Car.CarId + ")" + tire_part);
            
            return opponentData;
        }
        private TyreType mapToTyreType(int tyreType, CarData.CarClassEnum carClassEnum)
        {

            if (carClassEnum == CarData.CarClassEnum.F1)
            {
                if (tyreType == 0)
                    return TyreType.Soft;
                else if(tyreType == 1) 
                     return TyreType.Medium;
                else if(tyreType == 2)
                    return TyreType.Hard;
                // Assume it will be 3 for wet once rain is enabled for F1
                else if(tyreType == 3)
                    return TyreType.Wet;
                else
                    return TyreType.Uninitialized;

            }
            else if(carClassEnum == CarData.CarClassEnum.INDYCAR)
            {
                if (tyreType == 0)
                    return TyreType.Primary;
                else if (tyreType == 1)
                    return TyreType.Alternate;
                // Assume it will be 2 for wet once rain is enabled for INDYCAR
                else if (tyreType == 2)
                    return TyreType.Wet;
                else
                    return TyreType.Uninitialized;
            }
            else // all other carclasses
            {
                if (tyreType == 0)
                    return TyreType.Primary;
                if (tyreType == 1)
                    return TyreType.Wet;
            }
            return TyreType.Uninitialized;
        }

        private FrozenOrderData GetFrozenOrderData(GameStateData currentGamestate, GameStateData previousGamestate, Sim shared)
        {
            FrozenOrderData frozenOrderData = new FrozenOrderData();
            if (!(currentGamestate.SessionData.SessionPhase == SessionPhase.Formation || currentGamestate.SessionData.SessionPhase == SessionPhase.FullCourseYellow)
                || shared.Telemetry.PaceMode == PaceMode.NotPacing)
            {
                if (currentGamestate.SessionData.SessionPhase == SessionPhase.Countdown)
                {
                    if (previousGamestate.SessionData.SessionPhase == SessionPhase.Gridwalk)
                    {
                        frozenOrderData.Phase = FrozenOrderPhase.FormationStanding;
                    }
                    else if (previousGamestate.SessionData.SessionPhase == SessionPhase.Countdown)
                    {
                        frozenOrderData.Phase = previousGamestate.FrozenOrderData.Phase;
                    }
                }
                return frozenOrderData;
            }

            if (currentGamestate.SafetyCarData.isOnTrack)
            {
                // this will probably break for series like F1 with single file rolling restarts,
                // if iRacing ever support restarts in offical road series.
                if (shared.Telemetry.PaceMode == PaceMode.SingleFileRestart)
                {
                    frozenOrderData.Phase = FrozenOrderPhase.FullCourseYellow;
                }
                else
                {
                    frozenOrderData.Phase = FrozenOrderPhase.Rolling;
                }
            }
            else if (previousGamestate.FrozenOrderData.Phase > FrozenOrderPhase.None)
            {
                frozenOrderData.Phase = FrozenOrderPhase.Rolling;
            }

            if (shared.Telemetry.CarIdxPaceRow == null || shared.Telemetry.CarIdxPaceRow.Length == 0)
            {
                // protects against old traces and players who haven't enabled enough cars
                return frozenOrderData;
            }
            if (shared.PaceCarPresent && (shared.PaceCar.Live.TrackSurface == TrackSurfaces.OnTrack || shared.PaceCar.Live.TrackSurface == TrackSurfaces.OffTrack))
            {
                frozenOrderData.SafetyCarSpeed = (float)shared.PaceCar.Live.Speed;
            }

            var columns = shared.Telemetry.CarIdxPaceColumn(currentGamestate.SessionData.SessionPhase == SessionPhase.Formation ? shared.SessionData.StartOnLeft : shared.SessionData.RestartOnLeft);

            int i = shared.Telemetry.PlayerCarIdx;
            FrozenOrderColumn our_line = columns[i];
            int our_row = shared.Telemetry.CarIdxPaceRow[i];
            var our_flags = shared.Telemetry.CarIdxPaceFlags[i];

            if (our_line == FrozenOrderColumn.None || our_row == -1)
            {
                return frozenOrderData;
            }

            var track_length = currentGamestate.SessionData.TrackDefinition.trackLength;

            if (our_row == 0)
            {
                // no reliable way to detect if we just jumped ahead a little bit or if the pace car came out
                // just behind us and we have to go the whole way round again, so play it safe and don't bother
                // trying to detect the "MoveToPole", "AllowToPass" or "Catchup" states.
                frozenOrderData.Action = FrozenOrderAction.StayInPole;
            }
            else if (currentGamestate.SessionData.SessionPhase == SessionPhase.Formation ||
                currentGamestate.PositionAndMotionData.CarSpeed > 10 &&
                currentGamestate.FlagData.fcyPhase > FullCourseYellowPhase.PENDING) {
                for (int j = 0; j < shared.Drivers.Count; j++)
                {
                    if (i == j) continue;
                    FrozenOrderColumn their_line = columns[j];
                    int their_row = shared.Telemetry.CarIdxPaceRow[j];
                    if (their_line != FrozenOrderColumn.None && their_row != -1 && their_line == our_line && their_row == our_row - 1)
                    {
                        var oppDriver = shared.Drivers[j];
                        if (currentGamestate.OpponentData.TryGetValue(oppDriver.Id.ToString(), out OpponentData opponent))
                        {
                            if (
                                // don't do this if we're ahead of the pace car (we might be taking a free pass). There is the corner
                                // case here where we actually need to let the pace car overtake us but there doesn't seem to be
                                // enough telemetry information to let us distinguish.
                                !currentGamestate.SafetyCarData.IsBehindWithinDistance(currentGamestate, 1, track_length / 2)
                                && opponent.IsBehindWithinDistance(currentGamestate, 1, track_length / 4))
                            {
                                frozenOrderData.Action = FrozenOrderAction.AllowToPass;
                            }
                            else if (((!currentGamestate.FlagData.isFullCourseYellow && currentGamestate.SessionData.SectorNumber == 3)
                                      || (shared.Telemetry.PaceMode == PaceMode.DoubleFileRestart && (!shared.PaceCarPresent || shared.PaceCar.Live.TrackSurface != TrackSurfaces.OnTrack))
                                      || shared.Telemetry.PaceMode == PaceMode.SingleFileRestart)
                                && currentGamestate.SessionData.SessionRunningTime > 30 // pitlane lineups can show up as sector 3
                                && opponent.IsAheadWithinDistance(currentGamestate, 30, track_length / 4)
                                && CarData.IsCarClassEqual(currentGamestate.carClass, opponent.CarClass))
                            {
                                // people might want this distance to be relaxed but if you fall back too much you're liable to be
                                // subject to a rules infringement protest, so Jim's just helping you help yourself. CatchUp does
                                // not mention the column, so should only be used when it is very obvious which car we need to catch
                                // (because we've been following them for a while).
                                frozenOrderData.Action = FrozenOrderAction.CatchUp;
                            }
                            else
                            {
                                frozenOrderData.Action = FrozenOrderAction.Follow;
                            }
                            frozenOrderData.CarNumberToFollowRaw = opponent.CarNumber;
                            frozenOrderData.DriverToFollowRaw = opponent.DriverRawName;
                        }
                    }
                }
            }

            if (shared.Telemetry.PaceMode == PaceMode.DoubleFileStart || shared.Telemetry.PaceMode == PaceMode.DoubleFileRestart)
            {
                // paceline=0 means same as pole
                // int our_position = 1 + 2 * our_row + shared.Telemetry.CarIdxPaceLine[i];
                // frozenOrderData.AssignedPosition = our_position;
                // it's actually better to NOT include assigned position, because it is informational only and
                // if it changes (e.g. cars ahead are not on the track) then it can cause the FrozenOrderMonitor
                // to invalidate messages.
                frozenOrderData.AssignedGridPosition = our_row + 1;
                frozenOrderData.AssignedColumn = our_line;
            }
            else
            {
                frozenOrderData.AssignedPosition = our_row + 1;
            }

            if (our_flags == PaceFlags.FreePass || our_flags == PaceFlags.WavedAround)
            {
                // if the telemetry doesn't update when we actually pass the pace car, we might need
                // to detect that and avoid setting this the moment we pass the pace car.
                frozenOrderData.Action = FrozenOrderAction.PassSafetyCar;
            }

            if (!frozenOrderData.PartialEquals(previousGamestate.FrozenOrderData))
            {
                Log.Debug(() => $"frozen order changed {frozenOrderData}");
            }

            return frozenOrderData;
        }
        private SafetyCarData GetSafetyCarData(SafetyCarData previousSafetyCarData, Driver safetyCar, float trackLength, bool safetyCarPresent)
        {
            SafetyCarData currentSafetyCarData = new SafetyCarData();
            if (!safetyCarPresent)
            {
                return currentSafetyCarData;
            }
            currentSafetyCarData.fcySafetyCarCallsEnabled = true;
            if (previousSafetyCarData != null)
            {
                currentSafetyCarData.Lap = previousSafetyCarData.Lap;
                currentSafetyCarData.PitEntranceDistanceRoundTrack = previousSafetyCarData.PitEntranceDistanceRoundTrack;
                if (safetyCar.Live.TrackSurface == TrackSurfaces.NotInWorld)
                {
                    currentSafetyCarData.TrackSurface = previousSafetyCarData.TrackSurface;
                    currentSafetyCarData.CurrentSector = previousSafetyCarData.CurrentSector;
                    currentSafetyCarData.DistanceRoundTrack = previousSafetyCarData.DistanceRoundTrack;
                }
                else
                {
                    currentSafetyCarData.TrackSurface = (int)safetyCar.Live.TrackSurface;
                    currentSafetyCarData.CurrentSector = safetyCar.Live.CurrentSector;
                    currentSafetyCarData.DistanceRoundTrack = safetyCar.Live.LapDistance * trackLength;
                }
                if (currentSafetyCarData.PitEntranceDistanceRoundTrack == -1.0f && (TrackSurfaces)previousSafetyCarData.TrackSurface == TrackSurfaces.OnTrack
                    && (TrackSurfaces)currentSafetyCarData.TrackSurface == TrackSurfaces.AproachingPits)
                {
                    currentSafetyCarData.PitEntranceDistanceRoundTrack = safetyCar.Live.LapDistance * trackLength;
                    //Console.WriteLine("currentSafetyCarData.PitEntranceDistanceRoundTrack = " + currentSafetyCarData.PitEntranceDistanceRoundTrack);
                }
            }
            else
            {
                currentSafetyCarData.TrackSurface = (int)safetyCar.Live.TrackSurface;
                currentSafetyCarData.CurrentSector = safetyCar.Live.CurrentSector;
                currentSafetyCarData.DistanceRoundTrack = safetyCar.Live.LapDistance * trackLength;
            }
            currentSafetyCarData.isOnTrack = (TrackSurfaces)currentSafetyCarData.TrackSurface == TrackSurfaces.OnTrack;
            /*if (prevTrackSurface != (TrackSurfaces)currentSafetyCarData.TrackSurface)
            {
                Console.WriteLine("shared.PaceCar.Live.TrackSurface = " + (TrackSurfaces)currentSafetyCarData.TrackSurface);
                prevTrackSurface = (TrackSurfaces)currentSafetyCarData.TrackSurface;
            }*/
            if (currentSafetyCarData.isOnTrack)
            {
                if (previousSafetyCarData != null && previousSafetyCarData.CurrentSector == 3 && currentSafetyCarData.CurrentSector == 1)
                {
                    currentSafetyCarData.Lap++;
                    //Console.WriteLine("currentGameState.SafetyCarData.Lap = " + currentSafetyCarData.Lap);
                }
            }
            else
            {
                currentSafetyCarData.Lap = 0;
            }
            return currentSafetyCarData;
        }
        private bool IsEvenNumber(int number)
        {
            return (number % 2 == 0);
        }
    }
}
