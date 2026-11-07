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
        public readonly int CarId, FormerRank;
        public readonly DateTime EnteredUtc;

        public PitGainEvidence(string key, OpponentData rival, int formerRank, DateTime entered)
        {
            Key = key; Name = rival.DriverRawName; Number = rival.CarNumber; CarId = rival.CostId;
            FormerRank = formerRank; EnteredUtc = entered;
        }

        public bool Matches(NativeFieldFact fact) => fact != null && fact.Kind == "pit" && fact.Key == Key &&
            fact.Name == Name && fact.CarNumber == Number && fact.CarId == CarId && Math.Abs((fact.Utc - EnteredUtc).TotalSeconds) <= 3;

        public bool StillInPits(GameStateData gs) => gs != null && gs.OpponentData.TryGetValue(Key, out var rival) &&
            rival != null && rival.IsActive && rival.InPits && rival.DriverRawName == Name && rival.CarNumber == Number && rival.CostId == CarId &&
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
        public readonly string EventId = Guid.NewGuid().ToString("N");
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
        internal string ReferenceTemplate;
        internal PitGainEvidence PitGain;
        internal bool FieldNamesAllowed;
        internal bool IsReserved => Volatile.Read(ref deliveryState) == 1;
        internal bool MaySpeakPitGain => PitGain != null && Volatile.Read(ref attempts) < 2;
        internal bool PitGainWasSpoken => DeliveryStartedUtc != DateTime.MinValue && Volatile.Read(ref pitGainSpoken) != 0;
        private int pitGainSpoken;

        public string ComposeForDelivery(RaceDataContext context, out Func<GameStateData, DateTime, bool> detailIsCurrent,
            bool standalonePitClaimed = false, LocalTemplateReference reference = null)
        {
            detailIsCurrent = null;
            if (PitGain != null) ReferenceTemplate = null;
            if (MaySpeakPitGain && !standalonePitClaimed && context != null && PitGain.StillInPits(context.GameState))
            {
                string pitRivalName = context.SendDriverNames ? RadioPhraseCatalog.SafeRivalName(PitGain.Name) : "";
                string position = Position.ToString(CultureInfo.InvariantCulture) + (RaceRadioLanguage.SeparateClass(context.GameState.SessionData) ? " in deiner Klasse" : "");
                detailIsCurrent = (live, now) => PitGain.StillInPits(live);
                string local = reference?.AuditedPitGain(pitRivalName, position, VariantCursor.Heard);
                if (local != null) ReferenceTemplate = "Fred/position_gained_pit";
                return local ?? RadioPhraseCatalog.Current.Format("pit-gain", VariantCursor.Heard,
                    pitRivalName.Length == 0 ? "Der Vordermann" : pitRivalName, position);
            }
            // If the optional detail expires during rendering, the ordinary retry can still report the pass.
            if (!ConfirmedPass || Volatile.Read(ref attempts) != 1 || context == null || !context.SendDriverNames)
                return Text;

            var state = context.GameState;
            var rival = RaceDataTools.Neighbour(state, true);
            if (!CloseRaceRival(context, rival)) return Text;

            string key = state.SessionData.OpponentKeyInFront;
            string name = rival.DriverRawName;
            string number = rival.CarNumber;
            string spokenName = new string((name ?? "").Where(c => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'').Take(60).ToArray()).Trim();
            if (spokenName.Length == 0) return Text;

            var tracks = context.Tracks;
            detailIsCurrent = (live, now) => {
                if (live == null || live.SessionData.OpponentKeyInFront != key) return false;
                var front = RaceDataTools.Neighbour(live, true);
                return front != null && front.DriverRawName == name && front.CarNumber == number &&
                    CloseRaceRival(new RaceDataContext { GameState = live, NowUtc = now,
                        LastTelemetryUtc = live.Now, Tracks = tracks }, front);
            };
            return Text + (RaceRadioLanguage.SeparateClass(state.SessionData) ? " Du bist in deiner Klasse knapp hinter " : " Du bist knapp hinter ") +
                spokenName + ". Weiter so.";
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
        private string aheadKey,behindKey,aheadNumber,behindNumber;
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
        public DateTime DetectedUtc, ExpiresUtc;
        private int deliveryState, attempts;
        private long playbackStartedTicks, retryNotBeforeTicks,playbackEndedTicks;
        private int playbackInterrupted;
        public bool PlaybackInterrupted {get{return Volatile.Read(ref playbackInterrupted)!=0;}}
        public DateTime PlaybackEndedUtc {get{long t=Interlocked.Read(ref playbackEndedTicks);return t>0?new DateTime(t,DateTimeKind.Utc):DateTime.MinValue;}}
        public void MarkPlaybackFinished(DateTime now,bool interrupted)
        {if(interrupted)Interlocked.Exchange(ref playbackInterrupted,1);Interlocked.Exchange(ref playbackEndedTicks,now.Ticks);}
        public const int MaximumDeliveryAttempts = 2;
        public DateTime DeliveryStartedUtc
        {
            get { long ticks = Interlocked.Read(ref playbackStartedTicks); return Volatile.Read(ref deliveryState) == 2 && ticks > 0 ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.MinValue; }
        }
        public bool DeliveryFailed { get { return Volatile.Read(ref deliveryState) == 3; } }
        public bool CanReserve(DateTime now)
        {
            return Volatile.Read(ref deliveryState) == 0 && Volatile.Read(ref attempts) < MaximumDeliveryAttempts &&
                now.Ticks >= Interlocked.Read(ref retryNotBeforeTicks) && now >= DetectedUtc && now <= ExpiresUtc;
        }
        public bool TryReserve(DateTime now)
        {
            if (!CanReserve(now) || Interlocked.CompareExchange(ref deliveryState, 1, 0) != 0) return false;
            Interlocked.Increment(ref attempts);
            LiveDiagnostics.Decision(Topic, "reserved", "awaiting_playback", Evidence);
            if(LiveDiagnostics.Enabled)LiveDiagnostics.Emit("race.event_reserved",LiveDiagnostics.Data("topic",Topic,"text",Text,"reason",Evidence),EventId);
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
                if (PitGain == null || spokePitGain) VariantCursor?.OnPlaybackStarted();
                AdditionalVariantCursor?.OnPlaybackStarted();
                LiveDiagnostics.Decision(Topic, "delivered", "playback_started", Evidence);
                if(LiveDiagnostics.Enabled)LiveDiagnostics.Emit("race.event_delivered",LiveDiagnostics.Data("topic",Topic,"reason",Evidence),EventId);
                if (ReferenceTemplate != null) LiveDiagnostics.Decision(Topic, "template_played", ReferenceTemplate);
            }
        }
        public void ReleaseForRetry(DateTime now, string reason = "delivery_failed")
        {
            bool terminal = now > ExpiresUtc || Volatile.Read(ref attempts) >= MaximumDeliveryAttempts;
            Interlocked.Exchange(ref retryNotBeforeTicks, now.AddSeconds(1).Ticks);
            if (Interlocked.CompareExchange(ref deliveryState, terminal ? 3 : 0, 1) == 1)
                LiveDiagnostics.Decision(Topic, terminal ? "suppressed" : "deferred", reason, Evidence);
        }
        public bool IsValid(GameStateData gs, DateTime now)
        {
            if (gs == null || DeliveryFailed || now < DetectedUtc || now > ExpiresUtc) return false;
            var s = gs.SessionData;
            bool weather = AllowPitlane && (Topic ?? "").StartsWith("weather-", StringComparison.Ordinal);
            bool pit = AllowPitlane && (Topic == "pit-limit" || Topic == "pit-speed");
            bool greeting = AllowPitlane && Topic == "greeting" && SessionGreetingDirector.SafeToGreet(
                s.SessionType, s.SessionPhase, gs.PitData.InPitlane, gs.PositionAndMotionData.CarSpeed);
            if (Topic == "greeting" && !greeting) return false;
            // Formation support is only valid during a confirmed formation, never after green.
            bool formation = Topic == "formation" && s.SessionType == SessionType.Race && s.SessionPhase == SessionPhase.Formation;
            if (Topic == "formation" && !formation) return false;
            bool qualifyingResult = Topic == "qualifying-result" && s.SessionType == SessionType.Qualify && AllowLapFinish &&
                (s.SessionPhase == SessionPhase.Checkered || s.SessionPhase == SessionPhase.Finished);
            bool liveWarning=(Topic??"").StartsWith("warning-",StringComparison.Ordinal) &&
                (s.SessionPhase==SessionPhase.Green || s.SessionPhase==SessionPhase.FullCourseYellow);
            bool phaseValid = liveWarning || qualifyingResult || formation || greeting || (weather || pit) && (s.SessionPhase == SessionPhase.Garage || s.SessionPhase == SessionPhase.Gridwalk || s.SessionPhase == SessionPhase.Countdown || s.SessionPhase == SessionPhase.FullCourseYellow || s.SessionPhase == SessionPhase.Formation) || s.SessionPhase == SessionPhase.Green || AllowLapFinish &&
                SessionLapObserver.Supported(s.SessionType) && (Topic == "own-lap" || Topic == "near-best") &&
                (s.SessionPhase == SessionPhase.Checkered || s.SessionPhase == SessionPhase.Finished);
            return s.SessionId == SessionId && s.SessionType == SessionType && phaseValid &&
                (!gs.PitData.InPitlane || qualifyingResult || weather || pit || greeting) && (Position < 0 || s.ClassPosition == Position) && (AdditionalValidity == null || AdditionalValidity(gs)) && (TimedValidity == null || TimedValidity(gs,now)) &&
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
                    opponent.CarNumber==(ahead?aheadNumber:behindNumber)) &&
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
                aheadKey=key;aheadNumber=op.CarNumber;aheadKind=RaceDataTools.ReadGap(ctx,op,true).Kind;aheadIdentityBound=true; }
            else { BehindName = op.DriverRawName; BehindGap = RaceDataTools.ReadGap(ctx, op, false).Seconds;
                behindKey=key;behindNumber=op.CarNumber;behindKind=RaceDataTools.ReadGap(ctx,op,false).Kind;behindIdentityBound=true; }
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
            public int Position, Laps, CarId;
            public bool Pit, Active;
            public string Name, Number;
            public float Distance, Speed;
        }
        private readonly Dictionary<string, Car> previousCars = new Dictionary<string, Car>();
        private readonly List<ObservedRaceCall> pending = new List<ObservedRaceCall>();
        private int stablePosition, candidatePosition, candidateSamples, session = int.MinValue, lastEndLap = -1;
        private DateTime candidateSince, lastTick, lastCall = DateTime.MinValue, nextSummary;
        private bool candidatePass, candidatePit, previousInPits;
        private RadioVariantCursor passVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor firstPassVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor pitGainVariants = new RadioVariantCursor();
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
        private bool wasGreen;
        private int startPosition;
        private int lastCutWarnings, lastCompletedLaps;
        private bool lastLapValid;
        private DateTime lastConductIssue = DateTime.MinValue;
        public int PendingCount { get { return pending.Count; } }
        public void Enqueue(ObservedRaceCall call)
        {
            if(pending.Count>=8) pending.RemoveAt(0);
            pending.Add(call); LiveDiagnostics.Decision(call.Topic,"detected",call.Evidence,call.Text);
        }

        public void Reset()
        {
            previousCars.Clear(); pending.Clear(); stablePosition = candidatePosition = candidateSamples = startPosition = 0;
            passVariants = new RadioVariantCursor();
            previousFrontKey = null;
            recentFrontPit = spokenPitGain = null;
            spokenPitGainUtc = DateTime.MinValue;
            lastCutWarnings = lastCompletedLaps = 0; lastLapValid = false; lastConductIssue = DateTime.MinValue;
            lastEndLap = -1; session = int.MinValue; lastTick = lastCall = DateTime.MinValue; wasGreen = false; previousInPits = false;
        }

        public void Observe(GameStateData gs, DateTime now, string driverName, int intervalSeconds)
        {
            if (gs == null) return;
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
                entering.DriverRawName == oldFront.Name && entering.CarNumber == oldFront.Number && entering.CostId == oldFront.CarId &&
                gs.carClass != null && entering.CarClass != null && CarData.IsCarClassEqual(gs.carClass, entering.CarClass, false))
                recentFrontPit = new PitGainEvidence(previousFrontKey, entering, oldFront.Position, now);
            if (!green || gs.PitData.InPitlane || previousInPits) recentFrontPit = null;
            if (startPosition == 0 && s.SessionStartClassPosition > 0) startPosition = s.SessionStartClassPosition;
            if (!green || gs.PitData.InPitlane || s.ClassPosition <= 0 || previousInPits)
            {
                stablePosition = s.ClassPosition; candidatePosition = 0;
                // Keep a deferred formation call across formation samples so it can retry delivery.
                pending.RemoveAll(c => c.Topic != "formation" || s.SessionPhase != SessionPhase.Formation || s.SessionType != SessionType.Race || gs.PitData.InPitlane);
            }
            else if (stablePosition <= 0) stablePosition = s.ClassPosition;
            else if (s.ClassPosition == stablePosition) candidatePosition = 0;
            else
            {
                if (candidatePosition != s.ClassPosition)
                {
                    candidatePosition = s.ClassPosition; candidateSince = now; candidateSamples = 1;
                    candidatePass = false; candidatePit = false;
                    if (candidatePosition == stablePosition - 1 && wasGreen)
                    {
                        foreach (var pair in gs.OpponentData)
                        {
                            Car before;
                            var rival = pair.Value;
                            if (!previousCars.TryGetValue(pair.Key, out before) || before.Position != candidatePosition || rival.ClassPosition != stablePosition ||
                                rival.CarClass == null || gs.carClass == null || rival.CarClass.getClassIdentifier() != gs.carClass.getClassIdentifier()) continue;
                            candidatePit |= before.Pit || rival.InPits;
                            float delta = (s.CompletedLaps - rival.CompletedLaps) * (s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength) +
                                gs.PositionAndMotionData.DistanceRoundTrack - rival.DistanceRoundTrack;
                            // Exclude stopped/spinning cars, lap corrections and unmeasured order changes from praise.
                            candidatePass |= !before.Pit && !rival.InPits && before.Active && rival.IsActive &&
                                before.Speed > 10 && rival.Speed > 10 && gs.PositionAndMotionData.CarSpeed > 10 &&
                                s.TrackDefinition != null && s.TrackDefinition.trackLength > 100 && delta >= 0 && delta < 150;
                        }
                    }
                }
                else candidateSamples++;
                if (conductUncertain) candidatePass = false;
                if (gs.OpponentData.Values.Any(o => o.ClassPosition == stablePosition && (o.InPits || !o.IsActive || o.Speed <= 10)))
                    candidatePass = false;
                if (candidateSamples >= 3 && now - candidateSince >= TimeSpan.FromMilliseconds(250))
                {
                    int gain = stablePosition - candidatePosition;
                    pending.RemoveAll(c => c.Topic == "position" || c.Topic == "summary");
                    if (gain > 0)
                    {
                        string text;
                        if (candidatePass && !candidatePit)
                        {
                            text = RaceRadioLanguage.Overtake(passVariants.Heard + 1, candidatePosition, driverName, RaceRadioLanguage.SeparateClass(s), firstPassVariants.Heard);
                        }
                        else text = RaceRadioLanguage.SeparateClass(s) ? "Wir sind vorgerückt. Jetzt " + RaceRadioLanguage.Rank(s, candidatePosition) + "." :
                            "Wir sind vorgerückt. Du liegst jetzt auf Position " + candidatePosition + ".";
                        var pitGain = gain == 1 && !conductUncertain && recentFrontPit != null && recentFrontPit.FormerRank == candidatePosition &&
                            now - recentFrontPit.EnteredUtc <= TimeSpan.FromSeconds(2) && recentFrontPit.StillInPits(gs) &&
                            gs.OpponentData[recentFrontPit.Key].ClassPosition == stablePosition ? recentFrontPit : null;
                        Add("position", text, gs, now, 8, 3,
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
                            // Delayed cut/invalidity flags can arrive after the rank changed, before speech starts.
                            var passCall = pending.Last();
                            passCall.ConfirmedPass = true;
                            passCall.VariantCursor = passVariants;
                            if(passVariants.Heard==0)passCall.AdditionalVariantCursor=firstPassVariants;
                            passCall.AdditionalValidity = live => !live.PenaltiesData.IsOffRacingSurface &&
                                live.PenaltiesData.CutTrackWarnings <= cuts &&
                                !(validLap && live.SessionData.CompletedLaps == lap && !live.SessionData.CurrentLapIsValid);
                        }
                    }
                    else Add("position", "Jetzt " + RaceRadioLanguage.Rank(s, candidatePosition) + ". Bleib dran.", gs, now, 8, 2, "stable_position_loss_cause_unknown");
                    stablePosition = candidatePosition; candidatePosition = 0;
                }
            }
            if (green && wasGreen && !gs.PitData.InPitlane && !s.SessionHasFixedTime && s.SessionNumberOfLaps > 0 &&
                s.SessionLapsRemaining > 0 && s.SessionLapsRemaining <= 3 && s.SessionLapsRemaining != lastEndLap)
            {
                lastEndLap = s.SessionLapsRemaining;
                string text = lastEndLap == 1
                    ? "Letzte Runde, " + SafeName(driverName) + (s.ClassPosition == 1 ? ". Du führst. Sauber nach Hause bringen." : ". Bleib konzentriert.")
                    : "Noch " + lastEndLap + " Runden, " + SafeName(driverName) + (s.ClassPosition == 1 ? ". Du führst. Bleib bei deinem Rhythmus." : ". Bleib bei deinem Rhythmus.");
                Add("race-end", text, gs, now, 12, 4, "fixed_lap_race_remaining_from_adapter");
                pending.Last().LapsRemaining = lastEndLap;
            }
            previousCars.Clear();
            foreach (var pair in gs.OpponentData)
                previousCars[pair.Key] = new Car { Position = pair.Value.ClassPosition, Laps = pair.Value.CompletedLaps,
                    Pit = pair.Value.InPits, Active = pair.Value.IsActive, Distance = pair.Value.DistanceRoundTrack, Speed = pair.Value.Speed,
                    Name = pair.Value.DriverRawName, Number = pair.Value.CarNumber, CarId = pair.Value.CostId };
            previousFrontKey = s.OpponentKeyInFront;
            previousInPits = gs.PitData.InPitlane; wasGreen = green;
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
                LiveDiagnostics.Decision(expired.Topic, "suppressed", "expired_or_state_changed", expired.Evidence);
                pending.Remove(expired);
            }
            if (minimal) pending.RemoveAll(c=>c.Priority<6);
            if (positionCooldownSeconds == 0) pending.RemoveAll(c => c.Topic == "position");
            if (overlap || busy) return null;
            var next = pending.Where(c => c.CanReserve(now)).OrderByDescending(c => c.Priority).FirstOrDefault();
            if ((next==null || next.Priority<6) && now-lastCall<TimeSpan.FromSeconds(Math.Max(1,positionCooldownSeconds)))return null;
            if (next == null && pending.Count == 0 && summarySeconds > 0 && now >= nextSummary && now - lastCall >= TimeSpan.FromSeconds(quietSeconds) &&
                gs != null && gs.SessionData.SessionType == SessionType.Race && gs.SessionData.SessionPhase == SessionPhase.Green &&
                gs.SessionData.CompletedLaps > 0 && !gs.PitData.InPitlane && gs.SessionData.ClassPosition > 0)
            {
                Add("summary", Summary(gs, laps, startPosition, driverName), gs, now, 6, 1, "current_position_start_position_clean_lap_window");
                next = pending.Last(); nextSummary = now.AddSeconds(summarySeconds);
            }
            if (next == null || !next.TryReserve(now)) return null;
            LiveDiagnostics.Decision(next.Topic, "selected", next.Evidence, next.Text);
            return next;
        }

        private void Add(string topic, string text, GameStateData gs, DateTime now, int seconds, int priority, string evidence)
        {
            if (pending.Count >= 8) pending.RemoveAt(0);
            pending.Add(new ObservedRaceCall { Topic = topic, Text = text, Evidence = evidence, Position = gs.SessionData.ClassPosition,
                SessionId = gs.SessionData.SessionId, DetectedUtc = now, ExpiresUtc = now.AddSeconds(seconds), Priority = priority });
            LiveDiagnostics.Decision(topic, "detected", evidence, text);
        }

        public static string Summary(GameStateData gs, IReadOnlyList<LapRecord> laps, int start, string name)
        {
            int pos = gs.SessionData.ClassPosition;
            if (pos <= 0) return null;
            bool separate = RaceRadioLanguage.SeparateClass(gs.SessionData);
            string text = separate ? SafeName(name) + ", " + RaceRadioLanguage.RaceRanks(gs.SessionData) + ". " :
                SafeName(name) + ", wir liegen auf Position " + pos + ". ";
            if (start > pos) text += "Seit dem Start " + (start - pos) + ((start - pos) == 1 ? " Platz" : " Plätze") + (separate ? " in deiner Klasse gewonnen. " : " gewonnen. ");
            else if (start > 0 && start < pos) text += (pos - start) + " Plätze hinter unserer Startposition. ";
            var recent = (laps ?? new LapRecord[0]).Reverse().Take(3).Reverse().ToArray();
            if (recent.Length == 3 && recent.All(l => l.Valid && !l.InPitLap && l.LapTime > 20 && l.LapTime < 1200) &&
                recent[1].LapNumber == recent[0].LapNumber + 1 && recent[2].LapNumber == recent[1].LapNumber + 1)
            {
                float spread = recent.Max(l => l.LapTime) - recent.Min(l => l.LapTime);
                if (spread <= Math.Max(0.6f, recent.Average(l => l.LapTime) * 0.01f))
                    text += "Die letzten drei sauberen Runden liegen innerhalb von " + spread.ToString("0.0", CultureInfo.GetCultureInfo("de-DE")) + " Sekunden. Weiter so.";
                else text += "Die letzten Rundenzeiten schwanken noch.";
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
