using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    public sealed class ObservedRaceCall
    {
        public string Topic, Text, Evidence;
        public int Position, SessionId, Priority, LapsRemaining = -1;
        public SessionType SessionType = SessionType.Race;
        public TeamMember Member = TeamMember.Ben;
        public int CompletedLaps = -1;
        public string AheadName, BehindName;
        public float AheadGap = -1, BehindGap = -1;
        public Func<GameStateData,bool> AdditionalValidity;
        public DateTime DetectedUtc, ExpiresUtc;
        public bool IsValid(GameStateData gs, DateTime now)
        {
            if (gs == null || now < DetectedUtc || now > ExpiresUtc) return false;
            var s = gs.SessionData;
            return s.SessionId == SessionId && s.SessionType == SessionType && s.SessionPhase == SessionPhase.Green &&
                !gs.PitData.InPitlane && (Position < 0 || s.ClassPosition == Position) && (AdditionalValidity == null || AdditionalValidity(gs)) &&
                (LapsRemaining < 0 || (!s.SessionHasFixedTime && s.SessionLapsRemaining == LapsRemaining)) &&
                (CompletedLaps < 0 || s.CompletedLaps == CompletedLaps) && GapStillValid(gs, true, AheadName, AheadGap) && GapStillValid(gs, false, BehindName, BehindGap);
        }
        private static bool GapStillValid(GameStateData gs, bool ahead, string name, float oldGap)
        {
            if (oldGap < 0) return true;
            var opponent = RaceDataTools.Neighbour(gs, ahead);
            float gap = RaceDataTools.GapSeconds(gs, opponent, ahead);
            return opponent != null && opponent.DriverRawName == name && gap > 0 && Math.Abs(gap - oldGap) <= .5f;
        }
        public void IncludeNeighbour(RaceDataContext ctx, bool ahead, bool appendText)
        {
            var op = RaceDataTools.Neighbour(ctx.GameState, ahead);
            var text = AiRadioSchedule.Compose(ahead ? "ahead" : "behind", ctx);
            if (op == null || text == null) return;
            if (appendText) Text += " " + text;
            if (ahead) { AheadName = op.DriverRawName; AheadGap = RaceDataTools.GapSeconds(ctx.GameState, op, true); }
            else { BehindName = op.DriverRawName; BehindGap = RaceDataTools.GapSeconds(ctx.GameState, op, false); }
        }
    }

    /// <summary>
    /// Local race observations. Confirmation is 250 ms / at least three samples, independent of LLM cooldowns.
    /// Rank changes alone never prove an overtake. The same active on-track rival must change relative order.
    /// No file, audio or network work is performed here. One telemetry-thread owner; calls are immutable after return.
    /// </summary>
    public sealed class ActiveRaceObserver
    {
        private sealed class Car { public int Position, Laps; public bool Pit, Active; public float Distance, Speed; }
        private readonly Dictionary<string, Car> previousCars = new Dictionary<string, Car>();
        private readonly List<ObservedRaceCall> pending = new List<ObservedRaceCall>();
        private int stablePosition, candidatePosition, candidateSamples, session = int.MinValue, passCount, lastEndLap = -1;
        private DateTime candidateSince, lastTick, lastCall = DateTime.MinValue, nextSummary;
        private bool candidatePass, candidatePit, previousInPits;
        private bool wasGreen;
        private int startPosition;
        public int PendingCount { get { return pending.Count; } }
        public void Enqueue(ObservedRaceCall call)
        {
            if(pending.Count>=8) pending.RemoveAt(0);
            pending.Add(call); LiveDiagnostics.Decision(call.Topic,"detected",call.Evidence,call.Text);
        }

        public void Reset()
        {
            previousCars.Clear(); pending.Clear(); stablePosition = candidatePosition = candidateSamples = passCount = startPosition = 0;
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
            lastTick = now;
            bool green = s.SessionType == SessionType.Race && s.SessionPhase == SessionPhase.Green;
            if (startPosition == 0 && s.SessionStartClassPosition > 0) startPosition = s.SessionStartClassPosition;
            if (!green || gs.PitData.InPitlane || s.ClassPosition <= 0 || previousInPits)
            {
                stablePosition = s.ClassPosition; candidatePosition = 0; pending.Clear();
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
                if (gs.OpponentData.Values.Any(o => o.ClassPosition == stablePosition && (o.InPits || !o.IsActive || o.Speed <= 10)))
                    candidatePass = false;
                if (candidateSamples >= 3 && now - candidateSince >= TimeSpan.FromMilliseconds(250))
                {
                    int gain = stablePosition - candidatePosition;
                    pending.RemoveAll(c => c.Topic == "position" || c.Topic == "summary");
                    if (gain > 0)
                    {
                        string prefix;
                        if (candidatePass && !candidatePit)
                        {
                            passCount++;
                            prefix = passCount % 3 == 1 ? "Sauber vorbeigekommen, " + SafeName(driverName) + ". " :
                                passCount % 3 == 2 ? "Und den nächsten geschnappt. " : "Stark, wieder einer weniger vor dir. ";
                        }
                        else prefix = candidatePit ? "Durch den Boxenstopp vorgerückt. " : "In der Wertung vorgerückt. ";
                        Add("position", prefix + "Du liegst jetzt auf Position " + candidatePosition + ".", gs, now, 8, 3,
                            candidatePass && !candidatePit ? "stable_rank_and_same_active_rival_crossing" : candidatePit ? "rival_pit" : "stable_rank_cause_unknown");
                    }
                    else Add("position", "P" + candidatePosition + " jetzt. Kopf hoch, dranbleiben.", gs, now, 8, 2, "stable_position_loss_cause_unknown");
                    stablePosition = candidatePosition; candidatePosition = 0;
                }
            }
            if (green && wasGreen && !gs.PitData.InPitlane && !s.SessionHasFixedTime && s.SessionNumberOfLaps > 0 &&
                s.SessionLapsRemaining > 0 && s.SessionLapsRemaining <= 3 && s.SessionLapsRemaining != lastEndLap)
            {
                lastEndLap = s.SessionLapsRemaining;
                string text = lastEndLap == 1 ? "Letzte Runde, " + SafeName(driverName) + ". Bleib konzentriert." :
                    "Noch " + lastEndLap + " Runden, " + SafeName(driverName) + ". Bleib bei deinem Rhythmus.";
                Add("race-end", text, gs, now, 12, 4, "fixed_lap_race_remaining_from_adapter");
                pending.Last().LapsRemaining = lastEndLap;
            }
            previousCars.Clear();
            foreach (var pair in gs.OpponentData)
                previousCars[pair.Key] = new Car { Position = pair.Value.ClassPosition, Laps = pair.Value.CompletedLaps,
                    Pit = pair.Value.InPits, Active = pair.Value.IsActive, Distance = pair.Value.DistanceRoundTrack, Speed = pair.Value.Speed };
            previousInPits = gs.PitData.InPitlane; wasGreen = green;
        }

        public ObservedRaceCall Take(GameStateData gs, DateTime now, bool overlap, bool busy, bool minimal,
            int summarySeconds, IReadOnlyList<LapRecord> laps, string driverName, int positionCooldownSeconds = 3, int quietSeconds = 12)
        {
            foreach (var expired in pending.Where(c => !c.IsValid(gs, now)).ToArray())
            {
                LiveDiagnostics.Decision(expired.Topic, "suppressed", "expired_or_state_changed", expired.Evidence);
                pending.Remove(expired);
            }
            if (minimal) pending.RemoveAll(c=>c.Priority<6);
            if (positionCooldownSeconds == 0) pending.RemoveAll(c => c.Topic == "position");
            if (overlap || busy) return null;
            var next = pending.OrderByDescending(c => c.Priority).FirstOrDefault();
            if ((next==null || next.Priority<6) && now-lastCall<TimeSpan.FromSeconds(Math.Max(1,positionCooldownSeconds)))return null;
            if (next == null && summarySeconds > 0 && now >= nextSummary && now - lastCall >= TimeSpan.FromSeconds(quietSeconds) &&
                gs != null && gs.SessionData.SessionType == SessionType.Race && gs.SessionData.SessionPhase == SessionPhase.Green &&
                gs.SessionData.CompletedLaps > 0 && !gs.PitData.InPitlane && gs.SessionData.ClassPosition > 0)
            {
                Add("summary", Summary(gs, laps, startPosition, driverName), gs, now, 6, 1, "current_position_start_position_clean_lap_window");
                next = pending.Last(); nextSummary = now.AddSeconds(summarySeconds);
            }
            if (next == null) return null;
            pending.Remove(next); lastCall = now;
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
            string text = SafeName(name) + ", wir liegen auf Position " + pos + ". ";
            if (start > pos) text += "Seit dem Start " + (start - pos) + ((start - pos) == 1 ? " Platz" : " Plätze") + " gewonnen. ";
            else if (start > 0 && start < pos) text += (pos - start) + " Plätze hinter unserer Startposition. ";
            var recent = (laps ?? new LapRecord[0]).Reverse().Take(3).Reverse().ToArray();
            if (recent.Length == 3 && recent.All(l => l.Valid && !l.InPitLap && l.LapTime > 20 && l.LapTime < 1200) &&
                recent[1].LapNumber == recent[0].LapNumber + 1 && recent[2].LapNumber == recent[1].LapNumber + 1)
            {
                float spread = recent.Max(l => l.LapTime) - recent.Min(l => l.LapTime);
                if (spread <= Math.Max(0.6f, recent.Average(l => l.LapTime) * 0.01f))
                    text += "Die letzten drei sauberen Runden liegen innerhalb von " + spread.ToString("0.0", CultureInfo.GetCultureInfo("de-DE")) + " Sekunden. Weiter so.";
                else text += "Zuletzt schwankende Rundenzeiten. Ich behalte den Verlauf im Blick.";
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
