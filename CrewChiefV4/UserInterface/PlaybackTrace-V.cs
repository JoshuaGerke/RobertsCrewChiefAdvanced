using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using CrewChiefV4.UserInterface.Models;

namespace CrewChiefV4.UserInterface
{
    public partial class PlaybackTraceWindow : Form
    {
        public PlaybackTraceWindow(string playbackFile)
        {
            ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.Icon = ((Icon)(resources.GetObject("$this.Icon")));
            InitializeComponent();
            //foreach (var control in new
            textBoxPlaybackFiletoolTip.SetToolTip(labelPlaybackSpeed, Configuration.getUIString("playback_speed_tooltip"));
            textBoxPlaybackFiletoolTip.SetToolTip(textBoxPlaybackSpeed, Configuration.getUIString("playback_speed_tooltip"));
            textBoxPlaybackFiletoolTip.SetToolTip(hScrollBarPlaybackSpeed, Configuration.getUIString("playback_speed_tooltip"));
            textBoxPlaybackFile.Text = playbackFile;
        }

        private void checkBoxPlayback_CheckedChanged(object sender, EventArgs e)
        {
            MainWindow.instance.filenameTextbox.Text = null;
            if (checkBoxPlayback.Checked)
            {
                MainWindow.instance.filenameTextbox.Text = textBoxPlaybackFile.Text;
                CrewChief.playbackIntervalMilliseconds = hScrollBarPlaybackSpeed.Value;
            }
            MainWindow.playingBackTrace = checkBoxPlayback.Checked;
        }

        private static string gamePrefix
        {
            get { return CrewChief.gameDefinition.commandLineName; }
        }

        private static string friendlyName
        {
            get {return CrewChief.gameDefinition.friendlyName; }
        }

        public static string filter {
            get { return $"{friendlyName} trace logs|{gamePrefix}*.zip|{friendlyName} unzipped trace logs|{gamePrefix}*.txt|Old {friendlyName} trace logs|{gamePrefix}*.cct|All trace logs|*.zip|All old trace logs|*.cct"; }
        }

        private void buttonChooseTraceFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog openTraceFileDialog = new OpenFileDialog();
            openTraceFileDialog.Filter = filter;
            openTraceFileDialog.FilterIndex = 0;
            openTraceFileDialog.InitialDirectory = DataFiles.DebugLogsFolder;
            openTraceFileDialog.FileName = "";
            openTraceFileDialog.RestoreDirectory = false;
            openTraceFileDialog.SupportMultiDottedExtensions = true;
            openTraceFileDialog.Title = "Select trace log";
            var dialogResult = openTraceFileDialog.ShowDialog(this);
            if (dialogResult == DialogResult.OK)
            {
                this.textBoxPlaybackFile.Text = Path.GetFileName(openTraceFileDialog.FileName);
                this.textBoxPlaybackFiletoolTip.SetToolTip(this.textBoxPlaybackFile, $"Play back a trace {openTraceFileDialog.FileName}");
            }
            else
            {
                textBoxPlaybackFile.Text = String.Empty;
            }
        }

        private void buttonCopyTraceFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog openTraceFileDialog = new OpenFileDialog();
            openTraceFileDialog.Filter = filter;
            openTraceFileDialog.FilterIndex = 0;
            openTraceFileDialog.InitialDirectory = DataFiles.UserLogsFolder;
            openTraceFileDialog.FileName = "";
            openTraceFileDialog.RestoreDirectory = false;
            openTraceFileDialog.SupportMultiDottedExtensions = true;
            openTraceFileDialog.Title = "Select trace log";
            var dialogResult = openTraceFileDialog.ShowDialog(this);
            if (dialogResult == DialogResult.OK)
            {
                string newPath = Path.Combine(DataFiles.DebugLogsFolder, Path.GetFileName(openTraceFileDialog.FileName));
                if (File.Exists(newPath))
                {
                    //ShowDialog(this, )
                }
                else
                {
                    File.Copy(openTraceFileDialog.FileName, newPath);
                }
            }
        }

        private void hScrollBarPlaybackSpeed_Scroll(object sender, ScrollEventArgs e)
        {
            textBoxPlaybackSpeed.Text = hScrollBarPlaybackSpeed.Value.ToString();
        }
    }
}
