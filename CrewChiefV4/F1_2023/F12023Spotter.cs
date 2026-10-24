using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.Events;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using CrewChiefV4.F1_2023.Utility;

namespace CrewChiefV4.F1_2023
{
    /// <summary>
    /// Spotter implementation for F1 2023
    /// </summary>
    class F12023Spotter : Spotter
    {
        private const float twoPi = (float)(2 * Math.PI);

        public F12023Spotter(AudioPlayer audioPlayer, bool initialEnabledState)
        {
            this.audioPlayer = audioPlayer;
            this.enabled = initialEnabledState;
            this.initialEnabledState = initialEnabledState;
            this.internalSpotter = new NoisyCartesianCoordinateSpotter(audioPlayer, true, UserSettings.GetUserSettings().getFloat("f1_2023_spotter_car_length"), 2);
        }

        public override void clearState() => internalSpotter.clearState();

        public override void trigger(object lastStateObj, object currentStateObj, GameStateData currentGameState)
        {
            F12023StructWrapper currentWrapper = currentStateObj as F12023StructWrapper;

            // If data is not valid do nothing
            var hasLapData = currentWrapper.packetLapData.m_lapData != null;
            var hasMotionData = currentWrapper.packetMotionData.m_carMotionData != null;
            var hasTelemetryData = currentWrapper.packetCarTelemetryData.m_carTelemetryData != null;
            var isDataValid = hasLapData && hasMotionData && hasTelemetryData;
            if (!isDataValid) return;

            // Spotter should only work when in an actual session and with available motion data
            var isInPits = currentWrapper.packetLapData.m_lapData[currentWrapper.packetLapData.m_header.m_playerCarIndex].m_pitStatus != 0;
            var isSpectating = currentWrapper.packetSessionData.m_isSpectating != 0;
            var isCarMotionDataAvailable = currentWrapper.packetMotionData.m_carMotionData.Length > 1;
            var shouldSpot = this.enabled && !isInPits && !isSpectating;
            if (!shouldSpot || !isCarMotionDataAvailable) return;

            MakeSpotterDoItsJob(currentWrapper);
        }

        private void MakeSpotterDoItsJob(F12023StructWrapper data)
        {
            // Get drivers data
            var playerData = PlayerSpatialData.FromGameData(data);
            var opponentsData = GetOpponentsData(data, playerData);

            // No need to spot if there is no opponent
            if (!opponentsData.Any()) return;

            // Convert F1 rotation to the CrewChief expected one
            var playerRotation = playerData.Yaw;
            if (playerRotation < 0) playerRotation = playerRotation * -1;
            else playerRotation = twoPi - playerRotation;

            // Delegate work to the spotter
            var opponentPositions = opponentsData.Select(opponent => opponent.RawPosition).ToList();
            var opponentVelocities = opponentsData.Select(opponent => opponent.RawVelocity).ToList();
            internalSpotter.triggerInternal(
                playerRotation, 
                playerData.RawPosition, 
                playerData.RawVelocity, 
                opponentPositions,
                opponentVelocities
            );
        }

        private List<OpponentSpatialData> GetOpponentsData(F12023StructWrapper data, in PlayerSpatialData playerData)
        {
            var carMotionData = data.packetMotionData.m_carMotionData;

            // Prealloc list size, we expect it to be amount of car motions minus the player
            var output = new List<OpponentSpatialData>(carMotionData.Length - 1);

            // Parse motion data
            for (var i = 0; i < carMotionData.Length; i++)
            {
                // If this is the player motion data, skip it
                if (i == playerData.DriverIndex) continue; 

                var currentOpponent = OpponentSpatialData.FromMotionData(carMotionData[i]);
                output.Add(currentOpponent);
            }

            return output;
        }
    }
}
