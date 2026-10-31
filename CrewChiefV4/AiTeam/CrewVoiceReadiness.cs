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

        /// <summary>Only at process startup, before any AudioPlayer access. Application-scoped .NET
        /// settings can be fresh in a new installation folder while the shared KI profile and pack remain intact.</summary>
        public static void PrepareStartupSelection()
        {
            var user = UserSettings.GetUserSettings();
            if (user.initFailed) return;
            var settings = AiTeamSettings.Load();
            if (!OwnsSara(settings)) return;
            // Do not use AudioPlayer or SaraSpotterPack.VoiceFolder here: that would freeze the old
            // static readonly AudioPlayer.playWithNAudio before we can restore its required setting.
            string soundRoot = user.getString("override_default_sound_pack_location");
            if (string.IsNullOrWhiteSpace(soundRoot) || !System.IO.Directory.Exists(soundRoot))
                soundRoot = DataFiles.SoundFilesFolder;
            RestoreStartupSelection(settings, System.IO.Path.Combine(soundRoot, "voice"),
                user.getString("spotter_name"), user.getBoolean("enable_spotter"), user.getBoolean("use_naudio"),
                user.setProperty, user.saveUserSettings);
        }

        private static bool OwnsSara(AiTeamSettings settings)
        {
            return settings != null && settings.Enabled && settings.SaraEnabled && settings.ReplaceCrewChiefVoice &&
                settings.TtsProvider != AiTtsProvider.WINDOWS;
        }

        /// <summary>Local pack validation and settings only; never generates clips, calls an API, restarts,
        /// or marks a pack as loaded. The normal audio constructor still has to load the actual Sara identity.</summary>
        public static bool RestoreStartupSelection(AiTeamSettings settings, string voiceFolder, string selectedSpotter,
            bool spotterEnabled, bool nAudioEnabled, Action<string, object> setProperty, Action save)
        {
            if (!OwnsSara(settings) || !SaraSpotterPack.IsGenerated(voiceFolder, ExpectedSaraVoiceKey(settings))) return false;
            bool changed = false;
            if (!string.Equals(selectedSpotter, SaraSpotterPack.SpotterName, StringComparison.Ordinal))
            { setProperty("spotter_name", SaraSpotterPack.SpotterName); changed = true; }
            if (!spotterEnabled) { setProperty("enable_spotter", true); changed = true; }
            if (!nAudioEnabled) { setProperty("use_naudio", true); changed = true; }
            if (changed) save();
            return changed;
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
