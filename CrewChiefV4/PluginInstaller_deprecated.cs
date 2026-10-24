using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CrewChiefV4
{
    partial class PluginInstaller
    {
        /// <summary>
        /// Some games have plugins that need to be copied to the game somewhere,
        /// some games have config files that may need to be updated,
        /// some games have both, some games have neither.
        /// </summary>
        public void InstallOrUpdatePlugins_deprecated(GameDefinition gameDefinition)
        {
            //gameInstallPath is also used to check if the user already was asked to update
            string gameInstallPath = "";
            string gameInstallPathPropertyName = null;
            string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            switch (gameDefinition.gameEnum)
            {
                case GameEnum.ACC:
                    {
                        string content = "[file not found]";
                        // treading as lightly as possible, use the same encoding that the game is using (unicode LE, no BOM)
                        Encoding LEunicodeWithoutBOM = new UnicodeEncoding(false, false);
                        bool writeBroadcastFile = true;
                        var broadcastPath = Path.Combine(myDocuments,
                                "Assetto Corsa Competizione",
                                "Config",
                                "broadcasting.json");
                        if (File.Exists(broadcastPath))
                        {
                            try
                            {
                                // again, treading as lightly as possible read the file content without locking allowing for the file being locked by the game
                                using (FileStream fileStream = new FileStream(
                                    broadcastPath,
                                    FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.ReadWrite))
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
                                Console.WriteLine("Exception getting broadcast.json: " + ex.Message);
                            }
                        }
                        else
                        {
                            Log.Error($"'{broadcastPath}' not found");
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
                                Console.WriteLine("Updating ACC broadcast file");
                                Console.WriteLine("Expected content:");
                                Console.WriteLine(accBroadcastFileContents);
                                Console.WriteLine("Actual content:");
                                Console.WriteLine(content);
                                // again, write with the same encoding the game uses
                                File.WriteAllText(broadcastPath, accBroadcastFileContents, LEunicodeWithoutBOM);
                            }
                            catch (Exception e) { Log.Exception(e); }
                        }
                        else
                        {
                            Log.Commentary($"ACC broadcast file '{broadcastPath}' already has expected content");
                        }
                        return;
                    }

                case GameEnum.RF2_64BIT:
                    gameInstallPathPropertyName = "rf2_install_path";
                    break;
                case GameEnum.LMU:
                    gameInstallPathPropertyName = "lmu_install_path";
                    break;
                case GameEnum.ASSETTO_32BIT:
                case GameEnum.ASSETTO_64BIT:
                case GameEnum.ASSETTO_128CARS:
                case GameEnum.ASSETTO_64BIT_RALLY:
                case GameEnum.ASSETTO_PRO:
                    gameInstallPathPropertyName = "acs_install_path";
                    break;
                case GameEnum.ASSETTO_EVO:
                    gameInstallPathPropertyName = "ace_install_path";
                    break;
                case GameEnum.RF1:
                    //special case here, will figure something clever out so we dont need to have Dan's dll included in every plugin folder.
                    switch (gameDefinition.lookupName)
                    {
                        case "automobilista":
                            {
                                gameInstallPathPropertyName = "ams_install_path";
                                break;
                            }
                        case "rFactor1":
                            {
                                gameInstallPathPropertyName = "rf1_install_path";
                                break;
                            }
                        case "asr":
                            {
                                gameInstallPathPropertyName = "asr_install_path";
                                break;
                            }
                        default:
                            {
                                // this is an rFactor based game that's not rFactor or AMS (so it's fTruck, Marcas or GSC) - no automatic installation of
                                // plugin for these old games
                                Log.Error("Auto-install of plugin not supported for " + gameDefinition.friendlyName);
                                Log.Error("Assuming that the plugin in install folder" +
                                    " (default location C:\\Program Files(x86)\\Britton IT Ltd\\CrewChiefV4\\plugins\\rFactor\\Plugins) has been copied to your game's install folder");
                                return;
                            }
                    }
                    break;
                case GameEnum.RBR:
                    gameInstallPathPropertyName = "rbr_install_path";
                    break;
                case GameEnum.DIRT:
                    UpdateDirtRallyXML(myDocuments + @"\My Games\DiRT Rally\hardwaresettings\hardware_settings_config.xml",
                            UserSettings.GetUserSettings().getInt("dirt_rally_udp_data_port"));
                    return;
                case GameEnum.DIRT_2:
                    UpdateDirtRallyXML(myDocuments + @"\My Games\DiRT Rally 2.0\hardwaresettings\hardware_settings_config.xml",
                                    UserSettings.GetUserSettings().getInt("dirt_rally_2_udp_data_port"));
                    UpdateDirtRallyXML(myDocuments + @"\My Games\DiRT Rally 2.0\hardwaresettings\hardware_settings_config_vr.xml",
                        UserSettings.GetUserSettings().getInt("dirt_rally_2_udp_data_port"));
                    return;
                case GameEnum.GTR2:
                    gameInstallPathPropertyName = "gtr2_install_path";
                    break;
                case GameEnum.IRACING:
                    UpdateIracingMaxCars();
                    break;
            }

            if (String.IsNullOrEmpty(gameInstallPathPropertyName))
            { // Nothing more required
                return;
            }

            // the game has a plugin
            gameInstallPath = UserSettings.GetUserSettings().getPath(gameInstallPathPropertyName);
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
                            return;
                        }
                    }
                }
            }

            if (Directory.Exists(gameInstallPath))
            { //we have a gameInstallPath so we can go on with installation/updating assuming that the user wants to enable the plugin.
                if (installOrUpdatePlugin(
                        Path.Combine(DataFiles.getDefaultFolderLocation("plugins"),
                            gameDefinition.pluginDirectory),
                        gameInstallPath,
                        gameDefinition.friendlyName))
                { // Set path Property and edit config even if already installed
                    switch (gameDefinition.gameEnum)
                    {
                        case GameEnum.RF2_64BIT:
                        case GameEnum.LMU:
                            {
                                string pluginFileName = gameDefinition.gameEnum == GameEnum.RF2_64BIT ? PluginInstaller.rf2PluginFileName : PluginInstaller.lmuPluginFileName;
                                try
                                {
                                    DLLcheck(gameDefinition);
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
                                        if (plugins.TryGetValue(pluginFileName, out plugin))
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
                                        }
                                        else
                                        {
                                            Log.Warning($"{pluginFileName} not found in {configFile}");
                                            if (presentEnableMessagebox())
                                            {
                                                plugins.Add(pluginFileName, new Dictionary<string, int>() { { " Enabled", 1 } });
                                                JsonFiles.WriteFile(configPath, plugins);
                                                Log.Commentary($"{pluginFileName} added to {configFile} with \" Enabled\" set to 1");
                                            }
                                            else
                                            {
                                                Log.Warning($"User chose not to add {pluginFileName}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Log.Error($"Config file '{configPath}' not found");
                                    }
                                }
                                catch (Exception e)
                                {
                                    Log.Exception(e, "Failed to enable plugin");
                                }

                                break;
                            }

                        case GameEnum.ASSETTO_32BIT:
                        case GameEnum.ASSETTO_64BIT:
                        case GameEnum.ASSETTO_128CARS:
                        case GameEnum.ASSETTO_64BIT_RALLY:
                        case GameEnum.ASSETTO_PRO:
                            {
                                UserSettings.GetUserSettings().setProperty("acs_install_path", gameInstallPath);
                                string pythonConfigPath = Path.Combine(myDocuments, @"Assetto Corsa\cfg", @"python.ini");
                                editAssettoCorsaConfigFile(pythonConfigPath);
                                break;
                            }
                        case GameEnum.ASSETTO_EVO:
                            {
                                UserSettings.GetUserSettings().setProperty("ace_install_path", gameInstallPath);
                                string pythonConfigPath = Path.Combine(myDocuments, @"ACE\cfg", @"python.ini");
                                editAssettoCorsaConfigFile(pythonConfigPath);
                                break;
                            }
                    }
                    if (gameInstallPath != UserSettings.GetUserSettings().getPath(gameInstallPathPropertyName))
                    {
                        UserSettings.GetUserSettings().setProperty(gameInstallPathPropertyName, gameInstallPath);
                        UserSettings.GetUserSettings().saveUserSettings();
                        Log.Commentary($"Updated install path for {gameDefinition.friendlyName} to '{gameInstallPath}'");
                    }
                }
                // else it isn't installed
            }
        }
    }
}
