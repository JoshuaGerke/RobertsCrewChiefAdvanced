using System;
using System.Threading;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Funkstille, solange der Fahrer spricht (Rob 16.11.2026: „sobald ich den Funkknopf drücke, müssen alle Ansagen pausieren,
    /// in Warteschlange danach kommen“). Während die Funktaste gedrückt ist und kurz danach startet keine Ansage – weder
    /// Crew Chief noch KI-Team. Die Warteschlange bleibt erhalten und läuft danach weiter; veraltete Meldungen fallen über ihre
    /// normale Gültigkeitsprüfung heraus. Einzige Ausnahme sind Saras Seitenwarnungen (Auto neben dir), weil sie die Sicherheit
    /// betreffen und nach dem Loslassen bereits veraltet wären.
    /// </summary>
    internal static class DriverRadioGate
    {
        /// <summary>Kurze Ruhe nach dem Loslassen, damit die Antwort auf die Frage nicht von einer Routinemeldung überholt wird.</summary>
        internal static readonly TimeSpan AfterRelease = TimeSpan.FromSeconds(1.5);
        /// <summary>Schutz gegen eine hängende Taste: länger als so lange gilt die Funkstille nicht.</summary>
        /// <summary>Monza 21.11.: Nach 30 s Funkspruch sprach Ben in Robs Satz hinein – die Grenze war zu knapp. Sie folgt jetzt der
        /// längsten Aufnahme plus Reserve.</summary>
        internal static readonly TimeSpan MaxHold = TimeSpan.FromSeconds(AiMicRecorder.MaxSeconds + 5);

        private static long pressedTicks, releasedTicks;
        private static int talking;
        /// <summary>
        /// RaceRoom Hockenheim 23.11.: 17 fertig gesprochene Meldungen verfielen in der Warteschlange, gehäuft genau dann, wenn Rob
        /// funkte oder auf eine Antwort wartete – darunter „Zeit ist um, jetzt kommt die letzte Runde!“ (21:47:30) und Saras Warnung
        /// vor Miky Favia. Die Zeit, in der Rob spricht oder die Crew ihm antwortet, zählt nicht gegen die Lebensdauer einer
        /// Meldung: Sie wird gutgeschrieben, höchstens <see cref="MaxCredit"/>.
        /// </summary>
        internal static readonly TimeSpan MaxCredit = TimeSpan.FromSeconds(25);
        private static long heldTicksDone, answerTicksDone, answerSinceTicks;
        private static int answers;
        private static readonly object answerGate = new object();

        internal static void Pressed(DateTime nowUtc)
        {
            Interlocked.Exchange(ref pressedTicks, nowUtc.Ticks);
            Interlocked.Exchange(ref talking, 1);
        }

        internal static void Released(DateTime nowUtc)
        {
            long pressed = Interlocked.Read(ref pressedTicks);
            if (Interlocked.Exchange(ref talking, 0) == 1 && pressed > 0 && nowUtc.Ticks > pressed)
                Interlocked.Add(ref heldTicksDone, Math.Min(nowUtc.Ticks - pressed, MaxHold.Ticks) + AfterRelease.Ticks);
            Interlocked.Exchange(ref releasedTicks, nowUtc.Ticks);
        }

        internal static void Reset()
        {
            Interlocked.Exchange(ref talking, 0);
            Interlocked.Exchange(ref pressedTicks, 0);
            Interlocked.Exchange(ref releasedTicks, 0);
            Interlocked.Exchange(ref heldTicksDone, 0);
            lock (answerGate) { answers = 0; answerTicksDone = 0; answerSinceTicks = 0; }
        }

        /// <summary>Eine Antwort an Rob wird vorbereitet (Frage erkannt bis Antwort eingereiht).</summary>
        internal static void AnswerStarted(DateTime nowUtc)
        {
            lock (answerGate) { if (answers++ == 0) answerSinceTicks = nowUtc.Ticks; }
        }

        internal static void AnswerEnded(DateTime nowUtc)
        {
            lock (answerGate)
            {
                if (answers == 0) return;
                if (--answers == 0 && answerSinceTicks > 0 && nowUtc.Ticks > answerSinceTicks) answerTicksDone += nowUtc.Ticks - answerSinceTicks;
            }
        }

        /// <summary>Gesamte Zeit (Ticks), in der Rob gefunkt hat oder auf eine Antwort wartete; steigt nur.</summary>
        internal static long BusyTicks(DateTime nowUtc)
        {
            long total = Interlocked.Read(ref heldTicksDone);
            long pressed = Interlocked.Read(ref pressedTicks);
            if (Volatile.Read(ref talking) == 1 && pressed > 0 && nowUtc.Ticks > pressed) total += Math.Min(nowUtc.Ticks - pressed, MaxHold.Ticks);
            lock (answerGate)
            {
                total += answerTicksDone;
                if (answers > 0 && answerSinceTicks > 0 && nowUtc.Ticks > answerSinceTicks) total += nowUtc.Ticks - answerSinceTicks;
            }
            return total;
        }

        /// <summary>Gutschrift für eine Meldung, die bei <paramref name="busyAtStart"/> eingereiht wurde.</summary>
        internal static TimeSpan Credit(long busyAtStart, DateTime nowUtc)
        {
            long d = BusyTicks(nowUtc) - busyAtStart;
            return d <= 0 ? TimeSpan.Zero : d >= MaxCredit.Ticks ? MaxCredit : TimeSpan.FromTicks(d);
        }

        /// <summary>True, solange die Funktaste gedrückt ist (höchstens <see cref="MaxHold"/>) und bis <see cref="AfterRelease"/> danach.</summary>
        internal static bool Holding(DateTime nowUtc)
        {
            long pressed = Interlocked.Read(ref pressedTicks), released = Interlocked.Read(ref releasedTicks);
            if (Volatile.Read(ref talking) == 1)
                return pressed > 0 && nowUtc.Ticks - pressed < MaxHold.Ticks;
            return released > 0 && nowUtc.Ticks >= released && nowUtc.Ticks - released < AfterRelease.Ticks;
        }

        /// <summary>Darf diese Meldung jetzt starten? Seitenwarnungen des Spotters immer, alles andere nicht während der Funkstille.</summary>
        internal static bool MayStart(bool spotter, DateTime nowUtc) => spotter || !Holding(nowUtc);

        /// <summary>
        /// Wartet vor dem Abspielen, solange Funkstille gilt. Fängt den Fall ab, dass eine Ansage schon ausgewählt, aber noch
        /// nicht gestartet ist, wenn der Fahrer drückt (Quali 20.11.: Mik sprach 0,4 s nach dem Drücken los und redete über Rob).
        /// Gibt die gewartete Zeit in Millisekunden zurück.
        /// </summary>
        internal static int WaitForSilence(bool exempt, Func<DateTime> utcNow, Action<int> sleep, int stepMs = 50)
        {
            if (exempt || utcNow == null || sleep == null) return 0;
            int waited = 0, limit = (int)(MaxHold + AfterRelease).TotalMilliseconds;
            while (waited < limit && Holding(utcNow()))
            {
                sleep(stepMs);
                waited += stepMs;
            }
            return waited;
        }
    }
}
