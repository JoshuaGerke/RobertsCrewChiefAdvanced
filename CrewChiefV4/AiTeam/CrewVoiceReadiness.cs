using System;
using System.Threading;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Checks the selected identity against the local audio that was loaded, without network access.</summary>
    public static class CrewVoiceReadiness
    {
        public sealed class Result
        {
            public bool Ready { get; private set; }
            public string Reason { get; private set; }
            public string Message { get; private set; }
            public Result(bool ready, string reason, string message) { Ready=ready; Reason=reason; Message=message; }
        }

        private static string loadedSaraVoiceKey;
        public static string LoadedSaraVoiceKey { get { return Volatile.Read(ref loadedSaraVoiceKey); } }

        // Called exactly when the legacy spotter chooses its folders, never when a new pack is generated.
        // Replacing files later cannot claim that the audio engine has reloaded those files.
        public static void RecordLoadedSpotterPack(string voiceFolder, string loadedCarLeftFolder)
        {
            string key = IsSaraFolder(loadedCarLeftFolder) ? SaraSpotterPack.ReadVoiceKey(voiceFolder) : "";
            Volatile.Write(ref loadedSaraVoiceKey, key);
        }

        public static string ExpectedSaraVoiceKey(AiTeamSettings settings)
        {
            if (settings.TtsProvider == AiTtsProvider.ELEVENLABS)
                return "elevenlabs|" + settings.ElevenLabsModel + "|" + settings.ElevenLabsVoiceSara + "|radio=" + settings.RadioEffect;
            if (settings.TtsProvider != AiTtsProvider.WINDOWS)
                return "cloud|" + settings.TtsModel + "|" + settings.VoiceSara + "|radio=" + settings.RadioEffect;
            return "windows|" + settings.WindowsVoiceSara + "|radio=" + settings.RadioEffect;
        }

        public static Result Evaluate(AiTeamSettings settings, bool packReady, string selectedSpotterName,
            string loadedCarLeftFolder, string loadedVoiceKey, bool spotterEnabled, bool interruptibleAudio = true)
        {
            if (settings == null) return new Result(false, "settings_missing", "Stimmeneinstellungen sind noch nicht verfügbar.");
            if (!settings.Enabled || (!settings.BenEnabled && !settings.MikEnabled && !settings.SaraEnabled))
                return new Result(true, "team_disabled", "KI-Team ausgeschaltet.");
            if (settings.TtsProvider == AiTtsProvider.WINDOWS)
                return new Result(false, "windows_voice_selected", "Windows-Systemstimmen sind gewählt. Für die Crew eine natürliche Stimmenquelle auswählen und die Originalclips vorbereiten.");
            if (!settings.ReplaceCrewChiefVoice)
                return new Result(false, "legacy_chief_enabled", "Die alten Crew-Chief-Ansagen sind noch aktiv. Für ausschließlich Ben, Mik und Sara „KI-Team ersetzt Crew-Chief-Stimme“ einschalten.");
            if (!settings.SaraEnabled) return new Result(true, "sara_disabled", "Sara ausgeschaltet; keine Seitenwarnungen von Sara.");
            if (!interruptibleAudio)
                return new Result(false, "interruptible_audio_required", "Für Saras sofortige Unterbrechungen nAudio aktivieren und die App neu starten.");
            if (!spotterEnabled)
                return new Result(false, "spotter_disabled", "Sara ist aktiviert, aber der lokale Spotter ist ausgeschaltet. Spotter aktivieren oder Sara ausdrücklich ausschalten.");
            if (!packReady)
                return new Result(false, "pack_missing_or_mismatched", "Saras Originalclips fehlen, sind beschädigt oder passen nicht zur gewählten Stimme. „Sara als Spotter erzeugen“ ausführen, danach die App vollständig neu starten.");
            if (!string.Equals(selectedSpotterName, SaraSpotterPack.SpotterName, StringComparison.Ordinal))
                return new Result(false, "spotter_not_selected", "Das Sara-Pack ist vorhanden, als lokaler Spotter ist aber eine andere Stimme gewählt. Im Crew-Chief-Hauptfenster Sara wählen und die App vollständig neu starten.");
            if (!IsSaraFolder(loadedCarLeftFolder) || !string.Equals(loadedVoiceKey, ExpectedSaraVoiceKey(settings), StringComparison.Ordinal))
                return new Result(false, "restart_required", "Saras gewählte Originalstimme ist vorbereitet, aber noch nicht in der Audioengine geladen. Die App vollständig schließen und neu starten.");
            return new Result(true, "ready", "Sara bereit – gewählte Originalstimme lokal geladen.");
        }

        private static bool IsSaraFolder(string folder)
        { return string.Equals((folder ?? "").Replace('\\','/'), "spotter_Sara/car_left", StringComparison.Ordinal); }
    }
}
