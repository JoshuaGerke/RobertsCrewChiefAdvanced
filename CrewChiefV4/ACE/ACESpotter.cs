using System;
using System.Collections.Generic;
using CrewChiefV4.Events;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using CrewChiefV4.ACE.aceData;
using CrewChiefV4.ACE;

namespace CrewChiefV4.assetto
{
    class ACESpotter : Spotter
    {
        private float twoPi = (float)(2 * Math.PI);
        // how long is a car? we use 3.5 meters by default here. Too long and we'll get 'hold your line' messages
        // when we're clearly directly behind the car
        private float carLength =  UserSettings.GetUserSettings().getFloat("acs_spotter_car_length");

        private float carWidth = 1.8f;

        // don't activate the spotter unless this many seconds have elapsed (race starts are messy)
        private readonly int timeAfterRaceStartToActivate = UserSettings.GetUserSettings().getInt("time_after_race_start_for_spotter");

        private DateTime previousTime = DateTime.UtcNow;

        private string currentPlayerCarClassID = "#not_set#";

        public ACESpotter(AudioPlayer audioPlayer, Boolean initialEnabledState)
        {
            this.audioPlayer = audioPlayer;
            this.enabled = initialEnabledState;
            this.initialEnabledState = initialEnabledState;
            this.internalSpotter = new NoisyCartesianCoordinateSpotter(audioPlayer, initialEnabledState, carLength, carWidth);
            Console.WriteLine("ACESpotter enable");
        }

        public override void clearState()
        {
            previousTime = DateTime.UtcNow;
            internalSpotter.clearState();
        }

        // For double-file manual rolling starts. Will only work when the cars are all nicely settled on the grid - preferably 
        // when the game thinks the race has just started
        public override Tuple<GridSide, Dictionary<int, GridSide>> getGridSide(Object currentStateObj)
        {
            ACEShared latestRawData = ((ACESharedMemoryReader.ACEStructWrapper)currentStateObj).data;
            aceVehicleInfo playerData = latestRawData.aceChief.vehicle[latestRawData.aceChief.playerIndex];
            float playerRotation = latestRawData.acePhysics.heading;
            if (playerRotation < 0)
            {
                playerRotation = twoPi + playerRotation;
            }
            float playerXPosition = playerData.worldPosition.x;
            float playerZPosition = playerData.worldPosition.y;
            int playerStartingPosition = playerData.carLeaderboardPosition;
            int numCars = Math.Min((int)latestRawData.aceGraphic.totalDrivers, assettoConstant.MaxVehicles);
            return getGridSideInternal(latestRawData, playerRotation, playerXPosition, playerZPosition, playerStartingPosition, numCars);
        }

        protected override float[] getWorldPositionOfDriverAtPosition(Object currentStateObj, int position)
        {
            ACEShared latestRawData = (ACEShared)currentStateObj;
            foreach (aceVehicleInfo vehicleInfo in latestRawData.aceChief.vehicle)
            {
                if (vehicleInfo.carLeaderboardPosition == position)
                {
                    return new float[] { vehicleInfo.worldPosition.x, vehicleInfo.worldPosition.y };
                }
            }
            return new float[] { 0, 0 };
        }

        public float mapToFloatTime(int time)
        {
            TimeSpan ts = TimeSpan.FromTicks(time);
            return (float)ts.TotalMilliseconds * 10;
        }
        public override void trigger(Object lastStateObj, Object currentStateObj, GameStateData currentGameState)
        {
            if (paused)
            {
                return;
            }
            ACEShared currentState = ((ACESharedMemoryReader.ACEStructWrapper)currentStateObj).data;
            ACEShared lastState = ((ACESharedMemoryReader.ACEStructWrapper)lastStateObj).data;

            if (!enabled || currentState.aceGraphic.totalDrivers <= 1 ||
                currentState.aceGraphic.status == ACEVO_STATUS.AC_REPLAY || currentState.aceGraphic.status == ACEVO_STATUS.AC_OFF ||
                (mapToFloatTime(currentState.aceChief.vehicle[currentState.aceChief.playerIndex].currentLapTimeMS) < timeAfterRaceStartToActivate &&
                currentState.aceChief.vehicle[currentState.aceChief.playerIndex].lapCount <= 0))
            { 
               return;  
            }
            DateTime now = DateTime.UtcNow;
            aceVehicleInfo currentPlayerData;
            aceVehicleInfo previousPlayerData;
            float timeDiffSeconds;
            try
            {
                currentPlayerData = currentState.aceChief.vehicle[currentState.aceChief.playerIndex];
                previousPlayerData = lastState.aceChief.vehicle[currentState.aceChief.playerIndex];
                timeDiffSeconds = ((float)(now - previousTime).TotalMilliseconds) / 1000f;
                previousTime = now;
                if (timeDiffSeconds <= 0)
                {
                    return;
                }
            }
            catch (Exception)
            {
                return;
            }
            if (currentGameState != null)
            {
                var carClass = currentGameState.carClass;
                if (carClass != null && !String.Equals(currentPlayerCarClassID, carClass.getClassIdentifier()))
                {
                    // Retrieve and use user overridable spotter car length/width.
                    this.internalSpotter.setCarDimensions(GlobalBehaviourSettings.spotterVehicleLength, GlobalBehaviourSettings.spotterVehicleWidth);
                    this.currentPlayerCarClassID = carClass.getClassIdentifier();
                }
            }
            float[] currentPlayerPosition = new float[] { currentPlayerData.worldPosition.x, currentPlayerData.worldPosition.z };

            if (currentPlayerData.isCarInPitlane == 0 || currentPlayerData.isCarInPitlane == 0) // TBD EVO was pitlane/pit
            {
                List<float[]> currentOpponentPositions = new List<float[]>();
                float[] playerVelocityData = new float[3];
                playerVelocityData[0] = currentPlayerData.speedMS;
                playerVelocityData[1] = (currentPlayerData.worldPosition.x - previousPlayerData.worldPosition.x) / timeDiffSeconds;
                playerVelocityData[2] = (currentPlayerData.worldPosition.z - previousPlayerData.worldPosition.z) / timeDiffSeconds;

                for (int i = 1; i < Math.Min(currentState.aceGraphic.totalDrivers, assettoConstant.MaxVehicles); i++)
                {
                    aceVehicleInfo vehicle = currentState.aceChief.vehicle[i];
                    if (vehicle.carId.carIdA == 0 || vehicle.isCarInPitlane == 1 || vehicle.isCarInPitlane == 1 || vehicle.isConnected != 1)
                    {
                        continue;
                    }
                    currentOpponentPositions.Add(new float[] { vehicle.worldPosition.x, vehicle.worldPosition.z });
                }
                float playerRotation = currentState.acePhysics.heading;
                
                if (playerRotation < 0)
                {
                    playerRotation = twoPi + playerRotation;
                }
                internalSpotter.triggerInternal(playerRotation, currentPlayerPosition, playerVelocityData, currentOpponentPositions);
            }
        }
    }
}