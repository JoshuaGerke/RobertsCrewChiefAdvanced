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
    // used to tag opponents for particular attention (team mates, rivals, etc). This class uses
    // sound fragments from other classes as well as its own. Some other event classes check the WatchedOpponentKeys list
    // to modify their behaviours. Team mate / rival us only used to select the correct sound in cases
    // where we don't have a driver name recording
    
    class WatchedOpponents : AbstractEvent
    {
        public static String folderIsInPosition = "watched_opponents/is_in_position";  // for when we exit the pits
        public static String folderYourTeamMate = "watched_opponents/your_team_mate";
        public static String folderYourRival = "watched_opponents/your_rival";

        public static String folderAcknowledgeWeWillWatch = "watched_opponents/acknowledge_we_will_watch"; // "we're now watching ..."
        public static String folderAcknowledgeWatchOpponentWithCarNumber = "watched_opponents/acknowledge_watch_with_car_number"; // "we're now watching car number..."
        public static String folderAcknowledgeWatchTeamMate = "watched_opponents/acknowledge_watch_team_mate"; // "we'll watch this guy as our team mate"
        public static String folderAcknowledgeWatchRival = "watched_opponents/acknowledge_watch_rival"; // "we'll watch this guy as our rival"
        public static String folderAcknowledgeStopWatchingAll = "watched_opponents/acknowledge_stop_watching_all";
        public static String folderAcknowledgeStopWatchingName = "watched_opponents/acknowledge_stop_watching_driver_name"; // "we'll stop watching..."
        public static String folderAcknowledgeStopWatchingCarNumber = "watched_opponents/acknowledge_stop_watching_car_number"; // "we'll stop watching car number..."
        public static String folderAcknowledgeStopWatchingTeamMate = "watched_opponents/acknowledge_stop_watching_team_mate"; // "we'll stop watching our team mate"
        public static String folderAcknowledgeStopWatchingRival = "watched_opponents/acknowledge_stop_watching_rival";
        public static String folderAcknowledgeUnknownDriver = "watched_opponents/acknowledge_unknown_driver";   // "we can't see this opponent"
        public static String folderAcknowledgeNoWayToReferToDriver = "watched_opponents/acknowledge_no_way_to_refer_to_driver"; // "we have no way to refer to this opponent"

        public static String folderHasJustDoneA = "watched_opponents/has_just_done_a";
        public static String folderTeamMateHasJustDoneA = "watched_opponents/team_mate_has_just_done_a";
        public static String folderRivalHasJustDoneA = "watched_opponents/rival_has_just_done_a";
        public static String folderTeamMateIsPittingFromPosition = "watched_opponents/team_mate_pitting_from_position";
        public static String folderRivalIsPittingFromPosition = "watched_opponents/rival_pitting_from_position";

        public static String folderIsLeavingThePit = "watched_opponents/is_leaving_pits";
        public static String folderTeamMateIsLeavingThePit = "watched_opponents/team_mate_is_leaving_pits";
        public static String folderRivalIsLeavingThePit = "watched_opponents/rival_is_leaving_pits";

        public static String folderIsNowInPosition = "watched_opponents/is_now_in_position"; // for when a watched opponent's race position changes
        public static String folderTeamMateIsNowInPosition = "watched_opponents/team_mate_is_now_in_position";
        public static String folderRivalIsNowInPosition = "watched_opponents/rival_is_now_in_position";

        private GameStateData currentGameState;

        public Dictionary<string, WatchedOpponentData> watchedOpponentData = new Dictionary<string, WatchedOpponentData>();
        // remember to use the opponentKey (NOT the driver name) when accessing the watchedOpponentKeys directly
        public static HashSet<String> watchedOpponentKeys = new HashSet<string>();
        // 3 ways to report a watched opponent - by name (if we have it), as "your team mate", or as "your rival". The first case
        // is obviously preferable, and the "your rival" case will sound pretty shit but in cases where we ask the app to watch an
        // opponent who's not our team mate and for whom we have no driver name sound, what else can we do?
        public static HashSet<String> teamMates = new HashSet<string>();
        public static HashSet<String> rivals = new HashSet<string>();
        private Dictionary<string, DateTime> nextPositionUpdateDueTimes = new Dictionary<string, DateTime>();

        // pit exit messages will spam because IsExitingPit is true for the entire out lap,
        // so only allow a single pit exit message for an opponent on any given lap
        private Dictionary<string, int> pitExitMessagesPlayed = new Dictionary<string, int>();

        private List<string> namesSetFromProperty = new List<string>();
        private Boolean loadNamesFromProperty = false;

        public class WatchedOpponentData
        {
            public float bestLapTime;
            public int sectorNumber;
            public bool inPit;
            public int classPosition;
            public TyreType currentTyreType;
            public WatchedOpponentData(float bestLapTime, int sectorNumber, bool inPit, int classPosition, TyreType currentTyreType)
            {
                this.bestLapTime = bestLapTime;
                this.sectorNumber = sectorNumber;
                this.inPit = inPit;
                this.classPosition = classPosition;
                this.currentTyreType = currentTyreType;
            }
        }

        public WatchedOpponents(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
            string rawNamesFromProperty = UserSettings.GetUserSettings().getString("watched_opponent_names");
            if (rawNamesFromProperty != null && rawNamesFromProperty.Trim().Length > 0)
            {
                foreach (string rawName in rawNamesFromProperty.Split(','))
                {
                    string name = rawName.Trim();
                    if (name.Length > 0)
                    {
                        namesSetFromProperty.Add(name);
                        loadNamesFromProperty = true;
                    }
                }
            }
        }

        public override List<SessionType> applicableSessionTypes
        {
            get { return new List<SessionType> { SessionType.Practice, SessionType.Qualify, SessionType.Race }; }
        }

        // allow this event to trigger for FCY, but only the retired and DQ'ed checks - same as Opponents class
        public override List<SessionPhase> applicableSessionPhases
        {
            get { return new List<SessionPhase> { SessionPhase.Green, SessionPhase.Countdown, SessionPhase.FullCourseYellow }; }
        }

        public override void clearState()
        {
            watchedOpponentKeys.Clear();
            teamMates.Clear();
            rivals.Clear();
            nextPositionUpdateDueTimes.Clear();
            pitExitMessagesPlayed.Clear();
        }

        public override bool isMessageStillValid(string eventSubType, GameStateData currentGameState, Dictionary<String, Object> validationData)
        {
            // TODO: not sure if we're going to need an override here
            return base.isMessageStillValid(eventSubType, currentGameState, validationData);
        }
        
        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            this.currentGameState = currentGameState;
            // if a watched opponents list is set in the property, attempt to populate it on session start and race start
            if (loadNamesFromProperty
                && (currentGameState.SessionData.IsNewSession || (currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.JustGoneGreen)))
            {
                foreach (string name in namesSetFromProperty)
                {
                    string fullMatch = null;
                    string surnameMatch = null;
                    foreach (KeyValuePair<string, OpponentData> entry in currentGameState.OpponentData)
                    {
                        // check for full name match against all the names first
                        if (entry.Value.DriverRawName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            fullMatch = entry.Key;
                            // no need to check any more opponents with a full match
                            break;
                        }
                        // fall back to matching on surnames only
                        else if (name.Equals(DriverNameHelper.getUsableDriverName(entry.Value.DriverRawName, true), StringComparison.InvariantCultureIgnoreCase))
                        {
                            surnameMatch = entry.Key;
                        }
                    }
                    if (fullMatch != null)
                    {
                        Console.WriteLine("Watching driver " + fullMatch + " (name set in watched opponents property = " + name + ")");
                        watchedOpponentKeys.Add(fullMatch);
                    }
                    else if (surnameMatch != null)
                    {
                        Console.WriteLine("Watching driver " + surnameMatch + " (name set in watched opponents property = " + name + ")");
                        watchedOpponentKeys.Add(surnameMatch);
                    }
                    else
                    {
                        Console.WriteLine("Watched driver " + name + " doesn't appear in this session");
                    }
                }
            }
            if (previousGameState == null || GameStateData.onManualFormationLap || watchedOpponentKeys.Count == 0)
            {
                return;
            }
            bool isRaceSession = currentGameState.SessionData.SessionType == SessionType.Race;
            if (isRaceSession && previousGameState.PitData.InPitlane && !currentGameState.PitData.InPitlane)
            {
                // we just left the pits in a race session, report location of our watched opponents
                foreach (String opponentKey in WatchedOpponents.watchedOpponentKeys)
                {
                    OpponentData opponentData;
                    if (currentGameState.OpponentData.TryGetValue(opponentKey, out opponentData))
                    {
                        Tuple<int, float> deltaToWatchedOpponent = currentGameState.SessionData.DeltaTime.GetSignedDeltaTimeWithLapDifference(opponentData.DeltaTime);
                        reportOpponentDeltaTime(opponentKey, opponentData, true, deltaToWatchedOpponent);
                    }
                }
            }
            else
            {
                foreach (String opponentKey in WatchedOpponents.watchedOpponentKeys)
                {
                    OpponentData opponentData;
                    WatchedOpponentData previousOpponentData;
                    if (currentGameState.OpponentData.TryGetValue(opponentKey, out opponentData))
                    {
                        if (!watchedOpponentData.TryGetValue(opponentKey, out previousOpponentData))
                        {
                            watchedOpponentData.Add(opponentKey,
                                new WatchedOpponentData(opponentData.CurrentBestLapTime, opponentData.CurrentSectorNumber,
                                                        opponentData.InPits, opponentData.ClassPosition, opponentData.CurrentTyres));
                        }
                        else
                        {
                            // check for new lap time
                            if (opponentData.IsNewLap && !opponentData.InPits)
                            {
                                // get the laptime, decide whether it's worth reporting
                                if (opponentData.LastLapValid && opponentData.LastLapTime == opponentData.CurrentBestLapTime && opponentData.LastLapTime > 0 && opponentData.OpponentLapData.Count > 2)
                                {
                                    if (AudioPlayer.canReadName(opponentData.DriverRawName, false))
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent laptime", 10,
                                            messageFragments: MessageContents(opponentData, folderHasJustDoneA, TimeSpanWrapper.FromSeconds(opponentData.LastLapTime, Precision.AUTO_LAPTIMES))));
                                    }
                                    else if (teamMates.Contains(opponentKey))
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent laptime", 10,
                                            messageFragments: MessageContents(folderTeamMateHasJustDoneA, TimeSpanWrapper.FromSeconds(opponentData.LastLapTime, Precision.AUTO_LAPTIMES))));
                                    }
                                    else if (rivals.Contains(opponentKey))
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent laptime", 10,
                                            messageFragments: MessageContents(folderRivalHasJustDoneA, TimeSpanWrapper.FromSeconds(opponentData.LastLapTime, Precision.AUTO_LAPTIMES))));
                                    }
                                    else if(opponentData.CarNumber != "-1")
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent laptime", 10,
                                            messageFragments: MessageContents(Opponents.folderCarNumber, new CarNumber(opponentData.CarNumber), folderHasJustDoneA,
                                            TimeSpanWrapper.FromSeconds(opponentData.LastLapTime, Precision.AUTO_LAPTIMES))));
                                    }
                                    else
                                    {
                                        Console.WriteLine("Unable to report on watched opponent " + opponentKey + " because we have no way to reference him (name, teammate / rival or car number)");
                                    }
                                }
                            }
                            // pitting is handled by the Strategy class                            
                            else if (isRaceSession && opponentData.isOnOutLap() && !opponentData.InPits)
                            {
                                int alreadyPlayedForLapNumber = -1;
                                // if we've no previously played pit exit for this opponent, or the last pit exit message was played on an earlier lap, we can play it
                                if (!pitExitMessagesPlayed.TryGetValue(opponentKey, out alreadyPlayedForLapNumber) || alreadyPlayedForLapNumber < opponentData.CompletedLaps)
                                {
                                    pitExitMessagesPlayed[opponentKey] = opponentData.CompletedLaps;
                                    if (AudioPlayer.canReadName(opponentData.DriverRawName, false))
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent exiting pit", 10,
                                            messageFragments: MessageContents(opponentData, folderIsLeavingThePit)));
                                    }
                                    else if (teamMates.Contains(opponentKey))
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent exiting pit", 10,
                                            messageFragments: MessageContents(folderTeamMateIsLeavingThePit)));
                                    }
                                    else if (rivals.Contains(opponentKey))
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent exiting pit", 10,
                                            messageFragments: MessageContents(folderRivalIsLeavingThePit)));
                                    }
                                    else if (opponentData.CarNumber != "-1")
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent exiting pit", 10,
                                            messageFragments: MessageContents(Opponents.folderCarNumber, new CarNumber(opponentData.CarNumber), folderIsLeavingThePit)));
                                    }
                                    else
                                    {
                                        Console.WriteLine("Unable to report on watched opponent " + opponentKey + " because we have no way to reference him (name, teammate / rival or car number)");
                                    }
                                }
                            }
                            else if (opponentData.ClassPosition != previousOpponentData.classPosition && currentGameState.SessionData.CompletedLaps > 0)
                            {
                                // only allow these every 40 seconds to prevent spamming
                                DateTime nextUpdateDue;
                                if (!nextPositionUpdateDueTimes.TryGetValue(opponentKey, out nextUpdateDue) || currentGameState.Now > nextUpdateDue)
                                {
                                    nextPositionUpdateDueTimes[opponentKey] = currentGameState.Now.AddSeconds(40);
                                    if (AudioPlayer.canReadName(opponentData.DriverRawName, false))
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent position change", 10,
                                            messageFragments: MessageContents(opponentData, folderIsNowInPosition, opponentData.ClassPosition)));
                                    }
                                    else if (teamMates.Contains(opponentKey))
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent position change", 10,
                                            messageFragments: MessageContents(folderTeamMateIsNowInPosition, opponentData.ClassPosition)));
                                    }
                                    else if (rivals.Contains(opponentKey))
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent position change", 10,
                                            messageFragments: MessageContents(folderRivalIsNowInPosition, opponentData.ClassPosition)));
                                    }
                                    else if (opponentData.CarNumber != "-1")
                                    {
                                        audioPlayer.playMessage(new QueuedMessage("watched opponent position change", 10,
                                            messageFragments: MessageContents(Opponents.folderCarNumber, new CarNumber(opponentData.CarNumber), folderIsNowInPosition, opponentData.ClassPosition)));
                                    }
                                    else
                                    {
                                        Console.WriteLine("Unable to report on watched opponent " + opponentKey + " because we have no way to reference him (name, teammate / rival or car number)");
                                    }
                                }
                            }
                            // opponent tyre changes are reported by the opponents class

                            // the FlagsMonitor checks the watchedOpponents list to determine if a driver is 'interesting'

                            // and finally update our previous data
                            previousOpponentData.bestLapTime = opponentData.CurrentBestLapTime;
                            previousOpponentData.sectorNumber = opponentData.CurrentSectorNumber;
                            previousOpponentData.inPit = opponentData.InPits;
                            previousOpponentData.classPosition = opponentData.ClassPosition;
                            previousOpponentData.currentTyreType = opponentData.CurrentTyres;
                        }
                    }
                }
            }
        }

        private void reportOpponentDeltaTime(String opponentKey, OpponentData opponent, bool triggeredFromPitExit, Tuple<int, float> deltas)
        {
            String opponentName = opponent.DriverRawName;
            int opponentClassPosition = opponent.ClassPosition;

            int lapDifference = deltas.Item1;
            float timeDelta = deltas.Item2;
            if (timeDelta == 0 || (lapDifference == 0 && Math.Abs(timeDelta) < 0.05))
            {
                // the delta is not usable
                Console.WriteLine("Skipping watched opponent delta report as he's too close");
            }
            else
            {
                List<MessageFragment> messageFragments = new List<MessageFragment>();
                if (AudioPlayer.canReadName(opponentName, false))
                {
                    messageFragments.Add(MessageFragment.Opponent(opponent));
                }
                else if (teamMates.Contains(opponentKey))
                {
                    messageFragments.Add(MessageFragment.Text(folderYourTeamMate));
                }
                else if (rivals.Contains(opponentKey))
                {
                    messageFragments.Add(MessageFragment.Text(folderYourRival));
                }
                else if (opponent.CarNumber != "-1")
                {
                    // eewwww
                    messageFragments.Add(MessageFragment.Text(Opponents.folderCarNumber));
                    messageFragments.AddRange((new CarNumber(opponent.CarNumber)).getMessageFragments());
                }
                messageFragments.Add(triggeredFromPitExit ? MessageFragment.Text(folderIsInPosition) : MessageFragment.Text(folderIsNowInPosition));
                messageFragments.Add(MessageFragment.Integer(opponentClassPosition));

                messageFragments.AddRange(MkOpponentDelta(
                    lapDifference: lapDifference,
                    timeDelta: timeDelta,
                    expectedDeltaSign: 0,
                    includeLapInfo: true));
                audioPlayer.playMessageImmediately(new QueuedMessage(opponentName + "_timeDelta", 0,
                            messageFragments: messageFragments));

            }
        }
        
        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.STOP_WATCHING_ALL,
            SpeechCommands.ID.WATCH_BEHIND_IN_THE_RACE,
            SpeechCommands.ID.WATCH_IN_FRONT_IN_THE_RACE,
        };
        private static readonly List<SpeechCommands.ID> StartsWith = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.RIVAL__,
            SpeechCommands.ID.STOP_WATCHING__,
            SpeechCommands.ID.TEAM_MATE__ ,
            SpeechCommands.ID.WATCH__,
        };
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            var cmd =  SpeechCommands.SpeechToCommand(Commands, voiceMessage);
            if (cmd == SpeechCommands.ID.NO_COMMAND)
            {
                cmd = SpeechCommands.SpeechStartsWithToCommand(StartsWith, voiceMessage, false);
            }
            return cmd;
        }

        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID cmd)
        {
            if (currentGameState == null)
            {
                return;
            }

            switch (cmd)
            {
                case SpeechCommands.ID.RIVAL__:
                case SpeechCommands.ID.TEAM_MATE__:
                case SpeechCommands.ID.WATCH__:
                    {
                        if (SpeechRecogniser.DYNAMIC_PHRASES)
                        {
                            // parse and shit
                            // parse out the next phrase - might be an opponent name, position whatever
                            // probably need to remove the "watch" fragment here in case we get collisions
                            (String opponentKey, bool _) = Opponents.getOpponentKey(voiceMessage, currentGameState);
                            if (opponentKey != null && currentGameState.OpponentData.ContainsKey(opponentKey))
                            {
                                OpponentData opponent = currentGameState.OpponentData[opponentKey];
                                switch (cmd)
                                {
                                    case SpeechCommands.ID.TEAM_MATE__:
                                        {
                                            teamMates.Add(opponentKey);
                                            watchedOpponentKeys.Add(opponentKey);
                                            if (AudioPlayer.canReadName(opponent.DriverRawName))
                                            {
                                                audioPlayer.playMessageImmediately(new QueuedMessage("add watch acknowledge with driver name", 0,
                                                    messageFragments: MessageContents(folderAcknowledgeWeWillWatch, opponent)));
                                            }
                                            else
                                            {
                                                audioPlayer.playMessageImmediately(new QueuedMessage(folderAcknowledgeWatchTeamMate, 0));
                                            }
                                            break;
                                        }
                                    case SpeechCommands.ID.RIVAL__:
                                        {
                                            rivals.Add(opponentKey);
                                            watchedOpponentKeys.Add(opponentKey);
                                            if (AudioPlayer.canReadName(opponent.DriverRawName))
                                            {
                                                audioPlayer.playMessageImmediately(new QueuedMessage("add watch acknowledge with driver name", 0,
                                                    messageFragments: MessageContents(folderAcknowledgeWeWillWatch, opponent)));
                                            }
                                            else
                                            {
                                                audioPlayer.playMessageImmediately(new QueuedMessage(folderAcknowledgeWatchRival, 0));
                                            }
                                            break;
                                        }
                                    default:
                                        {
                                            watchOpponent(opponentKey, opponent);
                                            break;
                                        }
                                }
                                }
                            else
                                {
                                    Console.WriteLine("Can't find driver to watch");
                                    audioPlayer.playMessageImmediately(new QueuedMessage(folderAcknowledgeUnknownDriver, 0));
                                }
                            }
                        break;
                    }
                case SpeechCommands.ID.STOP_WATCHING__:
                    {
                        if (SpeechRecogniser.DYNAMIC_PHRASES)
                        {
                            // parse out the next phrase - might be an opponent name, position whatever
                            // probably need to remove the "watch" fragment here in case we get collisions
                            (String opponentKey, bool _) = Opponents.getOpponentKey(voiceMessage, currentGameState);
                            if (opponentKey != null && currentGameState.OpponentData.ContainsKey(opponentKey))
                            {

                                OpponentData opponent = currentGameState.OpponentData[opponentKey];
                                if (AudioPlayer.canReadName(opponent.DriverRawName))
                                {
                                    audioPlayer.playMessageImmediately(new QueuedMessage("stop watching name", 0,
                                            messageFragments: MessageContents(folderAcknowledgeStopWatchingName, opponent)));
                                }
                                else if (teamMates.Contains(opponentKey))
                                {
                                    audioPlayer.playMessageImmediately(new QueuedMessage(folderAcknowledgeStopWatchingTeamMate, 0));
                                }
                                else if (rivals.Contains(opponentKey))
                                {
                                    audioPlayer.playMessageImmediately(new QueuedMessage(folderAcknowledgeStopWatchingCarNumber, 0));
                                }
                                else if (opponent.CarNumber != "-1")
                                {
                                    audioPlayer.playMessageImmediately(new QueuedMessage("stop watching car number", 0,
                                            messageFragments: MessageContents(folderAcknowledgeStopWatchingCarNumber, new CarNumber(opponent.CarNumber))));
                                }
                                else
                                {
                                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                                }
                                watchedOpponentKeys.Remove(opponentKey);
                                teamMates.Remove(opponentKey);
                                rivals.Remove(opponentKey);
                            }
                            else
                            {
                                Console.WriteLine("Can't find driver to stop watching");
                                audioPlayer.playMessageImmediately(new QueuedMessage(folderAcknowledgeUnknownDriver, 0));
                            }
                        }
                        break;
                    }

                case SpeechCommands.ID.STOP_WATCHING_ALL:
                    watchedOpponentKeys.Clear();
                    teamMates.Clear();
                    rivals.Clear();
                    audioPlayer.playMessageImmediately(new QueuedMessage(folderAcknowledgeStopWatchingAll, 0));
                    break;
                case SpeechCommands.ID.WATCH_IN_FRONT_IN_THE_RACE:
                {
                    var opponentKey = currentGameState.getOpponentKeyInFront(currentGameState.carClass);
                    if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out OpponentData opponent))
                    {
                        watchOpponent(opponentKey, opponent);
                    }

                    break;
                }
                case SpeechCommands.ID.WATCH_BEHIND_IN_THE_RACE:
                {
                    var opponentKey = currentGameState.getOpponentKeyBehind(currentGameState.carClass);
                    if (opponentKey != null && currentGameState.OpponentData.TryGetValue(opponentKey, out OpponentData opponent))
                    {
                        watchOpponent(opponentKey, opponent);
                    }

                    break;
                }
            }
        }

        private void watchOpponent(String opponentKey, OpponentData opponent)
        {
            if (AudioPlayer.canReadName(opponent.DriverRawName))
            {
                watchedOpponentKeys.Add(opponentKey);
                audioPlayer.playMessageImmediately(new QueuedMessage("add watch acknowledge with driver name", 0,
                    messageFragments: MessageContents(folderAcknowledgeWeWillWatch, opponent)));
            }
            else if (opponent.CarNumber != "-1")
            {
                watchedOpponentKeys.Add(opponentKey);
                audioPlayer.playMessageImmediately(new QueuedMessage("add watch acknowledge with driver number", 0,
                    messageFragments: MessageContents(folderAcknowledgeWatchOpponentWithCarNumber, new CarNumber(opponent.CarNumber))));
            }
            else
            {
                Console.WriteLine("Can't watch driver " + opponent.DriverRawName + " because we have no name or car number for him");
                audioPlayer.playMessageImmediately(new QueuedMessage(folderAcknowledgeNoWayToReferToDriver, 0));
            }
        }
    }

}
