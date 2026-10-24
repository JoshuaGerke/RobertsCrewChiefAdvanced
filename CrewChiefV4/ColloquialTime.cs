using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;

using System;

namespace CrewChiefV4
{
    /// <summary>
    /// Say the time as "ten past eight" / "quarter to nine" etc.
    /// </summary>
    public class ColloquialTime
    {
        struct MinuteToText
        {
            internal string pre;
            internal string post;
            internal int addHour;
        }

        private MinuteToText[] MinutesToText =
        {
            new MinuteToText{pre = "", post = " o'clock", addHour = 0},
            new MinuteToText{pre = "", post = " o'clock", addHour = 0},
            new MinuteToText{pre = "", post = " o'clock", addHour = 0},
            new MinuteToText{pre = "five past ", post = "", addHour = 0},
            new MinuteToText{pre = "five past ", post = "", addHour = 0},
            new MinuteToText{pre = "five past ", post = "", addHour = 0},
            new MinuteToText{pre = "five past ", post = "", addHour = 0},
            new MinuteToText{pre = "five past ", post = "", addHour = 0},
            new MinuteToText{pre = "ten past ", post = "", addHour = 0},
            new MinuteToText{pre = "ten past ", post = "", addHour = 0},
            new MinuteToText{pre = "ten past ", post = "", addHour = 0},
            new MinuteToText{pre = "ten past ", post = "", addHour = 0},
            new MinuteToText{pre = "ten past ", post = "", addHour = 0},
            new MinuteToText{pre = "quarter past ", post = "", addHour = 0},
            new MinuteToText{pre = "quarter past ", post = "", addHour = 0},
            new MinuteToText{pre = "quarter past ", post = "", addHour = 0},
            new MinuteToText{pre = "quarter past ", post = "", addHour = 0},
            new MinuteToText{pre = "quarter past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty five past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty five past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty five past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty five past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty five past ", post = "", addHour = 0},
            new MinuteToText{pre = "half past ", post = "", addHour = 0},
            new MinuteToText{pre = "half past ", post = "", addHour = 0},
            new MinuteToText{pre = "half past ", post = "", addHour = 0},
            new MinuteToText{pre = "half past ", post = "", addHour = 0},
            new MinuteToText{pre = "half past ", post = "", addHour = 0},
            new MinuteToText{pre = "twenty five to ", post = "", addHour = 1},
            new MinuteToText{pre = "twenty five to ", post = "", addHour = 1},
            new MinuteToText{pre = "twenty five to ", post = "", addHour = 1},
            new MinuteToText{pre = "twenty five to ", post = "", addHour = 1},
            new MinuteToText{pre = "twenty five to ", post = "", addHour = 1},
            new MinuteToText{pre = "twenty to ", post = "", addHour = 1},
            new MinuteToText{pre = "twenty to ", post = "", addHour = 1},
            new MinuteToText{pre = "twenty to ", post = "", addHour = 1},
            new MinuteToText{pre = "twenty to ", post = "", addHour = 1},
            new MinuteToText{pre = "twenty to ", post = "", addHour = 1},
            new MinuteToText{pre = "quarter to ", post = "", addHour = 1},
            new MinuteToText{pre = "quarter to ", post = "", addHour = 1},
            new MinuteToText{pre = "quarter to ", post = "", addHour = 1},
            new MinuteToText{pre = "quarter to ", post = "", addHour = 1},
            new MinuteToText{pre = "quarter to ", post = "", addHour = 1},
            new MinuteToText{pre = "ten to ", post = "", addHour = 1},
            new MinuteToText{pre = "ten to ", post = "", addHour = 1},
            new MinuteToText{pre = "ten to ", post = "", addHour = 1},
            new MinuteToText{pre = "ten to ", post = "", addHour = 1},
            new MinuteToText{pre = "ten to ", post = "", addHour = 1},
            new MinuteToText{pre = "five to ", post = "", addHour = 1},
            new MinuteToText{pre = "five to ", post = "", addHour = 1},
            new MinuteToText{pre = "five to ", post = "", addHour = 1},
            new MinuteToText{pre = "five to ", post = "", addHour = 1},
            new MinuteToText{pre = "nearly ", post = "", addHour = 1},
            new MinuteToText{pre = "nearly ", post = "", addHour = 1},
            new MinuteToText{pre = "nearly ", post = "", addHour = 1},
        };
        String[] hourStrings =
        {
            "midnight", "one", "two", "three","four", "five", "six",
            "seven", "eight", "nine", "ten", "eleven", "twelve"
        };
        /// <summary>
        /// Format time as it's said
        /// </summary>
        /// <param name="tod">time of day</param>
        /// <returns>lower case time of day</returns>
        public string Format(DateTime tod)
        {
            int minute = tod.Minute;
            var minuteFormat = MinutesToText[minute];
            int hour = tod.Hour + minuteFormat.addHour; // Add an hour if it's "to" the next hour
            if (hour > 23)
            {
                hour = 0;
            }
            if (hour > 12)
            {
                hour -= 12;
            }
            var hourString = hourStrings[hour];
            string result = minuteFormat.pre + hourString + minuteFormat.post;
            if (result == "midnight o'clock")
            {
                result = "midnight";
            }
            return result;
        }
    }

    //class ColloquialTimeEvent : AbstractEvent
    //{
    //    private ColloquialTime colloquialTime;
    //    AudioPlayer audioPlayer;
    //    public ColloquialTimeEvent(AudioPlayer audioPlayer)
    //    {
    //        colloquialTime = new ColloquialTime();
    //        this.audioPlayer = audioPlayer;
    //    }

    //    public override void clearState()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void sayTime(DateTime tod)
    //    {
    //        string timeString = colloquialTime.Format(tod);
    //        playMessage($"The time is {timeString}");
    //    }

    //    protected override void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    private void playMessage(string text)
    //    {
    //        QueuedMessage message;
    //        var fragment = MessageFragment.Text(text);
    //        fragment.allowTTS = true;

    //        string messageName = $"colloquial_time";
    //        message = new QueuedMessage(
    //            messageName,
    //            10,
    //            messageFragments: AbstractEvent.MessageContents(fragment),
    //            abstractEvent: this,
    //            type: SoundType.IMPORTANT_MESSAGE,
    //            priority: SoundMetadata.DEFAULT_PRIORITY
    //        );
    //        audioPlayer.playMessageImmediately(message);
    //    }
    //}
}
