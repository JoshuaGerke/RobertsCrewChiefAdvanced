using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Art eines Eintrags in der Rennchronik.</summary>
    internal enum ChronicleKind
    {
        Grid, PositionGained, PositionLost, OwnPit, RivalPit, OwnLap, OwnBest, FieldBest, LeaderChange,
        Yellow, Hazard, Contact, OwnMistake, Penalty, PenaltyServed, Retired, Left, LastLap, Finish,
        RaceControl, TrackLimit, Incident
    }

    /// <summary>Ein belegtes Ereignis: wann, welche Runde, wer, was – und woher die Crew es weiß.</summary>
    internal sealed class ChronicleEntry
    {
        public DateTime Utc;
        public int Session, Lap;
        public ChronicleKind Kind;
        /// <summary>Gegner (sprechbarer Name, nur wenn erlaubt) und sein Schlüssel im Spielstand; null bei eigenen Ereignissen.</summary>
        public string Subject, SubjectKey;
        /// <summary>Robs Klassenplatz nach dem Ereignis; 0 unbekannt.</summary>
        public int Position;
        /// <summary>Platz davor (Positionswechsel, Start); 0 unbekannt.</summary>
        public int PositionBefore;
        /// <summary>Zahlenwert des Ereignisses (Rundenzeit in s, Strafsekunden …); NaN ohne Wert.</summary>
        public double Value = double.NaN;
        public string Detail, Source;
        public DataOrigin Origin;
    }

    /// <summary>
    /// Rennchronik (Masterplan Schritt 1, Rob 21.11.2026): ein gemeinsames Protokoll aller belegten Ereignisse eines Rennens,
    /// für alle Simulationen gleich. Die Beobachter schreiben hinein, was sie sicher wissen; Rennverständnis und Renn-KI
    /// lesen nur von hier. Nichts wird geschätzt oder ergänzt: Fehlt ein Name, bleibt er leer.
    /// Ein Eintrag gilt für eine Sitzung; mit einer neuen Sitzung beginnt eine neue Chronik.
    /// </summary>
    internal sealed class RaceChronicle
    {
        internal const int MaxEntries = 600;
        internal static readonly RaceChronicle Current = new RaceChronicle();

        private readonly object gate = new object();
        private readonly List<ChronicleEntry> entries = new List<ChronicleEntry>();
        private int session = int.MinValue;
        private SessionType type = SessionType.Unavailable;

        internal int Session { get { lock (gate) return session; } }
        internal SessionType Type { get { lock (gate) return type; } }

        internal void Reset()
        {
            lock (gate) { entries.Clear(); session = int.MinValue; type = SessionType.Unavailable; }
        }

        /// <summary>Wechselt bei neuer Sitzung auf eine leere Chronik.</summary>
        internal void Follow(GameStateData gs)
        {
            if (gs == null) return;
            lock (gate)
            {
                if (gs.SessionData.SessionId == session && gs.SessionData.SessionType == type) return;
                entries.Clear(); session = gs.SessionData.SessionId; type = gs.SessionData.SessionType;
            }
        }

        /// <summary>
        /// Trägt ein Ereignis ein. Dasselbe Ereignis (Art und Gegner) innerhalb von <paramref name="dedupe"/> zählt nur einmal,
        /// damit mehrere Beobachter, die dasselbe sehen, keine Doppelungen erzeugen.
        /// </summary>
        internal ChronicleEntry Add(GameStateData gs, DateTime now, ChronicleKind kind, OpponentData subject = null, string subjectKey = null,
            string detail = null, string source = null, DataOrigin origin = DataOrigin.SimulatorReportedEvent, double value = double.NaN,
            int positionBefore = 0, TimeSpan? dedupe = null, string subjectName = null)
        {
            if (gs == null) return null;
            Follow(gs);
            string name = subjectName ?? (subject != null && subject.CanUseName ? RadioPhraseCatalog.SafeRivalName(subject.DriverRawName) : null);
            if (string.IsNullOrWhiteSpace(name)) name = null;
            var entry = new ChronicleEntry
            {
                Utc = now, Session = gs.SessionData.SessionId, Lap = gs.SessionData.CompletedLaps, Kind = kind,
                Subject = name, SubjectKey = subjectKey, Position = gs.SessionData.ClassPosition, PositionBefore = positionBefore,
                Value = value, Detail = detail, Source = source, Origin = origin
            };
            TimeSpan window = dedupe ?? TimeSpan.FromSeconds(5);
            // Gelb und Strafen melden oft zwei Quellen (Spielflagge und Rennleitung) mit eigener Beschreibung: eine Lage, ein Eintrag.
            bool byKind = kind == ChronicleKind.Yellow || kind == ChronicleKind.Penalty || kind == ChronicleKind.PenaltyServed;
            lock (gate)
            {
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    var e = entries[i];
                    if (now - e.Utc > window) break;
                    if (e.Kind != kind) continue;
                    if (byKind)
                    {
                        if (e.Subject == null && name != null) e.Subject = name; // der spätere Hinweis kennt den Betroffenen
                        return null;
                    }
                    if (e.SubjectKey == subjectKey && (subjectKey != null || e.Subject == name) && e.Detail == detail) return null;
                }
                entries.Add(entry);
                if (entries.Count > MaxEntries) entries.RemoveRange(0, entries.Count - MaxEntries);
            }
            LiveDiagnostics.Decision("chronicle-" + kind.ToString().ToLowerInvariant(), "recorded", source ?? "", (name ?? "") +
                (detail == null ? "" : " · " + detail));
            return entry;
        }

        internal List<ChronicleEntry> All() { lock (gate) return entries.ToList(); }

        internal List<ChronicleEntry> Recent(TimeSpan maxAge, DateTime now)
        {
            lock (gate) return entries.Where(e => now >= e.Utc && now - e.Utc <= maxAge).ToList();
        }

        internal List<ChronicleEntry> Of(ChronicleKind kind) { lock (gate) return entries.Where(e => e.Kind == kind).ToList(); }

        /// <summary>Alles, was mit einem Gegner passiert ist (nach Schlüssel), in zeitlicher Folge.</summary>
        internal List<ChronicleEntry> With(string subjectKey)
        {
            if (subjectKey == null) return new List<ChronicleEntry>();
            lock (gate) return entries.Where(e => e.SubjectKey == subjectKey).ToList();
        }

        /// <summary>Wie oft Rob und dieser Gegner auf der Strecke die Plätze getauscht haben.</summary>
        internal int Exchanges(string subjectKey)
        {
            if (subjectKey == null) return 0;
            lock (gate) return entries.Count(e => e.SubjectKey == subjectKey && (e.Kind == ChronicleKind.PositionGained || e.Kind == ChronicleKind.PositionLost) &&
                e.Detail == ChronicleRecorder.OnTrack);
        }

        /// <summary>Für das Sprachmodell: die jüngsten Einträge, neueste zuletzt, mit Runde, Art, Gegner und Quelle.</summary>
        internal JObject ToJson(DateTime now, int max = 40, bool names = true)
        {
            List<ChronicleEntry> list;
            int s; SessionType t;
            lock (gate) { list = entries.Skip(Math.Max(0, entries.Count - max)).ToList(); s = session; t = type; }
            var items = new JArray();
            foreach (var e in list)
            {
                var o = new JObject
                {
                    ["vor_s"] = Math.Round(Math.Max(0, (now - e.Utc).TotalSeconds)),
                    ["runde"] = e.Lap,
                    ["art"] = e.Kind.ToString(),
                };
                if (e.Subject != null && names) o["gegner"] = e.Subject;
                if (e.Position > 0) o["platz_danach"] = e.Position;
                if (e.PositionBefore > 0) o["platz_davor"] = e.PositionBefore;
                if (!double.IsNaN(e.Value)) o["wert"] = Math.Round(e.Value, 3);
                if (e.Detail != null) o["detail"] = e.Detail;
                o["herkunft"] = e.Origin.ToString();
                if (e.Source != null) o["quelle"] = e.Source;
                items.Add(o);
            }
            return new JObject
            {
                ["sitzung"] = t.ToString(),
                ["eintraege_gesamt"] = Count(),
                ["eintraege"] = items,
                ["hinweis"] = "Nur belegte Ereignisse. Fehlt ein Gegnername, war er nicht sicher erlaubt oder unbekannt – nicht ergänzen."
            };
        }

        internal int Count() { lock (gate) return entries.Count; }
    }

    /// <summary>
    /// Schreibt aus dem Spielstand in die Chronik, was jede Simulation gleich liefert: Startplatz, Platzwechsel mit Namen
    /// (Strecke oder Box), eigene Runden und Bestzeiten, eigene Boxenstopps, letzte Runde, Zielankunft. Ereignisse, die nur
    /// einzelne Beobachter oder Simulationen kennen (Gelb, Dreher, Strafen, Ausfälle, Serverabgang, Rennleitung), tragen diese
    /// selbst über <see cref="RaceChronicle.Add"/> ein.
    /// </summary>
    internal sealed class ChronicleRecorder
    {
        internal const string OnTrack = "strecke", ByPit = "box", OwnPitStop = "eigener_stopp";
        /// <summary>So lange muss Robs Klassenplatz stehen, bevor ein Wechsel als belegt gilt.</summary>
        internal static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

        private sealed class Rival { public int Rank; public bool Pit; public string Identity; }

        private readonly RaceChronicle chronicle;
        private int session = int.MinValue;
        private SessionType type = SessionType.Unavailable;
        private int stableRank, candidateRank, lastLap = -1;
        private DateTime candidateSince = DateTime.MinValue;
        private Dictionary<string, Rival> stableField = new Dictionary<string, Rival>();
        private DateTime fieldUtc = DateTime.MinValue;
        private bool gridDone, lastLapDone, finishDone, ownPitSinceStable, wasInPit;
        private float best = -1;

        internal ChronicleRecorder(RaceChronicle chronicle = null) { this.chronicle = chronicle ?? RaceChronicle.Current; }

        private void Reset(GameStateData gs)
        {
            session = gs.SessionData.SessionId; type = gs.SessionData.SessionType;
            stableRank = candidateRank = 0; lastLap = -1; candidateSince = fieldUtc = DateTime.MinValue; stableField.Clear();
            gridDone = lastLapDone = finishDone = ownPitSinceStable = wasInPit = false; best = -1;
            chronicle.Follow(gs);
        }

        private static bool SameClass(GameStateData gs, OpponentData o) =>
            gs.carClass == null || o.CarClass == null || CarData.IsCarClassEqual(gs.carClass, o.CarClass, false);

        private static Dictionary<string, Rival> Field(GameStateData gs)
        {
            var field = new Dictionary<string, Rival>();
            if (gs.OpponentData == null) return field;
            foreach (var pair in gs.OpponentData)
            {
                var o = pair.Value;
                if (o == null || !o.IsActive || o.ClassPosition <= 0 || !SameClass(gs, o)) continue;
                field[pair.Key] = new Rival { Rank = o.ClassPosition, Pit = o.InPits, Identity = RaceReader.Identity(pair.Key, o) };
            }
            return field;
        }

        internal void Observe(GameStateData gs, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session || s.SessionType != type) Reset(gs);
            if (s.SessionType != SessionType.Race) return;
            bool green = s.SessionPhase == SessionPhase.Green || s.SessionPhase == SessionPhase.FullCourseYellow;
            bool inPit = gs.PitData.InPitlane;

            if (!gridDone && (green || s.SessionPhase == SessionPhase.Countdown || s.SessionPhase == SessionPhase.Formation) && s.ClassPosition > 0)
            {
                gridDone = true;
                chronicle.Add(gs, now, ChronicleKind.Grid, detail: s.SessionPhase.ToString(), source: "startaufstellung", origin: DataOrigin.SimulatorReportedEvent);
                stableRank = s.ClassPosition; stableField = Field(gs); fieldUtc = now;
            }

            if (inPit && !wasInPit && green && s.CompletedLaps > 0)
                chronicle.Add(gs, now, ChronicleKind.OwnPit, source: "boxengasse", origin: DataOrigin.SimulatorReportedEvent);
            if (inPit) ownPitSinceStable = true;
            wasInPit = inPit;

            if (s.CompletedLaps != lastLap)
            {
                if (lastLap >= 0 && s.CompletedLaps == lastLap + 1 && s.LapTimePrevious > 0)
                {
                    chronicle.Add(gs, now, ChronicleKind.OwnLap, value: s.LapTimePrevious, detail: s.PreviousLapWasValid ? "gueltig" : "ungueltig",
                        source: "rundenzeit", origin: DataOrigin.SimulatorReportedEvent);
                    if (s.PreviousLapWasValid && s.PlayerLapTimeSessionBest > 0 && (best <= 0 || s.PlayerLapTimeSessionBest < best - .0005f) &&
                        Math.Abs(s.PlayerLapTimeSessionBest - s.LapTimePrevious) < .002f)
                        chronicle.Add(gs, now, ChronicleKind.OwnBest, value: s.LapTimePrevious, source: "persoenliche_bestzeit");
                }
                if (s.PlayerLapTimeSessionBest > 0) best = s.PlayerLapTimeSessionBest;
                lastLap = s.CompletedLaps;
            }

            if (!lastLapDone && green && LapWatch.OnLastLap(s))
            {
                lastLapDone = true;
                chronicle.Add(gs, now, ChronicleKind.LastLap, source: "letzte_runde");
            }
            if (!finishDone && (s.SessionPhase == SessionPhase.Checkered || s.SessionPhase == SessionPhase.Finished) && s.ClassPosition > 0 && gridDone)
            {
                finishDone = true;
                chronicle.Add(gs, now, ChronicleKind.Finish, positionBefore: FirstGrid(), source: "zielankunft");
            }

            if (!gridDone || !green || s.ClassPosition <= 0) return;
            if (s.ClassPosition == stableRank)
            {
                candidateRank = 0;
                // Feldbild höchstens einmal pro Sekunde neu (kein Neuanlegen in jedem Takt); es muss nur vor dem Wechsel stehen.
                if (now < fieldUtc || now - fieldUtc >= TimeSpan.FromSeconds(1)) { stableField = Field(gs); fieldUtc = now; }
                if (!inPit) ownPitSinceStable = false;
                return;
            }
            if (s.ClassPosition != candidateRank) { candidateRank = s.ClassPosition; candidateSince = now; return; }
            if (now - candidateSince < Settle) return;

            // Robs Platz steht: Wer hat mit ihm die Reihenfolge getauscht?
            var field = Field(gs);
            int before = stableRank, after = s.ClassPosition;
            foreach (var pair in stableField)
            {
                if (!field.TryGetValue(pair.Key, out var nowRival) || nowRival.Identity != pair.Value.Identity) continue;
                bool wasAhead = pair.Value.Rank < before, isAhead = nowRival.Rank < after;
                if (wasAhead == isAhead) continue;
                gs.OpponentData.TryGetValue(pair.Key, out var car);
                string how = ownPitSinceStable || inPit ? OwnPitStop : pair.Value.Pit || nowRival.Pit || car != null && car.InPits ? ByPit : OnTrack;
                chronicle.Add(gs, now, wasAhead ? ChronicleKind.PositionGained : ChronicleKind.PositionLost, car, pair.Key, how,
                    "klassenplatz", DataOrigin.SimulatorReportedEvent, positionBefore: before, dedupe: TimeSpan.FromSeconds(1));
            }
            stableRank = after; stableField = field; fieldUtc = now; candidateRank = 0; ownPitSinceStable = inPit;
        }

        private int FirstGrid()
        {
            var grid = chronicle.Of(ChronicleKind.Grid).FirstOrDefault();
            return grid == null ? 0 : grid.Position;
        }
    }
}
