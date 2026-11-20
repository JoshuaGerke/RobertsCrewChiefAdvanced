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
        internal static readonly TimeSpan MaxHold = TimeSpan.FromSeconds(30);

        private static long pressedTicks, releasedTicks;
        private static int talking;

        internal static void Pressed(DateTime nowUtc)
        {
            Interlocked.Exchange(ref pressedTicks, nowUtc.Ticks);
            Interlocked.Exchange(ref talking, 1);
        }

        internal static void Released(DateTime nowUtc)
        {
            Interlocked.Exchange(ref releasedTicks, nowUtc.Ticks);
            Interlocked.Exchange(ref talking, 0);
        }

        internal static void Reset()
        {
            Interlocked.Exchange(ref talking, 0);
            Interlocked.Exchange(ref pressedTicks, 0);
            Interlocked.Exchange(ref releasedTicks, 0);
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
