using System;
using System.Collections.Generic;
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
        private readonly TextBox apiKeyBox = new TextBox { UseSystemPasswordChar = true, Width = 330 };
        private readonly Label keyState = new Label { AutoSize = true };
        private readonly TextBox elevenKeyBox = new TextBox { UseSystemPasswordChar = true, Width = 330 };
        private readonly Label elevenKeyState = new Label { AutoSize = true };
        private readonly Dictionary<TeamMember, ComboBox> voiceBoxes = new Dictionary<TeamMember, ComboBox>();
        private readonly TextBox questionBox = new TextBox { Width = 360, Text = "Mik, wie sieht unsere Strategie aus?" };
        private readonly TextBox output = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Width = 560, Height = 300 };
        private readonly AiTeamService service = AiTeamService.Instance;

        public AiTeamForm()
        {
            Text = "KI-Team – Ben, Mik und Sara";
            Width = 620;
            Height = 820;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12), AutoScroll = true, WrapContents = false };
            Controls.Add(layout);

            layout.Controls.Add(Heading("Schlüssel (werden verschlüsselt nur für diesen Windows-Benutzer gespeichert)"));
            layout.Controls.Add(Hint("OpenRouter-Schlüssel für Ben, Mik und die Spracherkennung (openrouter.ai → Keys):"));
            layout.Controls.Add(Row(apiKeyBox, MakeButton("Speichern", SaveKey), MakeButton("Löschen", DeleteKey)));
            layout.Controls.Add(keyState);
            layout.Controls.Add(Hint("ElevenLabs-Schlüssel für die Stimmen (elevenlabs.io → Profil → API Keys):"));
            layout.Controls.Add(Row(elevenKeyBox, MakeButton("Speichern", SaveElevenKey), MakeButton("Löschen", DeleteElevenKey)));
            layout.Controls.Add(elevenKeyState);

            layout.Controls.Add(Heading("Stimmen (ElevenLabs)"));
            foreach (TeamMember member in new[] { TeamMember.Ben, TeamMember.Mik, TeamMember.Sara })
            {
                var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
                voiceBoxes[member] = box;
                layout.Controls.Add(Row(new Label { Text = TeamRouter.DisplayName(member) + ":", Width = 50, TextAlign = ContentAlignment.MiddleLeft }, box));
            }
            layout.Controls.Add(Row(MakeButton("Stimmen aus ElevenLabs laden", LoadVoices), MakeButton("Stimmen übernehmen", ApplyVoices),
                MakeButton("Sara als Spotter erzeugen", GenerateSaraPack)));
            layout.Controls.Add(Hint("Tipp: In der ElevenLabs-Stimmenbibliothek nach \"German\" filtern und Stimmen zum Konto hinzufügen, dann hier neu laden."));

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

        private static Label Hint(string text)
        {
            return new Label { Text = text, AutoSize = true, MaximumSize = new Size(560, 0), Margin = new Padding(0, 4, 0, 2) };
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
            string eleven = AiSecretStore.LoadElevenLabs();
            elevenKeyState.Text = string.IsNullOrEmpty(eleven) ? "Kein Schlüssel hinterlegt (dann Ersatzstimme)." : "Hinterlegt: " + AiSecretStore.Mask(eleven);
        }

        private void SaveElevenKey()
        {
            if (string.IsNullOrWhiteSpace(elevenKeyBox.Text))
            {
                return;
            }
            if (elevenKeyBox.Text.Trim().StartsWith("sk-or-"))
            {
                Append("Das ist ein OpenRouter-Schlüssel (beginnt mit sk-or-). Der gehört ins erste Feld.");
                return;
            }
            AiSecretStore.Save(elevenKeyBox.Text, AiSecretStore.ElevenLabsPath);
            elevenKeyBox.Text = "";
            UpdateKeyState();
            Append("ElevenLabs-Schlüssel gespeichert.");
            service.PrewarmSaraPhrases();
            LoadVoices();
        }

        private void DeleteElevenKey()
        {
            AiSecretStore.Delete(AiSecretStore.ElevenLabsPath);
            UpdateKeyState();
            Append("ElevenLabs-Schlüssel gelöscht.");
        }

        private void GenerateSaraPack()
        {
            Append("Erzeuge Saras deutsche Spotter-Ansagen (dauert etwa eine Minute) …");
            Run(service.GenerateSaraSpotterPackAsync(step => Append("  " + step)));
        }

        private void LoadVoices()
        {
            if (!service.HasElevenLabsKey)
            {
                Append("Erst den ElevenLabs-Schlüssel speichern.");
                return;
            }
            Append("Lade Stimmen …");
            service.ListElevenLabsVoicesAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Append(ExplainKeyError("ElevenLabs", t.Exception.GetBaseException().Message));
                    return;
                }
                if (IsDisposed)
                {
                    return;
                }
                BeginInvoke(new Action(() => FillVoices(t.Result)));
            });
        }

        private void FillVoices(List<ElevenLabsVoice> voices)
        {
            var s = service.Settings;
            foreach (var entry in voiceBoxes)
            {
                string current = s.ElevenLabsVoiceFor(entry.Key);
                entry.Value.Items.Clear();
                foreach (var voice in voices)
                {
                    entry.Value.Items.Add(voice);
                }
                entry.Value.SelectedItem = voices.FirstOrDefault(v => v.Id == current);
            }
            Append(voices.Count + " Stimmen geladen. Auswählen und \"Stimmen übernehmen\" drücken, dann mit \"Stimme Ben/Mik/Sara\" anhören.");
        }

        private void ApplyVoices()
        {
            var names = new Dictionary<TeamMember, string>
            {
                { TeamMember.Ben, "ai_team_elevenlabs_voice_ben" },
                { TeamMember.Mik, "ai_team_elevenlabs_voice_mik" },
                { TeamMember.Sara, "ai_team_elevenlabs_voice_sara" },
            };
            int changed = 0;
            foreach (var entry in voiceBoxes)
            {
                var voice = entry.Value.SelectedItem as ElevenLabsVoice;
                if (voice != null)
                {
                    UserSettings.GetUserSettings().setProperty(names[entry.Key], voice.Id);
                    changed++;
                }
            }
            if (changed == 0)
            {
                Append("Keine Stimme ausgewählt.");
                return;
            }
            UserSettings.GetUserSettings().saveUserSettings();
            service.ReloadSettings();
            Append("Stimmen übernommen.");
            if (!service.SaraSpotterPackCurrent)
            {
                GenerateSaraPack();
            }
        }

        private void SaveKey()
        {
            if (string.IsNullOrWhiteSpace(apiKeyBox.Text))
            {
                return;
            }
            string key = apiKeyBox.Text.Trim();
            if (key.StartsWith("sk_") && !key.StartsWith("sk-"))
            {
                Append("Das sieht nach einem ElevenLabs-Schlüssel aus (beginnt mit sk_). Der gehört ins zweite Feld.");
                return;
            }
            AiSecretStore.Save(key);
            apiKeyBox.Text = "";
            UpdateKeyState();
            Append("OpenRouter-Schlüssel gespeichert, prüfe ihn …");
            // gleich prüfen, damit ein falsch kopierter Schlüssel sofort auffällt
            service.TestConnectionAsync().ContinueWith(t =>
            {
                string result = t.Exception == null ? t.Result : t.Exception.GetBaseException().Message;
                Append(result.StartsWith("OK") ? "Schlüssel funktioniert. " + result : ExplainKeyError("OpenRouter", result));
                if (result.StartsWith("OK"))
                {
                    service.PrewarmSaraPhrases();
                }
            });
        }

        /// <summary>Fehlermeldungen der Anbieter in verständliche Hinweise übersetzen.</summary>
        public static string ExplainKeyError(string provider, string error)
        {
            string e = error ?? "";
            if (e.Contains("401") || e.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 && e.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Der " + provider + "-Schlüssel wird nicht angenommen. Meist ist er nicht vollständig kopiert oder im falschen Feld gelandet. " +
                       "Erstell beim Anbieter einen neuen Schlüssel, kopier ihn mit dem Kopier-Symbol und füg ihn erneut ein. (" + e + ")";
            }
            if (e.Contains("402") || e.IndexOf("credit", StringComparison.OrdinalIgnoreCase) >= 0 || e.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Bei " + provider + " ist kein Guthaben bzw. kein Kontingent mehr übrig. Bitte dort aufladen oder den Tarif prüfen. (" + e + ")";
            }
            if (e.Contains("403") || e.IndexOf("permission", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Dem " + provider + "-Schlüssel fehlen Berechtigungen. Beim Erstellen alle Rechte erlauben oder einen neuen Schlüssel anlegen. (" + e + ")";
            }
            if (e.IndexOf("Verbindung", StringComparison.OrdinalIgnoreCase) >= 0 || e.IndexOf("Zeitüberschreitung", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Keine Verbindung zu " + provider + ". Ist der PC online? (" + e + ")";
            }
            return provider + ": " + e;
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
                "Aktiv: " + (s.Enabled ? "ja" : "nein") + ", Funkmodus: " + s.RadioMode + ", Stimmen: " + s.TtsProvider +
                    (s.TtsProvider == AiTtsProvider.ELEVENLABS ? " (" + s.ElevenLabsModel + ", Schlüssel " + (service.HasElevenLabsKey ? "da" : "fehlt") + ")" : "") + "\r\n" +
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
