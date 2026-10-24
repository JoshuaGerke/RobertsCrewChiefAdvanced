using CrewChiefV4;

using FluentAssertions;

using NUnit.Framework;

using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TestDataFiles
    {
        [OneTimeSetUp]
        public void ClassInitialize()
        {
            var result = DataFiles.MakeFolders();
            result.Should().BeNull("because otherwise the folders weren't made");

        }
        [Test]
        public void TestDebugLogsFolder()
        {
            CrewChief.Debug.UseDebugFilePaths = true; // too late to affect the statics
            bool logFound = false;
            foreach (var filename in Directory.GetFiles(DataFiles.DebugLogsFolder))
            {
                if (filename.ToLower().Contains("console"))
                {
                    logFound = true;
                    break;
                }
            }
            Assert.IsTrue(logFound);
        }
        [Test]
        public void TestSoundFilesFolder()
        {
            bool voiceFound = false;
            foreach (var filename in Directory.GetDirectories(DataFiles.SoundFilesFolder))
            {
                if (filename.ToLower().Contains("voice"))
                {
                    voiceFound = true;
                    break;
                }
            }
            Assert.IsTrue(voiceFound);
        }
        [Test]
        public void TestProfilesFolder()
        {
            bool ControllerDataFound = false;
            foreach (var filename in Directory.GetDirectories(DataFiles.ProfilesFolder))
            {
                if (filename.ToLower().Contains("controllerdata"))
                {
                    ControllerDataFound = true;
                    break;
                }
            }
            Assert.IsTrue(ControllerDataFound);
        }
        [Test]
        public void TestDefaultFolder()
        {
            DataFiles.default_carClassData.Should().Contain("CrewChiefV4", AtLeast.Once());
        }
        [Test]
        // TDD test of the method of getting the folder path
        public void TestDebugPath()
        {
            Type t = typeof(TestDataFiles);
            Assembly assemFromType = t.Assembly;
            var path = Path.GetDirectoryName(assemFromType.Location);
            path.Should().Contain("UnitTest", Exactly.Once());
            Console.WriteLine(path);
        }

        [Test]
        public void ListFolders()
        {
            var folders = DataFiles.ListFolders();
            foreach (var folder in folders)
            {
                Console.WriteLine(folder);
            }
        }
        [Test]
        // Code snippets copied from the diff, result of old tested against the new
        // This can be deleted once the MR is merged
        public void ABtests()
        {
            if (CrewChief.Debug.UseDebugFilePaths)
            {
                Console.WriteLine("AB tests don't work in debug mode as MyDocuments has CrewChiefDebug appended");
                return;
            }
            String defaultSoundFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\CrewChiefV4\Sounds";
            defaultSoundFilesPath.Should().Be(DataFiles.SoundFilesFolder);
            string path = System.IO.Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "sounds-variety-data.txt");
            path.Should().Be(DataFiles.sounds_variety_data); 

            var overridePath = System.IO.Path.Combine(Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "terminologies.json");
            overridePath.Should().Be(DataFiles.terminologies);

            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrewChiefV4");
            path.Should().Be(DataFiles.BaseFolder);

            String userConfig = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "mqtt_telemetry.json");
            String defaultConfig = getDefaultFileLocation("mqtt_telemetry.json");
            userConfig = Path.GetFullPath(userConfig);
            userConfig.Should().Be(DataFiles.mqtt_telemetry);
            defaultConfig = Path.GetFullPath(defaultConfig);
            defaultConfig.Should().Be(DataFiles.default_mqtt_telemetry);

            String fullPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "pit_benchmarks.json");
            fullPath.Should().Be(DataFiles.pit_benchmarks);

            String userFilePath = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "chart_subscriptions.json");

            String defaultFilePath = getDefaultFileLocation("chart_subscriptions.json");
            userFilePath.Should().Be(DataFiles.chart_subscriptions);
            defaultFilePath = Path.GetFullPath(defaultFilePath);
            defaultFilePath.Should().Be(DataFiles.default_chart_subscriptions);

            string overlayFileName = "overlayFileName";
            path = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), "CrewChiefV4", overlayFileName);
            path.Should().Be(DataFiles.Overlay(overlayFileName));

            path = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "RF2");
            path.Should().Be(DataFiles.RF2Folder);

            /*public static*/ string splashImageFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\CrewChiefV4\";
            /*public static*/ string tempSplashImagePath = splashImageFolderPath + "splash_image_tmp.png";
            /*public static*/ string splashImagePath = splashImageFolderPath + "splash_image.png";
            splashImageFolderPath = Path.GetFullPath(splashImageFolderPath);
            tempSplashImagePath = Path.GetFullPath(tempSplashImagePath);
            splashImagePath = Path.GetFullPath(splashImagePath);
            splashImageFolderPath.Should().Be(DataFiles.LocalApplicationDataFolder + @"\");
            tempSplashImagePath.Should().Be(splashImageFolderPath + @"splash_image_tmp.png");
            splashImagePath.Should().Be(splashImageFolderPath + @"splash_image.png");

            string soundPackLocation = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\CrewChiefV4\Sounds\Voice";
            soundPackLocation.Should().Be(DataFiles.VoiceFilesFolder);

            path = logFilePath();
            path.Should().Be(DataFiles.UserLogsFolder);

            var cdPath = System.IO.Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.MyDocuments), @"CrewChiefV4\Profiles\ControllerData");
            cdPath.Should().Be(DataFiles.ControllerDataFolder);


            var MyDocuments = new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrewChiefV4"));
            var OldSettingsFile = new FileInfo(Path.Combine(MyDocuments.FullName, "vr_overlay_windows.json"));
            var DefaultFile = /*new FileInfo(*/Path.Combine(MyDocuments.FullName, "CrewChiefV4.vrconfig.json");
            DefaultFile.Should().Be(DataFiles.VRconfig);

            String fileName = "saved_command_macros.json";
            path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrewChiefV4");
            path = Path.Combine(path, fileName);
            path.Should().Be(DataFiles.saved_command_macros);

            bool forceDefault = false;
            path = Path.GetFullPath(getMacrosFileLocation(forceDefault));
            //RC path.Should().Be(DataFiles.GetFileLocation(DataFiles.saved_command_macros,
            //    DataFiles.default_saved_command_macros,
            //    "command macros", forceDefault));
            forceDefault = true;
            path = Path.GetFullPath(getMacrosFileLocation(forceDefault));
            path.Should().Be(DataFiles.GetFileLocation(DataFiles.saved_command_macros,
                DataFiles.default_saved_command_macros,
                "command macros", forceDefault));

            path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "iRacing", "telemetry");
            path.Should().Be(DataFiles.iRacingTelemetryFolder);

            string dataFilesPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "iRacing", "app.ini");
            dataFilesPath.Should().Be(DataFiles.iRacingAppIni);

            path = getDefaultFileLocation("carClassData.json");
            path = Path.GetFullPath(path);
            path.Should().Be(DataFiles.default_carClassData);

            path = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "carClassData.json");
            path.Should().Be(DataFiles.carClassData);

            string configFileName = "configFileName";
            path = getDefaultFileLocation(configFileName);
            path = Path.GetFullPath(path);
            path.Should().Be(DataFiles.getDefaultFileLocation(configFileName));

            path = getUserOverridesFileLocation(configFileName);
            path = Path.GetFullPath(path);
            //RC path.Should().Be(DataFiles.getUserOverridesFileLocation(configFileName));

            path = getDefaultFileLocation("controllerConfigurationData.json");
            path = Path.GetFullPath(path);
            path.Should().Be(DataFiles.default_controllerConfigurationData);

            var currProfileName = UserSettings.GetUserSettings().getString("current_settings_profile");
            path = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), @"CrewChiefV4\Profiles\ControllerData", currProfileName);
            path = Path.GetFullPath(path);
            path.Should().Be(DataFiles.CurrentProfile());

            path = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "unvocalized_driver_names.txt");
            path.Should().Be(DataFiles.unvocalized_driver_names);

            path = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "Pace_notes");
            path.Should().Be(DataFiles.PaceNotesFolder);


            if (CrewChief.Debug.UseDebugFilePaths)
            {
                dataFilesPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), @"..\", @"..\DataFiles\");
            }
            else
            {
                dataFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "DebugLogs");
            }
            dataFilesPath = Path.GetFullPath(dataFilesPath);
            dataFilesPath.Should().Be(DataFiles.DebugLogsFolder);

            string debugDataPath = Path.Combine(Environment.GetFolderPath(
                             Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "VoiceRecognitionDebug");
            debugDataPath = Path.GetFullPath(debugDataPath);
            debugDataPath.Should().Be(DataFiles.voiceRecognitionDebugFolder);

            path = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "Track_landmarks");
            path.Should().Be(DataFiles.track_landmarksFolder);

            String userLandmarks = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "Track_landmarks");
            userLandmarks.Should().Be(DataFiles.track_landmarksFolder);
        }

        #region OldMethods
        // Copy of old method in MainWindow.cs
        private string logFilePath()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "DebugLogs");
        }

        // Copy of old method in MacroManager.cs
        public static String getMacrosFileLocation(bool forceDefault = false)
        {
            String path = System.IO.Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments), "CrewChiefV4", "saved_command_macros.json");

            if (File.Exists(path) && !forceDefault) // forceDefault can/should only be true when called from the macro editor
            {
                Console.WriteLine("Loading user-configured command macros from Documents/CrewChiefV4/ folder");
                return path;
            }
            // make sure we save a copy to the user config directory
            // no need to worry about forceDefault as content of the file will be same.
            else if (!File.Exists(path))
            {
                try
                {
                    File.Copy(getDefaultFileLocation("saved_command_macros.json"), path);
                    Console.WriteLine("Loading user-configured command macros from Documents/CrewChiefV4/ folder");
                    return path;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error copying default macro configuration file to user dir : " + e.Message);
                    Console.WriteLine("Loading default command macros from installation folder");
                    return getDefaultFileLocation("saved_command_macros.json");
                }
            }
            else
            {
                Console.WriteLine("Loading default command macros from installation folder");
                return getDefaultFileLocation("saved_command_macros.json");
            }
        }

        // Copies of old methods in Configuration.cs
        public static String getDefaultFileLocation(String filename)
        {
            return getDefaultLocation(filename, true);
        }

        public static String getDefaultFolderLocation(String folderName)
        {
            return getDefaultLocation(folderName, false);
        }

        private static String getDefaultLocation(String name, Boolean isFile)
        {
            String regularPath = Application.StartupPath + @"\" + name;
            String debugPath = Application.StartupPath + @"\..\..\" + name;
            // Unit tests path:
            String utPath = Directory.GetCurrentDirectory() + @"\..\..\..\CrewChiefV4\" + name;
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

        public static String getUserOverridesFileLocation(String filename)
        {
            return Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\CrewChiefV4\" + filename);
        }
        #endregion OldMethods
    }
}
