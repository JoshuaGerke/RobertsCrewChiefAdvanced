using CrewChiefV4.GameState;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrewChiefV4.Audio;
using CrewChiefV4.R3E;

namespace CrewChiefV4.Events
{
    class SessionEndMessages
    {
        public const String sessionEndMessageIdentifier = "SESSION_END";

        public static String overrideDetailedDisqualifiedMessage = null;

        public static String folderPodiumFinish = "lap_counter/podium_finish";

        private const String folderWonRace = "lap_counter/won_race";

        private const String folderFinishedRace = "lap_counter/finished_race";

        private const String folderGoodFinish = "lap_counter/finished_race_good_finish";

        private const String folderFinishedRaceLast = "lap_counter/finished_race_last";

        public const String folderEndOfSession = "lap_counter/end_of_session";

        private const String folderEndOfSessionPole = "lap_counter/end_of_session_pole";

        public readonly Boolean enableSessionEndMessages = UserSettings.GetUserSettings().getBoolean("enable_session_end_messages");

        private AudioPlayer audioPlayer;

        private const int minSessionRunTimeForEndMessages = 60;

        private DateTime lastSessionEndMessagesPlayedAt = DateTime.MinValue;

        public SessionEndMessages(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
        }

        public void trigger(float sessionRunningTime, SessionType sessionType, SessionPhase lastSessionPhase, int startPosition,
            int finishPosition, int numCars, int completedLaps, Tuple<int, int> expectedFinishPosition, Boolean isDisqualified, Boolean isDNF, DateTime now)
        {
            if (!enableSessionEndMessages)
            {
                Console.WriteLine("Session end, position = " + finishPosition + ", session end messages are disabled");
                return;
            }
            if (lastSessionEndMessagesPlayedAt.AddSeconds(10) > now)
            {
                Console.WriteLine("Skipping duplicate session end message call - last call was " + (now - lastSessionEndMessagesPlayedAt).TotalSeconds.ToString("0.00") + " seconds ago");
                return;
            }
            switch (sessionType)
            {
                case SessionType.Race:
                {
                    if (sessionRunningTime >= minSessionRunTimeForEndMessages || completedLaps > 0)
                    {
                        if (lastSessionPhase == SessionPhase.Finished)
                        {
                            // only play session end message for races if we've actually finished, not restarted
                            lastSessionEndMessagesPlayedAt = now;
                            playFinishMessage(sessionType, startPosition, finishPosition, numCars, isDisqualified, isDNF, completedLaps, expectedFinishPosition);
                        }
                        else
                        {
                            Console.WriteLine("Skipping race session end message because the previous phase wasn't Finished");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Skipping race session end message because it didn't run for a lap or " + minSessionRunTimeForEndMessages + " seconds");
                    }

                    break;
                }
                case SessionType.Practice:
                case SessionType.Qualify:
                case SessionType.PrivateQualify:
                {
                    if (sessionRunningTime >= minSessionRunTimeForEndMessages)
                    {
                        if (lastSessionPhase == SessionPhase.Green || lastSessionPhase == SessionPhase.FullCourseYellow || 
                            lastSessionPhase == SessionPhase.Finished || lastSessionPhase == SessionPhase.Checkered)
                        {
                            lastSessionEndMessagesPlayedAt = now;
                            playFinishMessage(sessionType, startPosition, finishPosition, numCars, isDisqualified, isDNF, completedLaps, expectedFinishPosition);
                        }
                        else
                        {
                            Console.WriteLine("Skipping non-race session end message because the previous phase wasn't green, finished, or checkered");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Skipping non-race session end message because the session didn't run for " + minSessionRunTimeForEndMessages + " seconds");
                    }

                    break;
                }
            }
        }

        private void playFinishMessage(SessionType sessionType, int startPosition, int position, int numCars, Boolean isDisqualified, Boolean isDNF, int completedLaps, 
            Tuple<int, int> expectedFinishingPosition)
        {
            audioPlayer.suspendPearlsOfWisdom();
            switch (GlobalBehaviourSettings.racingType)
            {
                case CrewChief.RacingType.Circuit:
                {
                    if (position < 1)
                    {
                        Console.WriteLine("Session finished but position is < 1");
                    }
                    else if (isDisqualified)
                    {
                        Boolean playedRant = false;
                        if (completedLaps > 1)
                        {
                            playedRant = audioPlayer.playRant(sessionEndMessageIdentifier, AbstractEvent.MessageContents(Penalties.folderDisqualified));
                        }
                        if (!playedRant)
                        {
                            var disqualifiedMessage = overrideDetailedDisqualifiedMessage == null ? Penalties.folderDisqualified : overrideDetailedDisqualifiedMessage;
                            overrideDetailedDisqualifiedMessage = null;
                            audioPlayer.playMessage(new QueuedMessage(sessionEndMessageIdentifier, 0,
                                messageFragments: AbstractEvent.MessageContents(disqualifiedMessage), priority: 10));
                        }
                    }
                    else if (sessionType == SessionType.Race)
                    {
                        Boolean isLast = position == numCars;
                        if (isDNF)
                        {
                            audioPlayer.playMessage(new QueuedMessage(sessionEndMessageIdentifier, 0,
                                messageFragments: AbstractEvent.MessageContents(folderFinishedRaceLast), priority: 10));
                        }
                        else if (position == 1)
                        {
                            audioPlayer.playMessage(new QueuedMessage(sessionEndMessageIdentifier, 0,
                                messageFragments: AbstractEvent.MessageContents(folderWonRace), priority: 10));
                        }
                        else if (position < 4)
                        {
                            audioPlayer.playMessage(new QueuedMessage(sessionEndMessageIdentifier, 0,
                                messageFragments: AbstractEvent.MessageContents(folderPodiumFinish), priority: 10));
                        }
                        else if (position >= 4 && !isLast)
                        {
                            // check if this a significant improvement over the start position or, if we have it, if it's equal or better than our expected position
                            bool metExpectations = GlobalBehaviourSettings.maxComplaintsPerSession <= 0 /* if we've disabled complaints, we've always met expectations */
                                                   || (expectedFinishingPosition.Item1 != -1 && expectedFinishingPosition.Item1 >= position);
                            if (metExpectations ||
                                (startPosition > position &&
                                 ((startPosition <= 6 && position <= 5) ||
                                  (startPosition <= 10 && position <= 6) ||
                                  (startPosition - position >= 4))))
                            {
                                audioPlayer.playMessage(new QueuedMessage(sessionEndMessageIdentifier, 0,
                                    messageFragments: AbstractEvent.MessageContents(Position.folderStub + position, folderGoodFinish), priority: 10));
                            }
                            else
                            {
                                // if it's a shit finish, maybe launch into a tirade
                                Boolean playedRant = false;
                                int positionsLost = position - startPosition;
                                // check expectations - a 'fail' 
                                bool failedExpectations = expectedFinishingPosition.Item1 != -1 && expectedFinishingPosition.Item1 + 5 < position;
                                // if we've lost 9 or more positions, and this is more than half the field size (or badly missed our expectations) maybe play a rant
                                if (numCars > 2 && completedLaps > 1
                                                && (failedExpectations || (positionsLost > 8 && (float)positionsLost / (float)numCars >= 0.5f)))
                                {
                                    playedRant = audioPlayer.playRant(sessionEndMessageIdentifier, AbstractEvent.MessageContents(Position.folderStub + position));
                                }
                                if (!playedRant)
                                {
                                    audioPlayer.playMessage(new QueuedMessage(sessionEndMessageIdentifier, 0,
                                        messageFragments: AbstractEvent.MessageContents(Position.folderStub + position, folderFinishedRace), priority: 10));
                                }
                            }
                        }
                        else if (isLast)
                        {
                            if (GlobalBehaviourSettings.maxComplaintsPerSession <= 0 /* even if we've disabled complaints, we're still last... */)
                            {
                                audioPlayer.playMessage(new QueuedMessage(sessionEndMessageIdentifier, 0,
                                    messageFragments: AbstractEvent.MessageContents(Position.folderStub + position, folderFinishedRace), priority: 10));
                            }
                            else
                            {
                                Boolean playedRant = false;
                                if (numCars > 5 && completedLaps > 1)
                                {
                                    playedRant = audioPlayer.playRant(sessionEndMessageIdentifier, AbstractEvent.MessageContents(Position.folderStub + position));
                                }
                                if (!playedRant)
                                {
                                    audioPlayer.playMessage(new QueuedMessage(sessionEndMessageIdentifier, 0,
                                        messageFragments: AbstractEvent.MessageContents(folderFinishedRaceLast), priority: 10));
                                }
                            }
                        }
                    }
                    else
                    {
                        if ((sessionType == SessionType.Qualify ||
                             sessionType == SessionType.PrivateQualify) && position == 1)
                        {
                            audioPlayer.playMessage(new QueuedMessage(folderEndOfSessionPole, 0));
                        }
                        else
                        {
                            audioPlayer.playMessage(new QueuedMessage(sessionEndMessageIdentifier, 0,
                                messageFragments: AbstractEvent.MessageContents(folderEndOfSession, Position.folderStub + position), priority: 10));
                        }
                        // this "end of session" message was written this way for RF2, but when support was added for iRacing
                        // it was decided that it is more idiomatic to have the message play (randomly) on the gridwalk.
                        if (!Game.IRACING &&
                            (sessionType == SessionType.Qualify ||
                             sessionType == SessionType.PrivateQualify) &&
                            CrewChief.currentGameState != null)
                        {
                            // report the expected race finish position
                            Position.reportExpectedFinishPosition(audioPlayer, CrewChief.currentGameState.SessionData.expectedFinishingPosition,
                                false, false, CrewChief.currentGameState.SessionData.ClassPosition);
                        }
                    }

                    break;
                }
                case CrewChief.RacingType.Rally:
                {
                    if (!isDNF)
                    {
                        CoDriver coDriver = (CoDriver)CrewChief.getEvent("CoDriver");
                        coDriver.PlayFinishMessage();
                    }

                    break;
                }
            }
        }
    }
}
