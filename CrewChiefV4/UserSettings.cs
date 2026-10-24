using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

using Newtonsoft.Json;

namespace CrewChiefV4
{
    class UserSettings
    {
        // blat the user config folder for cases where it gets fucked up.
        public static void ForciblyDeleteConfigDirectory()
        {
            DataFiles.ForciblyDeleteDirectory(DataFiles.UserConfigFolder);
        }

        public Boolean initFailed = false;
        public string initFailedStack = "";
        public string initMessage = "";
        public string initFailedExceptionMessage = "";

        private static  String[] reservedNameStarts = new String[] { "CHANNEL_", "TOGGLE_", "VOICE_OPTION", "background_volume",
            "messages_volume", "last_game_definition", "UpdateSettings",ControllerConfiguration.ControllerData.PROPERTY_CONTAINER,
            "PERSONALISATION_NAME", "app_version", "spotter_name", "codriver_name", "codriver_style", "update_notify_attempted", "last_trace_file_name",
            "NAUDIO_DEVICE_GUID", "NAUDIO_RECORDING_DEVICE_GUID", "chief_name", "current_settings_profile", "main_window_position", "main_window_size"};

        private static String defaultUserSettingsfileName = "defaultSettings.json";
        public static String userProfilesPath = DataFiles.ProfilesFolder;
        public static String currentUserProfileFileName = "";

        public class UserProfileSettings
        {
            public Dictionary<string, object> userSettings { get; set; }
            public UserProfileSettings()
            {
                userSettings = new Dictionary<string, object>();
            }
        }

        public static UserProfileSettings currentActiveProfile = new UserProfileSettings();

        public Dictionary<string, object> currentApplicationSettings = new Dictionary<string, object>();

        public void loadActiveUserSettingsProfile(String fileName, Boolean loadingDefault)
        {
            Boolean settingsProfileBroken = false;
            // Create a user profile with the users current settings if it does not yet exist(new user, first time upgrade to new format, default file deleted)
            if (!File.Exists(Path.Combine(userProfilesPath, defaultUserSettingsfileName)))
            {
                UserProfileSettings userProfileSettings = new UserProfileSettings();
                foreach (SettingsProperty prop in getProperties())
                {
                    userProfileSettings.userSettings.Add(prop.Name, Properties.Settings.Default[prop.Name]);
                }
                saveUserSettingsFile(userProfileSettings, Path.Combine(userProfilesPath, defaultUserSettingsfileName));
            }

            try
            {
                // If the requested file does not exist load default settings profile
                if (!File.Exists(fileName))
                {
                    fileName = Path.Combine(userProfilesPath, defaultUserSettingsfileName);
                    setProperty("current_settings_profile", defaultUserSettingsfileName);
                    saveUserSettings();
                    currentUserProfileFileName = getString("current_settings_profile");
                }
                currentActiveProfile = readUserProfileSettingsFile(fileName);
                if (currentActiveProfile == null)
                {
                    currentActiveProfile = new UserProfileSettings();
                    settingsProfileBroken = true;
                }
                else if (currentActiveProfile.userSettings == null)
                {
                    currentActiveProfile.userSettings = new Dictionary<string, object>();
                    settingsProfileBroken = true;
                }
                else
                {
                    return;
                }
            }
            catch (Exception e)
            {
                settingsProfileBroken = true;
                Log.Exception(e, $"Could not find or read {fileName}");
            }

            if (settingsProfileBroken
                && loadingDefault)
            {
                Console.WriteLine($"Failed to Load default settings file at: '{fileName}', giving up.");
                return;
            }

            try
            {
                if (settingsProfileBroken)
                {
                    Utilities.TryBackupBrokenFile(fileName, "broken", "Broken user settings profile " + fileName);
                    // if the default settings file is broken we have to recreate it.
                    if (Path.Combine(userProfilesPath, defaultUserSettingsfileName).Equals(fileName))
                    {
                        UserProfileSettings userProfileSettings = new UserProfileSettings();
                        foreach (SettingsProperty prop in getProperties())
                        {
                            userProfileSettings.userSettings.Add(prop.Name, Properties.Settings.Default[prop.Name]);
                        }
                        saveUserSettingsFile(userProfileSettings, Path.Combine(userProfilesPath, defaultUserSettingsfileName));
                    }
                    setProperty("current_settings_profile", defaultUserSettingsfileName);
                    saveUserSettings();
                    currentUserProfileFileName = getString("current_settings_profile");
                    // Load default file
                    loadActiveUserSettingsProfile(fileName:Path.Combine(userProfilesPath, defaultUserSettingsfileName), loadingDefault:true);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to Load settings file ");
            }
        }
        public UserProfileSettings loadUserSettings(String fileName)
        {
            // If the requested file does not exist load default settings profile
            if (!File.Exists(fileName))
            {
                fileName = Path.Combine(userProfilesPath, defaultUserSettingsfileName);
            }
            return readUserProfileSettingsFile(fileName);
        }

        /// <summary>
        /// Read the JSON file and remove help strings (that start with #)
        /// put in when saving the Profile.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>null if there was an error</returns>
        private UserProfileSettings readUserProfileSettingsFile(string fileName)
        {
            UserProfileSettings userProfileSettings = JsonFiles.ReadFile<UserProfileSettings>(fileName);
            if (userProfileSettings != null)
            {
                var tempDict = new Dictionary<string, Object>(userProfileSettings.userSettings);
                foreach (var entry in tempDict)
                {
                    if (entry.Key.StartsWith("#"))
                    { // Strip out any help strings in file
                        userProfileSettings.userSettings.Remove(entry.Key);
                    }
                }
            }
            return userProfileSettings;
        }

        /// <summary>
        /// Save as a JSON file after sorting the entries and interleaving
        /// help strings where available. This makes them more readable
        /// to any weirdo that wants to do that.
        /// </summary>
        /// <param name="profileSettings"></param>
        /// <param name="fileName"></param>
        public static void saveUserSettingsFile(UserProfileSettings profileSettings, String fileName)
        {
            Dictionary<string, Object> commentedProfile = new Dictionary<string, object>();
            SortedDictionary<string, Object> tempSortedDict = new SortedDictionary<string, Object>(profileSettings.userSettings);
            foreach (var entry in tempSortedDict)
            {
                commentedProfile[entry.Key] = entry.Value;
                string helpString = Configuration.getUIStringMaybeNull(entry.Key + "_help");
                if (helpString != entry.Key + "_help")
                {   // If a help string is available interleave it in the dictionary
                    commentedProfile['#' + entry.Key] = helpString;
                }
            }
            profileSettings.userSettings = commentedProfile;
            JsonFiles.WriteFile(Path.Combine(userProfilesPath, fileName), profileSettings);

        }

        /// <summary>
        /// Upgrade settings in all user profiles found in user profiles folder
        /// </summary>
        private void upgradeUserProfileSettings()
        {
            try
            {
                string[] files = Directory.GetFiles(userProfilesPath, "*.json", SearchOption.TopDirectoryOnly);
                UserProfileSettings defaultAppSettings = new UserProfileSettings();
                //build a list of current user scope settings.
                foreach (SettingsProperty prop in getProperties())
                {
                    defaultAppSettings.userSettings.Add(prop.Name, Properties.Settings.Default[prop.Name] );
                }

                foreach (var file in files)
                {
                    UserProfileSettings userProfileSetting = loadUserSettings(file);
                    if(userProfileSetting != null)
                    {
                        Boolean save = false;
                        // add any missing items
                        var addedDefaultItems = defaultAppSettings.userSettings.Where(ups2 => !userProfileSetting.userSettings.Any(ups1 => ups1.Key == ups2.Key)).ToList();
                        if (addedDefaultItems.Count > 0)
                        {
                            foreach (var item in addedDefaultItems)
                            {
                                userProfileSetting.userSettings.Add(item.Key, item.Value);
                            }
                            save = true;
                        }
                        // remove items no longer used
                        var removedDefaultItems = userProfileSetting.userSettings.Where(ups2 => !defaultAppSettings.userSettings.Any(ups1 => ups1.Key == ups2.Key)).ToList();
                        if (removedDefaultItems.Count > 0)
                        {
                            foreach (var item in removedDefaultItems)
                            {
                                userProfileSetting.userSettings.Remove(item.Key);
                            }
                            save = true;
                        }
                        if (save)
                        {
                            saveUserSettingsFile(userProfileSetting, file);
                        }
                    }

                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Load the user settings from either "current_settings_profile"
        /// or the profile specified in the command line arg -profile <profile name>
        /// If the command line profile file does not exist use the default
        /// (not the current) profile instead.
        /// </summary>
        internal UserSettings()
        {
            // Set profile from command line '-profile "file name without extension" ...'.  This needs to be
            // done here, because this executes before Main.
            var profileRequestedFromCommandLine = CrewChief.CommandLine.Get("profile");

            if (!string.IsNullOrWhiteSpace(profileRequestedFromCommandLine))
            {
                // Initialise to defaultUserSettingsfileName for the case where
                // the specified profile does not exist
                Properties.Settings.Default["current_settings_profile"] = defaultUserSettingsfileName;
                var files = Directory.GetFiles(userProfilesPath, "*.json", SearchOption.TopDirectoryOnly).ToList();
                foreach (var file in files)
                {
                    var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
                    if (profileRequestedFromCommandLine.Equals(fileNameNoExt, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var fileName = Path.GetFileName(file);
                        Properties.Settings.Default["current_settings_profile"] = fileName;
                        break;
                    }
                }
            }
            try
            {
                // start by checked we can actually read a property value - this will throw an exception if the
                // user settings in AppData are broken
                initMessage = "Can't read a property value";
                int x = Properties.Settings.Default.main_window_position.X;

                // Add build in action mappings to reserved name list.
                List<string> nameList = reservedNameStarts.ToList();
                nameList.AddRange(ControllerConfiguration.builtInActionMappings.Keys);
                reservedNameStarts = nameList.ToArray();

                foreach (SettingsProperty prop in getProperties(true))
                {
                    currentApplicationSettings.Add(prop.Name, Properties.Settings.Default[prop.Name]);
                }

                // Copy user settings from previous application version if necessary
                string currentAppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                String savedAppVersion = getString("app_version");
                if (savedAppVersion == null || !savedAppVersion.Equals(currentAppVersion))
                {
                    savedAppVersion = string.IsNullOrWhiteSpace(savedAppVersion) ? "unknown" : savedAppVersion;
                    initMessage = $"Upgraded user settings from {savedAppVersion} to {currentAppVersion}";
                    Properties.Settings.Default.Upgrade();
                    setProperty("app_version", currentAppVersion);
                    Properties.Settings.Default.Save();
                    upgradeUserProfileSettings();

                    // We need to reload Application settings if we've upgraded (otherwise we miss a lot of stuff set in the prev app version, including active profile).
                    currentApplicationSettings.Clear();
                    foreach (SettingsProperty prop in getProperties(true))
                    {
                        currentApplicationSettings.Add(prop.Name, Properties.Settings.Default[prop.Name]);
                    }
                }

                // get the filename of the current active profile
                currentUserProfileFileName = getString("current_settings_profile");

                if (!string.IsNullOrWhiteSpace(currentUserProfileFileName))
                {
                    loadActiveUserSettingsProfile(fileName: Path.Combine(userProfilesPath, currentUserProfileFileName), loadingDefault: false);
                    initMessage = $"Loaded profile '{currentUserProfileFileName.Split('.')[0]}'";
                }
                else
                {
                    loadActiveUserSettingsProfile(fileName: Path.Combine(userProfilesPath, defaultUserSettingsfileName), loadingDefault: true);
                    initMessage = "Loaded default profile";
                }
            }
            catch (Exception exception)
            {
                // if any of this initialisation fails, the app is in an unusable state.
                Console.WriteLine(exception.Message);
                initFailed = true;
                initFailedExceptionMessage = exception.Message;
                initFailedStack = exception.StackTrace;
            }
        }

        private static List<SettingsProperty> getProperties(bool applicationScopeOnly = false)
        {
            List<SettingsProperty> props = new List<SettingsProperty>();

            foreach (SettingsProperty prop in Properties.Settings.Default.Properties)
            {
                Boolean isReserved = false;
                foreach (String reservedNameStart in reservedNameStarts)
                {
                    if (prop.Name.StartsWith(reservedNameStart))
                    {
                        if(applicationScopeOnly)
                        {
                            props.Add(prop);
                        }
                        isReserved = true;
                        break;
                    }
                }
                if (!isReserved && !applicationScopeOnly)
                {
                    props.Add(prop);
                }
            }
            return props.OrderBy(x => x.Name).ToList();
        }

        public List<SettingsProperty> getProperties(Type requiredType, String nameMustStartWith, String nameMustNotStartWith)
        {
            List<SettingsProperty> props = new List<SettingsProperty>();
            if (!initFailed)
            {
                foreach (SettingsProperty prop in Properties.Settings.Default.Properties)
                {
                    Boolean isReserved = false;
                    foreach (String reservedNameStart in reservedNameStarts)
                    {
                        if (prop.Name.StartsWith(reservedNameStart))
                        {
                            isReserved = true;
                            break;
                        }
                    }
                    if (!isReserved &&
                        (nameMustStartWith == null || nameMustStartWith.Length == 0 || prop.Name.StartsWith(nameMustStartWith)) &&
                        (nameMustNotStartWith == null || nameMustNotStartWith.Length == 0 || !prop.Name.StartsWith(nameMustNotStartWith)) &&
                        !prop.IsReadOnly && prop.PropertyType == requiredType)
                    {
                        props.Add(prop);
                    }
                }
            }
            return props.OrderBy(x => x.Name).ToList();
        }

        private static UserSettings _userSettings = new UserSettings();

        private Boolean propertiesUpdated = false;
        private Boolean userProfilePropertiesUpdated = false;

        public static UserSettings GetUserSettings()
        {
            if (_userSettings == null)
            {
                _userSettings = new UserSettings();
            }
            return _userSettings;
        }

        public String getString(String name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    if (currentActiveProfile.userSettings.TryGetValue(name, out object value))
                    {
                        return (String)value;
                    }
                    else if (currentApplicationSettings.TryGetValue(name, out value))
                    {
                        return (String)value;
                    }
                    else
                    {
                        return (String)Properties.Settings.Default[name];
                    }
                }
                catch (Exception)
                {
                    Log.DontSpam("PROPERTY " + name + " NOT FOUND").Commentary();
                }
            }

            return "";
        }

        /// <summary>
        /// Get the full path, expanding any environment variables like %PROGRAMFILES(X86)%
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public String getPath(String name)
        {
            string path = getString(name);
            if (!path.ToLower().Contains("steam://"))
            {
                path = expandPath(path, name);
            }
            return path;
        }

        /// <summary>
        /// Expand any environment variables like %PROGRAMFILES(X86)% in the path
        /// and return the full path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="name">name of the Property for error messages</param>
        /// <returns>original path if error</returns>
        internal string expandPath(string path, string name = "No path name")
        {
            string resultPath = path;
            if (path == null)
            {
                Log.Error($"Path '{name}' is blank");
            }
            else
            {
                try
                {
                    resultPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
                    if (resultPath != path)
                    {
                        Log.Commentary($"Path '{name}':'{path}' becomes '{resultPath}");
                    }
                }
                catch (Exception ex)
                {
                    if (!path.Contains("steam://"))
                    {
                        Log.Error($"Path '{name}':'{path}' is not valid");
                    }
                }
            }
            return resultPath;
        }

        public float getFloat(String name)
        {
            try
            {
                if (currentActiveProfile.userSettings.TryGetValue(name, out object value))
                {
                    return Convert.ToSingle(value);
                }
                else if (currentApplicationSettings.TryGetValue(name, out value))
                {
                    return Convert.ToSingle(value);
                }
                else
                {
                    return (float)Properties.Settings.Default[name];
                }
            }
            catch (Exception)
            {
                Log.DontSpam("PROPERTY " + name + " NOT FOUND").Commentary();
            }

            return 0f;
        }

        public Boolean getBoolean(String name)
        {
            try
            {
                if (currentActiveProfile.userSettings.TryGetValue(name, out object value))
                {
                    return Convert.ToBoolean(value);
                }
                else if (currentApplicationSettings.TryGetValue(name, out value))
                {
                    return Convert.ToBoolean(value);
                }
                else
                {
                    return (Boolean)Properties.Settings.Default[name];
                }
            }
            catch (Exception)
            {
                Log.DontSpam("PROPERTY " + name + " NOT FOUND").Commentary();
            }

            return false;
        }

        public int getInt(String name)
        {
            try
            {
                if (currentActiveProfile.userSettings.TryGetValue(name, out object value))
                {
                    return Convert.ToInt32(value);
                }
                else if (currentApplicationSettings.TryGetValue(name, out value))
                {
                    return Convert.ToInt32(value);
                }
                else
                {
                    return (int)Properties.Settings.Default[name];
                }
            }
            catch (Exception)
            {
                Log.DontSpam("PROPERTY " + name + " NOT FOUND").Commentary();
            }
            return 0;
        }

        public void setProperty(String name, Object value)
        {
            if (!initFailed)
            {
                try
                {
                    if (currentActiveProfile.userSettings.ContainsKey(name))
                    {
                        if (!value.Equals(currentActiveProfile.userSettings[name]))
                        {
                            userProfilePropertiesUpdated = true;
                            currentActiveProfile.userSettings[name] = value;
                        }
                    }
                    else if (value != null
                        && !value.Equals(Properties.Settings.Default[name]))
                    {
                        Properties.Settings.Default[name] = value;
                        currentApplicationSettings[name] = value;
                        propertiesUpdated = true;
                    }
                }
                catch (Exception ex)
                {   // This has been reported but I have no idea why it is happening
                    Log.setLogLevel(Log.LogType.Exception);
                    Log.Exception(ex, $"currentActiveProfile: {currentActiveProfile} currentActiveProfile.userSettings: {currentActiveProfile.userSettings}");
                }
            }
        }

        public void saveUserSettings()
        {
            // By MSDN it is not ok to write from multiple threads simultaneously, so lock here.
            lock (this)
            {
                if (!initFailed)
                {
                    if(propertiesUpdated)
                    {
                        Properties.Settings.Default.Save();
                    }
                    if(userProfilePropertiesUpdated)
                    {
                        saveUserSettingsFile(currentActiveProfile, currentUserProfileFileName);
                    }
                }
            }
        }

        /// <summary>
        /// Return the settings that have been changed
        /// </summary>
        /// <returns>String of lines, one per changed setting</returns>
        public static string getNonDefaultUserSettings()
        {
            string changes = null;
            string value = null;
            foreach (SettingsProperty prop in getProperties())
            {
                if (prop.Name == "FIRST_RUN")
                    continue; // It only has default value the first time

                // the name that the user sees for this in the Settings page
                var ui_name = Configuration.getUIStringMaybeNull(prop.Name);

                if (currentActiveProfile.userSettings.ContainsKey(prop.Name))
                {
                    if (prop.PropertyType == typeof(string))
                    {
                        if (prop.DefaultValue.Equals(currentActiveProfile.userSettings[prop.Name]))
                        {
                            continue;
                        }
                        // Quote strings to show up any white space
                        value = $"'{currentActiveProfile.userSettings[prop.Name]}'";
                    }
                    else
                    {
                        if (prop.DefaultValue.Equals(currentActiveProfile.userSettings[prop.Name].ToString()))
                        {
                            continue;
                        }
                        value = $"{currentActiveProfile.userSettings[prop.Name]}";
                    }
                    changes += $"{prop.Name}: {value} # {ui_name}\n";
                }
                else if (CrewChief.Debug.LoggingDebug) 
                {
                    changes += $"Error: '{prop.Name}' not in userSettings # {ui_name}\n";
                }
            }
            return changes;
        }
    }
}
