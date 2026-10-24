using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Buttons
    {
        /// <summary>
        /// Bit flags specifying which buttons are being pressed currently - see appendices
        /// </summary>
        public uint buttonStatus;
    }
}