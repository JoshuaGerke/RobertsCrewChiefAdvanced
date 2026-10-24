//#define TRACE_SPOTTER_ELAPSED_TIME

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CrewChiefV4.Events;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using GTR2SharedMemory;
using GTR2SharedMemory.GTR2Data;

// When team moves to VS2015 or newer.
//using static CrewChiefV4.GTR2.GTR2Constants;

namespace CrewChiefV4.GTR2
{
    class GTR2Spotter : Spotter
    {
        // how long is a car? we use 3.5 meters by default here. Too long and we'll get 'hold your line' messages
        // when we're clearly directly behind the car
        // Note: both below variables can be overrided in car class.
        private readonly float carLength = UserSettings.GetUserSettings().getFloat("gtr2_spotter_car_length");
        private readonly float carWidth = 1.8f;

        // don't activate the spotter unless this many seconds have elapsed (race starts are messy)
        private readonly int timeAfterRaceStartToActivate = UserSettings.GetUserSettings().getInt("time_after_race_start_for_spotter");

        private DateTime previousTime = DateTime.UtcNow;
        private string currentPlayerCarClassID = "#not_set#";

        public GTR2Spotter(AudioPlayer audioPlayer, Boolean initialEnabledState)
        {
            this.audioPlayer = audioPlayer;
            this.enabled = initialEnabledState;
            this.initialEnabledState = initialEnabledState;
            this.internalSpotter = new NoisyCartesianCoordinateSpotter(
                audioPlayer, initialEnabledState, carLength, carWidth);
        }

        public override void clearState()
        {
            this.previousTime = DateTime.UtcNow;
            this.internalSpotter.clearState();
        }

        private bool tryGetVehicleInfo(CrewChiefV4.GTR2.GTR2SharedMemoryReader.GTR2StructWrapper shared, out GTR2VehicleScoring vehicleScoring)
        {
            for (int i = 0; i < shared.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                if (shared.scoring.mVehicles[i].mIsPlayer == 1)
                {
                    vehicleScoring = shared.scoring.mVehicles[i];
                    return true;
                }
            }
            vehicleScoring = default(GTR2VehicleScoring);
            return false;
        }

        public override void trigger(Object lastStateObj, Object currentStateObj, GameStateData currentGameState)
        {
#if TRACE_SPOTTER_ELAPSED_TIME
            var watch = System.Diagnostics.Stopwatch.StartNew();
#endif
            if (this.paused)
                return;

            var lastState = lastStateObj as CrewChiefV4.GTR2.GTR2SharedMemoryReader.GTR2StructWrapper;
            var currentState = currentStateObj as CrewChiefV4.GTR2.GTR2SharedMemoryReader.GTR2StructWrapper;

            GTR2VehicleScoring currentPlayerScoring;
            GTR2VehicleScoring previousPlayerScoring;

            if (!this.enabled 
                || (!GlobalBehaviourSettings.ovalSpotterMode && currentState.scoring.mScoringInfo.mCurrentET < this.timeAfterRaceStartToActivate)
                || currentGameState.OpponentData.Count == 0
                || currentState.scoring.mScoringInfo.mInRealtime == 0
                || lastState.scoring.mScoringInfo.mInRealtime == 0
                // turn off spotter for formation lap before going green
                || currentState.scoring.mScoringInfo.mGamePhase == (int)GTR2Constants.GTR2GamePhase.Formation
                || !this.tryGetVehicleInfo(currentState, out currentPlayerScoring)
                || !this.tryGetVehicleInfo(lastState, out previousPlayerScoring))
                return;

            var now = DateTime.UtcNow;
            float timeDiffSeconds = ((float)(now - this.previousTime).TotalMilliseconds) / 1000.0f;
            this.previousTime = now;
            if (timeDiffSeconds <= 0.0f)
            {
                // In pits probably.
                return;
            }

            if (currentPlayerScoring.mInPits != 0)  // No spotter in pits.
                return;

            if (currentGameState != null)
            {
                var carClass = currentGameState.carClass;
                if (carClass != null && !string.Equals(this.currentPlayerCarClassID, carClass.getClassIdentifier()))
                {
                    // Retrieve and use user overridable spotter car length/width.
                    this.internalSpotter.setCarDimensions(GlobalBehaviourSettings.spotterVehicleLength, GlobalBehaviourSettings.spotterVehicleWidth);
                    this.currentPlayerCarClassID = carClass.getClassIdentifier();
                }
            }

            // Initialize current player information.
            float[] currentPlayerPosition = null;
            float currentPlayerSpeed = -1.0f;
            float playerRotation = 0.0f;

            var currentPlayerTelemetry = currentState.telemetry.mPlayerTelemetry;

            currentPlayerPosition = new float[] { (float)currentPlayerTelemetry.mPos.x, (float)currentPlayerTelemetry.mPos.z };
            currentPlayerSpeed = (float)Math.Sqrt((currentPlayerTelemetry.mLocalVel.x * currentPlayerTelemetry.mLocalVel.x)
                + (currentPlayerTelemetry.mLocalVel.y * currentPlayerTelemetry.mLocalVel.y)
                + (currentPlayerTelemetry.mLocalVel.z * currentPlayerTelemetry.mLocalVel.z));

            playerRotation = (float)(Math.Atan2(currentPlayerTelemetry.mOriZ.x, currentPlayerTelemetry.mOriZ.z));

            if (playerRotation < 0.0f)
                playerRotation = (float)(2.0f * Math.PI) + playerRotation;

            // Find position data for previous player vehicle.  Default to scoring pos, but use telemetry if available (corner case, should not happen often).
            var previousPlayerPosition = new float[] { (float)previousPlayerScoring.mPos.x, (float)previousPlayerScoring.mPos.z };
            /*for (int i = 0; i < lastState.telemetry.mNumVehicles; ++i)
            {
                if (previousPlayerScoring.mID == lastState.telemetry.mVehicles[i].mID)
                {
                    previousPlayerPosition = new float[] { (float)lastState.telemetry.mVehicles[i].mPos.x, (float)lastState.telemetry.mVehicles[i].mPos.z };
                    break;
                }
            }*/

            var playerVelocityData = new float[] {
                currentPlayerSpeed,
                (currentPlayerPosition[0] - previousPlayerPosition[0]) / timeDiffSeconds,
                (currentPlayerPosition[1] - previousPlayerPosition[1]) / timeDiffSeconds };

            var currentOpponentPositions = new List<float[]>();
            for (int i = 0; i < currentState.scoring.mScoringInfo.mNumVehicles; ++i)
            {
                var vehicle = currentState.scoring.mVehicles[i];
                if (vehicle.mIsPlayer == 1 || vehicle.mInPits == 1 || vehicle.mLapDist < 0.0f)
                    continue;

                //int opponentTelIdx = -1;
                /*if (idsToTelIndicesMap.TryGetValue(vehicle.mID, out opponentTelIdx))
                {
                    var opponentTelemetry = currentState.telemetry.mVehicles[opponentTelIdx];
                    currentOpponentPositions.Add(new float[] { (float)opponentTelemetry.mPos.x, (float)opponentTelemetry.mPos.z });
                }
                else*/
                currentOpponentPositions.Add(new float[] { (float)vehicle.mPos.x, (float)vehicle.mPos.z });  // Use scoring if telemetry isn't available.
            }

            this.internalSpotter.triggerInternal(playerRotation, currentPlayerPosition, playerVelocityData, currentOpponentPositions);

#if TRACE_SPOTTER_ELAPSED_TIME
            watch.Stop();
            var microseconds = watch.ElapsedTicks * 1000000 / System.Diagnostics.Stopwatch.Frequency;
            System.Console.WriteLine("Spotter microseconds: " + microseconds);
#endif
        }

        public NoisyCartesianCoordinateSpotter getInternalSpotter()
        {
            return this.internalSpotter;
        }
    }
}
