using System;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>One voice identity throughout local and cloud radio; never silently substitute a provider.</summary>
    public static class CrewSpeechPolicy
    {
        public static readonly string[] FirstTrackLimitPhrases = {
            "Pass auf die Streckenbegrenzung auf.",
            "Die Simulation meldet Tracklimits. Lass etwas Platz zum Rand.",
            "Achte auf die Limits. Bleib innerhalb der Begrenzung."
        };
        public static readonly string[] RepeatedTrackLimitPhrases = {
            "Wieder außerhalb der Limits. Lass etwas mehr Platz zum Rand.",
            "Noch eine Tracklimit-Meldung. Nimm am Rand etwas mehr Reserve.",
            "Schon wieder über den Limits. Bleib sauber auf der Strecke."
        };
        public static readonly string[] FirstInvalidLapPhrases = {
            "Diese Runde ist ungültig. Konzentrier dich auf die nächste Runde.",
            "Die Rundenzeit ist ungültig. Konzentrier dich auf die nächste gültige Zeit.",
            "Runde ungültig. Ruhig bleiben und wieder eine gültige Zeit setzen."
        };
        public static readonly string[] RepeatedInvalidLapPhrases = {
            "Noch eine ungültige Runde. Wir brauchen wieder eine gültige Zeit.",
            "Wieder keine gültige Rundenzeit. Bleib konzentriert.",
            "Die Runde ist erneut ungültig. Konzentrier dich auf die nächste."
        };
        // Both forms are prepared before driving; a track-limit warning alone does not prove lap invalidity.
        public static readonly string[] TrackLimitPhrases = FirstTrackLimitPhrases.Concat(RepeatedTrackLimitPhrases)
            .Concat(FirstTrackLimitPhrases.Concat(RepeatedTrackLimitPhrases).Select(p => p + " Diese Runde ist ungültig."))
            .Concat(FirstInvalidLapPhrases).Concat(RepeatedInvalidLapPhrases).ToArray();
        public static readonly string[] FinishPhrases = {
            "Jaaa, gewonnen! Ganz stark, Glückwunsch!",
            "Das ist der Sieg! Klasse gemacht, wir haben es geschafft!",
            "Im Ziel und ganz vorne! Stark gefahren, Glückwunsch zum Sieg!",
            "Ja, das ist Platz eins! Glückwunsch zum Sieg!",
            "Gewonnen! Stark gemacht. Freu dich darüber!",
            "Der Sieg ist deiner! Glückwunsch!"
        };

        public static readonly string[] ContactPhrases = {
            "Kontakt. Ich schau mir den Schaden an.",
            "Das hat gerumst. Ich prüf das Auto.",
            "Kontakt gesehen. Moment, ich check den Schaden.",
            "Autsch. Ich schau, was am Auto ist."
        };
        // Nur bei belegtem Kontakt mit starkem Tempoverlust oder schwerem Schaden, danach kommt der Schadensbericht.
        public static readonly string[] HeavyContactPhrases = {
            "Uff, das war heftig. Alles okay bei dir?",
            "Oh, das sah nicht gut aus. Geht's dir gut?",
            "Das war ein harter Einschlag. Alles in Ordnung?",
            "Oha. Alles okay bei dir? Ich schau mir das Auto an."
        };
        public static readonly string[] StoppedPhrases = {
            "Alles okay? Beim Wiederanfahren auf den Verkehr achten.",
            "Alles gut bei dir? Erst schauen, dann wieder rein.",
            "Alles klar? Ganz ruhig, pass beim Anfahren auf den Verkehr auf."
        };
        public static readonly string[] OffTrackPhrases = {
            "Neben der Strecke. Beim Zurückfahren auf den Verkehr achten.",
            "Du bist draußen. Schau auf den Verkehr, bevor du zurückkommst.",
            "Achtung, neben der Strecke. Komm kontrolliert zurück."
        };
        public const string WetTrackHint = " Die Strecke ist nass, vorsichtig am Gas.";
        public static readonly string[] IncidentPhrases = ContactPhrases.Concat(HeavyContactPhrases).Concat(StoppedPhrases).Concat(OffTrackPhrases).ToArray();

        public static string VoiceIdentity(AiTeamSettings s, TeamMember member)
        {
            string voice = s.TtsProvider == AiTtsProvider.ELEVENLABS ? s.ElevenLabsModel + "|" + s.ElevenLabsVoiceFor(member)
                : s.TtsProvider == AiTtsProvider.WINDOWS ? s.WindowsVoiceFor(member) : s.TtsModel + "|" + s.VoiceFor(member);
            return s.TtsProvider + "|" + voice + "|radio=" + s.RadioEffect;
        }

        public static string CacheKey(AiTeamSettings s, TeamMember member, string text)
        { return VoiceIdentity(s, member) + "|" + text; }

        public static bool IsPreparedPhrase(TeamMember member, string text)
        {
            return member == TeamMember.Sara && (FlagWatch.Phrases.Contains(text) || text.Length <= 60) ||
                member == TeamMember.Mik && (TrackLimitPhrases.Contains(text) || IncidentPhrases.Contains(text)) || member == TeamMember.Mik && (FinishPhrases.Contains(text) || text == SessionLapObserver.TimedLapStartPhrase) ||
                member == TeamMember.Ben && (TrackSituationWatch.CatchingPhrases.Contains(text) || TrackSituationWatch.ApproachingPhrases.Contains(text));
        }

        public static bool CacheOnly(bool automatic, int priority, TeamMember member)
        { return automatic && priority >= 8 && member == TeamMember.Sara; }

        // End-to-end lifetime includes synthesis and the audio queue, not only time after enqueue.
        public static int LifetimeSeconds(bool automatic, int priority)
        { return automatic ? (priority >= 8 ? 2 : priority >= 6 ? 6 : 10) : 20; }
    }
}
