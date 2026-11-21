using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    internal enum RaceLength { Unknown, Short, Medium, Long }

    /// <summary>
    /// Rob 21.11.: „Am Anfang des Starts sollte man den Fahrer nur mit dem Nötigsten versorgen. Wenn sich das Grid beruhigt hat,
    /// dann sollte man alle Dinge, die wir standardmäßig als Routine abrufen, nach und nach einbringen. Die Crew sollte erkennen,
    /// ob es ein langes, mittleres oder kurzes Rennen ist, und danach ihre Routine etwas anpassen.“
    /// RaceRoom Mantorp 21.11.: In den ersten 46 s nach Grün kamen 10 Meldungen, 4 davon unterbrochen – Platzverlust,
    /// Vorfallpunkte und „Runde ungültig“ mitten im Startgetümmel.
    /// Startphase: ab Grün, bis Rob die erste Runde fertig hat, seit Grün mindestens 45 s vergangen sind und sein Platz 10 s
    /// gehalten hat; spätestens nach 3 Minuten gilt das Feld als beruhigt.
    /// </summary>
    internal sealed class RaceRhythm
    {
        internal static readonly TimeSpan MinStart = TimeSpan.FromSeconds(45), StablePlace = TimeSpan.FromSeconds(10),
            MaxStart = TimeSpan.FromMinutes(3);
        /// <summary>Erste Routine frühestens so lange nach dem Beruhigen, danach je Thema dieser Abstand.</summary>
        internal static readonly TimeSpan FirstRoutineAfterSettle = TimeSpan.FromSeconds(15), RoutineStep = TimeSpan.FromSeconds(40);

        private string session;
        private DateTime greenUtc = DateTime.MinValue, placeSinceUtc = DateTime.MinValue;
        private int place;
        private bool settled;

        internal DateTime SettledUtc { get; private set; } = DateTime.MinValue;
        internal RaceLength Length { get; private set; }

        internal static bool Settled(int completedLaps, TimeSpan sinceGreen, TimeSpan samePlace) =>
            sinceGreen >= MaxStart || completedLaps >= 1 && sinceGreen >= MinStart && samePlace >= StablePlace;

        /// <summary>
        /// Rennlänge in Minuten: feste Zeit direkt, sonst Runden mal bekannte Rundenzeit, ohne Rundenzeit nach Rundenzahl.
        /// Kurz bis 20 Minuten (bzw. 12 Runden), mittel bis 45 Minuten (bzw. 30 Runden), darüber lang.
        /// </summary>
        internal static RaceLength Classify(bool fixedTime, float totalSeconds, int laps, float lapSeconds)
        {
            double minutes = -1;
            if (fixedTime && totalSeconds > 0) minutes = totalSeconds / 60.0;
            else if (laps > 0 && lapSeconds > 20) minutes = laps * lapSeconds / 60.0;
            if (minutes > 0) return minutes <= 20 ? RaceLength.Short : minutes <= 45 ? RaceLength.Medium : RaceLength.Long;
            if (laps > 0) return laps <= 12 ? RaceLength.Short : laps <= 30 ? RaceLength.Medium : RaceLength.Long;
            return RaceLength.Unknown;
        }

        internal static RaceLength Classify(GameStateData gs)
        {
            if (gs == null || gs.SessionData.SessionType != SessionType.Race) return RaceLength.Unknown;
            var s = gs.SessionData;
            float lap = s.PlayerLapTimeSessionBest > 0 ? s.PlayerLapTimeSessionBest : s.LapTimePrevious;
            return Classify(s.SessionHasFixedTime, s.SessionTotalRunTime, s.SessionNumberOfLaps, lap);
        }

        /// <summary>
        /// Faktor auf den eingestellten Abstand je Routinethema. Kurzes Rennen: Sprit, Reifen, Stint und Wetter halb so oft,
        /// Position und Abstände wie eingestellt. Langes Rennen: Sprit, Reifen und Stint etwas öfter, Position und Abstände
        /// etwas seltener. Mittel und unbekannt: wie eingestellt.
        /// </summary>
        internal static double Scale(RaceLength length, string topic)
        {
            switch (length)
            {
                case RaceLength.Short:
                    return topic == "fuel" || topic == "tyres" || topic == "stint" || topic == "weather" ? 2.0 : topic == "vehicle" ? 1.5 : 1.0;
                case RaceLength.Long:
                    return topic == "fuel" || topic == "tyres" || topic == "stint" ? 0.85 :
                        topic == "status" || topic == "ahead" || topic == "behind" ? 1.25 : 1.0;
                default: return 1.0;
            }
        }

        /// <summary>
        /// Was in der Startphase gesendet werden darf: Sicherheit, Flaggen, Strafen, Tracklimits, Boxengasse, Schäden und der
        /// Startspruch. Platzverlust, Vorfallpunkte, „Runde ungültig“, Zwischenstände und Routine warten.
        /// </summary>
        internal static bool StartEssential(string topic)
        {
            if (string.IsNullOrEmpty(topic)) return true; // ohne Thema: Antworten an Rob und Crew-Chief-Kernmeldungen
            if (topic == "warning-invalid-lap") return false;
            return topic.StartsWith("warning-", StringComparison.Ordinal) || topic.StartsWith("race-hazard", StringComparison.Ordinal) ||
                topic.StartsWith("race-traffic", StringComparison.Ordinal) || topic.StartsWith("race-class", StringComparison.Ordinal) ||
                topic.StartsWith("pit-", StringComparison.Ordinal) || topic.StartsWith("penalty", StringComparison.Ordinal) ||
                topic.StartsWith("damage", StringComparison.Ordinal) || topic.StartsWith("spotter", StringComparison.Ordinal) ||
                topic == "race-start" || topic == "race-disqualified" || topic == "race-retired" || topic == "formation" ||
                // LMU 21.11.: Schaden nach Kontakt und das Startfazit („9 Plätze gutgemacht“) gehören zum Nötigsten.
                topic.StartsWith("incident-damage", StringComparison.Ordinal) || topic == "start-verdict";
        }

        /// <summary>Einmal je Spielzustand; true genau dann, wenn sich das Feld gerade beruhigt hat.</summary>
        internal bool Observe(GameStateData gs, DateTime now)
        {
            if (gs == null) return false;
            var s = gs.SessionData;
            string key = s.SessionId + "|" + s.SessionType + "|" + (s.TrackDefinition == null ? "" : s.TrackDefinition.name);
            if (key != session) { session = key; greenUtc = placeSinceUtc = SettledUtc = DateTime.MinValue; place = 0; settled = false; }
            Length = Classify(gs);
            if (s.SessionType != SessionType.Race || settled) return false;
            if (s.SessionPhase != SessionPhase.Green && s.SessionPhase != SessionPhase.FullCourseYellow) return false;
            if (greenUtc == DateTime.MinValue) greenUtc = now;
            if (s.ClassPosition != place) { place = s.ClassPosition; placeSinceUtc = now; }
            if (!Settled(s.CompletedLaps, now - greenUtc, now - placeSinceUtc)) return false;
            settled = true; SettledUtc = now;
            LiveDiagnostics.Decision("race-rhythm", "settled", "start_phase_over_" + Length.ToString().ToLowerInvariant(),
                Math.Round((now - greenUtc).TotalSeconds) + " s nach Grün");
            return true;
        }

        /// <summary>Läuft gerade die Startphase eines Rennens?</summary>
        internal bool InStart(GameStateData gs) =>
            gs != null && gs.SessionData.SessionType == SessionType.Race && !settled && greenUtc != DateTime.MinValue;
    }

    /// <summary>
    /// Mantorp 21.11., 15:32:45–48: „Unfall voraus! Das ist Nacho Blanco.“ und zweimal „Gelb im Sektor 1 … Nacho Blanco“.
    /// In der Startphase wird derselbe Fahrer binnen 10 s nur einmal als Gefahr genannt.
    /// </summary>
    internal sealed class StartHazardNames
    {
        internal static readonly TimeSpan Window = TimeSpan.FromSeconds(10);
        private readonly System.Collections.Generic.Dictionary<string, DateTime> named =
            new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        internal bool NamedRecently(string name, DateTime now) =>
            !string.IsNullOrEmpty(name) && named.TryGetValue(name, out var at) && now >= at && now - at < Window;

        internal void Note(string name, DateTime now) { if (!string.IsNullOrEmpty(name)) named[name] = now; }
    }
}
