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
    class MulticlassWarnings : AbstractEvent
    {
        private Dictionary<String, float> separationForFasterClassWarning = new Dictionary<String, float>();
        private Dictionary<String, float> separationForSlowerClassWarning = new Dictionary<String, float>();

        // when calling multi-class warnings, our warning target time is the time between calling the warning, and the
        // gap between the faster and slower car being 'small' - that is, I don't care how long it'll be before I'm passed
        // by the faster guy, I want to know how long it'll be before he's pressuring me to let him through. So we add this
        // many metres to the calculated value.
        // Shit name for this. Use 10 metres because, reasons.
        private float classSeparationAdjustment = 10f;

        private readonly Boolean enableMulticlassWarnings = UserSettings.GetUserSettings().getBoolean("enable_multiclass_messages");
        private readonly Boolean disableSlowerCarWarningsInPracAndQual = UserSettings.GetUserSettings().getBoolean("slow_class_car_warnings_in_race_only");
        private readonly int targetWarningTimeForFasterClass = UserSettings.GetUserSettings().getInt("target_warning_time_for_faster_class_car");
        private readonly int targetWarningTimeForSlowerClass = UserSettings.GetUserSettings().getInt("target_warning_time_for_slower_class_car");


        private const String folderFasterCarsFightingBehindIncludingClassLeader = "multiclass/faster_cars_fighting_behind_inc_class_leader";
        private const String folderFasterCarsBehindIncludingClassLeader = "multiclass/faster_cars_behind_inc_class_leader";
        private const String folderFasterCarsBehindFighting = "multiclass/faster_cars_behind_fighting";
        private const String folderFasterCarsBehind = "multiclass/faster_cars_behind";
        private const String folderFasterCarBehindRacingPlayerForPositionIsClassLeader = "multiclass/faster_car_behind_racing_player_is_class_leader"; // this one's a mouth-full :(
        private const String folderFasterCarBehindIsClassLeader = "multiclass/faster_car_behind_is_class_leader";
        private const String folderFasterCarBehindRacingPlayerForPosition = "multiclass/faster_car_behind_racing_player";
        private const String folderFasterCarBehind = "multiclass/faster_car_behind";

        private const String folderSlowerCarsFightingAheadIncludingClassLeader = "multiclass/slower_cars_fighting_ahead_inc_class_leader";
        private const String folderSlowerCarsAheadIncludingClassLeader = "multiclass/slower_cars_ahead_inc_class_leader";
        private const String folderSlowerCarsAheadFighting = "multiclass/slower_cars_ahead_fighting";
        private const String folderSlowerCarsAhead = "multiclass/slower_cars_ahead";
        private const String folderSlowerCarAheadRacingPlayerForPositionIsClassLeader = "multiclass/slower_car_ahead_racing_player_is_class_leader";
        private const String folderSlowerCarAheadClassLeader = "multiclass/slower_car_ahead_is_class_leader";
        private const String folderSlowerCarAheadRacingPlayerForPosition = "multiclass/slower_car_ahead_racing_player";
        private const String folderSlowerCarAhead = "multiclass/slower_car_ahead";

        private const String folderYouAreBeingCaughtByFasterCars = "multiclass/you_are_being_caught_by_the_faster_cars";
        private const String folderYouAreCatchingSlowerCars = "multiclass/you_are_catching_the_slower_cars";
        private const String folderYouAreBeingCaughtByThe = "multiclass/you_are_being_caught_by_the";
        private const String folderYouAreCatchingThe = "multiclass/you_are_catching_the";
        private const String folderRunners = "multiclass/runners";

        private const String folderFasterClass = "multiclass/it_is_a_faster_class";
        private const String folderSlowerClass = "multiclass/it_is_a_slower_class";
        private const String folderSameClassAsUs = "multiclass/same_class_as_us";

        public const String folderClassStub = "multiclass/";
        private const String folderRunnersSuffix = "_runners";

        private static readonly Dictionary<TrackData.TrackLengthClass, int> minLapsForTrackLengthClass = new Dictionary<TrackData.TrackLengthClass, int>()
        {
            { TrackData.TrackLengthClass.VERY_SHORT, 5 },
            { TrackData.TrackLengthClass.SHORT, 4 },
            { TrackData.TrackLengthClass.MEDIUM, 3 },
            { TrackData.TrackLengthClass.LONG, 2 },
            { TrackData.TrackLengthClass.VERY_LONG, 2 },
        };
        private static readonly Dictionary<TrackData.TrackLengthClass, int> minTimeForTrackLengthClass = new Dictionary<TrackData.TrackLengthClass, int>()
        {
            // in seconds. For v short tracks we expect a lap to be < 1 minute. For very long
            // tracks we expect a lap to be > 6 minutes
            {TrackData.TrackLengthClass.VERY_SHORT, 60},
            {TrackData.TrackLengthClass.SHORT, 90},
            {TrackData.TrackLengthClass.MEDIUM, 120},
            {TrackData.TrackLengthClass.LONG, 210},
            {TrackData.TrackLengthClass.VERY_LONG, 390},
        };

        // we could potentially allow users to define this in the json file if it is desired, by allowing each entry
        // to have a "group_id" or something like that which we can use to construct the sets.
        private static readonly List<HashSet<CarData.CarClassEnum>> similarCarClasses = new List<HashSet<CarData.CarClassEnum>>()
            {
                // production car challenge in iRacing
                new HashSet<CarData.CarClassEnum>
                {
                    CarData.CarClassEnum.SPEC_MIATA,
                    CarData.CarClassEnum.TOYOTA_GR86,
                    CarData.CarClassEnum.CLIO_CUP
                }
            };

        private DateTime nextCheckForOtherCarClasses = DateTime.MinValue;
        private TimeSpan timeBetweenOtherClassChecks = TimeSpan.FromSeconds(4);
        private TimeSpan timeToWaitForOtherClassWarningToSettle = TimeSpan.FromSeconds(6);
        private OtherCarClassWarningData previousCheckOtherClassWarningData;
        private HashSet<string> driverNamesForSlowerClassLastWarnedAbout = new HashSet<string>();
        private HashSet<string> driverNamesForFasterClassLastWarnedAbout = new HashSet<string>();

        private DateTime timeOfLastSingleCarFasterClassWarning = DateTime.MinValue;
        private DateTime timeOfLastMultipleCarFasterClassWarning = DateTime.MinValue;
        private DateTime timeOfLastSingleCarSlowerClassWarning = DateTime.MinValue;
        private DateTime timeOfLastMultipleCarSlowerClassWarning = DateTime.MinValue;
        
        Dictionary<String, float> bestTimesByClass = new Dictionary<String, float>();

        enum ClassSpeedComparison {SLOWER, FASTER, UNKNOWN};

        // different approaches here for lapping and being lapped. The first faster car we see will be class leader,
        // so it's always appropriate to say "the faster class leaders are bearing down on us" or similar. For the slower
        // class, the first car we see will be in last place, so he might have dropped off the back of the field.
        private Boolean caughtByFasterClassInThisSession = false;
        private Boolean caughtSlowerClassInThisSession = false;

        private GameStateData currentGameState;

        static MulticlassWarnings()
        {
        }

        // distance ahead where we consider slower cars. As we'll be behind the opponent, the separation value is negative
        private const float slowerCarWarningZoneStart = -15;
        private const float slowerCarWarningZoneEndMax = -100;
        private const float slowerCarWarningZoneEndShortTracks = -150;
        private const float slowerCarWarningZoneEndNormalTracks = -200;
        private const float slowerCarWarningZoneEndLongTracks = -300;
        private float slowerCarWarningZoneEndToUse = -200;

        // distance behind where we consider faster cars
        private const float fasterCarWarningZoneStartShortTracks = 100;
        private const float fasterCarWarningZoneStartMin = 100;
        private const float fasterCarWarningZoneStartNormalTracks = 200;
        private const float fasterCarWarningZoneStartLongTracks = 250;
        private float fasterCarWarningZoneStartToUse = 200;
        private const float fasterCarWarningZoneEnd = 15;

        private HashSet<CarData.CarClassEnum> similarClasses;

        // cars within this many metres of each other will be considered as 'fighting for position'
        private const float maxSeparateToBeConsideredFighting = 30;
        
        public MulticlassWarnings(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;            
        }

        public override List<SessionType> applicableSessionTypes
        {
            get { return new List<SessionType> { SessionType.Race, SessionType.Practice, SessionType.Qualify }; }
        }

        public override List<SessionPhase> applicableSessionPhases
        {
            get { return new List<SessionPhase> { SessionPhase.Green }; }
        }

        public override void clearState()
        {
            nextCheckForOtherCarClasses = DateTime.MinValue;
            previousCheckOtherClassWarningData = null;
            driverNamesForSlowerClassLastWarnedAbout.Clear();
            driverNamesForFasterClassLastWarnedAbout.Clear();
            timeOfLastSingleCarFasterClassWarning = DateTime.MinValue;
            timeOfLastMultipleCarFasterClassWarning = DateTime.MinValue;
            timeOfLastSingleCarSlowerClassWarning = DateTime.MinValue;
            timeOfLastMultipleCarSlowerClassWarning = DateTime.MinValue;
            caughtByFasterClassInThisSession = false;
            caughtSlowerClassInThisSession = false;
            bestTimesByClass.Clear();
            separationForFasterClassWarning.Clear();
            separationForSlowerClassWarning.Clear();
            similarClasses = null;
        }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            if (CrewChief.qualifyingAndNoSpotterWhenQualifying)
            {
                return;
            }
            if (similarClasses == null)
            {
                similarClasses = new HashSet<CarData.CarClassEnum>();
                foreach (var group in similarCarClasses)
                {
                    if (group.Contains(currentGameState.carClass.carClassEnum))
                    {
                        similarClasses.UnionWith(group);
                    }
                }
                // remove the self reference
                similarClasses.Remove(currentGameState.carClass.carClassEnum);
                if (similarClasses.Count > 0)
                {
                    Log.Debug(() => $"Ignoring the following classes unless they are lapping us: {String.Join(",", similarClasses)}");
                }
            }

            this.currentGameState = currentGameState;
            int minLapsCompletedBeforeGettingBestTimes = currentGameState.SessionData.SessionType == SessionType.Race ? 2 : 0;
            if (!enableMulticlassWarnings ||
                GameStateData.onManualFormationLap || GameStateData.NumberOfClasses == 1 || CrewChief.forceSingleClass ||
                currentGameState.SessionData.iRacingLoneQualify ||
                currentGameState.SessionData.TrackDefinition == null || 
                (currentGameState.SessionData.SessionType == SessionType.Race && 
                    currentGameState.SessionData.CompletedLaps < minLapsForTrackLengthClass[currentGameState.SessionData.TrackDefinition.trackLengthClass]) ||
                (currentGameState.SessionData.SessionType != SessionType.Race &&
                    currentGameState.SessionData.SessionRunningTime < minTimeForTrackLengthClass[currentGameState.SessionData.TrackDefinition.trackLengthClass]) ||
                currentGameState.PitData.InPitlane ||
                currentGameState.PositionAndMotionData.CarSpeed < 5 || 
                currentGameState.PositionAndMotionData.DistanceRoundTrack <= 0)
            {
                return;
            }
            // update the best lap for each class - do this every sector once we've completed 2 laps
            if (currentGameState.SessionData.CompletedLaps >= minLapsCompletedBeforeGettingBestTimes && currentGameState.SessionData.IsNewSector)
            {
                getBestTimesByClass(currentGameState);
            }
            slowerCarWarningZoneEndToUse = slowerCarWarningZoneEndNormalTracks;
            fasterCarWarningZoneStartToUse = fasterCarWarningZoneStartNormalTracks;
            if (currentGameState.SessionData.JustGoneGreen && currentGameState.SessionData.TrackDefinition != null)
            {
                switch (currentGameState.SessionData.TrackDefinition.trackLengthClass)
                {
                    case TrackData.TrackLengthClass.LONG:
                    case TrackData.TrackLengthClass.VERY_LONG:
                        // adjust the class distance thresholds for longer tracks at session start
                        slowerCarWarningZoneEndToUse = slowerCarWarningZoneEndLongTracks;
                        fasterCarWarningZoneStartToUse = fasterCarWarningZoneStartLongTracks;
                        break;
                    case TrackData.TrackLengthClass.SHORT:
                    case TrackData.TrackLengthClass.VERY_SHORT:
                        // adjust the class distance thresholds for shorted tracks at session start
                        slowerCarWarningZoneEndToUse = slowerCarWarningZoneEndShortTracks;
                        fasterCarWarningZoneStartToUse = fasterCarWarningZoneStartShortTracks;
                        break;
                }
            }
            if (currentGameState.Now > nextCheckForOtherCarClasses)
            {
                OtherCarClassWarningData otherClassWarningData = getOtherCarClassWarningData(currentGameState);
                if (otherClassWarningData != null && (otherClassWarningData.numFasterCars > 0 || otherClassWarningData.numSlowerCars > 0))
                {
                    if (previousCheckOtherClassWarningData == null)
                    {
                        previousCheckOtherClassWarningData = otherClassWarningData;
                        // wait a while and check again before announcing
                        nextCheckForOtherCarClasses = currentGameState.Now.Add(timeToWaitForOtherClassWarningToSettle);
                    }
                    else
                    {
                        // check if this data is consistent with the previous data and that we've not warned about all these cars
                        if (otherClassWarningData.canAnnounce(previousCheckOtherClassWarningData,
                                driverNamesForFasterClassLastWarnedAbout, 
                                driverNamesForSlowerClassLastWarnedAbout,
                                currentGameState.Now,
                                timeOfLastSingleCarFasterClassWarning, 
                                timeOfLastMultipleCarFasterClassWarning, 
                                timeOfLastSingleCarSlowerClassWarning, 
                                timeOfLastMultipleCarSlowerClassWarning))
                        {
                            previousCheckOtherClassWarningData = null;
                            // do the announcing - need to decide which to prefer - read multiple?
                            Console.WriteLine(otherClassWarningData.ToString());

                            // when we're being caught by faster cars, if this is the first time in the session, and this guy is the leader
                            // (just in case this car has pitted and is a lap down or something) use a different message
                            if (currentGameState.SessionData.SessionType == SessionType.Race && 
                                !caughtByFasterClassInThisSession && 
                                otherClassWarningData.numFasterCars > 0 && 
                                otherClassWarningData.fasterCarsIncludeClassLeader)
                            {
                                caughtByFasterClassInThisSession = true;
                                timeOfLastMultipleCarFasterClassWarning = currentGameState.Now;
                                this.driverNamesForFasterClassLastWarnedAbout = new HashSet<string>(otherClassWarningData.fasterCarDriverNames);
                                // if we have the class name, use it
                                if (otherClassWarningData.carClassOfClosestFasterCar != null)
                                {
                                    String classNameToRead = carClassEnumToSoundFolder(otherClassWarningData.carClassOfClosestFasterCar.carClassEnum);
                                    if (classNameToRead != null)
                                    {
                                        String classWithRunnersSuffix = classNameToRead + folderRunnersSuffix;
                                        if (SoundCache.availableSounds.Contains(classWithRunnersSuffix))
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(
                                                "being_caught_by_known_car_class_runners", 5,
                                                messageFragments: MessageContents(folderYouAreBeingCaughtByThe,
                                                    classWithRunnersSuffix), abstractEvent: this,
                                                type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                        else
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(
                                                "being_caught_by_known_car_class_runners", 5,
                                                messageFragments: MessageContents(folderYouAreBeingCaughtByThe,
                                                    classNameToRead, folderRunners), abstractEvent: this,
                                                type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                    }
                                    else
                                    {
                                        // unfortunate code dupe
                                        audioPlayer.playMessageImmediately(new QueuedMessage(folderYouAreBeingCaughtByFasterCars, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                    }
                                }
                                else
                                {
                                    audioPlayer.playMessageImmediately(new QueuedMessage(folderYouAreBeingCaughtByFasterCars, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                }
                            }

                            else if (otherClassWarningData.numFasterCars >= 1) // (superfluous test to clarify the logic)
                            {   // Deal with faster cars in another class
                                if (otherClassWarningData.numFasterCars > 1)
                                {
                                    timeOfLastMultipleCarFasterClassWarning = currentGameState.Now;
                                    this.driverNamesForFasterClassLastWarnedAbout = new HashSet<string>(otherClassWarningData.fasterCarDriverNames);
                                    if (currentGameState.SessionData.SessionType == SessionType.Race && otherClassWarningData.fasterCarsIncludeClassLeader)
                                    {
                                        if (otherClassWarningData.fasterCarsRacingForPosition)
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(folderFasterCarsFightingBehindIncludingClassLeader, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                        else
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(folderFasterCarsBehindIncludingClassLeader, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                    }
                                    else if (currentGameState.SessionData.SessionType == SessionType.Race && otherClassWarningData.fasterCarsRacingForPosition)
                                    {
                                        audioPlayer.playMessageImmediately(new QueuedMessage(folderFasterCarsBehindFighting, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                    }
                                    else
                                    {
                                        audioPlayer.playMessageImmediately(new QueuedMessage(folderFasterCarsBehind, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                    }
                                    // don't bother with 'no blue flag' warning here - this only really makes sense if all the 
                                    // cars in the group are racing the player for position. Do we need to fix this in the OtherCarClassWarningData data?

                                    playExtraReputationAdvice(currentGameState, otherClassWarningData.fasterCarDriverNames, ahead: false);
                                }
                                else if (otherClassWarningData.numFasterCars == 1)
                                {
                                    timeOfLastSingleCarFasterClassWarning = currentGameState.Now;
                                    this.driverNamesForFasterClassLastWarnedAbout = new HashSet<string>(otherClassWarningData.fasterCarDriverNames);
                                    if (currentGameState.SessionData.SessionType == SessionType.Race && otherClassWarningData.fasterCarsIncludeClassLeader)
                                    {
                                        if (otherClassWarningData.fasterCarIsRacingPlayerForPosition)
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(folderFasterCarBehindRacingPlayerForPositionIsClassLeader, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                        else
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(folderFasterCarBehindIsClassLeader, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                    }
                                    else if (currentGameState.SessionData.SessionType == SessionType.Race && otherClassWarningData.fasterCarIsRacingPlayerForPosition)
                                    {
                                        audioPlayer.playMessageImmediately(new QueuedMessage(folderFasterCarBehindRacingPlayerForPosition, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                    }
                                    else
                                    {
                                        audioPlayer.playMessageImmediately(new QueuedMessage(folderFasterCarBehind, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                    }

                                    playExtraReputationAdvice(currentGameState, otherClassWarningData.fasterCarDriverNames, false);
                                }
                            }

                            if (otherClassWarningData.numSlowerCars >= 1) // (superfluous test to clarify the logic)
                            {   // Now deal with slower cars in another class
                                if (otherClassWarningData.numSlowerCars > 1)
                                {
                                    timeOfLastMultipleCarSlowerClassWarning = currentGameState.Now;
                                    this.driverNamesForSlowerClassLastWarnedAbout = new HashSet<string>(otherClassWarningData.slowerCarDriverNames);

                                    // for slower cars, the first car we see might have dropped off the back of the field so we only use the "you're catching
                                    // the slower cars" message if the first slow car we see is part of a group
                                    if (currentGameState.SessionData.SessionType == SessionType.Race && !caughtSlowerClassInThisSession)
                                    {
                                        // if we have the class name, use it
                                        if (otherClassWarningData.carClassOfClosestSlowerCar != null)
                                        {
                                            String classNameToRead = carClassEnumToSoundFolder(otherClassWarningData.carClassOfClosestSlowerCar.carClassEnum);
                                            if (classNameToRead != null)
                                            {
                                                String classWithRunnersSuffix = classNameToRead + folderRunnersSuffix;
                                                if (SoundCache.availableSounds.Contains(classWithRunnersSuffix))
                                                {
                                                    audioPlayer.playMessageImmediately(new QueuedMessage(
                                                        "catching_known_car_class_runners", 5,
                                                        messageFragments: MessageContents(folderYouAreCatchingThe,
                                                            classWithRunnersSuffix), abstractEvent: this,
                                                        type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                                }
                                                else
                                                {
                                                    audioPlayer.playMessageImmediately(new QueuedMessage(
                                                        "catching_known_car_class_runners", 5,
                                                        messageFragments: MessageContents(folderYouAreCatchingThe,
                                                            classNameToRead, folderRunners), abstractEvent: this,
                                                        type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                                }
                                            }
                                            else
                                            {
                                                // unfortunate code dupe
                                                audioPlayer.playMessageImmediately(new QueuedMessage(folderYouAreCatchingSlowerCars, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                            }
                                        }
                                        else
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(folderYouAreCatchingSlowerCars, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                    }
                                    else if (currentGameState.SessionData.SessionType == SessionType.Race && otherClassWarningData.slowerCarsIncludeClassLeader)
                                    {
                                        if (otherClassWarningData.slowerCarsRacingForPosition)
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(folderSlowerCarsFightingAheadIncludingClassLeader, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                        else
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(folderSlowerCarsAheadIncludingClassLeader, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                    }
                                    else if (currentGameState.SessionData.SessionType == SessionType.Race && otherClassWarningData.slowerCarsRacingForPosition)
                                    {
                                        audioPlayer.playMessageImmediately(new QueuedMessage(folderSlowerCarsAheadFighting, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                    }
                                    else if (currentGameState.SessionData.SessionType == SessionType.Race || !disableSlowerCarWarningsInPracAndQual)
                                    {
                                        audioPlayer.playMessageImmediately(new QueuedMessage(folderSlowerCarsAhead, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                    }
                                    caughtSlowerClassInThisSession = true;
                                    // don't bother with 'no blue flag' warning here - this only really makes sense if all the 
                                    // cars in the group are racing the player for position. Do we need to fix this in the OtherCarClassWarningData data?

                                    playExtraReputationAdvice(currentGameState, otherClassWarningData.slowerCarDriverNames, true);
                                }
                                else if (otherClassWarningData.numSlowerCars == 1)
                                {
                                    timeOfLastSingleCarSlowerClassWarning = currentGameState.Now;
                                    this.driverNamesForSlowerClassLastWarnedAbout = new HashSet<string>(otherClassWarningData.slowerCarDriverNames);
                                    if (currentGameState.SessionData.SessionType == SessionType.Race && otherClassWarningData.slowerCarsIncludeClassLeader)
                                    {
                                        if (otherClassWarningData.slowerCarIsRacingPlayerForPosition)
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(folderSlowerCarAheadRacingPlayerForPositionIsClassLeader, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                        else
                                        {
                                            audioPlayer.playMessageImmediately(new QueuedMessage(folderSlowerCarAheadClassLeader, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                        }
                                    }
                                    else if (currentGameState.SessionData.SessionType == SessionType.Race && otherClassWarningData.slowerCarIsRacingPlayerForPosition)
                                    {
                                        audioPlayer.playMessageImmediately(new QueuedMessage(folderSlowerCarAheadRacingPlayerForPosition, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                    }
                                    else if (currentGameState.SessionData.SessionType == SessionType.Race || !disableSlowerCarWarningsInPracAndQual)
                                    {
                                        audioPlayer.playMessageImmediately(new QueuedMessage(folderSlowerCarAhead, 5, abstractEvent: this, type: SoundType.IMPORTANT_MESSAGE, priority: 0));
                                    }
                                    caughtSlowerClassInThisSession = true;

                                    playExtraReputationAdvice(currentGameState, otherClassWarningData.slowerCarDriverNames, true);
                                }
                            }
                        }
                        // now wait a while before checking again - how long should we wait here? What if the 'canAnnounce' check fails?
                        nextCheckForOtherCarClasses = currentGameState.Now.Add(TimeSpan.FromSeconds(30));
                    }
                }
                else
                {
                    nextCheckForOtherCarClasses = currentGameState.Now.Add(timeBetweenOtherClassChecks);
                }
            }
        }


        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.IS_CAR_AHEAD_MY_CLASS,
            SpeechCommands.ID.IS_CAR_BEHIND_MY_CLASS,
            SpeechCommands.ID.WHAT_CLASS_IS_CAR_AHEAD,
            SpeechCommands.ID.WHAT_CLASS_IS_CAR_BEHIND,
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
            Boolean gotResponse = false;
            if (currentGameState != null)
            {
                switch (cmd)
                {
                    case SpeechCommands.ID.WHAT_CLASS_IS_CAR_AHEAD:
                    {
                        String opponentKey = currentGameState.getOpponentKeyInFrontOnTrack();
                        if (opponentKey != null)
                        {
                            gotResponse = true;
                            audioPlayer.playMessageImmediately(new QueuedMessage(getClassSound(currentGameState.OpponentData[opponentKey]), 0));
                        }

                        break;
                    }
                    case SpeechCommands.ID.WHAT_CLASS_IS_CAR_BEHIND:
                    {
                        String opponentKey = currentGameState.getOpponentKeyBehindOnTrack();
                        if (opponentKey != null)
                        {
                            gotResponse = true;
                            audioPlayer.playMessageImmediately(new QueuedMessage(getClassSound(currentGameState.OpponentData[opponentKey]), 0));
                        }

                        break;
                    }
                    case SpeechCommands.ID.IS_CAR_AHEAD_MY_CLASS:
                    {
                        String opponentKey = currentGameState.getOpponentKeyInFrontOnTrack();
                        if (opponentKey != null)
                        {
                            gotResponse = true;
                            CarData.CarClass opponentClass = currentGameState.OpponentData[opponentKey].CarClass;
                            if (CarData.IsCarClassEqual(currentGameState.carClass, opponentClass))
                            {
                                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderYes, 0));
                            }
                            else
                            {
                                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNo, 0));
                            }
                        }

                        break;
                    }
                    case SpeechCommands.ID.IS_CAR_BEHIND_MY_CLASS:
                    {
                        String opponentKey = currentGameState.getOpponentKeyBehindOnTrack();
                        if (opponentKey != null)
                        {
                            gotResponse = true;
                            CarData.CarClass opponentClass = currentGameState.OpponentData[opponentKey].CarClass;
                            if (CarData.IsCarClassEqual(currentGameState.carClass, opponentClass))
                            {
                                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderYes, 0));
                            }
                            else
                            {
                                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNo, 0));
                            }
                        }

                        break;
                    }
                }
            }
            if (!gotResponse)
            {
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
            }
        }

        private String getClassSound(OpponentData opponentToCheck)
        {
            CarData.CarClass opponentClass = opponentToCheck.CarClass;
            if (CarData.IsCarClassEqual(opponentClass, currentGameState.carClass))
            {
                return folderSameClassAsUs;
            }
            String classNameToRead = carClassEnumToSoundFolder(opponentClass.carClassEnum);
            if (classNameToRead != null)
            {
                return classNameToRead;
            }
            else
            {
                ClassSpeedComparison opponentComparisonToPlayer = getClassSpeedComparisonToPlayer(opponentClass.getClassIdentifier());
                switch (opponentComparisonToPlayer)
                {
                    case ClassSpeedComparison.FASTER:
                        return folderFasterClass;
                    case ClassSpeedComparison.SLOWER:
                        return folderSlowerClass;
                }
            }
            return AudioPlayer.folderNoData;
        }

        public static string carClassEnumToSoundFolder(CarData.CarClassEnum carClassEnum)
        {
            string folder = folderClassStub + CarData.getCarClassFromEnum(carClassEnum);
            if (!SoundCache.availableSounds.Contains(folder))
            {
                folder = null;
            }
            return folder;
        }

        // it would have been nice to construct this as part of a single message but that would be a major refactor
        // so we stick this on as an addendum after the main message about a nearby multiclass opponent.
        private void playExtraReputationAdvice(GameStateData currentGameState, HashSet<String> names, Boolean ahead)
        {
            string opponentKey;
            if (ahead)
            {
                opponentKey = currentGameState.getOpponentKeyInFrontOnTrack();
            }
            else
            {
                opponentKey = currentGameState.getOpponentKeyBehindOnTrack();
            }
            if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out OpponentData opponent))
            {
                if (names.Contains(opponent.DriverRawName))
                {
                    var advice = MkFreshReputationAdvice(opponent, ahead);
                    if (advice.Count > 0)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("multiclass_rep", 5, messageFragments: advice, priority: 0));
                    }
                }
            }
        }

        private class OtherCarClassWarningData
        {
            public int numFasterCars;
            public int numSlowerCars;
            public Boolean fasterCarsIncludeClassLeader;
            public Boolean slowerCarsIncludeClassLeader;
            public Boolean fasterCarsRacingForPosition;
            public Boolean slowerCarsRacingForPosition;
            public Boolean fasterCarIsRacingPlayerForPosition = false;
            public Boolean slowerCarIsRacingPlayerForPosition = false;
            public HashSet<string> fasterCarDriverNames;
            public HashSet<string> slowerCarDriverNames;
            public CarData.CarClass carClassOfClosestFasterCar;
            public CarData.CarClass carClassOfClosestSlowerCar;

            public OtherCarClassWarningData(int numFasterCars, 
                int numSlowerCars,
                Boolean fasterCarsIncludeClassLeader,
                Boolean slowerCarsIncludeClassLeader,
                Boolean fasterCarsRacingForPosition, 
                Boolean slowerCarsRacingForPosition, 
                Boolean fasterCarIsRacingPlayerForPosition, 
                Boolean slowerCarIsRacingPlayerForPosition,
                HashSet<string> fasterCarDriverNames, 
                HashSet<string> slowerCarDriverNames, 
                CarData.CarClass carClassOfClosestFasterCar,
                CarData.CarClass carClassOfClosestSlowerCar)
            {
                this.numFasterCars = numFasterCars;
                this.numSlowerCars = numSlowerCars;
                this.fasterCarsIncludeClassLeader = fasterCarsIncludeClassLeader;
                this.slowerCarsIncludeClassLeader = slowerCarsIncludeClassLeader;
                this.fasterCarsRacingForPosition = fasterCarsRacingForPosition;
                this.slowerCarsRacingForPosition = slowerCarsRacingForPosition;
                this.fasterCarIsRacingPlayerForPosition = fasterCarIsRacingPlayerForPosition;
                this.slowerCarIsRacingPlayerForPosition = slowerCarIsRacingPlayerForPosition;
                this.fasterCarDriverNames = fasterCarDriverNames;
                this.slowerCarDriverNames = slowerCarDriverNames;
                this.carClassOfClosestFasterCar = carClassOfClosestFasterCar;
                this.carClassOfClosestSlowerCar = carClassOfClosestSlowerCar;
            }

            // checks if the current warning data is consistent with the previous warning data and a bunch of other stuff
            // The goal here is to ensure we play significant warnings without spamming the player with messages
            public Boolean canAnnounce(OtherCarClassWarningData previousOtherCarClassWarningData,
                HashSet<string> fasterDriversAtLastAnnouncement,
                HashSet<string> slowerDriversAtLastAnnouncement, 
                DateTime now,
                DateTime timeOfLastSingleCarFasterClassWarning, 
                DateTime timeOfLastMultipleCarFasterClassWarning, 
                DateTime timeOfLastSingleCarSlowerClassWarning, 
                DateTime timeOfLastMultipleCarSlowerClassWarning)
            {
                if ((this.numFasterCars > 0 && previousOtherCarClassWarningData.numFasterCars > 0) ||
                    (this.numSlowerCars > 0 && previousOtherCarClassWarningData.numSlowerCars > 0))
                {
                    Boolean hasNewFasterDriverToWarnAbout = Enumerable.Except(fasterCarDriverNames, fasterDriversAtLastAnnouncement).Any();
                    Boolean hasNewSlowerDriverToWarnAbout = Enumerable.Except(slowerCarDriverNames, slowerDriversAtLastAnnouncement).Any();


                    if (hasNewFasterDriverToWarnAbout || hasNewSlowerDriverToWarnAbout)
                    {
                        // try to estimate how important this warning might be
                        if (numSlowerCars == 1 &&
                            (slowerCarsIncludeClassLeader || slowerCarIsRacingPlayerForPosition ||
                                now > timeOfLastSingleCarSlowerClassWarning.AddSeconds(60)))
                        {
                            return true;
                        }
                        else if (numSlowerCars > 1 &&
                            (slowerCarsIncludeClassLeader || slowerCarsRacingForPosition ||
                                now > timeOfLastMultipleCarSlowerClassWarning.AddSeconds(55)))
                        {
                            return true;
                        }
                        else if (numFasterCars == 1 &&
                            (fasterCarsIncludeClassLeader || fasterCarIsRacingPlayerForPosition ||
                                now > timeOfLastSingleCarFasterClassWarning.AddSeconds(50)))
                        {
                            return true;
                        }
                        else if (numFasterCars > 1 &&
                            (fasterCarsIncludeClassLeader || fasterCarsRacingForPosition ||
                                now > timeOfLastMultipleCarFasterClassWarning.AddSeconds(45)))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public override string ToString()
            {
                return "num faster cars closing = " + numFasterCars + " faster cars battling for position = " +
                    fasterCarsRacingForPosition + " faster cars include class leader = " + fasterCarsIncludeClassLeader +
                    " num slower cars closing = " + numSlowerCars + " slower cars battling for position = " +
                    slowerCarsRacingForPosition + " slower cars include class leader = " + slowerCarsIncludeClassLeader +
                    " faster car is racing player for position = " + fasterCarIsRacingPlayerForPosition +
                    " slower car is racing player for position = " + slowerCarIsRacingPlayerForPosition + 
                    " fasterCarDriverNames = " + String.Join(", ", fasterCarDriverNames) +
                    " slowerCarDriverNames = " + String.Join(", ", slowerCarDriverNames);
            }
        }

        private void getBestTimesByClass(GameStateData currentGameState)
        {
            // this is used by the main thread and the speech recogniser thread, both of which may update it, so lock before using
            lock (this)
            {
                String playerCarClassIdentifier = currentGameState.carClass.getClassIdentifier();
                float playerBestTime = currentGameState.SessionData.PlayerLapTimeSessionBest;
                float estimate = currentGameState.SessionData.EstimatedLapTime;
                if (estimate > 0 && (playerBestTime <= 0 || estimate < playerBestTime))
                {
                    playerBestTime = estimate;
                }
                if (playerBestTime > 0)
                {
                    // there's a lot of code dupe here with iteration of the opponents
                    if (bestTimesByClass.TryGetValue(playerCarClassIdentifier, out float existingBest))
                    {
                        if (playerBestTime < existingBest)
                        {
                            bestTimesByClass[playerCarClassIdentifier] = playerBestTime;
                        }
                    } else
                    {
                        bestTimesByClass[playerCarClassIdentifier] = playerBestTime;
                    }
                }
                foreach (OpponentData opponentData in currentGameState.OpponentData.Values)
                {
                    String opponentCarClassIdentifier = opponentData.CarClass.getClassIdentifier();
                    float bestLap = opponentData.CurrentBestLapTime;
                    estimate = opponentData.EstimatedLapTime;
                    if (estimate > 0 && (bestLap <= 0 || estimate < bestLap))
                    {
                        bestLap = estimate;
                    }
                    if (bestLap > 0)
                    {
                        if (bestTimesByClass.TryGetValue(opponentCarClassIdentifier, out float existingBest))
                        {
                            if (bestLap < existingBest)
                            {
                                bestTimesByClass[opponentCarClassIdentifier] = bestLap;
                            }
                        }
                        else
                        {
                            bestTimesByClass[opponentCarClassIdentifier] = bestLap;
                        }
                    }
                }
                float playerClassBestLap = playerBestTime;
                if (bestTimesByClass.TryGetValue(playerCarClassIdentifier, out playerClassBestLap))
                {
                    float playerClassAverageSpeed = currentGameState.SessionData.TrackDefinition.trackLength / playerClassBestLap;
                    foreach (String carClassIdentifier in bestTimesByClass.Keys)
                    {
                        if (String.Equals(playerCarClassIdentifier, carClassIdentifier))
                        {
                            continue;
                        }
                        float opponentClassAverageSpeed = currentGameState.SessionData.TrackDefinition.trackLength / bestTimesByClass[carClassIdentifier];
                        if (opponentClassAverageSpeed > playerClassAverageSpeed)
                        {
                            separationForFasterClassWarning[carClassIdentifier] =
                                Math.Max(fasterCarWarningZoneStartMin, targetWarningTimeForFasterClass * (opponentClassAverageSpeed - playerClassAverageSpeed) + classSeparationAdjustment);
                        }
                        else
                        {
                            // this will be negative, because we're behind
                            separationForSlowerClassWarning[carClassIdentifier] =
                                Math.Min(slowerCarWarningZoneEndMax, targetWarningTimeForSlowerClass * (opponentClassAverageSpeed - playerClassAverageSpeed) - classSeparationAdjustment);
                        }
                    }
                }                
            }
        }

        // returns ClassSpeedComparison.FASTER if the opponent class is faster than the player's class
        private ClassSpeedComparison getClassSpeedComparisonToPlayer(String opponentClassIdentifier)
        {
            // work out if this opponent is faster or slower
            float playerClassBestLap = 0;
            float opponentClassBestLap = 0;
            Boolean gotOpponentClassBestLap = false;
            Boolean gotPlayerClassBestLap = false;
            // the getBestTimesByClass dictionary may be updated by another Thread at any point, so lock on this before using it
            lock (this)
            {
                if (bestTimesByClass.Count == 0)
                {
                    getBestTimesByClass(currentGameState);
                }
                String playerCarClassId = currentGameState.carClass.getClassIdentifier();
                foreach (String carClassIdentifier in bestTimesByClass.Keys)
                {
                    if (!gotOpponentClassBestLap && String.Equals(carClassIdentifier, opponentClassIdentifier))
                    {
                        opponentClassBestLap = bestTimesByClass[carClassIdentifier];
                        gotOpponentClassBestLap = true;
                    }
                    if (!gotPlayerClassBestLap && String.Equals(carClassIdentifier, playerCarClassId))
                    {
                        playerClassBestLap = bestTimesByClass[carClassIdentifier];
                        gotPlayerClassBestLap = true;
                    }
                    if (gotOpponentClassBestLap && gotPlayerClassBestLap)
                    {
                        break;
                    }
                }
                if (playerClassBestLap > 0 && opponentClassBestLap > 0)
                {
                    return playerClassBestLap > opponentClassBestLap ? ClassSpeedComparison.FASTER : ClassSpeedComparison.SLOWER;
                }
            }
            return ClassSpeedComparison.UNKNOWN;
        }

        // to be called every couple of seconds, not every tick
        private OtherCarClassWarningData getOtherCarClassWarningData(GameStateData currentGameState)
        {
            float playerDistanceRoundTrack = currentGameState.PositionAndMotionData.DistanceRoundTrack;
            float trackLength = currentGameState.SessionData.TrackDefinition.trackLength;
            float halfTrackLength = trackLength / 2f;

            int numFasterCars = 0;
            int numSlowerCars = 0;
            Dictionary<int, List<float>> fasterCarLapCountAndSeparations = new Dictionary<int, List<float>>();
            Dictionary<int, List<float>> slowerCarLapCountAndSeparations = new Dictionary<int, List<float>>();
            Boolean fasterCarsIncludeClassLeader = false;
            Boolean slowerCarsIncludeClassLeader = false;
            Boolean fasterCarsRacingForPosition = false;
            Boolean slowerCarsRacingForPosition = false;
            // these are edge cases - a faster car is immediately behind us on track and is immediately behind us overall,
            // or a slower car is immediately ahead of us on track and is immediately ahead of us overall
            Boolean fasterCarIsRacingPlayerForPosition = false;
            Boolean slowerCarIsRacingPlayerForPosition = false;
            HashSet<string> fasterCarDriverNames = new HashSet<string>();
            HashSet<string> slowerCarDriverNames = new HashSet<string>();

            float closestFasterCarSeparation = float.MaxValue;
            float closestSlowerCarSeparation = float.MinValue;
            CarData.CarClass carClassOfClosestFasterCar = null;
            CarData.CarClass carClassOfClosestSlowerCar = null;

            foreach (OpponentData opponentData in currentGameState.OpponentData.Values)
            {
                if (CarData.IsCarClassEqual(opponentData.CarClass, currentGameState.carClass) ||
                    // excluding similar classes does mean we will rely on the game to give us blue flags
                    // since we won't get a pre-warning from the Timings "car behind on track" feature.
                    // if they are laps ahead of us, then fair enough... they can be treated as a faster class.
                    (similarClasses.Contains(opponentData.CarClass.carClassEnum) && opponentData.LapsDeltaFront <= 0) ||
                    opponentData.CurrentBestLapTime <= 0 || opponentData.InPits || opponentData.DistanceRoundTrack <= 0 ||
                    opponentData.Speed < 5 || !opponentData.IsActive)
                {
                    continue;
                }
                ClassSpeedComparison classSpeedComparisonToPlayer = getClassSpeedComparisonToPlayer(opponentData.CarClass.getClassIdentifier());
                if (classSpeedComparisonToPlayer == ClassSpeedComparison.UNKNOWN)
                {
                    continue;
                }
                // separation is +ve when the player is in front, -ve when he's behind
                float separation = playerDistanceRoundTrack - opponentData.DistanceRoundTrack;
                if (separation > halfTrackLength)
                {
                    separation = trackLength - separation;
                }
                else if (separation < -1 * halfTrackLength)
                {
                    separation = trackLength + separation;
                }
                float separationToUse;
                if (classSpeedComparisonToPlayer == ClassSpeedComparison.FASTER)
                {
                    if (!separationForFasterClassWarning.TryGetValue(opponentData.CarClass.getClassIdentifier(), out separationToUse))
                    {
                        separationToUse = fasterCarWarningZoneStartToUse;
                    }   
                }
                else
                {
                    if (!separationForSlowerClassWarning.TryGetValue(opponentData.CarClass.getClassIdentifier(), out separationToUse))
                    {
                        separationToUse = slowerCarWarningZoneEndToUse;
                    }
                }
                if (classSpeedComparisonToPlayer == ClassSpeedComparison.FASTER && separation > fasterCarWarningZoneEnd && separation < separationToUse)
                {
                    // player is ahead of a faster class car
                    numFasterCars++;
                    // remember separation for faster cars (player in front) is positive, and for slower cars (player behind) it's positive
                    if (separation < closestFasterCarSeparation)
                    {
                        closestFasterCarSeparation = separation;
                        carClassOfClosestFasterCar = opponentData.CarClass;
                    }
                    fasterCarDriverNames.Add(opponentData.DriverRawName);
                    if (opponentData.ClassPosition == 1)
                    {
                        fasterCarsIncludeClassLeader = true;
                    }
                    if (opponentData.OverallPosition == currentGameState.SessionData.OverallPosition + 1)
                    {
                        fasterCarIsRacingPlayerForPosition = true;
                    }
                    // get the separations of other cars we've already processed which are on the same lap as this opponent:
                    if (!fasterCarsRacingForPosition)
                    {
                        List<float> separationsForOtherCarsOnSameLap;
                        if (fasterCarLapCountAndSeparations.TryGetValue(opponentData.CompletedLaps, out separationsForOtherCarsOnSameLap))
                        {
                            // if any of them are close to each other, set the boolean flag
                            foreach (float otherSeparation in separationsForOtherCarsOnSameLap)
                            {
                                if (Math.Abs(otherSeparation - separation) < maxSeparateToBeConsideredFighting)
                                {
                                    fasterCarsRacingForPosition = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            separationsForOtherCarsOnSameLap = new List<float>();
                            separationsForOtherCarsOnSameLap.Add(separation);
                            fasterCarLapCountAndSeparations.Add(opponentData.CompletedLaps, separationsForOtherCarsOnSameLap);
                        }
                    }
                }
                // this separation check looks odd because the separation value is negative (player is behind) so the < and > appear reversed
                else if (classSpeedComparisonToPlayer == ClassSpeedComparison.SLOWER && separation < slowerCarWarningZoneStart && separation > separationToUse)
                {
                    // player is behind a slower class car
                    numSlowerCars++;
                    // remember separation for faster cars (player in front) is positive, and for slower cars (player behind) it's positive
                    if (separation > closestSlowerCarSeparation)
                    {
                        closestSlowerCarSeparation = separation;
                        carClassOfClosestSlowerCar = opponentData.CarClass;
                    }
                    slowerCarDriverNames.Add(opponentData.DriverRawName);
                    if (opponentData.ClassPosition == 1)
                    {
                        slowerCarsIncludeClassLeader = true;
                    }
                    if (opponentData.OverallPosition == currentGameState.SessionData.OverallPosition - 1)
                    {
                        slowerCarIsRacingPlayerForPosition = true;
                    }
                    // get the separations of other cars we've already processed which are on the same lap as this opponent:
                    if (!slowerCarsRacingForPosition)
                    {
                        List<float> separationsForOtherCarsOnSameLap;
                        if (slowerCarLapCountAndSeparations.TryGetValue(opponentData.CompletedLaps, out separationsForOtherCarsOnSameLap))
                        {
                            // if any of them are close to each other, set the boolean flag
                            foreach (float otherSeparation in separationsForOtherCarsOnSameLap)
                            {
                                if (Math.Abs(otherSeparation - separation) < maxSeparateToBeConsideredFighting)
                                {
                                    slowerCarsRacingForPosition = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            separationsForOtherCarsOnSameLap = new List<float>();
                            separationsForOtherCarsOnSameLap.Add(separation);
                            slowerCarLapCountAndSeparations.Add(opponentData.CompletedLaps, separationsForOtherCarsOnSameLap);
                        }
                    }
                }
            }
            return new OtherCarClassWarningData(numFasterCars, 
                numSlowerCars,
                fasterCarsIncludeClassLeader,
                slowerCarsIncludeClassLeader,
                fasterCarsRacingForPosition,
                slowerCarsRacingForPosition, 
                fasterCarIsRacingPlayerForPosition, 
                slowerCarIsRacingPlayerForPosition,
                fasterCarDriverNames, 
                slowerCarDriverNames, 
                carClassOfClosestFasterCar, 
                carClassOfClosestSlowerCar);
        }
    }
}
