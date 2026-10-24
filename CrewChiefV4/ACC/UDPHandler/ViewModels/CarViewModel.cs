using ksBroadcastingNetwork;
using ksBroadcastingNetwork.Structs;
using System.Collections.Generic;
using System.Linq;

namespace ksBroadcastingTestClient.Broadcasting
{
    public class CarViewModel
    {
        public int CarIndex { get; }
        public int RaceNumber { get; private set; }
        public int CarModelEnum { get; private set; }
        public string TeamName { get; private set; }
        public int CupCategoryEnum { get; private set; }
        public DriverViewModel CurrentDriver { get; private set; }

        public IEnumerable<DriverViewModel> InactiveDrivers { get { return Drivers.Where(x => x.DriverIndex != CurrentDriver?.DriverIndex); } }
        public IList<DriverViewModel> Drivers { get; } = new List<DriverViewModel>();

        public CarLocationEnum CarLocation { get; private set; }
        public int Delta { get; private set; }
        public int Gear { get; private set; }
        public int Kmh { get; private set; }
        public int Position { get; private set; }
        public int CupPosition { get; private set; }
        public int TrackPosition { get; private set; }
        public float SplinePosition { get; private set; }
        public float WorldX { get; private set; }
        public float WorldY { get; private set; }
        public float Yaw { get; private set; }
        public int Laps { get; private set; }
        public LapTime BestLap { get; private set; }
        public LapTime LastLap { get; private set; }
        public LapTime CurrentLap { get; private set; }
        public float GapFrontMeters { get; internal set; }

        public CarViewModel(ushort carIndex)
        {
            CarIndex = carIndex;
        }

        internal void Update(CarInfo carUpdate)
        {
            RaceNumber = carUpdate.RaceNumber;
            CarModelEnum = carUpdate.CarModelType;
            TeamName = carUpdate.TeamName;
            CupCategoryEnum = carUpdate.CupCategory;

            if (carUpdate.Drivers.Count != Drivers.Count)
            {
                Drivers.Clear();
                int driverIndex = 0;
                foreach (DriverInfo driver in carUpdate.Drivers)
                {
                    Drivers.Add(new DriverViewModel(driver, driverIndex++));
                }
            }
        }

        internal void Update(RealtimeCarUpdate carUpdate)
        {
            if (carUpdate.CarIndex != CarIndex)
            {
                System.Diagnostics.Debug.WriteLine($"Wrong {nameof(RealtimeCarUpdate)}.CarIndex {carUpdate.CarIndex} for {nameof(CarViewModel)}.CarIndex {CarIndex}");
                return;
            }

            if (CurrentDriver?.DriverIndex != carUpdate.DriverIndex)
            {
                // The driver has changed!
                CurrentDriver = Drivers.SingleOrDefault(x => x.DriverIndex == carUpdate.DriverIndex);
            }

            CarLocation = carUpdate.CarLocation;
            Delta = carUpdate.Delta;
            Gear = carUpdate.Gear;
            Kmh = carUpdate.Kmh;
            Position = carUpdate.Position;
            CupPosition = carUpdate.CupPosition;
            TrackPosition = carUpdate.TrackPosition;
            SplinePosition = carUpdate.SplinePosition;
            WorldX = carUpdate.WorldPosX;
            WorldY = carUpdate.WorldPosY;
            Yaw = carUpdate.Yaw;
            Laps = carUpdate.Laps;

            if (BestLap == null && carUpdate.BestSessionLap != null)
                BestLap = new LapTime();

            if (carUpdate.BestSessionLap != null)
                BestLap.Update(carUpdate.BestSessionLap);

            if (LastLap == null && carUpdate.LastLap != null)
                LastLap = new LapTime();

            if (carUpdate.LastLap != null)
                LastLap.Update(carUpdate.LastLap);

            if (CurrentLap == null && carUpdate.CurrentLap != null)
                CurrentLap = new LapTime();

            if (carUpdate.CurrentLap != null)
                CurrentLap.Update(carUpdate.CurrentLap);
        }
    }
}
