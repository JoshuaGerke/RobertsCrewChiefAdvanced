using System;

namespace CrewChiefV4
{
    /// <summary>
    /// All the debug status in one place.
    /// Not all of it is used perhaps...
    /// </summary>
    public class Debugging
    {
        // This pair will generally be the same but in cases where we're checking the behaviour in debug,
        // while pretending we're not in debug, it's useful to have them separate
        /// <summary>
        /// Running under a debugger
        /// (may be false if pretending we're not in debug)
        /// </summary>
        public Boolean RunningUnderDebugger { get; set; } = System.Diagnostics.Debugger.IsAttached;


        /// <summary>
        /// Running under a debugger.
        /// e.g. use My Documents/CrewChiefDebug instead of My Documents/CrewChiefV4
        /// </summary>
        public Boolean UseDebugFilePaths { get; set; } = System.Diagnostics.Debugger.IsAttached;

        /// <summary>
        /// Cmd line -debug :
        /// </summary>
        public Boolean DebugCommandLineOption { get; set; } = CrewChief.CommandLine.Get("debug") != null;
        /// <summary>
        /// Cmd line -profile_mode : Allow trace playback on Release build.
        /// </summary>
        public Boolean ProfileMode { get; set; } = CrewChief.CommandLine.Get("profile_mode") != null;
        /// <summary>
        /// Cmd line -debug_with_playback : Dump-to-file and playback controls enabled
        /// </summary>
        public Boolean DebugWithPlaybackMode { get; set; } = CrewChief.CommandLine.Get("debug_with_playback") != null;

        /// <summary>
        /// Spotter messages during trace playback (not doing anything much yet)
        /// </summary>
        public Boolean PlaybackSpotter {
            get
            {
                return !CrewChief.loadDataFromFile;
            }
        } 

        /// <summary>
        /// User chose to save speech debug data
        /// </summary>
        public Boolean SaveSREDebugData { get; set; }

        /// <summary>
        /// User chose to record a trace log.
        /// </summary>
        public Boolean UserTraceLogging { get; set; } = false;

        /// <summary>
        /// Show the borders on the TableLayout controls
        /// </summary>
        public Boolean ShowMainLayout { get; set; } = false;

        /// <summary>
        /// Log.Debug messages are logged.
        /// </summary>
        public Boolean LoggingDebug { get; set; } = true; // will be set by Logging anyway
        
        /// <summary>
        /// Program was built for debugging (vs Release)
        /// </summary>
        public const Boolean DebugBuild =
#if DEBUG
            true;
#else
            false;
#endif
        /// <summary>
        /// Can't have a constructor as statics get in first so call this explicitly
        /// Unit tests don't to avoid GetUserSettings() as it has dependencies
        /// </summary>
        public void Constructor()
        {
            SaveSREDebugData = UserSettings.GetUserSettings().getBoolean("save_sre_debug_data");
        }
        /// <summary>
        /// Breakpoint this function to catch exceptions before the call stack collapses
        /// and details are lost<br/>
        /// Usage: catch (Exception e) when (CrewChief.Debug.LogException(e))
        /// </summary>
        /// <returns>true so catch is executed</returns>
        public bool LogException(Exception e)
        {
            Tracepoints.Debugging.LogException(e);
            return true;
        }
    }

    public partial class Tracepoints
    {
        private static readonly TraceWindow traceWindow;
        internal static TraceWindow _testWindowHandle; // Used by unit tests
        static Tracepoints()
        {
            if (CrewChief.Debug.RunningUnderDebugger)
            {
                traceWindow = new TraceWindow();
            }
            _testWindowHandle = traceWindow;
        }

        /// <summary>
        /// If the node is not checked don't act on its tracepoint.
        /// </summary>
        /// <returns></returns>
        public static bool TracepointIsChecked(string nodePath)
        {
            return false; //traceWindow != null && traceWindow.IsNodeChecked(nodePath);
        }

        public class Debugging
        {
            public static void LogException(Exception e)
            {
                if (TracepointIsChecked("Tracepoints/Debugging/LogException"))
                {
                    Log.Debug(e.Message);
                }
            }
        }
    }
}