using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Art einer Rennleitungs-Meldung, für alle Simulationen gleich.</summary>
    internal enum RaceControlKind
    {
        Penalty, PenaltyServed, Disqualified, Warning, TrackLimit, IncidentPoints, Flag, SafetyCar, StartProcedure, StartOffence,
        MechanicalFlag, InputLock, Repair
    }

    /// <summary>
    /// Eine Meldung der Rennleitung oder des Regelwerks der Sim: was, warum, wen, Zählerstand, Grenze, was bis zur Grenze bleibt,
    /// woher und wann. Werte, die die Sim nicht liefert, bleiben -1 bzw. NaN; nichts wird geschätzt.
    /// </summary>
    internal sealed class RaceControlEvent
    {
        public RaceControlKind Kind;
        /// <summary>Kurzer fester Schlüssel („start-jump“, „cut-giveback“ …), bestimmt Funktext und Thema.</summary>
        public string Code;
        public string Reason;
        /// <summary>Name des betroffenen Gegners (null = eigenes Auto).</summary>
        public string Car;
        public int Count = -1, Limit = -1, Remaining = -1;
        public double Value = double.NaN;
        public string Source;
        public DateTime Utc;

        public bool IsPlayer => Car == null;

        public JObject ToJson(DateTime now)
        {
            var o = new JObject { ["vor_s"] = Math.Round(Math.Max(0, (now - Utc).TotalSeconds)), ["art"] = Kind.ToString(), ["code"] = Code };
            if (Reason != null) o["grund"] = Reason;
            if (Car != null) o["gegner"] = Car;
            if (Count >= 0) o["stand"] = Count;
            if (Limit >= 0) o["grenze"] = Limit;
            if (Remaining >= 0) o["bis_grenze"] = Remaining;
            if (!double.IsNaN(Value)) o["wert"] = Math.Round(Value, 2);
            o["quelle"] = Source;
            return o;
        }
    }

    /// <summary>Ein Erkenner liest den Rohzustand einer Sim und meldet nur Änderungen. Das erste Bild ist der Ausgangsstand.</summary>
    internal interface IRaceControlDetector
    {
        void Reset();
        void Detect(GameStateData gs, DateTime now, List<RaceControlEvent> into);
    }

    /// <summary>
    /// Rennleitung für alle Sims (Etappe 1): Die Erkenner je Sim lesen, was die offizielle Schnittstelle zusätzlich liefert
    /// (Startstrafe, Abkürzen mit Frist, Verwarnung, Technik-Flagge, Grund für Gelb, Vorfallart …). Jede Meldung geht in die
    /// Rennchronik und ins Lagebild der KI; was der Fahrer sofort wissen muss, geht als Pflichtmeldung an den Funk.
    /// </summary>
    internal sealed class RaceControlFeed
    {
        internal const int MaxRecent = 24;
        private static readonly object gate = new object();
        private static readonly List<RaceControlEvent> recent = new List<RaceControlEvent>();
        private static int recentSession = int.MinValue;

        private readonly IRaceControlDetector[] detectors;
        private readonly List<RaceControlEvent> scratch = new List<RaceControlEvent>();
        private int session = int.MinValue;
        private SessionType type = SessionType.Unavailable;

        internal RaceControlFeed(params IRaceControlDetector[] detectors)
        {
            this.detectors = detectors != null && detectors.Length > 0 ? detectors : new IRaceControlDetector[] {
                new CommonRaceControlDetector(), new R3ERaceControlDetector(), new IRacingRaceControlDetector(),
                new AceRaceControlDetector(), new Ams2RaceControlDetector(), new LmuRaceControlDetector() };
        }

        internal void Reset()
        {
            foreach (var d in detectors) d.Reset();
            session = int.MinValue; type = SessionType.Unavailable;
            LmuStreamEvents.Reset();
            lock (gate) { recent.Clear(); recentSession = int.MinValue; }
        }

        /// <summary>Ein Spielbild auswerten. Gibt die neuen Meldungen zurück (für Tests und Protokoll).</summary>
        internal List<RaceControlEvent> Observe(GameStateData gs, DateTime now, LiveWarningDispatcher warnings = null)
        {
            var result = new List<RaceControlEvent>();
            if (gs == null) return result;
            if (gs.SessionData.SessionId != session || gs.SessionData.SessionType != type)
            {
                foreach (var d in detectors) d.Reset();
                session = gs.SessionData.SessionId; type = gs.SessionData.SessionType;
                lock (gate) { recent.Clear(); recentSession = session; }
            }
            foreach (var d in detectors)
            {
                scratch.Clear();
                try { d.Detect(gs, now, scratch); }
                catch (Exception e) { LiveDiagnostics.Decision("race-control", "detector_error", d.GetType().Name, e.Message); d.Reset(); continue; }
                result.AddRange(scratch);
            }
            foreach (var e in result) Publish(e, gs, now, warnings);
            return result;
        }

        private static void Publish(RaceControlEvent e, GameStateData gs, DateTime now, LiveWarningDispatcher warnings)
        {
            lock (gate)
            {
                recent.Add(e);
                if (recent.Count > MaxRecent) recent.RemoveRange(0, recent.Count - MaxRecent);
            }
            LiveDiagnostics.Decision("race-control-" + e.Code, "detected", e.Source, e.ToJson(now).ToString(Newtonsoft.Json.Formatting.None));
            Chronicle(e, gs, now);
            if (e.Code == "rival-contact") FieldImpacts.Note(e.Car, e.Value, e.Reason == "objekt", now);
            if (warnings == null) return;
            var call = RaceControlRadio.Call(e, gs);
            if (call == null) return;
            var phase = gs.SessionData.SessionPhase;
            bool driving = phase == SessionPhase.Green || phase == SessionPhase.FullCourseYellow || phase == SessionPhase.Formation ||
                phase == SessionPhase.Countdown || phase == SessionPhase.Gridwalk;
            if (!driving && e.Kind != RaceControlKind.Disqualified) return;
            int session = gs.SessionData.SessionId;
            DateTime deadline = now.AddSeconds(call.ValidSeconds);
            Func<GameStateData, bool> still = RaceControlRadio.StillCurrent(e);
            warnings.Offer(call.Topic, call.Text, call.Member, call.Priority, gs, now,
                live => live.SessionData.SessionId == session && still(live), "race_control:" + e.Source + ":" + e.Code, -1,
                (live, utc) => utc <= deadline);
        }

        /// <summary>Rennleitung in die Rennchronik (alle Sitzungsarten; Gegner mit Namen nur, wenn die Sim ihn liefert).</summary>
        internal static void Chronicle(RaceControlEvent e, GameStateData gs, DateTime now)
        {
            ChronicleKind kind;
            switch (e.Kind)
            {
                case RaceControlKind.Penalty: case RaceControlKind.StartOffence: case RaceControlKind.InputLock: case RaceControlKind.Warning:
                case RaceControlKind.Disqualified:
                    kind = e.IsPlayer ? ChronicleKind.Penalty : ChronicleKind.RaceControl; break;
                case RaceControlKind.PenaltyServed: kind = e.IsPlayer ? ChronicleKind.PenaltyServed : ChronicleKind.RaceControl; break;
                case RaceControlKind.TrackLimit: kind = ChronicleKind.TrackLimit; break;
                case RaceControlKind.IncidentPoints: kind = ChronicleKind.Incident; break;
                default: kind = ChronicleKind.RaceControl; break;
            }
            string detail = e.Code + (e.Reason == null ? "" : ";" + e.Reason) + (e.Count >= 0 ? ";stand=" + e.Count : "") +
                (e.Limit >= 0 ? ";grenze=" + e.Limit : "") + (e.Remaining >= 0 ? ";bis_grenze=" + e.Remaining : "");
            // Chronik fasst Strafen eines Zeitfensters zusammen; Rennleitung je Code getrennt halten.
            RaceChronicle.Current.Add(gs, now, kind, detail: detail, source: e.Source, value: e.Value,
                subjectName: e.Car == null ? null : RadioPhraseCatalog.SafeRivalName(e.Car), dedupe: TimeSpan.FromSeconds(3));
        }

        /// <summary>Für das Sprachmodell: die jüngsten Meldungen der Rennleitung dieser Sitzung.</summary>
        internal static JObject Latest(int sessionId, DateTime now, int max = 10)
        {
            List<RaceControlEvent> list;
            lock (gate)
            {
                if (recentSession != sessionId || recent.Count == 0) return null;
                list = recent.Skip(Math.Max(0, recent.Count - max)).ToList();
            }
            return new JObject
            {
                ["meldungen"] = new JArray(list.Select(e => (JToken)e.ToJson(now))),
                ["hinweis"] = "Direkt aus der Schnittstelle der Sim. stand/grenze/bis_grenze nur, wenn die Sim sie liefert – nicht ergänzen."
            };
        }

        /// <summary>LMU: eine Zeile des Ergebnis-Datenstroms (Kontakte, Strafen anderer) in Chronik und Lagebild.</summary>
        internal RaceControlEvent ObserveLmuLine(LMURaceControlStream.Line line, GameStateData gs, DateTime now)
        {
            if (line == null || gs == null) return null;
            LmuStreamEvents.RememberPenaltyReason(line);
            var e = LmuStreamEvents.FromLine(line, now);
            if (e != null) Publish(e, gs, now, null);
            return e;
        }

        internal static List<RaceControlEvent> Snapshot() { lock (gate) return recent.ToList(); }

        internal static RaceControlEvent E(RaceControlKind kind, string code, string source, DateTime now, string reason = null)
            => new RaceControlEvent { Kind = kind, Code = code, Source = source, Utc = now, Reason = reason };
    }

    /// <summary>
    /// Was jede Sim über Crew Chiefs Spielstand gleich liefert: Disqualifikation schon während des Rennens und die Unterphasen
    /// einer Full Course Yellow bzw. Safety-Car-Phase (Box zu, Box offen, letzte Runde vor dem Restart). Beginn und Ende der
    /// Gelbphase meldet die Flaggenbeobachtung.
    /// </summary>
    internal sealed class CommonRaceControlDetector : IRaceControlDetector
    {
        private bool seen, dq;
        private FullCourseYellowPhase fcy = FullCourseYellowPhase.RACING;
        public void Reset() { seen = false; dq = false; fcy = FullCourseYellowPhase.RACING; }

        public void Detect(GameStateData gs, DateTime now, List<RaceControlEvent> into)
        {
            bool nowDq = gs.SessionData.IsDisqualified;
            var phase = gs.FlagData.isFullCourseYellow || gs.SessionData.SessionPhase == SessionPhase.FullCourseYellow ? gs.FlagData.fcyPhase : FullCourseYellowPhase.RACING;
            if (!seen) { seen = true; dq = nowDq; fcy = phase; return; }
            if (nowDq && !dq && gs.SessionData.SessionType == SessionType.Race && gs.SessionData.SessionPhase != SessionPhase.Finished)
                into.Add(RaceControlFeed.E(RaceControlKind.Disqualified, "dq", "spielstand_disqualifiziert", now));
            dq = nowDq;
            if (phase != fcy && gs.SessionData.SessionType == SessionType.Race)
            {
                string code = null;
                switch (phase)
                {
                    case FullCourseYellowPhase.PITS_CLOSED: code = "fcy-pits-closed"; break;
                    case FullCourseYellowPhase.PITS_OPEN_LEAD_LAP_VEHICLES: code = "fcy-pits-lead-lap"; break;
                    case FullCourseYellowPhase.PITS_OPEN: code = "fcy-pits-open"; break;
                    case FullCourseYellowPhase.LAST_LAP_NEXT: code = "fcy-last-lap-next"; break;
                    case FullCourseYellowPhase.LAST_LAP_CURRENT: code = "fcy-last-lap"; break;
                }
                if (code != null) into.Add(RaceControlFeed.E(RaceControlKind.SafetyCar, code, "spielstand_fcy_phase", now));
            }
            fcy = phase;
        }
    }

    /// <summary>
    /// RaceRoom ab Schnittstelle 3.6: Startvergehen (Frühstart, falsche Seite), verzögerter Boxengassenstart, Seite in der
    /// Formation und ob das eigene Auto die Gelbphase ausgelöst hat. Unter 3.6 meldet der Erkenner nichts.
    /// </summary>
    internal sealed class R3ERaceControlDetector : IRaceControlDetector
    {
        internal struct Input
        {
            public bool Valid, Race;
            public int StartPhase, StartPenaltyPending, FormationSide, YellowCausedIt;
            public float PitlaneStartDelay;
        }

        private bool seen, sideTold, pitlaneTold;
        private Input last;
        public void Reset() { seen = false; sideTold = false; pitlaneTold = false; last = default(Input); }

        internal static Input Read(GameStateData gs)
        {
            var w = gs?.rawGameData as RaceRoom.R3ESharedMemoryReader.R3EStructWrapper;
            if (w == null) return default(Input);
            var raw = w.data;
            var input = new Input { Valid = raw.VersionMajor == 3 && raw.VersionMinor >= 6 && raw.GamePaused == 0 && raw.GameInReplay == 0,
                Race = raw.SessionType == 2, StartPenaltyPending = raw.StartPenaltyPending, FormationSide = raw.FormationSide,
                PitlaneStartDelay = raw.PitlaneStartDelayRemaining, YellowCausedIt = raw.Flags.YellowCausedIt, StartPhase = -1 };
            int found = 0;
            if (raw.DriverData != null && raw.VehicleInfo.SlotId >= 0)
                for (int i = 0; i < Math.Min(raw.NumCars, raw.DriverData.Length); i++)
                    if (raw.DriverData[i].DriverInfo.SlotId == raw.VehicleInfo.SlotId) { found++; input.StartPhase = raw.DriverData[i].StartPhase; }
            if (found != 1) input.StartPhase = -1;
            return input;
        }

        public void Detect(GameStateData gs, DateTime now, List<RaceControlEvent> into) => Step(Read(gs), now, into);

        internal void Step(Input s, DateTime now, List<RaceControlEvent> into)
        {
            if (!s.Valid) { seen = false; return; }
            if (!seen) { seen = true; last = s; return; }
            if (s.Race && s.StartPenaltyPending != last.StartPenaltyPending && (s.StartPenaltyPending == 1 || s.StartPenaltyPending == 2))
                into.Add(RaceControlFeed.E(RaceControlKind.StartOffence, s.StartPenaltyPending == 1 ? "start-jump" : "start-side", "r3e_start_penalty_pending", now,
                    s.StartPenaltyPending == 1 ? "frühstart" : "falsche_seite"));
            // Seite einmal je Sitzung, solange die eigene Gruppe noch wartet.
            if (s.Race && !sideTold && s.StartPhase == 0 && (s.FormationSide == 0 || s.FormationSide == 1) && last.StartPhase == 0)
            {
                sideTold = true;
                var e = RaceControlFeed.E(RaceControlKind.StartProcedure, "formation-side", "r3e_formation_side", now, s.FormationSide == 0 ? "links" : "rechts");
                e.Count = s.FormationSide;
                into.Add(e);
            }
            if (s.Race && !pitlaneTold && s.PitlaneStartDelay >= 1 && s.PitlaneStartDelay < 600 && last.PitlaneStartDelay >= 1)
            {
                pitlaneTold = true;
                var e = RaceControlFeed.E(RaceControlKind.StartProcedure, "pitlane-start", "r3e_pitlane_start_delay", now);
                e.Value = Math.Round(s.PitlaneStartDelay);
                into.Add(e);
            }
            if (s.YellowCausedIt == 1 && last.YellowCausedIt == 0)
                into.Add(RaceControlFeed.E(RaceControlKind.Flag, "yellow-caused", "r3e_yellow_caused_it", now));
            last = s;
        }
    }

    /// <summary>
    /// iRacing: Disqualifikation, Verwarnung, Technik-Flagge, Caution und Restart-Signale aus den Sitzungsflaggen, die Art jedes
    /// neuen Vorfallpunkts (1x neben der Strecke, 2x Kontrollverlust, 4x schwerer Kontakt) und genutzte Fast Repairs.
    /// </summary>
    internal sealed class IRacingRaceControlDetector : IRaceControlDetector
    {
        internal struct Input
        {
            public bool Valid, Race, OffTrack;
            public uint Flags;
            public int Incidents, Limit, FastRepairUsed, FastRepairAvailable;
        }

        private bool seen;
        private Input last;
        private DateTime lastOffTrack = DateTime.MinValue;
        public void Reset() { seen = false; last = default(Input); lastOffTrack = DateTime.MinValue; }

        internal const uint Disqualify = 0x00020000, Black = 0x00010000, Furled = 0x00080000, Repair = 0x00100000, Caution = 0x00004000,
            CautionWaving = 0x00008000, GreenHeld = 0x00000400, TenToGo = 0x00000800, FiveToGo = 0x00001000, OneLapToGreen = 0x00000200, Red = 0x00000010;

        internal static Input Read(GameStateData gs)
        {
            var w = gs?.rawGameData as iRacing.iRacingSharedMemoryReader.iRacingStructWrapper;
            var t = w?.data?.Telemetry;
            if (t == null) return default(Input);
            return new Input { Valid = true, Race = gs.SessionData.SessionType == SessionType.Race, Flags = t.SessionFlags,
                OffTrack = t.PlayerTrackSurface == iRacing.TrackSurfaces.OffTrack, Incidents = t.PlayerCarMyIncidentCount,
                Limit = w.data.SessionData != null && w.data.SessionData.IsLimitedIncidents ? w.data.SessionData.IncidentLimit : -1,
                FastRepairUsed = t.FastRepairUsed, FastRepairAvailable = t.FastRepairAvailable };
        }

        public void Detect(GameStateData gs, DateTime now, List<RaceControlEvent> into) => Step(Read(gs), now, into);

        private bool Rose(Input s, uint flag) => (s.Flags & flag) != 0 && (last.Flags & flag) == 0;

        /// <summary>Art eines Vorfallzuwachses nach iRacings Straßen-Regelwerk, bestätigt durch die Streckenoberfläche.</summary>
        internal static string IncidentReason(int delta, bool recentlyOffTrack)
        {
            switch (delta)
            {
                case 1: return recentlyOffTrack ? "neben_der_strecke" : "1x";
                case 2: return "kontrollverlust";
                case 4: return "schwerer_kontakt";
                default: return delta + "x";
            }
        }

        internal void Step(Input s, DateTime now, List<RaceControlEvent> into)
        {
            if (!s.Valid) { seen = false; return; }
            if (s.OffTrack) lastOffTrack = now;
            if (!seen) { seen = true; last = s; return; }
            const string src = "iracing_session_flags";
            if (Rose(s, Disqualify)) into.Add(RaceControlFeed.E(RaceControlKind.Disqualified, "dq", src, now));
            // Eingerollte schwarze Flagge: Verwarnung oder Slow-down; die Strafe selbst meldet die Strafbeobachtung.
            if (Rose(s, Furled)) into.Add(RaceControlFeed.E(RaceControlKind.Warning, "furled", src, now, "eingerollte_schwarze_flagge"));
            if (Rose(s, Black) && (s.Flags & Furled) == 0) into.Add(RaceControlFeed.E(RaceControlKind.Penalty, "black", src, now, "schwarze_flagge"));
            if (Rose(s, Repair)) into.Add(RaceControlFeed.E(RaceControlKind.MechanicalFlag, "meatball", src, now, "reparatur_pflicht"));
            if (s.Race && (Rose(s, Caution) || Rose(s, CautionWaving)) && (last.Flags & (Caution | CautionWaving)) == 0)
                into.Add(RaceControlFeed.E(RaceControlKind.SafetyCar, "caution", src, now));
            if (s.Race && Rose(s, OneLapToGreen)) into.Add(RaceControlFeed.E(RaceControlKind.SafetyCar, "one-to-green", src, now));
            if (s.Race && Rose(s, GreenHeld)) into.Add(RaceControlFeed.E(RaceControlKind.Flag, "green-held", src, now));
            if (s.Race && Rose(s, Red)) into.Add(RaceControlFeed.E(RaceControlKind.Flag, "red", src, now));
            if (s.Race && Rose(s, TenToGo)) into.Add(RaceControlFeed.E(RaceControlKind.Flag, "ten-to-go", src, now));
            if (s.Race && Rose(s, FiveToGo)) into.Add(RaceControlFeed.E(RaceControlKind.Flag, "five-to-go", src, now));
            if (s.Incidents > last.Incidents && last.Incidents >= 0 && s.Incidents - last.Incidents <= 50)
            {
                int delta = s.Incidents - last.Incidents;
                var e = RaceControlFeed.E(RaceControlKind.IncidentPoints, "inc", "iracing_incident_count", now,
                    IncidentReason(delta, now - lastOffTrack <= TimeSpan.FromSeconds(3)));
                e.Count = s.Incidents; e.Value = delta;
                if (s.Limit > 0) { e.Limit = s.Limit; e.Remaining = Math.Max(0, s.Limit - s.Incidents); }
                into.Add(e);
            }
            if (s.FastRepairUsed > last.FastRepairUsed && last.FastRepairUsed >= 0)
            {
                var e = RaceControlFeed.E(RaceControlKind.Repair, "fast-repair", "iracing_fast_repair", now);
                e.Count = s.FastRepairUsed; if (s.FastRepairAvailable >= 0) e.Remaining = s.FastRepairAvailable;
                into.Add(e);
            }
            last = s;
        }
    }

    /// <summary>
    /// Assetto Corsa EVO: Zeitgewinn durch Abkürzen mit Rückgabefrist (Meter bis zur Strafe), angesammelte Strafzeit, Sperre der
    /// Eingaben nach einer Kollisionsstrafe, rote Flagge, Technik-Flagge, Öl/rutschig und Verwarnung.
    /// </summary>
    internal sealed class AceRaceControlDetector : IRaceControlDetector
    {
        internal struct Input
        {
            public bool Valid;
            public int Flag, GlobalFlag, CutPenaltyMs, DistanceToDeadline;
            public float CutDelta, ControlLock;
        }

        // Werte aus ACEVO_FLAG_TYPE.
        internal const int RedFlag = 3, BlackWhiteFlag = 7, OrangeCircleFlag = 9, SlipperyFlag = 10;
        private bool seen, giveBackOpen;
        private Input last;
        public void Reset() { seen = false; giveBackOpen = false; last = default(Input); }

        internal static Input Read(GameStateData gs)
        {
            var w = gs?.rawGameData as ACE.ACESharedMemoryReader.ACEStructWrapper;
            if (w?.data == null) return default(Input);
            var g = w.data.aceGraphic;
            return new Input { Valid = true, Flag = (int)g.flag, GlobalFlag = (int)g.globalFlag, CutPenaltyMs = g.raceCutGainedTimeMs,
                DistanceToDeadline = g.distanceToDeadline, CutDelta = g.raceCutCurrentDelta, ControlLock = g.controlLockTime };
        }

        public void Detect(GameStateData gs, DateTime now, List<RaceControlEvent> into) => Step(Read(gs), now, into);

        private static bool Good(float v, float min, float max) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= min && v <= max;

        internal void Step(Input s, DateTime now, List<RaceControlEvent> into)
        {
            if (!s.Valid) { seen = false; return; }
            if (!seen) { seen = true; last = s; giveBackOpen = Good(s.CutDelta, .1f, 120); return; }
            const string src = "acevo_graphics";
            // Zeitgewinn durch Abkürzen: einmal je Vorgang, mit Frist in Metern, solange die Sim eine liefert.
            if (!giveBackOpen && Good(s.CutDelta, .1f, 120))
            {
                giveBackOpen = true;
                var e = RaceControlFeed.E(RaceControlKind.TrackLimit, "cut-giveback", src + ".raceCutCurrentDelta", now);
                e.Value = Math.Round(s.CutDelta, 1);
                if (s.DistanceToDeadline > 0 && s.DistanceToDeadline < 100000) e.Remaining = s.DistanceToDeadline;
                into.Add(e);
            }
            else if (giveBackOpen && !Good(s.CutDelta, .05f, 120)) giveBackOpen = false;
            if (s.CutPenaltyMs > last.CutPenaltyMs && last.CutPenaltyMs >= 0 && s.CutPenaltyMs < 3600000)
            {
                var e = RaceControlFeed.E(RaceControlKind.Penalty, "cut-penalty", src + ".raceCutGainedTimeMs", now, "abkuerzen");
                e.Value = Math.Round(s.CutPenaltyMs / 1000.0, 1);
                into.Add(e);
            }
            if (Good(s.ControlLock, .5f, 600) && !Good(last.ControlLock, .05f, 600))
            {
                var e = RaceControlFeed.E(RaceControlKind.InputLock, "input-lock", src + ".controlLockTime", now, "kollision");
                e.Value = Math.Round(s.ControlLock);
                into.Add(e);
            }
            if (s.GlobalFlag == RedFlag && last.GlobalFlag != RedFlag) into.Add(RaceControlFeed.E(RaceControlKind.Flag, "red", src + ".globalFlag", now));
            if (s.Flag != last.Flag)
            {
                if (s.Flag == OrangeCircleFlag) into.Add(RaceControlFeed.E(RaceControlKind.MechanicalFlag, "meatball", src + ".flag", now, "technischer_defekt"));
                else if (s.Flag == SlipperyFlag) into.Add(RaceControlFeed.E(RaceControlKind.Flag, "slippery", src + ".flag", now, "rutschige_strecke"));
                else if (s.Flag == BlackWhiteFlag) into.Add(RaceControlFeed.E(RaceControlKind.Warning, "warning", src + ".flag", now, "unsportlich"));
            }
            last = s;
        }
    }

    /// <summary>Automobilista 2: Grund der höchsten Flagge (Alleinunfall, Kollision, Hindernis) und eigener Unfallzustand.</summary>
    internal sealed class Ams2RaceControlDetector : IRaceControlDetector
    {
        internal struct Input { public bool Valid; public uint FlagReason, CrashState; }
        private bool seen;
        private Input last;
        public void Reset() { seen = false; last = default(Input); }

        internal static Input Read(GameStateData gs)
        {
            var w = gs?.rawGameData as AMS2.AMS2SharedMemoryReader.AMS2StructWrapper;
            if (w == null) return default(Input);
            return new Input { Valid = true, FlagReason = w.data.mHighestFlagReason, CrashState = w.data.mCrashState };
        }

        internal static string FlagReason(uint r) => r == 1 ? "alleinunfall" : r == 2 ? "kollision" : r == 3 ? "hindernis" : null;
        internal static string Crash(uint c) => c == 1 ? "neben_der_strecke" : c == 2 ? "grosses_teil" : c == 3 ? "dreher" : c == 4 ? "ueberschlag" : null;

        public void Detect(GameStateData gs, DateTime now, List<RaceControlEvent> into) => Step(Read(gs), now, into);

        internal void Step(Input s, DateTime now, List<RaceControlEvent> into)
        {
            if (!s.Valid) { seen = false; return; }
            if (!seen) { seen = true; last = s; return; }
            if (s.FlagReason != last.FlagReason && FlagReason(s.FlagReason) != null)
                into.Add(RaceControlFeed.E(RaceControlKind.Flag, "yellow-reason", "ams2_highest_flag_reason", now, FlagReason(s.FlagReason)));
            if (s.CrashState != last.CrashState && Crash(s.CrashState) != null)
                into.Add(RaceControlFeed.E(RaceControlKind.IncidentPoints, "crash-state", "ams2_crash_state", now, Crash(s.CrashState)));
            last = s;
        }
    }

    /// <summary>
    /// Le Mans Ultimate über das native Abbild: neue Strafen bei Gegnern (Zähler je Auto) und eigene Disqualifikation über den
    /// Zielstatus. Eigene Strafen, Tracklimit-Punkte und Rennleitungstexte laufen bereits über die bestehenden LMU-Beobachter.
    /// </summary>
    internal sealed class LmuRaceControlDetector : IRaceControlDetector
    {
        internal sealed class CarInput { public int Id, Penalties, FinishStatus; public string Name; public bool Player; }
        private readonly Dictionary<int, CarInput> last = new Dictionary<int, CarInput>();
        private bool seen;
        private DateTime lastUtc;
        public void Reset() { last.Clear(); seen = false; lastUtc = DateTime.MinValue; }

        public void Detect(GameStateData gs, DateTime now, List<RaceControlEvent> into)
        {
            if (Game.game != GameEnum.LMU) { Reset(); return; }
            var snap = LMUNativeSnapshot.Fresh(now);
            if (snap == null || snap.Utc == lastUtc) return;
            lastUtc = snap.Utc;
            Step(snap.Cars.Select(c => new CarInput { Id = c.Id, Penalties = c.Penalties, FinishStatus = c.FinishStatus, Name = c.Name, Player = c.Player }), now, into);
        }

        internal void Step(IEnumerable<CarInput> cars, DateTime now, List<RaceControlEvent> into)
        {
            var current = cars.Where(c => c != null).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
            if (seen)
                foreach (var c in current.Values)
                {
                    CarInput p;
                    if (!last.TryGetValue(c.Id, out p) || p.Name != c.Name) continue;
                    if (!c.Player && c.Penalties > p.Penalties && c.Penalties - p.Penalties <= 5)
                    {
                        var e = RaceControlFeed.E(RaceControlKind.Penalty, "rival-penalty", "lmu_native_num_penalties", now, LmuStreamEvents.PenaltyReason(c.Name, now));
                        e.Car = c.Name; e.Count = c.Penalties;
                        into.Add(e);
                    }
                    else if (!c.Player && c.Penalties < p.Penalties && c.Penalties >= 0)
                    {
                        var e = RaceControlFeed.E(RaceControlKind.PenaltyServed, "rival-penalty-served", "lmu_native_num_penalties", now);
                        e.Car = c.Name; e.Count = c.Penalties;
                        into.Add(e);
                    }
                    if (!c.Player && c.FinishStatus == 3 && p.FinishStatus != 3)
                    {
                        var e = RaceControlFeed.E(RaceControlKind.Disqualified, "rival-dq", "lmu_native_finish_status", now);
                        e.Car = c.Name;
                        into.Add(e);
                    }
                }
            seen = true;
            last.Clear();
            foreach (var p in current) last[p.Key] = new CarInput { Id = p.Value.Id, Penalties = p.Value.Penalties, FinishStatus = p.Value.FinishStatus, Name = p.Value.Name, Player = p.Value.Player };
        }
    }

    /// <summary>
    /// LMU-Ergebnisdatenstrom: Kontaktmeldungen („A(3) reported contact (0.21) with another vehicle B(7)“ bzw. mit einem
    /// Streckenobjekt) und Strafgründe anderer Fahrer. Der Wortlaut folgt dem rFactor-Ergebnisformat und ist für LMU noch nicht an
    /// einer Aufnahme bestätigt: Was nicht eindeutig passt, bleibt reines Protokoll.
    /// </summary>
    internal static class LmuStreamEvents
    {
        private static readonly System.Text.RegularExpressions.Regex Contact = new System.Text.RegularExpressions.Regex(
            @"^(?<a>.+?)\s*\((?<ai>\d+)\)\s+reported contact\s*\((?<sev>[0-9]+(?:[.,][0-9]+)?)\)\s+with\s+(?:another vehicle\s+(?<b>.+?)\s*\((?<bi>\d+)\)|(?<obj>.+?))\s*\.?$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        private static readonly System.Text.RegularExpressions.Regex DriverAttribute = new System.Text.RegularExpressions.Regex(
            @"\bdriver\s*=\s*""(?<n>[^""]+)""", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        private static readonly Dictionary<string, KeyValuePair<string, DateTime>> reasons = new Dictionary<string, KeyValuePair<string, DateTime>>();

        internal static RaceControlEvent FromLine(LMURaceControlStream.Line line, DateTime now)
        {
            string text = LMURaceControlStream.Plain(line.Text);
            var m = Contact.Match(text);
            if (!m.Success) return null;
            string a = m.Groups["a"].Value.Trim(), b = m.Groups["b"].Success ? m.Groups["b"].Value.Trim() : null;
            int ai = int.Parse(m.Groups["ai"].Value, CultureInfo.InvariantCulture), bi = m.Groups["bi"].Success ? int.Parse(m.Groups["bi"].Value, CultureInfo.InvariantCulture) : -1;
            double sev;
            if (!double.TryParse(m.Groups["sev"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out sev)) sev = double.NaN;
            bool meA = Is(line, a, ai), meB = b != null && Is(line, b, bi);
            RaceControlEvent e;
            if (meA || meB)
            {
                e = RaceControlFeed.E(RaceControlKind.IncidentPoints, b == null ? "contact-object" : "contact", "lmu_results_stream", now,
                    b == null ? Clip(m.Groups["obj"].Value) : null);
                if (b != null) e.Car = meA ? b : a;
            }
            else
            {
                e = RaceControlFeed.E(RaceControlKind.Flag, "rival-contact", "lmu_results_stream", now, b == null ? "objekt" : "mit " + Clip(b));
                e.Car = a;
            }
            e.Value = sev;
            return e;
        }

        private static bool Is(LMURaceControlStream.Line line, string name, int id) =>
            line.PlayerId >= 0 && id == line.PlayerId || !string.IsNullOrWhiteSpace(line.Player) && string.Equals(name, line.Player.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string Clip(string s) { s = (s ?? "").Trim(); return s.Length > 60 ? s.Substring(0, 60) : s; }

        /// <summary>Strafgrund einer Strafzeile zu einem anderen Fahrer merken (für die Zählerauswertung).</summary>
        internal static void RememberPenaltyReason(LMURaceControlStream.Line line)
        {
            if (line == null || !string.Equals(line.Tag, "Penalty", StringComparison.OrdinalIgnoreCase)) return;
            var m = DriverAttribute.Match(line.Text ?? "");
            if (!m.Success) return;
            string reason = Clip(LMURaceControlStream.Plain(line.Text));
            if (reason.Length == 0) return;
            lock (reasons)
            {
                if (reasons.Count > 64) reasons.Clear();
                reasons[m.Groups["n"].Value.Trim()] = new KeyValuePair<string, DateTime>(reason, line.Utc);
            }
        }

        internal static string PenaltyReason(string name, DateTime now)
        {
            if (name == null) return null;
            lock (reasons)
            {
                KeyValuePair<string, DateTime> r;
                return reasons.TryGetValue(name.Trim(), out r) && now >= r.Value && now - r.Value <= TimeSpan.FromSeconds(30) ? r.Key : null;
            }
        }

        internal static void Reset() { lock (reasons) reasons.Clear(); }
    }
}
