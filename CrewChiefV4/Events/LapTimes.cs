using CrewChiefV4.GameState;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrewChiefV4.Audio;
using CrewChiefV4.NumberProcessing;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("UnitTest.Misc")]

namespace CrewChiefV4.Events
{
    class LapTimes : AbstractEvent
    {
        #region Settings
        readonly int frequencyOfRaceSectorDeltaReports = UserSettings.GetUserSettings().getInt("frequency_of_race_sector_delta_reports");
        readonly int frequencyOfPracticeAndQualSectorDeltaReports = UserSettings.GetUserSettings().getInt("frequency_of_prac_and_qual_sector_delta_reports");
        readonly int frequencyOfPlayerRaceLapTimeReports = UserSettings.GetUserSettings().getInt("frequency_of_player_race_lap_time_reports");
        readonly int frequencyOfPlayerQualAndPracLapTimeReports = UserSettings.GetUserSettings().getInt("frequency_of_player_prac_and_qual_lap_time_reports");
        readonly Boolean raceSectorReportsAtEachSector = UserSettings.GetUserSettings().getBoolean("race_sector_reports_at_each_sector");
        readonly Boolean practiceAndQualSectorReportsAtEachSector = UserSettings.GetUserSettings().getBoolean("practice_and_qual_sector_reports_at_each_sector");
        readonly Boolean raceSectorReportsAtLapEnd = UserSettings.GetUserSettings().getBoolean("race_sector_reports_at_lap_end");
        readonly Boolean practiceAndQualSectorReportsLapEnd = UserSettings.GetUserSettings().getBoolean("practice_and_qual_sector_reports_at_lap_end");
        readonly Boolean disablePCarspracAndQualPoleDeltaReports = UserSettings.GetUserSettings().getBoolean("disable_pcars_prac_and_qual_pole_deltas");

        readonly Boolean reportAllLaptimesInHotlapMode = UserSettings.GetUserSettings().getBoolean("report_all_laps_in_hotlap_mode");
        #endregion Settings

        int maxQueueLengthForRaceSectorDeltaReports = 0;
        int maxQueueLengthForRaceLapTimeReports = 0;

        #region SpeechFolders

        // for qualifying:
        // "that was a 1:34.2, you're now 0.4 seconds off the pace"
        private const String folderLapTimeIntro = "lap_times/time_intro";
        private const String folderGapIntro = "lap_times/gap_intro";

        private const String folderGapOutroOffPace = "lap_times/gap_outro_off_pace";

        private const String folderSelfGapOutroOffPace = "lap_times/off_the_self_pace";

        private const String folderLessThanATenthOffThePace = "lap_times/less_than_a_tenth_off_the_pace";

        private const String folderSelfLessThanATenthOffThePace = "lap_times/less_than_a_tenth_off_self_pace";

        private const String folderQuickerThanSecondPlace = "lap_times/quicker_than_second_place";

        private const String folderPaceOK = "lap_times/pace_ok";
        private const String folderPaceBad = "lap_times/pace_bad";
        private const String folderNeedToFindOneMoreTenth = "lap_times/need_to_find_one_more_tenth";
        private const String folderNeedToFindASecond = "lap_times/need_to_find_a_second";
        private const String folderNeedToFindMoreThanASecond = "lap_times/need_to_find_more_than_a_second";
        private const String folderNeedToFindAFewMoreTenths = "lap_times/need_to_find_a_few_more_tenths";

        // for race:
        private const String folderBestLapInRace = "lap_times/best_lap_in_race";
        private const String folderBestLapInRaceForClass = "lap_times/best_lap_in_race_for_class";

        private const String folderGoodLap = "lap_times/good_lap";

        private const String folderConsistentTimes = "lap_times/consistent";

        private const String folderImprovingTimes = "lap_times/improving";

        private const String folderWorseningTimes = "lap_times/worsening";

        private const String folderPersonalBest = "lap_times/personal_best";
        private const String folderSettingCurrentRacePace = "lap_times/setting_current_race_pace";
        private const String folderMatchingCurrentRacePace = "lap_times/matching_race_pace";

        public const String folderSector1Fastest = "lap_times/sector1_fastest";
        public const String folderSector2Fastest = "lap_times/sector2_fastest";
        public const String folderSector3Fastest = "lap_times/sector3_fastest";
        public const String folderSector1and2Fastest = "lap_times/sector1_and_2_fastest";
        public const String folderSector2and3Fastest = "lap_times/sector2_and_3_fastest";
        public const String folderSector1and3Fastest = "lap_times/sector1_and_3_fastest";
        public const String folderAllSectorsFastest = "lap_times/sector_all_fastest";

        private const String folderSector1Fast = "lap_times/sector1_fast";
        private const String folderSector2Fast = "lap_times/sector2_fast";
        private const String folderSector3Fast = "lap_times/sector3_fast";
        private const String folderSector1and2Fast = "lap_times/sector1_and_2_fast";
        private const String folderSector2and3Fast = "lap_times/sector2_and_3_fast";
        private const String folderSector1and3Fast = "lap_times/sector1_and_3_fast";
        private const String folderAllSectorsFast = "lap_times/sector_all_fast";

        private const String folderSector1ATenthOffThePace = "lap_times/sector1_a_tenth_off_pace";
        private const String folderSector2ATenthOffThePace = "lap_times/sector2_a_tenth_off_pace";
        private const String folderSector3ATenthOffThePace = "lap_times/sector3_a_tenth_off_pace";
        private const String folderSector1and2ATenthOffThePace = "lap_times/sector1_and_2_a_tenth_off_pace";
        private const String folderSector2and3ATenthOffThePace = "lap_times/sector2_and_3_a_tenth_off_pace";
        private const String folderSector1and3ATenthOffThePace = "lap_times/sector1_and_3_a_tenth_off_pace";
        private const String folderAllSectorsATenthOffThePace = "lap_times/sector_all_a_tenth_off_pace";

        private const String folderSector1TwoTenthsOffThePace = "lap_times/sector1_two_tenths_off_pace";
        private const String folderSector2TwoTenthsOffThePace = "lap_times/sector2_two_tenths_off_pace";
        private const String folderSector3TwoTenthsOffThePace = "lap_times/sector3_two_tenths_off_pace";
        private const String folderSector1and2TwoTenthsOffThePace = "lap_times/sector1_and_2_two_tenths_off_pace";
        private const String folderSector2and3TwoTenthsOffThePace = "lap_times/sector2_and_3_two_tenths_off_pace";
        private const String folderSector1and3TwoTenthsOffThePace = "lap_times/sector1_and_3_two_tenths_off_pace";
        private const String folderAllSectorsTwoTenthsOffThePace = "lap_times/sector_all_two_tenths_off_pace";

        private const String folderSector1ASecondOffThePace = "lap_times/sector1_a_second_off_pace";
        private const String folderSector2ASecondOffThePace = "lap_times/sector2_a_second_off_pace";
        private const String folderSector3ASecondOffThePace = "lap_times/sector3_a_second_off_pace";
        private const String folderSector1and2ASecondOffThePace = "lap_times/sector1_and_2_a_second_off_pace";
        private const String folderSector2and3ASecondOffThePace = "lap_times/sector2_and_3_a_second_off_pace";
        private const String folderSector1and3ASecondOffThePace = "lap_times/sector1_and_3_a_second_off_pace";
        private const String folderAllSectorsASecondOffThePace = "lap_times/sector_all_a_second_off_pace";

        private const String folderSector1Is = "lap_times/sector1_is";
        private const String folderSector2Is = "lap_times/sector2_is";
        private const String folderSector3Is = "lap_times/sector3_is";
        private const String folderSectors1And2Are = "lap_times/sector1_and_2_are";
        private const String folderSectors2And3Are = "lap_times/sector2_and_3_are";
        private const String folderSectors1And3Are = "lap_times/sector1_and_3_are";
        private const String folderAllThreeSectorsAre = "lap_times/sector_all_are";
        private const String folderOffThePace = "lap_times/off_the_pace";

        private const String folderSelfSector1ATenthOffThePace = "lap_times/sector1_a_tenth_off_self_pace";
        private const String folderSelfSector2ATenthOffThePace = "lap_times/sector2_a_tenth_off_self_pace";
        private const String folderSelfSector3ATenthOffThePace = "lap_times/sector3_a_tenth_off_self_pace";
        private const String folderSelfSector1and2ATenthOffThePace = "lap_times/sector1_and_2_a_tenth_off_self_pace";
        private const String folderSelfSector2and3ATenthOffThePace = "lap_times/sector2_and_3_a_tenth_off_self_pace";
        private const String folderSelfSector1and3ATenthOffThePace = "lap_times/sector1_and_3_a_tenth_off_self_pace";
        private const String folderSelfAllSectorsATenthOffThePace = "lap_times/sector_all_a_tenth_off_self_pace";

        private const String folderSelfSector1TwoTenthsOffThePace = "lap_times/sector1_two_tenths_off_self_pace";
        private const String folderSelfSector2TwoTenthsOffThePace = "lap_times/sector2_two_tenths_off_self_pace";
        private const String folderSelfSector3TwoTenthsOffThePace = "lap_times/sector3_two_tenths_off_self_pace";
        private const String folderSelfSector1and2TwoTenthsOffThePace = "lap_times/sector1_and_2_two_tenths_off_self_pace";
        private const String folderSelfSector2and3TwoTenthsOffThePace = "lap_times/sector2_and_3_two_tenths_off_self_pace";
        private const String folderSelfSector1and3TwoTenthsOffThePace = "lap_times/sector1_and_3_two_tenths_off_self_pace";
        private const String folderSelfAllSectorsTwoTenthsOffThePace = "lap_times/sector_all_two_tenths_off_self_pace";

        private const String folderSelfSector1ASecondOffThePace = "lap_times/sector1_a_second_off_self_pace";
        private const String folderSelfSector2ASecondOffThePace = "lap_times/sector2_a_second_off_self_pace";
        private const String folderSelfSector3ASecondOffThePace = "lap_times/sector3_a_second_off_self_pace";
        private const String folderSelfSector1and2ASecondOffThePace = "lap_times/sector1_and_2_a_second_off_self_pace";
        private const String folderSelfSector2and3ASecondOffThePace = "lap_times/sector2_and_3_a_second_off_self_pace";
        private const String folderSelfSector1and3ASecondOffThePace = "lap_times/sector1_and_3_a_second_off_self_pace";
        private const String folderSelfAllSectorsASecondOffThePace = "lap_times/sector_all_a_second_off_self_pace";

        private const String folderSelfOffThePace = "lap_times/off_the_self_pace";

        #endregion

        // if the lap is within 0.3% of the best lap time play a message
        private const Single goodLapPercent = 0.3f;

        private const Single matchingRacePacePercent = 0.1f;

        // if the lap is within 0.5% of the previous lap it's considered consistent
        private const Single consistencyLimit = 0.5f;

        private List<float> lapTimesWindow = new List<float>();
        private List<Conditions.ConditionsSample> conditionsWindow = new List<Conditions.ConditionsSample>();

        private const int lapTimesWindowSize = 5;

        private ConsistencyResult lastConsistencyMessage;

        // lap number when the last consistency update was made
        private int lastConsistencyUpdate;

        private Boolean lapIsValid;

        /// <summary>
        /// Compared to opponents
        /// </summary>
        private LastLapRating lastLapRating;

        /// <summary>
        /// Compared to my best
        /// </summary>
        private LastLapRating lastLapSelfRating;

        private TimeSpan deltaPlayerLastToSessionBestInClass;

        private Boolean deltaPlayerLastToSessionBestInClassSet = false;

        private SessionType sessionType;

        private GameStateData currentGameState;

        /// <summary>
        /// # of pace laps in the window according to track length
        /// </summary>
        /// <param name="trackDefinition" may be null></param>
        private int paceCheckLapsWindow(TrackDefinition trackDefinition)
        {
             Dictionary<TrackData.TrackLengthClass, int> paceCheckLapsWindow = new Dictionary<TrackData.TrackLengthClass, int> {
                { TrackData.TrackLengthClass.VERY_LONG, 2 },
                { TrackData.TrackLengthClass.LONG, 3 },
                { TrackData.TrackLengthClass.MEDIUM, 4 },
                { TrackData.TrackLengthClass.SHORT, 5 },
                { TrackData.TrackLengthClass.VERY_SHORT, 6}
            };
            return trackDefinition == null ? paceCheckLapsWindow[TrackData.TrackLengthClass.LONG] : paceCheckLapsWindow[trackDefinition.trackLengthClass];
        }
        /// <summary>
        /// Depends on track length
        /// </summary>
        private int paceCheckLapsWindowForRaceToUse;

        private Boolean isHotLappingOrLonePractice;

        private TimeSpan lastGapToSecondWhenLeadingPracOrQual;

        private int ClassPositionAtStartOfCurrentLap = -1;

        /// <summary>
        /// Delta that defines a lap as an outlier
        /// </summary>
        public static readonly Dictionary<TrackData.TrackLengthClass, float> outlierPaceLimits = new Dictionary<TrackData.TrackLengthClass, float> {
            { TrackData.TrackLengthClass.VERY_LONG, 15 },
            { TrackData.TrackLengthClass.LONG, 8 },
            { TrackData.TrackLengthClass.MEDIUM, 3 },
            { TrackData.TrackLengthClass.SHORT, 2 },
            { TrackData.TrackLengthClass.VERY_SHORT, 2}
        };

        /// <summary>
        /// some calls (pearls and lap times) are suppressed until we've completed a number of laps depending on the track length class
        /// </summary>
        public static Dictionary<TrackData.TrackLengthClass, int> lapsBeforeAnnouncingGaps = new Dictionary<TrackData.TrackLengthClass, int> {
            { TrackData.TrackLengthClass.VERY_LONG, 0 },
            { TrackData.TrackLengthClass.LONG, 1 },
            { TrackData.TrackLengthClass.MEDIUM, 2 },
            { TrackData.TrackLengthClass.SHORT, 3 },
            { TrackData.TrackLengthClass.VERY_SHORT, 4}
        };

        // The time gap categories
        internal enum Delta
        {
            FAST,
            A_TENTH,
            TWO_TENTHS,
            A_SECOND,
            AUTO_GAPS,
            NONE
        }

        public LapTimes(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
            if (frequencyOfRaceSectorDeltaReports > 7)
            {
                maxQueueLengthForRaceSectorDeltaReports = 7;
            }
            else if (frequencyOfRaceSectorDeltaReports > 5)
            {
                maxQueueLengthForRaceSectorDeltaReports = 4;
            }
            else
            {
                maxQueueLengthForRaceSectorDeltaReports = 3;
            }
            if (frequencyOfPlayerRaceLapTimeReports > 7)
            {
                maxQueueLengthForRaceLapTimeReports = 7;
            }
            else if (frequencyOfPlayerRaceLapTimeReports > 5)
            {
                maxQueueLengthForRaceLapTimeReports = 5;
            }
            else
            {
                maxQueueLengthForRaceLapTimeReports = 4;
            }
        }

        public override void clearState()
        {
            lapTimesWindow = new List<float>(lapTimesWindowSize);
            conditionsWindow = new List<Conditions.ConditionsSample>();
            lastConsistencyUpdate = 0;
            lastConsistencyMessage = ConsistencyResult.NOT_APPLICABLE;
            lapIsValid = true;
            lastLapRating = LastLapRating.NO_DATA;
            lastLapSelfRating = LastLapRating.NO_DATA;
            deltaPlayerLastToSessionBestInClass = TimeSpan.MaxValue;
            deltaPlayerLastToSessionBestInClassSet = false;
            currentGameState = null;
            isHotLappingOrLonePractice = false;
            lastGapToSecondWhenLeadingPracOrQual = TimeSpan.Zero;
            ClassPositionAtStartOfCurrentLap = -1;
        }

        public override bool isMessageStillValid(string eventSubType, GameStateData currentGameState, Dictionary<string, object> validationData)
        {
            if (PitStops.isPittingThisLap)
            {
                return false;
            }
            // not sure if we need this - validate that we're not in sector 2 by the time the lap consistency message is played
            if ((eventSubType == folderImprovingTimes || eventSubType == folderConsistentTimes || eventSubType == folderWorseningTimes) &&
                    currentGameState.SessionData.SectorNumber != 1)
            {
                return false;
            }
            else
            {
                return base.isMessageStillValid(eventSubType, currentGameState, validationData);
            }
        }

        protected override void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            if (GameStateData.onManualFormationLap)
            {
                return;
            }
            sessionType = currentGameState.SessionData.SessionType;
            this.currentGameState = currentGameState;  // Nice... leave it to avoid a diff that would obscure the refactoring

            if (currentGameState.SessionData.IsNewLap)
            {
                ClassPositionAtStartOfCurrentLap = currentGameState.SessionData.ClassPosition;
                _consoleLogLapTime(currentGameState);
                paceCheckLapsWindowForRaceToUse = paceCheckLapsWindow(currentGameState.SessionData.TrackDefinition);
                deltaPlayerLastToSessionBestInClassSet = _getDeltaToBestInClass(currentGameState);
            }

            // check the current lap is still valid
            if (lapIsValid && currentGameState.SessionData.CompletedLaps > 0 &&
                !currentGameState.SessionData.IsNewLap && !currentGameState.SessionData.CurrentLapIsValid)
            {
                lapIsValid = false;
            }
            if (previousGameState != null && previousGameState.SessionData.CompletedLaps <= currentGameState.FlagData.lapCountWhenLastWentGreen)
            {
                return;
            }
            (float[] lapAndSectorsComparisonData,
                float[] lapAndSectorsSelfComparisonData) = _getLapAndSectorsComparisonData(currentGameState);

            bool __lapWorthRating = (!currentGameState.PitData.OnInLap && previousGameState != null && !previousGameState.PitData.OnOutLap
                && !currentGameState.PitData.InPitlane   // as this is a new lap, check whether the *previous* state was an outlap
                && !currentGameState.FlagData.previousLapWasFCY);    // don't announce lap times if we've just gone green after FCY
            
            if (__lapWorthRating)
            {
                Boolean sectorsReportedForLap = false;
                
                bool __flyingLap = (currentGameState.SessionData.IsNewLap &&
                    (((currentGameState.SessionData.SessionType == SessionType.HotLap
                       || currentGameState.SessionData.SessionType == SessionType.LonePractice
                       || currentGameState.SessionData.SessionType == SessionType.Qualify
                       || currentGameState.SessionData.SessionType == SessionType.PrivateQualify)
                            && currentGameState.SessionData.CompletedLaps > 0) ||
                      currentGameState.SessionData.CompletedLaps > 1));

                if (__flyingLap)
                {
                    if (lapTimesWindow == null)
                    {
                        lapTimesWindow = new List<float>(lapTimesWindowSize);
                    }
                    lastLapRating = getLastLapRating(currentGameState, lapAndSectorsComparisonData, false /*selfPace*/);
                    lastLapSelfRating = getLastLapRating(currentGameState, lapAndSectorsSelfComparisonData, true /*selfPace*/);

                    bool __lapWorthReporting = (currentGameState.SessionData.PreviousLapWasValid && lastLapRating != LastLapRating.OUTLIER);
                    // in endurance races, we want to disable most messages because the data is
                    // usually not available for the entire race, leading to a slew of false positives
                    // on every driver swap.
                    if (currentGameState.SessionData.SessionTotalRunTime >= 60f * 60f)
                    {
                        switch (lastLapRating)
                        {
                            case LastLapRating.SETTING_CURRENT_PACE:
                            case LastLapRating.CLOSE_TO_CURRENT_PACE:
                            case LastLapRating.CLOSE_TO_OVERALL_LEADER:
                            case LastLapRating.CLOSE_TO_CLASS_LEADER:
                            case LastLapRating.MEH:
                            case LastLapRating.BAD:
                                break;
                            default:
                                __lapWorthReporting = false;
                                break;
                        }
                    }

                    if (__lapWorthReporting)
                    {
                        lapTimesWindow.Insert(0, currentGameState.SessionData.LapTimePrevious);
                        Conditions.ConditionsSample conditionsSample = currentGameState.Conditions.getMostRecentConditions();
                        if (conditionsSample != null)
                        {
                            conditionsWindow.Insert(0, conditionsSample);
                        }
                        if (lapIsValid && !currentGameState.PitData.InPitlane)
                        {
                            Boolean playedLapTime = false;
                            if (isHotLappingOrLonePractice && reportAllLaptimesInHotlapMode)
                            {
                                // If requested, always play the laptime in hotlap/lone practice mode
                                audioPlayer.playMessage(new QueuedMessage("laptime", 0,
                                        messageFragments: MessageContents(folderLapTimeIntro, TimeSpanWrapper.FromSeconds(currentGameState.SessionData.LapTimePrevious, Precision.AUTO_LAPTIMES)),
                                        abstractEvent: this, priority: 10));
                                playedLapTime = true;
                            }
                            else if (((currentGameState.SessionData.SessionType == SessionType.Qualify ||
                                       currentGameState.SessionData.SessionType == SessionType.PrivateQualify ||
                                       currentGameState.SessionData.SessionType == SessionType.Practice) && frequencyOfPlayerQualAndPracLapTimeReports > Utilities.random.NextDouble() * 10)
                                || (currentGameState.SessionData.SessionType == SessionType.Race && frequencyOfPlayerRaceLapTimeReports > Utilities.random.NextDouble() * 10))
                            {
                                // usually play it in practice / qual mode, occasionally play it in race mode
                                QueuedMessage gapFillerLapTime = new QueuedMessage("laptime", 0,
                                    messageFragments: MessageContents(folderLapTimeIntro, TimeSpanWrapper.FromSeconds(currentGameState.SessionData.LapTimePrevious, Precision.AUTO_LAPTIMES)),
                                    abstractEvent: this, priority: 0);
                                if (currentGameState.SessionData.SessionType == SessionType.Race)
                                {
                                    gapFillerLapTime.maxPermittedQueueLengthForMessage = maxQueueLengthForRaceLapTimeReports;
                                }
                                audioPlayer.playMessage(gapFillerLapTime);
                                playedLapTime = true;
                            }

                            bool __deltaInLiveLap = (deltaPlayerLastToSessionBestInClassSet &&
                                (currentGameState.SessionData.SessionType == SessionType.Qualify
                                 || currentGameState.SessionData.SessionType == SessionType.PrivateQualify
                                 || currentGameState.SessionData.SessionType == SessionType.Practice
                                 || currentGameState.SessionData.SessionType == SessionType.HotLap
                                 || currentGameState.SessionData.SessionType == SessionType.LonePractice));

                            if (__deltaInLiveLap)
                            {
                                if (currentGameState.SessionData.SessionType == SessionType.HotLap || 
                                    currentGameState.SessionData.SessionType == SessionType.LonePractice ||
                                    currentGameState.OpponentData.Count == 0)
                                {
                                    sectorsReportedForLap = _playLoneLap(currentGameState, lapAndSectorsComparisonData);
                                }
                                // need to be careful with the rating here as it's based on the known opponent laps, and we may have joined the session part way through
                                else if (currentGameState.SessionData.ClassPosition == 1)
                                {
                                    _playGapBehind(previousGameState);
                                }
                                else // mop up... something
                                {
                                    _playIfPersonalBest(currentGameState);

                                    // don't read this message if the rounded time gap is 0.0 seconds or it's more than 59 seconds
                                    // only play qual / prac deltas for Raceroom as the PCars data is inaccurate for sessions joined part way through
                                    if (frequencyOfPlayerQualAndPracLapTimeReports > Utilities.random.NextDouble() * 10 &&
                                        (!disablePCarspracAndQualPoleDeltaReports || Game.RACE_ROOM || Game.IRACING || Game.ASSETTO1) &&
                                        (deltaPlayerLastToSessionBestInClass.Seconds > 0 || deltaPlayerLastToSessionBestInClass.Milliseconds > 50) &&
                                        deltaPlayerLastToSessionBestInClass.Seconds < 60)
                                    {
                                        // delay this a bit...
                                        audioPlayer.playMessage(new QueuedMessage("lapTimeNotRaceGap", 0,
                                            messageFragments: MessageContents(folderGapIntro, new TimeSpanWrapper(deltaPlayerLastToSessionBestInClass, Precision.AUTO_GAPS), folderGapOutroOffPace), abstractEvent: this, priority: 5));
                                    }
                                    if (!GlobalBehaviourSettings.useOvalLogic &&
                                        practiceAndQualSectorReportsLapEnd && frequencyOfPracticeAndQualSectorDeltaReports > Utilities.random.NextDouble() * 10)
                                    {
                                        List<MessageFragment> sectorMessageFragments = getSectorDeltaMessages(SectorReportOption.ALL, currentGameState.SessionData.LastSector1Time, lapAndSectorsComparisonData[1],
                                            currentGameState.SessionData.LastSector2Time, lapAndSectorsComparisonData[2], currentGameState.SessionData.LastSector3Time, lapAndSectorsComparisonData[3], true, false /*selfPace*/);
                                        if (sectorMessageFragments.Count > 0)
                                        {
                                            audioPlayer.playMessage(new QueuedMessage("sectorDeltas", 0, messageFragments: sectorMessageFragments, abstractEvent: this, priority: 5));
                                            sectorsReportedForLap = true;
                                        }
                                    }
                                }
                            }
                            // !__deltaInLiveLap
                            else if (currentGameState.SessionData.SessionType == SessionType.Race && !currentGameState.PitData.InPitlane && !currentGameState.FlagData.isFullCourseYellow)
                            {
                                bool playedLapMessage = _playLapRating(currentGameState);

                                sectorsReportedForLap = _playSectorsMessage_notOvals(currentGameState, playedLapTime, playedLapMessage, lapAndSectorsComparisonData, sectorsReportedForLap);

                                // play the consistency message if we've not played the good lap message, or sometimes
                                // play them both
                                _playConsistencyMessage(currentGameState, playedLapMessage);
                            }
                        }
                    }
                }
                // report sector delta at the completion of a sector?
                if (!sectorsReportedForLap && currentGameState.SessionData.IsNewSector && !currentGameState.FlagData.isFullCourseYellow &&
                    ((currentGameState.SessionData.SessionType == SessionType.Race && raceSectorReportsAtEachSector) ||
                     (currentGameState.SessionData.SessionType != SessionType.Race && practiceAndQualSectorReportsAtEachSector)))
                {
                    _playSectorDelta_notOvals(currentGameState, lapAndSectorsComparisonData);
                }
            }
            if (currentGameState.SessionData.IsNewLap && !currentGameState.PitData.OnOutLap)
            {
                // lapIsValid has mixed use.  It is used to track if current lap is valid, but is also used
                // to decide if previous lap was valid when the new lap begins.  So reset it here.
                lapIsValid = true;
            }
        }

        // Chunks extracted from legacy version of method above
        private void _consoleLogLapTime(GameStateData currentGameState)
        {
            if (currentGameState.SessionData.CompletedLaps > 0)
            {
                if (currentGameState.SessionData.LapTimePrevious > 0.0f)
                {
                    Console.WriteLine("Laptime: " + TimeSpan.FromSeconds(currentGameState.SessionData.LapTimePrevious).ToString(@"mm\:ss\.fff") + ",  Valid = " + currentGameState.SessionData.PreviousLapWasValid);
                }
                else
                {
                    Console.WriteLine("Laptime: " + currentGameState.SessionData.LapTimePrevious.ToString("0.000") + ",  Valid = " + currentGameState.SessionData.PreviousLapWasValid);
                }
            }
        }

        private bool _getDeltaToBestInClass(GameStateData currentGameState)
        {
            bool _deltaPlayerLastToSessionBestInClassSet = false;
            float _lastLapTime = currentGameState.SessionData.LapTimePrevious;
            if (_lastLapTime > 0)
            {
                if (currentGameState.OpponentData.Count > 0 && 
                    currentGameState.SessionData.SessionType != SessionType.LonePractice && 
                    currentGameState.SessionData.SessionType != SessionType.HotLap)
                {
                    if (currentGameState.SessionData.SessionType == SessionType.Qualify || 
                        currentGameState.SessionData.SessionType == SessionType.PrivateQualify)
                    {
                        // always want the overall delta in qually
                        float opponentOverallBest = currentGameState.TimingData.getPlayerClassOpponentBestLapTime(TimingData.ConditionsEnum.ANY);
                        if (opponentOverallBest > 0)
                        {
                            deltaPlayerLastToSessionBestInClass = TimeSpan.FromSeconds(_lastLapTime - opponentOverallBest);
                            _deltaPlayerLastToSessionBestInClassSet = true;
                        }
                    }
                    else
                    {
                        // get the delta for the current conditions
                        float opponentBestInCurrentConditions = currentGameState.TimingData.getPlayerClassOpponentBestLapTime(TimingData.ConditionsEnum.CURRENT);
                        if (opponentBestInCurrentConditions > 0)
                        {
                            deltaPlayerLastToSessionBestInClass = TimeSpan.FromSeconds(_lastLapTime - opponentBestInCurrentConditions);
                            _deltaPlayerLastToSessionBestInClassSet = true;
                        }
                    }
                }
                else if (currentGameState.SessionData.PlayerLapTimeSessionBest > 0 && currentGameState.SessionData.CompletedLaps > 1)
                {
                    deltaPlayerLastToSessionBestInClass = TimeSpan.FromSeconds(_lastLapTime - currentGameState.SessionData.PlayerLapTimeSessionBest);
                    _deltaPlayerLastToSessionBestInClassSet = true;
                }
            }
            return _deltaPlayerLastToSessionBestInClassSet;
        }

        private (float[], float[]) _getLapAndSectorsComparisonData(GameStateData currentGameState)
        {
            float[] lapAndSectorsComparisonData = new float[] { -1, -1, -1, -1 };
            float[] lapAndSectorsSelfComparisonData = new float[] { -1, -1, -1, -1 };
            if (currentGameState.SessionData.IsNewLap)
            {
                // If this is a new lap, then the just completed lap became last lap.  We do not want to use it as a
                // Qualification/Practice and self pace comparison, we need the previous player best time.
                lapAndSectorsSelfComparisonData = currentGameState.SessionData.getPlayerTimeAndSectorsForBestLap(true /*ignoreLast*/);
            }
            else
            {
                lapAndSectorsSelfComparisonData = currentGameState.SessionData.getPlayerTimeAndSectorsForBestLap(false /*ignoreLast*/);
            }

            if (currentGameState.SessionData.IsNewSector)
            {
                isHotLappingOrLonePractice = currentGameState.SessionData.SessionType == SessionType.HotLap || currentGameState.SessionData.SessionType == SessionType.LonePractice ||
                                             (currentGameState.OpponentData.Count == 0 || (currentGameState.OpponentData.Count == 1 && currentGameState.OpponentData.First().Value.DriverRawName == currentGameState.SessionData.DriverRawName));
                if (isHotLappingOrLonePractice)
                {
                    // note that lone practice in changing conditions doesn't take conditions into account. This is a bit of an edge case
                    lapAndSectorsComparisonData[0] = lapAndSectorsSelfComparisonData[0];
                    lapAndSectorsComparisonData[1] = lapAndSectorsSelfComparisonData[1];
                    lapAndSectorsComparisonData[2] = lapAndSectorsSelfComparisonData[2];
                    lapAndSectorsComparisonData[3] = lapAndSectorsSelfComparisonData[3];
                }
                else
                {
                    switch (currentGameState.SessionData.SessionType)
                    {
                        // in qual sessions we want absolute timings. We can also use absolute timings if the conditions are static.
                        // If the conditions are changing we want timings relative to the prevailing conditions for non-qual sessions.
                        // For race sessions we want the recent pace
                        case SessionType.Race:
                            {
                                if (!currentGameState.TimingData.conditionsHaveChanged)
                                {
                                    // no changing conditions, get the 'pace' from the most recent laps
                                    lapAndSectorsComparisonData = currentGameState.getTimeAndSectorsForBestOpponentLapInWindow(paceCheckLapsWindowForRaceToUse, currentGameState.carClass);
                                }
                                else
                                {
                                    // use data relevant to current conditions
                                    lapAndSectorsComparisonData = new float[] {
                                    currentGameState.TimingData.getPlayerClassOpponentBestLapTime(),
                                    currentGameState.TimingData.getPlayerClassOpponentBestLapSector1Time(),
                                    currentGameState.TimingData.getPlayerClassOpponentBestLapSector2Time(),
                                    currentGameState.TimingData.getPlayerClassOpponentBestLapSector3Time()
                                };
                                }

                                break;
                            }
                        case SessionType.Practice:
                            {
                                if (!currentGameState.TimingData.conditionsHaveChanged)
                                {
                                    // no changing conditions, get the 'pace' from the all the recorded laps
                                    lapAndSectorsComparisonData = currentGameState.getTimeAndSectorsForBestOpponentLapInWindow(-1, currentGameState.carClass);
                                }
                                else
                                {
                                    // use data relevant to current conditions
                                    lapAndSectorsComparisonData = new float[] {
                                    currentGameState.TimingData.getPlayerClassOpponentBestLapTime(),
                                    currentGameState.TimingData.getPlayerClassOpponentBestLapSector1Time(),
                                    currentGameState.TimingData.getPlayerClassOpponentBestLapSector2Time(),
                                    currentGameState.TimingData.getPlayerClassOpponentBestLapSector3Time()
                                };
                                }

                                break;
                            }
                        case SessionType.Qualify:
                        case SessionType.PrivateQualify:
                            // not interested in the conditions, just want best laps from all the data we have
                            lapAndSectorsComparisonData = currentGameState.getTimeAndSectorsForBestOpponentLapInWindow(-1, currentGameState.carClass);
                            break;
                    }
                }
            }
            return (lapAndSectorsComparisonData, lapAndSectorsSelfComparisonData);
        }

        private bool _playLoneLap(GameStateData currentGameState, float[] lapAndSectorsComparisonData)
        {
            bool sectorsReportedForLap = false;
            if (currentGameState.SessionData.CompletedLaps > 1 &&
                (isHotLappingOrLonePractice ? lastLapRating == LastLapRating.BEST_OVERALL : lastLapRating == LastLapRating.BEST_IN_CLASS
                                                                                            || deltaPlayerLastToSessionBestInClass <= TimeSpan.Zero))
            {
                audioPlayer.playMessage(new QueuedMessage(folderPersonalBest, 0, abstractEvent: this, priority: 3));
            }
            else if (deltaPlayerLastToSessionBestInClass > TimeSpan.Zero  // Guard against first lap time set.
                     && deltaPlayerLastToSessionBestInClass < TimeSpan.FromMilliseconds(50))
            {
                audioPlayer.playMessage(new QueuedMessage((isHotLappingOrLonePractice ? folderSelfLessThanATenthOffThePace : folderLessThanATenthOffThePace), 0, abstractEvent: this, priority: 3));
            }
            else if (deltaPlayerLastToSessionBestInClass > TimeSpan.Zero  // Guard against first lap time set.
                     && deltaPlayerLastToSessionBestInClass < TimeSpan.MaxValue)
            {
                audioPlayer.playMessage(new QueuedMessage("lapTimeNotRaceGap", 0,
                    messageFragments: MessageContents(folderGapIntro, new TimeSpanWrapper(deltaPlayerLastToSessionBestInClass, Precision.AUTO_GAPS),
                        isHotLappingOrLonePractice ? folderSelfGapOutroOffPace : folderGapOutroOffPace), abstractEvent: this, priority: 3));
            }
            if (!GlobalBehaviourSettings.useOvalLogic &&
                practiceAndQualSectorReportsLapEnd && frequencyOfPracticeAndQualSectorDeltaReports > Utilities.random.NextDouble() * 10)
            {
                List<MessageFragment> sectorMessageFragments = getSectorDeltaMessages(SectorReportOption.ALL, currentGameState.SessionData.LastSector1Time, lapAndSectorsComparisonData[1],
                    currentGameState.SessionData.LastSector2Time, lapAndSectorsComparisonData[2], currentGameState.SessionData.LastSector3Time, lapAndSectorsComparisonData[3], true, isHotLappingOrLonePractice /*selfPace*/);
                if (sectorMessageFragments.Count > 0)
                {
                    audioPlayer.playMessage(new QueuedMessage("sectorsHotLap", 0, messageFragments: sectorMessageFragments, abstractEvent: this, priority: 3));
                    sectorsReportedForLap = true;
                }
            }
            return sectorsReportedForLap;
        }

        private void _playGapBehind(GameStateData previousGameState)
        {
            Boolean newGapToSecond = false;
            if (previousGameState != null && previousGameState.SessionData.ClassPosition > 1)
            {
                newGapToSecond = true;
            }
            if (deltaPlayerLastToSessionBestInClass < lastGapToSecondWhenLeadingPracOrQual)
            {
                newGapToSecond = true;
                lastGapToSecondWhenLeadingPracOrQual = deltaPlayerLastToSessionBestInClass;
            }
            if (newGapToSecond)
            {
                TimeSpan gapBehind = deltaPlayerLastToSessionBestInClass.Negate();
                // only play qual / prac deltas for Raceroom as the PCars data is inaccurate for sessions joined part way through
                if (frequencyOfPlayerQualAndPracLapTimeReports > Utilities.random.NextDouble() * 10 &&
                    (!disablePCarspracAndQualPoleDeltaReports ||
                     Game.RACE_ROOM ||
                     Game.IRACING ||
                     Game.ASSETTO1
                    ) &&
                    (gapBehind.Seconds > 0 || gapBehind.Milliseconds > 50))
                {
                    // delay this a bit...
                    int delay = Utilities.random.Next(0, 8);
                    audioPlayer.playMessage(new QueuedMessage("lapTimeNotRaceGap", delay + 6,
                        messageFragments: MessageContents(folderGapIntro, new TimeSpanWrapper(gapBehind, Precision.AUTO_GAPS), folderQuickerThanSecondPlace), abstractEvent: this, priority: 5));
                }
            }
        }

        private void _playIfPersonalBest(GameStateData currentGameState)
        {
            if (currentGameState.SessionData.CompletedLaps > 1 &&
                (lastLapRating == LastLapRating.PERSONAL_BEST_STILL_SLOW ||
                 lastLapRating == LastLapRating.PERSONAL_BEST_CLOSE_TO_CLASS_LEADER ||
                 lastLapRating == LastLapRating.PERSONAL_BEST_CLOSE_TO_OVERALL_LEADER))
            {
                audioPlayer.playMessage(new QueuedMessage(folderPersonalBest, 0, abstractEvent: this, priority: 7));
            }
        }

        /// <summary>
        /// How good was that lap?
        /// </summary>
        /// <returns></returns>
        private bool _playLapRating(GameStateData currentGameState)
        {
            Boolean playedLapMessage = false;
            if (frequencyOfPlayerRaceLapTimeReports > Utilities.random.NextDouble() * 10)
            {
                bool allowPearls = currentGameState.SessionData.CompletedLaps >= lapsBeforeAnnouncingGaps[currentGameState.SessionData.TrackDefinition.trackLengthClass];
                float pearlLikelihood = allowPearls ? 0 : 0.8f;
                switch (lastLapRating)
                {
                    case LastLapRating.BEST_OVERALL:
                        playedLapMessage = true;
                        audioPlayer.playMessage(new QueuedMessage(folderBestLapInRace, 0, abstractEvent: this, priority: 3), PearlsOfWisdom.PearlType.GOOD, pearlLikelihood);
                        break;
                    case LastLapRating.BEST_IN_CLASS:
                        playedLapMessage = true;
                        audioPlayer.playMessage(new QueuedMessage(folderBestLapInRaceForClass, 0, abstractEvent: this, priority: 3), PearlsOfWisdom.PearlType.GOOD, pearlLikelihood);
                        break;
                    case LastLapRating.SETTING_CURRENT_PACE:
                        playedLapMessage = true;
                        audioPlayer.playMessage(new QueuedMessage(folderSettingCurrentRacePace, 0, abstractEvent: this, priority: 3), PearlsOfWisdom.PearlType.GOOD, pearlLikelihood);
                        break;
                    case LastLapRating.CLOSE_TO_CURRENT_PACE:
                        // don't keep playing this one
                        if (Utilities.random.NextDouble() < 0.5)
                        {
                            playedLapMessage = true;
                            audioPlayer.playMessage(new QueuedMessage(folderMatchingCurrentRacePace, 0, abstractEvent: this, priority: 0), PearlsOfWisdom.PearlType.GOOD, pearlLikelihood);
                        }
                        break;
                    case LastLapRating.PERSONAL_BEST_CLOSE_TO_OVERALL_LEADER:
                    case LastLapRating.PERSONAL_BEST_CLOSE_TO_CLASS_LEADER:
                        playedLapMessage = true;
                        audioPlayer.playMessage(new QueuedMessage(folderGoodLap, 0, abstractEvent: this, priority: 0), PearlsOfWisdom.PearlType.GOOD, pearlLikelihood);
                        break;
                    case LastLapRating.PERSONAL_BEST_STILL_SLOW:
                        playedLapMessage = true;
                        audioPlayer.playMessage(new QueuedMessage(folderPersonalBest, 0, abstractEvent: this, priority: 0), PearlsOfWisdom.PearlType.NEUTRAL, pearlLikelihood);
                        break;
                    case LastLapRating.CLOSE_TO_OVERALL_LEADER:
                    case LastLapRating.CLOSE_TO_CLASS_LEADER:
                        // this is an OK lap but not a PB. We only want to say "decent lap" occasionally here
                        if (Utilities.random.NextDouble() < 0.2)
                        {
                            playedLapMessage = true;
                            audioPlayer.playMessage(new QueuedMessage(folderGoodLap, 0, abstractEvent: this, priority: 0), PearlsOfWisdom.PearlType.NEUTRAL, pearlLikelihood);
                        }
                        break;
                    default:
                        break;
                }
            }
            return playedLapMessage;
        }

        private bool _playSectorsMessage_notOvals(GameStateData currentGameState, bool playedLapTime, bool playedLapMessage, float[] lapAndSectorsComparisonData, bool sectorsReportedForLap)
        {
            if (!GlobalBehaviourSettings.useOvalLogic &&
                currentGameState.SessionData.ClassPosition == ClassPositionAtStartOfCurrentLap &&
                raceSectorReportsAtLapEnd && frequencyOfRaceSectorDeltaReports > Utilities.random.NextDouble() * 15)
            {
                double r = Utilities.random.NextDouble();
                SectorReportOption reportOption = SectorReportOption.ALL;
                if (playedLapTime && playedLapMessage)
                {
                    // if we've already played a laptime and lap rating, use the short sector message.
                    reportOption = SectorReportOption.WORST_ONLY;
                }
                else if (r > 0.5 || ((playedLapTime || playedLapMessage) && r > 0.2))
                {
                    // if we've played one of these, usually use the abbrieviated version. If we've played neither, sometimes use the abbrieviated version
                    reportOption = SectorReportOption.WORST_ONLY;
                }

                List<MessageFragment> sectorMessageFragments = getSectorDeltaMessages(reportOption, currentGameState.SessionData.LastSector1Time, lapAndSectorsComparisonData[1],
                    currentGameState.SessionData.LastSector2Time, lapAndSectorsComparisonData[2], currentGameState.SessionData.LastSector3Time, lapAndSectorsComparisonData[3], false, false /*selfPace*/);
                if (sectorMessageFragments.Count > 0)
                {
                    QueuedMessage message = new QueuedMessage("sectorDeltas", 0, messageFragments: sectorMessageFragments, abstractEvent: this, priority: 0);
                    message.maxPermittedQueueLengthForMessage = maxQueueLengthForRaceSectorDeltaReports;
                    audioPlayer.playMessage(message);
                    sectorsReportedForLap = true;
                }
            }
            return sectorsReportedForLap;
        }

        private void _playConsistencyMessage(GameStateData currentGameState, bool playedLapMessage)
        {
            Boolean playConsistencyMessage = !GlobalBehaviourSettings.justTheFacts && (!playedLapMessage || Utilities.random.NextDouble() < 0.25);
            if (playConsistencyMessage && currentGameState.SessionData.CompletedLaps >= lastConsistencyUpdate + lapTimesWindowSize &&
                lapTimesWindow.Count >= lapTimesWindowSize)
            {
                int delay = Utilities.random.Next(0, 8);
                ConsistencyResult consistency = checkAgainstPreviousLaps(currentGameState.SessionData.TrackDefinition.isOval);
                switch (consistency)
                {
                    case ConsistencyResult.CONSISTENT:
                        lastConsistencyUpdate = currentGameState.SessionData.CompletedLaps;
                        audioPlayer.playMessage(new QueuedMessage(folderConsistentTimes, delay + 10, secondsDelay: delay, abstractEvent: this, priority: 0));
                        break;
                    case ConsistencyResult.IMPROVING:
                        lastConsistencyUpdate = currentGameState.SessionData.CompletedLaps;
                        audioPlayer.playMessage(new QueuedMessage(folderImprovingTimes, delay + 10, secondsDelay: delay, abstractEvent: this, priority: 5));
                        break;
                    case ConsistencyResult.WORSENING:
                        {
                            // don't play the worsening message if the lap rating is good
                            if (lastLapRating == LastLapRating.BEST_IN_CLASS || lastLapRating == LastLapRating.BEST_OVERALL ||
                                lastLapRating == LastLapRating.SETTING_CURRENT_PACE || lastLapRating == LastLapRating.CLOSE_TO_CURRENT_PACE)
                            {
                                Console.WriteLine("Skipping 'worsening' laptimes message - inconsistent with lap rating");
                            }
                            else if (currentGameState.SessionData.ClassPosition >= currentGameState.SessionData.ClassPositionAtStartOfCurrentLap)
                            {
                                // only complain about worsening laptimes if we've not overtaken anyone on this lap
                                lastConsistencyUpdate = currentGameState.SessionData.CompletedLaps;
                                if (GlobalBehaviourSettings.complaintsCountInThisSession < GlobalBehaviourSettings.maxComplaintsPerSession)
                                {
                                    audioPlayer.playMessage(new QueuedMessage(folderWorseningTimes, delay + 10, secondsDelay: delay, abstractEvent: this,
                                        validationData: new Dictionary<String, Object>(), priority: 3));
                                    GlobalBehaviourSettings.complaintsCountInThisSession++;
                                }
                            }

                            break;
                        }
                }
            }
        }

        private void _playSectorDelta_notOvals(GameStateData currentGameState, float[] lapAndSectorsComparisonData)
        {
            double r = Utilities.random.NextDouble() * 10;
            Boolean canPlayForRace = frequencyOfRaceSectorDeltaReports > r * 1.5;
            Boolean canPlayForPracAndQual = frequencyOfPracticeAndQualSectorDeltaReports > r;

            // only report sector time if this is a valid lap
            Boolean sectorWasOnValidLap;
            if (currentGameState.SessionData.IsNewLap)
            {
                sectorWasOnValidLap = currentGameState.SessionData.PreviousLapWasValid;
            }
            else
            {
                sectorWasOnValidLap = currentGameState.SessionData.CurrentLapIsValid;
            }

            if (!GlobalBehaviourSettings.useOvalLogic &&
                sectorWasOnValidLap &&
                ((currentGameState.SessionData.SessionType == SessionType.Race && canPlayForRace) ||
                 (((currentGameState.SessionData.SessionType == SessionType.Practice && (currentGameState.OpponentData.Count > 0 || currentGameState.SessionData.CompletedLaps > 1))
                   || currentGameState.SessionData.SessionType == SessionType.Qualify ||
                   currentGameState.SessionData.SessionType == SessionType.PrivateQualify ||
                   ((currentGameState.SessionData.SessionType == SessionType.HotLap || currentGameState.SessionData.SessionType == SessionType.LonePractice)
                    && currentGameState.SessionData.CompletedLaps > 1)) && canPlayForPracAndQual)))
            {
                float playerSector = -1;
                float comparisonSector = -1;
                SectorSet sectorEnum = SectorSet.NONE;
                switch (currentGameState.SessionData.SectorNumber)
                {
                    case 1:
                        playerSector = currentGameState.SessionData.LastSector3Time;
                        comparisonSector = lapAndSectorsComparisonData[3];
                        sectorEnum = SectorSet.THREE;
                        break;
                    case 2:
                        playerSector = currentGameState.SessionData.LastSector1Time;
                        comparisonSector = lapAndSectorsComparisonData[1];
                        sectorEnum = SectorSet.ONE;
                        break;
                    case 3:
                        playerSector = currentGameState.SessionData.LastSector2Time;
                        comparisonSector = lapAndSectorsComparisonData[2];
                        sectorEnum = SectorSet.TWO;
                        break;
                }
                List<MessageFragment> messageFragments = getSingleSectorDeltaMessages(sectorEnum, playerSector, comparisonSector, isHotLappingOrLonePractice /*selfPace*/);
                if (messageFragments.Count > 0)
                {
                    int delay = Utilities.random.Next(2, 4);
                    audioPlayer.playMessage(new QueuedMessage("singleSectorDelta", delay + 10, secondsDelay: delay, messageFragments: messageFragments, abstractEvent: this, priority: 5));
                }
            }
        }


        private ConsistencyResult checkAgainstPreviousLaps(Boolean isOval)
        {
            if (conditionsWindow.Count() >= lapTimesWindowSize && ConditionsHaveChanged(conditionsWindow[0], conditionsWindow[lapTimesWindowSize - 1]))
            {
                return ConsistencyResult.NOT_APPLICABLE;
            }

            Boolean isImproving = true;
            Boolean isWorsening = true;
            Boolean isConsistent = true;

            for (int index = 0; index < lapTimesWindowSize - 1; index++)
            {
                // check the lap time was recorded
                if (lapTimesWindow[index] <= 0)
                {
                    Console.WriteLine("No data for consistency check");
                    lastConsistencyMessage = ConsistencyResult.NOT_APPLICABLE;
                    return ConsistencyResult.NOT_APPLICABLE;
                }
                if (lapTimesWindow[index] >= lapTimesWindow[index + 1])
                {
                    isImproving = false;
                    break;
                }
            }

            for (int index = 0; index < lapTimesWindowSize - 1; index++)
            {
                if (lapTimesWindow[index] <= lapTimesWindow[index + 1])
                {
                    isWorsening = false;
                }
            }

            for (int index = 0; index < lapTimesWindowSize - 1; index++)
            {
                float lastLap = lapTimesWindow[index];
                float lastButOneLap = lapTimesWindow[index + 1];
                float consistencyRange = (lastButOneLap * consistencyLimit) / 100;
                if (lastLap > lastButOneLap + consistencyRange || lastLap < lastButOneLap - consistencyRange)
                {
                    isConsistent = false;
                }
            }

            if (isImproving)
            {
                if (lastConsistencyMessage == ConsistencyResult.IMPROVING)
                {
                    // don't play the same improving message - see if the consistent message might apply
                    if (isConsistent)
                    {
                        lastConsistencyMessage = ConsistencyResult.CONSISTENT;
                        return ConsistencyResult.CONSISTENT;
                    }
                }
                else
                {
                    lastConsistencyMessage = ConsistencyResult.IMPROVING;
                    return ConsistencyResult.IMPROVING;
                }
            }
            if (isWorsening)
            {
                // disable this for ovals
                if (isOval)
                {
                    return ConsistencyResult.NOT_APPLICABLE;
                }
                if (lastConsistencyMessage == ConsistencyResult.WORSENING)
                {
                    // don't play the same worsening message - see if the consistent message might apply
                    if (isConsistent)
                    {
                        lastConsistencyMessage = ConsistencyResult.CONSISTENT;
                        return ConsistencyResult.CONSISTENT;
                    }
                }
                else
                {
                    lastConsistencyMessage = ConsistencyResult.WORSENING;
                    return ConsistencyResult.WORSENING;
                }
            }
            if (isConsistent)
            {
                lastConsistencyMessage = ConsistencyResult.CONSISTENT;
                return ConsistencyResult.CONSISTENT;
            }
            return ConsistencyResult.NOT_APPLICABLE;
        }

        private enum ConsistencyResult
        {
            NOT_APPLICABLE, CONSISTENT, IMPROVING, WORSENING
        }

        private LastLapRating getLastLapRating(GameStateData currentGameState, float[] bestLapComparisonData, Boolean selfPace)
        {
            // if we've only completed a couple of laps, make this 'no data'
            if (currentGameState.SessionData.CompletedLaps < 3)
            {
                return LastLapRating.NO_DATA;
            }
            if (currentGameState.SessionData.PreviousLapWasValid && currentGameState.SessionData.LapTimePrevious > 0)
            {
                float closeThreshold = currentGameState.SessionData.LapTimePrevious * goodLapPercent / 100;
                float matchingRacePaceThreshold = currentGameState.SessionData.LapTimePrevious * matchingRacePacePercent / 100;

                // no point in reporting lap awesomeness if we have no comparison data:
                Boolean hasPlayerLapComparisonData = currentGameState.SessionData.CompletedLaps > 1
                    && currentGameState.SessionData.LapTimePrevious > 0
                    && currentGameState.SessionData.PreviousLapWasValid;

                Boolean sessionHasOpponents = currentGameState.SessionData.SessionType != SessionType.HotLap
					&& currentGameState.SessionData.SessionType != SessionType.LonePractice
					&& currentGameState.OpponentData.Count > 0;
                Boolean hasComparisonData = (sessionHasOpponents || selfPace) && bestLapComparisonData[0] > 0;

                if (!hasPlayerLapComparisonData && !hasComparisonData)
                {
                    return LastLapRating.NO_DATA;
                }

                if (!selfPace)
                {
                    if (currentGameState.SessionData.OverallSessionBestLapTime == currentGameState.SessionData.LapTimePrevious)
                    {
                        return LastLapRating.BEST_OVERALL;
                    }
                    else if (GameStateData.Multiclass && currentGameState.SessionData.PlayerClassSessionBestLapTime == currentGameState.SessionData.LapTimePrevious)
                    {
                        return LastLapRating.BEST_IN_CLASS;
                    }
                    else if (currentGameState.SessionData.SessionType == SessionType.Race && bestLapComparisonData[0] > 0 && bestLapComparisonData[0] >= currentGameState.SessionData.LapTimePrevious)
                    {
                        return LastLapRating.SETTING_CURRENT_PACE;
                    }
                    else if (currentGameState.SessionData.SessionType == SessionType.Race && bestLapComparisonData[0] > 0 && bestLapComparisonData[0] > currentGameState.SessionData.LapTimePrevious - closeThreshold)
                    {
                        return LastLapRating.CLOSE_TO_CURRENT_PACE;
                    }
                    else if (currentGameState.SessionData.LapTimePrevious == currentGameState.SessionData.PlayerLapTimeSessionBest)
                    {
                        if (currentGameState.SessionData.OpponentsLapTimeSessionBestOverall > currentGameState.SessionData.LapTimePrevious - closeThreshold)
                        {
                            return LastLapRating.PERSONAL_BEST_CLOSE_TO_OVERALL_LEADER;
                        }
                        else if (currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass > currentGameState.SessionData.LapTimePrevious - closeThreshold)
                        {
                            return LastLapRating.PERSONAL_BEST_CLOSE_TO_CLASS_LEADER;
                        }
                        else if (currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass > 0 || currentGameState.SessionData.OpponentsLapTimeSessionBestOverall > 0)
                        {
                            return LastLapRating.PERSONAL_BEST_STILL_SLOW;
                        }
                    }
                    else if (currentGameState.SessionData.OpponentsLapTimeSessionBestOverall >= currentGameState.SessionData.LapTimePrevious - closeThreshold)
                    {
                        return LastLapRating.CLOSE_TO_OVERALL_LEADER;
                    }
                    else if (currentGameState.SessionData.OpponentsLapTimeSessionBestPlayerClass >= currentGameState.SessionData.LapTimePrevious - closeThreshold)
                    {
                        return LastLapRating.CLOSE_TO_CLASS_LEADER;
                    }
                    else if (currentGameState.SessionData.PlayerLapTimeSessionBest >= currentGameState.SessionData.LapTimePrevious - closeThreshold
                        && currentGameState.SessionData.CompletedLaps > 1)
                    {
                        return LastLapRating.CLOSE_TO_PERSONAL_BEST;
                    }
                    else if (bestLapComparisonData[0] > 0 &&
                        bestLapComparisonData[0] < currentGameState.SessionData.LapTimePrevious - LapTimes.outlierPaceLimits[currentGameState.SessionData.TrackDefinition.trackLengthClass])
                    {
                        // this is an outlier
                        return LastLapRating.OUTLIER;
                    }
                    else if (bestLapComparisonData[0] > 0 && bestLapComparisonData[0] < currentGameState.SessionData.LapTimePrevious - 3)
                    {
                        // 3 seconds off the pace
                        return LastLapRating.BAD;
                    }
                    else if (currentGameState.SessionData.PlayerLapTimeSessionBest > 0)
                    {
                        return LastLapRating.MEH;
                    }
                }
                else
                {
                    if (bestLapComparisonData[0] > 0 && currentGameState.SessionData.LapTimePrevious == bestLapComparisonData[0])
                    {
                        return LastLapRating.PERSONAL_BEST;
                    }
                    else if (bestLapComparisonData[0] > 0 && bestLapComparisonData[0] >= currentGameState.SessionData.LapTimePrevious - closeThreshold
                        && currentGameState.SessionData.CompletedLaps > 1)
                    {
                        return LastLapRating.CLOSE_TO_PERSONAL_BEST;
                    }
                    else if (bestLapComparisonData[0] > 0 && bestLapComparisonData[0] < currentGameState.SessionData.LapTimePrevious - 3)
                    {
                        // 3 seconds off the pace
                        return LastLapRating.BAD;
                    }
                    else if (currentGameState.SessionData.PlayerLapTimeSessionBest > 0)
                    {
                        return LastLapRating.MEH;
                    }
                }
            }
            return LastLapRating.NO_DATA;
        }

        internal static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.HOWS_MY_PACE,
            SpeechCommands.ID.HOWS_MY_SELF_PACE,
            SpeechCommands.ID.WHATS_MY_BEST_LAP_TIME,
            SpeechCommands.ID.WHATS_MY_LAST_SECTOR_TIME,
            SpeechCommands.ID.WHATS_THE_FASTEST_LAP_TIME,
            SpeechCommands.ID.WHAT_ARE_MY_SECTOR_TIMES,
            SpeechCommands.ID.WHAT_WAS_MY_LAST_LAP_TIME,
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
                case SpeechCommands.ID.WHAT_ARE_MY_SECTOR_TIMES:
                {
                    if (currentGameState != null &&
                        currentGameState.SessionData.LastSector1Time > -1 && currentGameState.SessionData.LastSector2Time > -1 && currentGameState.SessionData.LastSector3Time > -1)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("sector1Time", 0,
                            messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(currentGameState.SessionData.LastSector1Time, Precision.AUTO_LAPTIMES))));
                        audioPlayer.playMessageImmediately(new QueuedMessage("sector2Time", 0,
                            messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(currentGameState.SessionData.LastSector2Time, Precision.AUTO_LAPTIMES))));
                        audioPlayer.playMessageImmediately(new QueuedMessage("sector3Time", 0,
                            messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(currentGameState.SessionData.LastSector3Time, Precision.AUTO_LAPTIMES))));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                    }

                    break;
                }
                case SpeechCommands.ID.WHATS_MY_LAST_SECTOR_TIME:
                {
                    if (currentGameState != null && currentGameState.SessionData.SectorNumber == 1 && currentGameState.SessionData.LastSector3Time > -1)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("sector3Time", 0,
                            messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(currentGameState.SessionData.LastSector3Time, Precision.AUTO_LAPTIMES))));
                    }
                    else if (currentGameState != null && currentGameState.SessionData.SectorNumber == 2 && currentGameState.SessionData.LastSector1Time > -1)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("sector1Time", 0,
                            messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(currentGameState.SessionData.LastSector1Time, Precision.AUTO_LAPTIMES))));
                    }
                    else if (currentGameState != null && currentGameState.SessionData.SectorNumber == 3 && currentGameState.SessionData.LastSector2Time > -1)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("sector2Time", 0,
                            messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(currentGameState.SessionData.LastSector2Time, Precision.AUTO_LAPTIMES))));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                    }

                    break;
                }
                case SpeechCommands.ID.WHATS_MY_BEST_LAP_TIME:
                {
                    Boolean gotData = false;
                    if (CrewChief.currentGameState != null)
                    {
                        float bestLap = CrewChief.currentGameState.TimingData.getPlayerBestLapTime(TimingData.ConditionsEnum.ANY);
                        if (bestLap > 0)
                        {
                            gotData = true;
                            audioPlayer.playMessageImmediately(new QueuedMessage("bestLapTime", 0,
                                messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(bestLap, Precision.AUTO_LAPTIMES))));
                        }
                    }
                    if (!gotData)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                    }

                    break;
                }
                case SpeechCommands.ID.WHATS_THE_FASTEST_LAP_TIME:
                {
                    if (currentGameState != null && currentGameState.SessionData.PlayerClassSessionBestLapTime > 0)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("sessionFastestLaptime", 0,
                            messageFragments: MessageContents(TimeSpanWrapper.FromSeconds(currentGameState.SessionData.PlayerClassSessionBestLapTime, Precision.AUTO_LAPTIMES))));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                    }

                    break;
                }
                case SpeechCommands.ID.WHAT_WAS_MY_LAST_LAP_TIME:
                {
                    if (CrewChief.currentGameState != null && CrewChief.currentGameState.SessionData.LapTimePrevious > 0)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("laptime", 0,
                            messageFragments: MessageContents(folderLapTimeIntro, TimeSpanWrapper.FromSeconds(
                                CrewChief.currentGameState.SessionData.LapTimePrevious, Precision.AUTO_LAPTIMES))));

                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                    }

                    break;
                }
                case SpeechCommands.ID.HOWS_MY_PACE:
                    if (sessionType == SessionType.Race)
                    {
                        reportRacePace(false);
                    }
                    else
                    {
                        reportPracticeQualifyPace(false);
                    }
                    break;
                case SpeechCommands.ID.HOWS_MY_SELF_PACE:
                    if (sessionType == SessionType.Race)
                    {
                        reportRacePace(true);
                    }
                    else
                    {
                        reportPracticeQualifyPace(true);
                    }
                    break;
            }
        }

        /// <summary>
        /// Report pace saved in lastLapRating or lastLapSelfRating
        /// </summary>
        /// <param name="selfPace" Pace compared to my best vs compared to opponents></param>
        private void reportRacePace(bool selfPace)
        {
            if (currentGameState == null)
            {
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                // done.
            }
            else
            {
                float[] bestComparisonLapData;
                if (selfPace)
                {
                    bestComparisonLapData = currentGameState.SessionData.getPlayerTimeAndSectorsForBestLap(false /*ignoreLast*/);  // Currently, use sectors of the best valid lap for self pace comparison.
                    // Later down the road, we might want use best sector times out of all the valid laps,
                    // or report them as a response to some separate voice command.
                }
                else if (currentGameState.TimingData.conditionsHaveChanged)
                {
                    bestComparisonLapData = new float[] {
                        currentGameState.TimingData.getPlayerClassOpponentBestLapTime(),
                        currentGameState.TimingData.getPlayerClassOpponentBestLapSector1Time(),
                        currentGameState.TimingData.getPlayerClassOpponentBestLapSector2Time(),
                        currentGameState.TimingData.getPlayerClassOpponentBestLapSector3Time()
                    };
                }
                else
                {
                    bestComparisonLapData = currentGameState.getTimeAndSectorsForBestOpponentLapInWindow(paceCheckLapsWindowForRaceToUse, currentGameState.carClass);
                }
                if (bestComparisonLapData[0] < 0 || lastLapRating == LastLapRating.NO_DATA)
                {
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                    // done.
                }
                else
                {
                    TimeSpan lapToCompare = TimeSpan.FromSeconds(currentGameState.SessionData.LapTimePrevious - bestComparisonLapData[0]);
                    String timeToFindFolder = null;
                    if (lapToCompare.Seconds == 0 && lapToCompare.Milliseconds < 200)
                    {
                        timeToFindFolder = folderNeedToFindOneMoreTenth;
                    }
                    else if (lapToCompare.Seconds == 0 && lapToCompare.Milliseconds < 600)
                    {
                        timeToFindFolder = folderNeedToFindAFewMoreTenths;
                    }
                    else if ((lapToCompare.Seconds == 1 && lapToCompare.Milliseconds < 500) ||
                             (lapToCompare.Seconds == 0 && lapToCompare.Milliseconds >= 600))
                    {
                        timeToFindFolder = folderNeedToFindASecond;
                    }
                    else if ((lapToCompare.Seconds == 1 && lapToCompare.Milliseconds >= 500) ||
                             lapToCompare.Seconds > 1)
                    {
                        timeToFindFolder = folderNeedToFindMoreThanASecond;
                    }
                    List<MessageFragment> messages = new List<MessageFragment>();
                    if (!selfPace)
                    {
                        switch (lastLapRating)
                        {
                            case LastLapRating.BEST_OVERALL:
                            case LastLapRating.BEST_IN_CLASS:
                            case LastLapRating.SETTING_CURRENT_PACE:
                                audioPlayer.playMessageImmediately(new QueuedMessage(folderSettingCurrentRacePace, 0));
                                break;
                            case LastLapRating.PERSONAL_BEST_CLOSE_TO_OVERALL_LEADER:
                            case LastLapRating.PERSONAL_BEST_CLOSE_TO_CLASS_LEADER:
                            case LastLapRating.CLOSE_TO_OVERALL_LEADER:
                            case LastLapRating.CLOSE_TO_CLASS_LEADER:
                            case LastLapRating.PERSONAL_BEST_STILL_SLOW:
                            case LastLapRating.CLOSE_TO_PERSONAL_BEST:
                            case LastLapRating.CLOSE_TO_CURRENT_PACE:
                                if (timeToFindFolder == null || timeToFindFolder != folderNeedToFindMoreThanASecond)
                                {
                                    if (lastLapRating == LastLapRating.CLOSE_TO_CURRENT_PACE)
                                    {
                                        messages.Add(MessageFragment.Text(folderMatchingCurrentRacePace));
                                    }
                                    else
                                    {
                                        messages.Add(MessageFragment.Text(folderPaceOK));
                                    }
                                }
                                if (timeToFindFolder != null)
                                {
                                    messages.Add(MessageFragment.Text(timeToFindFolder));
                                }
                                if (messages.Count > 0)
                                {
                                    audioPlayer.playMessageImmediately(new QueuedMessage("lapTimeRacePaceReport", 0, messageFragments: messages));
                                }
                                break;
                            case LastLapRating.MEH:
                                if (timeToFindFolder != null)
                                {
                                    messages.Add(MessageFragment.Text(timeToFindFolder));
                                }
                                audioPlayer.playMessageImmediately(new QueuedMessage("lapTimeRacePaceReport", 0, messageFragments: messages));
                                break;
                            case LastLapRating.BAD:
                                // don't play this if we've disabled complaints or have complained too much
                                if (GlobalBehaviourSettings.complaintsCountInThisSession < GlobalBehaviourSettings.maxComplaintsPerSession)
                                {
                                    messages.Add(MessageFragment.Text(folderPaceBad));
                                    GlobalBehaviourSettings.complaintsCountInThisSession++;
                                }
                                if (timeToFindFolder != null)
                                {
                                    messages.Add(MessageFragment.Text(timeToFindFolder));
                                }
                                audioPlayer.playMessageImmediately(new QueuedMessage("lapTimeRacePaceReport", 0, messageFragments: messages));

                                break;
                            default:
                                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                                break;
                        }
                    }
                    else
                    {
                        // Fors self pace case, announce last lap time.
                        if (currentGameState.SessionData.LapTimePrevious > 0)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage("laptime", 0,
                                messageFragments: MessageContents(folderLapTimeIntro, TimeSpanWrapper.FromSeconds(
                                    currentGameState.SessionData.LapTimePrevious, Precision.AUTO_LAPTIMES))));
                        }

                        switch (lastLapSelfRating)
                        {
                            case LastLapRating.PERSONAL_BEST:
                                audioPlayer.playMessageImmediately(new QueuedMessage(folderPersonalBest, 0));
                                break;
                            case LastLapRating.CLOSE_TO_PERSONAL_BEST:
                                if (timeToFindFolder == null || timeToFindFolder != folderNeedToFindMoreThanASecond)
                                {
                                    messages.Add(MessageFragment.Text(folderPaceOK));
                                }
                                if (timeToFindFolder != null)
                                {
                                    messages.Add(MessageFragment.Text(timeToFindFolder));
                                }
                                if (messages.Count > 0)
                                {
                                    audioPlayer.playMessageImmediately(new QueuedMessage("lapTimeRacePaceReport", 0, messageFragments: messages));
                                }
                                break;
                            case LastLapRating.BAD:
                                if (GlobalBehaviourSettings.complaintsCountInThisSession < GlobalBehaviourSettings.maxComplaintsPerSession)
                                {
                                    messages.Add(MessageFragment.Text(folderPaceBad));
                                    GlobalBehaviourSettings.complaintsCountInThisSession++;
                                }
                                if (timeToFindFolder != null)
                                {
                                    messages.Add(MessageFragment.Text(timeToFindFolder));
                                }
                                audioPlayer.playMessageImmediately(new QueuedMessage("lapTimeRacePaceReport", 0, messageFragments: messages));
                                break;
                            case LastLapRating.MEH:
                                if (timeToFindFolder != null)
                                {
                                    messages.Add(MessageFragment.Text(timeToFindFolder));
                                }
                                audioPlayer.playMessageImmediately(new QueuedMessage("lapTimeRacePaceReport", 0, messageFragments: messages));
                                break;
                            default:
                                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                                break;
                        }
                    }
                    SectorReportOption reportOption = SectorReportOption.ALL;
                    double r = Utilities.random.NextDouble();
                    // usually report the combined sectors
                    if (r > 0.33)
                    {
                        reportOption = SectorReportOption.WORST_ONLY;
                    }
                    List<MessageFragment> sectorDeltaMessages = getSectorDeltaMessages(reportOption, currentGameState.SessionData.LastSector1Time, bestComparisonLapData[1],
                        currentGameState.SessionData.LastSector2Time, bestComparisonLapData[2], currentGameState.SessionData.LastSector3Time, bestComparisonLapData[3], false, selfPace);
                    if (sectorDeltaMessages.Count > 0)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("sectorDeltas", 0, messageFragments: sectorDeltaMessages));
                    }
                }
            }
        }

        /// <summary>
        /// Report pace saved in lastLapRating or lastLapSelfRating
        /// </summary>
        /// <param name="selfPace" Pace compared to my best vs compared to opponents></param>
        private void reportPracticeQualifyPace(bool selfPace)
        {
            if (!deltaPlayerLastToSessionBestInClassSet)
            {
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                // done.
            }
            else
            {
                if (!selfPace)
                {
                    if (deltaPlayerLastToSessionBestInClass <= TimeSpan.Zero)
                    {
                        TimeSpan gapBehind = deltaPlayerLastToSessionBestInClass.Negate();
                        if (gapBehind.Seconds > 0 || gapBehind.Milliseconds > 50)
                        {
                            // delay this a bit...
                            audioPlayer.playMessageImmediately(new QueuedMessage("lapTimeNotRaceGap", 0,
                                messageFragments: MessageContents(folderGapIntro, new TimeSpanWrapper(gapBehind, Precision.AUTO_GAPS), folderQuickerThanSecondPlace), abstractEvent: this));
                        }
                    }
                    else if (deltaPlayerLastToSessionBestInClass.Seconds == 0 && deltaPlayerLastToSessionBestInClass.Milliseconds < 50)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(folderLessThanATenthOffThePace, 0));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("lapTimeNotRaceGap", 0,
                            messageFragments: MessageContents(new TimeSpanWrapper(deltaPlayerLastToSessionBestInClass, Precision.AUTO_GAPS), folderGapOutroOffPace)));
                    }
                }
                else
                {
                    // Fors self pace case, announce last lap time.
                    if (currentGameState.SessionData.LapTimePrevious > 0)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("laptime", 0,
                            messageFragments: MessageContents(folderLapTimeIntro, TimeSpanWrapper.FromSeconds(
                                currentGameState.SessionData.LapTimePrevious, Precision.AUTO_LAPTIMES))));

                        // We also neeed to announce how good it is.
                        List<MessageFragment> messages = new List<MessageFragment>();
                        switch (lastLapSelfRating)
                        {
                            case LastLapRating.PERSONAL_BEST:
                                audioPlayer.playMessageImmediately(new QueuedMessage(folderPersonalBest, 0));
                                break;
                            case LastLapRating.CLOSE_TO_PERSONAL_BEST:
                                audioPlayer.playMessageImmediately(new QueuedMessage(folderPaceOK, 0));
                                break;
                            case LastLapRating.MEH:
                            case LastLapRating.BAD:
                                if (GlobalBehaviourSettings.complaintsCountInThisSession < GlobalBehaviourSettings.maxComplaintsPerSession)
                                {
                                    messages.Add(MessageFragment.Text(folderPaceBad));
                                    GlobalBehaviourSettings.complaintsCountInThisSession++;
                                }
                                audioPlayer.playMessageImmediately(new QueuedMessage("lapTimeRacePaceReport", 0, messageFragments: messages));
                                break;
                            default:
                                break;
                        }
                    }
                }

                // wrap this in a try-catch until I work out why the array indices are being screwed up in online races (yuk...)
                try
                {
                    float[] bestComparisonLapData = selfPace
                        ? currentGameState.SessionData.getPlayerTimeAndSectorsForBestLap(false /*ignoreLast*/)  // Currently, use sectors of the best valid lap for self pace comparison.
                        // Later down the road, we might want use best sector times out of all the valid laps,
                        // or report them as a response to some separate voice command.
                        : new float[] {
                            currentGameState.TimingData.getPlayerClassOpponentBestLapTime(),
                            currentGameState.TimingData.getPlayerClassOpponentBestLapSector1Time(),
                            currentGameState.TimingData.getPlayerClassOpponentBestLapSector2Time(),
                            currentGameState.TimingData.getPlayerClassOpponentBestLapSector3Time(),
                        };

                    List<MessageFragment> sectorDeltaMessages = getSectorDeltaMessages(SectorReportOption.ALL, currentGameState.SessionData.LastSector1Time, bestComparisonLapData[1],
                        currentGameState.SessionData.LastSector2Time, bestComparisonLapData[2], currentGameState.SessionData.LastSector3Time, bestComparisonLapData[3], true, selfPace);
                    if (sectorDeltaMessages.Count > 0)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("sectorDeltas", 0, messageFragments: sectorDeltaMessages));
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Unable to get sector deltas: ");
                }
            }
        }

        private enum LastLapRating
        {
            BEST_OVERALL, BEST_IN_CLASS, SETTING_CURRENT_PACE, CLOSE_TO_CURRENT_PACE, PERSONAL_BEST, PERSONAL_BEST_CLOSE_TO_OVERALL_LEADER,
            PERSONAL_BEST_CLOSE_TO_CLASS_LEADER, PERSONAL_BEST_STILL_SLOW, CLOSE_TO_OVERALL_LEADER, CLOSE_TO_CLASS_LEADER,
            CLOSE_TO_PERSONAL_BEST, MEH, BAD, NO_DATA, OUTLIER
        }

        public enum SectorSet
        {
            ONE, TWO, THREE, NONE
        }

        /// <summary>
        /// Categorise the time gap
        /// delta < 5 hundredths : "fast"
        /// delta ~ 1 tenth : "a tenth"
        /// delta ~ 2 tenths : "two tenths"
        /// delta ~ 1 second : "a second"
        /// delta < 10 seconds : "x point x seconds"
        ///       (message calculated later)
        /// </summary>
        /// <param name="delta"></param>
        /// <returns>Deltas</returns>
        internal static Delta GetDelta(float delta)
        {
            Delta result = Delta.NONE;
            if (delta < 0.05f)
            {
                result = Delta.FAST;
            }
            else if (delta < 0.15f)
            {
                result = Delta.A_TENTH;
            }
            else if (delta < 0.25f)
            {
                result = Delta.TWO_TENTHS;
            }
            else if (delta > 0.95f && delta < 1.05f)
            {
                result = Delta.A_SECOND;
            }
            else if (delta < 10)
            {
                result = Delta.AUTO_GAPS;
            }
            return result;
        }
        public static List<MessageFragment> getSingleSectorDeltaMessages(SectorSet sector, float playerTime, float comparisonTime, bool selfPace)
        {
            List<MessageFragment> messages = new List<MessageFragment>();
            // the sector times must be > 5 seconds to be considered valid
            if (playerTime > 5 && comparisonTime > 5)
            {
                float delta = playerTime - comparisonTime;  // (No point in rounding it)
                switch (GetDelta(delta))
                {
                    case Delta.FAST:
                        switch (sector)
                        {
                            case SectorSet.ONE:
                                messages.Add(MessageFragment.Text(folderSector1Fast));
                                break;
                            case SectorSet.TWO:
                                messages.Add(MessageFragment.Text(folderSector2Fast));
                                break;
                            default:
                                messages.Add(MessageFragment.Text(folderSector3Fast));
                                break;
                        }
                        break;
                    case Delta.A_TENTH:
                        switch (sector)
                        {
                            case SectorSet.ONE:
                                messages.Add(MessageFragment.Text(selfPace ? folderSelfSector1ATenthOffThePace : folderSector1ATenthOffThePace));
                                break;
                            case SectorSet.TWO:
                                messages.Add(MessageFragment.Text(selfPace ? folderSelfSector2ATenthOffThePace : folderSector2ATenthOffThePace));
                                break;
                            default:
                                messages.Add(MessageFragment.Text(selfPace ? folderSelfSector3ATenthOffThePace : folderSector3ATenthOffThePace));
                                break;
                        }
                        break;
                    case Delta.TWO_TENTHS:
                        switch (sector)
                        {
                            case SectorSet.ONE:
                                messages.Add(MessageFragment.Text(selfPace ? folderSelfSector1TwoTenthsOffThePace : folderSector1TwoTenthsOffThePace));
                                break;
                            case SectorSet.TWO:
                                messages.Add(MessageFragment.Text(selfPace ? folderSelfSector2TwoTenthsOffThePace : folderSector2TwoTenthsOffThePace));
                                break;
                            default:
                                messages.Add(MessageFragment.Text(selfPace ? folderSelfSector3TwoTenthsOffThePace : folderSector3TwoTenthsOffThePace));
                                break;
                        }
                        break;
                    case Delta.A_SECOND:
                        switch (sector)
                        {
                            case SectorSet.ONE:
                                messages.Add(MessageFragment.Text(selfPace ? folderSelfSector1ASecondOffThePace : folderSector1ASecondOffThePace));
                                break;
                            case SectorSet.TWO:
                                messages.Add(MessageFragment.Text(selfPace ? folderSelfSector2ASecondOffThePace : folderSector2ASecondOffThePace));
                                break;
                            default:
                                messages.Add(MessageFragment.Text(selfPace ? folderSelfSector3ASecondOffThePace : folderSector3ASecondOffThePace));
                                break;
                        }
                        break;
                    case Delta.AUTO_GAPS:
                        if (delta < (4.0f + 2.0f * Utilities.random.NextDouble()))
                        {
                            switch (sector)
                            {
                                // delta < 4 to 6 seconds
                                case SectorSet.ONE:
                                    messages.Add(MessageFragment.Text(folderSector1Is));
                                    break;
                                case SectorSet.TWO:
                                    messages.Add(MessageFragment.Text(folderSector2Is));
                                    break;
                                default:
                                    messages.Add(MessageFragment.Text(folderSector3Is));
                                    break;
                            }
                            messages.Add(MessageFragment.Time(TimeSpanWrapper.FromSeconds(delta, Precision.AUTO_GAPS)));
                            messages.Add(MessageFragment.Text(selfPace ? folderSelfOffThePace : folderOffThePace));
                        }
                        break;
                    default:
                        break;
                }
            }
            if (messages.Count > 0)
            {
                Console.WriteLine("Sector = " + sector + " delta (-ve = player faster) = " + (playerTime - comparisonTime).ToString("0.000"));
                Console.WriteLine("Resolved delta message: " + String.Join(", ", messages));
            }
            else
            {
                Console.WriteLine("Skipping sector pace delta message:  Sector = " + sector + " delta (-ve = player faster) = " + (playerTime - comparisonTime).ToString("0.000"));
            }
            return messages;
        }

        /// <summary>
        /// If the delta < 0.5 round it to 0.01 else to 0.1
        /// </summary>
        private static float getAutoRoundedDelta(float time1, float time2)
        {
            float unroundedDelta = time1 - time2;
            float delta;
            if (Math.Abs(unroundedDelta) < 0.5)
            {
                delta = ((float)Math.Round(unroundedDelta * 100)) / 100f;
            }
            else
            {
                delta = ((float)Math.Round(unroundedDelta * 10)) / 10f;
            }
            return delta;
        }

        public static List<MessageFragment> getSectorDeltaMessages(SectorReportOption reportOption, float playerSector1, float comparisonSector1, float playerSector2,
            float comparisonSector2, float playerSector3, float comparisonSector3, Boolean comparisonIncludesAllLaps, bool selfPace)
        {
            List<MessageFragment> messageFragments = new List<MessageFragment>();
            float delta1 = float.MaxValue;
            float delta2 = float.MaxValue;
            float delta3 = float.MaxValue;
            // the sector times must be > 5 seconds to be considered valid
            if (playerSector1 > 5 && comparisonSector1 > 5)
            {
                if (playerSector1 < comparisonSector1)
                {
                    delta1 = -1;
                }
                else
                {
                    delta1 = getAutoRoundedDelta(playerSector1, comparisonSector1);
                }
            } if (playerSector2 > 5 && comparisonSector2 > 5)
            {
                if (playerSector2 < comparisonSector2)
                {
                    delta2 = -1;
                }
                else
                {
                    delta2 = getAutoRoundedDelta(playerSector2, comparisonSector2);
                }
            }
            if (playerSector3 > 5 && comparisonSector3 > 5)
            {
                if (playerSector3 < comparisonSector3)
                {
                    delta3 = -1;
                }
                else
                {
                    delta3 = getAutoRoundedDelta(playerSector3, comparisonSector3);
                }
            }

            if (reportOption == SectorReportOption.WORST_ONLY)
            {
                // remove the 2 best sector deltas so we only report on the worst one
                if (delta1 < float.MaxValue && delta1 > delta2 && delta1 > delta3)
                {
                    // worst is delta1
                    delta2 = float.MaxValue;
                    delta3 = float.MaxValue;
                }
                else if (delta2 < float.MaxValue && delta2 > delta1 && delta2 > delta3)
                {
                    // worst is delta2
                    delta1 = float.MaxValue;
                    delta3 = float.MaxValue;
                }
                else if (delta3 < float.MaxValue && delta3 > delta1 && delta3 > delta2)
                {
                    // worst is delta3
                    delta1 = float.MaxValue;
                    delta2 = float.MaxValue;
                }
            }
            // now report the deltas
            Boolean reportedDelta1 = false;
            Boolean reportedDelta2 = false;
            Boolean reportedDelta3 = false;
            if (nearlyEqual(delta1, delta2))
            {
                if (nearlyEqual(delta3, delta1))
                {	// 1 ~= 2 ~= 3
                    reportedDelta1 = true;
                    reportedDelta2 = true;
                    reportedDelta3 = true;
                    switch (GetDelta(delta1))
                    {
                        case Delta.FAST:
                            messageFragments.Add(MessageFragment.Text(folderAllSectorsFast));
                            break;
                        case Delta.A_TENTH:
                            messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfAllSectorsATenthOffThePace : folderAllSectorsATenthOffThePace));
                            break;
                        case Delta.TWO_TENTHS:
                            messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfAllSectorsTwoTenthsOffThePace : folderAllSectorsTwoTenthsOffThePace));
                            break;
                        case Delta.A_SECOND:
                            messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfAllSectorsASecondOffThePace : folderAllSectorsASecondOffThePace));
                            break;
                        case Delta.AUTO_GAPS:
                            messageFragments.Add(MessageFragment.Text(folderAllThreeSectorsAre));
                            messageFragments.Add(MessageFragment.Time(TimeSpanWrapper.FromSeconds(delta1, Precision.AUTO_GAPS)));
                            messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfOffThePace : folderOffThePace));
                            break;
                        default:
                            break;
                    }
                }
                else
                {	// 1 ~= 2
                    reportedDelta1 = true;
                    reportedDelta2 = true;
                    switch (GetDelta(delta1))
                    {
                        case Delta.FAST:
                            messageFragments.Add(MessageFragment.Text(folderSector1and2Fast));
                            break;
                        case Delta.A_TENTH:
                            messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector1and2ATenthOffThePace : folderSector1and2ATenthOffThePace));
                            break;
                        case Delta.TWO_TENTHS:
                            messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector1and2TwoTenthsOffThePace : folderSector1and2TwoTenthsOffThePace));
                            break;
                        case Delta.A_SECOND:
                            messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector1and2ASecondOffThePace : folderSector1and2ASecondOffThePace));
                            break;
                        case Delta.AUTO_GAPS:
                            messageFragments.Add(MessageFragment.Text(folderSectors1And2Are));
                            messageFragments.Add(MessageFragment.Time(TimeSpanWrapper.FromSeconds(delta1, Precision.AUTO_GAPS)));
                            messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfOffThePace : folderOffThePace));
                            break;
                        default:
                            break;
                    }
                }
            }
            else if (nearlyEqual(delta2, delta3))
            {	// 2 ~= 3
                reportedDelta2 = true;
                reportedDelta3 = true;
                switch (GetDelta(delta2))
                {
                    case Delta.FAST:
                        messageFragments.Add(MessageFragment.Text(folderSector2and3Fast));
                        break;
                    case Delta.A_TENTH:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector2and3ATenthOffThePace : folderSector2and3ATenthOffThePace));
                        break;
                    case Delta.TWO_TENTHS:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector2and3TwoTenthsOffThePace : folderSector2and3TwoTenthsOffThePace));
                        break;
                    case Delta.A_SECOND:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector2and3ASecondOffThePace : folderSector2and3ASecondOffThePace));
                        break;
                    case Delta.AUTO_GAPS:
                        messageFragments.Add(MessageFragment.Text(folderSectors2And3Are));
                        messageFragments.Add(MessageFragment.Time(TimeSpanWrapper.FromSeconds(delta2, Precision.AUTO_GAPS)));
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfOffThePace : folderOffThePace));
                        break;
                    default:
                        break;
                }
            }
            else if (nearlyEqual(delta1, delta3))
            {	// 1 ~= 3
                reportedDelta1 = true;
                reportedDelta3 = true;
                switch (GetDelta(delta1))
                {
                    case Delta.FAST:
                        messageFragments.Add(MessageFragment.Text(folderSector1and3Fast));
                        break;
                    case Delta.A_TENTH:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector1and3ATenthOffThePace : folderSector1and3ATenthOffThePace));
                        break;
                    case Delta.TWO_TENTHS:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector1and3TwoTenthsOffThePace : folderSector1and3TwoTenthsOffThePace));
                        break;
                    case Delta.A_SECOND:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector1and3ASecondOffThePace : folderSector1and3ASecondOffThePace));
                        break;
                    case Delta.AUTO_GAPS:
                        messageFragments.Add(MessageFragment.Text(folderSectors1And3Are));
                        messageFragments.Add(MessageFragment.Time(TimeSpanWrapper.FromSeconds(delta1, Precision.AUTO_GAPS)));
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfOffThePace : folderOffThePace));
                        break;
                    default:
                        break;
                }
            }
            // else all 3 deltas different

            if (!reportedDelta1)
            {
                switch (GetDelta(delta1))
                {
                    case Delta.FAST:
                        messageFragments.Add(MessageFragment.Text(folderSector1Fast));
                        break;
                    case Delta.A_TENTH:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector1ATenthOffThePace : folderSector1ATenthOffThePace));
                        break;
                    case Delta.TWO_TENTHS:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector1TwoTenthsOffThePace : folderSector1TwoTenthsOffThePace));
                        break;
                    case Delta.A_SECOND:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector1ASecondOffThePace : folderSector1ASecondOffThePace));
                        break;
                    case Delta.AUTO_GAPS:
                        messageFragments.Add(MessageFragment.Text(folderSector1Is));
                        messageFragments.Add(MessageFragment.Time(TimeSpanWrapper.FromSeconds(delta1, Precision.AUTO_GAPS)));
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfOffThePace : folderOffThePace));
                        break;
                    default:
                        break;
                }
            }
            if (!reportedDelta2)
            {
                switch (GetDelta(delta2))
                {
                    case Delta.FAST:
                        messageFragments.Add(MessageFragment.Text(folderSector2Fast));
                        break;
                    case Delta.A_TENTH:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector2ATenthOffThePace : folderSector2ATenthOffThePace));
                        break;
                    case Delta.TWO_TENTHS:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector2TwoTenthsOffThePace : folderSector2TwoTenthsOffThePace));
                        break;
                    case Delta.A_SECOND:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector2ASecondOffThePace : folderSector2ASecondOffThePace));
                        break;
                    case Delta.AUTO_GAPS:
                        messageFragments.Add(MessageFragment.Text(folderSector2Is));
                        messageFragments.Add(MessageFragment.Time(TimeSpanWrapper.FromSeconds(delta2, Precision.AUTO_GAPS)));
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfOffThePace : folderOffThePace));
                        break;
                    default:
                        break;
                }
            }
            if (!reportedDelta3)
            {
                switch (GetDelta(delta3))
                {
                    case Delta.FAST:
                        messageFragments.Add(MessageFragment.Text(folderSector3Fast));
                        break;
                    case Delta.A_TENTH:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector3ATenthOffThePace : folderSector3ATenthOffThePace));
                        break;
                    case Delta.TWO_TENTHS:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector3TwoTenthsOffThePace : folderSector3TwoTenthsOffThePace));
                        break;
                    case Delta.A_SECOND:
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfSector3ASecondOffThePace : folderSector3ASecondOffThePace));
                        break;
                    case Delta.AUTO_GAPS:
                        messageFragments.Add(MessageFragment.Text(folderSector3Is));
                        messageFragments.Add(MessageFragment.Time(TimeSpanWrapper.FromSeconds(delta3, Precision.AUTO_GAPS)));
                        messageFragments.Add(MessageFragment.Text(selfPace ? folderSelfOffThePace : folderOffThePace));
                        break;
                    default:
                        break;
                }
            }
            if (messageFragments.Count > 0)
            {
                Console.WriteLine("Player best sectors " + playerSector1.ToString("0.000") + ",    " + playerSector2.ToString("0.000") + ",    " + playerSector3.ToString("0.000"));
                Console.WriteLine("Opponent best sectors " + comparisonSector1.ToString("0.000") + ",    " + comparisonSector2.ToString("0.000") + ",    " + comparisonSector3.ToString("0.000"));
                Console.WriteLine("S1 delta (-ve = player faster) = " + (playerSector1 - comparisonSector1).ToString("0.000") +
                    "    S2 delta  = " + (playerSector2 - comparisonSector2).ToString("0.000") +
                    "    S3 delta  = " + (playerSector3 - comparisonSector3).ToString("0.000"));
                Console.WriteLine("Resolved delta message: " + String.Join(", ", messageFragments));
            }
            return messageFragments;
        }

        public enum SectorReportOption
        {
            WORST_ONLY, ALL
        }

        private Boolean ConditionsHaveChanged(Conditions.ConditionsSample sample1, Conditions.ConditionsSample sample2)
        {
            if (sample1 == null || sample2 == null)
            {
                // hmm....
                return false;
            }
            return ConditionsMonitor.getRainLevel(sample1.RainDensity) != ConditionsMonitor.getRainLevel(sample2.RainDensity) ||
                Math.Abs(sample1.TrackTemperature - sample2.TrackTemperature) > 4;
        }

        public static Boolean nearlyEqual(float a, float b)
        {
            if (a == b)
            {
                return true;
            }
            // calculate a suitable epsilon
            float absA = Math.Abs(a);
            float absB = Math.Abs(b);
            float diff = Math.Abs(absA - absB);
            float epsilon;
            if (diff <= 0.1f)
            {
                epsilon = 0.04f;
            }
            else if (diff <= 0.5f)
            {
                epsilon = 0.1f;
            }
            else
            {
                epsilon = 0.15f;
            }

            return diff < epsilon;
        }
    }
}
