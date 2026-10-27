using System;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>One voice identity throughout local and cloud radio; never silently substitute a provider.</summary>
    public static class CrewSpeechPolicy
    {
        public static readonly string[] TrackLimitPhrases = {
            "Pass auf die Streckenbegrenzung auf. Diese Runde ist ungültig.",
            "Wieder außerhalb der Limits. Lass etwas mehr Platz zum Rand.",
            "Noch eine ungültige Runde. Bleib sauber, wir brauchen eine gültige Zeit.",
            "Achte auf die Limits. Nimm lieber etwas Tempo raus und bleib auf der Strecke."
        };
        public static readonly string[] FinishPhrases = {
            "Jaaa, gewonnen! Ganz stark, Glückwunsch!",
            "Das ist der Sieg! Klasse gemacht, wir haben es geschafft!",
            "Im Ziel und ganz vorne! Stark gefahren, Glückwunsch zum Sieg!"
        };

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
                member == TeamMember.Mik && TrackLimitPhrases.Contains(text) || member == TeamMember.Mik && FinishPhrases.Contains(text);
        }

        public static bool CacheOnly(bool automatic, int priority, TeamMember member)
        { return automatic && priority >= 8 && member == TeamMember.Sara; }

        // End-to-end lifetime includes synthesis and the audio queue, not only time after enqueue.
        public static int LifetimeSeconds(bool automatic, int priority)
        { return automatic ? (priority >= 8 ? 2 : priority >= 6 ? 6 : 10) : 20; }
    }
}
