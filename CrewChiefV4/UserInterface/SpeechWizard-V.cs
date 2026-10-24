using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CrewChiefV4.UserInterface.Models;
using CrewChiefV4.UserInterface.VMs;

namespace CrewChiefV4.UserInterface
{
    public partial class SpeechWizard_V : Form
    {
        private readonly SpeechWizard_VM vm;
        private readonly SpeechWizard model;
        private MicrophoneGraph microphoneGraph;
        private System.Windows.Forms.Timer microphoneTimer;
        private Button playbackButton;
        private Label microphoneTestHint;
        private NAudio.Wave.WaveOutEvent microphonePlayback;
        private NAudio.Wave.RawSourceWaveStream microphonePlaybackStream;
        private SpeechRecogniser recordedRecogniser;
        private DiagnosticMicrophone diagnosticMicrophone;
        private bool diagnosticAttempted;

        private void StopDiagnosticMicrophone()
        {
            diagnosticMicrophone?.Dispose();
            diagnosticMicrophone = null;
            diagnosticAttempted = false;
        }

        private sealed class DiagnosticMicrophone : IDisposable
        {
            public readonly NAudio.Wave.WaveFormat Format = new NAudio.Wave.WaveFormat(16000, 1);
            private readonly NAudio.Wave.WaveInEvent capture;
            private readonly object sync = new object();
            private readonly byte[] recording = new byte[16000 * 2 * 10];
            private int next, count;
            private float peak;
            private DateTime lastSample = DateTime.MinValue;
            private bool disposed;
            private readonly SpeechRecogniser recogniser;
            private int recordingSession = -1;

            public DiagnosticMicrophone(SpeechRecogniser speechRecogniser)
            {
                recogniser = speechRecogniser;
                capture = new NAudio.Wave.WaveInEvent
                {
                    DeviceNumber = -1, WaveFormat = Format, BufferMilliseconds = 50
                };
                capture.DataAvailable += AudioAvailable;
                try { capture.StartRecording(); }
                catch { capture.Dispose(); throw; }
            }

            private void AudioAvailable(object sender, NAudio.Wave.WaveInEventArgs e)
            {
                lock (sync)
                {
                    if (disposed) return;
                    for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
                    {
                        int sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                        peak = Math.Max(peak, Math.Abs(sample) / 32768f);
                    }
                    lastSample = DateTime.UtcNow;
                    // Meter all samples; retain only the open PTT/listening interval.
                    if (recogniser == null) return;
                    int session = recogniser.WizardListeningSession;
                    if (session != recordingSession)
                    {
                        next = count = 0;
                        recordingSession = session;
                    }
                    if (MainWindow.voiceOption != MainWindow.VoiceOptionEnum.HOLD)
                    {
                        next = count = 0;
                        return;
                    }
                    if (!recogniser.WizardListening) return;
                    int offset = Math.Max(0, e.BytesRecorded - recording.Length);
                    int length = e.BytesRecorded - offset;
                    int first = Math.Min(length, recording.Length - next);
                    Array.Copy(e.Buffer, offset, recording, next, first);
                    Array.Copy(e.Buffer, offset + first, recording, 0, length - first);
                    next = (next + length) % recording.Length;
                    count = Math.Min(recording.Length, count + length);
                    lastSample = DateTime.UtcNow;
                }
            }

            public bool HasAudio { get { lock (sync) { return count > 0; } } }

            public float ReadPeak(out bool receiving)
            {
                lock (sync)
                {
                    receiving = (DateTime.UtcNow - lastSample).TotalMilliseconds < 500;
                    float result = receiving ? peak : 0;
                    peak = 0;
                    return result;
                }
            }

            public byte[] Snapshot()
            {
                lock (sync)
                {
                    var result = new byte[count];
                    int start = (next - count + recording.Length) % recording.Length;
                    int first = Math.Min(count, recording.Length - start);
                    Array.Copy(recording, start, result, 0, first);
                    Array.Copy(recording, 0, result, first, count - first);
                    return result;
                }
            }

            public void Dispose()
            {
                lock (sync) { disposed = true; }
                capture.DataAvailable -= AudioAvailable;
                capture.Dispose();
                lock (sync) { Array.Clear(recording, 0, recording.Length); count = 0; }
            }
        }

        private void StopMicrophonePlayback()
        {
            if (microphonePlayback != null)
            {
                microphonePlayback.Stop();
                microphonePlayback.Dispose();
                microphonePlayback = null;
            }
            microphonePlaybackStream?.Dispose();
            microphonePlaybackStream = null;
        }

        private void PlaybackButton_Click(object sender, EventArgs e)
        {
            if (microphonePlayback != null)
            {
                StopMicrophonePlayback();
                return;
            }
            if (MainWindow.voiceOption != MainWindow.VoiceOptionEnum.HOLD) return;
            var recogniser = MainWindow.instance?.crewChief?.speechRecogniser;
            NAudio.Wave.WaveFormat format;
            byte[] audio;
            if (diagnosticMicrophone != null)
            {
                format = diagnosticMicrophone.Format;
                audio = diagnosticMicrophone.Snapshot();
            }
            else
            {
                if (recogniser == null) return;
                audio = recogniser.GetMicrophoneRecording(out format);
            }
            if (audio.Length == 0) return;
            try
            {
                microphonePlaybackStream = new NAudio.Wave.RawSourceWaveStream(
                    new System.IO.MemoryStream(audio, false), format);
                microphonePlayback = new NAudio.Wave.WaveOutEvent();
                microphonePlayback.Init(microphonePlaybackStream);
                microphonePlayback.Play();
            }
            catch (Exception exception)
            {
                StopMicrophonePlayback();
                MessageBox.Show(this, "Unable to play microphone audio: " + exception.Message,
                    "Microphone playback", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void InitialiseMicrophoneGraph()
        {
            microphoneGraph = new MicrophoneGraph { Dock = DockStyle.Fill, Margin = new Padding(6) };
            tableLayoutPanel1.RowCount = 4;
            int graphHeight = Math.Max(160, Font.Height * 7);
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, graphHeight));
            tableLayoutPanel1.RowStyles[1].Height = 40F;
            tableLayoutPanel1.RowStyles[2].Height = 60F;
            tableLayoutPanel1.Controls.Add(microphoneGraph, 0, 3);
            tableLayoutPanel1.SetColumnSpan(microphoneGraph, 2);
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + 130);
            playbackButton = new Button
            {
                Text = "Play back", AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.None,
                Font = buttonRestartCrewChief.Font,
                Padding = new Padding(12, 4, 12, 4)
            };
            playbackButton.Click += PlaybackButton_Click;
            var speechButtons = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                Margin = new Padding(6)
            };
            speechButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            speechButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            speechButtons.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            speechButtons.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            microphoneTestHint = new Label
            {
                AutoSize = true, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(6)
            };
            speechButtons.Controls.Add(microphoneTestHint, 0, 2);
            tableLayoutPanel1.Controls.Remove(buttonRestartCrewChief);
            speechButtons.Controls.Add(buttonRestartCrewChief, 0, 0);
            speechButtons.Controls.Add(playbackButton, 0, 1);
            tableLayoutPanel1.Controls.Add(speechButtons, 1, 1);
            toolTip1.SetToolTip(playbackButton, "Play the latest PTT/listening interval (up to 10 seconds) through the Windows default output. Headphones avoid feeding playback into the microphone.");
            recordedRecogniser = MainWindow.instance?.crewChief?.speechRecogniser;
            recordedRecogniser?.ClearMicrophoneRecording();
            Disposed += (sender, args) =>
            {
                StopMicrophonePlayback();
                StopDiagnosticMicrophone();
            };
            microphoneTimer = new System.Windows.Forms.Timer(components) { Interval = 50 };
            microphoneTimer.Tick += MicrophoneTimer_Tick;
            microphoneTimer.Start();
        }

        private void MicrophoneTimer_Tick(object sender, EventArgs e)
        {
            var recogniser = MainWindow.instance?.crewChief?.speechRecogniser;
            if (!ReferenceEquals(recordedRecogniser, recogniser))
            {
                recordedRecogniser?.ClearMicrophoneRecording();
                StopDiagnosticMicrophone();
                recordedRecogniser = recogniser;
            }
            if (microphonePlayback != null &&
                microphonePlayback.PlaybackState == NAudio.Wave.PlaybackState.Stopped)
                StopMicrophonePlayback();
            // Follow the running recogniser's mode: settings changes take effect after restart.
            bool usesNAudio = recogniser != null ? recogniser.UsesNAudioSpeechInput : checkBoxNaudio.Checked;
            if (usesNAudio)
                StopDiagnosticMicrophone();
            else if (!diagnosticAttempted)
            {
                diagnosticAttempted = true;
                try { diagnosticMicrophone = new DiagnosticMicrophone(recogniser); }
                catch (Exception exception)
                {
                    microphoneGraph.Status = "Microphone unavailable: " + exception.Message;
                }
            }
            if (diagnosticMicrophone != null)
                microphoneGraph.Status = "Windows default microphone (diagnostic capture)";
            else if (usesNAudio)
                microphoneGraph.Status = "Speech microphone (hold PTT if used)";
            bool pttSelected = MainWindow.voiceOption == MainWindow.VoiceOptionEnum.HOLD;
            if (!pttSelected) StopMicrophonePlayback();
            microphoneTestHint.Text = pttSelected
                ? "Hold PTT and speak, then release and press Play back."
                : "For microphone testing and playback, select Hold button (push-to-talk) in the main window.";
            playbackButton.Enabled = pttSelected && (microphonePlayback != null ||
                (diagnosticMicrophone != null ? diagnosticMicrophone.HasAudio :
                    recogniser != null && recogniser.HasMicrophoneRecording));
            playbackButton.Text = microphonePlayback == null ? "Play back" : "Stop playback";
            bool receiving = false;
            float peak = diagnosticMicrophone != null ? diagnosticMicrophone.ReadPeak(out receiving) :
                recogniser == null ? 0 : recogniser.ReadMicrophonePeak(out receiving);
            microphoneGraph.AddSample(peak, receiving);
        }

        private sealed class MicrophoneGraph : Panel
        {
            public string Status { get; set; } = "Speech microphone";
            private readonly float[] history = new float[200]; // Ten seconds at 20 Hz.
            private int next;
            private float current;
            private bool receiving;

            public MicrophoneGraph()
            {
                DoubleBuffered = true;
                BackColor = Color.FromArgb(25, 25, 25);
                ForeColor = Color.WhiteSmoke;
                AccessibleName = "Microphone level graph";
            }

            public void AddSample(float peak, bool hasAudio)
            {
                current = Math.Max(0, Math.Min(1, peak));
                receiving = hasAudio;
                history[next] = current;
                next = (next + 1) % history.Length;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                string title = receiving
                    ? string.Format("Microphone peak: {0:0}%", current * 100)
                    : "No audio arriving — " + Status;
                e.Graphics.DrawString(title, Font, Brushes.WhiteSmoke, 6, 4);
                int top = Font.Height + 14;
                int height = ClientSize.Height - top - 8;
                int width = ClientSize.Width - 48;
                if (height < 2 || width < 2) return;
                using (var grid = new Pen(Color.FromArgb(65, 65, 65)))
                using (var trace = new Pen(Color.LimeGreen, 2))
                {
                    for (int percent = 0; percent <= 100; percent += 50)
                    {
                        float y = top + height * (1 - percent / 100f);
                        e.Graphics.DrawLine(grid, 40, y, 40 + width, y);
                        e.Graphics.DrawString(percent.ToString(), Font, Brushes.WhiteSmoke, 2, y - Font.Height / 2f);
                    }
                    var points = new PointF[history.Length];
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new PointF(40 + width * i / (points.Length - 1f),
                            top + height * (1 - history[(next + i) % history.Length]));
                    e.Graphics.DrawLines(trace, points);
                }
            }
        }


        public SpeechWizard_V(bool IsAppRunning)
        {
            vm = new SpeechWizard_VM(this);
            model = new SpeechWizard(vm);
            ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.Icon = ((Icon)(resources.GetObject("$this.Icon")));
            this.StartPosition = FormStartPosition.CenterParent;
            InitializeComponent();

            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                _ = new DarkModeForms.DarkModeCS(this);

            this.toolTip1.SetToolTip(this.radioButtonMicrosoftSRE, Configuration.getUIString("prefer_microsoft_sre_help"));
            this.toolTip1.SetToolTip(this.radioButtonSystemSRE, Configuration.getUIString("prefer_system_sre_help"));
            this.toolTip1.SetToolTip(this.checkBoxNaudio, Configuration.getUIString("use_naudio_for_speech_recognition_help"));
            this.toolTip1.SetToolTip(this.textBoxSpeechRecognitionCountry, Configuration.getUIString("speech_recognition_country_help"));
            this.toolTip1.SetToolTip(this.textBoxUILanguage, Configuration.getUIString("ui_language_help"));
            this.toolTip1.SetToolTip(this.checkBoxFreeDictation, Configuration.getUIString("use_free_dictation_for_chat_help"));
            checkBoxNaudio.Checked = UserSettings.GetUserSettings().getBoolean("use_naudio_for_speech_recognition");
            radioButtonMicrosoftSRE.Checked = !UserSettings.GetUserSettings().getBoolean("prefer_system_sre");
            radioButtonSystemSRE.Checked = UserSettings.GetUserSettings().getBoolean("prefer_system_sre");
            checkBoxFreeDictation.Checked = UserSettings.GetUserSettings().getBoolean("use_free_dictation_for_chat");
            textBoxSpeechRecognitionCountry.Text = UserSettings.GetUserSettings().getString("speech_recognition_country");
            textBoxUILanguage.Text = UserSettings.GetUserSettings().getString("ui_language");
            model.Init(IsAppRunning);
            SpeechWizard.Active = true;
            InitialiseMicrophoneGraph();
        }

        private void buttonRestartCrewChief_Click(object sender, EventArgs e)
        {
            model.buttonRestartCrewChief_Click();
        }

        private void radioButtonMicrosoftSRE_CheckedChanged(object sender, EventArgs e)
        {
            model.radioButtonMicrosoftSRE_CheckedChanged();
        }

        private void radioButtonSystem_CheckedChanged(object sender, EventArgs e)
        {
            model.radioButtonSystem_CheckedChanged();
        }

        private void checkBoxNaudio_CheckedChanged(object sender, EventArgs e)
        {
            model.checkBoxNaudio_CheckedChanged(checkBoxNaudio.Checked);
        }

        private void checkBoxFreeDictation_CheckedChanged(object sender, EventArgs e)
        {
            model.checkBoxFreeDictation_CheckedChanged(checkBoxFreeDictation.Checked);
        }

        private void textBoxUILanguage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                model.textBoxUILanguage_KeyDown(textBoxUILanguage.Text);
            }
        }

        private void textBoxSpeechRecognitionCountry_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                model.textBoxSpeechRecognitionCountry_KeyDown(textBoxSpeechRecognitionCountry.Text);
            }
        }

        private void SpeechWizard_V_FormClosed(object sender, FormClosedEventArgs e)
        {
            microphoneTimer?.Stop();
            StopMicrophonePlayback();
            recordedRecogniser?.ClearMicrophoneRecording();
            StopDiagnosticMicrophone();
            SpeechWizard.Active = false;
            model.ShutDown();
        }

        private void buttonHelp_Click(object sender, EventArgs e)
        {
            var form = new CrewChiefV4.HelpWindow(this, "VoiceRecognition_InstallationTraining");
            form.ShowDialog(this);
        }
    }
}

