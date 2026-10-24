using System;
using System.Collections.Generic;
using System.IO;

namespace CrewChiefV4
{
    public static class ControllerNames
    {
        public class ControllerNamesData
        {
            public string name { get; set; }
            public Guid guid { get; set; }
            public Dictionary<int, string> buttons { get; set; }
        }

        private static readonly object controllerNamesLock = new object();
        private static Dictionary<Guid, ControllerNamesData> controllerNames =
            new Dictionary<Guid, ControllerNamesData>();
        private static DateTime controllerNamesFolderLastWrite = DateTime.MinValue;

        public static event Action<Guid, string> ControllerNameChanged;

        public static ControllerNamesData GetControllerNames(Guid guid)
        {
            loadControllerNames();

            ControllerNamesData config;
            return controllerNames.TryGetValue(guid, out config) ? config : null;
        }

        public static string GetControllerDisplayName(Guid guid, string defaultName)
        {
            ControllerNamesData config = GetControllerNames(guid);
            return config != null && !string.IsNullOrWhiteSpace(config.name)
                ? config.name.Trim()
                : defaultName;
        }

        public static string GetControllerButtonNameOrNumber(Guid guid, int buttonIndex)
        {
            ControllerNamesData config = GetControllerNames(guid);
            string buttonName;

            if (config != null
                && config.buttons != null
                && config.buttons.TryGetValue(buttonIndex, out buttonName)
                && !string.IsNullOrWhiteSpace(buttonName))
            {
                return buttonName.Trim();
            }

            return Configuration.getUIString("button") + ": " + (buttonIndex + 1);
        }

        public static string FindControllerNamesFile(Guid guid)
        {
            if (!Directory.Exists(DataFiles.ControllerButtonNamesFolder))
            {
                return null;
            }

            foreach (string filename in Directory.GetFiles(DataFiles.ControllerButtonNamesFolder, "*.json"))
            {
                try
                {
                    ControllerNamesData config = JsonFiles.ReadFile<ControllerNamesData>(filename);
                    if (config != null && config.guid == guid)
                    {
                        return filename;
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error reading controller names from " + filename + ": ");
                }
            }

            return null;
        }

        public static void SaveControllerNames(string filename, ControllerNamesData config)
        {
            JsonFiles.WriteFile(filename, config);

            lock (controllerNamesLock)
            {
                controllerNamesFolderLastWrite = DateTime.MinValue;
            }

            loadControllerNames();

            Action<Guid, string> handler = ControllerNameChanged;
            if (handler != null)
            {
                handler(config.guid, config.name);
            }
        }

        private static void loadControllerNames()
        {
            lock (controllerNamesLock)
            {
                if (!Directory.Exists(DataFiles.ControllerButtonNamesFolder))
                {
                    controllerNames.Clear();
                    controllerNamesFolderLastWrite = DateTime.MinValue;
                    return;
                }

                DateTime lastWrite =
                    Directory.GetLastWriteTimeUtc(DataFiles.ControllerButtonNamesFolder);

                if (lastWrite == controllerNamesFolderLastWrite)
                {
                    return;
                }

                var loadedNames = new Dictionary<Guid, ControllerNamesData>();

                foreach (string filename in
                    Directory.GetFiles(DataFiles.ControllerButtonNamesFolder, "*.json"))
                {
                    try
                    {
                        ControllerNamesData config =
                            JsonFiles.ReadFile<ControllerNamesData>(filename);

                        if (config != null && config.guid != Guid.Empty)
                        {
                            if (config.buttons == null)
                            {
                                config.buttons = new Dictionary<int, string>();
                            }

                            loadedNames[config.guid] = config;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Error reading controller names from " + filename + ": ");
                    }
                }

                controllerNames = loadedNames;
                controllerNamesFolderLastWrite = lastWrite;
            }
        }
    }
}
