using F12023UdpNet;
using System.Numerics;

namespace CrewChiefV4.F1_2023.Utility
{
    struct PlayerSpatialData
    {
        public int DriverIndex { get; set; }
        public Vector2 Position { get; set; }
        public Vector3 Velocity { get; set; }
        public float Yaw { get; set; }

        public static PlayerSpatialData FromGameData(F12023StructWrapper data)
        {
            var index = data.packetMotionData.m_header.m_playerCarIndex;
            var playerData = data.packetMotionData.m_carMotionData[index];

            return new PlayerSpatialData
            {
                DriverIndex = index,
                Position = new Vector2(playerData.m_worldPositionX, playerData.m_worldPositionZ),
                Velocity = new Vector3(
                    data.packetCarTelemetryData.m_carTelemetryData[data.packetCarTelemetryData.m_header.m_playerCarIndex].m_speed * 0.277778f,
                    playerData.m_worldVelocityX,
                    playerData.m_worldVelocityZ
                ),
                Yaw = playerData.m_yaw
            };
        }

        /// <summary>
        /// Get position information as float[]
        /// </summary>
        public float[] RawPosition { get => Position.toFloatArray(); }

        /// <summary>
        /// Get velocity information as float[]
        /// </summary>
        public float[] RawVelocity { get => Velocity.toFloatArray(); }
    }

    struct OpponentSpatialData
    {
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }

        public static OpponentSpatialData FromMotionData(CarMotionData data)
        {
            return new OpponentSpatialData
            {
                Position = new Vector2(data.m_worldPositionX, data.m_worldPositionZ),
                Velocity = new Vector2(data.m_worldVelocityX, data.m_worldVelocityZ)
            };
        }

        /// <summary>
        /// Get position information as float[]
        /// </summary>
        public float[] RawPosition { get => Position.toFloatArray(); }

        /// <summary>
        /// Get velocity information as float[]
        /// </summary>
        public float[] RawVelocity { get => Velocity.toFloatArray(); }
    }
}