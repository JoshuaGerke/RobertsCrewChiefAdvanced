using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Aussprache von Fahrernamen. Viele Sims liefern Namen ganz in Großbuchstaben („JAH TESÏ“, RaceRoom Mantorp 21.11.);
    /// die Stimme buchstabiert solche Wörter dann. Nur Namen, die im Feld wirklich vorkommen, werden für die Stimme
    /// in normale Schreibweise gebracht. Text, Mitschnitt und Prüfungen behalten den Originalnamen.
    /// </summary>
    internal static class SpokenNames
    {
        private static readonly HashSet<string> Roman = new HashSet<string>(StringComparer.Ordinal)
            { "II", "III", "IV", "VI", "VII", "VIII", "IX", "XI", "XII" };

        /// <summary>Ein Namenswort ganz in Großbuchstaben, mindestens zwei Buchstaben, keine römische Zahl.</summary>
        internal static bool Shouted(string word)
        {
            if (string.IsNullOrEmpty(word) || Roman.Contains(word)) return false;
            int letters = 0;
            foreach (char c in word)
            {
                if (char.IsLower(c)) return false;
                if (char.IsLetter(c)) letters++;
            }
            return letters >= 2;
        }

        /// <summary>„JAH“ → „Jah“, „O'NEIL“ → „O'Neil“, „MÜLLER-LÜDENSCHEIDT“ → „Müller-Lüdenscheidt“.</summary>
        internal static string Normal(string word)
        {
            var sb = new StringBuilder(word.Length);
            bool start = true;
            foreach (char c in word)
            {
                sb.Append(start ? char.ToUpper(c, CultureInfo.InvariantCulture) : char.ToLower(c, CultureInfo.InvariantCulture));
                start = !char.IsLetter(c);
            }
            return sb.ToString();
        }

        /// <summary>Name in sprechbarer Schreibweise; null, wenn nichts zu ändern ist.</summary>
        internal static string Speakable(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var words = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (!words.Any(Shouted)) return null;
            return string.Join(" ", words.Select(w => Shouted(w) ? Normal(w) : w));
        }

        /// <summary>Ersetzt jeden bekannten Namen in Großbuchstaben im Funktext durch die sprechbare Schreibweise.</summary>
        internal static string Prepare(string text, IEnumerable<string> knownNames)
        {
            if (string.IsNullOrEmpty(text) || knownNames == null) return text;
            // Längste Namen zuerst, damit „JAH TESÏ“ vor „TESÏ“ ersetzt wird.
            foreach (string name in knownNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim())
                         .Distinct(StringComparer.Ordinal).OrderByDescending(n => n.Length))
            {
                string spoken = Speakable(name);
                if (spoken == null || text.IndexOf(name, StringComparison.Ordinal) < 0) continue;
                text = Regex.Replace(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(name) + @"(?![\p{L}\p{N}])", spoken.Replace("$", "$$"),
                    RegexOptions.CultureInvariant);
            }
            return text;
        }
    }
}
