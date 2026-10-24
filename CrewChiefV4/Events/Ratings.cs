using System;
using System.Collections.Generic;

using CrewChiefV4.Audio;
using CrewChiefV4.GameState;

namespace CrewChiefV4.Events
{
    /// <summary>
    /// Responses about iRating, license level, incident points and strength of field
    /// </summary>
    public class Ratings : AbstractEvent
    {
        public const String folderYouHave = "incidents/you_have";
        public const String folderincidentPoints = "incidents/incident_points";
        private const String folderincidentPointslimit = "incidents/the_incident_points_limit_is";
        private const String folderUnlimitedPoints = "incidents/no_incident_points_limit";

        private int maxIncidentCount = -1;
        private int incidentsCount = -1;
        private int iRating = -1;
        private int strenghtOfField = -1;
        private Boolean hasLimitedIncidents = false;

        private Tuple<String, float> licenseLevel;


        public Ratings(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
            clearState();
        }

        public override void clearState()
        {
            this.incidentsCount = -1;
            this.maxIncidentCount = -1;
            this.iRating = -1;
            this.hasLimitedIncidents = false;
            this.licenseLevel = new Tuple<string, float>("invalid", -1);
        }

        public override List<SessionPhase> applicableSessionPhases
        {
            get { return new List<SessionPhase> { SessionPhase.Green, SessionPhase.Countdown, SessionPhase.FullCourseYellow }; }
        }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            // allow incident points and SoF for other games so we can ask about them in R3E:
            maxIncidentCount = currentGameState.SessionData.MaxIncidentCount;
            incidentsCount = currentGameState.SessionData.CurrentIncidentCount;
            strenghtOfField = currentGameState.SessionData.StrengthOfField;
            hasLimitedIncidents = currentGameState.SessionData.HasLimitedIncidents;
            licenseLevel = currentGameState.SessionData.LicenseLevel;
            iRating = currentGameState.SessionData.iRating;
	    }
		
/*
            if (hasLimitedIncidents)
            {
                //play < 5 incident left warning.
                if (incidentsCount >= maxIncidentCount - 5 && !playedIncidentsWarning)
                {
                    playedIncidentsWarning = true;
                    audioPlayer.playMessageImmediately(new QueuedMessage("Incidents/limit", MessageContents(folderYouHave, incidentsCount, folderincidentPoints,
                        Pause(200), folderincidentPointslimit, maxIncidentCount), 0));

                }
                else if (incidentsCount >= maxIncidentCount - 1 && !playedLastIncidentsLeftWarning)
                {
                    playedLastIncidentsLeftWarning = true;
                    //play 1 incident left warning.
                }
            }
 */

        /// <summary>
        /// Translate the license letter(s) to the speech folder name
        /// </summary>
        /// <param name="licenseID">"a" / "b" ... "wc"</param>
        /// <returns>the speech folder name</returns>
        public static string GetLicenseFolder(string licenseID)
        {
            var license = new Dictionary<string, string>()
            {
                { "a", "licence/a_licence" },
                { "b", "licence/b_licence" },
                { "c", "licence/c_licence" },
                { "d", "licence/d_licence" },
                { "r", "licence/rookie_licence" },
                { "wc", "licence/pro_licence" },
            };
            license.TryGetValue(licenseID, out string folder);
            return folder;
        }

        /// <summary>
        /// Create a license level message
        /// </summary>
        /// <param name="licenseLevel"></param>
        /// <returns>null if licenseLevel is invalid</returns>
        public static QueuedMessage LicenseLevelMessage(Tuple<String, float> licenseLevel)
        {
            if (licenseLevel.Item2 != -1)
            {
                Tuple<int, int> wholeandfractional = Utilities.WholeAndFractionalPart(licenseLevel.Item2, 2);
                string folder = GetLicenseFolder(licenseLevel.Item1.ToLower());
                if (folder != null)
                {
                    List<MessageFragment> messageFragments = new List<MessageFragment> { MessageFragment.Text(folder) };
                    messageFragments.AddRange(MessageContents(wholeandfractional.Item1,
                        NumberReader.folderPoint,
                        wholeandfractional.Item2));
                    QueuedMessage licenceLevelMessage =
                        new QueuedMessage("License/license", 0, messageFragments: messageFragments);
                    return licenceLevelMessage;
                }
            }
            return null;
        }
        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.HOW_MANY_INCIDENT_POINTS,
            SpeechCommands.ID.WHATS_MY_IRATING,
            SpeechCommands.ID.WHATS_MY_LICENSE_CLASS,
            SpeechCommands.ID.WHATS_THE_INCIDENT_LIMIT,
            SpeechCommands.ID.WHATS_THE_SOF,
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
                case SpeechCommands.ID.HOW_MANY_INCIDENT_POINTS:
                {
                        if (incidentsCount == -1)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                        }
                        else
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage("Incidents/incidents", 0, messageFragments: MessageContents(folderYouHave, incidentsCount, folderincidentPoints)));
                        }

                        return;
                }
                case SpeechCommands.ID.WHATS_THE_INCIDENT_LIMIT:
                {
                    if (hasLimitedIncidents)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("Incidents/limit", 0, messageFragments: MessageContents(folderincidentPointslimit, maxIncidentCount)));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("Incidents/limit", 0, messageFragments: MessageContents(folderUnlimitedPoints)));
                    }

                    return;
                }
                case SpeechCommands.ID.WHATS_MY_IRATING:
                {
                    if (iRating != -1)
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("license/irating", 0, messageFragments: MessageContents(iRating)));
                        return;
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                        return;
                    }
                }
                case SpeechCommands.ID.WHATS_MY_LICENSE_CLASS:
                {
                    {
                        var msg = LicenseLevelMessage(licenseLevel);
                        if (msg != null)
                        {
                            audioPlayer.playDelayedImmediateMessage(msg);
                        }
                        else
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                        }
                    }
                    return;
                }
                case SpeechCommands.ID.WHATS_THE_SOF:
                {
                    // for R3E we need to recalculate this on each request unless we're in a race session. For race sessions we want to use the fixed SoF the mapper generated
                    // at the green light
                    int sofToReport;
                    if (Game.RACE_ROOM && CrewChief.currentGameState != null && CrewChief.currentGameState.SessionData.SessionType != SessionType.Race)
                    {
                        sofToReport = R3E.R3ERatings.getAverageRatingForParticipants(CrewChief.currentGameState.OpponentData);
                    }
                    else
                    {
                        sofToReport = this.strenghtOfField;
                    }

                    if (sofToReport != -1)
                    {
                        if (Game.IRACING && GameStateData.Multiclass && GlobalBehaviourSettings.sofIsPlayerClass)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage("sof", 0, MessageContents(LapCounter.folderStrengthOfFieldOurClass, sofToReport)));
                        }
                        else
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage("sof", 0, MessageContents(LapCounter.folderStrengthOfField, sofToReport)));
                        }
                        return;
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                        return;
                    }
                }
            }
        }
    }
}
