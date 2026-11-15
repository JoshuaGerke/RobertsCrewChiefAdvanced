using System;
using System.Collections.Generic;
using CrewChiefV4.AiTeam;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;

namespace CrewChiefV4.Events
{
    /// <summary>
    /// Verbindet das KI-Team (Ben, Mik, Sara) mit dem Crew-Chief-Eventsystem. Liest nur, schreibt nichts
    /// in den GameState und übergibt die Zustandskanten synchron an <see cref="AiTeamService"/>.
    /// </summary>
    public class AiTeamEvent : AbstractEvent
    {
        public AiTeamEvent(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
            AiTeamService.Instance.Attach(audioPlayer);
        }

        private static readonly List<SessionType> SupportedTypes = new List<SessionType>
        {
            SessionType.Practice, SessionType.Qualify, SessionType.PrivateQualify, SessionType.Race, SessionType.HotLap, SessionType.LonePractice
        };

        private static readonly List<SessionPhase> SupportedPhases = new List<SessionPhase>
        {
            SessionPhase.Garage, SessionPhase.Gridwalk, SessionPhase.Formation, SessionPhase.Countdown,
            SessionPhase.Green, SessionPhase.FullCourseYellow, SessionPhase.Checkered, SessionPhase.Finished
        };

        public override List<SessionType> applicableSessionTypes => SupportedTypes;
        public override List<SessionPhase> applicableSessionPhases => SupportedPhases;

        public override void clearState()
        {
            AiTeamService.Instance.OnSessionCleared();
        }

        protected override void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            try
            {
                AiTeamService.Instance.OnTick(previousGameState, currentGameState);
            }
            catch (Exception e)
            {
                // Das KI-Team darf den Crew-Chief-Kern nie stören
                Log.Exception(e, "KI-Team: ");
            }
        }
    }
}
