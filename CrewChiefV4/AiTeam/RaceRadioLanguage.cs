using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Short local wording for already confirmed facts. Selection never adds a cause,
    /// strategy, penalty or performance claim. The small per-topic memory survives a
    /// session reset so the same crew does not start every session with the same line.
    /// </summary>
    public sealed class RaceRadioLanguage
    {
        private readonly Random random;
        private readonly Dictionary<string, int> previous = new Dictionary<string, int>();

        public RaceRadioLanguage(int? seed = null)
        { random = seed.HasValue ? new Random(seed.Value) : new Random(); }

        private string Select(string topic, string[] phrases)
        {
            // Callers below use a fixed set of topics and immutable catalogues.
            // Keep a bound as a safeguard if another local topic is added later.
            int last;
            bool known = previous.TryGetValue(topic, out last);
            int next = random.Next(known && phrases.Length > 1 ? phrases.Length - 1 : phrases.Length);
            if (known && phrases.Length > 1 && next >= last) next++;
            if (!known && previous.Count >= 16) previous.Clear();
            previous[topic] = next;
            return phrases[next];
        }

        public string InvalidLap(bool repeated)
        {
            return Select(repeated ? "invalid-repeat" : "invalid-first", repeated
                ? CrewSpeechPolicy.RepeatedInvalidLapPhrases : CrewSpeechPolicy.FirstInvalidLapPhrases);
        }

        public string TrackLimit(bool repeated, bool lapInvalid)
        {
            string text = Select(repeated ? "limits-repeat" : "limits-first", repeated
                ? CrewSpeechPolicy.RepeatedTrackLimitPhrases : CrewSpeechPolicy.FirstTrackLimitPhrases);
            return lapInvalid ? text + " Diese Runde ist ungültig." : text;
        }

        public string Contact(bool heavy)
        { return heavy ? Select("contact-heavy", CrewSpeechPolicy.HeavyContactPhrases) : Select("contact", CrewSpeechPolicy.ContactPhrases); }

        public string WeatherRain(bool raining)
        { return Select(raining?"rain-start":"rain-stop",raining?CrewSpeechPolicy.RainStartPhrases:CrewSpeechPolicy.RainStopPhrases); }
        public string WeatherWetness(bool wetter)
        { return Select(wetter?"wetter":"drier",wetter?CrewSpeechPolicy.WetterPhrases:CrewSpeechPolicy.DrierPhrases); }

        public string DamageChanged() { return Select("damage-detected", CrewSpeechPolicy.DamageDetectedPhrases); }

        public string Stopped() { return Select("stopped", CrewSpeechPolicy.StoppedPhrases); }

        public string OffTrack(bool wet)
        { return Select("off-track", CrewSpeechPolicy.OffTrackPhrases) + (wet ? CrewSpeechPolicy.WetTrackHint : ""); }

        internal static bool SeparateClass(SessionData s) => s.ClassPosition > 0 &&
            (s.OverallPosition > 0 && s.OverallPosition != s.ClassPosition ||
             s.NumCarsInPlayerClass > 0 && s.NumCarsOverall > s.NumCarsInPlayerClass);

        internal static string Rank(SessionData s, int position) =>
            (SeparateClass(s) ? "Klassenplatz " : "Platz ") + position;

        internal static string RaceRanks(SessionData s) => s.ClassPosition <= 0 ?
            (s.OverallPosition > 0 ? "Gesamtplatz " + s.OverallPosition : null) :
            Rank(s, s.ClassPosition) + (SeparateClass(s) && s.OverallPosition > 0 ? ", gesamt Platz " + s.OverallPosition : "");

        internal static string MeasuredGap(bool ahead, string name, string interval, bool separateClass, int variant = 0)
        {
            bool named = !string.IsNullOrWhiteSpace(name);
            if (separateClass)
            {
                if ((variant & 1) == 0)
                    return "In deiner Klasse " + (ahead ? "liegen " : "hast du ") + interval +
                        (ahead ? " zwischen dir und " : " Vorsprung auf ") +
                        (named ? name : ahead ? "dem Vordermann" : "den Hintermann") + ".";
                return "Klassenabstand " + (ahead ? "nach vorn " : "nach hinten ") + interval +
                    (named ? " zu " + name : "") + ".";
            }
            if ((variant & 1) == 1)
                return (ahead ? "Nach vorn " : "Nach hinten ") + interval + (named ? " zu " + name : "") + ".";
            string opponent = named ? name : ahead ? "Der Wagen vor dir" : "Der Wagen hinter dir";
            return opponent + " liegt " + interval + (ahead ? " vor dir." : " hinter dir.");
        }

        public string Finish(SessionData s)
        {
            if (s == null || s.SessionPhase != SessionPhase.Finished) return null;
            // These flags take precedence over a numeric rank left over in the game state.
            if (s.IsDNF) return "Die Session ist beendet. Du bist als ausgefallen geführt.";
            if (s.IsDisqualified) return "Die Session ist beendet. Die Wertung meldet eine Disqualifikation.";
            if (s.ClassPosition == 1 && !SeparateClass(s)) return Select("victory", CrewSpeechPolicy.FinishPhrases);
            string ranks = RaceRanks(s);
            if (ranks == null) return "Im Ziel. Die endgültige Platzierung ist noch nicht gemeldet.";
            string text = "Im Ziel! " + ranks + ".";
            if (s.ClassPosition == 1) return text + " Klassensieg. Stark gefahren!";
            if (s.ClassPosition > 0 && s.SessionStartClassPosition > s.ClassPosition)
            {
                int gain = s.SessionStartClassPosition - s.ClassPosition;
                text += " " + gain + (gain == 1 ? " Platz" : " Plätze") + (SeparateClass(s) ? " in deiner Klasse gutgemacht." : " gutgemacht.");
            }
            string group = s.ClassPosition > 0 && s.ClassPosition <= 3 ? "finish-podium" : "finish-other";
            var catalog = RadioPhraseCatalog.Current;
            return text + " " + Select(group, Enumerable.Range(0, catalog.Count(group)).Select(i => catalog.Format(group, i)).ToArray());
        }

        /// <summary>Use only after a same-rival on-track pass has been confirmed.</summary>
        public static string Overtake(int confirmedPassCount, int position, string driverName, bool classRank = false)
        {
            if (confirmedPassCount <= 0 || position <= 0) return null;
            string name = new string((driverName ?? "Rob").Where(c => char.IsLetter(c) || c == ' ' || c == '-').Take(30).ToArray()).Trim();
            if (name.Length == 0) name = "Rob";
            var catalog = RadioPhraseCatalog.Current;
            string rank = position + (classRank ? " in deiner Klasse" : "");
            return confirmedPassCount == 1 ? catalog.Format("pass-first", 0, rank, name) :
                catalog.Format("pass-repeat", confirmedPassCount - 2, rank);
        }

    }
}
