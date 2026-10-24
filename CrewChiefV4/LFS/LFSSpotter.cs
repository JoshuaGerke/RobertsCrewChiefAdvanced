using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using System;
using System.Collections.Generic;
using System.Linq;
using static CrewChiefV4.LFS.LFSInSimReader;

namespace CrewChiefV4.LFS
{
    /// <summary>
    /// Radar proximity and overlap spotter for Live for Speed.
    /// Uses InSim CompCar coordinate and heading data from MCI packets, converting from LFS
    /// world space (X=East, Y=North, Z=Up) to CrewChief's spotter Cartesian coordinate system.
    /// </summary>
    class LFSSpotter : Spotter
    {
        private readonly float defaultCarLength = 4.5f;
        private readonly float defaultCarWidth = 1.8f;
        private readonly int timeAfterRaceStartToActivate = UserSettings.GetUserSettings().getInt("time_after_race_start_for_spotter");

        private string currentPlayerCarClassID = "#not_set#";

        public LFSSpotter(AudioPlayer audioPlayer, Boolean initialEnabledState)
        {
            this.audioPlayer = audioPlayer;
            this.enabled = initialEnabledState;
            this.initialEnabledState = initialEnabledState;

            float carLength = UserSettings.GetUserSettings().getFloat("lfs_spotter_car_length");
            if (carLength <= 0.0f)
            {
                carLength = defaultCarLength;
            }

            this.internalSpotter = new NoisyCartesianCoordinateSpotter(audioPlayer, initialEnabledState, carLength, defaultCarWidth);
        }

        public override void clearState()
        {
            internalSpotter.clearState();
        }

        public override void trigger(Object lastStateObj, Object currentStateObj, GameStateData currentGameState)
        {
            if (this.paused || !this.enabled)
            {
                return;
            }

            var currentWrapper = currentStateObj as LFSStructWrapper;
            if (currentWrapper == null || currentWrapper.Data == null)
            {
                return;
            }

            LFSData lfsData = currentWrapper.Data;
            byte viewPLID = lfsData.SessionState.ViewPLID;
            if ((viewPLID == 0 || !lfsData.Players.ContainsKey(viewPLID)) && lfsData.Players.Count > 0)
            {
                viewPLID = lfsData.Players.Keys.First();
            }
            if (viewPLID == 0 || !lfsData.Players.ContainsKey(viewPLID))
            {
                return;
            }

            Player viewedPlayer = lfsData.Players[viewPLID];
            if (viewedPlayer.Scoring == null)
            {
                return;
            }

            // If not spawned on track or in garage (coordinates 0,0)
            if (viewedPlayer.Scoring.X == 0 && viewedPlayer.Scoring.Y == 0)
            {
                return;
            }

            // Don't spot in pit lane
            if (viewedPlayer.PitData != null && viewedPlayer.PitData.PitLaneFact == InSimDotNet.Packets.PitLaneFact.PITLANE_ENTER)
            {
                return;
            }

            if (currentGameState != null && currentGameState.SessionData != null)
            {
                if (currentGameState.SessionData.SessionRunningTime < this.timeAfterRaceStartToActivate &&
                    currentGameState.SessionData.SessionType == SessionType.Race)
                {
                    return;
                }

                var carClass = currentGameState.carClass;
                if (carClass != null && !string.Equals(this.currentPlayerCarClassID, carClass.getClassIdentifier()))
                {
                    this.internalSpotter.setCarDimensions(GlobalBehaviourSettings.spotterVehicleLength, GlobalBehaviourSettings.spotterVehicleWidth);
                    this.currentPlayerCarClassID = carClass.getClassIdentifier();
                }
            }

            // Speed in m/s (LFS CompCar.Speed: 32768 = 100 m/s => 327.68 = 1 m/s)
            float currentPlayerSpeed = (float)viewedPlayer.Scoring.Speed / 327.68f;

            // Player reference position: arbitrary non-zero coordinates so spotter stays active
            float[] currentPlayerPosition = new float[] { 1000.0f, 1000.0f };
            float[] playerVelocityData = new float[] { currentPlayerSpeed, 0.0f, 0.0f };

            // LFS Heading: 0 = +Y (North), 32768 = 180 deg, measured anticlockwise from above.
            // Using the proven relative transformation from Radar_v5.py:
            // CVHAngRad = 2 * PI - Heading * 2 * PI / 65536.0
            // x1 = dx * cos(CVHAngRad) - dy * sin(CVHAngRad)  => +x1 = right, -x1 = left
            // y1 = dx * sin(CVHAngRad) + dy * cos(CVHAngRad)  => +y1 = ahead, -y1 = behind
            double theta = 2.0 * Math.PI - (double)viewedPlayer.Scoring.Heading * (2.0 * Math.PI) / 65536.0;
            float cosTheta = (float)Math.Cos(theta);
            float sinTheta = (float)Math.Sin(theta);

            int playerX = viewedPlayer.Scoring.X;
            int playerY = viewedPlayer.Scoring.Y;

            List<float[]> currentOpponentPositions = new List<float[]>();
            List<float> opponentSpeeds = new List<float>();

            foreach (var kvp in lfsData.Players)
            {
                Player opp = kvp.Value;
                if (opp.PLID == viewPLID || opp.Scoring == null)
                {
                    continue;
                }

                // Ignore cars not spawned or in garage
                if (opp.Scoring.X == 0 && opp.Scoring.Y == 0)
                {
                    continue;
                }

                // Ignore opponents in pit lane
                if (opp.PitData != null && opp.PitData.PitLaneFact == InSimDotNet.Packets.PitLaneFact.PITLANE_ENTER)
                {
                    continue;
                }

                float dx = (float)(opp.Scoring.X - playerX) / 65536.0f;
                float dy = (float)(opp.Scoring.Y - playerY) / 65536.0f;

                float lateralOffset = dx * cosTheta - dy * sinTheta;       // meters: > 0 is RIGHT, < 0 is LEFT
                float longitudinalOffset = dx * sinTheta + dy * cosTheta;  // meters: > 0 is AHEAD, < 0 is BEHIND

                // In NoisyCartesianCoordinateSpotter:
                // alignedX < 0 is RIGHT, alignedX > 0 is LEFT
                // alignedZ < 0 is AHEAD, alignedZ > 0 is BEHIND
                // Passing rotation = 0:
                // alignedX = oppX - playerX = (1000 - lateralOffset) - 1000 = -lateralOffset (< 0 if right, > 0 if left)
                // alignedZ = oppZ - playerZ = (1000 - longitudinalOffset) - 1000 = -longitudinalOffset (< 0 if ahead, > 0 if behind)
                currentOpponentPositions.Add(new float[] { 1000.0f - lateralOffset, 1000.0f - longitudinalOffset });
                opponentSpeeds.Add((float)opp.Scoring.Speed / 327.68f);
            }

            // Always trigger, even with 0 opponents, so that "clear" messages are issued when overlaps end!
            internalSpotter.triggerInternal(
                0.0f,
                currentPlayerPosition,
                playerVelocityData,
                currentOpponentPositions,
                null,
                opponentSpeeds.ToArray()
            );
        }
    }
}
