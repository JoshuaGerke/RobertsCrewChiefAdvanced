using ksBroadcastingNetwork;
using ksBroadcastingNetwork.Structs;
using System.Linq;

namespace ksBroadcastingTestClient.Broadcasting
{
    public class DriverViewModel
    {
        public int DriverIndex { get; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string ShortName { get; private set; }
        public string DisplayName { get; private set; }
        public DriverCategory Category { get; private set; }


        internal DriverViewModel(DriverInfo driverUpdate, int driverIndex)
        {
            DriverIndex = driverIndex;
            FirstName = driverUpdate.FirstName;
            LastName = driverUpdate.LastName;
            ShortName = driverUpdate.ShortName;
            Category = driverUpdate.Category;

            var displayName = $"{FirstName} {LastName}".Trim();
            if (displayName.Length > 35)
                displayName = $"{FirstName?.First()}. {LastName}".TrimStart('.').Trim();
            if (displayName.Length > 35)
                displayName = $"{LastName}".Trim();
            if (displayName.Length > 35)
                displayName = $"{LastName.Substring(0, 33)}...".Trim();

            if (string.IsNullOrEmpty(displayName))
                displayName = "NO NAME";

            DisplayName = displayName;
        }
    }
}
