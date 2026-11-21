using System;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Rangfolge der Funkregie (Masterprompt Kap. 10): Sicherheit vor Pflicht vor Ereignis vor Daten.</summary>
    internal enum RadioRank { Safety = 0, Mandatory = 1, Event = 2, Data = 3 }

    /// <summary>Entscheidung der Funkregie über eine Meldung.</summary>
    internal enum RadioVerdict { Send, Hold, Drop }

    /// <summary>Lage, in der die Funkregie entscheidet.</summary>
    internal struct RadioSituation
    {
        public bool PlayerOut, StartPhase, SameHazardNamed;
        /// <summary>Zuletzt gesendete Sicherheitsmeldung; MinValue = keine.</summary>
        public DateTime LastSafetyUtc;
        public DateTime Now;
    }

    /// <summary>
    /// Die Funkregie als eine Stelle, die über jede beobachtete Rennmeldung entscheidet, bevor sie zum Lautsprecher geht.
    /// Regeln (jede aus einer Aufnahme vom 21.11.):
    /// - Rob aus dem Rennen (schwarze Flagge, DSQ, Aufgabe): nur noch die Meldung dazu, Strafen und Auswertung (AC Monza).
    /// - Startphase: nur Sicherheit und Pflicht; derselbe Fahrer binnen 10 s nur einmal als Gefahr (RaceRoom Mantorp).
    /// - Datenmeldungen (Status, Zwischenstand, Pace, Coaching) warten 6 s nach einer Sicherheitsmeldung, damit Rob nach
    ///   „Gelb!“ oder „Unfall voraus!“ nicht sofort die Platzstatistik hört.
    /// </summary>
    internal static class RadioDirectorPolicy
    {
        internal static readonly TimeSpan DataAfterSafety = TimeSpan.FromSeconds(6);

        internal static RadioRank Classify(string topic)
        {
            if (string.IsNullOrEmpty(topic)) return RadioRank.Event;
            if (topic == "warning-penalty" || topic.StartsWith("warning-rc-pen", StringComparison.Ordinal) ||
                topic.StartsWith("warning-rc-giveback", StringComparison.Ordinal) || topic == "warning-flag-5" ||
                topic == "race-disqualified" || topic == "warning-tracklimit" || topic.StartsWith("pit-", StringComparison.Ordinal) ||
                topic.StartsWith("damage", StringComparison.Ordinal) || topic.StartsWith("warning-engine", StringComparison.Ordinal))
                return RadioRank.Mandatory;
            if (topic.StartsWith("warning-", StringComparison.Ordinal) || topic.StartsWith("race-hazard", StringComparison.Ordinal) ||
                topic.StartsWith("race-traffic", StringComparison.Ordinal) || topic.StartsWith("race-class", StringComparison.Ordinal) ||
                topic.StartsWith("spotter", StringComparison.Ordinal))
                return RadioRank.Safety;
            if (topic == "status" || topic == "ahead" || topic == "behind" || topic == "progress" || topic == "pace" ||
                topic == "fuel" || topic == "tyres" || topic == "stint" || topic == "vehicle" || topic == "weather" ||
                topic == "race-summary" || topic == "focus-brief" || topic.StartsWith("coach", StringComparison.Ordinal) ||
                topic.StartsWith("insight", StringComparison.Ordinal) || topic == "incident-points" || topic == "resume")
                return RadioRank.Data;
            return RadioRank.Event;
        }

        /// <summary>Was mit der Meldung geschieht, und warum (null bei „senden“).</summary>
        internal static RadioVerdict Decide(string topic, RadioSituation s, out string reason)
        {
            reason = null;
            if (s.PlayerOut && !RaceExclusion.Allowed(topic)) { reason = "player_out_of_race"; return RadioVerdict.Drop; }
            if (s.StartPhase)
            {
                if (!RaceRhythm.StartEssential(topic)) { reason = "race_start_phase_essentials_only"; return RadioVerdict.Drop; }
                if (s.SameHazardNamed) { reason = "race_start_same_hazard_named"; return RadioVerdict.Drop; }
            }
            if (Classify(topic) == RadioRank.Data && s.LastSafetyUtc != DateTime.MinValue && s.Now >= s.LastSafetyUtc &&
                s.Now - s.LastSafetyUtc < DataAfterSafety)
            { reason = "data_waits_after_safety_call"; return RadioVerdict.Hold; }
            return RadioVerdict.Send;
        }
    }
}
