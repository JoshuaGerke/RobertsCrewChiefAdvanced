using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.Events;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;

namespace CrewChiefV4.PMR
{
    class PMRSpotter : Spotter
    {
        private readonly float twoPi = (float)(2 * Math.PI);

        // delay before spotter becomes active after race start (same key as other games)
        private readonly int timeAfterRaceStartToActivate =
            UserSettings.GetUserSettings().getInt("time_after_race_start_for_spotter");

        // same behaviour as PCars2 – allow disabling spotter in time trial / hotlap
        private readonly bool enableSpotterInTimetrial =
            UserSettings.GetUserSettings().getBoolean("enable_spotter_in_timetrial");

        // reuse the existing car-length setting for now
        // (you can add a "pmr_spotter_car_length" key later if you prefer)
        private readonly float carLength =
            UserSettings.GetUserSettings().getFloat("pcars2_spotter_car_length");

        // default car width used by NoisyCartesianCoordinateSpotter
        private readonly float carWidth = 1.8f;

        private DateTime timeToStartSpotting = DateTime.UtcNow;

        private string currentPlayerCarClassID = "#not_set#";

        private readonly HashSet<int> positionsFilledForThisTick = new HashSet<int>();

        private DateTime nextCarClassCheckDue = DateTime.MinValue;

        public PMRSpotter(AudioPlayer audioPlayer, bool initialEnabledState)
        {
            this.audioPlayer = audioPlayer;
            this.enabled = initialEnabledState;
            this.initialEnabledState = initialEnabledState;

            // PMR is always UDP, so just use the UDP-friendly dimensions
            this.internalSpotter =
                new NoisyCartesianCoordinateSpotter(audioPlayer, initialEnabledState, carLength, carWidth);
        }

        public override void clearState()
        {
            timeToStartSpotting = DateTime.UtcNow;
            internalSpotter.clearState();
        }

        // For double-file manual rolling starts – same pattern as PCars2Spotterv2
        public override Tuple<GridSide, Dictionary<int, GridSide>> getGridSide(object currentStateObj)
        {
            var wrapper = currentStateObj as PMRUDPReader.PMRStructWrapper;
            if (wrapper == null || wrapper.PlayerTelemetry == null || wrapper.PlayerRaceState == null)
            {
                return base.getGridSide(currentStateObj);
            }

            var playerChassis = wrapper.PlayerTelemetry.m_chassis;

            float playerRotation = CalculatePlayerRotation(playerChassis);
            float playerXPosition = playerChassis.m_posWS.x;
            float playerZPosition = playerChassis.m_posWS.z;
            int playerStartingPosition = wrapper.PlayerRaceState.m_racePos;
            int numCars = wrapper.RaceInfo.m_numParticipants;

            return getGridSideInternal(wrapper, playerRotation, playerXPosition, playerZPosition,
                                       playerStartingPosition, numCars);
        }

        // Used by getGridSideInternal() – find world position for car starting at a specific grid slot
        protected override float[] getWorldPositionOfDriverAtPosition(object currentStateObj, int position)
        {
            var wrapper = currentStateObj as PMRUDPReader.PMRStructWrapper;
            if (wrapper == null || wrapper.Leaderboard == null || wrapper.AllTelemetry == null)
                return null;

            var raceState = wrapper.Leaderboard.FirstOrDefault(r => r.m_racePos == position);
            if (raceState == null)
                return null;

            var tel = wrapper.AllTelemetry.FirstOrDefault(t => t.m_vehicleId == raceState.m_vehicleId);
            if (tel == null)
                return null;

            return new[] { tel.m_chassis.m_posWS.x, tel.m_chassis.m_posWS.z };
        }

        public override void trigger(object lastStateObj, object currentStateObj, GameStateData currentGameState)
        {
            if (paused)
                return;

            var currentWrapper = currentStateObj as PMRUDPReader.PMRStructWrapper;
            var lastWrapper = lastStateObj as PMRUDPReader.PMRStructWrapper;

            if (currentWrapper == null ||
                currentWrapper.RaceInfo == null ||
                currentWrapper.PlayerTelemetry == null ||
                currentWrapper.PlayerRaceState == null)
            {
                return;
            }

            UDPRaceInfo raceInfo = currentWrapper.RaceInfo;
            UDPParticipantRaceState playerState = currentWrapper.PlayerRaceState;

            // Only spot in active sessions
            if (raceInfo.m_state != UDPRaceSessionState.Active)
            {
                return;
            }


            if (playerState.m_inPits)
            {
                return;
            }

            DateTime now = new DateTime(currentWrapper.ticksWhenRead);

            bool isRaceSession = currentGameState != null &&
                                 currentGameState.SessionData.SessionType == SessionType.Race;
            bool isTimeTrialLike = currentGameState != null &&
                                   currentGameState.SessionData.SessionType == SessionType.HotLap;

            // Detect transition into an Active *race* session to delay spotter at race start
            if (isRaceSession &&
                lastWrapper != null &&
                lastWrapper.RaceInfo != null &&
                lastWrapper.RaceInfo.m_state != UDPRaceSessionState.Active &&
                raceInfo.m_state == UDPRaceSessionState.Active)
            {
                if (GlobalBehaviourSettings.ovalSpotterMode)
                {
                    timeToStartSpotting = now.Add(TimeSpan.FromSeconds(2));
                }
                else
                {
                    timeToStartSpotting = now.Add(TimeSpan.FromSeconds(timeAfterRaceStartToActivate));
                }
            }

            if (isRaceSession && now < timeToStartSpotting)
            {
                // race just started and we haven’t waited long enough yet
                return;
            }

            if (isTimeTrialLike && !enableSpotterInTimetrial)
            {
                // respect the global "no spotter in time trial" setting
                return;
            }

            if (!enabled)
            {
                return;
            }

            if (raceInfo.m_numParticipants <= 1)
            {
                return; // nothing to spot
            }

            if (currentGameState != null && currentGameState.PitData.InPitlane)
            {
                // avoid spotter while in the pit lane
                return;
            }

            // Adjust car dimensions if the user's spotter car-size overrides change for this class
            if (currentGameState != null && currentGameState.Now > nextCarClassCheckDue)
            {
                var carClass = currentGameState.carClass;
                if (carClass != null && !string.Equals(currentPlayerCarClassID, carClass.getClassIdentifier()))
                {
                    internalSpotter.setCarDimensions(
                        GlobalBehaviourSettings.spotterVehicleLength,
                        GlobalBehaviourSettings.spotterVehicleWidth);
                    currentPlayerCarClassID = carClass.getClassIdentifier();
                }

                nextCarClassCheckDue = currentGameState.Now.AddSeconds(5);
            }

            var playerTelemetry = currentWrapper.PlayerTelemetry;
            var playerChassis = playerTelemetry.m_chassis;

            // Player world position (X, Z)
            float[] currentPlayerPosition =
            {
                playerChassis.m_posWS.x,
                playerChassis.m_posWS.z
            };

            // Player speed + velocity vector, like PCars2Spotterv2
            float[] playerVelocityData = new float[3];
            playerVelocityData[0] = playerChassis.m_overallSpeed;
            playerVelocityData[1] = playerChassis.m_velocityWS.x;
            playerVelocityData[2] = playerChassis.m_velocityWS.z;

            // Build opponent positions
            if (currentWrapper.Leaderboard == null || currentWrapper.AllTelemetry == null)
            {
                return;
            }

            List<float[]> currentOpponentPositions = new List<float[]>();
            positionsFilledForThisTick.Clear();
            positionsFilledForThisTick.Add(currentWrapper.PlayerRaceState.m_racePos);

            foreach (var entry in currentWrapper.Leaderboard)
            {
                if (entry.m_isPlayer)
                    continue;

                if (entry.m_sessionFinished || entry.m_dq)
                    continue;

                if (positionsFilledForThisTick.Contains(entry.m_racePos))
                    continue;

                var oppTel = currentWrapper.AllTelemetry.FirstOrDefault(t => t.m_vehicleId == entry.m_vehicleId);
                if (oppTel == null)
                    continue;

                currentOpponentPositions.Add(new[]
                {
                    oppTel.m_chassis.m_posWS.x,
                    oppTel.m_chassis.m_posWS.z
                });

                positionsFilledForThisTick.Add(entry.m_racePos);
            }

            if (currentOpponentPositions.Count == 0)
            {
                return;
            }

            float playerRotation = CalculatePlayerRotation(playerChassis);

            internalSpotter.triggerInternal(
                playerRotation,
                currentPlayerPosition,
                playerVelocityData,
                currentOpponentPositions);
        }

        private float CalculatePlayerRotation(UDPVehicleTelemetryChassis chassis)
        {
            // World-space horizontal velocity
            float vx = chassis.m_velocityWS.x;
            float vz = chassis.m_velocityWS.z;

            // If we're basically stationary, orientation doesn't matter much – just return 0
            if (Math.Abs(vx) < 0.01f && Math.Abs(vz) < 0.01f)
            {
                return 0f;
            }

            // Heading in the XZ plane, 0 radians when pointing along +X, CCW positive
            float yaw = (float)Math.Atan2(vz, vx);

            if (yaw < 0)
            {
                yaw += twoPi;
            }

            yaw -= 0.5f * (float)Math.PI; // rotate -90°
            if (yaw >= twoPi) yaw -= twoPi;

            return yaw;
        }

    }
}
