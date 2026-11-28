using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Vorfallpunkte (RaceRoom, iRacing) und Tracklimit-Verstöße (alle Sims), P7 nach Robs Entscheidung vom 28.11.2026:
    /// <list type="bullet">
    /// <item><b>Mit Limit:</b> je eine Warnung bei einem Drittel, zwei Dritteln, kurz vor dem Limit, beim letzten Punkt und beim
    /// Erreichen – jede Stufe nur einmal und nur beim Anstieg, die Formulierungen wechseln. Kurz vor dem Limit deutlich:
    /// „Rob, jetzt kannst du dir nicht mehr viel erlauben …“.</item>
    /// <item><b>Ohne Limit:</b> trotzdem bis zu drei Hinweise im Rennen, nach Dritteln der Renndistanz (Zeit oder Runden) – nur wenn
    /// im Abschnitt Punkte oder Tracklimit-Verstöße dazugekommen sind, und ohne ein Limit zu behaupten.</item>
    /// </list>
    /// iRacing Navarra 28.11.: Die ersten Punkte (2, 4) kamen in der Startphase, die erste Warnung erst bei 11 von 17, zwei Minuten
    /// später Disqualifikation. Mit Dritteln kommt die erste Warnung bei 6. „Erster Vorfallpunkt, Stand 2 von 17“ (T11) gibt es nicht mehr.
    /// </summary>
    internal sealed class IncidentPointsWatch
    {
        private int session = int.MinValue, last = -1, reportedLevel, variant;
        private ObservedRaceCall call;
        // Ohne Limit: Stand zu Beginn des laufenden Renndrittels und wie viele Hinweise schon kamen.
        private int thirdStartPoints = -1, thirdStartCuts = -1, thirdsChecked;
        private static long lastDetectedTicks;

        internal static bool Known(SessionData s) =>
            s != null && s.HasLimitedIncidents && s.MaxIncidentCount > 0 && s.MaxIncidentCount < 10000 &&
            s.CurrentIncidentCount >= 0 && s.CurrentIncidentCount <= s.MaxIncidentCount + 50;

        /// <summary>Hat Mik Vorfallpunkte innerhalb von <paramref name="span"/> erkannt oder gemeldet (für Bens Fokus-Satz, N10)?</summary>
        internal static bool SpokeRecently(DateTime now, TimeSpan span)
        {
            long t = System.Threading.Interlocked.Read(ref lastDetectedTicks);
            return t > 0 && now.Ticks >= t && now.Ticks - t <= span.Ticks;
        }

        internal void Reset() { session = int.MinValue; last = -1; reportedLevel = 0; call = null; thirdStartPoints = thirdStartCuts = -1; thirdsChecked = 0; }

        /// <summary>Punkte, ab denen „kurz vor dem Limit“ gilt: höchstens ein Sechstel des Limits übrig, mindestens 2.</summary>
        internal static int CloseMargin(int max) => Math.Max(2, max / 6);

        /// <summary>Stufe 0 = nichts, 1 = ein Drittel, 2 = zwei Drittel, 3 = kurz vor dem Limit, 4 = noch ein Punkt, 5 = Limit erreicht.</summary>
        internal static int Level(int count, int max)
        {
            if (max <= 0 || count <= 0) return 0;
            int left = max - count;
            if (left <= 0) return 5;
            if (left <= 1) return 4;
            if (left <= CloseMargin(max)) return 3;
            if (count * 3 >= max * 2) return 2;
            if (count * 3 >= max) return 1;
            return 0;
        }

        /// <summary>Anteil der Renndistanz (0..1) aus Zeit oder Runden; NaN, wenn unbekannt.</summary>
        internal static double RaceProgress(SessionData s)
        {
            if (s == null) return double.NaN;
            if (s.SessionHasFixedTime && s.SessionTotalRunTime > 60) return Math.Max(0, Math.Min(1, s.SessionRunningTime / s.SessionTotalRunTime));
            if (s.SessionNumberOfLaps > 0) return Math.Max(0, Math.Min(1, s.CompletedLaps / (double)s.SessionNumberOfLaps));
            if (s.SessionTotalRunTime > 60) return Math.Max(0, Math.Min(1, s.SessionRunningTime / s.SessionTotalRunTime));
            return double.NaN;
        }

        /// <summary>Prüfpunkte ohne Limit: nach einem Drittel, nach zwei Dritteln und kurz vor Schluss (90 %).</summary>
        internal static readonly double[] ThirdMarks = { 1 / 3.0, 2 / 3.0, .9 };

        internal void Observe(GameStateData gs, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (Known(s)) ObserveLimited(gs, s, now);
            else ObserveThirds(gs, s, now);
        }

        private void ObserveLimited(GameStateData gs, SessionData s, DateTime now)
        {
            int count = s.CurrentIncidentCount, max = s.MaxIncidentCount;
            if (last < 0) { last = count; reportedLevel = Level(count, max); return; } // Beim Verbinden mitten im Rennen: Ausgangsstand, keine Meldung
            if (count <= last) { last = Math.Min(last, count); return; }
            last = count;
            int level = Level(count, max);
            if (level <= reportedLevel) return;  // jede Stufe einmal, nur beim Anstieg
            reportedLevel = level;
            Offer(gs, now, Text(level, count, max, variant++), "game_incident_counter_and_session_limit;level=" + level,
                level >= 3 ? 7 : level == 2 ? 5 : 4, level >= 3 ? 20 : 30, live => live.SessionData.CurrentIncidentCount == count);
        }

        /// <summary>
        /// Ohne Limit (z. B. iRacing ohne Vorfalllimit, Sims nur mit Tracklimit-Zähler): im Rennen nach jedem Renndrittel ein kurzer
        /// Hinweis, wenn im Abschnitt Vorfallpunkte oder Tracklimit-Verstöße dazugekommen sind. Höchstens drei je Rennen.
        /// </summary>
        private void ObserveThirds(GameStateData gs, SessionData s, DateTime now)
        {
            if (s.SessionType != SessionType.Race) return;
            int points = s.CurrentIncidentCount >= 0 && s.CurrentIncidentCount < 10000 ? s.CurrentIncidentCount : -1;
            int cuts = gs.PenaltiesData.CutTrackWarnings >= 0 && gs.PenaltiesData.CutTrackWarnings < 1000 ? gs.PenaltiesData.CutTrackWarnings : -1;
            if (s.SessionPhase != SessionPhase.Green && s.SessionPhase != SessionPhase.Checkered && s.SessionPhase != SessionPhase.FullCourseYellow) return;
            double progress = RaceProgress(s);
            if (double.IsNaN(progress)) return;
            if (thirdStartPoints < 0 && thirdStartCuts < 0)
            {
                // Erst ab dem Start zählen; wer mitten im Rennen verbindet, bekommt erst den nächsten vollen Abschnitt.
                thirdStartPoints = points; thirdStartCuts = cuts;
                while (thirdsChecked < ThirdMarks.Length && progress >= ThirdMarks[thirdsChecked]) thirdsChecked++;
                return;
            }
            if (thirdsChecked >= ThirdMarks.Length || progress < ThirdMarks[thirdsChecked]) return;
            int mark = thirdsChecked++;
            int newPoints = points >= 0 && thirdStartPoints >= 0 ? points - thirdStartPoints : 0;
            int newCuts = cuts >= 0 && thirdStartCuts >= 0 ? cuts - thirdStartCuts : 0;
            thirdStartPoints = points; thirdStartCuts = cuts;
            if (newPoints <= 0 && newCuts <= 0) { LiveDiagnostics.Decision("incident-points", "skipped", "race_third_" + (mark + 1) + "_clean"); return; }
            Offer(gs, now, ThirdText(mark, Math.Max(0, newPoints), points, Math.Max(0, newCuts), variant++),
                "race_third_" + (mark + 1) + "_without_limit;points+" + newPoints + ";cuts+" + newCuts, 3, 40, null);
        }

        private void Offer(GameStateData gs, DateTime now, string text, string evidence, int priority, int seconds, Func<GameStateData, bool> valid)
        {
            var s = gs.SessionData;
            call = new ObservedRaceCall
            {
                Topic = "incident-points", Text = text, Evidence = evidence, Member = TeamMember.Mik,
                Priority = priority, Position = -1, SessionId = s.SessionId, SessionType = s.SessionType,
                DetectedUtc = now, ExpiresUtc = now.AddSeconds(seconds), AdditionalValidity = valid
            };
            System.Threading.Interlocked.Exchange(ref lastDetectedTicks, now.Ticks);
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        /// <summary>Hinweis nach einem Renndrittel ohne Limit. Kein Limit behaupten, nur was dazukam.</summary>
        internal static string ThirdText(int mark, int newPoints, int totalPoints, int newCuts, int variant)
        {
            string part = mark == 0 ? RadioLanguage.T("im ersten Renndrittel", "in the first third of the race", "en el primer tercio de la carrera") :
                mark == 1 ? RadioLanguage.T("im zweiten Drittel", "in the second third", "en el segundo tercio") :
                RadioLanguage.T("seit dem zweiten Drittel", "since the second third", "desde el segundo tercio");
            string what;
            string pts = newPoints == 1 ? RadioLanguage.T("ein Vorfallpunkt", "one incident point", "un punto de incidente") :
                newPoints.ToString(RadioLanguage.Culture) + RadioLanguage.T(" Vorfallpunkte", " incident points", " puntos de incidente");
            string cut = newCuts == 1 ? RadioLanguage.T("ein Tracklimit-Verstoß", "one track-limit warning", "un aviso de límites de pista") :
                newCuts.ToString(RadioLanguage.Culture) + RadioLanguage.T(" Tracklimit-Verstöße", " track-limit warnings", " avisos de límites de pista");
            if (newPoints > 0 && newCuts > 0) what = pts + RadioLanguage.T(" und ", " and ", " y ") + cut;
            else what = newPoints > 0 ? pts : cut;
            string total = newPoints > 0 && totalPoints > newPoints
                ? RadioLanguage.T(", insgesamt " + totalPoints, ", " + totalPoints + " in total", ", " + totalPoints + " en total") : "";
            return RadioVariants.At(variant,
                RadioLanguage.T("Info: " + part + " " + what + total + ". Sauber bleiben, Rob.", "Info: " + what + " " + part + total + ". Keep it clean, Rob.", "Info: " + what + " " + part + total + ". Limpio, Rob."),
                RadioLanguage.T("Kurzer Stand " + part + ": " + what + total + ". Etwas ruhiger an den Randsteinen.", "Quick tally " + part + ": " + what + total + ". A bit calmer on the kerbs.", "Balance " + part + ": " + what + total + ". Algo más de calma en los pianos."),
                RadioLanguage.T("Rob, " + part + " kamen " + what + total + " dazu. Lieber eine Zehntel liegen lassen.", "Rob, " + what + " " + part + total + ". Better leave a tenth.", "Rob, " + what + " " + part + total + ". Mejor dejar una décima."));
        }

        internal static string Text(int level, int count, int max, int variant)
        {
            string c = count.ToString(RadioLanguage.Culture), m = max.ToString(RadioLanguage.Culture), left = (max - count).ToString(RadioLanguage.Culture);
            switch (level)
            {
                case 5:
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Rob, das Vorfalllimit ist erreicht: " + c + " von " + m + ".", "Rob, the incident limit is reached: " + c + " of " + m + ".", "Rob, límite de incidentes alcanzado: " + c + " de " + m + "."),
                        RadioLanguage.T("Limit erreicht, Rob: " + c + " von " + m + " Vorfallpunkten.", "Limit reached, Rob: " + c + " of " + m + " incident points.", "Límite alcanzado, Rob: " + c + " de " + m + " puntos de incidente."),
                        RadioLanguage.T("Rob, " + c + " von " + m + " – die Vorfallpunkte sind aufgebraucht.", "Rob, " + c + " of " + m + " – the incident points are used up.", "Rob, " + c + " de " + m + ": puntos de incidente agotados."));
                case 4:
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Achtung Rob, nur noch ein Vorfallpunkt bis zur Disqualifikation. Absolut sauber fahren.",
                            "Careful Rob, one more incident point and you're disqualified. Keep it completely clean.",
                            "Cuidado Rob, un punto de incidente más y te descalifican. Totalmente limpio."),
                        RadioLanguage.T(c + " von " + m + " Vorfallpunkten. Noch einer, dann ist Schluss. Kein Risiko mehr.",
                            c + " of " + m + " incident points. One more and it's over. No more risks.",
                            c + " de " + m + " puntos de incidente. Uno más y se acabó. Sin riesgos."),
                        RadioLanguage.T("Rob, letzte Warnung: ein Punkt bleibt bis zum Limit. Lieber eine Position verlieren als rausfliegen.",
                            "Rob, final warning: one point left to the limit. Better lose a place than get thrown out.",
                            "Rob, último aviso: queda un punto hasta el límite. Mejor perder un puesto que quedar fuera."));
                case 3:
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Rob, jetzt kannst du dir nicht mehr viel erlauben: " + c + " von " + m + " Vorfallpunkten, noch " + left + " bis zur Disqualifikation.",
                            "Rob, you can't afford much more now: " + c + " of " + m + " incident points, " + left + " left before disqualification.",
                            "Rob, ya no te puedes permitir mucho: " + c + " de " + m + " puntos de incidente, quedan " + left + " antes de la descalificación."),
                        RadioLanguage.T("Es wird eng, Rob: " + c + " von " + m + ". Nur noch " + left + " Punkte Luft, ab jetzt kein Risiko mehr.",
                            "It's getting tight, Rob: " + c + " of " + m + ". Only " + left + " points of margin, no more risks from here.",
                            "Vamos justos, Rob: " + c + " de " + m + ". Solo " + left + " puntos de margen, sin riesgos desde ahora."),
                        RadioLanguage.T(c + " von " + m + " Vorfallpunkten – kurz vor dem Limit. Lieber eine Position hergeben als rausfliegen.",
                            c + " of " + m + " incident points – close to the limit. Better give up a place than get thrown out.",
                            c + " de " + m + " puntos de incidente, cerca del límite. Mejor ceder un puesto que quedar fuera."));
                case 2:
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Zwei Drittel der Vorfallpunkte sind weg: " + c + " von " + m + ". Ruhiger am Rand, Rob.",
                            "Two thirds of the incident points are gone: " + c + " of " + m + ". Calmer at the edges, Rob.",
                            "Dos tercios de los puntos de incidente gastados: " + c + " de " + m + ". Más calma en los bordes, Rob."),
                        RadioLanguage.T(c + " von " + m + " Vorfallpunkten. Noch " + left + " frei, also kein unnötiges Risiko.",
                            c + " of " + m + " incident points. " + left + " left, so no unnecessary risks.",
                            c + " de " + m + " puntos de incidente. Quedan " + left + ", nada de riesgos innecesarios."),
                        RadioLanguage.T("Vorfallpunkte bei " + c + " von " + m + ". Zwei Drittel weg, jetzt Zweikämpfe mit Bedacht.",
                            "Incident points at " + c + " of " + m + ". Two thirds gone, pick your battles now.",
                            "Puntos de incidente en " + c + " de " + m + ". Dos tercios gastados, elige bien tus batallas."));
                default:
                    return RadioVariants.At(variant,
                        RadioLanguage.T("Info: ein Drittel der Vorfallpunkte ist weg, " + c + " von " + m + ".",
                            "Info: a third of the incident points are gone, " + c + " of " + m + ".", "Info: un tercio de los puntos de incidente gastados, " + c + " de " + m + "."),
                        RadioLanguage.T("Vorfallpunkte jetzt " + c + " von " + m + ", ein Drittel ist verbraucht. Behalt die Tracklimits im Blick.",
                            "Incident points now " + c + " of " + m + ", a third used. Keep an eye on track limits.",
                            "Puntos de incidente: " + c + " de " + m + ", un tercio gastado. Ojo con los límites de pista."),
                        RadioLanguage.T("Zur Info: " + c + " von " + m + " Vorfallpunkten, noch " + left + " frei.",
                            "For info: " + c + " of " + m + " incident points, " + left + " left.",
                            "Para que lo sepas: " + c + " de " + m + " puntos de incidente, quedan " + left + "."));
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
