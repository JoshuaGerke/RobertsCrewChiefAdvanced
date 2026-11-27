using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rob, 23.11.2026: „Sara meldet kein Gelb mehr ab sofort! Sie meldet nur, wenn vor uns ein langsamer Fahrer ist, jemand steht
    /// oder ein Unfall voraus ist. Nicht, wenn jemand kurz die Strecke verlässt. Alle anderen sicherheitsrelevanten Infos meldet
    /// sie weiter unverzüglich, z. B. in allen Sims Full Course Yellow und Safety Car.“
    /// Im Monza-Rennen am 22.11. kamen von Saras 36 Funksprüchen 26 nur zu gewöhnlichem Gelb oder „Gelb aufgehoben“.
    /// Gilt für alle Sims: die Flaggen aus dem Spielstand (FlagWatch) und die Gelb-/Grün-Meldungen der Rennleitung.
    /// Erkannt und in der Rennchronik festgehalten wird Gelb weiterhin – nur der Funk entfällt.
    /// </summary>
    /// <summary>
    /// Schwere Kontakte und Einschläge anderer Fahrer (LMU-Ergebnisstrom). Sie werden nicht gesprochen (Rob 24.11.: „nicht jeden
    /// Kontakt“), machen aber aus einem stehenden oder gedrehten Wagen voraus einen Unfall.
    /// </summary>
    internal static class FieldImpacts
    {
        /// <summary>Ab dieser Stärke zählt ein Kontakt zwischen zwei Autos bzw. ein Einschlag als Unfall (Algarve: 80–2100 bzw. bis 8000).</summary>
        internal const double CarImpact = 500, ObjectImpact = 1500;
        internal static readonly TimeSpan Window = TimeSpan.FromSeconds(30);
        private static readonly System.Collections.Generic.Dictionary<string, DateTime> recent =
            new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        internal static string Key(string rawName)
        {
            string spoken = RadioPhraseCatalog.SafeRivalName(rawName);
            return string.IsNullOrWhiteSpace(spoken) ? null : RaceTrafficWatch.NameClipText(spoken);
        }

        internal static void Note(string rawName, double severity, bool withObject, DateTime now)
        {
            if (double.IsNaN(severity) || severity < (withObject ? ObjectImpact : CarImpact)) return;
            string key = Key(rawName);
            if (key == null) return;
            lock (recent)
            {
                if (recent.Count > 64) recent.Clear();
                recent[key] = now;
            }
        }

        /// <summary>Hatte der Wagen hinter diesem Namensclip („Das ist …“) eben einen schweren Kontakt?</summary>
        internal static bool Recent(string nameClip, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(nameClip)) return false;
            lock (recent) return recent.TryGetValue(nameClip, out var at) && now >= at && now - at <= Window;
        }

        internal static void Reset() { lock (recent) recent.Clear(); }
    }

    internal static class SaraFlagPolicy
    {
        /// <summary>Gewöhnliches Gelb am Funk (Sektor, lokal, doppelt, aufgehoben). Rob: aus.</summary>
        internal static bool PlainYellowRadio = false;

        /// <summary>„Unfall voraus“ nur, wenn ein Wagen so nah voraus wirklich steht, sich gedreht hat oder fast steht.</summary>
        internal const float IncidentAheadM = 400f;
        /// <summary>Ohne Lagebild: so langsam (m/s, etwa 30 km/h) muss ein Wagen bis 300 m voraus sein.</summary>
        internal const float CrawlingMps = 8f;

        /// <summary>Gewöhnliches Gelb oder das Ende davon (Spielflagge oder Rennleitung).</summary>
        internal static bool IsPlainYellow(string topic)
        {
            string t = topic ?? "";
            // FlagWatch: 0 lokales Gelb, 1 Gelb aufgehoben, 2 Sektor-Gelb, 3 Doppelt Gelb.
            if (t == "warning-flag-0" || t == "warning-flag-1" || t == "warning-flag-2" || t == "warning-flag-3") return true;
            return t.StartsWith("warning-rc-yellow-", StringComparison.Ordinal) || t.StartsWith("warning-rc-dyellow-", StringComparison.Ordinal) ||
                t.StartsWith("warning-rc-green-", StringComparison.Ordinal);
        }

        /// <summary>Darf Sara diese Flaggen-/Gefahrenmeldung sprechen? Sonst nennt reason den Grund.</summary>
        internal static bool Allows(string topic, GameStateData gs, FieldView view, DateTime now, out string reason)
        {
            reason = null;
            if (IsPlainYellow(topic) && !PlainYellowRadio) { reason = "rob_no_plain_yellow"; return false; }
            // FlagWatch 8 „Unfall voraus!“ kommt aus der Unfall-Entfernung des Spiels – die steht auch, wenn jemand nur kurz neben
            // die Strecke fährt und weiterfährt. Gesprochen wird es nur mit einem stehenden, gedrehten oder fast stehenden Wagen voraus.
            if (topic == "warning-flag-8" && !IncidentConfirmed(gs, view, now)) { reason = "incident_without_stopped_car_ahead"; return false; }
            return true;
        }

        /// <summary>
        /// Rob 24.11. (LMU Algarve): „Sara muss nicht jeden Kontakt der anderen melden, nur Unfälle, Dreher, langsame Fahrzeuge.
        /// Kurz mit Gelb ankündigen: ‚Gelb – vorne ist ein Unfall – Fahrername‘, ‚Gelb – vorne steht ein Wagen‘.“ Saras
        /// Gefahrenmeldungen voraus bekommen einen kurzen Auftakt: „Gelb!“, wenn die Sim Gelb zeigt, „Unfall“, wenn der Wagen
        /// eben einen schweren Kontakt oder Einschlag hatte. Der Name folgt wie bisher am Ende. Gewöhnliches Gelb ohne Gefahr und
        /// Kontakte anderer bleiben stumm.
        /// </summary>
        internal static bool HazardLeadTopic(string topic) =>
            topic == "warning-hazard-far" || topic == "race-hazard-ahead";

        /// <summary>Audit 25.11. (Astra 2 x 1,5 s): Gefahr voraus wartet nicht auf neu erzeugte Clips für Auftakt oder Namen.
        /// Fehlt einer, geht der Kernspruch sofort raus und der Clip wird für das nächste Mal erzeugt.</summary>
        internal static bool NoRenderWait(string topic) => HazardLeadTopic(topic) ||
            (topic ?? "").StartsWith("race-hazard", StringComparison.Ordinal) || (topic ?? "").StartsWith("warning-hazard", StringComparison.Ordinal);

        internal static string HazardLead(string topic, GameStateData gs, string nameClip, DateTime now)
        {
            if (!HazardLeadTopic(topic)) return null;
            bool yellow = YellowAheadWatch.SimShowsYellow(gs);
            bool crash = FieldImpacts.Recent(nameClip, now);
            return HazardLead(yellow, crash);
        }

        internal static string[] LeadOptions(bool yellow, bool crash)
        {
            if (crash && yellow)
                return new[] {
                    RadioLanguage.T("Gelb, vorne ist ein Unfall!", "Yellow, there's a crash ahead!", "¡Amarilla, accidente delante!"),
                    RadioLanguage.T("Gelb! Unfall vor dir!", "Yellow! Accident ahead!", "¡Amarilla! ¡Accidente delante!"),
                    RadioLanguage.T("Gelb, da vorne hat es gekracht!", "Yellow, there's been a crash up ahead!", "¡Amarilla, ha habido un choque delante!"),
                    RadioLanguage.T("Achtung, Gelb, Unfall voraus!", "Careful, yellow, crash ahead!", "¡Cuidado, amarilla, accidente delante!"),
                    RadioLanguage.T("Gelbe Flagge, vorne ein Unfall!", "Yellow flag, accident ahead!", "¡Bandera amarilla, accidente delante!") };
            if (crash)
                return new[] {
                    RadioLanguage.T("Vorne ist ein Unfall!", "There's a crash ahead!", "¡Hay un accidente delante!"),
                    RadioLanguage.T("Unfall vor dir!", "Accident ahead of you!", "¡Accidente delante de ti!"),
                    RadioLanguage.T("Da vorne hat es gekracht!", "There's been a crash up ahead!", "¡Ha habido un choque delante!"),
                    RadioLanguage.T("Achtung, Unfall voraus!", "Careful, crash ahead!", "¡Cuidado, accidente delante!"),
                    RadioLanguage.T("Vorsicht, vorne ein Unfall!", "Watch out, accident ahead!", "¡Ojo, accidente delante!") };
            if (yellow)
                return new[] {
                    RadioLanguage.T("Gelb!", "Yellow!", "¡Amarilla!"),
                    RadioLanguage.T("Gelb vorne!", "Yellow ahead!", "¡Amarilla delante!"),
                    RadioLanguage.T("Achtung, Gelb!", "Careful, yellow!", "¡Cuidado, amarilla!"),
                    RadioLanguage.T("Gelbe Flagge!", "Yellow flag!", "¡Bandera amarilla!"),
                    RadioLanguage.T("Gelb, Gelb!", "Yellow, yellow!", "¡Amarilla, amarilla!") };
            return new string[0];
        }

        internal static string HazardLead(bool yellow, bool crash)
        {
            var options = LeadOptions(yellow, crash);
            return options.Length == 0 ? null : RadioVariants.Pick("hazard-lead-" + (yellow ? "y" : "") + (crash ? "c" : ""), options);
        }

        /// <summary>Alle Auftakte in der aktuellen Sprache (zum Vorbereiten in Saras Stimme).</summary>
        internal static IEnumerable<string> HazardLeadPhrases =>
            LeadOptions(true, true).Concat(LeadOptions(false, true)).Concat(LeadOptions(true, false));

        /// <summary>Steht, dreht oder schleicht ein Wagen kurz vor Rob?</summary>
        internal static bool IncidentConfirmed(GameStateData gs, FieldView view, DateTime now)
        {
            if (view != null && view.Cars != null && view.Fresh(now, 2))
                return view.Cars.Any(c => c != null && !c.IsPlayer && !c.InPit && (c.Motion == CarMotion.Stopped || c.Motion == CarMotion.Spun) &&
                    !float.IsNaN(c.GapM) && c.GapM > 0 && c.GapM <= IncidentAheadM);
            if (gs == null || gs.OpponentData == null || gs.SessionData.TrackDefinition == null) return false;
            float length = gs.SessionData.TrackDefinition.trackLength, own = gs.PositionAndMotionData.DistanceRoundTrack;
            if (!(length > 100) || float.IsNaN(own)) return false;
            foreach (var o in gs.OpponentData.Values)
            {
                if (o == null || !o.IsActive || o.InPits || !(o.Speed >= 0) || o.Speed > CrawlingMps) continue;
                float gap = ((o.DistanceRoundTrack - own) % length + length) % length;
                if (gap >= 5 && gap <= 300) return true;
            }
            return false;
        }
    }
}
