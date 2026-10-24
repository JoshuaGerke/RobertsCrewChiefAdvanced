using ksBroadcastingNetwork;
using ksBroadcastingNetwork.Structs;
using System.Linq;

namespace ksBroadcastingTestClient.Broadcasting
{
    public class LapTime
    {
        public int? LaptimeMS { get; private set; }
        public int? Split1MS { get; private set; }
        public int? Split2MS { get; private set; }
        public int? Split3MS { get; private set; }

        public LapType Type { get; private set; }
        public bool IsValid { get; private set; }

        internal void Update(LapInfo lapUpdate)
        {
            var isChanged = LaptimeMS != lapUpdate.LaptimeMS;

            if (isChanged)
            {
                Type = lapUpdate.Type;
                IsValid = lapUpdate.IsValidForBest;

                LaptimeMS = IsValid ? lapUpdate.LaptimeMS : null;
                Split1MS = IsValid ? lapUpdate.Splits.FirstOrDefault() : null;
                Split2MS = IsValid ? lapUpdate.Splits.Skip(1).FirstOrDefault() : null;
                Split3MS = IsValid ? lapUpdate.Splits.Skip(2).FirstOrDefault() : null;
            }
        }
    }
}
