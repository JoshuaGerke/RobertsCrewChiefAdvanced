using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StartLights
    {
        /// <summary>
        /// Number of lights showing
        /// </summary>
        public byte numLights;
    }
}