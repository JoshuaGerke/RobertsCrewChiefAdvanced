using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using NAudio.CoreAudioApi;

using CrewChiefV4.ACC;
using CrewChiefV4.Audio;
using CrewChiefV4.commands;
using CrewChiefV4.Events;
using CrewChiefV4.Overlay;
using CrewChiefV4.R3E;
using CrewChiefV4.SRE;
using CrewChiefV4.UserInterface.Models;

namespace CrewChiefV4
{
    public partial class SpeechRecogniser : IDisposable
    {
        public static bool hasMadeVoiceCommandSinceStarting = false;

        private SREWrapper sreWrapper;

        public static int sreSessionId = 0;
        public static float distanceWhenVoiceCommandStarted = 0;
        public static DateTime timeVoiceCommandStarted = DateTime.MinValue;

        private readonly int nAudioWaveInSampleRate = UserSettings.GetUserSettings().getInt("naudio_wave_in_sample_rate");
        private readonly int nAudioWaveInChannelCount = UserSettings.GetUserSettings().getInt("naudio_wave_in_channel_count");
        private readonly int nAudioWaveInSampleDepth = UserSettings.GetUserSettings().getInt("naudio_wave_in_sample_depth");

        private static readonly Boolean tuneConfidenceThresholds = UserSettings.GetUserSettings().getBoolean("sre_enable_threshold_tuning");

        // used in nAudio mode:
        public static Dictionary<string, Tuple<string, int>> speechRecognitionDevices = new Dictionary<string, Tuple<string, int>>();
        public static int speechInputDeviceIndex = 0;
        public static int cachedSpeechInputDeviceIndex = 0;
        private readonly Boolean useNAudio = UserSettings.GetUserSettings().getBoolean("use_naudio_for_speech_recognition");
        private readonly Boolean disableBehaviorAlteringVoiceCommands = UserSettings.GetUserSettings().getBoolean("disable_behavior_altering_voice_commands");
        public Boolean disableOverlayVoiceCommands = UserSettings.GetUserSettings().getBoolean("disable_overlay_voice_commands");
        private readonly string debugDataPath = DataFiles.voiceRecognitionDebugFolder;
        private RingBufferStream.RingBufferStream buffer;
        private NAudio.Wave.WaveInEvent waveIn;

        private Thread nAudioAlwaysOnListenerThread = null;
        private bool nAudioAlwaysOnkeepRecording = false;

        private readonly String localeCountryPropertySetting = UserSettings.GetUserSettings().getString("speech_recognition_country");

        private readonly string minimum_name_voice_recognition_confidence_windows_prop_name = Configuration.getUIString("minimum_name_voice_recognition_confidence_system_sre");
        private readonly string minimum_name_voice_recognition_confidence_microsoft_prop_name = Configuration.getUIString("minimum_name_voice_recognition_confidence");
        private readonly string minimum_trigger_voice_recognition_confidence_windows_prop_name = Configuration.getUIString("trigger_word_sre_min_confidence_system_sre");
        private readonly string minimum_trigger_voice_recognition_confidence_microsoft_prop_name = Configuration.getUIString("trigger_word_sre_min_confidence");
        private readonly string minimum_voice_recognition_confidence_windows_prop_name = Configuration.getUIString("minimum_voice_recognition_confidence_system_sre");
        private readonly string minimum_voice_recognition_confidence_microsoft_prop_name = Configuration.getUIString("minimum_voice_recognition_confidence");
        private readonly string minimum_rally_voice_recognition_confidence_windows_prop_name = Configuration.getUIString("minimum_rally_voice_recognition_confidence_system_sre");
        private readonly string minimum_rally_voice_recognition_confidence_microsoft_prop_name = Configuration.getUIString("minimum_rally_voice_recognition_confidence_microsoft_sre");

        private enum ThresholdType
        {
            STANDARD, NAMES, RALLY, TRIGGER
        }

        private string recogniserName;

        private readonly Dictionary<ThresholdType, SREThresholdInfo> thresholds = new Dictionary<ThresholdType, SREThresholdInfo>();

        private readonly Boolean disable_alternative_voice_commands = UserSettings.GetUserSettings().getBoolean("disable_alternative_voice_commands");
        private readonly Boolean enable_iracing_pit_stop_commands = UserSettings.GetUserSettings().getBoolean("enable_iracing_pit_stop_commands");
        private static readonly Boolean use_verbose_responses = UserSettings.GetUserSettings().getBoolean("use_verbose_responses");

        private static readonly String sreConfigLanguageSetting = Configuration.getSpeechRecognitionConfigOption("language");
        private static readonly String sreConfigDefaultLocaleSetting = Configuration.getSpeechRecognitionConfigOption("defaultLocale");

        private readonly int trigger_word_listen_timeout = UserSettings.GetUserSettings().getInt("trigger_word_listen_timeout");

        private static readonly Boolean alarmClockVoiceRecognitionEnabled = UserSettings.GetUserSettings().getBoolean("enable_alarm_clock_voice_recognition");

        #region Speech recognition phrases
        public static readonly String[] HOWS_MY_TYRE_WEAR = Configuration.getSpeechRecognitionPhrases("HOWS_MY_TYRE_WEAR");
        public static readonly String[] HOWS_MY_TRANSMISSION = Configuration.getSpeechRecognitionPhrases("HOWS_MY_TRANSMISSION");
        public static readonly String[] HOWS_MY_AERO = Configuration.getSpeechRecognitionPhrases("HOWS_MY_AERO");
        public static readonly String[] HOWS_MY_ENGINE = Configuration.getSpeechRecognitionPhrases("HOWS_MY_ENGINE");
        public static readonly String[] HOWS_MY_SUSPENSION = Configuration.getSpeechRecognitionPhrases("HOWS_MY_SUSPENSION");
        public static readonly String[] HOWS_MY_BRAKES = Configuration.getSpeechRecognitionPhrases("HOWS_MY_BRAKES");
        public static readonly String[] HOWS_MY_FUEL = Configuration.getSpeechRecognitionPhrases("HOWS_MY_FUEL");
        public static readonly String[] HOWS_MY_BATTERY = Configuration.getSpeechRecognitionPhrases("HOWS_MY_BATTERY");
        public static readonly String[] HOWS_MY_PACE = Configuration.getSpeechRecognitionPhrases("HOWS_MY_PACE");
        public static readonly String[] HOWS_MY_SELF_PACE = Configuration.getSpeechRecognitionPhrases("HOWS_MY_SELF_PACE");
        public static readonly String[] HOW_ARE_MY_TYRE_TEMPS = Configuration.getSpeechRecognitionPhrases("HOW_ARE_MY_TYRE_TEMPS");
        public static readonly String[] WHAT_ARE_MY_TYRE_TEMPS = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_MY_TYRE_TEMPS");
        public static readonly String[] WHAT_ARE_MY_TYRE_PRESSURES = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_MY_TYRE_PRESSURES");
        public static readonly String[] HOW_ARE_MY_BRAKE_TEMPS = Configuration.getSpeechRecognitionPhrases("HOW_ARE_MY_BRAKE_TEMPS");
        public static readonly String[] WHAT_ARE_MY_BRAKE_TEMPS = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_MY_BRAKE_TEMPS");
        public static readonly String[] HOW_ARE_MY_ENGINE_TEMPS = Configuration.getSpeechRecognitionPhrases("HOW_ARE_MY_ENGINE_TEMPS");
        public static readonly String[] WHAT_ARE_MY_ENGINE_TEMPS = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_MY_ENGINE_TEMPS");
        public static readonly String[] WHAT_IS_MY_OIL_TEMP = Configuration.getSpeechRecognitionPhrases("WHAT_IS_MY_OIL_TEMP");
        public static readonly String[] WHAT_IS_MY_WATER_TEMP = Configuration.getSpeechRecognitionPhrases("WHAT_IS_MY_WATER_TEMP");
        public static readonly String[] WHATS_MY_GAP_IN_FRONT = Configuration.getSpeechRecognitionPhrases("WHATS_MY_GAP_IN_FRONT");
        public static readonly String[] WHATS_MY_GAP_BEHIND = Configuration.getSpeechRecognitionPhrases("WHATS_MY_GAP_BEHIND");
        public static readonly String[] WHATS_MY_GAP_IN_FRONT_ON_TRACK = Configuration.getSpeechRecognitionPhrases("WHATS_MY_GAP_IN_FRONT_ON_TRACK");
        public static readonly String[] WHATS_MY_GAP_BEHIND_ON_TRACK = Configuration.getSpeechRecognitionPhrases("WHATS_MY_GAP_BEHIND_ON_TRACK");
        public static readonly String[] WHATS_MY_GAP_TO_LEADER = Configuration.getSpeechRecognitionPhrases("WHATS_MY_GAP_TO_LEADER");
        public static readonly String[] WHATS_THE_LEADER_LAP = Configuration.getSpeechRecognitionPhrases("WHATS_THE_LEADER_LAP");
        public static readonly String[] NOTE_BAD_DRIVER_AHEAD_ON_TRACK = Configuration.getSpeechRecognitionPhrases("NOTE_BAD_DRIVER_AHEAD_ON_TRACK");
        public static readonly String[] NOTE_BAD_DRIVER_BEHIND_ON_TRACK = Configuration.getSpeechRecognitionPhrases("NOTE_BAD_DRIVER_BEHIND_ON_TRACK");
        public static readonly String[] WHAT_WAS_MY_LAST_LAP_TIME = Configuration.getSpeechRecognitionPhrases("WHAT_WAS_MY_LAST_LAP_TIME");
        public static readonly String[] WHATS_MY_BEST_LAP_TIME = Configuration.getSpeechRecognitionPhrases("WHATS_MY_BEST_LAP_TIME");
        public static readonly String[] WHATS_THE_FASTEST_LAP_TIME = Configuration.getSpeechRecognitionPhrases("WHATS_THE_FASTEST_LAP_TIME");
        public static readonly String[] WHATS_MY_POSITION = Configuration.getSpeechRecognitionPhrases("WHATS_MY_POSITION");
        public static readonly String[] WHATS_MY_FUEL_LEVEL = Configuration.getSpeechRecognitionPhrases("WHATS_MY_FUEL_LEVEL");
        public static readonly String[] WHATS_MY_FUEL_USAGE = Configuration.getSpeechRecognitionPhrases("WHATS_MY_FUEL_USAGE");
        public static readonly String[] WHATS_MY_IRATING = Configuration.getSpeechRecognitionPhrases("WHATS_MY_IRATING");
        public static readonly String[] WHATS_MY_LICENSE_CLASS = Configuration.getSpeechRecognitionPhrases("WHATS_MY_LICENSE_CLASS");
        public static readonly String[] WHATS_MY_EXPECTED_FINISH_POSITION = Configuration.getSpeechRecognitionPhrases("WHATS_MY_EXPECTED_FINISH_POSITION");
        public static readonly String[] WHAT_TYRES_AM_I_ON = Configuration.getSpeechRecognitionPhrases("WHAT_TYRES_AM_I_ON");
        public static readonly String[] WHAT_ARE_THE_RELATIVE_TYRE_PERFORMANCES = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_THE_RELATIVE_TYRE_PERFORMANCES");
        public static readonly String[] HOW_LONG_WILL_THESE_TYRES_LAST = Configuration.getSpeechRecognitionPhrases("HOW_LONG_WILL_THESE_TYRES_LAST");
        public static readonly String[] WHATS_PITLANE_SPEED_LIMIT = Configuration.getSpeechRecognitionPhrases("WHATS_PITLANE_SPEED_LIMIT");
        public static readonly String[] HOW_MANY_LAPS_SINCE_PITTING = Configuration.getSpeechRecognitionPhrases("HOW_MANY_LAPS_SINCE_PITTING");
        public static readonly String[] WHERE_SHOULD_I_LINE_UP = Configuration.getSpeechRecognitionPhrases("WHERE_SHOULD_I_LINE_UP");

        public static readonly String[] SET_FUEL_STRATEGY_CAUTIOUS = Configuration.getSpeechRecognitionPhrases("SET_FUEL_STRATEGY_CAUTIOUS");
        public static readonly String[] SET_FUEL_STRATEGY_RISKY = Configuration.getSpeechRecognitionPhrases("SET_FUEL_STRATEGY_RISKY");
        public static readonly String[] SET_FUEL_STRATEGY_RESET = Configuration.getSpeechRecognitionPhrases("SET_FUEL_STRATEGY_RESET");
        public static readonly String[] HOW_MUCH_FUEL_TO_END_OF_RACE = Configuration.getSpeechRecognitionPhrases("HOW_MUCH_FUEL_TO_END_OF_RACE");
        public static readonly String[] CALCULATE_FUEL_FOR = Configuration.getSpeechRecognitionPhrases("CALCULATE_FUEL_FOR");
        public static readonly String[] LAP = Configuration.getSpeechRecognitionPhrases("LAP");
        public static readonly String[] LAPS = Configuration.getSpeechRecognitionPhrases("LAPS");
        public static readonly String[] MINUTE = Configuration.getSpeechRecognitionPhrases("MINUTE");
        public static readonly String[] MINUTES = Configuration.getSpeechRecognitionPhrases("MINUTES");
        public static readonly String[] HOUR = Configuration.getSpeechRecognitionPhrases("HOUR");
        public static readonly String[] HOURS = Configuration.getSpeechRecognitionPhrases("HOURS");

        public static readonly String[] KEEP_QUIET = Configuration.getSpeechRecognitionPhrases("KEEP_QUIET");
        public static readonly String[] KEEP_ME_INFORMED = Configuration.getSpeechRecognitionPhrases("KEEP_ME_INFORMED");
        public static readonly String[] TELL_ME_THE_GAPS = Configuration.getSpeechRecognitionPhrases("TELL_ME_THE_GAPS");
        public static readonly String[] DONT_TELL_ME_THE_GAPS = Configuration.getSpeechRecognitionPhrases("DONT_TELL_ME_THE_GAPS");
        public static readonly String[] TALK_TO_ME_ANYWHERE = Configuration.getSpeechRecognitionPhrases("TALK_TO_ME_ANYWHERE");
        public static readonly String[] DONT_TALK_IN_THE_CORNERS = Configuration.getSpeechRecognitionPhrases("DONT_TALK_IN_THE_CORNERS");
        public static readonly String[] WHATS_THE_TIME = Configuration.getSpeechRecognitionPhrases("WHATS_THE_TIME");
        public static readonly String[] ENABLE_YELLOW_FLAG_MESSAGES = Configuration.getSpeechRecognitionPhrases("ENABLE_YELLOW_FLAG_MESSAGES");
        public static readonly String[] DISABLE_YELLOW_FLAG_MESSAGES = Configuration.getSpeechRecognitionPhrases("DISABLE_YELLOW_FLAG_MESSAGES");
        public static readonly String[] ENABLE_MANUAL_FORMATION_LAP = Configuration.getSpeechRecognitionPhrases("ENABLE_MANUAL_FORMATION_LAP");
        public static readonly String[] DISABLE_MANUAL_FORMATION_LAP = Configuration.getSpeechRecognitionPhrases("DISABLE_MANUAL_FORMATION_LAP");

        public static readonly String[] WHOS_IN_FRONT_IN_THE_RACE = Configuration.getSpeechRecognitionPhrases("WHOS_IN_FRONT_IN_THE_RACE");
        public static readonly String[] WHOS_TWO_IN_FRONT_IN_THE_RACE = Configuration.getSpeechRecognitionPhrases("WHOS_TWO_IN_FRONT_IN_THE_RACE");
        public static readonly String[] WHOS_BEHIND_IN_THE_RACE = Configuration.getSpeechRecognitionPhrases("WHOS_BEHIND_IN_THE_RACE");
        public static readonly String[] WHOS_IN_FRONT_ON_TRACK = Configuration.getSpeechRecognitionPhrases("WHOS_IN_FRONT_ON_TRACK");
        public static readonly String[] WHOS_BEHIND_ON_TRACK = Configuration.getSpeechRecognitionPhrases("WHOS_BEHIND_ON_TRACK");
        public static readonly String[] WHOS_LEADING = Configuration.getSpeechRecognitionPhrases("WHOS_LEADING");
        public static readonly String[] WHATS_MY_CAR_NUMBER = Configuration.getSpeechRecognitionPhrases("WHATS_MY_CAR_NUMBER");
        public static readonly String[] WHEN_DID_IN_FRONT_ON_TRACK_PIT = Configuration.getSpeechRecognitionPhrases("WHEN_DID_IN_FRONT_ON_TRACK_PIT");
        public static readonly String[] WHEN_DID_BEHIND_ON_TRACK_PIT = Configuration.getSpeechRecognitionPhrases("WHEN_DID_BEHIND_ON_TRACK_PIT");

        public static readonly String[] WHERE_AM_I_FASTER = Configuration.getSpeechRecognitionPhrases("WHERE_AM_I_FASTER");
        public static readonly String[] WHERE_AM_I_SLOWER = Configuration.getSpeechRecognitionPhrases("WHERE_AM_I_SLOWER");

        public static readonly String[] HOW_LONGS_LEFT = Configuration.getSpeechRecognitionPhrases("HOW_LONGS_LEFT");
        public static readonly String[] HOW_MANY_DRS_ACTIVATIONS_LEFT = Configuration.getSpeechRecognitionPhrases("HOW_MANY_DRS_ACTIVATIONS_LEFT");
        public static readonly String[] WHAT_LAP_AM_I_ON = Configuration.getSpeechRecognitionPhrases("WHAT_LAP_AM_I_ON");
        private static readonly String[] SPOT = Configuration.getSpeechRecognitionPhrases("SPOT");
        private static readonly String[] DONT_SPOT = Configuration.getSpeechRecognitionPhrases("DONT_SPOT");
        public static readonly String[] REPEAT_LAST_MESSAGE = Configuration.getSpeechRecognitionPhrases("REPEAT_LAST_MESSAGE");
        public static readonly String[] HAVE_I_SERVED_MY_PENALTY = Configuration.getSpeechRecognitionPhrases("HAVE_I_SERVED_MY_PENALTY");
        public static readonly String[] DO_I_HAVE_A_PENALTY = Configuration.getSpeechRecognitionPhrases("DO_I_HAVE_A_PENALTY");
        public static readonly String[] DO_I_STILL_HAVE_A_PENALTY = Configuration.getSpeechRecognitionPhrases("DO_I_STILL_HAVE_A_PENALTY");
        public static readonly String[] DO_I_HAVE_A_MANDATORY_PIT_STOP = Configuration.getSpeechRecognitionPhrases("DO_I_HAVE_A_MANDATORY_PIT_STOP");
        public static readonly String[] WHAT_ARE_MY_SECTOR_TIMES = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_MY_SECTOR_TIMES");
        public static readonly String[] WHATS_MY_LAST_SECTOR_TIME = Configuration.getSpeechRecognitionPhrases("WHATS_MY_LAST_SECTOR_TIME");
        public static readonly String[] WHATS_THE_AIR_TEMP = Configuration.getSpeechRecognitionPhrases("WHATS_THE_AIR_TEMP");
        public static readonly String[] WHATS_THE_TRACK_TEMP = Configuration.getSpeechRecognitionPhrases("WHATS_THE_TRACK_TEMP");
        public static readonly String[] WHATS_MY_BRAKE_BIAS = Configuration.getSpeechRecognitionPhrases("WHATS_MY_BRAKE_BIAS");
        public static readonly String[] RADIO_CHECK = Configuration.getSpeechRecognitionPhrases("RADIO_CHECK");

        public static readonly String[] IS_MY_PIT_BOX_OCCUPIED = Configuration.getSpeechRecognitionPhrases("IS_MY_PIT_BOX_OCCUPIED");
        public static readonly String[] PLAY_POST_PIT_POSITION_ESTIMATE = Configuration.getSpeechRecognitionPhrases("PLAY_POST_PIT_POSITION_ESTIMATE");
        public static readonly String[] PRACTICE_PIT_STOP = Configuration.getSpeechRecognitionPhrases("PRACTICE_PIT_STOP");

        public static readonly String[] ENABLE_CUT_TRACK_WARNINGS = Configuration.getSpeechRecognitionPhrases("ENABLE_CUT_TRACK_WARNINGS");
        public static readonly String[] DISABLE_CUT_TRACK_WARNINGS = Configuration.getSpeechRecognitionPhrases("DISABLE_CUT_TRACK_WARNINGS");

        public static readonly String[] HOWS_MY_LEFT_FRONT_CAMBER = Configuration.getSpeechRecognitionPhrases("HOWS_MY_LEFT_FRONT_CAMBER");
        public static readonly String[] HOWS_MY_RIGHT_FRONT_CAMBER = Configuration.getSpeechRecognitionPhrases("HOWS_MY_RIGHT_FRONT_CAMBER");
        public static readonly String[] HOWS_MY_LEFT_REAR_CAMBER = Configuration.getSpeechRecognitionPhrases("HOWS_MY_LEFT_REAR_CAMBER");
        public static readonly String[] HOWS_MY_RIGHT_REAR_CAMBER = Configuration.getSpeechRecognitionPhrases("HOWS_MY_RIGHT_REAR_CAMBER");
        public static readonly String[] HOWS_MY_FRONT_CAMBER = Configuration.getSpeechRecognitionPhrases("HOWS_MY_FRONT_CAMBER");
        public static readonly String[] HOWS_MY_REAR_CAMBER = Configuration.getSpeechRecognitionPhrases("HOWS_MY_REAR_CAMBER");
        public static readonly String[] HOW_ARE_MY_TYRE_PRESSURES = Configuration.getSpeechRecognitionPhrases("HOW_ARE_MY_TYRE_PRESSURES");
        public static readonly String[] HOW_ARE_MY_FRONT_TYRE_PRESSURES = Configuration.getSpeechRecognitionPhrases("HOW_ARE_MY_FRONT_TYRE_PRESSURES");
        public static readonly String[] HOW_ARE_MY_REAR_TYRE_PRESSURES = Configuration.getSpeechRecognitionPhrases("HOW_ARE_MY_REAR_TYRE_PRESSURES");
        public static readonly String[] HOWS_MY_LEFT_FRONT_CAMBER_RIGHT_NOW = Configuration.getSpeechRecognitionPhrases("HOWS_MY_LEFT_FRONT_CAMBER_RIGHT_NOW");
        public static readonly String[] HOWS_MY_RIGHT_FRONT_CAMBER_RIGHT_NOW = Configuration.getSpeechRecognitionPhrases("HOWS_MY_RIGHT_FRONT_CAMBER_RIGHT_NOW");
        public static readonly String[] HOWS_MY_LEFT_REAR_CAMBER_RIGHT_NOW = Configuration.getSpeechRecognitionPhrases("HOWS_MY_LEFT_REAR_CAMBER_RIGHT_NOW");
        public static readonly String[] HOWS_MY_RIGHT_REAR_CAMBER_RIGHT_NOW = Configuration.getSpeechRecognitionPhrases("HOWS_MY_RIGHT_REAR_CAMBER_RIGHT_NOW");
        public static readonly String[] HOWS_MY_FRONT_CAMBER_RIGHT_NOW = Configuration.getSpeechRecognitionPhrases("HOWS_MY_FRONT_CAMBER_RIGHT_NOW");
        public static readonly String[] HOWS_MY_REAR_CAMBER_RIGHT_NOW = Configuration.getSpeechRecognitionPhrases("HOWS_MY_REAR_CAMBER_RIGHT_NOW");
        public static readonly String[] HOW_ARE_MY_TYRE_PRESSURES_RIGHT_NOW = Configuration.getSpeechRecognitionPhrases("HOW_ARE_MY_TYRE_PRESSURES_RIGHT_NOW");
        public static readonly String[] HOW_ARE_MY_FRONT_TYRE_PRESSURES_RIGHT_NOW = Configuration.getSpeechRecognitionPhrases("HOW_ARE_MY_FRONT_TYRE_PRESSURES_RIGHT_NOW");
        public static readonly String[] HOW_ARE_MY_REAR_TYRE_PRESSURES_RIGHT_NOW = Configuration.getSpeechRecognitionPhrases("HOW_ARE_MY_REAR_TYRE_PRESSURES_RIGHT_NOW");

        public static readonly String[] WHAT_ARE_MY_LEFT_FRONT_SURFACE_TEMPS = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_MY_LEFT_FRONT_SURFACE_TEMPS");
        public static readonly String[] WHAT_ARE_MY_RIGHT_FRONT_SURFACE_TEMPS = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_MY_RIGHT_FRONT_SURFACE_TEMPS");
        public static readonly String[] WHAT_ARE_MY_LEFT_REAR_SURFACE_TEMPS = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_MY_LEFT_REAR_SURFACE_TEMPS");
        public static readonly String[] WHAT_ARE_MY_RIGHT_REAR_SURFACE_TEMPS = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_MY_RIGHT_REAR_SURFACE_TEMPS");

        public static readonly String[] HOW_MANY_LAPS_ON_TYRE_SET = Configuration.getSpeechRecognitionPhrases("HOW_MANY_LAPS_ON_TYRE_SET");
        public static readonly String[] HOW_OLD_ARE_THESE_TYRES = Configuration.getSpeechRecognitionPhrases("HOW_OLD_ARE_THESE_TYRES");

        public static readonly String[] STOP_COMPLAINING = Configuration.getSpeechRecognitionPhrases("STOP_COMPLAINING");

        // R3E only for now:
        public static readonly String[] WHAT_ARE_THE_PIT_ACTIONS = Configuration.getSpeechRecognitionPhrases("WHAT_ARE_THE_PIT_ACTIONS");

        public static readonly bool DYNAMIC_PHRASES = Configuration.getSpeechRecognitionPhrases("DYNAMIC_PHRASES").Length > 0;
        public static readonly String ON = Configuration.getSpeechRecognitionConfigOption("ON");
        public static readonly String POSSESSIVE = Configuration.getSpeechRecognitionConfigOption("POSSESSIVE");
        public static readonly String WHERE_IS = Configuration.getSpeechRecognitionConfigOption("WHERE_IS");
        public static readonly String WHERES = Configuration.getSpeechRecognitionConfigOption("WHERES");
        public static readonly String POSITION_LONG = Configuration.getSpeechRecognitionConfigOption("POSITION_LONG");
        public static readonly String POSITION_SHORT = Configuration.getSpeechRecognitionConfigOption("POSITION_SHORT");

        public static readonly String WHOS_IN = Configuration.getSpeechRecognitionConfigOption("WHOS_IN");
        public static readonly String WHATS = Configuration.getSpeechRecognitionConfigOption("WHATS");
        public static readonly String BEST_LAP = Configuration.getSpeechRecognitionConfigOption("BEST_LAP");
        public static readonly String BEST_LAP_TIME = Configuration.getSpeechRecognitionConfigOption("BEST_LAP_TIME");
        public static readonly String LAST_LAP = Configuration.getSpeechRecognitionConfigOption("LAST_LAP");
        public static readonly String LAST_LAP_TIME = Configuration.getSpeechRecognitionConfigOption("LAST_LAP_TIME");
        public static readonly String THE_LEADER = Configuration.getSpeechRecognitionConfigOption("THE_LEADER");
        public static readonly String THE_CAR_AHEAD = Configuration.getSpeechRecognitionConfigOption("THE_CAR_AHEAD");
        public static readonly String THE_CAR_IN_FRONT = Configuration.getSpeechRecognitionConfigOption("THE_CAR_IN_FRONT");
        public static readonly String THE_GUY_AHEAD = Configuration.getSpeechRecognitionConfigOption("THE_GUY_AHEAD");
        public static readonly String THE_GUY_IN_FRONT = Configuration.getSpeechRecognitionConfigOption("THE_GUY_IN_FRONT");
        public static readonly String THE_CAR_BEHIND = Configuration.getSpeechRecognitionConfigOption("THE_CAR_BEHIND");
        public static readonly String THE_GUY_BEHIND = Configuration.getSpeechRecognitionConfigOption("THE_GUY_BEHIND");
        public static readonly String CAR_NUMBER = Configuration.getSpeechRecognitionConfigOption("CAR_NUMBER");

        public static readonly String WHAT_TYRES_IS = Configuration.getSpeechRecognitionConfigOption("WHAT_TYRES_IS");
        public static readonly String WHAT_TYRE_IS = Configuration.getSpeechRecognitionConfigOption("WHAT_TYRE_IS");

        public static readonly String IRATING = Configuration.getSpeechRecognitionConfigOption("IRATING");
        public static readonly String LICENSE_CLASS = Configuration.getSpeechRecognitionConfigOption("LICENSE_CLASS");

        // for R3E only
        public static readonly String[] WHATS_MY_RATING = Configuration.getSpeechRecognitionPhrases("WHATS_MY_RATING");
        public static readonly String[] WHATS_MY_RANK = Configuration.getSpeechRecognitionPhrases("WHATS_MY_RANK");
        public static readonly String[] WHATS_MY_REPUTATION = Configuration.getSpeechRecognitionPhrases("WHATS_MY_REPUTATION");
        public static readonly String[] HOW_GOOD_IS = Configuration.getSpeechRecognitionPhrases("HOW_GOOD_IS");
        public static readonly String RATING = Configuration.getSpeechRecognitionConfigOption("RATING");
        public static readonly String REPUTATION = Configuration.getSpeechRecognitionConfigOption("REPUTATION");
        public static readonly String RANK = Configuration.getSpeechRecognitionConfigOption("RANK");

        public static readonly String[] PLAY_CORNER_NAMES = Configuration.getSpeechRecognitionPhrases("PLAY_CORNER_NAMES");

        public static readonly String[] DAMAGE_REPORT = Configuration.getSpeechRecognitionPhrases("DAMAGE_REPORT");
        public static readonly String[] CAR_STATUS = Configuration.getSpeechRecognitionPhrases("CAR_STATUS");
        public static readonly String[] SESSION_STATUS = Configuration.getSpeechRecognitionPhrases("SESSION_STATUS");
        public static readonly String[] STATUS = Configuration.getSpeechRecognitionPhrases("STATUS");

        public static readonly String[] START_PACE_NOTES_PLAYBACK = Configuration.getSpeechRecognitionPhrases("START_PACE_NOTES_PLAYBACK");
        public static readonly String[] STOP_PACE_NOTES_PLAYBACK = Configuration.getSpeechRecognitionPhrases("STOP_PACE_NOTES_PLAYBACK");

        // pitstop commands specific to iRacing:
        public static readonly String[] PIT_STOP_ADD = Configuration.getSpeechRecognitionPhrases("PIT_STOP_ADD");
        public static readonly String[] LITERS = Configuration.getSpeechRecognitionPhrases("LITERS");
        public static readonly String[] GALLONS = Configuration.getSpeechRecognitionPhrases("GALLONS");
        public static readonly String[] PIT_STOP_TEAROFF = Configuration.getSpeechRecognitionPhrases("PIT_STOP_TEAROFF");
        public static readonly String[] PIT_STOP_FAST_REPAIR = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FAST_REPAIR");
        public static readonly String[] PIT_STOP_CLEAR_ALL = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CLEAR_ALL");
        public static readonly String[] PIT_STOP_CLEAR_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CLEAR_TYRES");
        public static readonly String[] PIT_STOP_CLEAR_WIND_SCREEN = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CLEAR_WIND_SCREEN");
        public static readonly String[] PIT_STOP_CLEAR_FAST_REPAIR = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CLEAR_FAST_REPAIR");
        public static readonly String[] PIT_STOP_CLEAR_FUEL = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CLEAR_FUEL");

        public static readonly String[] PIT_STOP_CHANGE_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_TYRES");  // for ACC
        public static readonly String[] PIT_STOP_CHANGE_ALL_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_ALL_TYRES");
        public static readonly String[] PIT_STOP_CHANGE_FRONT_LEFT_TYRE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_FRONT_LEFT_TYRE");
        public static readonly String[] PIT_STOP_CHANGE_FRONT_RIGHT_TYRE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_FRONT_RIGHT_TYRE");
        public static readonly String[] PIT_STOP_CHANGE_REAR_LEFT_TYRE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_REAR_LEFT_TYRE");
        public static readonly String[] PIT_STOP_CHANGE_REAR_RIGHT_TYRE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_REAR_RIGHT_TYRE");

        public static readonly String[] PIT_STOP_CHANGE_TYRE_PRESSURE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_TYRE_PRESSURE");
        public static readonly String[] PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE");
        public static readonly String[] PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE");
        public static readonly String[] PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE");
        public static readonly String[] PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE");

        public static readonly String[] PIT_STOP_CHANGE_LEFT_SIDE_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_LEFT_SIDE_TYRES");
        public static readonly String[] PIT_STOP_CHANGE_RIGHT_SIDE_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_RIGHT_SIDE_TYRES");

        public static readonly String[] PIT_STOP_CHANGE_FRONT_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_FRONT_TYRES");
        public static readonly String[] PIT_STOP_CHANGE_REAR_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_REAR_TYRES");
        public static readonly String[] PIT_STOP_FIX_FRONT_AERO = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FIX_FRONT_AERO");
        public static readonly String[] PIT_STOP_FIX_REAR_AERO = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FIX_REAR_AERO");
        public static readonly String[] PIT_STOP_FIX_ALL_AERO = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FIX_ALL_AERO");
        public static readonly String[] PIT_STOP_FIX_NO_AERO = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FIX_NO_AERO");
        public static readonly String[] PIT_STOP_FIX_SUSPENSION = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FIX_SUSPENSION");
        public static readonly String[] PIT_STOP_DONT_FIX_SUSPENSION = Configuration.getSpeechRecognitionPhrases("PIT_STOP_DONT_FIX_SUSPENSION");
        public static readonly String[] PIT_STOP_FIX_ALL = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FIX_ALL");  // rF2 & R3E
        public static readonly String[] PIT_STOP_FIX_BODY = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FIX_BODY");  // rF2
        public static readonly String[] PIT_STOP_FIX_NONE = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FIX_NONE");  // rF2
        public static readonly String[] PIT_STOP_SERVE_PENALTY = Configuration.getSpeechRecognitionPhrases("PIT_STOP_SERVE_PENALTY");
        public static readonly String[] PIT_STOP_DONT_SERVE_PENALTY = Configuration.getSpeechRecognitionPhrases("PIT_STOP_DONT_SERVE_PENALTY");
        public static readonly String[] PIT_STOP_REFUEL = Configuration.getSpeechRecognitionPhrases("PIT_STOP_REFUEL");
        public static readonly String[] PIT_STOP_DONT_REFUEL = Configuration.getSpeechRecognitionPhrases("PIT_STOP_DONT_REFUEL");
        public static readonly String[] PIT_STOP_NEXT_TYRE_COMPOUND = Configuration.getSpeechRecognitionPhrases("PIT_STOP_NEXT_TYRE_COMPOUND");
        public static readonly String[] PIT_STOP_SOFT_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_SOFT_TYRES");
        public static readonly String[] PIT_STOP_SUPERSOFT_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_SUPERSOFT_TYRES");
        public static readonly String[] PIT_STOP_ULTRASOFT_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_ULTRASOFT_TYRES");
        public static readonly String[] PIT_STOP_HYPERSOFT_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_HYPERSOFT_TYRES");
        public static readonly String[] PIT_STOP_MEDIUM_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_MEDIUM_TYRES");
        public static readonly String[] PIT_STOP_HARD_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_HARD_TYRES");
        public static readonly String[] PIT_STOP_INTERMEDIATE_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_INTERMEDIATE_TYRES");
        public static readonly String[] PIT_STOP_WET_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_WET_TYRES");
        public static readonly String[] PIT_STOP_SELECT_TYRE_SET = Configuration.getSpeechRecognitionPhrases("PIT_STOP_SELECT_TYRE_SET");
        public static readonly String[] PIT_STOP_DRY_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_DRY_TYRES");
        public static readonly String[] PIT_STOP_MONSOON_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_MONSOON_TYRES");
        public static readonly String[] PIT_STOP_OPTION_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_OPTION_TYRES");
        public static readonly String[] PIT_STOP_PRIME_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_PRIME_TYRES");
        public static readonly String[] PIT_STOP_ALTERNATE_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_ALTERNATE_TYRES");

        // ACC only:
        public static readonly String[] PIT_STOP_CHANGE_FRONT_PRESSURES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_FRONT_PRESSURES");
        public static readonly String[] PIT_STOP_CHANGE_REAR_PRESSURES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_CHANGE_REAR_PRESSURES");
        public static readonly String[] PIT_STOP_SELECT_LEAST_USED_TYRE_SET = Configuration.getSpeechRecognitionPhrases("PIT_STOP_SELECT_LEAST_USED_TYRE_SET");

        // LMU only:
        public static readonly String[] PIT_STOP_USED_HARD_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_USED_HARD_TYRES");
        public static readonly String[] PIT_STOP_USED_MEDIUM_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_USED_MEDIUM_TYRES");
        public static readonly String[] PIT_STOP_USED_SOFT_TYRES = Configuration.getSpeechRecognitionPhrases("PIT_STOP_USED_SOFT_TYRES");

        public static readonly String[] POINT = Configuration.getSpeechRecognitionPhrases("POINT");

        public static readonly String[] HOW_MANY_INCIDENT_POINTS = Configuration.getSpeechRecognitionPhrases("HOW_MANY_INCIDENT_POINTS");
        public static readonly String[] WHATS_THE_INCIDENT_LIMIT = Configuration.getSpeechRecognitionPhrases("WHATS_THE_INCIDENT_LIMIT");
        public static readonly String[] WHATS_THE_SOF = Configuration.getSpeechRecognitionPhrases("WHATS_THE_SOF");

        public static readonly String[] PIT_STOP_FUEL_TO_THE_END = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FUEL_TO_THE_END");
        public static readonly String[] PIT_STOP_FILL_TO = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FILL_TO");

        public static readonly String[] PIT_STOP_FUEL_RATIO = Configuration.getSpeechRecognitionPhrases("PIT_STOP_FUEL_RATIO");
        public static readonly String[] PIT_STOP_VIRTUAL_ENERGY = Configuration.getSpeechRecognitionPhrases("PIT_STOP_VIRTUAL_ENERGY");
        
        public static readonly String[] DISPLAY_SECTORS = Configuration.getSpeechRecognitionPhrases("DISPLAY_SECTORS");  // rF2
        public static readonly String[] DISPLAY_PIT_MENU = Configuration.getSpeechRecognitionPhrases("DISPLAY_PIT_MENU");  // rF2
        public static readonly String[] DISPLAY_TYRES = Configuration.getSpeechRecognitionPhrases("DISPLAY_TYRES");  // rF2
        public static readonly String[] DISPLAY_TEMPS = Configuration.getSpeechRecognitionPhrases("DISPLAY_TEMPS");  // rF2
        public static readonly String[] DISPLAY_RACE_INFO = Configuration.getSpeechRecognitionPhrases("DISPLAY_RACE_INFO");  // rF2
        public static readonly String[] DISPLAY_STANDINGS = Configuration.getSpeechRecognitionPhrases("DISPLAY_STANDINGS");  // rF2
        public static readonly String[] DISPLAY_PENALTIES = Configuration.getSpeechRecognitionPhrases("DISPLAY_PENALTIES");  // rF2
        public static readonly String[] DISPLAY_NEXT = Configuration.getSpeechRecognitionPhrases("DISPLAY_NEXT");  // rF2

        private static readonly String[] MORE_INFO = Configuration.getSpeechRecognitionPhrases("MORE_INFO");

        private static readonly String[] I_AM_OK = Configuration.getSpeechRecognitionPhrases("I_AM_OK");

        public static readonly String[] IS_CAR_AHEAD_MY_CLASS = Configuration.getSpeechRecognitionPhrases("IS_CAR_AHEAD_MY_CLASS");
        public static readonly String[] IS_CAR_BEHIND_MY_CLASS = Configuration.getSpeechRecognitionPhrases("IS_CAR_BEHIND_MY_CLASS");
        public static readonly String[] WHAT_CLASS_IS_CAR_AHEAD = Configuration.getSpeechRecognitionPhrases("WHAT_CLASS_IS_CAR_AHEAD");
        public static readonly String[] WHAT_CLASS_IS_CAR_BEHIND = Configuration.getSpeechRecognitionPhrases("WHAT_CLASS_IS_CAR_BEHIND");

        public static readonly String[] SET_ALARM_CLOCK = Configuration.getSpeechRecognitionPhrases("SET_ALARM_CLOCK");
        public static readonly String[] CLEAR_ALARM_CLOCK = Configuration.getSpeechRecognitionPhrases("CLEAR_ALARM_CLOCK");
        public static readonly String[] AM = Configuration.getSpeechRecognitionPhrases("AM");
        public static readonly String[] PM = Configuration.getSpeechRecognitionPhrases("PM");

        // overlay controls
        public static readonly String[] HIDE_OVERLAY = Configuration.getSpeechRecognitionPhrases("HIDE_OVERLAY");
        public static readonly String[] SHOW_OVERLAY = Configuration.getSpeechRecognitionPhrases("SHOW_OVERLAY");
        public static readonly String[] SHOW_CONSOLE = Configuration.getSpeechRecognitionPhrases("SHOW_CONSOLE");
        public static readonly String[] SHOW_All_OVERLAYS = Configuration.getSpeechRecognitionPhrases("SHOW_All_OVERLAYS");
        public static readonly String[] SHOW_CHART = Configuration.getSpeechRecognitionPhrases("SHOW_CHART");
        public static readonly String[] CLEAR_CHART = Configuration.getSpeechRecognitionPhrases("CLEAR_CHART");
        public static readonly String[] REFRESH_CHART = Configuration.getSpeechRecognitionPhrases("REFRESH_CHART");
        public static readonly String[] SHOW_STACKED_CHARTS = Configuration.getSpeechRecognitionPhrases("SHOW_STACKED_CHARTS");
        public static readonly String[] SHOW_SINGLE_CHART = Configuration.getSpeechRecognitionPhrases("SHOW_SINGLE_CHART");
        public static readonly String[] CLEAR_DATA = Configuration.getSpeechRecognitionPhrases("CLEAR_DATA");
        public static readonly String[] SHOW_TIME = Configuration.getSpeechRecognitionPhrases("SHOW_TIME");
        public static readonly String[] SHOW_DISTANCE = Configuration.getSpeechRecognitionPhrases("SHOW_DISTANCE");
        public static readonly String[] HIDE_CONSOLE = Configuration.getSpeechRecognitionPhrases("HIDE_CONSOLE");
        public static readonly String[] HIDE_CHART = Configuration.getSpeechRecognitionPhrases("HIDE_CHART");

        public static readonly String[] CHART_COMMAND_ADD = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_ADD");
        public static readonly String[] CHART_COMMAND_REMOVE = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_REMOVE");
        public static readonly String[] CHART_COMMAND_BEST_LAP = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_BEST_LAP");
        public static readonly String[] CHART_COMMAND_LAST_LAP = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_LAST_LAP");
        public static readonly String[] CHART_COMMAND_OPPONENT_BEST_LAP = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_OPPONENT_BEST_LAP");
        public static readonly String[] CHART_COMMAND_SHOW_SECTOR_1 = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_SHOW_SECTOR_1");
        public static readonly String[] CHART_COMMAND_SHOW_SECTOR_2 = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_SHOW_SECTOR_2");
        public static readonly String[] CHART_COMMAND_SHOW_SECTOR_3 = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_SHOW_SECTOR_3");
        public static readonly String[] CHART_COMMAND_SHOW_ALL_SECTORS = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_SHOW_ALL_SECTORS");
        public static readonly String[] CHART_COMMAND_ZOOM_IN = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_ZOOM_IN");
        public static readonly String[] CHART_COMMAND_ZOOM_OUT = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_ZOOM_OUT");
        public static readonly String[] CHART_COMMAND_RESET_ZOOM = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_RESET_ZOOM");
        public static readonly String[] CHART_COMMAND_PAN_LEFT = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_PAN_LEFT");
        public static readonly String[] CHART_COMMAND_PAN_RIGHT = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_PAN_RIGHT");
        public static readonly String[] CHART_COMMAND_SHOW_NEXT_LAP = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_SHOW_NEXT_LAP");
        public static readonly String[] CHART_COMMAND_SHOW_PREVIOUS_LAP = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_SHOW_PREVIOUS_LAP");
        public static readonly String[] CHART_COMMAND_SHOW_LAST_LAP = Configuration.getSpeechRecognitionPhrases("CHART_COMMAND_SHOW_LAST_LAP");

        public static readonly String[] SHOW_SUBTITLES = Configuration.getSpeechRecognitionPhrases("SHOW_SUBTITLES");
        public static readonly String[] HIDE_SUBTITLES = Configuration.getSpeechRecognitionPhrases("HIDE_SUBTITLES");

        // rally stuff
        private static readonly bool loadHomophones = UserSettings.GetUserSettings().getBoolean("use_dictation_grammar_for_rally") && SREWrapperFactory.useSystem;
        public static readonly String[] RALLY_EARLIER_CALLS = Configuration.getSpeechRecognitionPhrases("RALLY_EARLIER_CALLS", loadHomophones);
        public static readonly String[] RALLY_LATER_CALLS = Configuration.getSpeechRecognitionPhrases("RALLY_LATER_CALLS", loadHomophones);
        public static readonly String[] RALLY_CORNER_NUMBER_FIRST = Configuration.getSpeechRecognitionPhrases("RALLY_CORNER_NUMBER_FIRST", loadHomophones);
        public static readonly String[] RALLY_CORNER_DIRECTION_FIRST = Configuration.getSpeechRecognitionPhrases("RALLY_CORNER_DIRECTION_FIRST", loadHomophones);
        public static readonly String[] RALLY_CORNER_DECRIPTIONS = Configuration.getSpeechRecognitionPhrases("RALLY_CORNER_DECRIPTIONS", loadHomophones);
        // pace note creation / correction
        public static readonly String[] RALLY_START_RECORDING_STAGE_NOTES = Configuration.getSpeechRecognitionPhrases("RALLY_START_RECORDING_STAGE_NOTES", loadHomophones);
        public static readonly String[] RALLY_FINISH_RECORDING_STAGE_NOTES = Configuration.getSpeechRecognitionPhrases("RALLY_FINISH_RECORDING_STAGE_NOTES", loadHomophones);
        public static readonly String[] RALLY_CORRECTION = Configuration.getSpeechRecognitionPhrases("RALLY_CORRECTION", loadHomophones);
        public static readonly String[] RALLY_EARLIER = Configuration.getSpeechRecognitionPhrases("RALLY_EARLIER", loadHomophones);
        public static readonly String[] RALLY_LATER = Configuration.getSpeechRecognitionPhrases("RALLY_LATER", loadHomophones);
        public static readonly String[] RALLY_INSERT = Configuration.getSpeechRecognitionPhrases("RALLY_INSERT", loadHomophones);
        public static readonly String[] RALLY_LEFT = Configuration.getSpeechRecognitionPhrases("RALLY_LEFT", loadHomophones);
        public static readonly String[] RALLY_RIGHT = Configuration.getSpeechRecognitionPhrases("RALLY_RIGHT", loadHomophones);
        public static readonly String[] RALLY_1 = Configuration.getSpeechRecognitionPhrases("RALLY_1", loadHomophones);
        public static readonly String[] RALLY_2 = Configuration.getSpeechRecognitionPhrases("RALLY_2", loadHomophones);
        public static readonly String[] RALLY_3 = Configuration.getSpeechRecognitionPhrases("RALLY_3", loadHomophones);
        public static readonly String[] RALLY_4 = Configuration.getSpeechRecognitionPhrases("RALLY_4", loadHomophones);
        public static readonly String[] RALLY_5 = Configuration.getSpeechRecognitionPhrases("RALLY_5", loadHomophones);
        public static readonly String[] RALLY_6 = Configuration.getSpeechRecognitionPhrases("RALLY_6", loadHomophones);
        public static readonly String[] RALLY_HAIRPIN = Configuration.getSpeechRecognitionPhrases("RALLY_HAIRPIN", loadHomophones);
        public static readonly String[] RALLY_OPEN_HAIRPIN = Configuration.getSpeechRecognitionPhrases("RALLY_OPEN_HAIRPIN", loadHomophones);
        public static readonly String[] RALLY_SQUARE = Configuration.getSpeechRecognitionPhrases("RALLY_SQUARE", loadHomophones);
        public static readonly String[] RALLY_FLAT = Configuration.getSpeechRecognitionPhrases("RALLY_FLAT", loadHomophones);
        private static readonly String[] RALLY_START_RECE = Configuration.getSpeechRecognitionPhrases("RALLY_START_RECE", loadHomophones);
        private static readonly String[] RALLY_FINISH_RECE = Configuration.getSpeechRecognitionPhrases("RALLY_FINISH_RECE", loadHomophones);
        public static readonly String[] RALLY_DISTANCE = Configuration.getSpeechRecognitionPhrases("RALLY_DISTANCE", loadHomophones);

        public static readonly String[] RALLY_TIGHTENS_TO_5 = Configuration.getSpeechRecognitionPhrases("RALLY_TIGHTENS_TO_5", loadHomophones);
        public static readonly String[] RALLY_TIGHTENS_TO_4 = Configuration.getSpeechRecognitionPhrases("RALLY_TIGHTENS_TO_4", loadHomophones);
        public static readonly String[] RALLY_TIGHTENS_TO_3 = Configuration.getSpeechRecognitionPhrases("RALLY_TIGHTENS_TO_3", loadHomophones);
        public static readonly String[] RALLY_TIGHTENS_TO_2 = Configuration.getSpeechRecognitionPhrases("RALLY_TIGHTENS_TO_2", loadHomophones);
        public static readonly String[] RALLY_TIGHTENS_TO_1 = Configuration.getSpeechRecognitionPhrases("RALLY_TIGHTENS_TO_1", loadHomophones);
        public static readonly String[] RALLY_TIGHTENS_TO_HAIRPIN = Configuration.getSpeechRecognitionPhrases("RALLY_TIGHTENS_TO_HAIRPIN", loadHomophones);

        public static readonly String[] RALLY_CUT = Configuration.getSpeechRecognitionPhrases("RALLY_CUT", loadHomophones);
        public static readonly String[] RALLY_DONT_CUT = Configuration.getSpeechRecognitionPhrases("RALLY_DONT_CUT", loadHomophones);
        public static readonly String[] RALLY_TIGHTENS = Configuration.getSpeechRecognitionPhrases("RALLY_TIGHTENS", loadHomophones);
        public static readonly String[] RALLY_TIGHTENS_BAD = Configuration.getSpeechRecognitionPhrases("RALLY_TIGHTENS_BAD", loadHomophones);
        public static readonly String[] RALLY_TIGHTENS_THEN_OPENS = Configuration.getSpeechRecognitionPhrases("RALLY_TIGHTENS_THEN_OPENS", loadHomophones);
        public static readonly String[] RALLY_OPENS_THEN_TIGHTENS = Configuration.getSpeechRecognitionPhrases("RALLY_OPENS_THEN_TIGHTENS", loadHomophones);
        public static readonly String[] RALLY_WIDENS = Configuration.getSpeechRecognitionPhrases("RALLY_WIDENS", loadHomophones);
        public static readonly String[] RALLY_WIDE_OUT = Configuration.getSpeechRecognitionPhrases("RALLY_WIDE_OUT", loadHomophones);
        public static readonly String[] RALLY_GO_STRAIGHT = Configuration.getSpeechRecognitionPhrases("RALLY_GO_STRAIGHT", loadHomophones);
        public static readonly String[] RALLY_MAYBE = Configuration.getSpeechRecognitionPhrases("RALLY_MAYBE", loadHomophones);
        public static readonly String[] RALLY_LONG = Configuration.getSpeechRecognitionPhrases("RALLY_LONG", loadHomophones);
        public static readonly String[] RALLY_LONGLONG = Configuration.getSpeechRecognitionPhrases("RALLY_LONGLONG", loadHomophones);
        public static readonly String[] RALLY_OPENS = Configuration.getSpeechRecognitionPhrases("RALLY_OPENS", loadHomophones);
        public static readonly String[] RALLY_PLUS = Configuration.getSpeechRecognitionPhrases("RALLY_PLUS", loadHomophones);
        public static readonly String[] RALLY_MINUS = Configuration.getSpeechRecognitionPhrases("RALLY_MINUS", loadHomophones);
        public static readonly String[] RALLY_BRIDGE = Configuration.getSpeechRecognitionPhrases("RALLY_BRIDGE", loadHomophones);
        public static readonly String[] RALLY_FORD = Configuration.getSpeechRecognitionPhrases("RALLY_FORD", loadHomophones);
        public static readonly String[] RALLY_JUNCTION = Configuration.getSpeechRecognitionPhrases("RALLY_JUNCTION", loadHomophones);
        public static readonly String[] RALLY_CAUTION = Configuration.getSpeechRecognitionPhrases("RALLY_CAUTION", loadHomophones);
        public static readonly String[] RALLY_DOUBLE_CAUTION = Configuration.getSpeechRecognitionPhrases("RALLY_DOUBLE_CAUTION", loadHomophones);
        public static readonly String[] RALLY_CREST = Configuration.getSpeechRecognitionPhrases("RALLY_CREST", loadHomophones);
        public static readonly String[] RALLY_OVER_CREST = Configuration.getSpeechRecognitionPhrases("RALLY_OVER_CREST", loadHomophones);
        public static readonly String[] RALLY_OVER_BRIDGE = Configuration.getSpeechRecognitionPhrases("RALLY_OVER_BRIDGE", loadHomophones);
        public static readonly String[] RALLY_JUMP = Configuration.getSpeechRecognitionPhrases("RALLY_JUMP", loadHomophones);
        public static readonly String[] RALLY_OVER_JUMP = Configuration.getSpeechRecognitionPhrases("RALLY_OVER_JUMP", loadHomophones);
        public static readonly String[] RALLY_BIG_JUMP = Configuration.getSpeechRecognitionPhrases("RALLY_BIG_JUMP", loadHomophones);
        public static readonly String[] RALLY_BAD_CAMBER = Configuration.getSpeechRecognitionPhrases("RALLY_BAD_CAMBER", loadHomophones);
        public static readonly String[] RALLY_TARMAC = Configuration.getSpeechRecognitionPhrases("RALLY_TARMAC", loadHomophones);
        public static readonly String[] RALLY_GRAVEL = Configuration.getSpeechRecognitionPhrases("RALLY_GRAVEL", loadHomophones);
        public static readonly String[] RALLY_SNOW = Configuration.getSpeechRecognitionPhrases("RALLY_SNOW", loadHomophones);
        public static readonly String[] RALLY_SLIPPY = Configuration.getSpeechRecognitionPhrases("RALLY_SLIPPY", loadHomophones);
        public static readonly String[] RALLY_CONCRETE = Configuration.getSpeechRecognitionPhrases("RALLY_CONCRETE", loadHomophones);
        public static readonly String[] RALLY_TUNNEL = Configuration.getSpeechRecognitionPhrases("RALLY_TUNNEL", loadHomophones);
        public static readonly String[] RALLY_LEFT_ENTRY_CHICANE = Configuration.getSpeechRecognitionPhrases("RALLY_LEFT_ENTRY_CHICANE", loadHomophones);
        public static readonly String[] RALLY_RIGHT_ENTRY_CHICANE = Configuration.getSpeechRecognitionPhrases("RALLY_RIGHT_ENTRY_CHICANE", loadHomophones);
        public static readonly String[] RALLY_RUTS = Configuration.getSpeechRecognitionPhrases("RALLY_RUTS", loadHomophones);
        public static readonly String[] RALLY_DEEP_RUTS = Configuration.getSpeechRecognitionPhrases("RALLY_DEEP_RUTS", loadHomophones);
        public static readonly String[] RALLY_CARE = Configuration.getSpeechRecognitionPhrases("RALLY_CARE", loadHomophones);
        public static readonly String[] RALLY_DANGER = Configuration.getSpeechRecognitionPhrases("RALLY_DANGER", loadHomophones);
        public static readonly String[] RALLY_KEEP_MIDDLE = Configuration.getSpeechRecognitionPhrases("RALLY_KEEP_MIDDLE", loadHomophones);
        public static readonly String[] RALLY_KEEP_LEFT = Configuration.getSpeechRecognitionPhrases("RALLY_KEEP_LEFT", loadHomophones);
        public static readonly String[] RALLY_KEEP_RIGHT = Configuration.getSpeechRecognitionPhrases("RALLY_KEEP_RIGHT", loadHomophones);
        public static readonly String[] RALLY_KEEP_IN = Configuration.getSpeechRecognitionPhrases("RALLY_KEEP_IN", loadHomophones);
        public static readonly String[] RALLY_KEEP_OUT = Configuration.getSpeechRecognitionPhrases("RALLY_KEEP_OUT", loadHomophones);
        public static readonly String[] RALLY_BUMPS = Configuration.getSpeechRecognitionPhrases("RALLY_BUMPS", loadHomophones);
        public static readonly String[] RALLY_OVER_RAILS = Configuration.getSpeechRecognitionPhrases("RALLY_OVER_RAILS", loadHomophones);
        public static readonly String[] RALLY_UPHILL = Configuration.getSpeechRecognitionPhrases("RALLY_UPHILL", loadHomophones);
        public static readonly String[] RALLY_DOWNHILL = Configuration.getSpeechRecognitionPhrases("RALLY_DOWNHILL", loadHomophones);
        public static readonly String[] RALLY_BRAKE = Configuration.getSpeechRecognitionPhrases("RALLY_BRAKE", loadHomophones);
        public static readonly String[] RALLY_LOOSE_GRAVEL = Configuration.getSpeechRecognitionPhrases("RALLY_LOOSE_GRAVEL", loadHomophones);
        public static readonly String[] RALLY_NARROWS = Configuration.getSpeechRecognitionPhrases("RALLY_NARROWS", loadHomophones);
        public static readonly String[] RALLY_THROUGH_GATE = Configuration.getSpeechRecognitionPhrases("RALLY_THROUGH_GATE", loadHomophones);
        public static readonly String[] RALLY_LOGS_INSIDE = Configuration.getSpeechRecognitionPhrases("RALLY_LOGS_INSIDE", loadHomophones);
        public static readonly String[] RALLY_ROCKS_INSIDE = Configuration.getSpeechRecognitionPhrases("RALLY_ROCKS_INSIDE", loadHomophones);
        public static readonly String[] RALLY_TREE_INSIDE = Configuration.getSpeechRecognitionPhrases("RALLY_TREE_INSIDE", loadHomophones);
        public static readonly String[] RALLY_LOGS_OUTSIDE = Configuration.getSpeechRecognitionPhrases("RALLY_LOGS_OUTSIDE", loadHomophones);
        public static readonly String[] RALLY_ROCKS_OUTSIDE = Configuration.getSpeechRecognitionPhrases("RALLY_ROCKS_OUTSIDE", loadHomophones);
        public static readonly String[] RALLY_TREE_OUTSIDE = Configuration.getSpeechRecognitionPhrases("RALLY_TREE_OUTSIDE", loadHomophones);
        public static readonly String[] RALLY_TWISTY = Configuration.getSpeechRecognitionPhrases("RALLY_TWISTY", loadHomophones);
        public static readonly String[] RALLY_DIP = Configuration.getSpeechRecognitionPhrases("RALLY_DIP", loadHomophones);

        public static readonly String[] RALLY_INTO = Configuration.getSpeechRecognitionPhrases("RALLY_INTO", loadHomophones);
        public static readonly String[] RALLY_THEN = Configuration.getSpeechRecognitionPhrases("RALLY_THEN", loadHomophones);
        public static readonly String[] RALLY_AND = Configuration.getSpeechRecognitionPhrases("RALLY_AND", loadHomophones);

        // most specific first, so "big jump" comes before "jump" when we parse in the event code
        public static readonly List<string[]> RallyObstacleCommands = new List<string[]>()
        {
            SpeechRecogniser.RALLY_BAD_CAMBER,
            SpeechRecogniser.RALLY_BIG_JUMP,
            SpeechRecogniser.RALLY_OVER_BRIDGE,
            SpeechRecogniser.RALLY_BRIDGE,
            SpeechRecogniser.RALLY_BUMPS,
            SpeechRecogniser.RALLY_CARE,
            SpeechRecogniser.RALLY_DANGER,
            SpeechRecogniser.RALLY_DOUBLE_CAUTION,
            SpeechRecogniser.RALLY_CAUTION,
            SpeechRecogniser.RALLY_CONCRETE,
            SpeechRecogniser.RALLY_OVER_CREST,
            SpeechRecogniser.RALLY_CREST,
            SpeechRecogniser.RALLY_DEEP_RUTS,
            SpeechRecogniser.RALLY_FORD,
            SpeechRecogniser.RALLY_LOOSE_GRAVEL,
            SpeechRecogniser.RALLY_GRAVEL,
            SpeechRecogniser.RALLY_SNOW,
            SpeechRecogniser.RALLY_SLIPPY,
            SpeechRecogniser.RALLY_OVER_JUMP,
            SpeechRecogniser.RALLY_JUMP,
            SpeechRecogniser.RALLY_JUNCTION,
            SpeechRecogniser.RALLY_KEEP_IN,
            SpeechRecogniser.RALLY_KEEP_LEFT,
            SpeechRecogniser.RALLY_KEEP_MIDDLE,
            SpeechRecogniser.RALLY_KEEP_OUT,
            SpeechRecogniser.RALLY_KEEP_RIGHT,
            SpeechRecogniser.RALLY_LEFT_ENTRY_CHICANE,
            SpeechRecogniser.RALLY_TIGHTENS_THEN_OPENS,
            SpeechRecogniser.RALLY_OPENS_THEN_TIGHTENS,
            SpeechRecogniser.RALLY_OPENS,
            SpeechRecogniser.RALLY_OVER_RAILS,
            SpeechRecogniser.RALLY_RIGHT_ENTRY_CHICANE,
            SpeechRecogniser.RALLY_RUTS,
            SpeechRecogniser.RALLY_TARMAC,
            SpeechRecogniser.RALLY_TUNNEL,
            SpeechRecogniser.RALLY_NARROWS,
            SpeechRecogniser.RALLY_LOGS_INSIDE,
            SpeechRecogniser.RALLY_ROCKS_INSIDE,
            SpeechRecogniser.RALLY_TREE_INSIDE,
            SpeechRecogniser.RALLY_LOGS_OUTSIDE,
            SpeechRecogniser.RALLY_ROCKS_OUTSIDE,
            SpeechRecogniser.RALLY_TREE_OUTSIDE,
            SpeechRecogniser.RALLY_UPHILL,
            SpeechRecogniser.RALLY_DOWNHILL,
            SpeechRecogniser.RALLY_BRAKE,
            SpeechRecogniser.RALLY_THROUGH_GATE,
            SpeechRecogniser.RALLY_WIDENS,
            SpeechRecogniser.RALLY_GO_STRAIGHT,
            SpeechRecogniser.RALLY_TWISTY,
            SpeechRecogniser.RALLY_DIP,

            SpeechRecogniser.RALLY_TIGHTENS_TO_1,
            SpeechRecogniser.RALLY_TIGHTENS_TO_2,
            SpeechRecogniser.RALLY_TIGHTENS_TO_3,
            SpeechRecogniser.RALLY_TIGHTENS_TO_4,
            SpeechRecogniser.RALLY_TIGHTENS_TO_5,
            SpeechRecogniser.RALLY_TIGHTENS_TO_HAIRPIN,

            SpeechRecogniser.RALLY_INTO,
            SpeechRecogniser.RALLY_THEN,
            SpeechRecogniser.RALLY_AND
        };

        // for watching opponent - "watch [bob]", "tell me about [bob]"
        public static readonly String WATCH = Configuration.getSpeechRecognitionConfigOption("WATCH");
        public static readonly String STOP_WATCHING = Configuration.getSpeechRecognitionConfigOption("STOP_WATCHING");
        // special cases so we can tell the app that a watched driver is team mate or rival
        public static readonly String TEAM_MATE = Configuration.getSpeechRecognitionConfigOption("TEAM_MATE");
        public static readonly String RIVAL = Configuration.getSpeechRecognitionConfigOption("RIVAL");
        public static readonly String[] STOP_WATCHING_ALL = Configuration.getSpeechRecognitionPhrases("STOP_WATCHING_ALL");
        public static readonly String[] WATCH_IN_FRONT_IN_THE_RACE = Configuration.getSpeechRecognitionPhrases("WATCH_IN_FRONT_IN_THE_RACE");
        public static readonly String[] WATCH_BEHIND_IN_THE_RACE = Configuration.getSpeechRecognitionPhrases("WATCH_BEHIND_IN_THE_RACE");

        // TODO: team mate / rival status request?

        public static readonly String[] TOGGLE_TURN_NUMBERS = Configuration.getSpeechRecognitionPhrases("TOGGLE_TURN_NUMBERS");

        // Steam VR stuff
        public static readonly String[] TOGGLE_VR_OVERLAYS = Configuration.getSpeechRecognitionPhrases("TOGGLE_VR_OVERLAYS");
        public static readonly String[] SHOW_VR_SETTING = Configuration.getSpeechRecognitionPhrases("SHOW_VR_SETTING");
        public static readonly String[] HIDE_VR_SETTING = Configuration.getSpeechRecognitionPhrases("HIDE_VR_SETTING");

        // Volume controls
        public static readonly String[] CREW_CHIEF_QUIETER = Configuration.getSpeechRecognitionPhrases("CREW_CHIEF_QUIETER");
        public static readonly String[] CREW_CHIEF_LOUDER = Configuration.getSpeechRecognitionPhrases("CREW_CHIEF_LOUDER");
        public static readonly String[] GAME_QUIETER = Configuration.getSpeechRecognitionPhrases("GAME_QUIETER");
        public static readonly String[] GAME_LOUDER = Configuration.getSpeechRecognitionPhrases("GAME_LOUDER");
        public static readonly String[] VOIP_QUIETER = Configuration.getSpeechRecognitionPhrases("VOIP_QUIETER");
        public static readonly String[] VOIP_LOUDER = Configuration.getSpeechRecognitionPhrases("VOIP_LOUDER");
        #endregion Speech recognition phrases

        private readonly Dictionary<GameEnum, string[]> whatsOpponentChoices = new Dictionary<GameEnum, string[]> {
            { GameEnum.IRACING, new String[] { LAST_LAP, LAST_LAP_TIME, BEST_LAP, BEST_LAP_TIME, IRATING, LICENSE_CLASS } },
            { GameEnum.RACE_ROOM, new String[] { LAST_LAP, LAST_LAP_TIME, BEST_LAP, BEST_LAP_TIME, RATING, RANK, REPUTATION } },
            // the array for UNKNOWN is what we'll use if there's no game-specific array
            { GameEnum.UNKNOWN, new String[] { LAST_LAP, LAST_LAP_TIME, BEST_LAP, BEST_LAP_TIME } }
        };

        private String lastRecognisedText = null;

        public CrewChief crewChief;

        public Boolean initialised = false;

        public MainWindow.VoiceOptionEnum voiceOptionEnum;

        private readonly HashSet<string> driverNamesInUse = new HashSet<string>();
        private readonly HashSet<string> carNumbersInUse = new HashSet<string>();
        private readonly HashSet<string> opponentsAddedToSoundCacheMidSession = new HashSet<string>();

        private readonly List<GrammarWrapper> opponentGrammarList = new List<GrammarWrapper>();
        private readonly List<GrammarWrapper> iracingPitstopGrammarList = new List<GrammarWrapper>();
        private readonly List<GrammarWrapper> r3ePitstopGrammarList = new List<GrammarWrapper>();
        private readonly List<GrammarWrapper> ratingsGrammarList = new List<GrammarWrapper>();
        private readonly List<GrammarWrapper> accPitstopGrammarList = new List<GrammarWrapper>();
        private readonly List<GrammarWrapper> pitManagerGrammarList = new List<GrammarWrapper>();
        private readonly List<GrammarWrapper> overlayGrammarList = new List<GrammarWrapper>();
        private readonly List<GrammarWrapper> rallyGrammarList = new List<GrammarWrapper>();

        private GrammarWrapper macroGrammar = null;

        private readonly Dictionary<String, ExecutableCommandMacro> macroLookup = new Dictionary<string, ExecutableCommandMacro>();

        public CultureInfo cultureInfo;

        public static readonly Dictionary<String[], String> carNumberToNumber = getCarNumberMappings();

        public static readonly Dictionary<String[], int> numberToNumber = getNumberMappings(1, 199);

        public static readonly Dictionary<String[], int> racePositionNumberToNumber = getNumberMappings(1, 64);

        public static readonly Dictionary<String[], int> hourMappings = getNumberMappings(0, 24);

        public static readonly Dictionary<String[], int> minuteMappings = getNumberMappings(0, 59);

        public static readonly Dictionary<String[], int> numbers0_199 = getNumberMappings(0, 199);

        private ChoicesWrapper digitsChoices;

        private ChoicesWrapper hourChoices;

        public static Boolean waitingForSpeech = false;

        public static readonly Boolean respondWhileChannelIsStillOpen = UserSettings.GetUserSettings().getBoolean("sre_respond_while_channel_still_open");

        public static Boolean gotRecognitionResult = false;

        // guard against race condition between closing channel and sre_SpeechRecognised event completing
        public static Boolean keepRecognisingInHoldMode = false;

        private SREWrapper triggerSreWrapper;

        // This is the trigger phrase used to activate the 'full' SRE
        private readonly String keyWord = UserSettings.GetUserSettings().getString("trigger_word_for_always_on_sre");

        private readonly EventWaitHandle triggerTimeoutWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
        private Thread restartWaitTimeoutThreadReference = null;

        // experimental free-dictation grammar for chat messages
        private readonly Boolean useFreeDictationForChatMessages = UserSettings.GetUserSettings().getBoolean("use_free_dictation_for_chat");
        private static readonly String startChatMacroName = "start chat message";
        private static readonly String endChatMacroName = "end chat message";
        private static readonly String chatContextStart = UserSettings.GetUserSettings().getString("free_dictation_chat_start_word");
        private const string chatContextEnd = null;
        private GrammarWrapper chatDictationGrammar;
        private static ExecutableCommandMacro startChatMacro = null;
        private static ExecutableCommandMacro endChatMacro = null;

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint waveInGetNumDevs();
        [DllImport("winmm.dll", SetLastError = true)]
        static extern Int32 waveInMessage(IntPtr hWaveOut, int uMsg, out int dwParam1, IntPtr dwParam2);
        [DllImport("winmm.dll", SetLastError = true)]
        static extern Int32 waveInMessage(IntPtr hWaveOut, int uMsg, IntPtr dwParam1, int dwParam2);

        private static string GetWaveInEndpointId(int devNumber)
        {
            int cbEndpointId;
            string result = string.Empty;
            waveInMessage((IntPtr)devNumber, AudioPlayer.DRV_QUERYFUNCTIONINSTANCEIDSIZE, out cbEndpointId, IntPtr.Zero);
            IntPtr strPtr = Marshal.AllocHGlobal(cbEndpointId);
            waveInMessage((IntPtr)devNumber, AudioPlayer.DRV_QUERYFUNCTIONINSTANCEID, strPtr, cbEndpointId);
            result = Marshal.PtrToStringAuto(strPtr);
            Marshal.FreeHGlobal(strPtr);
            return result;
        }

        public static List<AudioPlayer.WaveDevice> GetWaveInDevices()
        {
            List<AudioPlayer.WaveDevice> retVal = new List<AudioPlayer.WaveDevice>();
            foreach (var dev in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                AudioPlayer.WaveDevice di = new AudioPlayer.WaveDevice()
                {
                    EndpointGuid = dev.ID,
                    FullName = dev.FriendlyName,
                    WaveDeviceId = -1,
                };

                for (int waveOutIdx = 0; waveOutIdx < waveInGetNumDevs(); waveOutIdx++)
                {
                    string guid = GetWaveInEndpointId(waveOutIdx);
                    if (guid == di.EndpointGuid)
                    {
                        di.WaveDeviceId = waveOutIdx;
                        break;
                    }
                }
                retVal.Add(di);
            }
            return retVal;
        }

        static SpeechRecogniser()
        {
            if (UserSettings.GetUserSettings().getBoolean("use_naudio_for_speech_recognition"))
            {
                String speechRecognitionDeviceGuid = UserSettings.GetUserSettings().getString("NAUDIO_RECORDING_DEVICE_GUID");
                bool foundSpeechRecognitionDevice = false;
                speechRecognitionDevices.Clear();
                List<AudioPlayer.WaveDevice> devices = GetWaveInDevices();
                foreach (var dev in devices)
                {
                    NAudio.Wave.WaveInCapabilities capabilities = NAudio.Wave.WaveIn.GetCapabilities(dev.WaveDeviceId);
                    // Update legacy audio device "GUID" to MMdevice guid which does does contain a unique GUID
                    if (speechRecognitionDeviceGuid.Contains(capabilities.ProductName))
                    {
                        UserSettings.GetUserSettings().setProperty("NAUDIO_RECORDING_DEVICE_GUID", dev.EndpointGuid);
                        UserSettings.GetUserSettings().saveUserSettings();
                    }
                    int disambiguator = 2;
                    string disambiguatedFullName = dev.FullName;
                    while (speechRecognitionDevices.ContainsKey(disambiguatedFullName))
                    {
                        disambiguatedFullName = dev.FullName + "(" + disambiguator + ")";
                        disambiguator++;
                    }
                    Console.WriteLine($"Device name: {disambiguatedFullName} Guid: {dev.EndpointGuid} DeviceWaveId {dev.WaveDeviceId}");
                    speechRecognitionDevices.Add(disambiguatedFullName, new Tuple<string, int>(dev.EndpointGuid, dev.WaveDeviceId));
                }
                // Check that A device has been selected
                if (String.IsNullOrEmpty(speechRecognitionDeviceGuid))
                {
                    speechRecognitionDeviceGuid = AudioPlayer.GetDefaultInputDeviceId();
                    UserSettings.GetUserSettings().setProperty("NAUDIO_RECORDING_DEVICE_GUID", speechRecognitionDeviceGuid);
                    UserSettings.GetUserSettings().saveUserSettings();
                    AudioPlayer.UpdateUI();
                    Log.Error($"No message audio input device selected, setting to default: {AudioPlayer.GetDefaultInputDeviceName()}");
                }
                foreach (var dev in speechRecognitionDevices)
                {
                    if (dev.Value.Item1 == speechRecognitionDeviceGuid)
                    {
                        Console.WriteLine($"Detected saved audio input device: {dev.Key}");
                        foundSpeechRecognitionDevice = true;
                    }
                }
                if (!foundSpeechRecognitionDevice)
                {
                    Console.WriteLine($"Unable to find saved audio input device, using default: {AudioPlayer.GetDefaultInputDeviceName()}");
                }
            }
        }

        // load voice commands for triggering keyboard macros. The String key of the input Dictionary is the
        // command list key in speech_recognition_config.txt. When one of these phrases is heard the map value
        // CommandMacro is executed.
        public void loadMacroVoiceTriggers(Dictionary<string, ExecutableCommandMacro> voiceTriggeredMacros)
        {
            if (!initialised)
            {
                return;
            }
            macroLookup.Clear();
            if (macroGrammar != null && macroGrammar.Loaded())
            {
                sreWrapper.UnloadGrammar(macroGrammar);
            }
            if (voiceTriggeredMacros.Count == 0)
            {
                Console.WriteLine("No macro voice triggers defined for the current game.");
                return;
            }
            ChoicesWrapper macroChoices = SREWrapperFactory.createNewChoicesWrapper();
            foreach (KeyValuePair<String, ExecutableCommandMacro> entry in voiceTriggeredMacros)
            {
                String triggerPhrase = entry.Key;
                ExecutableCommandMacro executableCommandMacro = entry.Value;
                if (executableCommandMacro.macro.intRange != null)
                {
                    foreach (KeyValuePair<String[], int> numberEntry in numberToNumber)
                    {
                        if (numberEntry.Key != null && numberEntry.Key.Length > 0 && numberEntry.Value >= executableCommandMacro.macro.intRange.Item1 && numberEntry.Value <= executableCommandMacro.macro.intRange.Item2)
                        {
                            String thisPhrase = executableCommandMacro.macro.startPhrase + numberEntry.Key[0] + executableCommandMacro.macro.endPhrase;
                            if (!macroLookup.ContainsKey(thisPhrase))
                            {
                                macroLookup.Add(thisPhrase, voiceTriggeredMacros[triggerPhrase]);
                            }
                            macroChoices.Add(thisPhrase);
                        }
                    }
                }
                else
                {
                    // validate?
                    if (!macroLookup.ContainsKey(triggerPhrase))
                    {
                        macroLookup.Add(triggerPhrase, voiceTriggeredMacros[triggerPhrase]);
                    }
                    macroChoices.Add(triggerPhrase);
                }
            }
            GrammarBuilderWrapper macroGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper();
            macroGrammarBuilder.SetCulture(cultureInfo);
            macroGrammarBuilder.Append(macroChoices);
            macroGrammar = SREWrapperFactory.createNewGrammarWrapper(macroGrammarBuilder, "macroGrammarBuilder");
            sreWrapper.LoadGrammar(macroGrammar);
            Console.WriteLine("Loaded " + voiceTriggeredMacros.Count + " macro voice triggers into the speech recogniser");
        }

        private static Dictionary<String[], int> getNumberMappings(int start, int end)
        {
            Dictionary<String[], int> dict = new Dictionary<string[], int>();
            for (int i = start; i <= end; i++)
            {
                dict.Add(Configuration.getSpeechRecognitionPhrases(i.ToString()), i);
            }
            return dict;
        }

        private static Dictionary<String[], string> getCarNumberMappings()
        {
            Dictionary<String[], string> dict = new Dictionary<string[], string>();
            for (int i = 0; i <= 999; i++)
            {
                dict.Add(getPossibleCarNumberPhrases(i), i.ToString());
            }
            return dict;
        }

        private static string[] getPossibleCarNumberPhrases(int number)
        {
            List<string> phrases = new List<string>();
            string numberStr = number.ToString();
            phrases.AddRange(Configuration.getSpeechRecognitionPhrases(numberStr));
            if (number < 100)
            {
                // add a leading zero if 1 < 100
                numberStr = "0" + numberStr;
                phrases.AddRange(Configuration.getSpeechRecognitionPhrases(numberStr));
                if (number < 10)
                {
                    // add another leading zero if i < 10
                    numberStr = "0" + numberStr;
                    phrases.AddRange(Configuration.getSpeechRecognitionPhrases(numberStr));
                }
            }
            // for numbers >= 100 allow "one three one" and "one thirty one" forms
            if (number >= 100)
            {
                string leadingNumber = numberStr[0].ToString();
                string middleNumber = numberStr[1].ToString();
                string finalNumber = numberStr[2].ToString();
                string leadingNumberPhrase = Configuration.getSpeechRecognitionPhrases(leadingNumber).FirstOrDefault();   //"one" / "two" / etc
                string middleNumberPhrase = Configuration.getSpeechRecognitionPhrases(middleNumber).FirstOrDefault();
                string finalNumberPhrase = Configuration.getSpeechRecognitionPhrases(finalNumber).FirstOrDefault();
                string combinedFinalNumbersPhrase = Configuration.getSpeechRecognitionPhrases(middleNumber + finalNumber).FirstOrDefault();   //"twenty-one" / "twenty-two" / etc

                if (leadingNumberPhrase == null || middleNumberPhrase == null || finalNumberPhrase == null || combinedFinalNumbersPhrase == null)
                {
                    return phrases.ToArray();
                }

                if (middleNumber == "0")
                {
                    // need to add "one oh one", "five zero three", etc
                    string[] zeroPhrases = Configuration.getSpeechRecognitionPhrases("0");   //"zero", "oh", etc
                    foreach (string zeroPhrase in zeroPhrases)
                    {
                        phrases.Add(leadingNumberPhrase + "-" + zeroPhrase + "-" + finalNumberPhrase);
                    }
                }
                else
                {
                    // need to add "one two three", "four sevent eight", etc
                    phrases.Add(leadingNumberPhrase + "-" + middleNumberPhrase + "-" + finalNumberPhrase);
                    phrases.Add(leadingNumberPhrase + "-" + combinedFinalNumbersPhrase);
                }
            }
            return phrases.ToArray();
        }

        // if alwaysUseAllPhrases is true, we add all the phrase options to the recogniser even if the disable_alternative_voice_commands option is true.
        // If alwaysUseAllAppends is true we do the same thing with the append options.
        //
        // The generatedGrammars are loaded by this method call, they're only returned to allow us to detect which grammar has been triggered - the
        // opponent grammar processing stuff needs this.
        private List<GrammarWrapper> addCompoundChoices(String[] phrases, Boolean alwaysUseAllPhrases, ChoicesWrapper choices, String[] append, Boolean alwaysUseAllAppends)
        {
            List<GrammarWrapper> generatedGrammars = new List<GrammarWrapper>();
            GrammarBuilderWrapper gb = SREWrapperFactory.createNewGrammarBuilderWrapper();
            gb.SetCulture(cultureInfo);
            ChoicesWrapper initialChoices = SREWrapperFactory.createNewChoicesWrapper();
            foreach (string s in phrases)
            {
                if (s == null || s.Trim().Count() == 0)
                {
                    continue;
                }
                initialChoices.Add(s);
                if (disable_alternative_voice_commands && !alwaysUseAllPhrases)
                {
                    break;
                }
            }
            gb.Append(initialChoices);
            gb.Append(choices);
            Boolean addAppendChoices = false;
            if (append != null && append.Length > 0)
            {
                ChoicesWrapper appendChoices = SREWrapperFactory.createNewChoicesWrapper();
                foreach (string sa in append)
                {
                    if (sa == null || sa.Trim().Count() == 0)
                    {
                        continue;
                    }
                    addAppendChoices = true;
                    appendChoices.Add(sa.Trim().Trim());
                    if (disable_alternative_voice_commands && !alwaysUseAllAppends)
                    {
                        break;
                    }
                }
                if (addAppendChoices)
                {
                    gb.Append(appendChoices);
                }
            }
            GrammarWrapper grammar = SREWrapperFactory.createNewGrammarWrapper(gb, phrases[0]);
            sreWrapper.LoadGrammar(grammar);
            generatedGrammars.Add(grammar);
            return generatedGrammars;
        }

        // ensure the SRE has stopped waiting for speech, and if we're in trigger-word mode, reset it to the
        // default audio input device
        public void stop()
        {
            StopWizardListening();
            if (!initialised)
            {
                return;
            }

            try
            {
                if (sreWrapper != null)
                {
                    sreWrapper.RecognizeAsyncCancel();
                }
                if (voiceOptionEnum == MainWindow.VoiceOptionEnum.TRIGGER_WORD)
                {
                    if (triggerSreWrapper != null)
                    {
                        triggerSreWrapper.RecognizeAsyncCancel();
                    }
                    if (sreWrapper != null)
                    {
                        sreWrapper.SetInputToDefaultAudioDevice();
                    }
                }
            }
            catch (Exception)
            {
                Log.Error("Error resetting recogniser");
            }
        }

        public void Dispose()
        {
            StopWizardListening();
            if (!initialised)
            {
                return;
            }

            if (waveIn != null)
            {
                try
                {
                    waveIn.Dispose();
                }
                catch (Exception e) {Log.Exception(e);}
            }
            // VL: do not dispose SRE engines.  It is not clear when, and if ever any stupid outstanding Async call will complete.
            // Outstanding Async calls block Dispose on shutdown.
            //
            // Another option is not to call any Async calls from SpeechRecognizer.stop if MainWindow.instance is null.  However,
            // since we are not continuously re-creating SRE instances, it is safest to simply not Dispose, as it is very unlikely
            // to cause any system wide impact/leak.
            if (sreWrapper != null)
            {
                try
                {
                    sreWrapper.SetInputToNull();
                }
                catch (Exception e) {Log.Exception(e);}
                try
                {
                    //sre.Dispose();
                }
                catch (Exception e) {Log.Exception(e);}
                sreWrapper = null;
            }
            if (triggerSreWrapper != null)
            {
                try
                {
                    //triggerSre.Dispose();
                }
                catch (Exception e) {Log.Exception(e);}
                triggerSreWrapper = null;
            }
            initialised = false;
        }

        public SpeechRecogniser(CrewChief crewChief)
        {
            float minimum_name_voice_recognition_confidence_windows = UserSettings.GetUserSettings().getFloat("minimum_name_voice_recognition_confidence_system_sre");
            float minimum_name_voice_recognition_confidence_microsoft = UserSettings.GetUserSettings().getFloat("minimum_name_voice_recognition_confidence");
            float minimum_trigger_voice_recognition_confidence_windows = UserSettings.GetUserSettings().getFloat("trigger_word_sre_min_confidence_system_sre");
            float minimum_trigger_voice_recognition_confidence_microsoft = UserSettings.GetUserSettings().getFloat("trigger_word_sre_min_confidence");
            float minimum_voice_recognition_confidence_windows = UserSettings.GetUserSettings().getFloat("minimum_voice_recognition_confidence_system_sre");
            float minimum_voice_recognition_confidence_microsoft = UserSettings.GetUserSettings().getFloat("minimum_voice_recognition_confidence");
            float minimum_rally_voice_recognition_confidence_windows = UserSettings.GetUserSettings().getFloat("minimum_rally_voice_recognition_confidence_system_sre");
            float minimum_rally_voice_recognition_confidence_microsoft = UserSettings.GetUserSettings().getFloat("minimum_rally_voice_recognition_confidence_microsoft_sre");
            this.crewChief = crewChief;
            if (minimum_name_voice_recognition_confidence_microsoft < 0 || minimum_name_voice_recognition_confidence_microsoft > 1)
            {
                minimum_name_voice_recognition_confidence_microsoft = 0.4f;
            }
            if (minimum_voice_recognition_confidence_microsoft < 0 || minimum_voice_recognition_confidence_microsoft > 1)
            {
                minimum_voice_recognition_confidence_microsoft = 0.5f;
            }
            if (minimum_trigger_voice_recognition_confidence_microsoft < 0 || minimum_trigger_voice_recognition_confidence_microsoft > 1)
            {
                minimum_trigger_voice_recognition_confidence_microsoft = 0.6f;
            }
            if (minimum_rally_voice_recognition_confidence_microsoft < 0 || minimum_rally_voice_recognition_confidence_microsoft > 1)
            {
                minimum_rally_voice_recognition_confidence_microsoft = 0.35f;
            }
            if (minimum_name_voice_recognition_confidence_windows < 0 || minimum_name_voice_recognition_confidence_windows > 1)
            {
                minimum_name_voice_recognition_confidence_windows = 0.75f;
            }
            if (minimum_voice_recognition_confidence_windows < 0 || minimum_voice_recognition_confidence_windows > 1)
            {
                minimum_voice_recognition_confidence_windows = 0.7f;
            }
            if (minimum_trigger_voice_recognition_confidence_windows < 0 || minimum_trigger_voice_recognition_confidence_windows > 1)
            {
                minimum_trigger_voice_recognition_confidence_windows = 0.95f;
            }
            if (minimum_rally_voice_recognition_confidence_windows < 0 || minimum_rally_voice_recognition_confidence_windows > 1)
            {
                minimum_rally_voice_recognition_confidence_windows = 0.55f;
            }
            if (SREWrapperFactory.useSystem)
            {
                this.recogniserName = "System recogniser";
                this.thresholds.Add(ThresholdType.STANDARD,
                    new SREThresholdInfo(minimum_voice_recognition_confidence_windows, minimum_voice_recognition_confidence_windows_prop_name, ThresholdType.STANDARD));
                this.thresholds.Add(ThresholdType.NAMES,
                     new SREThresholdInfo(minimum_name_voice_recognition_confidence_windows, minimum_name_voice_recognition_confidence_windows_prop_name, ThresholdType.NAMES));
                this.thresholds.Add(ThresholdType.RALLY,
                     new SREThresholdInfo(minimum_rally_voice_recognition_confidence_windows, minimum_rally_voice_recognition_confidence_windows_prop_name, ThresholdType.RALLY));
                this.thresholds.Add(ThresholdType.TRIGGER,
                     new SREThresholdInfo(minimum_trigger_voice_recognition_confidence_windows, minimum_trigger_voice_recognition_confidence_windows_prop_name, ThresholdType.TRIGGER));
            }
            else
            {
                this.recogniserName = "Microsoft recogniser";
                this.thresholds.Add(ThresholdType.STANDARD,
                    new SREThresholdInfo(minimum_voice_recognition_confidence_microsoft, minimum_voice_recognition_confidence_microsoft_prop_name, ThresholdType.STANDARD));
                this.thresholds.Add(ThresholdType.NAMES,
                     new SREThresholdInfo(minimum_name_voice_recognition_confidence_microsoft, minimum_name_voice_recognition_confidence_microsoft_prop_name, ThresholdType.NAMES));
                this.thresholds.Add(ThresholdType.RALLY,
                     new SREThresholdInfo(minimum_rally_voice_recognition_confidence_microsoft, minimum_rally_voice_recognition_confidence_microsoft_prop_name, ThresholdType.RALLY));
                this.thresholds.Add(ThresholdType.TRIGGER,
                    new SREThresholdInfo(minimum_trigger_voice_recognition_confidence_microsoft, minimum_trigger_voice_recognition_confidence_microsoft_prop_name, ThresholdType.TRIGGER));
            }
            if (numberToNumber.First().Key.Count() == 0)
            {
                Log.Warning("Number strings not available. It is likely that you have a custom speech_recognition_override.txt which does not include the default values. All voice commands, including macros, that use numbers will be disabled.");
            }
        }

        private Tuple<String, String> parseLocalePropertyValue(String value)
        {
            if (value != null && value.Length > 1)
            {
                if (value.Length == 2)
                {
                    return new Tuple<String, String>(value.ToLowerInvariant(), null);
                }
                if (value.Length == 4)
                {
                    return new Tuple<String, String>(value.Substring(0, 2).ToLowerInvariant(), value.Substring(2).ToUpperInvariant());
                }
                if (value.Length == 5)
                {
                    return new Tuple<String, String>(value.Substring(0, 2).ToLowerInvariant(), value.Substring(3).ToUpperInvariant());
                }
            }
            return new Tuple<String, String>(null, null);
        }

        private Boolean initWithLocale()
        {
            Debug.Assert(!initialised);
            if (initialised)
            {
                return false;
            }
            LangCodes langCodes = getLangCodes();
            this.cultureInfo = SREWrapperFactory.GetCultureInfo(langCodes.langAndCountryToUse, langCodes.langToUse, true);

            if (cultureInfo != null)
            {
                Console.WriteLine("Got SRE for " + cultureInfo);
                this.sreWrapper = SREWrapperFactory.createNewSREWrapper(cultureInfo, voiceOptionEnum == MainWindow.VoiceOptionEnum.HOLD ? TimeSpan.FromSeconds(1.5) : (TimeSpan?)null);
                this.triggerSreWrapper = SREWrapperFactory.createNewSREWrapper(cultureInfo, (TimeSpan?) null);
                return this.sreWrapper != null;
            }
            if (langCodes.countryToUse == null)
            {
                if (langCodes.langToUse == "en")
                {
                    if (MessageBox.Show(
                        Utilities.Strings.NewlinesInLongString(Configuration.getUIString("install_any_speechlanguage_popup_text"), 55),
                        Configuration.getUIString("install_speechplatform_popup_title"),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
                    {
                        Process.Start("https://www.microsoft.com/en-us/download/details.aspx?id=27224");
                    }
                    Log.Error("Unable to initialise speech engine with English voice recognition pack. " +
                                       "Check that at least one of MSSpeech_SR_en-GB_TELE.msi, MSSpeech_SR_en-US_TELE.msi, " +
                                       "MSSpeech_SR_en-AU_TELE.msi, MSSpeech_SR_en-CA_TELE.msi or MSSpeech_SR_en-IN_TELE.msi are installed." +
                                       " It can be downloaded from https://www.microsoft.com/en-us/download/details.aspx?id=27224");
                }
                else
                {
                    if (MessageBox.Show(Configuration.getUIString("install_single_speechlanguage_popup_text_start") + langCodes.langToUse +
                    Configuration.getUIString("install_single_speechlanguage_popup_text_end"),
                    Configuration.getUIString("install_speechplatform_popup_title"),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
                    {
                        Process.Start("https://www.microsoft.com/en-us/download/details.aspx?id=27224");
                    }
                    Log.Error("Unable to initialise speech engine with '" + langCodes.langToUse + "' voice recognition pack. " +
                    "Check that and appropriate language pack is installed." +
                    " They can be downloaded from https://www.microsoft.com/en-us/download/details.aspx?id=27224");
                }

                return false;
            }
            else
            {
                if (MessageBox.Show(Configuration.getUIString("install_single_speechlanguage_popup_text_start") + langCodes.langAndCountryToUse +
                    Configuration.getUIString("install_single_speechlanguage_popup_text_end"),
                    Configuration.getUIString("install_speechplatform_popup_title"),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
                {
                    Process.Start("https://www.microsoft.com/en-us/download/details.aspx?id=27224");
                }
                Log.Error("Unable to initialise speech engine with voice recognition pack for location " + langCodes.langAndCountryToUse +
                          ". Check MSSpeech_SR_" + langCodes.langAndCountryToUse + "_TELE.msi is installed." +
                          " It can be downloaded from https://www.microsoft.com/en-us/download/details.aspx?id=27224");

                return false;
            }
        }

        private void validateAndAdd(String speechPhrase, ChoicesWrapper choices)
        {
            validateAndAdd(new string[] { speechPhrase }, choices);
        }

        private void validateAndAdd(String[] speechPhrases, ChoicesWrapper choices)
        {
            if (speechPhrases != null && speechPhrases.Count() > 0)
            {
                Boolean valid = true;
                foreach (String s in speechPhrases)
                {
                    if (s == null || s.Trim().Count() == 0)
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid)
                {
                    if (disable_alternative_voice_commands)
                    {
                        choices.Add(speechPhrases[0]);
                    }
                    else
                    {
                        choices.Add(speechPhrases);
                    }
                }
            }
        }

        public void initialiseSpeechEngine()
        {
            initialised = false;
            if (useNAudio)
            {
                buffer = new RingBufferStream.RingBufferStream(48000);
                waveIn = new NAudio.Wave.WaveInEvent();
                waveIn.DeviceNumber = SpeechRecogniser.speechInputDeviceIndex;
            }
            // try to initialize SpeechRecognitionEngine if it trows user is most likely missing SpeechPlatformRuntime.msi from the system
            LangCodes langCodes = getLangCodes();
            Console.WriteLine("got language codes data " + langCodes.ToString());
            this.cultureInfo = SREWrapperFactory.GetCultureInfo(langCodes.langAndCountryToUse, langCodes.langToUse, false);
            // if we're using the system SRE, check we have the required language before proceeding
            if (SREWrapperFactory.useSystem && this.cultureInfo == null)
            {
                // if we have no culture info here we need to fall back to the MS SRE and get the culture again
                Log.Error("Unable to get language for System SRE with lang " + langCodes.langToUse + " or " + langCodes.langAndCountryToUse);
                Log.Error("You may need to add an appropriate language from the Windows 'Time and language' control panel (go to Languages -> Add a language). " +
                          "App will fall back to Microsoft SRE");
                SREWrapperFactory.useSystem = false;
                this.cultureInfo = SREWrapperFactory.GetCultureInfo(langCodes.langAndCountryToUse, langCodes.langToUse, false);
            }
            var sre = SREWrapperFactory.createNewSREWrapper(this.cultureInfo, voiceOptionEnum == MainWindow.VoiceOptionEnum.HOLD ? TimeSpan.FromSeconds(1.5) : (TimeSpan?) null, true);

            if (sre == null)
            {
                if (MessageBox.Show(
                    Utilities.Strings.NewlinesInLongString(Configuration.getUIString("install_speechplatform_popup_text"), 55),
                    Configuration.getUIString("install_speechplatform_popup_title"),
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
                {
                    Process.Start("https://www.microsoft.com/en-us/download/details.aspx?id=27225");
                }
                Log.Error("Unable to initialise speech engine. Check that SpeechPlatformRuntime.msi is installed. It can be downloaded from https://www.microsoft.com/en-us/download/details.aspx?id=27225");
                return;
            }

            //this is not likely to throw but we try to catch it anyways.
            try
            {
                if (!initWithLocale())
                {
                    return;
                }
                Console.WriteLine("Speech engine initialized successfully.");
            }
            catch (Exception ex)
            {
                Log.Error("Unable to initialise speech engine.");
                Log.Exception(ex);
            }

            try
            {
                if (useNAudio)
                {
                    waveIn.WaveFormat = new NAudio.Wave.WaveFormat(nAudioWaveInSampleRate, nAudioWaveInChannelCount);
                    waveIn.DataAvailable += new EventHandler<NAudio.Wave.WaveInEventArgs>(waveIn_DataAvailable);
                    waveIn.NumberOfBuffers = 3;
                }
                else
                {
                    sreWrapper.SetInputToDefaultAudioDevice();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Unable to set default audio device, speech recognition may not function and may crash the app");
                Log.Exception(ex);
            }

            sreWrapper.SetInitialSilenceTimeout(TimeSpan.Zero);
            try
            {
                if (SREWrapperFactory.useSystem)
                {
                    sreWrapper.AddSpeechRecognizedCallback(new EventHandler<System.Speech.Recognition.SpeechRecognizedEventArgs>(sre_SpeechRecognizedSystem));
                    sreWrapper.AddRecognitionCompleteCallback(new EventHandler<System.Speech.Recognition.RecognizeCompletedEventArgs>(sre_SpeechRecognitionCompleteSystem));
                    sreWrapper.AddRecognitionRejectedCallback(new EventHandler<System.Speech.Recognition.SpeechRecognitionRejectedEventArgs>(sre_SpeechRecognitionRejectedSystem));
                    triggerSreWrapper.AddSpeechRecognizedCallback(new EventHandler<System.Speech.Recognition.SpeechRecognizedEventArgs>(trigger_SpeechRecognizedSystem));
                    triggerSreWrapper.AddRecognitionCompleteCallback(new EventHandler<System.Speech.Recognition.RecognizeCompletedEventArgs>(sre_SpeechRecognitionCompleteSystem));
                    triggerSreWrapper.AddRecognitionRejectedCallback(new EventHandler<System.Speech.Recognition.SpeechRecognitionRejectedEventArgs>(sre_SpeechRecognitionRejectedSystem));
                }
                else
                {
                    sreWrapper.AddSpeechRecognizedCallback(new EventHandler<Microsoft.Speech.Recognition.SpeechRecognizedEventArgs>(sre_SpeechRecognizedMicrosoft));
                    sreWrapper.AddRecognitionCompleteCallback(new EventHandler<Microsoft.Speech.Recognition.RecognizeCompletedEventArgs>(sre_SpeechRecognitionCompleteMicrosoft));
                    sreWrapper.AddRecognitionRejectedCallback(new EventHandler<Microsoft.Speech.Recognition.SpeechRecognitionRejectedEventArgs>(sre_SpeechRecognitionRejectedMicrosoft));
                    triggerSreWrapper.AddSpeechRecognizedCallback(new EventHandler<Microsoft.Speech.Recognition.SpeechRecognizedEventArgs>(trigger_SpeechRecognizedMicrosoft));
                    triggerSreWrapper.AddRecognitionCompleteCallback(new EventHandler<Microsoft.Speech.Recognition.RecognizeCompletedEventArgs>(sre_SpeechRecognitionCompleteMicrosoft));
                    triggerSreWrapper.AddRecognitionRejectedCallback(new EventHandler<Microsoft.Speech.Recognition.SpeechRecognitionRejectedEventArgs>(sre_SpeechRecognitionRejectedMicrosoft));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to add event handler to speech engine");
                Log.Exception(ex);
                return;
            }
            if (CrewChief.Debug.SaveSREDebugData)
            {
                Console.WriteLine("SRE is running in debug mode. Captured audio and metadata will be written to " + debugDataPath);
            }
            initialised = true;
        }

        public void loadSRECommands()
        {
            if (sreWrapper == null)
            {
                return;
            }
            try
            {
                sreWrapper.UnloadAllGrammars();
                iracingPitstopGrammarList.Clear();
                r3ePitstopGrammarList.Clear();
                accPitstopGrammarList.Clear();
                rallyGrammarList.Clear();
                pitManagerGrammarList.Clear();
                overlayGrammarList.Clear();
                opponentGrammarList.Clear();
                if (disable_alternative_voice_commands)
                {
                    Console.WriteLine("*Alternative voice commands are disabled, only the first command from each line in speech_recognition_config.txt will be available*");
                }
                else
                {
                    Console.WriteLine("Loading all voice command alternatives from speech_recognition_config.txt");
                }

                // generic commands for all games. Note that these won't necessarily be wired up for every game
                ChoicesWrapper staticSpeechChoices = SREWrapperFactory.createNewChoicesWrapper();
                Console.WriteLine("Loading shared SRE commands");

                validateAndAdd(WHATS_THE_TIME, staticSpeechChoices);
                validateAndAdd(REPEAT_LAST_MESSAGE, staticSpeechChoices);
                validateAndAdd(RADIO_CHECK, staticSpeechChoices);

                if (UserSettings.GetUserSettings().getBoolean("enable_overlay_window")
                    && !this.disableOverlayVoiceCommands)
                {
                    validateAndAdd(HIDE_OVERLAY, staticSpeechChoices);
                    validateAndAdd(SHOW_OVERLAY, staticSpeechChoices);
                    validateAndAdd(SHOW_CONSOLE, staticSpeechChoices);
                    validateAndAdd(SHOW_All_OVERLAYS, staticSpeechChoices);
                    validateAndAdd(SHOW_CHART, staticSpeechChoices);
                    validateAndAdd(CLEAR_CHART, staticSpeechChoices);
                    validateAndAdd(REFRESH_CHART, staticSpeechChoices);
                    validateAndAdd(SHOW_STACKED_CHARTS, staticSpeechChoices);
                    validateAndAdd(SHOW_SINGLE_CHART, staticSpeechChoices);
                    validateAndAdd(CLEAR_DATA, staticSpeechChoices);
                    validateAndAdd(SHOW_TIME, staticSpeechChoices);
                    validateAndAdd(SHOW_DISTANCE, staticSpeechChoices);
                    validateAndAdd(HIDE_CONSOLE, staticSpeechChoices);
                    validateAndAdd(HIDE_CHART, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_SHOW_SECTOR_1, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_SHOW_SECTOR_2, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_SHOW_SECTOR_3, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_SHOW_ALL_SECTORS, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_ZOOM_IN, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_ZOOM_OUT, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_RESET_ZOOM, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_PAN_LEFT, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_PAN_RIGHT, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_SHOW_NEXT_LAP, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_SHOW_PREVIOUS_LAP, staticSpeechChoices);
                    validateAndAdd(CHART_COMMAND_SHOW_LAST_LAP, staticSpeechChoices);
                }

                if (UserSettings.GetUserSettings().getBoolean("enable_subtitle_overlay")
                    && !this.disableOverlayVoiceCommands)
                {
                    validateAndAdd(SHOW_SUBTITLES, staticSpeechChoices);
                    validateAndAdd(HIDE_SUBTITLES, staticSpeechChoices);
                }

                if (UserSettings.GetUserSettings().getBoolean("enable_vr_overlay_windows")
                    && !this.disableOverlayVoiceCommands)
                {
                    validateAndAdd(TOGGLE_VR_OVERLAYS, staticSpeechChoices);
                    validateAndAdd(SHOW_VR_SETTING, staticSpeechChoices);
                    validateAndAdd(HIDE_VR_SETTING, staticSpeechChoices);
                }

                if (alarmClockVoiceRecognitionEnabled)
                {
                    validateAndAdd(CLEAR_ALARM_CLOCK, staticSpeechChoices);
                    this.hourChoices = SREWrapperFactory.createNewChoicesWrapper();
                    foreach (KeyValuePair<String[], int> entry in hourMappings)
                    {
                        foreach (String numberStr in entry.Key)
                        {
                            hourChoices.Add(numberStr);
                        }
                    }
                    List<String> minuteArray = new List<String>();
                    foreach (KeyValuePair<String[], int> entry in minuteMappings)
                    {
                        foreach (String numberStr in entry.Key)
                        {
                            foreach (String ams in AM)
                            {
                                minuteArray.Add(numberStr + " " + ams);
                            }
                            foreach (String pms in PM)
                            {
                                minuteArray.Add(numberStr + " " + pms);
                            }
                            minuteArray.Add(numberStr);
                        }
                    }
                    if (!hourChoices.IsEmpty()) {
                      addCompoundChoices(SET_ALARM_CLOCK, false, this.hourChoices, minuteArray.ToArray(), true);
                    }
                }

                if (GameOrVoipVolume.VolumeControlEnabled)
                {
                    validateAndAdd(CREW_CHIEF_QUIETER, staticSpeechChoices);
                    validateAndAdd(CREW_CHIEF_LOUDER, staticSpeechChoices);
                    validateAndAdd(GAME_QUIETER, staticSpeechChoices);
                    validateAndAdd(GAME_LOUDER, staticSpeechChoices);
                    validateAndAdd(VOIP_QUIETER, staticSpeechChoices);
                    validateAndAdd(VOIP_LOUDER, staticSpeechChoices);
                }

                if (!Game.NONE)
                {
                    validateAndAdd(HOWS_MY_TYRE_WEAR, staticSpeechChoices);
                    validateAndAdd(HOWS_MY_TRANSMISSION, staticSpeechChoices);
                    validateAndAdd(HOWS_MY_AERO, staticSpeechChoices);
                    validateAndAdd(HOWS_MY_ENGINE, staticSpeechChoices);
                    validateAndAdd(HOWS_MY_SUSPENSION, staticSpeechChoices);
                    validateAndAdd(HOWS_MY_BRAKES, staticSpeechChoices);
                    validateAndAdd(HOWS_MY_FUEL, staticSpeechChoices);
                    validateAndAdd(HOWS_MY_BATTERY, staticSpeechChoices);
                    validateAndAdd(WHAT_ARE_MY_ENGINE_TEMPS, staticSpeechChoices);
                    validateAndAdd(WHAT_IS_MY_OIL_TEMP, staticSpeechChoices);
                    validateAndAdd(WHAT_IS_MY_WATER_TEMP, staticSpeechChoices);
                    validateAndAdd(HOW_ARE_MY_TYRE_TEMPS, staticSpeechChoices);
                    validateAndAdd(WHAT_ARE_MY_TYRE_TEMPS, staticSpeechChoices);
                    validateAndAdd(WHAT_ARE_MY_TYRE_PRESSURES, staticSpeechChoices);
                    validateAndAdd(HOW_ARE_MY_BRAKE_TEMPS, staticSpeechChoices);
                    validateAndAdd(WHAT_ARE_MY_BRAKE_TEMPS, staticSpeechChoices);
                    validateAndAdd(HOW_ARE_MY_ENGINE_TEMPS, staticSpeechChoices);

                    validateAndAdd(DAMAGE_REPORT, staticSpeechChoices);
                    validateAndAdd(CAR_STATUS, staticSpeechChoices);
                    validateAndAdd(SESSION_STATUS, staticSpeechChoices);
                    validateAndAdd(STATUS, staticSpeechChoices);
                    validateAndAdd(WHATS_THE_AIR_TEMP, staticSpeechChoices);
                    validateAndAdd(WHATS_THE_TRACK_TEMP, staticSpeechChoices);
                    validateAndAdd(WHATS_MY_BRAKE_BIAS, staticSpeechChoices);
                    validateAndAdd(MORE_INFO, staticSpeechChoices);
                    validateAndAdd(I_AM_OK, staticSpeechChoices);
                }

                GrammarBuilderWrapper staticGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper();
                staticGrammarBuilder.SetCulture(cultureInfo);
                staticGrammarBuilder.Append(staticSpeechChoices);
                GrammarWrapper staticGrammar = SREWrapperFactory.createNewGrammarWrapper(staticGrammarBuilder, "SREcommands");
                sreWrapper.LoadGrammar(staticGrammar);
                // end of shared commands

                // now the commands for the game type
                loadSRECommandsForGameType();
                // now the commands for the specific game
                loadSRECommandsForSpecificGame();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to configure speech engine grammar");
                Log.Exception(ex);
                return;
            }
        }

        private void loadSRECommandsForSpecificGame()
        {
            switch (CrewChief.gameDefinition.gameEnum)
            {
                case GameEnum.IRACING:
                    addiRacingSpeechRecogniser();
                    addRatingsCommands();
                    break;
                case GameEnum.RACE_ROOM:
                    addR3ESpeechRecogniser();
                    addRatingsCommands();
                    break;
                case GameEnum.RF2_64BIT:
                    addPitManagerSpeechRecogniser("rf2_enable_pit_manager");
                    break;
                case GameEnum.LMU:
                    addPitManagerSpeechRecogniser("lmu_enable_pit_manager");
                    break;
                case GameEnum.ACC:
                    addACCPitManagerSpeechRecogniser();
                    break;
                default:
                    break;
            }
        }

        private void loadSRECommandsForGameType()
        {
            switch (Game.gameType)
            {
                // standard circuit racing games
                case GameType.ACC:
                case GameType.AMS2:
                case GameType.ASSETTO:
                case GameType.GTR2:
                case GameType.IRACING:
                case GameType.PCARS:
                case GameType.PCARS2:
                case GameType.RACE_ROOM:
                case GameType.RF1:
                case GameType.RF2:
                case GameType.LMU:
                    loadSpotterCommands();
                    loadBasicCircuitRacingCommands();
                    loadExtendedCircuitRacingCommands();
                    break;
                // 'basic' circuit racing games (heh)
                case GameType.PCARS3:
                    loadSpotterCommands();
                    loadBasicCircuitRacingCommands();
                    break;
                // spotter-only games
                case GameType.F1_20S:
                    loadSpotterCommands();
                    break;
                // rally games
                case GameType.RBR:
                case GameType.DIRT:
                case GameType.DIRT2:
                case GameType.ASSETTO_RALLY:
                    addRallySpeechRecogniser();
                    break;
                default:
                    break;
            }
        }

        private void loadSpotterCommands()
        {
            try
            {
                if (!disableBehaviorAlteringVoiceCommands)
                {
                    Console.WriteLine("Loading spotter speech recognition commands");
                    ChoicesWrapper staticSpeechChoices = SREWrapperFactory.createNewChoicesWrapper();
                    validateAndAdd(SPOT, staticSpeechChoices);
                    validateAndAdd(DONT_SPOT, staticSpeechChoices);
                    if (!staticSpeechChoices.IsEmpty()) {
                        GrammarBuilderWrapper staticGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper();
                        staticGrammarBuilder.SetCulture(cultureInfo);
                        staticGrammarBuilder.Append(staticSpeechChoices);
                        GrammarWrapper staticGrammar = SREWrapperFactory.createNewGrammarWrapper(staticGrammarBuilder, "SpotterCommands");
                        sreWrapper.LoadGrammar(staticGrammar);
                    }
                }
                else
                {
                    Console.WriteLine("Skipping spotter speech recognition command because behaviour-altering commands are enabled");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to configure spotter speech engine grammar");
                Log.Exception(ex);
                return;
            }
        }

        private void loadBasicCircuitRacingCommands()
        {
            try
            {
                Console.WriteLine("Loading basic circuit racing speech recognition commands");
                this.digitsChoices = SREWrapperFactory.createNewChoicesWrapper();
                foreach (KeyValuePair<String[], int> entry in numberToNumber)
                {
                    foreach (String numberStr in entry.Key)
                    {
                        digitsChoices.Add(numberStr);
                    }
                }

                ChoicesWrapper staticSpeechChoices = SREWrapperFactory.createNewChoicesWrapper();
                validateAndAdd(HOWS_MY_PACE, staticSpeechChoices);
                validateAndAdd(HOWS_MY_SELF_PACE, staticSpeechChoices);
                validateAndAdd(WHATS_MY_GAP_IN_FRONT, staticSpeechChoices);
                validateAndAdd(WHATS_MY_GAP_BEHIND, staticSpeechChoices);
                validateAndAdd(WHATS_MY_GAP_IN_FRONT_ON_TRACK, staticSpeechChoices);
                validateAndAdd(WHATS_MY_GAP_BEHIND_ON_TRACK, staticSpeechChoices);
                validateAndAdd(WHATS_MY_GAP_TO_LEADER, staticSpeechChoices);
                validateAndAdd(WHATS_THE_LEADER_LAP, staticSpeechChoices);
                validateAndAdd(NOTE_BAD_DRIVER_AHEAD_ON_TRACK, staticSpeechChoices);
                validateAndAdd(NOTE_BAD_DRIVER_BEHIND_ON_TRACK, staticSpeechChoices);
                validateAndAdd(WHAT_WAS_MY_LAST_LAP_TIME, staticSpeechChoices);
                validateAndAdd(WHATS_MY_BEST_LAP_TIME, staticSpeechChoices);
                validateAndAdd(WHATS_MY_POSITION, staticSpeechChoices);
                validateAndAdd(WHATS_MY_EXPECTED_FINISH_POSITION, staticSpeechChoices);
                validateAndAdd(WHAT_TYRES_AM_I_ON, staticSpeechChoices);
                validateAndAdd(WHAT_ARE_THE_RELATIVE_TYRE_PERFORMANCES, staticSpeechChoices);
                validateAndAdd(PLAY_CORNER_NAMES, staticSpeechChoices);
                validateAndAdd(TOGGLE_TURN_NUMBERS, staticSpeechChoices);

                validateAndAdd(START_PACE_NOTES_PLAYBACK, staticSpeechChoices);
                validateAndAdd(STOP_PACE_NOTES_PLAYBACK, staticSpeechChoices);

                validateAndAdd(HOWS_MY_LEFT_FRONT_CAMBER, staticSpeechChoices);
                validateAndAdd(HOWS_MY_RIGHT_FRONT_CAMBER, staticSpeechChoices);
                validateAndAdd(HOWS_MY_LEFT_REAR_CAMBER, staticSpeechChoices);
                validateAndAdd(HOWS_MY_RIGHT_REAR_CAMBER, staticSpeechChoices);
                validateAndAdd(HOWS_MY_FRONT_CAMBER, staticSpeechChoices);
                validateAndAdd(HOWS_MY_REAR_CAMBER, staticSpeechChoices);
                validateAndAdd(HOW_ARE_MY_TYRE_PRESSURES, staticSpeechChoices);
                validateAndAdd(HOW_ARE_MY_FRONT_TYRE_PRESSURES, staticSpeechChoices);
                validateAndAdd(HOW_ARE_MY_REAR_TYRE_PRESSURES, staticSpeechChoices);
                validateAndAdd(HOWS_MY_LEFT_FRONT_CAMBER_RIGHT_NOW, staticSpeechChoices);
                validateAndAdd(HOWS_MY_RIGHT_FRONT_CAMBER_RIGHT_NOW, staticSpeechChoices);
                validateAndAdd(HOWS_MY_LEFT_REAR_CAMBER_RIGHT_NOW, staticSpeechChoices);
                validateAndAdd(HOWS_MY_RIGHT_REAR_CAMBER_RIGHT_NOW, staticSpeechChoices);
                validateAndAdd(HOWS_MY_FRONT_CAMBER_RIGHT_NOW, staticSpeechChoices);
                validateAndAdd(HOWS_MY_REAR_CAMBER_RIGHT_NOW, staticSpeechChoices);
                validateAndAdd(HOW_ARE_MY_TYRE_PRESSURES_RIGHT_NOW, staticSpeechChoices);
                validateAndAdd(HOW_ARE_MY_FRONT_TYRE_PRESSURES_RIGHT_NOW, staticSpeechChoices);
                validateAndAdd(HOW_ARE_MY_REAR_TYRE_PRESSURES_RIGHT_NOW, staticSpeechChoices);
                validateAndAdd(WHAT_ARE_MY_LEFT_FRONT_SURFACE_TEMPS, staticSpeechChoices);
                validateAndAdd(WHAT_ARE_MY_LEFT_REAR_SURFACE_TEMPS, staticSpeechChoices);
                validateAndAdd(WHAT_ARE_MY_RIGHT_FRONT_SURFACE_TEMPS, staticSpeechChoices);
                validateAndAdd(WHAT_ARE_MY_RIGHT_REAR_SURFACE_TEMPS, staticSpeechChoices);

                if (!disableBehaviorAlteringVoiceCommands)
                {
                    validateAndAdd(KEEP_QUIET, staticSpeechChoices);
                    validateAndAdd(KEEP_ME_INFORMED, staticSpeechChoices);
                    validateAndAdd(TELL_ME_THE_GAPS, staticSpeechChoices);
                    validateAndAdd(DONT_TELL_ME_THE_GAPS, staticSpeechChoices);
                    validateAndAdd(ENABLE_YELLOW_FLAG_MESSAGES, staticSpeechChoices);
                    validateAndAdd(DISABLE_YELLOW_FLAG_MESSAGES, staticSpeechChoices);
                    validateAndAdd(ENABLE_MANUAL_FORMATION_LAP, staticSpeechChoices);
                    validateAndAdd(DISABLE_MANUAL_FORMATION_LAP, staticSpeechChoices);
                    validateAndAdd(TALK_TO_ME_ANYWHERE, staticSpeechChoices);
                    validateAndAdd(DONT_TALK_IN_THE_CORNERS, staticSpeechChoices);
                    validateAndAdd(ENABLE_CUT_TRACK_WARNINGS, staticSpeechChoices);
                    validateAndAdd(DISABLE_CUT_TRACK_WARNINGS, staticSpeechChoices);
                    validateAndAdd(STOP_COMPLAINING, staticSpeechChoices);
                }

                validateAndAdd(WHATS_THE_FASTEST_LAP_TIME, staticSpeechChoices);

                validateAndAdd(WHERE_AM_I_FASTER, staticSpeechChoices);
                validateAndAdd(WHERE_AM_I_SLOWER, staticSpeechChoices);

                validateAndAdd(HOW_LONGS_LEFT, staticSpeechChoices);
                validateAndAdd(WHAT_LAP_AM_I_ON, staticSpeechChoices);
                validateAndAdd(WHAT_ARE_MY_SECTOR_TIMES, staticSpeechChoices);
                validateAndAdd(WHATS_MY_LAST_SECTOR_TIME, staticSpeechChoices);

                validateAndAdd(WHOS_IN_FRONT_IN_THE_RACE, staticSpeechChoices);
                validateAndAdd(WHOS_TWO_IN_FRONT_IN_THE_RACE, staticSpeechChoices);
                validateAndAdd(WHOS_BEHIND_IN_THE_RACE, staticSpeechChoices);
                validateAndAdd(WHOS_IN_FRONT_ON_TRACK, staticSpeechChoices);
                validateAndAdd(WHOS_BEHIND_ON_TRACK, staticSpeechChoices);
                validateAndAdd(WHOS_LEADING, staticSpeechChoices);
                validateAndAdd(WHEN_DID_IN_FRONT_ON_TRACK_PIT, staticSpeechChoices);
                validateAndAdd(WHEN_DID_BEHIND_ON_TRACK_PIT, staticSpeechChoices);

                validateAndAdd(WHATS_MY_CAR_NUMBER, staticSpeechChoices);
                validateAndAdd(WHATS_MY_RATING, staticSpeechChoices);
                validateAndAdd(WHATS_MY_RANK, staticSpeechChoices);
                validateAndAdd(WHATS_MY_REPUTATION, staticSpeechChoices);

                GrammarBuilderWrapper staticGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper();
                staticGrammarBuilder.SetCulture(cultureInfo);
                staticGrammarBuilder.Append(staticSpeechChoices);
                GrammarWrapper staticGrammar = SREWrapperFactory.createNewGrammarWrapper(staticGrammarBuilder, "BasicCircuitRacingCommands");
                sreWrapper.LoadGrammar(staticGrammar);

                if (SREWrapperFactory.useSystem && useFreeDictationForChatMessages)
                {
                    this.chatDictationGrammar = SREWrapperFactory.CreateChatDictationGrammarWrapper();
                    SREWrapperFactory.LoadChatDictationGrammar(this.sreWrapper, this.chatDictationGrammar, chatContextStart, chatContextEnd);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to configure basic circuit racing speech engine grammar");
                Log.Exception(ex);
                return;
            }
        }

        private void loadExtendedCircuitRacingCommands()
        {
            try
            {
                Console.WriteLine("Loading " + CrewChief.gameDefinition.friendlyName + " speech recognition commands");
                ChoicesWrapper staticSpeechChoices = SREWrapperFactory.createNewChoicesWrapper();
                validateAndAdd(WHATS_MY_FUEL_LEVEL, staticSpeechChoices);
                validateAndAdd(WHATS_MY_FUEL_USAGE, staticSpeechChoices);

                // the fuel strategy influences this answer, as well as pit managers
                validateAndAdd(SET_FUEL_STRATEGY_CAUTIOUS, staticSpeechChoices);
                validateAndAdd(SET_FUEL_STRATEGY_RISKY, staticSpeechChoices);
                validateAndAdd(SET_FUEL_STRATEGY_RESET, staticSpeechChoices);
                validateAndAdd(HOW_MUCH_FUEL_TO_END_OF_RACE, staticSpeechChoices);
                validateAndAdd(HOW_LONG_WILL_THESE_TYRES_LAST, staticSpeechChoices);
                validateAndAdd(WHATS_PITLANE_SPEED_LIMIT, staticSpeechChoices);
                validateAndAdd(HOW_MANY_LAPS_SINCE_PITTING, staticSpeechChoices);
                validateAndAdd(WHERE_SHOULD_I_LINE_UP, staticSpeechChoices);
                validateAndAdd(WHAT_ARE_THE_PIT_ACTIONS, staticSpeechChoices);
                validateAndAdd(HAVE_I_SERVED_MY_PENALTY, staticSpeechChoices);
                validateAndAdd(DO_I_HAVE_A_PENALTY, staticSpeechChoices);
                validateAndAdd(DO_I_STILL_HAVE_A_PENALTY, staticSpeechChoices);
                validateAndAdd(IS_MY_PIT_BOX_OCCUPIED, staticSpeechChoices);
                validateAndAdd(PLAY_POST_PIT_POSITION_ESTIMATE, staticSpeechChoices);
                validateAndAdd(PRACTICE_PIT_STOP, staticSpeechChoices);
                validateAndAdd(DO_I_HAVE_A_MANDATORY_PIT_STOP, staticSpeechChoices);

                if (Game.IRACING)
                {
                    validateAndAdd(HOW_OLD_ARE_THESE_TYRES, staticSpeechChoices);
                }

                validateAndAdd(WHAT_CLASS_IS_CAR_AHEAD, staticSpeechChoices);
                validateAndAdd(WHAT_CLASS_IS_CAR_BEHIND, staticSpeechChoices);
                validateAndAdd(IS_CAR_AHEAD_MY_CLASS, staticSpeechChoices);
                validateAndAdd(IS_CAR_BEHIND_MY_CLASS, staticSpeechChoices);

                GrammarBuilderWrapper staticGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper();
                staticGrammarBuilder.SetCulture(cultureInfo);
                staticGrammarBuilder.Append(staticSpeechChoices);
                GrammarWrapper staticGrammar = SREWrapperFactory.createNewGrammarWrapper(staticGrammarBuilder, "ExtendedCircuitRacingCommands");
                sreWrapper.LoadGrammar(staticGrammar);

                // now the fuel choices
                List<string> fuelTimeChoices = new List<string>();
                if (disable_alternative_voice_commands)
                {
                    fuelTimeChoices.Add(LAPS[0]);
                    fuelTimeChoices.Add(MINUTES[0]);
                    fuelTimeChoices.Add(HOURS[0]);
                }
                else
                {
                    fuelTimeChoices.AddRange(LAPS);
                    fuelTimeChoices.AddRange(MINUTES);
                    fuelTimeChoices.AddRange(HOURS);
                }
                if (!digitsChoices.IsEmpty())
                {
                    addCompoundChoices(CALCULATE_FUEL_FOR, false, this.digitsChoices, fuelTimeChoices.ToArray(), true);
                }

                // now the 'laps on tyre set' choices, enabled for ACC only
                if (Game.ACC)
                {
                    ChoicesWrapper lapsOnTyreSetIntroChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                    foreach (string intro in HOW_MANY_LAPS_ON_TYRE_SET)
                    {
                        validateAndAdd(intro, lapsOnTyreSetIntroChoicesWrapper);
                    }
                    ChoicesWrapper tyreSetIntAmountChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                    foreach (KeyValuePair<String[], int> numberEntry in numberToNumber)
                    {
                        // 1-50 is the valid range here
                        if (numberEntry.Value >= 1 && numberEntry.Value <= 50)
                        {
                            validateAndAdd(numberEntry.Key[0], tyreSetIntAmountChoicesWrapper);
                        }
                    }
                    // now assemble the choices
                    GrammarBuilderWrapper lapsOnTyreSetGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper();
                    lapsOnTyreSetGrammarBuilder.SetCulture(cultureInfo);
                    lapsOnTyreSetGrammarBuilder.Append(lapsOnTyreSetIntroChoicesWrapper, 1, 1);
                    lapsOnTyreSetGrammarBuilder.Append(tyreSetIntAmountChoicesWrapper, 1, 1);
                    GrammarWrapper lapsOnTyreSetGrammar = SREWrapperFactory.createNewGrammarWrapper(lapsOnTyreSetGrammarBuilder, "lapsOnTyreSetGrammarBuilder");
                    sreWrapper.LoadGrammar(lapsOnTyreSetGrammar);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to configure " + CrewChief.gameDefinition.friendlyName + " speech engine grammar");
                Log.Exception(ex);
                return;
            }
        }

        private String[] getWhatsPossessiveChoices()
        {
            return CrewChief.gameDefinition != null && whatsOpponentChoices.ContainsKey(CrewChief.gameDefinition.gameEnum) ?
                whatsOpponentChoices[CrewChief.gameDefinition.gameEnum] : whatsOpponentChoices[GameEnum.UNKNOWN];
        }

        public void addNewOpponentName(String rawDriverName, String carNumberString)
        {
            if (!initialised || !DYNAMIC_PHRASES)
            {
                return;
            }
            try
            {
                String usableNameForSRE = DriverNameHelper.getUsableDriverNameForSRE(rawDriverName);
                Console.WriteLine("Adding new (mid-session joined) opponent name to speech recogniser: " + Environment.NewLine + usableNameForSRE);
                Tuple<HashSet<string>, HashSet<string>> choices = getDriverChoices(usableNameForSRE, carNumberString);
                loadNameAndNamePossessiveChoices(choices.Item1, choices.Item2);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to add new driver to speech recognition engine - ");
                Log.Exception(ex);
            }
        }

        public void addOpponentsSpeechRecognition(List<String> names, HashSet<string> carNumbers)
        {
            if (!initialised || !DYNAMIC_PHRASES)
            {
                return;
            }
            driverNamesInUse.Clear();
            carNumbersInUse.Clear();
            foreach (GrammarWrapper opponentGrammar in opponentGrammarList)
            {
                sreWrapper.UnloadGrammar(opponentGrammar);
            }
            opponentGrammarList.Clear();
            opponentsAddedToSoundCacheMidSession.Clear();

            // need choice sets for names, possessive names, positions, possessive positions, and combined:
            HashSet<string> nameChoices = new HashSet<string>();
            HashSet<string> namePossessiveChoices = new HashSet<string>();
            foreach (String name in names)
            {
                Tuple<HashSet<string>, HashSet<string>> choices = getDriverChoices(name, "-1");
                nameChoices.UnionWith(choices.Item1);
                namePossessiveChoices.UnionWith(choices.Item2);
            }
            Console.WriteLine("Adding names to SRE: " + Environment.NewLine + String.Join(", ", nameChoices));
            foreach (String number in carNumbers)
            {
                Tuple<HashSet<string>, HashSet<string>> choices = getDriverChoices(null, number);
                nameChoices.UnionWith(choices.Item1);
                namePossessiveChoices.UnionWith(choices.Item2);
            }
            ChoicesWrapper opponentNameChoices = SREWrapperFactory.createNewChoicesWrapper();
            ChoicesWrapper opponentNamePossessiveChoices = SREWrapperFactory.createNewChoicesWrapper();
            opponentNameChoices.Add(THE_CAR_AHEAD);
            opponentNameChoices.Add(THE_CAR_BEHIND);
            opponentNameChoices.Add(THE_LEADER);
            opponentNamePossessiveChoices.Add(THE_CAR_AHEAD + POSSESSIVE);
            opponentNamePossessiveChoices.Add(THE_CAR_BEHIND + POSSESSIVE);
            opponentNamePossessiveChoices.Add(THE_LEADER + POSSESSIVE);

            if (!disable_alternative_voice_commands)
            {
                opponentNameChoices.Add(THE_GUY_AHEAD);
                opponentNameChoices.Add(THE_CAR_IN_FRONT);
                opponentNameChoices.Add(THE_GUY_IN_FRONT);
                opponentNameChoices.Add(THE_GUY_BEHIND);
                opponentNamePossessiveChoices.Add(THE_GUY_AHEAD + POSSESSIVE);
                opponentNamePossessiveChoices.Add(THE_CAR_IN_FRONT + POSSESSIVE);
                opponentNamePossessiveChoices.Add(THE_GUY_IN_FRONT + POSSESSIVE);
                opponentNamePossessiveChoices.Add(THE_GUY_BEHIND + POSSESSIVE);
            }
            {
                loadNameAndNamePossessiveChoices(nameChoices, namePossessiveChoices, opponentNameChoices, opponentNamePossessiveChoices);
            }

            {
                ChoicesWrapper whosInPositionChoices = SREWrapperFactory.createNewChoicesWrapper();
                HashSet<string> positions = new HashSet<string>();
                HashSet<string> positionsPossessive = new HashSet<string>();
                foreach (KeyValuePair<String[], int> entry in racePositionNumberToNumber)
                {
                    foreach (String numberStr in entry.Key)
                    {
                        positions.Add(POSITION_SHORT + " " + numberStr);
                        positionsPossessive.Add(POSITION_SHORT + " " + numberStr + POSSESSIVE);
                        whosInPositionChoices.Add(POSITION_SHORT + " " + numberStr);
                        if (!disable_alternative_voice_commands)
                        {
                            positions.Add(POSITION_LONG + " " + numberStr);
                            positionsPossessive.Add(POSITION_LONG + " " + numberStr + POSSESSIVE);
                            whosInPositionChoices.Add(POSITION_LONG + " " + numberStr);
                        }
                    }
                }
                opponentGrammarList.AddRange(addCompoundChoices(new String[] { WHOS_IN }, false, whosInPositionChoices, null, true));
                loadNameAndNamePossessiveChoices(positions, positionsPossessive);
            }
        }

        private Tuple<HashSet<string>, HashSet<string>> getDriverChoices(string usableNameForSRE, string carNumberString)
        {
            HashSet<string> nameChoices = new HashSet<string>();
            HashSet<string> namePossessiveChoices = new HashSet<string>();

            if (usableNameForSRE != null && usableNameForSRE.Length > 0 && !driverNamesInUse.Contains(usableNameForSRE))
            {
                driverNamesInUse.Add(usableNameForSRE);
                nameChoices.Add(usableNameForSRE);
                namePossessiveChoices.Add(usableNameForSRE + POSSESSIVE);
            }
            if (carNumberString != "-1" && carNumberToNumber.ContainsValue(carNumberString))
            {
                if (!carNumbersInUse.Contains(carNumberString))
                {
                    carNumbersInUse.Add(carNumberString);
                    String[] numberOptions = carNumberToNumber.FirstOrDefault(x => x.Value == carNumberString).Key;
                    foreach (String number in numberOptions)
                    {
                        nameChoices.Add(CAR_NUMBER + " " + number);
                        namePossessiveChoices.Add(CAR_NUMBER + " " + number + POSSESSIVE);
                    }
                }
                // if the car number has a 0 or 00 in front of it, also listen for the number without the leading zero(s)
                if (carNumberString.StartsWith("0"))
                {
                    int parsed;
                    if (int.TryParse(carNumberString, out parsed))
                    {
                        String carNumberStringAlternate = parsed.ToString();
                        if (!carNumbersInUse.Contains(carNumberStringAlternate))
                        {
                            carNumbersInUse.Add(carNumberStringAlternate);
                            String[] numberOptionsWithoutLeadingZeros = carNumberToNumber.FirstOrDefault(x => x.Value == parsed.ToString()).Key;
                            foreach (String number in numberOptionsWithoutLeadingZeros)
                            {
                                nameChoices.Add(CAR_NUMBER + " " + number);
                                namePossessiveChoices.Add(CAR_NUMBER + " " + number + POSSESSIVE);
                            }
                        }
                    }
                }
            }
            return new Tuple<HashSet<string>, HashSet<string>>(nameChoices, namePossessiveChoices);
        }

        private void loadNameAndNamePossessiveChoices(HashSet<string> nameChoices, HashSet<string> namePossessiveChoices,
            ChoicesWrapper opponentNameChoices = null, ChoicesWrapper opponentNamePossessiveChoices = null)
        {
            if (opponentNameChoices == null)
            {
                opponentNameChoices = SREWrapperFactory.createNewChoicesWrapper();
            }
            if (opponentNamePossessiveChoices == null)
            {
                opponentNamePossessiveChoices = SREWrapperFactory.createNewChoicesWrapper();
            }
            if (nameChoices.Count() > 0)
            {
                foreach (string nameChoice in nameChoices)
                {
                    opponentNameChoices.Add(nameChoice);
                }
                String[] enabledOpponentChoices = UserSettings.GetUserSettings().getBoolean("enable_watch_car_command") ?
                    new String[] { WHERE_IS, WHERES, WATCH, TEAM_MATE, RIVAL, STOP_WATCHING } : new String[] { WHERE_IS, WHERES };
                opponentGrammarList.AddRange(addCompoundChoices(enabledOpponentChoices, false, opponentNameChoices, null, true));

                opponentGrammarList.AddRange(addCompoundChoices(new String[] { WHAT_TYRE_IS, WHAT_TYRES_IS }, false, opponentNameChoices, new String[] { ON }, true));

                if (Game.RACE_ROOM || Game.IRACING)
                {
                    opponentGrammarList.AddRange(addCompoundChoices(HOW_GOOD_IS, false, opponentNameChoices, null, true));
                }
            }
            if (namePossessiveChoices.Count() > 0)
            {
                foreach (string namePossessiveChoice in namePossessiveChoices)
                {
                    opponentNamePossessiveChoices.Add(namePossessiveChoice);
                }
                opponentGrammarList.AddRange(addCompoundChoices(new String[] { WHATS }, true, opponentNamePossessiveChoices, getWhatsPossessiveChoices(), true));
            }
        }

        public void addOverlayGrammar()
        {
            if (!initialised)
            {
                return;
            }
            overlayGrammarList.Clear();
            if (UserSettings.GetUserSettings().getBoolean("enable_overlay_window"))
            {

                ChoicesWrapper overlayChoices = SREWrapperFactory.createNewChoicesWrapper();
                List<string> chartCommands = OverlayDataSource.getAllChartVoiceCommands();
                if (chartCommands.Count > 0)
                {
                    foreach (string chartCommand in chartCommands)
                    {
                        validateAndAdd(new string[] { chartCommand }, overlayChoices);
                    }
                }
                GrammarBuilderWrapper overlayGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper(overlayChoices);
                overlayGrammarBuilder.SetCulture(cultureInfo);
                GrammarWrapper overlayGrammar = SREWrapperFactory.createNewGrammarWrapper(overlayGrammarBuilder, "overlayGrammarBuilder");
                overlayGrammarList.Add(overlayGrammar);
                sreWrapper.LoadGrammar(overlayGrammar);
            }
        }

        private void addRallySpeechRecogniser()
        {
            // note that all the current rally voice commands are 'behaviour altering', so exit this method immediately if these are disable
            // so we're not adding empty grammars to the SRE
            if (!initialised || disableBehaviorAlteringVoiceCommands)
            {
                return;
            }
            try
            {
                // basic commands
                ChoicesWrapper basicChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                validateAndAdd(RALLY_EARLIER_CALLS, basicChoicesWrapper);
                validateAndAdd(RALLY_LATER_CALLS, basicChoicesWrapper);
                validateAndAdd(RALLY_CORNER_DECRIPTIONS, basicChoicesWrapper);
                validateAndAdd(RALLY_CORNER_DIRECTION_FIRST, basicChoicesWrapper);
                validateAndAdd(RALLY_CORNER_NUMBER_FIRST, basicChoicesWrapper);
                validateAndAdd(RALLY_START_RECE, basicChoicesWrapper);
                validateAndAdd(RALLY_FINISH_RECE, basicChoicesWrapper);
                validateAndAdd(RALLY_DISTANCE, basicChoicesWrapper);
                GrammarBuilderWrapper basicRallyGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper(basicChoicesWrapper);
                basicRallyGrammarBuilder.SetCulture(cultureInfo);
                GrammarWrapper basicRallyGrammar = SREWrapperFactory.createNewGrammarWrapper(basicRallyGrammarBuilder, "basicRallyGrammarBuilder");
                rallyGrammarList.Add(basicRallyGrammar);
                sreWrapper.LoadGrammar(basicRallyGrammar);


                // now the stage notes commands - these may use a defined grammar or free dictation
                if (UserSettings.GetUserSettings().getBoolean("use_dictation_grammar_for_rally") && SREWrapperFactory.useSystem)
                {
                    GrammarWrapper rallyDicationGrammar = SREWrapperFactory.CreateChatDictationGrammarWrapper();
                    SREWrapperFactory.LoadChatDictationGrammar(this.sreWrapper, rallyDicationGrammar, null, null);
                    rallyGrammarList.Add(rallyDicationGrammar);
                }
                else
                {
                    // for corrections
                    ChoicesWrapper correctionChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper(); // this will be added at the start with 0 or 1 repeats
                    validateAndAdd(RALLY_CORRECTION, correctionChoicesWrapper);
                    validateAndAdd(RALLY_INSERT, correctionChoicesWrapper);

                    // modifier commands. These are generally used to modify a corner call but can apply to other obstacles.
                    ChoicesWrapper stageNoteCommandChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                    validateAndAdd(RALLY_CUT, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_DONT_CUT, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_TIGHTENS, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_LONG, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_LONGLONG, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_NARROWS, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_TIGHTENS_BAD, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_WIDENS, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_WIDE_OUT, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_MAYBE, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_OPENS, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_PLUS, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_MINUS, stageNoteCommandChoicesWrapper);

                    // corner commands
                    validateAndAdd(RALLY_LEFT, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_RIGHT, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_1, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_2, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_3, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_4, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_5, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_6, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_SQUARE, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_HAIRPIN, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_OPEN_HAIRPIN, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_FLAT, stageNoteCommandChoicesWrapper);

                    // obstacle commands
                    ChoicesWrapper obstacleChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                    foreach (string[] command in SpeechRecogniser.RallyObstacleCommands)
                    {
                        validateAndAdd(command, stageNoteCommandChoicesWrapper);
                    }

                    // distance correction commands
                    validateAndAdd(RALLY_EARLIER, stageNoteCommandChoicesWrapper);
                    validateAndAdd(RALLY_LATER, stageNoteCommandChoicesWrapper);

                    // now assemble the choices
                    GrammarBuilderWrapper obstaclesAndCornersRallyGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper();
                    obstaclesAndCornersRallyGrammarBuilder.SetCulture(cultureInfo);
                    obstaclesAndCornersRallyGrammarBuilder.Append(correctionChoicesWrapper, 0, 1);          // separate grammar for optional 'correction' - maybe add 'insert' to this
                    obstaclesAndCornersRallyGrammarBuilder.Append(stageNoteCommandChoicesWrapper, 0, 8);    // between 0 and 8 matches for any word in any order

                    GrammarWrapper obstaclesAndCornersRallyGrammar = SREWrapperFactory.createNewGrammarWrapper(obstaclesAndCornersRallyGrammarBuilder, "obstaclesAndCornersRallyGrammarBuilder");
                    rallyGrammarList.Add(obstaclesAndCornersRallyGrammar);
                    sreWrapper.LoadGrammar(obstaclesAndCornersRallyGrammar);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to add rally commands to speech recognition engine");
                Log.Exception(ex);
            }
        }

        private void addR3ESpeechRecogniser()
        {
            if (!initialised)
            {
                return;
            }
            try
            {
                ChoicesWrapper r3eChoices = SREWrapperFactory.createNewChoicesWrapper();
                validateAndAdd(PIT_STOP_CHANGE_ALL_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_CHANGE_FRONT_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_CHANGE_REAR_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_CLEAR_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_FIX_FRONT_AERO, r3eChoices);
                validateAndAdd(PIT_STOP_FIX_BODY, r3eChoices);
                validateAndAdd(PIT_STOP_FIX_REAR_AERO, r3eChoices);
                validateAndAdd(PIT_STOP_FIX_ALL_AERO, r3eChoices);
                validateAndAdd(PIT_STOP_FIX_NO_AERO, r3eChoices);
                validateAndAdd(PIT_STOP_FIX_SUSPENSION, r3eChoices);
                validateAndAdd(PIT_STOP_DONT_FIX_SUSPENSION, r3eChoices);
                validateAndAdd(PIT_STOP_SERVE_PENALTY, r3eChoices);
                validateAndAdd(PIT_STOP_DONT_SERVE_PENALTY, r3eChoices);
                validateAndAdd(PIT_STOP_NEXT_TYRE_COMPOUND, r3eChoices);
                validateAndAdd(PIT_STOP_HARD_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_MEDIUM_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_SOFT_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_PRIME_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_ALTERNATE_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_OPTION_TYRES, r3eChoices);
                validateAndAdd(PIT_STOP_DONT_REFUEL, r3eChoices);
                validateAndAdd(PIT_STOP_REFUEL, r3eChoices);
                validateAndAdd(PIT_STOP_FIX_ALL, r3eChoices);
                validateAndAdd(PIT_STOP_FIX_NONE, r3eChoices);

                GrammarBuilderWrapper r3eGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper(r3eChoices);
                r3eGrammarBuilder.SetCulture(cultureInfo);
                GrammarWrapper r3eGrammar = SREWrapperFactory.createNewGrammarWrapper(r3eGrammarBuilder, "r3eGrammarBuilder");
                r3ePitstopGrammarList.Add(r3eGrammar);
                sreWrapper.LoadGrammar(r3eGrammar);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to add R3E pit stop commands to speech recognition engine");
                Log.Exception(ex);
            }
        }

        private void addACCPitManagerSpeechRecogniser()
        {
            if (!initialised)
            {
                return;
            }
            try
            {
                ChoicesWrapper accChoices = SREWrapperFactory.createNewChoicesWrapper();
                validateAndAdd(PIT_STOP_CHANGE_TYRES, accChoices);
                validateAndAdd(PIT_STOP_CLEAR_TYRES, accChoices);
                validateAndAdd(PIT_STOP_WET_TYRES, accChoices);
                validateAndAdd(PIT_STOP_DRY_TYRES, accChoices);
                validateAndAdd(PIT_STOP_DONT_REFUEL, accChoices);
                validateAndAdd(PIT_STOP_SELECT_LEAST_USED_TYRE_SET, accChoices);
                GrammarBuilderWrapper accGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper(accChoices);
                accGrammarBuilder.SetCulture(cultureInfo);
                GrammarWrapper accGrammar = SREWrapperFactory.createNewGrammarWrapper(accGrammarBuilder, "accGrammarBuilder");
                accPitstopGrammarList.Add(accGrammar);
                sreWrapper.LoadGrammar(accGrammar);

                // now complex grammar for pit stop tyre pressure changes
                ChoicesWrapper pressureIntroChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                ChoicesWrapper pressureIntAmountChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                ChoicesWrapper pressureFractionAmountChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                foreach (string intro in PIT_STOP_CHANGE_TYRE_PRESSURE)
                {
                    validateAndAdd(intro, pressureIntroChoicesWrapper);
                }
                foreach (string intro in PIT_STOP_CHANGE_FRONT_PRESSURES)
                {
                    validateAndAdd(intro, pressureIntroChoicesWrapper);
                }
                foreach (string intro in PIT_STOP_CHANGE_REAR_PRESSURES)
                {
                    validateAndAdd(intro, pressureIntroChoicesWrapper);
                }
                foreach (string intro in PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE)
                {
                    validateAndAdd(intro, pressureIntroChoicesWrapper);
                }
                foreach (string intro in PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE)
                {
                    validateAndAdd(intro, pressureIntroChoicesWrapper);
                }
                foreach (string intro in PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE)
                {
                    validateAndAdd(intro, pressureIntroChoicesWrapper);
                }
                foreach (string intro in PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE)
                {
                    validateAndAdd(intro, pressureIntroChoicesWrapper);
                }
                foreach (KeyValuePair<String[], int> numberEntry in numberToNumber)
                {
                    // 20.4 - 35 is the valid range here
                    bool lowerLimit = false;
                    bool upperLimit = false;
                    if (numberEntry.Value >= 20 && numberEntry.Value <= 35)
                    {
                        validateAndAdd(numberEntry.Key[0], pressureIntAmountChoicesWrapper);
                        lowerLimit = numberEntry.Value == 20;
                        upperLimit = numberEntry.Value == 35;
                    }
                    else if (numberEntry.Value >= 0 && numberEntry.Value <= 9 && !upperLimit)
                    {
                        if (!lowerLimit || numberEntry.Value >= 4)
                        {
                            validateAndAdd(POINT[0] + " " + numberEntry.Key[0], pressureFractionAmountChoicesWrapper);
                        }
                    }
                }
                //  note that 24.0 should be "twenty four", there's no "point zero" in the grammar
                // now assemble the choices
                GrammarBuilderWrapper pressureChangeGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper();
                pressureChangeGrammarBuilder.SetCulture(cultureInfo);
                pressureChangeGrammarBuilder.Append(pressureIntroChoicesWrapper, 1, 1);
                pressureChangeGrammarBuilder.Append(pressureIntAmountChoicesWrapper, 1, 1);
                pressureChangeGrammarBuilder.Append(pressureFractionAmountChoicesWrapper, 0, 1);    // optional fractional part
                GrammarWrapper pressureChangeGrammar = SREWrapperFactory.createNewGrammarWrapper(pressureChangeGrammarBuilder, "pressureChangeGrammarBuilder");
                accPitstopGrammarList.Add(pressureChangeGrammar);
                sreWrapper.LoadGrammar(pressureChangeGrammar);

                // now complex grammar for tyre set changes
                ChoicesWrapper tyreSetIntroChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                ChoicesWrapper tyreSetIntAmountChoicesWrapper = SREWrapperFactory.createNewChoicesWrapper();
                foreach (string intro in PIT_STOP_SELECT_TYRE_SET)
                {
                    validateAndAdd(intro, tyreSetIntroChoicesWrapper);
                }
                foreach (KeyValuePair<String[], int> numberEntry in numberToNumber)
                {
                    // 1-50 is the valid range here
                    if (numberEntry.Value >= 1 && numberEntry.Value <= 50)
                    {
                        validateAndAdd(numberEntry.Key[0], tyreSetIntAmountChoicesWrapper);
                    }
                }
                // now assemble the choices
                GrammarBuilderWrapper tyreSetGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper();
                tyreSetGrammarBuilder.SetCulture(cultureInfo);
                tyreSetGrammarBuilder.Append(tyreSetIntroChoicesWrapper, 1, 1);
                tyreSetGrammarBuilder.Append(tyreSetIntAmountChoicesWrapper, 1, 1);
                GrammarWrapper tyreSetGrammar = SREWrapperFactory.createNewGrammarWrapper(tyreSetGrammarBuilder, "pressureChangeGrammarBuilder");
                accPitstopGrammarList.Add(tyreSetGrammar);
                sreWrapper.LoadGrammar(tyreSetGrammar);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to add ACC pit stop commands to speech recognition engine");
                Log.Exception(ex);
            }
        }

        private void addiRacingSpeechRecogniser()
        {
            if (!initialised)
            {
                return;
            }
            try
            {
                ChoicesWrapper iRacingChoices = SREWrapperFactory.createNewChoicesWrapper();
                if (enable_iracing_pit_stop_commands)
                {
                    iracingPitstopGrammarList.AddRange(tyrePressureCommands());
                    if (!digitsChoices.IsEmpty())
                    {
                        iracingPitstopGrammarList.AddRange(fuelCommands(false));
                    }

                    validateAndAdd(PIT_STOP_TEAROFF, iRacingChoices);
                    validateAndAdd(PIT_STOP_FAST_REPAIR, iRacingChoices);
                    validateAndAdd(PIT_STOP_CLEAR_ALL, iRacingChoices);
                    validateAndAdd(PIT_STOP_CLEAR_TYRES, iRacingChoices);
                    validateAndAdd(PIT_STOP_CLEAR_WIND_SCREEN, iRacingChoices);
                    validateAndAdd(PIT_STOP_CLEAR_FAST_REPAIR, iRacingChoices);
                    validateAndAdd(PIT_STOP_CLEAR_FUEL, iRacingChoices);
                    validateAndAdd(PIT_STOP_CHANGE_ALL_TYRES, iRacingChoices);
                    validateAndAdd(PIT_STOP_CHANGE_FRONT_LEFT_TYRE, iRacingChoices);
                    validateAndAdd(PIT_STOP_CHANGE_FRONT_RIGHT_TYRE, iRacingChoices);
                    validateAndAdd(PIT_STOP_CHANGE_REAR_LEFT_TYRE, iRacingChoices);
                    validateAndAdd(PIT_STOP_CHANGE_REAR_RIGHT_TYRE, iRacingChoices);
                    validateAndAdd(PIT_STOP_CHANGE_LEFT_SIDE_TYRES, iRacingChoices);
                    validateAndAdd(PIT_STOP_CHANGE_RIGHT_SIDE_TYRES, iRacingChoices);
                    validateAndAdd(PIT_STOP_CHANGE_FRONT_TYRES, iRacingChoices);
                    validateAndAdd(PIT_STOP_CHANGE_REAR_TYRES, iRacingChoices);
                    validateAndAdd(PIT_STOP_FUEL_TO_THE_END, iRacingChoices);
                }

                GrammarBuilderWrapper iRacingGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper(iRacingChoices);
                iRacingGrammarBuilder.SetCulture(cultureInfo);
                GrammarWrapper iRacingGrammar = SREWrapperFactory.createNewGrammarWrapper(iRacingGrammarBuilder, "iRacingGrammarBuilder");
                iracingPitstopGrammarList.Add(iRacingGrammar);
                sreWrapper.LoadGrammar(iRacingGrammar);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to add iRacing pit stop commands to speech recognition engine");
                Log.Exception(ex);
            }
        }

        private void addRatingsCommands()
        {
            if (!initialised)
            {
                return;
            }
            try
            {
                ChoicesWrapper RatingsChoices = SREWrapperFactory.createNewChoicesWrapper();
                validateAndAdd(HOW_MANY_INCIDENT_POINTS, RatingsChoices);
                validateAndAdd(WHATS_THE_INCIDENT_LIMIT, RatingsChoices);
                validateAndAdd(WHATS_THE_SOF, RatingsChoices);
                if (Game.IRACING)
                {
                    validateAndAdd(WHATS_MY_IRATING, RatingsChoices);
                    validateAndAdd(WHATS_MY_LICENSE_CLASS, RatingsChoices);
                }

                GrammarBuilderWrapper RatingsGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper(RatingsChoices);
                RatingsGrammarBuilder.SetCulture(cultureInfo);
                GrammarWrapper RatingsGrammar = SREWrapperFactory.createNewGrammarWrapper(RatingsGrammarBuilder, "RatingsGrammarBuilder");
                ratingsGrammarList.Add(RatingsGrammar);
                sreWrapper.LoadGrammar(RatingsGrammar);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to add Ratings commands to speech recognition engine");
                Log.Exception(ex);
            }
        }
        private List<GrammarWrapper> tyrePressureCommands()
        {
            List<GrammarWrapper> result = new List<GrammarWrapper>();
            List<string> tyrePressureChangePhrases = new List<string>();
            if (disable_alternative_voice_commands)
            {
                tyrePressureChangePhrases.Add(PIT_STOP_CHANGE_TYRE_PRESSURE[0]);
                tyrePressureChangePhrases.Add(PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE[0]);
                tyrePressureChangePhrases.Add(PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE[0]);
                tyrePressureChangePhrases.Add(PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE[0]);
                tyrePressureChangePhrases.Add(PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE[0]);
            }
            else
            {
                tyrePressureChangePhrases.AddRange(PIT_STOP_CHANGE_TYRE_PRESSURE);
                tyrePressureChangePhrases.AddRange(PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE);
                tyrePressureChangePhrases.AddRange(PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE);
                tyrePressureChangePhrases.AddRange(PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE);
                tyrePressureChangePhrases.AddRange(PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE);
            }

            if (!digitsChoices.IsEmpty())
            {
                result = addCompoundChoices(tyrePressureChangePhrases.ToArray(), true, this.digitsChoices, null, true);
            }
            return result;
        }

        private List<GrammarWrapper> fuelCommands(bool fillToAsUsedByRf2LMU = false)
        {
            List<GrammarWrapper> result = new List<GrammarWrapper>();
            List<string> litresAndGallons = new List<string>();
            litresAndGallons.AddRange(LITERS);
            litresAndGallons.AddRange(GALLONS);
            result.AddRange(addCompoundChoices(PIT_STOP_ADD, false, this.digitsChoices, litresAndGallons.ToArray(), true));
            // add the fuel choices with no unit - these use the default / reported unit for fuel
            result.AddRange(addCompoundChoices(PIT_STOP_ADD, false, this.digitsChoices, null, true));
            if (fillToAsUsedByRf2LMU)
            {
                result.AddRange(addCompoundChoices(PIT_STOP_FILL_TO, false, this.digitsChoices, litresAndGallons.ToArray(), true));
                // add the fuel choices with no unit - these use the default / reported unit for fuel
                result.AddRange(addCompoundChoices(PIT_STOP_FILL_TO, false, this.digitsChoices, null, true));
            }
            return result;
        }

        private void addPitManagerSpeechRecogniser(string pmEnableProperty)
        {
            if (!initialised || !UserSettings.GetUserSettings().getBoolean(pmEnableProperty))
            {
                return;
            }
            try
            {
                ChoicesWrapper pitManagerChoices = SREWrapperFactory.createNewChoicesWrapper();
                pitManagerGrammarList.AddRange(tyrePressureCommands());
                if (!digitsChoices.IsEmpty())
                {
                    pitManagerGrammarList.AddRange(fuelCommands(true));
                    if (Game.LMU)
                    {
                        pitManagerGrammarList.AddRange(addCompoundChoices(PIT_STOP_FUEL_RATIO, false, this.digitsChoices, null, true));
                        pitManagerGrammarList.AddRange(addCompoundChoices(PIT_STOP_VIRTUAL_ENERGY, false, this.digitsChoices, null, true));
                    }
                }
                var cmds = PitManager.PitManager.GetPitManagerCommands();
                if (cmds.Count == 0)
                {   // Thread race condition when CC is run first time after Windows reboot.
                    // Make sure crewChiefThread starts ahead of loadSREGrammarThread
                    // so PitManager() constructor has set up PM_event_dict
                    Thread.Sleep(500); // 100 mS was enough but...
                    cmds = PitManager.PitManager.GetPitManagerCommands();
                    Log.Commentary($"PitManager race condition work round - GetPitManagerCommands: {cmds.Count}");
                }
                foreach (var cmd in cmds)
                {
                    validateAndAdd(cmd, pitManagerChoices);
                }
                GrammarBuilderWrapper PitManagerGrammarBuilder = SREWrapperFactory.createNewGrammarBuilderWrapper(pitManagerChoices);
                PitManagerGrammarBuilder.SetCulture(cultureInfo);
                GrammarWrapper PitManagerGrammar = SREWrapperFactory.createNewGrammarWrapper(PitManagerGrammarBuilder, "PitManagerGrammarBuilder");
                pitManagerGrammarList.Add(PitManagerGrammar);
                sreWrapper.LoadGrammar(PitManagerGrammar);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to add Pit Manager pit stop commands to speech recognition engine");
                Log.Exception(ex);
            }
        }

        public static Boolean ResultContains(String result, String[] alternatives, Boolean logMatch = false)
        {
            result = result.ToLower();
            foreach (String alternative in alternatives)
            {
                if (result == alternative.ToLower())
                {
                    if (logMatch)
                    {
                        Console.WriteLine("Matching entire response: \"" + alternative + "\"");
                    }
                    return true;
                }
            }
            // no result with == so try contains
            foreach (String alternative in alternatives)
            {
                String alternativeLower = alternative.ToLower();
                if (result.Contains(alternativeLower))
                {
                    if (logMatch)
                    {
                        Console.WriteLine("matching partial response " + alternativeLower);
                    }
                    return true;
                }
            }
            return false;
        }

        public static Boolean ResultStartsWith(String result, String[] alternatives, Boolean logMatch = true)
        {
            result = result.ToLower().Trim();
            foreach (String alternative in alternatives)
            {
                String alternativeLower = alternative.ToLower();
                if (result.StartsWith(alternativeLower))
                {
                    if (logMatch)
                    {
                        Console.WriteLine("matching partial response " + alternativeLower);
                    }
                    return true;
                }
            }
            return false;
        }

        public static Tuple<string, int> GetResultMatchWithStartIndex(String result, String[] alternatives, Boolean logMatch = true)
        {
            result = result.ToLower();
            foreach (String alternative in alternatives)
            {
                if (result == alternative.ToLower())
                {
                    if (logMatch)
                    {
                        Console.WriteLine("Matching entire response: \"" + alternative + "\"");
                    }
                    return new Tuple<string, int> (result, 0);
                }
            }
            // no result with == so try contains
            foreach (String alternative in alternatives)
            {
                String alternativeLower = alternative.ToLower();
                if (result.Contains(alternativeLower))
                {
                    if (logMatch)
                    {
                        Console.WriteLine("matching partial response " + alternativeLower);
                    }
                    return new Tuple<string, int>(alternativeLower, result.IndexOf(alternativeLower));
                }
            }
            return new Tuple<string, int>("", -1);
        }

        private Boolean switchFromRegularToTriggerRecogniser()
        {
            if (!initialised)
            {
                return false;
            }

            int attempts = 0;
            Boolean success = false;
            while (!success && attempts < 3)
            {
                attempts++;
                try
                {
                    // the cancel call takes some time to complete but returns immediately, so wait a bit before switching inputs
                    sreWrapper.RecognizeAsyncCancel();
                    Thread.Sleep(5);
                    sreWrapper.SetInputToNull();
                    triggerSreWrapper.SetInputToDefaultAudioDevice();
                    triggerSreWrapper.RecognizeAsync();
                    waitingForSpeech = false;
                    success = true;
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }
            if (success)
            {
                if (attempts > 1)
                {
                    Console.WriteLine("Took " + attempts + " attempts to switch from regular to trigger SRE");
                }
            }
            else
            {
                Console.WriteLine("Failed to switch SRE after " + attempts + " attempts");
            }
            return success;
        }

        private Boolean switchFromTriggerToRegularRecogniser()
        {
            if (!initialised)
            {
                return false;
            }

            int attempts = 0;
            Boolean success = false;
            while (!success && attempts < 3)
            {
                attempts++;
                try
                {
                    // the cancel call takes some time to complete but returns immediately, so wait a bit before switching inputs
                    triggerSreWrapper.RecognizeAsyncCancel();
                    Thread.Sleep(5);
                    triggerSreWrapper.SetInputToNull();
                    sreWrapper.SetInputToDefaultAudioDevice();
                    success = true;
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }
            if (success)
            {
                if (attempts > 1)
                {
                    Console.WriteLine("Took " + attempts + " attempts to switch from trigger to regular SRE");
                }
                recognizeAsync();
                // if we reject messages while we're talking to the chief, attempt to interrupt any sound currently playing
                if (PlaybackModerator.rejectMessagesWhenTalking)
                {
                    SoundCache.InterruptCurrentlyPlayingSound(true);
                }
                ThreadStart startListingBeep = crewChief.audioPlayer.playStartListeningBeep;
                Thread startListingBeepThread = new Thread(startListingBeep);

                startListingBeepThread.Name = "SpeechRecogniser.audioPlayer.playStartListeningBeep";
                ThreadManager.RegisterRootThread(startListingBeepThread);

                startListingBeepThread.Start();
                SpeechRecogniser.distanceWhenVoiceCommandStarted = CrewChief.currentGameState == null ? 0 : CrewChief.currentGameState.PositionAndMotionData.DistanceRoundTrack;
            }
            else
            {
                Console.WriteLine("Failed to switch SRE after " + attempts + " attempts");
            }
            return success;
        }

        private void restartWaitTimeoutThread(int timeout)
        {
            triggerTimeoutWaitHandle.Set();
            ThreadManager.UnregisterTemporaryThread(restartWaitTimeoutThreadReference);
            restartWaitTimeoutThreadReference = new Thread(() =>
            {
                triggerTimeoutWaitHandle.Reset();
                Thread.CurrentThread.IsBackground = true;
                Boolean signalled = triggerTimeoutWaitHandle.WaitOne(timeout);
                if (signalled)
                {
                    // thread was stopped so we got some speech or asked the thread to shut down
                    // (no point in logging anything here)
                }
                else
                {
                    // timeout waiting
                    if (waitingForSpeech)
                    {
                        // no result
                        Console.WriteLine("Gave up waiting for voice command, now waiting for trigger word " + keyWord);
                        crewChief.audioPlayer.playListeningEndBeep();
                        switchFromRegularToTriggerRecogniser();
                    }
                }
            });
            restartWaitTimeoutThreadReference.Name = "SpeachRecognizer.restartWaitTimeoutThreadReference";
            ThreadManager.RegisterTemporaryThread(restartWaitTimeoutThreadReference);
            restartWaitTimeoutThreadReference.Start();
        }

        void trigger_SpeechRecognizedSystem(object sender, System.Speech.Recognition.SpeechRecognizedEventArgs e)
        {
            trigger_SpeechRecognized(sender, e);
        }

        void trigger_SpeechRecognizedMicrosoft(object sender, Microsoft.Speech.Recognition.SpeechRecognizedEventArgs e)
        {
            trigger_SpeechRecognized(sender, e);
        }

        void trigger_SpeechRecognized(object sender, object e)
        {
            if (!initialised)
            {
                return;
            }
            float recognitionConfidence = SREWrapperFactory.GetCallbackConfidence(e);
            SREThresholdInfo thresholdInfo = this.thresholds[ThresholdType.TRIGGER];
            if (thresholdInfo.checkConfidence(recognitionConfidence, keyWord))
            {
                Console.WriteLine(this.recogniserName + " heard keyword \"" + keyWord + "\", waiting for command, confidence " + recognitionConfidence.ToString("0.000"));
                switchFromTriggerToRegularRecogniser();
                restartWaitTimeoutThread(trigger_word_listen_timeout);
            }
            else
            {
                Console.WriteLine(this.recogniserName + " heard keyword \"" + keyWord +
                    "\" but confidence " + recognitionConfidence.ToString("0.000") +
                    " is below the minimum threshold of " + thresholdInfo.getCurrentThreshold() + " set in property \"" + thresholdInfo.thresholdPropertyName + "\"");
            }
        }

        void sre_SpeechRecognizedMicrosoft(object sender, Microsoft.Speech.Recognition.SpeechRecognizedEventArgs e)
        {
            sre_SpeechRecognized(sender, e);
        }

        void sre_SpeechRecognizedSystem(object sender, System.Speech.Recognition.SpeechRecognizedEventArgs e)
        {
            sre_SpeechRecognized(sender, e);
        }

        void sre_SpeechRecognitionCompleteMicrosoft(object sender, Microsoft.Speech.Recognition.RecognizeCompletedEventArgs e)
        {
            saveAudio(sender, e);
        }

        void sre_SpeechRecognitionCompleteSystem(object sender, System.Speech.Recognition.RecognizeCompletedEventArgs e)
        {
            saveAudio(sender, e);
        }

        void sre_SpeechRecognitionRejectedMicrosoft(object sender, Microsoft.Speech.Recognition.SpeechRecognitionRejectedEventArgs e)
        {
            saveAudio(sender, e, false);
        }

        void sre_SpeechRecognitionRejectedSystem(object sender, System.Speech.Recognition.SpeechRecognitionRejectedEventArgs e)
        {
            saveAudio(sender, e, false);
        }

        public void TracePlayback(string text)
        {
            sreWrapper.EmulateRecognize(text);
        }
        void sre_SpeechRecognized(object sender, object e)
        {
            if (!initialised)
            {
                return;
            }
            SpeechRecogniser.hasMadeVoiceCommandSinceStarting = true;
            // cancel the thread that's waiting for a speech recognised timeout:
            triggerTimeoutWaitHandle.Set();
            SpeechRecogniser.waitingForSpeech = false;
            SpeechRecogniser.gotRecognitionResult = true;
            PlaybackModerator.holdModeTalkingToChief = false;
            Boolean youWot = false;
            String recognisedText = SREWrapperFactory.GetCallbackText(e);
            String[] recognisedWords = SREWrapperFactory.GetCallbackWordsList(e);
            float recognitionConfidence = SREWrapperFactory.GetCallbackConfidence(e);
            object recognitionGrammar = SREWrapperFactory.GetCallbackGrammar(e);
            Tracepoints.SpeechRecogniser.speechRecognised(recognisedText);
            UpdateWizardRecognitionStatus(recognisedText, recognitionConfidence);
            Console.WriteLine(this.recogniserName + " recognised : \"" + recognisedText + "\", Confidence = " + recognitionConfidence.ToString("0.000"));
            if (CrewChief.SpeechTrace != null)
            {
                CrewChief.SpeechTrace.Add(recognisedText);
            }

            bool useDictationGrammarForRally = false;   // this really doesn't work well. Perhaps it'll be reinstated at some point
            float confidenceRallyDictationThreshold = 0.3f;

            try
            {
                // special case when we're waiting for a message after a heavy crash:
                if (DamageReporting.waitingForDriverIsOKResponse)
                {
                    DamageReporting damageReportingEvent = (DamageReporting)CrewChief.getEvent("DamageReporting");
                    if (thresholds[ThresholdType.STANDARD].checkConfidence(recognitionConfidence, recognisedText) && ResultContains(recognisedText, I_AM_OK, false))
                    {
                        damageReportingEvent.cancelWaitingForDriverIsOK(DamageReporting.DriverOKResponseType.CLEARLY_OK);
                    }
                    else
                    {
                        damageReportingEvent.cancelWaitingForDriverIsOK(DamageReporting.DriverOKResponseType.NOT_UNDERSTOOD);
                    }
                }
                else
                {
                    bool chatSent = false;
                    if (useFreeDictationForChatMessages && this.chatDictationGrammar != null &&
                        recognitionGrammar == this.chatDictationGrammar.GetInternalGrammar())
                    {
                        Console.WriteLine("chat recognised: \"" + recognisedText + "\"");

                        if (recognisedText.StartsWith(chatContextStart))
                        {
                            string chatText = TidyChatText(recognisedText, chatContextStart, driverNamesInUse);
                            Chat.SendChatText(chatText);
                            chatSent = true;
                        }
                    }

                    if (!chatSent)
                    {
                        if (CrewChief.gameDefinition.racingType == CrewChief.RacingType.Rally &&
                            GrammarWrapperListContains(rallyGrammarList, recognitionGrammar))
                        {
                            SREThresholdInfo thresholdInfo = thresholds[ThresholdType.RALLY];
                            if (thresholdInfo.checkConfidence(recognitionConfidence, recognisedText))
                            {
                                this.lastRecognisedText = recognisedText;
                                CrewChief.HandleEvent("CoDriver", recognisedText);
                            }
                            else
                            {
                                Console.WriteLine("Confidence " + recognitionConfidence.ToString("0.000") +
                                                  " is below the minimum threshold of " +
                                                  thresholdInfo.getCurrentThreshold() + " set in property \"" +
                                                  thresholdInfo.thresholdPropertyName + "\"");
                                crewChief.youWot(true);
                                youWot = true;
                            }
                        }
                        else if (GrammarWrapperListContains(opponentGrammarList, recognitionGrammar))
                        {
                            SREThresholdInfo thresholdInfo = thresholds[ThresholdType.NAMES];
                            if (thresholdInfo.checkConfidence(recognitionConfidence, recognisedText))
                            {
                                this.lastRecognisedText = recognisedText;
                                if (recognisedText.StartsWith(WATCH) || recognisedText.StartsWith(RIVAL) ||
                                    recognisedText.StartsWith(TEAM_MATE) || recognisedText.StartsWith(STOP_WATCHING))
                                {
                                    CrewChief.HandleEvent("WatchedOpponents", recognisedText);
                                }
                                else
                                {
                                    CrewChief.HandleEvent("Opponents", recognisedText);
                                }
                            }
                            else
                            {
                                Console.WriteLine("Confidence " + recognitionConfidence.ToString("0.000") +
                                                  " is below the minimum threshold of " +
                                                  thresholdInfo.getCurrentThreshold() + " set in property \"" +
                                                  thresholdInfo.thresholdPropertyName + "\"");
                                crewChief.youWot(true);
                                youWot = true;
                            }
                        }
                        else if (thresholds[ThresholdType.STANDARD]
                                 .checkConfidence(recognitionConfidence, recognisedText))
                        {
                            if (macroGrammar != null && macroGrammar.GetInternalGrammar() == recognitionGrammar &&
                                macroLookup.ContainsKey(recognisedText))
                            {
                                this.lastRecognisedText = recognisedText;
                                macroLookup[recognisedText].execute(recognisedText, false, true);
                            }
                            else if (GrammarWrapperListContains(iracingPitstopGrammarList, recognitionGrammar))
                            {
                                this.lastRecognisedText = recognisedText;
                                CrewChief.HandleEvent("IRacingBroadcastMessageEvent", recognisedText);
                            }
                            else if (GrammarWrapperListContains(r3ePitstopGrammarList, recognitionGrammar))
                            {
                                this.lastRecognisedText = recognisedText;
                                R3EPitMenuManager.processVoiceCommand(recognisedText, crewChief.audioPlayer);
                            }
                            else if (GrammarWrapperListContains(ratingsGrammarList, recognitionGrammar))
                            {
                                this.lastRecognisedText = recognisedText;
                                CrewChief.HandleEvent("Ratings", recognisedText);
                            }
                            else if (GrammarWrapperListContains(accPitstopGrammarList, recognitionGrammar))
                            {
                                this.lastRecognisedText = recognisedText;
                                ACCPitMenuManager.processVoiceCommand(recognisedText, crewChief.audioPlayer);
                            }
                            else if (GrammarWrapperListContains(pitManagerGrammarList, recognitionGrammar))
                            {
                                this.lastRecognisedText = recognisedText;
                                try
                                {
                                    CrewChief.HandleEvent("PitManagerVoiceCmds", recognisedText);
                                }
                                catch
                                {
                                    if (CrewChief.Debug.RunningUnderDebugger)
                                    {
                                        Console.WriteLine("Pit Manager not included");
                                    }
                                }
                            }
                            else if (GrammarWrapperListContains(overlayGrammarList, recognitionGrammar))
                            {
                                this.lastRecognisedText = recognisedText;
                                CrewChief.HandleEvent("OverlayController", recognisedText);
                            }
                            else if (ResultContains(recognisedText, REPEAT_LAST_MESSAGE, false))
                            {
                                // in rally mode, repeat-last-message needs to replay all the last command batch so send this to the CoDriver event
                                if (CrewChief.gameDefinition.racingType == CrewChief.RacingType.Rally)
                                {
                                    CrewChief.HandleEvent("CoDriver", recognisedText);
                                }
                                else
                                {
                                    crewChief.audioPlayer.repeatLastMessage();
                                }
                            }
                            else if (ResultContains(recognisedText, MORE_INFO, false) &&
                                     this.lastRecognisedText != null && !use_verbose_responses)
                            {
                                var eventResults = getEventsForSpeech(this.lastRecognisedText);
                                if (eventResults != null)
                                {
                                    AbstractEvent abstractEvent = eventResults[0].abstractEvent;
                                    if (abstractEvent != null)
                                    {
                                        abstractEvent.respondMoreInformation(this.lastRecognisedText, true);
                                    }
                                }
                            }
                            else
                            {
                                this.lastRecognisedText = recognisedText;
                                var events = getEventsForSpeech(recognisedText);
                                if (events != null)
                                {
                                    foreach (EventResult eventResult in events)
                                    {
                                        if (eventResult.abstractEvent != null)
                                        {
                                            eventResult.abstractEvent.respond(recognisedText, eventResult.cmd);

                                            if (use_verbose_responses)
                                            {
                                                // In verbose mode, always respond with more info.
                                                eventResult.abstractEvent.respondMoreInformation(this.lastRecognisedText, false);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Log.Error($"Speech command {recognisedText} not handled");
                                }
                            }
                        }
                        else if (CrewChief.gameDefinition.racingType == CrewChief.RacingType.Rally
                                 && SREWrapperFactory.useSystem
                                 && useDictationGrammarForRally
                                 && recognitionConfidence > confidenceRallyDictationThreshold)
                        {
                            // note that cases where the confidence is high for a free dictation rally grammar match, we'll have already
                            // invoked the CoDriver Respond call - this check is for cases where confidence is below the 'proper' threshold
                            // but above the (lower) rally free dictation threshold
                            this.lastRecognisedText = recognisedText;
                            CrewChief.HandleEvent("CoDriver", recognisedText);
                        }
                        else
                        {
                            Console.WriteLine("Confidence " + recognitionConfidence.ToString("0.000") +
                                              " is below the minimum threshold of " +
                                              thresholds[ThresholdType.STANDARD].getCurrentThreshold() +
                                              " set in property \"" +
                                              thresholds[ThresholdType.STANDARD].thresholdPropertyName + "\"");
                            crewChief.youWot(true);
                            youWot = true;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine("Unable to respond - error message: " + exception.Message + " stack " + exception.StackTrace);
            }
            // 'stop' the recogniser if we're ALWAYS_ON (because we restart it below) or TOGGLE
            // (because the user might have forgotten to press the button to close the channel).
            // For HOLD mode, let the recogniser continue listening and executing commands (invoking this
            // callback again from another thread) until the button is released, which will call
            // RecogniseAsyncCancel
            if (voiceOptionEnum == MainWindow.VoiceOptionEnum.TOGGLE)
            {
                sreWrapper.RecognizeAsyncStop();
                Thread.Sleep(500);
                Console.WriteLine("Stopping speech recognition");
            }
            else if (voiceOptionEnum == MainWindow.VoiceOptionEnum.ALWAYS_ON)
            {
                if (!useNAudio)
                {
                    sreWrapper.RecognizeAsyncStop();
                    Thread.Sleep(500);
                    Console.WriteLine("Restarting speech recognition");
                    recognizeAsync();
                    waitingForSpeech = true;
                }
                else
                {
                    waitingForSpeech = true;
                }
            }
            else if (voiceOptionEnum == MainWindow.VoiceOptionEnum.TRIGGER_WORD)
            {
                if (!youWot)
                {
                    Console.WriteLine("Waiting for trigger word " + keyWord);
                    switchFromRegularToTriggerRecogniser();
                }
                else
                {
                    // wait a little longer here as the "I didn't catch that" takes a second or two to say
                    restartWaitTimeoutThread(trigger_word_listen_timeout + 2000);
                    waitingForSpeech = true;
                }
            }
            else
            {
                // in hold-button mode, we're now waiting-for-speech until we get another result or the button is released
                if (SpeechRecogniser.keepRecognisingInHoldMode)
                {
                    Console.WriteLine("Waiting for more speech");
                    waitingForSpeech = true;
                }
            }
        }

        /// <summary>
        /// Tidy up the recognised text:
        ///   remove the chat trigger word,
        ///   Capitalise the first letter,
        ///   prefix with [AutoChat],
        ///   Capitalise any opponent names
        /// </summary>
        /// <param name="recognisedText">recognised speech</param>
        /// <param name="chatContextStart">chat trigger word</param>
        /// <param name="opponentNames">list of other drivers</param>
        /// <returns>The tidied content with prefix</returns>
        internal static string TidyChatText(string recognisedText,
            string chatContextStart,
            HashSet<string> opponentNames = null)
        {
            string chatText = recognisedText.TrimStart(chatContextStart.ToCharArray()).Trim();
            var words = chatText.Split(' ');
            for (var word = 0; word < words.Length; word++)
            {
                foreach (var name in opponentNames)
                {
                    if (string.Equals(words[word], name.ToLower()))
                    {
                        words[word] = char.ToUpper(name[0]) + name.Substring(1); ;
                    }
                }
            }

            chatText = string.Join(" ", words);
            // Capitalise the first letter
            chatText = char.ToUpper(chatText[0]) + chatText.Substring(1);
            chatText = "[AutoChat]" + chatText;
            return chatText;
        }

        public void stopTriggerRecogniser()
        {
            if (!initialised)
            {
                return;
            }

            if (triggerSreWrapper != null)
            {
                triggerSreWrapper.RecognizeAsyncCancel();
            }
        }

        public void startContinuousListening()
        {
            if (!initialised)
            {
                return;
            }

            if (voiceOptionEnum == MainWindow.VoiceOptionEnum.TRIGGER_WORD)
            {
                try
                {
                    triggerSreWrapper.UnloadAllGrammars();
                    GrammarBuilderWrapper gb = SREWrapperFactory.createNewGrammarBuilderWrapper();
                    ChoicesWrapper c = SREWrapperFactory.createNewChoicesWrapper();
                    c.Add(keyWord);
                    gb.SetCulture(cultureInfo);
                    gb.Append(c);
                    triggerSreWrapper.LoadGrammar(SREWrapperFactory.createNewGrammarWrapper(gb, "TriggerWordGrammar"));
                    sreWrapper.SetInputToNull();
                    triggerSreWrapper.SetInputToDefaultAudioDevice();
                    triggerSreWrapper.RecognizeAsync();
                    Console.WriteLine("waiting for trigger word " + keyWord);
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }
            else
            {
                recognizeAsync();
            }
        }

        public void recognizeAsync()
        {
            if (!initialised)
            {
                return;
            }
            StartWizardListening();
            SpeechRecogniser.timeVoiceCommandStarted = CrewChief.currentGameState == null ? DateTime.UtcNow : CrewChief.currentGameState.Now;
            SpeechRecogniser.distanceWhenVoiceCommandStarted = CrewChief.currentGameState == null ? 0 : CrewChief.currentGameState.PositionAndMotionData.DistanceRoundTrack;
            SpeechRecogniser.sreSessionId++;
            Console.WriteLine("Opened channel - waiting for speech");
            SpeechRecogniser.waitingForSpeech = true;
            SpeechRecogniser.gotRecognitionResult = false;
            SpeechRecogniser.keepRecognisingInHoldMode = true;
            try
            {
                if (useNAudio)
                {
                    Console.WriteLine("Getting audio from nAudio input stream");
                    if (MainWindow.voiceOption == MainWindow.VoiceOptionEnum.HOLD)
                    {
                        try
                        {
                            waveIn.StartRecording();
                        }
                        catch (NAudio.MmException e)
                        {
                            Log.Fatal("Not getting audio from nAudio device - is the microphone connected?");
                        }
                        catch (Exception e)
                        {
                            Utilities.ReportException(e, "Exception in SpeechRecognitionEngine.RecognizeAsync.", false /*needReport*/);
                        }
                    }
                    else if (MainWindow.voiceOption == MainWindow.VoiceOptionEnum.ALWAYS_ON)
                    {
                        nAudioAlwaysOnkeepRecording = true;
                        Debug.Assert(nAudioAlwaysOnListenerThread == null, "nAudio AlwaysOn Listener Thread wasn't shut down correctly.");

                        // This thread is manually synchronized in recongizeAsyncCancel
                        nAudioAlwaysOnListenerThread = new Thread(() =>
                        {
                            try
                            {
                                waveIn.StartRecording();
                            }
                            catch (NAudio.MmException e)
                            {
                                Log.Fatal("Not getting audio from nAudio device - is the microphone connected?");
                            }
                            catch (Exception e)
                            { 
                                Utilities.ReportException(e, "Exception in SpeechRecognitionEngine.RecognizeAsync.", false /*needReport*/);
                            }
                            while (nAudioAlwaysOnkeepRecording
                                && crewChief.running)  // Exit as soon as we begin shutting down.
                            {
                                if (!Utilities.InterruptedSleep(5000 /*totalWaitMillis*/, 1000 /*waitWindowMillis*/, () => nAudioAlwaysOnkeepRecording && crewChief.running /*keepWaitingPredicate*/))
                                {
                                    break;
                                }
                                try
                                {
                                    sreWrapper.SetInputToAudioStream(buffer, nAudioWaveInSampleRate, nAudioWaveInSampleDepth, nAudioWaveInChannelCount); // otherwise input gets unset
                                }
                                catch (Exception e)
                                { // Maybe caused by game shutting down?
                                    Log.Verbose("'Cannot perform this operation while the recognizer is doing recognition'");
                                    //break;  perhaps??
                                }
                                try
                                {
                                    sreWrapper.RecognizeAsync(); // before this call
                                }
                                catch (Exception e)
                                {
                                    Utilities.ReportException(e, "Exception in SpeechRecognitionEngine.RecognizeAsync.", false /*needReport*/);
                                }
                            }
                            StopNAudioWaveIn();
                        });

                        nAudioAlwaysOnListenerThread.Name = "SpeechRecogniser.nAudioAlwaysOnListenerThread";
                        nAudioAlwaysOnListenerThread.Start();

                    }
                }
                else
                {
                    Console.WriteLine("Getting audio from default device");
                    try
                    {
                        sreWrapper.RecognizeAsync();
                    }
                    catch (System.InvalidOperationException e)
                    {
                        Log.Fatal("Not getting audio from default device - is the microphone connected?");
                    }
                    catch (Exception e)
                    {
                        Utilities.ReportException(e, "Exception in SpeechRecognitionEngine.RecognizeAsync.", false /*needReport*/);
                        throw e;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to start speech recognition");
                Log.Exception(ex);
            }
        }

        public void recognizeAsyncCancel(Boolean isShuttingDown = false)
        {
            StopWizardListening();
            if (!initialised)
            {
                return;
            }

            Console.WriteLine("Cancelling wait for speech");
            SpeechRecogniser.waitingForSpeech = false;
            if (useNAudio)
            {
                if (MainWindow.voiceOption == MainWindow.VoiceOptionEnum.HOLD)
                {
                    SpeechRecogniser.keepRecognisingInHoldMode = false;
                    StopNAudioWaveIn();
                    if (!isShuttingDown)
                    {
                        sreWrapper.SetInputToAudioStream(buffer, nAudioWaveInSampleRate, nAudioWaveInSampleDepth, nAudioWaveInChannelCount); // otherwise input gets unset
                        try
                        {
                            sreWrapper.RecognizeAsync(); // before this call
                        }
                        catch (Exception e)
                        {
                            Utilities.ReportException(e, "Exception in SpeechRecognitionEngine.RecognizeAsync.", false /*needReport*/);
                        }
                    }
                }
                else if (MainWindow.voiceOption == MainWindow.VoiceOptionEnum.ALWAYS_ON)
                {
                    nAudioAlwaysOnkeepRecording = false;
                    sreWrapper.RecognizeAsyncCancel();

                    // Wait for nAudioAlwaysOnListenerThread thread to exit.
                    if (nAudioAlwaysOnListenerThread != null)
                    {
                        if (nAudioAlwaysOnListenerThread.IsAlive)
                        {
                            Console.WriteLine("Waiting for nAudio Always On listener to stop...");
                            if (!nAudioAlwaysOnListenerThread.Join(5000))
                            {
                                var errMsg = "Warning: Timed out waiting for nAudio Always On listener to stop";
                                Console.WriteLine(errMsg);
                                Debug.WriteLine(errMsg);
                            }
                        }
                        nAudioAlwaysOnListenerThread = null;
                        Console.WriteLine("nAudio Always On listener stopped");
                    }
                }
            }
            else
            {
                SpeechRecogniser.keepRecognisingInHoldMode = false;
                sreWrapper.RecognizeAsyncCancel();
            }
        }

        private void StopNAudioWaveIn()
        {
            if (!initialised)
            {
                return;
            }

            int retries = 0;
            Boolean stopped = false;
            while (!stopped && retries < 3)
            {
                try
                {
                    waveIn.StopRecording();
                    stopped = true;
                }
                catch (Exception)
                {
                    Thread.Sleep(50);
                    retries++;
                }
            }
        }

        public void changeInputDevice(int dev)
        {
            if (!initialised)
            {
                return;
            }

            waveIn.DeviceNumber = dev;
        }

        private void waveIn_DataAvailable(object sender, NAudio.Wave.WaveInEventArgs e)
        {
            UpdateMicrophonePeak(e.Buffer, e.BytesRecorded);
            if (!initialised)
            {
                return;
            }

            lock (buffer)
            {
                buffer.Write(e.Buffer, (int)buffer.Position, e.BytesRecorded);
            }
        }

        public struct EventResult
        {
            public AbstractEvent abstractEvent;
            public SpeechCommands.ID cmd;
        }
        private List<EventResult> getEventsForSpeech(String recognisedSpeech)
        {
            List<EventResult> results = new List<EventResult>();
            if (!initialised)
            {
                return null;
            }
            if(SubtitleManager.enableSubtitles)
            {
                SubtitleManager.AddPhraseForSpeech(recognisedSpeech);
            }

            if (ResultContains(recognisedSpeech, DONT_SPOT, false))
            {
                crewChief.disableSpotter();
                return results;
            }
            else if (ResultContains(recognisedSpeech, SPOT, false))
            {
                crewChief.enableSpotter();
                return results;
            }
            else
            {
                return getEventsForAction(recognisedSpeech, CrewChief.eventsList);
            }
        }

        public static List<EventResult> getEventsForAction(String recognisedSpeech, Dictionary<String, AbstractEvent> _eventsList)
        {
            List<EventResult> results = new List<EventResult>();
            EventResult result;

            foreach (var eventHandler in _eventsList)
            {
                result.cmd = CrewChief.getEvent(eventHandler.Key, _eventsList).HandlesEvent(recognisedSpeech);
                if (result.cmd != SpeechCommands.ID.NO_COMMAND)
                {
                    result.abstractEvent = eventHandler.Value;
                    results.Add(result);
                }
            }
            return results;
        }

        private Boolean GrammarWrapperListContains(List<GrammarWrapper> grammarWrapperList, object grammar)
        {
            foreach (GrammarWrapper grammarWrapper in grammarWrapperList)
            {
                if (grammarWrapper.GetInternalGrammar() == grammar)
                {
                    return true;
                }
            }
            return false;
        }

        public static ExecutableCommandMacro getStartChatMacro()
        {
            if (SpeechRecogniser.startChatMacro == null)
            {
                MacroManager.macros.TryGetValue(SpeechRecogniser.startChatMacroName, out SpeechRecogniser.startChatMacro);
            }
            return SpeechRecogniser.startChatMacro;
        }

        public static ExecutableCommandMacro getEndChatMacro()
        {
            if (SpeechRecogniser.endChatMacro == null)
            {
                MacroManager.macros.TryGetValue(SpeechRecogniser.endChatMacroName, out SpeechRecogniser.endChatMacro);
            }
            return SpeechRecogniser.endChatMacro;
        }

        private LangCodes getLangCodes()
        {
            LangCodes langCodes = new LangCodes();
            String overrideCountry = null;
            if (localeCountryPropertySetting != null && localeCountryPropertySetting.Length == 2)
            {
                overrideCountry = localeCountryPropertySetting.ToUpper();
            }
            // for backwards compatibility
            Boolean useDefaultLocaleInsteadOfLanguage = sreConfigDefaultLocaleSetting != null && sreConfigDefaultLocaleSetting.Length > 0;

            Tuple<String, String> sreConfigLangAndCountry = parseLocalePropertyValue(useDefaultLocaleInsteadOfLanguage ? sreConfigDefaultLocaleSetting : sreConfigLanguageSetting);
            String sreConfigLang = sreConfigLangAndCountry.Item1;
            String sreConfigCountry = sreConfigLangAndCountry.Item2;

            langCodes.langToUse = sreConfigLang;
            langCodes.countryToUse = overrideCountry != null ? overrideCountry : sreConfigCountry;
            langCodes.langAndCountryToUse = langCodes.countryToUse != null ? langCodes.langToUse + "-" + langCodes.countryToUse : null;

            return langCodes;
        }

        class LangCodes
        {
            public string countryToUse;
            public string langToUse;
            public string langAndCountryToUse;
            public override string ToString()
            {
                return "countryToUse = \"" + countryToUse + "\", langToUse = \"" + langToUse + "\" langAndCountryToUse = \"" + langAndCountryToUse + "\"";
            }
        }

        class HistoricSRECommandInfo
        {
            public readonly float confidence;    // the confidence reported for this SRE operation
            public float threshold;     // the current threshold in force at the time this operation was evaluated. IMPORTANT: this
                                        // may be *greater* than the confidence but we still might have accepted the command
            public readonly string command;      // the command text as reported by the SRE
            public readonly DateTime dateTime;   // the dateTime this was evalulated
            public HistoricSRECommandInfo(float confidence, float threshold, string command, DateTime dateTime)
            {
                this.confidence = confidence;
                this.threshold = threshold;
                this.command = command;
                this.dateTime = dateTime;
            }
        }

        class SREThresholdInfo
        {
            private static readonly float secondsBetweenLoggedSREAttempts = 4f; // any SRE callbacks more frequent than this many seconds are ignored for auto-tuning
            private static readonly float maxSecondsBetweenRepeatedCommands = 10f;

            private float currentThreshold;
            private float initialThreshold;
            private readonly ThresholdType type;
            public readonly string thresholdPropertyName;
            private  bool initialCheckCompleted = false;

            private readonly LinkedList<HistoricSRECommandInfo> rejectedCommands = new LinkedList<HistoricSRECommandInfo>();
            private readonly LinkedList<HistoricSRECommandInfo> acceptedCommands = new LinkedList<HistoricSRECommandInfo>();

            private int acceptedCountSinceLastReview = 0;
            private int rejectedCountSinceLastReview = 0;

            public SREThresholdInfo(float initialThreshold, string thresholdPropertyName, ThresholdType type)
            {
                this.initialThreshold = initialThreshold;
                this.currentThreshold = initialThreshold;
                this.thresholdPropertyName = thresholdPropertyName;
                this.type = type;
            }

            public string getCurrentThreshold()
            {
                return this.currentThreshold.ToString("0.000");
            }

            public bool checkConfidence(float confidence, string recognisedText)
            {
                reviewThreshold();
                if (type != ThresholdType.TRIGGER && isRepeatOfLastRejectedCommand(recognisedText))
                {
                    // accept this command and, because it's a repeat of a previously rejected command, adjust the threshold.
                    float newThreshold;
                    if (confidence < currentThreshold)
                    {
                        // Both commands are below the threshold, adjust it such that the better of the two would have been recognised
                        newThreshold = Math.Max(confidence, this.rejectedCommands.Last.Value.confidence);
                    }
                    else
                    {
                        // this command has been recognised but the previous attempt failed. The previous attempt may have been a clear command
                        // and a near-miss, or it may have been mumbled incomprehensible horseshit, we have no way of knowing. So move the threshold
                        // such that the average of the two would have been recognised
                        newThreshold = (confidence + this.rejectedCommands.Last.Value.confidence) / 2;
                    }
                    newThreshold = newThreshold - (newThreshold * 0.05f);
                    if (newThreshold < this.currentThreshold)
                    {
                        Console.WriteLine("Command appears to have been re-tried, lowering threshold");
                        updateCurrentThreshold(newThreshold);
                    }
                    addAcceptedCommand(confidence, recognisedText);
                    return true;
                }
                if (confidence > currentThreshold)
                {
                    addAcceptedCommand(confidence, recognisedText);
                    return true;
                }
                else
                {
                    addRejectedCommand(confidence, recognisedText);
                    return false;
                }
            }
            private void updateCurrentThreshold(float newThreshold)
            {
                // TODO: probably need different behaviour here when running in "always on" mode to prevent cases where the app mis-recognises noise
                // and auto adjusts the thresholds down repeatedly                
                if (SpeechRecogniser.tuneConfidenceThresholds && newThreshold > 0 && newThreshold < 1)
                {
                    Console.WriteLine("Updating session's SRE threshold from " + this.currentThreshold + " to "
                        + newThreshold + " for type " + type + " (property name " + this.thresholdPropertyName + ")");
                    this.currentThreshold = newThreshold;
                }
                else
                {
                    Console.WriteLine("SRE confidence tuner recommends setting the session's SRE threshold from " + this.currentThreshold + " to "
                        + newThreshold + " for type " + type + " (property name " + this.thresholdPropertyName + "). This recommendation will be ignored");
                }
            }
            private void addRejectedCommand(float confidence, string command)
            {
                DateTime lastRejectedCommandDateTime = this.rejectedCommands.Last == null ? DateTime.MinValue : this.rejectedCommands.Last.Value.dateTime;
                // don't add this into the rejected list if it comes immediately after the last rejected command
                if ((DateTime.UtcNow - lastRejectedCommandDateTime).TotalSeconds >= SREThresholdInfo.secondsBetweenLoggedSREAttempts)
                {
                    rejectedCountSinceLastReview++;
                    this.rejectedCommands.AddLast(new HistoricSRECommandInfo(confidence, currentThreshold, command, DateTime.UtcNow));
                }
            }
            private void addAcceptedCommand(float confidence, string command)
            {
                DateTime lastAcceptedCommandDateTime = this.acceptedCommands.Last == null ? DateTime.MinValue : this.acceptedCommands.Last.Value.dateTime;
                if ((DateTime.UtcNow - lastAcceptedCommandDateTime).TotalSeconds >= SREThresholdInfo.secondsBetweenLoggedSREAttempts)
                {
                    acceptedCountSinceLastReview++;
                    this.acceptedCommands.AddLast(new HistoricSRECommandInfo(confidence, currentThreshold, command, DateTime.UtcNow));
                }
            }
            private void reviewThreshold()
            {
                // periodically inspect the accepted and rejected lists to see how the threshold looks.
                // The goal is to find cases where there are too many items in the rejected list that are fairly close to their threshold, and adjust the
                // threshold such that more of these items would have been accepted

                int acceptedPlusRejected = acceptedCountSinceLastReview + rejectedCountSinceLastReview;
                // do an initial rough-n-ready threshold check after the first 2 non-trigger word commands
                if (this.type != ThresholdType.TRIGGER && !this.initialCheckCompleted && acceptedPlusRejected == 2)
                {
                    this.initialCheckCompleted = true;
                    float maxConfidence = Math.Max(getMaxConfidence(true, 2), getMaxConfidence(false, 2));
                    Console.WriteLine("Best confidence score from first 2 SRE commands = " + maxConfidence + ", threshold = " + this.currentThreshold);
                    if (maxConfidence < this.currentThreshold)
                    {
                        this.currentThreshold = maxConfidence - (maxConfidence * 0.1f);
                    }
                }
                else if (acceptedPlusRejected > 5)
                {
                    Console.WriteLine("Reviewing SRE confidence threshold for " + type + ", there have been " + acceptedCountSinceLastReview +
                        " accepted command and " + rejectedCountSinceLastReview + " rejected commands since the last review, threshold is currently " + this.currentThreshold);
                    float acceptedRatio = (float) acceptedCountSinceLastReview / (float)(acceptedPlusRejected);
                    if (acceptedRatio == 1)
                    {
                        // hooray, no rejected commands. The threshold may be *way* too low so the accepted commands are full of crap, but we
                        // have no way of knowing this. Assume that we could be more strict here
                        float minAcceptedConfidence = getMinConfidence(false, acceptedCountSinceLastReview);
                        // set the threshold to be just under whatever the worst confidence we had was
                        float newConfidence = minAcceptedConfidence - (minAcceptedConfidence * 0.1f);
                        updateCurrentThreshold(newConfidence);
                    }
                    else if (acceptedRatio == 0)
                    {
                        // none of the recent commands have been accepted
                        float maxRejectedConfidence = getMaxConfidence(true, rejectedCountSinceLastReview);
                        // set the threshold to be just under whatever the best confidence we had was
                        float newConfidence = maxRejectedConfidence - (maxRejectedConfidence * 0.05f);
                        // update the confidence with the best case from the rejected commands. TODO: is it safe for the trigger word threshold to be updated like this?
                        updateCurrentThreshold(maxRejectedConfidence);
                    }
                    else
                    {
                        float averageRejectedConfidence = getAverageConfidence(true, rejectedCountSinceLastReview);
                        float maxRejectedConfidence = getMaxConfidence(true, rejectedCountSinceLastReview);
                        float averageAcceptedConfidence = getAverageConfidence(false, acceptedCountSinceLastReview);
                        float maxAcceptedConfidence = getMaxConfidence(false, acceptedCountSinceLastReview);
                        // some rejections so may need to tune the threshold. The approach will (probably) be different depending on the
                        // SRE mode and what this threshold is for
                        if (this.type == ThresholdType.TRIGGER)
                        {
                            // be extra careful with trigger. There's only 1 word in the grammar so we can easily lower the threshold so
                            // it triggers on any noise
                            float suggestedNewThreshold = maxRejectedConfidence + ((maxAcceptedConfidence - maxRejectedConfidence) / 2);
                            if (suggestedNewThreshold < this.currentThreshold || acceptedRatio > 0.7)
                            {
                                // only update the threshold if we're lowering it, or if most of our commands have been accepted
                                updateCurrentThreshold(suggestedNewThreshold);
                            }
                        }
                        else
                        {
                            // I've absolutely no idea if this "forumla" is nonsense but it'll do for now. Move the threshold so it's half way between
                            // the average rejected and average accepted confidence
                            float suggestedNewThreshold = averageRejectedConfidence + ((averageAcceptedConfidence - averageRejectedConfidence) / 2);
                            if (suggestedNewThreshold < this.currentThreshold || acceptedRatio > 0.7)
                            {
                                // only update the threshold if we're lowering it, or if most of our commands have been accepted
                                updateCurrentThreshold(suggestedNewThreshold);
                            }
                        }
                    }
                    // reset the accepted / rejected counts
                    acceptedCountSinceLastReview = 0;
                    rejectedCountSinceLastReview = 0;
                }
            }
            private bool isRepeatOfLastRejectedCommand(string recognisedText)
            {
                return this.rejectedCommands.Last != null
                        && this.rejectedCommands.Last.Value.command == recognisedText
                        && (DateTime.UtcNow - this.rejectedCommands.Last.Value.dateTime).TotalSeconds < SREThresholdInfo.maxSecondsBetweenRepeatedCommands;
            }
            private float getAverageConfidence(bool isRejectedMessages, int totalToCheck)
            {
                float confidence = 0;
                int count = 0;
                LinkedList<HistoricSRECommandInfo> historicCommandInfoList = isRejectedMessages ? this.rejectedCommands : this.acceptedCommands;
                LinkedListNode<HistoricSRECommandInfo> lastNode = historicCommandInfoList.Last;
                while (totalToCheck > count && lastNode != null)
                {
                    confidence += lastNode.Value.confidence;
                    lastNode = lastNode.Previous;
                    count++;
                }
                return confidence / count;
            }
            private float getMaxConfidence(bool isRejectedMessages, int totalToCheck)
            {
                float confidence = 0;
                int count = 0;
                LinkedList<HistoricSRECommandInfo> historicCommandInfoList = isRejectedMessages ? this.rejectedCommands : this.acceptedCommands;
                LinkedListNode<HistoricSRECommandInfo> lastNode = historicCommandInfoList.Last;
                while (totalToCheck > count && lastNode != null)
                {
                    confidence = Math.Max(lastNode.Value.confidence, confidence);
                    lastNode = lastNode.Previous;
                    count++;
                }
                return confidence;
            }
            private float getMinConfidence(bool isRejectedMessages, int totalToCheck)
            {
                float confidence = 1;
                int count = 0;
                LinkedList<HistoricSRECommandInfo> historicCommandInfoList = isRejectedMessages ? this.rejectedCommands : this.acceptedCommands;
                LinkedListNode<HistoricSRECommandInfo> lastNode = historicCommandInfoList.Last;
                while (totalToCheck > count && lastNode != null)
                {
                    confidence = Math.Min(lastNode.Value.confidence, confidence);
                    lastNode = lastNode.Previous;
                    count++;
                }
                return confidence;
            }
        }
    }
}

namespace CrewChiefV4 // has to be CrewChiefV4.Tracepoints for automated discovery
{
    public partial class Tracepoints
    {
        public class SpeechRecogniser
        {
            public static void speechRecognised(string text)
            {
                if (TracepointIsChecked("Tracepoints/SpeechRecogniser/speechRecognised"))
                {
                    Log.Debug(() => $"Speech recognised {text}");
                }
            }
        }
    }
}
