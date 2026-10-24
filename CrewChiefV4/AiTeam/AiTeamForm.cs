using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Menü "KI-Team": API-Schlüssel, Verbindungstest, Stimmtest, Testfrage ohne Mikrofon, Diagnose.
    /// Alle übrigen Einstellungen stehen im Eigenschaftendialog unter der Kategorie "KI-Team".
    /// </summary>
    public class AiTeamForm : Form
    {
        private readonly TextBox apiKeyBox = new TextBox { UseSystemPasswordChar = true, Width = 360 };
        private readonly Label keyState = new Label { AutoSize = true };
        private readonly TextBox questionBox = new TextBox { Width = 360, Text = "Mik, wie sieht unsere Strategie aus?" };
        private readonly TextBox output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 560, Height = 300 };
        private readonly AiTeamService service = AiTeamService.Instance;

        public AiTeamForm()
        {
            Text = "KI-Team – Ben, Mik und Sara";
            Width = 620;
            Height = 640;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12), AutoScroll = true, WrapContents = false };
            Controls.Add(layout);

            layout.Controls.Add(Heading("API-Schlüssel (wird verschlüsselt nur für diesen Windows-Benutzer gespeichert)"));
            layout.Controls.Add(Row(apiKeyBox, MakeButton("Speichern", SaveKey), MakeButton("Löschen", DeleteKey)));
            layout.Controls.Add(keyState);

            layout.Controls.Add(Heading("Test"));
            layout.Controls.Add(Row(MakeButton("Verbindung testen", TestConnection), MakeButton("Stimme Ben", () => Speak(TeamMember.Ben)),
                MakeButton("Stimme Mik", () => Speak(TeamMember.Mik)), MakeButton("Stimme Sara", () => Speak(TeamMember.Sara))));
            layout.Controls.Add(Row(questionBox, MakeButton("Frage senden", AskQuestion)));
            layout.Controls.Add(new Label
            {
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                Text = "Die Testfrage läuft genau wie ein Funkspruch über Routing, Telemetrie und Sprachausgabe – nur ohne Mikrofon. " +
                       "Ohne laufendes Spiel sagt das Team ehrlich, dass keine Daten da sind.",
            });

            layout.Controls.Add(Heading("Status und Diagnose"));
            layout.Controls.Add(output);
            layout.Controls.Add(Row(MakeButton("Diagnose aktualisieren", ShowDiagnostics), MakeButton("Zähler zurücksetzen", () => { service.Stats.Reset(); ShowDiagnostics(); })));

            service.StatusChanged += OnStatus;
            FormClosed += (s, e) => service.StatusChanged -= OnStatus;
            UpdateKeyState();
            ShowDiagnostics();
        }

        private static Label Heading(string text)
        {
            return new Label { Text = text, AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Margin = new Padding(0, 10, 0, 4) };
        }

        private static FlowLayoutPanel Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
            row.Controls.AddRange(controls);
            return row;
        }

        private static Button MakeButton(string text, Action onClick)
        {
            var b = new Button { Text = text, AutoSize = true };
            b.Click += (s, e) => onClick();
            return b;
        }

        private void UpdateKeyState()
        {
            string key = AiSecretStore.Load();
            keyState.Text = string.IsNullOrEmpty(key) ? "Kein Schlüssel hinterlegt." : "Hinterlegt: " + AiSecretStore.Mask(key);
        }

        private void SaveKey()
        {
            if (string.IsNullOrWhiteSpace(apiKeyBox.Text))
            {
                return;
            }
            AiSecretStore.Save(apiKeyBox.Text);
            apiKeyBox.Text = "";
            UpdateKeyState();
            Append("API-Schlüssel gespeichert.");
            service.PrewarmSaraPhrases();
        }

        private void DeleteKey()
        {
            AiSecretStore.Delete();
            UpdateKeyState();
            Append("API-Schlüssel gelöscht.");
        }

        private void TestConnection()
        {
            Append("Teste Verbindung …");
            Run(service.TestConnectionAsync());
        }

        private void Speak(TeamMember member)
        {
            string text;
            switch (member)
            {
                case TeamMember.Mik: text = "Mik hier. Ich hab die Strategie im Blick und melde mich, wenn sich was ergibt."; break;
                case TeamMember.Sara: text = "Sara hier. Ich hab die Seiten im Blick."; break;
                default: text = "Ben hier. Funk steht, ich hab das Auto im Blick."; break;
            }
            Run(service.SpeakAsync(member, text, true, System.Threading.CancellationToken.None).ContinueWith(t => t.Exception == null ? "Stimmtest " + member + " eingereiht." : t.Exception.GetBaseException().Message));
        }

        private void AskQuestion()
        {
            string q = questionBox.Text;
            if (string.IsNullOrWhiteSpace(q))
            {
                return;
            }
            Append("Fahrer: " + q);
            Run(service.AskAsync(q).ContinueWith(t => t.Exception == null ? "Antwort: " + (t.Result ?? "(verworfen)") : "Fehler: " + t.Exception.GetBaseException().Message));
        }

        private void ShowDiagnostics()
        {
            var s = service.Settings;
            string mics = "?";
            string voices = "?";
            try { mics = string.Join(", ", AiMicRecorder.AvailableDevices()); } catch (Exception e) { mics = e.Message; }
            try { voices = string.Join(", ", WindowsTts.InstalledVoices()); } catch (Exception e) { voices = e.Message; }
            Append("---- Diagnose ----\r\n" +
                "Aktiv: " + (s.Enabled ? "ja" : "nein") + ", Funkmodus: " + s.RadioMode + ", Stimmen: " + s.TtsProvider + "\r\n" +
                "API: " + s.BaseUrl + " | schnell: " + s.FastModel + " | Analyse: " + s.DeepModel + " | Erkennung: " + s.SttModel + " | Stimme: " + s.TtsModel + "\r\n" +
                "Funktaste: Controller-Aktion \"KI-Team anfunken\"" + (string.IsNullOrEmpty(s.PttKey) ? "" : " oder Taste " + s.PttKey) + "\r\n" +
                "Mikrofone: " + mics + "\r\n" +
                "Windows-Stimmen: " + voices + "\r\n" +
                "Telemetrie zuletzt: " + (service.Memory.LastTickUtc == DateTime.MinValue ? "noch keine" : "vor " + (int)(DateTime.UtcNow - service.Memory.LastTickUtc).TotalSeconds + " s") +
                ", aufgezeichnete Runden: " + service.Memory.GetLaps().Count + "\r\n" +
                service.Stats.ToString());
        }

        private void OnStatus(string text)
        {
            Append(text);
        }

        private void Run(Task<string> task)
        {
            task.ContinueWith(t => Append(t.Exception == null ? t.Result : "Fehler: " + t.Exception.GetBaseException().Message));
        }

        private void Append(string text)
        {
            if (IsDisposed || string.IsNullOrEmpty(text))
            {
                return;
            }
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => Append(text))); } catch (Exception) { }
                return;
            }
            output.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
        }
    }
}
