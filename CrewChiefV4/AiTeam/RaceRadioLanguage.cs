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
            return lapInvalid ? text + CrewSpeechPolicy.LapInvalidSuffix : text;
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

        // Gleiche Klassen- und Gesamtposition: einfach "Platz", auch wenn die Fahrzeugzahlen abweichen (R3E zählt dort uneinheitlich)
        internal static bool SeparateClass(SessionData s) => s.ClassPosition > 0 &&
            (s.OverallPosition > 0 ? s.OverallPosition != s.ClassPosition :
             s.NumCarsInPlayerClass > 0 && s.NumCarsOverall > s.NumCarsInPlayerClass);

        // EN/ES nennen Positionen als "P7".
        internal static string Rank(SessionData s, int position) => RadioLanguage.IsGerman ?
            (SeparateClass(s) ? "Klassenplatz " : "Platz ") + position :
            "P" + position + (SeparateClass(s) ? RadioLanguage.T("", " in class", " en tu clase") : "");

        internal static string RaceRanks(SessionData s) => s.ClassPosition <= 0 ?
            (s.OverallPosition > 0 ? RadioLanguage.T("Gesamtplatz " + s.OverallPosition, "P" + s.OverallPosition + " overall", "P" + s.OverallPosition + " en la general") : null) :
            Rank(s, s.ClassPosition) + (SeparateClass(s) && s.OverallPosition > 0 ? RadioLanguage.T(", gesamt Platz " + s.OverallPosition, ", P" + s.OverallPosition + " overall", ", P" + s.OverallPosition + " en la general") : "");

        internal static string MeasuredGap(bool ahead, string name, string interval, bool separateClass, int variant = 0)
        {
            bool named = !string.IsNullOrWhiteSpace(name);
            if (RadioLanguage.Current == RadioLanguage.English)
            {
                if (separateClass)
                {
                    if ((variant & 1) == 0)
                        return ahead ? "In your class, there's " + interval + " between you and " + (named ? name : "the car ahead") + "." :
                            "In your class, you have " + interval + " on " + (named ? name : "the car behind") + ".";
                    return "Class gap " + (ahead ? "ahead " : "behind ") + interval + (named ? " to " + name : "") + ".";
                }
                if ((variant & 1) == 1)
                    return "Gap " + (ahead ? "ahead " : "behind ") + interval + (named ? " to " + name : "") + ".";
                return (named ? name : ahead ? "The car ahead" : "The car behind") + " is " + interval + (ahead ? " ahead of you." : " behind you.");
            }
            if (RadioLanguage.Current == RadioLanguage.Spanish)
            {
                if (separateClass)
                {
                    if ((variant & 1) == 0)
                        return ahead ? "En tu clase hay " + interval + " entre tú y " + (named ? name : "el coche de delante") + "." :
                            "En tu clase tienes " + interval + " de ventaja sobre " + (named ? name : "el coche de detrás") + ".";
                    return "Distancia en clase " + (ahead ? "hacia delante " : "hacia atrás ") + interval + (named ? " con " + name : "") + ".";
                }
                if ((variant & 1) == 1)
                    return "Distancia " + (ahead ? "hacia delante " : "hacia atrás ") + interval + (named ? " con " + name : "") + ".";
                return (named ? name : ahead ? "El coche de delante" : "El coche de detrás") + " está a " + interval + (ahead ? " por delante de ti." : " por detrás de ti.");
            }
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
            if (s.IsDNF) return RadioLanguage.T("Die Session ist beendet. Du bist als ausgefallen geführt.", "The session is over. You're classified as a retirement.", "La sesión ha terminado. Figuras como abandono.");
            if (s.IsDisqualified) return RadioLanguage.T("Die Session ist beendet. Die Wertung meldet eine Disqualifikation.", "The session is over. The classification shows a disqualification.", "La sesión ha terminado. La clasificación indica una descalificación.");
            if (s.ClassPosition == 1 && !SeparateClass(s)) return Select("victory", CrewSpeechPolicy.FinishPhrases);
            string ranks = RaceRanks(s);
            if (ranks == null) return RadioLanguage.T("Im Ziel. Die endgültige Platzierung ist noch nicht gemeldet.", "Chequered flag. Final position isn't confirmed yet.", "En meta. La posición final aún no está confirmada.");
            string text = RadioLanguage.T("Im Ziel! ", "Chequered flag! ", "¡En meta! ") + ranks + ".";
            if (s.ClassPosition == 1) return text + RadioLanguage.T(" Klassensieg. Stark gefahren!", " Class win. Strong drive!", " Victoria de clase. ¡Gran carrera!");
            if (s.ClassPosition > 0 && s.SessionStartClassPosition > s.ClassPosition)
            {
                int gain = s.SessionStartClassPosition - s.ClassPosition;
                bool separate = SeparateClass(s);
                text += RadioLanguage.T(
                    " " + gain + (gain == 1 ? " Platz" : " Plätze") + (separate ? " in deiner Klasse gutgemacht." : " gutgemacht."),
                    " Gained " + gain + (gain == 1 ? " place" : " places") + (separate ? " in your class." : "."),
                    " Has ganado " + gain + (gain == 1 ? " puesto" : " puestos") + (separate ? " en tu clase." : "."));
            }
            string group = s.ClassPosition > 0 && s.ClassPosition <= 3 ? "finish-podium" : "finish-other";
            var catalog = RadioPhraseCatalog.Current;
            return text + " " + Select(group, Enumerable.Range(0, catalog.Count(group)).Select(i => catalog.Format(group, i)).ToArray());
        }

        /// <summary>Use only after a same-rival on-track pass has been confirmed.</summary>
        public static string Overtake(int confirmedPassCount, int position, string driverName, bool classRank = false, int firstVariant = 0)
        {
            if (confirmedPassCount <= 0 || position <= 0) return null;
            string name = new string((driverName ?? "Rob").Where(c => char.IsLetter(c) || c == ' ' || c == '-').Take(30).ToArray()).Trim();
            if (name.Length == 0) name = "Rob";
            var catalog = RadioPhraseCatalog.Current;
            string rank = position + (classRank ? RadioLanguage.T(" in deiner Klasse", " in your class", " en tu clase") : "");
            return confirmedPassCount == 1 ? catalog.Format("pass-first", firstVariant, rank, name) :
                catalog.Format("pass-repeat", confirmedPassCount - 2, rank);
        }

    }
}
