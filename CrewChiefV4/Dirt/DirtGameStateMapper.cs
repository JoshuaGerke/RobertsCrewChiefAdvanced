using System;
using CrewChiefV4.GameState;

/**
 * Maps memory mapped file to a local game-agnostic representation.
 * */
namespace CrewChiefV4.Dirt
{
    class DirtGameStateMapper : GameStateMapper
    {
        public override void versionCheck(Object memoryMappedFileStruct)
        {
            // no version data in the stream so this is a no-op
        }

        public override GameStateData mapToGameStateData(Object structWrapper, GameStateData previousGameState)
        {
            DirtStructWrapper wrapper = (DirtStructWrapper)structWrapper;
            long ticks = wrapper.ticksWhenRead;
            GameStateData gsd = new GameStateData(ticks);
            gsd.SessionData.SessionRunningTime = wrapper.dirtData.currentStageTime;
            gsd.SessionData.SessionPhase = gsd.SessionData.SessionRunningTime <= 0 ? SessionPhase.Countdown : SessionPhase.Green;
            bool createNewTrackDefinition = false;
            if (previousGameState != null)
            {
                // if we appear to be starting a new session, we'll want to generate our track name:
                if (wrapper.dirtData.stageLength > 0
                    && previousGameState.SessionData.SessionPhase == SessionPhase.Countdown
                    && gsd.SessionData.SessionPhase == SessionPhase.Green)
                {
                    createNewTrackDefinition = true;
                }
                else
                {
                    // otherwise keep the last generated track name and notes
                    gsd.CoDriverPacenotes = previousGameState.CoDriverPacenotes;
                    gsd.SessionData.TrackDefinition = previousGameState.SessionData.TrackDefinition;
                }
            }
            if (createNewTrackDefinition || gsd.SessionData.TrackDefinition == null)
            {
                float stageLengthAtFirstDistanceUpdate = wrapper.dirtData.stageLength;
                string trackName = "stage_length_" + stageLengthAtFirstDistanceUpdate;
                if (UserSettings.GetUserSettings().getBoolean("include_start_pos_in_dirt_stage_names"))
                {
                    // track name includes the start point x and z co-ordinates (rounded to the nearest hundred metres in case
                    // the start position varies a little). This allows tracks with the same distance but different start points (e.g.
                    // reversed stages) to be differentiated
                    int worldXAtFirstDistanceUpdate = roundToNearest100(wrapper.dirtData.worldX);
                    int worldZAtFirstDistanceUpdate = roundToNearest100(wrapper.dirtData.worldZ);
                    trackName += "^x" + worldXAtFirstDistanceUpdate + "z" + worldZAtFirstDistanceUpdate;
                }
                if (stageLengthAtFirstDistanceUpdate > 0)
                {
                    Console.WriteLine("Stage details: length=" + stageLengthAtFirstDistanceUpdate +
                        ", starting position X=" + wrapper.dirtData.worldX + ", Y=" + wrapper.dirtData.worldY + ", Z=" + wrapper.dirtData.worldZ);
                    Console.WriteLine("Using folder name \"" + trackName + "\" for pace notes");
                }
                gsd.SessionData.TrackDefinition = TrackData.getTrackDefinition(trackName, wrapper.dirtData.stageLength);
            }
            gsd.SessionData.SessionType = SessionType.Race;
            gsd.PositionAndMotionData.DistanceRoundTrack = wrapper.dirtData.lapDistance;
            gsd.PositionAndMotionData.CarSpeed = wrapper.dirtData.speed;

            return gsd;
        }

        private PitWindow mapToPitWindow(GameStateData currentGameState, uint pitSchedule, uint pitMode)
        {
            return PitWindow.Unavailable;
        }

        private int roundToNearest100(float f)
        {
            return 100 * ((int) Math.Round(f / 100f));
        }
    }
}
