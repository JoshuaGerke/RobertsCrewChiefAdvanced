using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;

namespace CrewChiefV4.Events
{
    class DriverSwaps : AbstractEvent
    {
        private Boolean playedStintOverThisLap = false;
        private Boolean played15MinutesLeftInStint = false;
        private Boolean played10MinutesLeftInStint = false;
        private Boolean played5MinutesLeftInStint = false;
        private Boolean played2MinutesLeftInStint = false;

        private const String folder15MinutesLeftInStint = "driver_swaps/15_minutes_left_in_stint";
        private const String folder10MinutesLeftInStint = "driver_swaps/10_minutes_left_in_stint";
        private const String folder5MinutesLeftInStint = "driver_swaps/5_minutes_left_in_stint";
        private const String folder2MinutesLeftInStint = "driver_swaps/2_minutes_left_in_stint";
        private const String folderEndOfDriverStint = "driver_swaps/pit_this_lap_for_driver_change";
        private const String folderEndOfDriverStintReminder = "driver_swaps/pit_now_for_driver_change";
        private const String folderEndOfTotalDriverStint = "driver_swaps/pit_this_lap_driver_change_no_more_stints";

        public DriverSwaps(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
        }

        public override void clearState()
        {
            playedStintOverThisLap = false;
            played15MinutesLeftInStint = false;
            played10MinutesLeftInStint = false;
            played5MinutesLeftInStint = false;
            played2MinutesLeftInStint = false;
        }

        protected override void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            if (currentGameState.SessionData.SessionType != SessionType.Race)
            {
                return;
            }
            if (previousGameState != null && previousGameState.PitData.DriverStintSecondsRemaining < currentGameState.PitData.DriverStintSecondsRemaining)
            {
                Console.WriteLine("Driver stint time remaining has increased, clearing state");
                clearState();
            }
            else if (previousGameState != null && playedStintOverThisLap && previousGameState.SessionData.SectorNumber == 2 && currentGameState.SessionData.SectorNumber == 3)
            {
                audioPlayer.playMessage(new QueuedMessage(folderEndOfDriverStintReminder, 0));
            }
            else if (!playedStintOverThisLap && !currentGameState.PitData.InPitlane && currentGameState.PitData.DriverStintSecondsRemaining > 0)
            {
                if (currentGameState.SessionData.IsNewLap && currentGameState.SessionData.PlayerLapTimeSessionBest > 0)
                {
                    // check if we'll need to swap at the end of this lap
                    if (currentGameState.PitData.DriverStintSecondsRemaining < currentGameState.SessionData.PlayerLapTimeSessionBest + 30)
                    {
                        playedStintOverThisLap = true;
                        Console.WriteLine("Current stint expiring - pit this lap");
                        audioPlayer.playMessage(new QueuedMessage(folderEndOfDriverStint, 0));
                    }
                    // check if we'll have run out of total stint time at the end of this lap
                    else if (currentGameState.PitData.DriverStintTotalSecondsRemaining < currentGameState.SessionData.PlayerLapTimeSessionBest + 30)
                    {
                        playedStintOverThisLap = true;
                        Console.WriteLine("Total driver seat time expiring - pit this lap");
                        audioPlayer.playMessage(new QueuedMessage(folderEndOfTotalDriverStint, 0));
                    }
                }
                // checks for intervals
                if (!played15MinutesLeftInStint &&
                    currentGameState.PitData.DriverStintSecondsRemaining < 960 && currentGameState.PitData.DriverStintSecondsRemaining > 930)
                {
                    played15MinutesLeftInStint = true;
                    Console.WriteLine("15 mins left in this stint");
                    audioPlayer.playMessage(new QueuedMessage(folder15MinutesLeftInStint, 0));
                }
                else if (!played10MinutesLeftInStint &&
                    currentGameState.PitData.DriverStintSecondsRemaining < 600 && currentGameState.PitData.DriverStintSecondsRemaining > 570)
                {
                    played15MinutesLeftInStint = true;
                    played10MinutesLeftInStint = true;
                    Console.WriteLine("10 mins left in this stint");
                    audioPlayer.playMessage(new QueuedMessage(folder10MinutesLeftInStint, 0));
                }
                else if (!played5MinutesLeftInStint &&
                    currentGameState.PitData.DriverStintSecondsRemaining < 300 && currentGameState.PitData.DriverStintSecondsRemaining > 270)
                {
                    played15MinutesLeftInStint = true;
                    played10MinutesLeftInStint = true;
                    played5MinutesLeftInStint = true;
                    Console.WriteLine("5 mins left in this stint");
                    audioPlayer.playMessage(new QueuedMessage(folder5MinutesLeftInStint, 0));
                }
                else if (!played2MinutesLeftInStint &&
                    currentGameState.PitData.DriverStintSecondsRemaining < 120 && currentGameState.PitData.DriverStintSecondsRemaining > 110)
                {
                    played15MinutesLeftInStint = true;
                    played10MinutesLeftInStint = true;
                    played5MinutesLeftInStint = true;
                    played2MinutesLeftInStint = true;
                    Console.WriteLine("2 mins left in this stint");
                    audioPlayer.playMessage(new QueuedMessage(folder2MinutesLeftInStint, 0));
                }
            }
        }

        public override void respond(String voiceMessage)
        {
        }
    }
}
