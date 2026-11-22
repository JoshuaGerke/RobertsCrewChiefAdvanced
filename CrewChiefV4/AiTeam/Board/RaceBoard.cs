using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam.Board
{
    /// <summary>Woher ein Wert der Lagetafel stammt.</summary>
    internal enum BoardSource { None, Native, CrewChief }

    /// <summary>Ein Wert mit Herkunft und Zeitpunkt; ohne Herkunft gilt er als unbekannt.</summary>
    internal struct Fact<T>
    {
        public T Value;
        public BoardSource Source;
        public DateTime Utc;

        internal bool Known => Source != BoardSource.None;
        internal double AgeSeconds(DateTime now) => Known ? Math.Max(0, (now - Utc).TotalSeconds) : double.PositiveInfinity;
        internal bool Fresh(DateTime now, double maxSeconds) => Known && now >= Utc && (now - Utc).TotalSeconds <= maxSeconds;

        internal static Fact<T> From(T value, BoardSource source, DateTime utc) => new Fact<T> { Value = value, Source = source, Utc = utc };
        internal static readonly Fact<T> Unknown = default(Fact<T>);
        public override string ToString() => Known ? Value + "@" + (Source == BoardSource.Native ? "nativ" : "cc") : "?";
    }

    /// <summary>Offene Strafe aus Spiel oder Crew Chief.</summary>
    internal sealed class BoardPenalty
    {
        public bool DriveThrough, StopAndGo, SlowDown, TimeDeduction;
        public int Outstanding;
        /// <summary>RaceRoom: noch zurückzugebende Sekunden bzw. Plätze; -1 = unbekannt.</summary>
        public float SlowDownSeconds = -1;
        public int PositionsToGiveBack = -1;
        internal bool Any => DriveThrough || StopAndGo || SlowDown || TimeDeduction || Outstanding > 0 || PositionsToGiveBack > 0;
        internal string Kind => DriveThrough ? "durchfahrt" : StopAndGo ? "stop_and_go" : SlowDown ? "slowdown" :
            TimeDeduction ? "zeitstrafe" : PositionsToGiveBack > 0 ? "plaetze_zurueck" : Outstanding > 0 ? "offen" : "keine";
    }

    /// <summary>Eine Momentaufnahme der Lagetafel: ein Rennzustand für die ganze Crew.</summary>
    internal sealed class BoardSnapshot
    {
        public DateTime Utc;
        public string Sim;
        public Fact<FieldSession> Session;
        public Fact<FieldPhase> Phase;
        public Fact<int> Place, FieldSize, LapsDone, LapsLeft;
        public Fact<float> TimeLeft, GapFront, GapBehind, Speed;
        public Fact<bool> InPit;
        public Fact<BoardPenalty> Penalty;
        /// <summary>Nächster Wagen vor bzw. hinter Rob auf der Strecke (Sekunden über das Feldtempo, egal welche Klasse oder Runde).</summary>
        public Fact<float> TrackAheadS, TrackBehindS;
        public string TrackAheadName, TrackBehindName;
        /// <summary>Alter der Daten des nächsten Wagens vorn/hinten in ms (wie frisch die Sim ihn zuletzt gemeldet hat).</summary>
        public double TrackAheadAgeMs = -1, TrackBehindAgeMs = -1;
        /// <summary>Widersprüche zwischen nativem Bild und Crew Chief in diesem Bild (Schlüssel → „nativ/cc“).</summary>
        public readonly Dictionary<string, string> Conflicts = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Lagetafel (Masterprompt Kap. 10, Rob 21.11.: „tiefer in den nativen Datenstrom einsteigen und daraus alle unsere Dinge
    /// beziehen … es steht und fällt mit der Aktualität“). Führt das native Bild der Sim (<see cref="FieldView"/>) und Crew Chiefs
    /// Spielzustand zu EINEM Rennzustand zusammen; jeder Wert trägt Herkunft und Zeitpunkt.
    /// Regeln je Wert, aus den Aufnahmen vom 20./21.11. belegt:
    /// - Session und Feldgröße: nativ zuerst. RaceRoom Mantorp: Crew Chief meldete erst „Hotlap“, dann „Qualifying“, das Spiel
    ///   durchgehend „Qualifying“; das Feld (22) meldet nur das Spiel verlässlich.
    /// - Platz: Crew Chief zuerst, nativ als Gegenprobe. RaceRoom: 462 von 472 Bildern gleich; AC: Crew Chief nutzt die
    ///   Echtzeit-Rangliste, die zu den Streckenpositionen passte.
    /// - Strafen: beide Quellen zusammen (RaceRoom liefert Art und Sekunden nativ, die anderen Sims über Crew Chief).
    /// - Abstände, Runden, Restzeit, Tempo: Crew Chief (Zeitmessung), nativ nur, wenn Crew Chief nichts hat.
    /// Ein nativer Wert gilt nur, solange das Bild höchstens <see cref="NativeMaxAge"/> alt ist.
    /// </summary>
    internal static class RaceBoard
    {
        internal const double NativeMaxAge = 1.0;
        private static volatile BoardSnapshot current;

        /// <summary>Die zuletzt gebaute Lagetafel; null vor dem ersten Bild.</summary>
        internal static BoardSnapshot Current => current;

        internal static FieldSession SessionOf(SessionType t)
        {
            switch (t)
            {
                case SessionType.Race: return FieldSession.Race;
                case SessionType.Qualify: case SessionType.PrivateQualify: return FieldSession.Qualify;
                case SessionType.Practice: case SessionType.LonePractice: case SessionType.HotLap: return FieldSession.Practice;
                default: return FieldSession.Unknown;
            }
        }

        internal static FieldPhase PhaseOf(SessionPhase p)
        {
            switch (p)
            {
                case SessionPhase.Garage: return FieldPhase.Garage;
                case SessionPhase.Gridwalk: return FieldPhase.Gridwalk;
                case SessionPhase.Formation: return FieldPhase.Formation;
                case SessionPhase.Countdown: return FieldPhase.Countdown;
                case SessionPhase.Green: case SessionPhase.FullCourseYellow: return FieldPhase.Green;
                case SessionPhase.Checkered: case SessionPhase.Finished: return FieldPhase.Checkered;
                default: return FieldPhase.Unknown;
            }
        }

        /// <summary>Baut die Lagetafel aus beiden Quellen; reine Funktion ohne Zustand.</summary>
        internal static BoardSnapshot Compose(GameStateData gs, FieldView v, DateTime now)
        {
            var b = new BoardSnapshot { Utc = now };
            bool native = v != null && v.Fresh(now, NativeMaxAge) && !v.InReplay;
            DateTime nu = native ? v.Utc : DateTime.MinValue;
            var s = gs?.SessionData;
            b.Sim = native ? v.Sim : null;

            // Session und Phase: nativ zuerst
            if (native && v.Session != FieldSession.Unknown) b.Session = Fact<FieldSession>.From(v.Session, BoardSource.Native, nu);
            else if (s != null && SessionOf(s.SessionType) != FieldSession.Unknown) b.Session = Fact<FieldSession>.From(SessionOf(s.SessionType), BoardSource.CrewChief, now);
            if (native && v.Phase != FieldPhase.Unknown) b.Phase = Fact<FieldPhase>.From(v.Phase, BoardSource.Native, nu);
            else if (s != null && PhaseOf(s.SessionPhase) != FieldPhase.Unknown) b.Phase = Fact<FieldPhase>.From(PhaseOf(s.SessionPhase), BoardSource.CrewChief, now);
            if (native && s != null && v.Session != FieldSession.Unknown && SessionOf(s.SessionType) != FieldSession.Unknown && SessionOf(s.SessionType) != v.Session)
                b.Conflicts["session"] = v.Session + "/" + s.SessionType;

            // Feldgröße: nativ zuerst
            int nativeCars = native ? Math.Max(v.NumCars, v.MaxPlace) : 0;
            if (nativeCars > 0) b.FieldSize = Fact<int>.From(nativeCars, BoardSource.Native, nu);
            else if (s != null && s.NumCarsOverall > 0) b.FieldSize = Fact<int>.From(s.NumCarsOverall, BoardSource.CrewChief, now);

            // Platz: Crew Chief zuerst, nativ als Gegenprobe
            int nativePlace = native && v.Player != null ? v.Player.PlaceClass > 0 ? v.Player.PlaceClass : v.Player.Place : 0;
            if (s != null && s.ClassPosition > 0) b.Place = Fact<int>.From(s.ClassPosition, BoardSource.CrewChief, now);
            else if (nativePlace > 0) b.Place = Fact<int>.From(nativePlace, BoardSource.Native, nu);
            if (s != null && s.ClassPosition > 0 && nativePlace > 0 && s.ClassPosition != nativePlace)
                b.Conflicts["place"] = nativePlace + "/" + s.ClassPosition;

            // Runden, Restzeit, Abstände, Tempo: Crew Chief, nativ als Ersatz
            if (s != null && s.CompletedLaps >= 0) b.LapsDone = Fact<int>.From(s.CompletedLaps, BoardSource.CrewChief, now);
            else if (native && v.Player != null) b.LapsDone = Fact<int>.From(v.Player.CompletedLaps, BoardSource.Native, nu);
            if (native && v.PlayerLapsLeft >= 0) b.LapsLeft = Fact<int>.From(v.PlayerLapsLeft, BoardSource.Native, nu);
            else if (s != null && !s.SessionHasFixedTime && s.SessionLapsRemaining >= 0) b.LapsLeft = Fact<int>.From(s.SessionLapsRemaining, BoardSource.CrewChief, now);
            if (s != null && s.SessionHasFixedTime && s.SessionTimeRemaining >= 0) b.TimeLeft = Fact<float>.From(s.SessionTimeRemaining, BoardSource.CrewChief, now);
            else if (native && v.TimedSession && !float.IsNaN(v.TimeRemaining) && v.TimeRemaining >= 0) b.TimeLeft = Fact<float>.From(v.TimeRemaining, BoardSource.Native, nu);
            if (s != null && s.TimeDeltaFront > 0) b.GapFront = Fact<float>.From(s.TimeDeltaFront, BoardSource.CrewChief, now);
            if (s != null && s.TimeDeltaBehind > 0) b.GapBehind = Fact<float>.From(s.TimeDeltaBehind, BoardSource.CrewChief, now);
            if (gs != null) b.Speed = Fact<float>.From(gs.PositionAndMotionData.CarSpeed, BoardSource.CrewChief, now);
            else if (native && v.Player != null && !float.IsNaN(v.Player.Speed)) b.Speed = Fact<float>.From(v.Player.Speed, BoardSource.Native, nu);
            if (gs != null) b.InPit = Fact<bool>.From(gs.PitData.InPitlane, BoardSource.CrewChief, now);
            else if (native && v.Player != null) b.InPit = Fact<bool>.From(v.Player.InPit, BoardSource.Native, nu);

            // Strafen: beide Quellen zusammen
            var pen = new BoardPenalty();
            bool penKnown = false;
            if (gs != null)
            {
                var p = gs.PenaltiesData;
                pen.DriveThrough = p.HasDriveThrough; pen.StopAndGo = p.HasStopAndGo; pen.SlowDown = p.HasSlowDown;
                pen.TimeDeduction = p.HasTimeDeduction; pen.Outstanding = Math.Max(0, p.NumOutstandingPenalties);
                penKnown = true;
            }
            if (native)
            {
                if (v.PositionsToGiveBack >= 0) { pen.PositionsToGiveBack = v.PositionsToGiveBack; penKnown = true; }
                if (v.PlayerSlowDown > 0) { pen.SlowDown = true; pen.SlowDownSeconds = v.PlayerSlowDown; penKnown = true; }
            }
            if (penKnown) b.Penalty = Fact<BoardPenalty>.From(pen, native && (v.PositionsToGiveBack >= 0 || v.PlayerSlowDown > 0) ? BoardSource.Native : BoardSource.CrewChief, now);
            // Gestaffelter Start (RaceRoom 3.6): Die Session ist schon grün, die eigene Startgruppe wartet aber noch.
            if (gs != null && s.SessionType == SessionType.Race && RaceSituationWatch.InFormation(gs, now) && s.SessionPhase == SessionPhase.Green)
                b.Phase = Fact<FieldPhase>.From(FieldPhase.Formation, BoardSource.Native, now);
            // Wer ist direkt vor und hinter Rob auf der Strecke (Lagebild, 20 Hz)?
            if (native && v.Player != null && v.Cars != null && !v.InMenus)
            {
                var ahead = v.Ahead(1).FirstOrDefault();
                var behind = v.Behind(1).FirstOrDefault();
                if (ahead != null) { b.TrackAheadS = Fact<float>.From(ahead.GapS, BoardSource.Native, nu); b.TrackAheadName = ahead.Name; b.TrackAheadAgeMs = ahead.DataAgeMs(now); }
                if (behind != null) { b.TrackBehindS = Fact<float>.From(-behind.GapS, BoardSource.Native, nu); b.TrackBehindName = behind.Name; b.TrackBehindAgeMs = behind.DataAgeMs(now); }
            }
            return b;
        }

        private static string lastSession;
        private static readonly Dictionary<string, DateTime> conflictSince = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private static readonly HashSet<string> conflictLogged = new HashSet<string>(StringComparer.Ordinal);
        private static DateTime lastEmit = DateTime.MinValue;
        /// <summary>Ein Widerspruch wird gemeldet, wenn er so lange ununterbrochen besteht.</summary>
        internal static readonly TimeSpan ConflictHold = TimeSpan.FromSeconds(3);

        /// <summary>Einmal je Spielzustand: neue Tafel bauen, Widersprüche verfolgen, alle 2 s in den Mitschnitt schreiben.</summary>
        internal static BoardSnapshot Update(GameStateData gs, FieldView v, DateTime now)
        {
            var b = Compose(gs, v, now);
            string session = gs == null ? null : gs.SessionData.SessionId + "|" + gs.SessionData.SessionType;
            lock (conflictSince)
            {
                if (session != lastSession) { lastSession = session; conflictSince.Clear(); conflictLogged.Clear(); }
                foreach (var key in new List<string>(conflictSince.Keys))
                    if (!b.Conflicts.ContainsKey(key)) { conflictSince.Remove(key); conflictLogged.Remove(key); }
                foreach (var c in b.Conflicts)
                {
                    if (!conflictSince.TryGetValue(c.Key, out var since)) conflictSince[c.Key] = since = now;
                    if (now - since >= ConflictHold && conflictLogged.Add(c.Key))
                        LiveDiagnostics.Decision("board", "conflict", c.Key, "nativ/cc " + c.Value + " seit " + Math.Round((now - since).TotalSeconds, 1) + " s");
                }
            }
            current = b;
            if (LiveDiagnostics.Enabled && (now - lastEmit >= TimeSpan.FromSeconds(2) || now < lastEmit))
            {
                lastEmit = now;
                LiveDiagnostics.Emit("board", LiveDiagnostics.Data(
                    "board_session", b.Session.ToString(), "board_phase", b.Phase.ToString(), "board_place", b.Place.ToString(),
                    "board_field", b.FieldSize.ToString(), "board_laps_left", b.LapsLeft.ToString(),
                    "board_time_left", b.TimeLeft.Known ? Math.Round(b.TimeLeft.Value, 1).ToString(CultureInfo.InvariantCulture) + "@" + Src(b.TimeLeft.Source) : "?",
                    "board_penalty", b.Penalty.Known ? b.Penalty.Value.Kind + "@" + Src(b.Penalty.Source) : "?",
                    "board_track_ahead", b.TrackAheadS.Known ? Math.Round(b.TrackAheadS.Value, 2).ToString(CultureInfo.InvariantCulture) + "s/" + Math.Round(b.TrackAheadAgeMs) + "ms" : "?",
                    "board_track_behind", b.TrackBehindS.Known ? Math.Round(b.TrackBehindS.Value, 2).ToString(CultureInfo.InvariantCulture) + "s/" + Math.Round(b.TrackBehindAgeMs) + "ms" : "?",
                    "board_native_age_s", v == null ? -1 : Math.Round(Math.Max(0, (now - v.Utc).TotalSeconds), 3),
                    "board_conflicts", b.Conflicts.Count == 0 ? "" : string.Join(";", b.Conflicts)));
            }
            return b;
        }

        private static string Src(BoardSource s) => s == BoardSource.Native ? "nativ" : s == BoardSource.CrewChief ? "cc" : "?";

        internal static void Reset() { lock (conflictSince) { lastSession = null; conflictSince.Clear(); conflictLogged.Clear(); } current = null; }
    }
}
