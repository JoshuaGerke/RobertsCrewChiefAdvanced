using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrewChiefV4.Events
{
    public abstract class Spotter
    {
        protected NoisyCartesianCoordinateSpotter internalSpotter;

        private Boolean _paused;
        protected Boolean paused
        {
            get => _paused;
            set
            {
                if (_paused != value)
                {
                    _paused = value;
                    Tracepoints.Spotter.paused_Changed(_paused);
                }
            }
        }

        private Boolean _enabled;
        protected Boolean enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    //Tracepoints.Spotter.enabled_Changed(_enabled);  tjw  That stops the Console Window??????????????
                }
            }
        }
        protected Boolean initialEnabledState;

        protected AudioPlayer audioPlayer;

        public abstract void clearState();

        public abstract void trigger(Object lastState, Object currentState, GameStateData currentGameState);

        public bool hasOverlap()
        {
            return internalSpotter.hasOverlap;
        }

        public void enableSpotter()
        {
            enabled = true;
            audioPlayer.playMessageImmediately(new QueuedMessage(NoisyCartesianCoordinateSpotter.folderEnableSpotter, 0));
        }
        public void disableSpotter()
        {
            enabled = false;
            audioPlayer.playMessageImmediately(new QueuedMessage(NoisyCartesianCoordinateSpotter.folderDisableSpotter, 0));
        }

        public void pause()
        {
            this.paused = true;
        }

        public void unpause()
        {
            this.paused = false;
        }

        public Boolean isPaused()
        {
            return this.paused;
        }
        
        protected virtual float[] getWorldPositionOfDriverAtPosition(Object currentStateObj, int position)
        {
            return null;
        }

        public virtual Tuple<GridSide, Dictionary<int, GridSide>> getGridSide(Object currentStateObj)
        {
            return new Tuple<GridSide, Dictionary<int, GridSide>>(GridSide.UNKNOWN, new Dictionary<int, GridSide>());
        }

        protected Tuple<GridSide, Dictionary<int, GridSide>> getGridSideInternal(Object currentStateObj, float playerRotation, float playerXPosition,
            float playerZPosition, int playerStartingPosition, int numCars)
        {
            GridSide playerGridSide = GridSide.UNKNOWN;
            Boolean countForwards = playerStartingPosition != 1;

            float[] worldPositionOfOpponent = null;
            int opponentStartingPositionToCheck = countForwards ? playerStartingPosition - 1 : playerStartingPosition + 1;
            int opponentCheckCount = 0;
            // only check 5 opponents, then give up
            while (opponentCheckCount < 5)
            {
                worldPositionOfOpponent = getWorldPositionOfDriverAtPosition(currentStateObj, opponentStartingPositionToCheck);
                if (worldPositionOfOpponent != null)
                {
                    float[] alignedCoordiates = this.internalSpotter.getAlignedXZCoordinates(playerRotation,
                        playerXPosition, playerZPosition, worldPositionOfOpponent[0], worldPositionOfOpponent[1]);
                    if (alignedCoordiates[0] < -2)
                    {
                        playerGridSide = GridSide.LEFT;
                        break;
                    }
                    else if (alignedCoordiates[0] > 2)
                    {
                        playerGridSide = GridSide.RIGHT;
                        break;
                    }
                }
                if (countForwards)
                {
                    if (opponentStartingPositionToCheck == 1)
                    {
                        // we're counting forwards and have reached pole, so go back from the player
                        opponentStartingPositionToCheck = playerStartingPosition + 1;
                        countForwards = false;
                    }
                    else
                    {
                        opponentStartingPositionToCheck--;
                    }
                }
                else
                {
                    if (opponentStartingPositionToCheck == numCars - 1)
                    {
                        // we're counting backwards and have reached last, so go forward from the player
                        opponentStartingPositionToCheck = playerStartingPosition - 1;
                        countForwards = true;
                    }
                    else
                    {
                        opponentStartingPositionToCheck++;
                    }
                }
            }
            // now get GridSide for the opponents starting ahead
            int opponentAheadPosition = playerStartingPosition - 1;
            Dictionary<int, GridSide> opponentGridSides = new Dictionary<int, GridSide>();
            while (opponentAheadPosition > 0)
            {
                worldPositionOfOpponent = getWorldPositionOfDriverAtPosition(currentStateObj, opponentAheadPosition);
                if (worldPositionOfOpponent != null)
                {
                    float[] alignedCoordiates = this.internalSpotter.getAlignedXZCoordinates(playerRotation,
                        playerXPosition, playerZPosition, worldPositionOfOpponent[0], worldPositionOfOpponent[1]);
                    if (Math.Abs(alignedCoordiates[0]) > 2)
                    {
                        switch (playerGridSide)
                        {
                            case GridSide.LEFT:
                                opponentGridSides.Add(opponentAheadPosition, GridSide.RIGHT);
                                break;
                            case GridSide.RIGHT:
                                opponentGridSides.Add(opponentAheadPosition, GridSide.LEFT);
                                break;
                        }
                    }
                    else switch (playerGridSide)
                    {
                        case GridSide.LEFT:
                            opponentGridSides.Add(opponentAheadPosition, GridSide.LEFT);
                            break;
                        case GridSide.RIGHT:
                            opponentGridSides.Add(opponentAheadPosition, GridSide.RIGHT);
                            break;
                    }
                }
                opponentAheadPosition--;
            }
            return new Tuple<GridSide, Dictionary<int, GridSide>>(playerGridSide, opponentGridSides);
        }
    }

    public enum GridSide
    {
        UNKNOWN, LEFT, RIGHT
    }
}

namespace CrewChiefV4 // has to be CrewChiefV4.Tracepoints for automated discovery
{
    public partial class Tracepoints
    {
        public class Spotter
        {
            public static void enabled_Changed(bool _true)
            {
                if (TracepointIsChecked("Tracepoints/Spotter/enabled_Changed"))
                {
                    Log.Debug(_true ? "Spotter enabled" : "Spotter disabled");
                }
            }
            public static void paused_Changed(bool _true)
            {
                if (TracepointIsChecked("Tracepoints/Spotter/paused_Changed"))
                {
                    Log.Debug(_true ? "Spotter paused" : "Spotter active");
                }
            }
        }
    }
}
