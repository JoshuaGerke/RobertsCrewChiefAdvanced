using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Bounded, local practice/qualifying observation. No network, audio or disk on the telemetry thread.</summary>
    public sealed class SessionLapObserver
    {
        private readonly List<ObservedRaceCall> pending = new List<ObservedRaceCall>();
        private int session = int.MinValue, lastLap = -1, improvements;
        private SessionType type = SessionType.Unavailable;
        private float personalBest = -1, classBest = -1;
        private DateTime lastRadio = DateTime.MinValue;
        internal const string TimedLapStartPhrase = "Zeit läuft. Die erste gezeitete Runde beginnt, bleib bei deinem Rhythmus.";
        private bool firstTimedLapCalled;

        public void Reset()
        { pending.Clear(); session = int.MinValue; type = SessionType.Unavailable; personalBest = classBest = -1; lastLap = -1; improvements = 0; lastRadio = DateTime.MinValue; firstTimedLapCalled = false; }

        public static bool Supported(SessionType t)
        { return t == SessionType.Practice || t == SessionType.LonePractice || t == SessionType.Qualify || t == SessionType.PrivateQualify || t == SessionType.HotLap; }

        public void Observe(GameStateData gs, LapRecord completed, DateTime now, bool names, GameStateData previous = null)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (!Supported(s.SessionType)) { Reset(); return; }
            if (session != s.SessionId || type != s.SessionType)
            {
                Reset(); session = s.SessionId; type = s.SessionType;
                // Joining an existing session does not turn its historic best into a new event.
                personalBest = s.PlayerLapTimeSessionBest;
                classBest = s.PlayerClassSessionBestLapTime;
                return;
            }
            if (s.SessionPhase != SessionPhase.Green) { pending.Clear(); return; }
            bool ownCall = false;
            var length = s.TrackDefinition == null ? 0 : s.TrackDefinition.trackLength;
            float step = previous == null ? float.NaN : s.SessionRunningTime - previous.SessionData.SessionRunningTime;
            bool continuous = !float.IsNaN(step) && !float.IsInfinity(step) && step > 0 && step <= 1.5f && previous != null &&
                length - previous.PositionAndMotionData.DistanceRoundTrack + gs.PositionAndMotionData.DistanceRoundTrack <= 180 * step + 15;
            if (!firstTimedLapCalled && continuous && previous != null && previous.SessionData.SessionId == s.SessionId &&
                previous.SessionData.SessionType == s.SessionType && previous.PitData.OnOutLap && !gs.PitData.OnOutLap &&
                !gs.PitData.InPitlane && s.IsNewLap && gs.SessionData.CurrentLapIsValid && length > 100 &&
                previous.PositionAndMotionData.DistanceRoundTrack > length * .8f &&
                gs.PositionAndMotionData.DistanceRoundTrack >= 0 && gs.PositionAndMotionData.DistanceRoundTrack < length * .2f)
            {
                firstTimedLapCalled = true;
                Add(gs, now, "timed-lap-start", TimedLapStartPhrase,
                    "mapped_outlap_end_new_lap_and_start_finish_crossing", 3);
            }
            if (completed != null && completed.LapNumber != lastLap)
            {
                lastLap = completed.LapNumber;
                if (completed.Valid && !completed.InPitLap && Good(completed.LapTime))
                {
                    float time = completed.LapTime;
                    bool first = !Good(personalBest), improved = !first && time < personalBest - .01f;
                    if (first || improved)
                    {
                        string lead = first ? "Erste gültige Zeit steht: " : (++improvements % 2 == 1 ? "Sauber, neue persönliche Bestzeit: " : "Wieder verbessert, stark: ");
                        string text = lead + LocalAnswers.LapTime(time) + ".";
                        // The recorded lap remains true when standings or another driver's best changes.
                        // Fresh classification and gaps belong to the separate timing reports.
                        Add(gs, now, "own-lap", text, first ? "first_valid_lap" : "personal_best_improved", 4);
                        ownCall = true;
                    }
                    personalBest = Good(personalBest) ? Math.Min(personalBest, time) : time;
                }
            }
            float nextBest = s.PlayerClassSessionBestLapTime;
            if (Good(nextBest) && Good(classBest) && nextBest < classBest - .01f && !ownCall)
            {
                var rival = gs.OpponentData.Values.FirstOrDefault(o => o.IsActive && gs.carClass != null &&
                    CarData.IsCarClassEqual(gs.carClass, o.CarClass) && Good(o.CurrentBestLapTime) && Math.Abs(o.CurrentBestLapTime - nextBest) < .01f);
                if (rival != null)
                {
                    string name = names ? new string((rival.DriverRawName ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').Take(50).ToArray()) : "";
                    string text = (name.Length > 0 ? name : "Ein Gegner") + " setzt die neue Klassenbestzeit: " + LocalAnswers.LapTime(nextBest) + ".";
                    Add(gs, now, "rival-best", text, "measured_class_best_and_identified_rival", 2);
                }
            }
            if (Good(nextBest)) classBest = nextBest;
        }

        private static bool Good(float t) { return !float.IsNaN(t) && !float.IsInfinity(t) && t > 10 && t < 3600; }
        private void Add(GameStateData gs, DateTime now, string topic, string text, string evidence, int priority)
        {
            pending.RemoveAll(c => c.Topic == topic);
            pending.Add(new ObservedRaceCall { Topic=topic, Text=text, Evidence=evidence, Priority=priority, Member=TeamMember.Mik,
                Position=-1, SessionId=session, SessionType=type, CompletedLaps=gs.SessionData.CompletedLaps,
                DetectedUtc=now, ExpiresUtc=now.AddSeconds(topic == "timed-lap-start" ? 4 : 12) });
            LiveDiagnostics.Decision(topic,"detected",evidence,text);
        }
        public ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy)
        {
            foreach (var delivered in pending.Where(c => c.DeliveryStartedUtc != DateTime.MinValue).ToArray())
            {
                if (delivered.DeliveryStartedUtc > lastRadio) lastRadio = delivered.DeliveryStartedUtc;
                pending.Remove(delivered);
            }
            foreach (var expired in pending.Where(c => !c.IsValid(gs,now)).ToArray())
            {
                LiveDiagnostics.Decision(expired.Topic,"suppressed","expired_or_lap_changed",expired.Evidence);
                pending.Remove(expired);
            }
            if (busy || now-lastRadio < TimeSpan.FromSeconds(8)) return null;
            var call = pending.Where(c => c.CanReserve(now)).OrderByDescending(c => c.Priority).FirstOrDefault();
            return call != null && call.TryReserve(now) ? call : null;
        }
    }
}
