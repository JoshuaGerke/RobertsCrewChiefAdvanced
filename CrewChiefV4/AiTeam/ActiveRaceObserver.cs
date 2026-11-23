using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    internal sealed class PitGainEvidence
    {
        public readonly string Key, Name, Number;
        public readonly string Identity;
        public readonly int CarId, FormerRank;
        public readonly DateTime EnteredUtc;

        public PitGainEvidence(string key, OpponentData rival, int formerRank, DateTime entered)
        {
            Key = key; Name = rival.DriverRawName; Number = rival.CarNumber; CarId = rival.CostId;
            Identity = RaceReader.Identity(key, rival);
            FormerRank = formerRank; EnteredUtc = entered;
        }

        public bool Matches(NativeFieldFact fact) => fact != null && fact.Kind == "pit" && fact.Key == Key &&
            fact.Identity == Identity && Math.Abs((fact.Utc - EnteredUtc).TotalSeconds) <= 3;

        public bool StillInPits(GameStateData gs) => gs != null && gs.OpponentData.TryGetValue(Key, out var rival) &&
            rival != null && rival.IsActive && rival.InPits && RaceReader.Identity(Key, rival) == Identity &&
            gs.carClass != null && rival.CarClass != null && CarData.IsCarClassEqual(gs.carClass, rival.CarClass, false);
    }

    public sealed class PreparedRadioDetail
    {
        public string Text;
        public Func<GameStateData,DateTime,bool> StillCurrent;
    }

    public sealed class ObservedRaceCall
    {
        public string Topic, Text, Evidence;
        internal string OpponentNameClip, HazardIdentity, ChaseOpponentIdentity;
        public string EventId { get; internal set; } = Guid.NewGuid().ToString("N");
        public Func<GameStateData,DateTime,PreparedRadioDetail> PreparedDetail;

        /// <summary>Optional numeric detail is cache-only. A miss always retains the immediate generic warning.
        /// After any failed delivery attempt retry the generic warning, never wait on cloud synthesis.</summary>
        public string ComposePreparedDetail(GameStateData state, DateTime now, Func<string,bool> cached,
            out Func<GameStateData,DateTime,bool> stillCurrent)
        {
            stillCurrent=null;
            if(PreparedDetail==null || Volatile.Read(ref attempts)!=1 || cached==null) return Text;
            var detail=PreparedDetail(state,now);
            if(detail==null || detail.StillCurrent==null || !detail.StillCurrent(state,now) || !cached(detail.Text)) return Text;
            stillCurrent=detail.StillCurrent;return detail.Text;
        }
        internal bool ConfirmedPass;
        internal RadioVariantCursor VariantCursor, AdditionalVariantCursor;
        internal Action<DateTime> PlaybackCompleted;
        internal string VariantGroup;
        internal int VariantIndex = -1;
        internal void BindVariant(RadioVariantCursor cursor, string group, int index)
        {
            VariantCursor = cursor;
            VariantGroup = group;
            VariantIndex = index;
            ReferenceTemplate = RadioPhraseCatalog.Current.ReferenceFor(group, index);
        }
        internal string ReferenceTemplate;
        /// <summary>RRCA 2.0 (6.3): Platz, den diese Ansage als aktuellen eigenen Klassenplatz nennt (0 = keiner).
        /// Damit erkennt das Crew-Gedächtnis, wenn derselbe Stand schon gemeldet wurde.</summary>
        internal int StatedPosition;
        /// <summary>RRCA 2.0 (6.3): semantische Ereignis-ID (z. B. „klassenbestzeit|80.108“). Gleiche ID = gleicher Vorfall,
        /// auch wenn ein anderer Wächter ihn anders formuliert.</summary>
        internal string SemanticKey;
        internal PitGainEvidence PitGain;
        internal bool FieldNamesAllowed;
        internal bool IsReserved => Volatile.Read(ref deliveryState) == 1;
        internal bool MaySpeakPitGain => PitGain != null && Volatile.Read(ref attempts) < 2;
        internal bool PitGainWasSpoken => DeliveryStartedUtc != DateTime.MinValue && Volatile.Read(ref pitGainSpoken) != 0;
        private int pitGainSpoken;

        public string ComposeForDelivery(RaceDataContext context, out Func<GameStateData, DateTime, bool> detailIsCurrent,
            bool standalonePitClaimed = false)
        {
            detailIsCurrent = null;
            if (PitGain != null) ReferenceTemplate = null;
            if (MaySpeakPitGain && !standalonePitClaimed && context != null && PitGain.StillInPits(context.GameState))
            {
                string pitRivalName = context.SendDriverNames ? RadioPhraseCatalog.SafeRivalName(PitGain.Name) : "";
                string position = Position.ToString(CultureInfo.InvariantCulture) + (RaceRadioLanguage.SeparateClass(context.GameState.SessionData) ? RadioLanguage.T(" in deiner Klasse", " in your class", " en tu clase") : "");
                detailIsCurrent = (live, now) => PitGain.StillInPits(live);
                const string group = "pit-gain";
                var catalog = RadioPhraseCatalog.Current;
                int index = VariantCursor.Select(group, catalog.Count(group));
                BindVariant(VariantCursor, group, index);
                return catalog.Format(group, index,
                    pitRivalName.Length == 0 ? RadioLanguage.T("Der Vordermann", "The car ahead", "Tu rival de delante") : pitRivalName, position);
            }
            if (RaceStatus && Volatile.Read(ref attempts) == 1 && context?.GameState?.SessionData.SessionType == SessionType.Race)
            {
                var stateForStatus = context.GameState;
                var front = RaceDataTools.Neighbour(stateForStatus, true);
                var gap = RaceDataTools.ReadGap(context, front, true);
                string statusRivalName = context.SendDriverNames && front?.CanUseName == true ?
                    RadioPhraseCatalog.SafeRivalName(front.DriverRawName) : "";
                if (statusRivalName.Length > 0 && gap.Kind == RaceGapKind.SimulatorTiming && gap.Seconds >= .1f && gap.Seconds <= 2.5f)
                {
                    string frontKey = stateForStatus.SessionData.OpponentKeyInFront;
                    string statusRivalIdentity = RaceReader.Identity(frontKey, front);
                    detailIsCurrent = (live, at) => live.SessionData.OpponentKeyInFront == frontKey &&
                        RaceDataTools.Neighbour(live, true) is OpponentData currentFront &&
                        RaceReader.Identity(frontKey, currentFront) == statusRivalIdentity &&
                        RaceDataTools.ReadGap(live, currentFront, true) is RaceGapMeasurement refreshed &&
                        refreshed.Kind == gap.Kind && Math.Abs(refreshed.Seconds - gap.Seconds) <= .4f;
                    return Text + " Auf " + statusRivalName + " fehlen " + GermanRadioText.Interval(gap.Seconds) + ".";
                }
            }
            // If the optional detail expires during rendering, the ordinary retry can still report the pass.
            if (!ConfirmedPass || Volatile.Read(ref attempts) != 1 || context == null || !context.SendDriverNames)
                return Text;

            var state = context.GameState;
            var rival = RaceDataTools.Neighbour(state, true);
            if (!CloseRaceRival(context, rival)) return Text;

            string key = state.SessionData.OpponentKeyInFront;
            string name = rival.DriverRawName;
            string identity = RaceReader.Identity("",rival);
            string spokenName = rival.CanUseName ? RadioPhraseCatalog.SafeRivalName(name) : "";
            if (spokenName.Length == 0) return Text;

            var tracks = context.Tracks;
            detailIsCurrent = (live, now) => {
                if (live == null || live.SessionData.OpponentKeyInFront != key) return false;
                var front = RaceDataTools.Neighbour(live, true);
                return front != null && RaceReader.Identity("",front) == identity &&
                    CloseRaceRival(new RaceDataContext { GameState = live, NowUtc = now,
                        LastTelemetryUtc = live.Now, Tracks = tracks }, front);
            };
            return Text + (RaceRadioLanguage.SeparateClass(state.SessionData) ?
                    RadioLanguage.T(" Du bist in deiner Klasse knapp hinter ", " In your class you're right behind ", " En tu clase vas justo detrás de ") :
                    RadioLanguage.T(" Du bist knapp hinter ", " You're right behind ", " Vas justo detrás de ")) +
                spokenName + RadioLanguage.T(". Weiter so.", ". Keep it up.", ". Sigue así.");
        }

        private static bool CloseRaceRival(RaceDataContext context, OpponentData rival)
        {
            if (rival == null || !rival.IsActive || rival.InPits || rival.isEnteringPits()) return false;
            string key = context.GameState.SessionData.OpponentKeyInFront;
            if (key == null || !context.GameState.OpponentData.TryGetValue(key, out var mapped) || !ReferenceEquals(mapped, rival))
                return false;
            var gap = RaceDataTools.ReadGap(context, rival, true);
            return (gap.Kind == RaceGapKind.SimulatorTiming || gap.Kind == RaceGapKind.InterpolatedTiming) &&
                gap.Seconds > 0.05f && gap.Seconds <= 1.2f;
        }
        public int Position, SessionId, Priority, LapsRemaining = -1;
        public SessionType SessionType = SessionType.Race;
        public TeamMember Member = TeamMember.Ben;
        public int CompletedLaps = -1;
        public string AheadName, BehindName;
        private string aheadKey,behindKey,aheadIdentity,behindIdentity;
        private bool aheadIdentityBound,behindIdentityBound;
        private RaceGapKind aheadKind,behindKind;
        private Func<GameStateData,bool,DateTime,RaceGapMeasurement> timedGapReader;
        public float AheadGap = -1, BehindGap = -1;
        public Func<GameStateData,bool,float> CurrentGapReader;
        public Func<GameStateData,bool> AdditionalValidity;
        public Func<GameStateData,DateTime,bool> TimedValidity;
        // Only a just-completed practice/qualifying lap can outlive the timed session phase.
        public bool AllowLapFinish;
        public bool AllowPitlane;
        internal bool RaceStatus;
        /// <summary>Gewählte Formulierung des Rennstatus, bleibt beim Auffrischen erhalten.</summary>
        internal int StatusVariant;
        /// <summary>Im selben Funkspruch mitgesagte Berichte (Thema, Formulierung), die beim Auffrischen neu berechnet werden.</summary>
        internal List<KeyValuePair<string, int>> BundleParts;
        /// <summary>Eingestellter Bericht, der schon zu lange wartet: kommt in der nächsten freien Lücke, auch im Zweikampf.</summary>
        internal bool Guaranteed;
        internal Func<RaceDataContext, string> RefreshText;
        internal Func<RaceDataContext, float> RefreshGapS;
        internal float SpokenGapS = -1;
        internal bool CanRefresh => RaceStatus || RefreshText != null;
        internal bool WasDeferred { get; private set; }
        internal bool RefreshBeforeSpeak(RaceDataContext context)
        {
            if (!CanRefresh) return true;
            var live = context.GameState;
            if (live == null || context.NowUtc > ExpiresUtc || live.SessionData.SessionId != SessionId ||
                live.SessionData.SessionType != SessionType || live.SessionData.SessionPhase != SessionPhase.Green ||
                live.FlagData.isLocalYellow || live.PitData.InPitlane) return false;
            // AMS2 Spa 23.11.: Vier Formulierungen wurden erzeugt, gesprochen wurde viermal „Platz 3. Seit dem Start 1 Platz
            // gutgemacht.“ – die Auffrischung vor dem Sprechen nahm immer die erste. Sie behält jetzt die gewählte Formulierung.
            string current = RaceStatus ? AiRadioSchedule.Compose("status", context, StatusVariant) : RefreshText(context);
            if (string.IsNullOrWhiteSpace(current)) return false;
            // Mitgesagte Berichte (Rob 23.11.) mit frischen Zahlen und derselben Formulierung wieder anhängen.
            if (BundleParts != null)
                foreach (var part in BundleParts)
                {
                    string fresh = AiRadioSchedule.Compose(part.Key, context, part.Value);
                    if (!string.IsNullOrWhiteSpace(fresh) && !current.Contains(fresh)) current += " " + fresh;
                }
            Text = current;
            if (RefreshGapS != null) SpokenGapS = RefreshGapS(context);
            if (RaceStatus)
            {
                var snapshot = live.SessionData;
                Position = snapshot.ClassPosition;
                int overall = snapshot.OverallPosition;
                bool separate = RaceRadioLanguage.SeparateClass(snapshot);
                AdditionalValidity = state => !state.FlagData.isLocalYellow &&
                    (!separate || state.SessionData.OverallPosition == overall);
            }
            return true;
        }
        public DateTime DetectedUtc, ExpiresUtc;
        private int deliveryState, attempts;
        private long playbackStartedTicks, retryNotBeforeTicks,playbackEndedTicks;
        private int playbackInterrupted;
        public bool PlaybackInterrupted {get{return Volatile.Read(ref playbackInterrupted)!=0;}}
        public DateTime PlaybackEndedUtc {get{long t=Interlocked.Read(ref playbackEndedTicks);return t>0?new DateTime(t,DateTimeKind.Utc):DateTime.MinValue;}}
        public void MarkPlaybackFinished(DateTime now,bool interrupted)
        {
            DateTime started=DeliveryStartedUtc;
            if(started==DateTime.MinValue || now<started || Interlocked.CompareExchange(ref playbackEndedTicks,now.Ticks,0)!=0) return;
            if(interrupted)Interlocked.Exchange(ref playbackInterrupted,1);
            else
            {
                if (PitGain == null || Volatile.Read(ref pitGainSpoken) != 0) VariantCursor?.OnPlaybackCompleted(VariantGroup, VariantIndex);
                AdditionalVariantCursor?.OnPlaybackCompleted();
                PlaybackCompleted?.Invoke(now);
                if (ReferenceTemplate != null) LiveDiagnostics.Decision(Topic, "template_completed", ReferenceTemplate);
            }
            if(LiveDiagnostics.Enabled)LiveDiagnostics.Emit("race.event_completed",LiveDiagnostics.Data("topic",Topic,
                "duration_ms",(now-started).TotalMilliseconds,"template",ReferenceTemplate,
                "result",interrupted?"interrupted":"backend_finished_not_audibility_proof"),EventId);
        }
        public const int MaximumDeliveryAttempts = 2;
        public DateTime DeliveryStartedUtc
        {
            get { long ticks = Interlocked.Read(ref playbackStartedTicks); return Volatile.Read(ref deliveryState) == 2 && ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue; }
        }
        public bool DeliveryFailed { get { return Volatile.Read(ref deliveryState) == 3; } }
        /// <summary>Mindestens ein Zustellversuch lief bis zur Audiowiedergabe oder Sprachausgabe (vielleicht teilweise gehört).</summary>
        internal bool Attempted => Volatile.Read(ref attempts) > 0;
        public bool CanReserve(DateTime now)
        {
            return Volatile.Read(ref deliveryState) == 0 && Volatile.Read(ref attempts) < MaximumDeliveryAttempts &&
                now.Ticks >= Interlocked.Read(ref retryNotBeforeTicks) && now >= DetectedUtc && now <= ExpiresUtc;
        }
        public bool TryReserve(DateTime now)
        {
            if (!CanReserve(now) || Interlocked.CompareExchange(ref deliveryState, 1, 0) != 0) return false;
            Interlocked.Increment(ref attempts);
            LiveDiagnostics.Decision(Topic, "reserved", "awaiting_playback", Evidence, EventId);
            if(LiveDiagnostics.Enabled)LiveDiagnostics.Emit("race.event_reserved",LiveDiagnostics.Data("topic",Topic,"text",Text,"reason",Evidence,"template",ReferenceTemplate),EventId);
            return true;
        }
        // Called from the audio callback. Observers consume this state on their own telemetry thread.
        public void MarkPlaybackStarted(DateTime now, bool spokePitGain = false)
        {
            if (now < DetectedUtc || now > ExpiresUtc || Volatile.Read(ref deliveryState) != 1) return;
            Interlocked.Exchange(ref playbackStartedTicks, now.Ticks);
            Volatile.Write(ref pitGainSpoken, spokePitGain ? 1 : 0);
            if (Interlocked.CompareExchange(ref deliveryState, 2, 1) == 1)
            {
                LiveDiagnostics.Decision(Topic, "delivered", "playback_started", Evidence, EventId);
                if(LiveDiagnostics.Enabled)LiveDiagnostics.Emit("race.event_delivered",LiveDiagnostics.Data("topic",Topic,"reason",Evidence,"template",ReferenceTemplate,
                    "duration_ms",(now-DetectedUtc).TotalMilliseconds,"measurement","detection_to_audio_backend_start"),EventId);
                if (ReferenceTemplate != null) LiveDiagnostics.Decision(Topic, "template_started", ReferenceTemplate);
            }
        }
        public void ReleaseForRetry(DateTime now, string reason = "delivery_failed")
        {
            bool terminal = now > ExpiresUtc || Volatile.Read(ref attempts) >= MaximumDeliveryAttempts;
            bool immediateTraffic = Topic == "race-hazard-ahead" || Topic == "race-traffic-rear" || Topic == "race-class-ahead";
            Interlocked.Exchange(ref retryNotBeforeTicks, now.AddMilliseconds(immediateTraffic ? 100 : 1000).Ticks);
            if (Interlocked.CompareExchange(ref deliveryState, terminal ? 3 : 0, 1) == 1)
            {
                LiveDiagnostics.Decision(Topic, terminal ? "suppressed" : "deferred", reason, Evidence, EventId);
                if(LiveDiagnostics.Enabled)LiveDiagnostics.Emit("race.event_retry",LiveDiagnostics.Data("topic",Topic,
                    "reason",reason,"status",terminal?"suppressed":"deferred"),EventId);
            }
        }
        internal void DeferDelivery(DateTime now, string reason)
        {
            if (!CanRefresh || now > ExpiresUtc) { ReleaseForRetry(now, reason); return; }
            if (Interlocked.CompareExchange(ref deliveryState, 4, 1) != 1) return;
            Interlocked.Decrement(ref attempts);
            Interlocked.Exchange(ref retryNotBeforeTicks, now.AddMilliseconds(400).Ticks);
            WasDeferred = true;
            Volatile.Write(ref deliveryState, 0);
            LiveDiagnostics.Decision(Topic, "deferred", reason, Evidence, EventId);
        }
        /// <summary>Rennkopf: jetzt nicht, später erneut versuchen. Zählt nicht als Zustellversuch.</summary>
        internal void HoldBack(DateTime now, TimeSpan wait, string reason)
        {
            if (Interlocked.CompareExchange(ref deliveryState, 4, 1) != 1) return;
            Interlocked.Decrement(ref attempts);
            Interlocked.Exchange(ref retryNotBeforeTicks, now.Add(wait).Ticks);
            Volatile.Write(ref deliveryState, 0);
            LiveDiagnostics.Decision(Topic, "held", reason, Evidence, EventId);
        }
        public bool IsValid(GameStateData gs, DateTime now)
        {
            if (gs == null || DeliveryFailed || now < DetectedUtc || now > ExpiresUtc) return false;
            if (gs.SourceTelemetryUtc != DateTime.MinValue &&
                (now < gs.SourceTelemetryUtc || now - gs.SourceTelemetryUtc > TimeSpan.FromSeconds(1))) return false;
            var s = gs.SessionData;
            bool weather = AllowPitlane && (Topic ?? "").StartsWith("weather-", StringComparison.Ordinal);
            bool pit = AllowPitlane && (Topic == "pit-limit" || Topic == "pit-speed" || Topic == "pit-exit" ||
                Topic == "pit-arrival-support" || Topic == "pit-repair-wait" || Topic == "race-retired");
            bool greeting = AllowPitlane && Topic == "greeting" && SessionGreetingDirector.SafeToGreet(
                s.SessionType, s.SessionPhase, gs.PitData.InPitlane, gs.PositionAndMotionData.CarSpeed);
            if (Topic == "greeting" && !greeting) return false;
            // R3E 3.6 can keep a later start group in formation after global green.
            bool formation = Topic == "formation" && RaceSituationWatch.InFormation(gs, now);
            if (Topic == "formation" && !formation) return false;
            bool qualifyingResult = Topic == "qualifying-result" && QualifyingResultWatch.Supported(s.SessionType) && AllowLapFinish &&
                (s.SessionPhase == SessionPhase.Checkered || s.SessionPhase == SessionPhase.Finished);
            bool pitFinish = Topic == "pit-finish" && s.SessionType == SessionType.Race &&
                s.SessionPhase == SessionPhase.Finished && gs.PitData.InPitlane;
            bool disqualified = Topic == "race-disqualified" && LapWatch.ConfirmedDisqualification(gs);
            bool qualifyingLast = Topic == "qualifying-last" && QualifyingResultWatch.Supported(s.SessionType) &&
                (s.SessionPhase == SessionPhase.Green || s.SessionPhase == SessionPhase.Checkered);
            bool liveWarning=(Topic??"").StartsWith("warning-",StringComparison.Ordinal) &&
                (s.SessionPhase==SessionPhase.Green || s.SessionPhase==SessionPhase.FullCourseYellow);
            bool retiredAfterFlag = AllowPitlane && Topic == "race-retired" && s.SessionType == SessionType.Race &&
                (s.SessionPhase == SessionPhase.Checkered || s.SessionPhase == SessionPhase.Finished);
            // Schwarze Flagge ist Pflicht und gilt auch, wenn die Sim Rob danach in die Box versetzt (AC Monza 21.11.).
            bool blackFlag = Topic == "warning-flag-5" && RaceExclusion.Active(gs);
            // Strafe für Tempo in der Boxengasse kommt in der Boxengasse – dort muss sie auch gesagt werden dürfen.
            bool penalty = Topic == "warning-penalty" || (Topic ?? "").StartsWith("warning-rc-pen", StringComparison.Ordinal);
            bool phaseValid = blackFlag || retiredAfterFlag || disqualified || liveWarning || pitFinish || qualifyingResult || qualifyingLast || formation || greeting || (weather || pit) && (s.SessionPhase == SessionPhase.Garage || s.SessionPhase == SessionPhase.Gridwalk || s.SessionPhase == SessionPhase.Countdown || s.SessionPhase == SessionPhase.FullCourseYellow || s.SessionPhase == SessionPhase.Formation) || s.SessionPhase == SessionPhase.Green || AllowLapFinish &&
                SessionLapObserver.Supported(s.SessionType) && (Topic == "own-lap" || Topic == "near-best") &&
                (s.SessionPhase == SessionPhase.Checkered || s.SessionPhase == SessionPhase.Finished);
            return s.SessionId == SessionId && s.SessionType == SessionType && phaseValid &&
                (!gs.PitData.InPitlane || blackFlag || penalty || disqualified || pitFinish || qualifyingResult || weather || pit || greeting) && (Position < 0 || s.ClassPosition == Position) && (AdditionalValidity == null || AdditionalValidity(gs)) && (TimedValidity == null || TimedValidity(gs,now)) &&
                (LapsRemaining < 0 || (!s.SessionHasFixedTime && s.SessionLapsRemaining == LapsRemaining)) &&
                (CompletedLaps < 0 || s.CompletedLaps == CompletedLaps) && GapStillValid(gs, now, true, AheadName, AheadGap) && GapStillValid(gs, now, false, BehindName, BehindGap);
        }
        private bool GapStillValid(GameStateData gs, DateTime now, bool ahead, string name, float oldGap)
        {
            if (oldGap < 0) return true;
            var opponent = RaceDataTools.Neighbour(gs, ahead);
            var measurement=timedGapReader==null?null:timedGapReader(gs,ahead,now);
            if(measurement!=null && measurement.Kind!=(ahead?aheadKind:behindKind))return false;
            float gap = measurement!=null?measurement.Seconds:CurrentGapReader == null ? RaceDataTools.GapSeconds(gs, opponent, ahead) : CurrentGapReader(gs,ahead);
            string key=ahead?aheadKey:behindKey; OpponentData original;
            bool bound=ahead?aheadIdentityBound:behindIdentityBound;
            return opponent != null && opponent.DriverRawName == name &&
                (!bound || key!=null && gs.OpponentData.TryGetValue(key,out original) && ReferenceEquals(original,opponent) &&
                    RaceReader.Identity("",opponent)==(ahead?aheadIdentity:behindIdentity)) &&
                gap > 0 && Math.Abs(gap - oldGap) <= .5f;
        }
        public void IncludeNeighbour(RaceDataContext ctx, bool ahead, bool appendText)
        {
            // A confirmed event is kept short and independent of changing optional gap details.
            // Dedicated ahead/behind reports retain their own strict neighbour/gap validity.
            if (appendText && Topic != "ahead" && Topic != "behind") return;
            var op = RaceDataTools.Neighbour(ctx.GameState, ahead);
            var text = AiRadioSchedule.Compose(ahead ? "ahead" : "behind", ctx);
            if (op == null || text == null) return;
            if (appendText) Text += " " + text;
            timedGapReader=(live,direction,at)=>RaceDataTools.ReadGap(new RaceDataContext {
                GameState=live, NowUtc=at, Tracks=ctx.Tracks,
                LastTelemetryUtc=live.SourceTelemetryUtc==DateTime.MinValue?live.Now:live.SourceTelemetryUtc
            },RaceDataTools.Neighbour(live,direction),direction);
            string key=ctx.GameState.OpponentData.FirstOrDefault(p=>ReferenceEquals(p.Value,op)).Key;
            if (ahead) { AheadName = op.DriverRawName; AheadGap = RaceDataTools.ReadGap(ctx, op, true).Seconds;
                aheadKey=key;aheadIdentity=RaceReader.Identity("",op);aheadKind=RaceDataTools.ReadGap(ctx,op,true).Kind;aheadIdentityBound=true; }
            else { BehindName = op.DriverRawName; BehindGap = RaceDataTools.ReadGap(ctx, op, false).Seconds;
                behindKey=key;behindIdentity=RaceReader.Identity("",op);behindKind=RaceDataTools.ReadGap(ctx,op,false).Kind;behindIdentityBound=true; }
        }
    }

    /// <summary>
    /// Local race observations. Confirmation is 250 ms / at least three samples, independent of LLM cooldowns.
    /// Rank changes alone never prove an overtake. The same active on-track rival must change relative order.
    /// No file, audio or network work is performed here. One telemetry-thread owner; calls are immutable after return.
    /// </summary>
    public sealed class ActiveRaceObserver
    {
        private sealed class Car
        {
            public int Position;
            public bool Pit, Active;
            public string Identity;
            public float Speed;
        }
        private readonly Dictionary<string, Car> previousCars = new Dictionary<string, Car>();
        private readonly List<ObservedRaceCall> pending = new List<ObservedRaceCall>();
        private int stablePosition, candidatePosition, candidateSamples, session = int.MinValue, lastEndLap = -1, endLapCompleted = -1;
        private DateTime endLapArmedUntil = DateTime.MinValue;
        private int lossFrom;
        private DateTime lossStarted;
        private ObservedRaceCall lossCall;
        private readonly RadioVariantCursor lossVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor pressureVariants = new RadioVariantCursor();
        private string pressureRival, reportedPressureRival, pendingPressureRival;
        private ObservedRaceCall pressureCall;
        private float pressureBaseline;
        private RaceGapKind pressureKind;
        private DateTime pressureAt, reportedPressureAt;
        private DateTime candidateSince, lastTick, lastCall = DateTime.MinValue, nextSummary;
        private bool candidatePass, candidatePit, previousInPits;
        private string candidatePassKey, candidatePassIdentity;
        // Rennverständnis (Rob 21.11.): Wer hat Rob auf der Strecke überholt? Nur wenn genau ein Gegner von direkt hinter auf
        // direkt vor gewechselt ist, beide in Fahrt und nicht in der Box.
        private string candidateLossKey;
        private int storyVariant;
        private RadioVariantCursor passVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor firstPassVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor pitGainVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor lastLapVariants = new RadioVariantCursor(), closingLapVariants = new RadioVariantCursor();
        private string previousFrontKey;
        private PitGainEvidence recentFrontPit, spokenPitGain;
        private DateTime spokenPitGainUtc;

        public bool CoversPit(NativeFieldFact fact, GameStateData live, DateTime now)
        {
            if (spokenPitGain != null && now >= spokenPitGainUtc && now - spokenPitGainUtc <= TimeSpan.FromSeconds(12) && spokenPitGain.Matches(fact))
                return true;
            return pending.Any(call => call.PitGain != null && call.PitGain.Matches(fact) &&
                (call.PitGainWasSpoken || call.MaySpeakPitGain && call.DeliveryStartedUtc == DateTime.MinValue &&
                    call.IsValid(live, now) && call.PitGain.StillInPits(live)));
        }

        private void RememberSpokenPitGain()
        {
            foreach (var call in pending)
            {
                if (!call.PitGainWasSpoken || call.DeliveryStartedUtc <= spokenPitGainUtc) continue;
                spokenPitGain = call.PitGain;
                spokenPitGainUtc = call.DeliveryStartedUtc;
            }
        }
        private bool wasGreen, startVerdictDone;
        private int startPosition;
        private int lastCutWarnings, lastCompletedLaps;
        private bool lastLapValid;
        private DateTime lastConductIssue = DateTime.MinValue;
        public int PendingCount { get { return pending.Count; } }
        public void Enqueue(ObservedRaceCall call)
        {
            if(pending.Count>=8) pending.RemoveAt(0);
            pending.Add(call);
        }

        public void Reset()
        {
            previousCars.Clear(); pending.Clear(); stablePosition = candidatePosition = candidateSamples = startPosition = 0;
            lossFrom = 0; lossStarted = DateTime.MinValue; lossCall = null;
            pressureRival = reportedPressureRival = null;
            pressureCall = null; pendingPressureRival = null;
            pressureAt = reportedPressureAt = DateTime.MinValue;
            passVariants = new RadioVariantCursor();
            previousFrontKey = null;
            recentFrontPit = spokenPitGain = null;
            spokenPitGainUtc = DateTime.MinValue;
            lastCutWarnings = lastCompletedLaps = 0; lastLapValid = false; lastConductIssue = DateTime.MinValue;
            lastEndLap = -1; endLapCompleted = -1; endLapArmedUntil = DateTime.MinValue; session = int.MinValue; lastTick = lastCall = DateTime.MinValue; wasGreen = false; previousInPits = false; startVerdictDone = false;
        }

        public void Observe(GameStateData gs, DateTime now, string driverName, int intervalSeconds,
            AiRaceMemory memory = null, bool names = false)
        {
            if (gs == null) return;
            lossDriverName = driverName;
            var s = gs.SessionData;
            if (s.SessionId != session || (lastTick != DateTime.MinValue && (now <= lastTick || now - lastTick > TimeSpan.FromSeconds(3))))
            {
                Reset(); session = s.SessionId;
                nextSummary = now.AddSeconds(Math.Max(1, intervalSeconds));
            }
            bool hadSample = lastTick != DateTime.MinValue;
            bool newInvalidation = hadSample && lastLapValid && !s.CurrentLapIsValid && s.CompletedLaps == lastCompletedLaps;
            bool newCut = hadSample && gs.PenaltiesData.CutTrackWarnings > lastCutWarnings;
            if (newInvalidation || newCut || gs.PenaltiesData.IsOffRacingSurface) lastConductIssue = now;
            bool conductUncertain = now - lastConductIssue < TimeSpan.FromSeconds(2);
            lastCutWarnings = gs.PenaltiesData.CutTrackWarnings; lastCompletedLaps = s.CompletedLaps; lastLapValid = s.CurrentLapIsValid;
            lastTick = now;
            bool green = s.SessionType == SessionType.Race && s.SessionPhase == SessionPhase.Green;
            RememberSpokenPitGain();
            if (green && wasGreen && !previousInPits && !gs.PitData.InPitlane && stablePosition > 1 && previousFrontKey != null &&
                previousCars.TryGetValue(previousFrontKey, out var oldFront) && oldFront.Position == stablePosition - 1 && oldFront.Active && !oldFront.Pit &&
                gs.OpponentData.TryGetValue(previousFrontKey, out var entering) && entering != null && entering.IsActive && entering.InPits &&
                RaceReader.Identity(previousFrontKey, entering) == oldFront.Identity &&
                gs.carClass != null && entering.CarClass != null && CarData.IsCarClassEqual(gs.carClass, entering.CarClass, false))
                recentFrontPit = new PitGainEvidence(previousFrontKey, entering, oldFront.Position, now);
            if (!green || gs.PitData.InPitlane || previousInPits) recentFrontPit = null;
            if (startPosition == 0) startPosition = RaceStartLatch.Position(s);
            // Kurzes Startfazit nach der ersten halben Runde (Robs Wunsch), einmal pro Rennen, in wechselnden Formulierungen
            float trackLength = s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength;
            float lapDistance = gs.PositionAndMotionData.DistanceRoundTrack;
            if (green && !startVerdictDone && startPosition > 0 && s.CompletedLaps == 0 && s.ClassPosition > 0 && !gs.PitData.InPitlane &&
                trackLength > 500 && lapDistance >= trackLength / 2 && lapDistance < trackLength * .85f)
            {
                startVerdictDone = true;
                int verdictVariant = Environment.TickCount & 0xFF, verdictStart = startPosition, verdictSession = s.SessionId, verdictStory = storyVariant;
                // Rennverständnis (Rob 21.11.): an wem vorbei, wer vorbei – aus der Rennchronik, nur mit erlaubten Namen.
                Func<SessionData, string> compose = snapshot => {
                    string startText = StartVerdict(snapshot, verdictStart, driverName, verdictVariant);
                    var chronicle = RaceChronicle.Current;
                    if (names && chronicle.Session == verdictSession && chronicle.Type == SessionType.Race)
                    {
                        string who = RaceStory.StartNames(chronicle, verdictStory);
                        if (who.Length > 0) startText += " " + who;
                    }
                    return startText;
                };
                // LMU Bahrain 22.11.: Das Startfazit verfiel nach 8 s (erkannt 11:45:33, verfallen 11:45:41), weil Rob mitten im
                // Getümmel war und der Platz sich noch änderte. Jetzt lebt es 30 s, ist nicht an den Platz der Erkennung gebunden
                // und nennt beim Sprechen den aktuellen Platz.
                Add("start-verdict", compose(s), gs, now, StartVerdictSeconds, 5, "class_position_at_half_of_lap_one");
                var verdictCall = pending.Last();
                verdictCall.Position = -1;
                verdictCall.RefreshText = ctx => {
                    var snapshot = ctx.GameState.SessionData;
                    if (snapshot.SessionId != verdictSession || snapshot.ClassPosition <= 0) return null;
                    verdictCall.StatedPosition = snapshot.ClassPosition;
                    return compose(snapshot);
                };
                storyVariant++;
            }
            if (!green || gs.PitData.InPitlane || s.ClassPosition <= 0 || previousInPits)
            {
                stablePosition = s.ClassPosition; candidatePosition = 0;
                lossFrom = 0; lossCall = null;
                // Keep a deferred formation call across formation samples so it can retry delivery.
                pending.RemoveAll(c => c.Topic != "formation" || !RaceSituationWatch.InFormation(gs, now) || gs.PitData.InPitlane);
            }
            else if (stablePosition <= 0) stablePosition = s.ClassPosition;
            else if (s.ClassPosition == stablePosition) candidatePosition = 0;
            else
            {
                if (candidatePosition != s.ClassPosition)
                {
                    candidatePosition = s.ClassPosition; candidateSince = now; candidateSamples = 1;
                    candidatePass = false; candidatePit = false; candidatePassKey = candidatePassIdentity = candidateLossKey = null;
                    if (candidatePosition == stablePosition + 1 && wasGreen) candidateLossKey = Passer(gs, stablePosition, candidatePosition);
                    if (candidatePosition == stablePosition - 1 && wasGreen)
                    {
                        foreach (var pair in gs.OpponentData)
                        {
                            Car before;
                            var rival = pair.Value;
                            if (rival == null || !previousCars.TryGetValue(pair.Key, out before) || before.Identity != RaceReader.Identity(pair.Key, rival) ||
                                before.Position != candidatePosition || rival.ClassPosition != stablePosition ||
                                rival.CarClass == null || gs.carClass == null || rival.CarClass.getClassIdentifier() != gs.carClass.getClassIdentifier()) continue;
                            candidatePit |= before.Pit || rival.InPits;
                            float delta = (s.CompletedLaps - rival.CompletedLaps) * (s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength) +
                                gs.PositionAndMotionData.DistanceRoundTrack - rival.DistanceRoundTrack;
                            // Exclude stopped/spinning cars, lap corrections and unmeasured order changes from praise.
                            bool crossing = !before.Pit && !rival.InPits && before.Active && rival.IsActive &&
                                before.Speed > 10 && rival.Speed > 10 && gs.PositionAndMotionData.CarSpeed > 10 &&
                                s.TrackDefinition != null && s.TrackDefinition.trackLength > 100 && delta >= 0 && delta < 150;
                            if (!crossing) continue;
                            if (candidatePassKey != null)
                            {
                                candidatePass = false;
                                break;
                            }
                            candidatePassKey = pair.Key;
                            candidatePassIdentity = before.Identity;
                            candidatePass = true;
                        }
                    }
                }
                else candidateSamples++;
                if (conductUncertain) candidatePass = false;
                if (gs.OpponentData.Values.Any(o => o.ClassPosition == stablePosition && (o.InPits || !o.IsActive || o.Speed <= 10)))
                    candidatePass = false;
                // In der ersten Runde wechseln die Plätze im Sekundentakt; erst einen stabilen Platz melden statt jeden Zwischenstand
                TimeSpan settle = s.CompletedLaps < 1 && !candidatePass ? TimeSpan.FromSeconds(2.5) : TimeSpan.FromMilliseconds(250);
                if (candidateSamples >= 3 && now - candidateSince >= settle)
                {
                    int gain = stablePosition - candidatePosition;
                    pending.RemoveAll(c => c.Topic == "position" || c.Topic == "summary");
                    if (gain > 0)
                    {
                        string text;
                        bool firstPass = passVariants.Heard == 0;
                        string passGroup = firstPass ? "pass-first" : "pass-repeat";
                        var passCursor = firstPass ? firstPassVariants : passVariants;
                        int passIndex = 0;
                        // Rob 16.11.: Überholmanöver wurden oft nur als „vorgerückt“ gemeldet. Steht der überholte Klassengegner
                        // jetzt direkt hinter uns (unter 120 m, beide in Fahrt), ist es ein Manöver – auch ohne den engen Kreuzungsnachweis.
                        bool likelyPass = !candidatePass && !candidatePit && !conductUncertain && gain == 1 && LikelyPassedCarBehind(gs, candidatePosition);
                        if ((candidatePass || likelyPass) && !candidatePit)
                        {
                            passIndex = passCursor.Select(passGroup, RadioPhraseCatalog.Current.Count(passGroup));
                            text = RaceRadioLanguage.Overtake(firstPass ? 1 : passIndex + 2, candidatePosition, driverName,
                                RaceRadioLanguage.SeparateClass(s), passIndex);
                            // Rennverständnis (Rob 21.11.): wer überholt wurde, und die Vorgeschichte mit ihm aus der Rennchronik.
                            if (names && candidatePass && candidatePassKey != null && gs.OpponentData.TryGetValue(candidatePassKey, out var passedRival) &&
                                passedRival != null && passedRival.CanUseName)
                            {
                                string storyPassedName = RadioPhraseCatalog.SafeRivalName(passedRival.DriverRawName);
                                storyVariant = PhraseMemory.FreshVariant(storyVariant, 5, alt => RaceStory.Passed(storyPassedName, alt));
                                text = (text + " " + RaceStory.Passed(storyPassedName, storyVariant) + " " +
                                    RaceStory.AfterGain(candidatePassKey, now, storyVariant)).Trim();
                                storyVariant++;
                            }
                        }
                        // Rob 16.11.: „vorgerückt“ nur, wenn es gegenüber der zuletzt gemeldeten Position wirklich nach vorn geht.
                        else if (lastAnnouncedPosition > 0 && candidatePosition >= lastAnnouncedPosition)
                            text = PositionNow(s, candidatePosition, positionVariant++);
                        // Mit Tracklimit-Zweifel bleibt die Meldung neutral: kein Lob, keine Wertung.
                        else if (conductUncertain)
                            text = RaceRadioLanguage.SeparateClass(s) ? RadioLanguage.T("Wir sind vorgerückt. Jetzt ", "We've moved up. Now ", "Hemos subido. Ahora ") +
                                RaceRadioLanguage.Rank(s, candidatePosition) + "." :
                                RadioLanguage.T("Wir sind vorgerückt. Du liegst jetzt auf Position " + candidatePosition + ".",
                                    "We've moved up. You're now P" + candidatePosition + ".", "Hemos subido. Ahora vas P" + candidatePosition + ".");
                        else text = MovedUp(s, candidatePosition, positionVariant++, driverName);
                        var pitGain = gain == 1 && !conductUncertain && recentFrontPit != null && recentFrontPit.FormerRank == candidatePosition &&
                            now - recentFrontPit.EnteredUtc <= TimeSpan.FromSeconds(2) && recentFrontPit.StillInPits(gs) &&
                            gs.OpponentData[recentFrontPit.Key].ClassPosition == stablePosition ? recentFrontPit : null;
                        lastAnnouncedPosition = candidatePosition;
                        // LMU 22.11.: Die Platzmeldung lebte 9 s und verfiel hinter Funklast und Warteschlange. Sie bleibt an den
                        // aktuellen Platz gebunden (ändert er sich, ist sie ungültig) und darf deshalb länger auf ihre Lücke warten.
                        Add("position", text, gs, now, PositionCallSeconds, candidatePass && !candidatePit ? 5 : 3,
                            pitGain != null ? "same_class_front_rival_entered_pit_before_rank_gain" :
                            candidatePass && !candidatePit ? "stable_rank_and_same_active_rival_crossing" : conductUncertain ? "stable_rank_with_track_limit_uncertainty" : "stable_rank_cause_unknown");
                        if (pitGain != null)
                        {
                            pending.Last().PitGain = pitGain;
                            pending.Last().VariantCursor = pitGainVariants;
                            recentFrontPit = null;
                        }
                        if (candidatePass && !candidatePit && pitGain == null)
                        {
                            int cuts = gs.PenaltiesData.CutTrackWarnings, lap = s.CompletedLaps;
                            bool validLap = s.CurrentLapIsValid;
                            string passedKey = candidatePassKey, passedIdentity = candidatePassIdentity;
                            // Delayed cut/invalidity flags can arrive after the rank changed, before speech starts.
                            var passCall = pending.Last();
                            passCall.ConfirmedPass = true;
                            if (memory != null && passedKey != null && gs.OpponentData.TryGetValue(passedKey, out var passedCar) &&
                                RaceReader.Identity(passedKey, passedCar) == passedIdentity)
                            {
                                string passedName = names && passedCar.CanUseName ?
                                    RadioPhraseCatalog.SafeRivalName(passedCar.DriverRawName) : "";
                                string confirmed = (passedName.Length == 0 ? "Auf der Strecke an einem Klassengegner vorbeigegangen." :
                                    "Auf der Strecke an " + passedName + " vorbeigegangen.") +
                                    " Von Platz " + stablePosition + " auf " + candidatePosition + ".";
                                memory.AddObservation(confirmed, now, "confirmed-on-track-pass", passedKey, passCall.Evidence);
                            }
                            passCall.BindVariant(passCursor, passGroup, passIndex);
                            if(firstPass)passCall.AdditionalVariantCursor=passVariants;
                            passCall.AdditionalValidity = live => !live.FlagData.isLocalYellow && !live.PenaltiesData.IsOffRacingSurface &&
                                live.PenaltiesData.CutTrackWarnings <= cuts &&
                                !(validLap && live.SessionData.CompletedLaps == lap && !live.SessionData.CurrentLapIsValid) &&
                                passedKey != null && live.OpponentData.TryGetValue(passedKey, out var passed) && passed != null &&
                                passed.IsActive && !passed.InPits && passed.ClassPosition > live.SessionData.ClassPosition &&
                                RaceReader.Identity(passedKey, passed) == passedIdentity;
                        }
                        // RaceRoom Monza 22.11.: Viermal hintereinander „Wieder einen Platz erkämpft. Sauber.“ Beim wahrscheinlichen
                        // Überholen (Gegner direkt hinter uns) wurde der Satz gewählt, aber nie als gehört verbucht – der Zeiger blieb stehen.
                        else if (likelyPass && !candidatePit && pitGain == null)
                        {
                            pending.Last().BindVariant(passCursor, passGroup, passIndex);
                            if (firstPass) pending.Last().AdditionalVariantCursor = passVariants;
                        }
                    }
                    else if (lossFrom == 0 && (-gain >= 3 || gs.PositionAndMotionData.CarSpeed > 0 && gs.PositionAndMotionData.CarSpeed < 12))
                    {
                        lossFrom = stablePosition;
                        lossStarted = now;
                    }
                    else if (lossFrom == 0)
                    {
                        lastAnnouncedPosition = candidatePosition;
                        string lostText = PositionLost(s, candidatePosition, positionVariant++, driverName);
                        if (names && candidateLossKey != null && gs.OpponentData.TryGetValue(candidateLossKey, out var passer) && passer != null && passer.CanUseName &&
                            passer.ClassPosition == candidatePosition - 1 && !passer.InPits)
                        {
                            string storyPasserName = RadioPhraseCatalog.SafeRivalName(passer.DriverRawName);
                            storyVariant = PhraseMemory.FreshVariant(storyVariant, 5, alt => RaceStory.PassedBy(storyPasserName, alt));
                            lostText = (RaceStory.PassedBy(storyPasserName, storyVariant) + " " + lostText + " " +
                                RaceStory.AfterLoss(candidateLossKey, now, storyVariant)).Trim();
                            storyVariant++;
                        }
                        // LMU 22.11. (Rob): „Valgo hat mich wieder überholt. Das war viel wichtiger.“ Die Meldung stand hinter „Durchatmen“
                        // und „Blick nach hinten“ in der Schlange und verfiel nach 8 s.
                        Add("position", lostText, gs, now, PositionCallSeconds, 2, candidateLossKey != null ? "stable_position_loss_same_class_rival_crossed" : "stable_position_loss_cause_unknown");
                    }
                    if (gain < 0)
                        memory?.AddObservation("Zurückgefallen von Platz " + stablePosition + " auf " + candidatePosition + ".",
                            now, "confirmed-position-loss", null, "stable_classification");
                    stablePosition = candidatePosition; candidatePosition = 0;
                }
            }
            ObservePositionLoss(gs, now, green);
            // AMS2 Spa 23.11.: „Noch 3 Runden“, „Noch 2“ und „Letzte Runde“ kamen je 14 s vor Robs Ziellinie (an der Bus-Stop-
            // Schikane) – die Restrunden zählt die Sim ab dem Führenden. Die Ansage kommt jetzt, wenn Rob selbst die Linie quert.
            // Führt Rob selbst, kann die Sim die Restrunden einen Tick nach seiner Rundenzahl umstellen – daher ein kurzes Fenster.
            if (endLapCompleted >= 0 && s.CompletedLaps > endLapCompleted) endLapArmedUntil = now.AddSeconds(5);
            endLapCompleted = s.CompletedLaps;
            if (green && wasGreen && now <= endLapArmedUntil && !gs.PitData.InPitlane && !s.SessionHasFixedTime && s.SessionNumberOfLaps > 0 &&
                s.SessionLapsRemaining > 0 && s.SessionLapsRemaining <= 3 && s.SessionLapsRemaining != lastEndLap)
            {
                lastEndLap = s.SessionLapsRemaining;
                endLapArmedUntil = DateTime.MinValue;
                string group=lastEndLap==1?"race-last":"race-closing";
                var cursor=lastEndLap==1?lastLapVariants:closingLapVariants;
                var catalog=RadioPhraseCatalog.Current;
                int variant=cursor.Select(group,catalog.Count(group));
                string text=lastEndLap==1?catalog.Format(group,variant,SafeName(driverName)):
                    catalog.Format(group,variant,lastEndLap.ToString(CultureInfo.InvariantCulture),SafeName(driverName));
                bool separateClass=RaceRadioLanguage.SeparateClass(s);
                if(s.ClassPosition==1)text+=separateClass?RadioLanguage.T(" Du führst in deiner Klasse."," You're leading your class."," Lideras tu clase."):
                    RadioLanguage.T(" Du führst."," You're leading."," Vas líder.");
                Add("race-end", text, gs, now, 12, 4, "fixed_lap_race_remaining_from_adapter");
                pending.Last().LapsRemaining = lastEndLap;
                pending.Last().BindVariant(cursor,group,variant);
                pending.Last().AdditionalValidity=live=>RaceRadioLanguage.SeparateClass(live.SessionData)==separateClass;
            }
            previousCars.Clear();
            foreach (var pair in gs.OpponentData)
                if (pair.Value != null)
                    previousCars[pair.Key] = new Car { Position = pair.Value.ClassPosition,
                        Pit = pair.Value.InPits, Active = pair.Value.IsActive, Speed = pair.Value.Speed,
                        Identity = RaceReader.Identity(pair.Key, pair.Value) };
            previousFrontKey = s.OpponentKeyInFront;
            previousInPits = gs.PitData.InPitlane; wasGreen = green;
        }

        private void ObservePositionLoss(GameStateData state, DateTime now, bool green)
        {
            if (lossFrom == 0 || !green) return;
            int position = state.SessionData.ClassPosition;
            if (lossCall?.DeliveryStartedUtc > DateTime.MinValue || now - lossStarted > TimeSpan.FromSeconds(30) ||
                position <= lossFrom && candidatePosition == 0)
            {
                lossFrom = 0;
                lossCall = null;
                return;
            }
            if (position != stablePosition || candidatePosition != 0 || position - lossFrom < 2 ||
                lossCall != null && lossCall.IsValid(state, now) ||
                now - candidateSince < TimeSpan.FromSeconds(2) ||
                state.PositionAndMotionData.CarSpeed < 12 && now - candidateSince < TimeSpan.FromSeconds(5)) return;

            pending.RemoveAll(call => call.Topic == "position" || call.Topic == "position-loss");
            bool separateClass = RaceRadioLanguage.SeparateClass(state.SessionData);
            const string group = "race-position-loss";
            var catalog = RadioPhraseCatalog.Current;
            int variant = lossVariants.Select(group, catalog.Count(group));
            int from = lossFrom;
            // Rob 16.11.: im Wechsel persönlich angesprochen.
            string lossName = lossCount++ % 2 == 0 ? AiPersonas.CleanForRadio(lossDriverName ?? "", 30, false) : "";
            string text = RadioPhraseCatalog.Address(catalog.Format(group, variant,
                lossFrom.ToString(CultureInfo.InvariantCulture), position.ToString(CultureInfo.InvariantCulture)), lossName) +
                (separateClass ? " In deiner Klasse." : "");
            Add("position-loss", text, state, now, 12, 4, "confirmed_grouped_class_position_loss");
            lossCall = pending.Last();
            var positionCall = lossCall;
            positionCall.BindVariant(lossVariants, group, variant);
            positionCall.RefreshText = ctx => {
                var snapshot = ctx.GameState.SessionData;
                int rank = snapshot.ClassPosition;
                if (rank < from + 2 || rank != Volatile.Read(ref stablePosition)) return null;
                positionCall.Position = rank;
                return RadioPhraseCatalog.Address(catalog.Format(group, variant, from.ToString(CultureInfo.InvariantCulture),
                    rank.ToString(CultureInfo.InvariantCulture)), lossName) + (RaceRadioLanguage.SeparateClass(snapshot) ? " In deiner Klasse." : "");
            };
        }

        internal void ObserveRearPressure(RaceDataContext context, RaceGapMeasurement gap)
        {
            var state = context.GameState;
            var s = state.SessionData;
            var now = context.NowUtc;
            if (pressureCall?.DeliveryStartedUtc > DateTime.MinValue)
            {
                reportedPressureRival = pendingPressureRival;
                reportedPressureAt = pressureCall.DeliveryStartedUtc;
                pressureCall = null;
            }
            var rival = RaceDataTools.Neighbour(state, false);
            // P3 (Rob 16.11.): Duelle schon in Runde 1 – nach dem Startgetümmel (ab 40 % der ersten Runde), mit am selben Punkt gemessenem Abstand.
            float firstLapLength = s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength;
            bool afterStartScrum = s.CompletedLaps >= 1 || firstLapLength > 500 && state.PositionAndMotionData.DistanceRoundTrack >= firstLapLength * .4f &&
                state.PositionAndMotionData.DistanceRoundTrack < firstLapLength * .85f; // der Startplatz liegt oft kurz vor der Linie
            if (s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Green || state.PitData.InPitlane ||
                !afterStartScrum || state.FlagData.isLocalYellow || rival == null || rival.InPits ||
                !(gap.Kind == RaceGapKind.SimulatorTiming || gap.Kind == RaceGapKind.InterpolatedTiming) ||
                !(gap.Seconds > .1f && gap.Seconds < 8))
            {
                pressureRival = null;
                return;
            }
            string identity = RaceReader.Identity(s.OpponentKeyBehind, rival);
            if (pressureRival != identity || pressureKind != gap.Kind || now < pressureAt ||
                gap.Seconds > pressureBaseline + .2f || now - pressureAt > TimeSpan.FromSeconds(15))
            {
                pressureRival = identity;
                pressureKind = gap.Kind;
                pressureBaseline = gap.Seconds;
                pressureAt = now;
                return;
            }
            if ((gap.Seconds > 2.2f && pressureBaseline - gap.Seconds < .9f) ||
                gap.Seconds > 2.6f || pressureBaseline - gap.Seconds < (gap.Seconds > 1.2f ? .30f : .45f) ||
                now - pressureAt < TimeSpan.FromSeconds(1.5) ||
                identity == reportedPressureRival && now - reportedPressureAt < TimeSpan.FromSeconds(45) ||
                pending.Any(call => call.Topic == "rear-pressure" && call.IsValid(state, now))) return;

            string name = context.SendDriverNames && rival.CanUseName ? RadioPhraseCatalog.SafeRivalName(rival.DriverRawName) : "";
            var catalog = RadioPhraseCatalog.Current;
            const string group = "race-rear-pressure";
            int variant = pressureVariants.Select(group, catalog.Count(group));
            string interval = (gap.IsEstimated ? RadioLanguage.T("etwa ", "about ", "unos ") : "") + RadioLanguage.Num(gap.Seconds, "0.0") + RadioLanguage.T(" Sekunden", " seconds", " segundos");
            string text = catalog.Format(group, variant, name.Length > 0 ? name : RadioLanguage.T("Der Hintermann", "The car behind", "El coche de detrás"), interval);
            // Rennverständnis (Rob 21.11.): Kennen wir ihn schon aus einem Duell, sagt Ben es dazu.
            string story = name.Length > 0 ? RaceStory.Approaching(s.OpponentKeyBehind, now, storyVariant) : "";
            if (story.Length > 0) { text += " " + story; storyVariant++; }
            Add("rear-pressure", text, state, now, 10, 4, "same_class_rival_interval_shrinking_into_attack_range");
            var warning = pending.Last();
            warning.FieldNamesAllowed = name.Length != 0;
            warning.BindVariant(pressureVariants, group, variant);
            warning.AdditionalValidity = live => !live.FlagData.isLocalYellow &&
                RaceDataTools.Neighbour(live, false) is OpponentData current && !current.InPits && !current.isEnteringPits() &&
                RaceReader.Identity(live.SessionData.OpponentKeyBehind, current) == identity;
            RaceGapMeasurement ReadCurrent(RaceDataContext ctx) =>
                RaceDataTools.ReadGap(ctx, RaceDataTools.Neighbour(ctx.GameState, false), false);
            bool HasThreat(RaceGapMeasurement fresh) => fresh.Kind == gap.Kind && fresh.Seconds > .05f && fresh.Seconds <= 3f;
            warning.TimedValidity = (live, at) => HasThreat(ReadCurrent(new RaceDataContext { GameState=live, NowUtc=at,
                Tracks=context.Tracks, LastTelemetryUtc=live.SourceTelemetryUtc != DateTime.MinValue ? live.SourceTelemetryUtc :
                    context.Tracks != null ? live.Now : context.LastTelemetryUtc }));
            warning.RefreshGapS = ctx => ReadCurrent(ctx).Seconds;
            warning.RefreshText = ctx => {
                if (!warning.AdditionalValidity(ctx.GameState)) return null;
                var fresh = ReadCurrent(ctx);
                if (!HasThreat(fresh)) return null;
                string duration = (fresh.IsEstimated ? "etwa " : "") + GermanRadioText.Interval(fresh.Seconds);
                return catalog.Format(group, variant, name.Length > 0 ? name : "Der Hintermann", duration);
            };
            pressureCall = warning;
            pendingPressureRival = identity;
        }

        internal bool RearPressureActive(GameStateData state, DateTime now)
        {
            if (pressureCall == null && reportedPressureRival == null) return false;
            var rival = RaceDataTools.Neighbour(state, false);
            if (rival == null) return false;
            string identity = RaceReader.Identity(state.SessionData.OpponentKeyBehind, rival);
            return pendingPressureRival == identity && pressureCall != null && pressureCall.IsValid(state, now) ||
                identity == pressureRival && identity == reportedPressureRival;
        }

        public ObservedRaceCall Take(GameStateData gs, DateTime now, bool overlap, bool busy, bool minimal,
            int summarySeconds, IReadOnlyList<LapRecord> laps, string driverName, int positionCooldownSeconds = 3, int quietSeconds = 12)
        {
            RememberSpokenPitGain();
            foreach (var delivered in pending.Where(c => c.DeliveryStartedUtc != DateTime.MinValue).ToArray())
            {
                if (delivered.DeliveryStartedUtc > lastCall) lastCall = delivered.DeliveryStartedUtc;
                pending.Remove(delivered);
            }
            foreach (var expired in pending.Where(c => !c.IsValid(gs, now)).ToArray())
            {
                LiveDiagnostics.Decision(expired.Topic, "suppressed", now > expired.ExpiresUtc ? "expired" :
                    expired.DeliveryFailed ? "delivery_attempts_exhausted" :
                    expired.Position > 0 && gs?.SessionData.ClassPosition != expired.Position ? "position_changed" : "race_facts_changed",
                    expired.Evidence, expired.EventId);
                pending.Remove(expired);
            }
            if (minimal) pending.RemoveAll(c=>c.Priority<6);
            if (positionCooldownSeconds == 0) pending.RemoveAll(c => c.Topic == "position" || c.Topic == "position-loss");
            if (overlap || busy) return null;
            var next = pending.Where(c => c.CanReserve(now)).OrderByDescending(c => c.Priority).FirstOrDefault();
            if ((next==null || next.Priority<6 && next.Topic!="formation") &&
                now-lastCall<TimeSpan.FromSeconds(Math.Max(1,positionCooldownSeconds))) return null;
            if (next == null && pending.Count == 0 && summarySeconds > 0 && now >= nextSummary && now - lastCall >= TimeSpan.FromSeconds(quietSeconds) &&
                gs != null && gs.SessionData.SessionType == SessionType.Race && gs.SessionData.SessionPhase == SessionPhase.Green &&
                gs.SessionData.CompletedLaps > 0 && !gs.PitData.InPitlane && gs.SessionData.ClassPosition > 0)
            {
                Add("summary", Summary(gs, laps, startPosition, driverName), gs, now, 6, 1, "current_position_start_position_clean_lap_window");
                next = pending.Last(); nextSummary = now.AddSeconds(summarySeconds);
            }
            if (next == null || !next.TryReserve(now)) return null;
            LiveDiagnostics.Decision(next.Topic, "selected", next.Evidence, next.Text, next.EventId);
            return next;
        }

        /// <summary>Lebensdauer einer eigenen Platzmeldung (gebunden an den gemeldeten Platz).</summary>
        internal const int PositionCallSeconds = 15;

        /// <summary>Lebensdauer des Startfazits: lang genug, um das Getümmel der ersten Runde abzuwarten.</summary>
        internal const int StartVerdictSeconds = 30;

        private void Add(string topic, string text, GameStateData gs, DateTime now, int seconds, int priority, string evidence)
        {
            if (pending.Count >= 8) pending.RemoveAt(0);
            pending.Add(new ObservedRaceCall { Topic = topic, Text = text, Evidence = evidence, Position = gs.SessionData.ClassPosition,
                SessionId = gs.SessionData.SessionId, DetectedUtc = now, ExpiresUtc = now.AddSeconds(seconds), Priority = priority,
                // Diese Ansagen nennen den aktuellen Klassenplatz („jetzt Platz 5“, „von 7 auf 9“, „guter Start … Platz 14“).
                StatedPosition = topic == "position" || topic == "position-loss" || topic == "start-verdict" ? gs.SessionData.ClassPosition : 0 });
            LiveDiagnostics.Decision(topic, "detected", evidence, text, pending.Last().EventId);
        }

        /// <summary>Gleiche Klasse, direkt hinter uns, in Fahrt und auf der Strecke nah – Hinweis auf ein echtes Überholmanöver.</summary>
        /// <summary>
        /// Der Gegner, der Rob gerade auf der Strecke überholt hat: vorher direkt hinter Rob, jetzt direkt vor ihm, derselbe Wagen,
        /// dieselbe Klasse, beide in Fahrt und nicht in der Box. Mehrdeutig oder unbelegt: null.
        /// </summary>
        private string Passer(GameStateData gs, int oldOwn, int newOwn)
        {
            string found = null;
            foreach (var pair in gs.OpponentData)
            {
                var rival = pair.Value;
                if (rival == null || !previousCars.TryGetValue(pair.Key, out var before) || before.Identity != RaceReader.Identity(pair.Key, rival) ||
                    before.Position != newOwn || rival.ClassPosition != oldOwn || before.Pit || rival.InPits || !before.Active || !rival.IsActive ||
                    before.Speed <= 10 || rival.Speed <= 10 || gs.PositionAndMotionData.CarSpeed <= 10 ||
                    rival.CarClass == null || gs.carClass == null || rival.CarClass.getClassIdentifier() != gs.carClass.getClassIdentifier()) continue;
                if (found != null) return null;
                found = pair.Key;
            }
            return found;
        }

        internal static bool LikelyPassedCarBehind(GameStateData gs, int ownPosition)
        {
            var s = gs.SessionData;
            float length = s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength;
            if (length < 100 || gs.PositionAndMotionData.CarSpeed <= 10 || gs.carClass == null) return false;
            OpponentData behind = null;
            foreach (var o in gs.OpponentData.Values)
            {
                if (o == null || !o.IsActive || o.ClassPosition != ownPosition + 1 || o.CarClass == null ||
                    o.CarClass.getClassIdentifier() != gs.carClass.getClassIdentifier()) continue;
                if (behind != null) return false;
                behind = o;
            }
            if (behind == null || behind.InPits || behind.Speed <= 10) return false;
            float delta = (s.CompletedLaps - behind.CompletedLaps) * length + gs.PositionAndMotionData.DistanceRoundTrack - behind.DistanceRoundTrack;
            return delta >= 0 && delta < 120;
        }

        private int lastAnnouncedPosition, positionVariant, lossCount;
        private string lossDriverName;

        private static string Address(string driverName)
        {
            string n = AiPersonas.CleanForRadio(driverName ?? "", 30, false);
            return n.Length == 0 ? "" : n;
        }

        /// <summary>Platz gewonnen ohne bestätigtes Überholmanöver – neun Varianten (Rob 17.11.: lebendiger), teils mit Namen.</summary>
        internal static string MovedUp(SessionData s, int position, int variant, string driverName = null)
        {
            string rank = RaceRadioLanguage.Rank(s, position), n = Address(driverName);
            switch (((variant % 9) + 9) % 9)
            {
                case 5: return RadioLanguage.T("Plus eins! " + rank + ".", "Plus one! " + rank + ".", "¡Uno más! " + rank + ".");
                case 6: return n.Length > 0 ? RadioLanguage.T("Gut gemacht, " + n + ", wir liegen jetzt auf " + rank + ".", "Good job, " + n + ", we're " + rank + " now.", "Bien hecho, " + n + ", ahora vamos " + rank + ".")
                    : RadioLanguage.T("Gut gemacht, wir liegen jetzt auf " + rank + ".", "Good job, we're " + rank + " now.", "Bien hecho, ahora vamos " + rank + ".");
                case 7: return RadioLanguage.T(rank + ". Und der Nächste wartet schon.", rank + ". And the next one is waiting.", rank + ". Y el siguiente ya espera.");
                case 8: return RadioLanguage.T("Wieder einer weniger vor dir. " + rank + ".", "One less in front of you. " + rank + ".", "Uno menos delante. " + rank + ".");
                case 0: return RadioLanguage.T("Wir sind vorgerückt. Jetzt " + rank + ".", "We've moved up. Now " + rank + ".", "Hemos subido. Ahora " + rank + ".");
                case 1: return RadioLanguage.T("Einen Platz nach vorn, jetzt " + rank + ".", "One place up, now " + rank + ".", "Un puesto más, ahora " + rank + ".");
                case 2: return n.Length > 0 ? RadioLanguage.T("Sauber, " + n + ". " + rank + ".", "Nice, " + n + ". " + rank + ".", "Bien, " + n + ". " + rank + ".")
                    : RadioLanguage.T("Sauber. " + rank + ".", "Nice. " + rank + ".", "Bien. " + rank + ".");
                case 3: return RadioLanguage.T("Es geht vorwärts: " + rank + ".", "Moving forward: " + rank + ".", "Vamos hacia delante: " + rank + ".");
                default: return RadioLanguage.T(rank + ". Weiter so.", rank + ". Keep it going.", rank + ". Sigue así.");
            }
        }

        /// <summary>Neutrale Standmeldung, wenn die Position nicht besser ist als die zuletzt gemeldete.</summary>
        internal static string PositionNow(SessionData s, int position, int variant)
        {
            string rank = RaceRadioLanguage.Rank(s, position);
            switch (((variant % 6) + 6) % 6)
            {
                case 3: return RadioLanguage.T("Zur Info: " + rank + ".", "For info: " + rank + ".", "Para que lo sepas: " + rank + ".");
                case 4: return RadioLanguage.T("Aktuell " + rank + ".", "Currently " + rank + ".", "Actualmente " + rank + ".");
                case 5: return RadioLanguage.T("Kurzer Stand: " + rank + ".", "Quick update: " + rank + ".", "Situación rápida: " + rank + ".");
                case 0: return RadioLanguage.T("Stand jetzt: " + rank + ".", "Right now: " + rank + ".", "Ahora mismo: " + rank + ".");
                case 1: return RadioLanguage.T("Du liegst auf " + rank + ".", "You're running " + rank + ".", "Vas " + rank + ".");
                default: return RadioLanguage.T(rank + " im Moment.", rank + " at the moment.", rank + " por ahora.");
            }
        }

        /// <summary>Platz verloren – ruhig, mit Blick nach vorn, fünf Varianten.</summary>
        internal static string PositionLost(SessionData s, int position, int variant, string driverName = null)
        {
            string rank = RaceRadioLanguage.Rank(s, position), n = Address(driverName);
            switch (((variant % 9) + 9) % 9)
            {
                case 5: return RadioLanguage.T(rank + ". Nicht hinterherhetzen, die Chance kommt wieder.", rank + ". Don't chase it, the chance will come again.", rank + ". No te precipites, la ocasión volverá.");
                case 6: return n.Length > 0 ? RadioLanguage.T("Abhaken, " + n + ". " + rank + ", wir bleiben ruhig.", "Let it go, " + n + ". " + rank + ", we stay calm.", "Olvídalo, " + n + ". " + rank + ", con calma.")
                    : RadioLanguage.T("Abhaken. " + rank + ", wir bleiben ruhig.", "Let it go. " + rank + ", we stay calm.", "Olvídalo. " + rank + ", con calma.");
                case 7: return RadioLanguage.T("Ein Platz weg, " + rank + ". Saubere Runden, dann kommt das zurück.", "One place gone, " + rank + ". Clean laps and it'll come back.", "Un puesto menos, " + rank + ". Vueltas limpias y volverá.");
                case 8: return RadioLanguage.T(rank + ". Durchatmen, neu sortieren, weiter geht's.", rank + ". Breathe, reset, keep going.", rank + ". Respira, reorganiza y adelante.");
                case 0: return RadioLanguage.T("Jetzt " + rank + ". Bleib dran.", "Now " + rank + ". Stay with it.", "Ahora " + rank + ". Sigue ahí.");
                case 1: return RadioLanguage.T(rank + ". Kopf hoch, den holen wir uns zurück.", rank + ". Head up, we'll get that back.", rank + ". Ánimo, lo recuperamos.");
                case 2: return n.Length > 0 ? RadioLanguage.T("Platz verloren, " + n + ", jetzt " + rank + ". Ruhig weiter.", "Lost a place, " + n + ", now " + rank + ". Stay calm.", "Perdimos un puesto, " + n + ", ahora " + rank + ". Con calma.")
                    : RadioLanguage.T("Platz verloren, jetzt " + rank + ". Ruhig weiter.", "Lost a place, now " + rank + ". Stay calm.", "Perdimos un puesto, ahora " + rank + ". Con calma.");
                case 3: return RadioLanguage.T(rank + " jetzt. Konzentrier dich auf die nächste Kurve.", rank + " now. Focus on the next corner.", rank + " ahora. Concéntrate en la próxima curva.");
                default: return RadioLanguage.T("Wir sind auf " + rank + " zurückgefallen. Das Rennen ist noch lang.", "We've dropped to " + rank + ". Plenty of race left.", "Hemos caído a " + rank + ". Queda carrera.");
            }
        }

        internal static string StartVerdict(SessionData s, int start, string name, int variant = 0)
        {
            int pos = s.ClassPosition, gained = start - pos;
            string rank = RaceRadioLanguage.Rank(s, pos), n = SafeName(name);
            int v = ((variant % 5) + 5) % 5;
            if (v == 3)
            {
                if (gained > 0) return RadioLanguage.T("Sehr gut, " + n + "! " + (gained == 1 ? "Einer" : gained.ToString()) + " mehr nach dem Start, " + rank + ". Jetzt Ruhe reinbringen.",
                    "Very good, " + n + "! " + gained + " up after the start, " + rank + ". Now settle in.",
                    "¡Muy bien, " + n + "! " + gained + " más tras la salida, " + rank + ". Ahora tranquilidad.");
                if (gained < 0) return RadioLanguage.T(n + ", der Start war nicht ideal, " + rank + ". Wir haben noch viele Runden.",
                    n + ", the start wasn't ideal, " + rank + ". We've got plenty of laps.", n + ", la salida no fue ideal, " + rank + ". Quedan muchas vueltas.");
                return RadioLanguage.T(n + ", gut durch die erste Runde gekommen, " + rank + ".",
                    n + ", good first lap, " + rank + ".", n + ", buena primera vuelta, " + rank + ".");
            }
            if (v == 4)
            {
                if (gained > 0) return RadioLanguage.T("Das war ein Start nach Maß, " + n + ". " + rank + ", " + (gained == 1 ? "ein Platz" : gained + " Plätze") + " besser.",
                    "That was a textbook start, " + n + ". " + rank + ", " + gained + (gained == 1 ? " place" : " places") + " better.",
                    "Salida de manual, " + n + ". " + rank + ", " + gained + (gained == 1 ? " puesto" : " puestos") + " mejor.");
                if (gained < 0) return RadioLanguage.T(n + ", " + rank + " nach dem Start. Kopf frei, Schritt für Schritt zurück nach vorn.",
                    n + ", " + rank + " after the start. Clear head, step by step back to the front.", n + ", " + rank + " tras la salida. Cabeza fría, paso a paso hacia delante.");
                return RadioLanguage.T(n + ", sauber durchgekommen, " + rank + " gehalten. Jetzt finden wir unseren Rhythmus.",
                    n + ", through cleanly, holding " + rank + ". Now let's find our rhythm.", n + ", has pasado limpio, mantienes " + rank + ". Ahora a buscar el ritmo.");
            }
            if (v == 1)
            {
                if (gained > 0) return RadioLanguage.T("Starker Start, " + n + ". Plus " + gained + ", du bist jetzt " + rank + ".",
                    "Strong start, " + n + ". Up " + gained + ", you're " + rank + " now.", "Gran salida, " + n + ". " + gained + " más, ahora " + rank + ".");
                if (gained < 0) return RadioLanguage.T("Der Start hat " + (-gained == 1 ? "einen Platz" : -gained + " Plätze") + " gekostet, " + n + ". " + rank + ", wir holen uns das zurück.",
                    "The start cost us " + -gained + (-gained == 1 ? " place" : " places") + ", " + n + ". " + rank + ", we'll get that back.",
                    "La salida nos costó " + -gained + (-gained == 1 ? " puesto" : " puestos") + ", " + n + ". " + rank + ", lo recuperamos.");
                return RadioLanguage.T("Start ohne Verluste, " + n + ". " + rank + ", jetzt in den Rhythmus finden.",
                    "No losses at the start, " + n + ". " + rank + ", now find your rhythm.", "Salida sin pérdidas, " + n + ". " + rank + ", ahora coge el ritmo.");
            }
            if (v == 2)
            {
                if (gained > 0) return RadioLanguage.T("Erste halbe Runde, " + n + ": " + (gained == 1 ? "ein Platz" : gained + " Plätze") + " gewonnen. " + rank + ", sauber weiter.",
                    "Half a lap in, " + n + ": " + gained + (gained == 1 ? " place" : " places") + " gained. " + rank + ", keep it clean.",
                    "Media vuelta, " + n + ": " + gained + (gained == 1 ? " puesto ganado" : " puestos ganados") + ". " + rank + ", limpio.");
                if (gained < 0) return RadioLanguage.T(n + ", nach dem Start " + rank + ". Kein Stress, das Rennen ist lang.",
                    n + ", " + rank + " after the start. No stress, it's a long race.", n + ", " + rank + " tras la salida. Sin agobios, la carrera es larga.");
                return RadioLanguage.T(n + ", Position am Start gehalten, " + rank + ". Gut gemacht.",
                    n + ", held position at the start, " + rank + ". Well done.", n + ", posición mantenida en la salida, " + rank + ". Bien hecho.");
            }
            if (gained > 0) return SafeName(name) + RadioLanguage.T(", guter Start! " + (gained == 1 ? "Einen Platz" : gained + " Plätze") + " gewonnen, jetzt " + rank + ".",
                ", good start! Gained " + gained + (gained == 1 ? " place" : " places") + ", now " + rank + ".",
                ", ¡buena salida! " + (gained == 1 ? "Una posición ganada" : gained + " posiciones ganadas") + ", ahora " + rank + ".");
            if (gained < 0) return SafeName(name) + RadioLanguage.T(", am Start " + (-gained == 1 ? "einen Platz" : -gained + " Plätze") + " verloren, jetzt " + rank + ". Ruhig bleiben, das Rennen ist noch lang.",
                ", lost " + -gained + (-gained == 1 ? " place" : " places") + " at the start, now " + rank + ". Stay calm, it's a long race.",
                ", " + (-gained == 1 ? "una posición perdida" : -gained + " posiciones perdidas") + " en la salida, ahora " + rank + ". Tranquilo, la carrera es larga.");
            return SafeName(name) + RadioLanguage.T(", sauberer Start, " + rank + " gehalten.", ", clean start, holding " + rank + ".", ", salida limpia, mantienes " + rank + ".");
        }

        /// <summary>Ordnet den Platz im Feld ein, statt ihn nur zu nennen.</summary>
        internal static string Assess(SessionData s)
        {
            int pos = s.ClassPosition;
            int cars = RaceRadioLanguage.SeparateClass(s) ? s.NumCarsInPlayerClass : Math.Max(s.NumCarsOverall, s.NumCarsInPlayerClass);
            if (pos == 1) return RadioVariants.Pick("assess-lead",
                RadioLanguage.T("Du führst. ", "You're leading. ", "Vas líder. "),
                RadioLanguage.T("Du führst, vorne ist frei. ", "You're leading, clear road ahead. ", "Vas líder, pista libre delante. "),
                RadioLanguage.T("Du führst das Feld an. ", "You're leading the field. ", "Encabezas el grupo. "),
                RadioLanguage.T("Du führst, alles in deiner Hand. ", "You're leading, it's all in your hands. ", "Vas líder, todo en tus manos. "));
            if (pos <= 3) return RadioVariants.Pick("assess-podium",
                RadioLanguage.T("Das ist ein Podiumsplatz. ", "That's a podium place. ", "Es puesto de podio. "),
                RadioLanguage.T("Damit stehst du auf dem Podium. ", "That puts you on the podium. ", "Con eso estás en el podio. "),
                RadioLanguage.T("Podiumsplatz, festhalten. ", "Podium place, hold on to it. ", "Puesto de podio, a mantenerlo. "),
                RadioLanguage.T("Wir sind auf Podiumskurs. ", "We're on course for the podium. ", "Vamos camino del podio. "));
            if (cars < 6) return "";
            if (pos <= Math.Max(4, cars / 3)) return RadioVariants.Pick("assess-front",
                RadioLanguage.T("Damit bist du im vorderen Feld. ", "That's the front part of the field. ", "Estás en la parte delantera. "),
                RadioLanguage.T("Das ist vorderes Feld, gute Lage. ", "That's up near the front, good spot. ", "Es la parte delantera, buena posición. "),
                RadioLanguage.T("Wir sind im vorderen Feld unterwegs. ", "We're running near the front. ", "Rodamos en la zona delantera. "),
                RadioLanguage.T("Vorderes Feld, da wollen wir bleiben. ", "Front part of the field, let's stay there. ", "Zona delantera, ahí queremos quedarnos. "));
            if (pos <= cars * 2 / 3) return RadioVariants.Pick("assess-mid",
                RadioLanguage.T("Das ist Mittelfeld, da geht noch was. ", "That's midfield, there's more to come. ", "Es zona media, aún hay margen. "),
                RadioLanguage.T("Mittelfeld, nach vorn ist Luft. ", "Midfield, there's room to move up. ", "Zona media, hay margen para subir. "),
                RadioLanguage.T("Wir stecken im Mittelfeld, Geduld bringt Plätze. ", "We're in the midfield, patience brings places. ", "Estamos en zona media, la paciencia da puestos. "),
                RadioLanguage.T("Mittelfeld, hier wird viel gekämpft, bleib sauber. ", "Midfield, lots of fighting here, stay clean. ", "Zona media, aquí se pelea mucho, limpio. "));
            return RadioVariants.Pick("assess-back",
                RadioLanguage.T("Wir sind im hinteren Feld, Platz für Platz nach vorn. ", "We're towards the back, one place at a time. ", "Vamos atrás, posición a posición hacia delante. "),
                RadioLanguage.T("Hinteres Feld, wir arbeiten uns nach vorn. ", "Towards the back, we'll work our way up. ", "Zona trasera, vamos escalando. "),
                RadioLanguage.T("Noch hinten, aber das Rennen ist nicht vorbei. ", "Still at the back, but the race isn't over. ", "Aún atrás, pero la carrera no ha terminado. "),
                RadioLanguage.T("Hinteres Feld, jeder saubere Überholer zählt. ", "Towards the back, every clean pass counts. ", "Zona trasera, cada adelantamiento limpio cuenta. "));
        }

        public static string Summary(GameStateData gs, IReadOnlyList<LapRecord> laps, int start, string name)
        {
            int pos = gs.SessionData.ClassPosition;
            if (pos <= 0) return null;
            bool separate = RaceRadioLanguage.SeparateClass(gs.SessionData);
            string text = separate ? SafeName(name) + ", " + RaceRadioLanguage.RaceRanks(gs.SessionData) + ". " :
                SafeName(name) + RadioLanguage.T(", wir liegen auf Position " + pos + ". ", ", we're running P" + pos + ". ", ", vamos P" + pos + ". ");
            text += Assess(gs.SessionData);
            int gained = start - pos, lost = pos - start;
            if (start > pos) text += RadioLanguage.T(
                "Seit dem Start " + gained + (gained == 1 ? " Platz" : " Plätze") + (separate ? " in deiner Klasse gewonnen. " : " gewonnen. "),
                "We've gained " + gained + (gained == 1 ? " place" : " places") + " since the start" + (separate ? " in your class. " : ". "),
                "Hemos ganado " + gained + (gained == 1 ? " posición" : " posiciones") + " desde la salida" + (separate ? " en tu clase. " : ". "));
            else if (start > 0 && start < pos) text += RadioLanguage.T(lost + " Plätze hinter unserer Startposition. ",
                lost + (lost == 1 ? " place" : " places") + " behind our starting position. ",
                lost + (lost == 1 ? " posición" : " posiciones") + " por detrás de nuestra posición de salida. ");
            var recent = (laps ?? new LapRecord[0]).Reverse().Take(3).Reverse().ToArray();
            if (recent.Length == 3 && recent.All(l => l.Valid && !l.InPitLap && l.LapTime > 20 && l.LapTime < 1200) &&
                recent[1].LapNumber == recent[0].LapNumber + 1 && recent[2].LapNumber == recent[1].LapNumber + 1)
            {
                float spread = recent.Max(l => l.LapTime) - recent.Min(l => l.LapTime);
                if (spread <= Math.Max(0.6f, recent.Average(l => l.LapTime) * 0.01f))
                    text += RadioLanguage.T("Die letzten drei sauberen Runden liegen innerhalb von ", "Last three clean laps within ", "Las últimas tres vueltas limpias están dentro de ") +
                        RadioLanguage.Num(spread, "0.0") + RadioVariants.Pick("summary-consistent",
                        RadioLanguage.T(" Sekunden. Weiter so.", " seconds. Keep it up.", " segundos. Sigue así."),
                        RadioLanguage.T(" Sekunden. Sehr konstant.", " seconds. Very consistent.", " segundos. Muy constante."),
                        RadioLanguage.T(" Sekunden. Genau so weiter.", " seconds. Exactly like that.", " segundos. Justo así."),
                        RadioLanguage.T(" Sekunden. Schöner Rhythmus.", " seconds. Nice rhythm.", " segundos. Buen ritmo."));
                else text += RadioVariants.Pick("summary-inconsistent",
                    RadioLanguage.T("Die letzten Rundenzeiten schwanken noch.", "Lap times are still inconsistent.", "Los tiempos por vuelta todavía varían."),
                    RadioLanguage.T("Die Rundenzeiten sind noch unruhig.", "The lap times are still up and down.", "Los tiempos aún suben y bajan."),
                    RadioLanguage.T("Bei den Zeiten fehlt noch die Konstanz.", "Consistency is still missing in the times.", "Todavía falta constancia en los tiempos."),
                    RadioLanguage.T("Die Zeiten springen noch, Rhythmus suchen.", "The times are still jumping, find a rhythm.", "Los tiempos aún saltan, busca el ritmo."));
            }
            // No claim about a plan, tyre advantage, race finish or corner attack without supporting measurements.
            return text.Trim();
        }

        private static string SafeName(string name)
        {
            string clean = new string((name ?? "Rob").Where(c => char.IsLetter(c) || c == ' ' || c == '-').Take(30).ToArray()).Trim();
            return clean.Length == 0 ? "Rob" : clean;
        }
    }
}
