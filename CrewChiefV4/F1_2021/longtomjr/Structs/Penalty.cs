using System.Runtime.InteropServices;

namespace F12021UdpNet
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct Penalty
    {
        public byte penaltyType;          // Penalty type – see Appendices
        public byte infringementType;     // Infringement type – see Appendices
        public byte vehicleIdx;           // Vehicle index of the car the penalty is applied to
        public byte otherVehicleIdx;      // Vehicle index of the other car involved
        public byte time;                 // Time gained, or time spent doing action in seconds
        public byte lapNum;               // Lap the penalty occurred on
        public byte placesGained;         // Number of places gained by this
    }
}