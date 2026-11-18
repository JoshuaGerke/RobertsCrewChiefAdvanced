using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    internal enum DriverMoodState { Neutral, Flow, Pressure, Frustration }

    /// <summary>
    /// C7 Rennstimmung: Wie läuft es gerade für den Fahrer? Nur aus Fakten der letzten drei sauberen Runden – Plätze
    /// verloren, ungültige Runden, Konstanz, Hintermann direkt dran, knappe Vorfallpunkte. Steuert den Ton: Wer frustriert
    /// ist, bekommt keine Fehlerliste, wer im Fluss ist, wird nicht mit Coaching gestört, wer unter Druck steht, hört nur
    /// das Nötige. Keine Gefühle raten: ohne genug Runden bleibt die Stimmung neutral.
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
                case DriverMoodState.Flow: return "Läuft rund: kurz bestätigen, nicht mit Coaching stören.";
                case DriverMoodState.Pressure: return "Druck von hinten oder knappe Vorfallpunkte: knapp, ruhig, nur das Nötige.";
                case DriverMoodState.Frustration: return "Es läuft gerade nicht: ruhig und aufbauend, höchstens ein konkreter Punkt, keine Fehlerliste.";
                default: return "Normaler Ton.";
            }
        }

        /// <summary>Fahrtechnik-Kritik und Kurven-Coaching, die je nach Stimmung warten oder entfallen.</summary>
        internal static bool IsCoaching(string topic) =>
            topic == "corner-coaching" || topic == "insight-lockup" || topic == "insight-wheelspin" ||
            topic == "insight-balance" || topic == "insight-offtrack";
    }
}
