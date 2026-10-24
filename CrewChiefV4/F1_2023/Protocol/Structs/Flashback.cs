using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Flashback
    {
        /// <summary>
        /// Frame identifier flashed back to
        /// </summary>
        public uint flashbackFrameIdentifier;

        /// <summary>
        /// Session time flashed back to
        /// </summary>
        public float flashbackSessionTime;
    }
}