using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Initials are exact aliases. Similar sounds are suggestions, never confirmed identities.</summary>
    public static class OpponentNameMatching
    {
        private static string Name(OpponentData car) => RaceFieldTools.Normalize(car.DriverRawName);

        private static IEnumerable<string> Abbreviations(string name)
        {
            var words = name.Split(' ');
            for (int count = 1; count < words.Length; count++)
            {
                string tail = string.Join(" ", words.Skip(count));
                if (tail.Replace(" ", "").Length < 3) continue;
                string initials = string.Concat(words.Take(count).Where(w => w.Length > 0).Select(w => w[0]));
                yield return string.Join(" ", initials.Select(c => c.ToString())) + " " + tail;
                yield return initials + " " + tail;
                yield return initials + tail.Replace(" ", "");
            }
        }

        public static List<OpponentData> Find(string query, IEnumerable<OpponentData> cars)
        {
            string normalized = RaceFieldTools.Normalize(query);
            var field = cars.Where(c => c != null && !string.IsNullOrWhiteSpace(c.DriverRawName)).ToList();
            var full = field.Where(c => Name(c) == normalized).ToList();
            if (full.Count > 0) return full;
            var abbreviated = field.Where(c => Abbreviations(Name(c)).Contains(normalized)).ToList();
            if (abbreviated.Count > 0) return abbreviated;
            return field.Where(c => normalized.Length >= 3 && Name(c).EndsWith(" " + normalized, StringComparison.Ordinal)).ToList();
        }

        public static List<OpponentData> InQuestion(string question, IEnumerable<OpponentData> cars)
        {
            string q = " " + RaceFieldTools.Normalize(question) + " ";
            var field = cars.Where(c => c != null && !string.IsNullOrWhiteSpace(c.DriverRawName)).ToList();
            var full = field.Where(c => q.Contains(" " + Name(c) + " ")).ToList();
            if (full.Count > 0) return full;
            var abbreviated = field.Where(c => Abbreviations(Name(c)).Any(a => q.Contains(" " + a + " "))).ToList();
            if (abbreviated.Count > 0) return abbreviated;
            return field.Where(c => { string surname = Name(c).Split(' ').Last(); return surname.Length >= 3 && q.Contains(" " + surname + " "); }).ToList();
        }

        private static string SoundKey(string value) => value.Replace("ff", "f").Replace("v", "f");

        private static int Distance(string a, string b)
        {
            var previous = Enumerable.Range(0, b.Length + 1).ToArray();
            var current = new int[b.Length + 1];
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                var swap = previous; previous = current; current = swap;
            }
            return previous[b.Length];
        }

        public static List<OpponentData> Suggest(string query, IEnumerable<OpponentData> cars)
        {
            string[] words = RaceFieldTools.Normalize(query).Split(' ');
            string surname = words.LastOrDefault() ?? "";
            if (surname.Length < 4) return new List<OpponentData>();
            string sound = SoundKey(surname);
            return cars.Where(c => c != null && !string.IsNullOrWhiteSpace(c.DriverRawName)).Select(c => {
                string normalized = Name(c);
                var actual = normalized.Split(' ');
                bool initialMatches = words.Length < 2 || words[0].Length != 1 || actual[0].Length > 0 && words[0][0] == actual[0][0];
                int distance = Distance(sound, SoundKey(actual.Last()));
                return new { Car = c, Distance = distance, InitialMatches = initialMatches };
            }).Where(c => c.InitialMatches && c.Distance <= Math.Min(2, Math.Max(1, sound.Length / 3)))
                .OrderBy(c => c.Distance).ThenBy(c => Name(c.Car), StringComparer.Ordinal)
                .Take(3).Select(c => c.Car).ToList();
        }
    }
}
