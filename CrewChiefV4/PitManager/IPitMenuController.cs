using System.Collections.Generic;
using CrewChiefV4.LMU;
using CrewChiefV4.RF2;

using static CrewChiefV4.RF2.Rf2PitMenuController;

namespace PitMenuAPI
{
    public interface IPitMenuController : IPitMenu
    {
        Rf2PitMenuController.PitMenuContents GetMenuContents();
        bool SmartSetCategory(string category);
        bool RelativeFuelStrategy();
        int GetFuelLevelSetting();
        bool SetFuelLevel(int requiredFuel);
        List<string> GetTyreTypeNames();
        List<string> GetTyreChangeCategories();
        bool SetTyreType(string whichTyre, string requiredType);
        float GetFuelRatio();
        float GetMaxFuelRatio();
        bool SetFuelRatio(int percentage);
        int GetVirtualEnergy();
        bool SetVirtualEnergy(int percentage);

    }
}
