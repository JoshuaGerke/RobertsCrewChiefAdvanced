using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    internal enum RacePhase { NotRacing, PreStart, Start, Early, Middle, Late, Final, LastLap, Finished }
    internal enum RaceAttention { Unknown, Pit, Caution, Battle, Traffic, Open, Calm }
    internal enum RaceFocus { None, Safe, Defend, Attack, BringHome, Rhythm }
    internal enum MindVerdict { Admit, Hold, Drop }

    /// <summary>Ein unveränderlicher Lagebericht des Rennkopfs. Wird pro Tick neu veröffentlicht und darf von jedem Thread gelesen werden.</summary>
    internal sealed class RaceSituation
    {
        public DateTime Utc;
        public int SessionId;
        public RacePhase Phase;
        public RaceAttention Attention;
        public RaceFocus Focus;
        public float GapAhead = -1, GapBehind = -1;
        public float FrontRate, RearRate;           // Sekunden pro Runde, positiv = Abstand schrumpft
        public int FrontLapsToCatch = -1, RearLapsToCatch = -1, LapsLeft = -1;
        public string FrontName, RearName;
        public bool FuelShort, IncidentsTight, FrontInPits;
        public string[] Threats = new string[0], Opportunities = new string[0];

        internal bool Fresh(DateTime now) => now >= Utc && now - Utc <= TimeSpan.FromSeconds(1.5);

        internal JObject Describe()
        {
            var o = new JObject
            {
                ["rennphase"] = PhaseName(Phase),
                ["lage"] = AttentionName(Attention),
                ["fokus"] = FocusName(Focus),
                ["gefahren"] = new JArray(Threats),
                ["chancen"] = new JArray(Opportunities),
                ["runden_uebrig"] = LapsLeft > 0 ? (JToken)LapsLeft : null,
            };
            if (FrontRate != 0) o["vorne_abstand_aenderung_s_pro_runde"] = Math.Round(-FrontRate, 2);
            if (FrontLapsToCatch > 0) o["vorne_dran_in_runden"] = FrontLapsToCatch;
            if (RearRate != 0) o["hinten_abstand_aenderung_s_pro_runde"] = Math.Round(-RearRate, 2);
            if (RearLapsToCatch > 0) o["hinten_dran_in_runden"] = RearLapsToCatch;
            o["hinweis"] = "Lokale Einschätzung des Rennkopfs aus Messwerten. Trends erst nach drei Runden gegen denselben Gegner; " +
                "'dran in Runden' ist eine Hochrechnung bei gleichbleibendem Tempo, keine Vorhersage. Negativ bei Abstandsänderung = Abstand schrumpft.";
            return o;
        }

        internal string Summary() => PhaseName(Phase) + "/" + AttentionName(Attention) + "/" + FocusName(Focus) +
            (Threats.Length > 0 ? " gefahren=" + string.Join(",", Threats) : "") + (Opportunities.Length > 0 ? " chancen=" + string.Join(",", Opportunities) : "");

        internal static string PhaseName(RacePhase p)
        {
            switch (p)
            {
                case RacePhase.PreStart: return "vor_dem_start";
                case RacePhase.Start: return "startrunde";
                case RacePhase.Early: return "fruehe_phase";
                case RacePhase.Middle: return "rennmitte";
                case RacePhase.Late: return "spaete_phase";
                case RacePhase.Final: return "schlussphase";
                case RacePhase.LastLap: return "letzte_runde";
                case RacePhase.Finished: return "beendet";
                default: return "kein_rennen";
            }
        }

        internal static string AttentionName(RaceAttention a)
        {
            switch (a)
            {
                case RaceAttention.Pit: return "boxengasse";
                case RaceAttention.Caution: return "gelbphase";
                case RaceAttention.Battle: return "zweikampf";
                case RaceAttention.Traffic: return "verkehr";
                case RaceAttention.Open: return "freie_fahrt";
                case RaceAttention.Calm: return "ruhig";
                default: return "unbekannt";
            }
        }

        internal static string FocusName(RaceFocus f)
        {
            switch (f)
            {
                case RaceFocus.Safe: return "sicher_ins_ziel";
                case RaceFocus.Defend: return "verteidigen";
                case RaceFocus.Attack: return "angreifen";
                case RaceFocus.BringHome: return "nach_hause_bringen";
                case RaceFocus.Rhythm: return "rhythmus";
                default: return "keiner";
            }
        }
    }

    /// <summary>Abstand zu demselben Klassennachbarn, einmal pro Runde gemessen.</summary>
    internal sealed class GapHistory
    {
        public string Id;
        public readonly List<float> Gaps = new List<float>();
        internal void Clear() { Gaps.Clear(); }
    }

    /// <summary>
    /// Der Renningenieur-Kopf (P6, Robs Kernwunsch vom 16.11.2026): eine zentrale Stelle, die die Rennlage laufend zusammenführt
    /// und bewertet – Rennphase, Lage um das Auto (Box, Gelb, Zweikampf, Verkehr, freie Fahrt, ruhig), Abstandstrends gegen
    /// denselben Gegner, Gefahren und Chancen – und daraus einen Fokus ableitet (sicher ankommen, verteidigen, angreifen,
    /// nach Hause bringen, Rhythmus). Vor jeder lokalen Funkmeldung entscheidet er, ob sie jetzt wichtig genug ist:
    /// zulassen, zurückhalten (später erneut) oder verwerfen (in dieser Lage nicht mehr relevant).
    /// Sicherheit, Flaggen, Gefahren, eigene Rundenzeiten und Platzwechsel werden nie zurückgehalten.
    /// </summary>
    internal sealed class RaceMind
    {
        private static RaceSituation latest;
        /// <summary>Zuletzt veröffentlichte Lage (für Analysewerkzeug und Zwischenresümee); null außerhalb von Rennen.</summary>
        internal static RaceSituation Latest => Volatile.Read(ref latest);

        internal readonly GapHistory Front = new GapHistory(), Rear = new GapHistory();
        /// <summary>Runde der letzten Abstandsmessung; Beobachter reagieren einmal je neuem Wert.</summary>
        internal int SampledLap { get; private set; } = -1;

        private int session = int.MinValue;
        private DateTime battleUntil, trafficUntil;
        private bool fuelShort;
        private string lastSummary;

        internal void Reset()
        {
            session = int.MinValue; SampledLap = -1; battleUntil = trafficUntil = lastPositionNews = DateTime.MinValue; fuelShort = false; lastSummary = null;
            Front.Id = Rear.Id = null; Front.Clear(); Rear.Clear();
            Volatile.Write(ref latest, null);
        }

        // ---------- Bewertung ----------

        internal static RacePhase PhaseOf(SessionData s)
        {
            if (s.SessionType != SessionType.Race) return RacePhase.NotRacing;
            switch (s.SessionPhase)
            {
                case SessionPhase.Garage: case SessionPhase.Gridwalk: case SessionPhase.Formation: case SessionPhase.Countdown: return RacePhase.PreStart;
                case SessionPhase.Finished: return RacePhase.Finished;
                case SessionPhase.Unavailable: return RacePhase.NotRacing;
            }
            int left = LapWatch.LapsLeft(s);
            if (left == 1) return RacePhase.LastLap;
            if (s.CompletedLaps < 1) return RacePhase.Start;
            if (left == 2) return RacePhase.Final;
            double progress = RaceSummaryWatch.Progress(s);
            if (progress < 0) return RacePhase.Middle;
            return progress < .25 ? RacePhase.Early : progress < .75 ? RacePhase.Middle : RacePhase.Late;
        }

        /// <summary>Einordnung der Lage. Zweikampf hält 3 s, Verkehr 2 s nach, damit die Lage nicht bei jedem Zehntel kippt.</summary>
        internal RaceAttention AttentionOf(GameStateData gs, DateTime now, bool overlap, float ahead, float behind, TrackPositionSnapshot track)
        {
            var s = gs.SessionData;
            if (gs.PitData.InPitlane) return RaceAttention.Pit;
            if (s.SessionPhase == SessionPhase.FullCourseYellow || gs.FlagData.isLocalYellow) return RaceAttention.Caution;
            if (overlap || ahead > .05f && ahead < 1f || behind > .05f && behind < 1f) battleUntil = now.AddSeconds(3);
            if (now < battleUntil) return RaceAttention.Battle;
            float speed = gs.PositionAndMotionData.CarSpeed;
            bool near = ahead > .05f && ahead < 2.5f || behind > .05f && behind < 2.5f;
            if (!near && speed > 5 && track != null && now >= track.Utc && now - track.Utc <= TimeSpan.FromSeconds(1))
                foreach (var car in track.Cars)
                    if (!car.InPits && car.SpatiallyConsistent && car.RouteGapM.HasValue &&
                        Math.Abs(car.RouteGapM.Value) <= speed * 2.5f && Math.Abs(car.RouteGapM.Value) >= .5)
                    { near = true; break; }
            if (near) trafficUntil = now.AddSeconds(2);
            if (now < trafficUntil) return RaceAttention.Traffic;
            return RaceSummaryWatch.Calm(gs, ahead, behind) ? RaceAttention.Calm : RaceAttention.Open;
        }

        internal static RaceFocus FocusOf(RaceSituation x)
        {
            if (x.Phase == RacePhase.NotRacing || x.Phase == RacePhase.PreStart || x.Phase == RacePhase.Finished) return RaceFocus.None;
            if (x.IncidentsTight) return RaceFocus.Safe;
            bool rearThreat = x.GapBehind > 0 && x.GapBehind < 1.2f || x.RearLapsToCatch > 0 && x.RearLapsToCatch <= 3 &&
                (x.LapsLeft <= 0 || x.RearLapsToCatch < x.LapsLeft);
            bool frontChance = x.GapAhead > 0 && x.GapAhead < 1.5f || x.FrontLapsToCatch > 0 && x.FrontLapsToCatch <= 3 &&
                (x.LapsLeft <= 0 || x.FrontLapsToCatch < x.LapsLeft);
            if (rearThreat && !frontChance) return RaceFocus.Defend;
            if (frontChance) return RaceFocus.Attack;
            if (rearThreat) return RaceFocus.Defend;
            if (x.Phase == RacePhase.Final || x.Phase == RacePhase.LastLap) return RaceFocus.BringHome;
            return RaceFocus.Rhythm;
        }

        internal void Observe(GameStateData gs, DateTime now, bool overlap, RaceGapMeasurement front, RaceGapMeasurement rear,
            TrackPositionSnapshot track, Func<RaceDataContext> context)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race) { if (session != int.MinValue) Reset(); return; }
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            float ahead = Seconds(front), behind = Seconds(rear);
            var phase = PhaseOf(s);

            if (s.SessionPhase == SessionPhase.Green && s.IsNewLap && s.CompletedLaps != SampledLap && s.CompletedLaps >= 1)
            {
                SampledLap = s.CompletedLaps;
                bool ownPitLap = gs.PitData.InPitlane || gs.PitData.OnOutLap || gs.PitData.OnInLap;
                Sample(Front, RaceDataTools.Neighbour(gs, true), front, ownPitLap);
                Sample(Rear, RaceDataTools.Neighbour(gs, false), rear, ownPitLap);
                var fuel = FuelEstimate.From(context(), 2);
                fuelShort = fuel != null && !fuel.ProvisionalConsumption && fuel.AdditionalWithoutReserve > 0;
            }

            var x = new RaceSituation { Utc = now, SessionId = s.SessionId, Phase = phase, GapAhead = ahead, GapBehind = behind, LapsLeft = LapWatch.LapsLeft(s) };
            x.Attention = phase == RacePhase.NotRacing || phase == RacePhase.PreStart || phase == RacePhase.Finished
                ? RaceAttention.Unknown : AttentionOf(gs, now, overlap, ahead, behind, track);
            var frontCar = RaceDataTools.Neighbour(gs, true);
            var rearCar = RaceDataTools.Neighbour(gs, false);
            if (frontCar != null && Front.Id == OpponentStopLog.Key(frontCar))
            {
                x.FrontRate = GapTrendWatch.ClosingRate(Front.Gaps);
                if (ahead > 0) x.FrontLapsToCatch = GapTrendWatch.LapsToCatch(ahead, x.FrontRate);
            }
            if (rearCar != null && Rear.Id == OpponentStopLog.Key(rearCar))
            {
                x.RearRate = GapTrendWatch.ClosingRate(Rear.Gaps);
                if (behind > 0) x.RearLapsToCatch = GapTrendWatch.LapsToCatch(behind, x.RearRate);
            }
            x.FrontName = frontCar?.DriverRawName; x.RearName = rearCar?.DriverRawName;
            x.FrontInPits = frontCar != null && frontCar.InPits && s.ClassPosition > 1;
            x.FuelShort = fuelShort;
            x.IncidentsTight = IncidentPointsWatch.Known(s) && IncidentPointsWatch.Level(s.CurrentIncidentCount, s.MaxIncidentCount) >= 3;

            var threats = new List<string>();
            if (x.Attention == RaceAttention.Caution) threats.Add("gelbphase");
            if (behind > .05f && behind < 1f) threats.Add("hintermann_direkt_dran");
            else if (x.RearLapsToCatch > 0 && x.RearLapsToCatch <= 5 && (x.LapsLeft <= 0 || x.RearLapsToCatch < x.LapsLeft)) threats.Add("hintermann_holt_auf");
            if (x.IncidentsTight) threats.Add("vorfallpunkte_knapp");
            if (x.FuelShort) threats.Add("sprit_reicht_nicht");
            var chances = new List<string>();
            if (ahead > .05f && ahead < 1f && s.ClassPosition > 1) chances.Add("vordermann_in_reichweite");
            else if (x.FrontLapsToCatch > 0 && (x.LapsLeft <= 0 || x.FrontLapsToCatch < x.LapsLeft)) chances.Add("vordermann_einholbar");
            if (x.FrontInPits) chances.Add("vordermann_in_der_box");
            x.Threats = threats.ToArray(); x.Opportunities = chances.ToArray();
            x.Focus = FocusOf(x);
            Volatile.Write(ref latest, x);

            string summary = x.Summary();
            if (summary != lastSummary)
            {
                lastSummary = summary;
                LiveDiagnostics.Decision("race-mind", "state", "measured_phase_gaps_trends", summary);
            }
        }

        private static float Seconds(RaceGapMeasurement m) => m != null && m.IsRaceGap && m.Seconds > 0 && m.Seconds < 600 ? m.Seconds : -1;

        private static void Sample(GapHistory h, OpponentData op, RaceGapMeasurement gap, bool ownPitLap)
        {
            string id = op == null ? null : OpponentStopLog.Key(op);
            if (id != h.Id) { h.Clear(); h.Id = id; }
            // Nur derselbe Gegner, keine Boxrunden, keine reinen Streckenmeter-Schätzungen.
            if (op == null || ownPitLap || op.InPits || gap == null || !gap.IsRaceGap || gap.Kind == RaceGapKind.TrackDistanceEstimate ||
                !(gap.Seconds > 0) || gap.Seconds > 60) { h.Clear(); return; }
            h.Gaps.Add(gap.Seconds);
            if (h.Gaps.Count > 5) h.Gaps.RemoveAt(0);
        }

        // ---------- Entscheidung: was ist jetzt wichtig? ----------

        /// <summary>Meldungen, die nie zurückgehalten werden: alles ab Priorität 6, Warnungen, Box, Sicherheit und Regeln.</summary>
        internal static bool Essential(ObservedRaceCall call)
        {
            string t = call.Topic ?? "";
            if (call.Priority >= 6) return true;
            if (t.StartsWith("warning-", StringComparison.Ordinal) || t.StartsWith("pit-", StringComparison.Ordinal) && t != "pit-stop-time") return true;
            return t == "incident-points" || t == "race-hazard-ahead" || t == "race-traffic-rear" || t == "race-disqualified";
        }

        /// <summary>Hintergrundwissen über andere Autos, Statistik und Komfortmeldungen – im Zweikampf und in der Schlussphase unwichtig.</summary>
        internal static bool Background(ObservedRaceCall call)
        {
            string t = call.Topic ?? "";
            return t.StartsWith("field-", StringComparison.Ordinal) || t.StartsWith("reader-", StringComparison.Ordinal) ||
                t == "stint-consistency" || t == "status" || t == "summary" || t == "rival-best" || t == "rival-sector-loss" ||
                t == "corner-coaching" || t == "track-welcome" || t == "pit-stop-time" ||
                t.StartsWith("weather-", StringComparison.Ordinal) && call.Priority < 5;
        }

        /// <summary>Eigene Tempo-Motivation (Sektorbestzeit, nahe an der Bestzeit): passt fast immer, nur nicht mitten im Zweikampf oder unter Gelb.</summary>
        internal static bool OwnPace(string t) => t == "sector-best" || t == "near-best" || t == "own-best-pace" || t == "tyre-warm";

        /// <summary>
        /// Zentrale Freigabe. Nur bekannte Informations-Meldungen werden gesteuert; alles andere (Platzwechsel, eigene Runden,
        /// Taktik, Sprit, Flaggen, Gefahren) geht unverändert durch. Ohne frische Lage oder außerhalb des Rennens wird nichts blockiert.
        /// </summary>
        internal static MindVerdict Judge(RaceSituation x, ObservedRaceCall call, DateTime now, out string reason)
        {
            reason = null;
            if (call == null || x == null || !x.Fresh(now) || call.SessionType != SessionType.Race || x.SessionId != call.SessionId) return MindVerdict.Admit;
            if (Essential(call)) return MindVerdict.Admit;
            string t = call.Topic ?? "";
            bool lastLaps = x.Phase == RacePhase.LastLap || x.Phase == RacePhase.Finished;
            bool driverBusy = x.Attention == RaceAttention.Battle || x.Attention == RaceAttention.Caution;

            if (Background(call))
            {
                if (lastLaps || x.Phase == RacePhase.Final) { reason = "final_laps_only_own_race"; return MindVerdict.Drop; }
                // Wer gerade verteidigt oder angreift, braucht keine Nachrichten über andere Autos – die verfallen.
                if ((x.Focus == RaceFocus.Defend || x.Focus == RaceFocus.Attack) && t.StartsWith("field-", StringComparison.Ordinal))
                { reason = "focus_on_own_fight"; return MindVerdict.Drop; }
                if (x.Phase == RacePhase.Start || x.Phase == RacePhase.PreStart) { reason = "start_phase_driver_busy"; return MindVerdict.Hold; }
                if (driverBusy || x.Attention == RaceAttention.Traffic) { reason = "busy_background_waits"; return MindVerdict.Hold; }
                if (x.Attention == RaceAttention.Pit && t != "pit-stop-time") { reason = "pit_lane"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (t == "race-summary")
            {
                if (lastLaps || x.Phase == RacePhase.Final) { reason = "final_laps_no_summary"; return MindVerdict.Drop; }
                if (x.Attention != RaceAttention.Calm) { reason = "summary_needs_calm_moment"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (t == "gap-trend")
            {
                if (lastLaps) { reason = "last_lap_no_projection"; return MindVerdict.Drop; }
                if (driverBusy || x.Attention == RaceAttention.Pit || x.Phase == RacePhase.Start) { reason = "busy_projection_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (OwnPace(t) && driverBusy) { reason = "battle_or_caution"; return MindVerdict.Hold; }
            return MindVerdict.Admit;
        }

        private DateTime lastPositionNews = DateTime.MinValue;

        /// <summary>
        /// Zentrale Freigabe mit Gedächtnis (Rob 16.11., Mosport: nach dem Dreher kamen „Platz 5“, „Platz 6“, „Platz 7“, „von 7 auf 9“
        /// und „Platz 9, seit dem Start 3 verloren“ in wenigen Sekunden):
        ///  - ein einzelner Platzverlust wartet kurz, bis die Wertung steht – rasche Folgeverluste ersetzen ihn;
        ///  - Status/Zusammenfassung direkt nach einer Platzmeldung entfällt.
        /// </summary>
        internal MindVerdict Judge(ObservedRaceCall call, DateTime now, out string reason)
        {
            var verdict = Judge(Latest, call, now, out reason);
            if (verdict != MindVerdict.Admit || call == null || call.SessionType != SessionType.Race) return verdict;
            string t = call.Topic ?? "";
            if (t == "position" && call.Priority <= 2 && now - call.DetectedUtc < TimeSpan.FromSeconds(2.5))
            { reason = "position_loss_settling"; return MindVerdict.Hold; }
            if ((t == "status" || t == "summary") && now - lastPositionNews < TimeSpan.FromSeconds(20))
            { reason = "position_just_reported"; return MindVerdict.Drop; }
            if (t == "position" || t == "position-loss") lastPositionNews = now;
            return verdict;
        }
    }
}
