using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.GameState
{
    class DummyGameStateMapper : GameStateMapper
    {
        public override GameStateData mapToGameStateData(object memoryMappedFileStruct, GameStateData previousGameState)
        {
            return null;
        }

        public override void versionCheck(object memoryMappedFileStruct)
        {
            //
        }
    }
}
