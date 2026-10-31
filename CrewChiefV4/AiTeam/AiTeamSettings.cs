using System;

namespace CrewChiefV4.AiTeam
{
    // Reihenfolge muss zu den ui_text-Einträgen *_listprop_value_N passen
    public enum AiRadioMode { MINIMAL, NORMAL, ACTIVE }

    public enum AiTtsProvider { OPENAI_COMPATIBLE, WINDOWS, ELEVENLABS }

    /// <summary>
    /// Alle Einstellungen des KI-Teams (Ben, Mik, Sara). Werden über den normalen
    /// Crew-Chief-Eigenschaftendialog gepflegt (Kategorie "KI-Team"), der API-Schlüssel
    /// separat und verschlüsselt über <see cref="AiSecretStore"/>.
    /// </summary>
    public class AiTeamSettings
    {
        public bool Enabled = true;
        public bool BenEnabled = true, MikEnabled = true, SaraEnabled = true;
        public bool IsMemberEnabled(TeamMember member)
        { return Enabled && (member == TeamMember.Ben ? BenEnabled : member == TeamMember.Mik ? MikEnabled : SaraEnabled); }
        // Voreinstellung: OpenRouter (ein Schlüssel für Chat und Spracherkennung) + ElevenLabs für die Stimmen
        public string BaseUrl = "https://openrouter.ai/api/v1";
        public string FastModel = "openai/gpt-6-luna";
        public string DeepModel = "openai/gpt-6.1-sol";
        public string ReasoningEffort = "none";
        public string SttModel = "openai/gpt-transcribe";
        public string TtsModel = "microsoft/mai-voice-2.1-flash";
        public AiTtsProvider TtsProvider = AiTtsProvider.ELEVENLABS;
        public string ElevenLabsModel = "eleven_flash_v2_5";
        // Standardstimmen aus der ElevenLabs-Grundausstattung, im Fenster "KI-Team" per Liste änderbar
        public string ElevenLabsVoiceBen = "JBFqnCBsd6RMkjVDRZzb";
        public string ElevenLabsVoiceMik = "nPczCjzI2devNBz1zQrb";
        public string ElevenLabsVoiceSara = "EXAVITQu4vr4xnSDxMaL";
        public string VoiceBen = "de-DE-Klaus:MAI-Voice-2.1-Flash";
        public string VoiceMik = "de-DE-Grant:MAI-Voice-2.1-Flash";
        public string VoiceSara = "de-DE-Mia:MAI-Voice-2.1-Flash";
        public string WindowsVoiceBen = "";
        public string WindowsVoiceMik = "";
        public string WindowsVoiceSara = "";
        public AiRadioMode RadioMode = AiRadioMode.NORMAL;
        public string DriverName = "Rob";
        public string DriverNickname = "";
        public string RadioName { get { return string.IsNullOrWhiteSpace(DriverNickname) ? DriverName : DriverNickname.Trim(); } }
        public string PttKey = "";
        public string Microphone = "";
        public int TimeoutSeconds = 12;
        public int MaxCallsPerSession = 300;
        public int StatusIntervalMinutes = 2; // Lokaler, belegter Rennstatus; 0 bleibt explizit deaktiviert
        public AiRadioPreferences RadioPreferences = new AiRadioPreferences();
        public bool RadioEffect = true;
        public bool SendDriverNames = true;
        public bool WriteLog = true;
        public bool PersistentMemory = true;
        public bool ReplaceCrewChiefVoice = true;

        public static AiTeamSettings Load()
        {
            var s = new AiTeamSettings();
            var u = UserSettings.GetUserSettings();
            s.Enabled = Get(() => u.getBoolean("enable_ai_team"), s.Enabled);
            s.BenEnabled = Get(() => u.getBoolean("enable_ai_team_ben"), true);
            s.MikEnabled = Get(() => u.getBoolean("enable_ai_team_mik"), true);
            s.SaraEnabled = Get(() => u.getBoolean("enable_ai_team_sara"), true);
            s.BaseUrl = GetString(() => u.getString("ai_team_api_base_url"), s.BaseUrl);
            s.FastModel = GetString(() => u.getString("ai_team_fast_model"), s.FastModel);
            s.DeepModel = GetString(() => u.getString("ai_team_deep_model"), s.DeepModel);
            s.ReasoningEffort = Get(() => u.getString("ai_team_reasoning_effort"), s.ReasoningEffort);
            s.SttModel = GetString(() => u.getString("ai_team_stt_model"), s.SttModel);
            s.TtsModel = GetString(() => u.getString("ai_team_tts_model"), s.TtsModel);
            s.TtsProvider = GetEnum(() => u.getString("ai_team_tts_provider_listprop"), s.TtsProvider);
            s.ElevenLabsModel = GetString(() => u.getString("ai_team_elevenlabs_model"), s.ElevenLabsModel);
            s.ElevenLabsVoiceBen = GetString(() => u.getString("ai_team_elevenlabs_voice_ben"), s.ElevenLabsVoiceBen);
            s.ElevenLabsVoiceMik = GetString(() => u.getString("ai_team_elevenlabs_voice_mik"), s.ElevenLabsVoiceMik);
            s.ElevenLabsVoiceSara = GetString(() => u.getString("ai_team_elevenlabs_voice_sara"), s.ElevenLabsVoiceSara);
            s.VoiceBen = GetString(() => u.getString("ai_team_voice_ben"), s.VoiceBen);
            s.VoiceMik = GetString(() => u.getString("ai_team_voice_mik"), s.VoiceMik);
            s.VoiceSara = GetString(() => u.getString("ai_team_voice_sara"), s.VoiceSara);
            s.WindowsVoiceBen = Get(() => u.getString("ai_team_windows_voice_ben"), s.WindowsVoiceBen);
            s.WindowsVoiceMik = Get(() => u.getString("ai_team_windows_voice_mik"), s.WindowsVoiceMik);
            s.WindowsVoiceSara = Get(() => u.getString("ai_team_windows_voice_sara"), s.WindowsVoiceSara);
            s.RadioMode = GetEnum(() => u.getString("ai_team_radio_mode_listprop"), s.RadioMode);
            s.DriverName = GetString(() => u.getString("ai_team_driver_name"), s.DriverName);
            s.DriverNickname = Get(() => u.getString("ai_team_driver_nickname"), "");
            s.PttKey = Get(() => u.getString("ai_team_ptt_key"), s.PttKey);
            s.Microphone = Get(() => u.getString("ai_team_microphone"), s.Microphone);
            s.TimeoutSeconds = Math.Max(3, Get(() => u.getInt("ai_team_timeout_seconds"), s.TimeoutSeconds));
            s.MaxCallsPerSession = Math.Max(0, Get(() => u.getInt("ai_team_max_calls_per_session"), s.MaxCallsPerSession));
            s.StatusIntervalMinutes = Math.Max(0, Get(() => u.getInt("ai_team_status_interval_minutes"), s.StatusIntervalMinutes));
            s.RadioEffect = Get(() => u.getBoolean("enable_ai_team_radio_effect"), s.RadioEffect);
            s.SendDriverNames = Get(() => u.getBoolean("enable_ai_team_send_driver_names"), s.SendDriverNames);
            s.WriteLog = Get(() => u.getBoolean("enable_ai_team_log"), s.WriteLog);
            s.PersistentMemory = Get(() => u.getBoolean("enable_ai_team_persistent_memory"), true);
            s.ReplaceCrewChiefVoice = Get(() => u.getBoolean("enable_ai_team_replaces_crew_chief_voice"), s.ReplaceCrewChiefVoice);
            s.RadioPreferences = AiRadioPreferences.Load();
            return s;
        }

        public string VoiceFor(TeamMember member)
        {
            switch (member)
            {
                case TeamMember.Mik: return VoiceMik;
                case TeamMember.Sara: return VoiceSara;
                default: return VoiceBen;
            }
        }

        public string ElevenLabsVoiceFor(TeamMember member)
        {
            switch (member)
            {
                case TeamMember.Mik: return ElevenLabsVoiceMik;
                case TeamMember.Sara: return ElevenLabsVoiceSara;
                default: return ElevenLabsVoiceBen;
            }
        }

        public string WindowsVoiceFor(TeamMember member)
        {
            switch (member)
            {
                case TeamMember.Mik: return WindowsVoiceMik;
                case TeamMember.Sara: return WindowsVoiceSara;
                default: return WindowsVoiceBen;
            }
        }

        private static T Get<T>(Func<T> getter, T fallback)
        {
            try { return getter(); }
            catch (Exception) { return fallback; }
        }

        private static string GetString(Func<string> getter, string fallback)
        {
            string value = Get(getter, fallback);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static T GetEnum<T>(Func<string> getter, T fallback) where T : struct
        {
            T parsed;
            return Enum.TryParse(Get(getter, ""), true, out parsed) ? parsed : fallback;
        }
    }
}
