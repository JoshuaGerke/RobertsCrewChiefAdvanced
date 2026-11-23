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
            "gegner", "vor mir", "hinter mir", "abstand", "position", "platz", "führend", "leader", "runden noch", "wie lange noch", "restzeit", "pace der",
            " pit", "strategy", "fuel", "opponent", "ahead of me", "behind me", "in front of me", " gap", "laps left", "laps to go", "how long", "time left", "pace of",
            "parada", "estrategia", "combustible", "gasolina", "delante de m", "detrás de m", "detras de m", "distancia", "posición", "posicion", "puesto",
            "líder", "lider", "vueltas quedan", "vueltas faltan", "cuánto queda", "cuanto queda", "tiempo queda", "ritmo de" };
        private static readonly string[] benTopics = { "reifen", "druck", "temperatur", "bremse", "setup", "untersteuer", "übersteuer", "schieb", "traktion",
            "balance", "fahr", "kurve", "schaden", "motor", "getriebe", "abtrieb", "aero", "differential", "stabi", "feder", "dämpfer", "verliere ich",
            "tyre", "tire", "pressure", "brake", "understeer", "oversteer", "traction", "driving", "corner", "damage", "engine", "gearbox", "downforce",
            "anti-roll", "springs", "damper", "losing time", "neumátic", "neumatic", "goma", "presión", "presion", "freno", "subviraje", "sobreviraje",
            "tracción", "traccion", "equilibrio", "conducc", "curva", "daño", "caja de cambios", "carga aerodin", "diferencial", "muelle", "amortiguador", "pierdo" };

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

            // LMU 22.11.: „Die Gelbphasen kriegst du aber auch mit, ja, Sarah, und meldest die, oder?“ und „Ich rede von Sarah,
            // nicht von dir“ gingen an Mik und Ben – der Name stand nicht in den ersten drei Wörtern. Wird genau ein Crewmitglied
            // später im Satz angesprochen (zwischen Kommas, am Satzende, „von/mit/an Sarah“), ist es gemeint. Ein Gegnername
            // mitten im Satz („Wie weit ist Mike Rockenfeller?“) zählt nicht.
            var addressedLater = AddressedLater(text);
            if (addressedLater.HasValue && !DriverRadioPreferences.IsCommand(text) && !RadioAmountCommand.Parse(text).HasValue)
                return new RoutedRequest { Member = addressedLater.Value, Question = text, AddressedByName = true };

            // „Spotter aus/ein“ gehört zu Sara, ganz gleich, wer zuletzt gesprochen hat.
            if (SpotterCommand.Parse(text).HasValue)
                return new RoutedRequest { Member = TeamMember.Sara, Question = text, AddressedByName = false };

            // Explicit routine preferences belong to the pit wall, even after a conversation with Sara.
            if (DriverRadioPreferences.IsCommand(text) || RadioAmountCommand.Parse(text).HasValue)
                return new RoutedRequest { Member = TeamMember.Ben, Question = text, AddressedByName = false };

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

        private static readonly Dictionary<TeamMember, Regex> addressed = names.ToDictionary(p => p.Key, p => new Regex(
            @"(?:[,;:!?.]\s*|\b(?:von|mit|an|zu|für|frag|to|with|ask|con|a|para)\s+)(?:" + string.Join("|", p.Value.Select(Regex.Escape)) + @")(?=\s*(?:[,;:!?.]|$))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled));

        /// <summary>Genau ein Crewmitglied später im Satz angesprochen, sonst null.</summary>
        internal static TeamMember? AddressedLater(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            TeamMember? found = null;
            foreach (var entry in addressed)
            {
                if (!entry.Value.IsMatch(text)) continue;
                if (found.HasValue) return null; // zwei Namen: unklar, wie bisher nach Thema
                found = entry.Key;
            }
            return found;
        }

        /// <summary>
        /// LMU-Test 18.11.: „Seid ihr alle da?“ ging an den ausgeschalteten Mik und blieb unbeantwortet. Ist der gewählte
        /// Sprecher aus, antwortet ein eingeschaltetes Crewmitglied: zuerst der letzte Gesprächspartner, dann Ben, Mik, Sara.
        /// null, wenn niemand eingeschaltet ist.
        /// </summary>
        internal static TeamMember? Available(TeamMember wanted, Func<TeamMember, bool> enabled, TeamMember? last)
        {
            if (enabled(wanted)) return wanted;
            if (last.HasValue && enabled(last.Value)) return last.Value;
            foreach (var m in new[] { TeamMember.Ben, TeamMember.Mik, TeamMember.Sara }) if (enabled(m)) return m;
            return null;
        }

        // LMU-Test 18.11.: „Le Mans Ultimate“ kam als „Lemon Ultimate“ an, und die Crew sprach es so nach.
        private static readonly Regex lmuMisheard = new Regex(@"\b(lemons?|lemans|le\s?man|lehmanns?|lehmans?)\s+ultimate?\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Korrigiert bekannte Hörfehler bei Sim-Namen in der Spracherkennung.</summary>
        internal static string FixSimNames(string transcript)
        {
            return string.IsNullOrEmpty(transcript) ? transcript : lmuMisheard.Replace(transcript, "Le Mans Ultimate");
        }

        internal static bool HasWord(string text, string word)
        {
            return Regex.IsMatch(text ?? "", @"\b" + Regex.Escape(word) + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        // "Reifendruck vorne links" oder "Ist die Box frei?" sind keine Fragen nach Autos neben dem Fahrer
        private static readonly Regex notAboutSides = new Regex(
            @"reifen|druck|temperatur|brems|flügel|felge|schaden|aufhängung|setup|kurve|vorne|hinten|\bbox|strecke|sprit|tank|motor|" +
            @"\btyre|\btire|pressure|brake|\bwing|\brim\b|damage|suspension|corner|\bfront|\brear|\bpit\b|track|fuel|engine|" +
            @"neumátic|neumatic|\bgoma|presión|presion|\bfren[oa]|alerón|aleron|llanta|daño|suspensi|curva|delante|detrás|detras|pista|combustible|gasolina",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Englisch "left" heißt oft "übrig" ("laps left"), "right" oft "richtig"/"sofort" – das sind keine Seitenfragen.
        private static readonly Regex leftNotSide = new Regex(
            @"\b(laps?|time|minutes?|seconds?|fuel|litres?|liters?|much|many|anything|nothing|enough|got)\s+left\b|\bleft\s+(to go|over|in the tank)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        private static readonly Regex rightNotSide = new Regex(
            @"\b(all right|alright|that'?s right|is that right|you'?re right|right now|right away)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Fragt der Fahrer nach links? Deutsch, Englisch und Spanisch gleichzeitig.</summary>
        internal static bool AsksLeft(string text)
        {
            return HasWord(text, "links") || HasWord(text, "izquierda") || HasWord(text, "left") && !leftNotSide.IsMatch(text ?? "");
        }

        /// <summary>Fragt der Fahrer nach rechts? Deutsch, Englisch und Spanisch gleichzeitig.</summary>
        internal static bool AsksRight(string text)
        {
            return HasWord(text, "rechts") || HasWord(text, "derecha") || HasWord(text, "right") && !rightNotSide.IsMatch(text ?? "");
        }

        public static bool IsSideQuestion(string text)
        {
            if (HasWord(text, "neben") || Regex.IsMatch(text ?? "", @"\büberlapp\w*\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
                Regex.IsMatch(text ?? "", @"\b(beside|alongside|next to (me|us)|overlap\w*|solap\w*|al lado|a mi lado|a nuestro lado)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return true;
            }
            return (AsksLeft(text) || AsksRight(text) || HasWord(text, "frei") || HasWord(text, "clear") || HasWord(text, "libre")) && !notAboutSides.IsMatch(text ?? "");
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
