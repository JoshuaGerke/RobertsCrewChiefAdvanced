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

        public string Finish(SessionData s)
        {
            if (s == null || s.SessionPhase != SessionPhase.Finished) return null;
            // These flags take precedence over a numeric rank left over in the game state.
            if (s.IsDNF) return "Die Session ist beendet. Du bist als ausgefallen geführt.";
            if (s.IsDisqualified) return "Die Session ist beendet. Die Wertung meldet eine Disqualifikation.";
            if (s.ClassPosition == 1) return Select("victory", CrewSpeechPolicy.FinishPhrases);
            if (s.ClassPosition <= 0) return "Im Ziel. Die endgültige Platzierung ist noch nicht gemeldet.";
            string text = "Im Ziel! Platz " + s.ClassPosition + ".";
            if (s.SessionStartClassPosition > s.ClassPosition)
            {
                int gain = s.SessionStartClassPosition - s.ClassPosition;
                text += " " + gain + (gain == 1 ? " Platz" : " Plätze") + " gutgemacht.";
            }
            string group = s.ClassPosition <= 3 ? "finish-podium" : "finish-other";
            var catalog = RadioPhraseCatalog.Current;
            return text + " " + Select(group, Enumerable.Range(0, catalog.Count(group)).Select(i => catalog.Format(group, i)).ToArray());
        }

        /// <summary>Use only after a same-rival on-track pass has been confirmed.</summary>
        public static string Overtake(int confirmedPassCount, int position, string driverName)
        {
            if (confirmedPassCount <= 0 || position <= 0) return null;
            string name = new string((driverName ?? "Rob").Where(c => char.IsLetter(c) || c == ' ' || c == '-').Take(30).ToArray()).Trim();
            if (name.Length == 0) name = "Rob";
            var catalog = RadioPhraseCatalog.Current;
            return confirmedPassCount == 1 ? catalog.Format("pass-first", 0, position.ToString(), name) :
                catalog.Format("pass-repeat", confirmedPassCount - 2, position.ToString());
        }

    }
}
