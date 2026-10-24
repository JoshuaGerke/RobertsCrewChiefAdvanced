using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.GameState
{
    class DummyGameDataReader : GameDataReader
    {
        public override void Dispose()
        {
            //
        }

        public override void DumpRawGameData()
        {
            //
        }

        public override object ReadGameData(bool forSpotter)
        {
            return null;
        }

        public override TracedGameData ReadGameDataFromFile(string filename, int pauseBeforeStart)
        {
            return null;
        }

        public override void ResetGameDataFromFile()
        {
            //
        }

        protected override bool InitialiseInternal()
        {
            return true;
        }
    }
}
