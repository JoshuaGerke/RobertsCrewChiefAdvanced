using System;
using System.Collections.Generic;

using CrewChiefV4.Audio;
using CrewChiefV4.Events.PubSub;
using CrewChiefV4.GameState;
using CrewChiefV4.NumberProcessing;
using CrewChiefV4.R3E;

namespace CrewChiefV4.Events
{
    class Opponents : AbstractEvent
    {
        private const String validationDriverAheadKey = "validationDriverAheadKey";
        private const String validationNewLeaderKey = "validationNewLeaderKey";


        public const String folderCarNumber = "opponents/car_number";
        private const String folderLeaderIsPitting = "opponents/the_leader_is_pitting";
        private const String folderCarAheadIsPitting = "opponents/the_car_ahead_is_pitting";
        private const String folderCarBehindIsPitting = "opponents/the_car_behind_is_pitting";

        public const String folderTheLeader = "opponents/the_leader";
        public const String folderIsPitting = "opponents/is_pitting";
        private const String folderAheadIsPitting = "opponents/ahead_is_pitting";
        private const String folderBehindIsPitting = "opponents/behind_is_pitting";

        private const String folderIsNowOn = "opponents/is_now_on";

        private const String folderLeaderHasJustDoneA = "opponents/the_leader_has_just_done_a";
        private const String folderTheCarAheadHasJustDoneA = "opponents/the_car_ahead_has_just_done_a";
        private const String folderTheCarBehindHasJustDoneA = "opponents/the_car_behind_has_just_done_a";
        public const String folderNewFastestLapFor = "opponents/new_fastest_lap_for";

        public const String folderOneLapBehind = "opponents/one_lap_behind";
        public const String folderOneLapAhead = "opponents/one_lap_ahead";

        private const String folderIsNowLeading = "opponents/is_now_leading";
        private const String folderNextCarIs = "opponents/next_car_is";

        private const String folderCantPronounceName = "opponents/cant_pronounce_name";

        private const String folderWeAre = "opponents/we_are";

        // optional intro for opponent position (not used in English)
        private const String folderOpponentPositionIntro = "position/opponent_position_intro";

        private const String folderHasJustRetired = "opponents/has_just_retired";
        private const String folderHasJustBeenDisqualified = "opponents/has_just_been_disqualified";

        private const String folderRatingIntro = "opponents/rating_intro";
        private const String folderReputationIntro = "opponents/reputation_intro";

        private const int maxSecondsAheadToReportOpponent = 30; // If way behind don't tell player about the car in front unless prompted

        private readonly int frequencyOfOpponentRaceLapTimes = UserSettings.GetUserSettings().getInt("frequency_of_opponent_race_lap_times");
        private readonly int frequencyOfOpponentPracticeAndQualLapTimes = UserSettings.GetUserSettings().getInt("frequency_of_opponent_practice_and_qual_lap_times");

        private readonly int secondsToAnnounceNextCar = UserSettings.GetUserSettings().getInt("seconds_to_announce_next_car");

        private static readonly bool allowNumberAfterName = UserSettings.GetUserSettings().getBoolean("opponents_number_after_name");

        private static readonly bool opponentRatingInfo = UserSettings.GetUserSettings().getBoolean("enable_opponent_rating_info");

        private float minImprovementBeforeReadingOpponentRaceTime;
        private float maxOffPaceBeforeReadingOpponentRaceTime;

        private GameStateData currentGameState;

        private DateTime nextLeadChangeMessage = DateTime.MinValue;

        private DateTime nextCarAheadChangeMessage = DateTime.MinValue;
        private DateTime nextCarOnTrackChangeMessage = DateTime.MinValue;
        private string lastCarAheadAnnounced = null;

        private const string positionIsPlayerKey = null;

        // single set here because we never want to announce a DQ and a retirement for the same guy
        private HashSet<String> announcedRetirementsAndDQs = new HashSet<String>();

        // this prevents us from bouncing between 'next car is...' messages:
        private Dictionary<string, DateTime> onlyAnnounceOpponentAfter = new Dictionary<string, DateTime>();
        private TimeSpan waitBeforeAnnouncingSameOpponentAhead = TimeSpan.FromMinutes(UserSettings.GetUserSettings().getInt("minutes_to_announce_same_next_car"));
        private DateTime timeToPollOpponentAhead = DateTime.MinValue;

        private String lastLeaderAnnounced = null;
        private volatile String lastDriverAheadOnTrack = null;

        private int minSecondsBetweenOpponentTyreChangeCalls = 10;
        private int maxSecondsBetweenOpponentTyreChangeCalls = 20;
        private DateTime suppressOpponentTyreChangeUntil = DateTime.MinValue;

        // time and lap
        private Dictionary<string, (DateTime, int)> lastPitted = new Dictionary<string, (DateTime, int)>();


        public Opponents(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
            maxOffPaceBeforeReadingOpponentRaceTime = (float)frequencyOfOpponentRaceLapTimes / 10f;
            minImprovementBeforeReadingOpponentRaceTime = (1f - maxOffPaceBeforeReadingOpponentRaceTime) / 5f;
            EventBus.Subscribe(GameEvent.SectorChanged, _announceIfLappedCarAhead);
        }

        public override List<SessionType> applicableSessionTypes
        {
            get { return new List<SessionType> { SessionType.Practice, SessionType.Qualify, SessionType.Race }; }
        }

        // allow this event to trigger for FCY, but only the retired and DQ'ed checks:
        public override List<SessionPhase> applicableSessionPhases
        {
            get { return new List<SessionPhase> { SessionPhase.Green, SessionPhase.Countdown, SessionPhase.FullCourseYellow, SessionPhase.Formation }; }
        }

        public override void clearState()
        {
            currentGameState = null;
            nextLeadChangeMessage = DateTime.MinValue;
            nextCarAheadChangeMessage = DateTime.MinValue;
            nextCarOnTrackChangeMessage = DateTime.MinValue;
            announcedRetirementsAndDQs.Clear();
            onlyAnnounceOpponentAfter.Clear();
            lastLeaderAnnounced = null;
            lastDriverAheadOnTrack = null;
            lastCarAheadAnnounced = null;
            timeToPollOpponentAhead = DateTime.MinValue;
            lastPitted.Clear();
        }

        public override bool isMessageStillValid(string eventSubType, GameStateData currentGameState, Dictionary<String, Object> validationData)
        {
            if (base.isMessageStillValid(eventSubType, currentGameState, validationData))
            {
                if (validationData != null)
                {
                    object validationValue = null;
                    if (validationData.TryGetValue(validationDriverAheadKey, out validationValue))
                    {
                        String expectedOpponentName = (String)validationValue;
                        OpponentData opponentInFront = currentGameState.SessionData.ClassPosition > 1 ?
                            currentGameState.getOpponentAtClassPosition(currentGameState.SessionData.ClassPosition - 1, currentGameState.carClass) : null;
                        String actualOpponentName = opponentInFront == null ? null : opponentInFront.DriverRawName;
                        if (actualOpponentName != expectedOpponentName)
                        {
                            if (actualOpponentName != null && expectedOpponentName != null)
                            {
                                Console.WriteLine("New car in front message for opponent " + expectedOpponentName +
                                    " no longer valid - driver in front is now " + actualOpponentName);
                            }
                            return false;
                        }
                        else if (opponentInFront != null && (opponentInFront.InPits || opponentInFront.isEnteringPits()))
                        {
                            Console.WriteLine("New car in front message for opponent " + expectedOpponentName +
                                " no longer valid - driver is " + (opponentInFront.InPits ? "in pits" : "is entering the pits"));
                        }
                    }
                    else if (validationData.TryGetValue(validationNewLeaderKey, out validationValue))
                    {
                        String expectedLeaderName = (String)validationValue;
                        if (currentGameState.SessionData.ClassPosition == 1)
                        {
                            Console.WriteLine("New leader message for opponent " + expectedLeaderName +
                                    " no longer valid - player is now leader");
                            return false;
                        }
                        OpponentData actualLeader = currentGameState.getOpponentAtClassPosition(1, currentGameState.carClass);
                        String actualLeaderName = actualLeader == null ? null : actualLeader.DriverRawName;
                        if (actualLeaderName != expectedLeaderName)
                        {
                            if (actualLeaderName != null && expectedLeaderName != null)
                            {
                                Console.WriteLine("New leader message for opponent " + expectedLeaderName +
                                    " no longer valid - leader is now " + actualLeaderName);
                            }
                            return false;
                        }
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private Object getOpponentIdentifierForTyreChange(OpponentData opponentData, int playerRacePosition)
        {
            // leader
            int positionToCheck;
            if (opponentData.PositionOnApproachToPitEntry > 0)
            {
                positionToCheck = opponentData.PositionOnApproachToPitEntry;
            }
            else
            {
                // fallback if the PositionOnApproachToPitEntry isn't set - shouldn't really happen
                positionToCheck = opponentData.ClassPosition;
            }
            if (positionToCheck == 1)
            {
                return folderTheLeader;
            }
            // 2nd, 3rd, or within 2 positions of the player
            if ((positionToCheck > 1 && positionToCheck <= 3) ||
                (playerRacePosition - 2 <= positionToCheck && playerRacePosition + 2 >= positionToCheck))
            {
                if (opponentData.CanUseName && AudioPlayer.canReadName(opponentData.DriverRawName, false))
                {
                    return opponentData;
                }
                else
                {
                    return Position.folderStub + positionToCheck;
                }
            }
            return null;
        }

        bool racing;  // shorthand for currentGameState.SessionData.SessionType == SessionType.Race

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            this.currentGameState = currentGameState;
            racing = currentGameState.SessionData.SessionType == SessionType.Race;
            if (GameStateData.onManualFormationLap ||
                (racing && currentGameState.SessionData.SessionPhase < SessionPhase.Green))
            {
                return;
            }

            foreach (KeyValuePair<string, OpponentData> entry in currentGameState.OpponentData)
            {
                OpponentData opponent = entry.Value;
                if (opponent.InPits)
                {
                    lastPitted[entry.Key] = (currentGameState.Now, opponent.CompletedLaps + 1);
                }
            }

            // skip the lap time checks and stuff under yellow:
            if (currentGameState.SessionData.SessionPhase != SessionPhase.FullCourseYellow)
            {
                if (nextCarAheadChangeMessage == DateTime.MinValue)
                {
                    // this effectively means to delay the message at the start of the race
                    nextCarAheadChangeMessage = currentGameState.Now.Add(TimeSpan.FromSeconds(30));
                    nextCarOnTrackChangeMessage = nextCarAheadChangeMessage;
                }
                if (nextLeadChangeMessage == DateTime.MinValue)
                {
                    nextLeadChangeMessage = currentGameState.Now.Add(TimeSpan.FromSeconds(30));
                }
                if (currentGameState.SessionData.SessionType != SessionType.Race || frequencyOfOpponentRaceLapTimes > 0)
                {
                    foreach (KeyValuePair<string, OpponentData> entry in currentGameState.OpponentData)
                    {
                        string opponentKey = entry.Key;
                        OpponentData opponentData = entry.Value;
                        if (!CarData.IsCarClassEqual(opponentData.CarClass, currentGameState.carClass))
                        {
                            // not interested in opponents from other classes
                            continue;
                        }

                        _announceAnyStuffAboutOpponent(currentGameState, opponentData, opponentKey);
                    }
                }
            }

            // allow the retired and DQ checks under yellow:
            if (racing &&
                ((currentGameState.SessionData.SessionHasFixedTime && currentGameState.SessionData.SessionTimeRemaining > 0) ||
                 (!currentGameState.SessionData.SessionHasFixedTime && currentGameState.SessionData.SessionLapsRemaining > 0)))
            {
                // don't bother processing retired and DQ'ed drivers and position changes if we're not allowed to use the names:
                if (CrewChief.enableDriverNames)
                {
                    _announceAnyRetiredDrivers(currentGameState);
                    _announceAnyDisqualifiedDrivers(currentGameState);
                    // skip the position change checks under yellow:
                    if (currentGameState.SessionData.SessionPhase != SessionPhase.FullCourseYellow)
                    {
                        bool __updateDue = currentGameState.SessionData.ClassPosition > 2 &&            // 3rd or worse ???
                                           !currentGameState.PitData.InPitlane &&                       // and not in pits
                                           (!currentGameState.SessionData.IsRacingSameCarInFront ||     // and (opponent has changed
                                            previousGameState.PitData.InPitlane ||                      // or just left pits
                                            currentGameState.Now > timeToPollOpponentAhead);            // or due to update anyway)
                        if (__updateDue)
                        {
                            _updateDue(currentGameState);
                        }
                        _announceIfLeadChanged(previousGameState, currentGameState);
                    }
                }

                HashSet<string> announcedPitters = new HashSet<string>();
                announcedPitters.Add(_announceIfLeaderPitting(currentGameState));

                announcedPitters.Add(_announceIfCarAheadPitting(currentGameState));

                announcedPitters.Add(_announceIfCarBehindPitting(currentGameState));

                _announceIfOpponentAheadPitting(currentGameState, announcedPitters);

                _announceIfOpponentBehindPitting(currentGameState, announcedPitters);
            }
        }

        private void _announceAnyStuffAboutOpponent(GameStateData currentGameState, OpponentData opponentData, string opponentKey)
        {
            // in race sessions, announce tyre type changes once the session is underway
            if (racing &&
                currentGameState.SessionData.SessionRunningTime > 30 && 
                opponentData.hasJustChangedToDifferentTyreType && 
                currentGameState.Now > suppressOpponentTyreChangeUntil)
            {
                // this may be a race position or an OpponentData object
                Object opponentIdentifier = getOpponentIdentifierForTyreChange(opponentData, currentGameState.SessionData.ClassPosition);
                if (opponentIdentifier != null)
                {
                    suppressOpponentTyreChangeUntil = currentGameState.Now.AddSeconds(Utilities.random.Next(minSecondsBetweenOpponentTyreChangeCalls, maxSecondsBetweenOpponentTyreChangeCalls));
                    audioPlayer.playMessage(new QueuedMessage("opponent_tyre_change_" + opponentIdentifier.ToString(), 20,
                        messageFragments: MessageContents(opponentIdentifier, folderIsNowOn, TyreMonitor.getFolderForTyreType(opponentData.CurrentTyres)),
                        abstractEvent: this, priority: 5));
                }
            }

            bool __justDoneAtLeast2laps = opponentData.IsNewLap && opponentData.LastLapTime > 0 && 
                                          opponentData.OpponentLapData.Count > 1;
            bool __validLap = opponentData.LastLapValid && opponentData.CurrentBestLapTime > 0;

            if (__justDoneAtLeast2laps && __validLap && 
                !WatchedOpponents.watchedOpponentKeys.Contains(opponentKey) /* to avoid duplicate messages*/)
            {
                float currentFastestLap;
                if (currentGameState.SessionData.PlayerLapTimeSessionBest == -1)
                {
                    currentFastestLap = currentGameState.SessionData.OpponentsLapTimeSessionBestOverall;
                }
                else if (currentGameState.SessionData.OpponentsLapTimeSessionBestOverall == -1)
                {
                    currentFastestLap = currentGameState.SessionData.PlayerLapTimeSessionBest;
                }
                else
                {
                    currentFastestLap = Math.Min(currentGameState.SessionData.PlayerLapTimeSessionBest, currentGameState.SessionData.OpponentsLapTimeSessionBestOverall);
                }

                // this opponent has just completed a lap - do we need to report it? if it's fast overall and more than
                // a tenth quicker then his previous best we do...
                bool __racedAtLeast3laps = racing && opponentData.CompletedLaps > 2;
                bool __what_____ = !PitStops.waitingForMandatoryStopTimer && 
                                currentGameState.SessionData.SessionType != SessionType.Race &&
                                opponentData.CompletedLaps > 1;
                bool __canBeNamed = opponentData.CanUseName && AudioPlayer.canReadName(opponentData.DriverRawName, false);

                bool __realImprovement = opponentData.LastLapTime <= opponentData.CurrentBestLapTime &&
                                        opponentData.LastLapTime < opponentData.PreviousBestLapTime - minImprovementBeforeReadingOpponentRaceTime &&
                                        opponentData.LastLapTime < currentFastestLap + maxOffPaceBeforeReadingOpponentRaceTime;
                bool __bestPracQuallyTime = (currentGameState.SessionData.SessionType == SessionType.Practice || 
                                             currentGameState.SessionData.SessionType == SessionType.Qualify) && 
                                            opponentData.LastLapTime <= opponentData.CurrentBestLapTime;
                if ((__racedAtLeast3laps || __what_____) && 
                    opponentData.LastLapTime <= currentFastestLap &&
                    __canBeNamed)
                {
                    if ((racing && frequencyOfOpponentRaceLapTimes > 0) ||
                        (!racing && frequencyOfOpponentPracticeAndQualLapTimes > 0))
                    {
                        audioPlayer.playMessage(new QueuedMessage("new_fastest_lap", 5,
                            messageFragments: MessageContents(folderNewFastestLapFor, opponentData, 
                                TimeSpanWrapper.FromSeconds(opponentData.LastLapTime, Precision.AUTO_LAPTIMES)), abstractEvent: this, priority: 3));
                    }
                }
                else if ((racing && __realImprovement) || __bestPracQuallyTime)
                {
                    if (currentGameState.SessionData.ClassPosition > 1 && opponentData.ClassPosition == 1 &&
                        (racing || frequencyOfOpponentPracticeAndQualLapTimes != 0))
                    {
                        // he's leading, and has recorded 3 or more laps, and this one's his fastest
                        Console.WriteLine("Leader fast lap - this lap time = " + opponentData.LastLapTime + " session best = " + currentFastestLap);
                        audioPlayer.playMessage(new QueuedMessage("leader_good_laptime", 5,
                            messageFragments: MessageContents(folderLeaderHasJustDoneA, TimeSpanWrapper.FromSeconds(opponentData.LastLapTime, Precision.AUTO_LAPTIMES)),
                            abstractEvent: this, priority: 3));
                    }
                    else if (currentGameState.SessionData.ClassPosition > 1 && opponentData.ClassPosition == currentGameState.SessionData.ClassPosition - 1 &&
                             (racing || Utilities.random.Next(10) < frequencyOfOpponentPracticeAndQualLapTimes))
                    {
                        // he's ahead of us, and has recorded 3 or more laps, and this one's his fastest
                        Console.WriteLine("Car ahead fast lap - this lap time = " + opponentData.LastLapTime + " session best = " + currentFastestLap);
                        audioPlayer.playMessage(new QueuedMessage("car_ahead_good_laptime", 5,
                            messageFragments: MessageContents(folderTheCarAheadHasJustDoneA, TimeSpanWrapper.FromSeconds(opponentData.LastLapTime, Precision.AUTO_LAPTIMES)),
                            abstractEvent: this, priority: 0));
                    }
                    else if (!currentGameState.isLast() && opponentData.ClassPosition == currentGameState.SessionData.ClassPosition + 1 &&
                             (racing || Utilities.random.Next(10) < frequencyOfOpponentPracticeAndQualLapTimes))
                    {
                        // he's behind us, and has recorded 3 or more laps, and this one's his fastest
                        Console.WriteLine("Car behind fast lap - this lap time = " + opponentData.LastLapTime + " session best = " + currentFastestLap);
                        audioPlayer.playMessage(new QueuedMessage("car_behind_good_laptime", 5,
                            messageFragments: MessageContents(folderTheCarBehindHasJustDoneA, TimeSpanWrapper.FromSeconds(opponentData.LastLapTime, Precision.AUTO_LAPTIMES)),
                            abstractEvent: this, priority: 0));
                    }
                }
            }
        }

        private void _announceAnyRetiredDrivers(GameStateData currentGameState)
        {
            foreach (var entry in currentGameState.retriedDriverNames)
            {
                String retiredDriver = entry.Key;
                if (!announcedRetirementsAndDQs.Contains(retiredDriver))
                {
                    announcedRetirementsAndDQs.Add(retiredDriver);
                    if ((Game.RF1 || Game.RF2_LMU)
                        && currentGameState.SessionData.SessionPhase != SessionPhase.Green
                        && currentGameState.SessionData.SessionPhase != SessionPhase.FullCourseYellow
                        && currentGameState.SessionData.SessionPhase != SessionPhase.Checkered)
                    {
                        // In an offline session of the ISI games it is possible to select more AI drivers than a track can handle.
                        // The ones that don't fit on a track are marked as DNF before session goes Green.  Don't announce those.
                        continue;
                    }
                    var msg = new List<MessageFragment>();
                    if (AudioPlayer.canReadName(retiredDriver, false))
                    {
                        msg.Add(MessageFragment.Text(DriverNameHelper.getUsableDriverName(retiredDriver)));
                    }
                    else if (entry.Value != null && !entry.Value.Equals("-1"))
                    {
                        msg.Add(MessageFragment.Text(Opponents.folderCarNumber));
                        msg.AddRange(new CarNumber(entry.Value).getMessageFragments());
                    }
                    if (msg.Count > 0)
                    {
                        msg.AddRange(MessageContents(folderHasJustRetired));
                        audioPlayer.playMessage(new QueuedMessage("retirement", 10, msg, abstractEvent: this));
                    }
                }
            }
        }

        private void _announceAnyDisqualifiedDrivers(GameStateData currentGameState)
        {
            foreach (var entry in currentGameState.disqualifiedDriverNames)
            {
                var dqDriver = entry.Key;
                if (!announcedRetirementsAndDQs.Contains(dqDriver))
                {
                    announcedRetirementsAndDQs.Add(dqDriver);
                    var msg = new List<MessageFragment>();
                    if (AudioPlayer.canReadName(dqDriver, false))
                    {
                        msg.Add(MessageFragment.Text(DriverNameHelper.getUsableDriverName(dqDriver)));
                    }
                    else if (entry.Value != null && !entry.Value.Equals("-1"))
                    {
                        msg.Add(MessageFragment.Text(Opponents.folderCarNumber));
                        msg.AddRange(new CarNumber(entry.Value).getMessageFragments());
                    }
                    if (msg.Count > 0)
                    {
                        msg.AddRange(MessageContents(folderHasJustBeenDisqualified));
                        audioPlayer.playMessage(new QueuedMessage("disqualified", 10, msg, abstractEvent: this));
                    }
                }
            }
        }

        private void _announceIfLeadChanged(GameStateData previousGameState, GameStateData currentGameState)
        {
            if (currentGameState.SessionData.HasLeadChanged
                // protect against games that tell us who's leading on track
                && !currentGameState.SessionData.LeaderHasFinishedRace)
            {
                var leaderKey = currentGameState.getOpponentKeyAtClassPosition(1, currentGameState.carClass);
                if (leaderKey != null && currentGameState.OpponentData.TryGetValue(leaderKey, out OpponentData leader))
                {
                    String name = leader.DriverRawName;
                    if (!WatchedOpponents.watchedOpponentKeys.Contains(leaderKey) &&
                        currentGameState.SessionData.ClassPosition > 1 && previousGameState.SessionData.ClassPosition > 1 &&
                        !name.Equals(lastLeaderAnnounced) &&
                        currentGameState.Now > nextLeadChangeMessage && leader.CanUseName && AudioPlayer.canReadName(name, false))
                    {
                        Console.WriteLine("Lead change, current leader is " + name + " laps completed = " + currentGameState.SessionData.CompletedLaps);
                        // we use the short version on the basis
                        // that we don't need to make an assessment
                        // about who they are, but we still want to
                        // be able to identify them on track
                        // (number) and voice (ideally name).
                        //
                        // iRacing car numbers are linked to skill,
                        // we may expand this list.
                        var msg = MkOpponentShort(leader, preferPosition: false, requestNumber: Game.IRACING, preferCantPronounce: true);
                        msg.Add(MessageFragment.Text(folderIsNowLeading));

                        audioPlayer.playMessage(new QueuedMessage("new_leader", 4, secondsDelay: 2,
                            messageFragments: msg,
                            abstractEvent: this,
                            validationData: new Dictionary<string, object> { { validationNewLeaderKey, name } }, priority: 3));
                        nextLeadChangeMessage = currentGameState.Now.Add(TimeSpan.FromSeconds(60));
                        lastLeaderAnnounced = name;
                    }
                }
            }
        }

        /// <summary>
        /// Time to give an update on the opponent ahead
        /// </summary>
        /// <param name="currentGameState"></param>
        private void _updateDue(GameStateData currentGameState)
        {
            // this unlocks the corner case where the car ahead changed but we didn't call it because
            // we were in too much traffic or some other reason.
            timeToPollOpponentAhead = currentGameState.Now.Add(TimeSpan.FromSeconds(30));

            if (Game.IRACING ? currentGameState.SessionData.LapCount > currentGameState.FlagData.lapCountWhenLastWentGreen :
                               currentGameState.SessionData.CompletedLaps > 0)
            {
                string opponentKey = currentGameState.SessionData.OpponentKeyInFront;
                if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out OpponentData opponentData))
                {
                    var announceAfterTime = DateTime.MinValue;
                    string opponentName = opponentData.DriverRawName;
                    // Log.Debug(() => $"potential new car ahead: {opponentName} with delta {currentGameState.SessionData.TimeDeltaFront} (last driver was {lastCarAheadAnnounced})");
                    if (opponentName.Equals(lastCarAheadAnnounced))
                    {
                        // guard against spam where the opponent ahead is actually the same as what we last called
                        // so they are not really a "new car ahead".
                    }
                    else if (currentGameState.SessionData.TimeDeltaFront < 0.2)
                    {
                        // guard against when the racing is too tight to call it right now
                    }
                    else if (currentGameState.SessionData.TimeDeltaBehind > 0 && 
                             currentGameState.SessionData.TimeDeltaBehind <= 1 && 
                             currentGameState.SessionData.TimeDeltaFront >= 5)
                    {
                        // guard against when the car behind is so close that we don't care about a car ahead in the distance
                    }
                    else if (currentGameState.SessionData.TimeDeltaFront > maxSecondsAheadToReportOpponent
                            || currentGameState.SessionData.LapsDeltaFront != 0)
                    {
                        // guard against reporting drivers a long way ahead.
                    }
                    else if (opponentData.DeltaTime != null && opponentData.DeltaTime.totalDistanceTravelled > currentGameState.SessionData.DeltaTime.totalDistanceTravelled + getHorizon(currentGameState.SessionData.TrackDefinition))
                    {
                        // guard against reporting drivers a long way ahead, based on track distance.
                    }
                    else if (opponentData.isEnteringPits() || 
                             opponentData.InPits ||
                             opponentData.stoppedInLandmark != null ||
                             (opponentData.trackSurface != -1 &&
                              opponentData.trackSurface != (int)iRacing.TrackSurfaces.OnTrack &&
                              opponentData.Speed > 0.7 * currentGameState.PositionAndMotionData.CarSpeed))
                    {
                        // avoid a potential false positive. If they are really ahead we'll get them at the next poll
                    }
                    else if (WatchedOpponents.watchedOpponentKeys.Contains(opponentKey))
                    {
                        // never call watched opponents, they have their own system
                    }
                    else if (onlyAnnounceOpponentAfter.TryGetValue(opponentName, out announceAfterTime) && 
                             currentGameState.Now < announceAfterTime)
                    {
                        // we told the player about this driver very recently, even though positions have swapped, they should recognise them
                    }
                    else if (!opponentData.CanUseName)
                    {
                        // weird stale data, is this still relevant?
                    }
                    else if (AudioPlayer.canReadName(opponentName, false) || opponentData.CarNumber != "-1")
                    {
                        DelayedMessage callback = delegate(GameStateData futureGameState, List<MessageFragment> msg, List<MessageFragment> alt)
                        {
                            OpponentData futureOpponent = futureGameState.getOpponentInFront(futureGameState.carClass);
                            if (!opponentName.Equals(futureOpponent?.DriverRawName))
                            {
                                Log.Debug(() => $"new_car_ahead invalidated; changed from {opponentName} to {futureOpponent?.DriverRawName}");
                                return;
                            }
                            msg.AddRange(MkOpponentDetailed(futureOpponent, false, true, hackIncludeNextIfNoRep: true));

                            lastCarAheadAnnounced = futureOpponent.DriverRawName;
                            nextCarAheadChangeMessage = futureGameState.Now.Add(TimeSpan.FromSeconds(secondsToAnnounceNextCar));
                            onlyAnnounceOpponentAfter[futureOpponent.DriverRawName] = futureGameState.Now.Add(waitBeforeAnnouncingSameOpponentAhead);
                        };

                        int delay = Utilities.random.Next(Position.maxSecondsToWaitBeforeReportingPass + 1, Position.maxSecondsToWaitBeforeReportingPass + 3);
                        int despam_delay = (nextCarAheadChangeMessage - currentGameState.Now).Seconds;
                        if (despam_delay > delay)
                        {
                            delay = despam_delay;
                        }

                        // unique message names so that we can schedule multiple updates at once (the bad ones will be invalidated)
                        audioPlayer.playMessage(new QueuedMessage("new_car_ahead_" + opponentKey, delay + 5, secondsDelay: delay, delayedMessage: callback));
                    }
                }
            }
        }

        // the maximum distance ahead of us where we'd care about reporting the car ahead
        private float getHorizon(TrackDefinition track)
        {
            return Math.Min(2000.0f, track.trackLength / 3.0f);
        }

        private void _announceIfLappedCarAhead(GameStateData currentGameState)
        {
            if (!CrewChief.enableDriverNames || 
                currentGameState.SessionData.SessionPhase == SessionPhase.FullCourseYellow)
            {
                return;
            }
            // here we check for lapped cars and let the player know that they are not racing (with the
            // limited sound data we have available).
            //
            // We don't check for cars behind because that is handled by the Timing logic and is more
            // subtle. We don't care about cars that are laps ahead. Arguably this logic could be moved into Timings
            // but it is very rare that you'd come across a car ahead that is a lap down and not want
            // to know about it, and this is a much more efficient way to detect it than tracking
            // delta times.
            //
            // checking for new cars on track on every tick is a bit expensive, so only do this when
            // crossing over sector boundaries. We could change this to gap points to be more responsive.
            var key = currentGameState.getOpponentKeyInFrontOnTrack(onlyIncludeClass: currentGameState.carClass);
            if (key != null && (lastDriverAheadOnTrack == null || !lastDriverAheadOnTrack.Equals(key))
                            && currentGameState.OpponentData.TryGetValue(key, out OpponentData opponent)
                            && opponent.ClassPosition > currentGameState.SessionData.ClassPosition
                            && !WatchedOpponents.watchedOpponentKeys.Contains(key))
            {
                var (lapDiff, timeDiff) = currentGameState.SessionData.DeltaTime.GetRelativeDelta(opponent.DeltaTime);
                if (lapDiff > 0 && Math.Abs(timeDiff) < 10)
                {
                    lastDriverAheadOnTrack = key;

                    var message = MkLapDifferenceMessage(lapDiff);
                    // unfortunately there is no separate audio for "next car on track" but the context should make this clear
                    message.Insert(0, MessageFragment.Text(folderNextCarIs));
                    // since we are lapping the car, we are unlikely to remember any reputation info, so ask for it fresh
                    message.AddRange( MkFreshReputationAdvice(opponent, opponentIsAhead: true));

                    DelayedMessage callback = delegate (GameStateData futureGameState, List<MessageFragment> primary, List<MessageFragment> alt)
                    {
                        if (!key.Equals(futureGameState.getOpponentKeyInFrontOnTrack(onlyIncludeClass: futureGameState.carClass))) { return; }
                        primary.AddRange(message);
                        nextCarOnTrackChangeMessage = futureGameState.Now.Add(TimeSpan.FromSeconds(secondsToAnnounceNextCar));
                    };

                    int delay = Utilities.random.Next(Position.maxSecondsToWaitBeforeReportingPass + 1, Position.maxSecondsToWaitBeforeReportingPass + 3);
                    int despam_delay = (nextCarOnTrackChangeMessage - currentGameState.Now).Seconds;
                    if (despam_delay > delay)
                    {
                        delay = despam_delay;
                    }

                    audioPlayer.playMessage(new QueuedMessage("lapped_car_ahead", delay + 2, secondsDelay: delay, delayedMessage: callback, messageFragments: message));
                }
            }
        }

        private string _announceIfLeaderPitting(GameStateData currentGameState)
        {
            String announcedPitters = String.Empty;
            var opponentForLeaderPitting = currentGameState.PitData.OpponentForLeaderPitting;
            if (currentGameState.PitData.LeaderIsPitting &&
                currentGameState.SessionData.SessionPhase != SessionPhase.Countdown && currentGameState.SessionData.SessionPhase != SessionPhase.Formation &&
                !WatchedOpponents.watchedOpponentKeys.Contains(currentGameState.GetOpponentKey(currentGameState.PitData.OpponentForLeaderPitting)) &&
                !Strategy.opponentsWhoWillExitCloseInFront.Contains(currentGameState.PitData.OpponentForLeaderPitting.DriverRawName))
            {
                audioPlayer.playMessage(new QueuedMessage("leader_is_pitting", 10,
                    messageFragments: MessageContents(folderTheLeader, currentGameState.PitData.OpponentForLeaderPitting,
                        folderIsPitting),
                    alternateMessageFragments: MessageContents(folderLeaderIsPitting), abstractEvent: this, priority: 3));
                announcedPitters = currentGameState.PitData.OpponentForLeaderPitting.DriverRawName;
            }
            return announcedPitters;
        }

        private string _announceIfCarAheadPitting(GameStateData currentGameState)
        {
            String announcedPitters = String.Empty;
            if (currentGameState.PitData.CarInFrontIsPitting && currentGameState.SessionData.TimeDeltaFront > 3 &&
                currentGameState.SessionData.SessionPhase != SessionPhase.Countdown && currentGameState.SessionData.SessionPhase != SessionPhase.Formation &&
                !WatchedOpponents.watchedOpponentKeys.Contains(currentGameState.GetOpponentKey(currentGameState.PitData.OpponentForCarAheadPitting)) &&
                !Strategy.opponentsWhoWillExitCloseInFront.Contains(currentGameState.PitData.OpponentForCarAheadPitting.DriverRawName) &&
                !announcedPitters.Contains(currentGameState.PitData.OpponentForCarAheadPitting.DriverRawName))
            {
                audioPlayer.playMessage(new QueuedMessage("car_in_front_is_pitting", 10,
                    messageFragments: MessageContents(currentGameState.PitData.OpponentForCarAheadPitting,
                        folderAheadIsPitting),
                    alternateMessageFragments: MessageContents(folderCarAheadIsPitting), abstractEvent: this, priority: 3));
                announcedPitters = currentGameState.PitData.OpponentForCarAheadPitting.DriverRawName;
            }
            return announcedPitters;
        }

        private string _announceIfCarBehindPitting(GameStateData currentGameState)
        {
            String announcedPitters = String.Empty;
            if (currentGameState.PitData.CarBehindIsPitting && currentGameState.SessionData.TimeDeltaBehind > 3 &&
                currentGameState.SessionData.SessionPhase != SessionPhase.Countdown && currentGameState.SessionData.SessionPhase != SessionPhase.Formation &&
                !WatchedOpponents.watchedOpponentKeys.Contains(currentGameState.GetOpponentKey(currentGameState.PitData.OpponentForCarBehindPitting)) &&
                !Strategy.opponentsWhoWillExitCloseBehind.Contains(currentGameState.PitData.OpponentForCarBehindPitting.DriverRawName) &&
                !announcedPitters.Contains(currentGameState.PitData.OpponentForCarBehindPitting.DriverRawName))
            {
                audioPlayer.playMessage(new QueuedMessage("car_behind_is_pitting", 10,
                    messageFragments: MessageContents(currentGameState.PitData.OpponentForCarBehindPitting,
                        folderBehindIsPitting),
                    alternateMessageFragments: MessageContents(folderCarBehindIsPitting), abstractEvent: this, priority: 3));
                announcedPitters = currentGameState.PitData.OpponentForCarBehindPitting.DriverRawName;
            }
            return announcedPitters;
        }

        private void _announceIfOpponentAheadPitting(GameStateData currentGameState, HashSet<string> announcedPitters)
        {
            if (Strategy.opponentFrontToWatchForPitting != null && !announcedPitters.Contains(Strategy.opponentFrontToWatchForPitting)
                                                                && !WatchedOpponents.watchedOpponentKeys.Contains(currentGameState.GetOpponentKey(Strategy.opponentFrontToWatchForPitting)))
            {
                foreach (KeyValuePair<String, OpponentData> entry in currentGameState.OpponentData)
                {
                    if (entry.Value.DriverRawName == Strategy.opponentFrontToWatchForPitting)
                    {
                        if (entry.Value.InPits)
                        {
                            audioPlayer.playMessage(new QueuedMessage("car_is_pitting", 10,
                                messageFragments: MessageContents(entry.Value,
                                    currentGameState.SessionData.ClassPosition > entry.Value.ClassPosition ? folderAheadIsPitting : folderBehindIsPitting),
                                abstractEvent: this, priority: 3));
                            Strategy.opponentFrontToWatchForPitting = null;
                            break;
                        }
                    }
                }
            }
        }

        private void _announceIfOpponentBehindPitting(GameStateData currentGameState, HashSet<string> announcedPitters)
        {
            if (Strategy.opponentBehindToWatchForPitting != null && !announcedPitters.Contains(Strategy.opponentBehindToWatchForPitting)
                                                                 && !WatchedOpponents.watchedOpponentKeys.Contains(currentGameState.GetOpponentKey(Strategy.opponentBehindToWatchForPitting)))
            {
                foreach (KeyValuePair<String, OpponentData> entry in currentGameState.OpponentData)
                {
                    if (entry.Value.DriverRawName == Strategy.opponentBehindToWatchForPitting)
                    {
                        if (entry.Value.InPits)
                        {
                            audioPlayer.playMessage(new QueuedMessage("car_is_pitting", 10,
                                messageFragments: MessageContents(entry.Value,
                                    currentGameState.SessionData.ClassPosition > entry.Value.ClassPosition ? folderAheadIsPitting : folderBehindIsPitting),
                                abstractEvent: this, priority: 3));
                            Strategy.opponentBehindToWatchForPitting = null;
                            break;
                        }
                    }
                }
            }
        }

        //private static readonly List<SpeechCommands.ID> opponentKeys = new List<SpeechCommands.ID>
        //{
        //    SpeechCommands.ID.THE_LEADER,
        //    SpeechCommands.ID.THE_CAR_AHEAD,
        //    SpeechCommands.ID.THE_GUY_AHEAD,
        //    SpeechCommands.ID.THE_CAR_IN_FRONT,
        //    SpeechCommands.ID.THE_GUY_IN_FRONT,
        //    SpeechCommands.ID.THE_CAR_BEHIND,
        //    SpeechCommands.ID.THE_GUY_BEHIND,
        //    SpeechCommands.ID.POSITION_LONG,
        //    SpeechCommands.ID.POSITION_SHORT,
        //    SpeechCommands.ID.CAR_NUMBER,
        //};
        //private SpeechCommands.ID translate(String voiceMessage)
        //{
        //    return SpeechCommands.SpeechToCommand(Commands, voiceMessage, false);
        //}
        public static Tuple<string, Boolean> getOpponentKey(String voiceMessage, GameStateData currentGameState, String expectedNumberSuffix = null)
        {
            string opponentKey = null;
            Boolean gotByPositionNumber = false;
            if (voiceMessage.Contains(SpeechRecogniser.THE_LEADER))
            {
                if (currentGameState.SessionData.ClassPosition > 1)
                {
                    opponentKey = currentGameState.getOpponentKeyAtClassPosition(1, currentGameState.carClass);
                }
                else if (currentGameState.SessionData.ClassPosition == 1)
                {
                    opponentKey = positionIsPlayerKey;
                }
            }
            else if ((voiceMessage.Contains(SpeechRecogniser.THE_CAR_AHEAD) || voiceMessage.Contains(SpeechRecogniser.THE_GUY_AHEAD) ||
                voiceMessage.Contains(SpeechRecogniser.THE_GUY_IN_FRONT) || voiceMessage.Contains(SpeechRecogniser.THE_CAR_IN_FRONT)) && currentGameState.SessionData.ClassPosition > 1)
            {
                opponentKey = currentGameState.getOpponentKeyInFront(currentGameState.carClass);
            }
            else if ((voiceMessage.Contains(SpeechRecogniser.THE_CAR_BEHIND) || voiceMessage.Contains(SpeechRecogniser.THE_GUY_BEHIND)) &&
                            !currentGameState.isLast())
            {
                opponentKey = currentGameState.getOpponentKeyBehind(currentGameState.carClass);
            }
            else if (voiceMessage.Contains(SpeechRecogniser.POSITION_LONG) || voiceMessage.Contains(SpeechRecogniser.POSITION_SHORT))
            {
                int position = 0;
                Boolean found = false;
                foreach (KeyValuePair<String[], int> entry in SpeechRecogniser.racePositionNumberToNumber)
                {
                    foreach (String numberStr in entry.Key)
                    {
                        if (expectedNumberSuffix.Length > 0)
                        {
                            if (voiceMessage.Contains(" " + numberStr + expectedNumberSuffix))
                            {
                                position = entry.Value;
                                found = true;
                                break;
                            }
                        }
                        else
                        {
                            if (voiceMessage.EndsWith(" " + numberStr))
                            {
                                position = entry.Value;
                                found = true;
                                break;
                            }
                        }
                    }
                    if (found)
                    {
                        break;
                    }
                }
                if (position != currentGameState.SessionData.ClassPosition)
                {
                    opponentKey = currentGameState.getOpponentKeyAtClassPosition(position, currentGameState.carClass);
                }
                else
                {
                    opponentKey = positionIsPlayerKey;
                }
                gotByPositionNumber = true;
            }
            else if (voiceMessage.Contains(SpeechRecogniser.CAR_NUMBER))
            {
                String carNumber = "-1";
                Boolean found = false;
                foreach (KeyValuePair<String[], String> entry in SpeechRecogniser.carNumberToNumber)
                {
                    foreach (String numberStr in entry.Key)
                    {
                        if (expectedNumberSuffix.Length > 0)
                        {
                            if (voiceMessage.Contains(" " + numberStr + expectedNumberSuffix))
                            {
                                carNumber = entry.Value;
                                found = true;
                                break;
                            }
                        }
                        else
                        {
                            if (voiceMessage.EndsWith(" " + numberStr))
                            {
                                carNumber = entry.Value;
                                found = true;
                                break;
                            }
                        }
                    }
                    if (found)
                    {
                        break;
                    }
                }
                if (carNumber != "-1" && carNumber != currentGameState.SessionData.PlayerCarNr)
                {
                    opponentKey = currentGameState.getOpponentKeyForCarNumber(carNumber);
                }
                gotByPositionNumber = false;
            }
            else
            {
                foreach (KeyValuePair<string, OpponentData> entry in currentGameState.OpponentData)
                {
                    String usableDriverNameForSRE = DriverNameHelper.getUsableDriverNameForSRE(entry.Value.DriverRawName);
                    // check for full username match so we're not triggering on substrings within other words
                    if (usableDriverNameForSRE != null
                        && (voiceMessage.Contains(" " + usableDriverNameForSRE + " ") || voiceMessage.EndsWith(" " + usableDriverNameForSRE)))
                    {
                        opponentKey = entry.Key;
                        break;
                    }
                }
            }
            return new Tuple<string, bool>(opponentKey, gotByPositionNumber);
        }

        private float getOpponentLastLap(string opponentKey)
        {
            OpponentData opponentData = null;
            if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out opponentData))
            {
                return opponentData.LastLapTime;
            }
            return -1;
        }

        private float getOpponentBestLap(string opponentKey)
        {
            OpponentData opponentData = null;
            if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out opponentData))
            {
                return opponentData.CurrentBestLapTime;
            }
            return -1;
        }

        private Tuple<String, float> getOpponentLicensLevel(string opponentKey)
        {
            OpponentData opponentData = null;
            if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out opponentData))
            {
                return opponentData.LicensLevel;
            }
            return new Tuple<String, float>("invalid", -1);
        }
        private int getOpponentIRating(string opponentKey)
        {
            OpponentData opponentData = null;
            if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out opponentData))
            {
                return opponentData.iRating;
            }
            return -1;
        }
        private int getOpponentR3EUserId(string opponentKey)
        {
            OpponentData opponentData = null;
            if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out opponentData))
            {
                return opponentData.r3eUserId;
            }
            return -1;
        }

        // The philosophy of the detailed message is to keep the message as terse as possible whilst returning as much relevant information
        // about the car and driver as possible. That means not including information that is obvious (e.g. if this is invoked when it is
        // clear that it's for the car ahead then we don't include position). We use the absense of information to imply "similar
        // as you" for car class, license, rating, etc.
        //
        // Requesting to include the car position implies that this is a track position query, rather than a leaderboard query, where
        // the user might not be sure if they are racing the car (different class, lapped).
        //
        // A litmus test is that if some information doesn't help to answer:
        //
        // - how can I refer to this person on the radio?
        // - am I racing them for position?
        // - can I trust this person to race cleanly?
        //
        // or the information is obvious from the context, then we should not include that information.
        //
        // Things we'd like to include in the future:
        //
        //   - club (where US is considered the default for ovals, your own club elsewhere)
        //   - latency (only when it is above a threshold)
        //   - customerid / length of time on the service (e.g. racing less than 6 months)
        //   - when they last pitted (relevant to let us know if we're racing them)
        //   - have we recorded them in a list of wreckless (or clean or clueless) drivers
        //   - incident points in this race
        //
        // Things we should not include, because they are better suited for a "gap to" style query
        //
        //   - lap difference
        //   - faster/slower than us
        //
        // NOTE: carIsAhead is only included to workaround the fact that we only have voice recordings for reputations
        //       of cars with ahead/behind baked into the message. If we can update the sound pack so that the messages
        //       are neutral to the other car's location then we can drop this parameter.
        private List<MessageFragment> MkOpponentDetailed(OpponentData opponent, bool includePosition, bool carIsAhead, bool hackIncludeNextIfNoRep = false)
        {
            var fragments = MkFreshReputationAdvice(opponent, carIsAhead, short_form: !hackIncludeNextIfNoRep);

            // this is such a hack, I am so sorry. The reputations advice will prefix a "the car ahead/behind" but if
            // there is no advice, and we need to say "the next car" then we need to patch it back in again.
            if (fragments.Count == 0 && hackIncludeNextIfNoRep)
            {
                fragments.Add(MessageFragment.Text(folderNextCarIs));
            }

            fragments.AddRange(MkOpponentShort(opponent, preferPosition: false, requestNumber: GlobalBehaviourSettings.useOvalLogic, preferCantPronounce: true));

            // if they are a different class, say the class
            if (!CarData.IsCarClassEqual(opponent.CarClass, currentGameState.carClass))
            {
                var clip = MulticlassWarnings.carClassEnumToSoundFolder(opponent.CarClass.carClassEnum);
                if (clip != null)
                {
                    fragments.Add(MessageFragment.Text(clip));
                }
            }
            else if (includePosition)
            {
                // position in class. There's no point in reporting the position of other classes, although
                // it might make sense to report when it's the leader.
                fragments.AddRange(MkOpponentPosition(opponent.ClassPosition));
            }

            if (opponentRatingInfo && Game.IRACING)
            {
                // safety license (only report if different to us)
                Tuple<string, float> license = opponent.LicensLevel;
                Tuple<string, float> our_license = currentGameState.SessionData.LicenseLevel;
                if (license.Item2 != -1 && !string.Equals(license.Item1, our_license.Item1, StringComparison.CurrentCultureIgnoreCase))
                {
                    string folder = Ratings.GetLicenseFolder(license.Item1.ToLower());
                    if (folder != null)
                    {
                        fragments.Add(MessageFragment.Text(folder));
                    }
                }

                // iRating (only report if significantly different to us)
                int irating = opponent.iRating;
                int our_irating = currentGameState.SessionData.iRating;
                if (irating > 0 && Math.Abs(irating - our_irating) >= our_irating / 10)
                {
                    fragments.Add(MessageFragment.Text(folderRatingIntro));
                    fragments.AddRange(Utilities.WholeAndFractionalMessage(irating / 1000.0f));
                }
            }
            else if (opponentRatingInfo && Game.RACE_ROOM)
            {
                R3ERatingData ratingData = R3ERatings.getRatingForUserId(opponent.r3eUserId);
                if (R3ERatings.playerRating != null && ratingData != null)
                {
                    Console.WriteLine("got rating data for opponent:" + ratingData.ToString());

                    float their_rep = ratingData.reputation;
                    float our_rep = R3ERatings.playerRating.reputation;
                    if (Math.Abs(their_rep - our_rep) >= Math.Abs(our_rep / 10.0))
                    {
                        fragments.Add(MessageFragment.Text(folderReputationIntro));
                        Tuple<int, int> rep = Utilities.WholeAndFractionalPart(their_rep, rounding: true);
                        fragments.Add(MessageFragment.Integer(rep.Item1));
                    }

                    float their_rating = ratingData.rating;
                    float our_rating = R3ERatings.playerRating.rating;
                    if (Math.Abs(their_rating - our_rating) >= Math.Abs(our_rating / 10.0))
                    {
                        fragments.Add(MessageFragment.Text(folderRatingIntro));
                        fragments.AddRange(Utilities.WholeAndFractionalMessage(their_rating / 1000.0f));
                    }
                }
            }

            return fragments;
        }

        // A short form way of referring to an opponent.
        //
        // We prefer to use their name, but if it is not available we will fall back to their car number (if available),
        // otherwise falling back to their position. A request to include the car number in addition to the name can be made
        // by the caller, which can be disabled by the user.
        //
        // `preferPosition` can be used to prefer the position instead of the car number which is more useful in some
        // contexts (e.g. P1 is coming to lap you).
        //
        // `allowCantPronounce` offers an alternative in the case that the car number is not known and positions can't be used
        // (e.g. if it is already used elsewhere in the message).
        //
        // `allowPosition` is risky and if set to false means that the result can be empty, so use at your own risk. It is
        // useful in cases where we don't want any fallback to position or "can't pronounce" when the car number is not
        // available, which is really just raceroom.
        public static List<MessageFragment> MkOpponentShort(OpponentData opponent, bool preferPosition, bool requestNumber, bool preferCantPronounce, bool allowPosition = true)
        {
            var messages = new List<MessageFragment>();

            bool number_available = !opponent.CarNumber.Equals("-1");
            bool includeNumber = false;

            if (AudioPlayer.canReadName(opponent.DriverRawName))
            {
                messages.Add(MessageFragment.Opponent(opponent));
                includeNumber = requestNumber && allowNumberAfterName && number_available;
            }
            else if (preferPosition)
            {
                messages.AddRange(MkOpponentPosition(opponent.ClassPosition));
                // no includeNumber here, it's too noisy to have multiple numbers
            }
            else if (number_available)
            {
                includeNumber = true;
            }
            else if (preferCantPronounce)
            {
                // unfortunately this audio message can sometimes be "I really don't know" which can be confusing,
                // but at least half the time it is "I can't pronounce the name".
                messages.Add(MessageFragment.Text(folderCantPronounceName));
            }
            else if (allowPosition)
            {
                messages.AddRange(MkOpponentPosition(opponent.ClassPosition));
            }

            if (includeNumber)
            {
                messages.Add(MessageFragment.Text(Opponents.folderCarNumber));
                messages.AddRange(new CarNumber(opponent.CarNumber).getMessageFragments());
            }

            return messages;
        }

        private bool PlayOpponentDetailed(string opponentKey, bool includePosition, bool carIsAhead)
        {
            if (opponentKey == null) { return false; }
            return currentGameState.OpponentData.TryGetValue(opponentKey, out OpponentData o) && PlayOpponentDetailed(o, includePosition, carIsAhead);
        }
        private bool PlayOpponentDetailed(OpponentData opponent, bool includePosition, bool carIsAhead)
        {
            if (opponent == null)
            {
                return false;
            }
            var messages = MkOpponentDetailed(opponent, includePosition, carIsAhead);
            QueuedMessage queuedMessage = new QueuedMessage("opponentDetailed", 0, messageFragments: messages);

            if (queuedMessage.canBePlayed)
            {
                onlyAnnounceOpponentAfter[opponent.DriverRawName] = currentGameState.Now.Add(waitBeforeAnnouncingSameOpponentAhead);
                audioPlayer.playMessageImmediately(queuedMessage);
                return true;
            }
            return false;
        }
        public static List<MessageFragment> MkOpponentPosition(int position)
        {
            if (SoundCache.availableSounds.Contains(folderOpponentPositionIntro))
            {
                return MessageContents(folderOpponentPositionIntro, Position.folderStub + position);
            }
            else
            {
                return MessageContents(Position.folderStub + position);
            }
        }
        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.HOW_GOOD_IS,
            SpeechCommands.ID.WHATS_MY_CAR_NUMBER,
            SpeechCommands.ID.WHAT_TYRES_AM_I_ON,
            SpeechCommands.ID.WHOS_BEHIND_ON_TRACK,
            SpeechCommands.ID.WHOS_BEHIND_IN_THE_RACE,  // includes "who's behind" so must be after WHOS_BEHIND_ON_TRACK
            SpeechCommands.ID.WHOS_IN_FRONT_ON_TRACK,
            SpeechCommands.ID.WHOS_IN_FRONT_IN_THE_RACE,  // includes "who's ahead" so must be after WHOS_IN_FRONT_ON_TRACK
            SpeechCommands.ID.WHOS_TWO_IN_FRONT_IN_THE_RACE,
            SpeechCommands.ID.WHOS_LEADING,
            SpeechCommands.ID.WHEN_DID_IN_FRONT_ON_TRACK_PIT,
            SpeechCommands.ID.WHEN_DID_BEHIND_ON_TRACK_PIT,
        };
        private static readonly List<SpeechCommands.ID> StartsWith = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.WHAT_TYRE_IS__,
            SpeechCommands.ID.WHAT_TYRES_IS__,
            SpeechCommands.ID.WHOS_IN__,
            SpeechCommands.ID.WHERE_IS__,
            SpeechCommands.ID.WHERES__
        };
        private static readonly List<SpeechCommands.ID> WhatCommands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.WHATS__LAST_LAP,
            SpeechCommands.ID.WHATS__LAST_LAP_TIME,
            SpeechCommands.ID.WHATS__BEST_LAP,
            SpeechCommands.ID.WHATS__BEST_LAP_TIME,
            SpeechCommands.ID.WHATS__LICENSE_CLASS,
            SpeechCommands.ID.WHATS__IRATING,
            SpeechCommands.ID.WHATS__REPUTATION,
            SpeechCommands.ID.WHATS__RATING,
            SpeechCommands.ID.WHATS__RANK
        };
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            SpeechCommands.ID cmd = SpeechCommands.SpeechToCommand(Commands, voiceMessage, false);

            if (cmd == SpeechCommands.ID.NO_COMMAND)
            {
                cmd = SpeechCommands.SpeechStartsWithToCommand(StartsWith, voiceMessage, false);
                if (cmd == SpeechCommands.ID.NO_COMMAND &&
                    voiceMessage.StartsWith(SpeechRecogniser.WHATS))
                {
                    cmd = SpeechCommands.SpeechEndsWithToCommand(WhatCommands, voiceMessage);
                }
            }
            return cmd;
        }

        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID cmd)
        {
            Boolean gotData = false;
            if (currentGameState == null)
            {
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                return;
            }
            switch (cmd)
            {
                case SpeechCommands.ID.WHAT_TYRES_AM_I_ON:
                    {
                        gotData = true;
                        audioPlayer.playMessageImmediately(new QueuedMessage(TyreMonitor.getFolderForTyreType(currentGameState.TyreData.FrontLeftTyreType), 0));
                        break;
                    }
                case SpeechCommands.ID.WHAT_TYRE_IS__:
                case SpeechCommands.ID.WHAT_TYRES_IS__:
                    {
                        // only have data here for r3e, rf2 and iracing, other games don't expose opponent tyre types
                        if (Game.RF2_LMU || Game.RACE_ROOM || Game.IRACING)
                        {
                            string opponentKey = getOpponentKey(voiceMessage, currentGameState, " " + SpeechRecogniser.ON).Item1;
                            if (opponentKey != null)
                            {
                                OpponentData opponentData = currentGameState.OpponentData[opponentKey];
                                if (opponentData != null)
                                {
                                    gotData = true;
                                    audioPlayer.playMessageImmediately(new QueuedMessage(TyreMonitor.getFolderForTyreType(opponentData.CurrentTyres), 0));
                                    Log.Debug($"WHAT_TYRE_IS__ {opponentData.DriverRawName} '{voiceMessage}'");
                                }
                            }
                        }
                        break;
                    }
                case SpeechCommands.ID.HOW_GOOD_IS:
                    {
                        if (Game.RACE_ROOM)
                        {
                            R3ERatingData ratingData = R3ERatings.getRatingForUserId(getOpponentR3EUserId(getOpponentKey(voiceMessage, currentGameState, "").Item1));
                            if (ratingData != null)
                            {
                                gotData = true;
                                Console.WriteLine("got rating data for opponent:" + ratingData.ToString());
                                // if we don't explicitly split the sounds up here they'll be read ints
                                int reputationIntPart = (int)ratingData.reputation;
                                int reputationDecPart = (int)(10 * (ratingData.reputation - (float)reputationIntPart));
                                int ratingIntPart = (int)ratingData.rating;
                                int ratingDecPart = (int)(10 * (ratingData.rating - (float)ratingIntPart));
                                audioPlayer.playMessageImmediately(new QueuedMessage("opponentReputationAndRating", 0,
                                    messageFragments: MessageContents(folderReputationIntro, reputationIntPart, NumberReader.folderPoint, reputationDecPart,
                                        folderRatingIntro, ratingIntPart, NumberReader.folderPoint, ratingDecPart)));
                            }
                        }
                        else if (Game.IRACING)
                        {
                            int rating = getOpponentIRating(getOpponentKey(voiceMessage, currentGameState, "").Item1);
                            if (rating != -1)
                            {
                                gotData = true;
                                audioPlayer.playMessageImmediately(new QueuedMessage("opponentiRating", 0,
                                    messageFragments: MessageContents(folderRatingIntro, rating)));
                            }
                        }
                        break;
                    }
                case SpeechCommands.ID.WHATS__LAST_LAP:
                case SpeechCommands.ID.WHATS__LAST_LAP_TIME:
                    {
                        float lastLap = getOpponentLastLap(getOpponentKey(voiceMessage, currentGameState, SpeechRecogniser.POSSESSIVE + " ").Item1);
                        if (lastLap != -1)
                        {
                            gotData = true;
                            audioPlayer.playMessageImmediately(new QueuedMessage("opponentLastLap", 0,
                                messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(lastLap, Precision.AUTO_LAPTIMES))));

                        }
                        break;
                    }
                case SpeechCommands.ID.WHATS__BEST_LAP:
                case SpeechCommands.ID.WHATS__BEST_LAP_TIME:

                    {
                        float bestLap = getOpponentBestLap(getOpponentKey(voiceMessage, currentGameState, SpeechRecogniser.POSSESSIVE + " ").Item1);
                        if (bestLap != -1)
                        {
                            gotData = true;
                            audioPlayer.playMessageImmediately(new QueuedMessage("opponentBestLap", 0,
                                messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(bestLap, Precision.AUTO_LAPTIMES))));

                        }
                        break;
                    }
                case SpeechCommands.ID.WHATS__LICENSE_CLASS:
                    {
                        Tuple<string, float> licenseLevel = getOpponentLicensLevel(getOpponentKey(voiceMessage, currentGameState, SpeechRecogniser.POSSESSIVE + " ").Item1);
                        var msg = Ratings.LicenseLevelMessage(licenseLevel);
                        if (msg != null)
                        {
                            gotData = true;
                            audioPlayer.playDelayedImmediateMessage(msg);
                        }
                        break;
                    }
                case SpeechCommands.ID.WHATS__IRATING:
                    {
                        int rating = getOpponentIRating(getOpponentKey(voiceMessage, currentGameState, SpeechRecogniser.POSSESSIVE + " ").Item1);
                        if (rating != -1)
                        {
                            gotData = true;
                            audioPlayer.playMessageImmediately(new QueuedMessage("opponentiRating", 0, messageFragments: MessageContents(rating)));
                        }
                        break;
                    }
                case SpeechCommands.ID.WHATS__RATING:
                    {
                        if (Game.RACE_ROOM)
                        {
                            R3ERatingData ratingData = R3ERatings.getRatingForUserId(getOpponentR3EUserId(getOpponentKey(voiceMessage, currentGameState, SpeechRecogniser.POSSESSIVE + " ").Item1));
                            if (ratingData != null)
                            {
                                gotData = true;
                                Console.WriteLine("got rating data for opponent:" + ratingData.ToString());
                                // if we don't explicitly split the sound up here it'll be read as an int
                                int intPart = (int)ratingData.rating;
                                int decPart = (int)(10 * (ratingData.rating - (float)intPart));
                                audioPlayer.playMessageImmediately(new QueuedMessage("opponentRating", 0,
                                    messageFragments: MessageContents(intPart, NumberReader.folderPoint, decPart)));
                            }
                        }
                        break;
                    }
                case SpeechCommands.ID.WHATS__REPUTATION:
                    {
                        if (Game.RACE_ROOM)
                        {
                            R3ERatingData ratingData = R3ERatings.getRatingForUserId(getOpponentR3EUserId(getOpponentKey(voiceMessage, currentGameState, SpeechRecogniser.POSSESSIVE + " ").Item1));
                            if (ratingData != null)
                            {
                                gotData = true;
                                Console.WriteLine("got rating data for opponent:" + ratingData.ToString());
                                // if we don't explicitly split the sound up here it'll be read as an int
                                int intPart = (int)ratingData.reputation;
                                int decPart = (int)(10 * (ratingData.reputation - (float)intPart));
                                audioPlayer.playMessageImmediately(new QueuedMessage("opponentReputation", 0,
                                    messageFragments: MessageContents(intPart, NumberReader.folderPoint, decPart)));
                            }
                        }
                        break;
                    }
                case SpeechCommands.ID.WHATS__RANK:
                    {
                        if (Game.RACE_ROOM)
                        {
                            R3ERatingData ratingData = R3ERatings.getRatingForUserId(getOpponentR3EUserId(getOpponentKey(voiceMessage, currentGameState, SpeechRecogniser.POSSESSIVE + " ").Item1));
                            if (ratingData != null)
                            {
                                gotData = true;
                                Console.WriteLine("got rating data for opponent:" + ratingData.ToString());
                                List<MessageFragment> fragments = new List<MessageFragment>();
                                // ensure hundreds don't get truncated
                                fragments.Add(MessageFragment.Integer(ratingData.rank, false));
                                audioPlayer.playMessageImmediately(new QueuedMessage("opponentRank", 0, messageFragments: fragments));
                            }
                        }
                        break;
                    }
                case SpeechCommands.ID.WHERE_IS__:
                case SpeechCommands.ID.WHERES__:
                    {
                        Tuple<string, Boolean> response = getOpponentKey(voiceMessage, currentGameState, "");
                        string opponentKey = response.Item1;
                        Boolean gotByPositionNumber = response.Item2;
                        OpponentData opponent = null;
                        if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out opponent))
                        {
                            if (opponent.IsActive)
                            {
                                int position = opponent.ClassPosition;
                                Tuple<int, float> deltas = currentGameState.SessionData.DeltaTime.GetSignedDeltaTimeWithLapDifference(opponent.DeltaTime);
                                int lapDifference = deltas.Item1;
                                float timeDelta = deltas.Item2;
                                if (currentGameState.SessionData.SessionType != SessionType.Race || timeDelta == 0 || (lapDifference == 0 && Math.Abs(timeDelta) < 0.05))
                                {
                                    // the delta is not usable - say the position if we didn't directly ask by position

                                    if (!gotByPositionNumber)
                                    {
                                        if (SoundCache.availableSounds.Contains(folderOpponentPositionIntro))
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage("opponentPosition", 0,
                                                messageFragments: MessageContents(folderOpponentPositionIntro, Position.folderStub + position)));
                                        }
                                        else
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage("opponentPosition", 0,
                                                messageFragments: MessageContents(Position.folderStub + position)));
                                        }
                                        gotData = true;
                                    }
                                }
                                else
                                {
                                    List<MessageFragment> message = new List<MessageFragment>();
                                    if (!gotByPositionNumber)
                                    {
                                        message.AddRange(MkOpponentPosition(position));
                                        message.AddRange(MessageContents(Pause(200)));
                                    }
                                    message.AddRange(MkOpponentDelta(
                                        lapDifference: lapDifference,
                                        timeDelta: timeDelta,
                                        expectedDeltaSign: 0,
                                        includeLapInfo: true));
                                    audioPlayer.playMessageImmediately(new QueuedMessage("opponentTimeDelta", 0, messageFragments: message));
                                    gotData = true;
                                }
                            }
                            else
                            {
                                Console.WriteLine("Driver " + opponent.DriverRawName + " is no longer active in this session");
                            }
                        }
                        break;
                    }
                case SpeechCommands.ID.WHOS_BEHIND_ON_TRACK:
                    {
                        if (PlayOpponentDetailed(currentGameState.getOpponentKeyBehindOnTrack(), includePosition: true, carIsAhead: false))
                        {
                            gotData = true;
                        }

                        break;
                    }
                case SpeechCommands.ID.WHOS_IN_FRONT_ON_TRACK:
                    {
                        if (PlayOpponentDetailed(currentGameState.getOpponentKeyInFrontOnTrack(), includePosition: true, carIsAhead: true))
                        {
                            gotData = true;
                        }

                        break;
                    }
                case SpeechCommands.ID.WHOS_BEHIND_IN_THE_RACE:
                    {
                        if (currentGameState.isLast())
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(Position.folderLast, 0));

                            gotData = true;
                        }
                        else
                        {
                            OpponentData opponent = currentGameState.getOpponentAtClassPosition(currentGameState.SessionData.ClassPosition + 1, currentGameState.carClass);
                            gotData = PlayOpponentDetailed(opponent, includePosition: false, carIsAhead: false);
                        }

                        break;
                    }
                case SpeechCommands.ID.WHOS_IN_FRONT_IN_THE_RACE:
                    {
                        if (currentGameState.SessionData.ClassPosition == 1)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(Position.folderLeading, 0));

                            gotData = true;
                        }
                        else
                        {
                            OpponentData opponent = currentGameState.getOpponentAtClassPosition(currentGameState.SessionData.ClassPosition - 1, currentGameState.carClass);
                            gotData = PlayOpponentDetailed(opponent, includePosition: false, carIsAhead: true);
                        }

                        break;
                    }
                case SpeechCommands.ID.WHOS_TWO_IN_FRONT_IN_THE_RACE:
                    {
                        if (currentGameState.SessionData.ClassPosition <= 2)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage("two_ahead_nonsense", 0, MessageContents(Position.folderDriverPositionIntro, Position.folderStub + currentGameState.SessionData.ClassPosition)));
                            gotData = true;
                        }
                        else
                        {
                            OpponentData opponent = currentGameState.getOpponentAtClassPosition(currentGameState.SessionData.ClassPosition - 2, currentGameState.carClass);
                            gotData = PlayOpponentDetailed(opponent, includePosition: false, carIsAhead: true);
                        }
                        break;
                    }
                case SpeechCommands.ID.WHEN_DID_IN_FRONT_ON_TRACK_PIT:
                    {
                        // it would be great to be able to have this info added to the MkOpponentDetailed message but we don't
                        // have a "they last pitted" or "ago" or "on lap" recording from Jim.
                        var key = currentGameState.getOpponentKeyInFrontOnTrack();
                        if (lastPitted.TryGetValue(key, out (DateTime, int) pitted))
                        {
                            int minutes = (currentGameState.Now - pitted.Item1).Minutes;
                            // could include relative or absolute laps here but it is ambiguous without the voice to qualify it
                            audioPlayer.playMessageImmediately(new QueuedMessage("in_front_pitted", 0, MessageContents(minutes, "numbers/minutes")));
                            gotData = true;
                        }
                        break;
                    }
                case SpeechCommands.ID.WHEN_DID_BEHIND_ON_TRACK_PIT:
                    {
                        var key = currentGameState.getOpponentKeyBehindOnTrack();
                        if (lastPitted.TryGetValue(key, out (DateTime, int) pitted))
                        {
                            int minutes = (currentGameState.Now - pitted.Item1).Minutes;
                            audioPlayer.playMessageImmediately(new QueuedMessage("behind_pitted", 0, MessageContents(minutes, "numbers/minutes")));
                            gotData = true;
                        }
                        break;
                    }

                case SpeechCommands.ID.WHOS_LEADING:
                    {
                        if (currentGameState.SessionData.ClassPosition > 1)
                        {
                            OpponentData opponent = currentGameState.getOpponentAtClassPosition(1, currentGameState.carClass);
                            gotData = PlayOpponentDetailed(opponent, includePosition: false, carIsAhead: true);
                        }
                        break;
                    }
                case SpeechCommands.ID.WHATS_MY_CAR_NUMBER:
                    {
                        if (currentGameState.SessionData.PlayerCarNr != "-1")
                        {
                            var fragments = new List<MessageFragment>();
                            fragments.Add(MessageFragment.Text(Opponents.folderCarNumber));
                            fragments.AddRange(new CarNumber(currentGameState.SessionData.PlayerCarNr).getMessageFragments());
                            audioPlayer.playMessageImmediately(new QueuedMessage("whats_my_car_number", 0, messageFragments: fragments));
                            gotData = true;
                        }
                        break;
                    }
                case SpeechCommands.ID.WHOS_IN__:
                    {
                        string opponentKey = getOpponentKey(voiceMessage, currentGameState, "").Item1;
                        if (opponentKey != null)
                        {
                            if (opponentKey == positionIsPlayerKey)
                            {
                                audioPlayer.playMessageImmediately(new QueuedMessage(folderWeAre, 0));

                                gotData = true;
                            }
                            else
                            {
                                currentGameState.OpponentData.TryGetValue(opponentKey, out OpponentData opponent);
                                if (opponent != null)
                                {
                                    bool carIsAhead = opponent.ClassPosition < currentGameState.SessionData.ClassPosition;
                                    gotData = PlayOpponentDetailed(opponent, includePosition: false, carIsAhead: carIsAhead);
                                }
                            }
                        }
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
            if (!gotData)
            {
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));

            }
        }

        //private float getOpponentBestLap(List<float> opponentLapTimes, int lapsToCheck)
        //{
        //    if (opponentLapTimes == null && opponentLapTimes.Count == 0)
        //    {
        //        return -1;
        //    }
        //    float bestLap = opponentLapTimes[opponentLapTimes.Count - 1];
        //    int minIndex = opponentLapTimes.Count - lapsToCheck;
        //    for (int i = opponentLapTimes.Count - 1; i >= minIndex; i--)
        //    {
        //        if (opponentLapTimes[i] > 0 && opponentLapTimes[i] < bestLap)
        //        {
        //            bestLap = opponentLapTimes[i];
        //        }
        //    }
        //    return bestLap;
        //}
    }
}
