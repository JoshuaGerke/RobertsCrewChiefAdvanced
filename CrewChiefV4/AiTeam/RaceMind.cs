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
        /// <summary>C7: Rennstimmung des Fahrers aus den letzten drei Runden.</summary>
        public DriverMoodState Mood;
        public string MoodReason;
        /// <summary>Funklast: Anspannung 0–1 (geglättet) aus Abständen, Zweikampf, Verkehr, Gelb, Phase und Stimmung.</summary>
        public double Tension;
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
                ["fahrerstimmung"] = DriverMood.Name(Mood),
                ["ton"] = DriverMood.Tone(Mood),
                ["funklast"] = RadioLoad.Level(Tension),
            };
            if (Tension >= .75) o["funk_hinweis"] = "Hohe Anspannung: nur das Nötigste, ein kurzer Satz.";
            if (FrontRate != 0) o["vorne_abstand_aenderung_s_pro_runde"] = Math.Round(-FrontRate, 2);
            if (FrontLapsToCatch > 0) o["vorne_dran_in_runden"] = FrontLapsToCatch;
            if (RearRate != 0) o["hinten_abstand_aenderung_s_pro_runde"] = Math.Round(-RearRate, 2);
            if (RearLapsToCatch > 0) o["hinten_dran_in_runden"] = RearLapsToCatch;
            o["hinweis"] = "Lokale Einschätzung des Rennkopfs aus Messwerten. Trends erst nach drei Runden gegen denselben Gegner; " +
                "'dran in Runden' ist eine Hochrechnung bei gleichbleibendem Tempo, keine Vorhersage. Negativ bei Abstandsänderung = Abstand schrumpft.";
            return o;
        }

        internal string Summary() => PhaseName(Phase) + "/" + AttentionName(Attention) + "/" + FocusName(Focus) + "/" + DriverMood.Name(Mood) +
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
        /// <summary>„Ruhig“ im Sinne des Rennkopfs: 5 s frei (der Zwischenstand selbst begnügt sich mit 3 s).</summary>
        internal const float MindCalmSeconds = 5f;
        /// <summary>Höchste Wartezeit einer Hintergrundmeldung im Verkehr (ohne Zweikampf).</summary>
        internal static readonly TimeSpan TrafficHoldLimit = TimeSpan.FromSeconds(6);
        private static RaceSituation latest;
        /// <summary>Zuletzt veröffentlichte Lage (für Analysewerkzeug und Zwischenresümee); null außerhalb von Rennen.</summary>
        internal static RaceSituation Latest => Volatile.Read(ref latest);
        /// <summary>Nur für Tests: eine Lage veröffentlichen (null setzt zurück).</summary>
        internal static void Publish(RaceSituation x) => Volatile.Write(ref latest, x);

        internal readonly GapHistory Front = new GapHistory(), Rear = new GapHistory();
        /// <summary>Runde der letzten Abstandsmessung; Beobachter reagieren einmal je neuem Wert.</summary>
        internal int SampledLap { get; private set; } = -1;

        private int session = int.MinValue;
        private DateTime battleUntil, trafficUntil;
        private bool fuelShort;
        private string lastSummary, moodReason;
        private DriverMoodState mood;
        private DateTime lastFrustratedCoaching = DateTime.MinValue;
        /// <summary>Funklast: Anspannung und Sprechzeit entscheiden, wie viel gerade gefunkt wird.</summary>
        internal readonly RadioLoad Load = new RadioLoad();

        /// <summary>Vom Dienst bei jedem Sprechbeginn: zählt die Sprechzeit für die Funklast.</summary>
        internal void NoteSpoken(string topic, string text, DateTime now) => Load.NoteSpoken(topic, text, now);

        internal void Reset()
        {
            session = int.MinValue; SampledLap = -1; battleUntil = trafficUntil = lastPositionNews = DateTime.MinValue; fuelShort = false; lastSummary = null; mood = DriverMoodState.Neutral; moodReason = null; lastFrustratedCoaching = DateTime.MinValue; Load.Reset();
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
            return RaceSummaryWatch.Calm(gs, ahead, behind, MindCalmSeconds) ? RaceAttention.Calm : RaceAttention.Open;
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
                var ctx = context();
                var fuel = FuelEstimate.From(ctx, 2);
                fuelShort = fuel != null && !fuel.ProvisionalConsumption && fuel.AdditionalWithoutReserve > 0;
                bool tight = IncidentPointsWatch.Known(s) && IncidentPointsWatch.Level(s.CurrentIncidentCount, s.MaxIncidentCount) >= 3;
                mood = DriverMood.Assess(ctx?.Memory?.GetLaps(), tight, out moodReason);
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
            x.Mood = mood; x.MoodReason = moodReason;
            x.Tension = Load.Update(RadioLoad.RawTension(ahead, behind, overlap, x.Attention, phase, mood), now);
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
        /// <summary>Ein Bericht aus den Einstellungen „Regelmäßige Funkberichte“.</summary>
        internal static bool RoutineReport(ObservedRaceCall call) => call?.Evidence == "configured_interval_current_measurements";
        /// <summary>Höchste Wartezeit eines eingestellten Berichts im Zweikampf.</summary>
        internal static readonly TimeSpan RoutineBattleHold = TimeSpan.FromSeconds(8);

        internal static bool Background(ObservedRaceCall call)
        {
            string t = call.Topic ?? "";
            return t.StartsWith("field-", StringComparison.Ordinal) || t.StartsWith("reader-", StringComparison.Ordinal) ||
                t == "stint-consistency" || t == "status" || t == "summary" || t == "rival-best" || t == "rival-sector-loss" ||
                t == "corner-coaching" || t == "track-welcome" || t == "pit-stop-time" ||
                t.StartsWith("weather-", StringComparison.Ordinal) && call.Priority < 5;
        }

        /// <summary>Eigene Tempo-Motivation (Sektorbestzeit, nahe an der Bestzeit): passt fast immer, nur nicht mitten im Zweikampf oder unter Gelb.</summary>
        internal static bool OwnPace(string t) => t == "sector-best" || t == "near-best" || t == "own-best-pace" || t == "lap-on-pace" || t == "tyre-warm";

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
            // Rob 21.11.: eingestellte Berichte, die schon zu lange warten, kommen in der nächsten freien Lücke – auch im
            // Zweikampf oder Verkehr. Nur unter Gelb und in der Box warten sie weiter, in der letzten Runde entfallen sie.
            if (call.Guaranteed)
            {
                if (lastLaps) { reason = "last_lap_only_essentials"; return MindVerdict.Drop; }
                if (x.Attention == RaceAttention.Caution || x.Attention == RaceAttention.Pit) { reason = "caution_or_pit_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            // AMS2 Spa 23.11. (Rob: „zum zigsten Mal“): Der eingestellte Rennstatus galt als Hintergrundmeldung und wartete, solange
            // irgendein Auto innerhalb von 2,5 s fuhr – mit 14 Autos fast immer; 27-mal zurückgestellt. Ein fälliger Bericht aus
            // Robs Einstellungen ist kein Hintergrund: Verkehr hält ihn nicht auf, ein Zweikampf höchstens kurz.
            if (RoutineReport(call))
            {
                if (x.Phase == RacePhase.Finished) { reason = "race_finished"; return MindVerdict.Drop; }
                if (x.Attention == RaceAttention.Caution || x.Attention == RaceAttention.Pit || x.Phase == RacePhase.PreStart)
                { reason = "caution_or_pit_waits"; return MindVerdict.Hold; }
                bool battleWaited = call.DetectedUtc != DateTime.MinValue && now >= call.DetectedUtc && now - call.DetectedUtc >= RoutineBattleHold;
                if (x.Attention == RaceAttention.Battle && !battleWaited) { reason = "routine_waits_for_battle_gap"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            bool driverBusy = x.Attention == RaceAttention.Battle || x.Attention == RaceAttention.Caution;

            // C7 (Rob 18.11.): Kritik bleibt drin, nur das Timing folgt der Stimmung. Unter Druck wartet sie, bis Luft ist;
            // frustriert kommt höchstens ein Punkt auf einmal (Taktung im Rennkopf) und aufbauend formuliert.
            if (DriverMood.IsCoaching(t) && x.Mood == DriverMoodState.Pressure) { reason = "driver_under_pressure"; return MindVerdict.Hold; }
            // Nachfragen nach Platzverlust oder Dreher kommen auch, wenn noch Autos in der Nähe sind – nur nicht unter Gelb oder in der Box.
            // LMU 22.11. (Rob): „Ich hatte gerade Dubus überholt – keine Reaktion.“ Nach dem eigenen Überholen ist der Gegner direkt
            // hinter Rob, die Lage heißt Zweikampf – genau dann gehört die Bewertung des Manövers in den Funk. Sie wartet nur
            // unter Gelb und in der Box; auch in den letzten Runden zählt sie, erst nach der Zielflagge nicht mehr.
            if (t.StartsWith("judge-", StringComparison.Ordinal) && RadioLoad.OwnPositionChange(t))
            {
                if (x.Phase == RacePhase.Finished) { reason = "race_finished"; return MindVerdict.Drop; }
                if (x.Attention == RaceAttention.Caution || x.Attention == RaceAttention.Pit) { reason = "caution_or_pit_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (t == "judge-dropped" || t == "judge-spin")
            {
                if (lastLaps) { reason = "last_lap_only_essentials"; return MindVerdict.Drop; }
                if (x.Attention == RaceAttention.Caution || x.Attention == RaceAttention.Pit) { reason = "caution_or_pit_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }

            // RaceRoom Monza 22.11.: Beide Klassenbestzeiten (Duval 1:57,397, Molck 1:56,978) warteten im Dauerzweikampf 19- bzw.
            // 19-mal und verfielen. Rob: „Immer dann, wenn jemand eine Bestzeit fährt.“ Sie kommt jetzt auch im Zweikampf und in der
            // Schlussphase; nur Gelb, Box und Start lassen sie kurz warten, in der letzten Runde entfällt sie. Seite an Seite wartet
            // sie wie jede Meldung auf eine Lücke.
            if (t == "field-fastest" || t == "field-fastest-overall")
            {
                if (lastLaps) { reason = "last_lap_only_essentials"; return MindVerdict.Drop; }
                if (x.Attention == RaceAttention.Caution || x.Attention == RaceAttention.Pit || x.Phase == RacePhase.Start || x.Phase == RacePhase.PreStart)
                { reason = "caution_pit_or_start_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            // RaceRoom Hockenheim 23.11. (Rob: „Sektorenvergleiche zum Vordermann, Zweikämpfe, näherkommen, weiterwegfahren vom
            // hinteren“): Im Pulk galt 72 % der Zeit „Zweikampf“; genau die Informationen über den eigenen Kampf warteten dann als
            // Hintergrund oder Projektion, bis sie verfielen. Im Zweikampf sind sie das Wichtigste – nur Gelb, Box und Start
            // lassen sie warten; die Funklast und die Fahrerbelastung (Kurve, Bremszone, Auto daneben) entscheiden den Moment.
            // Rob 23.11.: „Sara muss mir sofort sagen, wenn jemand ausscheidet, an die Box fährt oder rauskommt.“ RaceRoom
            // Hockenheim: „Luca Berger ist raus“ verfiel mit focus_on_own_fight. Diese Feldnachrichten sind kurz und betreffen
            // Robs Rennen; nur Gelb, die eigene Box und der Start lassen sie warten, nach der Zielflagge entfallen sie.
            if (NativeFieldEvents.SaraNews(t))
            {
                if (x.Phase == RacePhase.Finished) { reason = "race_finished"; return MindVerdict.Drop; }
                if (x.Attention == RaceAttention.Caution || x.Attention == RaceAttention.Pit || x.Phase == RacePhase.Start || x.Phase == RacePhase.PreStart)
                { reason = "caution_pit_or_start_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (RadioLoad.FightContext(t) && !lastLaps && x.Attention == RaceAttention.Battle &&
                x.Phase != RacePhase.Start && x.Phase != RacePhase.PreStart)
                return MindVerdict.Admit;
            if (Background(call))
            {
                if (lastLaps || x.Phase == RacePhase.Final) { reason = "final_laps_only_own_race"; return MindVerdict.Drop; }
                // Wer gerade verteidigt oder angreift, braucht keine Nachrichten über andere Autos – die verfallen.
                // Ausnahme: die schnellste Runde eines Gegners wartet nur (LMU 22.11.: im Rennen nie gemeldet).
                if ((x.Focus == RaceFocus.Defend || x.Focus == RaceFocus.Attack) && t.StartsWith("field-", StringComparison.Ordinal) &&
                    t != "field-fastest" && t != "field-fastest-overall" && t != "field-topspeed")
                { reason = "focus_on_own_fight"; return MindVerdict.Drop; }
                if (x.Phase == RacePhase.Start || x.Phase == RacePhase.PreStart) { reason = "start_phase_driver_busy"; return MindVerdict.Hold; }
                // RaceRoom Mantorp 22.11.: Die schnellste Runde eines Gegners wurde dreimal im Verkehr gehalten und verfiel. Verkehr
                // ist kein Zweikampf: sie kommt dann gleich, nur Zweikampf und Gelb lassen sie warten.
                bool fastest = t == "field-fastest" || t == "field-fastest-overall";
                // AMS2 Spa 23.11.: Mit 14 Autos lag fast immer ein Wagen innerhalb von 2,5 s; Tempovergleich, Stand und
                // Endphase warteten minutenlang (über 60-mal gehalten), Rob: „Ich höre nichts von euch.“ Verkehr hält eine
                // Hintergrundmeldung jetzt höchstens 6 s auf (viele verfallen nach 10 s); Zweikampf und Gelb lassen sie weiter warten.
                bool trafficWaitedEnough = call.DetectedUtc != DateTime.MinValue && now >= call.DetectedUtc && now - call.DetectedUtc >= TrafficHoldLimit;
                if (driverBusy || x.Attention == RaceAttention.Traffic && !fastest && !trafficWaitedEnough) { reason = "busy_background_waits"; return MindVerdict.Hold; }
                if (x.Attention == RaceAttention.Pit && t != "pit-stop-time") { reason = "pit_lane"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (t == "race-summary")
            {
                if (lastLaps || x.Phase == RacePhase.Final) { reason = "final_laps_no_summary"; return MindVerdict.Drop; }
                // LMU 20.11.: Mit „nur ganz ruhig“ kam in 20 Minuten Multiclass kein Zwischenstand. Frei (kein Zweikampf, kein
                // Verkehr, kein Gelb) genügt.
                if (x.Attention != RaceAttention.Calm && x.Attention != RaceAttention.Open) { reason = "summary_needs_calm_moment"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (t == "focus-brief")
            {
                // Lagewechsel erklären: nicht mitten im Zweikampf, unter Gelb, in der Box oder in der Startrunde – dann wartet es kurz.
                if (lastLaps) { reason = "last_lap_only_essentials"; return MindVerdict.Drop; }
                if (driverBusy || x.Attention == RaceAttention.Pit || x.Phase == RacePhase.Start) { reason = "busy_brief_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (t == "gap-trend")
            {
                if (lastLaps) { reason = "last_lap_no_projection"; return MindVerdict.Drop; }
                if (driverBusy || x.Attention == RaceAttention.Pit || x.Phase == RacePhase.Start) { reason = "busy_projection_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (t.StartsWith("story-", StringComparison.Ordinal))
            {
                // Rennverlauf (Rob 23.11.): nie unter Gelb, in der Box oder in der Startrunde; in der letzten Runde entfällt er.
                // Die Zweikampfdauer und „wie angekündigt“ betreffen den Kampf selbst und kommen auch im Zweikampf (die Funklast
                // wartet die enge Phase ab); freigefahren, abgesetzt und die Reaktion des Kollegen warten auf eine ruhige Lage.
                if (lastLaps) { reason = "last_lap_only_essentials"; return MindVerdict.Drop; }
                if (x.Attention == RaceAttention.Caution || x.Attention == RaceAttention.Pit || x.Phase == RacePhase.Start || x.Phase == RacePhase.PreStart)
                { reason = "caution_pit_or_start_waits"; return MindVerdict.Hold; }
                bool fightStory = t == "story-duel-front" || t == "story-duel-rear" || t == "story-kept-ahead" || t == "story-kept-behind";
                if (!fightStory && x.Attention == RaceAttention.Battle) { reason = "busy_story_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (t.StartsWith("judge-", StringComparison.Ordinal) || t.StartsWith("insight-", StringComparison.Ordinal))
            {
                // Lob, Ansporn und Mitgefühl (Rob 16.11.): nie mitten im Zweikampf, unter Gelb, in der Box oder in der Startrunde;
                // in den letzten Runden zählt nur noch das Nötigste.
                if (lastLaps) { reason = "last_lap_only_essentials"; return MindVerdict.Drop; }
                if (driverBusy || x.Attention == RaceAttention.Pit || x.Phase == RacePhase.Start) { reason = "busy_judgement_waits"; return MindVerdict.Hold; }
                return MindVerdict.Admit;
            }
            if (OwnPace(t) && driverBusy) { reason = "battle_or_caution"; return MindVerdict.Hold; }
            return MindVerdict.Admit;
        }

        private DateTime lastPositionNews = DateTime.MinValue;

        /// <summary>Gründe, aus denen eine Meldung nur wegen des Zweikampfs wartet (nicht wegen Gelb, Box oder Start).</summary>
        internal static bool BattleWait(string reason) =>
            reason == "busy_background_waits" || reason == "busy_judgement_waits" || reason == "busy_story_waits" ||
            reason == "busy_brief_waits" || reason == "busy_projection_waits" || reason == "battle_or_caution";

        /// <summary>So lange gilt ein wortgleicher Ansporn als Wiederholung.</summary>
        internal static readonly TimeSpan ColourRepeatWindow = TimeSpan.FromMinutes(5);

        /// <summary>Lob, Ansporn oder Fahrtipp, dessen ganzer Satz (Zahlen ausgenommen) eben erst zu hören war.</summary>
        internal static bool RepeatsRecentColour(ObservedRaceCall call, DateTime now)
        {
            if (call == null || string.IsNullOrWhiteSpace(call.Text)) return false;
            var c = RadioLoad.Classify(call.Topic, call.Priority);
            if (c != RadioClass.Encouragement && c != RadioClass.Coaching) return false;
            DateTime last = PhraseMemory.LastHeard(call.Text, now);
            return last != DateTime.MinValue && now - last < ColourRepeatWindow;
        }

        /// <summary>
        /// Zentrale Freigabe mit Gedächtnis (Rob 16.11., Mosport: nach dem Dreher kamen „Platz 5“, „Platz 6“, „Platz 7“, „von 7 auf 9“
        /// und „Platz 9, seit dem Start 3 verloren“ in wenigen Sekunden):
        ///  - ein einzelner Platzverlust wartet kurz, bis die Wertung steht – rasche Folgeverluste ersetzen ihn;
        ///  - Status/Zusammenfassung direkt nach einer Platzmeldung entfällt.
        /// </summary>
        internal MindVerdict Judge(ObservedRaceCall call, DateTime now, out string reason)
        {
            var verdict = Judge(Latest, call, now, out reason);
            // RaceRoom Hockenheim 23.11.: Im Dauerzweikampf warteten Wetter, Reifen, Lob und Zwischenstand, bis sie verfielen.
            // Nach einer Funkstille entscheidet die Funklast (mit gesenkter Hürde), ob im Zweikampf Platz dafür ist.
            if (verdict == MindVerdict.Hold && BattleWait(reason) && call != null && Latest != null && Latest.Fresh(now) &&
                Latest.Attention == RaceAttention.Battle && Load.SilenceEarned(now))
            { verdict = MindVerdict.Admit; reason = null; }
            if (verdict != MindVerdict.Admit || call == null || call.SessionType != SessionType.Race) return verdict;
            string t = call.Topic ?? "";
            if (t == "position" && call.Priority <= 2 && now - call.DetectedUtc < TimeSpan.FromSeconds(2.5))
            { reason = "position_loss_settling"; return MindVerdict.Hold; }
            // Startgetümmel (Mosport: vier Platzmeldungen in 30 s): in der Startrunde jede Platzmeldung 3 s setzen lassen –
            // rasche Wechsel ersetzen einander, das Startfazit nach der halben Runde fasst zusammen.
            var latest = Latest;
            if (t == "position" && latest != null && latest.SessionId == call.SessionId && latest.Fresh(now) && latest.Phase == RacePhase.Start &&
                now - call.DetectedUtc < TimeSpan.FromSeconds(3))
            { reason = "start_positions_settling"; return MindVerdict.Hold; }
            if ((t == "status" || t == "summary") && now - lastPositionNews < TimeSpan.FromSeconds(20))
            { reason = "position_just_reported"; return MindVerdict.Drop; }
            if (t == "position" || t == "position-loss") lastPositionNews = now;
            // Satzgedächtnis (Rob 23.11., Monza): Lob, Ansporn und Fahrtipps, die in den letzten Minuten wortgleich (bis auf Zahlen)
            // zu hören waren, entfallen – lieber eine Funkpause als derselbe Satz noch einmal.
            if (!call.Guaranteed && RepeatsRecentColour(call, now)) { reason = "repeat_within_minutes"; return MindVerdict.Drop; }
            // Funklast: je angespannter und je mehr schon gefunkt wurde, desto wichtiger muss eine Ansage sein.
            // Ein zugesagter Routinebericht hat schon lange genug gewartet; die Funklast hält ihn nicht noch einmal auf.
            if (!call.Guaranteed && !RoutineReport(call) && !Load.Allows(t, call.Priority, now, out string loadReason)) { reason = loadReason; return MindVerdict.Hold; }
            // Frustriert: höchstens ein Fahrtechnik-Hinweis je zwei Minuten, die übrigen entfallen.
            if (DriverMood.IsCoaching(t) && latest != null && latest.SessionId == call.SessionId && latest.Fresh(now) && latest.Mood == DriverMoodState.Frustration)
            {
                if (now - lastFrustratedCoaching < TimeSpan.FromMinutes(2)) { reason = "driver_frustrated_one_point_at_a_time"; return MindVerdict.Drop; }
                lastFrustratedCoaching = now;
            }
            return verdict;
        }
    }
}
