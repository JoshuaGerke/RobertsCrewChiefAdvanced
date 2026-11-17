using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Behauptungsprüfer vor der Sprachausgabe (Rob 17.11.2026, Masterprompt C4). Antworten des Sprachmodells werden vor dem
    /// Sprechen gegen den Live-Zustand geprüft: Sätze, die Robs eigene Position oder die verbleibenden Runden falsch nennen,
    /// werden gestrichen; bleibt nichts übrig, kommt die gemessene Lage aus lokalen Daten. Nur eindeutige, prüfbare
    /// Behauptungen – was sich nicht prüfen lässt, bleibt unverändert (keine Sprachpolizei).
    /// </summary>
    internal static class AnswerClaimCheck
    {
        private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        // Eigene Position: nur Sätze mit Bezug auf Rob/das Team, damit Gegnerpositionen („Keller liegt auf Platz 3“) unberührt bleiben.
        // Nur Zustandsaussagen („du liegst/bist/fährst …“), keine Ziele wie „du kannst Platz 4 holen“.
        private static readonly Regex OwnPositionDe = new Regex(@"\b(?:du\s+(?:bist|liegst|fährst)|wir\s+(?:sind|liegen|fahren))\b[^.!?]{0,25}?\b(?:auf\s+)?(?:Platz|Position|P)\s?(\d{1,3})\b", Opt);
        private static readonly Regex OwnPositionEn = new Regex(@"\b(?:you['’]re|you\s+are|we['’]re|we\s+are|you\s+sit)\b[^.!?]{0,25}?\b(?:P|position|place)\s?(\d{1,3})\b", Opt);
        private static readonly Regex OwnPositionEs = new Regex(@"\b(?:vas|estás|estas|estamos|vamos|rodamos|ruedas)\b[^.!?]{0,25}?\b(?:puesto|posición|posicion|P)\s?(\d{1,3})\b", Opt);
        private static readonly Regex LapsLeftDe = new Regex(@"\bnoch\s+(\d{1,3})\s+Runden?\s+(?:zu\s+fahren|bis\s+(?:ins\s+Ziel|zur\s+Flagge|zum\s+Ende))", Opt);
        private static readonly Regex LapsLeftEn = new Regex(@"\b(\d{1,3})\s+laps?\s+(?:to\s+go|remaining|left\s+in\s+the\s+race)", Opt);
        private static readonly Regex LapsLeftEs = new Regex(@"\bquedan?\s+(\d{1,3})\s+vueltas?\s+(?:para\s+(?:el\s+final|la\s+bandera)|hasta\s+(?:el\s+final|la\s+meta|la\s+bandera))", Opt);

        private static IEnumerable<string> Sentences(string text) =>
            Regex.Split(text, @"(?<=[.!?])\s+").Where(s => s.Trim().Length > 0);

        /// <summary>Grund, warum ein Satz widerlegt ist, oder null.</summary>
        internal static string Refutes(string sentence, int classPosition, int overallPosition, int lapsLeft)
        {
            var own = RadioLanguage.IsGerman ? OwnPositionDe : RadioLanguage.Current == RadioLanguage.Spanish ? OwnPositionEs : OwnPositionEn;
            if (classPosition > 0)
                foreach (Match m in own.Matches(sentence))
                    if (int.TryParse(m.Groups[1].Value, out int claimed) && claimed != classPosition && claimed != overallPosition)
                        return "own_position_" + claimed + "_measured_" + classPosition;
            if (lapsLeft > 0)
            {
                var laps = RadioLanguage.IsGerman ? LapsLeftDe : RadioLanguage.Current == RadioLanguage.Spanish ? LapsLeftEs : LapsLeftEn;
                foreach (Match m in laps.Matches(sentence))
                    if (int.TryParse(m.Groups[1].Value, out int claimed) && Math.Abs(claimed - lapsLeft) > 1)
                        return "laps_left_" + claimed + "_measured_" + lapsLeft;
            }
            return null;
        }

        /// <summary>
        /// Gibt die Antwort ohne widerlegte Sätze zurück. <paramref name="removed"/> nennt die Gründe. Bleibt nichts übrig,
        /// liefert die Methode die gemessene Lage (Platz) oder null.
        /// </summary>
        internal static string Check(string answer, GameStateData gs, out List<string> removed)
        {
            removed = new List<string>();
            if (string.IsNullOrWhiteSpace(answer) || gs == null) return answer;
            var s = gs.SessionData;
            int lapsLeft = s.SessionType == SessionType.Race ? LapWatch.LapsLeft(s) : -1;
            var kept = new List<string>();
            foreach (string sentence in Sentences(answer))
            {
                string reason = Refutes(sentence, s.ClassPosition, s.OverallPosition, lapsLeft);
                if (reason == null) kept.Add(sentence.Trim());
                else removed.Add(reason);
            }
            if (removed.Count == 0) return answer;
            if (kept.Count > 0) return string.Join(" ", kept);
            return s.ClassPosition > 0 ? ActiveRaceObserver.PositionNow(s, s.ClassPosition, 0) : null;
        }
    }
}
