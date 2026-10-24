using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    /// <summary>
    /// Car status of a car in the race.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct CarStatusData
    {
        /// <summary>
        /// 0 (off) - 1 (medium) - 2 (full)
        /// </summary>
        public TractionControlStatus m_tractionControl;

        /// <summary>
        /// 0 (off) - 1 (on)
        /// </summary>
        public byte m_antiLockBrakes;

        /// <summary>
        /// Fuel mix - 0 = lean, 1 = standard, 2 = rich, 3 = max
        /// </summary>
        public FuelMix m_fuelMix;

        /// <summary>
        /// Front brake bias (percentage)
        /// </summary>
        public byte m_frontBrakeBias;

        /// <summary>
        /// Pit limiter status - 0 = off, 1 = on
        /// </summary>
        public byte m_pitLimiterStatus;

        /// <summary>
        /// Current fuel mass
        /// </summary>
        public float m_fuelInTank;

        /// <summary>
        /// Fuel capacity
        /// </summary>
        public float m_fuelCapacity;

        /// <summary>
        /// Fuel remaining in terms of laps (value on MFD)
        /// </summary>
        public float m_fuelRemainingLaps;

        /// <summary>
        /// Cars max RPM, point of rev limiter
        /// </summary>
        public ushort m_maxRPM;

        /// <summary>
        /// Cars idle RPM
        /// </summary>
        public ushort m_idleRPM;

        /// <summary>
        /// Maximum number of gears
        /// </summary>
        public byte m_maxGears;

        /// <summary>
        /// 0 = not allowed, 1 = allowed
        /// </summary>
        public DrsAllowed m_drsAllowed;

        /// <summary>
        /// 0 = DRS not available, non-zero - DRS will be available in [X] metres
        /// </summary>
        public ushort m_drsActivationDistance;

        /// <summary>
        /// F1 Modern - 16 = C5, 17 = C4, 18 = C3, 19 = C2, 20 = C1
        /// 21 = C0, 7 = inter, 8 = wet
        /// F1 Classic - 9 = dry, 10 = wet
        /// F2 – 11 = super soft, 12 = soft, 13 = medium,
        /// </summary>
        public TyreCompound m_actualTyreCompound;

        /// <summary>
        /// F1 visual (can be different from actual compound)
        /// 16 = soft, 17 = medium, 18 = hard, 7 = inter, 8 = wet
        /// F1 Classic – same as above
        /// F2 ‘19, 15 = wet, 19 – super soft, 20 = soft
        /// 21 = medium , 22 = hard
        /// </summary>
        public VisualTyreCompound m_tyreVisualCompound;

        /// <summary>
        /// Age in laps of the current set of tyres
        /// </summary>
        public byte m_tyresAgeLaps;

        /// <summary>
        /// -1 = invalid/unknown, 0 = none, 1 = green
        /// 2 = blue, 3 = yellow
        /// </summary>
        public VehicleFiaFlag m_vehicleFiaFlags;

        /// <summary>
        /// ERS energy store in Joules
        /// </summary>
        public float m_ersStoreEnergy;

        /// <summary>
        /// Engine power output of ICE (W)
        /// </summary>
        public float m_enginePowerICE;

        /// <summary>
        /// Engine power output of MGU-K (W)
        /// </summary>
        public float m_enginePowerMGUK;

        /// <summary>
        /// ERS deployment mode, 0 = none, 1 = medium
   		/// 2 = hotlap, 3 = overtake
        /// </summary>
        public ErsDeployMode m_ersDeployMode;

        /// <summary>
        /// ERS energy harvested this lap by MGU-K
        /// </summary>
        public float m_ersHarvestedThisLapMGUK;

        /// <summary>
        /// ERS energy harvested this lap by MGU-H
        /// </summary>
        public float m_ersHarvestedThisLapMGUH;

        /// <summary>
        /// ERS energy deployed this lap
        /// </summary>
        public float m_ersDeployedThisLap;

        /// <summary>
        /// Whether the car is paused in a network game
        /// </summary>
        public byte m_networkPaused;
    }
}
