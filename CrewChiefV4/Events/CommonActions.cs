using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using CrewChiefV4.NumberProcessing;
using CrewChiefV4.Overlay;
using CrewChiefV4.R3E;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static System.Net.Mime.MediaTypeNames;

namespace CrewChiefV4.Events
{
    class CommonActions : AbstractEvent
    {
        private Boolean keepQuietEnabled = false;
        private readonly Boolean useVerboseResponses = UserSettings.GetUserSettings().getBoolean("use_verbose_responses");
        GameStateData currentGameState = null;
        public CommonActions(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
        }
        public override void clearState()
        {
        }
        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            this.currentGameState = currentGameState;
        }

        internal static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.CAR_STATUS,
            SpeechCommands.ID.DAMAGE_REPORT,
            SpeechCommands.ID.DISABLE_CUT_TRACK_WARNINGS,
            SpeechCommands.ID.DISABLE_MANUAL_FORMATION_LAP,
            SpeechCommands.ID.DISABLE_YELLOW_FLAG_MESSAGES,
            SpeechCommands.ID.DONT_TALK_IN_THE_CORNERS,
            SpeechCommands.ID.DONT_TELL_ME_THE_GAPS,
            SpeechCommands.ID.ENABLE_CUT_TRACK_WARNINGS,
            SpeechCommands.ID.ENABLE_MANUAL_FORMATION_LAP,
            SpeechCommands.ID.ENABLE_YELLOW_FLAG_MESSAGES,
            SpeechCommands.ID.HIDE_SUBTITLES,
            SpeechCommands.ID.GAME_QUIETER,
            SpeechCommands.ID.GAME_LOUDER,
            SpeechCommands.ID.KEEP_ME_INFORMED,
            SpeechCommands.ID.KEEP_QUIET,
            SpeechCommands.ID.PLAY_CORNER_NAMES,
            SpeechCommands.ID.RADIO_CHECK,
            SpeechCommands.ID.SESSION_STATUS,
            SpeechCommands.ID.SHOW_SUBTITLES,
            SpeechCommands.ID.START_PACE_NOTES_PLAYBACK,
            SpeechCommands.ID.STATUS,
            SpeechCommands.ID.STOP_COMPLAINING,
            SpeechCommands.ID.STOP_PACE_NOTES_PLAYBACK,
            SpeechCommands.ID.TALK_TO_ME_ANYWHERE,
            SpeechCommands.ID.TELL_ME_THE_GAPS,
            SpeechCommands.ID.TOGGLE_TURN_NUMBERS,
            SpeechCommands.ID.VOIP_QUIETER,
            SpeechCommands.ID.VOIP_LOUDER,
            SpeechCommands.ID.VOLUME_DOWN,
            SpeechCommands.ID.VOLUME_UP,
            SpeechCommands.ID.WHATS_MY_RANK,
            SpeechCommands.ID.WHATS_MY_RATING,
            SpeechCommands.ID.WHATS_MY_REPUTATION,
            SpeechCommands.ID.WHATS_THE_TIME,
        };
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            SpeechCommands.ID cmd = SpeechCommands.SpeechToCommand(Commands, voiceMessage, false);
            if (cmd == SpeechCommands.ID.NO_COMMAND)
            {
                cmd = SpeechCommands.ControllerToCommand(ControllerCommands, voiceMessage);
            }
            return cmd;
        }
        internal static List<SpeechCommands.ID> ControllerCommands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.ADD_TRACK_LANDMARK,
            SpeechCommands.ID.GET_CAR_STATUS,
            SpeechCommands.ID.GET_DAMAGE_REPORT,
            SpeechCommands.ID.GET_FUEL_STATUS,
            SpeechCommands.ID.GET_SESSION_STATUS,
            SpeechCommands.ID.GET_STATUS,
            SpeechCommands.ID.PIT_PREDICTION,
            SpeechCommands.ID.PRINT_TRACK_DATA,
            SpeechCommands.ID.READ_CORNER_NAMES_FOR_LAP,
            SpeechCommands.ID.REPEAT_LAST_MESSAGE_BUTTON,
            SpeechCommands.ID.TOGGLE_BLOCK_MESSAGES_IN_HARD_PARTS,
            SpeechCommands.ID.TOGGLE_ENABLE_CUT_TRACK_WARNINGS,
            SpeechCommands.ID.TOGGLE_MANUAL_FORMATION_LAP,
            SpeechCommands.ID.TOGGLE_PACE_NOTES_PLAYBACK,
            SpeechCommands.ID.TOGGLE_PACE_NOTES_RECORDING,
            SpeechCommands.ID.TOGGLE_RACE_UPDATES_FUNCTION,
            SpeechCommands.ID.TOGGLE_READ_OPPONENT_DELTAS,
            SpeechCommands.ID.TOGGLE_TRACK_LANDMARKS_RECORDING,
            SpeechCommands.ID.TOGGLE_YELLOW_FLAG_MESSAGES,
        };
        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID cmd)
        {
            if (!(Commands.Concat(ControllerCommands)).Contains(cmd))
            {
                Log.Error($"{cmd.ToString()} not handled by {this.GetType().Name}");
                return;
            }
            switch (cmd)
            {
                case SpeechCommands.ID.RADIO_CHECK:
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderRadioCheckResponse, 0));
                    break;
                case SpeechCommands.ID.KEEP_QUIET:
                    enableKeepQuietMode();
                    break;
                case SpeechCommands.ID.PLAY_CORNER_NAMES:
                    playCornerNamesForCurrentLap();
                    break;
                case SpeechCommands.ID.DONT_TELL_ME_THE_GAPS:
                    disableDeltasMode();
                    break;
                case SpeechCommands.ID.TELL_ME_THE_GAPS:
                    enableDeltasMode();
                    break;
                case SpeechCommands.ID.ENABLE_YELLOW_FLAG_MESSAGES:
                    enableYellowFlagMessages();
                    break;
                case SpeechCommands.ID.DISABLE_YELLOW_FLAG_MESSAGES:
                    disableYellowFlagMessages();
                    break;
                case SpeechCommands.ID.ENABLE_CUT_TRACK_WARNINGS:
                    enableCutTrackWarnings();
                    break;
                case SpeechCommands.ID.DISABLE_CUT_TRACK_WARNINGS:
                    disableCutTrackWarnings();
                    break;
                case SpeechCommands.ID.ENABLE_MANUAL_FORMATION_LAP:
                    enableManualFormationLapMode();
                    break;
                case SpeechCommands.ID.DISABLE_MANUAL_FORMATION_LAP:
                    disableManualFormationLapMode();
                    break;
                case SpeechCommands.ID.WHATS_THE_TIME:
                    reportCurrentTime();
                    break;
                case SpeechCommands.ID.TALK_TO_ME_ANYWHERE:
                    disableDelayMessagesInHardParts();
                    break;
                case SpeechCommands.ID.DONT_TALK_IN_THE_CORNERS:
                    enableDelayMessagesInHardParts();
                    break;
                case SpeechCommands.ID.KEEP_ME_INFORMED:
                    disableKeepQuietMode();
                    break;
                case SpeechCommands.ID.STOP_COMPLAINING:
                    Console.WriteLine("Disabling complaining messages for this session");
                    GlobalBehaviourSettings.maxComplaintsPerSession = 0;
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    break;
                case SpeechCommands.ID.WHATS_MY_RANK:
                {
                    R3ERatingData playerRatings = R3ERatings.playerRating;
                    if (playerRatings != null)
                    {
                        List<MessageFragment> fragments = new List<MessageFragment>();
                        // ensure hundreds don't get truncated
                        fragments.Add(MessageFragment.Integer(playerRatings.rank, false));
                        audioPlayer.playMessageImmediately(new QueuedMessage("playerRank", 0, messageFragments: fragments));
                    }

                    break;
                }
                case SpeechCommands.ID.WHATS_MY_REPUTATION:
                {
                    R3ERatingData playerRatings = R3ERatings.playerRating;
                    if (playerRatings != null)
                    {
                        // if we don't explicitly split the sound up here it'll be read as an int
                        int intPart = (int)playerRatings.reputation;
                        int decPart = (int)(10 * (playerRatings.reputation - (float)intPart));
                        audioPlayer.playMessageImmediately(new QueuedMessage("playerReputation", 0,
                            messageFragments: MessageContents(intPart, NumberReader.folderPoint, decPart)));
                    }

                    break;
                }
                case SpeechCommands.ID.WHATS_MY_RATING:
                {
                    R3ERatingData playerRatings = R3ERatings.playerRating;
                    if (playerRatings != null)
                    {
                        // if we don't explicitly split the sound up here it'll be read as an int
                        int intPart = (int)playerRatings.rating;
                        int decPart = (int)(10 * (playerRatings.rating - (float)intPart));
                        audioPlayer.playMessageImmediately(new QueuedMessage("playerRating", 0,
                            messageFragments: MessageContents(intPart, NumberReader.folderPoint, decPart)));
                    }

                    break;
                }
                // multiple events for status reporting:
                case SpeechCommands.ID.DAMAGE_REPORT:
                case SpeechCommands.ID.GET_DAMAGE_REPORT:
                    Console.WriteLine("Getting damage report");
                    getDamageReport();
                    break;
                case SpeechCommands.ID.CAR_STATUS:
                case SpeechCommands.ID.GET_CAR_STATUS:
                    Console.WriteLine("Getting car status");
                    getCarStatus();
                    break;
                case SpeechCommands.ID.STATUS:
                case SpeechCommands.ID.GET_STATUS:
                    Console.WriteLine("Getting full status");
                    getStatus();
                    break;
                case SpeechCommands.ID.SESSION_STATUS:
                case SpeechCommands.ID.GET_SESSION_STATUS:
                    Console.WriteLine("Getting session status");
                    getSessionStatus();
                    break;
                case SpeechCommands.ID.START_PACE_NOTES_PLAYBACK:
                {
                    if (!DriverTrainingService.isPlayingPaceNotes)
                    {
                        togglePaceNotesPlayback();
                    }

                    break;
                }
                case SpeechCommands.ID.STOP_PACE_NOTES_PLAYBACK:
                {
                    if (DriverTrainingService.isPlayingPaceNotes)
                    {
                        togglePaceNotesPlayback();
                    }

                    break;
                }
                case SpeechCommands.ID.SHOW_SUBTITLES:
                    SubtitleOverlay.shown = true;
                    break;
                case SpeechCommands.ID.HIDE_SUBTITLES:
                    SubtitleOverlay.shown = false;
                    break;
                case SpeechCommands.ID.TOGGLE_RACE_UPDATES_FUNCTION:
                    Console.WriteLine("Toggling keep quiet mode");
                    toggleKeepQuietMode();
                    break;
                case SpeechCommands.ID.TOGGLE_READ_OPPONENT_DELTAS:
                    Console.WriteLine("Toggling read opponent deltas mode");
                    toggleReadOpponentDeltasMode();
                    break;
                case SpeechCommands.ID.TOGGLE_MANUAL_FORMATION_LAP:
                    Console.WriteLine("Toggling manual formation lap mode");
                    toggleManualFormationLapMode();
                    break;
                case SpeechCommands.ID.READ_CORNER_NAMES_FOR_LAP:
                    Console.WriteLine("Enabling corner name reading for current lap");
                    playCornerNamesForCurrentLap();
                    break;
                case SpeechCommands.ID.REPEAT_LAST_MESSAGE_BUTTON:
                    Console.WriteLine("Repeating last message");
                    audioPlayer.repeatLastMessage();
                    break;
                case SpeechCommands.ID.TOGGLE_YELLOW_FLAG_MESSAGES:
                    Console.WriteLine("Toggling yellow flag messages to: " + (CrewChief.yellowFlagMessagesEnabled ? "disabled" : "enabled"));
                    toggleEnableYellowFlagsMode();
                    break;
                case SpeechCommands.ID.TOGGLE_BLOCK_MESSAGES_IN_HARD_PARTS:
                    Console.WriteLine("Toggling delay-messages-in-hard-parts");
                    toggleDelayMessagesInHardParts();
                    break;
                case SpeechCommands.ID.GET_FUEL_STATUS:
                    Console.WriteLine("Getting fuel/battery status");
                    reportFuelBatteryStatus();
                    break;
                case SpeechCommands.ID.TOGGLE_PACE_NOTES_RECORDING:
                    Console.WriteLine("Start / stop pace notes recording");
                    togglePaceNotesRecording();
                    break;
                case SpeechCommands.ID.TOGGLE_PACE_NOTES_PLAYBACK:
                    Console.WriteLine("Start / stop pace notes playback");
                    togglePaceNotesPlayback();
                    break;
                case SpeechCommands.ID.TOGGLE_TRACK_LANDMARKS_RECORDING:
                    Console.WriteLine("Start / stop track landmark recording");
                    toggleTrackLandmarkRecording();
                    break;
                case SpeechCommands.ID.TOGGLE_ENABLE_CUT_TRACK_WARNINGS:
                    Console.WriteLine("Enable / disable cut track warnings");
                    toggleEnableCutTrackWarnings();
                    break;
                case SpeechCommands.ID.ADD_TRACK_LANDMARK:
                    //dont confirm press here we do that in addLandmark
                    toggleAddTrackLandmark();
                    break;
                case SpeechCommands.ID.PIT_PREDICTION:
                {
                    Console.WriteLine("pit prediction");
                    if (currentGameState != null)
                    {
                        Strategy strategy = (Strategy)CrewChief.getEvent("Strategy");
                        switch (currentGameState.SessionData.SessionType)
                        {
                            case SessionType.Race:
                                strategy.respondRace();
                                break;
                            case SessionType.Practice:
                                strategy.respondPracticeStop();
                                break;
                        }
                    }

                    break;
                }
                case SpeechCommands.ID.PRINT_TRACK_DATA:
                {
                    if (currentGameState != null && currentGameState.SessionData != null &&
                        currentGameState.SessionData.TrackDefinition != null)
                    {
                        string posInfo = "";
                        var worldPos = currentGameState.PositionAndMotionData.WorldPosition;
                        if (worldPos != null && worldPos.Length > 2)
                        {
                            posInfo = string.Format(", position x:{0:0.000} y:{1:0.000} z:{2:0.000}", worldPos[0], worldPos[1], worldPos[2]);
                        }
                        if (Game.RACE_ROOM)
                        {
                            Console.WriteLine("RaceroomLayoutId: " + currentGameState.SessionData.TrackDefinition.id + ", distanceRoundLap:" +
                                              currentGameState.PositionAndMotionData.DistanceRoundTrack.ToString("0.000") + ", player's car ID: " + currentGameState.carClass.getClassIdentifier() + posInfo);
                        }
                        else
                        {
                            Console.WriteLine("TrackName: " + currentGameState.SessionData.TrackDefinition.name + ", distanceRoundLap:" +
                                              currentGameState.PositionAndMotionData.DistanceRoundTrack.ToString("0.000") + ", player's car ID: " + currentGameState.carClass.getClassIdentifier() + posInfo);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No track data available");
                    }

                    break;
                }
                case SpeechCommands.ID.TOGGLE_TURN_NUMBERS:
                { 
                    TrackData.useTurnNumbers = !TrackData.useTurnNumbers;
                    if (TrackData.useTurnNumbers)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.phraseIfItHasBeenRecorded("acknowledge/use_turn_numbers"), 0));
                        Log.Commentary("Using turn numbers");
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.phraseIfItHasBeenRecorded("acknowledge/use_corner_names"), 0));
                        Log.Commentary("Using corner names");
                    }
                    break;
                }
                case SpeechCommands.ID.VOLUME_DOWN:
                {
                    if (MainWindow.instance != null)
                    {
                        volumeDownAck(MainWindow.instance.volumeDown());
                    }
                    break;
                }
                case SpeechCommands.ID.VOLUME_UP:
                {
                    if (MainWindow.instance != null)
                    {
                        volumeUpAck(MainWindow.instance.volumeUp());
                    }
                    break;
                }
                case SpeechCommands.ID.GAME_QUIETER:
                {
                    volumeDownAck(GameOrVoipVolume.DecreaseGameVolume());
                    break;
                }
                case SpeechCommands.ID.GAME_LOUDER:
                {
                    volumeUpAck(GameOrVoipVolume.IncreaseGameVolume());
                    break;
                }
                case SpeechCommands.ID.VOIP_QUIETER:
                {
                    volumeDownAck(GameOrVoipVolume.DecreaseVoipVolume());
                    break;
                }
                case SpeechCommands.ID.VOIP_LOUDER:
                {
                    volumeUpAck(GameOrVoipVolume.IncreaseVoipVolume());
                    break;
                }
                default:
                    break;
            }
            //Console.WriteLine(voiceMessage);
        }
        void volumeUpAck(bool status)
        { 
         if (status)
            {
                audioPlayer.playMessageOrFallback("acknowledge/volume_up", AudioPlayer.folderAcknowlegeOK);
            }
         else
            {
                audioPlayer.playMessageOrFallbackToBleep("acknowledge/full_volume");
            }
        }
        void volumeDownAck(bool status)
        {
            if (status)
            {
                audioPlayer.playMessageOrFallback("acknowledge/volume_down", AudioPlayer.folderAcknowlegeOK);
            }
            else
            {
                audioPlayer.playMessageOrFallbackToBleep("acknowledge/volume_muted");
            }
        }
        public void enableKeepQuietMode()
        {
            keepQuietEnabled = true;
            audioPlayer.enableKeepQuietMode();
        }
        public void disableKeepQuietMode()
        {
            keepQuietEnabled = false;
            // also disable the global speak-only-when-spoken-to setting
            GlobalBehaviourSettings.speakOnlyWhenSpokenTo = false;
            audioPlayer.disableKeepQuietMode();
        }
        public void toggleKeepQuietMode()
        {
            if (keepQuietEnabled)
            {
                disableKeepQuietMode();
            }
            else
            {
                enableKeepQuietMode();
            }
        }

        public void toggleEnableCutTrackWarnings()
        {
            if (GlobalBehaviourSettings.cutTrackWarningsEnabled)
            {
                disableCutTrackWarnings();
            }
            else
            {
                enableCutTrackWarnings();
            }
        }

        public void enableCutTrackWarnings()
        {
            GlobalBehaviourSettings.cutTrackWarningsEnabled = true;
            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderCutWarningsEnabled, 0));
        }

        public void disableCutTrackWarnings()
        {
            GlobalBehaviourSettings.cutTrackWarningsEnabled = false;
            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderCutWarningsDisabled, 0));
        }

        public void toggleDelayMessagesInHardParts()
        {
            if (AudioPlayer.delayMessagesInHardParts)
            {
                disableDelayMessagesInHardParts();
            }
            else
            {
                enableDelayMessagesInHardParts();
            }
        }

        public void enableDelayMessagesInHardParts()
        {
            if (!AudioPlayer.delayMessagesInHardParts)
            {
                AudioPlayer.delayMessagesInHardParts = true;
            }
            // switch the gap points to use the adjusted ones
            if (currentGameState != null && currentGameState.SessionData.TrackDefinition != null && currentGameState.hardPartsOnTrackData.hardPartsMapped)
            {
                currentGameState.SessionData.TrackDefinition.adjustGapPoints(currentGameState.hardPartsOnTrackData.processedHardPartsForBestLap);
            }
            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowledgeEnableDelayInHardParts, 0));
        }

        public void disableDelayMessagesInHardParts()
        {
            if (AudioPlayer.delayMessagesInHardParts)
            {
                AudioPlayer.delayMessagesInHardParts = false;
            }
            // switch the gap points back to use the regular ones
            if (currentGameState != null && currentGameState.SessionData.TrackDefinition != null && currentGameState.hardPartsOnTrackData.hardPartsMapped)
            {
                currentGameState.SessionData.TrackDefinition.setGapPoints();
            }
            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowledgeDisableDelayInHardParts, 0));
        }

        public void toggleReadOpponentDeltasMode()
        {
            if (CrewChief.readOpponentDeltasForEveryLap)
            {
                disableDeltasMode();
            }
            else
            {
                enableDeltasMode();
            }
        }

        public void enableDeltasMode()
        {
            if (!CrewChief.readOpponentDeltasForEveryLap)
            {
                CrewChief.readOpponentDeltasForEveryLap = true;
            }
            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDeltasEnabled, 0));
        }

        public void disableDeltasMode()
        {
            if (CrewChief.readOpponentDeltasForEveryLap)
            {
                CrewChief.readOpponentDeltasForEveryLap = false;
            }
            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDeltasDisabled, 0));
        }

        public void toggleEnableYellowFlagsMode()
        {
            if (CrewChief.yellowFlagMessagesEnabled)
            {
                disableYellowFlagMessages();
            }
            else
            {
                enableYellowFlagMessages();
            }
        }

        public void enableYellowFlagMessages()
        {
            CrewChief.yellowFlagMessagesEnabled = true;
            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderYellowEnabled, 0));
        }

        public void disableYellowFlagMessages()
        {
            CrewChief.yellowFlagMessagesEnabled = false;
            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderYellowDisabled, 0));
        }

        public void toggleManualFormationLapMode()
        {
            if (GameStateData.useManualFormationLap)
            {
                disableManualFormationLapMode();
            }
            else
            {
                enableManualFormationLapMode();
            }
        }

        public void enableManualFormationLapMode()
        {
            // Prevent accidential trigger during the race.  Luckily, there's a handy hack available :)
            if (currentGameState != null && currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.CompletedLaps >= 1)
            {
                Console.WriteLine("Rejecting manual formation lap request due to race already in progress");
                return;
            }
            if (!GameStateData.useManualFormationLap)
            {
                GameStateData.useManualFormationLap = true;
                GameStateData.onManualFormationLap = true;
            }
            Console.WriteLine("Manual formation lap mode is ACTIVE");
            audioPlayer.playMessageImmediately(new QueuedMessage(LapCounter.folderManualFormationLapModeEnabled, 0));
        }

        public void disableManualFormationLapMode()
        {
            if (GameStateData.useManualFormationLap)
            {
                GameStateData.useManualFormationLap = false;
                GameStateData.onManualFormationLap = false;
            }
            Console.WriteLine("Manual formation lap mode is DISABLED");
            audioPlayer.playMessageImmediately(new QueuedMessage(LapCounter.folderManualFormationLapModeDisabled, 0));
        }

        private void reportFuelBatteryStatus()
        {
            if (GlobalBehaviourSettings.enabledMessageTypes.Contains(MessageTypes.BATTERY))
            {
                ((Battery)CrewChief.getEvent("Battery")).reportBatteryStatus(true);
                if (useVerboseResponses)
                {
                    ((Battery)CrewChief.getEvent("Battery")).reportExtendedBatteryStatus(true, false);
                }
            }
            else
            {
                Fuel.ReportFuelStatus(true, (CrewChief.currentGameState != null && CrewChief.currentGameState.SessionData.SessionType == SessionType.Race));
            }
        }
        public void playCornerNamesForCurrentLap()
        {
            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.phraseIfItHasBeenRecorded("acknowledge/play_corner_names"), 0));
            if (currentGameState == null && CrewChief.currentGameState != null)
                CrewChief.currentGameState.readLandmarksForThisLap = true;
            else if (currentGameState != null)
                currentGameState.readLandmarksForThisLap = true;
        }

        public void togglePaceNotesPlayback()
        {
            if (DriverTrainingService.isPlayingPaceNotes)
            {
                DriverTrainingService.stopPlayingPaceNotes();
                audioPlayer.playMessageOrFallback(DriverTrainingService.folderEndedPlayback, AudioPlayer.folderAcknowlegeOK);
            }
            else
            {
                if (CrewChief.currentGameState != null && CrewChief.currentGameState.SessionData.TrackDefinition != null)
                {
                    if (!DriverTrainingService.isPlayingPaceNotes)
                    {
                        if (DriverTrainingService.loadPaceNotes(CrewChief.gameDefinition.gameEnum,
                                CrewChief.currentGameState.SessionData.TrackDefinition.name, CrewChief.currentGameState.carClass.carClassEnum, audioPlayer))
                        {
                            audioPlayer.playMessageOrFallback(DriverTrainingService.folderStartedPlayback, AudioPlayer.folderAcknowlegeOK);
                        }
                        else
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                            Console.WriteLine("Attempted to start pace notes, but none are available for this circuit");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No track or car has been loaded - start an on-track session before loading a pace notes");
                }
            }
        }

        public void togglePaceNotesRecording()
        {
            if (DriverTrainingService.isRecordingPaceNotes)
            {
                DriverTrainingService.completeRecordingPaceNotes();
                audioPlayer.playMessageOrFallback(DriverTrainingService.folderEndedRecording, AudioPlayer.folderAcknowlegeOK);
            }
            else
            {
                if (CrewChief.trackName == null || CrewChief.trackName.Equals(""))
                {
                    Console.WriteLine("No track has been loaded - start an on-track session before recording pace notes");
                    return;
                }
                if (CrewChief.carClass == CarData.CarClassEnum.UNKNOWN_RACE || CrewChief.carClass == CarData.CarClassEnum.USER_CREATED)
                {
                    Console.WriteLine("No car class has been set - this pace notes session will not be class specific");
                }

                audioPlayer.playMessageOrFallback(DriverTrainingService.folderStartedRecording, AudioPlayer.folderAcknowlegeOK);
                DriverTrainingService.startRecordingPaceNotes(CrewChief.gameDefinition.gameEnum,
                    CrewChief.trackName, CrewChief.carClass);
            }
        }
        public void toggleTrackLandmarkRecording()
        {
            if (TrackLandMarksRecorder.isRecordingTrackLandmarks)
            {
                TrackLandMarksRecorder.completeRecordingTrackLandmarks();
            }
            else
            {
                if (CrewChief.trackName == null || CrewChief.trackName.Equals(""))
                {
                    Console.WriteLine("No track has been loaded - start an on-track session before recording landmarks");
                    return;
                }
                else
                {
                    TrackLandMarksRecorder.startRecordingTrackLandmarks(CrewChief.gameDefinition.gameEnum,
                    CrewChief.trackName, CrewChief.raceroomTrackId);
                }

            }
        }
        public void toggleAddTrackLandmark()
        {
            if (TrackLandMarksRecorder.isRecordingTrackLandmarks)
            {
                TrackLandMarksRecorder.addLandmark(CrewChief.distanceRoundTrack);
            }
        }
        // nasty... these triggers come from the speech recogniser or from button presses, and invoke speech
        // recognition 'respond' methods in the events
        public void getStatus()
        {
            CrewChief.HandleEvent("Penalties", SpeechRecogniser.STATUS[0]);
            CrewChief.HandleEvent("RaceTime", SpeechRecogniser.STATUS[0]);
            // Legacy: it didn't handle it // CrewChief.HandleEvent("Position", SpeechRecogniser.STATUS[0]);
            CrewChief.HandleEvent("PitStops", SpeechRecogniser.STATUS[0]);
            CrewChief.HandleEvent("DamageReporting", SpeechRecogniser.STATUS[0]);
            if (GlobalBehaviourSettings.enabledMessageTypes.Contains(MessageTypes.BATTERY))
            {
                CrewChief.HandleEvent("Battery", SpeechRecogniser.CAR_STATUS[0]);
            }
            else
            {
                CrewChief.HandleEvent("Fuel", SpeechRecogniser.CAR_STATUS[0]);
            }
            CrewChief.HandleEvent("TyreMonitor", SpeechRecogniser.STATUS[0]);
            CrewChief.HandleEvent("EngineMonitor", SpeechRecogniser.STATUS[0]);
            CrewChief.HandleEvent("Timings", SpeechRecogniser.STATUS[0]);
        }

        public void getSessionStatus()
        {
            CrewChief.HandleEvent("Penalties", SpeechRecogniser.SESSION_STATUS[0]);
            CrewChief.HandleEvent("RaceTime", SpeechRecogniser.SESSION_STATUS[0]);
            // Legacy: it didn't handle it // CrewChief.HandleEvent("Position", SpeechRecogniser.SESSION_STATUS[0]);
            CrewChief.HandleEvent("PitStops", SpeechRecogniser.SESSION_STATUS[0]);
            CrewChief.HandleEvent("Timings", SpeechRecogniser.SESSION_STATUS[0]);
        }

        public void getCarStatus()
        {
            CrewChief.HandleEvent("DamageReporting", SpeechRecogniser.CAR_STATUS[0]);
            if (GlobalBehaviourSettings.enabledMessageTypes.Contains(MessageTypes.BATTERY))
            {
                CrewChief.HandleEvent("Battery", SpeechRecogniser.CAR_STATUS[0]);
            }
            else
            {
                CrewChief.HandleEvent("Fuel", SpeechRecogniser.CAR_STATUS[0]);
            }
            CrewChief.HandleEvent("TyreMonitor", SpeechRecogniser.CAR_STATUS[0]);
            CrewChief.HandleEvent("EngineMonitor", SpeechRecogniser.CAR_STATUS[0]);
        }

        public void getDamageReport()
        {
            CrewChief.HandleEvent("DamageReporting", SpeechRecogniser.DAMAGE_REPORT[0]);
        }

        public void reportCurrentTime()
        {
            DateTime now = DateTime.Now;
            if (UserSettings.GetUserSettings().getBoolean("colloquial_time") &&
                SoundCache.SetUpTTS())
            {
                var ct = new ColloquialTime();
                string timeString = ct.Format(now);
                var fragment = MessageFragment.Text("It's " + timeString);
                fragment.allowTTS = true;
                AudioPlayer.ttsOption = AudioPlayer.TTS_OPTION.ANY_TIME;
                audioPlayer.playMessageImmediately(new QueuedMessage("current_time", 0,
                                messageFragments: MessageContents(fragment)));
            }
            else // read the time out numerically
            {
                const String folderAM = "alarm_clock/am";
                const String folderPM = "alarm_clock/pm";
                int hour = now.Hour;
                int minute = now.Minute;
                Boolean isPastMidDay = false;
                if (hour >= 12)
                {
                    isPastMidDay = true;
                }
                if ("it".Equals(AudioPlayer.soundPackLanguage, StringComparison.InvariantCultureIgnoreCase))
                {
                    audioPlayer.playMessageImmediately(new QueuedMessage("current_time", 0,
                        messageFragments: AbstractEvent.MessageContents(hour, NumberReaderIt2.folderAnd, now.Minute)));
                }
                else
                {
                    if (hour == 0)
                    {
                        isPastMidDay = false;
                        hour = 24;
                    }
                    if (hour > 12)
                    {
                        hour = hour - 12;
                    }
                    if (minute < 10)
                    {
                        if (minute == 0)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage("current_time", 0,
                               messageFragments: AbstractEvent.MessageContents(hour, isPastMidDay ? folderPM : folderAM)));
                        }
                        else
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage("current_time", 0,
                                messageFragments: AbstractEvent.MessageContents(hour, NumberReader.folderOh, now.Minute, isPastMidDay ? folderPM : folderAM)));
                        }
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("current_time", 0,
                            messageFragments: AbstractEvent.MessageContents(hour, now.Minute, isPastMidDay ? folderPM : folderAM)));
                    }
                }
            }
        }
    }
}
