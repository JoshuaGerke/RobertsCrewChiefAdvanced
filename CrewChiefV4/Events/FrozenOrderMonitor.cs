/*
 * This monitor announces order information during Full Course Yellow/Yellow (in NASCAR), Rolling start and Formation/standing starts.
 * 
 * Official website: thecrewchief.org 
 * License: MIT
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrewChiefV4.RaceRoom.RaceRoomData;
using System.Threading;
using CrewChiefV4.GameState;
using CrewChiefV4.Audio;
using CrewChiefV4.NumberProcessing;

namespace CrewChiefV4.Events
{
    class FrozenOrderMonitor : AbstractEvent
    {
        private const int ACTION_STABLE_THRESHOLD = 10;
        private const float DIST_TO_START_TO_ANNOUNCE_POS_REMINDER = 300.0f;

        private readonly bool useDriverName = UserSettings.GetUserSettings().getBoolean("iracing_fcy_formation_use_drivername");
        private readonly bool useDoubleFileVariation = UserSettings.GetUserSettings().getBoolean("formation_doublefile_variation");

        // Number of updates FO Action and Driver name were the same.
        private int numUpdatesActionSame = 0;
        private Int64 lastActionUpdateTicks = DateTime.MinValue.Ticks;

        // Last FO Action and Driver name announced.
        private FrozenOrderAction currFrozenOrderAction = FrozenOrderAction.None;
        private string currDriverToFollow = null;
        private FrozenOrderColumn currFrozenOrderColumn = FrozenOrderColumn.None;

        // Next FO Action and Driver to be announced if it stays stable for a ACTION_STABLE_THRESHOLD times.
        private FrozenOrderAction newFrozenOrderAction = FrozenOrderAction.None;
        private string newDriverToFollow = null;
        private FrozenOrderColumn newFrozenOrderColumn = FrozenOrderColumn.None;

        private bool formationStandingStartAnnounced = false;
        private bool formationStandingPreStartReminderAnnounced = false;
        private DateTime formationStandingStartAnnouncedAt = DateTime.MinValue;

        private bool scrLastFCYLapLaneAnnounced = false;
        private DateTime lastPlayedPaceCar = DateTime.MinValue;

        // sounds...
        public const string folderFollow = "frozen_order/follow";
        public const string folderInTheLeftColumn = "frozen_order/in_the_left_column";
        public const string folderInTheRightColumn = "frozen_order/in_the_right_column";
        public const string folderInTheInsideColumn = "frozen_order/in_the_inside_column";
        public const string folderInTheOutsideColumn = "frozen_order/in_the_outside_column";

        // for cases where we have no driver name:
        private const string folderLineUpInLeftColumn = "frozen_order/line_up_in_the_left_column";
        private const string folderLineUpInRightColumn = "frozen_order/line_up_in_the_right_column";
        private const string folderLineUpInInsideColumn = "frozen_order/line_up_in_the_inside_column";
        private const string folderLineUpInOutsideColumn = "frozen_order/line_up_in_the_outside_column";

        public const string folderCatchUpTo = "frozen_order/catch_up_to";    // can we have multiple phrasings of this without needing different structure?
        public const string folderAllow = "frozen_order/allow";
        public const string folderToPass = "frozen_order/to_pass";
        public const string folderTheSafetyCar = "frozen_order/the_safety_car";
        public const string folderThePaceCar = "frozen_order/the_pace_car";
        public const string folderYoureAheadOfAGuyYouShouldBeFollowing = "frozen_order/youre_ahead_of_guy_you_should_follow";
        public const string folderYouNeedToCatchUpToTheGuyAhead = "frozen_order/you_need_to_catch_up_to_the_guy_ahead";
        public const string folderAllowGuyBehindToPass = "frozen_order/allow_guy_behind_to_pass";

        // For car numbers
        public const string folderFollowCarNumber = "frozen_order/follow_car_number";
        public const string folderCatchUpToCarNumber = "frozen_order/catch_up_to_car_number";
        public const string folderAllowCarNumber = "frozen_order/allow_car_number";

        // single file
        public const string folderFCYLineUpSingleFile = "frozen_order/fcy_lineup_single_file";
        public const string folderLineUpSingleFileBehind = "frozen_order/line_up_single_file_behind";
        public const string folderLineUpSingleFileBehindCarNumber = "frozen_order/line_up_single_file_behind_car_number";
        public const string folderLineUpSingleFileBehindSafetyCarEU = "frozen_order/line_up_single_file_behind_safety_car_eu";
        public const string folderLineUpSingleFileBehindSafetyCarUS = "frozen_order/line_up_single_file_behind_safety_car_usa";


        public const string folderWeStartingFromPosition = "frozen_order/were_starting_from_position";
        public const string folderRow = "frozen_order/row";    // "starting from position 4, row 2 in the outside column" - uses column stuff above
        // we'll use the get-ready sound from the LapCounter event here
        public const string folderWereStartingFromPole = "frozen_order/were_starting_from_pole";
        public const string folderSafetyCarSpeedIs = "frozen_order/safety_car_speed_is";
        public const string folderPaceCarSpeedIs = "frozen_order/pace_car_speed_is";
        public const string folderMilesPerHour = "frozen_order/miles_per_hour";
        public const string folderKilometresPerHour = "frozen_order/kilometres_per_hour";

        // "just left" the racetrack
        public const string folderSafetyCarJustLeft = "frozen_order/safety_car_just_left";
        public const string folderPaceCarJustLeft = "frozen_order/pace_car_just_left";

        // "is out" meaning it is on track AND cars should follow it (this is not the same as joining the track)
        public const string folderSafetyCarIsOut = "frozen_order/safetycar_out_eu";
        public const string folderPaceCarIsOut = "frozen_order/safetycar_out_usa";

        public const string folderRollingStartReminder = "frozen_order/thats_a_rolling_start";
        public const string folderStandingStartReminder = "frozen_order/thats_a_standing_start";
        public const string folderStayInPole = "frozen_order/stay_in_pole";
        public const string folderStayInPoleInInsideColumn = "frozen_order/stay_in_pole_in_inside_column";
        public const string folderStayInPoleInOutsideColumn = "frozen_order/stay_in_pole_in_outside_column";
        public const string folderStayInPoleInLeftColumn = "frozen_order/stay_in_pole_in_left_column";
        public const string folderStayInPoleInRightColumn = "frozen_order/stay_in_pole_in_right_column";
        public const string folderMoveToPole = "frozen_order/move_to_pole";
        public const string folderMoveToPoleRow = "frozen_order/move_to_pole_row";
        public const string folderPassThePaceCar = "frozen_order/pass_the_pace_car";
        public const string folderPassTheSafetyCar = "frozen_order/pass_the_safety_car";

        // Validation stuff:
        private const string validateMessageTypeKey = "validateMessageTypeKey";
        private const string validateMessageTypeAction = "validateMessageTypeAction";
        private const string validationActionKey = "validationActionKey";
        private const string validationAssignedPositionKey = "validationAssignedPositionKey";
        private const string validationDriverToFollowKey = "validationDriverToFollowKey";

        private volatile bool updateRequested = false;

        public FrozenOrderMonitor(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
        }

        public override List<SessionType> applicableSessionTypes
        {
            get { return new List<SessionType> { SessionType.Race }; }
        }

        public override List<SessionPhase> applicableSessionPhases
        {
            get { return new List<SessionPhase> { SessionPhase.Formation, SessionPhase.Countdown, SessionPhase.FullCourseYellow }; }
        }

        /*
         * IMPORTANT: This method is called twice - when the message becomes due, and immediately before playing it (which may have a 
         * delay caused by the length of the queue at the time). So be *very* careful when checking and updating local state in here.
         */
        public override bool isMessageStillValid(string eventSubType, GameStateData currentGameState, Dictionary<String, Object> validationData)
        {
            if (base.isMessageStillValid(eventSubType, currentGameState, validationData))
            {
                if (currentGameState.PitData.InPitlane)
                    return false;

                if (validationData == null)
                    return true;

                // given that the delays are only a few seconds, and the data is coming directly from the server,
                // there is very little benefit from invalidating messages which are almost always still relevant
                // even if not 100% accurate (e.g. an "allow to pass" might change into a "stay behind").
                if (Game.IRACING)
                {
                    return true;
                }

                if ((string)validationData[FrozenOrderMonitor.validateMessageTypeKey] == FrozenOrderMonitor.validateMessageTypeAction)
                {
                    var queuedAction = (FrozenOrderAction)validationData[FrozenOrderMonitor.validationActionKey];
                    var queuedAssignedPosition = (int)validationData[FrozenOrderMonitor.validationAssignedPositionKey];
                    var queuedDriverToFollow = (string)validationData[FrozenOrderMonitor.validationDriverToFollowKey];
                    if (queuedAction == currentGameState.FrozenOrderData.Action
                        && queuedAssignedPosition == currentGameState.FrozenOrderData.AssignedPosition
                        && queuedDriverToFollow == currentGameState.FrozenOrderData.DriverToFollowRaw)
                        return true;
                    else
                    {
                        Console.WriteLine(string.Format("Frozen Order: message invalidated.  Was {0} {1} {2} is {3} {4} {5}", queuedAction, queuedAssignedPosition, queuedDriverToFollow,
                            currentGameState.FrozenOrderData.Action, currentGameState.FrozenOrderData.AssignedPosition, currentGameState.FrozenOrderData.DriverToFollowRaw));
                        return false;
                    }
                }
            }
            return false;
        }

        public override void clearState()
        {
            this.formationStandingStartAnnounced = false;
            this.formationStandingPreStartReminderAnnounced = false;
            this.formationStandingStartAnnouncedAt = DateTime.MinValue;
            this.numUpdatesActionSame = 0;
            this.newFrozenOrderAction = FrozenOrderAction.None;
            this.newDriverToFollow = null;
            this.newFrozenOrderColumn = FrozenOrderColumn.None;
            this.currFrozenOrderAction = FrozenOrderAction.None;
            this.currDriverToFollow = null;
            this.currFrozenOrderColumn = FrozenOrderColumn.None;
            this.scrLastFCYLapLaneAnnounced = false;
            this.lastActionUpdateTicks = DateTime.MinValue.Ticks;

            // for some bizarre reason, we clear the state when there is no frozen order phase
            // so we rely on resetting this every time the pace car is detected leaving the pits.
            // this.lastPlayedPaceCar = DateTime.MinValue;
            this.updateRequested = false;
        }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            bool force_update = updateRequested;
            updateRequested = false;

            var cgs = currentGameState;
            var pgs = previousGameState;
            if (!GlobalBehaviourSettings.enableFrozenOrderMessages
                || GameStateData.onManualFormationLap  // We may want manual formation to phase of FrozenOrder.
                || pgs == null
                || (cgs.SessionData.SessionPhase == SessionPhase.Countdown && !(Game.ACC || Game.IRACING)))
                return; // don't process if we've just started a session

            if ((Game.ACC || Game.IRACING) && !LapCounter.playedPreLightsMessage)
            {
                return;
            }

            var cfod = cgs.FrozenOrderData;
            var pfod = pgs.FrozenOrderData;

            var cfodp = cgs.FrozenOrderData.Phase;
            if (cfodp == FrozenOrderPhase.None)
                return;  // Nothing to do.

            var useAmericanTerms = GlobalBehaviourSettings.useAmericanTerms || GlobalBehaviourSettings.useOvalLogic;
            var useOvalLogic = GlobalBehaviourSettings.useOvalLogic;

            if (currentGameState.SessionData.SessionPhase == SessionPhase.FullCourseYellow
                && previousGameState != null && !previousGameState.SafetyCarData.isOnTrack && currentGameState.SafetyCarData.isOnTrack)
            {
                // this just removes the noise of "the pace car is out" followed almost immediately by "the pace car is in turn 1".
                lastPlayedPaceCar = currentGameState.Now;
                // this doesn't need its own message because "the pace car is out and pits are closed" typically plays
                // var pace_car = useAmericanTerms ? folderThePaceCar : folderTheSafetyCar;
                // audioPlayer.playMessage(new QueuedMessage("pace_car", 5, MessageContents(pace_car, WatchedOpponents.folderIsLeavingThePit), priority: 10));
            }
            if (currentGameState.SessionData.SessionPhase == SessionPhase.FullCourseYellow
                && currentGameState.SessionData.TrackDefinition.trackLandmarks.Count > 0
                && currentGameState.SafetyCarData.isOnTrack
                && currentGameState.SafetyCarData.DistanceRoundTrack > 0
                && currentGameState.FrozenOrderData.SafetyCarSpeed > 0
                && (currentGameState.PositionAndMotionData.CarSpeed > 1.5 * currentGameState.FrozenOrderData.SafetyCarSpeed
                    // we are interested if we are trying to catch the pace car or if we're in the pits (e.g. we are
                    // repairing minor damage) and want to know when it's coming back round again.
                    || currentGameState.PitData.InPitlane && currentGameState.PositionAndMotionData.CarSpeed == 0)
                && currentGameState.Now >= lastPlayedPaceCar.AddSeconds(15))
            {
                float p = currentGameState.SafetyCarData.DistanceRoundTrack;
                var found = currentGameState.SessionData.TrackDefinition.trackLandmarks.Find(lm => lm.distanceRoundLapStart <= p && p <= lm.distanceRoundLapEnd);
                if (found != null)
                {
                    lastPlayedPaceCar = currentGameState.Now;
                    // it would be better if the spotter said this, but we only have CrewChief recordings.
                    // it would also be good if we had a "safety car" variant.
                    audioPlayer.playMessage(new QueuedMessage("pace_car", 5, MessageContents(FlagsMonitor.folderPaceCarIsIn, "corners/" + found.landmarkName), priority: 10));
                }
            }

            if (cgs.PitData.InPitlane)
            {
                return;
            }

            if (pfod.Phase == FrozenOrderPhase.None)
            {
                Console.WriteLine("Frozen Order: New Phase detected: " + cfod.Phase);
                // we used to have randomness here, but it would mean that the message often arrives AFTER the information about where to line up,
                // which is just weird. Better to randomise the formation information.
                if (cfod.Phase == FrozenOrderPhase.Rolling && !currentGameState.FlagData.isFullCourseYellow && !Game.ACC /* ACC is always rolling start, no reminder needed */)
                    audioPlayer.playMessage(new QueuedMessage(folderRollingStartReminder, 4, abstractEvent: this, priority: 10));
                else if (cfod.Phase == FrozenOrderPhase.FormationStanding)
                {
                    audioPlayer.playMessageImmediately(new QueuedMessage(folderStandingStartReminder, 4, abstractEvent: this, priority: 10));
                }


                // Clear previous state.
                this.clearState();
            }

            // Because FO Action is distance dependent, it tends to fluctuate.
            // We need to detect when it stabilizes (values stay identical for ACTION_STABLE_THRESHOLD times).
            if (cfod.Action == pfod.Action
                && cfod.DriverToFollowRaw == pfod.DriverToFollowRaw
                && cfod.AssignedColumn == pfod.AssignedColumn)
                ++this.numUpdatesActionSame;
            else
            {
                this.numUpdatesActionSame = 0;
                this.lastActionUpdateTicks = cgs.Now.Ticks;
            }

            this.newFrozenOrderAction = cfod.Action;
            this.newDriverToFollow = cfod.DriverToFollowRaw;
            this.newFrozenOrderColumn = cfod.AssignedColumn;

            var isActionUpdateStable = !Game.GTR2
                ? this.numUpdatesActionSame >= FrozenOrderMonitor.ACTION_STABLE_THRESHOLD
                : TimeSpan.FromTicks(cgs.Now.Ticks - this.lastActionUpdateTicks).TotalMilliseconds > 500;  // GTR2 updates at ~2FPS.  So use ticks to detect stability.  Ticks is better in general, but I've no time to test rF2/iR for now.

            // Detect if we should be following SC, as SC has no driver name.
            var shouldFollowSafetyCar = false;
            var driverToFollow = "";
            var carNumberInt = -1;
            var carNumberString = "";
            var useCarNumber = false;
            if (cfodp == FrozenOrderPhase.Rolling || cfodp == FrozenOrderPhase.FullCourseYellow)
            {
                shouldFollowSafetyCar = (cfod.AssignedColumn == FrozenOrderColumn.None && cfod.AssignedPosition == 1)  // Single file order.
                    || (cfod.AssignedColumn != FrozenOrderColumn.None && (cfod.AssignedGridPosition == 1 || cfod.AssignedPosition > 0 && cfod.AssignedPosition <= 2));  // Double file (grid) order.

                useCarNumber = (Game.IRACING || Game.ACC
                                || (Game.GTR2 && !cgs.carClass.preferNameForOrderMessages))
                    && !shouldFollowSafetyCar && cfod.CarNumberToFollowRaw != "-1" && Int32.TryParse(cfod.CarNumberToFollowRaw, out carNumberInt)
                    && !(useDriverName && AudioPlayer.canReadName(cfod.DriverToFollowRaw, false));
                carNumberString = cfod.CarNumberToFollowRaw;
                driverToFollow = shouldFollowSafetyCar ? (useAmericanTerms ? folderThePaceCar : folderTheSafetyCar) : (useCarNumber ? cfod.CarNumberToFollowRaw : cfod.DriverToFollowRaw);
            }

            bool useVariation = cfod.AssignedColumn == FrozenOrderColumn.None || useDoubleFileVariation;

            if (cfodp == FrozenOrderPhase.Rolling
                && cfod.Action != FrozenOrderAction.None)
            {
                var prevDriverToFollow = this.currDriverToFollow;
                var prevFrozenOrderAction = this.currFrozenOrderAction;

                if (force_update)
                {
                    prevDriverToFollow = null;
                    prevFrozenOrderAction = FrozenOrderAction.None;
                }

                if (force_update || (isActionUpdateStable
                    && (this.currFrozenOrderAction != this.newFrozenOrderAction
                        || this.currDriverToFollow != this.newDriverToFollow
                        || this.currFrozenOrderColumn != this.newFrozenOrderColumn)))
                {
                    this.currFrozenOrderAction = this.newFrozenOrderAction;
                    this.currDriverToFollow = this.newDriverToFollow;
                    this.currFrozenOrderColumn = this.newFrozenOrderColumn;

                    // canReadDriverToFollow will be true if we're behind the safety car or we can read the driver's name:
                    var canReadDriverToFollow = shouldFollowSafetyCar || useCarNumber || AudioPlayer.canReadName(driverToFollow);

                    var usableDriverNameToFollow = (shouldFollowSafetyCar || useCarNumber) ? driverToFollow : DriverNameHelper.getUsableDriverName(driverToFollow);

                    var validationData = new Dictionary<string, object>();
                    validationData.Add(FrozenOrderMonitor.validateMessageTypeKey, FrozenOrderMonitor.validateMessageTypeAction);
                    validationData.Add(FrozenOrderMonitor.validationActionKey, cfod.Action);
                    validationData.Add(FrozenOrderMonitor.validationAssignedPositionKey, cfod.AssignedPosition);
                    validationData.Add(FrozenOrderMonitor.validationDriverToFollowKey, cfod.DriverToFollowRaw);

                    if (force_update)
                    {
                        validationData = null;
                    }

                    if (this.newFrozenOrderAction == FrozenOrderAction.Follow
                        && prevDriverToFollow != this.currDriverToFollow)  // Don't announce Follow messages for the driver that we caught up to or allowed to pass.
                    {
                        if (canReadDriverToFollow)
                        {
                            // Follow messages are only meaningful if there's name to announce.
                            int delay = force_update ? 0 : Utilities.random.Next(3, 6);

                            // For some extra randomsess, announce message without column info.
                            bool randomly_shorter = useVariation && Utilities.random.Next(1, 11) > 8;
                            if (cfod.AssignedColumn == FrozenOrderColumn.None || randomly_shorter)
                            {
                                if (!useCarNumber || shouldFollowSafetyCar)
                                    audioPlayer.playMessage(new QueuedMessage(!shouldFollowSafetyCar ? "frozen_order/follow_driver" : "frozen_order/follow_safety_car", delay + 6,
                                        secondsDelay: delay, messageFragments: MessageContents(folderFollow, usableDriverNameToFollow), abstractEvent: this,
                                        validationData: validationData, priority: 10));
                                else
                                    audioPlayer.playMessage(new QueuedMessage("frozen_order/follow_driver", 0,
                                        secondsDelay: 0, messageFragments:
                                        MessageContents(folderFollowCarNumber, new CarNumber(carNumberString)), abstractEvent: this,
                                        validationData: validationData, priority: 10));
                            }
                            else
                            {
                                string columnName;
                                if (useOvalLogic)
                                    columnName = cfod.AssignedColumn == FrozenOrderColumn.Left ? folderInTheInsideColumn : folderInTheOutsideColumn;
                                else
                                    columnName = cfod.AssignedColumn == FrozenOrderColumn.Left ? folderInTheLeftColumn : folderInTheRightColumn;
                                if (!useCarNumber || shouldFollowSafetyCar)
                                    audioPlayer.playMessage(new QueuedMessage(!shouldFollowSafetyCar ? "frozen_order/follow_driver_in_col" : "frozen_order/follow_safety_car_in_col", delay + 6,
                                        secondsDelay: delay, messageFragments: MessageContents(folderFollow, usableDriverNameToFollow, columnName), abstractEvent: this,
                                        validationData: validationData, priority: 10));
                                else
                                    audioPlayer.playMessage(new QueuedMessage("frozen_order/follow_driver_in_col", 0,
                                        secondsDelay: 0, messageFragments:
                                        MessageContents(folderFollowCarNumber, new CarNumber(carNumberString), columnName), abstractEvent: this,
                                        validationData: validationData, priority: 10));
                            }
                        }
                    }
                    else switch (this.newFrozenOrderAction)
                        {
                            case FrozenOrderAction.AllowToPass:
                                {
                                    // Follow messages are only meaningful if there's name to announce.
                                    int delay = force_update ? 0 : Utilities.random.Next(1, 4);

                                    // Randomly, announce message without name.
                                    bool randomly_shorter = useVariation && Utilities.random.Next(0, 11) > 1;
                                    if ((canReadDriverToFollow && randomly_shorter)
                                        || shouldFollowSafetyCar)  // Unless it is SC
                                    {
                                        if (!useCarNumber || shouldFollowSafetyCar)
                                            audioPlayer.playMessage(new QueuedMessage(!shouldFollowSafetyCar ? "frozen_order/allow_driver_to_pass" : "frozen_order/allow_safety_car_to_pass", delay + 6,
                                                secondsDelay: delay, messageFragments: MessageContents(folderAllow, usableDriverNameToFollow, folderToPass), abstractEvent: this,
                                                validationData: validationData, priority: 10));
                                        else
                                            audioPlayer.playMessage(new QueuedMessage("frozen_order/allow_driver_to_pass", delay + 6,
                                                secondsDelay: delay, messageFragments:
                                                MessageContents(folderAllowCarNumber, new CarNumber(carNumberString), folderToPass), abstractEvent: this,
                                                validationData: validationData, priority: 10));

                                    }
                                    else
                                        audioPlayer.playMessage(new QueuedMessage(folderYoureAheadOfAGuyYouShouldBeFollowing, delay + 6, secondsDelay: delay, abstractEvent: this,
                                            validationData: validationData, priority: 10));

                                    break;
                                }
                            case FrozenOrderAction.CatchUp:
                                {
                                    int delay = force_update ? 0 : Utilities.random.Next(1, 4);
                                    // Randomly, announce message without name.
                                    bool randomly_shorter = useVariation && Utilities.random.Next(0, 11) > 1;
                                    if ((canReadDriverToFollow && randomly_shorter)
                                        || shouldFollowSafetyCar)
                                    {
                                        if (!useCarNumber || shouldFollowSafetyCar)
                                            audioPlayer.playMessage(new QueuedMessage(!shouldFollowSafetyCar ? "frozen_order/catch_up_to_driver" : "frozen_order/catch_up_to_safety_car", delay + 6,
                                                secondsDelay: delay, messageFragments: MessageContents(folderCatchUpTo, usableDriverNameToFollow), abstractEvent: this,
                                                validationData: validationData, priority: 10));
                                        else
                                            audioPlayer.playMessage(new QueuedMessage("frozen_order/catch_up_to_driver", delay + 6,
                                                secondsDelay: delay, messageFragments:
                                                MessageContents(folderCatchUpToCarNumber, new CarNumber(carNumberString)), abstractEvent: this,
                                                validationData: validationData, priority: 10));
                                    }
                                    else
                                        audioPlayer.playMessage(new QueuedMessage(folderYouNeedToCatchUpToTheGuyAhead, delay + 6, secondsDelay: delay, abstractEvent: this,
                                            validationData: validationData, priority: 10));

                                    break;
                                }
                            default:
                                {
                                    if (this.newFrozenOrderAction == FrozenOrderAction.StayInPole
                                        && prevFrozenOrderAction != FrozenOrderAction.MoveToPole)  // No point in nagging user to stay in pole if we previously told them to move there.
                                    {
                                        int delay = force_update ? 0 : Utilities.random.Next(0, 3);
                                        // Randomly, announce message without column info.
                                        bool randomly_shorter = useVariation && Utilities.random.Next(1, 11) > 8;
                                        if (cfod.AssignedColumn == FrozenOrderColumn.None || randomly_shorter)
                                            audioPlayer.playMessage(new QueuedMessage("frozen_order/stay_in_pole", delay + 6, secondsDelay: delay,
                                                messageFragments: MessageContents(folderStayInPole), abstractEvent: this, validationData: validationData, priority: 10));
                                        else
                                        {
                                            string folderToPlay = null;
                                            if (useOvalLogic)
                                                folderToPlay = cfod.AssignedColumn == FrozenOrderColumn.Left ? folderStayInPoleInInsideColumn : folderStayInPoleInOutsideColumn;
                                            else
                                                folderToPlay = cfod.AssignedColumn == FrozenOrderColumn.Left ? folderStayInPoleInLeftColumn : folderStayInPoleInRightColumn;

                                            audioPlayer.playMessage(new QueuedMessage("frozen_order/stay_in_pole_in_column", delay + 6, secondsDelay: delay,
                                                messageFragments: MessageContents(folderToPlay), abstractEvent: this, validationData: validationData, priority: 10));
                                        }
                                    }
                                    else if (this.newFrozenOrderAction == FrozenOrderAction.MoveToPole)
                                    {
                                        int delay = force_update ? 0 : Utilities.random.Next(2, 5);
                                        if (cfod.AssignedColumn == FrozenOrderColumn.None)
                                            audioPlayer.playMessage(new QueuedMessage("frozen_order/move_to_pole", delay + 6, secondsDelay: delay,
                                                messageFragments: MessageContents(folderMoveToPole), abstractEvent: this,
                                                validationData: validationData, priority: 10));
                                        else
                                            audioPlayer.playMessage(new QueuedMessage("frozen_order/move_to_pole_row", delay + 6, secondsDelay: delay,
                                                messageFragments: MessageContents(folderMoveToPoleRow), abstractEvent: this,
                                                validationData: validationData, priority: 10));
                                    }

                                    break;
                                }
                        }
                }
            }
            else if (Game.IRACING && pfod.Phase == FrozenOrderPhase.None && cfod.Phase == FrozenOrderPhase.FullCourseYellow)
            {
                // in iRacing the pace car leaves the pits like a normal car, so we can say when it is on track
                var prevDriverToFollow = this.currDriverToFollow;
                var prevFrozenOrderColumn = this.currFrozenOrderColumn;
                this.currFrozenOrderAction = this.newFrozenOrderAction;
                this.currDriverToFollow = this.newDriverToFollow;
                this.currFrozenOrderColumn = this.newFrozenOrderColumn;

                var canReadDriverToFollow = shouldFollowSafetyCar || useCarNumber || AudioPlayer.canReadName(driverToFollow);

                var validationData = new Dictionary<string, object>();
                validationData.Add(FrozenOrderMonitor.validateMessageTypeKey, FrozenOrderMonitor.validateMessageTypeAction);
                validationData.Add(FrozenOrderMonitor.validationActionKey, cfod.Action);
                validationData.Add(FrozenOrderMonitor.validationAssignedPositionKey, cfod.AssignedPosition);
                validationData.Add(FrozenOrderMonitor.validationDriverToFollowKey, cfod.DriverToFollowRaw);

                int delay = Utilities.random.Next(0, 3);
                if (shouldFollowSafetyCar)
                {
                    audioPlayer.playMessage(new QueuedMessage("frozen_order/lineup_single_file_follow_safety_car",
                        delay + 6, secondsDelay: delay, messageFragments: MessageContents(useAmericanTerms ? folderPaceCarIsOut : folderSafetyCarIsOut, useAmericanTerms ? folderLineUpSingleFileBehindSafetyCarUS : folderLineUpSingleFileBehindSafetyCarEU),
                        abstractEvent: this, validationData: validationData, priority: 10));
                }
                else if (useCarNumber)
                {
                    audioPlayer.playMessage(new QueuedMessage("frozen_order/lineup_single_file_follow_car_number",
                        delay + 6, secondsDelay: delay, messageFragments:
                        MessageContents(useAmericanTerms ? folderPaceCarIsOut : folderSafetyCarIsOut, folderLineUpSingleFileBehindCarNumber, new CarNumber(carNumberString)),
                        abstractEvent: this, validationData: validationData, priority: 10));

                }
                else if (canReadDriverToFollow)
                {
                    var usableDriverNameToFollow = shouldFollowSafetyCar || useCarNumber ? driverToFollow : (driverToFollow != null ? DriverNameHelper.getUsableDriverName(driverToFollow) : null);
                    audioPlayer.playMessage(new QueuedMessage("frozen_order/lineup_single_file_follow",
                        delay + 6, secondsDelay: delay, messageFragments: MessageContents(useAmericanTerms ? folderPaceCarIsOut : folderSafetyCarIsOut, folderLineUpSingleFileBehind, usableDriverNameToFollow),
                        abstractEvent: this, validationData: validationData, priority: 10));
                }
                else
                    audioPlayer.playMessage(new QueuedMessage(folderPaceCarIsOut, 10, abstractEvent: this));
            }
            else if (cfodp == FrozenOrderPhase.FullCourseYellow
                && cfod.Action != FrozenOrderAction.None)
            {
                var prevDriverToFollow = this.currDriverToFollow;
                var prevFrozenOrderColumn = this.currFrozenOrderColumn;
                if (force_update)
                {
                    prevDriverToFollow = null;
                    prevFrozenOrderColumn = FrozenOrderColumn.None;
                }

                var announceSCRLastFCYLapLane = useAmericanTerms
                    && currentGameState.StockCarRulesData.stockCarRulesEnabled
                    && (currentGameState.FlagData.fcyPhase == FullCourseYellowPhase.LAST_LAP_NEXT || currentGameState.FlagData.fcyPhase == FullCourseYellowPhase.LAST_LAP_CURRENT);

                if (force_update || (isActionUpdateStable
                    && (this.currFrozenOrderAction != this.newFrozenOrderAction
                        || this.currDriverToFollow != this.newDriverToFollow
                        || this.currFrozenOrderColumn != this.newFrozenOrderColumn
                        || announceSCRLastFCYLapLane && !this.scrLastFCYLapLaneAnnounced)))
                {
                    this.currFrozenOrderAction = this.newFrozenOrderAction;
                    this.currDriverToFollow = this.newDriverToFollow;
                    this.currFrozenOrderColumn = this.newFrozenOrderColumn;

                    this.scrLastFCYLapLaneAnnounced = announceSCRLastFCYLapLane;

                    // canReadDriverToFollow will be true if we're behind the safety car or we can read the driver's name:
                    var canReadDriverToFollow = shouldFollowSafetyCar || useCarNumber || AudioPlayer.canReadName(driverToFollow);

                    var usableDriverNameToFollow = shouldFollowSafetyCar || useCarNumber ? driverToFollow : (driverToFollow != null ? DriverNameHelper.getUsableDriverName(driverToFollow) : null);

                    var validationData = new Dictionary<string, object>();
                    validationData.Add(FrozenOrderMonitor.validateMessageTypeKey, FrozenOrderMonitor.validateMessageTypeAction);
                    validationData.Add(FrozenOrderMonitor.validationActionKey, cfod.Action);
                    validationData.Add(FrozenOrderMonitor.validationAssignedPositionKey, cfod.AssignedPosition);
                    validationData.Add(FrozenOrderMonitor.validationDriverToFollowKey, cfod.DriverToFollowRaw);
                    if (force_update)
                    {
                        validationData = null;
                    }

                    if (this.newFrozenOrderAction == FrozenOrderAction.Follow
                        && ((prevDriverToFollow != this.currDriverToFollow)  // Don't announce Follow messages for the driver that we caught up to or allowed to pass.
                            || (prevFrozenOrderColumn != this.currFrozenOrderColumn && announceSCRLastFCYLapLane)))  // But announce for SCR last FCY lap.
                    {
                        string columnName;
                        if (useOvalLogic)
                            columnName = cfod.AssignedColumn == FrozenOrderColumn.Left ? folderInTheInsideColumn : folderInTheOutsideColumn;
                        else
                            columnName = cfod.AssignedColumn == FrozenOrderColumn.Left ? folderInTheLeftColumn : folderInTheRightColumn;

                        int delay = force_update ? 0 : Utilities.random.Next(0, 3);
                        if (canReadDriverToFollow)
                        {
                            if (!useCarNumber || shouldFollowSafetyCar)
                            {
                                if (announceSCRLastFCYLapLane && cfod.AssignedColumn == FrozenOrderColumn.Left)
                                    audioPlayer.playMessage(new QueuedMessage(!shouldFollowSafetyCar ? "frozen_order/follow_driver_in_left" : "frozen_order/follow_safety_car_in_left",
                                        delay + 6, secondsDelay: delay, messageFragments: MessageContents(folderFollow, usableDriverNameToFollow, columnName), abstractEvent: this, validationData: validationData, priority: 10));
                                else if (announceSCRLastFCYLapLane && cfod.AssignedColumn == FrozenOrderColumn.Right)
                                    audioPlayer.playMessage(new QueuedMessage(!shouldFollowSafetyCar ? "frozen_order/follow_driver_in_right" : "frozen_order/follow_safety_car_in_right",
                                        delay + 6, secondsDelay: delay,
                                        messageFragments: MessageContents(folderFollow, usableDriverNameToFollow, columnName),
                                        abstractEvent: this, validationData: validationData, priority: 10));
                                else
                                    audioPlayer.playMessage(new QueuedMessage(!shouldFollowSafetyCar ? "frozen_order/follow_driver" : "frozen_order/follow_safety_car", delay + 6,
                                        secondsDelay: delay, messageFragments: MessageContents(folderFollow, usableDriverNameToFollow), abstractEvent: this, validationData: validationData, priority: 10));
                            }
                            else
                            {
                                if (announceSCRLastFCYLapLane && cfod.AssignedColumn == FrozenOrderColumn.Left)
                                    audioPlayer.playMessage(new QueuedMessage("frozen_order/follow_driver_in_left",
                                        delay + 6, secondsDelay: delay, messageFragments: MessageContents(folderFollowCarNumber, new CarNumber(carNumberString), columnName),
                                        abstractEvent: this, validationData: validationData, priority: 10));
                                else if (announceSCRLastFCYLapLane && cfod.AssignedColumn == FrozenOrderColumn.Right)
                                    audioPlayer.playMessage(new QueuedMessage("frozen_order/follow_driver_in_right",
                                        delay + 6, secondsDelay: delay,
                                        messageFragments: MessageContents(folderFollowCarNumber, new CarNumber(carNumberString), columnName),
                                        abstractEvent: this, validationData: validationData, priority: 10));
                                else
                                    audioPlayer.playMessage(new QueuedMessage("frozen_order/follow_driver", delay + 6,
                                        secondsDelay: delay, messageFragments: MessageContents(folderFollowCarNumber, new CarNumber(carNumberString)),
                                        abstractEvent: this, validationData: validationData, priority: 10));
                            }
                        }
                        else if (announceSCRLastFCYLapLane && cfod.AssignedColumn == FrozenOrderColumn.Left)
                            audioPlayer.playMessage(new QueuedMessage(columnName, delay + 6, secondsDelay: delay,
                                abstractEvent: this, validationData: validationData, priority: 10));
                        else if (announceSCRLastFCYLapLane && cfod.AssignedColumn == FrozenOrderColumn.Right)
                            audioPlayer.playMessage(new QueuedMessage(columnName, delay + 6, secondsDelay: delay,
                                abstractEvent: this, validationData: validationData, priority: 10));
                    }
                    else switch (this.newFrozenOrderAction)
                        {
                            case FrozenOrderAction.AllowToPass:
                                {
                                    int delay = force_update ? 0 : Utilities.random.Next(1, 4);
                                    // Randomly, announce message without name.
                                    bool randomly_shorter = useVariation && Utilities.random.Next(0, 11) > 1;
                                    if ((canReadDriverToFollow && randomly_shorter)
                                        || shouldFollowSafetyCar)
                                    {
                                        if (!useCarNumber || shouldFollowSafetyCar)
                                            audioPlayer.playMessage(new QueuedMessage(!shouldFollowSafetyCar ? "frozen_order/allow_driver_to_pass" : "frozen_order/allow_safety_car_to_pass",
                                                delay + 6, secondsDelay: delay, messageFragments: MessageContents(folderAllow, usableDriverNameToFollow, folderToPass),
                                                abstractEvent: this, validationData: validationData, priority: 10));
                                        else
                                            audioPlayer.playMessage(new QueuedMessage("frozen_order/allow_driver_to_pass",
                                                delay + 6, secondsDelay: delay, messageFragments:
                                                MessageContents(folderAllowCarNumber, new CarNumber(carNumberString), folderToPass),
                                                abstractEvent: this, validationData: validationData, priority: 10));
                                    }
                                    else
                                        audioPlayer.playMessage(new QueuedMessage(folderYoureAheadOfAGuyYouShouldBeFollowing, delay + 6, secondsDelay: delay, abstractEvent: this,
                                            validationData: validationData, priority: 10));

                                    break;
                                }
                            case FrozenOrderAction.CatchUp:
                                {
                                    int delay = force_update ? 0 : Utilities.random.Next(1, 4);
                                    // Randomly, announce message without name.
                                    bool randomly_shorter = useVariation && Utilities.random.Next(0, 11) > 1;
                                    if (canReadDriverToFollow && randomly_shorter
                                        || shouldFollowSafetyCar)
                                    {
                                        if (!useCarNumber || shouldFollowSafetyCar)
                                            audioPlayer.playMessage(new QueuedMessage(!shouldFollowSafetyCar ? "frozen_order/catch_up_to_driver" : "frozen_order/catch_up_to_safety_car",
                                                delay + 6, secondsDelay: delay, messageFragments: MessageContents(folderCatchUpTo, usableDriverNameToFollow),
                                                abstractEvent: this, validationData: validationData, priority: 10));
                                        else
                                            audioPlayer.playMessage(new QueuedMessage("frozen_order/catch_up_to_driver",
                                                delay + 6, secondsDelay: delay, messageFragments:
                                                MessageContents(folderCatchUpToCarNumber, new CarNumber(carNumberString)),
                                                abstractEvent: this, validationData: validationData, priority: 10));
                                    }
                                    else
                                        audioPlayer.playMessage(new QueuedMessage(folderYouNeedToCatchUpToTheGuyAhead, delay + 6, secondsDelay: delay, abstractEvent: this,
                                            validationData: validationData, priority: 10));

                                    break;
                                }
                            case FrozenOrderAction.PassSafetyCar:
                                {
                                    int delay = force_update ? 0 : Utilities.random.Next(1, 4);
                                    audioPlayer.playMessage(new QueuedMessage(useAmericanTerms ? folderPassThePaceCar : folderPassTheSafetyCar, delay + 6, secondsDelay: delay, abstractEvent: this,
                                        validationData: validationData, priority: 10));
                                    break;
                                }
                        }
                }
            }
            else if (cfodp == FrozenOrderPhase.FormationStanding
                && cfod.Action != FrozenOrderAction.None)
            {
                string columnName = null;
                if (cfod.AssignedColumn != FrozenOrderColumn.None)
                {
                    if (useOvalLogic)
                        columnName = cfod.AssignedColumn == FrozenOrderColumn.Left ? folderInTheInsideColumn : folderInTheOutsideColumn;
                    else
                        columnName = cfod.AssignedColumn == FrozenOrderColumn.Left ? folderInTheLeftColumn : folderInTheRightColumn;
                }
                if (!this.formationStandingStartAnnounced && cgs.SessionData.SessionRunningTime > 10)
                {
                    this.formationStandingStartAnnounced = true;
                    this.formationStandingStartAnnouncedAt = cgs.Now;
                    var isStartingFromPole = cfod.AssignedPosition == 1;
                    int delay = force_update ? 0 : Utilities.random.Next(0, 3);
                    if (isStartingFromPole)
                    {
                        if (columnName == null)
                            audioPlayer.playMessage(new QueuedMessage(folderWereStartingFromPole, 6, abstractEvent: this));
                        else
                            audioPlayer.playMessage(new QueuedMessage("frozen_order/youre_starting_from_pole_in_column", delay + 6, secondsDelay: delay,
                                    messageFragments: MessageContents(folderWereStartingFromPole, columnName), abstractEvent: this, priority: 10));
                    }
                    else
                    {
                        if (columnName == null)
                            audioPlayer.playMessage(new QueuedMessage("frozen_order/youre_starting_from_pos", delay + 6, secondsDelay: delay,
                                    messageFragments: MessageContents(folderWeStartingFromPosition, cfod.AssignedPosition), abstractEvent: this, priority: 10));
                        else
                            audioPlayer.playMessage(new QueuedMessage("frozen_order/youre_starting_from_pos_row_in_column", delay + 6, secondsDelay: delay,
                                    messageFragments: MessageContents(folderWeStartingFromPosition, cfod.AssignedPosition, folderRow, cfod.AssignedGridPosition, columnName),
                                    abstractEvent: this, priority: 10));
                    }
                }

                if (!this.formationStandingPreStartReminderAnnounced
                    && this.formationStandingStartAnnouncedAt != DateTime.MinValue
                    && (cgs.Now - this.formationStandingStartAnnouncedAt).TotalSeconds >= 30.0
                    && cgs.SessionData.SectorNumber == 3
                    && cgs.PositionAndMotionData.DistanceRoundTrack > (cgs.SessionData.TrackDefinition.trackLength - FrozenOrderMonitor.DIST_TO_START_TO_ANNOUNCE_POS_REMINDER))
                {
                    this.formationStandingPreStartReminderAnnounced = true;
                    var isStartingFromPole = cfod.AssignedPosition == 1;
                    int delay = force_update ? 0 : Utilities.random.Next(0, 3);
                    if (isStartingFromPole)
                    {
                        if (columnName == null)
                            audioPlayer.playMessage(new QueuedMessage("frozen_order/get_ready_starting_from_pole", delay + 6, secondsDelay: delay,
                                    messageFragments: MessageContents(LapCounter.folderGetReady, folderWereStartingFromPole), abstractEvent: this, priority: 10));
                        else
                            audioPlayer.playMessage(new QueuedMessage("frozen_order/get_ready_starting_from_pole_in_column", delay + 6, secondsDelay: delay,
                                    messageFragments: MessageContents(LapCounter.folderGetReady, folderWereStartingFromPole, columnName), abstractEvent: this, priority: 10));
                    }
                    else
                    {
                        if (columnName == null)
                            audioPlayer.playMessage(new QueuedMessage("frozen_order/get_ready_youre_starting_from_pos", delay + 6, secondsDelay: delay,
                                    messageFragments: MessageContents(LapCounter.folderGetReady, folderWeStartingFromPosition, cfod.AssignedPosition), abstractEvent: this, priority: 10));
                        else
                            audioPlayer.playMessage(new QueuedMessage("frozen_order/get_ready_youre_starting_from_pos_row_in_column", delay + 6, secondsDelay: delay,
                                    messageFragments: MessageContents(LapCounter.folderGetReady, folderWeStartingFromPosition, cfod.AssignedPosition, folderRow, cfod.AssignedGridPosition, columnName),
                                    abstractEvent: this, priority: 10));
                    }
                }
            }

            // Announce SC speed.
            if (pfod.SafetyCarSpeed == -1.0f && cfod.SafetyCarSpeed != -1.0f)
            {
                int delay = Utilities.random.Next(10, 16);
                // by delaying the message we get a better reading in games where the pace car needs to get up to speed
                DelayedMessage callback = delegate (GameStateData futureGameState, List<MessageFragment> messageFragments, List<MessageFragment> alt)
                {
                    if (futureGameState.FrozenOrderData.SafetyCarSpeed <= 10) { return; }

                    var kmPerHour = futureGameState.FrozenOrderData.SafetyCarSpeed * 3.6f;
                    if (!GlobalBehaviourSettings.useMetric)
                    {
                        messageFragments.Add(MessageFragment.Text(FrozenOrderMonitor.folderPaceCarSpeedIs));
                        var milesPerHour = kmPerHour * 0.621371f;
                        messageFragments.Add(MessageFragment.Integer((int)Math.Round(milesPerHour), false));
                        messageFragments.Add(MessageFragment.Text(FrozenOrderMonitor.folderMilesPerHour));
                    }
                    else
                    {
                        messageFragments.Add(MessageFragment.Text(FrozenOrderMonitor.folderSafetyCarSpeedIs));
                        messageFragments.Add(MessageFragment.Integer((int)Math.Round(kmPerHour), false));
                        messageFragments.Add(MessageFragment.Text(FrozenOrderMonitor.folderKilometresPerHour));
                    }
                };

                audioPlayer.playMessage(new QueuedMessage("frozen_order/pace_car_speed", delay + 6, secondsDelay: delay, delayedMessage: callback, priority: 10));
            }

            // Announce SC left.
            if ((pfod.SafetyCarSpeed != -1.0f && cfod.SafetyCarSpeed == -1.0f) ||
                (cgs.SafetyCarData.fcySafetyCarCallsEnabled && (cfodp == FrozenOrderPhase.FullCourseYellow) && pgs.SafetyCarData.isOnTrack && !cgs.SafetyCarData.isOnTrack))
            {
                // it would be good if there was a way to detect when the pace car pulls off the track to enter the pit road but that's
                // unfortunately still considered "on-track" in iRacing. Because as soon as the pace car leaves the racing surface, it's
                // fair game to start the race.
                if (useAmericanTerms)
                    audioPlayer.playMessage(new QueuedMessage(folderPaceCarJustLeft, 10, abstractEvent: this));
                else
                    audioPlayer.playMessage(new QueuedMessage(folderSafetyCarJustLeft, 10, abstractEvent: this));
            }

            // For fast rolling, do nothing for now.
        }

        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.WHERE_SHOULD_I_LINE_UP
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
                case SpeechCommands.ID.WHERE_SHOULD_I_LINE_UP:
                    // ideally we would calculate the information and play it immediately,
                    // but that would involve a large refactor of the code above, so this
                    // is the best we can do without a rewrite. Unfortunately this will
                    // sometimes have the effect of Jim seemingly ignoring us.
                    this.updateRequested = true;
                    Log.Debug(() => $"player requested formation information, hopefully we tell them {CrewChief.currentGameState.FrozenOrderData}");
                    break;

                default:
                    break;
            }
        }
    }
}