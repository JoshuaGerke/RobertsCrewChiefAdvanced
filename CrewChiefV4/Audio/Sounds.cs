using CrewChiefV4.GameState;

using NAudio.Wave;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Security;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;

namespace CrewChiefV4.Audio
{
    public class SoundCache
    {
        // guard against other Threads closing the radio channel while a message set is being played.
        // Note this doesn't prevent interruptions (which allow the message fragments loop to complete while
        // not playing all the messages) - this guards against the channel being closed during a Pause operation
        public static Boolean IS_PLAYING = false;

        public const String TTS_IDENTIFIER = "TTS_IDENTIFIER";
        public const String DOWNLOAD_IDENTIFIER = "DOWNLOAD_IDENTIFIER";
        private readonly Boolean useAlternateBeeps = UserSettings.GetUserSettings().getBoolean("use_alternate_beeps");
        private readonly double minSecondsBetweenPersonalisedMessages = (double)UserSettings.GetUserSettings().getInt("min_time_between_personalised_messages");
        public static Boolean forceStereoPlayback;
        public static int forceResamplePlayback;
        public static Boolean recordVarietyData;
        public static Boolean dumpListOfUnvocalizedNames;
        public static Boolean eagerLoadSoundFiles;
        public static float ttsVolumeBoost;
        public static float spotterVolumeBoost;
        public static int ttsTrimStartMilliseconds;
        public static int ttsTrimEndMilliseconds;
        public static Boolean lazyLoadSubtitles;
        public static int pauseBetweenFragments;

        // all this global state is really gnarly, but it would be a major refactor to clean it up at this point.
        // effectively, SoundCache is a singleton, and designs for new features must take that into account
        private static LinkedList<String> dynamicLoadedSounds = new LinkedList<String>();
        public static Dictionary<String, SoundSet> soundSets = new Dictionary<String, SoundSet>();
        public static Dictionary<String, SingleSound> singleSounds = new Dictionary<String, SingleSound>();
        public static HashSet<String> availableDriverNames = new HashSet<String>();
        public static HashSet<String> availableDriverNamesForTrainee = new HashSet<String>();
        public static HashSet<String> availableDriverNamesForUI = new HashSet<String>();
        public static string[] availableDriverNamesForUIAsArray = new string[] { };
        public static HashSet<String> availableSounds = new HashSet<String>();
        internal static HashSet<String> availablePrefixesAndSuffixes = new HashSet<String>();

        private Boolean useSwearyMessages;
        private Boolean useMaleSounds;
        private static Boolean allowCaching;
        private String[] eventTypesToKeepCached;
        private const int maxSoundPlayerCacheSize = 500;
        private const int soundPlayerPurgeBlockSize = 100;
        internal static int currentSoundsLoaded;
        public static int activeSoundPlayerObjects;
        internal static int prefixesAndSuffixesCount = 0;

        private static Boolean purging = false;
        private Thread expireCachedSoundsThread = null;

        private DateTime lastPersonalisedMessageTime = DateTime.MinValue;

        internal static DateTime lastSwearyMessageTime = DateTime.MinValue;

        internal static SpeechSynthesizer synthesizer;

        public static Boolean hasSuitableTTSVoice = false;

        public static Boolean cancelLazyLoading = false;
        public static Boolean cancelDriverNameLoading = false;

        private static Dictionary<String, Tuple<int, int>> varietyData = new Dictionary<string, Tuple<int, int>>();

        internal static SingleSound currentlyPlayingSound;

        // cacheSoundsThread is initialized on the main thread only, so it is safe to sync on the main thread.
        public static Thread cacheSoundsThread;

        private static Thread loadDriverNameSoundsThread;

        // for logging
        internal static int swearySoundsSkipped = 0;
        internal static int maleSoundsSkipped = 0;

        static SoundCache()
        {
        forceStereoPlayback = UserSettings.GetUserSettings().getBoolean("force_stereo");
        forceResamplePlayback = UserSettings.GetUserSettings().getInt("force_resample");
        recordVarietyData = UserSettings.GetUserSettings().getBoolean("record_sound_variety_data");
        dumpListOfUnvocalizedNames = UserSettings.GetUserSettings().getBoolean("save_list_of_unvocalized_names");
        eagerLoadSoundFiles = UserSettings.GetUserSettings().getBoolean("load_sound_files_on_startup");
        ttsVolumeBoost = UserSettings.GetUserSettings().getFloat("tts_volume_boost");
        spotterVolumeBoost = UserSettings.GetUserSettings().getFloat("spotter_volume_boost");
        ttsTrimStartMilliseconds = UserSettings.GetUserSettings().getInt("tts_trim_start_milliseconds");
        ttsTrimEndMilliseconds = UserSettings.GetUserSettings().getInt("tts_trim_end_milliseconds");
        lazyLoadSubtitles = UserSettings.GetUserSettings().getBoolean("lazy_load_subtitles");
        pauseBetweenFragments = UserSettings.GetUserSettings().getInt("pause_between_message_fragments");
        }

        private static void loadExistingVarietyData()
        {
            if (SoundCache.recordVarietyData)
            {
                string path = DataFiles.sounds_variety_data;
                StringBuilder fileString = new StringBuilder();
                StreamReader file = null;
                try
                {
                    file = new StreamReader(path);
                    String line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if (!line.Trim().StartsWith("#"))
                        {
                            // split the line. Sound path, files count, played count, variety score
                            String[] lineData = line.Split(',');
                            varietyData[lineData[0]] = new Tuple<int, int>(int.Parse(lineData[1]), int.Parse(lineData[2]));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error reading file " + path + ": ");
                }
                finally
                {
                    if (file != null)
                    {
                        file.Close();
                    }
                }
            }
        }

        public static void saveVarietyData()
        {
            if (SoundCache.recordVarietyData)
            {
                string path = DataFiles.sounds_variety_data;
                StringBuilder fileString = new StringBuilder();
                TextWriter tw = new StreamWriter(path, false);
                List<SoundVarietyDataPoint> data = new List<SoundVarietyDataPoint>();
                foreach (KeyValuePair<String, Tuple<int, int>> entry in varietyData)
                {
                    data.Add(new SoundVarietyDataPoint(entry.Key, entry.Value.Item1, entry.Value.Item2));
                }
                data.Sort();
                foreach (SoundVarietyDataPoint dataPoint in data)
                {
                    tw.WriteLine(dataPoint.soundName + "," + dataPoint.numSounds + "," + dataPoint.timesPlayed + "," + dataPoint.score);
                }
                tw.Close();
            }
        }

        internal static void addUseToVarietyData(String soundPath, int soundsInThisSet)
        {
            // want the last 4 folders from the full sound path:
            String[] pathFragments = soundPath.Split('\\');
            if (pathFragments.Length > 3)
            {
                String interestingSoundPath = pathFragments[pathFragments.Length - 4] + "/" + pathFragments[pathFragments.Length - 3] +
                    "/" + pathFragments[pathFragments.Length - 2] + "/" + pathFragments[pathFragments.Length - 1];
                if (varietyData.ContainsKey(interestingSoundPath))
                {
                    varietyData[interestingSoundPath] = new Tuple<int, int>(soundsInThisSet, varietyData[interestingSoundPath].Item2 + 1);
                }
                else
                {
                    varietyData.Add(interestingSoundPath, new Tuple<int, int>(soundsInThisSet, 1));
                }
            }
        }

        // these collections are for a 2nd (trainee) crew chief that is allowed to hijack the output if
        // they can speak an entire phrase. There's a lot of assumptions in the code that Jim is the 1st crewchief
        // so although it may be theoretically possible to make Jim the trainee, it will probably break things.
        private Dictionary<String, SoundSet> soundSets2ndChief = new Dictionary<String, SoundSet>();
        private Dictionary<String, SingleSound> singleSounds2ndChief = new Dictionary<String, SingleSound>();
        private HashSet<String> availablePrefixesAndSuffixes2ndChief = new HashSet<String>();

        /// <summary>
        /// Soundpack updates may want to delete sounds, they do that by
        /// overwriting them with zero length files. Now delete them.
        /// </summary>
        public static void DeleteZeroLengthWavFiles(string rootFolder)
        {
            // Delete empty files
            DirectoryInfo rootDirectory = new DirectoryInfo(rootFolder);
            foreach (var file in rootDirectory.GetFiles("*.wav", SearchOption.AllDirectories))
            {
                if (file.Length == 0)
                {
                    try
                    {
                        file.Delete();
                        Log.Commentary($"Deleted unwanted sound file {file.FullName}");
                    }
                    catch (SecurityException ex)
                    {
                        Log.Error($"No permission to delete unwanted sound file {file.FullName}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Could not delete unwanted sound file {file.FullName}: {ex.Message}");
                        break;
                    }
                }
            }

            return; // Don't delete empty subfolders for... reasons

            // Delete empty subfolders
            //foreach (var subDir in rootDirectory.GetDirectories("*", SearchOption.AllDirectories))
            //{
            //    if (subDir.GetFiles().Length == 0 && subDir.GetDirectories().Length == 0)
            //    {
            //        try
            //        {
            //            subDir.Delete();
            //            Log.Commentary($"Deleted empty sound folder {subDir.FullName}");
            //        }
            //        catch (Exception ex)
            //        {
            //            Log.Error($"Could not delete unwanted sound folder {subDir.FullName}: {ex.Message}");
            //        }
            //    }
            //}
        }
        public SoundCache(
            DirectoryInfo soundsFolder,
            DirectoryInfo secondChief,
            DirectoryInfo sharedSoundsFolder,
            String[] eventTypesToKeepCached,
            Boolean useSwearyMessages, Boolean useMaleSounds, Boolean allowCaching, String selectedPersonalisation, bool verbose)
        {
            loadExistingVarietyData();

            // ensure the static state is nuked before we start updating it
            SoundCache.dynamicLoadedSounds.Clear();
            SoundCache.soundSets.Clear();
            SoundCache.singleSounds.Clear();
            SoundCache.availableDriverNames.Clear();
            SoundCache.availableDriverNamesForTrainee.Clear();
            SoundCache.availableDriverNamesForUI.Clear();
            SoundCache.availableSounds.Clear();
            SoundCache.availablePrefixesAndSuffixes.Clear();

            if (AudioPlayer.ttsOption != AudioPlayer.TTS_OPTION.NEVER)
            {
                SetUpTTS();
            }
            SoundCache.currentSoundsLoaded = 0;
            SoundCache.activeSoundPlayerObjects = 0;
            this.eventTypesToKeepCached = eventTypesToKeepCached;
            this.useSwearyMessages = useSwearyMessages;
            this.useMaleSounds = useMaleSounds;
            SoundCache.allowCaching = allowCaching;
            DirectoryInfo[] sharedSoundsFolders = sharedSoundsFolder.GetDirectories();
            foreach (DirectoryInfo soundFolder in sharedSoundsFolders)
            {
                if (soundFolder.Name == "fx")
                {
                    // these are eagerly loaded on the main thread, soundPlayers are created and they're always in the SoundPlayer cache.
                    prepareFX(soundFolder, verbose);
                }
            }

            DirectoryInfo[] soundsFolders = soundsFolder.GetDirectories();
            foreach (DirectoryInfo soundFolder in soundsFolders)
            {
                DirectoryInfo soundFolder2ndChief = null;
                if (secondChief != null)
                {
                    soundFolder2ndChief = new DirectoryInfo(secondChief + "\\" + soundFolder.Name);
                    if (!soundFolder2ndChief.Exists)
                    {
                        soundFolder2ndChief.Create();
                    }
                }

                switch (soundFolder.Name)
                {
                    case "personalisations":
                        {
                            if (selectedPersonalisation != AudioPlayer.NO_PERSONALISATION_SELECTED)
                            {
                                // these are eagerly loaded on the main thread, soundPlayers are created and they're always in the SoundPlayer cache.
                                // If the number of prefixes and suffixes keeps growing this will need to be moved to a background thread but take care
                                // to ensure the objects which hold the sounds are all created on the main thread, with only the file reading and
                                // SoundPlayer creation part done in the background (just like we do for voice messages).

                                try
                                {
                                    if (!Directory.Exists(soundFolder.FullName + "\\" + selectedPersonalisation))
                                    {
                                        createCompositePrefixesAndSuffixes(soundsFolder, soundFolder, selectedPersonalisation);
                                    }
                                    if (secondChief != null && !Directory.Exists(soundFolder2ndChief.FullName + "\\" + selectedPersonalisation))
                                    {
                                        createCompositePrefixesAndSuffixes(secondChief, soundFolder2ndChief, selectedPersonalisation);
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.Exception(e, "Failed to build personalisation set for " + selectedPersonalisation + ": ");
                                }
                                preparePrefixesAndSuffixes(soundFolder, selectedPersonalisation, verbose, SoundCache.soundSets, SoundCache.availablePrefixesAndSuffixes);
                                if (soundFolder2ndChief != null)
                                {
                                    preparePrefixesAndSuffixes(soundFolder2ndChief, selectedPersonalisation, verbose, this.soundSets2ndChief, this.availablePrefixesAndSuffixes2ndChief);
                                }
                            }
                            else
                            {
                                Console.WriteLine("No name has been selected for personalised messages");
                            }

                            break;
                        }

                    case "voice":
                        {
                            // these are eagerly loaded on a background Thread. For frequently played sounds we create soundPlayers
                            // and hold them in the cache. For most sounds we just load the file(s) and create sound players when needed,
                            // allowing them to be cached until evicted.

                            // this creates empty sound objects:
                            prepareVoiceWithoutLoading(soundFolder, new DirectoryInfo(sharedSoundsFolder.FullName + "/voice"), verbose, SoundCache.availableSounds, SoundCache.soundSets);
                            if (soundFolder2ndChief != null)
                            {
                                var disposable = new HashSet<String>();
                                prepareVoiceWithoutLoading(soundFolder2ndChief, new DirectoryInfo(sharedSoundsFolder.FullName + "/voice"), verbose, disposable, this.soundSets2ndChief, add_shared: false);
                            }

                            // now spawn a Thread to load the sound files (and in some cases soundPlayers) in the background:
                            if (allowCaching && eagerLoadSoundFiles && !SoundCache.cancelLazyLoading)
                            {
                                // NOTE: this must be a UI thread.
                                ThreadManager.UnregisterResourceThread(SoundCache.cacheSoundsThread);
                                SoundCache.cacheSoundsThread = new Thread(() =>
                                {
                                    DateTime start = DateTime.UtcNow;
                                    Thread.CurrentThread.IsBackground = true;
                                    try
                                    {
                                        lock (soundSets)
                                        {
                                            var allSoundSets = soundSets.Values.Union(soundSets2ndChief.Values);
                                            // load the permanently cached sounds first, then the rest
                                            foreach (SoundSet soundSet in allSoundSets)
                                            {
                                                if (SoundCache.cancelLazyLoading)
                                                {
                                                    break;
                                                }
                                                else if (soundSet.cacheSoundPlayersPermanently)
                                                {
                                                    soundSet.loadAll();
                                                }
                                            }
                                            foreach (SoundSet soundSet in allSoundSets)
                                            {
                                                if (SoundCache.cancelLazyLoading)
                                                {
                                                    break;
                                                }
                                                else if (!soundSet.cacheSoundPlayersPermanently)
                                                {
                                                    soundSet.loadAll();
                                                }
                                            }
                                        }

                                        if (verbose)
                                        {
                                            if (AudioPlayer.playWithNAudio)
                                            {
                                                Console.WriteLine("Took " + (DateTime.UtcNow - start).TotalSeconds.ToString("0.00") + "s to lazy load remaining message sounds, there are now " +
                                                    SoundCache.currentSoundsLoaded + " loaded message sounds");
                                            }
                                            else
                                            {
                                                Console.WriteLine("Took " + (DateTime.UtcNow - start).TotalSeconds.ToString("0.00") + "s to lazy load remaining message sounds, there are now " +
                                                    SoundCache.currentSoundsLoaded + " loaded message sounds with " + SoundCache.activeSoundPlayerObjects + " active SoundPlayer objects");
                                            }
                                        }
                                        if (swearySoundsSkipped > 0)
                                        {
                                            Console.WriteLine("Skipped " + swearySoundsSkipped + " sweary sounds");
                                        }
                                        if (maleSoundsSkipped > 0)
                                        {
                                            Console.WriteLine("Skipped " + maleSoundsSkipped + " male-only sounds");
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        Log.Exception(e, "Error construction sounds cache: ");
                                    }
                                });
                                SoundCache.cacheSoundsThread.Name = "SoundCache.cacheSoundsThread";
                                ThreadManager.RegisterResourceThread(SoundCache.cacheSoundsThread);
                                SoundCache.cacheSoundsThread.Start();
                            }

                            break;
                        }

                    case "driver_names":
                        {
                            if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
                            {
                                // The folder of driver names is processed on the main thread and objects are created to hold the sounds,
                                // but the sound files are lazy-loaded on session start, along with the corresponding SoundPlayer objects.
                                prepareDriverNamesWithoutLoading(soundFolder, verbose, SoundCache.singleSounds, SoundCache.availableDriverNames, SoundCache.availableDriverNamesForUI);
                                SoundCache.availableDriverNamesForUIAsArray = SoundCache.availableDriverNamesForUI.ToArray();
                                if (soundFolder2ndChief != null)
                                {
                                    var disposable = new HashSet<string>();
                                    prepareDriverNamesWithoutLoading(soundFolder2ndChief, verbose, this.singleSounds2ndChief, SoundCache.availableDriverNamesForTrainee, disposable);
                                }
                            }

                            break;
                        }
                }
            }
            if (verbose)
            {
                if (AudioPlayer.playWithNAudio)
                {
                    if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
                    {
                        Console.WriteLine("Finished preparing sounds cache, found " + singleSounds.Count + " driver names and " + soundSets.Count +
                            " sound sets. Loaded " + SoundCache.currentSoundsLoaded + " message sounds");
                    }
                    else if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Rally)
                    {
                        Console.WriteLine("Finished preparing sounds cache, found " + singleSounds.Count + " beep sounds and " + soundSets.Count +
                            " sound sets. Loaded " + SoundCache.currentSoundsLoaded + " message sounds");
                    }
                }
                else
                {
                    if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
                    {
                        Console.WriteLine("Finished preparing sounds cache, found " + singleSounds.Count + " driver names and " + soundSets.Count +
                           " sound sets. Loaded " + SoundCache.currentSoundsLoaded + " message sounds with " + SoundCache.activeSoundPlayerObjects + " active SoundPlayer objects");
                    }
                    else if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Rally)
                    {
                        Console.WriteLine("Finished preparing sounds cache, found " + singleSounds.Count + " beep sounds and " + soundSets.Count +
                           " sound sets. Loaded " + SoundCache.currentSoundsLoaded + " message sounds with " + SoundCache.activeSoundPlayerObjects + " active SoundPlayer objects");
                    }
                }
                if (secondChief != null)
                {
                    Console.WriteLine($"trainee chief {secondChief} has {singleSounds2ndChief.Count} names and {soundSets2ndChief.Count} sound sets.");
                }

                if (prefixesAndSuffixesCount > 0)
                {
                    Console.WriteLine(prefixesAndSuffixesCount + " sounds have personalisations");
                }
            }
        }

        /// <summary>
        /// Initializes the text-to-speech (TTS) engine and selects a suitable installed voice for speech synthesis.
        /// </summary>
        /// <remarks>This method attempts to find and select a male adult voice from the
        /// installed TTS voices. If no such voice is found, it defaults to the first available voice, if any.
        /// If no voices are installed or initialization fails, TTS will be unavailable.</remarks>
        /// <returns>true if a TTS voice is available.</returns>
        public static bool SetUpTTS()
        {
            string ttsVoice = null;
            try
            {
                if (synthesizer != null)
                {
                    try
                    {
                        synthesizer.Dispose();
                        synthesizer = null;
                    }
                    catch (Exception e) { Log.Exception(e); }
                }
                synthesizer = new SpeechSynthesizer();
                Boolean hasMale = false;
                Boolean hasAdult = false;
                Boolean hasSenior = false;
                var voices = new List<InstalledVoice>(synthesizer.GetInstalledVoices().Where(v => v.Enabled));
                foreach (var voice in voices)
                {
                    Log.Debug(() => $"Available TTS voice: {voice.VoiceInfo.Name}");
                    if (voice.VoiceInfo.Age == VoiceAge.Adult)
                    {
                        hasAdult = true;
                    }
                    if (voice.VoiceInfo.Age == VoiceAge.Senior)
                    {
                        hasSenior = true;
                    }
                    if (voice.VoiceInfo.Gender == VoiceGender.Male)
                    {
                        hasMale = true;
                    }
                    if (hasMale && (hasAdult || hasSenior))
                    {
                        ttsVoice = voice.VoiceInfo.Name;
                        hasSuitableTTSVoice = true;
                        Log.Commentary($"Using TTS voice {ttsVoice}");
                        break;
                    }
                }
                if (ttsVoice == null)
                {
                    Console.WriteLine("No suitable (male adult) TTS voice pack found in the following list:");
                    foreach (var voice in voices)
                    {
                        Console.WriteLine("  " + voice.VoiceInfo.Name);
                    }
                    Console.WriteLine("TTS will only be used in response to voice commands (and will probably sound awful). " +
                        "US versions of Windows 8.1 and Windows 10/11 should be able to use Microsoft's 'David' voice - " +
                        "this can be selected in the Control Panel");
                    Console.WriteLine(@"https://stackoverflow.com/a/69219822/4108941 fix may work for you");
                    hasSuitableTTSVoice = false;
                    if (synthesizer.GetInstalledVoices().Count >= 1)
                    {
                        ttsVoice = synthesizer.GetInstalledVoices()[0].VoiceInfo.Name;
                        Console.WriteLine("Defaulting to voice " + ttsVoice);
                    }
                    else
                    {
                        Log.Warning("No TTS voices installed at all");
                    }
                }

                // this appears to just hang indefinitely. So don't bother trying to set it and let the system use the default voice
                // which will probably be shit, but MS TTS is shit anyway and now it's even shitter because it crashes the fucking
                // app on start up. Nobbers.
                // synthesizer.SelectVoiceByHints(VoiceGender.Male, hasAdult ? VoiceAge.Adult : VoiceAge.Senior);
                if (ttsVoice != null)
                {
                    synthesizer.SelectVoice(ttsVoice);
                    synthesizer.Volume = 100;
                    synthesizer.Rate = 0;
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Unable to initialise the TTS engine, TTS will not be available. " +
                            "Check a suitable Microsoft TTS voice pack is installed");
                AudioPlayer.ttsOption = AudioPlayer.TTS_OPTION.NEVER;
            }
            return (ttsVoice != null);
        }

        public static void InterruptCurrentlyPlayingSound(Boolean blockBeeps)
        {
            // only works with nAudio
            if (AudioPlayer.playWithNAudio && currentlyPlayingSound != null)
            {
                if (blockBeeps || !currentlyPlayingSound.isBleep)
                {
                    Boolean blockedABeep = currentlyPlayingSound.Stop();
                    if (blockedABeep)
                    {
                        PlaybackModerator.BlockNAudioPlaybackFor(500);
                    }
                }
            }
        }

        public static void loadSingleSound(String soundName, String fullPath, String subtitle = null)
        {
            if (!singleSounds.ContainsKey(soundName))
            {
                SingleSound singleSound = new SingleSound(fullPath, true, true, false, subtitle);
                singleSounds.Add(soundName, singleSound);
            }
        }

        public static Boolean hasSingleSound(String soundName)
        {
            return availableDriverNames.Contains(soundName) || singleSounds.ContainsKey(soundName);
        }

        public static void loadDriverNameSounds(HashSet<String> names)
        {
            Console.WriteLine("Loading driver name sounds: " + Environment.NewLine + String.Join(", ", names));
            if (SoundCache.cancelDriverNameLoading
                || GlobalBehaviourSettings.racingType != CrewChief.RacingType.Circuit)
                return;

            // Trace debugging only note: During trace playback session changes are very fast, so kill previous thread as it still being alive is not an indicator of a problem.
            ThreadManager.UnregisterTemporaryThread(SoundCache.loadDriverNameSoundsThread, killIfAlive: MainWindow.playingBackTrace);
            SoundCache.loadDriverNameSoundsThread = new Thread(() =>
            {
                try
                {
                    int loadedCount = 0;
                    DateTime start = DateTime.UtcNow;
                    // No need to early terminate this thread on form close, because it only loads driver names in
                    // a session, which isn't 1000's.
                    foreach (String name in names)
                    {
                        if (SoundCache.cancelDriverNameLoading)
                            return;

                        loadedCount++;
                        loadDriverNameSound(name, false);
                    }
                    if (AudioPlayer.playWithNAudio)
                    {
                        Console.WriteLine("Took " + (DateTime.UtcNow - start).TotalSeconds.ToString("0.00") + " seconds to load " +
                            loadedCount + " driver name sounds. There are now " + SoundCache.currentSoundsLoaded +
                            " sound files loaded");
                    }
                    else
                    {
                        Console.WriteLine("Took " + (DateTime.UtcNow - start).TotalSeconds.ToString("0.00") + " seconds to load " +
                            loadedCount + " driver name sounds. There are now " + SoundCache.currentSoundsLoaded +
                            " sound files loaded with " + SoundCache.activeSoundPlayerObjects + " active SoundPlayer objects");
                    }
                }
                catch (Exception ex)
                {
                    Utilities.ReportException(ex, "Error caching driver names", needReport: false);
                }
            });
            SoundCache.loadDriverNameSoundsThread.Name = "SoundCache.loadDriverNameSoundsThread";
            ThreadManager.RegisterTemporaryThread(SoundCache.loadDriverNameSoundsThread);
            SoundCache.loadDriverNameSoundsThread.Start();
        }

        public static void loadDriverNameSound(String name, Boolean isMidSession = true)
        {
            if (name == null || name.Length == 0)
            {
                return;
            }
            if (isMidSession)
            {
                Console.WriteLine("Loading (mid-session joined) opponent name sound: " + Environment.NewLine + name);
            }
            Boolean isInAvailableNames = availableDriverNames.Contains(name);
            if (dumpListOfUnvocalizedNames && !isInAvailableNames)
            {
                DriverNameHelper.unvocalizedNames.Add(name);
            }
            // if the name is in the availableDriverNames array then we have a sound file for it, so we can load it
            if (!allowCaching)
            {
                return;
            }
            if (isInAvailableNames)
            {
                singleSounds[name].LoadAndCacheSound();
                if (!purging)
                {
                    lock (SoundCache.dynamicLoadedSounds)
                    {
                        try
                        {
                            SoundCache.dynamicLoadedSounds.Remove(name);
                            SoundCache.dynamicLoadedSounds.AddLast(name);
                        }
                        catch (Exception e)
                        {
                            Log.Exception(e, "Error reordering sound cache while adding a driver name: ");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Unvocalized driver name: " + name);
            }
        }

        public Boolean eventHasPersonalisedPrefixOrSuffix(String eventName)
        {
            SoundSet ss = null;
            if (soundSets.TryGetValue(eventName, out ss))
            {
                return ss.hasPrefixOrSuffix;
            }
            return false;
        }

        internal Boolean personalisedMessageIsDue()
        {
            double secondsSinceLastPersonalisedMessage = (GameStateData.CurrentTime - lastPersonalisedMessageTime).TotalSeconds;
            Boolean due = false;
            if (minSecondsBetweenPersonalisedMessages == 0)
            {
                due = false;
            }
            else if (secondsSinceLastPersonalisedMessage > minSecondsBetweenPersonalisedMessages)
            {
                // we can now select a personalised message, but we don't always do this - the probability is based
                // on the time since the last one
                due = Utilities.random.NextDouble() < 1.2 - minSecondsBetweenPersonalisedMessages / secondsSinceLastPersonalisedMessage;
            }
            return due;
        }

        private void moveToTopOfCache(String soundName)
        {
            if (!purging)
            {
                lock (SoundCache.dynamicLoadedSounds)
                {
                    try
                    {
                        SoundCache.dynamicLoadedSounds.Remove(soundName);
                        SoundCache.dynamicLoadedSounds.AddLast(soundName);
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Error reordering sound cache while playing a sound: ");
                    }
                }
            }
        }

        public static bool trainee_active()
        {
            return AudioPlayer.TraineeChiefAvailable;
        }

        /*
         * canInterrupt will be true for regular messages triggered by the app's normal event logic. When a message
         * is played from the 'immediate' queue this will be false (spotter calls, command responses, some edge cases
         * where the message is time-critical). If this flag is true the presence of a message in the immediate queue
         * can make the app skip playing this sound.
         */
        public void Play(List<String> soundNames, SoundMetadata soundMetadata, bool mute = false)
        {
            List<SingleSound> singleSoundsToPlay = null;
            SoundSet prefix = null;
            SoundSet suffix = null;
            List<string> cacheable = null;
               
            if (trainee_active() && (singleSounds2ndChief.Count > 0 || soundSets2ndChief.Count > 0))
            {
                (singleSoundsToPlay, prefix, suffix, cacheable) = Play_(soundNames, singleSounds2ndChief, soundSets2ndChief);
            }
            if (singleSoundsToPlay == null || singleSoundsToPlay.Count == 0)
            {
                (singleSoundsToPlay, prefix, suffix, cacheable) = Play_(soundNames, SoundCache.singleSounds, SoundCache.soundSets);
            }
            else
            {
                Log.Debug(() => $"trainee crew chief hijacked {String.Join(",", soundNames)}");
            }

            if (singleSoundsToPlay == null || singleSoundsToPlay.Count == 0)
            {
                Console.WriteLine("No playable sounds could be found for " + String.Join(", ", soundNames));
                return;
            }

            try
            {
                foreach (var soundName in cacheable)
                {
                    moveToTopOfCache(soundName);
                }

                if (prefix != null)
                {
                    SingleSound prefixSound = prefix.getSingleSound(false);
                    if (prefixSound != null)
                    {
                        singleSoundsToPlay.Insert(0, prefixSound);
                        lastPersonalisedMessageTime = GameStateData.CurrentTime;
                    }
                }
                if (suffix != null)
                {
                    SingleSound suffixSound = suffix.getSingleSound(false);
                    if (suffixSound != null)
                    {
                        singleSoundsToPlay.Add(suffixSound);
                        lastPersonalisedMessageTime = GameStateData.CurrentTime;
                    }
                }
                if (SubtitleManager.enableSubtitles)
                {
                    SubtitleManager.AddPhrase(singleSoundsToPlay, soundMetadata);
                }

                if (mute)
                {
                    return;
                }

                SoundCache.IS_PLAYING = true;
                int firstSoundPosition = prefix == null ? 0 : 1;
                int lastSoundPosition = suffix == null ? singleSoundsToPlay.Count - 1 : singleSoundsToPlay.Count - 2;
                for (int i=0; i<singleSoundsToPlay.Count; i++)
                {
                    SingleSound singleSound = singleSoundsToPlay[i];
                    if (singleSound.isPause)
                    {
                        Thread.Sleep(singleSound.pauseLength);
                    }
                    else
                    {
                        singleSound.Play(soundMetadata);
                    }
                    // can add a pause between sounds (so not at the start or end of the list), skipping a prefix or suffix (personalisation)
                    bool addPause = SoundCache.pauseBetweenFragments > 0 && i >= firstSoundPosition && i < lastSoundPosition;
                    if (addPause)
                    {
                        Thread.Sleep(SoundCache.pauseBetweenFragments);
                    }
                }
            }
            finally
            {
                SoundCache.IS_PLAYING = false;
            }
        }

        // the single sounds for the sound names, and a prefix/suffix if relevant.
        //
        // all parameters will be null if the result is not playable. This should never happen
        // if the singleSounds and soundSets are for the 1st crewchief and the soundnames have
        // previously been validated against it.
        //
        // the final collection is the list of entries that should be given cache priority.
        private (List<SingleSound>, SoundSet, SoundSet, List<String>) Play_(
            List<String> soundNames,
            Dictionary<string, SingleSound> singleSounds,
            Dictionary<string, SoundSet> soundSets)
        {
            SoundSet prefix = null;
            SoundSet suffix = null;
            List<SingleSound> singleSoundsToPlay = new List<SingleSound>();
            List<String> cacheable = new List<String>();
            foreach (String soundName in soundNames)
            {
                if (soundName.StartsWith(AudioPlayer.PAUSE_ID))
                {
                    int pauseLength = 500;
                    try
                    {
                        String[] split = soundName.Split(':');
                        if (split.Count() == 2)
                        {
                            pauseLength = int.Parse(split[1]);
                        }
                    }
                    catch (Exception e) { Log.Exception(e); }
                    singleSoundsToPlay.Add(new SingleSound(pauseLength));
                }
                else if (soundName.StartsWith(TTS_IDENTIFIER))
                {
                    SingleSound singleSound = null;
                    if (!singleSounds.TryGetValue(soundName, out singleSound))
                    {
                        singleSound = new SingleSound(soundName.Substring(TTS_IDENTIFIER.Count()));
                        singleSounds.Add(soundName, singleSound);
                    }
                    cacheable.Add(soundName);
                    singleSoundsToPlay.Add(singleSound);
                }
                else if (soundName.StartsWith(DOWNLOAD_IDENTIFIER))
                {
                    SingleSound singleSound = null;
                    if (!singleSounds.TryGetValue(soundName, out singleSound))
                    {
                        singleSound = new SingleSound(soundName.Substring(DOWNLOAD_IDENTIFIER.Count()), true);
                        singleSounds.Add(soundName, singleSound);
                    }
                    cacheable.Add(soundName);
                    singleSoundsToPlay.Add(singleSound);
                }
                else
                {
                    Boolean preferPersonalised = personalisedMessageIsDue();
                    SingleSound singleSound = null;
                    SoundSet soundSet = null;
                    if (soundSets.TryGetValue(soundName, out soundSet))
                    {
                        // double check whether this soundSet wants to allow personalisations at this point -
                        // this prevents the app always choosing the personalised version of a sound if this sound is infrequent
                        if (soundSet.forceNonPersonalisedVersion())
                        {
                            preferPersonalised = false;
                        }
                        singleSound = soundSet.getSingleSound(preferPersonalised);
                        if (!soundSet.cacheSoundPlayersPermanently)
                        {
                            cacheable.Add(soundName);
                        }
                    }
                    else if (singleSounds.TryGetValue(soundName, out singleSound))
                    {
                        if (!singleSound.cacheSoundPlayerPermanently)
                        {
                            cacheable.Add(soundName);
                        }
                    }

                    if (singleSound == null)
                    {
                        return (null, null, null, null);
                    }
                    else
                    {
                        // hack... we double check the prefer setting here and only play the prefix / suffix if it's true.
                        // The list without prefixes and suffixes includes items which have optional ones, so we might want to
                        // play a sound that can have the prefix / suffix, but not the associated prefix / suffix
                        if (preferPersonalised && singleSound.prefixSoundSet != null)
                        {
                            prefix = singleSound.prefixSoundSet;
                        }
                        if (preferPersonalised && singleSound.suffixSoundSet != null)
                        {
                            suffix = singleSound.suffixSoundSet;
                        }
                        singleSoundsToPlay.Add(singleSound);
                    }
                }
            }
            return (singleSoundsToPlay, prefix, suffix, cacheable);
        }

        /*
         * canInterrupt will be true for regular messages triggered by the app's normal event logic. When a message
         * is played from the 'immediate' queue this will be false (spotter calls, command responses, some edge cases
         * where the message is time-critical). If this flag is true the presence of a message in the immediate queue
         * can make the app skip playing this sound.
         */
        public void Play(String soundName, SoundMetadata soundMetadata, Boolean mute = false)
        {
            List<String> l = new List<String>();
            l.Add(soundName);
            Play(l, soundMetadata, mute);
        }

        public void ExpireCachedSounds()
        {
            if (!purging && SoundCache.activeSoundPlayerObjects > maxSoundPlayerCacheSize)
            {
                purging = true;
                ThreadManager.UnregisterTemporaryThread(expireCachedSoundsThread);
                expireCachedSoundsThread = new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    int purgeCount = 0;
                    try
                    {
                        LinkedListNode<String> soundToPurge;
                        lock (SoundCache.dynamicLoadedSounds)
                        {
                            soundToPurge = SoundCache.dynamicLoadedSounds.First;
                        }
                        // No need to support cancellation of this thread, as it is not slow enough and we can wait for it.
                        while (soundToPurge != null && purgeCount <= soundPlayerPurgeBlockSize)
                        {
                            String soundToPurgeValue = soundToPurge.Value;
                            SoundSet soundSet = null;
                            SingleSound singleSound = null;
                            if (soundSets.TryGetValue(soundToPurgeValue, out soundSet))
                            {
                                purgeCount += soundSet.UnLoadAll();
                            }
                            else if (singleSounds.TryGetValue(soundToPurgeValue, out singleSound))
                            {
                                if (singleSound.UnLoad())
                                {
                                    purgeCount++;
                                }
                            }
                            lock (SoundCache.dynamicLoadedSounds)
                            {
                                var nextSoundToPurge = soundToPurge.Next;
                                SoundCache.dynamicLoadedSounds.Remove(soundToPurge);
                                soundToPurge = nextSoundToPurge;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Error purging sounds from cache: ");
                    }
                    finally
                    {
                        watch.Stop();
                        var elapsedMs = watch.ElapsedMilliseconds;
                        Console.WriteLine("Purged " + purgeCount + " sounds in " + elapsedMs + "ms, there are now " + SoundCache.activeSoundPlayerObjects + " active SoundPlayer objects");
                        purging = false;
                    }
                });
                expireCachedSoundsThread.Name = "SoundCache.expireCachedSoundsThread";
                ThreadManager.RegisterTemporaryThread(expireCachedSoundsThread);
                expireCachedSoundsThread.Start();
            }
        }

        public void StopAndUnloadAll()
        {
            if (synthesizer != null)
            {
                try
                {
                    synthesizer.Dispose();
                    synthesizer = null;
                }
                catch (Exception e) {Log.Exception(e);}
            }
            lock(soundSets)
            {
                foreach (SoundSet soundSet in soundSets.Values.Union(soundSets2ndChief.Values))
                {
                    try
                    {
                        soundSet.StopAll();
                        soundSet.UnLoadAll();
                    }
                    catch (Exception e) {Log.Exception(e);}
                }
                foreach (SingleSound singleSound in singleSounds.Values.Union(singleSounds2ndChief.Values))
                {
                    try
                    {
                        singleSound.Stop();
                        singleSound.UnLoad();
                    }
                    catch (Exception e) {Log.Exception(e);}
                }
            }

        }

        //public void StopAll()
        //{
        //    foreach (SoundSet soundSet in soundSets.Values.Union(soundSets2ndChief.Values))
        //    {
        //        try
        //        {
        //            soundSet.StopAll();
        //        }
        //        catch (Exception e) {Log.Exception(e);}
        //    }
        //    foreach (SingleSound singleSound in singleSounds.Values.Union(singleSounds2ndChief.Values))
        //    {
        //        try {
        //            singleSound.Stop();
        //        }
        //        catch (Exception e) {Log.Exception(e);}
        //    }
        //}

        private void prepareFX(DirectoryInfo fxSoundDirectory, bool verbose)
        {
            FileInfo[] bleepFiles = fxSoundDirectory.GetFiles();
            String alternate_prefix = useAlternateBeeps ? "alternate_" : "";
            String opposite_prefix = !useAlternateBeeps ? "alternate_" : "";
            if(verbose)
            {
                Console.WriteLine("Preparing sound effects");
            }

            // For fx with additional alternate versions (prefixed alternate_)
            String[] alternatives = { "start_bleep", "end_bleep", "short_start_bleep" };
            // For fx with NO alternate versions
            String[] plains = { "listen_start_sound", "listen_end_sound", "pace_notes_recording_start_stop_bleep", "drs_detected_bleep", "drs_available_bleep" };
            foreach (FileInfo bleepFile in bleepFiles)
            {
                if (bleepFile.Name.EndsWith(".wav"))
                {
                    foreach (string nameWithSuffix in alternatives)
                    {
                        maybeLoadFX(bleepFile, alternate_prefix, nameWithSuffix, nameWithSuffix);
                        maybeLoadFX(bleepFile, opposite_prefix, nameWithSuffix, "alternate_" + nameWithSuffix);
                    }
                    foreach (string nameWithSuffix in plains)
                    {
                        maybeLoadFX(bleepFile, "", nameWithSuffix, nameWithSuffix);
                    }
                }
            }
            if(verbose)
            {
                Console.WriteLine("Prepare sound effects completed");
            }
        }

        /// <summary>
        /// Tests <c>bleepFile</c> against name of sound we are looking to load.
        ///
        /// If there is a positive match, loads sound and caches under given <c>key</c>.
        /// Allows "_bleep" and "_sound" suffixes likewise. A prefix is an optional "alternate_"
        /// to accommodate alternative beep sound settings.
        /// </summary>
        private void maybeLoadFX(FileInfo bleepFile, string namePrefix, string nameWithSuffix, string key)
        {
            string name = nameWithSuffix;
            if (nameWithSuffix.EndsWith("_bleep"))
            {
                name = nameWithSuffix.Substring(0, nameWithSuffix.Length - "_bleep".Length);
            } else if (nameWithSuffix.EndsWith("_sound"))
            {
                name = nameWithSuffix.Substring(0, nameWithSuffix.Length - "_sound".Length);
            }
            if (!bleepFile.Name.StartsWith(namePrefix + name) || singleSounds.ContainsKey(key))
            {
                return;
            }
            SingleSound sound = new SingleSound(bleepFile.FullName, true, allowCaching, allowCaching);
            sound.isBleep = true;
            if (eagerLoadSoundFiles)
            {
                sound.LoadAndCacheFile();
            }
            singleSounds.Add(key, sound);
            availableSounds.Add(key);
        }

        // the collections are mutated; we use variables that intentionally shadow global mutable state
        // because we want to be able to do this for multiple voices. Ideally we wouldn't have global
        // state but getting rid of it would be a big refactor.
        private void prepareVoiceWithoutLoading(DirectoryInfo voiceDirectory, DirectoryInfo sharedVoiceDirectory, bool verbose,
            HashSet<String> availableSounds,
            Dictionary<String, SoundSet> soundSets,
            Boolean add_shared = true)
        {
            if (verbose)
            {
                Console.WriteLine("Preparing voice messages");
            }
            DirectoryInfo[] eventFolders = null;
            if (add_shared && !String.Equals(voiceDirectory.FullName, sharedVoiceDirectory.FullName, StringComparison.InvariantCultureIgnoreCase))
            {
                // This is the case of non-default Chief pack.
                if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
                {
                    // Get shared voice directories (spotter sounds).
                    DirectoryInfo[] spotterFolders = sharedVoiceDirectory.GetDirectories("spotter*");
                    DirectoryInfo[] radioCheckFolders = sharedVoiceDirectory.GetDirectories("radio_check*");

                    // Get redirected voice directories.  Exclude co-driver directories.
                    DirectoryInfo[] voiceFolders = voiceDirectory.GetDirectories().Where(d => !d.Name.StartsWith("codriver")).ToArray();

                    eventFolders = new DirectoryInfo[spotterFolders.Length + radioCheckFolders.Length + voiceFolders.Length];
                    spotterFolders.CopyTo(eventFolders, 0);
                    radioCheckFolders.CopyTo(eventFolders, spotterFolders.Length);
                    voiceFolders.CopyTo(eventFolders, spotterFolders.Length + radioCheckFolders.Length);
                }
                else if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Rally)
                {
                    // Get shared voice directories (codriver sounds).
                    DirectoryInfo[] codriverFolders = sharedVoiceDirectory.GetDirectories("codriver*");

                    // Get redirected voice directories.  Exclude directories irrelevant for rally.
                    DirectoryInfo[] voiceFolders = voiceDirectory.GetDirectories().Where(
                        d => (d.Name.StartsWith("acknowledge")
                            || d.Name.StartsWith("numbers")
                            || d.Name.StartsWith("alarm_clock"))).ToArray();

                    eventFolders = new DirectoryInfo[codriverFolders.Length + voiceFolders.Length];
                    codriverFolders.CopyTo(eventFolders, 0);
                    voiceFolders.CopyTo(eventFolders, codriverFolders.Length);
                }
            }
            else
            {
                if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit)
                {
                    eventFolders = voiceDirectory.GetDirectories().Where(d => !d.Name.StartsWith("codriver")).ToArray();
                }
                else if (GlobalBehaviourSettings.racingType == CrewChief.RacingType.Rally)
                {
                    eventFolders = voiceDirectory.GetDirectories().Where(
                        d => (d.Name.StartsWith("codriver")
                            || d.Name.StartsWith("acknowledge")
                            || d.Name.StartsWith("numbers")
                            || d.Name.StartsWith("alarm_clock"))).ToArray();
                }
            }

            foreach (DirectoryInfo eventFolder in eventFolders)
            {
                Boolean cachePermanently = allowCaching && this.eventTypesToKeepCached.Contains(eventFolder.Name);
                try
                {
                    DirectoryInfo[] eventDetailFolders = eventFolder.GetDirectories();
                    foreach (DirectoryInfo eventDetailFolder in eventDetailFolders)
                    {
                        String fullEventName = eventFolder.Name + "/" + eventDetailFolder.Name;
                        // if we're caching this sound set permanently, create the sound players immediately after the files are loaded
                        SoundSet soundSet = new SoundSet(eventDetailFolder, this.useSwearyMessages, this.useMaleSounds,
                            allowCaching, allowCaching, cachePermanently, cachePermanently, soundSets);
                        if (soundSet.hasSounds)
                        {
                            if (soundSets.ContainsKey(fullEventName))
                            {
                                Console.WriteLine("Event " + fullEventName + " sound set is already loaded");
                            }
                            else
                            {
                                //if (!add_shared)
                                //    Log.Debug(() => $"trainee crewchief loading {fullEventName}");
                                availableSounds.Add(fullEventName);
                                soundSets.Add(fullEventName, soundSet);
                            }
                        }
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine("Unable to find events folder");
                }
            }
            if (verbose)
            {
                Console.WriteLine("Prepare voice message completed");
            }
        }

        public static void prepareDriverNamesWithoutLoading(DirectoryInfo driverNamesDirectory, bool verbose,
            Dictionary<string, SingleSound> singleSounds,
            HashSet<string> availableDriverNames,
            HashSet<string> availableDriverNamesForUI)
        {
            if (verbose)
            {
                Console.WriteLine("Preparing driver names");
            }
            extractAvailableDriverNames(driverNamesDirectory, singleSounds, availableDriverNames, availableDriverNamesForUI);
            // Also check the "AI-generated" subdirectory for additional driver name sounds
            var aiGeneratedDirectory = new DirectoryInfo(Path.Combine(driverNamesDirectory.FullName, "AI-generated"));
            if (aiGeneratedDirectory.Exists)
            {
                extractAvailableDriverNames(aiGeneratedDirectory, singleSounds, availableDriverNames, availableDriverNamesForUI);
            }

            if (verbose)
            {
                Console.WriteLine("Prepare driver names completed");
            }
        }

        private static void extractAvailableDriverNames(DirectoryInfo driverNameDirectory,
            Dictionary<string, SingleSound> singleSounds,
            HashSet<string> availableDriverNames,
            HashSet<string> availableDriverNamesForUI)
        {
            foreach (var driverNameFile in driverNameDirectory.GetFiles("*.wav"))
            {
                var name = driverNameFile.Name.ToLower().Split(new[] { ".wav" }, StringSplitOptions.None)[0];
                if (!singleSounds.ContainsKey(name))
                {
                    singleSounds.Add(name, new SingleSound(driverNameFile.FullName, allowCaching, allowCaching, false));
                    availableDriverNames.Add(name);

                    var nameParts = name.Split(' ');
                    if (nameParts.Length > 1)
                    {
                        var nameForUI = new StringBuilder();
                        foreach (var part in nameParts)
                        {
                            nameForUI.Append($"{Utilities.Strings.FirstLetterToUpper(part)} ");
                        }

                        availableDriverNamesForUI.Add(nameForUI.ToString().TrimEnd());
                    }
                    else
                    {
                        availableDriverNamesForUI.Add(Utilities.Strings.FirstLetterToUpper(name));
                    }
                }
            }
        }

        private void preparePrefixesAndSuffixes(DirectoryInfo personalisationsDirectory, String selectedPersonalisation, bool verbose,
            Dictionary<string, SoundSet> soundSets,
            HashSet<string> availablePrefixesAndSuffixes)
        {
            if(verbose)
            {
                Console.WriteLine("Preparing personalisations for selected name " + selectedPersonalisation);
            }
            DirectoryInfo[] namesFolders = personalisationsDirectory.GetDirectories(selectedPersonalisation);
            foreach (DirectoryInfo namesFolder in namesFolders)
            {
                if (namesFolder.Name.Equals(selectedPersonalisation, StringComparison.InvariantCultureIgnoreCase))
                {
                    DirectoryInfo[] nameSubfolders = namesFolder.GetDirectories();
                    foreach (DirectoryInfo nameSubfolder in nameSubfolders)
                    {
                        if (nameSubfolder.Name.Equals("prefixes_and_suffixes"))
                        {
                            foreach (DirectoryInfo prefixesAndSuffixesFolder in nameSubfolder.GetDirectories())
                            {
                                // the lookup of all soundsets is not used when loading the prefixes/suffixes
                                var disposable = new Dictionary<string, SoundSet>();
                                // always keep the personalisations cached as they're reused frequently, so create the sound players immediately after the files are loaded
                                SoundSet soundSet = new SoundSet(prefixesAndSuffixesFolder, this.useSwearyMessages, this.useMaleSounds,
                                    allowCaching, allowCaching, true, true, disposable);
                                if (soundSet.hasSounds)
                                {
                                    availablePrefixesAndSuffixes.Add(prefixesAndSuffixesFolder.Name);
                                    soundSets.Add(prefixesAndSuffixesFolder.Name, soundSet);
                                }
                            }
                            break;
                        }
                    }
                    break;
                }
            }
            if (verbose)
            {
                Console.WriteLine("Prepare personalisations completed");
            }
        }

        private void createCompositePrefixesAndSuffixes(DirectoryInfo soundsRootDirectory,
            DirectoryInfo personalisationsDirectory, String selectedPersonalisation)
        {
            Console.WriteLine("Creating a new personalisation set from driver name sound " + selectedPersonalisation);
            String driverNameFile = soundsRootDirectory.FullName + "\\driver_names\\" + selectedPersonalisation + ".wav";
            if (!File.Exists(driverNameFile))
            {
                driverNameFile = soundsRootDirectory.FullName + "\\driver_names\\AI-generated" + selectedPersonalisation + ".wav";
            }
            if (!File.Exists(driverNameFile))
            {
                return;
            }
            DirectoryInfo nameFolder = personalisationsDirectory.CreateSubdirectory(selectedPersonalisation);
            DirectoryInfo prefixesAndSuffixesFolder = nameFolder.CreateSubdirectory("prefixes_and_suffixes");
            // now get the generic sounds and concatenate the wav files
            DirectoryInfo[] genericSoundsRoot = soundsRootDirectory.GetDirectories("composite_personalisation_stubs");
            if (genericSoundsRoot.Length == 1)
            {
                foreach (DirectoryInfo stubDirectory in genericSoundsRoot[0].GetDirectories())
                {
                    DirectoryInfo compositeFolder = prefixesAndSuffixesFolder.CreateSubdirectory(stubDirectory.Name);
                    foreach (FileInfo file in stubDirectory.GetFiles("*.wav"))
                    {
                        String outputFileName = compositeFolder.FullName + "\\" + file.Name;
                        List<String> files = new List<string>();
                        files.Add(file.FullName);
                        files.Add(driverNameFile);
                        concatenateWavFiles(outputFileName, files);
                    }
                }
            }
        }

        // copyright stackoverflow :P
        private void concatenateWavFiles(string outputFile, IEnumerable<string> sourceFiles)
        {
            byte[] buffer = new byte[1024];
            WaveFileWriter waveFileWriter = null;
            try
            {
                foreach (string sourceFile in sourceFiles)
                {
                    using (WaveFileReader reader = new WaveFileReader(sourceFile))
                    {
                        if (waveFileWriter == null)
                        {
                            // first time in create new Writer
                            waveFileWriter = new WaveFileWriter(outputFile, reader.WaveFormat);
                        }
                        else
                        {
                            if (!reader.WaveFormat.Equals(waveFileWriter.WaveFormat))
                            {
                                throw new InvalidOperationException("Can't concatenate WAV Files that don't share the same format");
                            }
                        }
                        int read;
                        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            waveFileWriter.WriteData(buffer, 0, read);
                        }
                    }
                }
            }
            finally
            {
                if (waveFileWriter != null)
                {
                    waveFileWriter.Dispose();
                }
            }
        }
    }

    public class SoundSet
    {
        private List<SingleSound> singleSoundsNoPrefixOrSuffix = new List<SingleSound>();
        private List<SingleSound> singleSoundsWithPrefixOrSuffix = new List<SingleSound>();
        private DirectoryInfo soundFolder;
        private Boolean useSwearyMessages;
        private Boolean useMaleSounds;
        private Boolean cacheSoundPlayers;
        private Boolean cacheFileData;
        private Boolean eagerlyCreateSoundPlayers;
        internal Boolean cacheSoundPlayersPermanently;
        internal Boolean hasSounds = false;
        private int soundsCount;
        internal Boolean hasPrefixOrSuffix = false;
        private List<int> prefixOrSuffixIndexes = null;
        private int prefixOrSuffixIndexesPosition = 0;
        private List<int> indexes = null;
        private int indexesPosition = 0;
        const String OPTIONAL_PREFIX_IDENTIFIER = "op_prefix";
        const String OPTIONAL_SUFFIX_IDENTIFIER = "op_suffix";
        const String REQUIRED_PREFIX_IDENTIFIER = "rq_prefix";
        const String REQUIRED_SUFFIX_IDENTIFIER = "rq_suffix";

        // allow the non-personalised versions of this soundset to play, if it's not frequent and has personalisations
        private Boolean lastVersionWasPersonalised = false;

        internal SoundSet(DirectoryInfo soundFolder, Boolean useSwearyMessages, Boolean useMaleSounds, Boolean cacheFileData, Boolean cacheSoundPlayers,
            Boolean cacheSoundPlayersPermanently, Boolean eagerlyCreateSoundPlayers, Dictionary<String, SoundSet> allSoundSets)
        {
            this.soundsCount = 0;
            this.soundFolder = soundFolder;
            this.useSwearyMessages = useSwearyMessages;
            this.useMaleSounds = useMaleSounds;
            this.cacheFileData = cacheFileData;
            this.cacheSoundPlayers = cacheSoundPlayers;
            this.eagerlyCreateSoundPlayers = eagerlyCreateSoundPlayers;
            this.cacheSoundPlayersPermanently = cacheSoundPlayersPermanently;
            initialise(allSoundSets);
        }

        internal Boolean forceNonPersonalisedVersion()
        {
            return lastVersionWasPersonalised && singleSoundsNoPrefixOrSuffix.Count > 0;
        }

        private void shuffle(List<int> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Utilities.random.Next(n + 1);
                int value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        internal void loadAll()
        {
            foreach (SingleSound sound in singleSoundsNoPrefixOrSuffix)
            {
                if (eagerlyCreateSoundPlayers && !AudioPlayer.playWithNAudio)
                {
                    sound.LoadAndCacheSound();
                }
                else
                {
                    sound.LoadAndCacheFile();
                }
            }
            foreach (SingleSound sound in singleSoundsWithPrefixOrSuffix)
            {
                if (eagerlyCreateSoundPlayers && !AudioPlayer.playWithNAudio)
                {
                    sound.LoadAndCacheSound();
                }
                else
                {
                    sound.LoadAndCacheFile();
                }
            }
        }

        // allSoundSets is used to lookup prefixes and suffixes, in a very convoluted manner.
        // To facilitate this, it is expected that all the prefixes and suffixes have been
        // loaded first.
        private void initialise(Dictionary<String, SoundSet> allSoundSets)
        {
            try
            {
                FileInfo[] soundFiles = this.soundFolder.GetFiles("*.wav");
                foreach (FileInfo soundFile in soundFiles)
                {
                    Boolean isSweary = soundFile.Name.Contains("sweary");
                    Boolean isMale = soundFile.Name.Contains("male");   // the sound uses a term like 'he', 'man', 'fella' etc
                    Boolean isBleep = soundFile.Name.Contains("bleep");
                    Boolean isSpotter = soundFile.FullName.Contains(@"\spotter");
                    Boolean isOptional = soundFile.Name.Contains(OPTIONAL_PREFIX_IDENTIFIER) || soundFile.Name.Contains(OPTIONAL_SUFFIX_IDENTIFIER);
                    if (!isSpotter && NoisyCartesianCoordinateSpotter.folderSpotterRadioCheckBSlash != null)
                    {
                        isSpotter = soundFile.FullName.Contains(NoisyCartesianCoordinateSpotter.folderSpotterRadioCheckBSlash);
                    }
                    if ((this.useSwearyMessages || !isSweary) && (this.useMaleSounds || !isMale))
                    {
                        if (soundFile.Name.Contains(REQUIRED_PREFIX_IDENTIFIER) || soundFile.Name.Contains(REQUIRED_SUFFIX_IDENTIFIER) ||
                            isOptional)
                        {
                            foreach (String prefixSuffixName in SoundCache.availablePrefixesAndSuffixes)
                            {
                                SoundSet additionalSoundSet = null;
                                if (soundFile.Name.Contains(prefixSuffixName) && allSoundSets.TryGetValue(prefixSuffixName, out additionalSoundSet))
                                {
                                    if (additionalSoundSet.hasSounds)
                                    {
                                        hasPrefixOrSuffix = true;
                                        hasSounds = true;
                                        SingleSound singleSound = new SingleSound(soundFile.FullName, this.cacheFileData, this.cacheSoundPlayers, this.cacheSoundPlayersPermanently);
                                        if (eagerlyCreateSoundPlayers)
                                        {
                                            if (!AudioPlayer.playWithNAudio)
                                            {
                                                singleSound.LoadAndCacheSound();
                                            }
                                            else
                                            {
                                                singleSound.LoadAndCacheFile();
                                            }
                                        }
                                        singleSound.isSweary = isSweary;
                                        if (soundFile.Name.Contains(OPTIONAL_SUFFIX_IDENTIFIER) || soundFile.Name.Contains(REQUIRED_SUFFIX_IDENTIFIER))
                                        {
                                            singleSound.suffixSoundSet = additionalSoundSet;
                                        }
                                        else
                                        {
                                            singleSound.prefixSoundSet = additionalSoundSet;
                                        }
                                        singleSoundsWithPrefixOrSuffix.Add(singleSound);
                                        SoundCache.prefixesAndSuffixesCount++;
                                        soundsCount++;
                                    }
                                    break;
                                }
                            }
                            if (isOptional)
                            {
                                hasSounds = true;
                                SingleSound singleSound = new SingleSound(soundFile.FullName, this.cacheFileData, this.cacheSoundPlayers, this.cacheSoundPlayersPermanently);
                                if (eagerlyCreateSoundPlayers)
                                {
                                    if (!AudioPlayer.playWithNAudio)
                                    {
                                        singleSound.LoadAndCacheSound();
                                    }
                                    else
                                    {
                                        singleSound.LoadAndCacheFile();
                                    }
                                }
                                singleSound.isSweary = isSweary;
                                singleSound.isSpotter = isSpotter;
                                singleSound.isBleep = isBleep;
                                singleSoundsNoPrefixOrSuffix.Add(singleSound);
                                soundsCount++;
                            }
                        }
                        else
                        {
                            hasSounds = true;
                            SingleSound singleSound = new SingleSound(soundFile.FullName, this.cacheFileData, this.cacheSoundPlayers, this.cacheSoundPlayersPermanently);
                            if (eagerlyCreateSoundPlayers)
                            {
                                if (!AudioPlayer.playWithNAudio)
                                {
                                    singleSound.LoadAndCacheSound();
                                }
                                else
                                {
                                    singleSound.LoadAndCacheFile();
                                }
                            }
                            singleSound.isSweary = isSweary;
                            singleSound.isSpotter = isSpotter;
                            singleSound.isBleep = isBleep;
                            singleSoundsNoPrefixOrSuffix.Add(singleSound);
                            soundsCount++;
                        }
                    }
                    else if (isSweary && !this.useSwearyMessages)
                    {
                        SoundCache.swearySoundsSkipped++;
                    }
                    else if (isMale && !this.useMaleSounds)
                    {
                        SoundCache.maleSoundsSkipped++;
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("Unable to find sounds folder for sound set " + this.soundFolder.FullName);
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to load sounds for sound set " + this.soundFolder.FullName + " exception: ");
            }
        }

        internal SingleSound getSingleSound(Boolean preferPersonalised)
        {
            if (SoundCache.recordVarietyData)
            {
                SoundCache.addUseToVarietyData(this.soundFolder.FullName, this.soundsCount);
            }
            if (!AudioPlayer.rantWaitingToPlay && preferPersonalised && singleSoundsWithPrefixOrSuffix.Count > 0)
            {
                if (prefixOrSuffixIndexes == null || prefixOrSuffixIndexesPosition == prefixOrSuffixIndexes.Count)
                {
                    prefixOrSuffixIndexes = createIndexes(singleSoundsWithPrefixOrSuffix.Count());
                    prefixOrSuffixIndexesPosition = 0;
                }
                SingleSound ss = null;
                while (prefixOrSuffixIndexesPosition < prefixOrSuffixIndexes.Count())
                {
                    ss = singleSoundsWithPrefixOrSuffix[prefixOrSuffixIndexes[prefixOrSuffixIndexesPosition]];
                    prefixOrSuffixIndexesPosition++;
                    if (!ss.isSweary)
                    {
                        break;
                    }
                    else
                    {
                        // this is a sweary message - can we play it? do we have to play it?
                        if (prefixOrSuffixIndexesPosition == prefixOrSuffixIndexes.Count || GameStateData.CurrentTime > SoundCache.lastSwearyMessageTime + TimeSpan.FromSeconds(10))
                        {
                            SoundCache.lastSwearyMessageTime = GameStateData.CurrentTime;
                            break;
                        }
                    }
                }
                lastVersionWasPersonalised = true;
                return ss;
            }
            else if (singleSoundsNoPrefixOrSuffix.Count > 0)
            {
                if (indexes == null || indexesPosition == indexes.Count)
                {
                    indexes = createIndexes(singleSoundsNoPrefixOrSuffix.Count());
                    indexesPosition = 0;
                }
                SingleSound ss = null;
                while (indexesPosition < indexes.Count())
                {
                    ss = singleSoundsNoPrefixOrSuffix[indexes[indexesPosition]];
                    indexesPosition++;
                    if (!ss.isSweary)
                    {
                        break;
                    }
                    else
                    {
                        // this is a sweary message - can we play it? do we have to play it?
                        if (indexesPosition == indexes.Count || GameStateData.CurrentTime > SoundCache.lastSwearyMessageTime + TimeSpan.FromSeconds(10))
                        {
                            SoundCache.lastSwearyMessageTime = GameStateData.CurrentTime;
                            break;
                        }
                    }
                }
                lastVersionWasPersonalised = false;
                return ss;
            }
            else
            {
                return null;
            }
        }

        private List<int> createIndexes(int count)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < count; i++)
            {
                indexes.Add(i);
            }
            shuffle(indexes);
            return indexes;
        }

        internal int UnLoadAll()
        {
            int unloadedCount = 0;
            foreach (SingleSound singleSound in singleSoundsNoPrefixOrSuffix)
            {
                if (singleSound.UnLoad())
                {
                    unloadedCount++;
                }
            }
            foreach (SingleSound singleSound in singleSoundsWithPrefixOrSuffix)
            {
                if (singleSound.UnLoad())
                {
                    unloadedCount++;
                }
            }
            return unloadedCount;
        }

        internal void StopAll()
        {
            foreach (SingleSound singleSound in singleSoundsNoPrefixOrSuffix)
            {
                singleSound.Stop();
            }
            foreach (SingleSound singleSound in singleSoundsWithPrefixOrSuffix)
            {
                singleSound.Stop();
            }
        }
    }

    public class SingleSound
    {
        private String ttsString = null;
        internal Boolean isSweary = false;
        public Boolean isPause = false;
        public Boolean isSpotter = false;
        public Boolean isBleep = false;
        internal Boolean isNumber = false;  // Currently only only set if subtitle processing is enabled.
        internal int pauseLength = 0;
        internal String fullPath;
        private byte[] fileBytes = null;
        private DateTime playbackStartTime = DateTime.MinValue;
        private MemoryStream memoryStream;
        private SoundPlayer soundPlayer;

        private Boolean cacheFileData;
        private Boolean cacheSoundPlayer;
        internal Boolean cacheSoundPlayerPermanently;
        private Boolean loadedSoundPlayer = false;
        private Boolean loadedFile = false;

        internal SoundSet prefixSoundSet = null;
        internal SoundSet suffixSoundSet = null;

        private NAudioOut nAudioOut = null;
        private NAudio.Wave.WaveFileReader reader = null;
        // When using nAudio, we wait for a playbackEnded callback. Sometimes this doesn't trigger so limit
        // how long we'll wait for this event
        private int allowedPlayTimeForNAudio;

        // only used for bleeps
        private int deviceIdWhenCached = 0;
        // note the volume level when the beep was cached so we can reload the sound if the volume has changed
        private float volumeWhenCached = 0;

        AutoResetEvent playWaitHandle = new AutoResetEvent(false);

        EventHandler<NAudio.Wave.StoppedEventArgs> eventHandler;

        private String subtitle = "";
        private bool loadSubtitleBeforePlaying = false;

        internal SingleSound(int pauseLength)
        {
            this.isPause = true;
            this.pauseLength = pauseLength;
        }

        internal SingleSound(String textToRender, Boolean allowDownload = false)
        {
            this.ttsString = textToRender;
            // always eagerly load and cache TTS phrases:
            cacheFileData = true;
            cacheSoundPlayer = true;
            LoadAndCacheFile(allowDownload);
            this.subtitle = textToRender;
            //Console.WriteLine("Loaded subtitle for sound = " + this.subtitle);
        }

        internal SingleSound(String fullPath, Boolean cacheFileData, Boolean cacheSoundPlayer, Boolean cacheSoundPlayerPermanently, string subtitle = null)
        {
            this.fullPath = fullPath;
            this.cacheFileData = cacheFileData || cacheSoundPlayer || cacheSoundPlayerPermanently;
            this.cacheSoundPlayer = cacheSoundPlayer || cacheSoundPlayerPermanently;
            this.cacheSoundPlayerPermanently = cacheSoundPlayerPermanently;
            if (SubtitleManager.enableSubtitles)
            {
                if (subtitle != null)
                {
                    this.subtitle = subtitle;
                }
                else
                {
                    if (SoundCache.lazyLoadSubtitles)
                    {
                        this.loadSubtitleBeforePlaying = true;
                    }
                    else
                    {
                        LoadSubtitle();
                    }
                }
            }
        }

        public String GetSubtitle()
        {
            if (SubtitleManager.enableSubtitles && this.loadSubtitleBeforePlaying)
            {
                LoadSubtitle();
            }
            return this.subtitle;
        }

        private void LoadSubtitle()
        {
            if (fullPath == null)
            {
                return; 
            }
            try
            {
                if (fullPath.Contains("numbers"))
                {
                    this.subtitle = SubtitleManager.ParseSubtitleForNumber(fullPath, this);
                }
                else if (fullPath.Contains("driver_names"))
                {
                    this.subtitle = Path.GetFileNameWithoutExtension(fullPath);
                    if (!string.IsNullOrWhiteSpace(this.subtitle))
                        this.subtitle = Utilities.Strings.FirstLetterToUpper(this.subtitle);
                }
                else if (fullPath.Contains("prefixes_and_suffixes"))
                {
                    this.subtitle = SubtitleManager.ParseSubtitleForPersonalisation(fullPath);
                }
                else
                {
                    this.subtitle = SubtitleManager.LoadSubtitleForSound(fullPath);
                }

                if (string.IsNullOrWhiteSpace(this.subtitle)
                    && !fullPath.Contains(@"\fx\")
                    && !fullPath.Contains(@"\breath_in"))  // Shouldn't breaths be moved to fx?
                {
                    Console.WriteLine($"Warning: no subtitle found for \"{fullPath}\"");
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to load subtitles for sound " + fullPath + " : ");
            }
            // don't retry loading the subtitle
            this.loadSubtitleBeforePlaying = false;
        }

        private byte[] DownloadDataFromUrl(string url)
        {
            using (var webClient = new System.Net.WebClient())
            {
                try
                {
                    byte[] data = webClient.DownloadData(url);

                    if (url.EndsWith(".mp3"))
                    {
                        return ConvertMp3ToWav(data);
                    }

                    return data;
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Failed to download data from URL: ");
                    return null;
                }
            }
        }

        private byte[] ConvertMp3ToWav(byte[] mp3Data)
        {
            try
            {
                using (var mp3Stream = new MemoryStream(mp3Data))
                using (var mp3Reader = new Mp3FileReader(mp3Stream))
                using (var wavStream = new MemoryStream())
                using (var wavWriter = new WaveFileWriter(wavStream, mp3Reader.WaveFormat))
                {
                    byte[] bytes = new byte[mp3Reader.Length];
                    mp3Reader.Read(bytes, 0, bytes.Length);
                    wavWriter.Write(bytes, 0, bytes.Length);
                    wavWriter.Flush();
                    return wavStream.ToArray();
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to convert mp3.\n Error: {e.StackTrace}");
                return null;
            }
        }

        internal void LoadAndCacheFile(Boolean allowDownload = false)
        {
            lock (this)
            {
                if (!loadedFile)
                {
                    try
                    {
                        if (ttsString != null)
                        {
                            if (allowDownload && ttsString.StartsWith("http"))
                            {
                                this.fileBytes = DownloadDataFromUrl(ttsString);
                            }
                            else
                            {
                                MemoryStream rawStream = new MemoryStream();
                                SoundCache.synthesizer.SetOutputToWaveStream(rawStream);
                                SoundCache.synthesizer.Speak(ttsString);
                                rawStream.Position = 0;
                                try
                                {
                                    this.fileBytes = ConvertTTSWaveStreamToBytes(rawStream, SoundCache.ttsTrimStartMilliseconds, SoundCache.ttsTrimEndMilliseconds);
                                }
                                catch (Exception e)
                                {
                                    // unable to trim and convert the tts stream, so save the raw stream and use that instead
                                    Log.Exception(e, "Failed to pre-process TTS audio data: ");
                                    this.memoryStream = rawStream;
                                }
                                SoundCache.synthesizer.SetOutputToNull();
                            }
                        }
                        else
                        {
                            this.fileBytes = File.ReadAllBytes(fullPath);
                            // JetBrains reports this goes to 1.5 GB? Biggest is CrewChiefV4\sounds\voice\rants\general\5.wav at 550k
                            // Make that CrewChiefV4\Sounds\alt\Jerry\voice\alarm_clock\alarms\alt_1.wav at 2.83 MB
                            var size = this.fileBytes.Length;
                            if (size > 5000000)
                            {   
                                var _size = ((float)size / 1000000).ToString("0.00");
                                Log.DebugError(() => $"Sound file '{fullPath}' is large ({_size} MB).");
                            }
                        }
                        loadedFile = true;
                        SoundCache.currentSoundsLoaded++;
                    }
                    catch (Exception ex)
                    {
                        // this can happen if the load files thread is running when file renames are being processed. While it looks
                        // bad, the user is prompted to restart the app anyway so it doesn't break anything
                        Console.WriteLine(string.Format("unable to load file:{0}", fullPath));
                    }
                }
            }
        }

        internal void LoadAndCacheSound()
        {
            lock (this)
            {
                if (!loadedFile)
                {
                    LoadAndCacheFile();
                }
                if (AudioPlayer.playWithNAudio)
                {
                    if (loadedSoundPlayer &&
                        (AudioPlayer.naudioMessagesPlaybackDeviceId != deviceIdWhenCached || this.getVolume(1f) != volumeWhenCached))
                    {
                        // naudio device ID or volume has changed since the beep was cached, so unload and re-cache it
                        try
                        {
                            this.reader.Dispose();
                        }
                        catch (Exception e) { Log.Exception(e); }
                        try
                        {
                            if (this.eventHandler != null) this.nAudioOut.SubscribePlaybackStopped(this.eventHandler);
                            this.nAudioOut.Stop();
                            this.nAudioOut.Dispose();
                        }
                        catch (Exception e) {Log.Exception(e);}
                        loadedSoundPlayer = false;
                    }
                    if (!loadedSoundPlayer)
                    {
                        volumeWhenCached = getVolume(1f);
                        LoadNAudioWaveOut();
                        loadedSoundPlayer = true;
                        deviceIdWhenCached = AudioPlayer.naudioMessagesPlaybackDeviceId;
                    }
                    else
                    {
                        this.reader.CurrentTime = TimeSpan.Zero;
                    }
                }
                else if (!AudioPlayer.playWithNAudio && !loadedSoundPlayer)
                {
                    // if we have file bytes, load them
                    if (this.fileBytes != null)
                    {
                        this.memoryStream = new MemoryStream(this.fileBytes);
                    }
                    // if we have the TTS memory stream, use it
                    else if (this.memoryStream != null && ttsString != null)
                    {
                        this.memoryStream.Position = 0;
                        Console.WriteLine("Loading TTS sound for " + ttsString);
                    }
                    else
                    {
                        Console.WriteLine("No sound data available");
                        return;
                    }
                    this.soundPlayer = new SoundPlayer(memoryStream);
                    this.soundPlayer.Load();
                    loadedSoundPlayer = true;
                    SoundCache.activeSoundPlayerObjects++;
                }
            }
        }
        /*
         * canInterrupt will be true for regular messages triggered by the app's normal event logic. When a message
         * is played from the 'immediate' queue this will be false (spotter calls, command responses, some edge cases
         * where the message is time-critical). If this flag is true the presence of a message in the immediate queue
         * can make the app skip playing this sound.
         */
        internal void Play(SoundMetadata soundMetadata)
        {
            if (!PlaybackModerator.ShouldPlaySound(this, soundMetadata))
                return;

            PlaybackModerator.PreProcessSound(this, soundMetadata);
            if (GameOrVoipVolume.AudioDuckingEnabled)
            {
                GameOrVoipVolume.DuckGameAudio();
            }
            if (AudioPlayer.playWithNAudio)
            {
                PlayNAudio(soundMetadata.isListenStartBeep);
            }
            else
            {
                PlaySoundPlayer();
            }
            if (GameOrVoipVolume.AudioDuckingEnabled)
            {
                GameOrVoipVolume.UnDuckGameAudio();
            }
        }

        private void PlaySoundPlayer()
        {
            if (!cacheFileData)
            {
                SoundPlayer soundPlayer = new SoundPlayer(fullPath);
                soundPlayer.Load();
                soundPlayer.PlaySync();
                try
                {
                    soundPlayer.Dispose();
                }
                catch (Exception e) {Log.Exception(e);}
            }
            else
            {
                lock (this)
                {
                    LoadAndCacheSound();
                    this.soundPlayer.PlaySync();
                }
                if (!cacheSoundPlayer)
                {
                    try
                    {
                        this.soundPlayer.Dispose();
                    }
                    catch (Exception e) {Log.Exception(e);}
                    this.loadedSoundPlayer = false;
                    SoundCache.activeSoundPlayerObjects--;
                }
            }
        }

        // if the sound is a listen start beep and caching the file data is enabled, we don't
        // wait for the sound to finish playing before returning control to the caller.
        private void PlayNAudio(Boolean isListenStartBeep)
        {
            float volume = getVolume(isSpotter ? SoundCache.spotterVolumeBoost : 1f);
            if (volume == 0)
            {
                SoundCache.currentlyPlayingSound = null;
                return;
            }
            if (!cacheFileData)
            {
                // if caching is switched off, load and play the file
                NAudioOut uncachedNAudioOut = NAudioOut.CreateOutput();
                WaveFileReader uncachedReader = new WaveFileReader(fullPath);
                setAllowedPlayTimeForNAudio(uncachedReader.Length, uncachedReader.WaveFormat);
                this.eventHandler = new EventHandler<StoppedEventArgs>(playbackStopped);
                uncachedNAudioOut.SubscribePlaybackStopped(this.eventHandler);
                ISampleProvider sampleProvider = createSampleProvider(uncachedReader, volume);
                try
                {
                    uncachedNAudioOut.Init(sampleProvider);
                    SoundCache.currentlyPlayingSound = this;
                    this.playbackStartTime = DateTime.UtcNow;
                    uncachedNAudioOut.Play();
                    if (!this.playWaitHandle.WaitOne(this.allowedPlayTimeForNAudio))
                    {
                        Log.Warning("Gave up waiting for nAudio playbackStopped callback");
                        SoundCache.currentlyPlayingSound = null;
                    }
                    uncachedNAudioOut.UnsubscribePlaybackStopped(this.playbackStopped);
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Exception ");
                }
                try
                {
                    uncachedReader.Dispose();
                }
                catch (Exception e) {Log.Exception(e);}
                try
                {
                    uncachedNAudioOut.Dispose();
                }
                catch (Exception e) {Log.Exception(e);}
            }
            else
            {
                // ensure the file is loaded then play it
                lock (this)
                {
                    try
                    {
                        LoadAndCacheSound();
                        this.reader.CurrentTime = TimeSpan.Zero;
                        SoundCache.currentlyPlayingSound = this;
                        this.playbackStartTime = DateTime.UtcNow;
                        this.nAudioOut.Play();
                        // Special case for the listen start beep - don't wait for it to finish playing before returning
                        if (!isListenStartBeep)
                        {
                            if (!this.playWaitHandle.WaitOne(allowedPlayTimeForNAudio))
                            {
                                Log.Warning("Gave up waiting for nAudio playbackStopped callback");
                                SoundCache.currentlyPlayingSound = null;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Exception ");
                    }
                }
            }
        }

        private void LoadNAudioWaveOut()
        {
            this.nAudioOut = NAudioOut.CreateOutput();
            float volumeBoost = isSpotter ? SoundCache.spotterVolumeBoost : 1f;
            // if we have file bytes, load them
            if (this.fileBytes != null)
            {
                this.memoryStream = new MemoryStream(this.fileBytes);
            }
            // if we have the TTS memory stream, use it
            else if (this.memoryStream != null && this.ttsString != null)
            {
                volumeBoost = SoundCache.ttsVolumeBoost;
                this.memoryStream.Position = 0;
            }
            else
            {
                Console.WriteLine("No sound data available");
                return;
            }
            this.reader = new NAudio.Wave.WaveFileReader(this.memoryStream);
            setAllowedPlayTimeForNAudio(this.reader.Length, this.reader.WaveFormat);
            this.eventHandler = new EventHandler<NAudio.Wave.StoppedEventArgs>(playbackStopped);
            this.nAudioOut.SubscribePlaybackStopped(this.eventHandler);
            float volume = getVolume(volumeBoost);

            ISampleProvider sampleProvider = createSampleProvider(this.reader, volume);
            this.nAudioOut.Init(sampleProvider);
            this.reader.CurrentTime = TimeSpan.Zero;
        }

        // If we don't get a playbackStopped event we'll give up waiting - base this wait time on the expected file play time + some buffer
        private void setAllowedPlayTimeForNAudio(long fileLength, WaveFormat waveFormat)
        {
            // convert to millis & bits->bytes, and add some grace period - note this is only for when we have missing callbacks so
            // we should never really need this value. Set the grace period to 300ms because reasons. As we're including the wav header in
            // the size calculation this will be a bit less than 300ms grace period
            this.allowedPlayTimeForNAudio = 300 + (int)(8000f * (float)fileLength / (float)(waveFormat.SampleRate * waveFormat.Channels * waveFormat.BitsPerSample));
        }

        private ISampleProvider createSampleProvider(WaveFileReader reader, float volume)
        {
            ISampleProvider sampleProvider = new NAudio.Wave.SampleProviders.SampleChannel(this.reader, SoundCache.forceStereoPlayback);
            ((NAudio.Wave.SampleProviders.SampleChannel)sampleProvider).Volume = volume;
            if (SoundCache.forceResamplePlayback > 500 && SoundCache.forceResamplePlayback <= 48000)
            {
                sampleProvider = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(sampleProvider, SoundCache.forceResamplePlayback);
            }
            return sampleProvider;
        }

        private void playbackStopped(object sender, NAudio.Wave.StoppedEventArgs e)
        {
            this.playWaitHandle.Set();
            SoundCache.currentlyPlayingSound = null;
        }

        private float getVolume(float boost)
        {
            float volume = MainWindow.currentMessageVolume * boost;
            // volume can be higher than 1, it seems. Not sure if this is device dependent
            /*if (volume > 1)
            {
                volume = 1;
            }*/
            if (volume < 0)
            {
                volume = 0;
            }
            return volume;
        }

        internal Boolean UnLoad()
        {
            Boolean unloaded = false;
            lock(this)
            {
                if (this.soundPlayer != null)
                {
                    this.soundPlayer.Stop();
                }
                if (this.nAudioOut != null && this.nAudioOut.PlaybackState != NAudio.Wave.PlaybackState.Stopped)
                {
                    this.nAudioOut.Stop();
                }
                if (this.memoryStream != null)
                {
                    try
                    {
                        this.memoryStream.Dispose();
                    }
                    catch (Exception e) {Log.Exception(e);}
                }
                if (this.soundPlayer != null)
                {
                    try
                    {
                        this.soundPlayer.Dispose();
                    }
                    catch (Exception e) {Log.Exception(e);}
                    this.loadedSoundPlayer = false;
                    unloaded = true;
                    SoundCache.activeSoundPlayerObjects--;
                }
                if (this.reader != null)
                {
                    try
                    {
                        this.reader.Dispose();
                    }
                    catch (Exception e) { Log.Exception(e); }
                }
                if (this.nAudioOut != null)
                {
                    try
                    {
                        this.nAudioOut.Dispose();
                    }
                    catch (Exception e) {Log.Exception(e);}
                    this.loadedSoundPlayer = false;
                    unloaded = true;
                }
            }
            return unloaded;
        }

        internal Boolean Stop()
        {
            Boolean blockedABeep = false;
            if (AudioPlayer.playWithNAudio && this.nAudioOut != null && this.nAudioOut.PlaybackState == NAudio.Wave.PlaybackState.Playing)
            {
                if (this.isBleep)
                {
                    blockedABeep = true;
                }
                this.nAudioOut.Stop();
                Console.WriteLine($"Stopping sound: {fullPath}");
            }
            else if (!AudioPlayer.playWithNAudio && this.soundPlayer != null)
            {
                this.soundPlayer.Stop();
            }
            return blockedABeep;
        }

        private byte[] ConvertTTSWaveStreamToBytes(MemoryStream inputStream, int startMillisecondsToTrim, int endMillisecondsToTrim)
        {
            NAudio.Wave.WaveFileReader reader = new NAudio.Wave.WaveFileReader(inputStream);
            // can only do volume stuff if it's a 16 bit wav stream (which it should be)
            Boolean canProcessVolume = reader.WaveFormat.BitsPerSample == 16;

            // work out how many bytes to trim off the start and end
            int totalMilliseconds = (int)reader.TotalTime.TotalMilliseconds;

            // don't trim the start if the resulting sound file would be < 1 second long - some issue prevents nAudio loading the byte array
            // if the start is trimmed and the sound is very short
            if (totalMilliseconds - startMillisecondsToTrim - endMillisecondsToTrim < 1000)
            {
                startMillisecondsToTrim = 0;
            }
            double bytesPerMillisecond = (double)reader.WaveFormat.AverageBytesPerSecond / 1000d;
            int startPos = (int)(startMillisecondsToTrim * bytesPerMillisecond);
            startPos = startPos - startPos % reader.WaveFormat.BlockAlign;

            int endBytesToTrim = (int)(endMillisecondsToTrim * bytesPerMillisecond);
            endBytesToTrim = endBytesToTrim - endBytesToTrim % reader.WaveFormat.BlockAlign;
            int endPos = (int)reader.Length - endBytesToTrim;

            if (startPos > endPos)
            {
                startPos = 0;
            }

            byte[] buffer = new byte[reader.BlockAlign * 100];
            MemoryStream outputStream = new MemoryStream();

            // process the wave file header
            int headerLength = (int) (inputStream.Length - reader.Length);  // PCM wave file header size should be 46 bytes, the last 4 bytes are the sample count
            byte[] header = new byte[headerLength];
            outputStream.SetLength((endPos - startPos) + headerLength);
            inputStream.Position = 0;
            inputStream.Read(header, 0, headerLength);
            uint dataSize = BitConverter.ToUInt32(header, headerLength - 4);
            dataSize = dataSize - ((uint)endBytesToTrim + (uint)startPos);
            byte[] newSize = BitConverter.GetBytes(dataSize);
            header[headerLength - 4] = newSize[0];
            header[headerLength - 3] = newSize[1];
            header[headerLength - 2] = newSize[2];
            header[headerLength - 1] = newSize[3];
            outputStream.Write(header, 0, headerLength);
            reader.Position = startPos;
            while (reader.Position < endPos)
            {
                int bytesRequired = (int)(endPos - reader.Position);
                if (bytesRequired > 0)
                {
                    int bytesToRead = Math.Min(bytesRequired, buffer.Length);
                    int bytesRead = reader.Read(buffer, 0, bytesToRead);

                    if (canProcessVolume)
                    {
                        for (int i = 0; i < buffer.Length; i+=2)
                        {
                            Int16 sample = BitConverter.ToInt16(buffer, i);
                            if (sample > 0)
                            {
                                sample = (short)Math.Min(short.MaxValue, sample * SoundCache.ttsVolumeBoost);
                            }
                            else
                            {
                                sample = (short)Math.Max(short.MinValue, sample * SoundCache.ttsVolumeBoost);
                            }
                            byte[] bytes = BitConverter.GetBytes(sample);
                            buffer[i] = bytes[0];
                            buffer[i+1] = bytes[1];
                        }
                    }
                    if (bytesRead > 0)
                    {
                        outputStream.Write(buffer, 0, bytesRead);
                    }
                }
            }
            return outputStream.ToArray();
        }
    }

    internal class SoundVarietyDataPoint : IComparable<SoundVarietyDataPoint>
    {
        internal String soundName;
        internal int numSounds;
        internal int timesPlayed;
        internal float score;
        internal SoundVarietyDataPoint(String soundName, int numSounds, int timesPlayed)
        {
            this.soundName = soundName;
            this.numSounds = numSounds;
            this.timesPlayed = timesPlayed;
            this.score = (float)numSounds / (float)timesPlayed;
        }

        // sort worst-first
        public int CompareTo(SoundVarietyDataPoint that)
        {
            if (this.score < that.score) return -1;
            if (this.score == that.score) return 0;
            return 1;
        }
    }
}
