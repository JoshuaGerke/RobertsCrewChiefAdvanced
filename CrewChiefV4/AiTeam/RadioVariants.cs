using System;
using System.Collections.Generic;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Abwechslung im Funk (Rob 17.11.2026, RRCA 2.0 Abschnitt 6.2): Jede Ansage hat 4–6 gleichwertige Formulierungen.
    /// Gewählt wird wie aus einem Beutel: zufällig, aber ohne die zuletzt gehörten Formulierungen derselben Situation
    /// (Schlüssel) – bei sechs Varianten werden die letzten drei gemieden, bei weniger entsprechend weniger. Mit
    /// <see cref="Seed"/> ist die Folge reproduzierbar (Tests, Replay). In den Tests ist die Wahl außerdem fest auf die
    /// erste Formulierung stellbar (<see cref="Deterministic"/>), damit vorhandene Prüfungen des Wortlauts weiter gelten.
    /// </summary>
    internal static class RadioVariants
    {
        private static readonly object gate = new object();
        private static Random random = new Random();
        private static readonly Dictionary<string, List<int>> recent = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        /// <summary>Nur für Tests: immer die erste Formulierung.</summary>
        internal static bool Deterministic { get; set; }

        /// <summary>Kleinste erlaubte Zahl an Formulierungen je Ansage (Robs Vorgabe).</summary>
        internal const int Minimum = 4;

        /// <summary>Höchstens so viele zuletzt gehörte Formulierungen werden gemieden.</summary>
        internal const int AvoidLast = 3;

        /// <summary>Reproduzierbare Folge (Tests, Replay): neuer Zufallsgenerator mit Startwert, Gedächtnis geleert.</summary>
        internal static void Seed(int seed) { lock (gate) { random = new Random(seed); recent.Clear(); } }

        /// <summary>Wie viele der letzten Formulierungen bei <paramref name="count"/> Varianten gemieden werden.</summary>
        internal static int Avoided(int count) => Math.Max(0, Math.Min(AvoidLast, count / 2));

        /// <summary>Zufällige Formulierung ohne die zuletzt gehörten je <paramref name="key"/>.</summary>
        internal static string Pick(string key, params string[] options)
        {
            if (options == null || options.Length == 0) return null;
            if (options.Length == 1 || Deterministic) return options[0];
            lock (gate) return options[NextIndex(key ?? "", options.Length)];
        }

        private static int NextIndex(string key, int count)
        {
            if (!recent.TryGetValue(key, out var heard)) recent[key] = heard = new List<int>();
            heard.RemoveAll(i => i >= count);
            int avoid = Avoided(count);
            while (heard.Count > avoid) heard.RemoveAt(0);
            var free = new List<int>(count);
            for (int i = 0; i < count; i++) if (!heard.Contains(i)) free.Add(i);
            int pick = free.Count > 0 ? free[random.Next(free.Count)] : random.Next(count);
            heard.Add(pick);
            while (heard.Count > avoid) heard.RemoveAt(0);
            if (recent.Count > 512) recent.Clear(); // begrenztes Gedächtnis
            return pick;
        }

        /// <summary>Feste Wahl über einen Zähler (für Stellen, die schon einen Variantenzähler führen).</summary>
        internal static string At(int variant, params string[] options) =>
            options == null || options.Length == 0 ? null : options[((variant % options.Length) + options.Length) % options.Length];
    }
}
