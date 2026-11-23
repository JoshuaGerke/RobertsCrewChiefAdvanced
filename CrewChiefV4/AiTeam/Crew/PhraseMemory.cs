using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Satzgedächtnis der Crew (Rob 23.11.: „In Monza kam dieselbe Überholreaktion viermal hintereinander“).
    ///
    /// Bisher merkte sich jede Stelle nur ihre eigenen zuletzt gewählten Formulierungen, und das schon beim Auswählen, nicht beim
    /// Sprechen. Wurde ein Satz gewählt, aber nicht gesprochen, oder bildeten zwei Stellen denselben Satz, kam er trotzdem wieder.
    /// Wie in Dialogsystemen aus Spielen üblich, wird das tatsächlich Gesprochene als Tatsache festgehalten („schon gesagt“), und
    /// jede Auswahl fragt dieses eine Gedächtnis ab: Formulierungen, die in den letzten Minuten zu hören waren, kommen erst wieder,
    /// wenn alle anderen auch schon dran waren.
    ///
    /// Verglichen wird das Gerüst eines Satzes: Kleinschreibung, Zahlen und Satzzeichen neutralisiert. „Platz 5, sauber.“ und
    /// „Platz 7, sauber.“ gelten als derselbe Satz.
    /// </summary>
    internal static class PhraseMemory
    {
        /// <summary>So lange gilt ein Satz als „gerade gehört“.</summary>
        internal static readonly TimeSpan Window = TimeSpan.FromMinutes(12);
        /// <summary>Eine Formulierung, deren festes Gerüst kürzer ist, wird nicht verglichen (zu allgemein, z. B. „ Sekunden“).</summary>
        internal const int MinimumSkeleton = 10;
        private const int Capacity = 600;

        private static readonly object gate = new object();
        private static readonly List<Heard> heard = new List<Heard>();
        private static readonly Regex number = new Regex(@"\d+([.,:]\d+)*", RegexOptions.Compiled);
        private static readonly Regex nonWord = new Regex(@"[^\p{L}#]+", RegexOptions.Compiled);

        private struct Heard { public string Skeleton; public DateTime Utc; public TeamMember Member; }

        /// <summary>Uhr; in Tests ersetzbar.</summary>
        internal static Func<DateTime> Clock = () => DateTime.UtcNow;

        internal static void Clear() { lock (gate) heard.Clear(); }

        /// <summary>Gerüst eines Satzes: klein, Zahlen als #, nur Buchstaben und einzelne Leerzeichen.</summary>
        internal static string Skeleton(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            string s = number.Replace(text.ToLowerInvariant(), "#");
            return nonWord.Replace(s, " ").Trim();
        }

        /// <summary>Tatsächlich gesprochen: der ganze Text und jeder einzelne Satz darin.</summary>
        internal static void NoteSpoken(TeamMember member, string text, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var parts = new List<string> { Skeleton(text) };
            parts.AddRange(Regex.Split(text, @"(?<=[.!?])\s+").Select(Skeleton));
            lock (gate)
            {
                Trim(now);
                foreach (string p in parts.Where(p => p.Length >= MinimumSkeleton).Distinct())
                    heard.Add(new Heard { Skeleton = p, Utc = now, Member = member });
                while (heard.Count > Capacity) heard.RemoveAt(0);
            }
        }

        private static void Trim(DateTime now) => heard.RemoveAll(h => h.Utc > now.AddSeconds(5) || now - h.Utc > Window);

        /// <summary>
        /// Wann zuletzt eine Formulierung mit diesem festen Text zu hören war; MinValue, wenn nicht im Fenster. Ein Bruchstück
        /// (etwa „ Du holst auf.“) zählt, wenn es in einem gehörten Satz vorkommt.
        /// </summary>
        internal static DateTime LastHeard(string fragment) => LastHeard(fragment, Clock());

        internal static DateTime LastHeard(string fragment, DateTime now)
        {
            string s = Skeleton(fragment);
            if (s.Length < MinimumSkeleton) return DateTime.MinValue;
            lock (gate)
            {
                Trim(now);
                DateTime last = DateTime.MinValue;
                foreach (var h in heard)
                    if (h.Utc > last && (h.Skeleton == s || h.Skeleton.Contains(s))) last = h.Utc;
                return last;
            }
        }

        /// <summary>Wurde eine dieser festen Textstellen (Vorlage ohne Platzhalter) zuletzt gehört? Die längste zählt.</summary>
        internal static DateTime LastHeardLiterals(IEnumerable<string> literals, DateTime now)
        {
            string longest = literals?.OrderByDescending(l => Skeleton(l).Length).FirstOrDefault();
            return longest == null ? DateTime.MinValue : LastHeard(longest, now);
        }

        /// <summary>
        /// Aus den Kandidaten (Indizes in options) die, die im Fenster nicht zu hören waren. Sind alle gehört, die am längsten
        /// zurückliegenden. Leere Eingabe bleibt leer.
        /// </summary>
        internal static List<int> Freshest(IList<int> candidates, Func<int, DateTime> lastHeard)
        {
            var result = new List<int>();
            if (candidates == null || candidates.Count == 0) return result;
            var stamps = candidates.Select(i => new { i, t = lastHeard(i) }).ToList();
            var unheard = stamps.Where(x => x.t == DateTime.MinValue).Select(x => x.i).ToList();
            if (unheard.Count > 0) return unheard;
            var oldest = stamps.Min(x => x.t);
            return stamps.Where(x => x.t == oldest).Select(x => x.i).ToList();
        }

        /// <summary>
        /// Für Satzbausteine mit fester Variantennummer (switch über variant): ausgehend von der gewünschten Nummer die erste
        /// Variante, deren Satz im Fenster nicht zu hören war; sind alle gehört, die am längsten zurückliegende.
        /// </summary>
        internal static int FreshVariant(int preferred, int count, Func<int, string> render)
        {
            if (count <= 1 || render == null) return preferred;
            DateTime now = Clock();
            int start = ((preferred % count) + count) % count;
            int best = start; DateTime oldest = DateTime.MaxValue;
            for (int k = 0; k < count; k++)
            {
                int i = (start + k) % count;
                string text;
                try { text = render(i); } catch (ArgumentException) { continue; }
                DateTime t = LastHeard(text, now);
                if (t == DateTime.MinValue) return i;
                if (t < oldest) { oldest = t; best = i; }
            }
            return best;
        }

        /// <summary>Für Diagnose: wie viele Sätze gerade im Gedächtnis sind.</summary>
        internal static int Count { get { lock (gate) { Trim(Clock()); return heard.Count; } } }
    }
}
