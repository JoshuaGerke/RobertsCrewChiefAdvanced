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
    /// KI-Orchestrator für Mik, Ben und Sara.
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
        public TrackAwareness Tracks { get; private set; } = new TrackAwareness();
        private readonly TrackSituationWatch trackWatch = new TrackSituationWatch();
        private readonly RaceTrafficWatch raceTraffic = new RaceTrafficWatch();
        private readonly QualifyingResultWatch qualifyingResults = new QualifyingResultWatch();
        private readonly LiveWarningDispatcher liveWarnings = new LiveWarningDispatcher();
        private readonly NativeFieldEvents fieldEvents = new NativeFieldEvents();
        private readonly NativeIncidentWatch nativeIncidents = new NativeIncidentWatch();
        private readonly ThermalWatch thermalWatch = new ThermalWatch();
        private readonly NumericDamageWatch numericDamageWatch = new NumericDamageWatch();
        private readonly FuelWatch fuelWatch = new FuelWatch();
        private readonly IncidentWatch incidentWatch = new IncidentWatch();
        private readonly FlagWatch flagWatch = new FlagWatch();
        private readonly WeatherWatch weatherWatch = new WeatherWatch();
        private readonly PitLaneWatch pitLaneWatch = new PitLaneWatch();
        private readonly LapWatch lapWatch = new LapWatch();
        private readonly SessionLapObserver sessionLaps = new SessionLapObserver();
        private readonly RaceRadioDirector radioDirector = new RaceRadioDirector();
        private readonly ActiveRaceObserver activeRace = new ActiveRaceObserver();
        private readonly RaceSituationWatch situationWatch = new RaceSituationWatch();
        private readonly RadioRenderGate radioRenderGate=new RadioRenderGate();
        private readonly AiRadioSchedule radioSchedule = new AiRadioSchedule();
        private readonly SessionGreetingDirector greetingDirector = new SessionGreetingDirector();
        private readonly CornerDetector cornerDetector = new CornerDetector();
        private bool detectCorners;
        private string detectTrackName;
        private DateTime lastStatusUtc = DateTime.MinValue;
        private string cornerTrackKey;

        /// <summary>Wird vom Fenster angezeigt (letzte Erkennung, letzte Antwort, Fehler).</summary>
        public event Action<string> StatusChanged;

        private AiTeamSettings settings = AiTeamSettings.Load();
        private OpenAiCompatibleClient client;
        private ElevenLabsClient elevenLabs;
        private AudioPlayer audioPlayer;
        private DiagnosticRecorder crewLog;
        private int crewLogGeneration;
        private readonly AiMicRecorder recorder = new AiMicRecorder();
        private readonly AiPushToTalk pushToTalk;
        private readonly SemaphoreSlim requestLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource currentRequest;
        private int pressSequence;
        private int sessionGeneration;

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
        private ObservedRaceCall pendingGreeting;
        private DateTime sessionSeenUtc = DateTime.MinValue;

        private readonly Dictionary<string, byte[]> phraseCache = new Dictionary<string, byte[]>();
        // ElevenLabs lehnt ab (Schlüssel, Kontingent): eine Weile nicht mehr fragen statt bei jedem Satz Credits und Zeit zu verbrennen
        private DateTime elevenLabsPausedUntilUtc = DateTime.MinValue;
        private static readonly TimeSpan ElevenLabsPause = TimeSpan.FromMinutes(10);

        private AiTeamService()
        {
            pushToTalk = new AiPushToTalk(ControllerHeld, OnPushToTalkPressed, OnPushToTalkReleased);
            ApplySettings(settings);
            AppDomain.CurrentDomain.ProcessExit += (sender, args) => {
                var log = Volatile.Read(ref crewLog); if (log != null) log.Dispose();
                DriverMemory.Stop();
            };
        }

        // ---------------- Lebenszyklus ----------------

        public void Attach(AudioPlayer player)
        {
            audioPlayer = player;
            Console.WriteLine("[KI-Team] " + (settings.Enabled
                ? "Mik, Ben und Sara bereit. Funktaste: Controller-Aktion \"KI-Team anfunken\"" + (string.IsNullOrEmpty(settings.PttKey) ? "" : " oder Taste " + settings.PttKey) +
                  ". API-Schlüssel: " + (HasApiKey ? "hinterlegt" : "fehlt (Menü KI-Team)") + "."
                : "deaktiviert (Einstellung \"KI-Team aktivieren\")."));
            if (settings.Enabled)
            {
                pushToTalk.Start();
                if (HasApiKey || elevenLabs.HasKey)
                {
                    PrewarmSaraPhrases();
                    if (!SaraSpotterPackCurrent)
                        Log("Saras Original-Spotterpack fehlt oder ist nicht aktuell. Vor der Fahrt im KI-Team-Menü erzeugen. Kein automatischer Packwechsel während der Fahrt.");
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
            NativeTrafficBridge.Enabled = settings.Enabled && settings.ReplaceCrewChiefVoice && settings.BenEnabled;
            NativeTrafficBridge.Clear();
            NativeIncidentWatch.Enabled=settings.Enabled && settings.ReplaceCrewChiefVoice && settings.BenEnabled;
            nativeIncidents.Reset();
            raceTraffic.CoveredAhead=nativeIncidents.CoversAhead;
            trackWatch.CoveredAhead=nativeIncidents.CoversAhead;
            DriverMemory.Configure(settings.Enabled && settings.PersistentMemory);
            ConfigureCrewLog(newSettings.WriteLog);
            AudioPlayer.aiTeamSpotterMuted = settings.Enabled && !settings.SaraEnabled;
            // "Nur noch das KI-Team spricht": Crew Chiefs eigene Ansagen nicht mehr einreihen (Spotter läuft separat weiter)
            AudioPlayer.messageFilter = settings.Enabled && settings.ReplaceCrewChiefVoice
                ? new Func<QueuedMessage, bool>(m => {
                    bool allowed=CrewChiefVoiceFilter.Allow(m.messageName,m.messageFolders,settings.SaraEnabled);
                    if(!allowed){liveWarnings.Capture(m,CrewChief.currentGameState,DateTime.UtcNow);fieldEvents.Capture(m.NativeFieldEvent);}
                    return allowed;
                })
                : null;
            client = new OpenAiCompatibleClient(settings.BaseUrl, () => AiSecretStore.Load(), TimeSpan.FromSeconds(settings.TimeoutSeconds), Stats);
            elevenLabs = new ElevenLabsClient(AiSecretStore.LoadElevenLabs, TimeSpan.FromSeconds(settings.TimeoutSeconds), Stats);
            pushToTalk.SetKey(settings.PttKey);
            elevenLabsPausedUntilUtc = DateTime.MinValue;
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

        public void SetPersistentMemory(bool enabled)
        {
            var user=UserSettings.GetUserSettings();
            user.setProperty("enable_ai_team_persistent_memory",enabled);user.saveUserSettings();
            settings.PersistentMemory=enabled;DriverMemory.Configure(settings.Enabled && enabled);
            Status(enabled ? "Dauerhaftes Fahrergedächtnis eingeschaltet." : "Fahrergedächtnis ausgeschaltet; gespeicherte Texte bleiben erhalten.");
        }

        public void SetMemberEnabled(TeamMember member, bool enabled)
        {
            var user=UserSettings.GetUserSettings();
            user.setProperty("enable_ai_team_"+member.ToString().ToLowerInvariant(),enabled);
            if (member == TeamMember.Sara) { user.setProperty("enable_spotter", enabled); GlobalBehaviourSettings.spotterEnabled=enabled; }
            user.saveUserSettings();
            // Keep voice caches and live telemetry intact; only change role selection.
            settings = AiTeamSettings.Load();
            NativeTrafficBridge.Enabled = settings.Enabled && settings.ReplaceCrewChiefVoice && settings.BenEnabled;
            NativeTrafficBridge.Clear();
            AudioPlayer.aiTeamSpotterMuted = settings.Enabled && !settings.SaraEnabled;
            Status(TeamRouter.DisplayName(member)+(enabled?" aktiviert.":" deaktiviert; wartende Meldungen werden verworfen."));
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
            bool newSession = session != Memory.SessionId || (previous != null && previous.SessionData.SessionType != current.SessionData.SessionType) ||
                (current.SessionData.IsNewSession && previous != null && !previous.SessionData.IsNewSession);
            if (newSession)
            {
                OnSessionCleared();
                Corners.SetTrack(null, 0);
                pendingGreeting = null;
                Interlocked.Exchange(ref callsThisSession, 0);
                lastProactiveLap = -1;
                fuelWatch.Reset();
                incidentWatch.Reset();
                flagWatch.Reset();
                weatherWatch.Reset();
                pitLaneWatch.Reset();
                lapWatch.Reset();
                sessionLaps.Reset();
                radioDirector.Reset();
                activeRace.Reset();
                situationWatch.Reset();
                Tracks.Reset();
                trackWatch.Reset();
                raceTraffic.Reset();
                qualifyingResults.Reset();
                liveWarnings.Reset();
                fieldEvents.Reset();
                nativeIncidents.Reset();
                thermalWatch.Reset();
                numericDamageWatch.Reset();
                NativeTrafficBridge.Clear();
                radioSchedule.Reset();
                lastStatusUtc = now;
                cornerTrackKey = null;
                sessionSeenUtc = now;
                DriverMemory.BeginSession(CrewChief.gameDefinition==null?null:CrewChief.gameDefinition.friendlyName,
                    current.SessionData.TrackDefinition==null?null:current.SessionData.TrackDefinition.name,
                    current.carName,current.SessionData.SessionType.ToString(),current.SessionData.SessionId);
            }
            // Leichtgewichtige lokale Auswertung; max. 20 Hz, keine Cloud-Anfrage.
            bool sampleDue = radioDirector.Observe(now, current);
            UpdateCornerTrack(current);
            if (sampleDue && Tracks.Observe(current, now)) LiveDiagnostics.TrackPositions(Tracks.Snapshot);
            var gapContext=new RaceDataContext {GameState=current,Tracks=Tracks,NowUtc=now,LastTelemetryUtc=now};
            var measuredFront=RaceDataTools.ReadGap(gapContext,RaceDataTools.Neighbour(current,true),true);
            var measuredRear=RaceDataTools.ReadGap(gapContext,RaceDataTools.Neighbour(current,false),false);
            bool lapCompleted = Memory.Update(previous, current, now, measuredFront, measuredRear);
            if (lapCompleted)
            {
                LiveDiagnostics.Lap(Memory);
                DriverMemory.RememberLap(CrewChief.gameDefinition == null ? null : CrewChief.gameDefinition.friendlyName,
                    current.SessionData.TrackDefinition == null ? null : current.SessionData.TrackDefinition.name,
                    current.carName,Memory.GetLaps(1).LastOrDefault(),current.SessionData.SessionId);
                var lap = Memory.GetLaps(1).LastOrDefault();
                bool cleanLap = lap != null && lap.Valid && !lap.InPitLap;
                Corners.CompleteLap(current.SessionData.CompletedLaps, current.SessionData.LapTimePrevious,
                    current.SessionData.PreviousLapWasValid && cleanLap);
                if (detectCorners)
                {
                    FinishCornerDetection(cleanLap);
                }
            }
            if (sampleDue && detectCorners && !current.PitData.InPitlane)
            {
                cornerDetector.Add(current.PositionAndMotionData.DistanceRoundTrack, current.PositionAndMotionData.CarSpeed * 3.6f);
            }
            if (sampleDue) Corners.Tick(current.PositionAndMotionData.DistanceRoundTrack, current.PositionAndMotionData.CarSpeed,
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
                    liveWarnings.Offer("warning-flag-"+Array.IndexOf(FlagWatch.Phrases,call),call,TeamMember.Sara,10,current,now,
                        live=>FlagWatch.StillRelevant(call,live),"mapped_flag_transition");
                }
            }
            foreach (var incident in incidentWatch.Evaluate(previous, current, now, settings.RadioMode == AiRadioMode.MINIMAL))
            {
                var radio = incident;
                if(CrewSpeechPolicy.TrackLimitPhrases.Contains(radio.Value))
                {
                    int warningLap=current.SessionData.CompletedLaps;
                    bool invalid=!current.SessionData.CurrentLapIsValid;
                    int cuts=current.PenaltiesData.CutTrackWarnings;
                    liveWarnings.Offer(invalid?"warning-invalid-lap":"warning-tracklimit",radio.Value,radio.Key,7,current,now,
                        live=>!live.PitData.InPitlane && live.SessionData.CompletedLaps==warningLap &&
                            (invalid?!live.SessionData.CurrentLapIsValid:live.PenaltiesData.CutTrackWarnings>=cuts),
                        invalid?"mapped_current_lap_invalid":"mapped_cut_warning",warningLap);
                    continue;
                }
                Log("Zwischenfall " + TeamRouter.DisplayName(radio.Key) + ": " + radio.Value);
                var valid = TimelyValidity(current, now, 6, live => live.SessionData.CompletedLaps == current.SessionData.CompletedLaps);
                radioSchedule.NoteRadio(now);
                int incidentPriority = CrewSpeechPolicy.HeavyContactPhrases.Contains(radio.Value) ? 7 : 6;
                // The following priority-7 damage report may not interrupt the driver's safety check.
                Task.Run(() => SpeakAsync(radio.Key, radio.Value, true, CancellationToken.None, valid, true, incidentPriority));
            }
            string lapCall = lapWatch.Evaluate(previous, current,
                measuredFront.IsRaceGap ? measuredFront.Seconds : -1,
                measuredRear.IsRaceGap ? measuredRear.Seconds : -1, settings.RadioMode == AiRadioMode.MINIMAL);
            if (lapCall != null && (current.SessionData.SessionHasFixedTime || current.SessionData.SessionPhase != SessionPhase.Green || settings.RadioMode == AiRadioMode.MINIMAL))
            {
                if (current.SessionData.SessionPhase == SessionPhase.Finished && current.SessionData.SessionType == SessionType.Race &&
                    !current.SessionData.IsDNF && !current.SessionData.IsDisqualified)
                {
                    lapCall += incidentWatch.FinishSummary(Memory.RecordedInvalidLaps);
                }
                Log("Rundenansage Mik: " + lapCall);
                var valid = TimelyValidity(current, now, 12, live => live.SessionData.SessionPhase == current.SessionData.SessionPhase && live.SessionData.ClassPosition == current.SessionData.ClassPosition);
                radioSchedule.NoteRadio(now);
                Task.Run(() => SpeakAsync(TeamMember.Mik, lapCall, true, CancellationToken.None, valid, true, 4));
            }
            var damage = incidentWatch.TakeDamageAssessment();
            if (damage != null)
            {
                int damageGeneration = Volatile.Read(ref sessionGeneration);
                Task.Run(() => ReportDamageAsync(damage, damageGeneration));
            }
            MaybeSessionBriefing(previous, current, now);
            sessionLaps.Observe(current, lapCompleted ? Memory.GetLaps(1).LastOrDefault() : null, now, settings.SendDriverNames, previous);
            var radioPrefs = settings.RadioPreferences;
            activeRace.Observe(current, now, settings.RadioName, 0);
            radioSchedule.Observe(current, now, radioPrefs);
            foreach (var situation in situationWatch.Evaluate(previous,current,now,settings.RadioName)) activeRace.Enqueue(situation);
            var trafficCall = trackWatch.Evaluate(Tracks,current,now);
            if (trafficCall != null) activeRace.Enqueue(trafficCall);
            raceTraffic.Observe(Tracks,current,now);
            qualifyingResults.Observe(current,now,settings.RadioName);
            weatherWatch.Observe(current,now);
            pitLaneWatch.Observe(current,now);
            MaybePreparePitLimit(current,now);
            bool weatherBusy = !settings.IsMemberEnabled(TeamMember.Mik) || Spotter().HasOverlap ||
                radioRenderGate.Busy || currentRequest != null;
            bool pitBusy=!settings.IsMemberEnabled(TeamMember.Mik) || Spotter().HasOverlap || radioRenderGate.UrgentBusy;
            if(sampleDue)
            {
                thermalWatch.Observe(current,now,liveWarnings);
                numericDamageWatch.Observe(current,now,liveWarnings);
            }
            nativeIncidents.Observe(current,now,liveWarnings,Memory,settings.SendDriverNames);
            fieldEvents.CaptureMappedPits(previous,current);
            fieldEvents.Observe(current,now,Memory,settings.SendDriverNames);
            liveWarnings.Observe(current,now);
            var raceCall = liveWarnings.Take(current,now,Spotter().HasOverlap || radioRenderGate.UrgentBusy,settings.IsMemberEnabled);
            if (raceCall == null) raceCall = pitLaneWatch.Take(current,now,pitBusy,7);
            if (raceCall == null) raceCall = raceTraffic.Take(current,now,Spotter().HasOverlap || radioRenderGate.UrgentBusy,settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null) raceCall = weatherWatch.Take(current,now,weatherBusy,5);
            if (raceCall == null) raceCall = activeRace.Take(current, now, Spotter().HasOverlap,
                radioRenderGate.Busy,
                settings.RadioMode == AiRadioMode.MINIMAL, 0, Memory.GetLaps(), settings.RadioName);
            if (raceCall == null) raceCall = qualifyingResults.Take(current,now,Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null,settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null) raceCall = sessionLaps.Take(current, now, settings.RadioMode == AiRadioMode.MINIMAL ||
                Spotter().HasOverlap || radioDirector.TrafficQuiet(now) || radioRenderGate.Busy || currentRequest != null);
            if (raceCall == null) raceCall = fieldEvents.Take(current,now,Spotter().HasOverlap || radioRenderGate.Busy || currentRequest!=null,
                settings.IsMemberEnabled(TeamMember.Ben),settings.RadioMode==AiRadioMode.MINIMAL);
            if (raceCall == null) raceCall = pitLaneWatch.Take(current,now,weatherBusy);
            if (raceCall == null) raceCall = weatherWatch.Take(current,now,weatherBusy || radioDirector.TrafficQuiet(now));
            if (raceCall == null)
            {
                string routineBusy=settings.RadioMode==AiRadioMode.MINIMAL?"minimal_mode":Spotter().HasOverlap?"spotter_overlap":
                    radioRenderGate.Busy?"speech_rendering":currentRequest!=null?"driver_request":proactiveRunning?"autonomous_analysis":null;
                // Nearby racing alone is not side-by-side. Short configured reports still yield to
                // actual overlap, braking, focus laps and urgent traffic calls, without perpetual 9s quiet.
                raceCall = radioSchedule.Take(BuildContext(current),radioPrefs,routineBusy!=null,
                    settings.IsMemberEnabled,settings.RadioMode!=AiRadioMode.ACTIVE,routineBusy);
            }
            if (raceCall != null)
            {
                var callContext = BuildContext(current);
                // Core event facts must survive unrelated neighbour/gap changes during speech rendering.
                if (raceCall.Topic == "ahead" || raceCall.Topic == "behind") raceCall.IncludeNeighbour(callContext, raceCall.Topic == "ahead", false);
                QueueLocalRaceCall(raceCall);
            }
            // All session types use the same optional schedule; no competing legacy status timer.

            string trigger = null;
            string topic = null;
            // Pit facts are spoken once by the native-field director; no second cloud narration.
            if (trigger == null && lapCompleted)
            {
                string closing = current.SessionData.SessionType == SessionType.Race &&
                    current.SessionData.PreviousLapWasValid && !current.PitData.InPitlane
                    ? radioDirector.MeasuredOpponentClosing(current.SessionData.OpponentKeyInFront,
                        current.SessionData.CompletedLaps, measuredFront.IsRaceGap ? measuredFront.Seconds : -1)
                    : null;
                string approaching = current.SessionData.SessionType == SessionType.Race &&
                    current.SessionData.PreviousLapWasValid && !current.PitData.InPitlane
                    ? radioDirector.MeasuredOpponentApproaching(current.SessionData.OpponentKeyBehind,
                        current.SessionData.CompletedLaps, measuredRear.IsRaceGap ? measuredRear.Seconds : -1) : null;
                string cornerLoss = Corners.FindConsistentLoss(); // Autonomous; independent of optional timers.
                if (closing != null)
                {
                    trigger = "Gemessener Gegnertrend: " + closing + " Ben soll sachlich berichten, ohne Überholversprechen.";
                    topic = "opponent-gap-trend";
                }
                else if (approaching != null)
                {
                    trigger = "Gemessener Gegnertrend hinten: " + approaching + " Ben berichtet nur den Messbefund, keine riskante Verteidigungsanweisung.";
                    topic = "opponent-behind-trend";
                }
                else if (!string.IsNullOrWhiteSpace(cornerLoss))
                {
                    trigger = "Verifizierter wiederholter Kurvenverlust: " + cornerLoss +
                        " Mik: höchstens einen aus den Messwerten belegbaren Fahrhinweis. Keine erfundene Ursache.";
                    topic = "corner-coaching";
                }
                else
                {
                    string pace = radioDirector.ConsistentPaceLoss(Memory.GetLaps());
                    if (pace != null)
                    {
                        trigger = "Gemessener Pace-Trend: " + pace + " Mik soll nur den Befund melden, keine Ursachen behaupten.";
                        topic = "pace-trend";
                    }
                    else if (current.SessionData.SessionType == SessionType.Race)
                    {
                        string fuel = radioDirector.MeasuredFuelIncrease(Memory.GetLaps());
                        if (fuel != null)
                        {
                            trigger = "Verifizierter Kraftstofftrend: " + fuel +
                                " Ben beschreibt die Messung, keine Ursache oder Boxenpflicht ohne bestätigte Restdistanz.";
                            topic = "fuel-burn-trend";
                        }
                    }
                }
                // Absichtlich KEIN API-Aufruf für eine gewöhnliche Runde ohne Erkenntnis.
            }
            if (trigger != null) LiveDiagnostics.Decision(topic, "detected", "local_evidence", trigger);
            int minimumProactiveLaps = current.SessionData.SessionType == SessionType.Race ? 2 : 1;
            if (trigger != null && topic != null && settings.RadioMode != AiRadioMode.MINIMAL && HasApiKey && !proactiveRunning &&
                current.SessionData.SessionPhase == SessionPhase.Green &&
                current.SessionData.CompletedLaps >= minimumProactiveLaps && !current.PitData.InPitlane &&
                !Spotter().HasOverlap &&
                (settings.MaxCallsPerSession <= 0 || callsThisSession < settings.MaxCallsPerSession * 0.8) &&
                radioDirector.ReserveInsight(topic, now, TimeSpan.FromMinutes(5)))
            {
                TeamMember owner = topic.StartsWith("opponent-", StringComparison.Ordinal) ||
                    topic.StartsWith("fuel-", StringComparison.Ordinal) ? TeamMember.Ben : TeamMember.Mik;
                MaybeProactive(current, trigger, lapCompleted, owner);
            }
            else if (trigger != null)
            {
                string gate = settings.RadioMode == AiRadioMode.MINIMAL ? "minimal_mode" : proactiveRunning ? "request_in_flight" :
                    current.SessionData.SessionPhase != SessionPhase.Green ? "session_phase" : current.SessionData.CompletedLaps < minimumProactiveLaps ? "minimum_laps" :
                    current.PitData.InPitlane ? "pitlane" : Spotter().HasOverlap ? "spotter_overlap" : !HasApiKey ? "no_api_key" :
                    settings.MaxCallsPerSession > 0 && callsThisSession >= settings.MaxCallsPerSession * 0.8 ? "budget_reserve" : "radio_reservation";
                LiveDiagnostics.Decision(topic,"suppressed",gate,trigger);
            }
        }

        /// <summary>Kurven der Strecke aus den Crew-Chief-Streckendaten übernehmen (Geraden werden ausgelassen).</summary>
        private void UpdateCornerTrack(GameStateData gs)
        {
            var track = gs.SessionData.TrackDefinition;
            if (track == null || track.trackLength <= 0)
            {
                return;
            }
            var landmarks = track.trackLandmarks ?? new List<TrackLandmark>();
            string key = track.name + "|" + landmarks.Count + "|" + gs.SessionData.SessionId;
            if (key == cornerTrackKey)
            {
                return;
            }
            cornerTrackKey = key;
            detectCorners = false;
            var corners = new List<CornerDefinition>();
            for (int i = 0; i < landmarks.Count; i++)
            {
                var lm = landmarks[i];
                if (lm.isStraight)
                {
                    continue;
                }
                string raw = lm.landmarkNames != null && lm.landmarkNames.Count > 0 ? lm.landmarkNames[0] : null;
                corners.Add(new CornerDefinition { Name = CornerCoach.FriendlyName(raw, corners.Count), Start = lm.distanceRoundLapStart, End = lm.distanceRoundLapEnd });
            }
            if (corners.Count == 0)
            {
                corners = LoadDetectedCorners(track.name);
                if (corners.Count == 0)
                {
                    // keine Kurvendaten (z. B. viele AMS2-Strecken): in der ersten sauberen Runde selbst vermessen
                    detectCorners = true;
                    detectTrackName = track.name;
                    cornerDetector.Reset(track.trackLength);
                    Log("Kurvenanalyse: keine Kurvendaten für " + track.name + ", vermesse die Kurven in der ersten sauberen Runde");
                }
            }
            Corners.SetTrack(corners, track.trackLength);
            Log("Kurvenanalyse: " + corners.Count + " Kurven für " + track.name);
        }

        private void FinishCornerDetection(bool cleanLap)
        {
            var found = cleanLap ? cornerDetector.Detect() : new List<CornerDefinition>();
            var track = CrewChief.currentGameState != null ? CrewChief.currentGameState.SessionData.TrackDefinition : null;
            if (found.Count >= 3 && track != null)
            {
                detectCorners = false;
                Corners.SetTrack(found, track.trackLength);
                SaveDetectedCorners(detectTrackName, found);
                Log("Kurvenanalyse: " + found.Count + " Kurven selbst vermessen für " + detectTrackName);
            }
            else
            {
                cornerDetector.Reset(track != null ? track.trackLength : 0); // nächste Runde neu versuchen
            }
        }

        private static string CornerFile(string trackName)
        {
            string safe = string.Concat((trackName ?? "strecke").Select(c => char.IsLetterOrDigit(c) ? c : '_'));
            return Path.Combine(AiSecretStore.Folder, "kurven", safe + ".json");
        }

        private List<CornerDefinition> LoadDetectedCorners(string trackName)
        {
            try
            {
                string file = CornerFile(trackName);
                if (File.Exists(file))
                {
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<List<CornerDefinition>>(File.ReadAllText(file)) ?? new List<CornerDefinition>();
                }
            }
            catch (Exception e)
            {
                Log("Gespeicherte Kurven nicht lesbar: " + e.Message);
            }
            return new List<CornerDefinition>();
        }

        private void SaveDetectedCorners(string trackName, List<CornerDefinition> corners)
        {
            try
            {
                string file = CornerFile(trackName);
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, Newtonsoft.Json.JsonConvert.SerializeObject(corners));
            }
            catch (Exception e)
            {
                Log("Kurven nicht gespeichert: " + e.Message);
            }
        }

        /// <summary>Regelmäßiger Statusfunk (Einstellung "Statusfunk alle … Minuten"), auch ohne Frage des Fahrers.</summary>
        private void MaybeStatus(GameStateData gs, DateTime now)
        {
            var s = gs.SessionData;
            if (settings.StatusIntervalMinutes <= 0 || settings.RadioMode == AiRadioMode.MINIMAL || !HasApiKey || proactiveRunning ||
                s.SessionPhase != SessionPhase.Green || s.CompletedLaps < 1 || gs.PitData.InPitlane || LapWatch.OnLastLap(s) ||
                Spotter().HasOverlap || radioDirector.TrafficQuiet(now) || (now - lastOverlapUtc).TotalSeconds < 8)
            {
                return;
            }
            // Statusbericht nur als bewusst aktivierte Option. Im Normalbetrieb lösen
            // belastbare Ereignisse die Meldungen aus statt eines Minuten-Timers.
            if ((now - lastStatusUtc).TotalMinutes < settings.StatusIntervalMinutes || (now - lastProactiveUtc).TotalSeconds < 90)
            {
                return;
            }
            if (settings.MaxCallsPerSession > 0 && callsThisSession >= settings.MaxCallsPerSession * 0.8)
            {
                return;
            }
            lastStatusUtc = now;
            RunScripted(AiPersonas.StatusPrompt, "Status");
        }

        /// <summary>Feste Sprit-Warnung (ohne KI), nur im Rennen.</summary>
        private void CheckFuel(GameStateData gs)
        {
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Green || !gs.FuelData.FuelUseActive)
            {
                return;
            }
            var estimate = FuelEstimate.From(BuildContext(gs));
            if (estimate == null) return;
            string warning = fuelWatch.Evaluate(s.CompletedLaps, gs.FuelData.FuelLeft, estimate.PerLap, estimate.LapsRemaining,
                gs.PitData.IsRefuellingAllowed, estimate.EstimatedFromTime, estimate.AdditionalWithoutReserve);
            if (warning != null)
            {
                var now=DateTime.UtcNow;
                var valid=TimelyValidity(gs,now,6, live => {
                    var fresh=FuelEstimate.From(BuildContext(live));
                    return fresh!=null && (fresh.AdditionalWithoutReserve>0)==(estimate.AdditionalWithoutReserve>0) &&
                        Math.Abs(fresh.AdditionalWithoutReserve-estimate.AdditionalWithoutReserve)<.3f;
                });
                Log("Sprit-Warnung: " + warning); radioSchedule.NoteRadio(now);
                Task.Run(() => SpeakAsync(TeamMember.Ben, warning, true, CancellationToken.None, valid, true, 7));
            }
        }

        public void OnSessionCleared()
        {
            Interlocked.Increment(ref sessionGeneration);
            var request = Interlocked.Exchange(ref currentRequest, null);
            if (request != null) { try { request.Cancel(); } catch (ObjectDisposedException) { } }
            Memory.Clear();
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
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("ptt.pressed",LiveDiagnostics.Data("utterance_sequence",pressSequence));
            recorder.Start(settings.Microphone);
            var running = currentRequest;
            if (running != null)
            {
                try { running.Cancel(); } catch (ObjectDisposedException) { }
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
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("ptt.released",LiveDiagnostics.Data("utterance_sequence",pressSequence));
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
            using (var diagnosticSpan = LiveDiagnostics.Begin("utterance", LiveDiagnostics.Data("utterance_sequence",sequence)))
            {
                try
                {
            if (!HasApiKey)
            {
                Status("Kein API-Schlüssel hinterlegt (Menü \"KI-Team\").");
                await SpeakAsync(TeamMember.Mik, "Für die Spracherkennung fehlt noch der API-Schlüssel.", true, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            string text;
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("stt.input",LiveDiagnostics.Data("utterance_sequence",sequence,"bytes",wav.Length,"source","microphone_in_memory_only"));
            try
            {
                text = await TranscribeWithFallbackAsync(wav).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Log("Spracherkennung fehlgeschlagen: " + e.Message);
                Status("Spracherkennung fehlgeschlagen: " + e.Message);
                await SpeakAsync(TeamMember.Mik, SpokenError(e, "Funk war nicht zu verstehen, die Verbindung hakt."), true, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("stt.recognized",LiveDiagnostics.Data("transcript",text,"utterance_sequence",sequence,"confidence_available",false));
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
                catch (Exception diagnosticError) { diagnosticSpan.Fail(diagnosticError); throw; }
            }
        }

        // ---------------- Anfrage bearbeiten ----------------

        /// <summary>Komplette Verarbeitung einer Fahrerfrage als Text (auch für den Testknopf im KI-Team-Fenster).</summary>
        public async Task<string> AskAsync(string transcript, DateTime? askedUtc = null)
        {
            using (var diagnosticSpan = LiveDiagnostics.Begin("question", LiveDiagnostics.Data("transcript",transcript)))
            {
                try
                {
            DateTime started = askedUtc ?? DateTime.UtcNow;
            int generation = Volatile.Read(ref sessionGeneration);
            var routed = TeamRouter.Route(transcript, RecentMember());
            var memoryState=CrewChief.currentGameState;
            DriverMemory.RememberDialogue(routed.Member,routed.Question,null,CrewChief.gameDefinition==null?null:CrewChief.gameDefinition.friendlyName,
                memoryState==null||memoryState.SessionData.TrackDefinition==null?null:memoryState.SessionData.TrackDefinition.name,
                memoryState==null?int.MinValue:memoryState.SessionData.SessionId);
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("routing",LiveDiagnostics.Data("transcript",transcript,"speaker",routed.Member.ToString(),"routing","local_heuristic","confidence_available",false));
            Status("Fahrer → " + TeamRouter.DisplayName(routed.Member) + ": " + routed.Question);
            if (!settings.IsMemberEnabled(routed.Member))
            {
                string inactive=TeamRouter.DisplayName(routed.Member)+" ist deaktiviert. Im KI-Team-Fenster aktivieren.";
                Status(inactive); LiveDiagnostics.Decision("question","suppressed","member_disabled"); return inactive;
            }
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

            string repeat = (routed.Question ?? "").Trim().TrimEnd('.', '!', '?').ToLowerInvariant();
            if (repeat == "wiederhol" || repeat == "wiederhole" || repeat == "nochmal" || repeat == "bitte wiederholen" || repeat == "was hast du gesagt")
            {
                var lastSpoken = Memory.GetRadio(TimeSpan.FromMinutes(3), DateTime.UtcNow).LastOrDefault();
                string repeated = lastSpoken == null ? "Es liegt noch kein bestätigter Funkspruch vor." : "Zuletzt am Funk: " + lastSpoken.Text;
                await SpeakAsync(routed.Member, repeated, true, CancellationToken.None, _ => generation == Volatile.Read(ref sessionGeneration)).ConfigureAwait(false);
                return repeated;
            }

            // 2. Einfache Standardfragen ohne KI
            string quick = LocalAnswers.TryAnswer(routed.Question, gs, Memory);
            if (quick != null)
            {
                Stats.AddLocalAnswer();
                // Der Fahrer hat gefragt: lieber der Wert von vor einer Sekunde als gar keine Antwort. Nur ein Sessionwechsel verwirft sie.
                await SpeakAsync(routed.Member, quick, true, CancellationToken.None, _ => generation == Volatile.Read(ref sessionGeneration)).ConfigureAwait(false);
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
            bool lockAcquired = false;
            CancellationTokenSource linked = null;
            try
            {
                await requestLock.WaitAsync(driverCancel.Token).ConfigureAwait(false);
                lockAcquired = true;
                linked = CancellationTokenSource.CreateLinkedTokenSource(driverCancel.Token);
                linked.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
                string answer = await AskModelAsync(routed, gs, linked.Token).ConfigureAwait(false);
                if (driverCancel.IsCancellationRequested || generation != Volatile.Read(ref sessionGeneration))
                {
                    return null;
                }
                if ((DateTime.UtcNow - started).TotalSeconds > 25)
                {
                    Log("Antwort verworfen, zu alt: " + answer);
                    return null;
                }
                await SpeakAsync(routed.Member, answer, true, driverCancel.Token, t => generation == Volatile.Read(ref sessionGeneration)).ConfigureAwait(false);
                RememberExchange(routed.Member, routed.Question, answer);
                Log("Gesamtlatenz Frage → Funkspruch eingereiht: " + (long)(DateTime.UtcNow - started).TotalMilliseconds + " ms");
                return answer;
            }
            catch (Exception e)
            {
                if (driverCancel.IsCancellationRequested || generation != Volatile.Read(ref sessionGeneration))
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
                if (lockAcquired) requestLock.Release();
                Interlocked.CompareExchange(ref currentRequest, null, driverCancel);
                if (linked != null) linked.Dispose();
            }

                }
                catch (Exception diagnosticError) { diagnosticSpan.Fail(diagnosticError); throw; }
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
                    ["content"] = AiPersonas.SystemPrompt(routed.Member, settings.RadioName, longAllowed) + DriverNotes.PromptBlock(routed.Question, ctx) +
                        "\n\nAktuelle Lage (Live-Daten mit Reifen, Sprit, direkten Gegnern und Box). Reicht sie für die Antwort, antworte direkt " +
                        "ohne Werkzeug. Werkzeuge nur für Details, die hier fehlen (Stintverlauf, Rundenzeiten, Schäden/Motor, Wetter, Ereignisse):\n" +
                        RaceDataTools.BuildBriefing(ctx).ToString(Newtonsoft.Json.Formatting.None) +
                        "\nZuletzt tatsächlich an die Audioausgabe übergebener Funk (historisch, Zahlen erneut prüfen):\n" +
                        string.Join("\n", Memory.GetRadio(TimeSpan.FromMinutes(3), DateTime.UtcNow).AsEnumerable().Reverse().Take(3).Reverse().Select(r => r.Text)),
                },
            };
            foreach (var turn in DialogHistory(gs))
            {
                messages.Add(turn);
            }
            messages.Add(new JObject { ["role"] = "user", ["content"] = "[an " + TeamRouter.DisplayName(routed.Member) + "] " + routed.Question });

            return await RunToolLoopAsync(client, model, OpenAiCompatibleClient.RadioReasoningEffort(settings.ReasoningEffort, longAllowed && NeedsDeepModel(routed.Question)), messages, longAllowed ? 600 : 250, longAllowed ? 600 : 260,
                () => BuildContext(CrewChief.currentGameState), () => Interlocked.Increment(ref callsThisSession), Log, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Tool-Calling-Schleife: Modell fragt Werkzeuge an, wir liefern frische Telemetrie, bis eine Textantwort kommt (max. 4 Runden).
        /// </summary>
        public static async Task<string> RunToolLoopAsync(OpenAiCompatibleClient client, string model, string reasoningEffort, JArray messages,
            int maxTokens, int maxChars, Func<RaceDataContext> contextProvider, Action countCall, Action<string> log, CancellationToken ct)
        {
            JArray tools = RaceDataTools.BuildToolDefinitions();
            bool recoveredLength = false;
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
                string finish = (string)response.SelectToken("choices[0].finish_reason");
                if (finish == "length")
                {
                    // Do not execute truncated tool calls or speak a cut-off technical recommendation.
                    if (recoveredLength || round == 3) throw new AiApiException("KI-Antwort am Ausgabelimit abgebrochen.");
                    recoveredLength = true;
                    reasoningEffort = OpenAiCompatibleClient.RadioReasoningEffort(reasoningEffort, false);
                    maxTokens = Math.Max(maxTokens, 2048);
                    if (log != null) log("KI-Ausgabelimit erreicht; ein begrenzter Wiederholungsversuch.");
                    continue;
                }
                var toolCalls = message["tool_calls"] as JArray;
                if (log != null)
                {
                    log("Modell " + model + " Runde " + (round + 1) + ": " + sw.ElapsedMilliseconds + " ms" + (toolCalls != null ? ", Werkzeuge: " +
                        string.Join(", ", toolCalls.Select(t => (string)t.SelectToken("function.name"))) : ""));
                }
                if (toolCalls == null || toolCalls.Count == 0)
                {
                    return RequireCompleteAnswer(response, maxChars);
                }
                messages.Add(new JObject { ["role"] = "assistant", ["content"] = message["content"], ["tool_calls"] = toolCalls });
                RaceDataContext ctx = contextProvider();
                foreach (var call in toolCalls)
                {
                    string name = (string)call.SelectToken("function.name");
                    JObject result;
                    if (name == "save_note")
                    {
                        string note = null;
                        try { note = (string)JObject.Parse((string)call.SelectToken("function.arguments") ?? "{}")["notiz"]; } catch (Exception) { }
                        bool saved=DriverMemory.TryAddNote(note);
                        if (saved && log != null)
                        {
                            log("Fahrerwunsch dauerhaft gespeichert.");
                        }
                        result = new JObject { ["gespeichert"] = saved, ["status"] = DriverMemory.Status, ["hinweis"] = saved ? "Lokal als Fahrerwunsch gespeichert und für künftige KI-Gespräche verfügbar. Keine Änderung der Ereignislogik, Einstellungen oder Software dadurch erfolgt." : "Nicht gespeichert: Gedächtnis ausgeschaltet oder noch nicht verfügbar. Das dem Fahrer ehrlich sagen." };
                    }
                    else
                    {
                        result = RaceDataTools.Execute(name, ctx, (string)call.SelectToken("function.arguments"));
                    }
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

        public static string RequireCompleteAnswer(JObject response, int maxChars)
        {
            string finish = (string)response.SelectToken("choices[0].finish_reason");
            if (finish == "length") throw new AiApiException("KI-Antwort am Ausgabelimit abgebrochen.");
            string text = AiPersonas.CleanForRadio(ContentText(response.SelectToken("choices[0].message.content")), maxChars, true);
            if (string.IsNullOrWhiteSpace(text))
                throw new AiApiException("KI lieferte keinen nutzbaren Antworttext (Abschluss: " + (finish ?? "unbekannt") + ").");
            return text;
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
            const string hint = "Rennfunk an die Boxencrew Mik, Ben und Sara. Simracing, Reifen, Sprit, Boxenstopp, Undercut.";
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

        /// <summary>Was Mik bei einem Fehler sagt: die echte Ursache statt pauschal "Verbindung hakt".</summary>
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

        private void MaybeProactive(GameStateData gs, string trigger, bool isLapTrigger, TeamMember owner)
        {
            if (!settings.IsMemberEnabled(owner)) return;
            if (settings.RadioMode == AiRadioMode.MINIMAL || proactiveRunning || !HasApiKey)
            {
                return;
            }
            var s = gs.SessionData;
            DateTime now = DateTime.UtcNow;
            bool racing = s.SessionPhase == SessionPhase.Green && s.SessionType != SessionType.Unavailable;
            // im Rennen erst nach der hektischen ersten Runde, im Training schon nach der ersten Runde
            int minLaps = s.SessionType == SessionType.Race ? 2 : 1;
            // in der letzten Runde keine Strategie mehr (Sprit, Box): das stört nur
            if (!racing || s.CompletedLaps < minLaps || gs.PitData.InPitlane || LapWatch.OnLastLap(s) || Spotter().HasOverlap || radioDirector.TrafficQuiet(now) || (now - lastOverlapUtc).TotalSeconds < 8)
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
            int generation = Volatile.Read(ref sessionGeneration);
            Task.Run(async () =>
            {
                try
                {
                    await RunProactiveAsync(trigger, owner, generation).ConfigureAwait(false);
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

        private async Task RunProactiveAsync(string trigger, TeamMember owner, int generation)
        {
            if (generation != Volatile.Read(ref sessionGeneration)) return;
            var ctx = BuildContext(CrewChief.currentGameState);
            if (ctx.GameState == null || DateTime.UtcNow - Memory.LastTickUtc > TimeSpan.FromSeconds(2))
            {
                return; // keine KI mit stehenden/veralteten Renndaten auslösen
            }
            var data = new JObject
            {
                ["anlass"] = trigger,
                ["verantwortlicher_ingenieur"] = TeamRouter.DisplayName(owner),
                ["lage"] = RaceDataTools.BuildBriefing(ctx),
                ["stint"] = RaceDataTools.Execute("get_stint_analysis", ctx),
                ["kurven"] = RaceDataTools.Execute("get_corner_analysis", ctx),
                ["ereignisse_und_letzte_funksprueche"] = RaceDataTools.Execute("get_recent_events", ctx),
            };
            var evidence = new RaceSpeechEvidence(ctx);
            var messages = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = string.Format(AiPersonas.ProactivePrompt, settings.RadioName) + DriverNotes.PromptBlock(trigger, ctx) },
                new JObject { ["role"] = "user", ["content"] = data.ToString(Newtonsoft.Json.Formatting.None) },
            };
            Interlocked.Increment(ref callsThisSession);
            JObject response;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds)))
            {
                response = await client.ChatAsync(settings.FastModel, messages, null, OpenAiCompatibleClient.RadioReasoningEffort(settings.ReasoningEffort, false), 200, true, cts.Token).ConfigureAwait(false);
            }
            string content = (string)response.SelectToken("choices[0].message.content");
            TeamMember member;
            string text;
            if (!TryParseProactive(content, out member, out text))
            {
                Log("Proaktiv: nichts zu funken (" + trigger + ")");
                return;
            }
            // Rollenverteilung gehört der lokalen Rennlogik, nicht dem Sprachmodell.
            if (member != owner)
            {
                Log("Proaktiv verworfen: falsche Rolle für das bestätigte Ereignis");
                return;
            }
            // KI-Text hat nach langer Cloud-Latenz möglicherweise keine aktuelle Grundlage mehr.
            if (generation != Volatile.Read(ref sessionGeneration) || !evidence.IsCurrent(BuildContext(CrewChief.currentGameState)))
            {
                Log("Proaktiv verworfen: Tatsachen verändert oder Antwort zu alt");
                return;
            }
            // Funkruhe, solange der Spotter arbeitet
            if (Spotter().HasOverlap || radioDirector.TrafficQuiet(DateTime.UtcNow))
            {
                Log("Proaktiv verworfen wegen Positionskampf/Überlappung: " + text);
                return;
            }
            Log("Proaktiv " + TeamRouter.DisplayName(member) + ": " + text);
            await SpeakAsync(member, text, false, CancellationToken.None, t => generation == Volatile.Read(ref sessionGeneration) &&
                evidence.IsCurrent(BuildContext(CrewChief.currentGameState))).ConfigureAwait(false);
        }

        /// <summary>
        /// Begrüßung mit Lagebericht einmal pro Session (ein paar Sekunden nach Sessionbeginn, sobald Daten da sind)
        /// und Nachbesprechung, wenn die Session zu Ende geht. Nicht im Funkmodus "Minimal".
        /// </summary>
        private void MaybeSessionBriefing(GameStateData previous, GameStateData current, DateTime now)
        {
            if (settings.RadioMode == AiRadioMode.MINIMAL || proactiveRunning || currentRequest != null ||
                radioRenderGate.Busy || Spotter().HasOverlap || radioDirector.TrafficQuiet(now)) return;
            TeamMember speaker = settings.IsMemberEnabled(TeamMember.Ben) ? TeamMember.Ben : TeamMember.Mik;
            if (!settings.IsMemberEnabled(speaker) || now < sessionSeenUtc.AddSeconds(4) || now > sessionSeenUtc.AddSeconds(45)) return;
            // Reserve once, acknowledge only at the audio backend, retry at most once within the safe window.
            if (pendingGreeting == null)
                pendingGreeting = greetingDirector.CreateCall(BuildContext(current), sessionSeenUtc.AddSeconds(45), settings.RadioName, speaker);
            if (pendingGreeting != null && pendingGreeting.DeliveryStartedUtc == DateTime.MinValue &&
                pendingGreeting.IsValid(current, now) && pendingGreeting.TryReserve(now))
                QueueLocalRaceCall(pendingGreeting);
        }

        /// <summary>Mik bewertet den Schaden mit der KI (Lage, Restdistanz, Empfehlung Box oder weiter); ohne KI der lokale Bericht.</summary>
        private async Task ReportDamageAsync(DamageAssessment damage, int generation)
        {
            var current=CrewChief.currentGameState;
            if(current==null || generation!=Volatile.Read(ref sessionGeneration))return;
            string measured=IncidentWatch.DamageReport(current.CarDamageData);
            if(string.IsNullOrWhiteSpace(measured))return;
            // Damage remains relevant while positions change. Keep transient classification out of this warning.
            string report=measured;
            await SpeakAsync(TeamMember.Mik,report,true,CancellationToken.None,_=> {
                var live=CrewChief.currentGameState;
                return generation==Volatile.Read(ref sessionGeneration) && live!=null &&
                    DateTime.UtcNow>=Memory.LastTickUtc && (DateTime.UtcNow-Memory.LastTickUtc).TotalSeconds<=1 &&
                    IncidentWatch.DamageReport(live.CarDamageData)==measured;
            },true,7).ConfigureAwait(false);
        }

        private bool IsBriefingStillSafe(int sessionId)
        {
            var current = CrewChief.currentGameState;
            return current != null && current.SessionData.SessionId == sessionId &&
                (DateTime.UtcNow - sessionSeenUtc).TotalSeconds <= 45 &&
                (DateTime.UtcNow - Memory.LastTickUtc).TotalSeconds <= 2 &&
                SessionGreetingDirector.SafeToGreet(current.SessionData.SessionType, current.SessionData.SessionPhase,
                    current.PitData.InPitlane, current.PositionAndMotionData.CarSpeed) &&
                !Spotter().HasOverlap && !radioDirector.TrafficQuiet(DateTime.UtcNow);
        }

        private void RunScripted(string prompt, string label, string briefingCue = null)
        {
            if (!settings.IsMemberEnabled(TeamMember.Ben) && !settings.IsMemberEnabled(TeamMember.Mik)) return;
            proactiveRunning = true;
            lastProactiveUtc = DateTime.UtcNow;
            int queuedSession = Memory.SessionId;
            int queuedGeneration = Volatile.Read(ref sessionGeneration);
            Task.Run(async () =>
            {
                try
                {
                    if (label == "Begrüßung" && !IsBriefingStillSafe(queuedSession)) return;
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
                        ["ereignisse_und_letzte_funksprueche"] = RaceDataTools.Execute("get_recent_events", ctx),
                    };
                    var evidence = new RaceSpeechEvidence(ctx);
                    if (briefingCue != null) data["begruessungsregie"] = briefingCue;
                    var messages = new JArray
                    {
                        new JObject { ["role"] = "system", ["content"] = string.Format(prompt, settings.RadioName) + DriverNotes.PromptBlock(label, ctx) },
                        new JObject { ["role"] = "user", ["content"] = data.ToString(Newtonsoft.Json.Formatting.None) },
                    };
                    Interlocked.Increment(ref callsThisSession);
                    JObject response;
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds)))
                    {
                        response = await client.ChatAsync(settings.FastModel, messages, null, OpenAiCompatibleClient.RadioReasoningEffort(settings.ReasoningEffort, false), 400, true, cts.Token).ConfigureAwait(false);
                    }
                    // Langsame KI-Antworten dürfen weder in neue Sessions hineinfunken
                    // noch die Startphase übersprechen. Nach jedem TTS erneut kontrollieren.
                    if (queuedGeneration != Volatile.Read(ref sessionGeneration) || Memory.SessionId != queuedSession ||
                        (label == "Begrüßung" && !IsBriefingStillSafe(queuedSession))) return;
                    foreach (var radio in ParseRadioList(ContentText(response.SelectToken("choices[0].message.content")), 2))
                    {
                        if (queuedGeneration != Volatile.Read(ref sessionGeneration) || Memory.SessionId != queuedSession ||
                            (label == "Begrüßung" && !IsBriefingStillSafe(queuedSession))) break;
                        Log(label + " " + TeamRouter.DisplayName(radio.Key) + ": " + radio.Value);
                        Func<string, bool> validity = text => queuedGeneration == Volatile.Read(ref sessionGeneration) &&
                            evidence.IsCurrent(BuildContext(CrewChief.currentGameState)) &&
                            (label != "Begrüßung" || IsBriefingStillSafe(queuedSession));
                        await SpeakAsync(radio.Key, radio.Value, false, CancellationToken.None, validity).ConfigureAwait(false);
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

        /// <summary>Liest {"funksprueche":[{"speaker":"mik|ben","text":"…"}]}; Sara und unbekannte Sprecher werden verworfen.</summary>
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

        private Func<string, bool> TimelyValidity(GameStateData source, DateTime detected, int seconds, Func<GameStateData,bool> condition)
        {
            int generation = Volatile.Read(ref sessionGeneration);
            int session = source.SessionData.SessionId; var type = source.SessionData.SessionType;
            return _ => {
                var live = CrewChief.currentGameState; var now = DateTime.UtcNow;
                return settings.Enabled && generation == Volatile.Read(ref sessionGeneration) && live != null &&
                    now >= detected && now <= detected.AddSeconds(seconds) && now >= Memory.LastTickUtc &&
                    now-Memory.LastTickUtc <= TimeSpan.FromSeconds(1) && live.SessionData.SessionId == session &&
                    live.SessionData.SessionType == type && condition(live);
            };
        }

        private byte[] PreparedSpeech(TeamMember member, string text)
        {
            string key = CrewSpeechPolicy.CacheKey(settings, member, text);
            lock (phraseCache) { byte[] pcm; if (phraseCache.TryGetValue(key, out pcm)) return pcm; }
            // Disk access is on a worker, never the telemetry/spotter thread.
            string file = PhraseCacheFile(member, text);
            if (File.Exists(file)) { var pcm = File.ReadAllBytes(file); if (pcm.Length > 0) return pcm; }
            throw new InvalidOperationException("Originalstimme noch nicht vorbereitet. Im KI-Team-Fenster Funkstimmen vorbereiten; keine Ersatzstimme wird verwendet.");
        }

        private bool TelemetryFresh()
        {
            DateTime last = Memory.LastTickUtc;
            DateTime now = DateTime.UtcNow;
            return now >= last && (now - last).TotalSeconds <= 1;
        }

        private void QueueLocalRaceCall(ObservedRaceCall call)
        {
            int generation = Volatile.Read(ref sessionGeneration);
            bool urgentPit=RadioRenderGate.IsUrgentPit(call) || call.Priority>=7 &&
                (call.Topic=="race-hazard-ahead" || call.Topic=="race-traffic-rear" || (call.Topic??"").StartsWith("warning-",StringComparison.Ordinal));
            if (!settings.IsMemberEnabled(call.Member) || !radioRenderGate.TryEnter(urgentPit))
            {
                call.ReleaseForRetry(DateTime.UtcNow, "member_disabled_or_channel_busy");
                return;
            }
            Task.Run(async () =>
            {
                try
                {
                    Func<string, bool> valid = _ => settings.Enabled && generation == Volatile.Read(ref sessionGeneration) &&
                        TelemetryFresh() && !Spotter().HasOverlap &&
                        call.IsValid(CrewChief.currentGameState, DateTime.UtcNow);
                    if (!valid(call.Text)) { call.ReleaseForRetry(DateTime.UtcNow, "event_no_longer_valid"); return; }
                    bool queued = await SpeakAsync(call.Member, call.Text, true, CancellationToken.None, valid, true, Math.Min(10, call.Priority),
                        () => { call.MarkPlaybackStarted(DateTime.UtcNow); },
                        () => call.ReleaseForRetry(DateTime.UtcNow, "audio_discarded_before_playback"),
                        interrupted=>call.MarkPlaybackFinished(DateTime.UtcNow,interrupted),call.Topic).ConfigureAwait(false);
                    if (!queued) call.ReleaseForRetry(DateTime.UtcNow, "speech_not_queued");
                }
                catch (Exception e) { call.ReleaseForRetry(DateTime.UtcNow, "delivery_exception"); Log("Lokaler Rennfunk: " + e.Message); }
                finally { radioRenderGate.Exit(urgentPit); }
            });
        }

        // ---------------- Sprachausgabe ----------------

        public Task SpeakAsync(TeamMember member, string text, bool isReply, CancellationToken ct)
        {
            return SpeakAsync(member, text, isReply, ct, null);
        }

        /// <param name="stillValid">Wird direkt vor dem Einreihen geprüft (Sara: Spotterlage neu bewerten). false = nicht abspielen.</param>
        /// <returns>true, wenn der Funkspruch eingereiht wurde.</returns>
        private async Task<bool> SpeakAsync(TeamMember member, string text, bool isReply, CancellationToken ct, Func<string, bool> stillValid, bool localOnly = false, int localPriority = 4, Action playbackStarted = null, Action discarded = null, Action<bool> playbackEnded = null, string observedTopic = null)
        {
            if (!settings.IsMemberEnabled(member)) { LiveDiagnostics.Decision("radio","suppressed","member_disabled"); return false; }
            using (var diagnosticSpan = LiveDiagnostics.Begin("radio", LiveDiagnostics.Data("speaker",member.ToString(),"text",text,"priority",localOnly?localPriority:isReply?5:0)))
            {
                try
                {
            if (!CrewSpeechPolicy.HasSpokenText(text))
            {
                { LiveDiagnostics.Decision("radio","suppressed","no_spoken_text"); return false; }
            }
            int generation = Volatile.Read(ref sessionGeneration);
            DateTime deadline = DateTime.UtcNow.AddSeconds(CrewSpeechPolicy.LifetimeSeconds(localOnly, localPriority));
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
                using (var renderDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    renderDeadline.CancelAfter(TimeSpan.FromSeconds(CrewSpeechPolicy.LifetimeSeconds(localOnly, localPriority)));
                    pcm = (CrewSpeechPolicy.CacheOnly(localOnly, localPriority, member) ||
                        localOnly && localPriority>=7 && CrewSpeechPolicy.IsPreparedPhrase(member,text)) ? PreparedSpeech(member, text) :
                        await RenderAsync(member, text, renderDeadline.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                { LiveDiagnostics.Decision("radio","suppressed","tts_cancelled"); return false; }
            }
            catch (Exception e)
            {
                Log("Sprachausgabe fehlgeschlagen: " + e.Message);
                { LiveDiagnostics.Decision("radio","suppressed","tts_error"); return false; }
            }
            if (!AiAudio.HasSpeechSignal(pcm))
            { LiveDiagnostics.Decision("radio","suppressed","tts_empty_or_silent",text); return false; }
            if (!settings.IsMemberEnabled(member) || ct.IsCancellationRequested || DateTime.UtcNow > deadline || generation != Volatile.Read(ref sessionGeneration) || audioPlayer == null)
            {
                { LiveDiagnostics.Decision("radio","suppressed","cancelled_stale_or_no_audio"); return false; }
            }
            // Nicht-dringliche KI-Meldungen nach TTS nicht mitten im Zweikampf abspielen.
            // Sofortige Fahrerantworten und lokale Sicherheits-/Schadensmeldungen bleiben möglich.
            if (!isReply && (Spotter().HasOverlap || radioDirector.TrafficQuiet(DateTime.UtcNow)))
            {
                Log("Nicht-dringlicher KI-Funk wegen Verkehr verworfen");
                { LiveDiagnostics.Decision("radio","suppressed","traffic_quiet"); return false; }
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
                { LiveDiagnostics.Decision("radio","suppressed","validity_changed"); return false; }
            }
            if (ct.IsCancellationRequested || generation != Volatile.Read(ref sessionGeneration) ||
                (stillValid != null && !stillValid(text))) { LiveDiagnostics.Decision("radio","suppressed","validity_changed_after_wait"); return false; }
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("tts.ready",LiveDiagnostics.Data("speaker",member.ToString(),"bytes",pcm.Length));
            byte[] wav = AiAudio.PcmToWav(pcm, AiAudio.OpenAiPcmSampleRate);
            var mw = MainWindow.instance;
            if (mw == null || mw.crewChief == null || !mw.crewChief.running)
            {
                // Crew Chief ist nicht gestartet (kein Spiel): Testausgabe direkt, sonst verwirft die Audioengine den Clip
                try
                {
                    LiveDiagnostics.Decision("audio","direct_playback","no_running_sim");
                    AiAudio.PlayDirect(wav, AudioPlayer.naudioMessagesPlaybackDeviceId);
                }
                catch (Exception e)
                {
                    Log("Direkte Wiedergabe fehlgeschlagen: " + e.Message);
                    { LiveDiagnostics.Decision("radio","suppressed","direct_playback_error"); return false; }
                }
                Memory.AddRadio(member, text, DateTime.UtcNow);
                DriverMemory.RememberRadio(member,text,Memory.SessionId);
                if (playbackStarted != null) playbackStarted();
                return true;
            }
            string id = AiAudioStore.Put(wav, TeamRouter.DisplayName(member) + ": " + text, () => {
                    string reason = !settings.IsMemberEnabled(member) ? "member_disabled" : DateTime.UtcNow > deadline ? "audio_deadline_expired" :
                        generation != Volatile.Read(ref sessionGeneration) ? "session_changed" :
                        stillValid != null && !stillValid(text) ? "event_or_facts_changed" :
                        !isReply && (Spotter().HasOverlap || radioDirector.TrafficQuiet(DateTime.UtcNow)) ? "traffic_priority" : null;
                    if (reason != null) LiveDiagnostics.Decision("audio", "suppressed", reason);
                    return reason == null;
                },
                () => { if (generation == Volatile.Read(ref sessionGeneration)) {
                    Memory.AddRadio(member, text, DateTime.UtcNow);
                DriverMemory.RememberRadio(member,text,Memory.SessionId);
                    if (playbackStarted != null) playbackStarted();
                } }, discarded, playbackEnded);
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("radio.enqueue",LiveDiagnostics.Data("clip_id",id,"topic",observedTopic,"speaker",member.ToString(),"text",text,"priority",localOnly?localPriority:isReply?5:SoundMetadata.DEFAULT_PRIORITY,"immediate",isReply));
            var fragments = new List<MessageFragment> { MessageFragment.Text(SoundCache.AI_AUDIO_IDENTIFIER + id) };
            if (isReply)
            {
                // End-to-end deadline is checked again at driver playback by AiAudioStore.
                var message = new QueuedMessage("ai_team_reply_" + id, Math.Max(1, (int)Math.Ceiling((deadline-DateTime.UtcNow).TotalSeconds)), messageFragments: fragments,
                    type: localOnly ? (localPriority >= 6 ? SoundType.CRITICAL_MESSAGE : SoundType.IMPORTANT_MESSAGE) : SoundType.VOICE_COMMAND_RESPONSE, priority: localOnly ? localPriority : 5);
                audioPlayer.playMessageImmediately(message, interruptForAiHazard: localOnly && localPriority >= 7);
            }
            else
            {
                var message = new QueuedMessage("ai_team_info_" + id, 20, messageFragments: fragments,
                    type: SoundType.REGULAR_MESSAGE, priority: SoundMetadata.DEFAULT_PRIORITY);
                audioPlayer.playMessage(message);
            }
            return true;

                }
                catch (Exception diagnosticError) { diagnosticSpan.Fail(diagnosticError); throw; }
            }
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
            using (var diagnosticSpan = LiveDiagnostics.Begin("tts", LiveDiagnostics.Data("speaker",member.ToString())))
            {
                try
                {
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("tts.render",LiveDiagnostics.Data("speaker",member.ToString(),"service",settings.TtsProvider.ToString(),"model",settings.TtsProvider==AiTtsProvider.ELEVENLABS?settings.ElevenLabsModel:settings.TtsModel));
            string cacheKey = CrewSpeechPolicy.CacheKey(settings, member, text);
            lock (phraseCache)
            {
                byte[] cached;
                if (phraseCache.TryGetValue(cacheKey, out cached))
                {
                    LiveDiagnostics.Decision("tts","cache_hit","memory");
                    return cached;
                }
            }
            // kurze Standardsätze liegen dauerhaft auf der Platte, damit sie nur einmal Credits kosten
            bool cacheable = CrewSpeechPolicy.IsPreparedPhrase(member, text);
            string diskFile = cacheable ? PhraseCacheFile(member, text) : null;
            if (diskFile != null && File.Exists(diskFile))
            {
                try
                {
                    byte[] stored = File.ReadAllBytes(diskFile);
                    lock (phraseCache)
                    {
                        phraseCache[cacheKey] = stored;
                    }
                    LiveDiagnostics.Decision("tts","cache_hit","disk");
                    return stored;
                }
                catch (Exception) { }
            }
            // Respect the selected source. A failed provider must not change the crew's identity.
            byte[] pcm = null;
            if (settings.TtsProvider == AiTtsProvider.ELEVENLABS && elevenLabs.HasKey && !ElevenLabsPaused)
                pcm = await TryTtsAsync("ElevenLabs", t => elevenLabs.SpeechPcmAsync(settings.ElevenLabsVoiceFor(member), settings.ElevenLabsModel, text, member, t), ct).ConfigureAwait(false);
            else if (settings.TtsProvider == AiTtsProvider.WINDOWS)
                pcm = WindowsTts.Render(text, settings.WindowsVoiceFor(member), member);
            else if (settings.TtsProvider != AiTtsProvider.ELEVENLABS && HasApiKey)
                pcm = await TryTtsAsync("Cloud-Stimme", t => client.SpeechPcmAsync(settings.TtsModel, settings.VoiceFor(member), text, AiPersonas.VoiceInstructions(member), t), ct).ConfigureAwait(false);
            if (pcm == null || pcm.Length == 0) throw new InvalidOperationException("Gewählte Stimme nicht verfügbar. Schlüssel/Kontingent prüfen. Keine Ersatzstimme verwendet.");
            if (settings.RadioEffect)
            {
                pcm = AiAudio.ApplyRadioEffect(pcm, AiAudio.OpenAiPcmSampleRate);
            }
            // kurze Standardsätze (vor allem Saras) zwischenspeichern
            if (cacheable)
            {
                lock (phraseCache)
                {
                    if (phraseCache.Count > 256)
                    {
                        phraseCache.Clear();
                    }
                    phraseCache[cacheKey] = pcm;
                }
                if (diskFile != null)
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(diskFile));
                        File.WriteAllBytes(diskFile, pcm);
                    }
                    catch (Exception) { }
                }
            }
            return pcm;

                }
                catch (Exception diagnosticError) { diagnosticSpan.Fail(diagnosticError); throw; }
            }
        }

        private bool ElevenLabsPaused { get { return DateTime.UtcNow < elevenLabsPausedUntilUtc; } }

        /// <summary>Dateiname im Stimmen-Cache: hängt an Quelle, Stimme, Modell, Funkfilter und Text.</summary>
        private string PhraseCacheFile(TeamMember member, string text)
        {
            string voice = settings.TtsProvider == AiTtsProvider.ELEVENLABS ? settings.ElevenLabsModel + "|" + settings.ElevenLabsVoiceFor(member)
                : settings.TtsProvider == AiTtsProvider.WINDOWS ? settings.WindowsVoiceFor(member)
                : settings.TtsModel + "|" + settings.VoiceFor(member);
            string key = settings.TtsProvider + "|" + voice + "|radio=" + settings.RadioEffect + "|" + text;
            using (var sha = System.Security.Cryptography.SHA1.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key))).Replace("-", "").ToLowerInvariant();
                return Path.Combine(AiSecretStore.Folder, "stimmen-cache", hash + ".pcm");
            }
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
                var api = e as AiApiException;
                int status = api != null && api.Status.HasValue ? (int)api.Status.Value : 0;
                if (source == "ElevenLabs" && (status == 401 || status == 402 || status == 403 || status == 429))
                {
                    elevenLabsPausedUntilUtc = DateTime.UtcNow + ElevenLabsPause;
                    Log("ElevenLabs lehnt ab, 10 Minuten Pause. Vorbereitete Originalclips bleiben verfügbar, keine Ersatzstimme: " + e.Message);
                    return null;
                }
                Log(source + " nicht verfügbar; gewählte Stimme bleibt unverändert: " + e.Message);
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
                if (settings.TtsProvider == AiTtsProvider.ELEVENLABS)
                {
                    return "elevenlabs|" + settings.ElevenLabsModel + "|" + settings.ElevenLabsVoiceSara + "|radio=" + settings.RadioEffect;
                }
                if (settings.TtsProvider != AiTtsProvider.WINDOWS)
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
            if (MainWindow.instance != null && MainWindow.instance.crewChief != null && MainWindow.instance.crewChief.running)
                return "Crew Chief zuerst regulär stoppen. Während einer laufenden Session wird kein Stimmenpaket geändert.";
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
                    JObject response = await client.ChatAsync(settings.FastModel, messages, null, OpenAiCompatibleClient.RadioReasoningEffort(settings.ReasoningEffort, false), 100, false, cts.Token).ConfigureAwait(false);
                    string text = RequireCompleteAnswer(response, 100);
                    return "OK – " + settings.FastModel + " antwortet in " + sw.ElapsedMilliseconds + " ms: \"" + (text ?? "").Trim() + "\"";
                }
            }
            catch (Exception e)
            {
                return "Fehler nach " + sw.ElapsedMilliseconds + " ms: " + e.Message;
            }
        }

        private int pitVoiceWarmRunning;
        private string preparedPitVoiceKey;
        private DateTime nextPitVoiceAttempt=DateTime.MinValue,pitParkedSince=DateTime.MinValue;
        // Only the actual reported limit is prepared, while parked in the pits. No batch of guessed limits.
        private void MaybePreparePitLimit(GameStateData gs,DateTime now)
        {
            if(gs==null || !gs.PitData.InPitlane || !TrackAwareness.InRange(gs.PositionAndMotionData.CarSpeed,0,1).HasValue)
            {pitParkedSince=DateTime.MinValue;return;}
            if(pitParkedSince==DateTime.MinValue || now<pitParkedSince) {pitParkedSince=now;return;}
            if(now-pitParkedSince<TimeSpan.FromSeconds(10) || radioRenderGate.Busy) return;
            if(gs==null || !settings.IsMemberEnabled(TeamMember.Mik) || !gs.PitData.InPitlane ||
                !TrackAwareness.InRange(gs.PositionAndMotionData.CarSpeed,0,1).HasValue || !PitLaneWatch.Known(gs.PitData.PitSpeedLimit) ||
                currentRequest!=null || Volatile.Read(ref prewarmRunning)!=0 || now<nextPitVoiceAttempt ||
                settings.TtsProvider==AiTtsProvider.WINDOWS ||
                (settings.TtsProvider==AiTtsProvider.ELEVENLABS ? elevenLabs==null || !elevenLabs.HasKey || ElevenLabsPaused : !HasApiKey)) return;
            string identity=CrewSpeechPolicy.VoiceIdentity(settings,TeamMember.Mik);
            string key=identity+"|pit-limit="+Math.Round(gs.PitData.PitSpeedLimit*3.6f);
            if(key==preparedPitVoiceKey || Interlocked.CompareExchange(ref pitVoiceWarmRunning,1,0)!=0) return;
            nextPitVoiceAttempt=now.AddSeconds(60);
            int generation=Volatile.Read(ref sessionGeneration);
            float pitLimit=gs.PitData.PitSpeedLimit;
            var phrases=PitLaneWatch.Phrases(pitLimit).Concat(PitLaneWatch.SpeedPhrases(pitLimit)).ToArray();
            Task.Run(async()=>
            {
                try
                {
                    foreach(string phrase in phrases)
                    {
                        var live=CrewChief.currentGameState;
                        if(generation!=Volatile.Read(ref sessionGeneration) || !settings.Enabled || !settings.IsMemberEnabled(TeamMember.Mik) ||
                            identity!=CrewSpeechPolicy.VoiceIdentity(settings,TeamMember.Mik) || live==null || !live.PitData.InPitlane ||
                            !PitLaneWatch.Known(live.PitData.PitSpeedLimit) || Math.Abs(live.PitData.PitSpeedLimit-pitLimit)>.1f ||
                            !TrackAwareness.InRange(live.PositionAndMotionData.CarSpeed,0,1).HasValue || currentRequest!=null) return;
                        await RenderAsync(TeamMember.Mik,phrase,CancellationToken.None).ConfigureAwait(false);
                    }
                    preparedPitVoiceKey=key;
                    Log("Boxenfunk für "+Math.Round(pitLimit*3.6f)+" Kilometer pro Stunde in der gewählten Mik-Stimme vorbereitet.");
                    LiveDiagnostics.Decision("pit-limit","prepared","selected_voice_actual_session_limit");
                }
                catch(Exception) { LiveDiagnostics.Decision("pit-limit","deferred","selected_voice_preparation_failed"); }
                finally { Interlocked.Exchange(ref pitVoiceWarmRunning,0); }
            });
        }

        /// <summary>Saras Standardsätze vorab rendern, damit lokale Antworten ohne Wartezeit kommen.</summary>
        private int prewarmRunning;
        public string PreparedVoiceStatus { get; private set; } = "Originalclips noch nicht geprüft.";
        public void PrewarmSaraPhrases() { Task.Run(() => PrepareRadioVoicesAsync()); }
        public async Task<string> PrepareRadioVoicesAsync()
        {
            if (Interlocked.CompareExchange(ref prewarmRunning, 1, 0) != 0) return "Vorbereitung läuft bereits.";
            try
            {
                PreparedVoiceStatus = "Originalclips werden geprüft/vorbereitet.";
                int ready=0, failed=0;
                var sara = new[] { "Links frei.", "Rechts frei.", "Noch einer links.", "Noch einer rechts.", "Noch zwei links.", "Noch zwei rechts.",
                    "Frei, keiner neben dir.", "Noch einer links, rechts frei.", "Noch einer rechts, links frei.", SaraLocal.NoDataAnswer }.Concat(FlagWatch.Phrases);
                var phrases = sara.Select(t => new KeyValuePair<TeamMember,string>(TeamMember.Sara,t))
                    .Concat(CrewSpeechPolicy.TrackLimitPhrases.Concat(LiveWarningDispatcher.MikPhrases).Concat(ThermalWatch.PreparedPhrases).Concat(CrewSpeechPolicy.FinishPhrases).Concat(CrewSpeechPolicy.IncidentPhrases).Concat(CrewSpeechPolicy.WeatherPhrases).Concat(PitLaneWatch.UnknownLimitPhrases).Concat(new[]{SessionLapObserver.TimedLapStartPhrase}).Select(t => new KeyValuePair<TeamMember,string>(TeamMember.Mik,t)))
                    .Concat(TrackSituationWatch.CatchingPhrases.Concat(TrackSituationWatch.StoppedPhrases).Concat(TrackSituationWatch.ApproachingPhrases).Concat(RaceTrafficWatch.PreparedPhrases).Concat(NativeIncidentWatch.Phrases).Concat(RaceSituationWatch.PreparedPhrases(settings.RadioName)).Select(t => new KeyValuePair<TeamMember,string>(TeamMember.Ben,t)))
                    .Where(p=>settings.IsMemberEnabled(p.Key)).ToArray();
                foreach (var phrase in phrases)
                {
                    if (!settings.IsMemberEnabled(phrase.Key)) continue;
                    try { await RenderAsync(phrase.Key,phrase.Value,CancellationToken.None).ConfigureAwait(false); ready++; }
                    catch (Exception) { failed++; }
                }
                PreparedVoiceStatus = ready + "/" + phrases.Length + " Originalclips bereit; " + failed + " fehlen. Sara-Spotterpack: " + (SaraSpotterPackCurrent ? "geprüft" : "fehlt/veraltet");
                Log(PreparedVoiceStatus); return PreparedVoiceStatus;
            }
            finally { Interlocked.Exchange(ref prewarmRunning,0); }
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
                DriverName = settings.RadioName,
                CrewChiefAdditionalFuelNeeded = extraFuel,
                Corners = Corners,
                Tracks = Tracks,
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
            var current=CrewChief.currentGameState;
            DriverMemory.RememberDialogue(member,question,answer,CrewChief.gameDefinition==null?null:CrewChief.gameDefinition.friendlyName,
                current==null||current.SessionData.TrackDefinition==null?null:current.SessionData.TrackDefinition.name,
                current==null?int.MinValue:current.SessionData.SessionId);
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

        private void ConfigureCrewLog(bool enabled)
        {
            int generation = Interlocked.Increment(ref crewLogGeneration);
            var old = Interlocked.Exchange(ref crewLog, null);
            if (old != null) old.RequestStop();
            // Folder enumeration, secret redaction setup and disk writes run outside the telemetry/audio thread.
            Task.Run(() => {
                if (old != null) old.Dispose();
                if (!enabled || generation != Volatile.Read(ref crewLogGeneration)) return;
                try {
                    var next = new DiagnosticRecorder(Path.Combine(DataFiles.BaseFolder, "AiTeam", "Logs"),
                        new DiagnosticLimits { QueueCapacity=256, SegmentBytes=1024*1024, CaptureBytes=4*1024*1024, FolderBytes=32*1024*1024 },
                        new[] { AiSecretStore.Load(), AiSecretStore.LoadElevenLabs() });
                    if (generation != Volatile.Read(ref crewLogGeneration)) { next.Dispose(); return; }
                    if (Interlocked.CompareExchange(ref crewLog, next, null) != null) { next.Dispose(); return; }
                    if (generation != Volatile.Read(ref crewLogGeneration))
                    { Interlocked.CompareExchange(ref crewLog, null, next); next.Dispose(); }
                } catch (Exception e) { Console.WriteLine("[KI-Team] Lokales Log nicht verfügbar: " + e.GetType().Name); }
            });
        }

        private void Log(string text)
        {
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("crew.trace",LiveDiagnostics.Data("text",text));
            Console.WriteLine("[KI-Team] " + text);
            if (!settings.WriteLog)
            {
                return;
            }
            var log = Volatile.Read(ref crewLog);
            try { if (log != null) log.TryRecord("crew.trace", LiveDiagnostics.Data("text", text), sessionId: Memory.SessionId); }
            catch (Exception) { }
        }
    }
}
