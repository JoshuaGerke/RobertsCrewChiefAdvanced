using System.Runtime.InteropServices;

namespace F12022UdpNet
{
    /// <summary>
    /// Car damage information
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CarDamageData
    {
        /// <summary>
        /// Tyre wear (percentage)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        float[] m_tyresWear;

        /// <summary>
        /// Tyre damage (percentage)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        byte[] m_tyresDamage;

        /// <summary>
        /// Brakes damage (percentage)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        byte[] m_brakesDamage;

        /// <summary>
        /// Front left wing damage (percentage)
        /// </summary>
        byte m_frontLeftWingDamage;

        /// <summary>
        /// Front right wing damage (percentage)
        /// </summary>
        byte m_frontRightWingDamage;

        /// <summary>
        /// Rear wing damage (percentage)
        /// </summary>
        byte m_rearWingDamage;

        /// <summary>
        /// Floor damage (percentage)
        /// </summary>
        byte m_floorDamage;

        /// <summary>
        /// Diffuser damage (percentage)
        /// </summary>
        byte m_diffuserDamage;

        /// <summary>
        /// Sidepod damage (percentage)
        /// </summary>
        byte m_sidepodDamage;

        /// <summary>
        /// Indicator for DRS fault, 0 = OK, 1 = fault
        /// </summary>
        byte m_drsFault;

        /// <summary>
        /// Indicator for ERS fault, 0 = OK, 1 = fault
        /// </summary>
        byte m_ersFault;

        /// <summary>
        /// Gear box damage (percentage)
        /// </summary>
        byte m_gearBoxDamage;

        /// <summary>
        /// Engine damage (percentage)
        /// </summary>
        byte m_engineDamage;

        /// <summary>
        /// Engine wear MGU-H (percentage)
        /// </summary>
        byte m_engineMGUHWear;

        /// <summary>
        /// Engine wear ES (percentage)
        /// </summary>
        byte m_engineESWear;

        /// <summary>
        /// Engine wear CE (percentage)
        /// </summary>
        byte m_engineCEWear;

        /// <summary>
        /// Engine wear ICE (percentage)
        /// </summary>
        byte m_engineICEWear;

        /// <summary>
        /// Engine wear MGU-K (percentage)
        /// </summary>
        byte m_engineMGUKWear;

        /// <summary>
        /// Engine wear TC (percentage)
        /// </summary>
        byte m_engineTCWear;

        /// <summary>
        /// Engine blown, 0 = OK, 1 = fault
        /// </summary>
        byte m_engineBlown;

        /// <summary>
        /// Engine seized, 0 = OK, 1 = fault
        /// </summary>
        byte m_engineSeized;
    }
}