// <copyright file="CarTelemetryData.cs" company="Racing Sim Tools">
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
    /// Telemetry of a car.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CarTelemetryData
    {
        public ushort m_speed;                         // Speed of car in kilometres per hour
        public float m_throttle;                      // Amount of throttle applied (0.0 to 1.0)
        public float m_steer;                         // Steering (-1.0 (full lock left) to 1.0 (full lock right))
        public float m_brake;                         // Amount of brake applied (0.0 to 1.0)
        public byte m_clutch;                        // Amount of clutch applied (0 to 100)
        public sbyte m_gear;                          // Gear selected (1-8, N=0, R=-1)
        public ushort m_engineRPM;                     // Engine RPM
        public byte m_drs;                           // 0 = off, 1 = on
        public byte m_revLightsPercent;              // Rev lights indicator (percentage)

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
        public ushort[] m_brakesTemperature;          // Brakes temperature (celsius)
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] m_tyresSurfaceTemperature;    // Tyres surface temperature (celsius)
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] m_tyresInnerTemperature;      // Tyres inner temperature (celsius)

        public ushort m_engineTemperature;             // Engine temperature (celsius)

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
        public float[] m_tyresPressure;              // Tyres pressure (PSI)

        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] m_surfaceType;                // Driving surface, see appendices
    }
}
