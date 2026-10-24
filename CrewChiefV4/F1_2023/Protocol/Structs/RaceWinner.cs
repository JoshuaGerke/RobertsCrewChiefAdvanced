using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// Vehicle index of the race winner
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RaceWinner
    {
        public byte vehicleIdx;
    }
}
