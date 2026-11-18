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
                if (d == null || !DateTime.TryParse((string)j["utc"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc)) continue;
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
                        break;
                }
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

        private static string Seconds(TimeSpan t) => Math.Round(t.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s";
    }
}
