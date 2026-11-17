using System;
using System.Collections.Generic;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Abwechslung im Funk (Rob 17.11.2026): Jede Ansage hat 4–6 gleichwertige Formulierungen; gewählt wird zufällig, aber nie
    /// zweimal hintereinander dieselbe für dieselbe Situation (Schlüssel). In den Tests ist die Wahl fest auf die erste
    /// Formulierung gestellt (<see cref="Deterministic"/>), damit vorhandene Prüfungen des Wortlauts weiter gelten.
    /// </summary>
    internal static class RadioVariants
    {
        private static readonly object gate = new object();
        private static readonly Random random = new Random();
        private static readonly Dictionary<string, int> last = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Nur für Tests: immer die erste Formulierung.</summary>
        internal static bool Deterministic { get; set; }

        /// <summary>Kleinste erlaubte Zahl an Formulierungen je Ansage (Robs Vorgabe).</summary>
        internal const int Minimum = 4;

        /// <summary>Zufällige Formulierung ohne direkte Wiederholung je <paramref name="key"/>.</summary>
        internal static string Pick(string key, params string[] options)
        {
            if (options == null || options.Length == 0) return null;
            if (options.Length == 1 || Deterministic) return options[0];
            lock (gate)
            {
                int i = random.Next(options.Length);
                if (last.TryGetValue(key ?? "", out int previous) && i == previous) i = (i + 1 + random.Next(options.Length - 1)) % options.Length;
                last[key ?? ""] = i;
                return options[i];
            }
        }

        /// <summary>Feste Wahl über einen Zähler (für Stellen, die schon einen Variantenzähler führen).</summary>
        internal static string At(int variant, params string[] options) =>
            options == null || options.Length == 0 ? null : options[((variant % options.Length) + options.Length) % options.Length];
    }
}
