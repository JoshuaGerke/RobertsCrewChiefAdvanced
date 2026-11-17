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
            switch (level)
            {
                case 5:
                    if (count >= max)
                        return RadioVariants.At(variant,
                            RadioLanguage.T("Rob, das Vorfalllimit ist erreicht: " + c + " von " + m + ".", "Rob, the incident limit is reached: " + c + " of " + m + ".", "Rob, límite de incidentes alcanzado: " + c + " de " + m + "."),
                            RadioLanguage.T("Limit erreicht, Rob: " + c + " von " + m + " Vorfallpunkten.", "Limit reached, Rob: " + c + " of " + m + " incident points.", "Límite alcanzado, Rob: " + c + " de " + m + " puntos de incidente."),
                            RadioLanguage.T("Rob, " + c + " von " + m + " – die Vorfallpunkte sind aufgebraucht.", "Rob, " + c + " of " + m + " – the incident points are used up.", "Rob, " + c + " de " + m + ": puntos de incidente agotados."),
                            RadioLanguage.T("Vorfallpunkte am Anschlag: " + c + " von " + m + ".", "Incident points maxed out: " + c + " of " + m + ".", "Puntos de incidente al tope: " + c + " de " + m + "."));
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Achtung Rob, nur noch ein Vorfallpunkt bis zur Disqualifikation. Absolut sauber fahren.",
                            "Careful Rob, one more incident point and you're disqualified. Keep it completely clean.",
                            "Cuidado Rob, un punto de incidente más y te descalifican. Totalmente limpio."),
                        RadioLanguage.T(c + " von " + m + " Vorfallpunkten. Noch einer, dann ist Schluss. Kein Risiko mehr.",
                            c + " of " + m + " incident points. One more and it's over. No more risks.",
                            c + " de " + m + " puntos de incidente. Uno más y se acabó. Sin riesgos."),
                        RadioLanguage.T("Rob, letzte Warnung: ein Punkt bleibt bis zum Limit. Lieber eine Position verlieren als rausfliegen.",
                            "Rob, final warning: one point left to the limit. Better lose a place than get thrown out.",
                            "Rob, último aviso: queda un punto hasta el límite. Mejor perder un puesto que quedar fuera."),
                        RadioLanguage.T("Ein einziger Punkt Luft, Rob. Jede Berührung, jeder Ausritt kann das Rennen beenden.",
                            "Just one point of margin, Rob. Any contact or trip off could end your race.",
                            "Solo un punto de margen, Rob. Cualquier toque o salida puede acabar tu carrera."));
                case 4:
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Vorfallpunkte jetzt " + c + " von " + m + ". Nur noch " + left + " bis zum Limit, bitte sauber bleiben.",
                            "Incident points now " + c + " of " + m + ". Only " + left + " left to the limit, keep it clean.",
                            "Puntos de incidente: " + c + " de " + m + ". Solo quedan " + left + ", mantente limpio."),
                        RadioLanguage.T("Rob, " + left + " Punkte Luft bis zur Disqualifikation. Tracklimits respektieren, keine Berührungen.",
                            "Rob, " + left + " points of margin before disqualification. Respect track limits, no contact.",
                            "Rob, " + left + " puntos de margen antes de la descalificación. Respeta los límites, sin contacto."),
                        RadioLanguage.T("Es wird eng mit den Vorfallpunkten: " + c + " von " + m + ". Ab jetzt lieber etwas Reserve lassen.",
                            "It's getting tight on incident points: " + c + " of " + m + ". Leave some margin from here.",
                            "Vamos justos de puntos de incidente: " + c + " de " + m + ". Deja algo de margen."),
                        RadioLanguage.T(c + " von " + m + " Vorfallpunkten – nur noch " + left + " frei. Zweikämpfe mit Bedacht.",
                            c + " of " + m + " incident points – only " + left + " left. Pick your battles.",
                            c + " de " + m + " puntos de incidente, solo quedan " + left + ". Elige bien tus batallas."));
                case 3:
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Drei Viertel der Vorfallpunkte sind weg: " + c + " von " + m + ". Ruhiger am Rand, Rob.",
                            "Three quarters of the incident points are gone: " + c + " of " + m + ". Calmer at the edges, Rob.",
                            "Tres cuartos de los puntos de incidente gastados: " + c + " de " + m + ". Más calma en los bordes, Rob."),
                        RadioLanguage.T(c + " von " + m + " Vorfallpunkten. Noch " + left + " frei, also kein unnötiges Risiko.",
                            c + " of " + m + " incident points. " + left + " left, so no unnecessary risks.",
                            c + " de " + m + " puntos de incidente. Quedan " + left + ", nada de riesgos innecesarios."),
                        RadioLanguage.T("Vorfallpunkte bei " + c + " von " + m + ". Es wird Zeit, die Limits zu respektieren.",
                            "Incident points at " + c + " of " + m + ". Time to respect the limits.",
                            "Puntos de incidente en " + c + " de " + m + ". Toca respetar los límites."),
                        RadioLanguage.T("Info, Rob: " + c + " von " + m + " Punkten. Noch " + left + " Reserve – gut haushalten.",
                            "Info, Rob: " + c + " of " + m + " points. " + left + " in reserve – use them wisely.",
                            "Info, Rob: " + c + " de " + m + " puntos. Quedan " + left + " de reserva, adminístralos bien."));
                case 2:
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Info: Hälfte der Vorfallpunkte erreicht, " + c + " von " + m + ".",
                            "Info: half of the incident points used, " + c + " of " + m + ".", "Info: mitad de los puntos de incidente, " + c + " de " + m + "."),
                        RadioLanguage.T("Halbzeit bei den Vorfallpunkten: " + c + " von " + m + ". Behalt die Tracklimits im Blick.",
                            "Halfway on incident points: " + c + " of " + m + ". Keep an eye on track limits.",
                            "Mitad de los puntos de incidente: " + c + " de " + m + ". Ojo con los límites de pista."),
                        RadioLanguage.T(c + " von " + m + " Vorfallpunkten – die Hälfte ist weg.", c + " of " + m + " incident points – half are gone.", c + " de " + m + " puntos de incidente: la mitad gastados."),
                        RadioLanguage.T("Zur Info: Vorfallpunkte stehen bei " + c + " von " + m + ".", "For info: incident points at " + c + " of " + m + ".", "Para que lo sepas: puntos de incidente en " + c + " de " + m + "."));
                default:
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Erster Vorfallpunkt, Stand " + c + " von " + m + ".", "First incident point, now " + c + " of " + m + ".", "Primer punto de incidente: " + c + " de " + m + "."),
                        RadioLanguage.T("Vorfallpunkte: " + c + " von " + m + ".", "Incident points: " + c + " of " + m + ".", "Puntos de incidente: " + c + " de " + m + "."),
                        RadioLanguage.T("Ein Vorfallpunkt notiert, jetzt " + c + " von " + m + ".", "Incident point noted, now " + c + " of " + m + ".", "Punto de incidente anotado, ahora " + c + " de " + m + "."),
                        RadioLanguage.T("Kleiner Hinweis: " + c + " von " + m + " Vorfallpunkten.", "Quick note: " + c + " of " + m + " incident points.", "Un apunte: " + c + " de " + m + " puntos de incidente."));
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
