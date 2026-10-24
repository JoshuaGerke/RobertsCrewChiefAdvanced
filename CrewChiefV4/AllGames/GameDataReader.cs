using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO.Compression;
using Newtonsoft.Json;
using System.Windows.Forms;

namespace CrewChiefV4
{
    public abstract class GameDataReader
    {
        protected String filenameToDump;
        public Boolean dumpToFile = false;
        public Boolean currentlyTracingGameData = false;
        public static string dumpedFilePath { get; private set; }

        protected abstract Boolean InitialiseInternal();

        public abstract Object ReadGameData(Boolean forSpotter);

        public abstract void Dispose();

        public abstract void DumpRawGameData();

        /// <summary>
        /// Raw game data and the tick when it was read
        /// </summary>
        public class TracedGameData
        {
            public Object GameData;
            public long TicksWhenRead;
        }
        /// <summary>
        /// Kinda enumerator, returns an item of game data each time it's called
        /// </summary>
        /// <param name="filename">(which EVERY game handler processes into a full path)</param>
        /// <param name="pauseBeforeStart">mS to sleep the first time it's called</param>
        /// <returns>raw game data and the tick when it was read</returns>
        public abstract TracedGameData ReadGameDataFromFile(String filename, int pauseBeforeStart);

        public abstract void ResetGameDataFromFile();

        protected String dataFilesPath;

        public GameDataReader()
        {
            if (DataFiles.MakeFolders() == null)
            {
                dataFilesPath = DataFiles.DebugLogsFolder;
            }
            else
            {
                Console.WriteLine("Unable to create folder for data file, no session record will be available");
                dataFilesPath = null;
            }
        }

        // NOTE: InitialiseInternal must be synchronized internally.
        public Boolean Initialise()
        {
            if (!Game.NONE)
            {
                Log.DontSpam($"Initialising link with {CrewChief.gameDefinition.friendlyName}...").Commentary();
            }
            Boolean initialised = InitialiseInternal();
#if FAKE_GAME
            initialised = true;
#endif
            if (dataFilesPath == null)
            {
                // We can't dump to file if there's no valid path.
                dumpToFile = false;
            }
            if (initialised && dumpToFile)
            {
                filenameToDump = Utilities.FileNames.TimeStampedFilePath(dataFilesPath,
                    CrewChief.gameDefinition.commandLineName + "_", ".zip");
                Console.WriteLine($"Session recording will be dumped to file: {filenameToDump}");
            }
            return initialised;
        }

        /// <summary>
        /// ValueTuple for the contents of a 4 in 1 log file.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class TraceContents<T>
        {
            [JsonProperty("SpeechTrace")]
            public Dictionary<long, string> SpeechTrace { get; }
            [JsonProperty("ConsoleLogPrefix")]
            public string[] ConsoleLogPrefix { get; }
            [JsonProperty("ConsoleLog")]
            public string[] ConsoleLog { get; }
            [JsonProperty("RawGameData")]
            public T RawGameData { get; }

            // Constructor
            public TraceContents(Dictionary<long, string> speechTrace, string[] consoleLogPrefix, string[] consoleLog, T rawGameData)
            {
                SpeechTrace = speechTrace;
                ConsoleLogPrefix = consoleLogPrefix;
                ConsoleLog = consoleLog;
                RawGameData = rawGameData;
            }
        }

        /// <summary>
        /// The four types of log file are stored as a tuple in a single compressed file.<br/>
        /// * the speech trace<br/>
        /// * the console log prefix<br/>
        /// * the console log<br/>
        /// * game-specific trace data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializableObject"></param>
        /// <param name="filePath"></param>
        protected void SerializeObject<T>(in T serializableObject, string filePath)
        {
            if (serializableObject == null) { return; }
            if (!MainWindow.shouldSaveTrace)
                return;

            string consoleLog = MainWindow.getConsoleLog();
            string consoleLogPrefix = MainWindow.prefixLogfile();
            Console.WriteLine("About to dump game data - this may take a while");

            try
            {
                DumpTrace(serializableObject, filePath, CrewChief.SpeechTrace.Dictionary, consoleLogPrefix, consoleLog);
                dumpedFilePath = filePath;
                Console.WriteLine("Done writing session data log to: " + filePath);
                Console.WriteLine("PLEASE RESTART THE APPLICATION BEFORE ATTEMPTING TO RECORD ANOTHER SESSION");
            }
            catch (Exception ex) when (CrewChief.Debug.LogException(ex))
            {
                Log.Exception(ex, "Unable to write compressed trace log");
            }
        }

        // (internal static for unit testing)
        internal static void DumpTrace<T>(T serializableObject, string filePath, Dictionary<long, string> speechTrace, string consoleLogPrefix, string consoleLog)
        {
            if (serializableObject == null)
            {
                return;
            }

            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Newtonsoft.Json.Formatting.Indented;

            using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
            {
                using (ZipArchive zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create))
                {
                    // Create an entry for the speech trace
                    ZipArchiveEntry speechTraceEntry = zipArchive.CreateEntry("SpeechTrace.json", CompressionLevel.Optimal);
                    using (Stream entryStream = speechTraceEntry.Open())
                    {
                        using (StreamWriter sw = new StreamWriter(entryStream, Encoding.UTF8))
                        {
                            using (JsonWriter writer = new JsonTextWriter(sw))
                            {
                                serializer.Serialize(writer, speechTrace);
                            }
                        }
                    }

                    // Create an entry for the console log prefix
                    ZipArchiveEntry consoleLogPrefixEntry = zipArchive.CreateEntry("ConsoleLogPrefix.txt", CompressionLevel.Optimal);
                    using (Stream entryStream = consoleLogPrefixEntry.Open())
                    {
                        using (StreamWriter sw = new StreamWriter(entryStream, Encoding.UTF8))
                        {
                            sw.Write(consoleLogPrefix);
                        }
                    }

                    // Create an entry for the console log
                    ZipArchiveEntry consoleLogEntry = zipArchive.CreateEntry("ConsoleLog.txt", CompressionLevel.Optimal);
                    using (Stream entryStream = consoleLogEntry.Open())
                    {
                        using (StreamWriter sw = new StreamWriter(entryStream, Encoding.UTF8))
                        {
                            sw.Write(consoleLog);
                        }
                    }

                    // Create an entry for the raw game data
                    ZipArchiveEntry rawGameDataEntry = zipArchive.CreateEntry("RawGameData.json", CompressionLevel.Optimal);
                    using (Stream entryStream = rawGameDataEntry.Open())
                    {
                        using (StreamWriter sw = new StreamWriter(entryStream, Encoding.UTF8))
                        {
                            using (JsonWriter writer = new JsonTextWriter(sw))
                            {
                                serializer.Serialize(writer, serializableObject);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the game-specific trace data from a saved compressed file
        /// Also load the speech trace
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath"></param>
        /// <returns></returns>
        internal T DeSerializeObject<T>(string filePath)
        {
            Console.WriteLine("About to load recorded game data from file " + filePath + " - this may take a while");
            if (string.IsNullOrEmpty(filePath)) { return default(T); }

            T objectOut = default(T);

            // Change the cursor to a wait cursor while reading the large data file,
            // then restore the default cursor when finished.
            // Use the main window's UI thread if available.
            bool cursorChanged = false;
            try
            {
                try
                {
                    if (MainWindow.instance != null && !MainWindow.instance.IsDisposed)
                    {
                        MainWindow.instance.Invoke((MethodInvoker)(() =>
                        {
                            MainWindow.instance.Cursor = Cursors.WaitCursor;
                            try
                            {
                                if (MainWindow.instance.consoleTextBox != null && !MainWindow.instance.consoleTextBox.IsDisposed)
                                {
                                    MainWindow.instance.consoleTextBox.Cursor = Cursors.WaitCursor;
                                }
                            }
                            catch (Exception)
                            {
                                // Ignore per-control cursor failures.
                            }
                        }));
                        cursorChanged = true;
                    }
                    else
                    {
                        Cursor.Current = Cursors.WaitCursor;
                        cursorChanged = true;
                    }
                }
                catch (Exception)
                {
                    // Ignore cursor failures and continue with deserialization.
                }

                try
                {
                    switch (Path.GetExtension(filePath))
                    {
                        case ".zip":
                            { // New 4 in 1 file
                                try
                                {   // Now using zip
                                    var traceContents = UnzipTraceContents<T>(filePath);
                                    objectOut = traceContents.RawGameData;
                                    CrewChief.SpeechTrace.LoadDictionary(traceContents.SpeechTrace);
                                }
                                catch (Exception e)
                                {   // Originally a gzip file which Windows File Manager won't easily open
                                    try
                                    {
                                        var traceContents = UnGzipTraceContents<T>(filePath);
                                        objectOut = traceContents.RawGameData;
                                        CrewChief.SpeechTrace.LoadDictionary(traceContents.SpeechTrace);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Exception(ex, $"Can't unzip {filePath}");
                                        throw;
                                    }
                                }
                                break;
                            }
                        case ".txt":
                            { // Plain 4 in 1 file, not gzipped
                                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                                {
                                    using (StreamReader reader = new StreamReader(fileStream))
                                    { // (Identical to the code above for the zipped file)
                                        using (JsonTextReader jsonReader = new JsonTextReader(reader))
                                        {
                                            JsonSerializer ser = new JsonSerializer();
                                            var traceContents = ser.Deserialize<TraceContents<T>>(jsonReader);
                                            objectOut = traceContents.RawGameData;
                                            CrewChief.SpeechTrace.LoadDictionary(traceContents.SpeechTrace);
                                        }
                                    }
                                }
                                break;
                            }
                        // Legacy files
                        case ".cct":
                            {
                                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                                {
                                    using (GZipStream zipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                                    {
                                        using (StreamReader reader = new StreamReader(zipStream))
                                        {
                                            using (JsonTextReader jsonReader = new JsonTextReader(reader))
                                            {
                                                JsonSerializer ser = new JsonSerializer();
                                                objectOut = ser.Deserialize<T>(jsonReader);
                                            }
                                        }
                                    }
                                }
                                break;
                            }
                        // Even older
                        case ".gz":
                            {
                                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                                {
                                    using (GZipStream zipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                                    {
                                        Type outType = typeof(T);
                                        XmlSerializer serializer = new XmlSerializer(outType);
                                        using (XmlReader xmlReader = new XmlTextReader(zipStream))
                                        {
                                            objectOut = (T)serializer.Deserialize(xmlReader);
                                        }
                                    }
                                }
                                break;
                            }
                        // assume xml
                        default:
                            {
                                using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
                                {
                                    Type outType = typeof(T);
                                    XmlSerializer serializer = new XmlSerializer(outType);
                                    using (XmlReader reader = new XmlTextReader(fileStream))
                                    {
                                        objectOut = (T)serializer.Deserialize(reader);
                                    }
                                }
                                break;
                            }
                    }
                    Console.WriteLine("Done reading session data from: " + filePath);
                }
                finally
                {
                    // Restore cursor even if deserialization throws.
                    try
                    {
                        if (cursorChanged)
                        {
                            if (MainWindow.instance != null && !MainWindow.instance.IsDisposed)
                            {
                                MainWindow.instance.Invoke((MethodInvoker)(() =>
                                {
                                    MainWindow.instance.Cursor = Cursors.Default;
                                    try
                                    {
                                        if (MainWindow.instance.consoleTextBox != null && !MainWindow.instance.consoleTextBox.IsDisposed)
                                        {
                                            MainWindow.instance.consoleTextBox.Cursor = Cursors.Default;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        // Ignore per-control cursor restore failures.
                                    }
                                }));
                            }
                            else
                            {
                                Cursor.Current = Cursors.Default;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore cursor restore failures.
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to read raw game data: " + ex.Message);
            }
            return objectOut;
        }

        // (internal static for unit testing)
        internal static TraceContents<T> UnzipTraceContents<T>(string filePath)
        {
            TraceContents<T> traceContents = null;
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
            {
                using (ZipArchive zipArchive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    // Check for the presence of the new format files
                    var speechTraceEntry = zipArchive.GetEntry("SpeechTrace.json");
                    var consoleLogPrefixEntry = zipArchive.GetEntry("ConsoleLogPrefix.txt");
                    var consoleLogEntry = zipArchive.GetEntry("ConsoleLog.txt");
                    var rawGameDataEntry = zipArchive.GetEntry("RawGameData.json");

                    if (speechTraceEntry != null && consoleLogPrefixEntry != null && consoleLogEntry != null && rawGameDataEntry != null)
                    {
                        // Read the speech trace
                        Dictionary<long, string> speechTrace = null;
                        using (Stream entryStream = speechTraceEntry.Open())
                        {
                            using (StreamReader reader = new StreamReader(entryStream))
                            {
                                using (JsonTextReader jsonReader = new JsonTextReader(reader))
                                {
                                    JsonSerializer ser = new JsonSerializer();
                                    speechTrace = ser.Deserialize<Dictionary<long, string>>(jsonReader);
                                }
                            }
                        }

                        // Read the console log prefix
                        string[] consoleLogPrefix = null;
                        using (Stream entryStream = consoleLogPrefixEntry.Open())
                        {
                            using (StreamReader reader = new StreamReader(entryStream))
                            {
                                consoleLogPrefix = reader.ReadToEnd().Split('\n');
                            }
                        }

                        // Read the console log
                        string[] consoleLog = null;
                        using (Stream entryStream = consoleLogEntry.Open())
                        {
                            using (StreamReader reader = new StreamReader(entryStream))
                            {
                                consoleLog = reader.ReadToEnd().Split('\n');
                            }
                        }

                        // Read the raw game data
                        T rawGameData = default(T);
                        using (Stream entryStream = rawGameDataEntry.Open())
                        {
                            using (StreamReader reader = new StreamReader(entryStream))
                            {
                                using (JsonTextReader jsonReader = new JsonTextReader(reader))
                                {
                                    JsonSerializer ser = new JsonSerializer();
                                    rawGameData = ser.Deserialize<T>(jsonReader);
                                }
                            }
                        }

                        // Create the TraceContents object
                        traceContents = new TraceContents<T>(speechTrace, consoleLogPrefix, consoleLog, rawGameData);
                    }
                    else
                    {
                        // Fall back to the old format with all 4 types of data in a tuple
                        var traceContentsEntry = zipArchive.GetEntry("TraceContents");
                        if (traceContentsEntry != null)
                        {
                            using (Stream entryStream = traceContentsEntry.Open())
                            {
                                using (StreamReader reader = new StreamReader(entryStream))
                                {
                                    using (JsonTextReader jsonReader = new JsonTextReader(reader))
                                    {
                                        JsonSerializer ser = new JsonSerializer();
                                        traceContents = ser.Deserialize<TraceContents<T>>(jsonReader);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return traceContents;
        }

        /// <summary>
        /// Gzip no longer used as Windows doesn't easily read gzip files.
        /// Kept for backwards-compatibility
        /// </summary>
        /// <returns></returns>
        static TraceContents<T> UnGzipTraceContents<T>(string filePath)
        {
            TraceContents<T> traceContents;
            using (FileStream fileStream = new FileStream(filePath, FileMode.Open))
            {
                using (GZipStream zipStream = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    using (StreamReader reader = new StreamReader(zipStream))
                    {
                        using (JsonTextReader jsonReader = new JsonTextReader(reader))
                        {
                            JsonSerializer ser = new JsonSerializer();
                            traceContents = ser.Deserialize<TraceContents<T>>(jsonReader);
                        }
                    }
                }
            }
            return traceContents;
        }

        public virtual Boolean hasNewSpotterData()
        {
            return true;
        }

        public virtual Object getLatestGameData()
        {
            return null;
        }

        public virtual void stop()
        {
            // no op - only implemented by UDP reader
        }

        // NOTE: This needs to be synchronized, because disconnection happens from CrewChief.Run and MainWindow.Dispose.
        // Does not apply to network data feeds.
        public virtual void DisconnectFromProcess()
        {
            // Is called when game process exits or Stop button is pressed and run loop terminates.
            // Can be used to release resources.

            // no op - only implemented for rF2.
        }
    }
}
