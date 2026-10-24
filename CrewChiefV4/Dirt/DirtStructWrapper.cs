using System;

namespace CrewChiefV4.Dirt
{
    public class DirtStructWrapper
    {
        public long ticksWhenRead = 0;

        public DirtData dirtData = new DirtData();
        public DirtStructWrapper CreateCopy(long ticksWhenCopied, Boolean forSpotter)
        {
            DirtStructWrapper copy = new DirtStructWrapper();
            copy.ticksWhenRead = ticksWhenCopied;
            copy.dirtData.time = this.dirtData.time;
            copy.dirtData.currentStageTime = this.dirtData.currentStageTime;
            copy.dirtData.lapDistance = this.dirtData.lapDistance;
            copy.dirtData.speed = this.dirtData.speed;
            copy.dirtData.stageLength = this.dirtData.stageLength;
            copy.dirtData.trackNumber = this.dirtData.trackNumber;
            copy.dirtData.worldX = this.dirtData.worldX;
            copy.dirtData.worldY = this.dirtData.worldY;
            copy.dirtData.worldZ = this.dirtData.worldZ;

            return copy;
        }
    }
}