using System.Runtime.InteropServices;

namespace F12023UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Penalty
    {
        /// <summary>
        /// Penalty type – see Appendices
        /// </summary>
        public byte penaltyType;

        /// <summary>
        /// Infringement type – see Appendices
        /// </summary>
        public byte infringementType;

        /// <summary>
        /// Vehicle index of the car the penalty is applied to
        /// </summary>
        public byte vehicleIdx;

        /// <summary>
        /// Vehicle index of the other car involved
        /// </summary>
        public byte otherVehicleIdx;

        /// <summary>
        /// Time gained, or time spent doing action in seconds
        /// </summary>
        public byte time;

        /// <summary>
        /// Lap the penalty occurred on
        /// </summary>
        public byte lapNum;

        /// <summary>
        /// Number of places gained by this
        /// </summary>
        public byte placesGained;
    }
}