using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Feedback from full, consecutive racing laps. Partial laps after joining or leaving the pits do not count.</summary>
    internal sealed class RaceFeedbackWatch
    {
        private readonly Queue<float> times = new Queue<float>(4);
        private readonly RadioVariantCursor stintVariants = new RadioVariantCursor(), defenseVariants = new RadioVariantCursor();
        private int lastBoundary = -1, defenseLaps, defensePosition;
        private bool clean, stintReported, inPit, pressureSeen, defenseReported;
        private string defenseIdentity;
        private DateTime lastStintReport, lastSample;
        private ObservedRaceCall stint, defense, pendingReview;
        private readonly RadioVariantCursor fuelVariants = new RadioVariantCursor();
        private sealed class FuelSavingPlan
        {
            public readonly int Session, Lap;
            public readonly float Target;
            public FuelSavingPlan(int session, int lap, float target)
            { Session = session; Lap = lap; Target = target; }
        }
        private FuelSavingPlan deliveredFuelPlan;
        private volatile FuelSavingPlan fuelPlan;
        private float firstFuelSample = -1;
        private readonly RadioVariantCursor chaseVariants = new RadioVariantCursor();

        private sealed class ChasePlan
        {
            public readonly int Session, Lap;
            public readonly string Identity;
            public readonly RaceGapMeasurement Baseline;
            public ChasePlan(int session, int lap, string identity, RaceGapMeasurement baseline)
            { Session = session; Lap = lap; Identity = identity; Baseline = baseline; }
        }
        private ChasePlan deliveredChasePlan;
        private volatile ChasePlan chasePlan;

        internal bool ChaseIsActive(GameStateData state)
        {
            var plan = chasePlan;
            return plan != null && state != null && IsGreen(state) && plan.Session == state.SessionData.SessionId;
        }
        private int chaseFullLaps;

        internal void RememberDeliveredChasePlan(RaceDataContext context, string identity)
        {
            var state = context?.GameState;
            if (state == null || !IsGreen(state)) return;
            var rival = RaceDataTools.Neighbour(state, true);
            if (rival == null || rival.InPits || rival.isEnteringPits() ||
                RaceReader.Identity(state.SessionData.OpponentKeyInFront, rival) != identity) return;
            var gap = RaceDataTools.ReadGap(context, rival, true);
            if (!MeasuredGap(gap.Kind, gap.Seconds) || gap.Seconds > 4f) return;
            Interlocked.Exchange(ref deliveredChasePlan,
                new ChasePlan(state.SessionData.SessionId, state.SessionData.CompletedLaps, identity, gap));
        }

        private static bool MeasuredGap(RaceGapKind kind, float seconds) =>
            (kind == RaceGapKind.SimulatorTiming || kind == RaceGapKind.InterpolatedTiming) && seconds >= .1f && seconds <= 30f;

        internal float? ActiveFuelTarget(GameStateData state)
        {
            var plan = fuelPlan;
            return plan != null && state != null && IsGreen(state) && state.FuelData.FuelUseActive &&
                state.SessionData.SessionId == plan.Session ? plan.Target : (float?)null;
        }

        internal void RememberDeliveredFuelPlan(GameStateData state, float target)
        {
            if (state == null || !IsGreen(state) || !state.FuelData.FuelUseActive ||
                !TrackAwareness.InRange(target, .01f, 250).HasValue) return;
            Interlocked.Exchange(ref deliveredFuelPlan, new FuelSavingPlan(state.SessionData.SessionId, state.SessionData.CompletedLaps, target));
        }

        public void Reset()
        {
            times.Clear(); lastBoundary = -1;
            clean = stintReported = inPit = pressureSeen = defenseReported = false;
            defenseIdentity = null; defenseLaps = defensePosition = 0;
            lastStintReport = lastSample = DateTime.MinValue;
            stint = defense = pendingReview = null;
            Interlocked.Exchange(ref deliveredFuelPlan, null);
            Interlocked.Exchange(ref deliveredChasePlan, null);
            chasePlan = null;
            chaseFullLaps = 0;
            fuelPlan = null;
            firstFuelSample = -1;
        }

        private static bool IsGreen(GameStateData gs) => gs.SessionData.SessionType == SessionType.Race &&
            gs.SessionData.SessionPhase == SessionPhase.Green && !gs.FlagData.isLocalYellow &&
            !gs.PitData.InPitlane && !gs.PitData.IsInGarage && !gs.PitData.OnOutLap && !gs.PitData.OnInLap &&
            !gs.PenaltiesData.IsOffRacingSurface;

        private static bool Close(RaceGapMeasurement gap) =>
            (gap.Kind == RaceGapKind.SimulatorTiming || gap.Kind == RaceGapKind.InterpolatedTiming) &&
            gap.Seconds > .05f && gap.Seconds <= 1f;

        public ObservedRaceCall Observe(RaceDataContext ctx, GameStateData previous, LapRecord lap,
            RaceGapMeasurement rearGap, bool rearPressure, string driver)
        {
            var gs = ctx.GameState;
            var s = gs.SessionData;
            var now = ctx.NowUtc;
            if (lastSample != DateTime.MinValue && (now < lastSample || now - lastSample > TimeSpan.FromSeconds(2)))
            {
                clean = false;
                times.Clear();
                defenseLaps = 0;
                defenseIdentity = null;
                pressureSeen = false;
            }
            lastSample = now;
            if (stint?.DeliveryStartedUtc > DateTime.MinValue)
            {
                lastStintReport = stint.DeliveryStartedUtc;
                stintReported = true;
                stint = null;
            }
            if (defense?.DeliveryStartedUtc > DateTime.MinValue)
            {
                defenseReported = true;
                defense = null;
            }
            if ((gs.PitData.InPitlane || gs.PitData.IsInGarage) && !inPit)
            {
                stintReported = false;
                times.Clear();
            }
            inPit = gs.PitData.InPitlane || gs.PitData.IsInGarage;
            bool currentClean = IsGreen(gs) && s.CurrentLapIsValid && ctx.TelemetryAgeSeconds >= 0 &&
                ctx.TelemetryAgeSeconds <= 1 && previous != null && previous.SessionData.SessionId == s.SessionId &&
                previous.PenaltiesData.CutTrackWarnings >= 0 &&
                previous.PenaltiesData.CutTrackWarnings == gs.PenaltiesData.CutTrackWarnings;
            if (!currentClean) { clean = false; times.Clear(); }
            var newPlan = Interlocked.Exchange(ref deliveredFuelPlan, null);
            if (newPlan != null)
            {
                fuelPlan = newPlan;
                firstFuelSample = -1;
            }
            if (fuelPlan != null && (fuelPlan.Session != s.SessionId || s.CompletedLaps < fuelPlan.Lap ||
                s.CompletedLaps > fuelPlan.Lap + 4 || inPit || !gs.FuelData.FuelUseActive))
                fuelPlan = null;

            var newChasePlan = Interlocked.Exchange(ref deliveredChasePlan, null);
            if (newChasePlan != null && chasePlan == null)
            {
                chasePlan = newChasePlan;
                chaseFullLaps = 0;
            }
            if (chasePlan != null)
            {
                string frontKey = s.OpponentKeyInFront;
                if (chasePlan.Session != s.SessionId || s.CompletedLaps < chasePlan.Lap ||
                    s.CompletedLaps > chasePlan.Lap + 4 || inPit || frontKey == null ||
                    !gs.OpponentData.TryGetValue(frontKey, out var front) || front == null || !front.IsActive ||
                    front.InPits || RaceReader.Identity(frontKey, front) != chasePlan.Identity)
                    chasePlan = null;
            }

            var rival = RaceDataTools.Neighbour(gs, false);
            string identity = currentClean && rival != null && !rival.InPits && !rival.isEnteringPits() && Close(rearGap)
                ? RaceReader.Identity(s.OpponentKeyBehind, rival) : null;
            if (identity == null || identity != defenseIdentity || defensePosition != s.ClassPosition)
            {
                defenseLaps = 0; pressureSeen = defenseReported = false;
                defenseIdentity = null;
            }
            if (identity != null && identity == defenseIdentity) pressureSeen |= rearPressure;
            if (!s.IsNewLap || s.CompletedLaps == lastBoundary)
            {
                var deferred = pendingReview;
                pendingReview = null;
                return deferred != null && deferred.IsValid(gs, now) ? deferred : null;
            }

            bool complete = clean && lastBoundary >= 0 && s.CompletedLaps == lastBoundary + 1 &&
                lap != null && lap.LapNumber == s.CompletedLaps && lap.Valid && !lap.InPitLap &&
                TrackAwareness.InRange(lap.LapTime, 10, 3600).HasValue;
            clean = currentClean;
            lastBoundary = s.CompletedLaps;
            if (complete)
            {
                if (times.Count == 4) times.Dequeue();
                times.Enqueue(lap.LapTime);
                if (identity != null && identity == defenseIdentity) defenseLaps++;
            }
            else { times.Clear(); defenseLaps = 0; }
            defenseIdentity = identity;
            defensePosition = s.ClassPosition;
            pressureSeen |= identity != null && rearPressure;
            string name = RadioPhraseCatalog.SafeRivalName(driver);
            string address = name.Length == 0 ? "" : ", " + name;
            var catalog = RadioPhraseCatalog.Current;
            var followUp = CheckFuelPlan(gs, now, lap, complete && currentClean, catalog);
            var chaseReview = CheckChasePlan(ctx, lap, complete && currentClean, catalog);
            if (followUp != null && chaseReview != null) pendingReview = chaseReview;
            if (followUp != null || chaseReview != null) return followUp ?? chaseReview;

            if (defenseLaps >= 3 && pressureSeen && !defenseReported && (defense == null || !defense.IsValid(gs, now)))
            {
                string opponent = ctx.SendDriverNames && rival.CanUseName ? RadioPhraseCatalog.SafeRivalName(rival.DriverRawName) : "";
                string group = "race-defense-hold";
                int index = defenseVariants.Select(group, catalog.Count(group));
                string text = catalog.Format(group, index, opponent.Length == 0 ? RadioLanguage.T("Der Hintermann", "The car behind", "El coche de detrás") : opponent,
                    GermanRadioText.Interval(rearGap.Seconds), address);
                int position = s.ClassPosition;
                defense = Create(gs, now, group, text, TeamMember.Ben, "three_clean_laps_same_nearby_class_rival");
                defense.FieldNamesAllowed = opponent.Length != 0;
                defense.BindVariant(defenseVariants, group, index);
                defense.AdditionalValidity = live => IsGreen(live) && live.SessionData.ClassPosition == position &&
                    RaceDataTools.Neighbour(live, false) is OpponentData current && !current.InPits &&
                    RaceReader.Identity(live.SessionData.OpponentKeyBehind, current) == identity;
                defense.TimedValidity = (live, at) => {
                    var gap = RaceDataTools.ReadGap(new RaceDataContext { GameState=live, NowUtc=at, Tracks=ctx.Tracks,
                        LastTelemetryUtc=live.SourceTelemetryUtc == DateTime.MinValue ? live.Now : live.SourceTelemetryUtc },
                        RaceDataTools.Neighbour(live, false), false);
                    return Close(gap) && Math.Abs(gap.Seconds - rearGap.Seconds) <= .35f;
                };
                return defense;
            }

            if (times.Count != 4 || stintReported || stint != null && stint.IsValid(gs, now) ||
                now - lastStintReport < TimeSpan.FromMinutes(12)) return null;
            double mean = times.Average(value => (double)value);
            double spread = Math.Sqrt(times.Average(value => (value - mean) * (value - mean)));
            if (spread >= .18) return null;
            const string stintGroup = "stint-consistency";
            int variant = stintVariants.Select(stintGroup, catalog.Count(stintGroup));
            stint = Create(gs, now, stintGroup, catalog.Format(stintGroup, variant, LocalAnswers.LapTime((float)mean), address),
                TeamMember.Mik, "four_consecutive_green_laps_spread_below_0.18s");
            stint.BindVariant(stintVariants, stintGroup, variant);
            int cuts = gs.PenaltiesData.CutTrackWarnings;
            stint.AdditionalValidity = live => IsGreen(live) && live.SessionData.CurrentLapIsValid &&
                live.PenaltiesData.CutTrackWarnings == cuts;
            return stint;
        }

        private ObservedRaceCall CheckFuelPlan(GameStateData state, DateTime now, LapRecord lap, bool complete, RadioPhraseCatalog catalog)
        {
            int elapsed = state.SessionData.CompletedLaps - (fuelPlan?.Lap ?? state.SessionData.CompletedLaps);
            // The lap on which the advice was heard was only partially driven with the target.
            if (fuelPlan == null || elapsed < 2) return null;
            if (!complete || !TrackAwareness.InRange(lap.FuelUsed, .01f, 250).HasValue)
            {
                firstFuelSample = -1;
                return null;
            }
            if (firstFuelSample < 0)
            {
                firstFuelSample = lap.FuelUsed;
                return null;
            }
            float target = fuelPlan.Target;
            float average = (firstFuelSample + lap.FuelUsed) / 2;
            fuelPlan = null;
            firstFuelSample = -1;
            const string group = "fuel-saving-review";
            int variant = fuelVariants.Select(group, catalog.Count(group));
            string comparison = average <= target + .005f
                ? RadioLanguage.T("Sparziel erreicht.", "Fuel target met.", "Objetivo de ahorro cumplido.")
                : RadioLanguage.T("Das ist noch über dem Sparziel.", "That is still above our fuel target.", "Seguimos por encima del objetivo de ahorro.");
            string text = catalog.Format(group, variant, RadioLanguage.Num(average, "0.00"), comparison);
            var fuelFeedback = Create(state, now, group, text, TeamMember.Ben, "two_complete_green_laps_after_delivered_fuel_advice");
            fuelFeedback.BindVariant(fuelVariants, group, variant);
            fuelFeedback.AdditionalValidity = IsGreen;
            return fuelFeedback;
        }

        private ObservedRaceCall CheckChasePlan(RaceDataContext context, LapRecord lap, bool complete, RadioPhraseCatalog catalog)
        {
            var plan = chasePlan;
            var state = context.GameState;
            if (plan == null || state.SessionData.CompletedLaps - plan.Lap < 2) return null;
            string key = state.SessionData.OpponentKeyInFront;
            var rival = RaceDataTools.Neighbour(state, true);
            if (rival == null || rival.InPits || rival.isEnteringPits() || RaceReader.Identity(key, rival) != plan.Identity)
            {
                chasePlan = null;
                return null;
            }
            if (!complete || lap.FrontIdentity != plan.Identity || lap.GapAheadKind != plan.Baseline.Kind ||
                !MeasuredGap(lap.GapAheadKind, lap.GapAhead))
            {
                chaseFullLaps = 0;
                return null;
            }
            if (++chaseFullLaps < 2) return null;
            chasePlan = null;
            float gain = plan.Baseline.Seconds - lap.GapAhead;
            string name = context.SendDriverNames && rival.CanUseName ? RadioPhraseCatalog.SafeRivalName(rival.DriverRawName) : "";
            string who = name.Length == 0 ? RadioLanguage.T("den Vordermann", "the car ahead", "el coche de delante") : name;
            string result = gain >= .35f ?
                RadioLanguage.T("Auf " + who + " hast du " + GermanRadioText.Interval(gain) + " aufgeholt.",
                    "You gained " + GermanRadioText.Interval(gain) + " on " + who + ".",
                    "Has recortado " + GermanRadioText.Interval(gain) + " a " + who + ".") :
                RadioLanguage.T("Der Abstand nach vorn liegt bei " + GermanRadioText.Interval(lap.GapAhead) + ".",
                    "The gap ahead is " + GermanRadioText.Interval(lap.GapAhead) + ".",
                    "La diferencia por delante es de " + GermanRadioText.Interval(lap.GapAhead) + ".");
            const string group = "race-chase-review";
            int variant = chaseVariants.Select(group, catalog.Count(group));
            var call = Create(state, context.NowUtc, group, catalog.Format(group, variant, result),
                TeamMember.Ben,"two_complete_green_laps_after_delivered_chase_call");
            call.FieldNamesAllowed = name.Length > 0 && gain >= .35f;
            call.BindVariant(chaseVariants, group, variant);
            call.AdditionalValidity = live => IsGreen(live) && live.SessionData.OpponentKeyInFront == key &&
                RaceDataTools.Neighbour(live, true) is OpponentData current && !current.InPits &&
                RaceReader.Identity(key, current) == plan.Identity;
            call.TimedValidity = (live, now) => {
                var fresh = RaceDataTools.ReadGap(new RaceDataContext { GameState=live, NowUtc=now, Tracks=context.Tracks,
                    LastTelemetryUtc=live.SourceTelemetryUtc == DateTime.MinValue ? live.Now : live.SourceTelemetryUtc },
                    RaceDataTools.Neighbour(live, true), true);
                return fresh.Kind == lap.GapAheadKind && Math.Abs(fresh.Seconds - lap.GapAhead) <= .35f;
            };
            return call;
        }

        private static ObservedRaceCall Create(GameStateData state, DateTime now, string topic, string text, TeamMember member, string evidence)
        {
            LiveDiagnostics.Decision(topic, "measured", evidence);
            return new ObservedRaceCall { Topic=topic, Text=text, Evidence=evidence, Position=-1, Priority=3, Member=member,
                SessionId=state.SessionData.SessionId, SessionType=state.SessionData.SessionType,
                CompletedLaps=state.SessionData.CompletedLaps,
                DetectedUtc=now, ExpiresUtc=now.AddSeconds(12) };
        }
    }
}
