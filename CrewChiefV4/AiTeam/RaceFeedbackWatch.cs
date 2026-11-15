using System;
using System.Collections.Generic;
using System.Linq;
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
        private ObservedRaceCall stint, defense;

        public void Reset()
        {
            times.Clear(); lastBoundary = -1;
            clean = stintReported = inPit = pressureSeen = defenseReported = false;
            defenseIdentity = null; defenseLaps = defensePosition = 0;
            lastStintReport = lastSample = DateTime.MinValue;
            stint = defense = null;
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

            var rival = RaceDataTools.Neighbour(gs, false);
            string identity = currentClean && rival != null && !rival.InPits && !rival.isEnteringPits() && Close(rearGap)
                ? RaceReader.Identity(s.OpponentKeyBehind, rival) : null;
            if (identity == null || identity != defenseIdentity || defensePosition != s.ClassPosition)
            {
                defenseLaps = 0; pressureSeen = defenseReported = false;
                defenseIdentity = null;
            }
            if (identity != null && identity == defenseIdentity) pressureSeen |= rearPressure;
            if (!s.IsNewLap || s.CompletedLaps == lastBoundary) return null;

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

        private static ObservedRaceCall Create(GameStateData state, DateTime now, string topic, string text, TeamMember member, string evidence)
        {
            LiveDiagnostics.Decision(topic, "measured", evidence);
            return new ObservedRaceCall { Topic=topic, Text=text, Evidence=evidence, Position=-1, Priority=3, Member=member,
                SessionId=state.SessionData.SessionId, CompletedLaps=state.SessionData.CompletedLaps,
                DetectedUtc=now, ExpiresUtc=now.AddSeconds(12) };
        }
    }
}
