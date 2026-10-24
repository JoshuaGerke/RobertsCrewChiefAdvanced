using CrewChiefV4.GameState;

namespace CrewChiefV4.F1_2022
{
    /// <summary>
    /// Maps memory mapped file to a local game-agnostic representation.
    /// </summary>
    class F12022GameStateMapper : GameStateMapper
    {
        public override void versionCheck(object memoryMappedFileStruct)
        {
            // no version data in the stream so this is a no-op
        }

        public override GameStateData mapToGameStateData(object structWrapper, GameStateData previousGameState)
        {
            F12022StructWrapper wrapper = structWrapper as F12022StructWrapper;
            long ticks = wrapper.ticksWhenRead;
            return new GameStateData(ticks);
        }
    }
}
