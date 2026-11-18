using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    internal enum DriverMoodState { Neutral, Flow, Pressure, Frustration }

    /// <summary>
    /// C7 Rennstimmung: Wie läuft es gerade für den Fahrer? Nur aus Fakten der letzten drei sauberen Runden – Plätze
    /// verloren, ungültige Runden, Konstanz, Hintermann direkt dran, knappe Vorfallpunkte. Steuert Ton und Timing: Wer
    /// frustriert ist, bekommt einen Punkt nach dem anderen und aufbauend formuliert, wer unter Druck steht, hört Kritik
    /// erst, wenn Luft ist. Keine Gefühle raten: ohne genug Runden bleibt die Stimmung neutral.
    /// </summary>
    internal static class DriverMood
    {
        internal const int Window = 3;
        internal const float FlowSpread = .4f, CloseBehind = 1f;

        internal static DriverMoodState Assess(IList<LapRecord> laps, bool incidentsTight, out string reason)
        {
            reason = "zu_wenig_runden";
            var recent = (laps ?? new List<LapRecord>()).Where(l => l != null && !l.InPitLap && l.Position > 0).Reverse().Take(Window + 1).Reverse().ToList();
            if (recent.Count < Window) return DriverMoodState.Neutral;
            var window = recent.Skip(recent.Count - Window).ToList();
            int startPosition = recent.Count > Window ? recent[0].Position : window[0].Position;
            int lost = window.Last().Position - startPosition;
            int invalid = window.Count(l => !l.Valid);
            if (lost >= 2 || invalid >= 2)
            {
                reason = lost >= 2 ? "plaetze_verloren_" + lost : "ungueltige_runden_" + invalid;
                return DriverMoodState.Frustration;
            }
            bool rearClose = window.Skip(Window - 2).All(l => l.GapBehind > 0 && l.GapBehind < CloseBehind);
            if (rearClose || incidentsTight)
            {
                reason = rearClose ? "hintermann_zwei_runden_unter_einer_sekunde" : "vorfallpunkte_knapp";
                return DriverMoodState.Pressure;
            }
            var times = window.Where(l => l.Valid && l.LapTime > 10 && l.LapTime < 3600).Select(l => l.LapTime).ToList();
            if (times.Count == Window && lost <= 0 && times.Max() - times.Min() <= FlowSpread)
            {
                reason = "drei_gueltige_runden_innerhalb_" + FlowSpread.ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";
                return DriverMoodState.Flow;
            }
            reason = "unauffaellig";
            return DriverMoodState.Neutral;
        }

        internal static string Name(DriverMoodState m)
        {
            switch (m)
            {
                case DriverMoodState.Flow: return "im_fluss";
                case DriverMoodState.Pressure: return "unter_druck";
                case DriverMoodState.Frustration: return "frustriert";
                default: return "neutral";
            }
        }

        /// <summary>Tonvorgabe für Ben und Mik in freien Antworten.</summary>
        internal static string Tone(DriverMoodState m)
        {
            switch (m)
            {
                case DriverMoodState.Flow: return "Läuft rund: kurz bestätigen und loben, Hinweise knapp halten.";
                case DriverMoodState.Pressure: return "Druck von hinten oder knappe Vorfallpunkte: knapp, ruhig, nur das Nötige.";
                case DriverMoodState.Frustration: return "Es läuft gerade nicht: ruhig und aufbauend, höchstens ein konkreter Punkt, keine Fehlerliste.";
                default: return "Normaler Ton.";
            }
        }

        /// <summary>Aufbauender Einstieg für einen Fahrtechnik-Hinweis, wenn es gerade nicht läuft (Rob 18.11.: Kritik bleibt, nur menschlicher).</summary>
        internal static string Softener() => RadioVariants.Pick("coaching-soft",
            RadioLanguage.T("Kopf hoch, das wird. Eine Sache noch:", "Chin up, it's coming. Just one thing:", "Ánimo, ya llegará. Solo una cosa:"),
            RadioLanguage.T("Ganz ruhig, nur ein Punkt:", "Easy now, just one point:", "Tranquilo, solo un detalle:"),
            RadioLanguage.T("Kein Stress, ein Tipp für dich:", "No stress, one tip for you:", "Sin estrés, un consejo:"),
            RadioLanguage.T("Du bist gleich wieder drin. Achte mal hierauf:", "You'll be back in it soon. Watch this:", "Enseguida vuelves a estar dentro. Fíjate en esto:"),
            RadioLanguage.T("Durchatmen, eine Kleinigkeit:", "Breathe, one small thing:", "Respira, un pequeño detalle:"));

        /// <summary>Fahrtechnik-Kritik und Kurven-Coaching, deren Timing der Stimmung folgt.</summary>
        internal static bool IsCoaching(string topic) =>
            topic == "corner-coaching" || topic == "insight-lockup" || topic == "insight-wheelspin" ||
            topic == "insight-balance" || topic == "insight-offtrack";
    }
}
