using CrewChiefV4.Events;
using CrewChiefV4.Events.PubSub;
using CrewChiefV4.GameState;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CrewChiefV4.Overlay
{
    // class containing raw game data which hasn't been through the mapper
    public class OverlayDataSource
    {
        public static X_AXIS_TYPE xAxisType = X_AXIS_TYPE.DISTANCE;

        // only used for iRacing now
        private static int previousDataPointLapCompleted = int.MinValue;

        private static Dictionary<GameEnum, List<OverlaySubscription>> subscribedData = new Dictionary<GameEnum, List<OverlaySubscription>>();

        private static Dictionary<string, LinkedList<OverlayLapData>> data = new Dictionary<string, LinkedList<OverlayLapData>>();

        // same data as above but for the player's fastest lap in the session
        private static float lapTimeForBestLapData = float.MaxValue;
        private static Dictionary<string, OverlayLapData> bestLapData = new Dictionary<string, OverlayLapData>();

        // opponent data stuff
        public static Boolean mapOpponentData = false;
        public static float bestOpponentLap = float.MaxValue;
        public static string bestOpponentLapDriverName = "";
        // the isNewLap flag can come later than the start of the lap so we need to copy over some data points from the previous lap
        // when starting a new one. These dicts keep track of this data so we don't need to keep deriving it

        // per-drivername dict of the previous point's lap distances
        private static Dictionary<string, float> lastPointLapDistances = new Dictionary<string, float>();
        // per-drivername dict of the of points where we want to start copying from the previous lap when we finally do start a new lap
        private static Dictionary<string, int> startPointsForCopyFromPreviousLap = new Dictionary<string, int>();
        private static Dictionary<string, Dictionary<string, OverlayLapData>> opponentsCurrentLapData = new Dictionary<string, Dictionary<string, OverlayLapData>>();
        private static Dictionary<string, OverlayLapData> opponentBestLapData = new Dictionary<string, OverlayLapData>();
        public static Dictionary<string, OverlayDataType> opponentDataFields = new Dictionary<string, OverlayDataType>();

        private static LinkedList<OverlayLapData> worldPositionData = new LinkedList<OverlayLapData>();
        private static OverlayLapData bestLapWorldPositionData = null;

        // sector position stuff:
        public static float sector1End = -1;
        public static float sector2End = -1;

        // need a better name for this - it's for "previous lap" and "next lap". By default we always want the last-1 lap (because the last
        // lap in the data is the lap the player is currently on, so is incomplete
        public static int countBack = 1;

        public static void loadChartSubscriptions()
        {
            subscribedData.Clear();
            clearData();
            Dictionary<GameEnum, List<OverlaySubscription>> allSubscriptions = JsonConvert.DeserializeObject<Dictionary<GameEnum, List<OverlaySubscription>>>(
                getFileContents(getSubscriptionsFileLocation()));
            List<OverlaySubscription> subscriptions = new List<OverlaySubscription>();
            List<OverlaySubscription> gameSpecificAndCommonSubscriptions;
            if (allSubscriptions.TryGetValue(CrewChief.gameDefinition.gameEnum, out gameSpecificAndCommonSubscriptions))
            {
                subscriptions.AddRange(gameSpecificAndCommonSubscriptions);
            }
            subscriptions.AddRange(CommonSubscriptions.getApplicableCommonSubscriptions(subscriptions));
            OverlayDataSource.setDataFieldsForGame(CrewChief.gameDefinition.gameEnum, subscriptions);
        }
        private static String getFileContents(String fullFilePath)
        {
            StringBuilder jsonString = new StringBuilder();
            StreamReader file = null;
            try
            {
                file = new StreamReader(fullFilePath);
                String line;
                while ((line = file.ReadLine()) != null)
                {
                    if (!line.Trim().StartsWith("#"))
                    {
                        jsonString.AppendLine(line);
                    }
                }
                return jsonString.ToString();
            }
            catch (Exception e)
            {
                Log.Exception(e, "Error reading file " + fullFilePath + ": ");
            }
            finally
            {
                if (file != null)
                {
                    file.Close();
                }
            }
            return null;
        }
        private static void updateUserChartSubscriptionsFile()
        {
            String userFilePath = DataFiles.chart_subscriptions;
            String defaultFilePath = DataFiles.default_chart_subscriptions;
            Dictionary<GameEnum, List<OverlaySubscription>> allUserSubscriptions;
            try
            {
                allUserSubscriptions = JsonConvert.DeserializeObject<Dictionary<GameEnum, List<OverlaySubscription>>>(
                    getFileContents(userFilePath));
            }
            catch (Exception e)
            {
                Log.Exception( e, "Error parsing user chart subscriptions: ");
                allUserSubscriptions = new Dictionary<GameEnum, List<OverlaySubscription>>();
            }

            Dictionary<GameEnum, List<OverlaySubscription>> allDefaultSubscriptions = JsonConvert.DeserializeObject<Dictionary<GameEnum, List<OverlaySubscription>>>(
                getFileContents(defaultFilePath));

            bool addedNewSubscription = false;
            foreach (var subscription in allDefaultSubscriptions)
            {
                List<OverlaySubscription> userSub = null;
                // first check if this is a game we already have data for
                if (allUserSubscriptions.TryGetValue(subscription.Key, out userSub))
                {
                    foreach (var overlay in subscription.Value)
                    {
                        // is it a new entry in the list
                        if (userSub.FirstOrDefault(os => os.voiceCommandFragment_Internal == overlay.voiceCommandFragment_Internal) == null)
                        {
                            allUserSubscriptions[subscription.Key].Add(overlay);
                            addedNewSubscription = true;
                        }
                    }
                }
                else // we add the entire list
                {
                    allUserSubscriptions[subscription.Key] = subscription.Value;
                    addedNewSubscription = true;
                }
            }
            if (addedNewSubscription)
            {
                Console.WriteLine("Saving new default chart subscriptions to user subscription file");
                saveSubscriptionsSettingsFile(allDefaultSubscriptions, userFilePath);
            }
        }
        public static void saveSubscriptionsSettingsFile(Dictionary<GameEnum, List<OverlaySubscription>> settings, String fileName)
        {
            JsonFiles.WriteFile(fileName, settings);
        }

        public static String getSubscriptionsFileLocation()
        {
            String path = DataFiles.chart_subscriptions;

            if (File.Exists(path))
            {
                // update the file if it exists.
                updateUserChartSubscriptionsFile();
                Console.WriteLine("Loading user-configured chart subscriptions from Documents/CrewChiefV4/ folder");
                return path;
            }
            // make sure we save a copy to the user config directory
            else if (!File.Exists(path))
            {
                try
                {
                    File.Copy(DataFiles.default_chart_subscriptions, path);
                    Console.WriteLine("Loading user-configured chart subscriptions from Documents/CrewChiefV4/ folder");
                    return path;
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error copying default chart subscriptions file to user dir : ");
                    Console.WriteLine("Loading default chart subscriptions from installation folder");
                    return DataFiles.default_chart_subscriptions;
                }
            }
            else
            {
                Console.WriteLine("Loading default chart subscriptions from installation folder");
                return DataFiles.default_chart_subscriptions;
            }
        }

        public static void clearData()
        {
            foreach (var lapdata in data)
            {
                lapdata.Value.Clear();
            }
            bestLapData.Clear();
            lapTimeForBestLapData = float.MaxValue;
            bestOpponentLap = float.MaxValue;
            bestOpponentLapDriverName = "";
            lastPointLapDistances.Clear();
            startPointsForCopyFromPreviousLap.Clear();
            opponentBestLapData.Clear();
            sector1End = -1;
            sector2End = -1;
            worldPositionData.Clear();
            bestLapWorldPositionData = null;
        }

        public static void setDataFieldsForGame(GameEnum gameEnum, List<OverlaySubscription> fields)
        {
            subscribedData[gameEnum] = new List<OverlaySubscription>();
            HashSet<string> addedFields = new HashSet<string>();
            foreach (OverlaySubscription subscription in fields)
            {
                string fieldName = subscription.isGroup ? subscription.groupMemberIds.ToString() : subscription.fieldName;
                if (addedFields.Contains(subscription.fieldName))
                {
                    Console.WriteLine("Game specific chart data subscription for field " + fieldName + " has already been added");
                    continue;
                }
                else
                {
                    addedFields.Add(fieldName);
                    subscribedData[gameEnum].Add(subscription);
                    if (!subscription.isGroup)
                    {
                        data[fieldName] = new LinkedList<OverlayLapData>();
                    }
                }
            }
        }

        public static List<OverlaySubscription> getOverlaySubscriptions()
        {
            List<OverlaySubscription> overlaySubscriptions;
            if (CrewChief.gameDefinition != null && subscribedData.TryGetValue(CrewChief.gameDefinition.gameEnum, out overlaySubscriptions))
            {
                return overlaySubscriptions;
            }
            else
            {
                return new List<OverlaySubscription>();
            }
        }

        public static List<string> getAllChartVoiceCommands()
        {
            List<string> commands = new List<string>();
            List<OverlaySubscription> overlaySubscriptions;
            if (CrewChief.gameDefinition != null && subscribedData.TryGetValue(CrewChief.gameDefinition.gameEnum, out overlaySubscriptions))
            {
                foreach (OverlaySubscription subscription in overlaySubscriptions)
                {
                    commands.AddRange(subscription.getVoiceCommands());
                }
            }
            return commands;
        }
        
        public static OverlaySubscription getOverlaySubscriptionForId(string id)
        {
            if (CrewChief.gameDefinition != null && subscribedData.ContainsKey(CrewChief.gameDefinition.gameEnum))
            {
                foreach (OverlaySubscription subscription in subscribedData[CrewChief.gameDefinition.gameEnum])
                {
                    if (subscription.id == id)
                    {
                        return subscription;
                    }
                }
            }
            return null;
        }

        public static string getLapTimeForBestLapString()
        {
            if (lapTimeForBestLapData > 0 && lapTimeForBestLapData < 10000)
            {
                return TimeSpan.FromSeconds(lapTimeForBestLapData).ToString(@"mm\:ss\.fff");
            }
            return "--:--:---";
        }

        public static List<DataPoint> getWorldPositions(SeriesMode seriesMode)
        {
            if (seriesMode == SeriesMode.LAST_LAP && worldPositionData != null && worldPositionData.Last != null)
            {
                try
                {
                    LinkedListNode<OverlayLapData> node = getCorrectLastLapNode(worldPositionData);
                    return node.Value.dataPoints;
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error getting track map data: ");
                }
            }
            else if (seriesMode == SeriesMode.BEST_LAP && bestLapWorldPositionData != null)
            {
                return bestLapWorldPositionData.dataPoints;
            }
            return null;
        }

        private static LinkedListNode<OverlayLapData> getCorrectLastLapNode(LinkedList<OverlayLapData> data)
        {
            int count = 0;
            LinkedListNode<OverlayLapData> node = data.Last;
            while (count < OverlayDataSource.countBack)
            {
                if (node != null && node.Previous != null)
                {
                    node = node.Previous;
                    count++;
                }
                else
                {
                    OverlayDataSource.countBack = count;
                    break;
                }
            }
            return node;
        }

        public static string getLapTimeForLastLapString()
        {
            try
            {
                var sub = data.FirstOrDefault();
                if (sub.Value != null && sub.Value.Count > 1)
                {
                    LinkedListNode<OverlayLapData> node = getCorrectLastLapNode(sub.Value);
                    if (node.Value.lapTime > 0)
                    {
                        return TimeSpan.FromSeconds(node.Value.lapTime).ToString(@"mm\:ss\.fff") + ", lap " + node.Value.lapNumber;
                    }
                }
            }
            catch (Exception)
            {
                return "--:--:---";
            }
            return "--:--:---";
        }
        public static List<Tuple<float, float[]>> getDataForLap(Tuple<OverlaySubscription, SeriesMode> overlaySubscription, SectorToShow sectorToShow)
        {
            List<Tuple<float, float[]>> seriesData = new List<Tuple<float, float[]>>();
            OverlayLapData overlayLapData = null;
            if (overlaySubscription.Item2 == SeriesMode.LAST_LAP)
            {
                if (data.ContainsKey(overlaySubscription.Item1.fieldName))
                {
                    LinkedList<OverlayLapData> allLapData = data[overlaySubscription.Item1.fieldName];
                    if (allLapData.Count > 1)
                    {
                        try
                        {
                            LinkedListNode<OverlayLapData> node = getCorrectLastLapNode(allLapData);
                            overlayLapData = node.Value;
                            OverlayController.clampXMaxTo = overlayLapData.dataPoints.Max(point => point.distanceRoundTrack);
                        }
                        catch (Exception e)
                        {
                            Log.Exception(e, "Error getting lap data: ");
                        }
                    }
                }
            }
            else if (overlaySubscription.Item2 == SeriesMode.BEST_LAP)
            {
                if (OverlayDataSource.bestLapData.ContainsKey(overlaySubscription.Item1.fieldName))
                {
                    try
                    {
                        overlayLapData = OverlayDataSource.bestLapData[overlaySubscription.Item1.fieldName];
                        OverlayController.clampXMaxTo = overlayLapData.dataPoints.Max(point => point.distanceRoundTrack);
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Error getting best lap data: ");
                    }
                }
            }
            else if (overlaySubscription.Item2 == SeriesMode.OPPONENT_BEST_LAP)
            {
                if (OverlayDataSource.opponentBestLapData.ContainsKey(overlaySubscription.Item1.opponentDataFieldname))
                {
                    try
                    {
                        overlayLapData = OverlayDataSource.opponentBestLapData[overlaySubscription.Item1.opponentDataFieldname];
                        OverlayController.clampXMaxTo = overlayLapData.dataPoints.Max(point => point.distanceRoundTrack);
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Error getting opponent lap data: ");
                    }
                }
            }
            if (overlayLapData != null)
            {
                if (overlaySubscription.Item1.dataSeriesType == DataSeriesType.HISTOGRAM)
                {
                    seriesData.AddRange(overlayLapData.convertHistogramSeries(sectorToShow, overlaySubscription.Item1.overlayDataType, overlaySubscription.Item1.histogramSteps));
                }
                else
                {
                    seriesData.AddRange(overlayLapData.convertTimeSeries(sectorToShow, overlaySubscription.Item1.overlayDataType, OverlayController.x_min, OverlayController.x_max));
                }
            }
            return seriesData;
        }

        public static void addIRacingData(GameStateData previousGameState, GameStateData currentGameState)
        {
            if (previousGameState == null
                || (!CrewChief.recordChartTelemetryDuringRace && currentGameState.SessionData.SessionType == SessionType.Race))
            {
                return;
            }            
            if (previousGameState.SessionData.SectorNumber != currentGameState.SessionData.SectorNumber && !currentGameState.PitData.InPitlane)
            {
                if (sector1End == -1 && previousGameState.SessionData.SectorNumber == 1 && currentGameState.SessionData.SectorNumber == 2
                    && currentGameState.PositionAndMotionData.DistanceRoundTrack > 0)
                {
                    sector1End = currentGameState.PositionAndMotionData.DistanceRoundTrack;
                }
                else if (sector2End == -1 && previousGameState.SessionData.SectorNumber == 2 && currentGameState.SessionData.SectorNumber == 3
                    && currentGameState.PositionAndMotionData.DistanceRoundTrack > 0)
                {
                    sector2End = currentGameState.PositionAndMotionData.DistanceRoundTrack;
                }
            }
            CrewChiefV4.iRacing.iRacingSharedMemoryReader.iRacingStructWrapper structWrapper = (CrewChiefV4.iRacing.iRacingSharedMemoryReader.iRacingStructWrapper)currentGameState.rawGameData;
            int lapsCompleted = structWrapper.data.Driver.Live.Lap;
            if (lapsCompleted == -1)
            {
                return;
            }
            if (lapsCompleted > previousDataPointLapCompleted)
            {
                foreach (OverlaySubscription field in subscribedData[GameEnum.IRACING])
                {
                    if (field.isGroup || field.isDiskData || !data.TryGetValue(field.fieldName, out var overlayData))
                    {
                        continue;
                    }
                    List<DataPoint> dataFromPrevious = new List<DataPoint>();
                    if (overlayData.Count > 0)
                    {
                        dataFromPrevious = overlayData.Last.Value.dataPoints.Where(d => d.distanceRoundTrack < currentGameState.PositionAndMotionData.DistanceRoundTrack).ToList();
                    }
                    overlayData.AddLast(new OverlayLapData(lapsCompleted, dataFromPrevious, 0));
                }
                previousDataPointLapCompleted = lapsCompleted;
            }

            if (currentGameState.SessionData.IsNewLap && currentGameState.SessionData.LapTimePrevious > 1.0f)
            {
                // get the last lap time if it's valid                
                Boolean copyBestLapData = false;
                float lastLapTime = currentGameState.SessionData.LapTimePrevious;
                if (previousDataPointLapCompleted != int.MinValue && (OverlayDataSource.lapTimeForBestLapData == -1 || lastLapTime < OverlayDataSource.lapTimeForBestLapData))
                {
                    copyBestLapData = true;
                    OverlayDataSource.lapTimeForBestLapData = lastLapTime;
                }
                foreach (OverlaySubscription field in subscribedData[GameEnum.IRACING])
                {
                    if (field.isGroup || field.isDiskData || !data.TryGetValue(field.fieldName, out var overlayData))
                    {
                        continue;
                    }
                    if (overlayData.Count > 0)
                    {
                        if (copyBestLapData)
                        {
                            OverlayDataSource.bestLapData[field.fieldName] = overlayData.Last.Previous.Value;
                        }
                        overlayData.Last.Previous.Value.lapTime = lastLapTime;
                    }
                }

            }
            foreach (OverlaySubscription field in subscribedData[GameEnum.IRACING])
            {
                if (field.isGroup || field.isDiskData || !data.TryGetValue(field.fieldName, out var overlayData))
                {
                    continue;
                }
                if (overlayData.Count == 0)
                {
                    overlayData.AddLast(new OverlayLapData(lapsCompleted));
                }
                OverlayLapData lapData = overlayData.Last.Value;
                previousDataPointLapCompleted = lapsCompleted;
                float distanceRoundTrack = currentGameState == null ? 0 : currentGameState.PositionAndMotionData.DistanceRoundTrack;
                object dataSource = field.isRawField ? (object)structWrapper.data.Telemetry : (object)currentGameState;
                lapData.addDataPoint(new DataPoint(lapsCompleted, distanceRoundTrack,
                        ReflectionGameStateAccessor.getPropertyValue(dataSource, field.fieldName), field.overlayDataType, structWrapper.ticksWhenRead, currentGameState.SessionData.SectorNumber));
            }

            if (mapOpponentData)
            {
                addOpponentData(currentGameState.carClass.carClassEnum, currentGameState.OpponentData, structWrapper.ticksWhenRead);
            }
        }

        public static void addGameData(GameStateData previousGameState, GameStateData currentGameState)
        {
            if (previousGameState == null ||
                (!CrewChief.recordChartTelemetryDuringRace && currentGameState.SessionData.SessionType == SessionType.Race))
            {
                return;
            }
            if (previousGameState.SessionData.SectorNumber != currentGameState.SessionData.SectorNumber && !currentGameState.PitData.InPitlane)
            {
                if (sector1End == -1 && previousGameState.SessionData.SectorNumber == 1 && currentGameState.SessionData.SectorNumber == 2
                    && currentGameState.PositionAndMotionData.DistanceRoundTrack > 0)
                {
                    sector1End = currentGameState.PositionAndMotionData.DistanceRoundTrack;
                }
                else if (sector2End == -1 && previousGameState.SessionData.SectorNumber == 2 && currentGameState.SessionData.SectorNumber == 3
                    && currentGameState.PositionAndMotionData.DistanceRoundTrack > 0)
                {
                    sector2End = currentGameState.PositionAndMotionData.DistanceRoundTrack;
                }
            }
            bool addNewDataContainer = worldPositionData.Last == null 
                || currentGameState.SessionData.IsNewLap
                || (previousGameState.PitData.IsInGarage && currentGameState.PitData.InPitlane)
                || (GameEventBool.PlayerTookControl);
            Boolean copyBestLapData = false;

            // get the last lap time if it's valid        
            if (currentGameState.SessionData.IsNewLap && currentGameState.SessionData.PreviousLapWasValid && !currentGameState.PitData.InPitlane 
                && currentGameState.SessionData.LapTimePrevious > 0 &&
                    (OverlayDataSource.lapTimeForBestLapData == -1 || currentGameState.SessionData.LapTimePrevious < OverlayDataSource.lapTimeForBestLapData))
                {
                    copyBestLapData = true;
                    OverlayDataSource.lapTimeForBestLapData = currentGameState.SessionData.LapTimePrevious;
                }
            
            if (addNewDataContainer)
            {
                foreach (OverlaySubscription field in subscribedData[CrewChief.gameDefinition.gameEnum])
                {
                    if (field.isGroup || field.isDiskData || !data.TryGetValue(field.fieldName, out var overlayData))
                    {
                        continue;
                    }
                    if (overlayData.Last != null)
                    {
                        if (copyBestLapData)
                        {
                            OverlayDataSource.bestLapData[field.fieldName] = overlayData.Last.Value;
                        }
                        overlayData.Last.Value.lapTime = currentGameState.SessionData.LapTimePrevious;
                    }
                    overlayData.AddLast(new OverlayLapData(currentGameState.SessionData.CompletedLaps));
                }
                if (copyBestLapData && worldPositionData.Last != null)
                {
                    bestLapWorldPositionData = worldPositionData.Last.Value;
                }
                worldPositionData.AddLast(new OverlayLapData(currentGameState.SessionData.CompletedLaps));
            }
            float distanceRoundTrack = currentGameState.PositionAndMotionData.DistanceRoundTrack;
            float[] worldPosition = currentGameState.PositionAndMotionData.WorldPosition == null ? new float[] { 0, 0 } :
                new float[] { currentGameState.PositionAndMotionData.WorldPosition[0], currentGameState.PositionAndMotionData.WorldPosition[1], currentGameState.PositionAndMotionData.WorldPosition[2] };
            worldPositionData.Last.Value.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps,
                distanceRoundTrack, worldPosition, OverlayDataType.FLOAT_3, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
            foreach (OverlaySubscription field in subscribedData[CrewChief.gameDefinition.gameEnum])
            {
                if (field.isGroup || field.isDiskData || !data.TryGetValue(field.fieldName, out var overlayData))
                {
                    continue;
                }         
                OverlayLapData lapData = overlayData.Last.Value;                
                if (field.isRawField)
                {
                    switch (CrewChief.gameDefinition.gameEnum)
                    {
                        case GameEnum.RACE_ROOM:
                            lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(((CrewChiefV4.RaceRoom.R3ESharedMemoryReader.R3EStructWrapper)currentGameState.rawGameData).data, field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                            break;
                        case GameEnum.PCARS2:
                        case GameEnum.PCARS3:
                            lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(((CrewChiefV4.PCars2.PCars2SharedMemoryReader.PCars2StructWrapper)currentGameState.rawGameData).data, field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                            break;
                        case GameEnum.AMS2:
                            lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(((CrewChiefV4.AMS2.AMS2SharedMemoryReader.AMS2StructWrapper)currentGameState.rawGameData).data, field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                            break;
                        case GameEnum.PCARS_32BIT:
                        case GameEnum.PCARS_64BIT:
                        case GameEnum.PCARS2_NETWORK:
                            lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(((CrewChiefV4.PCars.PCarsSharedMemoryReader.PCarsStructWrapper)currentGameState.rawGameData).data, field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                            break;
                        case GameEnum.RF1:
                            lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(((CrewChiefV4.rFactor1.RF1SharedMemoryReader.RF1StructWrapper)currentGameState.rawGameData), field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                            break;
                        case GameEnum.RF2_64BIT:
                        case GameEnum.LMU:
                            lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(((CrewChiefV4.rFactor2.RF2_LMUSharedMemoryReader.RF2_LMUStructWrapper)currentGameState.rawGameData), field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                            break;
                        case GameEnum.ACC:
                            lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(((CrewChiefV4.ACC.ACCSharedMemoryReader.ACCStructWrapper)currentGameState.rawGameData).data, field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                            break;
                        case GameEnum.ASSETTO_32BIT:
                        case GameEnum.ASSETTO_64BIT:
                        case GameEnum.ASSETTO_128CARS:
                        case GameEnum.ASSETTO_64BIT_RALLY:
                        case GameEnum.ASSETTO_PRO:
                            lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(((CrewChiefV4.assetto.ACSSharedMemoryReader.ACSStructWrapper)currentGameState.rawGameData).data, field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                            break;
                        case GameEnum.ASSETTO_EVO:
                            lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(((CrewChiefV4.ACE.ACESharedMemoryReader.ACEStructWrapper)currentGameState.rawGameData).data, field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    lapData.addDataPoint(new DataPoint(currentGameState.SessionData.CompletedLaps, distanceRoundTrack,
                        ReflectionGameStateAccessor.getPropertyValue(currentGameState, field.fieldName), field.overlayDataType, currentGameState.Ticks, currentGameState.SessionData.SectorNumber));
                }
            }
            if (mapOpponentData)
            {
                addOpponentData(currentGameState.carClass.carClassEnum, currentGameState.OpponentData, currentGameState.Ticks);
            }
        }

        public static void addIRacingDiskData(iRSDKSharp.iRacingDiskSDK diskTelemetry, GameStateData gameState)
        {
            float trackLength = 1f;
            if(gameState != null && gameState.SessionData.TrackDefinition != null)
            {
                trackLength = gameState.SessionData.TrackDefinition.trackLength;
            }
            foreach(var carSpeedData in data["PositionAndMotionData.CarSpeed"])
            {
                int lapNumber = carSpeedData.lapNumber;
                if (diskTelemetry.lapOffsets.TryGetValue(carSpeedData.lapNumber,out var offset))
                {
                    List<float> lapDistance = diskTelemetry.GetDataForLap("LapDistPct", lapNumber).ConvertAll(o => (float)o);
                    List<int> sessionTick = diskTelemetry.GetDataForLap("SessionTick", lapNumber).ConvertAll(o => (int)o);
                    List<double> Lat = diskTelemetry.GetDataForLap("Lat", lapNumber).ConvertAll(o => (double)o);
                    List<double> Lon = diskTelemetry.GetDataForLap("Lon", lapNumber).ConvertAll(o => (double)o);
                    List<float> Alt = diskTelemetry.GetDataForLap("Alt", lapNumber).ConvertAll(o => (float)o);
                    if (Lat.Count > 0 && Lon.Count > 0)
                    {
                        worldPositionData.AddLast(new OverlayLapData(lapNumber));
                        for (int i = 0; i < Lat.Count; i++)
                        {
                            float lapDistanceMetres = lapDistance[i] * trackLength;
                            int sectorNumber = -1;
                            if (sector1End > 0 && sector2End > 0)
                            {
                                sectorNumber = lapDistanceMetres < sector1End ? 1 : lapDistanceMetres < sector2End ? 2 : 3;
                            }
                            double[] point = SomeNiceStuffFromDavidTuckerFromiRacing.LatLonCVToPoint(Lat[i], Lon[i], Lat[0], Lon[0], gameState.SessionData.TrackDefinition.iracingTrackNorthOffset);
                            float[] worldPosition = new float[] {(float)point[0], (float)Alt[i], (float)point[1] };
                            worldPositionData.Last.Value.addDataPoint(new DataPoint(lapNumber, lapDistanceMetres, worldPosition, OverlayDataType.FLOAT_3, sessionTick[i], sectorNumber));
                        }
                    }
                           
                    foreach (OverlaySubscription field in subscribedData[GameEnum.IRACING])
                    {
                        if (field.isGroup || !field.isDiskData || !data.TryGetValue(field.fieldName, out var overlayData))
                        {
                            continue;
                        }                        
                        OverlayLapData lapData = overlayData.AddLast(new OverlayLapData(lapNumber)).Value;
                        lapData.lapTime = carSpeedData.lapTime;
                        List<object> telemetryLapData = diskTelemetry.GetDataForLap(field.fieldName, lapNumber);
                        if (telemetryLapData.Count > 0)
                        {
                            for (int i = 0; i < telemetryLapData.Count; i++)
                            {
                                float lapDistanceMetres = lapDistance[i] * trackLength;
                                int sectorNumber = -1;
                                if (sector1End > 0 && sector2End > 0)
                                {
                                    sectorNumber = lapDistanceMetres < sector1End ? 1 : lapDistanceMetres < sector2End ? 2 : 3;
                                }
                                lapData.addDataPoint(new DataPoint(lapNumber, lapDistanceMetres, telemetryLapData[i], field.overlayDataType, sessionTick[i], sectorNumber));
                            }
                            if (lapData.lapTime > 0 && (OverlayDataSource.lapTimeForBestLapData == -1 || lapData.lapTime <= OverlayDataSource.lapTimeForBestLapData))
                            {
                                OverlayDataSource.lapTimeForBestLapData = lapData.lapTime;
                                OverlayDataSource.bestLapData[field.fieldName] = lapData;
                                bestLapWorldPositionData = worldPositionData.Last.Value;
                            }                            
                        }
                    }
                }
            }
        }

        private static void addOpponentData(CarData.CarClassEnum carClassEnum, Dictionary<string, OpponentData> allOpponentData, long ticks)
        {
            foreach (KeyValuePair<string, OpponentData> entry in allOpponentData)
            {
                string driverName = entry.Key;
                OpponentData opponentData = entry.Value;
                if (CrewChief.forceSingleClass || opponentData.CarClass.carClassEnum == carClassEnum)
                {
                    Dictionary<string, OverlayLapData> thisOpponentCurrentLapData;
                    if (!opponentsCurrentLapData.TryGetValue(driverName, out thisOpponentCurrentLapData))
                    {
                        thisOpponentCurrentLapData = new Dictionary<string, OverlayLapData>();
                        opponentsCurrentLapData.Add(driverName, thisOpponentCurrentLapData);
                    }
                    if (opponentData.IsNewLap)
                    {
                        if (opponentData.LastLapValid && opponentData.LastLapTime > 0 &&
                            opponentData.LastLapTime < bestOpponentLap && thisOpponentCurrentLapData.Count > 0)
                        {
                            opponentBestLapData = cloneLapData(thisOpponentCurrentLapData);
                            bestOpponentLap = opponentData.LastLapTime;
                            bestOpponentLapDriverName = opponentData.DriverRawName;
                        }
                        int startPointForCopyFromPrevious;
                        // get the point where we think we should be starting our previous lap copy from:
                        OverlayDataSource.startPointsForCopyFromPreviousLap.TryGetValue(driverName, out startPointForCopyFromPrevious);
                        foreach (KeyValuePair<string, OverlayDataType> field in OverlayDataSource.opponentDataFields)
                        {
                            int lapNumber = 0;
                            List<DataPoint> previousData = null;
                            if (thisOpponentCurrentLapData.ContainsKey(field.Key))
                            {
                                lapNumber = thisOpponentCurrentLapData[field.Key].lapNumber + 1;
                                previousData = thisOpponentCurrentLapData[field.Key].dataPoints;
                            }
                            thisOpponentCurrentLapData[field.Key] = new OverlayLapData(lapNumber, previousData, startPointForCopyFromPrevious);
                        }
                    }
                    else
                    {
                        float thisPointLapDistance = opponentData.DistanceRoundTrack;
                        float lastPointLapDistance;
                        Boolean isLapStartPoint = false;

                        if (opponentData.CompletedLaps > 0 &&
                            OverlayDataSource.lastPointLapDistances.TryGetValue(driverName, out lastPointLapDistance)
                            && lastPointLapDistance - thisPointLapDistance > 300)
                        {
                            // this driver's lap distance has gone down by a lot, so assume this is the point where he crossed the line 
                            // and we need to copy all the data after this point into the next lap
                            isLapStartPoint = true;
                        }
                        OverlayDataSource.lastPointLapDistances[driverName] = thisPointLapDistance;
                        foreach (KeyValuePair<string, OverlayDataType> field in OverlayDataSource.opponentDataFields)
                        {
                            OverlayLapData dataForThisField;
                            if (!thisOpponentCurrentLapData.TryGetValue(field.Key, out dataForThisField))
                            {
                                dataForThisField = new OverlayLapData(0);
                                thisOpponentCurrentLapData[field.Key] = dataForThisField;
                            }
                            dataForThisField.addDataPoint(new DataPoint(opponentData.CompletedLaps, opponentData.DistanceRoundTrack,
                                ReflectionGameStateAccessor.getPropertyValue(opponentData, field.Key), field.Value, ticks, opponentData.CurrentSectorNumber));                  
                            if (isLapStartPoint)
                            {
                                startPointsForCopyFromPreviousLap[driverName] = dataForThisField.dataPoints.Count;
                            }
                        }
                    }
                }
            }
        }

        private static Dictionary<string, OverlayLapData> cloneLapData(Dictionary<string, OverlayLapData> opponentLapData)
        {
            Dictionary<string, OverlayLapData> clone = new Dictionary<string, OverlayLapData>();
            foreach (KeyValuePair<string, OverlayLapData> entry in opponentLapData)
            {
                OverlayLapData clonedOverlayLapData = new OverlayLapData(entry.Value.lapNumber);
                clonedOverlayLapData.dataPoints = new List<DataPoint>(entry.Value.dataPoints);
                clone.Add(entry.Key, clonedOverlayLapData);

            }
            return clone;
        }
    }
    public enum OverlayDataType
    {
        FLOAT, DOUBLE, INT, STRING,
        /* 4 element arrays for tyre data */
        FLOAT_4, INT_4, DOUBLE_4,
        /* 3 element array for IMO tyre data */
        FLOAT_3
    }
    public enum DataSeriesType
    {
        TIMESERIES, HISTOGRAM
    }
    public enum YAxisScaling
    {
        AUTO, MANUAL
    }
    public enum X_AXIS_TYPE
    {
        DISTANCE, TIME
    }
    public enum SectorToShow
    {
        ALL = 0, SECTOR_1 = 1, SECTOR_2 = 2, SECTOR_3 = 3
    }

    class OverlayLapData
    {
        public int lapNumber;
        public List<DataPoint> dataPoints = new List<DataPoint>();
        public float lapTime = -1;
        public OverlayLapData(int lapNumber)
        {
            this.lapNumber = lapNumber;
        }
        public OverlayLapData(int lapNumber, List<DataPoint> previousLapData, int previousLapCopyStartPoint)
        {
            this.lapNumber = lapNumber;
            if (previousLapCopyStartPoint > 0 && previousLapData.Count > previousLapCopyStartPoint)
            {
                dataPoints.AddRange(previousLapData.GetRange(previousLapCopyStartPoint, previousLapData.Count - previousLapCopyStartPoint));
            }
        }
        public void addDataPoint(DataPoint dataPoint)
        {
            this.dataPoints.Add(dataPoint);
        }
        public List<DataPoint> getDataPoints()
        {
            return new List<DataPoint>(dataPoints);
        }
        public List<Tuple<float, float[]>> convertHistogramSeries(SectorToShow sectorToShow, OverlayDataType type, int? numberOfBuckets)
        {
            List<Tuple<float, float[]>> data = new List<Tuple<float, float[]>>();
            if (dataPoints.Count > 0)
            {
                Boolean foundStartOfLap = false;
                float previousPointDistanceRoundTrack = float.MinValue;
                List<float[]> dataPointsToUse = new List<float[]>();
                float min = float.MaxValue;
                float max = float.MinValue;
                foreach (DataPoint dataPoint in dataPoints)
                {
                    if (dataPoint.distanceRoundTrack < 100)
                    {
                        foundStartOfLap = true;
                    }
                    if (foundStartOfLap)
                    {
                        if (previousPointDistanceRoundTrack - dataPoint.distanceRoundTrack > 200)
                        {
                            // this is generally because a datapoint from the next lap has leaked into this lap's data
                            break;
                        }
                        // if the data point is in the required sector, add it.
                        if (sectorToShow == SectorToShow.ALL || dataPoint.sector <= 0 || dataPoint.sector == (int)sectorToShow)
                        {
                            float[] point = null;
                            switch (type)
                            {
                                case OverlayDataType.FLOAT:
                                case OverlayDataType.DOUBLE:
                                case OverlayDataType.INT:
                                case OverlayDataType.STRING:
                                    point = new float[] { dataPoint.convertToFloat() };
                                    break;
                                case OverlayDataType.FLOAT_3:
                                    point = dataPoint.convertToFloat_3();
                                    break;
                                case OverlayDataType.FLOAT_4:
                                case OverlayDataType.DOUBLE_4:
                                case OverlayDataType.INT_4:
                                    point = dataPoint.convertToFloat_4();
                                    break;
                            }
                            dataPointsToUse.Add(point);
                            foreach (float v in point)
                            {
                                if (v < min)
                                {
                                    min = v;
                                }
                                if (v > max)
                                {
                                    max = v;
                                }
                            }
                        }
                        previousPointDistanceRoundTrack = dataPoint.distanceRoundTrack;
                    }
                }
                // now split them up into range counts
                int buckets = numberOfBuckets == null ? dataPointsToUse.Count / 10 : numberOfBuckets.Value;
                float step = (max - min) / (float)buckets;
                Dictionary<float, float[]> counts = new Dictionary<float, float[]>();
                for (int i = 0; i < buckets; i++)
                {
                    float rangeStart = min + (step * i);
                    float rangeEnd = rangeStart + step;
                    float thisKey = rangeStart + (step / 2f);
                    if (!counts.ContainsKey(thisKey))
                    {
                        switch (type)
                        {
                            case OverlayDataType.FLOAT:
                            case OverlayDataType.DOUBLE:
                            case OverlayDataType.INT:
                            case OverlayDataType.STRING:
                                counts.Add(thisKey, new float[] { 0 });
                                break;
                            case OverlayDataType.FLOAT_3:
                                counts.Add(thisKey, new float[] { 0, 0, 0 });
                                break;
                            case OverlayDataType.FLOAT_4:
                            case OverlayDataType.DOUBLE_4:
                            case OverlayDataType.INT_4:
                                counts.Add(thisKey, new float[] { 0, 0, 0, 0 });
                                break;
                        }
                    }
                    foreach (float[] point in dataPointsToUse)
                    {
                        for (int j = 0; j < point.Length; j++)
                        {
                            if (point[j] >= rangeStart && point[j] < rangeEnd)
                            {
                                counts[thisKey][j] = counts[thisKey][j] + 1;
                            }
                        }
                    }
                }
                float minXValueToShow = min / OverlayController.histogramZoomLevel;
                float maxXValueToShow = max / OverlayController.histogramZoomLevel;
                foreach (var entry in counts)
                {
                    if (entry.Key >= minXValueToShow && entry.Key <= maxXValueToShow)
                    {
                        float[] thisPoint = new float[entry.Value.Length];
                        for (int i=0; i<entry.Value.Length; i++)
                        {
                            // convert each value to a proportion of the total - note these are formatted as percentages on the chart y-axis
                            thisPoint[i] = entry.Value[i] / dataPointsToUse.Count;
                        }
                        data.Add(new Tuple<float, float[]>(entry.Key, thisPoint));
                    }
                }
            }
            return data;
        }

        public List<Tuple<float, float[]>> convertTimeSeries(SectorToShow sectorToShow, OverlayDataType type, float startPoint = -1, float endPoint = -1)
        {
            List<Tuple<float, float[]>> data = new List<Tuple<float, float[]>>();
            if (dataPoints.Count > 0)
            {
                Boolean foundStartOfLap = false;
                float previousPointDistanceRoundTrack = float.MinValue;
                long startTicks = dataPoints[0].ticksWhenRead;
                bool gotFirstPoint = false;
                foreach (DataPoint dataPoint in dataPoints)
                {
                    if (dataPoint.distanceRoundTrack < 100)
                    {
                        foundStartOfLap = true;
                    }
                    if (foundStartOfLap)
                    {
                        if (previousPointDistanceRoundTrack - dataPoint.distanceRoundTrack > 200)
                        {
                            // this is generally because a datapoint from the next lap has leaked into this lap's data
                            break;
                        }
                        // if we have no distance range and the data point is in the required sector, add it. If we have a distance range, it must be in that range
                        if ((startPoint == -1 && endPoint == -1 && (sectorToShow == SectorToShow.ALL || dataPoint.sector <= 0 || dataPoint.sector == (int)sectorToShow))
                            || dataPoint.distanceRoundTrack >= startPoint && dataPoint.distanceRoundTrack <= endPoint)
                        {
                            float xPoint = OverlayDataSource.xAxisType == X_AXIS_TYPE.DISTANCE ?
                                dataPoint.distanceRoundTrack : (float)TimeSpan.FromTicks(dataPoint.ticksWhenRead - startTicks).TotalSeconds;
                            switch (type)
                            {
                                case OverlayDataType.FLOAT:
                                case OverlayDataType.DOUBLE:
                                case OverlayDataType.INT:
                                case OverlayDataType.STRING:
                                    data.Add(new Tuple<float, float[]>(xPoint, new float[] { dataPoint.convertToFloat() }));
                                    break;
                                case OverlayDataType.FLOAT_3:
                                    data.Add(new Tuple<float, float[]>(xPoint, dataPoint.convertToFloat_3()));
                                    break;
                                case OverlayDataType.FLOAT_4:
                                case OverlayDataType.DOUBLE_4:
                                case OverlayDataType.INT_4:
                                    data.Add(new Tuple<float, float[]>(xPoint, dataPoint.convertToFloat_4()));
                                    break;
                            }
                            if (startPoint == -1 && endPoint == -1)
                            {
                                if (!gotFirstPoint)
                                {
                                    OverlayController.x_min = dataPoint.distanceRoundTrack;
                                    gotFirstPoint = true;
                                }
                                OverlayController.x_max = dataPoint.distanceRoundTrack;
                            }
                        }
                        previousPointDistanceRoundTrack = dataPoint.distanceRoundTrack;
                    }
                }
            }
            return data;
        }
    }
    public class DataPoint
    {
        public int lapsCompleted;
        public object datum;
        public OverlayDataType dataType;
        public long ticksWhenRead;
        public float distanceRoundTrack;
        public int sector;
        public DataPoint(int lapsCompleted, float distanceRoundTrack, object datum, OverlayDataType dataType, long ticksWhenRead, int sector)
        {
            this.lapsCompleted = lapsCompleted;
            this.distanceRoundTrack = distanceRoundTrack;
            this.datum = datum;
            this.dataType = dataType;
            this.ticksWhenRead = ticksWhenRead;
            this.sector = sector;
        }

        public float convertToFloat()
        {
            switch (dataType)
            {
                case OverlayDataType.DOUBLE:
                case OverlayDataType.FLOAT:
                case OverlayDataType.INT:
                    return Convert.ToSingle(datum);
                case OverlayDataType.STRING:
                    return float.Parse((string)datum);
                default:
                    throw new Exception("Unable to convert " + dataType + " to float");
            }
        }

        public int convertToInt()
        {
            switch (dataType)
            {
                case OverlayDataType.DOUBLE:
                case OverlayDataType.FLOAT:
                    return (int)Math.Round((double)datum);
                case OverlayDataType.INT:
                    return (int)datum;
                case OverlayDataType.STRING:
                    return int.Parse((string)datum);
                default:
                    throw new Exception("Unable to convert " + dataType + " to int");
            }
        }

        public float[] convertToFloat_4()
        {
            float[] converted = new float[4];
            switch (dataType)
            {
                case OverlayDataType.DOUBLE_4:
                    double[] doubleDatum = (double[])datum;
                    converted[0] = Convert.ToSingle(doubleDatum[0]);
                    converted[1] = Convert.ToSingle(doubleDatum[1]);
                    converted[2] = Convert.ToSingle(doubleDatum[2]);
                    converted[3] = Convert.ToSingle(doubleDatum[3]);
                    return converted;
                case OverlayDataType.FLOAT_4:
                    return (float[])datum;
                case OverlayDataType.INT_4:
                    int[] intDatum = (int[])datum;
                    converted[0] = Convert.ToSingle(intDatum[0]);
                    converted[1] = Convert.ToSingle(intDatum[1]);
                    converted[2] = Convert.ToSingle(intDatum[2]);
                    converted[3] = Convert.ToSingle(intDatum[3]);
                    return converted;
                default:
                    throw new Exception("Unable to convert " + dataType + " to float_4");
            }
        }

        public float[] convertToFloat_3()
        {
            float[] converted = new float[3];
            switch (dataType)
            {
                case OverlayDataType.FLOAT_3:
                    return (float[])datum;
                default:
                    throw new Exception("Unable to convert " + dataType + " to float_3");
            }
        }

        public int[] convertToInt_4()
        {
            int[] converted = new int[4];
            switch (dataType)
            {
                case OverlayDataType.DOUBLE_4:
                    double[] doubleDatum = (double[])datum;
                    converted[0] = (int)Math.Round(doubleDatum[0]);
                    converted[1] = (int)Math.Round(doubleDatum[1]);
                    converted[2] = (int)Math.Round(doubleDatum[2]);
                    converted[3] = (int)Math.Round(doubleDatum[3]);
                    return converted;
                case OverlayDataType.FLOAT_4:
                    float[] floatDatum = (float[])datum;
                    converted[0] = (int)Math.Round(floatDatum[0]);
                    converted[0] = (int)Math.Round(floatDatum[1]);
                    converted[0] = (int)Math.Round(floatDatum[2]);
                    converted[0] = (int)Math.Round(floatDatum[3]);
                    return converted;
                case OverlayDataType.INT_4:
                    return (int[])datum;
                default:
                    throw new Exception("Unable to convert " + dataType + " to int_4");
            }
        }
    }
    public static class SomeNiceStuffFromDavidTuckerFromiRacing
    {
        public static double RAD_PER_DEG_DOUBLE = 0.0174532925;
        public  static double[] LatLonCVToPoint(double lat, double lon, double baseLat, double baseLon, float trackAngle)
        {
            double[] xy = new double[]{ 0, 0 };
            // calculate the meters per decimal deg for our base latitude
            double rlat = baseLat * RAD_PER_DEG_DOUBLE;
            double meterPerDegLon = 111415.13 * Math.Cos(rlat) - 94.55 * Math.Cos(3.0 * rlat);
            double meterPerDegLat = 111132.09 - 566.05 * Math.Cos(2.0 * rlat) + 1.2 * Math.Cos(4.0 * rlat);

            // for now just use linear interpolation to map too lat/lon
            float ry = (float)((lat - baseLat) * meterPerDegLat); // lat
            float rx = (float)((lon - baseLon) * meterPerDegLon); // lon

            // rotate coordinates to point north
            // trackAngle is counter clockwise angle from x axis
            xy[0] = rx * (float)Math.Sin(trackAngle) + ry * (float)Math.Cos(trackAngle);
            xy[1] = -rx * (float)Math.Cos(trackAngle) + ry * (float)Math.Sin(trackAngle);
            return xy;
        }

        public static float altCVToPoint(float rz, float baseAlt)
        {
            return rz - baseAlt;
        }
    }
}
