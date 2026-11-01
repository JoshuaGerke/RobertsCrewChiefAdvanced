using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CrewChiefV4.GameState;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Neue Oberfläche nach Robs Designentwurf (AiTeam/ui), dargestellt per WebView2.
    /// Die Seite schickt Befehle, das Fenster speichert sie wie das alte KI-Team-Fenster und liefert
    /// zweimal pro Sekunde Live-Daten. Das alte Fenster bleibt über "Altes Fenster" erreichbar.
    /// </summary>
    public sealed class RrcaForm : Form
    {
        private static RrcaForm open;
        private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
        private static readonly string[] MemberIds = { "ben", "mik", "sara" };

        private readonly WebView2 web = new WebView2 { Dock = DockStyle.Fill };
        private readonly System.Windows.Forms.Timer live = new System.Windows.Forms.Timer { Interval = 500 };
        private readonly AiTeamService service = AiTeamService.Instance;
        private bool saraPackCurrent;
        private DateTime saraPackCheckedUtc = DateTime.MinValue;

        public static bool IsOpen { get { return open != null && !open.IsDisposed; } }

        public static void ShowSingle(IWin32Window owner)
        {
            if (open == null || open.IsDisposed)
            {
                open = new RrcaForm();
                // ohne Besitzer: sonst verschwindet RRCA, sobald das Crew-Chief-Fenster minimiert ist
                open.Show();
            }
            else
            {
                if (open.WindowState == FormWindowState.Minimized) open.WindowState = FormWindowState.Normal;
                open.Activate();
            }
        }

        private RrcaForm()
        {
            Text = "RRCA – Rob's Race Crew Advanced";
            Width = 1180;
            Height = 900;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = System.Drawing.Color.FromArgb(11, 14, 16);
            try { Icon = MainWindow.instance != null ? MainWindow.instance.Icon : null; } catch (Exception) { }
            Controls.Add(web);
            live.Tick += (s, e) => PushLive();
            service.StatusChanged += OnStatus;
            FormClosed += (s, e) => { live.Stop(); service.StatusChanged -= OnStatus; ShowMainWindow(); };
            Load += async (s, e) => await InitAsync();
        }

        private async Task InitAsync()
        {
            string ui = Path.Combine(Path.GetDirectoryName(typeof(RrcaForm).Assembly.Location), "AiTeam", "ui");
            try
            {
                // Benutzerdaten nicht neben die exe legen, die kann schreibgeschützt liegen
                string data = DataFiles.IsolatedRoot == null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RCCA", "WebView2") : Path.Combine(DataFiles.IsolatedRoot, "WebView2");
                var env = await CoreWebView2Environment.CreateAsync(null, data);
                await web.EnsureCoreWebView2Async(env);
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "Die neue Oberfläche braucht die Microsoft-Edge-WebView2-Runtime (bei Windows 11 dabei). " +
                    "Es öffnet sich das bisherige Fenster.\n\n" + e.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                BeginInvoke(new Action(() => { OpenOld(); Close(); }));
                return;
            }
            var core = web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.SetVirtualHostNameToFolderMapping("rrca.app", ui, CoreWebView2HostResourceAccessKind.DenyCors);
            core.WebMessageReceived += OnMessage;
            // externe Links nie im Fenster öffnen
            core.NewWindowRequested += (s, e) => e.Handled = true;
            core.NavigationStarting += (s, e) => { if (!e.Uri.StartsWith("https://rrca.app/")) e.Cancel = true; };
            core.Navigate("https://rrca.app/rrca.html");
        }

        private void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            JObject m;
            try { m = JObject.Parse(e.WebMessageAsJson); } catch (Exception) { return; }
            string cmd = (string)m["cmd"];
            try
            {
                Handle(cmd, m);
            }
            catch (Exception ex)
            {
                Notice("Fehler bei \"" + cmd + "\": " + ex.Message);
            }
        }

        private void Handle(string cmd, JObject m)
        {
            switch (cmd)
            {
                case "ready":
                    Post(BuildInit());
                    PushLive();
                    live.Start();
                    if (service.HasElevenLabsKey) LoadVoices(false);
                    break;
                case "setAudioLevel":
                    SetAudioLevel((string)m["channel"], m["percent"], (bool?)m["persist"] ?? true);
                    break;
                case "testMikNumbers":
                    TestMikNumbers((bool?)m["spelled"] ?? false);
                    break;
                case "setMember":
                    service.SetMemberEnabled(Member((string)m["member"]), (bool)m["on"]);
                    break;
                case "saraOnly":
                    service.SetMemberEnabled(TeamMember.Ben, false);
                    service.SetMemberEnabled(TeamMember.Mik, false);
                    service.SetMemberEnabled(TeamMember.Sara, true);
                    Notice("Nur Sara ist aktiv. Ben und Mik sind ausgeschaltet.");
                    break;
                case "setVoice":
                    SaveVoice(Member((string)m["member"]), (string)m["id"]);
                    break;
                case "loadVoices":
                    LoadVoices(true);
                    break;
                case "testVoice":
                    TestVoice(Member((string)m["member"]));
                    break;
                case "prepareVoices":
                    Notice("Bereite die Funkclips vor …");
                    Run(service.PrepareRadioVoicesAsync());
                    break;
                case "saraPack":
                    Notice("Erzeuge Saras deutsche Spotter-Ansagen, das dauert etwa eine Minute …");
                    Run(service.GenerateSaraSpotterPackAsync(step => { }).ContinueWith(t => { saraPackCheckedUtc = DateTime.MinValue; return t.Result; }));
                    break;
                case "testConnection":
                    Notice("Teste die Verbindung …");
                    Run(service.TestConnectionAsync());
                    break;
                case "ask":
                    Run(service.AskAsync((string)m["text"]).ContinueWith(t => t.Exception == null ? "Antwort: " + (t.Result ?? "(verworfen)") : "Fehler: " + t.Exception.GetBaseException().Message));
                    break;
                case "saveKey":
                    SaveKey((string)m["provider"], (string)m["key"]);
                    break;
                case "deleteKey":
                    AiSecretStore.Delete((string)m["provider"] == "elevenlabs" ? AiSecretStore.ElevenLabsPath : null);
                    Notice("Schlüssel gelöscht.");
                    Post(BuildInit());
                    break;
                case "setMicrophone":
                    SaveProperty("ai_team_microphone", (string)m["name"] ?? "");
                    Notice("Mikrofon gespeichert.");
                    break;
                case "setRadioEffect":
                    SaveProperty("enable_ai_team_radio_effect", (bool)m["on"]);
                    break;
                case "setRecorder":
                    if ((bool)m["on"]) Notice(LiveDiagnostics.Start()); else { LiveDiagnostics.Stop(); Notice("Diagnose gestoppt."); }
                    break;
                case "setMemory":
                    service.SetPersistentMemory((bool)m["on"]);
                    break;
                case "openMemory":
                    Directory.CreateDirectory(DriverMemory.Folder);
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + DriverMemory.Folder + "\"");
                    break;
                case "openDiag":
                    Directory.CreateDirectory(LiveDiagnostics.DefaultFolder);
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + LiveDiagnostics.DefaultFolder + "\"");
                    break;
                case "openNotes":
                    System.Diagnostics.Process.Start("https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/blob/main/HINWEISE_UND_DANK.md");
                    break;
                case "toggleRun":
                    if (MainWindow.instance != null) MainWindow.instance.startApplicationButton_Click(this, EventArgs.Empty);
                    break;
                case "setGame":
                    if (MainWindow.instance != null && !MainWindow.instance.IsAppRunning) MainWindow.instance.gameDefinitionList.Text = (string)m["name"];
                    break;
                case "setAutoDetect":
                    bool autoDetect = (bool)m["on"];
                    if (MainWindow.instance != null) MainWindow.instance.SetAutoDetectEnabled(autoDetect);
                    else
                    {
                        UserSettings.GetUserSettings().setProperty("enable_auto_detect", autoDetect);
                        UserSettings.GetUserSettings().saveUserSettings();
                    }
                    Notice(autoDetect ? "Spielerkennung ist sofort aktiv. Ein laufendes Spiel wird auch im Hintergrund erkannt." : "Spielerkennung ist ausgeschaltet. Die laufende Crew bleibt unverändert.");
                    Post(BuildInit());
                    break;
                case "fixVoices":
                    FixVoicesAndRestart();
                    break;
                case "openMain":
                    ShowMainWindow();
                    break;
                case "openOld":
                    OpenOld();
                    Post(BuildInit());
                    break;
                case "saveSettings":
                    SaveSettings((JObject)m["settings"], (JArray)m["topics"]);
                    break;
            }
        }

        /// <summary>Stellt alles so ein, dass nur Sara spottet, und startet neu, damit die Audioengine ihre Clips lädt.</summary>
        private void FixVoicesAndRestart()
        {
            if (MainWindow.instance != null && MainWindow.instance.IsAppRunning)
            {
                Notice("Crew Chief zuerst stoppen.");
                return;
            }
            var user = UserSettings.GetUserSettings();
            user.setProperty("spotter_name", SaraSpotterPack.SpotterName);
            user.setProperty("enable_spotter", true);
            user.setProperty("use_naudio", true);
            user.saveUserSettings();
            if (Utilities.RestartApp(app_restart: true))
            {
                // RestartApp launches with -multi; the caller must close the old host, not just this child window.
                if (MainWindow.instance != null) MainWindow.instance.Close();
                else Application.Exit();
            }
            else Notice("Gespeichert. Bitte Crew Chief einmal von Hand neu starten.");
        }

        private static void ShowMainWindow()
        {
            var main = MainWindow.instance;
            if (main == null || main.IsDisposed) return;
            if (main.WindowState == FormWindowState.Minimized) main.WindowState = FormWindowState.Normal;
            main.Show();
            main.Activate();
        }

        private void OpenOld()
        {
            using (var form = new AiTeamForm())
            {
                form.ShowDialog(this);
            }
        }

        private JObject BuildInit()
        {
            var s = service.Settings;
            var voices = new JObject();
            var roles = new JObject();
            foreach (string id in MemberIds)
            {
                voices[id] = s.ElevenLabsVoiceFor(Member(id));
                roles[id] = s.IsMemberEnabled(Member(id));
            }
            string[] mics = new string[0];
            try { mics = AiMicRecorder.AvailableDevices(); } catch (Exception) { }
            string openRouter = AiSecretStore.Load(), eleven = AiSecretStore.LoadElevenLabs();
            return new JObject
            {
                ["type"] = "init",
                ["version"] = Version(),
                ["games"] = new JArray(MainWindow.instance != null ? MainWindow.instance.gameDefinitionList.Items.Cast<object>().Select(g => g.ToString()) : Enumerable.Empty<string>()),
                ["audioLevels"] = AudioLevels(),
                ["autoDetect"] = UserSettings.GetUserSettings().getBoolean("enable_auto_detect"),
                ["roles"] = roles,
                ["voices"] = voices,
                ["microphones"] = new JArray(mics),
                ["microphone"] = s.Microphone ?? "",
                ["ptt"] = "Controller-Aktion „KI-Team anfunken“" + (string.IsNullOrEmpty(s.PttKey) ? "" : " oder Taste " + s.PttKey),
                ["radioEffect"] = s.RadioEffect,
                ["memoryEnabled"] = service.Settings.PersistentMemory,
                ["memoryStatus"] = DriverMemory.Status,
                ["memoryFolder"] = DriverMemory.Folder,
                ["recorder"] = LiveDiagnostics.Enabled,
                ["recorderStatus"] = "Diagnose: " + LiveDiagnostics.Status,
                ["keys"] = new JObject
                {
                    ["openrouter"] = string.IsNullOrEmpty(openRouter) ? null : "hinterlegt " + AiSecretStore.Mask(openRouter),
                    ["elevenlabs"] = string.IsNullOrEmpty(eleven) ? null : "hinterlegt " + AiSecretStore.Mask(eleven),
                },
                ["settings"] = new JObject
                {
                    ["radioMode"] = s.RadioMode.ToString(),
                    ["fastModel"] = s.FastModel,
                    ["deepModel"] = s.DeepModel,
                    ["sttModel"] = s.SttModel,
                    ["ttsModel"] = s.ElevenLabsModel,
                    ["reasoning"] = s.ReasoningEffort,
                    ["callLimit"] = s.MaxCallsPerSession,
                    ["timeout"] = s.TimeoutSeconds,
                    ["driverName"] = s.DriverName,
                    ["driverNickname"] = s.DriverNickname,
                },
                ["topics"] = new JArray(AiRadioPreferences.Options.Select(o => new JObject
                {
                    ["id"] = o.Id,
                    ["label"] = o.Label,
                    ["hint"] = o.Hint,
                    ["minimum"] = o.Minimum,
                    ["on"] = s.RadioPreferences[o.Id] > 0,
                    ["seconds"] = s.RadioPreferences[o.Id] > 0 ? s.RadioPreferences[o.Id] : o.DefaultSeconds,
                    ["minus"] = s.RadioPreferences.Minus(o.Id),
                    ["plus"] = s.RadioPreferences.Plus(o.Id),
                })),
            };
        }

        private void PushLive()
        {
            var gs = CrewChief.currentGameState;
            var tick = service.Memory.LastTickUtc;
            bool connected = gs != null && tick != DateTime.MinValue && (DateTime.UtcNow - tick).TotalSeconds < 5;
            string source = GameDataConnectionStatus.SourceAt(DateTime.UtcNow);
            var o = new JObject { ["type"] = "live", ["connected"] = connected, ["audioLevels"] = AudioLevels(),
                ["sourceStatus"] = !connected && source != null ? source + ": Datenquelle erreichbar" : null,
                ["sourceHint"] = "Noch keine gültigen Fahrdaten. Die Simulation kann im Menü, in der Garage oder pausiert sein." };
            if (MainWindow.instance != null)
            {
                o["running"] = MainWindow.instance.IsAppRunning;
                o["game"] = MainWindow.instance.gameDefinitionList.Text;
            }
            o["memoryEnabled"]=service.Settings.PersistentMemory;o["memoryStatus"]=DriverMemory.Status;o["memoryFolder"]=DriverMemory.Folder;
            if (connected)
            {
                var s = gs.SessionData;
                o["game"] = CrewChief.gameDefinition != null ? CrewChief.gameDefinition.friendlyName : "";
                o["track"] = s.TrackDefinition != null ? s.TrackDefinition.name : "";
                o["session"] = SessionName(s.SessionType);
                o["driver"] = service.Settings.RadioName;
                o["carClass"] = gs.carClass != null ? gs.carClass.getClassIdentifier() : "";
                o["lastLap"] = s.LapTimePrevious > 0 ? LapTime(s.LapTimePrevious) : "";
                o["position"] = s.ClassPosition;
                o["cars"] = s.NumCarsInPlayerClass;
                o["timed"] = s.SessionHasFixedTime;
                if (s.SessionHasFixedTime)
                {
                    float left = Math.Max(0, s.SessionTimeRemaining);
                    o["lapValue"] = ((int)left / 60) + ":" + ((int)left % 60).ToString("00");
                    o["lapSub"] = "Runde " + (s.CompletedLaps + 1) + (s.SessionTimeRemaining <= 0 && s.SessionRunningTime > 60 ? " · Uhr abgelaufen" : "");
                }
                else
                {
                    o["lapValue"] = (s.CompletedLaps + 1) + (s.SessionNumberOfLaps > 0 ? " / " + s.SessionNumberOfLaps : "");
                    o["lapSub"] = s.SessionNumberOfLaps > 0 ? Math.Max(0, s.SessionLapsRemaining) + " Runden verbleibend" : "";
                }
                var ahead = RaceDataTools.Neighbour(gs, true);
                var behind = RaceDataTools.Neighbour(gs, false);
                var front = RaceDataTools.ReadGap(gs, ahead, true);
                var rear = RaceDataTools.ReadGap(gs, behind, false);
                if (s.SessionType != SessionType.Race)
                {
                    var comparison = front.HasValue ? front : rear;
                    if (comparison.Kind == RaceGapKind.BestLapDifference)
                    {
                        o["gapValue"] = comparison.Seconds.ToString("0.000", De) + " s";
                        o["gapSub"] = "Bestzeitdifferenz zu " + Name(front.HasValue ? ahead : behind);
                    }
                }
                else if (rear.IsRaceGap && rear.Seconds > 0)
                {
                    o["gapValue"] = (rear.IsEstimated ? "≈ " : "+") + rear.Seconds.ToString("0.0", De) + " s";
                    o["gapSub"] = "auf " + Name(behind) + (rear.IsEstimated ? " · Näherung" : "") +
                        (front.IsRaceGap && front.Seconds > 0 ? " · vorne " + Name(ahead) + (front.IsEstimated ? " ungefähr " : " ") + front.Seconds.ToString("0.0", De) + " s" : "");
                }
                else if (front.IsRaceGap && front.Seconds > 0)
                {
                    o["gapValue"] = (front.IsEstimated ? "≈ " : "−") + front.Seconds.ToString("0.0", De) + " s";
                    o["gapSub"] = "auf " + Name(ahead) + " vor dir" + (front.IsEstimated ? " · Näherung" : "");
                }
            }

            var now = DateTime.UtcNow;
            var radio = service.Memory.GetRadio(TimeSpan.FromHours(3), now);
            var list = new JArray();
            foreach (var r in radio.Skip(Math.Max(0, radio.Count - 6)))
            {
                int colon = r.Text.IndexOf(": ", StringComparison.Ordinal);
                list.Add(new JObject
                {
                    ["time"] = r.TimeUtc.ToLocalTime().ToString("HH:mm"),
                    ["member"] = colon > 0 ? r.Text.Substring(0, colon) : "",
                    ["text"] = colon > 0 ? r.Text.Substring(colon + 2) : r.Text,
                });
            }
            o["radio"] = list;
            var last = radio.LastOrDefault();
            if (last != null && (now - last.TimeUtc).TotalSeconds < 6)
            {
                int colon = last.Text.IndexOf(": ", StringComparison.Ordinal);
                if (colon > 0) o["speaking"] = last.Text.Substring(0, colon).ToLowerInvariant();
            }
            var readiness = VoiceReadiness();
            o["voiceReadiness"] = new JObject { ["ready"] = readiness.Ready, ["reason"] = readiness.Reason, ["message"] = readiness.Message };
            o["warning"] = Warning(readiness);
            o["recorder"] = LiveDiagnostics.Enabled;
            o["recorderStatus"] = "Diagnose: " + LiveDiagnostics.Status;
            Post(o);
        }

        private CrewVoiceReadiness.Result VoiceReadiness()
        {
            var s = service.Settings;
            if (s.Enabled && s.SaraEnabled && (DateTime.UtcNow - saraPackCheckedUtc).TotalSeconds > 10)
            {
                saraPackCurrent = service.SaraSpotterPackCurrent;
                saraPackCheckedUtc = DateTime.UtcNow;
            }
            var user = UserSettings.GetUserSettings();
            return CrewVoiceReadiness.Evaluate(s, saraPackCurrent, user.getString("spotter_name"),
                NoisyCartesianCoordinateSpotter.LoadedCarLeftFolder, CrewVoiceReadiness.LoadedSaraVoiceKey, user.getBoolean("enable_spotter"), Audio.AudioPlayer.playWithNAudio);
        }

        /// <summary>Readiness includes the actually loaded local Sara identity, independently of cloud availability.</summary>
        private JToken Warning(CrewVoiceReadiness.Result readiness)
        {
            var s = service.Settings;
            if (!s.Enabled)
                return new JObject { ["text"] = "Das KI-Team ist in den Crew-Chief-Eigenschaften ausgeschaltet (\"KI-Team aktivieren\")." };
            if (!readiness.Ready)
            {
                var warning = new JObject { ["text"] = readiness.Message };
                if (readiness.Reason == "pack_missing_or_mismatched")
                { warning["cmd"] = "saraPack"; warning["button"] = "Sara-Clips vorbereiten"; }
                else if (readiness.Reason == "spotter_not_selected" || readiness.Reason == "restart_required" ||
                    readiness.Reason == "spotter_disabled" || readiness.Reason == "interruptible_audio_required")
                { warning["cmd"] = "fixVoices"; warning["button"] = "Beheben und neu starten"; }
                return warning;
            }
            // Prepared local Sara clips continue to work without either cloud key.
            if ((s.BenEnabled || s.MikEnabled) && !service.HasApiKey)
                return new JObject { ["text"] = "Der OpenRouter-Schlüssel fehlt. Ben und Mik können keine KI-Fragen beantworten; vorbereitete lokale Ansagen bleiben verfügbar.", ["cmd"] = "openKeys", ["button"] = "Schlüssel eintragen" };
            if ((s.BenEnabled || s.MikEnabled) && s.TtsProvider == AiTtsProvider.ELEVENLABS && !service.HasElevenLabsKey)
                return new JObject { ["text"] = "Der ElevenLabs-Schlüssel fehlt. Neue Texte können nicht gesprochen werden; vorbereitete Originalclips bleiben verfügbar.", ["cmd"] = "openKeys", ["button"] = "Schlüssel eintragen" };
            return null;
        }

        private void SaveSettings(JObject v, JArray topics)
        {
            var main = MainWindow.instance;
            if (main != null && main.crewChief != null && main.crewChief.running)
            {
                Notice("Crew Chief zuerst stoppen. Modelle und Berichte gelten ab dem nächsten Start.");
                return;
            }
            string fast = ((string)v["fastModel"] ?? "").Trim(), deep = ((string)v["deepModel"] ?? "").Trim();
            if (fast.Length == 0 || deep.Length == 0)
            {
                Notice("Beide Modellnamen müssen ausgefüllt sein.");
                return;
            }
            string fullName = ((string)v["driverName"] ?? "").Trim();
            string nickname = ((string)v["driverNickname"] ?? "").Trim();
            if (fullName.Length == 0 || fullName.Length > 80 || nickname.Length > 40 ||
                fullName.Any(char.IsControl) || nickname.Any(char.IsControl))
            {
                Notice("Bitte einen Fahrernamen (maximal 80 Zeichen) und optional einen Funknamen (maximal 40 Zeichen) eintragen.");
                return;
            }
            var prefs = new AiRadioPreferences();
            foreach (JObject t in topics)
            {
                string id = (string)t["id"];
                if (!AiRadioPreferences.Options.Any(o => o.Id == id)) continue;
                prefs.Set(id, (bool)t["on"] ? Int(t["seconds"], 120) : 0);
                prefs.SetTolerance(id, Int(t["minus"], 7), Int(t["plus"], 8));
            }
            AiRadioMode mode;
            var u = UserSettings.GetUserSettings();
            u.setProperty("ai_team_fast_model", fast);
            u.setProperty("ai_team_deep_model", deep);
            u.setProperty("ai_team_stt_model", ((string)v["sttModel"] ?? "").Trim());
            u.setProperty("ai_team_elevenlabs_model", ((string)v["ttsModel"] ?? "").Trim());
            u.setProperty("ai_team_reasoning_effort", ((string)v["reasoning"] ?? "none").Trim());
            u.setProperty("ai_team_max_calls_per_session", Math.Max(0, Int(v["callLimit"], 300)));
            u.setProperty("ai_team_timeout_seconds", Math.Max(3, Math.Min(60, Int(v["timeout"], 12))));
            if (Enum.TryParse((string)v["radioMode"], out mode)) u.setProperty("ai_team_radio_mode_listprop", mode.ToString());
            u.setProperty("ai_team_driver_name", fullName);
            u.setProperty("ai_team_driver_nickname", nickname);
            prefs.Save();
            service.ReloadSettings();
            Post(BuildInit());
            Notice("Gespeichert. Die Werte gelten ab dem nächsten Start von Crew Chief.");
        }

        private void SaveKey(string provider, string key)
        {
            key = (key ?? "").Trim();
            if (provider == "elevenlabs")
            {
                if (key.StartsWith("sk-or-")) { Notice("Das ist ein OpenRouter-Schlüssel (beginnt mit sk-or-), der gehört ins obere Feld."); return; }
                AiSecretStore.Save(key, AiSecretStore.ElevenLabsPath);
                Notice("ElevenLabs-Schlüssel gespeichert, lade die Stimmen …");
                service.PrewarmSaraPhrases();
                LoadVoices(true);
            }
            else
            {
                if (key.StartsWith("sk_") && !key.StartsWith("sk-")) { Notice("Das sieht nach einem ElevenLabs-Schlüssel aus (beginnt mit sk_), der gehört ins untere Feld."); return; }
                AiSecretStore.Save(key);
                Notice("OpenRouter-Schlüssel gespeichert, prüfe ihn …");
                Run(service.TestConnectionAsync().ContinueWith(t =>
                {
                    string result = t.Exception == null ? t.Result : t.Exception.GetBaseException().Message;
                    return result.StartsWith("OK") ? "Schlüssel funktioniert. " + result : AiTeamForm.ExplainKeyError("OpenRouter", result);
                }));
            }
            Post(BuildInit());
        }

        private void LoadVoices(bool loud)
        {
            if (!service.HasElevenLabsKey)
            {
                if (loud) Notice("Erst den ElevenLabs-Schlüssel speichern.");
                return;
            }
            if (loud) Notice("Lade die Stimmen …");
            service.ListElevenLabsVoicesAsync().ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    Notice(AiTeamForm.ExplainKeyError("ElevenLabs", t.Exception.GetBaseException().Message));
                    return;
                }
                var current = new JObject();
                foreach (string id in MemberIds) current[id] = service.Settings.ElevenLabsVoiceFor(Member(id));
                Post(new JObject
                {
                    ["type"] = "voices",
                    ["list"] = new JArray(t.Result.Select(v => new JObject { ["id"] = v.Id, ["name"] = v.ToString() })),
                    ["current"] = current,
                });
                if (loud) Notice(t.Result.Count + " Stimmen geladen. Auswahl wird sofort gespeichert, mit „Probe“ anhören.");
            });
        }

        private void SaveVoice(TeamMember member, string voiceId)
        {
            var main = MainWindow.instance;
            if (main != null && main.crewChief != null && main.crewChief.running)
            { Notice("Crew Chief zuerst stoppen. Stimmen werden vor der nächsten Fahrt geändert."); return; }
            if (string.IsNullOrWhiteSpace(voiceId) || voiceId == service.Settings.ElevenLabsVoiceFor(member)) return;
            SaveProperty(member == TeamMember.Mik ? "ai_team_elevenlabs_voice_mik" : member == TeamMember.Sara ? "ai_team_elevenlabs_voice_sara" : "ai_team_elevenlabs_voice_ben", voiceId);
            saraPackCheckedUtc = DateTime.MinValue;
            Notice(TeamRouter.DisplayName(member) + ": Stimme gespeichert." + (member == TeamMember.Sara ? " Jetzt „Sara als Spotter erzeugen“ drücken." : ""));
        }

        private static JObject AudioLevels()
        {
            var user = UserSettings.GetUserSettings();
            float messages = MainWindow.currentMessageVolume >= 0 ? MainWindow.currentMessageVolume : user.getFloat("messages_volume");
            return new JObject { ["messages"] = Percent(messages), ["background"] = Percent(user.getFloat("background_volume")) };
        }

        private static int Percent(float value)
        { return float.IsNaN(value) || float.IsInfinity(value) ? 0 : (int)Math.Round(Math.Max(0, Math.Min(1, value)) * 100); }

        private void SetAudioLevel(string channel, JToken value, bool persist)
        {
            int percent;
            if ((channel != "messages" && channel != "background") || value == null ||
                value.Type != JTokenType.Integer || !int.TryParse(value.ToString(), out percent) || percent < 0 || percent > 100)
            { Notice("Lautstärke muss zwischen 0 und 100 Prozent liegen."); return; }
            float volume = percent / 100f;
            var main = MainWindow.instance;
            var user = UserSettings.GetUserSettings();
            if (channel == "messages")
            {
                if (main != null) main.updateMessagesVolume(volume, persist, true);
                else { MainWindow.currentMessageVolume = volume; user.setProperty("messages_volume", volume); if (persist) user.saveUserSettings(); }
                AiAudio.RefreshDirectVolume();
            }
            else if (main != null) main.updateBackgroundVolume(volume, persist, true);
            else { user.setProperty("background_volume", volume); if (persist) user.saveUserSettings(); }
            Post(new JObject { ["type"] = "audioLevels", ["audioLevels"] = AudioLevels() });
        }

        private void TestMikNumbers(bool spelled)
        {
            if (MainWindow.instance != null && MainWindow.instance.IsAppRunning)
            { Notice("Für die Zahlenprobe Crew Chief zuerst stoppen."); return; }
            string text = spelled
                ? "Zahlenprobe. Rundenzeit: eine Minute zweiundfünfzig Komma drei zwei acht. Abstand: null Komma sieben Sekunden. Reifendruck: sechsundzwanzig Komma fünf PSI. Öltemperatur: einhundertzwei Grad."
                : "Zahlenprobe. Rundenzeit: 1:52,328. Abstand: 0,7 Sekunden. Reifendruck: 26,5 PSI. Öltemperatur: 102 Grad.";
            Run(service.SpeakAsync(TeamMember.Mik, text, true, CancellationToken.None).ContinueWith(t => t.Exception == null ? null : t.Exception.GetBaseException().Message));
        }

        private void TestVoice(TeamMember member)
        {
            string text = member == TeamMember.Ben ? "Ben hier. Ich hab die Strategie im Blick und melde mich, wenn sich was ergibt."
                : member == TeamMember.Sara ? "Sara hier. Ich hab die Seiten im Blick."
                : "Mik hier. Funk steht, ich hab das Auto im Blick.";
            Run(service.SpeakAsync(member, text, true, CancellationToken.None).ContinueWith(t => t.Exception == null ? null : t.Exception.GetBaseException().Message));
        }

        private void SaveProperty(string name, object value)
        {
            var main = MainWindow.instance;
            if (main != null && main.crewChief != null && main.crewChief.running)
            { Notice("Crew Chief zuerst stoppen. Audio- und Mikrofoneinstellungen gelten ab der nächsten Fahrt."); return; }
            UserSettings.GetUserSettings().setProperty(name, value);
            UserSettings.GetUserSettings().saveUserSettings();
            service.ReloadSettings();
            saraPackCheckedUtc = DateTime.MinValue;
        }

        private void OnStatus(string text)
        {
            Notice(text);
        }

        private void Run(Task<string> task)
        {
            task.ContinueWith(t => Notice(t.Exception == null ? t.Result : "Fehler: " + t.Exception.GetBaseException().Message));
        }

        private void Notice(string text)
        {
            if (!string.IsNullOrEmpty(text)) Post(new JObject { ["type"] = "notice", ["text"] = text });
        }

        private void Post(JObject message)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(() => Post(message))); } catch (Exception) { }
                return;
            }
            if (web.CoreWebView2 != null) web.CoreWebView2.PostWebMessageAsJson(message.ToString(Formatting.None));
        }

        private static TeamMember Member(string id)
        {
            return id == "mik" ? TeamMember.Mik : id == "sara" ? TeamMember.Sara : TeamMember.Ben;
        }

        private static int Int(JToken token, int fallback)
        {
            int n;
            return token != null && int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : fallback;
        }

        private static string Name(OpponentData op)
        {
            return op == null || string.IsNullOrEmpty(op.DriverRawName) ? "?" : op.DriverRawName;
        }

        private static string LapTime(float seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return (int)t.TotalMinutes + ":" + t.Seconds.ToString("00") + "," + t.Milliseconds.ToString("000");
        }

        private static string SessionName(SessionType type)
        {
            switch (type)
            {
                case SessionType.Race: return "Rennen";
                case SessionType.Qualify: return "Qualifying";
                case SessionType.Practice: return "Training";
                case SessionType.HotLap: return "Hotlap";
                default: return "";
            }
        }

        private static string Version()
        {
            try
            {
                string file = Path.Combine(Application.StartupPath, "VERSION.txt");
                var lines = File.Exists(file) ? File.ReadAllLines(file) : new string[0];
                var commit = lines.FirstOrDefault(l => l.StartsWith("Commit:"));
                var version = lines.FirstOrDefault(l => l.StartsWith("Version:"));
                string label = version == null ? "Vorschau" : version.Substring(8).Trim();
                if (commit != null) return label + " · " + commit.Substring(7).Trim().Substring(0, 7);
                if (version != null) return label;
            }
            catch (Exception) { }
            return "Vorschau";
        }
    }
}
