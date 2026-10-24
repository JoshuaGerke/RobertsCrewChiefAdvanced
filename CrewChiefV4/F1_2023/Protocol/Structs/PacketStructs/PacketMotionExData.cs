using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// The motion packet gives extended data for the car being driven with the goal of being able to drive a motion platform setup.
    /// 
    /// <para>Frequency: Rate as specified in menus</para>
    /// <para>Size: 217 bytes</para>
    /// <para>Version: 1</para>
    ///
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketMotionExData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        PacketHeader m_header;

        /** Extra player car ONLY data */

        /// <summary>
        /// RL, RR, FL, FR
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_suspensionPosition;

        /// <summary>
        /// RL, RR, FL, FR
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_suspensionVelocity;

        /// <summary>
        /// RL, RR, FL, FR
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_suspensionAcceleration;

        /// <summary>
        /// Speed of each wheel
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_wheelSpeed;

        /// <summary>
        /// Slip ratio for each wheel
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_wheelSlipRatio;

        /// <summary>
        /// Slip angles for each wheel
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_wheelSlipAngle;

        /// <summary>
        /// Lateral forces for each wheel
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_wheelLatForce;

        /// <summary>
        /// Longitudinal forces for each wheel
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_wheelLongForce;

        /// <summary>
        /// Height of centre of gravity above ground
        /// </summary>
        float m_heightOfCOGAboveGround;

        /// <summary>
        /// Velocity in local space – metres/s
        /// </summary>
        float m_localVelocityX;

        /// <summary>
        /// Velocity in local space – metres/s
        /// </summary>
        float m_localVelocityY;

        /// <summary>
        /// Velocity in local space – metres/s
        /// </summary>
        float m_localVelocityZ;

        /// <summary>
        /// Angular velocity x-component - radians/s
        /// </summary>
        float m_angularVelocityX;

        /// <summary>
        /// Angular velocity y-component - radians/s
        /// </summary>
        float m_angularVelocityY;

        /// <summary>
        /// Angular velocity z-component - radians/s
        /// </summary>
        float m_angularVelocityZ;

        /// <summary>
        /// Angular acceleration x-component – radians/s/s
        /// </summary>
        float m_angularAccelerationX;

        /// <summary>
        /// Angular acceleration y-component – radians/s/s
        /// </summary>
        float m_angularAccelerationY;

        /// <summary>
        /// Angular acceleration z-component – radians/s/s
        /// </summary>
        float m_angularAccelerationZ;

        /// <summary>
        /// Current front wheels angle in radians
        /// </summary>
        float m_frontWheelsAngle;

        /// <summary>
        /// Vertical forces for each wheel
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_wheelVertForce;
    }
}