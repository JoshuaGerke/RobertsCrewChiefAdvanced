using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Vorfallpunkte (RaceRoom, iRacing) gegen das Limit der Sitzung. Rob wurde am 16.11.2026 bei 30/30 disqualifiziert,
    /// obwohl RaceRoom den Zähler lieferte. Mik meldet jetzt gestaffelt: erster Punkt, Hälfte, drei Viertel, noch drei,
    /// noch einer – jeweils nur beim Anstieg und nur einmal je Stufe. Ohne bestätigtes Limit wird nichts behauptet.
    /// </summary>
    internal sealed class IncidentPointsWatch
    {
        private int session = int.MinValue, last = -1, reportedLevel, variant;
        private ObservedRaceCall call;

        internal static bool Known(SessionData s) =>
            s != null && s.HasLimitedIncidents && s.MaxIncidentCount > 0 && s.MaxIncidentCount < 10000 &&
            s.CurrentIncidentCount >= 0 && s.CurrentIncidentCount <= s.MaxIncidentCount + 50;

        internal void Reset() { session = int.MinValue; last = -1; reportedLevel = 0; call = null; }

        /// <summary>Stufe 0 = nichts, 1 = erster Punkt, 2 = Hälfte, 3 = drei Viertel, 4 = noch höchstens drei, 5 = noch höchstens einer.</summary>
        internal static int Level(int count, int max)
        {
            int left = max - count;
            if (left <= 1) return 5;
            if (left <= 3) return 4;
            if (count * 4 >= max * 3) return 3;
            if (count * 2 >= max) return 2;
            return count > 0 ? 1 : 0;
        }

        internal void Observe(GameStateData gs, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (!Known(s)) return;
            int count = s.CurrentIncidentCount, max = s.MaxIncidentCount;
            if (last < 0) { last = count; reportedLevel = Level(count, max); return; } // Beim Verbinden mitten im Rennen: Ausgangsstand, keine Meldung
            if (count <= last) { last = Math.Min(last, count); return; }
            last = count;
            int level = Level(count, max);
            // Ab Stufe 4 meldet jeder neue Punkt, darunter nur eine neu erreichte Stufe.
            if (level < 4 && level <= reportedLevel) return;
            reportedLevel = Math.Max(reportedLevel, level);
            string text = Text(level, count, max, variant++);
            call = new ObservedRaceCall
            {
                Topic = "incident-points", Text = text, Evidence = "game_incident_counter_and_session_limit", Member = TeamMember.Mik,
                Priority = level >= 4 ? 7 : level >= 2 ? 5 : 3, Position = -1, SessionId = s.SessionId, SessionType = s.SessionType,
                DetectedUtc = now, ExpiresUtc = now.AddSeconds(level >= 4 ? 20 : 30),
                AdditionalValidity = live => live.SessionData.CurrentIncidentCount == count
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        internal static string Text(int level, int count, int max, int variant)
        {
            string c = count.ToString(RadioLanguage.Culture), m = max.ToString(RadioLanguage.Culture), left = (max - count).ToString(RadioLanguage.Culture);
            int v = ((variant % 3) + 3) % 3;
            switch (level)
            {
                case 5:
                    if (count >= max)
                        return RadioLanguage.T("Rob, das Vorfalllimit ist erreicht: " + c + " von " + m + ".",
                            "Rob, the incident limit is reached: " + c + " of " + m + ".", "Rob, límite de incidentes alcanzado: " + c + " de " + m + ".");
                    return v == 0 ? RadioLanguage.T("Achtung Rob, nur noch ein Vorfallpunkt bis zur Disqualifikation. Absolut sauber fahren.",
                            "Careful Rob, one more incident point and you're disqualified. Keep it completely clean.",
                            "Cuidado Rob, un punto de incidente más y te descalifican. Totalmente limpio.")
                        : v == 1 ? RadioLanguage.T(c + " von " + m + " Vorfallpunkten. Noch einer, dann ist Schluss. Kein Risiko mehr.",
                            c + " of " + m + " incident points. One more and it's over. No more risks.",
                            c + " de " + m + " puntos de incidente. Uno más y se acabó. Sin riesgos.")
                        : RadioLanguage.T("Rob, letzte Warnung: ein Punkt bleibt bis zum Limit. Lieber eine Position verlieren als rausfliegen.",
                            "Rob, final warning: one point left to the limit. Better lose a place than get thrown out.",
                            "Rob, último aviso: queda un punto hasta el límite. Mejor perder un puesto que quedar fuera.");
                case 4:
                    return v == 0 ? RadioLanguage.T("Vorfallpunkte jetzt " + c + " von " + m + ". Nur noch " + left + " bis zum Limit, bitte sauber bleiben.",
                            "Incident points now " + c + " of " + m + ". Only " + left + " left to the limit, keep it clean.",
                            "Puntos de incidente: " + c + " de " + m + ". Solo quedan " + left + ", mantente limpio.")
                        : v == 1 ? RadioLanguage.T("Rob, " + left + " Punkte Luft bis zur Disqualifikation. Tracklimits respektieren, keine Berührungen.",
                            "Rob, " + left + " points of margin before disqualification. Respect track limits, no contact.",
                            "Rob, " + left + " puntos de margen antes de la descalificación. Respeta los límites, sin contacto.")
                        : RadioLanguage.T("Es wird eng mit den Vorfallpunkten: " + c + " von " + m + ". Ab jetzt lieber etwas Reserve lassen.",
                            "It's getting tight on incident points: " + c + " of " + m + ". Leave some margin from here.",
                            "Vamos justos de puntos de incidente: " + c + " de " + m + ". Deja algo de margen.");
                case 3:
                    return v == 0 ? RadioLanguage.T("Drei Viertel der Vorfallpunkte sind weg: " + c + " von " + m + ". Ruhiger am Rand, Rob.",
                            "Three quarters of the incident points are gone: " + c + " of " + m + ". Calmer at the edges, Rob.",
                            "Tres cuartos de los puntos de incidente gastados: " + c + " de " + m + ". Más calma en los bordes, Rob.")
                        : RadioLanguage.T(c + " von " + m + " Vorfallpunkten. Noch " + left + " frei, also kein unnötiges Risiko.",
                            c + " of " + m + " incident points. " + left + " left, so no unnecessary risks.",
                            c + " de " + m + " puntos de incidente. Quedan " + left + ", nada de riesgos innecesarios.");
                case 2:
                    return v == 0 ? RadioLanguage.T("Info: Hälfte der Vorfallpunkte erreicht, " + c + " von " + m + ".",
                            "Info: half of the incident points used, " + c + " of " + m + ".", "Info: mitad de los puntos de incidente, " + c + " de " + m + ".")
                        : RadioLanguage.T("Halbzeit bei den Vorfallpunkten: " + c + " von " + m + ". Behalt die Tracklimits im Blick.",
                            "Halfway on incident points: " + c + " of " + m + ". Keep an eye on track limits.",
                            "Mitad de los puntos de incidente: " + c + " de " + m + ". Ojo con los límites de pista.");
                default:
                    return RadioLanguage.T("Erster Vorfallpunkt, Stand " + c + " von " + m + ".",
                        "First incident point, now " + c + " of " + m + ".", "Primer punto de incidente: " + c + " de " + m + ".");
            }
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool mikEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) call = null;
            if (busy || !mikEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }
    }
}
