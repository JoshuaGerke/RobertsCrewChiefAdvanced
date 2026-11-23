using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Menschlicher Zweikampf-Kommentar (Rob 23.11., LMU Sebring: „Gute Zweikämpfe wurden nicht menschlich kommentiert, nur
    /// Daten und Fakten. Die Crew soll das Rennen wirklich lesen und mit Emotionen immer unterschiedlich kommentieren.“).
    ///
    /// Die lokale Rennlogik erkennt weiter, WAS passiert ist (Überholt, überholt worden, Duell seit Runden, freigefahren,
    /// Sektorvergleich, Abstandstrend) und bildet daraus einen festen Satz mit allen Fakten. Für diese Meldungen formuliert das
    /// schnelle Sprachmodell den Satz neu: mit der Vorgeschichte des Duells aus der Rennchronik, der Rennphase, der Stimmung des
    /// Fahrers und einem wechselnden Ton, so wie ein Renningenieur am Kommandostand mitfiebert.
    ///
    /// Sicherheitsnetz: Der neue Satz darf keine Zahl und keinen Namen enthalten, die nicht im festen Satz stehen, muss den Gegner
    /// nennen, wenn der feste Satz ihn nennt, darf nicht nach Maschine klingen und keine der letzten Formulierungen wiederholen.
    /// Kommt die Antwort nicht rechtzeitig oder fällt sie durch, wird der feste Satz gesprochen. Die Meldung geht nie verloren.
    /// </summary>
    internal static class BattleVoice
    {
        /// <summary>Längste Wartezeit auf den neuen Satz; danach spricht die Crew den festen Satz.</summary>
        internal static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2.5);
        internal const int MaxWords = 26, MaxChars = 190, RecentLines = 10;
        internal const double RepeatSimilarity = .6;

        private static readonly object gate = new object();
        private static readonly List<string> recent = new List<string>();
        private static int toneCursor;

        /// <summary>Zweikämpfe, Manöver und das Duell mit dem direkten Gegner.</summary>
        internal static bool Covers(string topic)
        {
            if (string.IsNullOrEmpty(topic)) return false;
            if (topic == "story-followup") return false; // Miks kurze Reaktion bleibt bewusst knapp
            return topic.StartsWith("story-", StringComparison.Ordinal) ||
                topic == "position" || topic == "position-loss" || topic == "judge-overtake" || topic == "judge-retake" ||
                topic == "judge-overtake-rough" || topic == "judge-overtake-countered" ||
                topic == "battle-sector" || topic == "gap-trend" || topic == "rear-pressure" || topic == "race-rear-pressure" ||
                topic == "insight-battle-ahead" || topic == "rival-topspeed" || topic == "start-verdict";
        }

        internal static void Reset() { lock (gate) { recent.Clear(); toneCursor = 0; } }

        internal static void Remember(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            lock (gate)
            {
                recent.Add(line);
                if (recent.Count > RecentLines) recent.RemoveAt(0);
            }
        }

        internal static List<string> Recent() { lock (gate) return recent.ToList(); }

        /// <summary>Worum es geht, in Worten für das Sprachmodell.</summary>
        internal static string Occasion(string topic, string text)
        {
            switch (topic)
            {
                case "position":
                    return LooksLikeLoss(text) ? "Der Fahrer hat gerade einen Platz verloren." : "Der Fahrer hat gerade einen Platz gewonnen.";
                case "position-loss": return "Der Fahrer hat mehrere Plätze verloren.";
                case "judge-overtake": return "Der Fahrer hat sauber überholt.";
                case "judge-retake": return "Der Fahrer hat sich den Platz zurückgeholt.";
                case "judge-overtake-rough": return "Der Fahrer hat überholt, aber es war hart an der Grenze.";
                case "judge-overtake-countered": return "Der Fahrer hat überholt, der Gegner hat sofort gekontert.";
                case "story-duel-front": return "Der Fahrer hängt seit mehreren Runden am Vordermann fest (Zweikampf nach vorn).";
                case "story-duel-rear": return "Der Fahrer verteidigt sich seit mehreren Runden gegen den Hintermann.";
                case "story-free": return "Der Fahrer hat sich nach einem Zweikampf vom Hintermann gelöst.";
                case "story-escaped": return "Der Vordermann ist dem Fahrer nach einem Zweikampf weggefahren.";
                case "story-kept-ahead": return "Der Fahrer hat den Vordermann eingeholt, wie vorher hochgerechnet.";
                case "story-kept-behind": return "Der Hintermann ist angekommen, wie vorher hochgerechnet.";
                case "battle-sector": return "Sektorvergleich mit dem direkten Gegner im Zweikampf.";
                case "gap-trend": return "Abstandstrend zum direkten Gegner über mehrere Runden.";
                case "rear-pressure":
                case "race-rear-pressure": return "Der Hintermann kommt in Angriffsreichweite.";
                case "insight-battle-ahead": return "Vor dem Fahrer kämpfen andere, daraus kann eine Chance werden.";
                case "rival-topspeed": return "Topspeed-Vergleich mit dem direkten Gegner.";
                case "start-verdict": return "Fazit nach dem Start.";
                default: return "Rennszene um den Fahrer.";
            }
        }

        private static bool LooksLikeLoss(string text)
        {
            string t = (text ?? "").ToLowerInvariant();
            return t.Contains("verlor") || t.Contains("vorbei an dir") || t.Contains("überholt dich") || t.Contains("hat dich") ||
                t.Contains(" lost") || t.Contains("passed you") || t.Contains("got by") || t.Contains("perdid") || t.Contains("te ha pasado") ||
                t.Contains("te adelant");
        }

        /// <summary>Wechselnder Ton passend zur Szene und zur Stimmung (Rob: „immer unterschiedlich, mit Emotionen“).</summary>
        internal static string Tone(string topic, string text, DriverMoodState mood)
        {
            string[] tones;
            bool loss = topic == "position" && LooksLikeLoss(text) || topic == "position-loss" || topic == "story-escaped";
            bool gain = topic == "position" && !loss || topic == "judge-overtake" || topic == "judge-retake" || topic == "story-kept-ahead";
            if (gain)
                tones = new[] { "begeistert, hörbar mitgerissen", "stolz und anerkennend", "jubelnd, aber sofort wieder fokussiert",
                    "trocken-anerkennend mit einem Augenzwinkern", "erleichtert und euphorisch", "voller Respekt für das Manöver" };
            else if (loss)
                tones = new[] { "aufmunternd, ruhig", "kämpferisch, der Platz ist noch nicht verloren", "gelassen, mit Blick auf die nächste Chance",
                    "verständnisvoll und motivierend", "entschlossen, gemeinsam zurückholen" };
            else if (topic == "story-free" || topic == "story-followup")
                tones = new[] { "erleichtert", "anerkennend, durchatmen", "zufrieden und ruhig", "stolz auf die Verteidigung" };
            else if (topic == "story-duel-rear" || topic == "story-kept-behind" || topic == "rear-pressure" || topic == "race-rear-pressure")
                tones = new[] { "ruhig und bestärkend", "angespannt mitfiebernd, aber klar", "entschlossen, Rücken frei halten",
                    "vertrauensvoll: du machst das", "konzentriert und knapp" };
            else
                tones = new[] { "mitfiebernd, Spannung in der Stimme", "taktisch-listig, wie ein Komplize", "anspornend",
                    "geduldig und überlegt", "kämpferisch", "neugierig auf die nächste Runde" };
            if (mood == DriverMoodState.Frustration) tones = tones.Concat(new[] { "besonders aufbauend" }).ToArray();
            if (mood == DriverMoodState.Flow) tones = tones.Concat(new[] { "locker, der Fahrer ist im Fluss" }).ToArray();
            int i;
            lock (gate) i = toneCursor++;
            return tones[i % tones.Length];
        }

        internal static string Prompt(string driverName, TeamMember member) => AiPersonas.Localize(
            "Du bist " + TeamRouter.DisplayName(member) + " am Kommandostand und funkst Fahrer " + driverName + " mitten im Rennen. " +
            "Du liest das Rennen wie ein echter Renningenieur, der mitfiebert: Du kennst die Vorgeschichte des Duells, die Rennphase und die Stimmung des Fahrers. " +
            "Aufgabe: Formuliere die KERNAUSSAGE neu, als einen oder zwei kurze, gesprochene Funksätze mit echter Emotion im angegebenen TON. " +
            "Greife, wenn es passt, die Vorgeschichte auf (zum Beispiel wie lange das Duell schon geht, ob ihr euch schon getauscht habt, ob es eine Revanche ist). " +
            "Behalte jeden Fakt der Kernaussage bei. Nenne den Gegner mit genau dem Namen aus der Kernaussage. " +
            "Keine neuen Zahlen, keine neuen Namen, keine erfundenen Details wie spät gebremst, Kurvennamen oder Reifenzustand. " +
            "Klinge wie ein Mensch, nicht wie ein Datenansager: keine Aufzählung, kein Bericht, kein Ich habe analysiert. " +
            "Wiederhole keine Formulierung aus ZULETZT_GESAGT und beginne anders als diese Sätze. " +
            "Sprich durchgehend Deutsch, in vollständigen kurzen Sätzen ohne fremdsprachigen Nachspann. " +
            "Antwort ausschließlich als JSON: {\"text\":\"höchstens 22 natürliche deutsche Wörter\"}.");

        /// <summary>Lage und Vorgeschichte für das Sprachmodell. Nur belegte Dinge, Namen nur, wenn erlaubt.</summary>
        internal static JObject Facts(string topic, string text, string tone, GameStateData gs, RaceSituation situation,
            IEnumerable<ChronicleEntry> chronicle, IList<string> rivalNames, IList<string> recentLines)
        {
            var o = new JObject
            {
                ["anlass"] = Occasion(topic, text),
                ["kernaussage"] = text,
                ["ton"] = tone,
                ["zuletzt_gesagt"] = new JArray(recentLines ?? new List<string>()),
            };
            if (gs != null)
            {
                var s = gs.SessionData;
                o["runde"] = s.CompletedLaps + 1;
                int left = LapWatch.LapsLeft(s);
                if (left > 0) o["runden_uebrig"] = left;
                o["rennabschnitt"] = left > 0 && left <= 3 ? "Schlussphase" : s.CompletedLaps < 3 ? "Anfangsphase" : "Rennmitte";
            }
            if (situation != null)
            {
                o["stimmung"] = DriverMood.Name(situation.Mood);
                o["lage"] = situation.Describe();
            }
            var history = new JArray();
            if (chronicle != null && rivalNames != null && rivalNames.Count > 0)
            {
                foreach (var e in chronicle.Where(e => e.Subject != null && rivalNames.Contains(e.Subject)).Reverse().Take(8).Reverse())
                    history.Add(new JObject { ["runde"] = e.Lap, ["art"] = e.Kind.ToString(), ["gegner"] = e.Subject,
                        ["wie"] = e.Detail ?? "" });
                int swaps = chronicle.Count(e => e.Subject != null && rivalNames.Contains(e.Subject) &&
                    (e.Kind == ChronicleKind.PositionGained || e.Kind == ChronicleKind.PositionLost) && e.Detail == ChronicleRecorder.OnTrack);
                o["platztausche_mit_gegner"] = swaps;
            }
            o["vorgeschichte_mit_gegner"] = history;
            return o;
        }

        /// <summary>Sprechbare Namen aus dem Feld, die im festen Satz vorkommen.</summary>
        internal static List<string> NamesIn(string text, IEnumerable<string> fieldNames)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(text) || fieldNames == null) return list;
            foreach (var n in fieldNames)
                if (!string.IsNullOrWhiteSpace(n) && text.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0 && !list.Contains(n)) list.Add(n);
            return list;
        }

        private static readonly Regex Number = new Regex(@"\d+(?:[.,]\d+)?", RegexOptions.Compiled);
        private static readonly Dictionary<string, string> NumberWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["zwei"] = "2", ["drei"] = "3", ["vier"] = "4", ["fünf"] = "5", ["sechs"] = "6", ["sieben"] = "7", ["acht"] = "8", ["neun"] = "9", ["zehn"] = "10",
            ["elf"] = "11", ["zwölf"] = "12", ["zehntel"] = "0,", ["hundertstel"] = "0,",
            ["two"] = "2", ["three"] = "3", ["four"] = "4", ["five"] = "5", ["six"] = "6", ["seven"] = "7", ["eight"] = "8", ["nine"] = "9", ["ten"] = "10",
            ["tenths"] = "0.", ["hundredths"] = "0.",
            ["dos"] = "2", ["tres"] = "3", ["cuatro"] = "4", ["cinco"] = "5", ["seis"] = "6", ["siete"] = "7", ["ocho"] = "8", ["nueve"] = "9", ["diez"] = "10",
            ["décimas"] = "0,", ["centésimas"] = "0,",
        };
        private static readonly string[] Machine =
        {
            "kann ich nicht", "als ki", "sprachmodell", "muss eingebaut", "keine daten", "ich habe analysiert", "laut daten", "json",
            "i can't", "i cannot", "as an ai", "language model", "no data", "no puedo", "como ia", "modelo de lenguaje", "sin datos",
        };

        /// <summary>
        /// Prüft den neuen Satz gegen den festen. Liefert den sprechbaren Satz oder null (dann bleibt der feste Satz).
        /// </summary>
        internal static string Validate(string candidate, string template, IList<string> allowedNames, IEnumerable<string> fieldNames,
            IList<string> recentLines, out string reason)
        {
            reason = null;
            string c = AiPersonas.CleanForRadio(candidate ?? "", MaxChars + 20, true)?.Trim().Trim('"', '\'', '„', '“') ?? "";
            if (c.Length < 8) { reason = "empty"; return null; }
            if (c.Length > MaxChars || c.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > MaxWords) { reason = "too_long"; return null; }
            string lower = c.ToLowerInvariant();
            if (Machine.Any(m => lower.Contains(m))) { reason = "sounds_like_machine"; return null; }
            string templ = template ?? "";
            foreach (Match m in Number.Matches(c))
            {
                string v = m.Value, alt = v.Contains(",") ? v.Replace(',', '.') : v.Replace('.', ',');
                if (templ.IndexOf(v, StringComparison.Ordinal) < 0 && templ.IndexOf(alt, StringComparison.Ordinal) < 0) { reason = "new_number:" + v; return null; }
            }
            foreach (var word in Regex.Split(lower, @"[^\p{L}]+"))
            {
                if (word.Length == 0 || !NumberWords.TryGetValue(word, out var digit)) continue;
                if (templ.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (digit.EndsWith(",") || digit.EndsWith(".")) { if (!Regex.IsMatch(templ, @"\d[.,]\d")) { reason = "new_number:" + word; return null; } continue; }
                if (!Regex.IsMatch(templ, @"(?<![\d.,])" + digit + @"(?![\d])")) { reason = "new_number:" + word; return null; }
            }
            var allowed = allowedNames ?? new List<string>();
            foreach (var n in allowed)
                if (c.IndexOf(n, StringComparison.OrdinalIgnoreCase) < 0) { reason = "rival_name_missing"; return null; }
            if (fieldNames != null)
                foreach (var n in fieldNames)
                {
                    if (string.IsNullOrWhiteSpace(n) || allowed.Any(a => string.Equals(a, n, StringComparison.OrdinalIgnoreCase))) continue;
                    if (allowed.Any(a => a.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    if (Regex.IsMatch(c, @"(?<!\p{L})" + Regex.Escape(n) + @"(?!\p{L})", RegexOptions.IgnoreCase)) { reason = "other_name:" + n; return null; }
                }
            if (recentLines != null && recentLines.Any(r => Similarity(r, c) >= RepeatSimilarity)) { reason = "repeats_recent_line"; return null; }
            if (Similarity(templ, c) >= .9) { reason = "same_as_template"; return null; }
            return c;
        }

        internal static double Similarity(string a, string b)
        {
            var x = Words(a); var y = Words(b);
            if (x.Count == 0 || y.Count == 0) return 0;
            return (double)x.Intersect(y).Count() / x.Union(y).Count();
        }

        private static HashSet<string> Words(string s) =>
            new HashSet<string>(Regex.Split((s ?? "").ToLowerInvariant(), @"[^\p{L}\p{N}]+").Where(w => w.Length > 2));

        internal static string ParseText(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;
            try { return (string)JObject.Parse(content)["text"]; }
            catch (Exception)
            {
                int a = content.IndexOf('{'), b = content.LastIndexOf('}');
                if (a >= 0 && b > a) { try { return (string)JObject.Parse(content.Substring(a, b - a + 1))["text"]; } catch (Exception) { } }
                return null;
            }
        }

        /// <summary>
        /// Holt den menschlichen Satz. <paramref name="ask"/> schickt die Nachrichten an das schnelle Sprachmodell und liefert den
        /// Antworttext. Ergebnis null: fester Satz bleibt (Grund in <paramref name="reason"/>).
        /// </summary>
        internal static async Task<Tuple<string, string>> ColorAsync(Func<JArray, CancellationToken, Task<string>> ask, string systemPrompt,
            JObject facts, string template, IList<string> allowedNames, IEnumerable<string> fieldNames, TimeSpan deadline)
        {
            var messages = new JArray
            {
                new JObject { ["role"] = "system", ["content"] = systemPrompt },
                new JObject { ["role"] = "user", ["content"] = facts.ToString(Newtonsoft.Json.Formatting.None) },
            };
            string content;
            try
            {
                using (var cts = new CancellationTokenSource(deadline))
                {
                    var work = ask(messages, cts.Token);
                    var done = await Task.WhenAny(work, Task.Delay(deadline)).ConfigureAwait(false);
                    if (done != work) { cts.Cancel(); return Tuple.Create<string, string>(null, "deadline"); }
                    content = await work.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { return Tuple.Create<string, string>(null, "deadline"); }
            catch (Exception e) { return Tuple.Create<string, string>(null, "model_error:" + e.GetType().Name); }
            string text = Validate(ParseText(content), template, allowedNames, fieldNames, Recent(), out string why);
            return Tuple.Create(text, why);
        }
    }
}
