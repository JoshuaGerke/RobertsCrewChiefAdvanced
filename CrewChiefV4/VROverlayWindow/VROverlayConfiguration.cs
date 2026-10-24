using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CrewChiefV4.VirtualReality
{
    /// <summary>
    /// Configuration for the VR Settings Form
    /// </summary>
    public class VROverlayConfiguration
    {
        public string HighlightColor { get; set; } = "#FFFF00"; //yellow

        public List<VROverlaySettings.HotKeyMapping> HotKeys { get; set; } = new List<VROverlaySettings.HotKeyMapping>();
        public List<VROverlayWindow> Windows { get; set; } = new List<VROverlayWindow>();

        /// <summary>
        /// File location where this instance was loaded
        /// </summary>
        [JsonIgnore]
        private FileInfo FileInfo { get; set; }

        static readonly FileInfo OldSettingsFile;
        static readonly FileInfo VrSettingsFile;

        static VROverlayConfiguration()
        {
            OldSettingsFile = new FileInfo(DataFiles.vr_overlay_windows_OLD);
            VrSettingsFile = new FileInfo(DataFiles.VRconfig);
        }        

        private void Save(FileInfo file)
        {
            file.Directory.TryCreate(Console.WriteLine);
            
            try
            {
                JsonFiles.WriteFile(file.FullName, this);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error saving VR config file {file.FullName}: {e.Message}");
            }
        }

        /// <summary>
        /// Save the settings file to the Default Location
        /// </summary>
        public void Save()
        {
            Save(FileInfo);
        }

        /// <summary>
        /// Load the settings file from the default location
        /// </summary>
        /// <returns></returns>
        public static VROverlayConfiguration FromFile()
        {
            return FromFile(VrSettingsFile);
        }


        /// <summary>
        /// Helper function to handle migration from old file type
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <returns></returns>
        public static VROverlayConfiguration FromFile(FileInfo fileInfo)
        {
            if (OldSettingsFile.Exists)
            {
                // migrate the old setting file to the new format
                var oldWindows = OldSettingsFile.TryReadFile<List<VROverlayWindow>>(Console.WriteLine);
                if (oldWindows == null)
                {
                    oldWindows = new List<VROverlayWindow>();
                }
                new VROverlayConfiguration
                {
                    Windows = oldWindows,
                    HotKeys = VROverlaySettings.HotKeyMapping.Default().ToList()
                }
                .Save(fileInfo);
                OldSettingsFile.Delete();
            }
            if (!fileInfo.Exists || fileInfo.TryReadFile<VROverlayConfiguration>() == null)
            {
                new VROverlayConfiguration
                {
                    Windows = new List<VROverlayWindow>(),
                    HotKeys = VROverlaySettings.HotKeyMapping.Default().ToList()
                }
                .Save(fileInfo);
            }

            var config = fileInfo.TryReadFile<VROverlayConfiguration>(Console.WriteLine);
            // note that this can still be null here if the Save call above fails - the console should have some errors so it should be obvious something's wrong
            if (config != null)
            {
                if (config.HotKeys == null)
                {
                    config.HotKeys = new List<VROverlaySettings.HotKeyMapping>();
                }
                if (config.Windows == null)
                {
                    config.Windows = new List<VROverlayWindow>();
                }
                // remove any hotkeys with an unrecognized action
                foreach (VROverlaySettings.HotKeyMapping hotKeyMapping in config.HotKeys.Where(m => !m.IsValid()).ToList())
                {
                    Console.WriteLine($"Unknown hotkey action '{hotKeyMapping.Id}', Hotkey will be ignored");
                    config.HotKeys.Remove(hotKeyMapping);
                }

                config.FileInfo = fileInfo;
            }
            return config;
        }
    }
}
