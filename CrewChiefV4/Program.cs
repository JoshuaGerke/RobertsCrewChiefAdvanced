//#define EXCEPTION_TEST
using CrewChiefV4.Audio;
using CrewChiefV4.UserInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using CrewChiefV4;

namespace UnitTest
{
    /// <summary>
    /// Contains various items required by unit test
    /// Partial class so units under test can add their own content
    /// </summary>
    public static partial class UnitTest
    {
        /// <summary>
        /// Running unit tests
        /// </summary>
        public static bool Active { get; set; } = false;

        /// <summary>
        /// Debugging unit tests rather than running them (used to avoid tests that require user input)
        /// </summary>
        public static bool Debugging { get; set; } = true;

        public static void CrewChiefInitialise()
        {
            CrewChief.Debug = new Debugging();
        }
    }

}
namespace CrewChiefV4
{
    static class Program
    {
        private static Dictionary<String, IntPtr> processorAffinities = new Dictionary<String, IntPtr> {
            { "cpu1", new IntPtr(0x0001) },
            { "cpu2", new IntPtr(0x0002) },
            { "cpu3", new IntPtr(0x0004) },
            { "cpu4", new IntPtr(0x0008) },
            { "cpu5", new IntPtr(0x0010) },
            { "cpu6", new IntPtr(0x0020) },
            { "cpu7", new IntPtr(0x0040) },
            { "cpu8", new IntPtr(0x0080) }
        };
        public static Loading LoadingScreen;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);
            Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(ThreadExceptionHandler);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            BootTrace("0");
            CrewChief.Debug = new Debugging();
            CrewChief.Debug.Constructor();

            // Set Invariant Culture for all threads as default.
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            // Set Invariant Culture for current thead.
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            foreach (var affinity in processorAffinities)
            {
                if (CrewChief.CommandLine.Get(affinity.Key) != null)
                {
                    try
                    {
                        var process = System.Diagnostics.Process.GetCurrentProcess();
                        // Set Core
                        process.ProcessorAffinity = affinity.Value;
                        Console.WriteLine("Set process core affinity to " + affinity.Key);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to set process affinity");
                    }
                }
            }
            BootTrace("1");
            MainWindow.soundTestMode = CrewChief.CommandLine.Get("sound_test") != null;
            MainWindow.disableControllerReacquire = CrewChief.CommandLine.Get("nodevicescan") != null;

            // Internal.
            Boolean allowMultipleInst = CrewChief.CommandLine.Get("multi") != null;
            
            if (!allowMultipleInst)
            {
                String commandPassed = CrewChief.CommandLine.GetCommandArg();
                if (!string.IsNullOrEmpty(commandPassed))
                {
                    if (CommandManager.ProcesssCommand(commandPassed))
                        return;  // This is execution to perform command, exit.
                }
                try
                {
                    var processes = System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location));
                    if (processes.Count() > 1)
                    {
                        var result = MessageBox.Show("Retry to close the other one\nCancel to close this one",
                            "Crew Chief is already running",
                            MessageBoxButtons.RetryCancel);
                        if (result == DialogResult.Cancel)
                        {
                            System.Diagnostics.Process.GetCurrentProcess().Kill();
                        }
                        else // Maybe overkill but may let users kill a stuck process
                        {
                            var startTime = System.Diagnostics.Process.GetCurrentProcess().StartTime;
                            foreach (var process in processes)
                            {
                                if (process.StartTime != startTime)
                                {
                                    process.Kill();
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // Shouldn't happen but belt and braces...
                    BootTrace("!");
                    System.Environment.Exit(1);
                }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool showSplashScreen = false;
            try
            {
                showSplashScreen = UserSettings.GetUserSettings().getBoolean("show_splash_screen");
                BootTrace("2");
            }
            catch (Exception)
            {
                // ignore, if we've been unable to load the settings the UserSettings instance should have the 'broken' flag set at this point
            }
#if EXCEPTION_TEST
            Utilities.ReportException(new Exception("Exception test"), "Testing the Exception handler", true);
#endif
            if (showSplashScreen)
            {
                LoadSplashImage();
                LoadingScreen = new Loading();
                // Display form modelessly
                LoadingScreen.StartPosition = FormStartPosition.CenterScreen;
                LoadingScreen.FormBorderStyle = FormBorderStyle.None;
                LoadingScreen.Show();
            }

#if !DEBUG
            try
            {
                SharpDX.Configuration.EnableObjectTracking = true;
                SharpDX.ComObject.LogMemoryLeakWarning = msg => Console.Write(msg);

#endif
#if !PIT_MANAGER_DEBUG
                BootTrace("3");
                MainWindow mw = new MainWindow();
                BootTrace("4");
                Application.Run(mw);
#else
                PitMenuDebug_V pmd = new PitMenuDebug_V();
                LoadingScreen.Hide();
                Application.Run(pmd);
#endif
#if !DEBUG
            }
            catch (System.ObjectDisposedException ex) 
            {
                // 'Cannot access a disposed object' after doRestart() has closed CC down
                // This shouldn't happen
                Log.Exception(ex, "Internal error after Crew Chief has shut itself down to restart");
            }
            catch (Exception ex)
            {
                // This definitely shouldn't happen
                Log.Exception(ex, "Somewhere in the app");
            }
#endif

            var watch = System.Diagnostics.Stopwatch.StartNew();
            ThreadManager.WaitForRootThreadsShutdown();
            watch.Stop();

            Debug.WriteLine("Root threads took: " + watch.ElapsedTicks * 1000 / System.Diagnostics.Stopwatch.Frequency + "ms to shutdown");

            watch = System.Diagnostics.Stopwatch.StartNew();
            ThreadManager.WaitForTemporaryThreadsShutdown();
            watch.Stop();
            Debug.WriteLine("Temporary threads took: " + watch.ElapsedTicks * 1000 / System.Diagnostics.Stopwatch.Frequency + "ms to shutdown");

            watch = System.Diagnostics.Stopwatch.StartNew();
            ThreadManager.WaitForResourceThreadsShutdown();
            watch.Stop();
            Debug.WriteLine("Resource threads took: " + watch.ElapsedTicks * 1000 / System.Diagnostics.Stopwatch.Frequency + "ms to shutdown");

            watch = System.Diagnostics.Stopwatch.StartNew();
            GlobalResources.Dispose();
            watch.Stop();
            Debug.WriteLine("Resource Disposal took: " + watch.ElapsedTicks * 1000 / System.Diagnostics.Stopwatch.Frequency + "ms");

            if (AudioPlayer.playWithNAudio)
                Debug.Assert(SoundCache.activeSoundPlayerObjects == 0);
        }

        // get the latest splash image and prepare it to be used on the next run (not this one)
        private static void LoadSplashImage()
        {
            BootTrace("LoadSplashImage 0");
            // download the latest splash image in a thread
            // if our working file exists, move it to be our actual splash image
            try
            {
                if (!Directory.Exists(Loading.splashImageFolderPath))
                {
                    BootTrace("LoadSplashImage 1");
                    Directory.CreateDirectory(Loading.splashImageFolderPath);
                }
                if (File.Exists(Loading.tempSplashImagePath))
                {
                    BootTrace("LoadSplashImage 2");
                    if (File.Exists(Loading.splashImagePath))
                    {
                        BootTrace("LoadSplashImage 3");
                        File.Delete(Loading.splashImagePath);
                    }
                    File.Move(Loading.tempSplashImagePath, Loading.splashImagePath);
                }
            }
            catch (Exception)
            {
                // can't move it but it exists, so nuke it
                BootTrace("LoadSplashImage !");
                try
                {
                    File.Delete(Loading.tempSplashImagePath);
                }
                catch (Exception e)
                {
                    BootTrace("LoadSplashImage !!");
                    Log.Exception(e);
                }
            }
            // refresh the image if we don't have one, and occasionally refresh anyway
            if (!File.Exists(Loading.splashImagePath) || new Random().NextDouble() > 0.9)
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    using (var client = new System.Net.WebClient())
                    {
                        try
                        {
                            BootTrace("LoadSplashImage 4");
                            client.DownloadFile(@"http://167.235.144.28/CrewChief_splash_image.png", Loading.tempSplashImagePath);
                        }
                        catch (Exception)
                        {
                            BootTrace("LoadSplashImage 4!");
                            // ignore - no splash screen, doesn't matter
                        }
                    }
                }).Start();
            }
        }

        /// <summary>
        /// If CC is run from the command line:
        /// CrewChiefV4.exe > output.txt 2>&1
        /// then output.txt contains these messages
        /// </summary>
        public static void BootTrace(string traceMessage)
        {
            Console.WriteLine("Boot trace " + traceMessage);
        }
        static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Console.WriteLine($"Unhandled exception: {ex.Message}");
            Utilities.ReportException(ex, ex.Message, true);
        }
        static void ThreadExceptionHandler(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Utilities.ReportException(e.Exception, e.Exception.Message, true);
        }
    }
}
