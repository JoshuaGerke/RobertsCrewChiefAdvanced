using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// KI-Orchestrator für Ben, Mik und Sara.
    ///
    /// Datenfluss: Funktaste → Mikrofon → Spracherkennung → <see cref="TeamRouter"/> →
    /// (Sara: <see cref="SaraLocal"/>, einfache Fragen: <see cref="LocalAnswers"/>, sonst KI mit <see cref="RaceDataTools"/>)
    /// → Sprachausgabe → Crew-Chief-AudioPlayer (Immediate-Queue, Spotter unterbricht jederzeit).
    ///
    /// Der Crew-Chief-Spotter selbst bleibt unverändert und hängt an keiner Stelle von hier ab.
    /// </summary>
    public class AiTeamService
    {
        private static readonly Lazy<AiTeamService> instance = new Lazy<AiTeamService>(() => new AiTeamService());
        public static AiTeamService Instance { get { return instance.Value; } }

        public readonly AiUsageStats Stats = new AiUsageStats();
        public readonly AiRaceMemory Memory = new AiRaceMemory();
        public readonly CornerCoach Corners = new CornerCoach();
        private readonly FuelWatch fuelWatch = new FuelWatch();
        private readonly IncidentWatch incidentWatch = new IncidentWatch();
        private readonly FlagWatch flagWatch = new FlagWatch();
        private string cornerTrackKey;

        /// <summary>Wird vom Fenster angezeigt (letzte Erkennung, letzte Antwort, Fehler).</summary>
        public event Action<string> StatusChanged;

        private AiTeamSettings settings = AiTeamSettings.Load();
        private OpenAiCompatibleClient client;
        private ElevenLabsClient elevenLabs;
        private AudioPlayer audioPlayer;
        private readonly AiMicRecorder recorder = new AiMicRecorder();
        private readonly AiPushToTalk pushToTalk;
        private readonly SemaphoreSlim requestLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource currentRequest;
        private int pressSequence;

        private readonly object dialogSync = new object();
        private readonly List<JObject> dialog = new List<JObject>();
        private TeamMember? lastMember;
        private DateTime lastExchangeUtc = DateTime.MinValue;
        private int dialogSessionId = int.MinValue;

        private int callsThisSession;
        private DateTime lastProactiveUtc = DateTime.MinValue;
        private int lastProactiveLap = -1;
        private DateTime lastOverlapUtc = DateTime.MinValue;
        private volatile bool proactiveRunning;
        private int briefedSession = int.MinValue;
        private int debriefedSession = int.MinValue;
        private DateTime sessionSeenUtc = DateTime.MinValue;

        private readonly Dictionary<string, byte[]> phraseCache = new Dictionary<string, byte[]>();

        private AiTeamService()
        {
            pushToTalk = new AiPushToTalk(ControllerHeld, OnPushToTalkPressed, OnPushToTalkReleased);
            ApplySettings(settings);
        }

        // ---------------- Lebenszyklus ----------------

        public void Attach(AudioPlayer player)
        {
            audioPlayer = player;
            Console.WriteLine("[KI-Team] " + (settings.Enabled
                ? "Ben, Mik und Sara bereit. Funktaste: Controller-Aktion \"KI-Team anfunken\"" + (string.IsNullOrEmpty(settings.PttKey) ? "" : " oder Taste " + settings.PttKey) +
                  ". API-Schlüssel: " + (HasApiKey ? "hinterlegt" : "fehlt (Menü KI-Team)") + "."
                : "deaktiviert (Einstellung \"KI-Team aktivieren\")."));
            if (settings.Enabled)
            {
                pushToTalk.Start();
                if (HasApiKey || elevenLabs.HasKey)
                {
                    PrewarmSaraPhrases();
                    if (!SaraSpotterPackCurrent && (elevenLabs.HasKey || HasApiKey))
                    {
                        Task.Run(() => GenerateSaraSpotterPackAsync(null));
                    }
                }
            }
        }

        public AiTeamSettings Settings { get { return settings; } }

        public void ReloadSettings()
        {
            ApplySettings(AiTeamSettings.Load());
        }

        private void ApplySettings(AiTeamSettings newSettings)
        {
            settings = newSettings;
            // "Nur noch das KI-Team spricht": Crew Chiefs eigene Ansagen nicht mehr einreihen (Spotter läuft separat weiter)
            AudioPlayer.messageFilter = settings.Enabled && settings.ReplaceCrewChiefVoice
                ? new Func<QueuedMessage, bool>(m => CrewChiefVoiceFilter.Allow(m.messageName, m.messageFolders))
                : null;
            client = new OpenAiCompatibleClient(settings.BaseUrl, () => AiSecretStore.Load(), TimeSpan.FromSeconds(settings.TimeoutSeconds), Stats);
            elevenLabs = new ElevenLabsClient(AiSecretStore.LoadElevenLabs, TimeSpan.FromSeconds(settings.TimeoutSeconds), Stats);
            pushToTalk.SetKey(settings.PttKey);
            lock (phraseCache)
            {
                phraseCache.Clear();
            }
            if (settings.Enabled && audioPlayer != null)
            {
                pushToTalk.Start();
            }
            else if (!settings.Enabled)
            {
                pushToTalk.Stop();
            }
        }

        public bool HasApiKey { get { return !string.IsNullOrEmpty(AiSecretStore.Load()); } }

        /// <summary>Pro Telemetrie-Tick aus dem Crew-Chief-Eventsystem (AiTeamEvent).</summary>
        public void OnTick(GameStateData previous, GameStateData current)
        {
            if (!settings.Enabled || current == null)
            {
                return;
            }
            DateTime now = DateTime.UtcNow;
            int session = current.SessionData.SessionId;
            if (session != Memory.SessionId)
            {
                Interlocked.Exchange(ref callsThisSession, 0);
                lastProactiveLap = -1;
                fuelWatch.Reset();
                incidentWatch.Reset();
                flagWatch.Reset();
                cornerTrackKey = null;
                sessionSeenUtc = now;
            }
            UpdateCornerTrack(current);
            bool lapCompleted = Memory.Update(previous, current, now);
            if (lapCompleted)
            {
                var lap = Memory.GetLaps(1).LastOrDefault();
                Corners.CompleteLap(current.SessionData.CompletedLaps, current.SessionData.LapTimePrevious,
                    current.SessionData.PreviousLapWasValid && (lap == null || !lap.InPitLap));
            }
            Corners.Tick(current.PositionAndMotionData.DistanceRoundTrack, current.PositionAndMotionData.CarSpeed,
                current.ControlData.ThrottlePedal, current.ControlData.BrakePedal, current.SessionData.SessionRunningTime, current.PitData.InPitlane,
                current.TransmissionData.Gear);
            if (Spotter().HasOverlap)
            {
                lastOverlapUtc = now;
            }
            if (lapCompleted)
            {
                CheckFuel(current);
            }
            if (settings.ReplaceCrewChiefVoice)
            {
                foreach (var flagCall in flagWatch.Evaluate(previous, current, now))
                {
                    string call = flagCall;
                    Log("Flagge Sara: " + call);
                    Task.Run(() => SpeakAsync(TeamMember.Sara, call, true, CancellationToken.None));
                }
            }
            foreach (var incident in incidentWatch.Evaluate(previous, current, now, settings.RadioMode == AiRadioMode.MINIMAL))
            {
                var radio = incident;
                Log("Zwischenfall " + TeamRouter.DisplayName(radio.Key) + ": " + radio.Value);
                Task.Run(() => SpeakAsync(radio.Key, radio.Value, true, CancellationToken.None));
            }
            MaybeSessionBriefing(previous, current, now);

            string trigger = null;
            if (previous != null)
            {
                if (!previous.PitData.CarInFrontIsPitting && current.PitData.CarInFrontIsPitting)
                {
                    trigger = "Das Auto direkt vor uns ist gerade an die Box gefahren.";
                }
                else if (!previous.PitData.CarBehindIsPitting && current.PitData.CarBehindIsPitting)
                {
                    trigger = "Das Auto direkt hinter uns ist gerade an die Box gefahren.";
                }
            }
            if (trigger == null && lapCompleted)
            {
                string cornerLoss = Corners.FindConsistentLoss();
                if (cornerLoss != null)
                {
                    // eindeutiger Befund aus der Kurvenmessung: Ben soll einen konkreten Fahrtipp geben
                    MaybeProactive(current, "Kurvenanalyse: " + cornerLoss + " Ben soll dazu einen kurzen, konkreten Fahrtipp geben.", false);
                    return;
                }
                trigger = "Runde " + current.SessionData.CompletedLaps + " abgeschlossen.";
            }
            if (trigger != null)
            {
                MaybeProactive(current, trigger, lapCompleted);
            }
        }

        /// <summary>Kurven der Strecke aus den Crew-Chief-Streckendaten übernehmen (Geraden werden ausgelassen).</summary>
        private void UpdateCornerTrack(GameStateData gs)
        {
            var track = gs.SessionData.TrackDefinition;
            if (track == null || track.trackLandmarks == null || track.trackLength <= 0)
            {
                return;
            }
            string key = track.name + "|" + track.trackLandmarks.Count + "|" + gs.SessionData.SessionId;
            if (key == cornerTrackKey)
            {
                return;
            }
            cornerTrackKey = key;
            var corners = new List<CornerDefinition>();
            for (int i = 0; i < track.trackLandmarks.Count; i++)
            {
                var lm = track.trackLandmarks[i];
                if (lm.isStraight)
                {
                    continue;
                }
                string raw = lm.landmarkNames != null && lm.landmarkNames.Count > 0 ? lm.landmarkNames[0] : null;
                corners.Add(new CornerDefinition { Name = CornerCoach.FriendlyName(raw, corners.Count), Start = lm.distanceRoundLapStart, End = lm.distanceRoundLapEnd });
            }
            Corners.SetTrack(corners, track.trackLength);
            Log("Kurvenanalyse: " + corners.Count + " Kurven für " + track.name);
        }

        /// <summary>Feste Sprit-Warnung (ohne KI), nur im Rennen.</summary>
        private void CheckFuel(GameStateData gs)
        {
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Green || !gs.FuelData.FuelUseActive)
            {
                return;
            }
            var laps = Memory.GetLaps();
            if (laps.Count(l => l.FuelUsed > 0 && !l.InPitLap) < 2)
            {
                return; // erst nach zwei sauber gemessenen Runden
            }
            float perLap = Memory.AverageFuelPerLap();
            float lapsRemaining;
            bool fromTime = false;
            if (!s.SessionHasFixedTime && s.SessionLapsRemaining > 0 && s.SessionLapsRemaining < 1000)
            {
                lapsRemaining = s.SessionLapsRemaining;
            }
            else
            {
                var timed = laps.Where(l => l.Valid && l.LapTime > 0 && !l.InPitLap).Reverse().Take(3).ToList();
                if (timed.Count == 0 || s.SessionTimeRemaining <= 0)
                {
                    return;
                }
                // nach Ablauf der Zeit wird die angefangene Runde noch zu Ende gefahren
                lapsRemaining = s.SessionTimeRemaining / timed.Average(l => l.LapTime) + 1;
                fromTime = true;
            }
            string warning = fuelWatch.Evaluate(s.CompletedLaps, gs.FuelData.FuelLeft, perLap, lapsRemaining, gs.PitData.IsRefuellingAllowed, fromTime);
            if (warning != null)
            {
                Log("Sprit-Warnung: " + warning);
                Task.Run(() => SpeakAsync(TeamMember.Mik, warning, true, CancellationToken.None));
            }
        }

        public void OnSessionCleared()
        {
            lock (dialogSync)
            {
                dialog.Clear();
                lastMember = null;
            }
        }

        // ---------------- Funktaste ----------------

        private static bool ControllerHeld()
        {
            var mw = MainWindow.instance;
            return mw != null && mw.controllerConfiguration != null && mw.controllerConfiguration.isActionHeld(ControllerConfiguration.TALK_TO_AI_TEAM, false);
        }

        private void OnPushToTalkPressed()
        {
            if (!settings.Enabled)
            {
                return;
            }
            // Der Fahrer will sprechen: Aufnahme zuerst, dann laufende KI-Anfrage und KI-Funkspruch stoppen
            Interlocked.Increment(ref pressSequence);
            recorder.Start(settings.Microphone);
            var running = currentRequest;
            if (running != null)
            {
                running.Cancel();
            }
            var playing = SoundCache.currentlyPlayingSound;
            if (playing != null && playing.isAiAudio)
            {
                try { SoundCache.InterruptCurrentlyPlayingSound(false); } catch (Exception) { }
            }
            if (audioPlayer != null)
            {
                audioPlayer.playStartListeningBeep();
            }
            Status("Höre zu …");
        }

        private void OnPushToTalkReleased()
        {
            TimeSpan duration;
            byte[] wav = recorder.Stop(out duration);
            if (audioPlayer != null)
            {
                audioPlayer.playListeningEndBeep();
            }
            if (wav == null)
            {
                Status("Funkspruch zu kurz, ignoriert.");
                return;
            }
            DateTime releasedUtc = DateTime.UtcNow;
            int sequence = pressSequence;
            Task.Run(async () =>
            {
                try
                {
                    await HandleSpeechAsync(wav, releasedUtc, sequence).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log("Funkspruch konnte nicht verarbeitet werden: " + e);
                    Status("Fehler: " + e.Message);
                }
            });
        }

        private async Task HandleSpeechAsync(byte[] wav, DateTime releasedUtc, int sequence)
        {
            if (!HasApiKey)
            {
                Status("Kein API-Schlüssel hinterlegt (Menü \"KI-Team\").");
                await SpeakAsync(TeamMember.Ben, "Für die Spracherkennung fehlt noch der API-Schlüssel.", true, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            string text;
            try
            {
                text = await TranscribeWithFallbackAsync(wav).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log("Spracherkennung fehlgeschlagen: " + e.Message);
                Status("Spracherkennung fehlgeschlagen: " + e.Message);
                await SpeakAsync(TeamMember.Ben, SpokenError(e, "Funk war nicht zu verstehen, die Verbindung hakt."), true, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            Log("Erkannt (" + (long)(DateTime.UtcNow - releasedUtc).TotalMilliseconds + " ms): " + text);
            if (sequence != pressSequence)
            {
                Log("Verworfen, der Fahrer hat inzwischen neu gefunkt.");
                return;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                Status("Nichts verstanden.");
                return;
            }
            await AskAsync(text, releasedUtc).ConfigureAwait(false);
        }

        // ---------------- Anfrage bearbeiten ----------------

        /// <summary>Komplette Verarbeitung einer Fahrerfrage als Text (auch für den Testknopf im KI-Team-Fenster).</summary>
        public async Task<string> AskAsync(string transcript, DateTime? askedUtc = null)
        {
            DateTime started = askedUtc ?? DateTime.UtcNow;
            var routed = TeamRouter.Route(transcript, RecentMember());
            Status("Fahrer → " + TeamRouter.DisplayName(routed.Member) + ": " + routed.Question);
            var gs = CrewChief.currentGameState;
            var ctxNow = DateTime.UtcNow;

            // 1. Sara: Sicherheitsfragen ausschließlich lokal
            if (routed.Member == TeamMember.Sara)
            {
                string local = SaraLocal.TryAnswer(routed.Question, Spotter(), gs, ctxNow);
                if (local != null)
                {
                    Stats.AddLocalAnswer();
                    local = await SpeakSaraSpotterAnswerAsync(routed.Question).ConfigureAwait(false);
                    RememberExchange(routed.Member, routed.Question, local);
                    return local;
                }
            }

            // 2. Einfache Standardfragen ohne KI
            string quick = LocalAnswers.TryAnswer(routed.Question, gs, Memory);
            if (quick != null)
            {
                Stats.AddLocalAnswer();
                await SpeakAsync(routed.Member, quick, true, CancellationToken.None).ConfigureAwait(false);
                RememberExchange(routed.Member, routed.Question, quick);
                return quick;
            }

            if (!HasApiKey)
            {
                const string noKey = "Dafür brauch ich die KI, aber es ist noch kein API-Schlüssel hinterlegt.";
                await SpeakAsync(routed.Member, noKey, true, CancellationToken.None).ConfigureAwait(false);
                return noKey;
            }
            if (settings.MaxCallsPerSession > 0 && callsThisSession >= settings.MaxCallsPerSession)
            {
                const string budget = "Das KI-Budget für diese Session ist aufgebraucht. Die lokalen Ansagen laufen weiter.";
                await SpeakAsync(routed.Member, budget, true, CancellationToken.None).ConfigureAwait(false);
                return budget;
            }

            // 3. KI mit Werkzeugen. driverCancel = Fahrer hat neu gefunkt (still verwerfen),
            //    Zeitlimit = KI zu langsam (kurz Bescheid sagen).
            var driverCancel = new CancellationTokenSource();
            var previous = Interlocked.Exchange(ref currentRequest, driverCancel);
            if (previous != null)
            {
                previous.Cancel();
            }
            await requestLock.WaitAsync().ConfigureAwait(false);
            var linked = CancellationTokenSource.CreateLinkedTokenSource(driverCancel.Token);
            linked.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            try
            {
                string answer = await AskModelAsync(routed, gs, linked.Token).ConfigureAwait(false);
                if (driverCancel.IsCancellationRequested)
                {
                    return null;
                }
                if ((DateTime.UtcNow - started).TotalSeconds > 25)
                {
                    Log("Antwort verworfen, zu alt: " + answer);
                    return null;
                }
                await SpeakAsync(routed.Member, answer, true, driverCancel.Token).ConfigureAwait(false);
                RememberExchange(routed.Member, routed.Question, answer);
                Log("Gesamtlatenz Frage → Funkspruch eingereiht: " + (long)(DateTime.UtcNow - started).TotalMilliseconds + " ms");
                return answer;
            }
            catch (Exception e)
            {
                if (driverCancel.IsCancellationRequested)
                {
                    return null;
                }
                Log("KI-Anfrage fehlgeschlagen: " + e.Message);
                Status("KI-Fehler: " + e.Message);
                string fallback = SpokenError(e, "Ich krieg gerade keine Antwort von der Auswertung. Versuch's gleich nochmal.");
                await SpeakAsync(routed.Member, fallback, true, CancellationToken.None).ConfigureAwait(false);
                return fallback;
            }
            finally
            {
                requestLock.Release();
                Interlocked.CompareExchange(ref currentRequest, null, driverCancel);
                linked.Dispose();
            }
        }

        private async Task<string> AskModelAsync(RoutedRequest routed, GameStateData gs, CancellationToken ct)
        {
            var ctx = BuildContext(gs);
            bool longAllowed = gs == null || gs.SessionData.SessionPhase == SessionPhase.Garage ||
                (gs.PitData.InPitlane && gs.PositionAndMotionData.CarSpeed < 1);
            string model = NeedsDeepModel(routed.Question) ? settings.DeepModel : settings.FastModel;

            var messages = new JArray
            {
                new JObject
                {
                    ["role"] = "system",
                    ["content"] = AiPersonas.SystemPrompt(routed.Member, settings.DriverName, longAllowed) +
                        "\n\nAktuelle Lage (Live-Daten mit Reifen, Sprit, direkten Gegnern und Box). Reicht sie für die Antwort, antworte direkt " +
                        "ohne Werkzeug. Werkzeuge nur für Details, die hier fehlen (Stintverlauf, Rundenzeiten, Schäden/Motor, Wetter, Ereignisse):\n" +
                        RaceDataTools.BuildBriefing(ctx).ToString(Newtonsoft.Json.Formatting.None),
                },
            };
            foreach (var turn in DialogHistory(gs))
            {
                messages.Add(turn);
            }
            messages.Add(new JObject { ["role"] = "user", ["content"] = "[an " + TeamRouter.DisplayName(routed.Member) + "] " + routed.Question });

            return await RunToolLoopAsync(client, model, settings.ReasoningEffort, messages, longAllowed ? 600 : 250, longAllowed ? 600 : 260,
                () => BuildContext(CrewChief.currentGameState), () => Interlocked.Increment(ref callsThisSession), Log, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Tool-Calling-Schleife: Modell fragt Werkzeuge an, wir liefern frische Telemetrie, bis eine Textantwort kommt (max. 4 Runden).
        /// </summary>
        public static async Task<string> RunToolLoopAsync(OpenAiCompatibleClient client, string model, string reasoningEffort, JArray messages,
            int maxTokens, int maxChars, Func<RaceDataContext> contextProvider, Action countCall, Action<string> log, CancellationToken ct)
        {
            JArray tools = RaceDataTools.BuildToolDefinitions();
            for (int round = 0; round < 4; round++)
            {
                if (countCall != null)
                {
                    countCall();
                }
                var sw = Stopwatch.StartNew();
                JObject response = await client.ChatAsync(model, messages, tools, reasoningEffort, maxTokens, false, ct).ConfigureAwait(false);
                var message = response.SelectToken("choices[0].message") as JObject;
                if (message == null)
                {
                    throw new AiApiException("Leere Antwort vom Modell.");
                }
                var toolCalls = message["tool_calls"] as JArray;
                if (log != null)
                {
                    log("Modell " + model + " Runde " + (round + 1) + ": " + sw.ElapsedMilliseconds + " ms" + (toolCalls != null ? ", Werkzeuge: " +
                        string.Join(", ", toolCalls.Select(t => (string)t.SelectToken("function.name"))) : ""));
                }
                if (toolCalls == null || toolCalls.Count == 0)
                {
                    string text = AiPersonas.CleanForRadio(ContentText(message["content"]), maxChars, true);
                    return string.IsNullOrWhiteSpace(text) ? "Dazu hab ich gerade keine belastbare Aussage." : text;
                }
                messages.Add(new JObject { ["role"] = "assistant", ["content"] = message["content"], ["tool_calls"] = toolCalls });
                RaceDataContext ctx = contextProvider();
                foreach (var call in toolCalls)
                {
                    string name = (string)call.SelectToken("function.name");
                    JObject result = RaceDataTools.Execute(name, ctx);
                    messages.Add(new JObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = (string)call["id"],
                        ["content"] = result.ToString(Newtonsoft.Json.Formatting.None),
                    });
                }
            }
            return "Ich komm mit der Auswertung gerade nicht zu einem sauberen Ergebnis.";
        }

        /// <summary>content ist meist ein String, manche kompatiblen Server liefern aber eine Liste von Textteilen.</summary>
        public static string ContentText(JToken content)
        {
            if (content == null || content.Type == JTokenType.Null)
            {
                return null;
            }
            if (content.Type == JTokenType.Array)
            {
                return string.Join(" ", content.Select(part => part.Type == JTokenType.Object ? (string)part["text"] : (string)part).Where(t => !string.IsNullOrEmpty(t)));
            }
            return content.Type == JTokenType.String ? (string)content : content.ToString();
        }

        public const string FallbackSttModel = "openai/whisper-large-v3-turbo";

        /// <summary>
        /// Spracherkennung mit dem eingestellten Modell. Lehnt der Anbieter das Modell ab (nicht wegen Schlüssel,
        /// Guthaben oder Netz), einmal mit einem Whisper-Modell wiederholen.
        /// </summary>
        private async Task<string> TranscribeWithFallbackAsync(byte[] wav)
        {
            const string hint = "Rennfunk an die Boxencrew Ben, Mik und Sara. Simracing, Reifen, Sprit, Boxenstopp, Undercut.";
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds)))
                {
                    return await client.TranscribeAsync(wav, settings.SttModel, "de", hint, cts.Token).ConfigureAwait(false);
                }
            }
            catch (AiApiException e) when (e.Status.HasValue && (int)e.Status.Value >= 400 && (int)e.Status.Value != 401 &&
                (int)e.Status.Value != 402 && (int)e.Status.Value != 403 && settings.SttModel != FallbackSttModel && settings.BaseUrl.Contains("openrouter.ai"))
            {
                Log("Spracherkennung mit " + settings.SttModel + " abgelehnt (" + e.Message + "), versuche " + FallbackSttModel);
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds)))
                {
                    return await client.TranscribeAsync(wav, FallbackSttModel, "de", hint, cts.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Was Ben bei einem Fehler sagt: die echte Ursache statt pauschal "Verbindung hakt".</summary>
        public static string SpokenError(Exception e, string connectionMessage)
        {
            var api = e as AiApiException;
            int status = api != null && api.Status.HasValue ? (int)api.Status.Value : 0;
            string msg = e.Message ?? "";
            if (status == 401 || (msg.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 && msg.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Der OpenRouter-Schlüssel wird nicht angenommen. Trag ihn bitte im KI-Team-Fenster neu ein.";
            }
            if (status == 402 || msg.IndexOf("credit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Bei OpenRouter ist kein Guthaben mehr drauf. Bitte aufladen.";
            }
            if (status == 403)
            {
                return "OpenRouter verweigert die Anfrage. Schau bitte ins KI-Team-Fenster.";
            }
            if (status == 429)
            {
                return "OpenRouter bremst gerade, zu viele Anfragen. Gleich nochmal versuchen.";
            }
            if (status >= 400 && status < 500)
            {
                return "Die Anfrage wurde abgelehnt, da stimmt eine Einstellung nicht. Details stehen im KI-Team-Fenster.";
            }
            return connectionMessage;
        }

        public static bool NeedsDeepModel(string question)
        {
            string q = (question ?? "").ToLowerInvariant();
            string[] markers = { "analys", "strategie", "undercut", "overcut", "warum", "wieso", "setup", "vergleich", "stint", "entscheide", "prognose", "erklär" };
            return markers.Any(m => q.Contains(m)) || q.Split(' ').Length > 16;
        }

        // ---------------- proaktive Funksprüche ----------------

        private void MaybeProactive(GameStateData gs, string trigger, bool isLapTrigger)
        {
            if (settings.RadioMode == AiRadioMode.MINIMAL || proactiveRunning || !HasApiKey)
            {
                return;
            }
            var s = gs.SessionData;
            DateTime now = DateTime.UtcNow;
            bool racing = s.SessionPhase == SessionPhase.Green && s.SessionType != SessionType.Unavailable;
            // im Rennen erst nach der hektischen ersten Runde, im Training schon nach der ersten Runde
            int minLaps = s.SessionType == SessionType.Race ? 2 : 1;
            if (!racing || s.CompletedLaps < minLaps || gs.PitData.InPitlane || Spotter().HasOverlap || (now - lastOverlapUtc).TotalSeconds < 8)
            {
                return;
            }
            if (settings.MaxCallsPerSession > 0 && callsThisSession >= settings.MaxCallsPerSession * 0.8)
            {
                return; // Rest des Budgets für Fragen des Fahrers aufheben
            }
            int lapInterval = settings.RadioMode == AiRadioMode.ACTIVE ? 1 : 2;
            double minSeconds = settings.RadioMode == AiRadioMode.ACTIVE ? 40 : 75;
            if (isLapTrigger && (s.CompletedLaps - lastProactiveLap < lapInterval || (now - lastProactiveUtc).TotalSeconds < minSeconds))
            {
                return;
            }
            if (!isLapTrigger && (now - lastProactiveUtc).TotalSeconds < 30)
            {
                return;
            }
            lastProactiveUtc = now;
            lastProactiveLap = s.CompletedLaps;
            proactiveRunning = true;
            Task.Run(async () =>
            {
                try
                {
                    await RunProactiveAsync(trigger).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log("Proaktiver Funk fehlgeschlagen: " + e.Message);
                }
                finally
                {
                    proactiveRunning = false;
                }
            });
        }

        private async Task RunProactiveAsync(string trigger)
        {
            var ctx = BuildContext(CrewChief.currentGameState);
            if (ctx.GameState == null)
            {
                return;
            }
            var data = new JObject
            {
                ["anlass"] = trigger,
                ["lage"] = RaceDataTools.BuildBriefing(ctx),
                ["stint"] = RaceDataTools.Execute("get_stint_analysis", ctx),
                ["kurven"] = RaceDataTools.Execute("get_corner_analysis", ctx),
                ["ereignisse_und_letzte_funksprueche"] = RaceDataTools.Execute("get_recent_events", ctx),
            };
            var messages = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = string.Format(AiPersonas.ProactivePrompt, settings.DriverName) },
                new JObject { ["role"] = "user", ["content"] = data.ToString(Newtonsoft.Json.Formatting.None) },
            };
            Interlocked.Increment(ref callsThisSession);
            JObject response;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds)))
            {
                response = await client.ChatAsync(settings.FastModel, messages, null, settings.ReasoningEffort, 200, true, cts.Token).ConfigureAwait(false);
            }
            string content = (string)response.SelectToken("choices[0].message.content");
            TeamMember member;
            string text;
            if (!TryParseProactive(content, out member, out text))
            {
                Log("Proaktiv: nichts zu funken (" + trigger + ")");
                return;
            }
            // Funkruhe, solange der Spotter arbeitet
            if (Spotter().HasOverlap)
            {
                Log("Proaktiv verworfen wegen Überlappung: " + text);
                return;
            }
            Log("Proaktiv " + TeamRouter.DisplayName(member) + ": " + text);
            await SpeakAsync(member, text, false, CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>
        /// Begrüßung mit Lagebericht einmal pro Session (ein paar Sekunden nach Sessionbeginn, sobald Daten da sind)
        /// und Nachbesprechung, wenn die Session zu Ende geht. Nicht im Funkmodus "Minimal".
        /// </summary>
        private void MaybeSessionBriefing(GameStateData previous, GameStateData current, DateTime now)
        {
            if (settings.RadioMode == AiRadioMode.MINIMAL || !HasApiKey || proactiveRunning)
            {
                return;
            }
            var s = current.SessionData;
            int session = s.SessionId;
            bool ended = s.SessionPhase == SessionPhase.Checkered || s.SessionPhase == SessionPhase.Finished;
            if (ended && debriefedSession != session && briefedSession == session && s.CompletedLaps >= 2 &&
                previous != null && previous.SessionData.SessionPhase != s.SessionPhase)
            {
                debriefedSession = session;
                RunScripted(AiPersonas.SessionDebriefPrompt, "Nachbesprechung");
                return;
            }
            if (briefedSession == session || ended || s.SessionPhase == SessionPhase.Unavailable || s.SessionType == SessionType.Unavailable)
            {
                return;
            }
            // kurz warten, bis Strecke, Wetter und Reifen in der Telemetrie stehen; nicht mitten im Zweikampf
            if ((now - sessionSeenUtc).TotalSeconds < 4 || string.IsNullOrEmpty(current.SessionData.TrackDefinition != null ? current.SessionData.TrackDefinition.name : null) ||
                Spotter().HasOverlap)
            {
                return;
            }
            briefedSession = session;
            RunScripted(AiPersonas.SessionBriefingPrompt, "Begrüßung");
        }

        private void RunScripted(string prompt, string label)
        {
            proactiveRunning = true;
            lastProactiveUtc = DateTime.UtcNow;
            Task.Run(async () =>
            {
                try
                {
                    var ctx = BuildContext(CrewChief.currentGameState);
                    if (ctx.GameState == null)
                    {
                        return;
                    }
                    var data = new JObject
                    {
                        ["lage"] = RaceDataTools.BuildBriefing(ctx),
                        ["wetter"] = RaceDataTools.Execute("get_track_conditions", ctx),
                        ["rundenzeiten"] = RaceDataTools.Execute("get_lap_times", ctx),
                        ["stint"] = RaceDataTools.Execute("get_stint_analysis", ctx),
                        ["kurven"] = RaceDataTools.Execute("get_corner_analysis", ctx),
                    };
                    var messages = new JArray
                    {
                        new JObject { ["role"] = "system", ["content"] = string.Format(prompt, settings.DriverName) },
                        new JObject { ["role"] = "user", ["content"] = data.ToString(Newtonsoft.Json.Formatting.None) },
                    };
                    Interlocked.Increment(ref callsThisSession);
                    JObject response;
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds)))
                    {
                        response = await client.ChatAsync(settings.FastModel, messages, null, settings.ReasoningEffort, 400, true, cts.Token).ConfigureAwait(false);
                    }
                    foreach (var radio in ParseRadioList(ContentText(response.SelectToken("choices[0].message.content")), 2))
                    {
                        Log(label + " " + TeamRouter.DisplayName(radio.Key) + ": " + radio.Value);
                        await SpeakAsync(radio.Key, radio.Value, false, CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    Log(label + " fehlgeschlagen: " + e.Message);
                }
                finally
                {
                    proactiveRunning = false;
                }
            });
        }

        /// <summary>Liest {"funksprueche":[{"speaker":"ben|mik","text":"…"}]}; Sara und unbekannte Sprecher werden verworfen.</summary>
        public static List<KeyValuePair<TeamMember, string>> ParseRadioList(string content, int max)
        {
            var result = new List<KeyValuePair<TeamMember, string>>();
            if (string.IsNullOrWhiteSpace(content))
            {
                return result;
            }
            try
            {
                var list = JObject.Parse(content)["funksprueche"] as JArray;
                if (list == null)
                {
                    return result;
                }
                foreach (var entry in list.OfType<JObject>())
                {
                    string speaker = ((string)entry["speaker"] ?? "").Trim().ToLowerInvariant();
                    string text = AiPersonas.CleanForRadio((string)entry["text"], 320, true);
                    if (string.IsNullOrWhiteSpace(text) || (speaker != "ben" && speaker != "mik"))
                    {
                        continue;
                    }
                    result.Add(new KeyValuePair<TeamMember, string>(speaker == "mik" ? TeamMember.Mik : TeamMember.Ben, text));
                    if (result.Count >= max)
                    {
                        break;
                    }
                }
            }
            catch (Exception) { }
            return result;
        }

        public static bool TryParseProactive(string content, out TeamMember member, out string text)
        {
            member = TeamMember.Ben;
            text = null;
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }
            try
            {
                var o = JObject.Parse(content);
                string speaker = ((string)o["speaker"] ?? "none").Trim().ToLowerInvariant();
                text = AiPersonas.CleanForRadio((string)o["text"], 200, true);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }
                if (speaker == "ben")
                {
                    member = TeamMember.Ben;
                    return true;
                }
                if (speaker == "mik")
                {
                    member = TeamMember.Mik;
                    return true;
                }
            }
            catch (Exception) { }
            return false;
        }

        // ---------------- Sprachausgabe ----------------

        public Task SpeakAsync(TeamMember member, string text, bool isReply, CancellationToken ct)
        {
            return SpeakAsync(member, text, isReply, ct, null);
        }

        /// <param name="stillValid">Wird direkt vor dem Einreihen geprüft (Sara: Spotterlage neu bewerten). false = nicht abspielen.</param>
        /// <returns>true, wenn der Funkspruch eingereiht wurde.</returns>
        private async Task<bool> SpeakAsync(TeamMember member, string text, bool isReply, CancellationToken ct, Func<string, bool> stillValid)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            // Ohne nAudio kann der Spotter laufende Clips nicht unterbrechen – dann nur kurze Funksprüche
            if (!AudioPlayer.playWithNAudio && text.Length > 160)
            {
                text = AiPersonas.CleanForRadio(text, 160, false);
            }
            Console.WriteLine("[KI-Team] " + TeamRouter.DisplayName(member) + ": " + text);
            Status(TeamRouter.DisplayName(member) + ": " + text);
            byte[] pcm;
            try
            {
                pcm = await RenderAsync(member, text, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                Log("Sprachausgabe fehlgeschlagen: " + e.Message);
                return false;
            }
            if (ct.IsCancellationRequested || audioPlayer == null)
            {
                return false;
            }
            if (stillValid == null)
            {
                // Ben/Mik: während einer Überlappung kurz warten, Sara hat Vorrang (Spotteransagen unterbrechen ohnehin alles)
                for (int i = 0; i < 25 && Spotter().HasOverlap && !ct.IsCancellationRequested; i++)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                }
            }
            else if (!stillValid(text))
            {
                return false;
            }
            byte[] wav = AiAudio.PcmToWav(pcm, AiAudio.OpenAiPcmSampleRate);
            var mw = MainWindow.instance;
            if (mw == null || mw.crewChief == null || !mw.crewChief.running)
            {
                // Crew Chief ist nicht gestartet (kein Spiel): Testausgabe direkt, sonst verwirft die Audioengine den Clip
                try
                {
                    AiAudio.PlayDirect(wav, AudioPlayer.naudioMessagesPlaybackDeviceId);
                }
                catch (Exception e)
                {
                    Log("Direkte Wiedergabe fehlgeschlagen: " + e.Message);
                    return false;
                }
                Memory.AddRadio(member, text, DateTime.UtcNow);
                return true;
            }
            string id = AiAudioStore.Put(wav, TeamRouter.DisplayName(member) + ": " + text);
            var fragments = new List<MessageFragment> { MessageFragment.Text(SoundCache.AI_AUDIO_IDENTIFIER + id) };
            if (isReply)
            {
                // Saras Lagemeldungen verfallen nach 2 s, alles andere nach 30 s
                var message = new QueuedMessage("ai_team_reply_" + id, stillValid != null ? 2 : 30, messageFragments: fragments,
                    type: SoundType.VOICE_COMMAND_RESPONSE, priority: 5);
                audioPlayer.playMessageImmediately(message);
            }
            else
            {
                var message = new QueuedMessage("ai_team_info_" + id, 20, messageFragments: fragments,
                    type: SoundType.REGULAR_MESSAGE, priority: SoundMetadata.DEFAULT_PRIORITY);
                audioPlayer.playMessage(message);
            }
            Memory.AddRadio(member, text, DateTime.UtcNow);
            return true;
        }

        /// <summary>
        /// Saras Spotterantwort: direkt vor dem Abspielen mit frischen Spotterdaten neu bewerten.
        /// Hat sich die Lage geändert, die neue Antwort sprechen; klappt das nicht, nur "keine sicheren Daten".
        /// </summary>
        private async Task<string> SpeakSaraSpotterAnswerAsync(string question)
        {
            Func<string> evaluate = () => SaraLocal.TryAnswer(question, Spotter(), CrewChief.currentGameState, DateTime.UtcNow) ?? SaraLocal.NoDataAnswer;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                string answer = evaluate();
                if (await SpeakAsync(TeamMember.Sara, answer, true, CancellationToken.None, t => t == evaluate()).ConfigureAwait(false))
                {
                    return answer;
                }
            }
            await SpeakAsync(TeamMember.Sara, SaraLocal.NoDataAnswer, true, CancellationToken.None).ConfigureAwait(false);
            return SaraLocal.NoDataAnswer;
        }

        private async Task<byte[]> RenderAsync(TeamMember member, string text, CancellationToken ct)
        {
            string cacheKey = member + "|" + settings.TtsProvider + "|" + text;
            lock (phraseCache)
            {
                byte[] cached;
                if (phraseCache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }
            }
            // Kette: gewählte Quelle → (ElevenLabs fällt aus) OpenAI-kompatible Stimme → Windows-Stimme
            byte[] pcm = null;
            bool usedFallback = false;
            if (settings.TtsProvider == AiTtsProvider.ELEVENLABS && elevenLabs.HasKey)
            {
                pcm = await TryTtsAsync("ElevenLabs", t => elevenLabs.SpeechPcmAsync(settings.ElevenLabsVoiceFor(member), settings.ElevenLabsModel, text, member, t), ct).ConfigureAwait(false);
                usedFallback = pcm == null;
            }
            if (pcm == null && settings.TtsProvider != AiTtsProvider.WINDOWS && HasApiKey)
            {
                pcm = await TryTtsAsync("Cloud-Stimme", t => client.SpeechPcmAsync(settings.TtsModel, settings.VoiceFor(member), text, AiPersonas.VoiceInstructions(member), t), ct).ConfigureAwait(false);
                usedFallback |= settings.TtsProvider == AiTtsProvider.ELEVENLABS && pcm != null;
            }
            if (pcm == null)
            {
                usedFallback = settings.TtsProvider != AiTtsProvider.WINDOWS;
                pcm = WindowsTts.Render(text, settings.WindowsVoiceFor(member), member);
            }
            if (settings.RadioEffect)
            {
                pcm = AiAudio.ApplyRadioEffect(pcm, AiAudio.OpenAiPcmSampleRate);
            }
            // kurze Standardsätze (vor allem Saras) zwischenspeichern
            if (text.Length <= 60 && !usedFallback)
            {
                lock (phraseCache)
                {
                    if (phraseCache.Count > 64)
                    {
                        phraseCache.Clear();
                    }
                    phraseCache[cacheKey] = pcm;
                }
            }
            return pcm;
        }

        private async Task<byte[]> TryTtsAsync(string source, Func<CancellationToken, Task<byte[]>> call, CancellationToken ct)
        {
            try
            {
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                    byte[] pcm = await call(timeout.Token).ConfigureAwait(false);
                    return pcm != null && pcm.Length > 0 ? pcm : null;
                }
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                Log(source + " nicht verfügbar, nehme die nächste Stimmenquelle: " + e.Message);
                return null;
            }
        }

        public bool HasElevenLabsKey { get { return elevenLabs.HasKey; } }

        public Task<List<ElevenLabsVoice>> ListElevenLabsVoicesAsync()
        {
            return elevenLabs.ListVoicesAsync(CancellationToken.None);
        }

        /// <summary>Kennung der aktuell gewählten Sara-Stimme; ändert sie sich, wird das Spotterpack neu erzeugt.</summary>
        public string SaraVoiceKey
        {
            get
            {
                if (settings.TtsProvider == AiTtsProvider.ELEVENLABS && elevenLabs.HasKey)
                {
                    return "elevenlabs|" + settings.ElevenLabsModel + "|" + settings.ElevenLabsVoiceSara + "|radio=" + settings.RadioEffect;
                }
                if (settings.TtsProvider != AiTtsProvider.WINDOWS && HasApiKey)
                {
                    return "cloud|" + settings.TtsModel + "|" + settings.VoiceSara + "|radio=" + settings.RadioEffect;
                }
                return "windows|" + settings.WindowsVoiceSara + "|radio=" + settings.RadioEffect;
            }
        }

        public bool SaraSpotterPackCurrent
        {
            get { return SaraSpotterPack.IsGenerated(SaraSpotterPack.VoiceFolder, SaraVoiceKey); }
        }

        private int saraPackRunning;

        /// <summary>
        /// Erzeugt Saras deutsches Spotterpack (etwa 40 kurze Ansagen) mit ihrer aktuellen Stimme und stellt in Crew Chief
        /// die Spotterstimme auf "Sara". Wirksam nach dem nächsten Neustart von Crew Chief.
        /// Fällt die gewählte Stimme aus, wird abgebrochen statt ein gemischtes Pack zu bauen.
        /// </summary>
        public async Task<string> GenerateSaraSpotterPackAsync(Action<string> progress)
        {
            if (Interlocked.Exchange(ref saraPackRunning, 1) == 1)
            {
                return "Saras Spotterpack wird bereits erzeugt.";
            }
            try
            {
                string key = SaraVoiceKey;
                Func<string, Task<byte[]>> render;
                if (key.StartsWith("elevenlabs|"))
                {
                    render = async text =>
                    {
                        byte[] pcm = await elevenLabs.SpeechPcmAsync(settings.ElevenLabsVoiceSara, settings.ElevenLabsModel, text, TeamMember.Sara, CancellationToken.None).ConfigureAwait(false);
                        return settings.RadioEffect ? AiAudio.ApplyRadioEffect(pcm, AiAudio.OpenAiPcmSampleRate) : pcm;
                    };
                }
                else if (key.StartsWith("cloud|"))
                {
                    render = async text =>
                    {
                        byte[] pcm = await client.SpeechPcmAsync(settings.TtsModel, settings.VoiceSara, text, AiPersonas.VoiceInstructions(TeamMember.Sara), CancellationToken.None).ConfigureAwait(false);
                        return settings.RadioEffect ? AiAudio.ApplyRadioEffect(pcm, AiAudio.OpenAiPcmSampleRate) : pcm;
                    };
                }
                else
                {
                    render = text =>
                    {
                        byte[] pcm = WindowsTts.Render(text, settings.WindowsVoiceSara, TeamMember.Sara);
                        return Task.FromResult(settings.RadioEffect ? AiAudio.ApplyRadioEffect(pcm, AiAudio.OpenAiPcmSampleRate) : pcm);
                    };
                }
                int files = await SaraSpotterPack.GenerateAsync(SaraSpotterPack.VoiceFolder, key, render, progress).ConfigureAwait(false);
                UserSettings.GetUserSettings().setProperty("spotter_name", SaraSpotterPack.SpotterName);
                UserSettings.GetUserSettings().saveUserSettings();
                string done = "Saras deutsches Spotterpack ist fertig (" + files + " Ansagen). Spotterstimme steht auf \"Sara\" – wirksam nach dem nächsten Neustart von Crew Chief.";
                Log(done);
                return done;
            }
            catch (Exception e)
            {
                string failed = "Saras Spotterpack konnte nicht erzeugt werden: " + e.Message;
                Log(failed);
                return failed;
            }
            finally
            {
                Interlocked.Exchange(ref saraPackRunning, 0);
            }
        }

        /// <summary>Testfunktion für das KI-Team-Fenster: ein Mini-Aufruf mit gemessener Latenz.</summary>
        public async Task<string> TestConnectionAsync()
        {
            if (!HasApiKey)
            {
                return "Kein API-Schlüssel hinterlegt.";
            }
            var messages = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = "Antworte nur mit genau diesen Worten: Funk steht." },
                new JObject { ["role"] = "user", ["content"] = "Funkcheck" },
            };
            var sw = Stopwatch.StartNew();
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds)))
                {
                    JObject response = await client.ChatAsync(settings.FastModel, messages, null, settings.ReasoningEffort, 20, false, cts.Token).ConfigureAwait(false);
                    string text = (string)response.SelectToken("choices[0].message.content");
                    return "OK – " + settings.FastModel + " antwortet in " + sw.ElapsedMilliseconds + " ms: \"" + (text ?? "").Trim() + "\"";
                }
            }
            catch (Exception e)
            {
                return "Fehler nach " + sw.ElapsedMilliseconds + " ms: " + e.Message;
            }
        }

        /// <summary>Saras Standardsätze vorab rendern, damit lokale Antworten ohne Wartezeit kommen.</summary>
        public void PrewarmSaraPhrases()
        {
            Task.Run(async () =>
            {
                string[] phrases = { "Links frei.", "Rechts frei.", "Noch einer links.", "Noch einer rechts.", "Noch zwei links.", "Noch zwei rechts.",
                    "Frei, keiner neben dir.", "Noch einer links, rechts frei.", "Noch einer rechts, links frei.", SaraLocal.NoDataAnswer };
                phrases = phrases.Concat(FlagWatch.Phrases).ToArray();
                foreach (var phrase in phrases)
                {
                    try
                    {
                        await RenderAsync(TeamMember.Sara, phrase, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception) { }
                }
            });
        }

        // ---------------- Hilfen ----------------

        private RaceDataContext BuildContext(GameStateData gs)
        {
            float extraFuel = float.MaxValue;
            try
            {
                if (gs != null && CrewChiefV4.Events.Fuel.inst != null)
                {
                    extraFuel = CrewChiefV4.Events.Fuel.GetAdditionalFuelToEndOfRace(false, false);
                }
            }
            catch (Exception) { }
            return new RaceDataContext
            {
                GameState = gs,
                Memory = Memory,
                Spotter = Spotter(),
                NowUtc = DateTime.UtcNow,
                LastTelemetryUtc = Memory.LastTickUtc,
                GameName = CrewChief.gameDefinition != null ? CrewChief.gameDefinition.friendlyName : null,
                SendDriverNames = settings.SendDriverNames,
                CrewChiefAdditionalFuelNeeded = extraFuel,
                Corners = Corners,
            };
        }

        private static SpotterSnapshot Spotter()
        {
            var mw = MainWindow.instance;
            return mw != null && mw.crewChief != null ? mw.crewChief.GetSpotterSnapshot() : SpotterSnapshot.None;
        }

        private TeamMember? RecentMember()
        {
            lock (dialogSync)
            {
                return (DateTime.UtcNow - lastExchangeUtc).TotalSeconds < 60 ? lastMember : null;
            }
        }

        private List<JObject> DialogHistory(GameStateData gs)
        {
            lock (dialogSync)
            {
                int session = gs != null ? gs.SessionData.SessionId : int.MinValue;
                if (session != dialogSessionId || (DateTime.UtcNow - lastExchangeUtc).TotalMinutes > 10)
                {
                    dialog.Clear();
                    dialogSessionId = session;
                }
                return dialog.Skip(Math.Max(0, dialog.Count - 8)).Select(d => (JObject)d.DeepClone()).ToList();
            }
        }

        private void RememberExchange(TeamMember member, string question, string answer)
        {
            lock (dialogSync)
            {
                dialog.Add(new JObject { ["role"] = "user", ["content"] = "[an " + TeamRouter.DisplayName(member) + "] " + question });
                // ohne "Ben: "-Präfix, sonst übernimmt das Modell die Markierung in seine Antworten
                dialog.Add(new JObject { ["role"] = "assistant", ["content"] = answer });
                while (dialog.Count > 16)
                {
                    dialog.RemoveAt(0);
                }
                lastMember = member;
                lastExchangeUtc = DateTime.UtcNow;
            }
        }

        private void Status(string text)
        {
            var handler = StatusChanged;
            if (handler != null)
            {
                try { handler(text); } catch (Exception) { }
            }
        }

        private void Log(string text)
        {
            Console.WriteLine("[KI-Team] " + text);
            if (!settings.WriteLog)
            {
                return;
            }
            try
            {
                string folder = Path.Combine(DataFiles.BaseFolder, "AiTeam");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "ki-team_" + DateTime.Now.ToString("yyyy-MM-dd") + ".log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + "  " + text + Environment.NewLine);
            }
            catch (Exception) { }
        }
    }
}
