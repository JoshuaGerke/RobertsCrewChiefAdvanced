using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Restzeit im Zeitrennen (LMU Sebring 23.11., Rob: „Keine Ansagen mehr über die Rennzeit“). Die englischen Zeitansagen von
    /// Crew Chief (noch 15, 10, 5, 2 Minuten) werden durch die Crew ersetzt und gefiltert; der Ersatz war ein Routinebericht und
    /// fiel mit „Ruhig“ weg. Die Marken sind jetzt ein eigenes Rennereignis: Ben sagt sie kurz an, auch wenn die Routine ruht.
    /// Eine Marke wird nur angesagt, wenn sie mindestens ein Viertel der Renndauer vor dem Ende liegt (kein „noch 15 Minuten“
    /// in einem 15-Minuten-Rennen) und das Rennen läuft.
    /// </summary>
    internal sealed class TimeLeftWatch
    {
        internal static readonly int[] Marks = { 15, 10, 5, 2 };
        private int session = int.MinValue, nextMark;
        private ObservedRaceCall call;
        private int variant;

        internal void Reset() { session = int.MinValue; nextMark = 0; call = null; }

        internal void Observe(GameStateData gs, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (call != null && (call.DeliveryStartedUtc != DateTime.MinValue || now > call.ExpiresUtc || call.DeliveryFailed)) call = null;
            if (s.SessionType != SessionType.Race || !s.SessionHasFixedTime || s.SessionPhase != SessionPhase.Green) return;
            float left = s.SessionTimeRemaining, total = s.SessionTotalRunTime;
            if (left <= 0 || total <= 0) return;
            while (nextMark < Marks.Length && left < Marks[nextMark] * 60 - 20) nextMark++; // verpasste Marken nicht nachholen
            if (nextMark >= Marks.Length) return;
            int mark = Marks[nextMark];
            if (left > mark * 60) return;
            nextMark++;
            if (mark * 60 > total * .75f) { LiveDiagnostics.Decision("time-left", "skipped", "mark_too_close_to_race_start", mark + " min"); return; }
            string text = Text(mark, s.ClassPosition, variant++);
            int position = s.ClassPosition;
            call = new ObservedRaceCall
            {
                Topic = "time-left", Text = text, Evidence = "session_time_remaining_crossed_" + mark + "_minutes", Member = TeamMember.Ben,
                Priority = 4, Position = position, SessionId = s.SessionId, SessionType = s.SessionType, DetectedUtc = now,
                ExpiresUtc = now.AddSeconds(40),
                AdditionalValidity = live => live.SessionData.SessionTimeRemaining > (mark - 1) * 60 && live.SessionData.ClassPosition == position
            };
            LiveDiagnostics.Decision(call.Topic, "detected", call.Evidence, text, call.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool enabled)
        {
            if (call == null || busy || !enabled) return null;
            if (!call.IsValid(gs, now))
            {
                if (now > call.ExpiresUtc) { LiveDiagnostics.Decision(call.Topic, "suppressed", "expired", call.Text, call.EventId); call = null; }
                return null;
            }
            return call.TryReserve(now) ? call : null;
        }

        /// <summary>Restzeit mit Platz, fünf Formulierungen je Marke (DE/EN/ES).</summary>
        internal static string Text(int minutes, int position, int variant)
        {
            string m = minutes.ToString(RadioLanguage.Culture), p = position.ToString(RadioLanguage.Culture);
            if (minutes <= 2)
                return AiRadioSchedule.Of(variant,
                    RadioLanguage.T("Noch zwei Minuten. Platz " + p + ", jetzt alles sauber nach Hause.", "Two minutes left. P" + p + ", bring it home clean.", "Quedan dos minutos. P" + p + ", llévalo a casa limpio."),
                    RadioLanguage.T("Zwei Minuten auf der Uhr. Volle Konzentration, Platz " + p + ".", "Two minutes on the clock. Full focus, P" + p + ".", "Dos minutos en el reloj. Concentración total, P" + p + "."),
                    RadioLanguage.T("Letzte zwei Minuten. Du bist Platz " + p + ", keine Fehler jetzt.", "Final two minutes. You're P" + p + ", no mistakes now.", "Últimos dos minutos. Vas P" + p + ", nada de errores ahora."),
                    RadioLanguage.T("Noch zwei Minuten, Platz " + p + ". Gib alles, was sauber geht.", "Two minutes to go, P" + p + ". Give it everything that's clean.", "Dos minutos, P" + p + ". Dalo todo, pero limpio."),
                    RadioLanguage.T("Zwei Minuten. Platz " + p + " halten, dann ist es geschafft.", "Two minutes. Hold P" + p + " and it's done.", "Dos minutos. Mantén la P" + p + " y está hecho."));
            return AiRadioSchedule.Of(variant,
                RadioLanguage.T("Noch " + m + " Minuten. Du liegst auf Platz " + p + ".", m + " minutes left. You're P" + p + ".", "Quedan " + m + " minutos. Vas P" + p + "."),
                RadioLanguage.T(m + " Minuten auf der Uhr, Platz " + p + ".", m + " minutes on the clock, P" + p + ".", m + " minutos en el reloj, P" + p + "."),
                RadioLanguage.T("Zur Info: noch " + m + " Minuten. Platz " + p + ", weiter so.", "For info: " + m + " minutes to go. P" + p + ", keep it up.", "Para que lo sepas: quedan " + m + " minutos. P" + p + ", sigue así."),
                RadioLanguage.T("Noch " + m + " Minuten Rennzeit. Platz " + p + ", Rhythmus halten.", m + " minutes of racing left. P" + p + ", hold the rhythm.", "Quedan " + m + " minutos de carrera. P" + p + ", mantén el ritmo."),
                RadioLanguage.T(m + " Minuten noch. Du bist Platz " + p + ", das passt.", m + " minutes to go. You're P" + p + ", that's good.", "Faltan " + m + " minutos. Vas P" + p + ", va bien."));
        }
    }
}
