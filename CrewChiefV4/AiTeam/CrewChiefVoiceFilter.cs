using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// "Nur noch das KI-Team spricht": Crew Chief rechnet weiter alles aus (Telemetrie, Spotter, Ereignisse),
    /// seine eigenen englischen Chef-Ansagen werden aber nicht mehr eingereiht. Durch kommen nur Funksprüche
    /// des KI-Teams und alles rund um den Spotter (Sara). Flaggen sagt Sara über <see cref="FlagWatch"/> auf Deutsch an.
    /// Der Spotter selbst läuft über AudioPlayer.playSpotterMessage und wird hier gar nicht gefiltert.
    /// </summary>
    public static class CrewChiefVoiceFilter
    {
        private static readonly string[] allowedPrefixes = { "spotter", "acknowledge/spotter", "radio_check" };

        public static bool Allow(string messageName, IEnumerable<string> messageFolders)
        {
            var folders = messageFolders == null ? new List<string>() : messageFolders.Where(f => f != null).ToList();
            if (folders.Any(f => f.StartsWith(CrewChiefV4.Audio.SoundCache.AI_AUDIO_IDENTIFIER, StringComparison.Ordinal)))
            {
                return true;
            }
            if (folders.Count > 0 && folders.All(f => allowedPrefixes.Any(p => f.StartsWith(p, StringComparison.OrdinalIgnoreCase)) || f.StartsWith(CrewChiefV4.Audio.AudioPlayer.PAUSE_ID)))
            {
                return true;
            }
            return messageName != null && allowedPrefixes.Any(p => messageName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }
    }
}
