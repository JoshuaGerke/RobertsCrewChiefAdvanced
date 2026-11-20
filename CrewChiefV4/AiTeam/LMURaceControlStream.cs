using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using rF2SharedMemory.LMUData;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rennleitung aus LMUs nativem Abbild (Rob 20.11.2026: „Die Strafen sagt LMUs interner Spotter konkret an – das müsst ihr
    /// nativ abgreifen“). Das Spiel legt bei jedem Wertungsbild die neuen Zeilen seines Ergebnis-Datenstroms ab (Strafen,
    /// Tracklimits, Vorfälle). Jede Zeile wird mitgeschrieben; Zeilen zum eigenen Auto werden eingeordnet.
    /// Der genaue Wortlaut der Zeilen ist noch nicht an einer Aufnahme bestätigt: Eingeordnet wird nur nach eindeutigen
    /// Schlüsselwörtern, alles andere bleibt Protokoll.
    /// </summary>
    internal static class LMURaceControlStream
    {
        internal sealed class Line { public DateTime Utc; public string Text, Tag, Player; public int PlayerId; }

        private static readonly ConcurrentQueue<Line> queue = new ConcurrentQueue<Line>();
        private static double lastEt = double.NaN;
        private static readonly int iEt = Marshal.OffsetOf(typeof(rF2ScoringInfo), "mCurrentET").ToInt32();
        private static readonly int vsId = Marshal.OffsetOf(typeof(rF2VehicleScoring), "mID").ToInt32(),
            vsName = Marshal.OffsetOf(typeof(rF2VehicleScoring), "mDriverName").ToInt32(), vsPlayer = Marshal.OffsetOf(typeof(rF2VehicleScoring), "mIsPlayer").ToInt32();
        internal static volatile bool Enabled = true;

        /// <summary>Aus dem kopierten Abbild: neue Zeilen einreihen (nur einmal je Wertungsbild).</summary>
        internal static int Observe(byte[] b, DateTime nowUtc)
        {
            if (!Enabled || b == null || b.Length < LMUNativeLayout.TotalSize) return 0;
            double et = BitConverter.ToDouble(b, LMUNativeLayout.ScoringOffset + iEt);
            if (et == lastEt) return 0;
            lastEt = et;
            long size = BitConverter.ToInt64(b, LMUNativeLayout.StreamSizeOffset);
            if (size <= 0 || size >= LMUNativeLayout.StreamSize) return 0;
            string text = Decode(b, LMUNativeLayout.StreamOffset, (int)size);
            string player = null; int playerId = -1;
            int scored = Math.Min(Math.Max(BitConverter.ToInt32(b, LMUNativeLayout.ScoringCountOffset), 0), LMUNativeLayout.MaxVehicles);
            for (int i = 0; i < scored; i++)
            {
                int o = LMUNativeLayout.ScoringVehiclesOffset + i * LMUNativeLayout.VehicleScoringSize;
                if (b[o + vsPlayer] == 0) continue;
                playerId = BitConverter.ToInt32(b, o + vsId);
                player = Decode(b, o + vsName, 32);
                break;
            }
            int count = 0;
            foreach (string raw in text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                queue.Enqueue(new Line { Utc = nowUtc, Text = line.Length > 400 ? line.Substring(0, 400) : line, Tag = Tag(line), Player = player, PlayerId = playerId });
                lock (recent) { recent.Add(Plain(line.Length > 400 ? line.Substring(0, 400) : line)); while (recent.Count > 12) recent.RemoveAt(0); }
                count++;
            }
            Line unused;
            while (queue.Count > 64) queue.TryDequeue(out unused);
            return count;
        }

        internal static bool TryTake(out Line line) => queue.TryDequeue(out line);
        internal static void Reset() { Line unused; while (queue.TryDequeue(out unused)) { } lastEt = double.NaN; lock (recent) recent.Clear(); }

        private static readonly List<string> recent = new List<string>();
        /// <summary>Die letzten Zeilen der Rennleitung (ohne Auszeichnungen) für das Sprachmodell.</summary>
        internal static string[] Recent { get { lock (recent) return recent.ToArray(); } }

        internal static string Decode(byte[] b, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && end < b.Length && b[end] != 0) end++;
            if (end == offset) return "";
            try { return new UTF8Encoding(false, true).GetString(b, offset, end - offset); }
            catch (ArgumentException) { return Encoding.GetEncoding(1252).GetString(b, offset, end - offset); }
        }

        private static readonly Regex TagPattern = new Regex(@"^<\s*([A-Za-z]+)", RegexOptions.CultureInvariant);
        internal static string Tag(string line) { var m = TagPattern.Match(line ?? ""); return m.Success ? m.Groups[1].Value : null; }

        private static readonly Regex Markup = new Regex(@"<[^>]*>", RegexOptions.CultureInvariant);
        private static readonly Regex IdAttribute = new Regex(@"\bid\s*=\s*""?(\d+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>Betrifft die Zeile das eigene Auto (Name im Text oder Slot-ID als Attribut)?</summary>
        internal static bool AboutPlayer(Line l)
        {
            if (l == null || string.IsNullOrEmpty(l.Text)) return false;
            if (!string.IsNullOrWhiteSpace(l.Player) && l.Text.IndexOf(l.Player.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) return true;
            var id = IdAttribute.Match(l.Text);
            return id.Success && l.PlayerId >= 0 && id.Groups[1].Value == l.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool Has(string text, params string[] words) => words.Any(w => text.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>
        /// Einordnung einer Zeile zum eigenen Auto: Art für <see cref="NativeRaceControl"/> („pen-type:dt:cut“, „pen-served“, „investigation“,
        /// „limits“) oder null. Nur eindeutige Schlüsselwörter.
        /// </summary>
        internal static string Classify(Line l)
        {
            if (!AboutPlayer(l)) return null;
            string tag = l.Tag ?? "", text = l.Text;
            bool penalty = tag.Equals("Penalty", StringComparison.OrdinalIgnoreCase) || Has(text, "penalty", "penalt");
            if (penalty && Has(text, "served", "cleared", "removed", "rescinded", "revoked")) return "pen-served";
            if (Has(text, "investigat")) return "investigation";
            if (penalty)
            {
                string type = Has(text, "drive thru", "drive-thru", "drive through", "drive-through", "drivethrough") ? "dt" :
                    Has(text, "stop/go", "stop-go", "stop and go", "stop & go", "stopgo", "stop-and-go") ? "sg" :
                    Regex.IsMatch(text, @"\b\d+\s*(s|sec|second)", RegexOptions.IgnoreCase) ? "time" : "";
                string cause = Has(text, "track limit", "cut") ? "cut" : Has(text, "speed") ? "speeding" : Has(text, "blue") ? "blue" :
                    Has(text, "false start", "jump start") ? "start" : Has(text, "red light", "pit exit") ? "red" : "";
                return "pen-type:" + (type.Length == 0 ? "any" : type) + ":" + cause;
            }
            if (tag.Equals("TrackLimits", StringComparison.OrdinalIgnoreCase) || Has(text, "track limit")) return "limits";
            return null;
        }

        /// <summary>Text ohne Auszeichnungen, für Protokoll und Sprachmodell.</summary>
        internal static string Plain(string line) => Markup.Replace(line ?? "", " ").Trim();
    }
}
