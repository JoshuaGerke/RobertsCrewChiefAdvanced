using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using CrewChiefV4.GameState;
using CrewChiefV4.Audio;
using System.Globalization;

namespace CrewChiefV4.Events
{

    public class AlarmClock : AbstractEvent
    {
        public static Boolean Active =>
            UserSettings.GetUserSettings().getBoolean("enable_alarm_clock_voice_recognition") ||
            UserSettings.GetUserSettings().getString("alarm_clock_times").Length > 0;

        private Boolean initialised = false;
        private List<Alarm> alarmTimes = new List<Alarm>();
        const String wakeUp = "alarm_clock/alarms";
        const String notifyYouAt = "alarm_clock/notify";
        const String folderAM = "alarm_clock/am";
        const String folderPM = "alarm_clock/pm";
        private readonly string preSetAlarms = UserSettings.GetUserSettings().getString("alarm_clock_times");

        private class Alarm
        {
            public Alarm(DateTime time, Boolean enabled)
            {
                this.alarmTime = time;
                this.enabled = enabled;
            }
            public DateTime alarmTime;
            public Boolean enabled;
        }
        public AlarmClock(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
        }
        void SetAlarm(DateTime alarmTime, Boolean enabled = true)
        {
            this.alarmTimes.Add(new Alarm(alarmTime, enabled));
        }
        public override void clearState()
        {
            initialised = false;
        }

        private void setFromProperties()
        {
            if (preSetAlarms?.Length > 0)
            {
                String[] alarms = preSetAlarms.Split(';');
                foreach (String alarm in alarms)
                {
                    DateTime alarmTime = ParseTimeInput(alarm);
                    if (alarmTime != DateTime.MinValue)
                    {
                        SetAlarm(alarmTime);
                        Console.WriteLine("Alarm has been set to " + alarmTime.ToString());
                    }
                    else
                    {
                        Console.WriteLine($"Invalid alarm time value '{alarm}'");
                    }
                }
            }
        }

        public static DateTime ParseTimeInput(string alarm)
        {
            CultureInfo enGB = new CultureInfo("en-GB");
            DateTime alarmTime = DateTime.MinValue;

            if (DateTime.TryParse(alarm, enGB, DateTimeStyles.None, out alarmTime))
            {
                DateTime now = DateTime.Now;
                alarmTime = new DateTime(now.Year, now.Month, now.Day, alarmTime.Hour, alarmTime.Minute, 00);
            }
            return alarmTime;
        }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            if (!initialised)
            {
                setFromProperties();
                initialised = true;
            }
            foreach (Alarm alarm in alarmTimes)
            {
                if (alarm.enabled && (DateTime.Now > alarm.alarmTime && DateTime.Now < alarm.alarmTime + TimeSpan.FromSeconds(60)))
                {
                    alarm.enabled = false;
                    audioPlayer.playMessageImmediately(new QueuedMessage(AlarmClock.wakeUp, 0, type: SoundType.CRITICAL_MESSAGE, priority: 15));
                }
            }
        }
        Tuple<int, int> getHourDigits(String voiceMessage)
        {
            foreach (KeyValuePair<String[], int> entry in SpeechRecogniser.hourMappings)
            {
                foreach (String numberStr in entry.Key)
                {
                    if (voiceMessage.Contains(" " + numberStr + " "))
                    {
                        int index = voiceMessage.IndexOf(" " + numberStr + " ") + numberStr.Length + 2;
                        if (index != -1)
                        {
                            return new Tuple<int, int>(entry.Value, index);
                        }
                    }

                }
            }
            return new Tuple<int, int>(-1, -1);
        }
        int getMinuteDigits(String voiceMessage)
        {
            foreach (KeyValuePair<String[], int> entry in SpeechRecogniser.minuteMappings)
            {
                foreach (String numberStr in entry.Key)
                {
                    if (voiceMessage.Equals(numberStr))
                    {
                        return entry.Value;
                    }
                }
            }
            return -1;
        }

        internal static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.CLEAR_ALARM_CLOCK,
            SpeechCommands.ID.SET_ALARM_CLOCK,
        };
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            return SpeechCommands.SpeechToCommand(Commands, voiceMessage);
        }

        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID cmd)
        {
            switch (cmd)
            {
                case SpeechCommands.ID.SET_ALARM_CLOCK:
                {
                    int index = voiceMessage.Length;
                    Boolean isPastMidDay = false;
                    Boolean isAfterMidnight = false;
                    foreach (String ams in SpeechRecogniser.AM)
                    {
                        if (voiceMessage.Contains(" " + ams))
                        {
                            index = voiceMessage.IndexOf(" " + ams);
                            isAfterMidnight = true;
                        }
                    }
                    foreach (String pms in SpeechRecogniser.PM)
                    {
                        if (voiceMessage.Contains(" " + pms))
                        {
                            index = voiceMessage.IndexOf(" " + pms);
                            isPastMidDay = true;
                        }
                    }
                    voiceMessage = voiceMessage.Substring(0, index);
                    Tuple<int, int> hourWithIndex = getHourDigits(voiceMessage);
                    int hour = isPastMidDay ? hourWithIndex.Item1 + 12 : hourWithIndex.Item1;
                    if (hour >= 12 && !isAfterMidnight)
                    {
                        isPastMidDay = true;
                    }
                    String minutesString = voiceMessage.Substring(hourWithIndex.Item2);
                    int minutes = getMinuteDigits(minutesString);
                    if (hourWithIndex.Item1 != -1 && minutes != -1)
                    {
                        SetAlarm(new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, hour, minutes, 00));
                        Console.WriteLine("Alarm has been set to " + new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, hour, minutes, 00).ToString());
                        if (hour == 0)
                        {
                            // not 100% sure its needed but there just in case.
                            isPastMidDay = false;
                            hour = 24;
                        }
                        if (hour > 12)
                        {
                            hour = hour - 12;
                        }
                        if (minutes < 10)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage("alarm", 0,
                                messageFragments: MessageContents(notifyYouAt, hour, NumberReader.folderOh, minutes, isPastMidDay ? folderPM : folderAM),
                                type: SoundType.CRITICAL_MESSAGE, priority: 0));
                        }
                        else
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage("alarm", 0,
                                messageFragments: MessageContents(notifyYouAt, hour, minutes, isPastMidDay ? folderPM : folderAM),
                                type: SoundType.CRITICAL_MESSAGE, priority: 0));
                        }
                    }

                    break;
                }
                case SpeechCommands.ID.CLEAR_ALARM_CLOCK:
                    alarmTimes.Clear();
                    audioPlayer.playMessageImmediately(new QueuedMessage("alarm", 0,
                        messageFragments: MessageContents(AudioPlayer.folderAcknowlegeOK)));
                    break;
            }

        }
    }
}
