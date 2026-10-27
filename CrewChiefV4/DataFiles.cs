using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace CrewChiefV4
{
    /// <summary>
    /// <para>Accessor for all CC data folders and files</para>
    /// TL;DR for Release builds:
    /// <para>"MyDocuments"/CrewChiefV4 used for profiles and other data files</para>
    /// <para>"LocalApplicationData"/Britton_IT_Ltd used for /"CC version"/user.config files via some hidden .Net magic</para>
    /// <para>"LocalApplicationData"/CrewChiefV4 used for sounds, UI text override, and splash_image.png</para>
    /// <para>"MyDocuments"/CrewChiefV4/datafiles/DebugLogs used to record sessions for replay (debugging)</para>
    /// <para>"MyDocuments"/iRacing/telemetry</para>
    /// Debug builds use different folders in some cases
    /// </summary>
    public static class DataFiles
    {
        // Optional process-local root, set before any static paths initialize.
        // Automated tests never touch the driver's active profiles.
        public static string IsolatedRoot
        {
            get { string root = Environment.GetEnvironmentVariable("RCCA_DATA_ROOT"); return string.IsNullOrWhiteSpace(root) ? null : Path.GetFullPath(root); }
        }
        #region StringsAsProperties
        // Proved necessary to ensure they're initialised when unit testing
        // called MakeFolders()
        // A shame because it makes the layout messy.

        /// <summary>
        /// <para>Release: "MyDocuments"/CrewChiefV4, typically C:\Users\"user"\Documents\CrewChiefV4</para>
        /// Debug: C:\Users\"user"\Documents\CrewChiefDebug\CrewChiefV4
        /// used for JSON files and other data files
        /// </summary>
        public static string BaseFolder
        {
            get
            {
                if (_BaseFolder == null)
                {
                    _BaseFolder = Path.Combine(MyDocuments_Debug, "CrewChiefV4");
                }

                return _BaseFolder;
            }
        }
        private static string _BaseFolder;

        /// <summary>
        /// <para>Release: "LocalApplicationData"/Britton_IT_Ltd, typically C:\Users\"user"\AppData\Local\Britton_IT_Ltd</para>
        /// Debug: C:\Users\"user"\Documents\CrewChiefDebug\Britton_IT_Ltd
        /// used for /"CC version"/user.config files via some hidden magic
        /// The only thing you'd want to do with this is zap the folder out of desperation
        /// </summary>
        public static string UserConfigFolder
        { get
            {
                if (_UserConfigFolder == null)
                {
                    _UserConfigFolder = IsolatedRoot != null ? Path.Combine(IsolatedRoot,"Config") : CrewChief.Debug.UseDebugFilePaths ? Path.Combine(MyDocuments_Debug, "Britton_IT_Ltd")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Britton_IT_Ltd");
                    // or
                    // var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoaming);
                    // config.FilePath
                }

                return _UserConfigFolder;
            }
        }
        private static string _UserConfigFolder;

        /// <summary>
        /// <para>Release: "LocalApplicationData"/CrewChiefV4, typically C:\Users\"user"\AppData\Local\CrewChiefV4</para>
        /// Debug: C:\Users\"user"\Documents\CrewChiefDebug\LocalApplicationData
        /// used for UI text override, and splash_image.png
        /// </summary>
        public static string LocalApplicationDataFolder
        { get 
            {
                if (_LocalApplicationDataFolder == null)
                {
                    _LocalApplicationDataFolder = IsolatedRoot != null ? Path.Combine(IsolatedRoot,"LocalApplicationData") : CrewChief.Debug.UseDebugFilePaths ? Path.Combine(MyDocuments_Debug, "LocalApplicationData")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrewChiefV4");
                }

                return _LocalApplicationDataFolder;
            } 
        }
        private static string _LocalApplicationDataFolder;

        /// <summary>
        /// "LocalApplicationData"/CrewChiefV4/Sounds, typically C:\Users\"user"\AppData\Local\CrewChiefV4\Sounds
        /// unless it is overwritten by Property override_default_sound_pack_location
        /// </summary>
        public static string SoundFilesFolder
        {
            get
            {
                if (_SoundFilesFolder == null)
                {
                    _SoundFilesFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrewChiefV4", "Sounds");
                }

                return _SoundFilesFolder;
            }
        }
        private static string _SoundFilesFolder;

        /// <summary>
        /// <para>"MyDocuments", typically C:\Users\"user"\Documents</para>
        /// </summary>
        private static string MyDocuments
        {
            get
            {
                if (_MyDocuments == null)
                {
                    _MyDocuments = IsolatedRoot == null ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Path.Combine(IsolatedRoot,"Documents");
                }
                return _MyDocuments;
            }
        }
        private static string _MyDocuments;
        /// <summary>
        /// <para>Release: "MyDocuments", typically C:\Users\"user"\Documents</para>
        /// Debug: C:\Users\"user"\Documents\CrewChiefDebug
        /// </summary>
        private static string MyDocuments_Debug
        {
            get
            {
                if (_MyDocuments_Debug == null)
                {
                    _MyDocuments_Debug = IsolatedRoot == null ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Path.Combine(IsolatedRoot,"Documents");
                    if (IsolatedRoot == null && CrewChief.Debug.UseDebugFilePaths)
                    {
                        _MyDocuments_Debug = Path.Combine(_MyDocuments_Debug, "CrewChiefDebug");
                    }
                }

                return _MyDocuments_Debug;
            }
        }
        private static string _MyDocuments_Debug;

        /// <summary>
        /// Path to the CC source files when running unit tests
        /// </summary>
        private static string UnitTestPath
        { get 
            {
                if (_UnitTestPath == null)
                {
                    installation = new Installation();
                    _UnitTestPath = installation.SourceFilesRoot;
                }
                return _UnitTestPath;
            }
        }
        private static string _UnitTestPath;

        private static Installation installation;
        /// <summary>
        /// <para>Release: "MyDocuments"/CrewChiefV4/datafiles</para>
        /// Debug:   "_repo_"/CrewChiefV4
        /// </summary>
        private static string DebugBaseFolder
        {
            get
            {
                if (_DebugBase == null)
                {
                    _DebugBase = CrewChief.Debug.UseDebugFilePaths ?
                    Path.Combine(installation.SourceFilesRoot, "Datafiles") :
                    BaseFolder;
                }

                return _DebugBase;
            }
        }
        private static string _DebugBase;

        /// <summary>
        /// <para>Release: "MyDocuments"/CrewChiefV4/datafiles/DebugLogs</para>
        /// Debug:   "_repo_"/CrewChiefV4/DebugLogs
        /// </summary>
        public static string DebugLogsFolder
        {
            get
            {
                if (_DebugLogsFolder == null)
                {
                    _DebugLogsFolder = Path.Combine(DebugBaseFolder, "DebugLogs");
                }

                return _DebugLogsFolder;
            }
        }
        private static string _DebugLogsFolder;

        /// <summary>
        /// <para>Release: "MyDocuments"/CrewChiefV4/datafiles/DebugLogs</para>
        /// Debug:   same
        /// </summary>
        public static string UserLogsFolder
        {
            get
            {
                if (_LogsFolder == null)
                {
                    _LogsFolder = Path.Combine(BaseFolder, "DebugLogs");
                }

                return _LogsFolder;
            }
        }
        private static string _LogsFolder;
        #endregion StringsAsProperties

        /// <summary>
        /// "LocalApplicationData"/CrewChiefV4/sounds/voice
        /// </summary>
        public static readonly string VoiceFilesFolder            = Path.Combine(SoundFilesFolder, "Voice");

        /// <summary>
        /// "MyDocuments"/CrewChiefV4/Profiles
        /// </summary>
        public static string ProfilesFolder { get; }              = Path.Combine(BaseFolder, "Profiles");

        /// <summary>
        /// "MyDocuments"/CrewChiefV4/Profiles/ControllerData
        /// </summary>
        public static readonly string ControllerDataFolder        = Path.Combine(ProfilesFolder, "ControllerData");

        /// <summary>
        /// "MyDocuments"/CrewChiefV4/Profiles/ControllerData/ButtonNames
        /// </summary>
        public static readonly string ControllerButtonNamesFolder = Path.Combine(ControllerDataFolder, "ButtonNames");

        private static string _iRacingTelemetryFolder;
        /// <summary>
        /// "MyDocuments"/iRacing/telemetry
        /// </summary>
        public static string iRacingTelemetryFolder
        {
            get
            {
                if (_iRacingTelemetryFolder == null)
                {
                    _iRacingTelemetryFolder = Path.Combine(MyDocuments, "iRacing", "telemetry");
                    makeFolder(_iRacingTelemetryFolder);
                }
                return _iRacingTelemetryFolder;
            }
        }
        public static readonly string iRacingAppIni               = Path.Combine(MyDocuments, "iRacing", "app.ini");

        private static string _paceNotesFolder;
        public static string PaceNotesFolder
        {
            get
            {
                if (_paceNotesFolder == null)
                {
                    _paceNotesFolder = Path.Combine(BaseFolder, "Pace_notes");
                    makeFolder(_paceNotesFolder);
                }
                return _paceNotesFolder;
            }
        }
        private static string _rF2Folder;
        public static string RF2Folder
        {
            get
            {
                if (_rF2Folder == null)
                {
                    _rF2Folder = Path.Combine(BaseFolder, "RF2");
                    makeFolder(_rF2Folder);
                }
                return _rF2Folder;
            }
        }
        private static string _LMUFolder;
        public static string LMUFolder
        {
            get
            {
                if (_LMUFolder == null)
                {
                    _LMUFolder = Path.Combine(BaseFolder, "LMU");
                    makeFolder(_LMUFolder);
                }
                return _LMUFolder;
            }
        }

        private static string _track_landmarksFolder;
        public static string track_landmarksFolder
        {
            get
            {
                if (_track_landmarksFolder == null)
                {
                    _track_landmarksFolder = Path.Combine(BaseFolder, "Track_landmarks");
                    makeFolder(_track_landmarksFolder);
                }
                return _track_landmarksFolder;
            }
        }

        public static string track_landmarksFolder_release_build
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                    "CrewChiefV4",
                    "Track_landmarks");
            }
        }
        private static string _track_splinesFolder;
        public static string track_splinesFolder
        {
            get
            {
                if (_track_splinesFolder == null)
                {
                    _track_splinesFolder = Path.Combine(BaseFolder, "Track_splines");
                    makeFolder(_track_splinesFolder);
                }
                return _track_splinesFolder;
            }
        }

        private static string _voiceRecognitionDebugFolder = null;
        public static string voiceRecognitionDebugFolder
        {
            get
            {
                if (_voiceRecognitionDebugFolder == null)
                {
                    _voiceRecognitionDebugFolder = Path.Combine(BaseFolder, "VoiceRecognitionDebug");
                    makeFolder(_voiceRecognitionDebugFolder);
                }
                return _voiceRecognitionDebugFolder;
            }
        }

        public static readonly string carClassData                = Path.Combine(BaseFolder, "carClassData.json");
        public static readonly string chart_subscriptions         = Path.Combine(BaseFolder, "chart_subscriptions.json");
        public static readonly string controllerConfigurationData = Path.Combine(BaseFolder, "controllerConfigurationData.json");
        public static readonly string fuel_usage                  = Path.Combine(BaseFolder, "fuel.json");
        public static readonly string game_volumes                = Path.Combine(BaseFolder, "game_volumes.json");
        public static readonly string iracing_reputations         = Path.Combine(BaseFolder, "iracing_reputations.json");
        public static readonly string mqtt_telemetry              = Path.Combine(BaseFolder, "mqtt_telemetry.json");
        public static readonly string pit_benchmarks              = Path.Combine(BaseFolder, "pit_benchmarks.json");
        public static readonly string saved_command_macros        = Path.Combine(BaseFolder, "saved_command_macros.json");
        public static readonly string sounds_variety_data         = Path.Combine(BaseFolder, "sounds-variety-data.txt");
        public static readonly string session_info_dump           = Path.Combine(BaseFolder, "session_info_dump.txt");
        /// <summary>
        /// "MyDocuments"/CrewChiefV4/startupError.txt
        /// </summary>
        public static readonly string startupError                = Path.Combine(BaseFolder, "startupError.txt");
        public static readonly string terminologies               = Path.Combine(BaseFolder, "terminologies.json");
        public static readonly string unvocalized_driver_names    = Path.Combine(BaseFolder, "unvocalized_driver_names.txt");
        /// <summary>
        /// "MyDocuments"/CrewChiefV4/CrewChiefV4.vrconfig.json
        /// </summary>
        public static readonly string VRconfig                    = Path.Combine(BaseFolder, "CrewChiefV4.vrconfig.json");
        /// <summary>
        /// "MyDocuments"/CrewChiefV4/vr_overlay_windows.json
        /// </summary>
        public static readonly string vr_overlay_windows_OLD      = Path.Combine(BaseFolder, "vr_overlay_windows.json");

        /// <summary>
        /// "MyDocuments"/CrewChiefV4/track_landmarks/track_landmarks.json
        /// </summary>
        public static readonly string track_landmarks             = Path.Combine(track_landmarksFolder, "track_landmarks.json");

        /// <summary>
        /// typically c:\Program Files (x86)\Britton IT Ltd\CrewChiefV4\carClassData.json
        /// </summary>
        public static readonly string default_carClassData                = getDefaultFileLocation("carClassData.json");
        public static readonly string default_chart_subscriptions         = getDefaultFileLocation("chart_subscriptions.json");
        public static readonly string default_controllerConfigurationData = getDefaultFileLocation("controllerConfigurationData.json");
        public static readonly string default_mqtt_telemetry              = getDefaultFileLocation("mqtt_telemetry.json");
        public static readonly string default_saved_command_macros        = getDefaultFileLocation("saved_command_macros.json");
        public static readonly string default_track_landmarks             = getDefaultFileLocation("trackLandmarksData.json");
        public static readonly string default_iracing_formation           = getDefaultFileLocation("iracing_formation.json");

        public static readonly string r3e_data_md5 = Path.Combine(BaseFolder, "r3e-data-md5.txt");

        private static bool foldersMade = false;

        /// <summary>
        /// Make all CC's essential data folders
        /// </summary>
        /// <returns>null if folders created otherwise folder that failed</returns>
        public static string MakeFolders()
        {
            if (!foldersMade)
            {
                foreach (var path in new List<string> {
                    BaseFolder,
                    UserConfigFolder,
                    LocalApplicationDataFolder,
                    DebugLogsFolder,
                    ProfilesFolder,
                    ControllerDataFolder,
                    ControllerButtonNamesFolder
                })
                {
                    string folderError = makeFolder(path);
                    if (folderError != null)
                    {
                        return folderError;
                    }
                }
            }
            foldersMade = true;
            return null;
        }

        private static string makeFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    Log.Fatal($"Error creating '{path}' : {ex.Message}");
                    if (path == null)
                    {
                        return "<null>";
                    }
                    return path;
                }
            }
            return null;
        }

        public static List<string> ListFolders()
        {
            List<string> result = new List<string>();
            foreach (var path in new List<string>
                     {
                         BaseFolder,
                         UserConfigFolder,
                         LocalApplicationDataFolder,
                         DebugLogsFolder,
                         ProfilesFolder,
                         ControllerDataFolder,
                         iRacingTelemetryFolder,
                         PaceNotesFolder,
                         RF2Folder,
                         track_landmarksFolder,
                         track_splinesFolder,
                         voiceRecognitionDebugFolder
                     })
            {
                result.Add(path);
            }

            return result;
        }

        public static string Profile(string profileName)
        {
            return Path.Combine(ControllerDataFolder, profileName);
        }
        public static string CurrentProfile()
        {
            return Profile(UserSettings.GetUserSettings().getString("current_settings_profile"));
        }
        public static string Overlay(string overlayName)
        {
            return Path.Combine(BaseFolder, overlayName);
        }
        public static String getUserOverridesFileLocation(String filename)
        {
            return Path.Combine(LocalApplicationDataFolder, filename);
        }

        /// <summary>
        /// Get the path to a user data file, initialising it with default
        /// values if necessary. If there is a problem return the path to 
        /// the defaults file (in the installation path)
        /// </summary>
        /// <param name="dataFile">the path to the data file</param>
        /// <param name="defaultFile">the path to the default file</param>
        /// <param name="description">for the log</param>
        /// <param name="forceMacroDefault">return default, can/should only
        /// be true when called from the macro editor</param>
        /// <returns>the path to the file that's in use</returns>
        public static String GetFileLocation(string dataFile, string defaultFile, string description, bool forceMacroDefault = false)
        {
            if (File.Exists(dataFile) && !forceMacroDefault)
            {
                Log.Info(() => $"Loading user-configured {description} from Documents/CrewChiefV4/ folder");
                return dataFile;
            }
            else if (!File.Exists(dataFile))
            {
                // make sure we save a copy to the user config directory
                // no need to worry about forceMacroDefault as content of the file will be same.
                try
                {
                    File.Copy(defaultFile, dataFile);
                    Log.Info(() => $"Loading user-configured {description} from Documents/CrewChiefV4/ folder");
                    Log.Info(() => $"(new file loaded with defaults from {defaultFile})");
                    return dataFile;
                }
                catch (Exception e)
                {
                    Log.Error($"Error copying default {description} to user dir '{dataFile}': " + e.Message);
                    Log.Error($"Loading default {description} from installation folder");
                    if (dataFile.Contains("OneDrive"))
                    {
                        Log.Error("(Possibly a problem with OneDrive on your PC)");
                    }
                    return defaultFile;
                }
            }
            else // (Macro) file exists but return the default anyway
            {
                Log.Info(() => $"Loading default {description} from installation folder");
                return defaultFile;
            }
        }

        /// <summary>
        /// Get the path for the file in the installation folder
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>typically c:\Program Files (x86)\Britton IT Ltd\CrewChiefV4\FILENAME </returns>
        public static String getDefaultFileLocation(String filename)
        {
            return getDefaultLocation(filename, true);
        }

        /// <summary>
        /// Get the path for the subfolder in the installation folder
        /// </summary>
        /// <param name="folderName"></param>
        /// <returns>typically c:\Program Files (x86)\Britton IT Ltd\CrewChiefV4\FOLDERNAME </returns>
        public static String getDefaultFolderLocation(String folderName)
        {
            return getDefaultLocation(folderName, false);
        }

        private static String getDefaultLocation(String name, Boolean isFile)
        {
            String regularPath = Application.StartupPath + @"\" + name;
            String debugPath = Application.StartupPath + @"\..\..\" + name;

            // Unit tests path:
            String utPath = "";
            try
            {
                utPath = Path.GetFullPath(Path.Combine(UnitTestPath, name));
            }
            catch (Exception ex)
            {
                Log.Info(() => $"No unit test path '{UnitTestPath}/{name}': {ex.Message}");
            }

            if (CrewChief.Debug.UseDebugFilePaths)
            {
                if (isFile)
                {
                    return File.Exists(debugPath) ? debugPath : File.Exists(regularPath) ? regularPath : utPath;
                }
                else
                {
                    return Directory.Exists(debugPath) ? debugPath : Directory.Exists(regularPath) ? regularPath : utPath;
                }
            }
            else
            {
                if (isFile)
                {
                    return File.Exists(regularPath) ? regularPath : File.Exists(debugPath) ? debugPath : utPath;
                }
                else
                {
                    return Directory.Exists(regularPath) ? regularPath : Directory.Exists(debugPath) ? debugPath : utPath;
                }
            }
        }

        /// <summary>
        /// Search for a file in the logs folders, first debug logs then
        /// the user's logs
        /// </summary>
        /// <param name="fileNameToResolve"></param>
        /// <returns></returns>
        public static string ResolveDataFile(string fileNameToResolve)
        {
            // Search in dataFiles (which may be a debug folder):
            if (Directory.Exists(DebugLogsFolder))
            {
                var resolvedFilePaths = Directory.GetFiles(DebugLogsFolder, fileNameToResolve, SearchOption.AllDirectories);
                if (resolvedFilePaths.Length > 0)
                {
                    return resolvedFilePaths[0];
                }
            }

            // Search documents debugLogs as well:
            var resolvedFileUserPaths = Directory.GetFiles(UserLogsFolder, fileNameToResolve, SearchOption.AllDirectories);

            if (resolvedFileUserPaths.Length > 0)
            {
                return resolvedFileUserPaths[0];
            }

            Log.Error($"Failed to resolve trace file full path: {fileNameToResolve} in {DebugLogsFolder} or {UserLogsFolder}");
            return null;
        }

        /// Depth-first recursive delete, with handling for descendant
        /// directories open in Windows Explorer and other Windows "not doing what its been told" arseholery.
        public static void ForciblyDeleteDirectory(string path)
        {
            foreach (string directory in Directory.GetDirectories(path))
            {
                ForciblyDeleteDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }
        public static void DeleteDebugFolders()
        {
            foreach (var path in new List<string> {
                    BaseFolder,
                    UserConfigFolder,
                    LocalApplicationDataFolder,
                    //DebugLogsFolder,
                    //ProfilesFolder,
                    //ControllerDataFolder,
                    iRacingTelemetryFolder,
                    //PaceNotesFolder,
                    //RF2Folder,
                    //track_landmarksFolder,
                    //voiceRecognitionDebugFolder
                })
            {
                ForciblyDeleteDirectory(path);
            }
            // Now get the user.config file used for Properties
            // e.g. %LOCALAPPDATA%\Britton_IT_Ltd\CrewChiefV4.exe_Url_m1bxdl5vgz04om0fj2vv55dp4opyjs4u\4.17.1.4\user.config 
            var level = ConfigurationUserLevel.PerUserRoamingAndLocal;
            var configuration = ConfigurationManager.OpenExeConfiguration(level);
            var configurationFilePath = configuration.FilePath;
            // then delete the whole folder used by this build, e.g. %LOCALAPPDATA%\Britton_IT_Ltd\CrewChiefV4.exe_Url_m1bxdl5vgz04om0fj2vv55dp4opyjs4u
            ForciblyDeleteDirectory(Path.GetDirectoryName(Path.GetDirectoryName(configurationFilePath)));
            // (Have to zap them all otherwise Properties.Settings.Default.Upgrade()
            // will use previous releases)
        }
    }

    /// <summary>
    /// Class separated from DataFiles as it seems initialisation was unreliable
    /// </summary>
    internal class Installation
    {
        // Yet another method for finding the project root when unit testing...
        static readonly Type t = typeof(DataFiles);
        static readonly Assembly assemFromType = t.Assembly;
        public string SourceFilesRoot
        {
            get
            {
                string path;
                try
                {
                    path = Path.GetFullPath(
                        (Path.Combine(
                            Directory.GetParent(
                                    Directory.GetParent(
                                            Directory.GetParent(
                                                Path.GetDirectoryName(assemFromType.Location)
                                            ).FullName)
                                        .FullName)
                                .FullName, "CrewChiefV4")));
                }
                catch (Exception ex)
                {
                    path = @"..\..\..\CrewChiefV4";
                }
                return path;
            }
        }
    }
}
