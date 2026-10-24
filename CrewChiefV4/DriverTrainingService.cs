using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using CrewChiefV4.Audio;

namespace CrewChiefV4
{
    class DriverTrainingService
    {
        public static readonly Boolean multiLapPaceNotes = UserSettings.GetUserSettings().getBoolean("multi_lap_pace_notes");

        public static String folderStartedRecording = "pace_notes/recording_started";
        public static String folderEndedRecording = "pace_notes/recording_ended";
        public static String folderStartedPlayback = "pace_notes/playback_started";
        public static String folderEndedPlayback = "pace_notes/playback_ended";

        private static int combineEntriesCloserThan = 20; // if a new entry's lap distance is within 20 metres of an existing entry's lap distance, combine them
        public static Boolean isPlayingPaceNotes = false;
        public static Boolean isRecordingPaceNotes = false;
        private static Boolean isRecordingSound = false;
        private static MetaData recordingMetaData;
        private static WaveInEvent waveSource = null;
        private static WaveFileWriter waveFile = null;

        private static GameEnum gameEnum;
        private static String trackName;
        private static CarData.CarClassEnum carClass;

        public static String folderPathForPaceNotes;

        private static Boolean recordingHasStarted = false;
        private static int lapCounterForRecordingMultiLapPaceNotes = 0;
        private static int lapCounterForPlayingMultiLapPaceNotes = 0;

        private static Object _lock = new Object();

        public static void incrementPaceNotesRecordingLapCounter()
        {
            // don't start incrementing this until there's at least 1 recording
            if (recordingHasStarted)
            {
                DriverTrainingService.lapCounterForRecordingMultiLapPaceNotes++;
            }
        }

        public static void incrementPaceNotesPlaybackLapCounter()
        {
            DriverTrainingService.lapCounterForPlayingMultiLapPaceNotes++;
        }

        public static Boolean loadPaceNotes(GameEnum gameEnum, String trackName, CarData.CarClassEnum carClass, AudioPlayer audioPlayer)
        {
            if (!isRecordingPaceNotes && !isPlayingPaceNotes)
            {
                isRecordingPaceNotes = false;
                isRecordingSound = false;
                lapCounterForPlayingMultiLapPaceNotes = 0;
                if (carClass != CarData.CarClassEnum.USER_CREATED && carClass != CarData.CarClassEnum.UNKNOWN_RACE)
                {
                    DriverTrainingService.folderPathForPaceNotes = getCarSpecificFolderPath(gameEnum, trackName, carClass);
                    // check for car-specific pace notes first
                    if (Directory.Exists(DriverTrainingService.folderPathForPaceNotes))
                    {
                        Console.WriteLine("Playing pace notes for circuit " + trackName + " with car class " + carClass.ToString());
                    }
                    else
                    {
                        // fall back to generic pace notes
                        DriverTrainingService.folderPathForPaceNotes = getAnyCarFolderPath(gameEnum, trackName);
                        if (Directory.Exists(DriverTrainingService.folderPathForPaceNotes))
                        {
                            Console.WriteLine("Playing pace notes for circuit " + trackName + " for any car class");
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    // don't know this car, see if we can use 'any' car class pace notes
                    DriverTrainingService.folderPathForPaceNotes = getAnyCarFolderPath(gameEnum, trackName);
                    if (Directory.Exists(DriverTrainingService.folderPathForPaceNotes))
                    {
                        Console.WriteLine("Playing pace notes for circuit " + trackName + " for any car class");
                    }
                    else
                    {
                        return false;
                    }
                }

                String fileName = System.IO.Path.Combine(DriverTrainingService.folderPathForPaceNotes, "metadata.json");
                if (File.Exists(fileName))
                {
                    try
                    {
                        DriverTrainingService.recordingMetaData = JsonConvert.DeserializeObject<MetaData>(File.ReadAllText(fileName));
                        if (DriverTrainingService.recordingMetaData == null)
                        {
                            return false;
                        }
                        if (DriverTrainingService.recordingMetaData.description != null && !DriverTrainingService.recordingMetaData.description.Equals(""))
                        {
                            Console.WriteLine("Playing pace notes with description " + DriverTrainingService.recordingMetaData.description);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Unable to parse pace notes metadata file: ");
                        return false;
                    }
                    foreach (MetaDataEntry entry in DriverTrainingService.recordingMetaData.entries)
                    {
                        if (!entry.loadSounds())
                        {
                            Console.WriteLine("Entry " + entry.ToString() + " failed to load, pace notes will not be available");
                            return false;
                        }
                    }
                    isPlayingPaceNotes = true;
                    if (DriverTrainingService.recordingMetaData.welcomeMessage != null
                        && DriverTrainingService.recordingMetaData.welcomeMessage.recordingNames != null
                        && DriverTrainingService.recordingMetaData.welcomeMessage.recordingNames.Count > 0)
                    {
                        DriverTrainingService.recordingMetaData.welcomeMessage.loadSounds();
                        if (DriverTrainingService.recordingMetaData.welcomeMessage.playAllInOrder)
                        {
                            List<MessageFragment> messageFragments = new List<MessageFragment>();
                            foreach (String recordingName in DriverTrainingService.recordingMetaData.welcomeMessage.recordingNames)
                            {
                                messageFragments.Add(MessageFragment.Text(recordingName));
                            }
                            // don't allow these to expire, and ensure we always have a unique label for this message set so
                            // we can always queue the next set
                            QueuedMessage message = new QueuedMessage("pace_notes_welcome", 0, messageFragments: messageFragments, type: SoundType.PACE_NOTE, priority: 0);
                            message.playEvenWhenSilenced = true;
                            audioPlayer.playMessageImmediately(message);
                        }
                        else
                        {
                            QueuedMessage message = new QueuedMessage(DriverTrainingService.recordingMetaData.welcomeMessage.getRandomRecordingName(), 1, type: SoundType.PACE_NOTE, priority: 0);
                            message.playEvenWhenSilenced = true;
                            audioPlayer.playMessageImmediately(message);
                        }
                    }
                    return true;
                }
                else
                {
                    Console.WriteLine("No metadata.json file exists in the pace notes folder " + DriverTrainingService.folderPathForPaceNotes);
                    return false;
                }
            }
            else
            {
                if (isRecordingPaceNotes)
                {
                    Console.WriteLine("A recording is already in progress, complete this first");
                }
                else
                {
                    Console.WriteLine("Already playing a session");
                }
                return false;
            }
        }

        public static void stopPlayingPaceNotes()
        {
            isPlayingPaceNotes = false;
        }

        public static void stopRecordingPaceNotes()
        {
            isRecordingPaceNotes = false;
        }

        public static void checkValidAndPlayIfNeeded(DateTime now, float speed, float yawAngle,
            float previousDistanceRoundTrack, float currentDistanceRoundTrack, Boolean isInPitLane, AudioPlayer audioPlayer)
        {
            if (isPlayingPaceNotes && !isRecordingPaceNotes && DriverTrainingService.recordingMetaData != null)
            {
                // if we're not in the pitlane but our lap counter is 0, then ensure we start at lap 1. Lap 0 is only
                // for pitlane messages
                if (!isInPitLane && DriverTrainingService.lapCounterForPlayingMultiLapPaceNotes == 0)
                {
                    DriverTrainingService.lapCounterForPlayingMultiLapPaceNotes = 1;
                }
                foreach (MetaDataEntry entry in DriverTrainingService.recordingMetaData.entries)
                {
                    if (entry.shouldPlay(DriverTrainingService.lapCounterForPlayingMultiLapPaceNotes, speed, yawAngle, previousDistanceRoundTrack, currentDistanceRoundTrack))
                    {
                        if (entry.description != null && !entry.description.Equals(""))
                        {
                            Console.WriteLine("Playing entry at distance " + entry.distanceRoundTrack + " with description " + entry.description);
                        }
                        else
                        {
                            Console.WriteLine("Playing entry at distance " + entry.distanceRoundTrack);
                        }
                        if (entry.playAllInOrder)
                        {
                            List<MessageFragment> messageFragments = new List<MessageFragment>();
                            foreach (String recordingName in entry.recordingNames)
                            {
                                messageFragments.Add(MessageFragment.Text(recordingName));
                            }
                            // don't allow these to expire, and ensure we always have a unique label for this message set so
                            // we can always queue the next set
                            QueuedMessage message = new QueuedMessage("pace_notes_" + currentDistanceRoundTrack, 0, messageFragments: messageFragments, type: SoundType.PACE_NOTE, priority: 0);
                            message.playEvenWhenSilenced = true;
                            audioPlayer.playMessageImmediately(message);
                        }
                        else
                        {
                            QueuedMessage message = new QueuedMessage(entry.getRandomRecordingName(), 1, type: SoundType.PACE_NOTE, priority: 0);
                            message.playEvenWhenSilenced = true;
                            audioPlayer.playMessageImmediately(message);
                        }
                    }
                }
            }
        }

        public static void startRecordingPaceNotes(GameEnum gameEnum, String trackName, CarData.CarClassEnum carClass)
        {
            if (!isPlayingPaceNotes && !isRecordingPaceNotes)
            {
                Console.WriteLine("Recording a pace notes session for circuit " + trackName + " with car class " + carClass.ToString());
                DriverTrainingService.gameEnum = gameEnum;
                DriverTrainingService.trackName = trackName;
                DriverTrainingService.carClass = carClass;
                DriverTrainingService.recordingHasStarted = false;                  // this will be set to true when the first recording is made
                DriverTrainingService.lapCounterForRecordingMultiLapPaceNotes = 0;  // this will be incremented to 1 if necessary. It starts at zero so
                                                                                    // cases where we enable recording before crossing the line to start our
                                                                                    // first lap don't screw up the counter
                if (carClass == CarData.CarClassEnum.UNKNOWN_RACE || carClass == CarData.CarClassEnum.USER_CREATED)
                {
                    Console.WriteLine("Recording pace notes for any car class");
                    DriverTrainingService.folderPathForPaceNotes = getAnyCarFolderPath(gameEnum, trackName);
                }
                else
                {
                    Console.WriteLine("Recording pace notes for car class " + carClass.ToString());
                    DriverTrainingService.folderPathForPaceNotes = getCarSpecificFolderPath(gameEnum, trackName, carClass);
                }
                Boolean createFolder = true;
                Boolean createNewMetaData = true;
                if (System.IO.Directory.Exists(folderPathForPaceNotes))
                {
                    createFolder = false;
                    String fileName = System.IO.Path.Combine(folderPathForPaceNotes, "metadata.json");
                    if (File.Exists(fileName))
                    {
                        if (!multiLapPaceNotes)
                        {
                            try
                            {
                                DriverTrainingService.recordingMetaData = JsonConvert.DeserializeObject<MetaData>(File.ReadAllText(fileName));
                                if (DriverTrainingService.recordingMetaData != null)
                                {
                                    Console.WriteLine("Pace notes for this game / track / car combination already exists. This will be extended");
                                    createNewMetaData = false;
                                }
                            }
                            catch (Exception e)
                            {
                                Utilities.TryBackupBrokenFile(fileName, "_broken", "Unable to load existing metadata: " + e.Message);
                            }
                        }
                        else
                        {
                            Utilities.TryBackupBrokenFile(fileName, "_old", "Pace notes for this game / track / car combination exist but cannot be extended because we're in multi-lap mode.  The old pacenotes file will be renamed.");
                        }
                    }
                }
                if (createFolder)
                {
                    System.IO.Directory.CreateDirectory(folderPathForPaceNotes);
                }
                if (createNewMetaData)
                {
                    DriverTrainingService.recordingMetaData = new MetaData(gameEnum.ToString(), carClass.ToString(), trackName);
                }

                isRecordingPaceNotes = true;
            }
        }

        private static String getCarSpecificFolderPath(GameEnum gameEnum, String trackName, CarData.CarClassEnum carClass)
        {
            return System.IO.Path.Combine(DataFiles.PaceNotesFolder,
                Utilities.FileNames.MakeValidForPathName(gameEnum.ToString()),
                Utilities.FileNames.MakeValidForPathName(carClass.ToString()),
                Utilities.FileNames.MakeValidForPathName(trackName));
        }

        private static String getAnyCarFolderPath(GameEnum gameEnum, String trackName)
        {
            return System.IO.Path.Combine(DataFiles.PaceNotesFolder,
                Utilities.FileNames.MakeValidForPathName(gameEnum.ToString()),
                Utilities.FileNames.MakeValidForPathName(trackName));
        }

        public static void abortRecordingPaceNotes()
        {
            if (isRecordingSound)
            {
                stopRecordingMessage(null);
            }
            System.IO.Directory.Delete(folderPathForPaceNotes, true);
            isRecordingPaceNotes = false;
        }

        public static void completeRecordingPaceNotes()
        {
            if (isRecordingSound)
            {
                stopRecordingMessage(null);
            }
            if (isRecordingPaceNotes)
            {
                try
                {
                    File.WriteAllText(System.IO.Path.Combine(folderPathForPaceNotes, "metadata.json"),
                        JsonConvert.SerializeObject(DriverTrainingService.recordingMetaData, Formatting.Indented));
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Unable to complete recording session : ");
                }
            }
            isRecordingPaceNotes = false;
        }

        public static void stopRecordingMessage(AudioPlayer audioPlayer)
        {
            if (DriverTrainingService.isRecordingSound)
            {
                try
                {
                    DriverTrainingService.waveSource.StopRecording();
                    if (audioPlayer != null)
                    {
                        audioPlayer.playPaceNoteRecordingStartStopBeep();
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Failed to record a pace notes session sound ");
                }
                DriverTrainingService.isRecordingSound = false;
            }
        }

        public static void startRecordingMessage(int distanceRoundTrack, AudioPlayer audioPlayer)
        {
            if (isRecordingPaceNotes && CrewChief.currentGameState != null)
            {
                if (DriverTrainingService.isRecordingSound)
                {
                    Console.WriteLine("Sound already being recorded");
                }
                else
                {
                    audioPlayer.playPaceNoteRecordingStartStopBeep();
                    Boolean addMetaDataEntryToStandardList = false;
                    Boolean addMetaDataEntryAsWelcomeMessage = false;
                    DriverTrainingService.recordingHasStarted = true;
                    MetaDataEntry entry;
                    if (CrewChief.currentGameState.PitData.InPitlane)
                    {
                        if (CrewChief.currentGameState.PositionAndMotionData.CarSpeed < 0.1)
                        {
                            // if we're stationary in the pits, make this our welcome message
                            addMetaDataEntryAsWelcomeMessage = true;
                            Console.WriteLine("This message will be the welcome message");
                            entry = new MetaDataEntry(0, 0);
                        }
                        else if (multiLapPaceNotes)
                        {
                            // we're rolling but we're in the pits, so make this a lap 0 message
                            addMetaDataEntryToStandardList = true;
                            entry = new MetaDataEntry(0, distanceRoundTrack);
                        }
                        else
                        {
                            Console.WriteLine("Enable multilap pace notes support to record pit lane messages");
                            return;
                        }
                    }
                    else
                    {
                        entry = multiLapPaceNotes ? null : DriverTrainingService.recordingMetaData.getClosestEntryInRange(distanceRoundTrack, combineEntriesCloserThan);
                    }
                    int? lapNumber = null;
                    if (entry == null)
                    {
                        addMetaDataEntryToStandardList = true;
                        if (multiLapPaceNotes)
                        {
                            // always start at lap number 1 for standard pace notes once we're out of the pits
                            if (DriverTrainingService.lapCounterForRecordingMultiLapPaceNotes == 0)
                            {
                                DriverTrainingService.lapCounterForRecordingMultiLapPaceNotes = 1;
                            }
                            lapNumber = DriverTrainingService.lapCounterForRecordingMultiLapPaceNotes;
                        }
                        entry = new MetaDataEntry(lapNumber, distanceRoundTrack);
                    }

                    // update the speed and yaw in the entry we're creating / modifying
                    float? yawWhenRecorded = null;
                    float? speedWhenRecorded = null;
                    if (CrewChief.currentGameState != null)
                    {
                        yawWhenRecorded = CrewChief.currentGameState.PositionAndMotionData.Orientation.Yaw;
                        speedWhenRecorded = CrewChief.currentGameState.PositionAndMotionData.CarSpeed;
                    }
                    entry.speedWhenRecorded = speedWhenRecorded;
                    entry.yawWhenRecorded = yawWhenRecorded;

                    int recordingIndex = entry.recordingNames.Count;
                    String fileNameStart = distanceRoundTrack + "_" + recordingIndex;
                    if (multiLapPaceNotes && lapNumber != null)
                    {
                        fileNameStart += "_lap_" + lapNumber;
                    }
                    String fileName = addMetaDataEntryAsWelcomeMessage ? "welcome_message.wav" : fileNameStart + ".wav";
                    String recordingName = DriverTrainingService.trackName + "_" + DriverTrainingService.carClass.ToString() + "_" + fileName;
                    try
                    {
                        lock (_lock)
                        {
                            DriverTrainingService.waveSource = new WaveInEvent();
                            DriverTrainingService.waveSource.WaveFormat = new WaveFormat(22050, 1);
                            DriverTrainingService.waveSource.DataAvailable += new EventHandler<WaveInEventArgs>(waveSource_DataAvailable);
                            DriverTrainingService.waveSource.RecordingStopped += new EventHandler<StoppedEventArgs>(waveSource_RecordingStopped);
                            DriverTrainingService.waveFile = new WaveFileWriter(createFileName(fileName), waveSource.WaveFormat);
                            DriverTrainingService.waveSource.StartRecording();
                        }
                        entry.recordingNames.Add(recordingName);
                        entry.fileNames.Add(fileName);
                        if (addMetaDataEntryAsWelcomeMessage)
                        {
                            DriverTrainingService.recordingMetaData.welcomeMessage = entry;
                        }
                        else if (addMetaDataEntryToStandardList)
                        {
                            DriverTrainingService.recordingMetaData.entries.Add(entry);
                        }
                        DriverTrainingService.isRecordingSound = true;
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Unable to create a pace notes sound ");
                    }
                }
            }
        }

        private static String createFileName(String name)
        {
            return System.IO.Path.Combine(DriverTrainingService.folderPathForPaceNotes, name);
        }

        static void waveSource_DataAvailable(object sender, WaveInEventArgs e)
        {
            lock (_lock)
            {
                if (DriverTrainingService.waveFile != null)
                {
                    DriverTrainingService.waveFile.Write(e.Buffer, 0, e.BytesRecorded);
                    DriverTrainingService.waveFile.Flush();
                }
            }
        }

        static void waveSource_RecordingStopped(object sender, StoppedEventArgs e)
        {
            lock (_lock)
            {
                if (DriverTrainingService.waveSource != null)
                {
                    DriverTrainingService.waveSource.Dispose();
                    DriverTrainingService.waveSource = null;
                }

                if (DriverTrainingService.waveFile != null)
                {
                    DriverTrainingService.waveFile.Dispose();
                    DriverTrainingService.waveFile = null;
                }
            }
        }
    }

    public class MetaData
    {
        public String description { get; set; }
        public String gameEnumName { get; set; }
        public String carClassName { get; set; }
        public String trackName { get; set; }
        public MetaDataEntry welcomeMessage {get; set;}
        public List<MetaDataEntry> entries { get; set; }

        public MetaData()
        {
            this.entries = new List<MetaDataEntry>();
            this.description = "";
        }

        public MetaData(String gameEnumName, String carClassName, String trackName)
        {
            this.gameEnumName = gameEnumName;
            this.carClassName = carClassName;
            this.trackName = trackName;
            this.description = "";
            this.entries = new List<MetaDataEntry>();
        }

        public MetaDataEntry getClosestEntryInRange(int distanceRoundTrack, int range)
        {
            int closestDifference = int.MaxValue;
            MetaDataEntry closestEntry = null;
            foreach (MetaDataEntry entry in entries)
            {
                // TODO: include track length in this calculation
                int difference = Math.Abs(entry.distanceRoundTrack - distanceRoundTrack);
                if (difference < closestDifference)
                {
                    closestDifference = difference;
                    closestEntry = entry;
                }
            }
            if (closestDifference <= range)
            {
                if (closestEntry.description != null && !closestEntry.description.Equals(""))
                {
                    Console.WriteLine("Adding this recording to existing entry " + closestEntry.description + " at distance " + closestEntry.distanceRoundTrack);
                }
                else
                {
                    Console.WriteLine("Adding this recording to existing entry at distance " + closestEntry.distanceRoundTrack);
                }
                return closestEntry;
            }
            else
            {
                return null;
            }
        }
    }

    public class MetaDataEntry
    {
        // this is optional
        public String description { get; set; }
        // this is the distanceRoundTrack where this pace note will trigger
        public int distanceRoundTrack { get; set; }

        // if these are null they're ignored
        // lap number (after starting pace notes playback) when this pace note will trigger. If this is
        // null the pace note will trigger every lap
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? lapNumber { get; set; } = null;
        // only trigger this pace note if we're exceeding this speed (optional)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? minimumSpeed { get; set; } = null;
        // only trigger this pace note if we're going at or slower than this speed (optional)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? maximumSpeed { get; set; } = null;

        // the yaw angle filters must both be present to work, I've not tested them
        // only trigger this pace note if our yaw angle is greater than this value (option, experimental)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? minimumYawAngle { get; set; } = null;
        // only trigger this pace note if our yaw angle is less than or equal to this value (option, experimental)
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? maximumYawAngle { get; set; } = null;

        // these are recorded for info only - they might be useful to base filter
        // values on, particularly the yaw data for early / late turn in.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? speedWhenRecorded { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public float? yawWhenRecorded { get; set; }

        // these are the recording names to be printed to the console - optional
        public List<String> recordingNames { get; set; }
        // these are the filenames to be loaded from the folder where the metadata json file sits
        public List<String> fileNames { get; set; }
        // if this is true each of the filenames for this pace note will play in order. If false we pick a random one
        public bool playAllInOrder { get; set; }
        // list of subtitles, optional, should have same order as file names
        public List<String> subtitles { get; set; }

        public MetaDataEntry()
        {
            this.recordingNames = new List<string>();
            this.fileNames = new List<string>();
            this.description = "";
            this.playAllInOrder = false;
        }

        public MetaDataEntry(int? lapNumber, int distanceRoundTrack)
        {
            this.lapNumber = lapNumber;
            this.distanceRoundTrack = distanceRoundTrack;
            this.description = "";
            this.recordingNames = new List<string>();
            this.fileNames = new List<string>();
            this.playAllInOrder = false;
        }

        public String getRandomRecordingName()
        {
            int index = Utilities.random.Next(recordingNames.Count);
            return recordingNames[index];
        }

        public Boolean shouldPlay(int lapNumber, float speed, float yawAngle,
            float previousDistanceRoundTrack, float currentDistanceRoundTrack)
        {
            // max speed is inclusive, min isn't. This is just so we can use the same speed filter number for a min and max filter and
            // there's no risk that being at the exact speed means neither trigger
            return previousDistanceRoundTrack < this.distanceRoundTrack
                && currentDistanceRoundTrack > this.distanceRoundTrack
                && (this.lapNumber == null || this.lapNumber == lapNumber)
                && (this.minimumSpeed == null || speed >= this.minimumSpeed)
                && (this.maximumSpeed == null || speed < this.maximumSpeed)
                && (this.minimumYawAngle == null || this.maximumYawAngle == null || (yawAngle >= this.minimumYawAngle && yawAngle <= maximumYawAngle));
        }

        public Boolean loadSounds()
        {
            int fileNamesCount = fileNames.Count;
            int recordingNamesCount = recordingNames.Count;
            Boolean cachedSounds = false;
            List<string> fileNamesToLoad = null;
            List<string> recordingNamesToLoad = null;
            if (fileNamesCount > 0 && fileNamesCount == recordingNamesCount)
            {
                fileNamesToLoad = this.fileNames;
                recordingNamesToLoad = this.recordingNames;
            }
            else if (fileNamesCount > 0)
            {
                fileNamesToLoad = this.fileNames;
                recordingNamesToLoad = this.fileNames;
            }
            else if (recordingNamesCount > 0)
            {
                fileNamesToLoad = this.recordingNames;
                recordingNamesToLoad = this.recordingNames;
            }

            if (fileNamesToLoad != null && recordingNamesToLoad != null)
            {
                for (int i=0; i< fileNamesToLoad.Count; i++)
                {
                    try
                    {
                        string subtitle = null;
                        if (subtitles != null && subtitles.Count > i)
                        {
                            subtitle = subtitles[i];
                        }
                        SoundCache.loadSingleSound(recordingNamesToLoad[i], System.IO.Path.Combine(DriverTrainingService.folderPathForPaceNotes, fileNamesToLoad[i]), subtitle);
                        cachedSounds = true;
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Unable to load sound " + fileNamesToLoad[i] + " from pace notes set " + DriverTrainingService.folderPathForPaceNotes + " : ");
                    }
                }
            }
            return cachedSounds;
        }
    }
}
