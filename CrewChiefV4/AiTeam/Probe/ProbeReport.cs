using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam.Probe
{
    /// <summary>
    /// Schreibt den Messbericht: CSV für Excel (Semikolon, Dezimalkomma), JSON für die Auswertung,
    /// Zusammenfassung in einfachem Deutsch. Enthält keine Fahrernamen und keine Schlüssel.
    /// </summary>
    internal static class ProbeReport
    {
        private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static string Csv(R3EProbeAnalyzer a)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Gruppe;Kanal;Feld im Spiel;Einheit;Status;Änderungen pro Sekunde;Anteil Quellbilder mit Änderung %;Intervall p50 ms;Intervall p95 ms;Intervall max ms;Min;Max;Stichproben;Änderungen;Änderungen ohne neues Quellbild;Hinweis");
            foreach (ProbeChannel c in a.Channels)
            {
                sb.Append(Cell(c.Group)).Append(';')
                  .Append(Cell(c.Id)).Append(';')
                  .Append(Cell(c.Path)).Append(';')
                  .Append(Cell(c.Unit)).Append(';')
                  .Append(Cell(c.Status)).Append(';')
                  .Append(N(a.ChangesPerSecond(c), "0.0")).Append(';')
                  .Append(N(a.FrameShare(c) * 100, "0")).Append(';')
                  .Append(N(c.Intervals.Percentile(0.5), "0.0")).Append(';')
                  .Append(N(c.Intervals.Percentile(0.95), "0.0")).Append(';')
                  .Append(N(c.Intervals.Count == 0 ? double.NaN : c.Intervals.MaxMs, "0")).Append(';')
                  .Append(N(c.Min, "0.###")).Append(';')
                  .Append(N(c.Max, "0.###")).Append(';')
                  .Append(c.Samples.ToString(Inv)).Append(';')
                  .Append(c.Changes.ToString(Inv)).Append(';')
                  .Append(c.ChangesWithoutFrame.ToString(Inv)).Append(';')
                  .Append(Cell(c.Note ?? ""))
                  .AppendLine();
            }
            return sb.ToString();
        }

        internal static JObject Json(R3EProbeAnalyzer a, DateTime startedLocal, double cpuPercent, string build)
        {
            var channels = new JArray();
            foreach (ProbeChannel c in a.Channels)
            {
                channels.Add(new JObject
                {
                    ["id"] = c.Id, ["group"] = c.Group, ["field"] = c.Path, ["unit"] = c.Unit, ["scope"] = c.Scope.ToString(),
                    ["status"] = c.Status,
                    ["changes_per_s"] = J(a.ChangesPerSecond(c)),
                    ["frame_share"] = J(a.FrameShare(c)),
                    ["interval_p50_ms"] = J(c.Intervals.Percentile(0.5)),
                    ["interval_p95_ms"] = J(c.Intervals.Percentile(0.95)),
                    ["interval_max_ms"] = c.Intervals.Count == 0 ? null : J(c.Intervals.MaxMs),
                    ["interval_buckets"] = new JArray(c.Intervals.Buckets()),
                    ["min"] = J(c.Min), ["max"] = J(c.Max),
                    ["samples"] = c.Samples, ["changes"] = c.Changes, ["changes_without_frame"] = c.ChangesWithoutFrame,
                    ["frames"] = c.Frames, ["invalid"] = c.Invalid, ["sentinel_hits"] = c.SentinelHits,
                    ["note"] = c.Note
                });
            }
            return new JObject
            {
                ["format"] = "rrca-telemetry-probe/1",
                ["sim"] = R3EProbeAnalyzer.SimName,
                ["build"] = build,
                ["started_local"] = startedLocal.ToString("yyyy-MM-dd HH:mm:ss", Inv),
                ["written_local"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Inv),
                ["layout_check"] = a.LayoutError ?? "ok",
                ["header_check"] = a.HeaderError ?? "ok",
                ["data_version"] = a.VersionMajor + "." + a.VersionMinor,
                ["track"] = a.TrackName, ["layout"] = a.LayoutName, ["sessions_seen"] = a.Sessions,
                ["time_ms"] = new JObject { ["active"] = J(a.ActiveMs), ["paused"] = J(a.PausedMs), ["menu"] = J(a.MenuMs), ["replay"] = J(a.ReplayMs), ["stalled"] = J(a.StalledMs) },
                ["source"] = new JObject
                {
                    ["frames_per_s"] = J(a.SourceFramesPerSecond),
                    ["sim_ticks_per_s"] = J(a.SimTicksPerSecond),
                    ["frame_interval_p50_ms"] = J(a.FrameIntervals.Percentile(0.5)),
                    ["frame_interval_p95_ms"] = J(a.FrameIntervals.Percentile(0.95)),
                    ["frame_interval_max_ms"] = a.FrameIntervals.Count == 0 ? null : J(a.FrameIntervals.MaxMs),
                    ["frame_interval_buckets"] = new JArray(a.FrameIntervals.Buckets()),
                },
                ["probe"] = new JObject
                {
                    ["polls"] = a.Polls,
                    ["poll_interval_p50_ms"] = J(a.PollIntervals.Percentile(0.5)),
                    ["poll_interval_p95_ms"] = J(a.PollIntervals.Percentile(0.95)),
                    ["read_cost_mean_us"] = J(a.Polls == 0 ? double.NaN : a.CostTicksSum / (double)a.Polls / a.TicksPerMs * 1000),
                    ["read_cost_max_us"] = J(a.CostTicksMax / a.TicksPerMs * 1000),
                    ["crew_chief_cpu_percent"] = J(cpuPercent),
                },
                ["opponents_max"] = a.MaxOpponents,
                ["interval_bucket_upper_ms"] = new JArray(IntervalHistogram.UpperMs.Select(u => double.IsInfinity(u) ? (double?)null : u)),
                ["channels"] = channels,
            };
        }

        internal static string Summary(R3EProbeAnalyzer a, DateTime startedLocal, double cpuPercent)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RRCA Telemetrie-Messung – " + R3EProbeAnalyzer.SimName);
            sb.AppendLine("Gestartet: " + startedLocal.ToString("dd.MM.yyyy HH:mm:ss", De) + " · geschrieben: " + DateTime.Now.ToString("HH:mm:ss", De));
            sb.AppendLine("Strecke: " + (string.IsNullOrEmpty(a.TrackName) ? "unbekannt" : a.TrackName + (string.IsNullOrEmpty(a.LayoutName) ? "" : " – " + a.LayoutName)));
            sb.AppendLine("Zeit im Auto (gemessen): " + Sec(a.ActiveMs) + " · Pause " + Sec(a.PausedMs) + " · Menü " + Sec(a.MenuMs) +
                " · Wiederholung " + Sec(a.ReplayMs) + " · ohne neue Daten " + Sec(a.StalledMs));
            sb.AppendLine("Datenformat: Version " + a.VersionMajor + "." + a.VersionMinor + " · Strukturprüfung: " + (a.LayoutError ?? "in Ordnung") +
                " · Abgleich mit dem Spiel: " + (a.HeaderError ?? "in Ordnung"));
            sb.AppendLine();
            sb.AppendLine("QUELLE");
            sb.AppendLine("  Neue Datenbilder vom Spiel: " + N(a.SourceFramesPerSecond, "0.0") + " pro Sekunde" +
                " (Abstand typisch " + N(a.FrameIntervals.Percentile(0.5), "0.0") + " ms, 95 % unter " + N(a.FrameIntervals.Percentile(0.95), "0.0") +
                " ms, längste Lücke " + N(a.FrameIntervals.Count == 0 ? double.NaN : a.FrameIntervals.MaxMs, "0") + " ms)");
            sb.AppendLine("  Physik-Takt laut Spiel: " + N(a.SimTicksPerSecond, "0") + " Ticks pro Sekunde");
            sb.AppendLine("  Messung selbst: tastet alle " + N(a.PollIntervals.Percentile(0.5), "0.0") + " ms ab (95 % unter " + N(a.PollIntervals.Percentile(0.95), "0.0") +
                " ms) · Lesezeit Ø " + N(a.Polls == 0 ? double.NaN : a.CostTicksSum / (double)a.Polls / a.TicksPerMs * 1000, "0") + " µs · Crew Chief gesamt Ø " + N(cpuPercent, "0.0") + " % CPU");
            sb.AppendLine();
            sb.AppendLine("GEGNER UND KARTE");
            sb.AppendLine("  Höchstens " + a.MaxOpponents + " andere Autos gleichzeitig in der Liste.");
            foreach (string id in new[] { "gegner.pos_x", "gegner.strecke_m", "gegner.platz", "gegner.abstand_vorne" })
            {
                ProbeChannel c = a.Channels.FirstOrDefault(x => x.Id == id);
                if (c == null) continue;
                sb.AppendLine("  " + id + ": " + c.Status + " · je Auto " + N(a.ChangesPerSecond(c), "0.0") + " Änderungen pro Sekunde · Abstand typisch " +
                    N(c.Intervals.Percentile(0.5), "0") + " ms, 95 % unter " + N(c.Intervals.Percentile(0.95), "0") + " ms");
            }
            sb.AppendLine();
            Append(sb, "FEHLT (Spiel liefert keinen Wert)", a.Channels.Where(c => c.Status.StartsWith("fehlt", StringComparison.Ordinal) || c.Status == "ungültig"));
            Append(sb, "UNVERÄNDERT WÄHREND DER MESSUNG (vorhanden, aber nicht bewegt – kann an der Fahrsituation liegen)", a.Channels.Where(c => c.Status == "konstant"));
            Append(sb, "LANGSAMER ALS DIE QUELLE (ändert sich in weniger als der Hälfte der Datenbilder)", a.Channels.Where(c => c.Status == "vorhanden" && a.FrameShare(c) < 0.5 && Moving(c)));
            sb.AppendLine("Hinweis: Alle Zahlen sind gemessen. Wie oft sich ein Wert ändert, hängt auch davon ab, ob er sich bewegen muss");
            sb.AppendLine("(Sprit im Stand, Platz ohne Überholmanöver). Die vollständige Tabelle steht in der CSV-Datei.");
            return sb.ToString();
        }

        // Werte, die sich bei Fahrt fast in jedem Bild ändern sollten
        private static bool Moving(ProbeChannel c)
        {
            return c.Id.StartsWith("bewegung.", StringComparison.Ordinal) || c.Id.StartsWith("eingabe.", StringComparison.Ordinal) ||
                c.Id == "motor.drehzahl" || c.Id.StartsWith("gegner.pos", StringComparison.Ordinal) || c.Id == "gegner.strecke_m" ||
                c.Id == "gegner.tempo" || c.Id.StartsWith("eigen_liste.", StringComparison.Ordinal) || c.Id == "runde.strecke_m" ||
                c.Id.StartsWith("reifen.drehzahl", StringComparison.Ordinal) || c.Id.StartsWith("fahrwerk.", StringComparison.Ordinal);
        }

        private static void Append(StringBuilder sb, string title, IEnumerable<ProbeChannel> channels)
        {
            var list = channels.ToList();
            if (list.Count == 0) return;
            sb.AppendLine(title);
            foreach (ProbeChannel c in list)
                sb.AppendLine("  " + c.Id + " (" + c.Path + ")" + (c.Status == "vorhanden" ? " · " + N(c.Frames == 0 ? double.NaN : 100.0 * c.Changes / c.Frames, "0") + " % der Bilder" : ""));
            sb.AppendLine();
        }

        /// <summary>Schreibt alle drei Dateien; erst in Zwischendateien, dann austauschen.</summary>
        internal static void Write(string folder, string baseName, R3EProbeAnalyzer a, DateTime startedLocal, double cpuPercent, string build)
        {
            Directory.CreateDirectory(folder);
            Replace(Path.Combine(folder, baseName + ".csv"), Csv(a), new UTF8Encoding(true));
            Replace(Path.Combine(folder, baseName + ".json"), Json(a, startedLocal, cpuPercent, build).ToString(), new UTF8Encoding(false));
            Replace(Path.Combine(folder, baseName + "_Zusammenfassung.txt"), Summary(a, startedLocal, cpuPercent), new UTF8Encoding(true));
        }

        private static void Replace(string path, string content, Encoding encoding)
        {
            string temp = path + ".tmp";
            File.WriteAllText(temp, content, encoding);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }

        private static string Cell(string s)
        {
            if (s.IndexOf(';') < 0 && s.IndexOf('"') < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        private static string N(double v, string format)
        {
            return double.IsNaN(v) || double.IsInfinity(v) ? "–" : v.ToString(format, De);
        }

        private static JToken J(double v)
        {
            return double.IsNaN(v) || double.IsInfinity(v) ? JValue.CreateNull() : new JValue(Math.Round(v, 4));
        }

        private static string Sec(double ms) { return N(ms / 1000.0, "0") + " s"; }
    }
}
