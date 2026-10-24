using CrewChiefV4.Events;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
namespace CrewChiefV4.Overlay
{
    public class ColorScheme
    {
        //[JsonConstructor]
        public ColorScheme(string name, Color backgroundColor, Color fontColor)
        {
            this.name = name;
            this.backgroundColor = backgroundColor;
            this.fontColor = fontColor;
        }
        public string name;
        public Color backgroundColor;
        public Color fontColor;
    }

    [Flags]
    public enum TextAlign : uint { Left = 0x00000001, Right = 0x00000002, Top = 0x00000004, Bottom = 0x00000008, Center = 0x00000010, CenterRect = 0x00000020, }

    public class OverlaySettings
    {
        public static Color mousePissYellow = new Color(204, 182, 97, 200);
        public static Color runningBottomBrown = new Color(18, 10, 0, 200);
        
        [JsonIgnore]
        public int windowHeight = 10;
        public int windowX = 0;
        public int windowY = 20;
        public int windowFPS = 30;
        public bool vSync = false;
        public int fontSize = 12;
        public string fontName = "Microsoft Sans Serif"; // same as UI
        public bool fontBold = false;
        public bool fontItalic = false;
        public bool textAntiAliasing = true;
        public string activeColorScheme = "CrewChief";
        public List<ColorScheme> colorSchemes;

        [JsonIgnore]
        public static ColorScheme defaultCrewChiefColorScheme = new ColorScheme("CrewChief", runningBottomBrown, new Color(204, 182, 97));
        [JsonIgnore]
        public static ColorScheme windowsGrayColorScheme = new ColorScheme("WindowsGray", Color.FromARGB(System.Drawing.Color.FromArgb(200, System.Drawing.Color.LightGray).ToArgb()), new Color(0, 0, 0));
        [JsonIgnore]
        public static ColorScheme transparentColorScheme = new ColorScheme("Transparent", Color.Transparent, OverlaySettings.mousePissYellow);

        public static T loadOverlaySetttings<T>(string overlayFileName) where T : class
        {
            String path = DataFiles.Overlay(overlayFileName);
            if (!File.Exists(path))
            {
                saveOverlaySetttings(overlayFileName, (T)Activator.CreateInstance(typeof(T)));
            }
            if (path != null)
            {
                try
                {
                    using (StreamReader r = new StreamReader(path))
                    {
                        string json = r.ReadToEnd();
                        T data = JsonConvert.DeserializeObject<T>(json);
                        if (data != null)
                        {
                            return data;
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error pasing " + path + ": ");
                }
            }
            return (T)Activator.CreateInstance(typeof(T));
        }
        public static void saveOverlaySetttings<T>(string overlayFileName, T settings)
        {
            if (overlayFileName != null && DataFiles.MakeFolders() == null)
            {
                String path = DataFiles.Overlay(overlayFileName);
                JsonFiles.WriteFile(path, settings);
            }
        }

    }
}

