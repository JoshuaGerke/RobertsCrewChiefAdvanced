using System;
using System.Collections.Generic;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Zählt je Gegner, wie oft er in dieser Sitzung außerhalb der Box auf der Strecke stehen geblieben ist
    /// (unter 1 m/s für mindestens 3 s, vorher schon einmal in Fahrt gesehen). Anlass: Rob fragte am 16.11.2026
    /// „Der steht ziemlich oft draußen, der Sergi Reche“ – das Analysewerkzeug kannte nur „in der Box“.
    /// Ohne gemessene Geschwindigkeit (nie in Fahrt gesehen) wird nichts gezählt.
    /// </summary>
    internal static class OpponentStopLog
    {
        private sealed class Entry { public bool SeenMoving, Counted; public float StillSince = -1; public int Stops; public bool StoppedNow; }

        private static readonly object gate = new object();
        private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static int session = int.MinValue;

        internal static string Key(OpponentData op) => (op.DriverRawName ?? "") + "#" + (op.CarNumber ?? "");

        internal static void Reset() { lock (gate) { entries.Clear(); session = int.MinValue; } }

        internal static void Observe(GameStateData gs)
        {
            if (gs?.OpponentData == null) return;
            var s = gs.SessionData;
            float t = s.SessionRunningTime;
            lock (gate)
            {
                if (s.SessionId != session) { entries.Clear(); session = s.SessionId; }
                if (float.IsNaN(t) || float.IsInfinity(t) || t < 0 || s.SessionPhase != SessionPhase.Green && s.SessionPhase != SessionPhase.FullCourseYellow) return;
                foreach (var op in gs.OpponentData.Values)
                {
                    if (op == null || string.IsNullOrEmpty(op.DriverRawName)) continue;
                    string key = Key(op);
                    if (!entries.TryGetValue(key, out var e)) { if (entries.Count > 256) continue; entries[key] = e = new Entry(); }
                    float v = op.Speed;
                    if (!op.IsActive || op.InPits || float.IsNaN(v) || float.IsInfinity(v)) { e.StillSince = -1; e.Counted = e.StoppedNow = false; continue; }
                    if (v >= 10) e.SeenMoving = true;
                    if (v >= 3) { e.StillSince = -1; e.Counted = e.StoppedNow = false; continue; }
                    if (v >= 1 || !e.SeenMoving) continue;
                    if (e.StillSince < 0 || t < e.StillSince) e.StillSince = t;
                    if (t - e.StillSince >= 3)
                    {
                        e.StoppedNow = true;
                        if (!e.Counted) { e.Counted = true; e.Stops++; }
                    }
                }
            }
        }

        /// <summary>Anzahl der Stillstände in dieser Sitzung und ob er gerade steht.</summary>
        internal static int Stops(OpponentData op, out bool stoppedNow)
        {
            stoppedNow = false;
            if (op == null) return 0;
            lock (gate)
            {
                if (!entries.TryGetValue(Key(op), out var e)) return 0;
                stoppedNow = e.StoppedNow;
                return e.Stops;
            }
        }
    }
}
