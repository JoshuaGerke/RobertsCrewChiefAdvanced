namespace CrewChiefV4.rFactor2
{
    // Thin compatibility wrapper that constructs the merged reader in LMU mode.
    public class LMUSharedMemoryReader : RF2_LMUSharedMemoryReader
    {
        public LMUSharedMemoryReader() : base(SharedMemoryMode.LMU) { }
    }
}