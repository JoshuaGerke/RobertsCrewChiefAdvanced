using CrewChiefV4.ACC.accData;

using ksBroadcastingNetwork;
using ksBroadcastingNetwork.Structs;

using ksBroadcastingTestClient;
using ksBroadcastingTestClient.Broadcasting;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;


namespace CrewChiefV4.ACC
{
    public class ACCSharedMemoryReader : GameDataReader
    {
        private MemoryMappedFile memoryMappedPhysicsFile;
        private int sharedmemoryPhysicssize;
        private byte[] sharedMemoryPhysicsReadBuffer;
        private GCHandle handlePhysics;

        private MemoryMappedFile memoryMappedGraphicFile;
        private int sharedmemoryGraphicsize;
        private byte[] sharedMemoryGraphicReadBuffer;
        private GCHandle handleGraphic;

        private MemoryMappedFile memoryMappedStaticFile;
        private int sharedmemoryStaticsize;
        private byte[] sharedMemoryStaticReadBuffer;

        private GCHandle handleStatic;

        private static UdpUpdateViewModel udpUpdateViewModel;

        private Boolean initialised = false;
        private List<ACCStructWrapper> dataToDump;
        private ACCStructWrapper[] dataReadFromFile = null;
        private int dataReadFromFileIndex = 0;
        private String lastReadFileName = null;
        private ACCStructWrapper previousAACStructWrapper; // Used when no data is comming in. EG: game is paused
        private float ackPenalityTime;

        private DateTime nextScheduleRequestCars = DateTime.MinValue;

        private SPageFileCrewChief mostRecentUDPData = null;

        // carId is typically 0 - 50 in offline session, and 1000+ in online sessions. Allow 10000 entries in this array
        private const int MAX_CAR_ID = 9999;
        private static DataForSync[] syncData = new DataForSync[MAX_CAR_ID + 1];

        private DateTime nextUDPDebugDueAt = DateTime.MinValue;

        public class ACCStructWrapper
        {
            public long ticksWhenRead;
            public ACCShared data;
        }

        public static void clearSyncData()
        {
            Array.Clear(ACCSharedMemoryReader.syncData, 0, MAX_CAR_ID + 1);
        }

        public override void DumpRawGameData()
        {
            if (dumpToFile && dataToDump != null && dataToDump.Count > 0 && filenameToDump != null)
            {
                foreach (ACCStructWrapper wrapper in dataToDump)
                {
                    wrapper.data.accChief.vehicle = getPopulatedDriverDataArray(wrapper.data.accChief.vehicle);
                }
                SerializeObject(dataToDump.ToArray<ACCStructWrapper>(), filenameToDump);
            }
        }

        public override void ResetGameDataFromFile()
        {
            dataReadFromFileIndex = 0;
        }

        public override TracedGameData ReadGameDataFromFile(String filename, int pauseBeforeStart)
        {
            if (dataReadFromFile == null || filename != lastReadFileName)
            {
                dataReadFromFileIndex = 0;
                var filePathResolved = Utilities.ResolveDataFile(this.dataFilesPath, filename);
                dataReadFromFile = DeSerializeObject<ACCStructWrapper[]>(filePathResolved);
                lastReadFileName = filename;
                Thread.Sleep(pauseBeforeStart);
            }
            if (dataReadFromFile != null && dataReadFromFile.Length > dataReadFromFileIndex)
            {
                ACCStructWrapper structWrapperData = dataReadFromFile[dataReadFromFileIndex];
                dataReadFromFileIndex++;
                return new TracedGameData
                {
                    GameData = structWrapperData, 
                    TicksWhenRead = structWrapperData.ticksWhenRead
                };
            }
            else
            {
                return null;
            }
        }

        protected override Boolean InitialiseInternal()
        {
            if (dumpToFile)
            {
                dataToDump = new List<ACCStructWrapper>();
            }
            lock (this)
            {
                if (!initialised)
                {
                    try
                    {
                        memoryMappedPhysicsFile = MemoryMappedFile.OpenExisting(accConstant.SharedMemoryNamePhysics, MemoryMappedFileRights.Read);
                        sharedmemoryPhysicssize = Marshal.SizeOf(typeof(SPageFilePhysics));
                        sharedMemoryPhysicsReadBuffer = new byte[sharedmemoryPhysicssize];

                        memoryMappedGraphicFile = MemoryMappedFile.OpenExisting(accConstant.SharedMemoryNameGraphic, MemoryMappedFileRights.Read);
                        sharedmemoryGraphicsize = Marshal.SizeOf(typeof(SPageFileGraphic));
                        sharedMemoryGraphicReadBuffer = new byte[sharedmemoryGraphicsize];

                        memoryMappedStaticFile = MemoryMappedFile.OpenExisting(accConstant.SharedMemoryNameStatic, MemoryMappedFileRights.Read);
                        sharedmemoryStaticsize = Marshal.SizeOf(typeof(SPageFileStatic));
                        sharedMemoryStaticReadBuffer = new byte[sharedmemoryStaticsize];

                        udpUpdateViewModel = new UdpUpdateViewModel(UserSettings.GetUserSettings().getString(accConstant.SettingMachineIpAddress));

                        initialised = true;
                        Console.WriteLine("Initialised Assetto Corsa Competizione shared memory");
                    }
                    catch
                    {
                        initialised = false;
                    }
                }
                return initialised;
            }
        }
        public static String getNameFromBytes(byte[] name)
        {
            return Encoding.Unicode.GetString(name);
        }

        public override Object ReadGameData(Boolean forSpotter)
        {
            lock (this)
            {
                ACCShared accShared = new ACCShared();
                if (!initialised)
                {
                    if (!InitialiseInternal())
                    {
                        throw new GameDataReadException("Failed to initialise shared memory");
                    }
                }
                try
                {
                    using (var sharedMemoryStreamView = memoryMappedStaticFile.CreateViewStream(0, sharedmemoryStaticsize, MemoryMappedFileAccess.Read))
                    {
                        BinaryReader _SharedMemoryStream = new BinaryReader(sharedMemoryStreamView);
                        sharedMemoryStaticReadBuffer = _SharedMemoryStream.ReadBytes(sharedmemoryStaticsize);
                        handleStatic = GCHandle.Alloc(sharedMemoryStaticReadBuffer, GCHandleType.Pinned);
                        accShared.accStatic = (SPageFileStatic)Marshal.PtrToStructure(handleStatic.AddrOfPinnedObject(), typeof(SPageFileStatic));
                        handleStatic.Free();
                    }
                    using (var sharedMemoryStreamView = memoryMappedGraphicFile.CreateViewStream(0, sharedmemoryGraphicsize, MemoryMappedFileAccess.Read))
                    {
                        BinaryReader _SharedMemoryStream = new BinaryReader(sharedMemoryStreamView);
                        sharedMemoryGraphicReadBuffer = _SharedMemoryStream.ReadBytes(sharedmemoryGraphicsize);
                        handleGraphic = GCHandle.Alloc(sharedMemoryGraphicReadBuffer, GCHandleType.Pinned);
                        accShared.accGraphic = (SPageFileGraphic)Marshal.PtrToStructure(handleGraphic.AddrOfPinnedObject(), typeof(SPageFileGraphic));
                        handleGraphic.Free();
                    }

                    using (var sharedMemoryStreamView = memoryMappedPhysicsFile.CreateViewStream(0, sharedmemoryPhysicssize, MemoryMappedFileAccess.Read))
                    {
                        BinaryReader _SharedMemoryStream = new BinaryReader(sharedMemoryStreamView);
                        sharedMemoryPhysicsReadBuffer = _SharedMemoryStream.ReadBytes(sharedmemoryPhysicssize);
                        handlePhysics = GCHandle.Alloc(sharedMemoryPhysicsReadBuffer, GCHandleType.Pinned);
                        accShared.accPhysics = (SPageFilePhysics)Marshal.PtrToStructure(handlePhysics.AddrOfPinnedObject(), typeof(SPageFilePhysics));
                        handlePhysics.Free();
                    }

                    accShared.accChief = new SPageFileCrewChief();

                    ACCStructWrapper structWrapper = new ACCStructWrapper();
                    DateTime now = DateTime.UtcNow;
                    structWrapper.ticksWhenRead = now.Ticks;
                    
                    if (now > nextScheduleRequestCars && !forSpotter)
                    {
                        System.Diagnostics.Debug.WriteLine("Requesting new car list.");
                        nextScheduleRequestCars = now.AddSeconds(10);
                        udpUpdateViewModel.ClientPanelVM.RequestEntryList();
                        return previousAACStructWrapper ?? structWrapper;
                    }

                    structWrapper.data = accShared;
                    // Send the previous state for accPhysics if the game is paused to prevent bogus track temp and other warnings on unpausing
                    // see if we have any non-zero data from the physics MMF
                    Boolean hasPhysicsData = accShared.accPhysics.airTemp != 0
                        || accShared.accPhysics.roadTemp != 0
                        || accShared.accPhysics.fuel != 0
                        || accShared.accPhysics.heading != 0
                        || accShared.accPhysics.pitch != 0;
                    if (!hasPhysicsData && previousAACStructWrapper != null && previousAACStructWrapper.data != null)
                    {
                        structWrapper.data.accPhysics = previousAACStructWrapper.data.accPhysics;
                    }

                    structWrapper.data.accStatic.SET_FROM_UDP_isTimedRace = udpUpdateViewModel.SessionInfoVM.RemainingTime.TotalMilliseconds > 0 ? 1 : 0;

                    // New penality?
                    if (structWrapper.data.accGraphic.penaltyTime != ackPenalityTime)
                    {

                        if (structWrapper.data.accGraphic.penaltyTime > ackPenalityTime && structWrapper.data.accGraphic.flag == AC_FLAG_TYPE.AC_NO_FLAG) // Penality flag not supported yet
                            structWrapper.data.accGraphic.flag = AC_FLAG_TYPE.AC_PENALTY_FLAG;

                        ackPenalityTime = structWrapper.data.accGraphic.penaltyTime;
                    }

                    if (forSpotter && this.mostRecentUDPData != null && this.mostRecentUDPData.vehicle.Length > 0)
                    {
                        for (int i = 0; i < this.mostRecentUDPData.vehicle.Length; i++)
                        {
                            int indexInCoordsArray = Array.IndexOf(structWrapper.data.accGraphic.carIDs, this.mostRecentUDPData.vehicle[i].carId);
                            if (indexInCoordsArray > -1 && indexInCoordsArray < structWrapper.data.accGraphic.carCoordinates.Length)
                            {
                                this.mostRecentUDPData.vehicle[i].worldPosition = structWrapper.data.accGraphic.carCoordinates[indexInCoordsArray];
                            }
                        }
                        structWrapper.data.accChief = this.mostRecentUDPData;
                    }
                    else
                    {
                        // Populate data from the ACC UDP info. We have to lock it because data can be updated while we read it
                        BroadcastingEvent[] events = udpUpdateViewModel.BroadcastingVM.EventVM.GetEvents();

                        AC_SESSION_TYPE sessionType;
                        switch (udpUpdateViewModel.SessionInfoVM.SessionType)
                        {
                            case RaceSessionType.Practice:
                                sessionType = AC_SESSION_TYPE.AC_PRACTICE;
                                break;
                            case RaceSessionType.Qualifying:
                                sessionType = AC_SESSION_TYPE.AC_QUALIFY;
                                break;
                            case RaceSessionType.Race:
                                sessionType = AC_SESSION_TYPE.AC_RACE;
                                break;
                            case RaceSessionType.Hotlap:
                            case RaceSessionType.HotlapSuperpole:
                            case RaceSessionType.Hotstint:
                            case RaceSessionType.Superpole:
                                sessionType = AC_SESSION_TYPE.AC_HOTLAP;
                                break;
                            default:
                                sessionType = AC_SESSION_TYPE.AC_HOTLAP;
                                break;
                        }
                        structWrapper.data.accGraphic.session = sessionType;
                        switch (udpUpdateViewModel.SessionInfoVM.Phase)
                        {
                            case SessionPhase.SessionOver:
                                structWrapper.data.accChief.SessionPhase = GameState.SessionPhase.Checkered;
                                break;
                            case SessionPhase.PreSession:
                                structWrapper.data.accChief.SessionPhase = GameState.SessionPhase.Countdown;
                                break;
                            case SessionPhase.PostSession:
                                structWrapper.data.accChief.SessionPhase = GameState.SessionPhase.Finished;
                                break;
                            case SessionPhase.PreFormation:
                            case SessionPhase.FormationLap:
                                structWrapper.data.accChief.SessionPhase = GameState.SessionPhase.Formation;
                                break;
                            case SessionPhase.Session:
                                structWrapper.data.accChief.SessionPhase = GameState.SessionPhase.Green;
                                break;
                            default:
                                structWrapper.data.accChief.SessionPhase = GameState.SessionPhase.Unavailable;
                                break;
                        }
                        structWrapper.data.accChief.isInternalMemoryModuleLoaded = 1;
                        structWrapper.data.accChief.trackLength = udpUpdateViewModel.BroadcastingVM.TrackVM?.TrackMeters ?? 0;
                        structWrapper.data.accChief.rainLevel = udpUpdateViewModel.SessionInfoVM.RainLevel;

                        // until we check that a driver's carId is also in the accGraphic.carIDs array, we don't know how long this list will be:
                        LinkedList<accVehicleInfo> activeVehicles = new LinkedList<accVehicleInfo>();
                        structWrapper.data.accChief.vehicle = new accVehicleInfo[udpUpdateViewModel.BroadcastingVM.Cars.Count];
                        List<float> distancesTravelled = new List<float>();
                        // get the player vehicle first and put this at the front of the list
                        var playerVehicle = getPlayerVehicle(udpUpdateViewModel.BroadcastingVM.Cars, accShared.accGraphic.playerCarID,
                            accShared.accStatic, accShared.accGraphic.position);
                        if (playerVehicle != null)
                        {
                            accVehicleInfo playerCar = createCar(1, playerVehicle, structWrapper.data.accGraphic.carIDs, structWrapper.data.accGraphic.carCoordinates);
                            activeVehicles.AddFirst(playerCar);
                            distancesTravelled.Add(playerCar.lapCount + playerCar.spLineLength);

                            // only add a car to our data set if it exists in the UDP data and the shared memory car IDs array
                            foreach (CarViewModel car in udpUpdateViewModel.BroadcastingVM.Cars)
                            {
                                if (car != playerVehicle && structWrapper.data.accGraphic.carIDs.Contains(car.CarIndex))
                                {
                                    accVehicleInfo opponentCar = createCar(0, car, structWrapper.data.accGraphic.carIDs, structWrapper.data.accGraphic.carCoordinates);
                                    activeVehicles.AddLast(opponentCar);
                                    distancesTravelled.Add(opponentCar.lapCount + opponentCar.spLineLength);
                                }
                            }
                            // now set the accVehicle array from our list of vehicles that we've deemed to be 'active'
                            structWrapper.data.accChief.vehicle = activeVehicles.ToArray();

                            if (sessionType == AC_SESSION_TYPE.AC_RACE)
                            {
                                List<float> sortedDistances = new List<float>(distancesTravelled);
                                sortedDistances.Sort();
                                sortedDistances.Reverse();
                                for (var i = 0; i < distancesTravelled.Count; i++)
                                {
                                    int positionFromSpline = sortedDistances.IndexOf(distancesTravelled[i]) + 1;
                                    structWrapper.data.accChief.vehicle[i].carRealTimeLeaderboardPosition = positionFromSpline;
                                }
                            }
                            // save the populated driver data so we can reuse it when reading for the spotter
                            this.mostRecentUDPData = structWrapper.data.accChief;
                        }
                    }

                    if (!forSpotter && currentlyTracingGameData && dataToDump != null)
                    {
                        dataToDump.Add(structWrapper);
                    }

                    previousAACStructWrapper = structWrapper;

                    if (now > nextUDPDebugDueAt)
                    {
                        Console.WriteLine("UDP stats:\n Incoming - " + 
                            string.Join(" ", BroadcastingNetworkProtocol.inboundMessageTypeCounts) + "\n Outgoing - " +
                            string.Join(" ", BroadcastingNetworkProtocol.outboundMessageTypeCounts));
                        nextUDPDebugDueAt = now.AddSeconds(60);
                    }
                    return structWrapper;
                }
                catch (AggregateException e1)
                {
                    Console.WriteLine("Inner exception:" + e1.Message + " " + e1.InnerException.StackTrace);
                    throw new GameDataReadException(e1.InnerException.Message, e1.InnerException);
                }
                catch (Exception e2)
                {
                    throw new GameDataReadException(e2.Message, e2);
                }
            }
        }

        public string getCarModel(int carModelEnum)
        {
            // 4 classes in ACC - GT4 (class enum 50 - 61), Porsche Cup (class enum 9) and Huracan Super Trofeo (class enum 18). Everything else is GT3
            if (carModelEnum == 9 || carModelEnum == 28)
            {
                return "porsche_911_cup";
            }
            else if (carModelEnum == 18 || carModelEnum == 29)
            {
                return "ks_lamborghini_huracan_st"; // this puts the ST in the GTE class
            }
            else if (carModelEnum >= 50 && carModelEnum <= 61)
            {
                return "GT4";
            }
            else if (carModelEnum == 27)
            {
                return "ks_acc_488_challenge";
            }
            else if (carModelEnum == 26)
            {
                return "ks_bmw_m2_cup";
            }
            return "GT3";
        }

        private accVehicleInfo createCar(int carIsPlayerVehicle, CarViewModel car, int[] carIds, accVec3[] carPositions)
        {
            var currentLap = car.CurrentLap;
            var lastLap = car.LastLap;
            var bestLap = car.BestLap;

            // we only ever add the player to position 0:
            string carDriverName;
            if (car.CurrentDriver != null)
            {
                carDriverName = car.CurrentDriver.DisplayName;
            }
            else
            {
                carDriverName = car.Drivers.Count > 0 ? car.Drivers.First().DisplayName : "";
            }

            // get the position in the CarIDs array
            float x_coord = 0;
            float z_coord = 0;
            int indexInCoordsArray = Array.IndexOf(carIds, car.CarIndex);
            if (indexInCoordsArray > -1 && indexInCoordsArray < carPositions.Length)
            {
                accVec3 carPosition = carPositions[indexInCoordsArray];
                x_coord = carPosition.x;
                z_coord = carPosition.z;
            }

            int carIndex = car.CarIndex;
            int bestLapTime = (bestLap?.IsValid ?? false) ? bestLap.LaptimeMS ?? 0 : 0;
            int bestSplit1Time = bestLap?.Split1MS ?? 0;
            int bestSplit2Time = bestLap?.Split2MS ?? 0;
            int bestSplit3Time = bestLap?.Split3MS ?? 0;
            int currentLapInvalid = (currentLap?.IsValid ?? false) ? 0 : 1;
            int currentLapTime = currentLap?.LaptimeMS ?? 0;
            int lastLapTime = (lastLap?.IsValid ?? false) ? lastLap.LaptimeMS ?? 0 : 0;
            int lastSplit1Time = lastLap?.Split1MS ?? 0;
            int lastSplit2Time = lastLap?.Split2MS ?? 0;
            int lastSplit3Time = lastLap?.Split3MS ?? 0;
            float splineLength = car.SplinePosition;
            int lapsCompleted = car.Laps;

            if (carIndex > MAX_CAR_ID)
            {
                // can't do the data sync on car IDs ouside the array, want if we're in debug (this will spam)
                if (CrewChief.Debug.RunningUnderDebugger)
                {
                    Console.WriteLine("CarID " + carIndex + " exceeds max dataSync size of " + MAX_CAR_ID);
                }
            }
            else if (syncStartlineData(car.CarIndex, lapsCompleted, splineLength,
                bestLapTime, bestSplit1Time, bestSplit2Time, bestSplit3Time,
                lastLapTime, lastSplit1Time, lastSplit2Time, lastSplit3Time,
                currentLapTime, currentLapInvalid))
            {
                // ask the syncData for the correct values:
                DataForSync dataForSync = syncData[carIndex];
                bestLapTime = dataForSync.getCorrectedBestLapTime(bestLapTime);
                bestSplit1Time = dataForSync.getCorrectedBestSplit1Time(bestSplit1Time);
                bestSplit2Time = dataForSync.getCorrectedBestSplit2Time(bestSplit2Time);
                bestSplit3Time = dataForSync.getCorrectedBestSplit3Time(bestSplit3Time);
                lastLapTime = dataForSync.getCorrectedLastLapTime(lastLapTime);
                lastSplit1Time = dataForSync.getCorrectedLastSplit1Time(lastSplit1Time);
                lastSplit2Time = dataForSync.getCorrectedLastSplit2Time(lastSplit2Time);
                lastSplit3Time = dataForSync.getCorrectedLastSplit3Time(lastSplit3Time);
                lapsCompleted = dataForSync.getCorrectedLapCount(lapsCompleted);
                splineLength = dataForSync.getCorrectedSpline(splineLength);
                currentLapInvalid = dataForSync.getCurrentLapInvalid(currentLapInvalid);
            }
            return new accVehicleInfo
            {
                bestLapMS = bestLapTime,
                bestSplit1TimeMS = bestSplit1Time,
                bestSplit2TimeMS = bestSplit2Time,
                bestSplit3TimeMS = bestSplit3Time,
                carId = carIndex,
                carLeaderboardPosition = car.Position,
                carModel = getCarModel(car.CarModelEnum),
                carRealTimeLeaderboardPosition = car.Position,  /* don't be tempted to use TrackPosition here, it's always zero */
                currentLapInvalid = currentLapInvalid,
                currentLapTimeMS = currentLapTime,
                isPlayerVehicle = carIsPlayerVehicle,
                driverName = carDriverName,
                isCarInPitEntry = car.CarLocation == CarLocationEnum.PitEntry ? 1 : 0,
                isCarInPitExit = car.CarLocation == CarLocationEnum.PitExit ? 1 : 0,
                isCarInPitlane = (car.CarLocation == CarLocationEnum.Pitlane) ? 1 : 0,
                isConnected = 1,
                lapCount = lapsCompleted,
                lastLapTimeMS = lastLapTime,
                lastSplit1TimeMS = lastSplit1Time,
                lastSplit2TimeMS = lastSplit2Time,
                lastSplit3TimeMS = lastSplit3Time,
                speedMS = car.Kmh * 0.277778f,
                spLineLength = splineLength,
                worldPosition = new accVec3 { x = x_coord, z = z_coord },
                raceNumber = car.RaceNumber
            };            
        }

        // returns true if we're in the magic sync zone and we're waiting for data
        private bool syncStartlineData(int carId, int lapCountFromData, float splineFromData, 
            int bestLapFromData, int bestSplit1TimeFromData, int bestSplit2TimeFromData, int bestSplit3TimeFromData,
            int lastLapFromData, int lastSplit1TimeFromData, int lastSplit2TimeFromData, int lastSplit3TimeFromData,
            int currentLapFromData, int currentLapInvalid)
        {
            if (splineFromData >= 0.93 || splineFromData <= 0.07)
            {
                DataForSync currentCarSyncData = ACCSharedMemoryReader.syncData[carId];
                if (currentCarSyncData == null)
                {
                    syncData[carId] = new DataForSync(lapCountFromData, splineFromData,
                        bestLapFromData, bestSplit1TimeFromData, bestSplit2TimeFromData, bestSplit3TimeFromData,
                        lastLapFromData, lastSplit1TimeFromData, lastSplit2TimeFromData, lastSplit3TimeFromData,
                        currentLapFromData, currentLapInvalid);
                    return true;
                }
                else
                {
                    return currentCarSyncData.update(lapCountFromData, splineFromData, bestLapFromData, lastLapFromData, currentLapFromData, currentLapInvalid);
                }
            }
            else
            {
                // clear the cached sync data for cars which are out of the zone
                ACCSharedMemoryReader.syncData[carId] = null;
                return false;
            }
        }

        class DataForSync
        {
            int lapCountBefore;
            float splineBefore;

            int bestLapTimeBefore;
            int bestSplit1TimeBefore;
            int bestSplit2TimeBefore;
            int bestSplit3TimeBefore;

            int lastLapTimeBefore;
            int lastSplit1TimeBefore;
            int lastSplit2TimeBefore;
            int lastSplit3TimeBefore;

            int currentLapTimeBefore;

            int currentLapInvalid;

            bool waitingForSpline = true;
            bool waitingForLapCount = true;
            bool waitingForLaptimes = true;

            public DataForSync(int lapCountBefore, float splineBefore,
                int bestLapTimeBefore,
                int bestSplit1TimeBefore,
                int bestSplit2TimeBefore,
                int bestSplit3TimeBefore,
                int lastLapTimeBefore,
                int lastSplit1TimeBefore,
                int lastSplit2TimeBefore,
                int lastSplit3TimeBefore,
                int currentLapTimeBefore,
                int currentLapInvalid)
            {
                this.lapCountBefore = lapCountBefore;
                this.splineBefore = splineBefore;
                this.bestLapTimeBefore = bestLapTimeBefore;
                this.bestSplit1TimeBefore = bestSplit1TimeBefore;
                this.bestSplit2TimeBefore = bestSplit2TimeBefore;
                this.bestSplit3TimeBefore = bestSplit3TimeBefore;

                this.lastLapTimeBefore = lastLapTimeBefore;
                this.lastSplit1TimeBefore = lastSplit1TimeBefore;
                this.lastSplit2TimeBefore = lastSplit2TimeBefore;
                this.lastSplit3TimeBefore = lastSplit3TimeBefore;

                this.currentLapTimeBefore = currentLapTimeBefore;

                this.currentLapInvalid = currentLapInvalid;

                if (currentLapInvalid == 1 && lastLapTimeBefore == 0)
                {
                    // if we're on an invalid lap and our lastLapTime is zero then finishing this lap won't update any of the laptime data,
                    // so don't wait for new lap data in this case:
                    this.waitingForLaptimes = false;
                }
            }
            public bool update(int lapCountFromTick, float splineFromTick, int bestLapTimeFromTick, int lastLapTimeFromTick, int currentLapTimeFromTick, int currentLapInvalid)
            {
                // special case: if our lap is still valid but we've got no currentLap time, and it's lap zero, we're on the formation lap
                bool onFormationLap = lapCountFromTick == 0 && currentLapTimeFromTick == 0 && currentLapInvalid == 0;
                if (onFormationLap)
                {
                    waitingForSpline = false;
                    waitingForLapCount = false;
                    waitingForLaptimes = false;
                }
                // don't do any work if we're not waiting. This instance will hang around in the syncData until the car is passed spline position 0.07,
                // it'll be checked on each tick until then.
                if (!waitingForAnyUpdates())
                {
                    return false;
                }
                if (this.waitingForLapCount && this.lapCountBefore < lapCountFromTick)
                {
                    // trivial case - lap count has incremented
                    this.waitingForLapCount = false;
                }
                if (this.waitingForSpline && splineFromTick < 0.93)
                {
                    // 0.93 cause that's our cutoff but the splineFromTick will typically be 0.0-something in this case
                    this.waitingForSpline = false;
                }
                if (this.waitingForLaptimes)
                {
                    if (currentLapInvalid == 1)
                    {
                        // save this if it's true, allows us to prevent transition from invalid to valid before we cross the line
                        this.currentLapInvalid = currentLapInvalid;
                    }
                    // any change in the best or last means we have new laptime data (here we assume best / last lap updates happen in the same tick)
                    // any reduction in the current laptime means we've reset
                    if (bestLapTimeFromTick != this.bestLapTimeBefore
                        || lastLapTimeFromTick != this.lastLapTimeBefore
                        || this.currentLapTimeBefore > currentLapTimeFromTick)
                    {
                        this.waitingForLaptimes = false;
                    }
                }
                return waitingForAnyUpdates();
            }
            public float getCorrectedSpline(float splineFromTick)
            {
                if (waitingForAnyUpdates())
                {
                    // allow spline position to update until it starts to decrease. It'll typically be a small number but anything lower than the
                    // magic zone start point is a decrease, so we check for that. This means we effectively pause the spline updates at 1 once that.
                    // reset to 0.something
                    return splineFromTick >= 0.93 ? splineFromTick : 1;
                }
                return splineFromTick;
            }
            public bool waitingForAnyUpdates()
            {
                // this is the guard - if any of the synchronized data items haven't been updated (or reset to zero in the case of the spline),
                // return false so the caller can ask this instance what the corrected values should be.
                return waitingForLapCount || waitingForLaptimes || waitingForSpline;
            }
            public int getCurrentLapInvalid(int currentLapInvalidFromTick)
            {
                // if we're waiting for updates, return the saved value so we don't allow this to reset before the new lap starts
                return waitingForAnyUpdates() ? this.currentLapInvalid : currentLapInvalidFromTick;
            }
            // laptime / lapcount stuff is trivial - get the previous best / last if we're waiting for any updates
            public int getCorrectedLapCount(int lapCountFromTick)
            {
                return waitingForAnyUpdates() ? this.lapCountBefore : lapCountFromTick;
            }
            public int getCorrectedLastLapTime(int lastLapTimeFromTick)
            {
                return waitingForAnyUpdates() ? this.lastLapTimeBefore : lastLapTimeFromTick;
            }
            public int getCorrectedLastSplit1Time(int lastSplit1TimeFromTick)
            {
                return waitingForAnyUpdates() ? this.lastSplit1TimeBefore : lastSplit1TimeFromTick;
            }
            public int getCorrectedLastSplit2Time(int lastSplit2TimeFromTick)
            {
                return waitingForAnyUpdates() ? this.lastSplit2TimeBefore : lastSplit2TimeFromTick;
            }
            public int getCorrectedLastSplit3Time(int lastSplit3TimeFromTick)
            {
                return waitingForAnyUpdates() ? this.lastSplit3TimeBefore : lastSplit3TimeFromTick;
            }
            public int getCorrectedBestLapTime(int bestLapTimeFromTick)
            {
                return waitingForAnyUpdates() ? this.bestLapTimeBefore : bestLapTimeFromTick;
            }
            public int getCorrectedBestSplit1Time(int bestSplit1TimeFromTick)
            {
                return waitingForAnyUpdates() ? this.bestSplit1TimeBefore : bestSplit1TimeFromTick;
            }
            public int getCorrectedBestSplit2Time(int bestSplit2TimeFromTick)
            {
                return waitingForAnyUpdates() ? this.bestSplit2TimeBefore : bestSplit2TimeFromTick;
            }
            public int getCorrectedBestSplit3Time(int bestSplit3TimeFromTick)
            {
                return waitingForAnyUpdates() ? this.bestSplit3TimeBefore : bestSplit3TimeFromTick;
            }
        }

        private CarViewModel getPlayerVehicle(List<CarViewModel> cars, int carId, SPageFileStatic accStatic, int positionFromSharedMem)
        {
            foreach (var car in cars)
            {
                if (car.CarIndex == carId)
                {
                    return car;
                }
            }
            int positionDiff = int.MaxValue;
            CarViewModel bestMatch = null;
            foreach (var car in cars)
            {
                foreach (var driver in car.Drivers)
                {
                    if (accStatic.playerName + " " + accStatic.playerSurname == driver.DisplayName)
                    {
                        // check the positions match
                        if (positionFromSharedMem == car.Position)
                        {
                            // yay, this is probably the player. Probably :(
                            return car;
                        }
                        else 
                        {
                            // the names match but the position doesn't so we need to check the rest of the array
                            int thisPositionDiff = Math.Abs(positionFromSharedMem - car.Position);
                            if (thisPositionDiff < positionDiff)
                            {
                                positionDiff = thisPositionDiff;
                                bestMatch = car;
                            }
                        }
                    }
                }
            }

            return bestMatch;
        }

        private accVehicleInfo[] getPopulatedDriverDataArray(accVehicleInfo[] raw)
        {
            List<accVehicleInfo> populated = new List<accVehicleInfo>();
            if (raw != null && raw.Count() > 0)
            {
                foreach (accVehicleInfo rawData in raw)
                {
                    if (rawData.carLeaderboardPosition > 0 || rawData.isPlayerVehicle == 1)
                    {
                        populated.Add(rawData);
                    }
                }
                if (populated.Count == 0)
                {
                    populated.Add(raw[0]);
                }
            }
            return populated.ToArray();
        }

        public override void Dispose()
        {
            if (memoryMappedPhysicsFile != null)
            {
                try
                {
                    memoryMappedPhysicsFile.Dispose();
                    memoryMappedPhysicsFile = null;
                }
                catch (Exception e) {Log.Exception(e);}
            }
            if (memoryMappedGraphicFile != null)
            {
                try
                {
                    memoryMappedGraphicFile.Dispose();
                    memoryMappedGraphicFile = null;
                }
                catch (Exception e) {Log.Exception(e);}
            }
            if (memoryMappedStaticFile != null)
            {
                try
                {
                    memoryMappedStaticFile.Dispose();
                    memoryMappedStaticFile = null;
                }
                catch (Exception e) {Log.Exception(e);}
            }
            if (udpUpdateViewModel != null)
            {
                try
                {
                    udpUpdateViewModel.Shutdown();
                }
                catch (Exception e) {Log.Exception(e);}
            }
            initialised = false;
        }
    }
}