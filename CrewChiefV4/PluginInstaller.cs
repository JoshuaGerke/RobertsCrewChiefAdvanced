//#define EVO_BROADCAST // Opponent data available  TBD EVO
using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;

namespace CrewChiefV4
{
    partial class PluginInstaller
    {
        Boolean messageBoxPresented;
        Boolean messageBoxResult;
        private const String rf2PluginFileName = "RF2.3.8.SharedMemoryMapPlugin.dll";
        private const String lmuPluginFileName = "LMU.3.8.SharedMemoryMapPlugin.dll";
        private const String rf2PluginFileNameOld32bit = "rFactor2SharedMemoryMapPlugin.dll";
        private const String rf2PluginFileNameOld64bit = "rFactor2SharedMemoryMapPlugin64.dll";

        private readonly string accBroadcastFileContents = String.Format(
            "{{\n    \"updListenerPort\": {0},\n    \"connectionPassword\": \"{1}\",\n    \"commandPassword\": \"{2}\"\n}}",
            ksBroadcastingTestClient.ClientConnections.ClientPanelViewModel.Port,
            ksBroadcastingTestClient.ClientConnections.ClientPanelViewModel.ConnectionPw,
            ksBroadcastingTestClient.ClientConnections.ClientPanelViewModel.CommandPw);
        public PluginInstaller()
        {
            messageBoxPresented = false;
            messageBoxResult = false;
        }

        private static string checkMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream));
                }
            }
        }

        //referance https://github.com/Jamedjo/RSTabExplorer/blob/master/RockSmithTabExplorer/Services/RocksmithLocator.cs
        private string getSteamFolder()
        {
            string steamInstallPath = "";
            try
            {
                RegistryKey steamKey = Registry.LocalMachine.OpenSubKey(@"Software\Valve\Steam") ?? Registry.LocalMachine.OpenSubKey(@"Software\Wow6432Node\Valve\Steam");
                if (steamKey != null)
                {
                    steamInstallPath = steamKey.GetValue("InstallPath").ToString();
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Exception getting steam folder: ");
            }
            return steamInstallPath;
        }

        private List<string> getSteamLibraryFolders()
        {
            List<string> folders = new List<string>();

            string steamFolder = getSteamFolder();
            if (Directory.Exists(steamFolder))
            {
                folders.Add(steamFolder);
                string configFile = Path.Combine(steamFolder, @"config\config.vdf");

                if (File.Exists(configFile))
                {
                    Regex regex = new Regex("BaseInstallFolder[^\"]*\"\\s*\"([^\"]*)\"");
                    using (StreamReader reader = new StreamReader(configFile))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            Match match = regex.Match(line);
                            if (match.Success)
                            {
                                folders.Add(Regex.Unescape(match.Groups[1].Value));
                            }
                        }
                    }
                }

            }
            return folders;
        }

        private Boolean presentInstallMessagebox(string gameName)
        {
            if (messageBoxPresented == false)
            {
                messageBoxPresented = true;
                var prompt = gameName + " " + Configuration.getUIStringWrapped("install_plugin_popup_text", int.MaxValue);
                if (DialogResult.OK == MessageBox.Show(prompt,
                        Configuration.getUIString("install_plugin_popup_title"),
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Information))
                {
                    messageBoxResult = true;
                }
            }
            return messageBoxResult;
        }

        private Boolean presentEnableMessagebox()
        {
            var prompt = Configuration.getUIStringWrapped("install_plugin_popup_enable_text", int.MaxValue);
            if (DialogResult.OK == MessageBox.Show(prompt, Configuration.getUIString("install_plugin_popup_enable_title"),
                MessageBoxButtons.OKCancel, MessageBoxIcon.Information))
            {
                return true;
            }
            return false;
        }

        private Boolean presentCreateMessagebox()
        {
            if (DialogResult.OK == MessageBox.Show(Configuration.getUIStringWrapped("install_plugin_popup_create_text"), Configuration.getUIString("install_plugin_popup_create_title"),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information))
            {
                return true;
            }
            return false;
        }

        private Boolean presentRemoveMessagebox()
        {
            if (DialogResult.OK == MessageBox.Show($"Remove {rf2PluginFileNameOld64bit}?  It's not used by Crew Chief anymore but other apps may use it", "Crew Chief rFactor 2 / LMU Shared Memory Update",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information))
            {
                return true;
            }
            return false;
        }

        private void presentInstallUpdateErrorMessagebox(string errorText)
        {
            MessageBox.Show(Configuration.getUIString("install_plugin_popup_error_text") + Environment.NewLine + errorText, Configuration.getUIString("install_plugin_popup_error_title"),
                MessageBoxButtons.OK, MessageBoxIcon.Error);

            // Make sure we wil retry again if user does not restart CC.
            messageBoxPresented = false;
        }

        /// <summary>
        /// Check whether the plugin is installed and up to date
        /// </summary>
        /// <returns>false if it's not installed</returns>
        private bool installOrUpdatePlugin(string source, string destination, string gameName)
        {
            bool result = false;
            //I stole this from the internetz(http://stackoverflow.com/questions/3201598/how-do-i-create-a-file-and-any-folders-if-the-folders-dont-exist)
            try
            {
                string[] files = null;

                if (destination[destination.Length - 1] != Path.DirectorySeparatorChar)
                {
                    destination += Path.DirectorySeparatorChar;
                }

                if (!Directory.Exists(destination))
                {
                    Directory.CreateDirectory(destination);
                }

                files = Directory.GetFileSystemEntries(source);
                foreach (string element in files)
                {
                    // Sub directories                    
                    if (Directory.Exists(element))
                    {
                        result = installOrUpdatePlugin(element, destination + Path.GetFileName(element), gameName);
                    }
                    else
                    {
                        // Files in directory
                        string destinationFile = destination + Path.GetFileName(element);
                        //if the file exists we will check if it needs updating
                        if (File.Exists(destinationFile))
                        {
                            var ccMD5 = checkMD5(element);
                            var presentMD5 = checkMD5(destinationFile);
                            if (!ccMD5.Equals(presentMD5))
                            {
                                Log.Warning($"New plugin checksum {ccMD5}, present plugin {presentMD5}");
                                //ask the user if they want to update the plugin
                                if (presentInstallMessagebox(gameName))
                                {
                                    File.Copy(element, destinationFile, true);
                                    Log.Commentary("Updated plugin file: " + destinationFile);
                                    Log.Verbose(() => $"Updated plugin checksum {checkMD5(destinationFile)}");
                                }
                                else
                                {
                                    Log.Warning($"User chose not to update '{destinationFile}'");
                                }
                            }
                            else
                            {
                                Log.Commentary($"Plugin '{destinationFile}' already present");
                            }
                        }
                        else
                        {
                            if (presentInstallMessagebox(gameName))
                            {
                                File.Copy(element, destinationFile, true);
                                Log.Commentary("Installed plugin file: " + destinationFile);
                            }
                        }
                        result = true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to Copy plugin files: ");
                presentInstallUpdateErrorMessagebox(e.Message);
            }
            return result;
        }

        /// <summary>
        /// Some games have plugins that need to be copied into the game somewhere,
        /// some games have config files that may need to be updated,
        /// some games have both, some games have neither.
        /// </summary>
        public void InstallOrUpdatePlugins(GameDefinition gameDefinition)
        {
            if (UserSettings.GetUserSettings().getBoolean("use_legacy_plugin_installer"))
            {
                InstallOrUpdatePlugins_deprecated(gameDefinition);
                return;
            }
            if (gameDefinition.hasPlugin)
            {   // Some games have plugins that need to be copied into the game somewhere
                string gameInstallPath = checkGameInstallPath(gameDefinition);

                if (Directory.Exists(gameInstallPath))
                { //we have a gameInstallPath so we can go on with installation/updating assuming that the user wants to enable the plugin.
                    if (!installOrUpdatePlugin(
                            Path.Combine(DataFiles.getDefaultFolderLocation("plugins"), gameDefinition.pluginDirectory),
                            gameInstallPath, gameDefinition.friendlyName))
                    {
                        return; // the plugin wasn't already installed and user did not install it
                    }
                }
            }
            else if (Game.RF1)
            {
                // this is an rFactor-based game that's not rFactor or AMS
                // (so it's fTruck, Marcas or GSC) - no automatic
                // installation of plugin for these old games
                Log.Warning("*** Auto-install of plugin is not supported for " + gameDefinition.friendlyName);
                Log.Warning("*** Continuing, assuming that the plugin in install folder" + " (default location C:\\Program Files(x86)\\Britton IT Ltd\\CrewChiefV4\\plugins\\rFactor\\Plugins) has been copied to your game's install folder");
                return;
            }

            // Some games have config files that may need to be updated
            updateGameConfigFile(gameDefinition);
        }

        /// <summary>
        /// Check if the install path is valid, if not ask the user to browse for it.
        /// </summary>
        /// <returns>gameInstallPath or null it doesn't exist</returns>
        private string checkGameInstallPath(GameDefinition gameDefinition)
        {
            string gameInstallPathPropertyName = gameDefinition.gameInstallPathPropertyName;
            string gameInstallPath = UserSettings.GetUserSettings().getPath(gameInstallPathPropertyName);

            if (!Directory.Exists(gameInstallPath))
            {
                //Present a messagebox to the user asking if they want to install plugins
                if (presentInstallMessagebox(gameDefinition.friendlyName))
                {   // First try to get the install folder from steam common install folders.
                    List<string> steamLibs = getSteamLibraryFolders();
                    foreach (string lib in steamLibs)
                    {
                        string commonPath = Path.Combine(lib, @"steamapps\common\" + gameDefinition.gameInstallDirectory);
                        if (Directory.Exists(commonPath))
                        {
                            gameInstallPath = commonPath;
                            Log.Commentary($"Install path for {gameDefinition.friendlyName} not given, found '{gameInstallPath}'");
                            break;
                        }
                    }
                    if (!Directory.Exists(gameInstallPath))
                    {   //Not found in steam folders ask the user to locate the directory
                        FolderBrowserDialog dialog = new FolderBrowserDialog();
                        dialog.ShowNewFolderButton = false;
                        dialog.Description = Configuration.getUIString("install_plugin_select_directory_start") + " " +
                                             gameDefinition.gameInstallDirectory + " " + Configuration.getUIString("install_plugin_select_directory_end") +
                                             " '" + gameInstallPath + "')";
                        dialog.RootFolder = Environment.SpecialFolder.MyComputer;

                        DialogResult result = dialog.ShowDialog();

                        if (result == DialogResult.OK && dialog.SelectedPath.Length > 0)
                        {
                            //This should now take care of checking against the main .exe instead of the folder name, special case for rFactor 2 as its has the file installed in ..\Bin64
                            if (Game.RF2_64BIT)
                            {
                                if (File.Exists(Path.Combine(dialog.SelectedPath, @"Bin64", gameDefinition.processName + ".exe")))
                                {
                                    gameInstallPath = dialog.SelectedPath;
                                }
                            }
                            else if (File.Exists(Path.Combine(dialog.SelectedPath, gameDefinition.processName + ".exe")))
                            {
                                gameInstallPath = dialog.SelectedPath;
                            }
                            else
                            {
                                //present again if user didn't select the correct folder 
                                InstallOrUpdatePlugins(gameDefinition);
                            }
                        }
                        else if (result == DialogResult.Cancel)
                        {
                            return gameInstallPath;
                        }
                    }
                }
                if (gameInstallPath != UserSettings.GetUserSettings().getPath(gameInstallPathPropertyName))
                {
                    UserSettings.GetUserSettings().setProperty(gameInstallPathPropertyName, gameInstallPath);
                    UserSettings.GetUserSettings().saveUserSettings();
                    Log.Commentary($"Updated install path for {gameDefinition.friendlyName} to '{gameInstallPath}'");
                }
            }
            return gameInstallPath;
        }

        /// <summary>
        /// Update the config file for the game if it has one.
        /// </summary>
        private void updateGameConfigFile(GameDefinition gameDefinition)
        {
            string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            // Check them every time as games may have edited them too.
            switch (Game.gameType)
            {
                // Finally, some games have both, edit config even if already installed
                case GameType.RF1:
                    {
                        Log.Commentary("Plugin installed but not enabled (if that's necessary?)");
                        break;
                    }
                case GameType.RF2:
                case GameType.LMU:
                    {
                        string gameInstallPathPropertyName = gameDefinition.gameInstallPathPropertyName;
                        string gameInstallPath = UserSettings.GetUserSettings().getPath(gameInstallPathPropertyName);
                        // Check that gameInstallPath matches where the game is actually running from
                        // rFactor 2 runs from gameInstallPath\Bin64
                        if (Utilities.IsGameRunning(gameDefinition.processName, gameDefinition.alternativeProcessNames, out var parentDir))
                        {
                            if (Game.RF2_64BIT)
                            {
                                parentDir = Directory.GetParent(parentDir).FullName; // Strip the Bin64
                            }
                            if (!gameInstallPath.Equals(parentDir, StringComparison.InvariantCultureIgnoreCase))
                            {
                                string gameInstallPathPropertyNameText = Configuration.getUIString(gameInstallPathPropertyName);
                                if (String.IsNullOrEmpty(parentDir))
                                {   // EAC anticheat prevents reading path of LMU.exe after V1.40
                                    gameInstallPath = UserSettings.GetUserSettings().getPath(gameInstallPathPropertyName);
                                    Log.Commentary($"Can't get path of {gameDefinition.friendlyName} so using Property '{gameInstallPathPropertyNameText}': '{gameInstallPath}'");
                                }
                                else
                                {
                                    Log.Warning($"{gameDefinition.friendlyName} is running from \n'{parentDir}' but '{gameInstallPathPropertyNameText}' is set to \n'{gameInstallPath}'");
                                    Log.Warning($"Set Property '{gameInstallPathPropertyNameText}' to {parentDir}");
                                    if (DialogResult.Yes == MessageBox.Show(
                                        $"Change Property '{gameInstallPathPropertyNameText}' to '{parentDir}'?",
                                            $"{gameDefinition.friendlyName} install path incorrect",
                                            MessageBoxButtons.YesNo,
                                            MessageBoxIcon.Question))
                                    {
                                        UserSettings.GetUserSettings().setProperty(gameInstallPathPropertyName, parentDir);
                                        UserSettings.GetUserSettings().saveUserSettings();
                                        gameInstallPath = parentDir;
                                    }
                                }
                            }
                        }

                        DLLcheck(gameDefinition);
                        updateRf2LmuConfigFile(gameInstallPath, gameDefinition);
                        break;
                    }
                case GameType.ASSETTO:
                case GameType.ASSETTO_RALLY:
                    {
                        string pythonConfigPath = Path.Combine(myDocuments, @"Assetto Corsa\cfg", @"python.ini");
                        editAssettoCorsaConfigFile(pythonConfigPath);
                        break;
                    }
                case GameType.ACC:
#if EVO_BROADCAST
                case GameType.ASSETTO_EVO:
#endif
                    {
                        updateAccOrAceBroadcastFile(gameDefinition, myDocuments);
                        break;
                    }
                case GameType.DIRT:
                    {
                        UpdateDirtRallyXML(
                            myDocuments + @"\My Games\DiRT Rally\hardwaresettings\hardware_settings_config.xml",
                            UserSettings.GetUserSettings().getInt("dirt_rally_udp_data_port"));
                        break;
                    }
                case GameType.DIRT2:
                    {
                        UpdateDirtRallyXML(
                            myDocuments + @"\My Games\DiRT Rally 2.0\hardwaresettings\hardware_settings_config.xml",
                            UserSettings.GetUserSettings().getInt("dirt_rally_2_udp_data_port"));
                        UpdateDirtRallyXML(
                            myDocuments + @"\My Games\DiRT Rally 2.0\hardwaresettings\hardware_settings_config_vr.xml",
                            UserSettings.GetUserSettings().getInt("dirt_rally_2_udp_data_port"));
                        break;
                    }
                case GameType.GTR2:
                    {
                        // not verified DLLcheck(gameDefinition);
                        break;
                    }
                case GameType.IRACING:
                    {
                        UpdateIracingMaxCars();
                        break;
                    }
                case GameType.RACE_ROOM:
                    {
                        checkR3eDataJson(gameDefinition);
                        break;
                    }
            }
        }

        private void updateRf2LmuConfigFile(string gameInstallPath, GameDefinition gameDefinition)
        {
            string pluginFileName = gameDefinition.gameEnum == GameEnum.RF2_64BIT ? PluginInstaller.rf2PluginFileName : PluginInstaller.lmuPluginFileName;
            try
            {
                const string configFile = "CustomPluginVariables.JSON";
                string configPath = Path.Combine(gameInstallPath, @"UserData\player", configFile);
                Dictionary<string, Dictionary<string, int>> plugins = new Dictionary<string, Dictionary<string, int>>();
                if (!File.Exists(configPath))
                {
                    if (presentCreateMessagebox())
                    {
                        plugins.Add(pluginFileName, new Dictionary<string, int>() { { " Enabled", 1 } });
                        JsonFiles.WriteFile(configPath, plugins);
                        Log.Commentary($"{pluginFileName} {configFile} created");
                    }
                    else
                    {
                        Log.Warning($"User chose not to create {pluginFileName}");
                    }
                }
                if (File.Exists(configPath))
                {
                    Log.Commentary($"Config file '{configPath}' found");
                    plugins = JsonFiles.ReadFile<Dictionary<string, Dictionary<string, int>>>(configPath);
                    Dictionary<string, int> plugin = null;
                    if (plugins != null && plugins.TryGetValue(pluginFileName, out plugin))
                    {
                        Log.Commentary($"{pluginFileName} found in {configFile}");
                        //the whitespace is intended, this is how the game writes it.
                        if (plugin[" Enabled"] != 1)
                        {
                            Log.Warning("\" Enabled\" not 1");
                            if (presentEnableMessagebox())
                            {
                                plugin[" Enabled"] = 1;
                                JsonFiles.WriteFile(configPath, plugins);
                                Log.Commentary("\" Enabled\" set to 1");
                            }
                            else
                            {
                                Log.Warning("User chose not to set \" Enabled\"");
                            }
                        }
                        else
                        {
                            Log.Commentary("\" Enabled\" already set to 1");
                        }
                        if (plugins.TryGetValue(rf2PluginFileNameOld32bit, out plugin))
                        {
                            Log.Warning($"{rf2PluginFileNameOld32bit} found in {configFile}, removed this entry for old 32bit plugin which conflicts with the current 64 bit one");
                            plugins.Remove(rf2PluginFileNameOld32bit);
                            JsonFiles.WriteFile(configPath, plugins);
                        }
                    }
                    else
                    {
                        if (plugins == null)
                        {
                            plugins = new Dictionary<string, Dictionary<string, int>>();
                        }
                        Log.Warning($"{pluginFileName} not found in {configFile}");
                        if (presentEnableMessagebox())
                        {
                            plugins.Add(pluginFileName, new Dictionary<string, int>() { { " Enabled", 1 } });
                            JsonFiles.WriteFile(configPath, plugins);
                            Log.Commentary($"{pluginFileName} added to {configFile} with \" Enabled\" set to 1");
                            // Other apps may be using the old 64bit plugin, so we can't remove it automatically.  Ask the user.
                            // Only do it once when enabling the new plugin.
                            if (plugins.TryGetValue(rf2PluginFileNameOld64bit, out plugin))
                            {
                                if (presentRemoveMessagebox())
                                {
                                    Log.Warning($"{rf2PluginFileNameOld64bit} found in {configFile}, removed the entry for the old 64bit plugin");
                                    plugins.Remove(rf2PluginFileNameOld64bit);
                                    JsonFiles.WriteFile(configPath, plugins);
                                }
                                else
                                {
                                    Log.Warning($"User chose not to remove {rf2PluginFileNameOld64bit} from {configFile}");
                                }
                            }
                        }
                        else
                        {
                            Log.Warning($"User chose not to add {pluginFileName}");
                        }
                    }
                }
                else
                {
                    Log.Error($"{gameDefinition.friendlyName} Config file '{configPath}' not found");
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Failed to enable plugin");
            }
        }

        private void updateAccOrAceBroadcastFile(GameDefinition gameDefinition, string myDocuments)
        {
            string content = "[file not found]";
            Dictionary<GameEnum, string> gameFolderInDocuments = new Dictionary<GameEnum, string>
            {
                {GameEnum.ACC,         "Assetto Corsa Competizione" },
                {GameEnum.ASSETTO_EVO, "ACE" }
            };
            // treading as lightly as possible, use the same encoding that the game is using (unicode LE, no BOM)
            Encoding LEunicodeWithoutBOM = new UnicodeEncoding(false, false);
            bool writeBroadcastFile = true;
            if (!gameFolderInDocuments.TryGetValue(gameDefinition.gameEnum, out var gameFolder))
            {
                Log.Error($"No game folder found for {gameDefinition.friendlyName}");
                return;
            }
            var broadcastPath = Path.Combine(myDocuments,
                gameFolder,
                "Config",
                "broadcasting.json");
            if (File.Exists(broadcastPath))
            {
                try
                {
                    // again, treading as lightly as possible read the file content without locking allowing for the file being locked by the game
                    using (FileStream fileStream = new FileStream(broadcastPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (StreamReader streamReader = new StreamReader(fileStream, LEunicodeWithoutBOM))
                        {
                            content = streamReader.ReadToEnd();
                            if (accBroadcastFileContents.Equals(content))
                            {
                                writeBroadcastFile = false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"Exception reading '{broadcastPath}'");
                }
            }
            else
            {
                Log.Error($"{gameDefinition.friendlyName} Broadcast Path '{broadcastPath}' not found");
            }
            if (writeBroadcastFile)
            {
                try
                {
                    // if the game is running it'll need to be bounced to pick up this change
                    if (Utilities.IsGameRunning(gameDefinition.processName, gameDefinition.alternativeProcessNames, out var parentDir))
                    {
                        MessageBox.Show("broadcasting.json needs to be updated and the game restarted. Please exit the game then click 'OK'");
                    }
                    Console.WriteLine($"Updating {gameDefinition.friendlyName} broadcast file");
                    Console.WriteLine("Expected content:");
                    Console.WriteLine(accBroadcastFileContents);
                    Console.WriteLine("Actual content:");
                    Console.WriteLine(content);
                    // again, write with the same encoding the game uses
                    File.WriteAllText(broadcastPath, accBroadcastFileContents, LEunicodeWithoutBOM);
                }
                catch (Exception e)
                {
                    Log.Exception(e, $"Writing '{broadcastPath}'");
                }
            }
            else
            {
                Log.Commentary($"{gameDefinition.friendlyName} broadcast file '{broadcastPath}' already has expected content");
            }
        }

        private void editAssettoCorsaConfigFile(string pythonConfigPath)
        {
            if (File.Exists(pythonConfigPath))
            {
                string valueActive = Utilities.ReadIniValue("CREWCHIEFEX", "ACTIVE", pythonConfigPath, "0");
                if (!valueActive.Equals("1"))
                {
                    if (presentEnableMessagebox())
                    {
                        Utilities.WriteIniValue("CREWCHIEFEX", "ACTIVE", "1", pythonConfigPath);
                        Log.Commentary($"'{pythonConfigPath}' edited");
                    }
                }
                else
                {
                    Log.Commentary($"'{pythonConfigPath}' already has [CREWCHIEFEX] ACTIVE=1");
                }
            }
            else
            {
                Log.Error($"Python Config Path '{pythonConfigPath}' not found");
            }
        }

        private void UpdateIracingMaxCars()
        {
            if (!iRacing.iRacingSharedMemoryReader.ReadAllCarsEnabled())
            {
                var choice = MessageBox.Show(
                    $"iRacing 'irsdkLogAllCars' is not set correctly for Crew Chief.\n\n" +
                    $"If you click 'OK' Crew Chief will automatically update your iRacing " + 
                    $"{DataFiles.iRacingAppIni} file (the iRacing sim must not be active for this to work).\n\n" +
                    "This warning will continue until 'irsdkLogAllCars' is changed, because Crew Chief " +
                    "will not work properly in really strange ways that you won't be able to explain.",
                    "Crew Chief needs a change to iRacing",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                if (choice == DialogResult.OK)
                {
                    Utilities.WriteIniValue("Misc", "irsdkLogAllCars", "1                \t; Log all cars to setup, or just the original 64 for backwards compatibility (edited by Crew Chief)", DataFiles.iRacingAppIni);
                    Log.Commentary($"'{DataFiles.iRacingAppIni}' updated");
                }
            }
        }

        /// <summary>
        /// Checks whether the r3e_data.json file has changed and updates its associated MD5 hash file if necessary.
        /// Logs a warning if a change is detected.
        /// </summary>
        private void checkR3eDataJson(GameDefinition gameDefinition)
        {
            if (Utilities.IsGameRunning(gameDefinition.processName, gameDefinition.alternativeProcessNames, out var parentDir) &&
                parentDir != null)
            {
                string r3e_data_json_path = Path.GetFullPath(Path.Combine(parentDir, @"..\GameData\General\r3e-data.json"));

                try
                {
                    if (File.Exists(r3e_data_json_path))
                    {
                        var md5 = PluginInstaller.checkMD5(r3e_data_json_path);
                        if (File.Exists(DataFiles.r3e_data_md5))
                        {
                            string oldMd5 = File.ReadAllText(DataFiles.r3e_data_md5);
                            if (md5 != oldMd5)
                            {
                                MessageBox.Show($"{r3e_data_json_path} has changed,\nCrew Chief's carClassData.json may need updating",
                                    $"{gameDefinition.friendlyName} classes changed",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                Log.Warning($"{r3e_data_json_path} changed, carClassData.json may need updating");
                            }
                        }
                        File.WriteAllText(DataFiles.r3e_data_md5, md5);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"{ex.Message} checking {r3e_data_json_path}");
                }
            }
        }

        private void UpdateDirtRallyXML(string fileName, int udpPort)
        {
            if (File.Exists(fileName))
            {
                try
                {
                    bool save = false;
                    XmlDocument doc = new XmlDocument();
                    doc.Load(fileName);
                    XmlNode root = doc.DocumentElement;
                    XmlNode udpNode = root.SelectSingleNode("descendant::udp");
                    if (udpNode == null)
                    {
                        // no UDP node, create it and it's motion_platform parent, with the attributes we need
                        save = true;
                        CreateDirtRallyUDPElement(doc, root, udpPort);
                    }
                    else
                    {
                        // check the attributes and update them if necessary
                        save = UpdateDirtRallyUDPAttributes(udpNode, udpPort);
                    }
                    if (save)
                    {
                        Console.WriteLine("Updating UDP element in " + fileName);
                        if (!File.Exists(fileName + "_backup"))
                        {
                            File.Copy(fileName, fileName + "_backup");
                        }
                        doc.Save(fileName);
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Failed to update settings XML file " + fileName + ", ");
                }
            }
            else
            {
                Log.Warning($"'{fileName} not found");
            }
        }

        private bool UpdateDirtRallyUDPAttributes(XmlNode udpNode, int udpPort)
        {
            bool save = false;
            if (udpNode.Attributes["enabled"] == null || !udpNode.Attributes["enabled"].Value.Equals("true"))
            {
                save = true;
                udpNode.Attributes["enabled"].Value = "true";
            }
            if (udpNode.Attributes["extradata"] == null || !int.TryParse(udpNode.Attributes["extradata"].Value, out var edv) || edv < 3)
            {
                // extradata doesn't exist, or it exists but it's not set to "3" or above
                save = true;
                udpNode.Attributes["extradata"].Value = "3";
            }
            if (udpNode.Attributes["port"] == null || !udpNode.Attributes["port"].Value.Equals(udpPort.ToString()))
            {
                save = true;
                udpNode.Attributes["port"].Value = udpPort.ToString();
            }
            return save;
        }

        private void CreateDirtRallyUDPElement(XmlDocument doc, XmlNode root, int udpPort)
        {
            // try to create it
            XmlNode motionPlatform = root.SelectSingleNode("descendant::motion_platform");
            if (motionPlatform == null)
            {
                motionPlatform = doc.CreateElement("motion_platform");
                root.AppendChild(motionPlatform);
            }
            XmlNode udpNode = doc.CreateElement("udp");
            XmlAttribute enabledAttrib = doc.CreateAttribute("enabled");
            enabledAttrib.Value = "true";
            udpNode.Attributes.Append(enabledAttrib);
            XmlAttribute extradataAttrib = doc.CreateAttribute("extradata");
            extradataAttrib.Value = "3";
            udpNode.Attributes.Append(extradataAttrib);
            XmlAttribute ipAttrib = doc.CreateAttribute("ip");
            ipAttrib.Value = "127.0.0.1";
            udpNode.Attributes.Append(ipAttrib);
            XmlAttribute portAttrib = doc.CreateAttribute("port");
            portAttrib.Value = udpPort.ToString();
            udpNode.Attributes.Append(portAttrib);
            XmlAttribute delayAttrib = doc.CreateAttribute("delay");
            delayAttrib.Value = "1";
            udpNode.Attributes.Append(delayAttrib);
            motionPlatform.AppendChild(udpNode);
        }


        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);
        private static bool DLLcheck(GameDefinition gameDefinition)
        {
            bool result = true;
            string[] LmuRf2Dlls = { "ADVAPI32.dll", "MSVCR120.dll", "KERNEL32.dll" };
            string[] Gtr2Dlls = { "vcruntime140.dll" };

            var dllNames = gameDefinition == GameDefinition.gtr2 ? Gtr2Dlls : LmuRf2Dlls;

            if (UserSettings.GetUserSettings().getBoolean("enable_lmu_rf2_plugin_dll_check"))
            {
                foreach (string dllName in dllNames)
                {
                    // Try to load the DLL
                    IntPtr handle = LoadLibrary(dllName);

                    if (handle != IntPtr.Zero)
                    {
                        Log.Verbose(() => $"{dllName} is installed and loaded successfully.");
                        FreeLibrary(handle); // Free the library if it was successfully loaded
                    }
                    else
                    {
                        Log.Warning($"{dllName} not found, plugin may not work");
                        if (DialogResult.No == MessageBox.Show(
                                Utilities.Strings.NewlinesInLongString(
                                    Configuration.getUIString(
                                        "install_plugin_dll_not_installed_text")) +
                                $" {gameDefinition.friendlyName}",
                                $"{dllName} " + Configuration.getUIString($"install_plugin_dll_not_installed_title"), 
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Stop))
                        {
                            MainWindow.instance.Close();
                        }
                        Log.Warning($"{dllName} not found, plugin may not work, continuing anyway");
                        result = true;
                    }
                }
            }
            return result;
        }
    }
}
