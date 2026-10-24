using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrewChiefV4.UserInterface
{
    public partial class SendLogTraceDialog : Form
    {
        const string recipient = "contact-project+mr-belowski-crewchiefv4-9438945-issue-@incoming.gitlab.com";
        public SendLogTraceDialog()
        {
            InitializeComponent();
            labelInstructions.Text = Configuration.getUIString("send_log_trace_instructions") + Environment.NewLine + recipient;
        }
        /// <summary>
        /// Browse for the log file and then send it to a file-sharing site
        /// </summary>
        public async Task LogFileChoose()
        {
            var filter = "Console logs|console_*.txt";
            OpenFileDialog openLogFileDialog = new OpenFileDialog();
            openLogFileDialog.Filter = filter;
            openLogFileDialog.FilterIndex = 0;
            openLogFileDialog.InitialDirectory = DataFiles.UserLogsFolder;
            openLogFileDialog.FileName = "";
            openLogFileDialog.RestoreDirectory = false;
            openLogFileDialog.SupportMultiDottedExtensions = true;
            openLogFileDialog.Title = "Select log file to send";
            var dialogResult = openLogFileDialog.ShowDialog(this);
            if (dialogResult == DialogResult.OK)
            {
                string logFilePath = openLogFileDialog.FileName;
                await Log_TraceSend(logFilePath);
            }
        }
        /// <summary>
        /// Browse for the trace file and then send it to a file-sharing site
        /// </summary>
        public async Task TraceFileChoose()
        {
            OpenFileDialog openTraceFileDialog = new OpenFileDialog();
            openTraceFileDialog.Filter = PlaybackTraceWindow.filter;
            openTraceFileDialog.FilterIndex = 0;
            openTraceFileDialog.InitialDirectory = DataFiles.UserLogsFolder;
            openTraceFileDialog.FileName = "";
            openTraceFileDialog.RestoreDirectory = false;
            openTraceFileDialog.SupportMultiDottedExtensions = true;
            openTraceFileDialog.Title = "Select trace log to send";
            var dialogResult = openTraceFileDialog.ShowDialog(this);
            if (dialogResult == DialogResult.OK)
            {
                await Log_TraceSend(openTraceFileDialog.FileName);
            }
        }

        private static string log_TracePath;
        /// <summary>
        /// Provide user with information to upload a lof or trace file to a
        /// file-sharing site and send an email with the URL to the CrewChief
        /// gitlab issue tracker
        /// </summary>
        /// <param name="_log_TracePath">Full path to the log/trace file</param>
        /// <returns></returns>
        public async Task Log_TraceSend(string _log_TracePath)
        {
            if (File.Exists(_log_TracePath))
            {
                log_TracePath = _log_TracePath;
                var sendLogTraceDialog = new SendLogTraceDialog();
                //sendLogTraceDialog.textBoxCrewChiefEmail.Text = "crewchiefv4-issue@gitlab.com";
                sendLogTraceDialog.textBoxCrewChiefEmail.Visible = false;
                toolTip1.SetToolTip(sendLogTraceDialog.labelCrewChiefEmail, recipient);
                toolTip1.SetToolTip(sendLogTraceDialog.buttonCopyCrewChiefEmail, recipient);
                sendLogTraceDialog.textBoxLogTraceFilePath.Text = Path.GetFileName(log_TracePath);
                toolTip1.SetToolTip(sendLogTraceDialog.textBoxLogTraceFilePath, log_TracePath);
                toolTip1.SetToolTip(sendLogTraceDialog.buttonCopyLogTraceFilePath, log_TracePath);
                sendLogTraceDialog.TopMost = true; // Make the dialog the top window
                sendLogTraceDialog.ShowDialog(this);
                Log.Commentary($"Log/Trace file: {_log_TracePath}");
            }
            else
            {
                Log.Error($"Log/Trace '{_log_TracePath}' not found");
                MessageBox.Show($"Log/Trace '{_log_TracePath}' not found", "File not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void buttonCopyLogTraceFilePath_Click(object sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(log_TracePath);
            }
            catch (Exception exception)
            {
                Log.Exception(exception, $"Couldn't copy '{log_TracePath}' to clipboard");
            }
        }

        private void buttonCrewChiefEmail_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(recipient);
        }
    }
}
