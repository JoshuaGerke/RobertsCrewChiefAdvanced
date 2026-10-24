using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CrewChiefV4.Events
{
    class OvertakingAidsMonitor : AbstractEvent
    {        
        public static String folderAFewTenthsOffDRSRange = "overtaking_aids/a_few_tenths_off_drs_range";
        public static String folderASecondOffDRSRange = "overtaking_aids/a_second_off_drs_range";
        public static String folderActivationsRemaining = "overtaking_aids/activations_remaining";
        public static String folderDontForgetDRS = "overtaking_aids/dont_forget_drs"; 
        public static String folderGuyBehindHasDRS = "overtaking_aids/guy_behind_has_drs";
        public static String folderDontForgetKERS = "overtaking_aids/remember_to_use_kers";
        public static String folderPushToPassNowAvailable = "overtaking_aids/push_to_pass_now_available";
        public static String folderDRSEnabled = "overtaking_aids/drs_enabled";
        public static String folderDRSDisabled = "overtaking_aids/drs_disabled";
        public static String folderDRSActivationsRemaining = "overtaking_aids/drs_activations_remaining";
        public static String folderOneDRSActivationsRemaining = "overtaking_aids/one_drs_activation_remaining";
        public static String folderNoDRSActivationsRemaining = "overtaking_aids/no_drs_activations_remaining";

        // for PtP:
        public static String folderNoActivationsRemaining = "overtaking_aids/no_activations_remaining";
        public static String folderOneActivationRemaining = "overtaking_aids/one_activation_remaining";
        public static String folderTenPtPActivationsRemaining = "overtaking_aids/ten_ptp_activations_remaining";
        public static String folderFivePtPActivationsRemaining = "overtaking_aids/five_ptp_activations_remaining";
        public static String folderThreePtPActivationsRemaining = "overtaking_aids/three_ptp_activations_remaining";
        public static String folderUsePtPReminder = "overtaking_aids/remember_to_use_ptp";

        private Boolean hasUsedDrsOnThisLap = false;    // Note that DTM 2015 experience has 3 DRS activations per lap - only moans if we've used none of them
        private Boolean drsAvailableOnThisLap = false;
        private float trackDistanceToCheckDRSGapFrontAt = -1;

        private Boolean hasUsedKersOnThisLap = false;
        private Boolean kersAvailableOnThisLap = false;

        private Boolean playedGetCloserForDRSOnThisLap = false;
        private Boolean playedOpponentHasDRSOnThisLap = false;

        private readonly Boolean drsMessagesEnabled = UserSettings.GetUserSettings().getBoolean("enable_drs_messages");
        private readonly Boolean ptpMessagesEnabled = UserSettings.GetUserSettings().getBoolean("enable_push_to_pass_messages");
        private readonly Boolean drsBeepsEnabled = UserSettings.GetUserSettings().getBoolean("enable_drs_beeps");

        private bool ptpHasCooldown = false;

        private bool hasUsedPtPOnThisLap = false;
        private bool hasRemindedPlayerToUsePtP = false;

        private DateTime nextDRSActivationsRemainingReminderTime = DateTime.MinValue;

        // DTM years that had limited DRS engagements per race had one hour race duration.  Try announcing
        // automatically twice per event.
        private readonly TimeSpan nextDRSActivationsReminderRemainingMinInterval = TimeSpan.FromSeconds(60 * 19);

        public override List<SessionType> applicableSessionTypes
        {
            get { return new List<SessionType> {
                SessionType.Race,
                // Only for DRS Beep
                SessionType.Practice, SessionType.Qualify, SessionType.PrivateQualify, SessionType.LonePractice
            }; }
        }

        public OvertakingAidsMonitor(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
        }

        public override void clearState()
        {
            this.hasUsedDrsOnThisLap = false;
            this.hasUsedKersOnThisLap = false;
            this.hasUsedPtPOnThisLap = false;
            this.hasRemindedPlayerToUsePtP = false;
            this.drsAvailableOnThisLap = false;
            this.kersAvailableOnThisLap = false;
            this.trackDistanceToCheckDRSGapFrontAt = -1;
            this.playedOpponentHasDRSOnThisLap = false;
            this.playedGetCloserForDRSOnThisLap = false;
            this.ptpHasCooldown = false;
        }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            if (GameStateData.onManualFormationLap
                || currentGameState.PitData.InPitlane
                || previousGameState == null
                || Game.ACC || Game.LMU) // ACC and LMU don't have DRS or Push to Pass
            {
                return;
            }

            // For all session types
            if (drsBeepsEnabled && previousGameState != null)
            {
                // State transitions:
                // *players passes detection point, game determines DRS will be available*
                // DETECTED [detected_bleep]
                // *crossing DRS start zone*
                // AVAILABLE [available_bleep]
                // *player enables DRS*
                // ENGAGED
                // *player lifts throttle or brakes while still in DRS zone*
                // DETECTED
                // *player presses throttle again while still in DRS zone*
                // AVAILABLE [available_bleep]
                // *player leaves DRS zone*
                if (!previousGameState.OvertakingAids.DrsDetected && !previousGameState.OvertakingAids.DrsEngaged &&
                    currentGameState.OvertakingAids.DrsDetected)
                {
                    audioPlayer.getSoundCache().Play("drs_detected_bleep", SoundMetadata.beep);
                }
                if (!previousGameState.OvertakingAids.DrsAvailable && currentGameState.OvertakingAids.DrsAvailable)
                {
                    audioPlayer.getSoundCache().Play("drs_available_bleep", SoundMetadata.beep);
                }
            }

            // DRS messages are for race only.
            if (currentGameState.SessionData.SessionType != SessionType.Race)
            {
                return;
            }
            if (drsMessagesEnabled && previousGameState != null)
            {
                if (!previousGameState.OvertakingAids.DrsEnabled && currentGameState.OvertakingAids.DrsEnabled)
                {
                    audioPlayer.playMessage(new QueuedMessage(folderDRSEnabled, 3, abstractEvent: this, priority: 10));
                    updateDRSActivationRemainingReminderTime(currentGameState);
                }
                else if (previousGameState.OvertakingAids.DrsEnabled && !currentGameState.OvertakingAids.DrsEnabled)
                {
                    audioPlayer.playMessage(new QueuedMessage(folderDRSDisabled, 3, abstractEvent: this, priority: 10));
                }
            }

            // DRS:
            if (drsMessagesEnabled && currentGameState.OvertakingAids.DrsEnabled && currentGameState.OvertakingAids.DrsRange != -1)
            {
                if (trackDistanceToCheckDRSGapFrontAt == -1 && currentGameState.SessionData.TrackDefinition != null)
                {
                    trackDistanceToCheckDRSGapFrontAt = currentGameState.SessionData.TrackDefinition.trackLength / 2;
                }
                if (currentGameState.SessionData.IsNewLap)
                {
                    if (drsAvailableOnThisLap && !hasUsedDrsOnThisLap)
                    {
                        audioPlayer.playMessage(new QueuedMessage("missed_available_drs", 10, 
                            messageFragments: MessageContents(folderDontForgetDRS), abstractEvent: this, priority: 0));
                    }
                    if (kersAvailableOnThisLap && !hasUsedKersOnThisLap)
                    {
                        if (Utilities.random.NextDouble() > 0.2)
                        {
                            audioPlayer.playMessage(new QueuedMessage("missed_available_kers", 10,
                                messageFragments: MessageContents(folderDontForgetKERS), abstractEvent: this, priority: 0));
                        }
                    }
                    drsAvailableOnThisLap = currentGameState.OvertakingAids.DrsAvailable;
                    hasUsedDrsOnThisLap = false;
                    playedGetCloserForDRSOnThisLap = false;
                    playedOpponentHasDRSOnThisLap = false;
                    kersAvailableOnThisLap = currentGameState.OvertakingAids.KersAvailable;
                    hasUsedKersOnThisLap = false;
                }
                if (currentGameState.OvertakingAids.DrsAvailable)
                {
                    drsAvailableOnThisLap = true;
                }
                if (currentGameState.OvertakingAids.DrsEngaged)
                {
                    hasUsedDrsOnThisLap = true;
                }
                if (!hasUsedDrsOnThisLap && !drsAvailableOnThisLap && !playedGetCloserForDRSOnThisLap &&
                    currentGameState.PositionAndMotionData.DistanceRoundTrack > trackDistanceToCheckDRSGapFrontAt)
                {
                    if (currentGameState.SessionData.TimeDeltaFront < 1.3 + currentGameState.OvertakingAids.DrsRange &&
                        currentGameState.SessionData.TimeDeltaFront >= 0.6 + currentGameState.OvertakingAids.DrsRange &&
                        currentGameState.OvertakingAids.DrsActivationsRemaining != 0)
                    {
                        if (ImmediateOpponentIsValidForDRSMessage(currentGameState, true /*inFront*/))
                        {
                            audioPlayer.playMessage(new QueuedMessage("drs_a_second_out_of_range", 3,
                                messageFragments: MessageContents(folderASecondOffDRSRange), abstractEvent: this, priority: 10));
                            playedGetCloserForDRSOnThisLap = true;
                        }
                    }
                    else if (currentGameState.SessionData.TimeDeltaFront < 0.6 + currentGameState.OvertakingAids.DrsRange &&
                        currentGameState.SessionData.TimeDeltaFront >= 0.1 + currentGameState.OvertakingAids.DrsRange &&
                        currentGameState.OvertakingAids.DrsActivationsRemaining != 0)
                    {
                        if (ImmediateOpponentIsValidForDRSMessage(currentGameState, true /*inFront*/))
                        {
                            audioPlayer.playMessage(new QueuedMessage("drs_a_few_tenths_out_of_range", 3,
                                messageFragments:  MessageContents(folderAFewTenthsOffDRSRange), abstractEvent: this, priority: 10));
                            playedGetCloserForDRSOnThisLap = true;
                        }
                    }
                }
                if (currentGameState.OvertakingAids.KersAvailable)
                {
                    kersAvailableOnThisLap = true;
                }
                if (currentGameState.OvertakingAids.KersEngaged)
                {
                    hasUsedKersOnThisLap = true;
                }
                if (!playedOpponentHasDRSOnThisLap && currentGameState.SessionData.TimeDeltaBehind <= currentGameState.OvertakingAids.DrsRange &&
                    currentGameState.SessionData.LapTimeCurrent > currentGameState.SessionData.TimeDeltaBehind &&
                    currentGameState.SessionData.LapTimeCurrent < currentGameState.SessionData.TimeDeltaBehind + 1 &&
                    currentGameState.OvertakingAids.DrsAvailable)
                {
                    playedOpponentHasDRSOnThisLap = true;
                    if (Utilities.random.NextDouble() >= 0.4)
                    {
                        if (ImmediateOpponentIsValidForDRSMessage(currentGameState, false /*inFront*/))
                        {
                            audioPlayer.playMessage(new QueuedMessage(folderGuyBehindHasDRS, 3, abstractEvent: this, priority: 10));
                        }
                    }
                }
                if (currentGameState.SessionData.SessionType == SessionType.Race
                    && currentGameState.OvertakingAids.DrsActivationsRemaining != -1
                    && (currentGameState.Now > this.nextDRSActivationsRemainingReminderTime
                        || (currentGameState.OvertakingAids.DrsActivationsRemaining == 0 
                            && previousGameState.OvertakingAids.DrsActivationsRemaining == 1))) // Announce running out of activations immediately.
                {
                    announceDRSActivationsRemaining(srResponse: false);
                    updateDRSActivationRemainingReminderTime(currentGameState);
                }
            }

            // push to pass
            if (!ptpHasCooldown)
            {
                // this is reset at the start of the session so will be true for the whole session if we ever have a non-zero wait time
                ptpHasCooldown = currentGameState.OvertakingAids.PushToPassWaitTimeLeft > 0;
            }
            if (ptpMessagesEnabled && currentGameState.OvertakingAids.PushToPassActivationsRemaining != -1)
            {
                if (previousGameState.OvertakingAids.PushToPassEngaged && !currentGameState.OvertakingAids.PushToPassEngaged &&
                    currentGameState.OvertakingAids.PushToPassActivationsRemaining == 0)
                {
                    audioPlayer.playMessage(new QueuedMessage(folderNoActivationsRemaining, 10, abstractEvent: this, priority: 5));
                }
                else if (ptpHasCooldown && previousGameState.OvertakingAids.PushToPassWaitTimeLeft > 0 && currentGameState.OvertakingAids.PushToPassWaitTimeLeft == 0)
                {
                    // if we've reached the end of the cooldown phase (when it exists), so warn about the availability and the remaining count
                    if (currentGameState.OvertakingAids.PushToPassActivationsRemaining == 1)
                    {
                        audioPlayer.playMessage(new QueuedMessage("one_push_to_pass_remaining", 10, 
                            messageFragments: MessageContents(folderPushToPassNowAvailable, folderOneActivationRemaining), abstractEvent: this, priority: 7));
                    }
                    else if (currentGameState.OvertakingAids.PushToPassActivationsRemaining > 0)
                    {
                        audioPlayer.playMessage(new QueuedMessage("push_to_pass_remaining", 5, 
                            messageFragments: MessageContents(folderPushToPassNowAvailable, currentGameState.OvertakingAids.PushToPassActivationsRemaining, folderActivationsRemaining), 
                            abstractEvent: this, priority: 2));
                    }
                }
                else if (!ptpHasCooldown && previousGameState.OvertakingAids.PushToPassEngaged && !currentGameState.OvertakingAids.PushToPassEngaged)
                {
                    switch (currentGameState.OvertakingAids.PushToPassActivationsRemaining)
                    {
                        // ptp has just stopped, warn about the remaining count
                        case 1:
                            audioPlayer.playMessage(new QueuedMessage("one_push_to_pass_remaining", 10,
                                messageFragments: MessageContents(folderOneActivationRemaining), abstractEvent: this));
                            break;
                        case 10:
                            audioPlayer.playMessage(new QueuedMessage("push_to_pass_remaining", 5,
                                messageFragments: MessageContents(folderTenPtPActivationsRemaining),
                                abstractEvent: this));
                            break;
                        case 5:
                            audioPlayer.playMessage(new QueuedMessage("push_to_pass_remaining", 5,
                                messageFragments: MessageContents(folderFivePtPActivationsRemaining),
                                abstractEvent: this));
                            break;
                        case 3:
                            audioPlayer.playMessage(new QueuedMessage("push_to_pass_remaining", 5,
                                messageFragments: MessageContents(folderThreePtPActivationsRemaining),
                                abstractEvent: this));
                            break;
                    }
                }
                // check if the player is using their PtP allocation properly - only applies to DTM 2020
                if (!hasRemindedPlayerToUsePtP && currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.carClass.carClassEnum == CarData.CarClassEnum.DTM_2020)
                {
                    if (currentGameState.SessionData.IsNewLap)
                    {
                        if (!hasUsedPtPOnThisLap && currentGameState.SessionData.CompletedLaps > 2 
                            && ((currentGameState.SessionData.SessionHasFixedTime && currentGameState.SessionData.PlayerLapTimeSessionBest > 0) || !currentGameState.SessionData.SessionHasFixedTime))
                        {
                            // we didn't use PtP on the previous lap, perhaps the player needs a reminder
                            int lapsRemaining;
                            if (currentGameState.SessionData.SessionHasFixedTime)
                            {
                                // need to estimate the remaining laps
                                lapsRemaining = (int) Math.Floor(currentGameState.SessionData.SessionTimeRemaining / currentGameState.SessionData.PlayerLapTimeSessionBest) + 1;
                            }
                            else
                            {
                                lapsRemaining = currentGameState.SessionData.SessionNumberOfLaps - currentGameState.SessionData.CompletedLaps;
                            }
                            if (currentGameState.OvertakingAids.PushToPassActivationsRemaining >= lapsRemaining)
                            {
                                audioPlayer.playMessage(new QueuedMessage(folderUsePtPReminder, 3, abstractEvent: this));
                                hasRemindedPlayerToUsePtP = true;
                            }
                        }
                        hasUsedPtPOnThisLap = false;
                    }
                    if (currentGameState.OvertakingAids.PushToPassEngaged)
                    {
                        hasUsedPtPOnThisLap = true;
                    }
                }
            }
        }

        private void updateDRSActivationRemainingReminderTime(GameStateData currentGameState)
        {
            if (currentGameState.OvertakingAids.DrsActivationsRemaining > 0)
            {
                this.nextDRSActivationsRemainingReminderTime = currentGameState.Now
                    + this.nextDRSActivationsReminderRemainingMinInterval
                    + TimeSpan.FromSeconds(Utilities.random.Next(0, 180));
            }
            else
            {
                // Don't announce "no more DRS activations left" multiple times.
                this.nextDRSActivationsRemainingReminderTime = DateTime.MaxValue;
            }
        }

        private bool ImmediateOpponentIsValidForDRSMessage(GameStateData currentGameState, bool inFront)
        {
            string opponentKey = inFront ? currentGameState.getOpponentKeyInFront(currentGameState.carClass) : currentGameState.getOpponentKeyBehind(currentGameState.carClass);
            OpponentData opponent;
            return opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out opponent) &&
                opponent != null && !opponent.isEnteringPits() && !opponent.isOnOutLap()/*TODO: change for correct impl*/ && !opponent.InPits && !opponent.isApporchingPits;
        }

        /// <summary>
        /// Respond to voice command
        /// </summary>
        /// <param name="voiceMessage">
        /// HOW_MANY_DRS_ACTIVATIONS_LEFT
        /// </param>
        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.HOW_MANY_DRS_ACTIVATIONS_LEFT
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
            if (!Commands.Contains(cmd))
            {
                Log.Error($"{cmd.ToString()} not handled by {this.GetType().Name}");
                return;
            }
            if (CrewChief.currentGameState != null
                && CrewChief.currentGameState.SessionData.SessionType == SessionType.Race)
            {
                if (cmd == SpeechCommands.ID.HOW_MANY_DRS_ACTIVATIONS_LEFT
                    && CrewChief.currentGameState.OvertakingAids.DrsActivationsRemaining != -1) // This only makes sense if ruleset has the limit.
                {
                    if (!CrewChief.currentGameState.OvertakingAids.DrsEnabled)
                    {
                        audioPlayer.playMessage(new QueuedMessage(folderDRSDisabled, 3, abstractEvent: this, priority: 10));
                    }
                    else
                    {
                        announceDRSActivationsRemaining(srResponse:true);
                    }

                    return;
                }
            }

            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
        }

        private void announceDRSActivationsRemaining(bool srResponse)
        {
            Debug.Assert(CrewChief.currentGameState.OvertakingAids.DrsActivationsRemaining != -1);

            if (CrewChief.currentGameState.OvertakingAids.DrsActivationsRemaining > 0)
            {
                audioPlayer.playMessage(new QueuedMessage("drs_activations_remaining", 10,
                    messageFragments: MessageContents(CrewChief.currentGameState.OvertakingAids.DrsActivationsRemaining,
                    CrewChief.currentGameState.OvertakingAids.DrsActivationsRemaining != 1 ? folderDRSActivationsRemaining : folderOneDRSActivationsRemaining)));
            }
            else if (CrewChief.currentGameState.OvertakingAids.DrsActivationsRemaining == 0)
            {
                audioPlayer.playMessage(new QueuedMessage(folderNoDRSActivationsRemaining, expiresAfter: 10,
                    alternateMessageFragments: null,
                    secondsDelay: srResponse ? 0 : 5));
            }
        }
    }
}
