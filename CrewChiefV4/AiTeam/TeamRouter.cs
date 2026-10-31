using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CrewChiefV4.AiTeam
{
    public enum TeamMember { Ben, Mik, Sara }

    public class RoutedRequest
    {
        public TeamMember Member;
        public string Question;
        /// <summary>true, wenn der Name ausdrücklich genannt wurde (sonst Themen- oder Kontextraten).</summary>
        public bool AddressedByName;
    }

    /// <summary>
    /// Entscheidet, wer angesprochen wurde ("Ben, …", "Mik, …", "Sara, …").
    /// Ohne Namen entscheidet das Thema, danach der letzte Gesprächspartner.
    /// Läuft lokal und ohne KI.
    /// </summary>
    public static class TeamRouter
    {
        private static readonly Dictionary<TeamMember, string[]> names = new Dictionary<TeamMember, string[]>
        {
            { TeamMember.Ben, new[] { "ben", "benn", "bän", "benny", "ben's" } },
            { TeamMember.Mik, new[] { "mik", "mick", "mike", "mikk", "mic", "mig", "mieck", "mitch" } },
            { TeamMember.Sara, new[] { "sara", "sarah", "zara", "zarah", "sahra", "sarra" } },
        };

        private static readonly string[] saraTopics = { "links", "rechts", "frei", "neben mir", "neben uns", "überlapp", "gelb", "unfall", "wrack", "steht auf der strecke" };
        private static readonly string[] mikTopics = { "box", "stopp", "stop ", "strategie", "sprit", "benzin", "kraftstoff", "tank", "undercut", "overcut",
            "gegner", "vor mir", "hinter mir", "abstand", "position", "platz", "führend", "leader", "runden noch", "wie lange noch", "restzeit", "pace der" };
        private static readonly string[] benTopics = { "reifen", "druck", "temperatur", "bremse", "setup", "untersteuer", "übersteuer", "schieb", "traktion",
            "balance", "fahr", "kurve", "schaden", "motor", "getriebe", "abtrieb", "aero", "differential", "stabi", "feder", "dämpfer", "verliere ich" };

        public static RoutedRequest Route(string transcript, TeamMember? lastMember)
        {
            string text = (transcript ?? "").Trim();
            string[] words = Regex.Split(text, @"[\s,.!?:;]+").Where(w => w.Length > 0).ToArray();

            // Name in den ersten drei Wörtern ("Ben, …", "Hey Mik …", "Okay Sara …")
            for (int i = 0; i < Math.Min(3, words.Length); i++)
            {
                string w = words[i].ToLowerInvariant();
                foreach (var entry in names)
                {
                    if (entry.Value.Contains(w))
                    {
                        return new RoutedRequest { Member = entry.Key, Question = StripLeadingName(text, words[i]), AddressedByName = true };
                    }
                }
            }

            string lower = " " + text.ToLowerInvariant() + " ";
            TeamMember? byTopic = null;
            if (IsSideQuestion(lower))
            {
                byTopic = TeamMember.Sara;
            }
            else if (mikTopics.Any(t => lower.Contains(t)))
            {
                byTopic = TeamMember.Ben;
            }
            else if (benTopics.Any(t => lower.Contains(t)))
            {
                byTopic = TeamMember.Mik;
            }
            TeamMember member = byTopic ?? lastMember ?? TeamMember.Mik;
            return new RoutedRequest { Member = member, Question = text, AddressedByName = false };
        }

        internal static bool HasWord(string text, string word)
        {
            return Regex.IsMatch(text ?? "", @"\b" + Regex.Escape(word) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        // "Reifendruck vorne links" oder "Ist die Box frei?" sind keine Fragen nach Autos neben dem Fahrer
        private static readonly Regex notAboutSides = new Regex(
            @"reifen|druck|temperatur|brems|flügel|felge|schaden|aufhängung|setup|kurve|vorne|hinten|\bbox|strecke|sprit|tank|motor",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static bool IsSideQuestion(string text)
        {
            if (HasWord(text, "neben") || Regex.IsMatch(text ?? "", @"\büberlapp\w*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return true;
            }
            return (HasWord(text, "links") || HasWord(text, "rechts") || HasWord(text, "frei")) && !notAboutSides.IsMatch(text ?? "");
        }

        private static string StripLeadingName(string text, string nameWord)
        {
            int index = text.IndexOf(nameWord, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return text;
            }
            string rest = text.Substring(index + nameWord.Length).TrimStart(' ', ',', '.', '!', '?', ':', ';', '-');
            return rest.Length == 0 ? text : rest;
        }

        public static string DisplayName(TeamMember member)
        {
            switch (member)
            {
                case TeamMember.Mik: return "Mik";
                case TeamMember.Sara: return "Sara";
                default: return "Ben";
            }
        }
    }
}
