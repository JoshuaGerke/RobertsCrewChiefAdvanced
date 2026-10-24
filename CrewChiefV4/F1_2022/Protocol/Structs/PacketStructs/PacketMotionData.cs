using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// Definition of the motion packet. It provides physics data for all the cars being driven.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketMotionData
    {
        /// <summary>
        /// Packet header
        /// </summary>
        public PacketHeader m_header;

        /// <summary>
        /// Data for all cars on track
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 22)]
        public CarMotionData[] m_carMotionData;

        /** 
         * Extra player car ONLY data 
         * Note: All wheel arrays have the following order: RL, RR, FL, FR
         */

        /// <summary>
        /// Suspension position of each wheel
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_suspensionPosition;

        /// <summary>
        /// Suspension velocity of each wheel
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_suspensionVelocity;

        /// <summary>
        /// Suspension acceleration of each wheel
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
        float[] m_wheelSlip;

        /// <summary>
        /// Velocity in local space
        /// </summary>
        float m_localVelocityX;

        /// <summary>
        /// Velocity in local space
        /// </summary>
        float m_localVelocityY;

        /// <summary>
        /// Velocity in local space
        /// </summary>
        float m_localVelocityZ;

        /// <summary>
        /// Angular velocity x-component
        /// </summary>
        float m_angularVelocityX;

        /// <summary>
        /// Angular velocity y-component
        /// </summary>
        float m_angularVelocityY;

        /// <summary>
        /// Angular velocity z-component
        /// </summary>
        float m_angularVelocityZ;

        /// <summary>
        /// Angular velocity x-component
        /// </summary>
        float m_angularAccelerationX;

        /// <summary>
        /// Angular velocity y-component
        /// </summary>
        float m_angularAccelerationY;

        /// <summary>
        /// Angular velocity z-component
        /// </summary>
        float m_angularAccelerationZ;

        /// <summary>
        /// Current front wheels angle in radians
        /// </summary>
        float m_frontWheelsAngle;

    }
}
