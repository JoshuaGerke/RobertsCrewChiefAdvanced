using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Conservative output hygiene, not a translator or a proof of factual accuracy.</summary>
    public static class RadioAnswerQuality
    {
        private static readonly Regex GermanTail = new Regex(
            @"^(bitte|achte|vermeide|pass|aufpassen|limiter|abstand|motor|bremsen|reifen|safety|track|gelb|blau|rot|grün|kalt|heiß|nass|trocken|mehr|weniger|aber|und|doch|dann|also|noch|jetzt|weiter|bleib|bleibe|das|die|der|den|dem|dein|wir|du|es|im|in|am|auf|bei|mit|ohne|bis|zum|zur|kein|nicht|vorne|vorn|hinten|links|rechts|sofort|vorsicht|achtung|stopp|box|schon|schone|brems|bremse|langsam|fahr|fahre|halt|halte|roll|rolle|zieh|ziehe|spar|spare|reduziere|kühl|kühle|passt|sieht|läuft|alles|ruhig|gut|prima|genau)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string Clean(string answer, IEnumerable<string> knownNames = null)
        {
            if (string.IsNullOrWhiteSpace(answer)) return null;
            string text = Regex.Replace(answer, @"[\p{Cc}\p{Cf}]+", " ").Trim();
            var names = (knownNames ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Take(256).ToArray();
            string inspected = text;
            foreach (string name in names.OrderByDescending(n => n.Length))
                inspected = Regex.Replace(inspected, Regex.Escape(name), m => new string('x', m.Length), RegexOptions.IgnoreCase);
            int unexpected = -1;
            for (int i = 0; i < inspected.Length; i++)
            {
                char c = inspected[i];
                if (char.IsLetter(c) && !(c <= '\u024f' || c >= '\u1d00' && c <= '\u1eff'))
                { unexpected = i; break; }
            }
            if (unexpected >= 0)
            {
                int boundary = text.LastIndexOfAny(new[] { '.', '!', '?' }, unexpected);
                if (boundary < 0) return null;
                text = text.Substring(0, boundary + 1).Trim();
            }
            // Drop a short, unfinished lower-case tail after a complete sentence only if it
            // has neither a normal German continuation, a value nor an actual known name.
            int end = text.LastIndexOfAny(new[] { '.', '!', '?' });
            if (end >= 0 && end < text.Length - 1)
            {
                string tail = text.Substring(end + 1).Trim();
                bool knownName = names.Any(n => tail.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.Split(' ').Any(part => part.Length >= 3 && Regex.IsMatch(tail, @"\b" + Regex.Escape(part) + @"\b", RegexOptions.IgnoreCase)));
                if (tail.Length > 0 && char.IsLower(tail[0]) && tail.Split(' ').Length <= 5 &&
                    !tail.Any(char.IsDigit) && !knownName && !GermanTail.IsMatch(tail))
                    text = text.Substring(0, end + 1).Trim();
            }
            return !text.Any(char.IsLetterOrDigit) ? null : text;
        }
    }
}
