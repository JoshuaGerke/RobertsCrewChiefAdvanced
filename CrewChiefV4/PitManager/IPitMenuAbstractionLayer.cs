using System.Collections.Generic;
using CrewChiefV4.RF2;

namespace PitMenuAPI
{
    public interface IPitMenuAbstractionLayer
    {
        IPitMenuController pmc { get; set; }

        bool Connect();
        void Disconnect();
        string GetChoice();
        bool SetChoice(string choice);
        bool SoftMatchCategory(string category);
        bool RelativeFuelStrategy();
        string CategoryDown();
        string CategoryUp();
        string ChoiceInc();
        string ChoiceDec();
        void RereadPitMenu();
        bool MfdPage(string Mfd);
        List<string> GetCategories();
        bool SmartSetCategory(string category);
        Rf2PitMenuController.PitMenuContents GetMenuContents();
        List<string> GetFrontTyreCategories();
        List<string> GetAllTyreCategories();
        string GetCurrentTyreType();
        List<string> GetRearTyreCategories();
        List<string> GetLeftTyreCategories();
        List<string> GetRightTyreCategories();
        List<string> GetTyreTypeNames();
        bool SetAllTyreTypes(string tyreType);
        bool SetCategoryAndChoice(string category, string choice);
        bool SetFuelLevel(int fuelLevel);
        float GetFuelRatio();
        float GetMaxFuelRatio();
        int GetVirtualEnergy();
        bool SetVirtualEnergy(int percentage);
        bool SetFuelRatio(int percentage);
        void SetMenuDict(Rf2PitMenuController.PitMenuContents dict);
    }
}
