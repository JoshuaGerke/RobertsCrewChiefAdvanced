using System;
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
