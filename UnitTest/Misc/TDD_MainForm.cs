using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using CrewChiefV4;
using CrewChiefV4.UserInterface;
using NUnit.Framework;

namespace UnitTest.Misc.WaitsForMainWindowToClose
{
    [TestFixture]
    public partial class TDD_MainForm : Form
    {
        // These tests only create forms if UnitTest.Debugging == true, i.e. if debugging the test
        // If the test is RUN it just exits.
        [Test]
        public void TDD_TraceWindow()
        {
            InitializeComponent();
            CrewChief.Debug.UseDebugFilePaths = true;
            var xx = DataFiles.DebugLogsFolder;
            var mainWindow = new PlaybackTraceWindow("trace file name");
            CrewChief.gameDefinition.commandLineName = "RF2_64BIT";
            CrewChief.gameDefinition.friendlyName = "rFactor 2";
            if (UnitTest.Debugging)
            {
                mainWindow.ShowDialog();
            }
        }

        [Test]
        public void TDD_MainWindowMenu()
        {
            InitializeComponent();
            CrewChief.Debug.UseDebugFilePaths = true;
            var xx = DataFiles.DebugLogsFolder;
            var mainWindow = new MainWindowClone();
            mainWindow.Width = 800;
            Font exemplarFont = new Font(FontFamily.GenericSansSerif, 8);
            mainWindow.menuStrip1 = new MainMenuStrip(mainWindow, exemplarFont); // Add the menu strip to the main window
            mainWindow.Controls.Add(mainWindow.menuStrip1);
            mainWindow.menuStrip1.ResumeLayout(false);
            mainWindow.menuStrip1.PerformLayout();
            mainWindow.menuStrip1.Width = Width;

            CrewChief.gameDefinition.commandLineName = "RF2_64BIT";
            CrewChief.gameDefinition.friendlyName = "rFactor 2";
            CrewChief.Debug.UserTraceLogging = true;

            if (UnitTest.Debugging)
            {
                var thread = new Thread(() =>
                {
                    Application.Run(mainWindow);
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();

                // Wait for the window to initialize
                while (mainWindow == null || !mainWindow.IsHandleCreated)
                {
                    Thread.Sleep(100);
                }

                // Ensure the window is visible
                mainWindow.Invoke(new Action(() =>
                {
                    mainWindow.Show();
                }));
                Assert.IsNotNull(mainWindow);
                Assert.IsTrue(mainWindow.Visible);

                // Keep the window open until it is manually closed
                thread.Join();
            }
            else
            {
                Log.Error("Test run not debugged");
            }
        }
        [Test]
        public void TDD_ShowOvalConfirmationForm()
        {
            InitializeComponent();
            CrewChief.Debug.UseDebugFilePaths = true;
            var xx = DataFiles.DebugLogsFolder;
            var mainWindow = new MainWindowClone();
            mainWindow.Width = 800;

            CrewChief.gameDefinition.commandLineName = "RF2_64BIT";
            CrewChief.gameDefinition.friendlyName = "rFactor 2";
            CrewChief.Debug.UserTraceLogging = true;

            if (UnitTest.Debugging)
            {
                var thread = new Thread(() =>
                {
                    Application.Run(mainWindow);
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();

                // Wait for the window to initialize
                while (mainWindow == null || !mainWindow.IsHandleCreated)
                {
                    Thread.Sleep(100);
                }

                // Ensure the window is visible
                mainWindow.Invoke(new Action(() =>
                {
                    mainWindow.Show();
                }));

                // Now the test:
                mainWindow.BeginInvoke(new Action(() =>
                {
                    TrackLandmarksData.ShowOvalConfirmationForm("Circuit, not oval", false);
                }));

                mainWindow.BeginInvoke(new Action(() =>
                {
                    TrackLandmarksData.ShowOvalConfirmationForm("Oval", true);
                }));

                Assert.IsNotNull(mainWindow);
                Assert.IsTrue(mainWindow.Visible);

                // Keep the window open until it is manually closed
                thread.Join();
            }
            else
            {
                Log.Error("Test run not debugged");
            }
        }
    }
    public class MainWindowClone : Form, IMainWindow
    {
        public RichTextBox ConsoleTextBox { get; } = new RichTextBox();
        public CheckBox RecordSession { get; set; } = new CheckBox();
        public TextBox FilenameTextbox { get; set; } = new TextBox();
        public MenuStrip menuStrip1;
        public void ClearConsole()
        {
        }
        public void CloseCC()
        {
            Close();
        }
        public Boolean DoRestart(String warningMessage, String warningTitle, Boolean removeSkipUpdates = false, Boolean mandatory = false, Boolean saveUserSettings = false)
        {
            return true;
        }
        public void OpponentNames()
        {
            // broken   var win = new OpponentNames_V(this);
            // win.ShowDialog(this);
        }
        public string PrefixLogfile(string path = null)
        {
            return "This is the log prefix";
        }
        public string SaveConsoleOutputText(bool saveAs)
        {
            return null;
        }
        public void ShowHelp()
        {
            var form = new HelpWindow(this, "index");
            form.ShowDialog(this);
        }
        public void SpeechWizard()
        {
            MainWindow.speechWizard_V = new SpeechWizard_V(true);
            MainWindow.speechWizard_V.ShowDialog(this);
        }
        public void LogNowSend()
        {
        }
        public void LogSavedSend()
        {
        }
        public void TracePlayback()
        {
            var traceWindow = new PlaybackTraceWindow("trace file name");
            traceWindow.Show();
        }
        public void TraceNowSend()
        {
            throw new NotImplementedException();
            var traceWindow = new PlaybackTraceWindow("trace file name");
            traceWindow.Show();
        }

        public void TraceSavedSend()
        {
            var traceWindow = new PlaybackTraceWindow("trace file name"); // Not shown, just reusing the class
            traceWindow.Show();
        }
    }
}
