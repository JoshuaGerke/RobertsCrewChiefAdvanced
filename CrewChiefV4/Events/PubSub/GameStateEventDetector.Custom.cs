using CrewChiefV4.GameState;

namespace CrewChiefV4.Events.PubSub
{
    public partial class GameStateEventDetector
    {
        // The boilerplate for this is provided by the generator, implement the
        // detection logic to replace the #error.
        private void DetectAiTookControl(GameStateData pgs, GameStateData cgs)
        {
            GameEventBool.AiTookControl = pgs.ControlData.ControlType != ControlType.AI &&
                                          cgs.ControlData.ControlType == ControlType.AI;
            if (GameEventBool.AiTookControl)
            {
                EventBus.Publish(GameEvent.AiTookControl, cgs);
            }
        }

        private void DetectPlayerTookControl(GameStateData pgs, GameStateData cgs)
        {
            GameEventBool.PlayerTookControl = pgs.ControlData.ControlType != ControlType.Player &&
                                              cgs.ControlData.ControlType == ControlType.Player;
            if (GameEventBool.PlayerTookControl)
            {
                EventBus.Publish(GameEvent.PlayerTookControl, cgs);
            }
        }

        private void DetectSector1Completed(GameStateData pgs, GameStateData cgs)
        {
            GameEventBool.Sector1Completed = pgs.SessionData.SectorNumber != cgs.SessionData.SectorNumber
                                             && pgs.SessionData.SectorNumber == 1;
            if (GameEventBool.Sector1Completed)
            {
                EventBus.Publish(GameEvent.Sector1Completed, cgs);
            }
        }

        private void DetectSector2Completed(GameStateData pgs, GameStateData cgs)
        {
            GameEventBool.Sector2Completed = pgs.SessionData.SectorNumber != cgs.SessionData.SectorNumber
                                             && pgs.SessionData.SectorNumber == 2;
            if (GameEventBool.Sector2Completed)
            {
                EventBus.Publish(GameEvent.Sector2Completed, cgs);
            }
        }

        private void DetectSector3Completed(GameStateData pgs, GameStateData cgs)
        {
            GameEventBool.Sector3Completed = pgs.SessionData.SectorNumber != cgs.SessionData.SectorNumber
                                             && pgs.SessionData.SectorNumber == 3;
            if (GameEventBool.Sector3Completed)
            {
                EventBus.Publish(GameEvent.Sector3Completed, cgs);
            }
        }

        private void DetectSessionFinished(GameStateData pgs, GameStateData cgs)
        {
            GameEventBool.SessionFinished = cgs.SessionData.SessionPhase == SessionPhase.Finished
                                            && pgs != null;
            if (GameEventBool.SessionFinished)
            {
                EventBus.Publish(GameEvent.SessionFinished, cgs);
            }
        }

        private void DetectPlayerIsAtPitExit(GameStateData pgs, GameStateData cgs)
        {
            GameEventBool.PlayerIsAtPitExit = cgs.PitData.IsAtPitExit && cgs.PositionAndMotionData.CarSpeed > 5;
            if (GameEventBool.PlayerIsAtPitExit)
            {
                EventBus.Publish(GameEvent.PlayerIsAtPitExit, cgs);
            }
        }

        // <NEW CUSTOM DETECTORS - generator inserts above this line>
    }
}
