using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Ein Telemetrie-Bild aus der Live-Diagnose (etwa 20 pro Sekunde).</summary>
    internal sealed class ReplayFrame
    {
        public DateTime Utc;
        public string SessionType, Phase, Flag;
        public int Lap, Position;
        public bool InPit, LapValid;
        public float Speed, Distance, Fuel = -1, GapFront = -1, GapBehind = -1, LapTime = -1, TimeRemaining = -1;
    }

    /// <summary>Eine gesprochene bzw. eingereihte Funkansage aus der Live-Diagnose.</summary>
    internal sealed class ReplayRadio
    {
        public DateTime Utc;
        public string Topic, Speaker, Text;
        public int Priority;
    }

    internal sealed class ReplaySession
    {
        public string Game, Track;
        public readonly List<ReplayFrame> Frames = new List<ReplayFrame>();
        public readonly List<ReplayRadio> Radio = new List<ReplayRadio>();
        /// <summary>RRCA 2.0 (7/10): Zeitpunkte der Funkkette je Ereignis-ID und gemessene Dauern (TTS, Sprachmodell, Antwort).</summary>
        public readonly Dictionary<string, DateTime> Detected = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        public readonly Dictionary<string, DateTime> Enqueued = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        public readonly Dictionary<string, DateTime> AudioStarted = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        public readonly List<double> TtsMs = new List<double>(), ApiMs = new List<double>(), AnswerMs = new List<double>();
        internal readonly List<DateTime> PttReleased = new List<DateTime>(), AnyAudioStart = new List<DateTime>();
    }

    internal sealed class AuditFinding
    {
        public DateTime Utc;
        public string Rule, Detail;
        public override string ToString() => Utc.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + Rule + ": " + Detail;
    }

    /// <summary>
    /// C8 Aufnehmen und Abspielen: liest eine Aufnahme der Live-Diagnose (JSON-Zeilen) wieder ein. Die Telemetrie-Bilder
    /// lassen sich als <see cref="GameStateData"/> abspielen, die Funkansagen werden gegen die Funkregeln geprüft
    /// (<see cref="RadioAudit"/>). So lassen sich Timing und Häufigkeit ohne Fahren prüfen.
    /// </summary>
    internal static class DiagnosticReplay
    {
        internal static ReplaySession Load(string path) => Load(File.ReadLines(path));

        internal static ReplaySession Load(IEnumerable<string> lines)
        {
            var session = new ReplaySession();
            foreach (var line in lines ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                JObject j;
                try { j = JObject.Parse(line); } catch (Exception) { continue; } // abgeschnittene letzte Zeile
                string kind = (string)j["kind"];
                var d = j["data"] as JObject;
                if (d == null || !Utc(j["utc"], out var utc)) continue;
                switch (kind)
                {
                    case "session.start":
                        session.Game = (string)d["game"] ?? session.Game; session.Track = (string)d["track"] ?? session.Track;
                        break;
                    case "telemetry":
                        session.Frames.Add(new ReplayFrame
                        {
                            Utc = utc, SessionType = (string)d["session_type"], Phase = (string)d["phase"], Flag = (string)d["flag"],
                            Lap = Int(d["lap"]), Position = Int(d["position"]), InPit = Bool(d["in_pit"]), LapValid = Bool(d["current_lap_valid"], true),
                            Speed = Float(d["speed_mps"], 0), Distance = Float(d["distance_m"], -1), Fuel = Float(d["fuel_l"], -1),
                            GapFront = Float(d["gap_front_s"], -1), GapBehind = Float(d["gap_behind_s"], -1), LapTime = Float(d["lap_time_s"], -1),
                            TimeRemaining = Float(d["time_remaining_s"], -1),
                        });
                        break;
                    case "radio.enqueue":
                        session.Radio.Add(new ReplayRadio { Utc = utc, Topic = (string)d["topic"], Speaker = (string)d["speaker"], Text = (string)d["text"], Priority = Int(d["priority"]) });
                        First(session.Enqueued, (string)d["event_id"], utc);
                        break;
                    case "decision":
                        if ((string)d["status"] == "detected") First(session.Detected, (string)d["event_id"], utc);
                        break;
                    case "audio.driver_submit":
                        First(session.AudioStarted, (string)d["event_id"], utc);
                        session.AnyAudioStart.Add(utc);
                        break;
                    case "tts.end":
                        if ((string)d["status"] == "completed" || (string)d["status"] == "ok" || d["status"] == null) AddMs(session.TtsMs, d["duration_ms"]);
                        break;
                    case "api.end":
                        AddMs(session.ApiMs, d["duration_ms"]);
                        break;
                    case "ptt.released":
                        session.PttReleased.Add(utc);
                        break;
                }
            }
            // Antwortzeit: Taste losgelassen bis zum nächsten Audiostart (höchstens 30 s, sonst keine Antwort).
            session.AnyAudioStart.Sort();
            foreach (var released in session.PttReleased)
            {
                int i = session.AnyAudioStart.BinarySearch(released);
                if (i < 0) i = ~i;
                if (i < session.AnyAudioStart.Count && session.AnyAudioStart[i] - released <= TimeSpan.FromSeconds(30))
                    session.AnswerMs.Add((session.AnyAudioStart[i] - released).TotalMilliseconds);
            }
            session.Frames.Sort((a, b) => a.Utc.CompareTo(b.Utc));
            session.Radio.Sort((a, b) => a.Utc.CompareTo(b.Utc));
            return session;
        }

        /// <summary>Ein Bild als Spielzustand für Wächter-Tests. Nur die aufgezeichneten Felder sind gesetzt.</summary>
        internal static GameStateData ToGameState(ReplayFrame f, int sessionId = 1)
        {
            var gs = new GameStateData(f.Utc.Ticks);
            var s = gs.SessionData;
            s.SessionId = sessionId;
            if (Enum.TryParse(f.SessionType ?? "", out SessionType type)) s.SessionType = type;
            if (Enum.TryParse(f.Phase ?? "", out SessionPhase phase)) s.SessionPhase = phase;
            if (Enum.TryParse(f.Flag ?? "", out FlagEnum flag)) s.Flag = flag;
            s.CompletedLaps = f.Lap; s.ClassPosition = s.OverallPosition = f.Position; s.CurrentLapIsValid = f.LapValid;
            if (f.TimeRemaining >= 0) s.SessionTimeRemaining = f.TimeRemaining;
            if (f.GapFront >= 0) s.TimeDeltaFront = f.GapFront;
            if (f.GapBehind >= 0) s.TimeDeltaBehind = f.GapBehind;
            if (f.LapTime > 0) s.LapTimePrevious = f.LapTime;
            gs.PitData.InPitlane = f.InPit;
            gs.PositionAndMotionData.CarSpeed = f.Speed;
            if (f.Distance >= 0) gs.PositionAndMotionData.DistanceRoundTrack = f.Distance;
            if (f.Fuel >= 0) { gs.FuelData.FuelUseActive = true; gs.FuelData.FuelLeft = f.Fuel; }
            return gs;
        }

        /// <summary>Zeitstempel mit voller Auflösung: Newtonsoft liest ISO-Zeiten schon als Datum ein; ein Umweg über
        /// Text würde die Millisekunden verlieren.</summary>
        private static bool Utc(JToken t, out DateTime utc)
        {
            utc = DateTime.MinValue;
            if (t == null) return false;
            if (t.Type == JTokenType.Date) { utc = ((DateTime)t).ToUniversalTime(); return true; }
            return DateTime.TryParse((string)t, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);
        }

        private static void First(Dictionary<string, DateTime> map, string id, DateTime utc)
        {
            if (!string.IsNullOrEmpty(id) && !map.ContainsKey(id)) map[id] = utc;
        }

        private static void AddMs(List<double> list, JToken t)
        {
            float ms = Float(t, -1);
            if (ms >= 0 && ms < 600000) list.Add(ms);
        }

        private static int Int(JToken t) { try { return t == null || t.Type == JTokenType.Null ? 0 : (int)t; } catch (Exception) { return 0; } }
        private static float Float(JToken t, float fallback) { try { return t == null || t.Type == JTokenType.Null ? fallback : (float)t; } catch (Exception) { return fallback; } }
        private static bool Bool(JToken t, bool fallback = false) { try { return t == null || t.Type == JTokenType.Null ? fallback : (bool)t; } catch (Exception) { return fallback; } }
    }

    /// <summary>
    /// Prüft die Funkansagen einer Aufnahme gegen die Regeln, die Rob beim Fahren aufgefallen sind: derselbe Satz kurz
    /// hintereinander, dasselbe Thema zu oft (Bathurst: achtmal Boxenlimit), zu viele Ansagen auf einmal, Tracklimit und
    /// ungültige Runde doppelt. Sara (Spotterin) ist von der Häufungsregel ausgenommen.
    /// </summary>
    internal static class RadioAudit
    {
        internal static readonly TimeSpan SameTextWindow = TimeSpan.FromSeconds(60), TopicWindow = TimeSpan.FromMinutes(5),
            CrowdWindow = TimeSpan.FromSeconds(30), TwinWindow = TimeSpan.FromSeconds(20);
        internal const int TopicLimit = 3, CrowdLimit = 4;

        internal static List<AuditFinding> Check(ReplaySession session)
        {
            var findings = new List<AuditFinding>();
            var radio = session?.Radio ?? new List<ReplayRadio>();
            for (int i = 0; i < radio.Count; i++)
            {
                var r = radio[i];
                var earlier = radio.Take(i).ToList();
                var sameText = earlier.LastOrDefault(e => e.Text == r.Text && r.Utc - e.Utc < SameTextWindow);
                if (sameText != null && !(r.Topic ?? "").StartsWith("warning-flag", StringComparison.Ordinal))
                    findings.Add(new AuditFinding { Utc = r.Utc, Rule = "gleicher_satz", Detail = "\"" + r.Text + "\" nach " + Seconds(r.Utc - sameText.Utc) });
                string family = Family(r.Topic);
                int sameTopic = earlier.Count(e => Family(e.Topic) == family && r.Utc - e.Utc < TopicWindow) + 1;
                if (sameTopic == TopicLimit)
                    findings.Add(new AuditFinding { Utc = r.Utc, Rule = "thema_zu_oft", Detail = family + " " + sameTopic + "x in 5 min" });
                if (r.Speaker != "Sara")
                {
                    int crowd = earlier.Count(e => e.Speaker != "Sara" && r.Utc - e.Utc < CrowdWindow) + 1;
                    bool reported = findings.Any(f => f.Rule == "funk_zu_voll" && r.Utc - f.Utc < CrowdWindow);
                    if (crowd >= CrowdLimit && !reported)
                        findings.Add(new AuditFinding { Utc = r.Utc, Rule = "funk_zu_voll", Detail = crowd + " Ansagen in 30 s" });
                }
                if (family == "tracklimit" || family == "ungueltige-runde")
                {
                    string twin = family == "tracklimit" ? "ungueltige-runde" : "tracklimit";
                    if (earlier.Any(e => Family(e.Topic) == twin && r.Utc - e.Utc < TwinWindow))
                        findings.Add(new AuditFinding { Utc = r.Utc, Rule = "tracklimit_doppelt", Detail = "Tracklimit und ungültige Runde binnen 20 s" });
                }
            }
            return findings;
        }

        /// <summary>Themen mit Fahrzeug-/Gegner-Endung zusammenfassen (warning-native-incident-12 → warning-native-incident).</summary>
        internal static string Family(string topic)
        {
            string t = topic ?? "";
            if (t.Length == 0) return "ohne_thema";
            if (t.StartsWith("pit-limit", StringComparison.Ordinal)) return "pit-limit";
            if (t.Contains("track-limit") || t.Contains("tracklimit") || t == "warning-cut") return "tracklimit";
            if (t.Contains("invalid")) return "ungueltige-runde";
            int dash = t.LastIndexOf('-');
            return dash > 0 && t.Substring(dash + 1).All(char.IsDigit) ? t.Substring(0, dash) : t;
        }

        internal static string Report(ReplaySession session)
        {
            var findings = Check(session);
            var lines = new List<string>
            {
                "Aufnahme: " + (session.Game ?? "?") + " / " + (session.Track ?? "?") + ", " + session.Frames.Count + " Telemetrie-Bilder, " + session.Radio.Count + " Ansagen.",
                findings.Count == 0 ? "Keine Auffälligkeiten im Funk." : findings.Count + " Auffälligkeiten:",
            };
            lines.AddRange(findings.Select(f => "  " + f));
            foreach (var g in session.Radio.GroupBy(r => Family(r.Topic)).OrderByDescending(g => g.Count()).Take(8))
                lines.Add("  Thema " + g.Key + ": " + g.Count() + "x");
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Perzentil (0–100) einer Liste in ms; NaN ohne Werte.</summary>
        internal static double Percentile(IList<double> values, double p)
        {
            if (values == null || values.Count == 0) return double.NaN;
            var sorted = values.OrderBy(v => v).ToList();
            double rank = Math.Max(0, Math.Min(sorted.Count - 1, p / 100.0 * (sorted.Count - 1)));
            int lo = (int)Math.Floor(rank), hi = (int)Math.Ceiling(rank);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
        }

        /// <summary>RRCA 2.0 (7/10): Latenzen der Funkkette aus einer Aufnahme (p50/p95/p99 in ms).</summary>
        internal static Dictionary<string, List<double>> Latencies(ReplaySession s)
        {
            var detectToQueue = s.Enqueued.Where(e => s.Detected.ContainsKey(e.Key)).Select(e => (e.Value - s.Detected[e.Key]).TotalMilliseconds).Where(v => v >= 0).ToList();
            var queueToAudio = s.AudioStarted.Where(e => s.Enqueued.ContainsKey(e.Key)).Select(e => (e.Value - s.Enqueued[e.Key]).TotalMilliseconds).Where(v => v >= 0).ToList();
            var detectToAudio = s.AudioStarted.Where(e => s.Detected.ContainsKey(e.Key)).Select(e => (e.Value - s.Detected[e.Key]).TotalMilliseconds).Where(v => v >= 0).ToList();
            return new Dictionary<string, List<double>>
            {
                ["erkannt_bis_eingereiht"] = detectToQueue, ["eingereiht_bis_audio"] = queueToAudio, ["erkannt_bis_audio"] = detectToAudio,
                ["tts_erzeugung"] = s.TtsMs, ["sprachmodell"] = s.ApiMs, ["frage_bis_antwort_audio"] = s.AnswerMs,
            };
        }

        internal static string LatencyReport(ReplaySession s)
        {
            var lines = new List<string> { "Latenzen (ms): Anzahl, p50, p95, p99" };
            foreach (var kv in Latencies(s))
                lines.Add("  " + kv.Key + ": " + kv.Value.Count + (kv.Value.Count == 0 ? " (keine Daten)" :
                    ", " + Math.Round(Percentile(kv.Value, 50)) + ", " + Math.Round(Percentile(kv.Value, 95)) + ", " + Math.Round(Percentile(kv.Value, 99))));
            return string.Join(Environment.NewLine, lines);
        }

        private static string Seconds(TimeSpan t) => Math.Round(t.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s";
    }
}
