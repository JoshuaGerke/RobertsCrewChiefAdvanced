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
        internal static bool LocalSaraEnabledForRoadCourse => instance.IsValueCreated && instance.Value.settings.Enabled &&
            instance.Value.settings.SaraEnabled;
        public static AiTeamService Instance { get { return instance.Value; } }

        public readonly AiUsageStats Stats = new AiUsageStats();
        public readonly AiRaceMemory Memory = new AiRaceMemory();
        public readonly CornerCoach Corners = new CornerCoach();
        public TrackAwareness Tracks { get; private set; } = new TrackAwareness();
        private readonly TrackSituationWatch trackWatch = new TrackSituationWatch();
        private readonly RaceTrafficWatch raceTraffic = new RaceTrafficWatch();
        private readonly InterruptedRadio interruptedRadio = new InterruptedRadio();
        private readonly QualifyingResultWatch qualifyingResults = new QualifyingResultWatch();
        private readonly LiveWarningDispatcher liveWarnings = new LiveWarningDispatcher();
        private readonly ChronicleRecorder chronicleRecorder = new ChronicleRecorder();
        private DateTime lastAcNativeEmit = DateTime.MinValue;
        private readonly NativeFieldEvents fieldEvents = new NativeFieldEvents();
        private readonly NativeTimingEvents timingEvents = new NativeTimingEvents();


        private string ReferenceStyle(TeamMember member, string cue) => workload.Guidance(DateTime.UtcNow);
        private ObservedRaceCall pendingTimingDelivery;
        private ObservedRaceCall pendingDamageAdvice;
        private readonly NativeIncidentWatch nativeIncidents = new NativeIncidentWatch();
        private readonly LMUTrackLimitPoints lmuTrackLimits = new LMUTrackLimitPoints();
        private readonly ThermalWatch thermalWatch = new ThermalWatch();
        private readonly NumericDamageWatch numericDamageWatch = new NumericDamageWatch();
        private readonly FuelWatch fuelWatch = new FuelWatch();
        private readonly IncidentWatch incidentWatch = new IncidentWatch();
        private readonly FlagWatch flagWatch = new FlagWatch();
        private readonly WeatherWatch weatherWatch = new WeatherWatch();
        private readonly PitLaneWatch pitLaneWatch = new PitLaneWatch();
        private readonly PitStopTimer pitStopTimer = new PitStopTimer();
        private readonly IncidentPointsWatch incidentPoints = new IncidentPointsWatch();
        private readonly RaceControlFeed raceControlFeed = new RaceControlFeed();
        private readonly RaceSummaryWatch raceSummary = new RaceSummaryWatch();
        private readonly GapTrendWatch gapTrend = new GapTrendWatch();
        private readonly BattleSectorWatch battleSector = new BattleSectorWatch();
        private readonly PitDamageWatch pitDamage = new PitDamageWatch();
        /// <summary>Renningenieur-Kopf: führt die Rennlage zusammen und entscheidet, welche Info-Meldung jetzt wichtig ist.</summary>
        private readonly RaceMind raceMind = new RaceMind();
        private readonly TyreGripWatch tyreGrip = new TyreGripWatch();
        private readonly QualiFuelWatch qualiFuel = new QualiFuelWatch();
        /// <summary>Robs Bestzeiten-Datenbank: Allzeit-Bestzeiten je Auto/Strecke mit Bedingungen, Kommentare in Training und Quali.</summary>
        private readonly PersonalBestWatch personalBest = new PersonalBestWatch();
        /// <summary>Der Renningenieur erklärt dauerhafte Lagewechsel (Angriff, Verteidigung, sicher, Schlussphase, Ruhe).</summary>
        private readonly FocusBriefWatch focusBrief = new FocusBriefWatch();
        private readonly EndgameBriefWatch endgameBrief = new EndgameBriefWatch();
        private readonly TyreWearWatch tyreWear = new TyreWearWatch();
        private readonly BackmarkerWatch backmarkers = new BackmarkerWatch();
        private readonly DriverJudgementWatch judgement = new DriverJudgementWatch();
        private readonly CrewMemory crewMemory = new CrewMemory();
        /// <summary>RRCA 2.0 (4.2): Ereignisgedächtnis mit Status je Ansage.</summary>
        private readonly RaceEventLog eventLog = new RaceEventLog();
        private readonly RaceInsightWatch insights = new RaceInsightWatch();
        private readonly PitLossMeter pitLoss = new PitLossMeter();
        private readonly DrivingDynamicsWatch dynamics = new DrivingDynamicsWatch(DynamicsSampler.Analyzer);
        private bool cornersFromSampler;
        private static string GameName => CrewChief.gameDefinition == null ? null : CrewChief.gameDefinition.friendlyName;
        private readonly LapWatch lapWatch = new LapWatch();
        private readonly SessionLapObserver sessionLaps = new SessionLapObserver();
        private readonly Dictionary<string, KeyValuePair<DateTime, ObservedRaceCall>> recentLocalCalls = new Dictionary<string, KeyValuePair<DateTime, ObservedRaceCall>>();
        private readonly DriverWorkload workload = new DriverWorkload();
        private readonly RaceReader raceReader = new RaceReader();
        private readonly RaceRadioDirector radioDirector = new RaceRadioDirector();
        private readonly ActiveRaceObserver activeRace = new ActiveRaceObserver();
        private readonly RaceFeedbackWatch raceFeedback = new RaceFeedbackWatch();
        private readonly PitCycleWatch pitCycle = new PitCycleWatch();

        internal JObject LiveRadioStatus(GameStateData live, DateTime now)
        {
            return new JObject {
                ["mode"] = workload.Mode.ToString(),
                ["routineOpen"] = settings.Enabled && workload.AllowsRoutine(now),
                ["fuelTarget"] = raceFeedback.ActiveFuelTarget(live),
                ["chaseActive"] = raceFeedback.ChaseIsActive(live),
                ["pitExit"] = pitCycle.ReadForecast(live, now)?.Status,
                ["inputHz"] = raceReader.InputRate(live, now) is double inputHz ? (JToken)Math.Round(inputHz) : null
            };
        }

        private readonly RaceSituationWatch situationWatch = new RaceSituationWatch();
        private readonly RadioRenderGate radioRenderGate=new RadioRenderGate();
        private readonly AiRadioSchedule radioSchedule = new AiRadioSchedule();
        private readonly SessionGreetingDirector greetingDirector = new SessionGreetingDirector();
        private readonly CornerDetector cornerDetector = new CornerDetector();
        private bool detectCorners;
        private string detectTrackName;
        private DateTime lastStatusUtc = DateTime.MinValue;
        private DateTime lastFuelCheckUtc = DateTime.MinValue;
        private ObservedRaceCall pitFinishCall;
        private string cornerTrackName;
        private int cornerLandmarkCount = -1;
        private int cornerTrackSession;

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
        private int freeAnswerSequence;
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
            RadioPhraseCatalog.LoadForApplication();
            RaceDataTools.CrewChiefRunning = CrewIsRunning;
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
            Field.FieldService.ViewSink = OnFieldView;
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

        internal static bool NativeIntakeEnabled => instance.IsValueCreated && instance.Value.settings.Enabled;

        public void ReloadSettings()
        {
            ApplySettings(AiTeamSettings.Load());
        }

        private void ApplySettings(AiTeamSettings newSettings)
        {
            settings = newSettings;
            RadioLanguage.Current = RadioLanguage.Resolve(settings.RadioLanguageSetting);
            NativeTrafficBridge.Enabled = settings.Enabled && settings.ReplaceCrewChiefVoice && settings.BenEnabled;
            NativeTrafficBridge.Clear();
            NativeIncidentWatch.Enabled=settings.Enabled && settings.ReplaceCrewChiefVoice && settings.BenEnabled;
            nativeIncidents.Reset();
            timingEvents.Reset();pendingTimingDelivery=null;
            CrewChiefV4.Events.Timings.NativeTimingHandoff=settings.Enabled && settings.ReplaceCrewChiefVoice
                ? new Func<NativeTimingFact,bool>(fact=> { if(settings.BenEnabled)timingEvents.Capture(fact);return true; }) : null;
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
            // Nach einem Stimm- oder Anbieterwechsel die Warnclips gleich neu vorbereiten, nicht erst auf Knopfdruck
            if (settings.Enabled && audioPlayer != null && (HasApiKey || (elevenLabs != null && elevenLabs.HasKey)))
            {
                PrewarmSaraPhrases();
            }
            if (settings.Enabled && audioPlayer != null)
            {
                pushToTalk.Start();
            }
            else if (!settings.Enabled)
            {
                DriverRadioGate.Reset();
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
            if (member == TeamMember.Sara)
            {
                user.setProperty("enable_spotter", enabled);
                GlobalBehaviourSettings.spotterEnabledInitialState = enabled;
                if (enabled) MainWindow.instance?.crewChief?.enableSpotter();
                else MainWindow.instance?.crewChief?.disableSpotter();
            }
            user.saveUserSettings();
            // Keep voice caches and live telemetry intact; only change role selection.
            settings = AiTeamSettings.Load();
            NativeTrafficBridge.Enabled = settings.Enabled && settings.ReplaceCrewChiefVoice && settings.BenEnabled;
            NativeTrafficBridge.Clear();
            NativeIncidentWatch.Enabled=settings.Enabled && settings.ReplaceCrewChiefVoice && settings.BenEnabled;
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
            if (current.RequiresNativeSourceClock && current.SourceTelemetryUtc == DateTime.MinValue &&
                (current.SessionData.SessionPhase == SessionPhase.Green || current.SessionData.SessionPhase == SessionPhase.FullCourseYellow))
                return;
            int session = current.SessionData.SessionId;
            bool newSession = session != Memory.SessionId || (previous != null && previous.SessionData.SessionType != current.SessionData.SessionType) ||
                (current.SessionData.IsNewSession && previous != null && !previous.SessionData.IsNewSession);
            if (newSession)
            {
                OnSessionCleared();
                Corners.SetTrack(null, 0);
                pendingGreeting = null; greetingAttempts = 0; greetingRetryAfter = DateTime.MinValue;
                trackWelcome = null; trackWelcomeDone = false;
                RaceStartLatch.Reset();
                Interlocked.Exchange(ref callsThisSession, 0);
                lastProactiveLap = -1;
                fuelWatch.Reset();
                lastFuelCheckUtc = DateTime.MinValue;
                pitFinishCall = null;
                incidentWatch.Reset();
                flagWatch.Reset();
                weatherWatch.Reset();
                pitLaneWatch.Reset();
                pitStopTimer.Reset();
                incidentPoints.Reset();
                raceControlFeed.Reset();
                raceSummary.Reset();
                gapTrend.Reset();
                battleSector.Reset();
                pitDamage.Reset();
                raceMind.Reset();
                tyreGrip.Reset();
                qualiFuel.Reset();
                personalBest.Reset();
                focusBrief.Reset();
                endgameBrief.Reset();
                tyreWear.Reset();
                backmarkers.Reset();
                judgement.Reset();
                crewMemory.Clear();
                eventLog.Clear();
                insights.Reset();
                OpponentStopLog.Reset();
                lapWatch.Reset(LapWatch.SameDisqualification(previous, current));
                sessionLaps.Reset();
                lock (recentLocalCalls) recentLocalCalls.Clear();
                radioDirector.Reset();
                workload.Reset();
                raceReader.Reset();
                activeRace.Reset();
                raceFeedback.Reset();
                pitCycle.Reset();
                Interlocked.Exchange(ref freeAnswerSequence, 0);
                situationWatch.Reset();
                Tracks.Reset();
                trackWatch.Reset();
                raceTraffic.Reset();
                qualifyingResults.Reset();
                liveWarnings.Reset();
                fieldEvents.Reset();
                timingEvents.Reset();pendingTimingDelivery=null;
                Interlocked.Exchange(ref pendingDamageAdvice, null);
                nativeIncidents.Reset();
                thermalWatch.Reset();
                numericDamageWatch.Reset();
                NativeTrafficBridge.Clear();
                radioSchedule.Reset();
                lastStatusUtc = now;
                cornerTrackName = null;
                cornerLandmarkCount = -1;
                sessionSeenUtc = now;
                DriverMemory.BeginSession(CrewChief.gameDefinition==null?null:CrewChief.gameDefinition.friendlyName,
                    current.SessionData.TrackDefinition==null?null:current.SessionData.TrackDefinition.name,
                    current.carName,current.SessionData.SessionType.ToString(),current.SessionData.SessionId);
                DriverMemory.BeginMeasuredSession(RaceFactContext.FromGameState(CrewChief.gameDefinition==null?null:CrewChief.gameDefinition.friendlyName,current,now),
                    current.SessionData.SessionType.ToString(),current.SessionData.SessionId);
            }
            // Leichtgewichtige lokale Auswertung; max. 20 Hz, keine Cloud-Anfrage.
            bool sampleDue = radioDirector.Observe(now, current);
            bool routineDue = radioDirector.RoutineDue(now, previous, current);
            UpdateCornerTrack(current);
            bool fastSpatial = TelemetrySourceClock.SupportsNativeCadence(Game.game) && current.SourceTelemetryUtc != DateTime.MinValue;
            if ((sampleDue || fastSpatial) && Tracks.Observe(current, now, fastSpatial) && sampleDue)
                LiveDiagnostics.TrackPositions(Tracks.Snapshot);
            var gapContext=new RaceDataContext {GameState=current,Tracks=Tracks,NowUtc=now,
                LastTelemetryUtc=current.SourceTelemetryUtc==DateTime.MinValue?current.Now:current.SourceTelemetryUtc};
            var measuredFront=RaceDataTools.ReadGap(gapContext,RaceDataTools.Neighbour(current,true),true);
            var measuredRear=RaceDataTools.ReadGap(gapContext,RaceDataTools.Neighbour(current,false),false);
            var steeringUnits = Game.AMS2 ? SteeringMeasurement.Normalized : Game.IRACING ? SteeringMeasurement.Radians : SteeringMeasurement.Unknown;
            workload.Observe(current, now, Spotter().HasOverlap, Tracks.Snapshot, measuredFront, measuredRear, steeringUnits);
            bool lapCompleted = Memory.Update(previous, current, now, measuredFront, measuredRear);
            raceMind.Observe(current, now, Spotter().HasOverlap, measuredFront, measuredRear, Tracks.Snapshot, () => BuildContext(current));
            ObservedRaceCall cornerImprovement = null;
            if (lapCompleted)
            {
                LiveDiagnostics.Lap(Memory);
                FuelCarry.Remember(current, Memory.AverageFuelPerLap(out int carryLaps, 1000), carryLaps);
                DriverMemory.RememberLap(CrewChief.gameDefinition == null ? null : CrewChief.gameDefinition.friendlyName,
                    current.SessionData.TrackDefinition == null ? null : current.SessionData.TrackDefinition.name,
                    current.carName,Memory.GetLaps(1).LastOrDefault(),current.SessionData.SessionId);
                var lap = Memory.GetLaps(1).LastOrDefault();
                string game=CrewChief.gameDefinition==null?null:CrewChief.gameDefinition.friendlyName;
                var measuredContext=RaceFactContext.FromGameState(game,current,now);
                DateTime source=current.SourceTelemetryUtc==DateTime.MinValue?current.Now:current.SourceTelemetryUtc;
                if(!newSession && previous!=null && measuredContext.SameVehicleTrack(RaceFactContext.FromGameState(game,previous,now)) &&
                    now>=source && now-source<=TimeSpan.FromSeconds(1))
                    DriverMemory.RememberMeasuredLap(measuredContext,lap,current.SessionData.SessionType.ToString(),current.SessionData.SessionId);
                bool cleanLap = lap != null && lap.Valid && !lap.InPitLap;
                Corners.CompleteLap(current.SessionData.CompletedLaps, current.SessionData.LapTimePrevious,
                    current.SessionData.PreviousLapWasValid && cleanLap, lap?.Green == true);
                cornerImprovement = Corners.TakeImprovement(current, lap, now);
                if (detectCorners)
                {
                    FinishCornerDetection(cleanLap);
                }
            }
            if (!newSession && previous != null &&
                (!previous.PitData.InPitlane && current.PitData.InPitlane ||
                 !previous.PitData.IsInGarage && current.PitData.IsInGarage ||
                 previous.SessionData.SessionPhase != SessionPhase.Finished && current.SessionData.SessionPhase == SessionPhase.Finished))
                DriverMemory.EndMeasuredStint();
            if (raceReader.IsDue(now))
            {
                var readerContext = new RaceDataContext { GameState = current, NowUtc = now, LastTelemetryUtc = Memory.LastTickUtc,
                    Memory = Memory, Tracks = Tracks, SendDriverNames = settings.SendDriverNames, DriverName = settings.RadioName, EventLog = eventLog };
                raceReader.Observe(readerContext, workload, thermalWatch);
            }
            if (sampleDue && detectCorners && !current.PitData.InPitlane)
            {
                cornerDetector.Add(current.PositionAndMotionData.DistanceRoundTrack, current.PositionAndMotionData.CarSpeed * 3.6f);
            }
            // Rob 17.11.: Liefert die schnelle Abtastung (100 Hz), misst das Kurven-Coaching Bremspunkte daraus – auf unter einen
            // Meter genau statt 5–7 m. Beim Wechsel der Quelle laufende Kurvenmessungen verwerfen (andere Zeitbasis).
            bool fastCorners = DynamicsSampler.FeedingFrames;
            if (fastCorners != cornersFromSampler) { Corners.ResetTrackers(); cornersFromSampler = fastCorners; }
            if (sampleDue && !fastCorners) Corners.Tick(current.PositionAndMotionData.DistanceRoundTrack, current.PositionAndMotionData.CarSpeed,
                current.ControlData.ThrottlePedal, current.ControlData.BrakePedal, current.SessionData.SessionRunningTime, current.PitData.InPitlane,
                current.TransmissionData.Gear);
            if (Spotter().HasOverlap)
            {
                lastOverlapUtc = now;
            }
            Board.RaceBoard.Update(current, Field.FieldPicture.Shared.Current, now);
            RaceExclusion.Observe(current);
            bool excluded = RaceExclusion.Active(current);
            if (raceRhythm.Observe(current, now)) radioSchedule.StaggerAfterStart(now, settings.RadioPreferences);
            bool startPhase = raceRhythm.InStart(current);
            radioSchedule.StartCalm = startPhase; radioSchedule.Length = raceRhythm.Length;
            if (lapCompleted || now - lastFuelCheckUtc >= TimeSpan.FromSeconds(1))
            {
                lastFuelCheckUtc = now;
                CheckFuel(current, now);
            }
            if (settings.ReplaceCrewChiefVoice)
            {
                foreach (var flagCall in flagWatch.Evaluate(previous, current, now))
                {
                    string call = flagCall;
                    Log("Flagge Sara: " + call);
                    // RaceRoom 20.11.: „Unfall voraus!“ ohne Namen – vereinbart war, wer dort steht.
                    // AC 20.11.: auch „Gelb im Sektor“ und „Gelb! Langsam“ nennen den Betroffenen, nicht nur „Unfall voraus“.
                    string incidentDriver = settings.SendDriverNames && FlagWatch.NamesIncident(call) ? FlagWatch.IncidentName(current, now) : null;
                    // Rob 21.11.: „Unfall voraus! Das ist Güven“ direkt nach „sehr langsames Fahrzeug voraus. Das ist Güven“ – einmal reicht.
                    if (incidentDriver != null && RaceTrafficWatch.SpokenRecently(incidentDriver, now, TimeSpan.FromSeconds(8), false))
                    {
                        if (FlagWatch.Kind(call) == "unfall_voraus") { LiveDiagnostics.Decision("warning-flag-8", "suppressed", "hazard_already_named:" + incidentDriver); continue; }
                        incidentDriver = null;
                    }
                    if (incidentDriver != null) RaceTrafficWatch.NoteSpoken(incidentDriver, now, true);
                    if (FlagWatch.NamesIncident(call) && current.SessionData.SessionType == SessionType.Race)
                        RaceChronicle.Current.Add(current, now, ChronicleKind.Yellow, detail: FlagWatch.Kind(call), source: "flaggen",
                            subjectName: FlagWatch.IncidentName(current, now), dedupe: TimeSpan.FromSeconds(20));
                    liveWarnings.Offer("warning-flag-"+Array.IndexOf(FlagWatch.Phrases,call),call,TeamMember.Sara,FlagWatch.Priority(call),current,now,
                        live=>FlagWatch.StillRelevant(call,live),"mapped_flag_transition",
                        timed: (live, when) => FlagWatch.FreshClear(call, now, when),
                        opponentNameClip: incidentDriver == null ? null : RaceTrafficWatch.NameClipText(incidentDriver));
                }
            }
            var incidents = incidentWatch.Evaluate(previous, current, now, settings.RadioMode == AiRadioMode.MINIMAL);
            string penaltyCall = incidentWatch.TakePenaltyCall();
            foreach (var incident in incidents)
            {
                var radio = incident;
                if (penaltyCall != null && radio.Value == penaltyCall) continue; // Pflichtmeldung, läuft über die Warnungen
                if (excluded) { LiveDiagnostics.Decision("incident", "suppressed", "player_out_of_race", radio.Value); continue; }
                if(CrewSpeechPolicy.TrackLimitPhrases.Contains(radio.Value))
                {
                    int warningLap=current.SessionData.CompletedLaps;
                    bool invalid=IncidentWatch.CurrentLapInvalid(current,now);
                    int cuts=current.PenaltiesData.CutTrackWarnings;
                    liveWarnings.Offer(invalid?"warning-invalid-lap":"warning-tracklimit",radio.Value,radio.Key,7,current,now,
                        live=>!live.PitData.InPitlane && live.SessionData.CompletedLaps==warningLap &&
                            (invalid?IncidentWatch.CurrentLapInvalid(live,DateTime.UtcNow):live.PenaltiesData.CutTrackWarnings>=cuts),
                        invalid?"mapped_current_lap_invalid":"mapped_cut_warning",warningLap);
                    continue;
                }
                if (CrewSpeechPolicy.IncidentPhrases.Contains(radio.Value)) workload.NoteIncident(now);
                Log("Zwischenfall " + TeamRouter.DisplayName(radio.Key) + ": " + radio.Value);
                var valid = TimelyValidity(current, now, 6, live => live.SessionData.CompletedLaps == current.SessionData.CompletedLaps);
                radioSchedule.NoteRadio(now);
                int incidentPriority = CrewSpeechPolicy.HeavyContactPhrases.Contains(radio.Value) ? 7 : 6;
                // The following priority-7 damage report may not interrupt the driver's safety check.
                Task.Run(() => SpeakAsync(radio.Key, radio.Value, true, CancellationToken.None, valid, true, incidentPriority));
            }
            if (penaltyCall != null && !excluded)
            {
                DateTime penaltyAt = now;
                liveWarnings.Offer("warning-penalty", penaltyCall, TeamMember.Ben, 9, current, now, live => true, "mapped_penalty_data",
                    timed: (live, when) => when >= penaltyAt && when - penaltyAt < MandatoryPenaltyWindow);
            }
            bool lateRace = current.SessionData.SessionType == SessionType.Race &&
                (current.SessionData.SessionPhase == SessionPhase.Green || current.SessionData.SessionPhase == SessionPhase.Checkered) &&
                (LapWatch.LapsLeft(current.SessionData) == 2 || LapWatch.OnLastLap(current.SessionData));
            var closingRival = lateRace ? RaceDataTools.Neighbour(current, true) : null;
            string closingName = settings.SendDriverNames && closingRival?.CanUseName == true ?
                RadioPhraseCatalog.SafeRivalName(closingRival.DriverRawName) : null;
            lapWatch.SendDriverNames = settings.SendDriverNames;
            string lapCall = lapWatch.Evaluate(previous, current,
                measuredFront.Kind == RaceGapKind.SimulatorTiming && (!lateRace || closingRival != null) ? measuredFront.Seconds : -1,
                measuredRear.Kind == RaceGapKind.SimulatorTiming ? measuredRear.Seconds : -1,
                settings.RadioMode == AiRadioMode.MINIMAL, rivalName: closingName,
                finalTactic: lateRace ? RaceDataTools.FinalLapTactic(gapContext) : null, driverName: settings.RadioName);
            if (lapCall != null && !LapWatch.ConfirmedDisqualification(current) && (current.SessionData.SessionHasFixedTime || current.SessionData.SessionPhase != SessionPhase.Green || settings.RadioMode == AiRadioMode.MINIMAL))
            {
                if (current.SessionData.SessionPhase == SessionPhase.Finished && current.SessionData.SessionType == SessionType.Race &&
                    !current.SessionData.IsDNF && !current.SessionData.IsDisqualified)
                {
                    lapCall += incidentWatch.FinishSummary(Memory.RecordedInvalidLaps);
                }
                Log("Rundenansage Mik: " + lapCall);
                string closingKey = current.SessionData.OpponentKeyInFront;
                string closingIdentity = closingRival == null ? null : RaceReader.Identity(closingKey, closingRival);
                bool includesFront = lapWatch.IncludesFrontGap;
                int closingLap = current.SessionData.CompletedLaps;
                string coreLapCall = includesFront ? lapWatch.CoreCall : null;
                var valid = TimelyValidity(current, now, 12, live => live.SessionData.SessionPhase == current.SessionData.SessionPhase &&
                    live.SessionData.ClassPosition == current.SessionData.ClassPosition && live.SessionData.OverallPosition == current.SessionData.OverallPosition &&
                    (current.SessionData.SessionPhase != SessionPhase.Green || live.SessionData.CompletedLaps == closingLap));
                var detailValid = TimelyValidity(current, now, 12, live => !live.FlagData.isLocalYellow &&
                    (closingName == null || settings.SendDriverNames) &&
                    live.SessionData.OpponentKeyInFront == closingKey && RaceDataTools.Neighbour(live, true) is OpponentData front &&
                    RaceReader.Identity(closingKey, front) == closingIdentity && live.SessionData.TimeDeltaFront > 0 &&
                    Math.Abs(live.SessionData.TimeDeltaFront - measuredFront.Seconds) <= .5f);
                radioSchedule.NoteRadio(now);
                string referenceTemplate = lapWatch.ReferenceTemplate;
                Action onPlayed = referenceTemplate == null ? (Action)null :
                    () => LiveDiagnostics.Decision("last-lap", "template_played", referenceTemplate);
                bool paceReviewNames = settings.SendDriverNames;
                var paceReview = current.SessionData.SessionPhase == SessionPhase.Finished &&
                    current.SessionData.SessionType == SessionType.Race && !current.SessionData.IsDNF &&
                    !current.SessionData.IsDisqualified && settings.RadioMode != AiRadioMode.MINIMAL &&
                    settings.IsMemberEnabled(TeamMember.Ben) ? RaceDataTools.RacePaceRadio(BuildContext(current)) : null;
                Action<bool> afterFinish = paceReview == null ? (Action<bool>)null : interrupted => {
                    if (!interrupted && valid(paceReview.Text))
                        Task.Run(() => SpeakAsync(TeamMember.Ben, paceReview.Text, true, CancellationToken.None,
                            _ => valid(paceReview.Text) && settings.RadioMode != AiRadioMode.MINIMAL &&
                                (!paceReviewNames || settings.SendDriverNames) &&
                                paceReview.StillCurrent(CrewChief.currentGameState, DateTime.UtcNow), true, 3));
                };
                Task.Run(async () =>
                {
                    if (coreLapCall != null && !detailValid(lapCall))
                    {
                        await SpeakAsync(TeamMember.Mik, coreLapCall, true, CancellationToken.None, valid, true, 4, onPlayed).ConfigureAwait(false);
                        return;
                    }
                    Action discarded = coreLapCall == null ? (Action)null : () =>
                    {
                        if (valid(coreLapCall) && !detailValid(lapCall))
                            Task.Run(() => SpeakAsync(TeamMember.Mik, coreLapCall, true, CancellationToken.None, valid, true, 4, onPlayed));
                    };
                    bool queued = await SpeakAsync(TeamMember.Mik, lapCall, true, CancellationToken.None,
                        text => valid(text) && (!includesFront || detailValid(text)), true, 4, onPlayed,
                        discarded: discarded, playbackEnded: afterFinish).ConfigureAwait(false);
                    if (!queued) discarded?.Invoke();
                });
            }
            var damage = incidentWatch.TakeDamageAssessment();
            if (damage != null)
            {
                int damageGeneration = Volatile.Read(ref sessionGeneration);
                // Ein noch nicht gesprochener dringender Boxenruf geht in der Bewertung auf; war er schon zu hören,
                // ergänzt die Bewertung nur noch, was er nicht gesagt hat.
                bool wheelsSaid = !pitDamage.AbsorbUrgent(now) && pitDamage.UrgentJustSpoken(now);
                Task.Run(() => ReportDamageAsync(damage, damageGeneration, wheelsSaid));
            }
            sessionLaps.Observe(current, lapCompleted ? Memory.GetLaps(1).LastOrDefault() : null, now, settings.SendDriverNames, previous,
                DriverMemory.QualifyingSectorRadioEnabled, Tracks, settings.RadioName, Memory);
            var bestStore = DriverMemory.Enabled ? PersonalBestStore.For(GameName) : null;
            if (lapCompleted)
            {
                var wearStore = DriverMemory.Enabled ? PersonalBestStore.For(GameName) : null;
                var wearCtx = RaceFactContext.FromGameState(GameName, current, now);
                float[] storedRate = wearStore != null && wearCtx.IdentityKnown
                    ? wearStore.StoredWear(wearCtx.Game, wearCtx.Car, wearCtx.Track, wearCtx.Layout, PersonalBestStore.Compound(current)) : null;
                tyreWear.Observe(current, Memory.GetLaps(1).LastOrDefault(), Memory, AiPersonas.CleanForRadio(settings.RadioName ?? "", 30, false), now, storedRate);
            }
            if (lapCompleted) personalBest.ObserveLap(current, Memory.GetLaps(1).LastOrDefault(), bestStore, GameName, settings.RadioName, now,
                seconds => sessionLaps.AnnouncedOwnLap(seconds, now));
            if (lapCompleted) PersonalBestWatch.RecordWear(current, bestStore, GameName, Memory.AverageWearPerLap(3), now);
            personalBest.Observe(current, bestStore, GameName, settings.RadioName, now);
            var radioPrefs = settings.RadioPreferences;
            activeRace.Observe(current, now, settings.RadioName, 0, Memory, settings.SendDriverNames);
            if (settings.RadioMode != AiRadioMode.MINIMAL && settings.IsMemberEnabled(TeamMember.Mik))
            {
                if (cornerImprovement != null)
                {
                    LiveDiagnostics.Decision(cornerImprovement.Topic,"detected",cornerImprovement.Evidence,cornerImprovement.Text,cornerImprovement.EventId);
                    activeRace.Enqueue(cornerImprovement);
                }
                else if (lapCompleted && CornerCoach.CanFollow(current))
                {
                    var hint = Corners.FindCoachingHint();
                    if (hint != null)
                    {
                        var advice = new ObservedRaceCall { Topic="corner-coaching",
                            Text=RadioLanguage.T("In " + hint.Name + " verlierst du wiederholt Zeit. Nimm dir die Kurve in der nächsten Runde vor.",
                                "You keep losing time in " + hint.Name + ". Work on that corner next lap.",
                                "Sigues perdiendo tiempo en " + hint.Name + ". Trabaja esa curva en la próxima vuelta."),
                            Evidence=hint.Evidence, Member=TeamMember.Mik, Priority=3, Position=-1,
                            SessionId=current.SessionData.SessionId, SessionType=current.SessionData.SessionType,
                            CompletedLaps=current.SessionData.CompletedLaps, DetectedUtc=now, ExpiresUtc=now.AddSeconds(9),
                            AdditionalValidity=CornerCoach.CanFollow, PlaybackCompleted=_=>Corners.RememberDeliveredHint(hint) };
                        LiveDiagnostics.Decision(advice.Topic,"detected",advice.Evidence,advice.Text,advice.EventId);
                        activeRace.Enqueue(advice);
                    }
                }
            }
            gapContext.SendDriverNames = settings.SendDriverNames;
            pitCycle.ObservePitExit(gapContext, previous);
            if (sampleDue && settings.RadioMode != AiRadioMode.MINIMAL && settings.IsMemberEnabled(TeamMember.Ben))
            {
                var pitResult = pitCycle.Observe(gapContext, measuredFront, measuredRear);
                if (pitResult != null)
                {
                    Memory.AddObservation(pitResult.Text,now,pitResult.Topic,null,pitResult.Evidence);
                    LiveDiagnostics.Decision(pitResult.Topic,"detected",pitResult.Evidence,pitResult.Text,pitResult.EventId);
                    activeRace.Enqueue(pitResult);
                }
            }
            activeRace.ObserveRearPressure(gapContext, measuredRear);
            if (current.SessionData.SessionType == SessionType.Race)
            {
                var feedback = raceFeedback.Observe(gapContext, previous, lapCompleted ? Memory.GetLaps(1).LastOrDefault() : null,
                    measuredRear, activeRace.RearPressureActive(current, now), settings.RadioName);
                if (feedback != null && settings.RadioMode != AiRadioMode.MINIMAL && settings.IsMemberEnabled(feedback.Member))
                {
                    LiveDiagnostics.Decision(feedback.Topic, "detected", feedback.Evidence, feedback.Text, feedback.EventId);
                    activeRace.Enqueue(feedback);
                }
            }
            if (routineDue) radioSchedule.Observe(current, now, radioPrefs);
            foreach (var situation in situationWatch.Evaluate(previous,current,now,settings.RadioName))
            {
                LiveDiagnostics.Decision(situation.Topic, "detected", situation.Evidence, situation.Text, situation.EventId);
                activeRace.Enqueue(situation);
            }
            var trafficCall = trackWatch.Evaluate(Tracks, current, now, DriverMemory.RearTrafficAlertSeconds);
            if (trafficCall != null) activeRace.Enqueue(trafficCall);
            raceTraffic.Observe(Tracks,current,now);
            qualifyingResults.Observe(current,now,settings.RadioName,previous);
            weatherWatch.Observe(current,now);
            pitLaneWatch.Observe(current,now);
            pitStopTimer.DriverName = AiPersonas.CleanForRadio(settings.RadioName ?? "", 30, false);
            pitStopTimer.Observe(current,now);
            incidentPoints.Observe(current,now);
            raceSummary.Observe(current,raceMind,() => BuildContext(current),now);
            gapTrend.Observe(current,raceMind,() => BuildContext(current),now);
            battleSector.Observe(current, raceMind, Memory, settings.SendDriverNames, now);
            {
                var frontCar = RaceDataTools.Neighbour(current, true); var rearCar = RaceDataTools.Neighbour(current, false);
                string frontName = settings.SendDriverNames && frontCar != null && frontCar.CanUseName ? RadioPhraseCatalog.SafeRivalName(frontCar.DriverRawName) : null;
                string rearName = settings.SendDriverNames && rearCar != null && rearCar.CanUseName ? RadioPhraseCatalog.SafeRivalName(rearCar.DriverRawName) : null;
                focusBrief.Observe(current, RaceMind.Latest, frontName, rearName, AiPersonas.CleanForRadio(settings.RadioName ?? "", 30, false), now);
                endgameBrief.Observe(current, RaceMind.Latest, frontName, rearName, now);
                backmarkers.Observe(current, Tracks.Snapshot, RaceMind.Latest, settings.SendDriverNames, frontName, rearName, now);
            }
            DataTruth.Current.Observe(current, now);
            VirtualEnergyWatch.Current.Observe(current, VirtualEnergyWatch.Read());
            if (lapCompleted && current.SessionData.CompletedLaps % 3 == 0)
                LiveDiagnostics.Emit("data-truth", DataTruth.Current.Report());
            // Rob 17.11.: schnelle Abtastung (100 Hz) für Fahrdynamik – eigener Faden, liest das Shared Memory direkt.
            if (DynamicsSampler.Supports(Game.game))
            {
                DynamicsSampler.Analyzer.CornerAt = Corners.CornerNameAt;
                if (DynamicsSampler.FrameSink == null)
                {
                    var coach = Corners;
                    DynamicsSampler.FrameSink = f => coach.Tick(f.LapDistance, f.Speed, f.Throttle, f.Brake, (float)f.TimeS, f.InPit, f.Gear);
                }
                DynamicsSampler.Ensure(Game.game);
                dynamics.Observe(current, now, lapCompleted, AiPersonas.CleanForRadio(settings.RadioName ?? "", 30, false));
                // Auch nach der ersten Runde, damit eine kurze Testfahrt die Quelle (z. B. „LMU nativ · 100 Hz“) belegt.
                if (lapCompleted && (current.SessionData.CompletedLaps % 3 == 0 || current.SessionData.CompletedLaps == 1))
                    LiveDiagnostics.Emit("dynamics", new Dictionary<string, object> { ["status"] = DynamicsSampler.Status });
                // Rob 21.11.: mehr native AC-Daten – erst messen (alle 10 s), dann nutzen.
                if (LiveDiagnostics.Enabled && now - lastAcNativeEmit >= TimeSpan.FromSeconds(10))
                {
                    var ac = AcNativeExtras.Fresh(now);
                    if (ac != null) { lastAcNativeEmit = now; LiveDiagnostics.Emit("ac-native", ac); }
                }
            }
            if (lapCompleted && current.SessionData.SessionType == SessionType.Race) pitLoss.Observe(current.SessionData.SessionId, Memory.GetLaps());
            insights.Observe(current, RaceMind.Latest, AiPersonas.CleanForRadio(settings.RadioName ?? "", 30, false), settings.SendDriverNames, now, lapCompleted,
                pitLoss.Seconds);
            judgement.Observe(current, Memory.GetLaps(12), lapCompleted, AiPersonas.CleanForRadio(settings.RadioName ?? "", 30, false), settings.SendDriverNames, now);
            OpponentStopLog.Observe(current);
            pitDamage.Observe(current, Tracks, AiPersonas.CleanForRadio(settings.RadioName ?? "", 30, false), now);
            tyreGrip.Observe(current, now);
            qualiFuel.Observe(current, Memory, settings.RadioName, now);
            MaybePreparePitLimit(current,now);
            MaybePrepareOpponentName(current, now);
            bool weatherBusy = !settings.IsMemberEnabled(TeamMember.Mik) || Spotter().HasOverlap ||
                radioRenderGate.Busy || currentRequest != null;
            bool pitBusy=!settings.IsMemberEnabled(TeamMember.Mik) || Spotter().HasOverlap || radioRenderGate.UrgentBusy;
            if(sampleDue || lapCompleted) thermalWatch.Observe(current,now,liveWarnings);
            if(sampleDue)
            {
                numericDamageWatch.Observe(current,now,liveWarnings);
            }
            nativeIncidents.Observe(current,now,liveWarnings,Memory,settings.SendDriverNames);
            fieldEvents.CaptureMappedPits(previous,current);
            fieldEvents.CaptureDepartures(previous,current,now);
            chronicleRecorder.Observe(current, now);
            fieldEvents.CaptureMappedFastestLap(previous, current);
            fieldEvents.RadioName = settings.RadioName;
            fieldEvents.Observe(current,now,Memory,settings.SendDriverNames);
            timingEvents.Observe(current,now,Memory,settings.SendDriverNames);
            if(pendingTimingDelivery!=null && pendingTimingDelivery.DeliveryStartedUtc!=DateTime.MinValue) {
                radioSchedule.NoteRelatedReport(pendingTimingDelivery.Topic=="native-gap-front"?"ahead":"behind",pendingTimingDelivery.DeliveryStartedUtc,radioPrefs);
                pendingTimingDelivery=null;
            }
            else if(pendingTimingDelivery!=null && !pendingTimingDelivery.IsValid(current,now))pendingTimingDelivery=null;
            liveWarnings.IncidentName = settings.SendDriverNames ? (Func<GameStateData, DateTime, string>)FlagWatch.IncidentName : null;
            liveWarnings.Observe(current,now);
            // Rennleitung aller Sims (Startvergehen, Abkürzen mit Frist, Verwarnung, Box zu/offen …): Chronik, Lagebild, Pflichtfunk.
            raceControlFeed.Observe(current, now, liveWarnings);
            // LMU: Rennleitung aus dem nativen Datenstrom – alles mitschreiben, eigene Strafen/Tracklimits sofort an die Crew.
            LMURaceControlStream.Line raceControl;
            while (LMURaceControlStream.TryTake(out raceControl))
            {
                string kind = LMURaceControlStream.Classify(raceControl);
                LiveDiagnostics.Decision("lmu-race-control", kind == null ? "logged" : "classified", (raceControl.Tag ?? "line") + (kind == null ? "" : ";" + kind),
                    LMURaceControlStream.Plain(raceControl.Text));
                Memory.AddObservation(RadioLanguage.T("Rennleitung: ", "Race control: ", "Dirección de carrera: ") + LMURaceControlStream.Plain(raceControl.Text), now, "race-control");
                raceControlFeed.ObserveLmuLine(raceControl, current, now);
                if (kind != null && now >= raceControl.Utc && now - raceControl.Utc < TimeSpan.FromSeconds(5) && current.SessionData.SessionPhase == SessionPhase.Green)
                    liveWarnings.OfferRaceControl(kind, current, now, raceControl.Tag ?? "line");
            }
            var lmuNative = LMUNativeSnapshot.Fresh(now);
            string limitPoints = lmuNative == null ? null : lmuTrackLimits.Observe(lmuNative, current);
            if (limitPoints != null)
            {
                int limitLap = current.SessionData.CompletedLaps;
                liveWarnings.Offer("warning-tracklimit", limitPoints, TeamMember.Mik, 7, current, now,
                    live => !live.PitData.InPitlane && live.SessionData.CompletedLaps == limitLap, "lmu_native_track_limit_steps=" + lmuNative.TrackLimitsSteps, limitLap);
            }
            var raceCall = liveWarnings.Take(current,now,Spotter().HasOverlap || radioRenderGate.UrgentBusy,settings.IsMemberEnabled);
            if (raceCall == null) raceCall = pitLaneWatch.Take(current,now,pitBusy,7);
            if (raceCall == null) raceCall = pitDamage.Take(current, now, Spotter().HasOverlap || radioRenderGate.UrgentBusy, settings.IsMemberEnabled);
            if (raceCall == null) raceCall = incidentPoints.Take(current,now,Spotter().HasOverlap || radioRenderGate.UrgentBusy,settings.IsMemberEnabled(TeamMember.Mik));
            if (raceCall == null) raceCall = raceTraffic.Take(current,now,Spotter().HasOverlap || radioRenderGate.UrgentBusy,settings.IsMemberEnabled(TeamMember.Ben));
            // Rob 16.11.: Reifen-Grip (heiß, im Fenster, dreckig) – Mik, sobald der Funk frei ist.
            if (raceCall == null) raceCall = tyreWear.Take(current,now,Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null,settings.IsMemberEnabled(TeamMember.Mik));
            if (raceCall == null) raceCall = tyreGrip.Take(current,now,Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null,settings.IsMemberEnabled(TeamMember.Mik));
            var damageAdvice = Interlocked.Exchange(ref pendingDamageAdvice, null);
            if (damageAdvice != null && damageAdvice.IsValid(current, now)) activeRace.Enqueue(damageAdvice);
            bool ownLapBusy = settings.RadioMode == AiRadioMode.MINIMAL ||
                !settings.IsMemberEnabled(TeamMember.Mik) || Spotter().HasOverlap ||
                radioRenderGate.UrgentBusy || radioRenderGate.ResultBusy;
            if (raceCall == null && settings.IsMemberEnabled(TeamMember.Mik))
                raceCall = lapWatch.TakeDisqualification(current, now, Spotter().HasOverlap || radioRenderGate.UrgentBusy, settings.RadioName);
            if (raceCall == null) raceCall = sessionLaps.Take(current, now, ownLapBusy, ownLapOnly: true);
            if (raceCall == null) raceCall = TakePitFinish(current, now);
            if (raceCall == null) raceCall = personalBest.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null,
                settings.IsMemberEnabled(TeamMember.Mik) && settings.RadioMode != AiRadioMode.MINIMAL);
            if (raceCall == null && !proactiveRunning && currentRequest == null)
            {
                // Rob 21.11.: Unterbrochenes nachreichen, sobald der Funk frei ist.
                bool free = !radioRenderGate.Busy && AiAudioStore.Count == 0 && !Spotter().HasOverlap && !radioDirector.TrafficQuiet(now);
                string resumed = interruptedRadio.TryTake(now, free, SessionPhaseKey(current), out var resumeMember, out var resumeTopic);
                if (resumed != null)
                {
                    LiveDiagnostics.Decision("resume", "detected", "interrupted_" + (resumeTopic ?? "reply"), resumed);
                    Task.Run(() => SpeakAsync(resumeMember, resumed, true, CancellationToken.None, _ => true, true, 4, observedTopic: "resume"));
                }
            }
            if (raceCall == null) raceCall = TakeSessionBriefing(current, now);
            if (raceCall == null) raceCall = qualifyingResults.Take(current,now,Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null,settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null) raceCall = qualiFuel.Take(current,now,Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null,settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = raceSummary.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null || proactiveRunning ||
                    !AllowsRadioNow(false), settings.IsMemberEnabled(TeamMember.Ben)); // keine Bremszone, keine Kurvenlast
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = backmarkers.Take(current, now, Spotter().HasOverlap || radioRenderGate.UrgentBusy || currentRequest != null,
                    settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = judgement.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null || proactiveRunning || sessionLaps.HotLap(current),
                    settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = insights.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null || proactiveRunning,
                    settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = dynamics.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null || proactiveRunning ||
                    sessionLaps.HotLap(current) || !AllowsRadioNow(false) || !settings.IsMemberEnabled(dynamics.PendingMember));
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = focusBrief.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null || proactiveRunning,
                    settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = endgameBrief.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null || proactiveRunning,
                    settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = gapTrend.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null || proactiveRunning,
                    settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = battleSector.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null || proactiveRunning,
                    settings.IsMemberEnabled(TeamMember.Ben));
            if (routineDue && raceCall == null && !proactiveRunning && settings.RadioMode != AiRadioMode.MINIMAL &&
                current.SessionData.SessionType == SessionType.Race && radioSchedule.StatusIsOverdue(now, radioPrefs) &&
                !Spotter().HasOverlap && !radioRenderGate.Busy && currentRequest == null && AllowsRadioNow(true))
                raceCall = radioSchedule.Take(BuildContext(current), radioPrefs, false, settings.IsMemberEnabled);
            if (raceCall == null) raceCall = sessionLaps.Take(current, now, settings.RadioMode == AiRadioMode.MINIMAL ||
                !settings.IsMemberEnabled(TeamMember.Mik) || Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null,
                radioDirector.TrafficQuiet(now));
            if (raceCall == null) raceCall = weatherWatch.Take(current,now,weatherBusy,5);
            if (raceCall == null) raceCall = activeRace.Take(current, now, Spotter().HasOverlap,
                radioRenderGate.Busy || currentRequest != null,
                settings.RadioMode == AiRadioMode.MINIMAL, 0, Memory.GetLaps(), settings.RadioName);
            if (raceCall == null) raceCall = fieldEvents.Take(current,now,Spotter().HasOverlap || radioRenderGate.Busy || currentRequest!=null,
                settings.IsMemberEnabled(TeamMember.Ben),settings.RadioMode==AiRadioMode.MINIMAL,
                fact => activeRace.CoversPit(fact, current, now));
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL &&
                !Spotter().HasOverlap && !radioRenderGate.Busy && currentRequest == null)
            {
                if (settings.IsMemberEnabled(TeamMember.Ben)) raceCall = radioDirector.TakePitHandover(current, now);
                if (raceCall == null && settings.IsMemberEnabled(TeamMember.Mik) &&
                    (!settings.IsMemberEnabled(TeamMember.Ben) || !radioDirector.PitHandoverPending(now)))
                    raceCall = pitCycle.TakeMergeCall(gapContext);
            }
            if (raceCall == null && settings.RadioMode != AiRadioMode.MINIMAL)
                raceCall = raceReader.Take(current, now, Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null || sessionLaps.HotLap(current), settings.IsMemberEnabled);
            if (raceCall == null) {
                raceCall=timingEvents.Take(BuildContext(current),Spotter().HasOverlap || radioRenderGate.Busy || currentRequest!=null || sessionLaps.HotLap(current),
                    settings.IsMemberEnabled(TeamMember.Ben),settings.RadioMode==AiRadioMode.MINIMAL);
                if(raceCall!=null)pendingTimingDelivery=raceCall;
            }
            if (raceCall == null) raceCall = pitLaneWatch.Take(current,now,weatherBusy);
            if (raceCall == null) raceCall = pitStopTimer.Take(current,now,Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null,
                settings.IsMemberEnabled(TeamMember.Ben));
            if (raceCall == null) raceCall = weatherWatch.Take(current,now,weatherBusy || radioDirector.TrafficQuiet(now));
            if (routineDue && raceCall == null)
            {
                string routineBusy=settings.RadioMode==AiRadioMode.MINIMAL?"minimal_mode":Spotter().HasOverlap?"spotter_overlap":
                    sessionLaps.HotLap(current)?"push_lap":
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
                if (closing != null && CrewChiefV4.Events.Timings.NativeTimingHandoff==null && !gapTrend.CoversRecently(true, current.SessionData.CompletedLaps))
                {
                    trigger = "Gemessener Gegnertrend: " + closing + " Ben soll sachlich berichten, ohne Überholversprechen.";
                    topic = "opponent-gap-trend";
                }
                else if (approaching != null && CrewChiefV4.Events.Timings.NativeTimingHandoff==null && !gapTrend.CoversRecently(false, current.SessionData.CompletedLaps))
                {
                    trigger = "Gemessener Gegnertrend hinten: " + approaching + " Ben berichtet nur den Messbefund, keine riskante Verteidigungsanweisung.";
                    topic = "opponent-behind-trend";
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
            if (trigger != null && excluded) { LiveDiagnostics.Decision(topic, "suppressed", "player_out_of_race", trigger); trigger = null; }
            if (trigger != null && startPhase) { LiveDiagnostics.Decision(topic, "suppressed", "race_start_phase_essentials_only", trigger); trigger = null; }
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
            var landmarks = track.trackLandmarks;
            int landmarkCount = landmarks?.Count ?? 0;
            if (track.name == cornerTrackName && landmarkCount == cornerLandmarkCount &&
                gs.SessionData.SessionId == cornerTrackSession)
            {
                return;
            }
            cornerTrackName = track.name;
            cornerLandmarkCount = landmarkCount;
            cornerTrackSession = gs.SessionData.SessionId;
            detectCorners = false;
            var corners = new List<CornerDefinition>();
            for (int i = 0; i < landmarkCount; i++)
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
        private void CheckFuel(GameStateData gs, DateTime now)
        {
            var s = gs.SessionData;
            DateTime source = gs.SourceTelemetryUtc == DateTime.MinValue ? gs.Now : gs.SourceTelemetryUtc;
            if (s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Green ||
                gs.PitData.InPitlane || gs.PitData.IsInGarage || !gs.FuelData.FuelUseActive ||
                source == DateTime.MinValue || now < source || now - source > TimeSpan.FromSeconds(1))
            {
                return;
            }
            var estimate = FuelEstimate.From(BuildContext(gs));
            if (estimate == null) return;
            string warning = fuelWatch.Evaluate(s.CompletedLaps, gs.FuelData.FuelLeft, estimate.PerLap, estimate.LapsRemaining,
                gs.PitData.IsRefuellingAllowed, estimate.EstimatedFromTime, out float savingTarget, estimate.AdditionalWithoutReserve,
                estimate.ProvisionalConsumption);
            if (warning != null)
            {
                var valid=TimelyValidity(gs,now,6, live => {
                    DateTime liveSource = live.SourceTelemetryUtc == DateTime.MinValue ? live.Now : live.SourceTelemetryUtc;
                    DateTime checkUtc = DateTime.UtcNow;
                    if (live.SessionData.SessionPhase != SessionPhase.Green ||
                        live.PitData.InPitlane || live.PitData.IsInGarage ||
                        checkUtc < liveSource || checkUtc - liveSource > TimeSpan.FromSeconds(1)) return false;
                    var fresh=FuelEstimate.From(BuildContext(live));
                    return fresh!=null && fresh.ProvisionalConsumption == estimate.ProvisionalConsumption &&
                        (fresh.AdditionalWithoutReserve>0)==(estimate.AdditionalWithoutReserve>0) &&
                        (fresh.AdditionalWithoutReserve + fresh.ReserveLitres > 0) ==
                            (estimate.AdditionalWithoutReserve + estimate.ReserveLitres > 0) &&
                        Math.Abs(fresh.AdditionalWithoutReserve-estimate.AdditionalWithoutReserve)<.3f;
                });
                Log("Sprit-Warnung: " + warning); radioSchedule.NoteRadio(now);
                int generation = Volatile.Read(ref sessionGeneration);
                Task.Run(() => SpeakAsync(TeamMember.Ben, warning, true, CancellationToken.None, valid, true, 7,
                    playbackEnded: interrupted => {
                        if (!interrupted && savingTarget > 0 && estimate.LapsRemaining >= 3 &&
                            generation == Volatile.Read(ref sessionGeneration))
                            raceFeedback.RememberDeliveredFuelPlan(CrewChief.currentGameState, savingTarget);
                    }));
            }
        }

        public void OnSessionCleared()
        {
            Interlocked.Increment(ref sessionGeneration);
            interruptedRadio.Clear();
            DriverMemory.EndMeasuredStint();
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
            // Rob 16.11.: Funkstille, solange er spricht – auch für Crew-Chief-Ansagen; die Warteschlange läuft danach weiter.
            DriverRadioGate.Pressed(DateTime.UtcNow);
            recorder.Start(settings.Microphone);
            var running = currentRequest;
            if (running != null)
            {
                try { running.Cancel(); } catch (ObjectDisposedException) { }
            }
            var playing = SoundCache.currentlyPlayingSound;
            if (playing != null && !playing.IsSafetyRadio && !playing.isBleep)
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
            DriverRadioGate.Released(DateTime.UtcNow);
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
                await SpeakAsync(TeamMember.Mik, RadioLanguage.T("Für die Spracherkennung fehlt noch der API-Schlüssel.", "Speech recognition still needs the API key.", "Al reconocimiento de voz todavía le falta la clave API."), true, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            string text;
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("stt.input",LiveDiagnostics.Data("utterance_sequence",sequence,"bytes",wav.Length,"source","microphone_in_memory_only"));
            try
            {
                text = TeamRouter.FixSimNames(await TranscribeWithFallbackAsync(wav).ConfigureAwait(false));
            }
            catch (Exception e)
            {
                Log("Spracherkennung fehlgeschlagen: " + e.Message);
                Status("Spracherkennung fehlgeschlagen: " + e.Message);
                await SpeakAsync(TeamMember.Mik, SpokenError(e, RadioLanguage.T("Funk war nicht zu verstehen, die Verbindung hakt.", "Couldn't understand that, the connection is patchy.", "No se te ha entendido, la conexión falla.")), true, CancellationToken.None).ConfigureAwait(false);
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
            // Nur ohne ausdrücklichen Namen umleiten; wer „Ben, …“ sagt, erfährt, dass Ben ausgeschaltet ist.
            var available = routed.AddressedByName ? (TeamMember?)routed.Member : TeamRouter.Available(routed.Member, settings.IsMemberEnabled, RecentMember());
            if (available.HasValue && available.Value != routed.Member)
            {
                LiveDiagnostics.Decision("question","rerouted","member_disabled:" + routed.Member + "->" + available.Value);
                routed.Member = available.Value;
            }
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

            if (!TeamRouter.IsSideQuestion(routed.Question))
            {
                Func<(string Text, string Evidence)> evaluateTraffic = () => {
                    var live = CrewChief.currentGameState;
                    string answer = LocalAnswers.TrackNeighbour(routed.Question, new RaceDataContext {
                        GameState = live, Tracks = Tracks, NowUtc = DateTime.UtcNow,
                        LastTelemetryUtc = live == null ? DateTime.MinValue :
                            live.SourceTelemetryUtc == DateTime.MinValue ? live.Now : live.SourceTelemetryUtc,
                        SendDriverNames = settings.SendDriverNames
                    }, out var evidence);
                    return (answer, evidence);
                };
                var trafficAnswer = evaluateTraffic();
                if (trafficAnswer.Text != null)
                {
                    Stats.AddLocalAnswer();
                    for (int attempt = 0; attempt < 2 && generation == Volatile.Read(ref sessionGeneration); attempt++)
                    {
                        var answer = trafficAnswer;
                        if (await SpeakAsync(routed.Member, answer.Text, true, CancellationToken.None,
                            _ => generation == Volatile.Read(ref sessionGeneration) &&
                                LocalAnswers.NeighbourReplyStillCurrent(answer, evaluateTraffic())).ConfigureAwait(false))
                        {
                            RememberExchange(routed.Member, routed.Question, answer.Text);
                            return answer.Text;
                        }
                        var fresh = evaluateTraffic();
                        if (fresh.Text == null || fresh.Equals(trafficAnswer)) break;
                        trafficAnswer = fresh;
                    }
                    return null;
                }
            }

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
            if (repeat == "wiederhol" || repeat == "wiederhole" || repeat == "nochmal" || repeat == "bitte wiederholen" || repeat == "was hast du gesagt" ||
                repeat == "repeat" || repeat == "say again" || repeat == "repeat that" || repeat == "what did you say" || repeat == "come again" ||
                repeat == "repite" || repeat == "repítelo" || repeat == "repitelo" || repeat == "otra vez" || repeat == "qué has dicho" || repeat == "que has dicho")
            {
                var lastSpoken = Memory.GetRadio(TimeSpan.FromMinutes(3), DateTime.UtcNow).LastOrDefault();
                string repeated = lastSpoken == null ? RadioLanguage.T("Es liegt noch kein bestätigter Funkspruch vor.", "Nothing confirmed on the radio yet.", "Todavía no hay ningún mensaje confirmado por radio.") :
                    RadioLanguage.T("Zuletzt am Funk: ", "Last on the radio: ", "Lo último por radio: ") + lastSpoken.Text;
                await SpeakAsync(routed.Member, repeated, true, CancellationToken.None, _ => generation == Volatile.Read(ref sessionGeneration)).ConfigureAwait(false);
                return repeated;
            }

            // 2. Einfache Standardfragen ohne KI
            string preferenceReply;
            if(routed.Member!=TeamMember.Sara && DriverMemory.TryRadioPreference(routed.Question,out preferenceReply))
            {
                Stats.AddLocalAnswer();
                await SpeakAsync(routed.Member,preferenceReply,true,CancellationToken.None,_=>generation==Volatile.Read(ref sessionGeneration)).ConfigureAwait(false);
                RememberExchange(routed.Member,routed.Question,preferenceReply);
                return preferenceReply;
            }
            bool comparisonNames = settings.SendDriverNames;
            string comparison = LocalAnswers.CompareRivalLap(routed.Question, BuildContext(gs));
            var quickContext = BuildContext(gs);
            string recentPass = LocalAnswers.RecentOwnPass(routed.Question, quickContext);
            string fuelPlan = RaceFuelPlan.AsksFuelForDistance(routed.Question) ? RaceFuelPlan.Answer(routed.Question, gs, Memory) : null;
            string quick = fuelPlan ?? comparison ?? recentPass ?? LocalAnswers.FuelToFinish(routed.Question, quickContext) ??
                LocalAnswers.PreviousStintFuel(routed.Question, quickContext) ??
                LocalAnswers.FuelConsumption(routed.Question, gs, Memory, settings.RadioName) ??
                LocalAnswers.LapPrediction(routed.Question, gs) ??
                LocalAnswers.TryAnswer(routed.Question, gs, Memory, comparisonNames);
            if (quick != null)
            {
                Stats.AddLocalAnswer();
                // Der Fahrer hat gefragt: lieber der Wert von vor einer Sekunde als gar keine Antwort. Nur ein Sessionwechsel verwirft sie.
                await SpeakAsync(routed.Member, quick, true, CancellationToken.None, _ =>
                    generation == Volatile.Read(ref sessionGeneration) && (!comparisonNames || settings.SendDriverNames) &&
                    (comparison == null || comparison == LocalAnswers.CompareRivalLap(routed.Question, BuildContext(CrewChief.currentGameState))) &&
                    (recentPass == null || recentPass == LocalAnswers.RecentOwnPass(routed.Question, BuildContext(CrewChief.currentGameState)))).ConfigureAwait(false);
                RememberExchange(routed.Member, routed.Question, quick);
                return quick;
            }

            if (!HasApiKey)
            {
                string noKey = RadioLanguage.T("Dafür brauch ich die KI, aber es ist noch kein API-Schlüssel hinterlegt.", "I need the AI for that, but there's no API key set yet.", "Para eso necesito la IA, pero todavía no hay clave API.");
                await SpeakAsync(routed.Member, noKey, true, CancellationToken.None).ConfigureAwait(false);
                return noKey;
            }
            if (settings.MaxCallsPerSession > 0 && callsThisSession >= settings.MaxCallsPerSession)
            {
                string budget = RadioLanguage.T("Das KI-Budget für diese Session ist aufgebraucht. Die lokalen Ansagen laufen weiter.", "The AI budget for this session is used up. Local calls keep running.", "El presupuesto de IA de esta sesión se ha agotado. Los avisos locales siguen.");
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
                // Phase 5 (LMU 18.11.: zweimal „abgebrochen“): Das Zeitlimit gilt für Datenabfragen; die erzwungene letzte
                // Antwortrunde bekommt eine kurze Nachfrist, statt mitten in der Antwort abzubrechen.
                linked.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds + FinalRoundGraceSeconds));
                DateTime deadline = DateTime.UtcNow.AddSeconds(settings.TimeoutSeconds);
                string answer = await AskModelAsync(routed, gs, linked.Token, () => deadline - DateTime.UtcNow).ConfigureAwait(false);
                var knownNames = (settings.SendDriverNames && gs != null ? gs.OpponentData.Values.Where(o => o != null).Select(o => o.DriverRawName) : Enumerable.Empty<string>())
                    .Concat(new[] { settings.RadioName });
                string cleanAnswer = RadioAnswerQuality.Clean(answer, knownNames);
                if (cleanAnswer != answer)
                    LiveDiagnostics.Emit("radio.answer_quality", LiveDiagnostics.Data("status", cleanAnswer == null ? "rejected" : "repaired", "reason", "language_or_incomplete_tail"));
                answer = cleanAnswer ?? RadioLanguage.T("Die Antwort war unklar. Frag bitte noch einmal.", "That answer was unclear. Ask again, please.", "La respuesta no ha quedado clara. Pregunta otra vez.");
                // C4 (Rob 17.11.): Eindeutig widerlegte Behauptungen (eigener Platz, Runden bis zum Ziel) nicht aussprechen.
                if (cleanAnswer != null)
                {
                    string checkedAnswer = AnswerClaimCheck.Check(answer, CrewChief.currentGameState ?? gs, out var refuted);
                    if (refuted.Count > 0)
                    {
                        LiveDiagnostics.Emit("radio.claim_check", LiveDiagnostics.Data("status", checkedAnswer == null ? "rejected" : "repaired",
                            "reason", string.Join(",", refuted)));
                        answer = checkedAnswer ?? RadioLanguage.T("Da bin ich mir gerade nicht sicher. Frag bitte gleich noch einmal.",
                            "I'm not sure about that right now. Ask me again in a moment.", "Ahora mismo no estoy seguro. Pregúntame otra vez en un momento.");
                    }
                }
                if (driverCancel.IsCancellationRequested || generation != Volatile.Read(ref sessionGeneration))
                {
                    return null;
                }
                if ((DateTime.UtcNow - started).TotalSeconds > 25)
                {
                    Log("Antwort verworfen, zu alt: " + answer);
                    return null;
                }
                Func<string, bool> answerStillCurrent = _ => generation == Volatile.Read(ref sessionGeneration) &&
                    RaceSituationWatch.AnswerMatchesStartPhase(gs, CrewChief.currentGameState, DateTime.UtcNow);
                if (!answerStillCurrent(answer))
                {
                    LiveDiagnostics.Decision("question", "suppressed", "start_phase_advanced");
                    return null;
                }
                if (!await SpeakAsync(routed.Member, answer, true, driverCancel.Token, answerStillCurrent).ConfigureAwait(false))
                    return null;
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
                // P4 (Rob 16.11.): Statt „keine Antwort“ eine lokale Antwort oder wenigstens die aktuelle Lage aus Messwerten.
                string fallback = SpokenError(e, LocalFallback(routed.Question, CrewChief.currentGameState) ??
                    RadioLanguage.T("Ich krieg gerade keine Antwort von der Auswertung. Versuch's gleich nochmal.", "I'm not getting an answer from the analysis right now. Try again in a moment.", "Ahora mismo no me llega respuesta del análisis. Inténtalo de nuevo enseguida."));
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

        private async Task<string> AskModelAsync(RoutedRequest routed, GameStateData gs, CancellationToken ct, Func<TimeSpan> remaining = null)
        {
            var ctx = BuildContext(gs);
            bool longAllowed = gs == null || gs.SessionData.SessionPhase == SessionPhase.Garage ||
                (gs.PitData.InPitlane && gs.PositionAndMotionData.CarSpeed < 1);
            string model = NeedsDeepModel(routed.Question) ? settings.DeepModel : settings.FastModel;
            var liveFacts = RaceDataTools.BuildBriefing(ctx);
            var mind = RaceMind.Latest;
            if (mind != null && gs != null && mind.SessionId == gs.SessionData.SessionId && mind.Fresh(DateTime.UtcNow)) liveFacts["rennkopf"] = mind.Describe();
            var fieldInsights = gs == null ? null : RaceInsightWatch.Latest(gs.SessionData.SessionId);
            if (fieldInsights != null && gs.SessionData.SessionType == SessionType.Race) liveFacts["feld_erkenntnisse"] = fieldInsights;
            var drivingDynamics = gs == null ? null : DynamicsSampler.Facts(gs.SessionData.CompletedLaps);
            if (drivingDynamics != null) liveFacts["fahrdynamik"] = drivingDynamics;
            // P4: Während der Fahrt beantwortet das schnelle Modell einfache Fragen in einer einzigen Anfrage.
            bool singleShot = !longAllowed && model == settings.FastModel && !NeedsTools(routed.Question, gs);
            // Robs Bestzeiten-Datenbank: Allzeit-Bestzeit für dieses Auto auf dieser Strecke mit den Bedingungen von damals.
            if (gs != null && DriverMemory.Enabled)
            {
                var factCtx = RaceFactContext.FromGameState(GameName, gs, DateTime.UtcNow);
                var pb = factCtx.IdentityKnown ? PersonalBestStore.For(GameName)?.Find(factCtx.Game, factCtx.Car, factCtx.Track, factCtx.Layout) : null;
                if (pb != null) liveFacts["allzeit_bestzeit_hier"] = pb.ToJson();
            }
            if (routed.Question.IndexOf("sektor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                routed.Question.IndexOf("bestzeit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                routed.Question.IndexOf("split", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var sectorFacts = RaceDataTools.NativeSectorBriefing(ctx);
                if (sectorFacts.HasValues) liveFacts["native_sektorreferenzen"] = sectorFacts;
            }
            if (LocalAnswers.AsksAboutRecentRaceAction(routed.Question))
                liveFacts["bestaetigte_ereignisse_seit_45s"] = RaceDataTools.RecentRaceActions(ctx);
            if (gs?.SessionData.SessionPhase == SessionPhase.Finished && gs.SessionData.SessionType == SessionType.Race)
                liveFacts["rennvergleich_top_drei"] = RaceDataTools.RacePaceReview(ctx);
            if (!longAllowed)
            {
                var corners = RaceDataTools.Execute("get_corner_analysis", ctx);
                var strongest = corners.SelectToken("kurven[0]");
                if (strongest != null) liveFacts["groesster_gemessener_kurvenverlust"] = strongest.DeepClone();
            }

            var messages = new JArray
            {
                new JObject
                {
                    ["role"] = "system",
                    ["content"] = AiPersonas.SystemPrompt(routed.Member, settings.RadioName, longAllowed) + ReferenceStyle(routed.Member, routed.Question) +
                        (routed.Member == TeamMember.Sara ? "" : FreeAnswerOpening(Interlocked.Increment(ref freeAnswerSequence) - 1) +
                            RaceTone(workload.Mode, LapWatch.OnLastLap(gs?.SessionData))) +
                        DriverNotes.PromptBlock(routed.Question, ctx) +
                        "\nvorherige_sitzung_referenz enthält historische eigene Messungen. Nenne ihre Herkunft und leite daraus keine aktuelle Reichweite ab." +
                        "\nBei Rückfragen zu einem Manöver werte bestaetigte_ereignisse_seit_45s in ihrer Reihenfolge aus; die aktuelle Position widerlegt einen früheren Platzgewinn nicht. " +
                        "Für die Rennrundenzahl gilt runden_absolviert; vergleichbare Runden sind eine Teilmenge. Nenne nur ausgewiesene Pace-Deltas." +
                        "\n\nAktuelle Lage (Live-Daten mit Reifen, Sprit, direkten Gegnern und Box). Reicht sie für die Antwort, antworte direkt " +
                        "ohne Werkzeug. Werkzeuge nur für Details, die hier fehlen (Stintverlauf, Rundenzeiten, Schäden/Motor, Wetter, Ereignisse):\n" +
                        liveFacts.ToString(Newtonsoft.Json.Formatting.None) +
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
                () => BuildContext(CrewChief.currentGameState), () => Interlocked.Increment(ref callsThisSession), Log, ct, remaining, singleShot).ConfigureAwait(false);
        }

        internal static string RaceTone(DriverWorkloadMode mode, bool lastLap)
        {
            return lastLap || mode == DriverWorkloadMode.Battle || mode == DriverWorkloadMode.Crisis
                ? "\nAm Funk: ein kurzer, ruhiger Satz mit der wichtigsten belegten Information. Keine Wiederholung des letzten Einstiegs."
                : "\nAm Funk: ruhig einordnen und, falls nötig, einen belegten nächsten Schritt nennen. Variiere den Einstieg gegenüber dem letzten Funkspruch.";
        }

        internal static string FreeAnswerOpening(int sequence)
        {
            switch (sequence & 3)
            {
                case 0: return "\nEinstieg: Beginne mit dem wichtigsten belegten Messwert, falls er die Frage direkt beantwortet.";
                case 1: return "\nEinstieg: Ordne die Lage in wenigen Worten ein. Danach die konkrete Antwort.";
                case 2: return "\nEinstieg: Beginne mit einem belegten Vergleich zum relevanten Gegner oder zur passenden Referenzrunde, sofern verfügbar.";
                default: return "\nEinstieg: Beginne mit dem nächsten sinnvollen Schritt in dieser Runde, sofern sich einer aus den Daten ableiten lässt.";
            }
        }

        /// <summary>
        /// Tool-Calling-Schleife: Modell fragt Werkzeuge an, wir liefern frische Telemetrie, bis eine Textantwort kommt (max. 4 Runden).
        /// </summary>
        /// <summary>Höchstzahl Modellrunden je Frage; die letzte läuft ohne Werkzeuge.</summary>
        internal const int MaxToolRounds = 4;

        public static async Task<string> RunToolLoopAsync(OpenAiCompatibleClient client, string model, string reasoningEffort, JArray messages,
            int maxTokens, int maxChars, Func<RaceDataContext> contextProvider, Action countCall, Action<string> log, CancellationToken ct,
            Func<TimeSpan> remaining = null, bool singleShot = false)
        {
            JArray tools = RaceDataTools.BuildToolDefinitions();
            bool recoveredLength = false;
            for (int round = 0; round < MaxToolRounds; round++)
            {
                if (countCall != null)
                {
                    countCall();
                }
                var sw = Stopwatch.StartNew();
                // P4 (Rob 16.11.): Wird die Zeit knapp, keine weitere Datenabfrage – das Modell muss mit dem Vorhandenen antworten.
                // Einfache Fragen während der Fahrt: Die Lage steckt schon in der Anfrage, also genau eine Anfrage ohne Werkzeugrunde.
                // Quali 20.11.: vier Runden mit je einem Werkzeug, danach nur die Ersatzfloskel. Die letzte Runde muss sprechen.
                bool lastRound = round == MaxToolRounds - 1;
                bool hurry = singleShot || lastRound || round > 0 && remaining != null && remaining() < TimeSpan.FromSeconds(ToolBudgetSeconds);
                if (hurry && log != null) log(singleShot ? "Einfache Frage: eine Anfrage ohne Werkzeuge." : lastRound ? "Letzte Runde: Antwort ohne weitere Werkzeuge erzwungen." : "Zeit knapp: Antwort ohne weitere Werkzeuge erzwungen.");
                JObject response = await client.ChatAsync(model, messages, tools, reasoningEffort, maxTokens, false, ct, hurry ? "none" : "auto").ConfigureAwait(false);
                var message = response.SelectToken("choices[0].message") as JObject;
                if (message == null)
                {
                    throw new AiApiException("Leere Antwort vom Modell.");
                }
                string finish = (string)response.SelectToken("choices[0].finish_reason");
                if (finish == "length")
                {
                    // Do not execute truncated tool calls or speak a cut-off technical recommendation.
                    if (recoveredLength || round == MaxToolRounds - 1) throw new AiApiException("KI-Antwort am Ausgabelimit abgebrochen.");
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
                var savedNotes = new List<string>();
                bool allSaved = true;
                foreach (var call in toolCalls)
                {
                    string name = (string)call.SelectToken("function.name");
                    JObject result;
                    if (name == "save_note")
                    {
                        string note = null;
                        try { note = (string)JObject.Parse((string)call.SelectToken("function.arguments") ?? "{}")["notiz"]; } catch (Exception) { }
                        bool saved=DriverMemory.TryAddNote(note);
                        if (saved) savedNotes.Add(note); else allSaved = false;
                        if (saved && log != null)
                        {
                            log("Fahrerwunsch dauerhaft gespeichert.");
                        }
                        result = new JObject { ["gespeichert"] = saved, ["status"] = DriverMemory.Status, ["hinweis"] = saved ? "Lokal als Fahrerwunsch gespeichert und für künftige KI-Gespräche verfügbar. Keine Änderung der Ereignislogik, Einstellungen oder Software dadurch erfolgt." : "Nicht gespeichert: Gedächtnis ausgeschaltet oder noch nicht verfügbar. Das dem Fahrer ehrlich sagen." };
                    }
                    else
                    {
                        result = RaceDataTools.Execute(name, ctx, (string)call.SelectToken("function.arguments"));
                        allSaved = false;
                    }
                    messages.Add(new JObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = (string)call["id"],
                        ["content"] = result.ToString(Newtonsoft.Json.Formatting.None),
                    });
                }
                // RRCA 2.0 (7): Nur ein Fahrerwunsch gespeichert – dafür keine zweite Modellrunde (Mosport 16.11.: ein Drittel
                // aller Fragen, je rund 2 s). Phase 5 (LMU 18.11.: zehnmal save_note + zweite Runde, je 2–3 s): Hat das Modell
                // seine Funkantwort gleich mitgeschickt, wird sie gesprochen; bei reinem Merkauftrag bestätigen wir lokal.
                string question = (string)messages.LastOrDefault(m => (string)m["role"] == "user")?["content"];
                string quick = SaveRoundAnswer(allSaved, savedNotes, ContentText(message["content"]), question, round, maxChars);
                if (quick != null)
                {
                    if (log != null) log("Fahrerwunsch gespeichert, Bestätigung ohne zweite Modellrunde.");
                    return quick.Length > maxChars && maxChars > 0 ? quick.Substring(0, maxChars) : quick;
                }
            }
            return RadioLanguage.T("Ich komm mit der Auswertung gerade nicht zu einem sauberen Ergebnis.", "I can't get a clean result from the analysis right now.", "Ahora mismo no consigo un resultado limpio del análisis.");
        }

        private static readonly System.Text.RegularExpressions.Regex MemoryVerb = new System.Text.RegularExpressions.Regex(
            @"\b(merk|speicher|notier|vergiss nicht|behalt|remember|save|note|keep in mind|guarda|recuerda|apunta|anota)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        private static readonly System.Text.RegularExpressions.Regex QuestionWord = new System.Text.RegularExpressions.Regex(
            @"\?|\b(wie|was|wann|wo|warum|wieso|welche[rsnm]?|wieviel|how|what|when|where|why|which|qué|cómo|cuándo|dónde|por qué|cuánto)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>Nur ein Merkauftrag ohne eigentliche Frage („Merk dir: …“)? Dann reicht die Bestätigung.</summary>
        internal static bool PureMemoryRequest(string question) =>
            !string.IsNullOrWhiteSpace(question) && MemoryVerb.IsMatch(question) && !QuestionWord.IsMatch(question);

        /// <summary>Bestätigung, wenn eine Modellrunde nur Fahrerwünsche gespeichert hat; null, wenn noch eine Antwort nötig ist.</summary>
        internal static string SaveOnlyAnswer(bool allSaved, IList<string> notes, string modelContent, int variant)
        {
            if (!allSaved || notes == null || notes.Count == 0) return null;
            if (!string.IsNullOrWhiteSpace(modelContent)) return modelContent.Trim();
            string note = string.Join(" ", notes.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim().TrimEnd('.') + "."));
            string head = RadioVariants.At(variant + Environment.TickCount % 5,
                RadioLanguage.T("Notiert:", "Noted:", "Anotado:"),
                RadioLanguage.T("Ist gespeichert:", "Saved:", "Guardado:"),
                RadioLanguage.T("Verstanden, merke ich mir:", "Copy, I'll remember:", "Entendido, lo recuerdo:"),
                RadioLanguage.T("Alles klar, gespeichert:", "All right, saved:", "De acuerdo, guardado:"),
                RadioLanguage.T("Geht klar, steht im Gedächtnis:", "Will do, it's in memory:", "Hecho, queda en memoria:"));
            return note.Length > 1 ? head + " " + note : RadioLanguage.T("Ist gespeichert.", "Saved.", "Guardado.");
        }

        /// <summary>
        /// Antwort nach einer Runde, in der nur Fahrerwünsche gespeichert wurden: der mitgeschickte Funktext des Modells, bei
        /// reinem Merkauftrag sonst eine lokale Bestätigung; null, wenn eine weitere Modellrunde nötig ist.
        /// </summary>
        internal static string SaveRoundAnswer(bool allSaved, IList<string> notes, string modelContent, string question, int variant, int maxChars)
        {
            if (!allSaved || notes == null || notes.Count == 0) return null;
            string spoken = AiPersonas.CleanForRadio(modelContent ?? "", maxChars > 0 ? maxChars : 400, true);
            if (!string.IsNullOrWhiteSpace(spoken) && spoken.Trim().Length >= 8) return spoken.Trim();
            return PureMemoryRequest(question) ? SaveOnlyAnswer(true, notes, null, variant) : null;
        }

        /// <summary>Nachfrist für die erzwungene letzte Antwortrunde über das KI-Zeitlimit hinaus.</summary>
        internal const double FinalRoundGraceSeconds = 4;

        /// <summary>Unter dieser Restzeit wird keine weitere Werkzeugrunde mehr erlaubt.</summary>
        internal const double ToolBudgetSeconds = 4.5;

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
            string hint = RadioLanguage.T("Rennfunk an die Boxencrew Mik, Ben und Sara. Simracing, Le Mans Ultimate, RaceRoom, Reifen, Sprit, Boxenstopp, Undercut.",
                "Race radio to the pit crew Mik, Ben and Sara. Sim racing, Le Mans Ultimate, RaceRoom, tyres, fuel, pit stop, undercut.",
                "Radio de carrera al equipo de boxes Mik, Ben y Sara. Simracing, Le Mans Ultimate, RaceRoom, neumáticos, combustible, parada en boxes, undercut.");
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds)))
                {
                    return await client.TranscribeAsync(wav, settings.SttModel, RadioLanguage.SpeechCode, hint, cts.Token).ConfigureAwait(false);
                }
            }
            catch (AiApiException e) when (e.Status.HasValue && (int)e.Status.Value >= 400 && (int)e.Status.Value != 401 &&
                (int)e.Status.Value != 402 && (int)e.Status.Value != 403 && settings.SttModel != FallbackSttModel && settings.BaseUrl.Contains("openrouter.ai"))
            {
                Log("Spracherkennung mit " + settings.SttModel + " abgelehnt (" + e.Message + "), versuche " + FallbackSttModel);
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds)))
                {
                    return await client.TranscribeAsync(wav, FallbackSttModel, RadioLanguage.SpeechCode, hint, cts.Token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Was Mik bei einem Fehler sagt: die echte Ursache statt pauschal "Verbindung hakt".</summary>
        /// <summary>
        /// P4: Antwortet das Sprachmodell nicht rechtzeitig, kommt eine lokale Antwort (wenn die Frage dazu passt)
        /// oder wenigstens die gemessene Lage – nie nur „keine Antwort“.
        /// </summary>
        private string LocalFallback(string question, GameStateData gs)
        {
            if (gs == null) return null;
            try
            {
                string local = LocalAnswers.TryAnswer(question, gs, Memory, settings.SendDriverNames);
                if (!string.IsNullOrWhiteSpace(local)) return local;
                return QuickStatus(BuildContext(gs), fallbackVariant++);
            }
            catch (Exception) { return null; }
        }
        private int fallbackVariant;

        internal static string QuickStatus(RaceDataContext ctx, int variant)
        {
            var gs = ctx?.GameState; if (gs == null) return null;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race || s.ClassPosition <= 0 || s.SessionPhase != SessionPhase.Green) return null;
            var parts = new List<string>();
            string[] openers = RadioLanguage.Pick(
                new[] { "Die Detailauswertung hängt gerade, kurz das Wichtigste:", "Das dauert mir gerade zu lange, deshalb kurz die Lage:", "Ganz knapp aus den Live-Daten:" },
                new[] { "The detailed analysis is stuck, here's the key point:", "That's taking too long, so quickly the situation:", "Quick from the live data:" },
                new[] { "El análisis detallado va lento, lo esencial:", "Tarda demasiado, así que rápido la situación:", "Rápido, con los datos en vivo:" });
            parts.Add(openers[((variant % openers.Length) + openers.Length) % openers.Length]);
            parts.Add(RaceRadioLanguage.Rank(s, s.ClassPosition) + ".");
            var front = RaceDataTools.Neighbour(gs, true); var rear = RaceDataTools.Neighbour(gs, false);
            var fg = front == null ? null : RaceDataTools.ReadGap(ctx, front, true);
            var rg = rear == null ? null : RaceDataTools.ReadGap(ctx, rear, false);
            if (fg != null && fg.IsRaceGap) parts.Add(RadioLanguage.T("Nach vorn ", "Ahead ", "Por delante ") + GermanRadioText.Interval(fg.Seconds) + ".");
            if (rg != null && rg.IsRaceGap) parts.Add(RadioLanguage.T("Nach hinten ", "Behind ", "Por detrás ") + GermanRadioText.Interval(rg.Seconds) + ".");
            if (IncidentPointsWatch.Known(s) && IncidentPointsWatch.Level(s.CurrentIncidentCount, s.MaxIncidentCount) >= 2)
                parts.Add(RadioLanguage.T("Vorfallpunkte " + s.CurrentIncidentCount + " von " + s.MaxIncidentCount + ".",
                    "Incident points " + s.CurrentIncidentCount + " of " + s.MaxIncidentCount + ".", "Puntos de incidente " + s.CurrentIncidentCount + " de " + s.MaxIncidentCount + "."));
            return string.Join(" ", parts);
        }

        public static string SpokenError(Exception e, string connectionMessage)
        {
            var api = e as AiApiException;
            int status = api != null && api.Status.HasValue ? (int)api.Status.Value : 0;
            string msg = e.Message ?? "";
            if (status == 401 || (msg.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 && msg.IndexOf("key", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return RadioLanguage.T("Der OpenRouter-Schlüssel wird nicht angenommen. Trag ihn bitte im KI-Team-Fenster neu ein.", "The OpenRouter key isn't accepted. Please enter it again in the AI team window.", "La clave de OpenRouter no se acepta. Vuelve a introducirla en la ventana del equipo de IA.");
            }
            if (status == 402 || msg.IndexOf("credit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return RadioLanguage.T("Bei OpenRouter ist kein Guthaben mehr drauf. Bitte aufladen.", "No credit left on OpenRouter. Please top up.", "No queda saldo en OpenRouter. Recárgalo, por favor.");
            }
            if (status == 403)
            {
                return RadioLanguage.T("OpenRouter verweigert die Anfrage. Schau bitte ins KI-Team-Fenster.", "OpenRouter is refusing the request. Please check the AI team window.", "OpenRouter rechaza la petición. Mira la ventana del equipo de IA.");
            }
            if (status == 429)
            {
                return RadioLanguage.T("OpenRouter bremst gerade, zu viele Anfragen. Gleich nochmal versuchen.", "OpenRouter is throttling, too many requests. Try again in a moment.", "OpenRouter está limitando, demasiadas peticiones. Inténtalo otra vez enseguida.");
            }
            if (status >= 400 && status < 500)
            {
                return RadioLanguage.T("Die Anfrage wurde abgelehnt, da stimmt eine Einstellung nicht. Details stehen im KI-Team-Fenster.", "The request was rejected, a setting is wrong. Details are in the AI team window.", "La petición ha sido rechazada, hay un ajuste mal. Los detalles están en la ventana del equipo de IA.");
            }
            return connectionMessage;
        }

        /// <summary>Fragen, deren Daten nicht in der mitgeschickten Lage stecken (Verlauf, Wetter, Schaden, andere Fahrer, Kurven).</summary>
        internal static bool NeedsTools(string question, GameStateData gs)
        {
            string q = (question ?? "").ToLowerInvariant();
            string[] markers = { "wetter", "regen", "schaden", "motor", "öltemp", "oeltemp", "wassertemp", "verlauf", "letzten runden", "rundenzeiten", "runden davor",
                "kurve", "bremspunkt", "feld", "führende", "fuehrende", "wer ist", "wer fährt", "wer liegt", "box", "notiz", "merk",
                // AC 20.11.: „Was hat so viel Temperatur gehabt?“ ohne Werkzeug → „aktuellere Reifendaten liegen mir nicht vor“.
                "temperatur", "heiß", "heiss", "temperature", "hot", "caliente",
                "weather", "rain", "damage", "engine", "history", "corner", "who is", "leader", "pit",
                "clima", "lluvia", "daño", "curva", "quién", "boxes" };
            return markers.Any(m => q.Contains(m)) || gs != null && RaceFieldTools.NamedInQuestion(question, gs).Count > 0;
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
                new JObject { ["role"] = "system", ["content"] = string.Format(AiPersonas.ProactivePrompt, settings.RadioName) + ReferenceStyle(owner, trigger) + DriverNotes.PromptBlock(trigger, ctx) },
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
        private ObservedRaceCall TakePitFinish(GameStateData current, DateTime now)
        {
            if (!settings.IsMemberEnabled(TeamMember.Ben) || settings.RadioMode == AiRadioMode.MINIMAL ||
                Spotter().HasOverlap || radioRenderGate.Busy || currentRequest != null) return null;
            var s = current.SessionData;
            if (s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Finished ||
                s.IsDNF || s.IsDisqualified || s.ClassPosition < 1 || !current.PitData.InPitlane ||
                current.PitData.IsInGarage || current.PositionAndMotionData.CarSpeed > 15) return null;
            if (pitFinishCall == null)
            {
                string name = AiPersonas.CleanForRadio(settings.RadioName ?? "Rob", 30, false);
                string finish = s.ClassPosition == 1 ? RadioLanguage.T("Glückwunsch zum Sieg.", "Congratulations on the win.", "Enhorabuena por la victoria.") :
                    s.ClassPosition <= 3 ? RadioLanguage.T("Glückwunsch zum Podium.", "Congratulations on the podium.", "Enhorabuena por el podio.") :
                    RadioLanguage.T("Gut zu Ende gebracht.", "Well brought home.", "Bien terminado.");
                pitFinishCall = new ObservedRaceCall {
                    Topic = "pit-finish", Text = name + ", " + finish + RadioLanguage.T(" Stell den Wagen an deiner Box ab.", " Park the car in your pit box.", " Deja el coche en tu box."),
                    Member = TeamMember.Ben, Priority = 3, Position = s.ClassPosition,
                    SessionId = s.SessionId, SessionType = s.SessionType, AllowPitlane = true,
                    DetectedUtc = now, ExpiresUtc = now.AddSeconds(20), Evidence = "finished_race_confirmed_pitlane_and_result",
                    AdditionalValidity = live => !live.PitData.IsInGarage && !live.SessionData.IsDNF &&
                        !live.SessionData.IsDisqualified && live.PositionAndMotionData.CarSpeed <= 15
                };
                LiveDiagnostics.Decision(pitFinishCall.Topic,"detected",pitFinishCall.Evidence,pitFinishCall.Text,pitFinishCall.EventId);
            }
            return pitFinishCall.IsValid(current, now) && pitFinishCall.TryReserve(now) ? pitFinishCall : null;
        }

        private ObservedRaceCall TakeSessionBriefing(GameStateData current, DateTime now)
        {
            // RaceRoom 20.11.: Auf dem Startplatz stehen Autos neben uns – Saras Überlappung verhinderte jede Rennbegrüßung.
            bool standing = current.PositionAndMotionData.CarSpeed < 1f;
            if (settings.RadioMode == AiRadioMode.MINIMAL || proactiveRunning || currentRequest != null ||
                radioRenderGate.Busy || Spotter().HasOverlap && !standing || radioDirector.TrafficQuiet(now) && !standing) return null;
            TeamMember speaker = settings.IsMemberEnabled(TeamMember.Ben) ? TeamMember.Ben : TeamMember.Mik;
            if (!settings.IsMemberEnabled(speaker)) return null;
            // Schon im Boxenmenü begrüßt: keine zweite Begrüßung beim Losfahren.
            if (GreetedInMenu(current.SessionData.SessionType, now))
            {
                if (!trackWelcomeDone) { trackWelcomeDone = true; LiveDiagnostics.Decision("greeting", "suppressed", "greeted_in_menu"); }
                return null;
            }
            // RaceRoom Mantorp 21.11.: Crew Chief meldete erst „Hotlap“, nach 2½ Minuten „Qualifying“ – dieselbe Session laut Spiel.
            // Ben begrüßte zweimal. Ist die Session im Lagebild dieselbe, bleibt es bei einer Begrüßung.
            var spoken = spokenGreeting;
            if (spoken != null && FieldAlreadyGreeted(spokenGreetingField, spoken.DeliveryStartedUtc, Field.MenuBriefing.Key(Field.FieldPicture.Shared.Current), now))
            {
                if (!trackWelcomeDone) { trackWelcomeDone = true; LiveDiagnostics.Decision("greeting", "suppressed", "field_session_already_greeted"); }
                return null;
            }
            // Fährt Rob los, bevor die Begrüßung im Stand lief, gleich die kurze Begrüßung auf der Strecke (nicht erst nach 45 s).
            bool greetingMissed = pendingGreeting != null && pendingGreeting.DeliveryStartedUtc == DateTime.MinValue &&
                current.SessionData.SessionType != SessionType.Race && current.PositionAndMotionData.CarSpeed > 5 && !pendingGreeting.IsValid(current, now);
            if (now > sessionSeenUtc.AddSeconds(45) || greetingMissed) return TakeTrackWelcome(current, now, speaker);
            // Reserve once, acknowledge only at the audio backend, retry at most once within the safe window.
            // RaceRoom 20.11. (Hotlap): Die Begrüßung wurde einmal wegen Fahrerbelastung verworfen und kam danach nie mehr –
            // Rob: „Es kam keine Begrüßung.“ Ein verworfener Versuch wird im Fenster bis zu dreimal neu angesetzt.
            bool greetingRetry = pendingGreeting != null && pendingGreeting.DeliveryStartedUtc == DateTime.MinValue && pendingGreeting.DeliveryFailed &&
                greetingAttempts < 3 && now >= greetingRetryAfter;
            if (pendingGreeting == null || greetingRetry || pendingGreeting.DeliveryStartedUtc == DateTime.MinValue &&
                !pendingGreeting.IsReserved && !pendingGreeting.DeliveryFailed && !pendingGreeting.IsValid(current, now))
            {
                if (greetingRetry) { greetingAttempts++; LiveDiagnostics.Decision("greeting", "retry", "previous_attempt_not_played_" + greetingAttempts); }
                pendingGreeting = greetingDirector.CreateCall(BuildContext(current), sessionSeenUtc.AddSeconds(45), settings.RadioName, speaker);
                greetingRetryAfter = now.AddSeconds(3);
            }
            return pendingGreeting != null && pendingGreeting.DeliveryStartedUtc == DateTime.MinValue &&
                pendingGreeting.IsValid(current, now) && pendingGreeting.TryReserve(now) ? pendingGreeting : null;
        }

        // ---------- Begrüßung im Boxenmenü (Rob 21.11.: „frisch in der Pitbox … die KI begrüßt mich nicht“) ----------
        private readonly object menuGate = new object();
        private string menuKey, menuGreetedKey;
        private DateTime menuSeenUtc = DateTime.MinValue, menuLastCheck = DateTime.MinValue, menuGreetedUtc = DateTime.MinValue;
        private Field.FieldSession menuGreetedSession;
        private int menuAttempts, menuVariant;

        /// <summary>
        /// Lagebild-Faden, höchstens einmal pro Sekunde wirksam: Steht Rob 3 s stabil im Menü/in der Garage einer bekannten
        /// Session, begrüßt ihn Ben mit Strecke, Feld und Ablauf – noch bevor Crew Chief einen Spielzustand liefert.
        /// </summary>
        private void OnFieldView(Field.FieldView v)
        {
            lock (menuGate)
            {
                if (v.Utc - menuLastCheck < TimeSpan.FromSeconds(1)) return;
                menuLastCheck = v.Utc;
            }
            MenuGreeting(v);
            Debrief(v);
        }

        private string debriefKey, debriefDoneKey;
        private DateTime debriefSinceUtc = DateTime.MinValue;
        private int debriefVariant;

        /// <summary>
        /// Rob 21.11.: „Wo bleibt die Zusammenfassung nach dem Practice?“ Training/Quali vorbei und Rob in der Box oder im Menü:
        /// einmal je Session die Auswertung mit Bestzeit (auch gestrichen), Rückstand, Höchsttempo und Verbrauch.
        /// </summary>
        private void Debrief(Field.FieldView v)
        {
            DateTime now = v.Utc;
            string text = null; string key;
            lock (menuGate)
            {
                bool over = v.Phase == Field.FieldPhase.Checkered || v.TimedSession && !float.IsNaN(v.TimeRemaining) && v.TimeRemaining <= 0 ||
                    v.Player != null && v.Player.Motion == Field.CarMotion.Finished;
                bool parked = v.Player != null && (v.Player.InPit && v.Player.Speed < 3 || v.PlayerInGarage || v.InMenus);
                key = v.Sim + "|" + v.Track + "|" + v.Session + "|" + v.TimeDuration;
                if (!settings.Enabled || settings.RadioMode == AiRadioMode.MINIMAL || v.Player == null || !over || !parked ||
                    v.Session != Field.FieldSession.Practice && v.Session != Field.FieldSession.Qualify) { debriefKey = null; return; }
                if (key != debriefKey) { debriefKey = key; debriefSinceUtc = now; }
                if (key == debriefDoneKey || now - debriefSinceUtc < TimeSpan.FromSeconds(6)) return;
                var mw = MainWindow.instance;
                if (audioPlayer == null || mw == null || mw.crewChief == null || !mw.crewChief.running || !settings.IsMemberEnabled(TeamMember.Ben)) return;
                if (proactiveRunning || currentRequest != null || radioRenderGate.Busy || AiAudioStore.Count > 0 || DriverRadioGate.Holding(DateTime.UtcNow)) return;
                float fuel = Memory.AverageFuelPerLap(out int fuelLaps);
                text = Field.SessionDebrief.Compose(v, AiPersonas.CleanForRadio(settings.RadioName ?? "Rob", 30, false), fuel, fuelLaps, debriefVariant++);
                if (text == null) return;
                debriefDoneKey = key;
            }
            LiveDiagnostics.Decision("session-debrief", "detected", "field_picture_session_over_player_parked", text);
            Task.Run(() => SpeakAsync(TeamMember.Ben, text, true, CancellationToken.None, _ => true, true, 3, observedTopic: "session-debrief"));
        }

        private void MenuGreeting(Field.FieldView v)
        {
            DateTime now = v.Utc;
            string greet = null; TeamMember speaker = TeamMember.Ben; string key;
            lock (menuGate)
            {
                if (!settings.Enabled || settings.RadioMode == AiRadioMode.MINIMAL || !Field.MenuBriefing.Ready(v)) { menuKey = null; return; }
                key = Field.MenuBriefing.Key(v);
                if (key != menuKey) { menuKey = key; menuSeenUtc = now; menuAttempts = 0; }
                if (key == menuGreetedKey || menuAttempts >= 2 || now - menuSeenUtc < TimeSpan.FromSeconds(3)) return;
                string skip = MenuGreetingSkip(v, CrewChief.currentGameState, pendingGreeting, trackWelcomeDone);
                if (skip != null)
                {
                    menuGreetedKey = key;
                    LiveDiagnostics.Decision("greeting-menu", "suppressed", skip);
                    return;
                }
                var mw = MainWindow.instance;
                if (audioPlayer == null || mw == null || mw.crewChief == null || !mw.crewChief.running) return;
                if (proactiveRunning || currentRequest != null || radioRenderGate.Busy || DriverRadioGate.Holding(DateTime.UtcNow)) return;
                speaker = settings.IsMemberEnabled(TeamMember.Ben) ? TeamMember.Ben : TeamMember.Mik;
                if (!settings.IsMemberEnabled(speaker)) return;
                greet = Field.MenuBriefing.Compose(v, AiPersonas.CleanForRadio(settings.RadioName ?? "Rob", 30, false), TeamRouter.DisplayName(speaker), menuVariant++);
                if (greet == null) return;
                menuAttempts++;
                menuGreetedKey = key; // gegen Doppelstart; bei Verwerfen wieder frei
            }
            var session = v.Session; string attemptKey = key;
            LiveDiagnostics.Decision("greeting-menu", "detected", "native_menu_track_session_field", greet);
            Task.Run(() => SpeakAsync(speaker, greet, true, CancellationToken.None, _ => true, true, 3,
                playbackStarted: () => { lock (menuGate) { menuGreetedUtc = DateTime.UtcNow; menuGreetedSession = session; } },
                discarded: () => { lock (menuGate) { if (menuGreetedKey == attemptKey && menuGreetedUtc < menuSeenUtc) menuGreetedKey = null; } },
                observedTopic: "greeting-menu"));
        }

        /// <summary>
        /// AC Monza 21.11.: Die normale Begrüßung lief um 14:37:52, 0,4 s später startete die Menübegrüßung – Rob hörte
        /// zweimal „willkommen in Monza“. Nach der schwarzen Flagge kam die Rennbegrüßung, obwohl das Rennen lief.
        /// Gründe, die Menübegrüßung auszulassen; null = begrüßen.
        /// </summary>
        internal static string MenuGreetingSkip(Field.FieldView v, GameStateData gs, ObservedRaceCall greeting, bool trackWelcomeDone)
        {
            if (Field.MenuBriefing.RaceUnderway(v)) return "race_underway_or_over";
            bool sameSession = gs != null && SameSession(v.Session, gs.SessionData.SessionType);
            if (sameSession)
            {
                if (gs.SessionData.IsDisqualified || gs.SessionData.IsDNF || gs.SessionData.Flag == FlagEnum.BLACK) return "player_out_of_session";
                if (gs.SessionData.SessionPhase == SessionPhase.Finished || gs.SessionData.SessionPhase == SessionPhase.Checkered) return "session_over";
                if (trackWelcomeDone) return "track_welcome_already_given";
                if (greeting != null && (greeting.IsReserved || greeting.DeliveryStartedUtc != DateTime.MinValue)) return "session_greeting_already_given";
            }
            return null;
        }

        internal static bool SameSession(Field.FieldSession field, SessionType type)
        {
            switch (field)
            {
                case Field.FieldSession.Race: return type == SessionType.Race;
                case Field.FieldSession.Qualify: return type == SessionType.Qualify || type == SessionType.PrivateQualify;
                case Field.FieldSession.Practice: case Field.FieldSession.Warmup: return type == SessionType.Practice || type == SessionType.LonePractice;
                default: return false;
            }
        }

        /// <summary>Wurde Rob für diese Session schon im Menü begrüßt (letzte 15 Minuten)?</summary>
        internal bool GreetedInMenu(SessionType type, DateTime now)
        {
            lock (menuGate)
            {
                if (menuGreetedUtc == DateTime.MinValue || now < menuGreetedUtc || now - menuGreetedUtc > TimeSpan.FromMinutes(15)) return false;
                return SameSession(menuGreetedSession, type);
            }
        }

        private int greetingAttempts;
        private DateTime greetingRetryAfter = DateTime.MinValue;
        private volatile ObservedRaceCall spokenGreeting;
        private readonly RaceRhythm raceRhythm = new RaceRhythm();
        /// <summary>Eine neue Strafe bleibt so lange stehen, bis sie gesagt ist (höchstens 90 s, dann ist sie überholt).</summary>
        internal static readonly TimeSpan MandatoryPenaltyWindow = TimeSpan.FromSeconds(90);
        private readonly StartHazardNames startNames = new StartHazardNames();
        private DateTime lastSafetyCallUtc = DateTime.MinValue;
        private DateTime lastPitLimitCallUtc = DateTime.MinValue;
        private volatile string spokenGreetingField;

        /// <summary>Wurde die laut Spiel gleiche Session in den letzten 15 Minuten schon begrüßt?</summary>
        internal static bool FieldAlreadyGreeted(string greetedField, DateTime greetedUtc, string currentField, DateTime now) =>
            greetedField != null && greetedField == currentField && greetedUtc != DateTime.MinValue &&
            now >= greetedUtc && now - greetedUtc <= TimeSpan.FromMinutes(15);
        private ObservedRaceCall trackWelcome;
        private bool trackWelcomeDone;
        private int trackWelcomeVariant;

        /// <summary>
        /// Rob 16.11.: Im Training oder Hotlapping kam keine Begrüßung, weil er schon fuhr. Fehlt die Begrüßung nach dem
        /// Standfenster, kommt einmal eine kurze Begrüßung auf der Strecke – nie im Rennen, nie im Zweikampf oder unter Gelb.
        /// </summary>
        private ObservedRaceCall TakeTrackWelcome(GameStateData current, DateTime now, TeamMember speaker)
        {
            var s = current.SessionData;
            if (trackWelcomeDone || pendingGreeting != null && pendingGreeting.DeliveryStartedUtc != DateTime.MinValue) { trackWelcomeDone = true; return null; }
            if (s.SessionType == SessionType.Race || !SessionLapObserver.Supported(s.SessionType) || now > sessionSeenUtc.AddMinutes(5)) return null;
            if (trackWelcome != null && trackWelcome.DeliveryStartedUtc != DateTime.MinValue) { trackWelcomeDone = true; trackWelcome = null; return null; }
            if (trackWelcome == null || !trackWelcome.IsValid(current, now) && !trackWelcome.IsReserved)
            {
                if (s.SessionPhase != SessionPhase.Green || current.FlagData.isLocalYellow || current.PitData.InPitlane) return null;
                string track = SessionGreetingDirector.SpokenTrack(s.TrackDefinition != null ? s.TrackDefinition.name : current.trackName);
                string name = AiPersonas.CleanForRadio(settings.RadioName ?? "", 30, false);
                string who = name.Length > 0 ? name : RadioLanguage.T("Fahrer", "driver", "piloto");
                string kind = s.SessionType == SessionType.HotLap ? RadioLanguage.T("Hotlap", "hotlap", "vuelta rápida") :
                    s.SessionType == SessionType.Qualify || s.SessionType == SessionType.PrivateQualify ? RadioLanguage.T("Qualifying", "qualifying", "clasificación") :
                    RadioLanguage.T("Training", "practice", "entrenamiento");
                // Rob 17.11.: wie am Kommandostand – Strecke, Session, Feld, ein Satz, der anschiebt.
                string motivation = SessionGreetingDirector.Motivation(s.SessionType, trackWelcomeVariant);
                string field = SessionGreetingDirector.FieldLine(current);
                string[] lines = RadioLanguage.Pick(
                    new[] { "Funkcheck, " + who + ". " + track + ", " + kind + ". " + field + motivation,
                            who + ", Crew ist am Funk. " + kind + " in " + track + ". " + field + motivation,
                            "Okay " + who + ", " + track + ", " + kind + ". " + field + motivation,
                            "Hallo " + who + ", " + TeamRouter.DisplayName(speaker) + " hier. Wir sind in " + track + ", " + kind + ". " + field + motivation,
                            who + ", wir sind bei dir. " + track + ", " + kind + ". " + field + motivation },
                    new[] { "Radio check, " + who + ". " + track + ", " + kind + ". " + field + motivation,
                            who + ", crew's on the radio. " + kind + " at " + track + ". " + field + motivation,
                            "Okay " + who + ", " + track + ", " + kind + ". " + field + motivation,
                            "Hello " + who + ", " + TeamRouter.DisplayName(speaker) + " here. We're at " + track + ", " + kind + ". " + field + motivation,
                            who + ", we're with you. " + track + ", " + kind + ". " + field + motivation },
                    new[] { "Prueba de radio, " + who + ". " + track + ", " + kind + ". " + field + motivation,
                            who + ", el equipo está en la radio. " + kind + " en " + track + ". " + field + motivation,
                            "Vale " + who + ", " + track + ", " + kind + ". " + field + motivation,
                            "Hola " + who + ", aquí " + TeamRouter.DisplayName(speaker) + ". Estamos en " + track + ", " + kind + ". " + field + motivation,
                            who + ", estamos contigo. " + track + ", " + kind + ". " + field + motivation });
                int session = s.SessionId; var type = s.SessionType;
                trackWelcome = new ObservedRaceCall { Topic = "track-welcome", Text = lines[(trackWelcomeVariant++) % lines.Length], Member = speaker,
                    Evidence = "no_greeting_in_stationary_window_non_race_session", Priority = 2, Position = -1, SessionId = session, SessionType = type,
                    DetectedUtc = now, ExpiresUtc = now.AddSeconds(20),
                    AdditionalValidity = live => !live.FlagData.isLocalYellow && !Spotter().HasOverlap };
                LiveDiagnostics.Decision(trackWelcome.Topic, "detected", trackWelcome.Evidence, trackWelcome.Text, trackWelcome.EventId);
            }
            return trackWelcome.IsValid(current, now) && trackWelcome.TryReserve(now) ? trackWelcome : null;
        }

        private string lastDamageReport; private DateTime lastDamageReportUtc = DateTime.MinValue;
        internal const double DamageRepeatSeconds = 120;

        /// <summary>Gleicher Schadensbefund wird nicht wiederholt, solange er sich nicht ändert (höchstens alle zwei Minuten).</summary>
        internal bool DamageReportIsNew(string report, DateTime now)
        {
            if (report == lastDamageReport && now >= lastDamageReportUtc && (now - lastDamageReportUtc).TotalSeconds < DamageRepeatSeconds) return false;
            lastDamageReport = report; lastDamageReportUtc = now;
            return true;
        }

        /// <summary>Mik meldet den Schaden. Ben folgt nach abgeschlossenem Audio mit einer belegten Boxenempfehlung.</summary>
        private async Task ReportDamageAsync(DamageAssessment damage, int generation, bool wheelsSaid = false)
        {
            var current = CrewChief.currentGameState;
            if (current == null || generation != Volatile.Read(ref sessionGeneration)) return;
            string advice = damage.Serious && settings.RadioMode != AiRadioMode.MINIMAL && settings.IsMemberEnabled(TeamMember.Ben)
                ? IncidentWatch.PitAdviceAfterDamage(current) : null;
            bool paired = advice != null;
            // Bathurst 17.11.: „Bremse vorne links-Schadenswert 0,1 Prozent“ nach einem leichten Kontakt. Ohne ernsten Schaden
            // nur Werte ab 1 Prozent, sonst ein kurzer Satz.
            bool brief = !damage.Serious;
            string report = IncidentWatch.DamageReport(current.CarDamageData, brief, includeAdvice: !paired, wheelsAlreadySaid: wheelsSaid);
            if (string.IsNullOrWhiteSpace(report)) return;
            // LMU 18.11.: „Aero stark beschädigt …“ achtmal wortgleich. Unveränderter Befund höchstens alle zwei Minuten.
            if (!DamageReportIsNew(report, DateTime.UtcNow))
            {
                LiveDiagnostics.Decision("incident-damage", "suppressed", "same_damage_report_within_120s", report);
                return;
            }
            var valid = TimelyValidity(current, DateTime.UtcNow, 18, live =>
                live.SessionData.SessionPhase == SessionPhase.Green &&
                IncidentWatch.DamageReport(live.CarDamageData, brief, includeAdvice: !paired, wheelsAlreadySaid: wheelsSaid) == report);
            string incidentId = Guid.NewGuid().ToString("N");
            LiveDiagnostics.Decision("incident-damage", "detected", "mapped_damage_assessment", report, incidentId);
            Action<bool> afterReport = paired ? (Action<bool>)(interrupted =>
            {
                if (interrupted || !valid(advice)) return;
                var now = DateTime.UtcNow;
                Func<GameStateData,bool> strategyIsCurrent = live => valid(advice) &&
                    settings.IsMemberEnabled(TeamMember.Ben) && settings.RadioMode != AiRadioMode.MINIMAL &&
                    IncidentWatch.PitAdviceAfterDamage(live) == advice;
                if (!strategyIsCurrent(CrewChief.currentGameState)) return;
                LiveDiagnostics.Decision("incident-pit-advice", "detected", "confirmed_damage_and_open_pit_window", advice, incidentId);
                Interlocked.Exchange(ref pendingDamageAdvice, new ObservedRaceCall {
                    Topic="incident-pit-advice", EventId=incidentId, Evidence="confirmed_damage_and_open_pit_window",
                    Member=TeamMember.Ben, Text=advice, Position=-1, Priority=4, SessionId=current.SessionData.SessionId,
                    SessionType=current.SessionData.SessionType, DetectedUtc=now, ExpiresUtc=now.AddSeconds(12),
                    AdditionalValidity=strategyIsCurrent, RefreshText=ctx=>strategyIsCurrent(ctx.GameState)?advice:null
                });
            }) : null;
            await SpeakAsync(TeamMember.Mik, report, true, CancellationToken.None, valid, true, 7,
                () => { pitDamage.NoteDamageReportStarted(DateTime.UtcNow); LiveDiagnostics.Decision("incident-damage", "delivered", "playback_started", report, incidentId); },
                playbackEnded: afterReport, observedTopic: "incident-damage", eventId: incidentId).ConfigureAwait(false);
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
                        new JObject { ["role"] = "system", ["content"] = string.Format(prompt, settings.RadioName) + ReferenceStyle(TeamMember.Mik, label) + DriverNotes.PromptBlock(label, ctx) },
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
            return null;
        }

        private bool TelemetryFresh()
        {
            DateTime last = Memory.LastTickUtc;
            DateTime now = DateTime.UtcNow;
            return now >= last && (now - last).TotalSeconds <= 1;
        }

        // Warnungen gleicher Art zählen als eine Ansage, alles andere nur bei identischem Text.
        internal static string RepeatKey(ObservedRaceCall call, out TimeSpan window)
        {
            string topic = call.Topic ?? "";
            if (call.HazardIdentity != null) { window = TimeSpan.FromSeconds(12); return "hazard|" + call.HazardIdentity; }
            if (topic == "race-hazard-ahead") { window = TimeSpan.FromSeconds(12); return topic; }
            if (topic == "race-traffic-rear" || topic == "race-class-ahead" || topic == "traffic-approaching" || topic == "traffic-catching") { window = TimeSpan.FromSeconds(15); return topic; }
            window = TimeSpan.FromSeconds(60);
            return string.IsNullOrEmpty(call.Text) ? null : call.Member + "|" + call.Text;
        }

        // Unterbrochene oder gescheiterte Ansagen dürfen wiederkommen.
        internal static bool Covers(ObservedRaceCall previous, DateTime queuedUtc, ObservedRaceCall call, TimeSpan window, DateTime now) =>
            previous != null && !ReferenceEquals(previous, call) && now - queuedUtc < window && !previous.PlaybackInterrupted && !previous.DeliveryFailed &&
            (previous.DeliveryStartedUtc != DateTime.MinValue ||
                call.HazardIdentity == null && now - queuedUtc < TimeSpan.FromSeconds(10));

        private bool CoveredByRecentCall(ObservedRaceCall call, DateTime now)
        {
            string key = RepeatKey(call, out var window);
            lock (recentLocalCalls)
                return key != null && recentLocalCalls.TryGetValue(key, out var last) && Covers(last.Value, last.Key, call, window, now);
        }

        private void RememberLocalCall(ObservedRaceCall call, DateTime now)
        {
            crewMemory.Remember(call, now);
            eventLog.Note(call, EventStatus.Reported, now);
            string key = RepeatKey(call, out _);
            if (key == null) return;
            lock (recentLocalCalls)
            {
                if (recentLocalCalls.Count > 64)
                    foreach (var old in recentLocalCalls.Where(p => now - p.Value.Key > TimeSpan.FromMinutes(2)).Select(p => p.Key).ToArray()) recentLocalCalls.Remove(old);
                recentLocalCalls[key] = new KeyValuePair<DateTime, ObservedRaceCall>(now, call);
            }
        }

        private bool AllowsRadioNow(bool shortTactical)
        {
            if (DriverRadioGate.Holding(DateTime.UtcNow)) return false; // der Fahrer spricht gerade
            if (!workload.AllowsReport(DateTime.UtcNow, shortTactical)) return false;
            var live = CrewChief.currentGameState;
            return shortTactical || live == null || live.PitData.InPitlane || !TelemetryFresh() ||
                !Corners.RadioBrakingWindow(live.PositionAndMotionData.DistanceRoundTrack, live.PositionAndMotionData.CarSpeed);
        }

        private void QueueLocalRaceCall(ObservedRaceCall call)
        {
            // Rob 21.11.: „Ben und Sara haben bei Unfällen die gleichen Funktionen – es reicht, wenn Sara das macht.“
            call.Member = CrewSpeechPolicy.HazardSpeaker(call.Topic, call.Member, SaraSpotting() || settings.IsMemberEnabled(TeamMember.Sara));
            // Funkregie: eine Stelle entscheidet über jede beobachtete Rennmeldung (senden, später, verwerfen).
            {
                var at = DateTime.UtcNow; var live = CrewChief.currentGameState;
                bool start = raceRhythm.InStart(live);
                var situation = new RadioSituation
                {
                    PlayerOut = RaceExclusion.Active(live), StartPhase = start,
                    SameHazardNamed = start && startNames.NamedRecently(call.OpponentNameClip, at),
                    LastSafetyUtc = lastSafetyCallUtc, LastPitLimitUtc = lastPitLimitCallUtc, Now = at,
                };
                var verdict = RadioDirectorPolicy.Decide(call.Topic, situation, out string why);
                if (verdict == RadioVerdict.Drop)
                {
                    call.MarkPlaybackStarted(at); call.MarkPlaybackFinished(at, false);
                    LiveDiagnostics.Decision(call.Topic, "suppressed", why, call.Text);
                    return;
                }
                if (verdict == RadioVerdict.Hold)
                {
                    call.HoldBack(at, TimeSpan.FromSeconds(2), why);
                    return;
                }
                if (start) startNames.Note(call.OpponentNameClip, at);
                if (RadioDirectorPolicy.Classify(call.Topic) == RadioRank.Safety) lastSafetyCallUtc = at;
                if (call.Topic == "pit-limit") lastPitLimitCallUtc = at;
            }
            if (call.Topic == "greeting" || call.Topic == "track-welcome")
            {
                spokenGreetingField = Field.MenuBriefing.Key(Field.FieldPicture.Shared.Current);
                spokenGreeting = call; // zählt erst, wenn die Wiedergabe wirklich begonnen hat
            }
            eventLog.Note(call, EventStatus.Detected, DateTime.UtcNow);
            int generation = Volatile.Read(ref sessionGeneration);
            bool ownResult = call.Topic == "own-lap" || call.Topic == "qualifying-last" ||
                call.Topic == "first-lap-fuel" || call.Topic == "fuel-baseline";
            bool urgentPit=RadioRenderGate.IsUrgentPit(call) || call.Priority>=7 &&
                (call.Topic=="race-hazard-ahead" || call.Topic=="race-traffic-rear" || call.Topic=="race-class-ahead" || call.Topic=="race-disqualified" ||
                (call.Topic??"").StartsWith("warning-",StringComparison.Ordinal) && call.Topic!="warning-numeric-damage");
            // Die Schadensmeldung mit Messwerten wird erst live gesprochen und darf Flaggen und Gefahren nicht sechs Sekunden blockieren
            if (CoveredByRecentCall(call, DateTime.UtcNow))
            {
                var at = DateTime.UtcNow;
                call.MarkPlaybackStarted(at); call.MarkPlaybackFinished(at, false);
                LiveDiagnostics.Decision(call.Topic, "suppressed", "same_call_recently_spoken", call.Text);
                return;
            }
            // C1: derselbe Satz aus einem anderen Wächter kurz nach dem ersten – nicht doppelt sprechen.
            if (crewMemory.SaidRecently(call, DateTime.UtcNow))
            {
                var at = DateTime.UtcNow;
                call.MarkPlaybackStarted(at); call.MarkPlaybackFinished(at, false);
                LiveDiagnostics.Decision(call.Topic, "suppressed", "crew_memory_same_sentence", call.Text);
                eventLog.Note(call, EventStatus.Dropped, at, "crew_memory_same_sentence");
                return;
            }
            // RRCA 2.0 (6.3): derselbe Vorfall bzw. derselbe gemeldete Platz in anderer Formulierung – nichts Neues.
            if (crewMemory.NothingNew(call, DateTime.UtcNow, out string sameEvent))
            {
                var at = DateTime.UtcNow;
                call.MarkPlaybackStarted(at); call.MarkPlaybackFinished(at, false);
                LiveDiagnostics.Decision(call.Topic, "suppressed", sameEvent, call.Text);
                eventLog.Note(call, EventStatus.Dropped, at, sameEvent);
                return;
            }
            // Renningenieur-Kopf: Ist diese Info in der aktuellen Rennlage wichtig genug? Sicherheit und eigene Rennereignisse gehen immer durch.
            var mindVerdict = raceMind.Judge(call, DateTime.UtcNow, out string mindReason);
            if (mindVerdict == MindVerdict.Hold)
            {
                call.HoldBack(DateTime.UtcNow, TimeSpan.FromSeconds(2), "race_mind:" + mindReason);
                eventLog.Note(call, EventStatus.Deferred, DateTime.UtcNow, "race_mind:" + mindReason);
                return;
            }
            if (mindVerdict == MindVerdict.Drop)
            {
                var at = DateTime.UtcNow;
                call.MarkPlaybackStarted(at); call.MarkPlaybackFinished(at, false);
                LiveDiagnostics.Decision(call.Topic, "suppressed", "race_mind:" + mindReason, call.Text);
                eventLog.Note(call, EventStatus.Dropped, at, "race_mind:" + mindReason);
                return;
            }
            if (call.Priority < 4 && !ownResult && !(call.Topic ?? "").StartsWith("timed-lap-", StringComparison.Ordinal) &&
                SessionLapObserver.FlyingLapWindow(CrewChief.currentGameState))
            {
                call.HoldBack(DateTime.UtcNow, TimeSpan.FromSeconds(2), "flying_lap_window");
                eventLog.Note(call, EventStatus.Deferred, DateTime.UtcNow, "flying_lap_window");
                return;
            }
            if (call.CanRefresh && (currentRequest != null || Spotter().HasOverlap || !AllowsRadioNow(IsShortTactical(call.Topic))))
            {
                call.DeferDelivery(DateTime.UtcNow, "waiting_for_clear_tactical_radio");
                return;
            }
            if (!settings.IsMemberEnabled(call.Member) || !(ownResult ? radioRenderGate.TryEnterResult() : radioRenderGate.TryEnter(urgentPit)))
            {
                call.ReleaseForRetry(DateTime.UtcNow, "member_disabled_or_channel_busy");
                return;
            }
            RememberLocalCall(call, DateTime.UtcNow);
            bool pitAlreadyClaimed = fieldEvents.HasClaimedPit(call.PitGain, DateTime.UtcNow);
            int renderReleased = 0;
            Action releaseRender = () => {
                if (Interlocked.Exchange(ref renderReleased, 1) != 0) return;
                if (ownResult) radioRenderGate.ExitResult(); else radioRenderGate.Exit(urgentPit);
            };
            Task.Run(async () =>
            {
                try
                {
                    var initialState = CrewChief.currentGameState;
                    bool backgroundStatistics = call.Evidence == "configured_interval_current_measurements" ||
                        call.Topic == "stint-consistency" || call.Topic == "reader-pace-front" || call.Topic == "reader-pace-rear";
                    var context = call.ConfirmedPass || call.PitGain != null || call.CanRefresh || backgroundStatistics ?
                        BuildContext(initialState) : null;
                    if (!call.RefreshBeforeSpeak(context)) { call.ReleaseForRetry(DateTime.UtcNow, "tactical_facts_no_longer_current"); return; }
                    float spokenGap = call.SpokenGapS;
                    string text = call.ComposeForDelivery(context, out var detailIsCurrent, pitAlreadyClaimed);
                    bool detailUsesNames = detailIsCurrent != null && context?.SendDriverNames == true;
                    if (call.PreparedDetail != null)
                        text=call.ComposePreparedDetail(initialState,DateTime.UtcNow,candidate=>{
                            try{var prepared=PreparedSpeech(call.Member,candidate);return prepared!=null && AiAudio.HasSpeechSignal(prepared);}catch(Exception){return false;}
                        },out detailIsCurrent);
                    string baseText = text;
                    byte[] namePcm = null;
                    // AC 20.11.: „Dreher vor dir“, Gelb und langsame Autos ohne Namen – der Name war nur dabei, wenn er zufällig
                    // schon vorbereitet war. Fehlt er, wird er jetzt sofort in derselben Stimme erzeugt (kurze Frist).
                    if (call.OpponentNameClip != null && settings.SendDriverNames &&
                        (namePcm = CachedOpponentName(call.Member, call.OpponentNameClip) ??
                            await RenderOpponentNameNow(call.Member, call.OpponentNameClip, generation, call.EventId).ConfigureAwait(false)) != null)
                    {
                        text += " " + call.OpponentNameClip;
                        detailUsesNames = true;
                    }
                    else if (call.OpponentNameClip != null)
                    {
                        LiveDiagnostics.Decision(call.Topic, "names_omitted",
                            settings.SendDriverNames ? "prepared_name_unavailable" : "name_permission_disabled", eventId: call.EventId);
                        if (settings.SendDriverNames && call.Member != TeamMember.Mik)
                            WarmOpponentName(new[] { call.OpponentNameClip }, generation, call.Member);
                    }
                    bool combinedPit = call.PitGain != null && detailIsCurrent != null;
                    bool CanRefreshRenderedFacts(GameStateData live) => spokenGap < 0 ||
                        Math.Abs(call.RefreshGapS(BuildContext(live)) - spokenGap) <= .35f;
                    Func<string, bool> valid = _ => {
                        var live = CrewChief.currentGameState;
                        var now = DateTime.UtcNow;
                        return settings.Enabled && settings.IsMemberEnabled(call.Member) &&
                            (!(call.FieldNamesAllowed || detailUsesNames) || settings.SendDriverNames) &&
                            (!(call.Topic??"").StartsWith("native-gap-",StringComparison.Ordinal) ||
                                settings.ReplaceCrewChiefVoice && settings.RadioMode!=AiRadioMode.MINIMAL) &&
                            (call.Topic != "quali-sector" && call.Topic != "sector-best" && call.Topic != "own-best-pace" ||
                                DriverMemory.QualifyingSectorRadioEnabled) &&
                            generation == Volatile.Read(ref sessionGeneration) &&
                            TelemetryFresh() && !Spotter().HasOverlap && call.IsValid(live, now) &&
                            (!backgroundStatistics || !RaceRadioDirector.SuppressBackgroundStatistics(BuildContext(live))) &&
                            (detailIsCurrent == null || detailIsCurrent(live, now)) && CanRefreshRenderedFacts(live);
                    };
                    void DeferOrRelease(string reason)
                    {
                        var live = CrewChief.currentGameState;
                        var now = DateTime.UtcNow;
                        if (call.CanRefresh && generation == Volatile.Read(ref sessionGeneration) && live != null &&
                            (call.RaceStatus || call.IsValid(live, now)) &&
                            (!AllowsRadioNow(IsShortTactical(call.Topic)) || Spotter().HasOverlap || !CanRefreshRenderedFacts(live) ||
                                call.RaceStatus && (detailIsCurrent != null || call.Position != live.SessionData.ClassPosition)))
                            call.DeferDelivery(now, "refresh_tactical_facts_after_delay");
                        else call.ReleaseForRetry(now, reason);
                    }
                    if (!valid(text)) { call.ReleaseForRetry(DateTime.UtcNow, "event_no_longer_valid"); return; }
                    if(LiveDiagnostics.Enabled)LiveDiagnostics.Emit("race.event_audio_requested",LiveDiagnostics.Data(
                        "topic",call.Topic,"speaker",call.Member.ToString(),"text",text),call.EventId);
                    bool queued = await SpeakAsync(call.Member, text, true, CancellationToken.None, valid, true, Math.Min(10, call.Priority),
                        () => {
                            call.MarkPlaybackStarted(DateTime.UtcNow, combinedPit);
                            if (call.Topic == "crew-thermal-pit-handover") pitCycle.NoteMergeReport(DateTime.UtcNow);
                            if(LiveDiagnostics.Enabled) {
                                var live=CrewChief.currentGameState;
                                LiveDiagnostics.Emit("race.spoken_facts",LiveDiagnostics.Data("topic",call.Topic,"text",text,
                                    "source_utc",live==null || live.SourceTelemetryUtc==DateTime.MinValue?null:live.SourceTelemetryUtc.ToString("o"),
                                    "opponent_front",live?.SessionData.OpponentKeyInFront,"opponent_behind",live?.SessionData.OpponentKeyBehind,
                                    "gap_front_s",call.AheadGap>=0?(object)call.AheadGap:null,"gap_behind_s",call.BehindGap>=0?(object)call.BehindGap:null),call.EventId);
                            }
                        },
                        () => DeferOrRelease("audio_discarded_before_playback"),
                        interrupted => {
                            var finishedUtc = DateTime.UtcNow;
                            call.MarkPlaybackFinished(finishedUtc, interrupted);
                            if (interrupted || generation != Volatile.Read(ref sessionGeneration) || !TelemetryFresh()) return;
                            var live = CrewChief.currentGameState;
                            if (call.ChaseOpponentIdentity != null)
                                raceFeedback.RememberDeliveredChasePlan(BuildContext(live), call.ChaseOpponentIdentity);
                            if (call.Member == TeamMember.Mik && (call.Topic ?? "").StartsWith("warning-temperature-", StringComparison.Ordinal) &&
                                !call.Topic.EndsWith("-clear", StringComparison.Ordinal) && settings.IsMemberEnabled(TeamMember.Ben))
                                radioDirector.OfferPitHandover(live, finishedUtc, (frame, at) =>
                                    frame != null && frame.PitData.PitWindow == PitWindow.Open && pitCycle.MergeReportDue(at) &&
                                    thermalWatch.SustainedHeat(frame, at) != 0 && pitCycle.ReadForecast(frame, at)?.Status == "clear");
                        },call.Topic,releaseRender,
                        namePcm, namePcm == null ? null : baseText, call.EventId).ConfigureAwait(false);
                    if (!queued) DeferOrRelease("speech_not_queued");
                }
                catch (Exception e) { call.ReleaseForRetry(DateTime.UtcNow, "delivery_exception"); Log("Lokaler Rennfunk: " + e.Message); }
                finally { releaseRender(); }
            });
        }

        // ---------------- Sprachausgabe ----------------

        private async Task<bool> WaitForCalmAsync(string text, DateTime deadline, int generation, Func<string, bool> valid, CancellationToken ct, bool shortTactical)
        {
            if (AllowsRadioNow(shortTactical)) return true;
            if (!radioRenderGate.TryHoldRoutine()) return false;
            LiveDiagnostics.Decision("radio", "deferred", "driver_workload");
            try
            {
                while (!AllowsRadioNow(shortTactical))
                {
                    if (ct.IsCancellationRequested || generation != Volatile.Read(ref sessionGeneration) ||
                        DateTime.UtcNow >= deadline || valid != null && !valid(text)) return false;
                    await Task.Delay(100).ConfigureAwait(false);
                }
                return true;
            }
            finally { radioRenderGate.ReleaseRoutine(); }
        }

        private static bool CrewIsRunning() => MainWindow.instance?.crewChief?.running == true;

        /// <summary>Session und Phase als Schlüssel: Unterbrochenes gilt nur, solange beide gleich bleiben.</summary>
        internal static string SessionPhaseKey(GameStateData gs) => SessionPhaseKey(gs, Board.RaceBoard.Current, DateTime.UtcNow);

        /// <summary>Session und Phase aus der Lagetafel, solange sie frisch ist (Hotlap/Qualifying-Wechsel von Crew Chief zählt nicht).</summary>
        internal static string SessionPhaseKey(GameStateData gs, Board.BoardSnapshot board, DateTime now)
        {
            if (gs == null) return null;
            if (board != null && board.Session.Fresh(now, 2) && board.Phase.Fresh(now, 2))
                return gs.SessionData.SessionId + "|" + board.Session.Value + "|" + board.Phase.Value;
            return gs.SessionData.SessionId + "|" + gs.SessionData.SessionType + "|" + gs.SessionData.SessionPhase;
        }

        public Task SpeakAsync(TeamMember member, string text, bool isReply, CancellationToken ct)
        {
            return SpeakAsync(member, text, isReply, ct, null);
        }

        /// <param name="stillValid">Wird direkt vor dem Einreihen geprüft (Sara: Spotterlage neu bewerten). false = nicht abspielen.</param>
        /// <returns>true, wenn der Funkspruch eingereiht wurde.</returns>
        private async Task<bool> SpeakAsync(TeamMember member, string text, bool isReply, CancellationToken ct, Func<string, bool> stillValid, bool localOnly = false, int localPriority = 4, Action playbackStarted = null, Action discarded = null, Action<bool> playbackEnded = null, string observedTopic = null, Action renderCompleted = null,
            byte[] preparedSuffix = null, string baseText = null, string eventId = null)
        {
            if (!settings.IsMemberEnabled(member)) { LiveDiagnostics.Decision("radio","suppressed","member_disabled"); return false; }
            {
                var ended = playbackEnded; string spokenText = text, spokenTopic = observedTopic;
                playbackEnded = interrupted => {
                    if (interrupted && !DriverRadioGate.Holding(DateTime.UtcNow)) interruptedRadio.Note(member, spokenText, spokenTopic, DateTime.UtcNow, SessionPhaseKey(CrewChief.currentGameState)); // nicht, wenn Rob selbst dazwischenfunkt
                    ended?.Invoke(interrupted);
                };
            }
            using (var diagnosticSpan = LiveDiagnostics.Begin("radio", LiveDiagnostics.Data("speaker",member.ToString(),"text",text,"priority",localOnly?localPriority:isReply?5:0),eventId))
            {
                try
                {
            if (!CrewSpeechPolicy.HasSpokenText(text))
            {
                { LiveDiagnostics.Decision("radio","suppressed","no_spoken_text"); return false; }
            }
            int generation = Volatile.Read(ref sessionGeneration);
            int lifetime = observedTopic == "own-lap" || observedTopic == "race-disqualified" ? 22 :
                CrewSpeechPolicy.LifetimeSeconds(localOnly, localPriority);
            DateTime deadline = DateTime.UtcNow.AddSeconds(lifetime);
            bool needsCalm = !isReply || localOnly && localPriority <= 3 || observedTopic == "reader-wet-compound" ||
                observedTopic == "position" && localPriority < 5 || observedTopic == "rear-pressure" || observedTopic == "position-loss" ||
                observedTopic == "incident-pit-advice" || observedTopic == "fuel-baseline" || observedTopic == "rival-sector-loss" || observedTopic == "reader-temperature-context" || observedTopic == "crew-thermal-pit-handover";
            bool shortTactical = localOnly && IsShortTactical(observedTopic);
            if (needsCalm && radioRenderGate.PreparedRoutineBusy && !AllowsRadioNow(shortTactical))
            { LiveDiagnostics.Decision("radio", "deferred", "prepared_routine_slot_busy"); return false; }
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
                    renderDeadline.CancelAfter(TimeSpan.FromSeconds(lifetime));
                    // Vorbereiteter Clip ohne Wartezeit; fehlt er, lieber dieselbe Stimme live erzeugen als eine Warnung verschlucken
                    string renderText = baseText ?? text;
                    bool disqualificationClip = localOnly && observedTopic == "race-disqualified";
                    pcm = (CrewSpeechPolicy.CacheOnly(localOnly, localPriority, member) || disqualificationClip ||
                        localOnly && localPriority>=7 && CrewSpeechPolicy.IsPreparedPhrase(member,renderText)) ? PreparedSpeech(member, renderText) : null;
                    if (pcm == null) pcm = await RenderAsync(member, renderText, renderDeadline.Token, eventId, disqualificationClip).ConfigureAwait(false);
                    else LiveDiagnostics.Decision("tts", "cache_hit", "prepared_clip", eventId: eventId);
                    if (preparedSuffix != null)
                    {
                        var combined = new byte[pcm.Length + preparedSuffix.Length];
                        Buffer.BlockCopy(pcm, 0, combined, 0, pcm.Length);
                        Buffer.BlockCopy(preparedSuffix, 0, combined, pcm.Length, preparedSuffix.Length);
                        pcm = combined;
                    }
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
            renderCompleted?.Invoke();
            if (needsCalm && CrewIsRunning() && !await WaitForCalmAsync(text, deadline, generation, stillValid, ct, shortTactical).ConfigureAwait(false))
            { LiveDiagnostics.Decision("radio", "suppressed", "workload_hold_expired_or_slot_busy"); return false; }
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
                    AiAudio.PlayDirect(wav, AudioPlayer.naudioMessagesPlaybackDeviceId, member);
                }
                catch (Exception e)
                {
                    Log("Direkte Wiedergabe fehlgeschlagen: " + e.Message);
                    { LiveDiagnostics.Decision("radio","suppressed","direct_playback_error"); return false; }
                }
                Memory.AddRadio(member, text, DateTime.UtcNow);
                DriverMemory.RememberRadio(member,text,Memory.SessionId);
                raceMind.NoteSpoken(observedTopic, text, DateTime.UtcNow);
                if (playbackStarted != null) playbackStarted();
                return true;
            }
            bool criticalEngineer = localOnly && localPriority == 7 && member != TeamMember.Sara && observedTopic != "race-disqualified";
            deadline = CrewSpeechPolicy.EngineerPlaybackDeadline(deadline, DateTime.UtcNow, SoundCache.SafetyRadioActive, criticalEngineer);
            string id = AiAudioStore.Put(wav, TeamRouter.DisplayName(member) + ": " + text, () => {
                    string reason = !settings.IsMemberEnabled(member) ? "member_disabled" : DateTime.UtcNow > deadline ? "audio_deadline_expired" :
                        generation != Volatile.Read(ref sessionGeneration) ? "session_changed" :
                        stillValid != null && !stillValid(text) ? "event_or_facts_changed" :
                        needsCalm && !AllowsRadioNow(shortTactical) ? "driver_workload_changed" :
                        !isReply && (Spotter().HasOverlap || radioDirector.TrafficQuiet(DateTime.UtcNow)) ? "traffic_priority" : null;
                    if (reason != null) LiveDiagnostics.Decision("audio", "suppressed", reason, eventId:eventId);
                    return reason == null;
                },
                () => { if (generation == Volatile.Read(ref sessionGeneration)) {
                    Memory.AddRadio(member, text, DateTime.UtcNow);
                DriverMemory.RememberRadio(member,text,Memory.SessionId);
                    raceMind.NoteSpoken(observedTopic, text, DateTime.UtcNow);
                    if (playbackStarted != null) playbackStarted();
                } }, discarded, playbackEnded, eventId, observedTopic);
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("radio.enqueue",LiveDiagnostics.Data("clip_id",id,"topic",observedTopic,"speaker",member.ToString(),"text",text,"priority",localOnly?localPriority:isReply?5:SoundMetadata.DEFAULT_PRIORITY,"immediate",isReply,"event_id",eventId),eventId);
            var fragments = new List<MessageFragment> { MessageFragment.Text(SoundCache.AI_AUDIO_IDENTIFIER + id) };
            if (isReply)
            {
                // End-to-end deadline is checked again at driver playback by AiAudioStore.
                var message = new QueuedMessage("ai_team_reply_" + id, Math.Max(1, (int)Math.Ceiling((deadline-DateTime.UtcNow).TotalSeconds)), messageFragments: fragments,
                    type: localOnly ? (localPriority >= 6 ? SoundType.CRITICAL_MESSAGE : SoundType.IMPORTANT_MESSAGE) : SoundType.VOICE_COMMAND_RESPONSE, priority: localOnly ? localPriority : 5);
                // Rob 17.11.: Ben und Mik fallen sich nicht mehr ins Wort – sie stellen sich hinten an. Nur Sara (Spotterin) darf
                // eine laufende Ansage unterbrechen.
                bool mayInterrupt = CrewSpeechPolicy.MayInterruptCrew(member);
                audioPlayer.playMessageImmediately(message, interruptForAiHazard: mayInterrupt && localOnly && localPriority >= 7,
                    interruptForAiResult: mayInterrupt && localOnly && (observedTopic == "own-lap" || observedTopic == "qualifying-last" || observedTopic == "position" && localPriority == 5),
                    aiTopic: observedTopic);
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

        private static bool IsShortTactical(string topic) =>
            topic == "ahead" || topic == "behind" || topic == "status" || topic == "formation" ||
            topic == "own-lap" || topic == "near-best" || topic == "position" || topic == "position-loss" ||
            topic == "rear-pressure" || topic == "sector-best" || topic == "own-best-pace" || topic == "rival-sector-loss" ||
            (topic ?? "").StartsWith("native-gap-", StringComparison.Ordinal) ||
            (topic ?? "").StartsWith("reader-pace-", StringComparison.Ordinal);

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

        /// <summary>Fahrernamen aus Lagebild und Crew Chief, nur für die Aussprache.</summary>
        private static IEnumerable<string> SpokenNameSources()
        {
            var names = new List<string>();
            try
            {
                var view = Field.FieldPicture.Shared.Current;
                if (view?.Cars != null) names.AddRange(view.Cars.Where(c => c != null).Select(c => c.Name));
                var gs = CrewChief.currentGameState;
                if (gs?.OpponentData != null) names.AddRange(gs.OpponentData.Values.Where(o => o != null).Select(o => o.DriverRawName));
            }
            catch (Exception) { } // Lesen während eines Sessionwechsels: dann eben ohne Namensaussprache
            return names;
        }

        private async Task<byte[]> RenderAsync(TeamMember member, string text, CancellationToken ct, string eventId = null, bool preparePhrase = false)
        {
            using (var diagnosticSpan = LiveDiagnostics.Begin("tts", LiveDiagnostics.Data("speaker",member.ToString()), eventId))
            {
                try
                {
            if (LiveDiagnostics.Enabled) LiveDiagnostics.Emit("tts.render",LiveDiagnostics.Data("speaker",member.ToString(),"service",settings.TtsProvider.ToString(),"model",settings.TtsProvider==AiTtsProvider.ELEVENLABS?settings.ElevenLabsModel:settings.TtsModel));
            // Namen in Großbuchstaben spricht die Stimme als Namen statt Buchstabe für Buchstabe (RaceRoom Mantorp 21.11.).
            string named = SpokenNames.Prepare(text, SpokenNameSources());
            string cacheKey = CrewSpeechPolicy.CacheKey(settings, member, named);
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
            bool cacheable = preparePhrase || CrewSpeechPolicy.IsPreparedPhrase(member, text);
            string diskFile = cacheable ? PhraseCacheFile(member, named) : null;
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
            // Pronunciation only: validation, diagnostics and cache identity keep the exact factual text.
            string spokenText = GermanRadioText.Prepare(named, pitWall: member != TeamMember.Sara);
            // Respect the selected source. A failed provider must not change the crew's identity.
            byte[] pcm = null;
            if (settings.TtsProvider == AiTtsProvider.ELEVENLABS && elevenLabs.HasKey && !ElevenLabsPaused)
                pcm = await TryTtsAsync("ElevenLabs", t => elevenLabs.SpeechPcmAsync(settings.ElevenLabsVoiceFor(member), settings.ElevenLabsModel, spokenText, member, t), ct).ConfigureAwait(false);
            else if (settings.TtsProvider == AiTtsProvider.WINDOWS)
                pcm = WindowsTts.Render(spokenText, settings.WindowsVoiceFor(member), member);
            else if (settings.TtsProvider != AiTtsProvider.ELEVENLABS && HasApiKey)
                pcm = await TryTtsAsync("Cloud-Stimme", t => client.SpeechPcmAsync(settings.TtsModel, settings.VoiceFor(member), spokenText, AiPersonas.VoiceInstructions(member), t), ct).ConfigureAwait(false);
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
                // Deutsch ohne Zusatz, damit bestehende Pakete gültig bleiben; andere Sprachen brauchen ein eigenes Paket
                return SaraVoiceKeyWithoutLanguage + (RadioLanguage.IsGerman ? "" : "|lang=" + RadioLanguage.Current);
            }
        }

        private string SaraVoiceKeyWithoutLanguage
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
            if(settings.TtsProvider==AiTtsProvider.ELEVENLABS && string.IsNullOrWhiteSpace(settings.ElevenLabsVoiceSara))
                return "Zuerst Saras eigene Stimme unter Team & Stimmen auswählen.";
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
        private readonly Dictionary<string, byte[]> preparedOpponentNames = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private byte[] CachedOpponentName(TeamMember member, string phrase)
        {
            string key = CrewSpeechPolicy.CacheKey(settings, member, phrase);
            lock (preparedOpponentNames)
                return preparedOpponentNames.TryGetValue(key, out var pcm) ? pcm : null;
        }
        private int opponentVoiceWarmRunning, opponentVoiceWarmCount, opponentVoiceWarmGeneration = -1;
        /// <summary>Namen je Sitzung vorbereiten (vorher 24 für die ganze Programmlaufzeit – danach fehlten alle Namen).</summary>
        internal const int OpponentWarmBudget = 60, OpponentWarmBudgetParked = 16;

        private bool AttemptedOpponentName(TeamMember member, string phrase)
        {
            string key = CrewSpeechPolicy.CacheKey(settings, member, phrase);
            lock (attemptedOpponentVoiceClips) return attemptedOpponentVoiceClips.Contains(key);
        }

        private int OpponentWarmCount()
        {
            int generation = Volatile.Read(ref sessionGeneration);
            if (Interlocked.Exchange(ref opponentVoiceWarmGeneration, generation) != generation) Interlocked.Exchange(ref opponentVoiceWarmCount, 0);
            return Volatile.Read(ref opponentVoiceWarmCount);
        }

        /// <summary>Name für einen anstehenden Funkspruch sofort erzeugen, wenn er nicht vorbereitet ist; null nach 1,5 s.</summary>
        private async Task<byte[]> RenderOpponentNameNow(TeamMember member, string phrase, int generation, string eventId)
        {
            try
            {
                string key = CrewSpeechPolicy.CacheKey(settings, member, phrase);
                byte[] pcm;
                using (var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500)))
                    pcm = await RenderAsync(member, phrase, deadline.Token, eventId).ConfigureAwait(false);
                if (!settings.SendDriverNames || generation != Volatile.Read(ref sessionGeneration) || !AiAudio.HasSpeechSignal(pcm)) return null;
                lock (preparedOpponentNames) preparedOpponentNames[key] = pcm;
                LiveDiagnostics.Decision("race-traffic-name", "prepared", "rendered_for_call", eventId: eventId);
                return pcm;
            }
            catch (Exception) { LiveDiagnostics.Decision("race-traffic-name", "deferred", "render_for_call_failed", eventId: eventId); return null; }
        }
        private DateTime nextOpponentVoiceWarmUtc;
        private readonly HashSet<string> attemptedOpponentVoiceClips = new HashSet<string>(StringComparer.Ordinal);
        private void MaybePrepareOpponentName(GameStateData gs, DateTime now)
        {
            if (!settings.SendDriverNames || !settings.IsMemberEnabled(TeamMember.Ben) ||
                !(SessionLapObserver.Supported(gs.SessionData.SessionType) || gs.SessionData.SessionType == SessionType.Race) ||
                radioRenderGate.Busy || currentRequest != null || now < nextOpponentVoiceWarmUtc ||
                OpponentWarmCount() >= OpponentWarmBudget || Volatile.Read(ref opponentVoiceWarmRunning) != 0) return;
            bool parked = (gs.PitData.InPitlane || gs.SessionData.SessionPhase == SessionPhase.Garage ||
                gs.SessionData.SessionPhase == SessionPhase.Gridwalk) &&
                TrackAwareness.InRange(gs.PositionAndMotionData.CarSpeed, 0, 1).HasValue;
            // Keep most of the bounded synthesis budget for nearby traffic on track.
            if (parked && OpponentWarmCount() >= OpponentWarmBudgetParked) return;
            var positions = Tracks.Snapshot;
            if (!parked && (positions == null || gs.SessionData.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane ||
                Spotter().HasOverlap || now < positions.SourceUtc || now - positions.SourceUtc > TimeSpan.FromSeconds(1))) return;
            nextOpponentVoiceWarmUtc = now.AddSeconds(parked ? 1 : 2);
            var names = parked
                ? gs.OpponentData.Values.Where(o => o != null && o.IsActive && o.CanUseName)
                    .OrderBy(o => o.ClassPosition <= 0 ? int.MaxValue : Math.Abs(o.ClassPosition - gs.SessionData.ClassPosition))
                    .Select(o => RadioPhraseCatalog.SafeRivalName(o.DriverRawName)).Where(n => n.Length > 0).Take(24)
                : positions.Cars.Where(c => !c.InPits && c.SpatiallyConsistent && c.RouteGapM.HasValue &&
                        Math.Abs(c.RouteGapM.Value) <= 750 && !string.IsNullOrWhiteSpace(c.Name))
                    .OrderBy(c => Math.Abs(c.RouteGapM.Value)).Select(c => c.Name).Take(8);
            // Ben (Verkehr, Gefahren) und Sara (Gelb, Unfall) sprechen Namen – beide Stimmen vorbereiten.
            var phrases = names.Distinct(StringComparer.Ordinal).Select(RaceTrafficWatch.NameClipText).ToArray();
            bool sara = settings.IsMemberEnabled(TeamMember.Sara) && settings.ReplaceCrewChiefVoice &&
                phrases.Length > 0 && phrases.All(p => CachedOpponentName(TeamMember.Ben, p) != null || AttemptedOpponentName(TeamMember.Ben, p));
            WarmOpponentName(phrases, Volatile.Read(ref sessionGeneration), sara ? TeamMember.Sara : TeamMember.Ben);
        }

        /// <summary>Sara spottet gerade: KI-Sara aktiv und der Spotter auf dieser Strecke eingeschaltet (Realistisch: nur Ovale).</summary>
        private bool SaraSpotting() => settings.IsMemberEnabled(TeamMember.Sara) && GlobalBehaviourSettings.spotterEnabled;

        private void WarmOpponentName(string[] phrases, int generation, TeamMember member = TeamMember.Ben)
        {
            if (phrases.Length == 0 || OpponentWarmCount() >= OpponentWarmBudget ||
                Interlocked.CompareExchange(ref opponentVoiceWarmRunning, 1, 0) != 0) return;
            Task.Run(async () =>
            {
                try
                {
                    if (generation != Volatile.Read(ref sessionGeneration) || !settings.SendDriverNames ||
                        !settings.IsMemberEnabled(member) || currentRequest != null ||
                        settings.TtsProvider == AiTtsProvider.WINDOWS && radioRenderGate.Busy) return;
                    var phrase = phrases.FirstOrDefault(candidate =>
                        !AttemptedOpponentName(member, candidate) && CachedOpponentName(member, candidate) == null);
                    if (phrase == null) return;
                    string key = CrewSpeechPolicy.CacheKey(settings, member, phrase);
                    lock (attemptedOpponentVoiceClips) attemptedOpponentVoiceClips.Add(key);
                    Interlocked.Increment(ref opponentVoiceWarmCount);
                    byte[] pcm;
                    using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4)))
                        pcm = await RenderAsync(member, phrase, deadline.Token).ConfigureAwait(false);
                    if (!settings.SendDriverNames || generation != Volatile.Read(ref sessionGeneration) ||
                        key != CrewSpeechPolicy.CacheKey(settings, member, phrase) || !AiAudio.HasSpeechSignal(pcm)) return;
                    lock (preparedOpponentNames)
                        preparedOpponentNames[key] = pcm;
                    LiveDiagnostics.Decision("race-traffic-name", "prepared", "selected_voice_opponent_clip");
                }
                catch (Exception) { LiveDiagnostics.Decision("race-traffic-name", "deferred", "preparation_unavailable"); }
                finally { Interlocked.Exchange(ref opponentVoiceWarmRunning, 0); }
            });
        }
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
                if(settings.TtsProvider==AiTtsProvider.ELEVENLABS && new[]{TeamMember.Ben,TeamMember.Mik,TeamMember.Sara}
                    .Any(m=>settings.IsMemberEnabled(m) && string.IsNullOrWhiteSpace(settings.ElevenLabsVoiceFor(m))))
                    return PreparedVoiceStatus="Zuerst eigene Stimmen für die aktiven Crew-Mitglieder auswählen.";
                PreparedVoiceStatus = "Originalclips werden geprüft/vorbereitet.";
                int ready=0, failed=0;
                var sara = SaraLocal.PreparedPhrases.Concat(FlagWatch.Phrases).Concat(NativeRaceControl.PreparedPhrases);
                var live = CrewChief.currentGameState;
                var hazardSections = live == null ? Enumerable.Empty<string>() :
                    RaceTrafficWatch.SectionPhrases(live.SessionData.TrackDefinition, live.PositionAndMotionData.DistanceRoundTrack);
                // Gefahrensätze auch in Saras Stimme, weil sie diese Warnungen spricht, sobald sie spottet.
                var phrases = sara.Concat(CrewSpeechPolicy.HazardPhrases).Concat(hazardSections).Distinct().Select(t => new KeyValuePair<TeamMember,string>(TeamMember.Sara,t))
                    .Concat(CrewSpeechPolicy.TrackLimitPhrases.Concat(LiveWarningDispatcher.MikPhrases).Concat(ThermalWatch.PreparedPhrases).Concat(CrewSpeechPolicy.FinishPhrases).Concat(LapWatch.DisqualificationPhrases(settings.RadioName)).Concat(CrewSpeechPolicy.IncidentPhrases).Concat(CrewSpeechPolicy.WeatherPhrases).Concat(PitLaneWatch.UnknownLimitPhrases).Concat(new[]{SessionLapObserver.TimedLapStartPhrase, SessionLapObserver.TimedLapApproachPhrase}).Select(t => new KeyValuePair<TeamMember,string>(TeamMember.Mik,t)))
                    .Concat(TrackSituationWatch.CatchingPhrases.Concat(TrackSituationWatch.StoppedPhrases).Concat(TrackSituationWatch.ApproachingPhrases)
                        .Concat(TrackSituationWatch.ClosingAheadPhrases).Concat(new[] { IncidentWatch.DamagePitAdvicePhrase })
                        .Concat(RaceTrafficWatch.PreparedPhrases).Concat(hazardSections).Concat(NativeIncidentWatch.Phrases).Concat(RaceSituationWatch.PreparedPhrases(settings.RadioName))
                        .Concat(MulticlassTraffic.PreparedPhrases)
                        .Select(t => new KeyValuePair<TeamMember,string>(TeamMember.Ben,t)))
                    .Where(p=>settings.IsMemberEnabled(p.Key)).ToArray();
                foreach (var phrase in phrases)
                {
                    if (!settings.IsMemberEnabled(phrase.Key)) continue;
                    try { await RenderAsync(phrase.Key,phrase.Value,CancellationToken.None,preparePhrase:true).ConfigureAwait(false); ready++; }
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
                Reader = raceReader,
                PitForecast = pitCycle.ReadForecast(gs, DateTime.UtcNow),
                PitLossSeconds = pitLoss.Seconds,
                PitLossSamples = pitLoss.Samples,
                LastPitStop = pitStopTimer.Last,
                EventLog = eventLog,
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
