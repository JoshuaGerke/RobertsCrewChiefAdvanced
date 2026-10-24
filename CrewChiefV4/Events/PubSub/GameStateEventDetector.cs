using CrewChiefV4.Audio;
using CrewChiefV4.GameState;

namespace CrewChiefV4.Events.PubSub
{
    public partial class GameStateEventDetector : AbstractEvent
    {
        public GameStateEventDetector(AudioPlayer audioPlayer)
        {
        }

        public override void clearState()
        {
            ResetEventBools();
        }

        protected override void triggerInternal(
            GameStateData previousGameState,
            GameStateData currentGameState)
        {
            // Prevent stale flags when there is no valid game state.
            if (previousGameState == null || currentGameState == null)
            {
                ResetEventBools();
                return;
            }

            DetectEvents(previousGameState, currentGameState);
            if (MainMenuStrip.ProblemsMenu.gameEventDebugForm != null && !MainMenuStrip.ProblemsMenu.gameEventDebugForm.IsDisposed)
            {
                MainMenuStrip.ProblemsMenu.gameEventDebugForm.UpdateGameState(currentGameState);
            }
        }
    }
}
