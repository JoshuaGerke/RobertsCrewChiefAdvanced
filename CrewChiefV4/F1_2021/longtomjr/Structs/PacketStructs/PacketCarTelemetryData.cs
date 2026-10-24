// <copyright file="PacketCarTelemetryData.cs" company="Racing Sim Tools">
// Original work Copyright (c) Codemasters. All rights reserved.
//
// Modified work Copyright (c) Racing Sim Tools.
//
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.
// </copyright>

namespace F12021UdpNet
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// This packet details telemetry for all the cars in the race.
    /// It details various values that would be recorded on the car such as speed, 
    /// throttle application, DRS etc.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketCarTelemetryData
    {
        /// <summary>
        /// Header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Telemetry data for every car
        /// </summary>
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 22)]
        public CarTelemetryData[] m_carTelemetryData;

        /// <summary>
        /// Bit flags specifying which buttons are being
        /// pressed currently - see appendices
        /// </summary>
        public uint m_buttonStatus;

        // Added in Beta 3:
        public byte m_mfdPanelIndex;       // Index of MFD panel open - 255 = MFD closed
                                            // Single player, race – 0 = Car setup, 1 = Pits
                                            // 2 = Damage, 3 =  Engine, 4 = Temperatures
                                            // May vary depending on game mode
        public byte m_mfdPanelIndexSecondaryPlayer;   // See above
        public sbyte m_suggestedGear;       // Suggested gear for the player (1-8)
                                    // 0 if no gear suggested

    }
}
