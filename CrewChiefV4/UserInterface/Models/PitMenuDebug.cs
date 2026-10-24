#if PIT_MANAGER_DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

using CrewChiefV4.Events;
using CrewChiefV4.LMU;
using CrewChiefV4.RF2;
using CrewChiefV4.UserInterface.VMs;

using PitMenuAPI;

namespace CrewChiefV4.UserInterface.Models
{
    internal class PitMenuDebug
    {
        public enum DirectionButton
        {
            UP,
            DOWN,
            LEFT,
            RIGHT
        }

        private readonly PitMenuDebug_VM viewModel;
        private readonly IPitMenu pm;
        private readonly IPitMenuController pmc;
        private readonly IPitMenuAbstractionLayer pmal;
        private static Rf2PitMenuController.PitMenuContents menuDict;
        public PitMenuDebug(PitMenuDebug_VM _viewModel)
        {
            viewModel = _viewModel;
            Log.setLogLevel(Log.LogType.Verbose);
            Serilog.Log.Information("Crew Chief logging via Serilog.");
            Game.game = WindowsControl.bringGameToForeground();
            switch (Game.game)
            {
                case GameEnum.RF2_64BIT:
                    pmal = new RF2PitMenuAbstractionLayer();
                    pm = new RF2PitMenu();
                    pmc = pmal.pmc;
                    pm.startUsingPitMenu();
                    menuDict = pmc.GetMenuContents();
                    viewModel.WriteMenu(menuDict);
                    FillMenuChoices();
                    string pitCategory = pm.GetCategory();
                    viewModel.CheckmarkPitCategory(pitCategory);
                    break;
                case GameEnum.LMU:
                    pmal = new LMUPitMenuAbstractionLayer();
                    pm = new LMUPitMenuAPI();
                    pmc = pmal.pmc; // really??
                    //pmc = new LMUPitMenuController();
                    pm.startUsingPitMenu();
                    menuDict = pmal.GetMenuContents();
                    viewModel.WriteMenu(menuDict);
                    FillMenuChoices();
                    pitCategory = pm.GetCategory();
                    viewModel.CheckmarkPitCategory(pitCategory);
                    break;
                default:
                    Log.Error($"PitMenuDebug: Unsupported game {Game.game.ToString()}");
                    break;
            }
            Fuel.fuelReportsInGallons = true;
            viewModel.LitresOrGallons(Fuel.fuelReportsInGallons);
        }

        public void MenuButton(DirectionButton directionButton)
        {
            WindowsControl.bringGameToForeground();
            //pm.startUsingPitMenu();
            switch (directionButton)
            {
                case DirectionButton.UP:
                    pmal.CategoryUp();
                    break;
                case DirectionButton.DOWN:
                    pmal.CategoryDown();
                    break;
                case DirectionButton.LEFT:
                    pmal.ChoiceDec();
                    break;
                case DirectionButton.RIGHT:
                    pmal.ChoiceInc();
                    break;
            }
        }

        public void SwitchMFD(string mfd)
        {
            WindowsControl.bringGameToForeground();
            pm.switchMFD(mfd);
            viewModel.SetMfdDropdown(mfd);
        }
        public void SelectPitCategory(string pitCategory)
        {
            WindowsControl.bringGameToForeground();
            pm.SetCategory(pitCategory);
            pitCategory = pm.GetCategory();
            viewModel.CheckmarkPitCategory(pitCategory);
            viewModel.SetMfdDropdown("MFDB");
        }

        public void FillMenuChoices()
        {
            List<string> menuChoices = new List<string>();
            var currentCategory = pm.GetCategory();
            foreach (var category in menuDict.items)
            {
                SelectPitCategory(category.category);
                menuChoices.Add(pmal.GetChoice());
            }

            SelectPitCategory(currentCategory);
            viewModel.SetChoices(menuChoices);
        }
        public void SetFuel(string litres)
        {
            float fuel;
            //SelectPitCategory("FUEL:");
            if (float.TryParse(litres, out fuel))
            {
                if (Fuel.fuelReportsInGallons)
                {
                    fuel = Utilities.Conversions.convertGallonsToLitres(fuel);
                }
                WindowsControl.bringGameToForeground();
                pmal.SetFuelLevel((int)fuel);
                FillMenuChoices();
            }
            else
            {
                Log.Error($"Can't parse fuel level '{litres}'");
            }
        }
    }
    public class WindowsControl
    {
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private static readonly List<string> processNames = new List<string>{"rFactor2","Le Mans Ultimate"};
        private static Process[] matchingProcesses = Process.GetProcesses();
        private static string processName;

        public static GameEnum bringGameToForeground()
        {
            IntPtr currentForgroundWindow = GetForegroundWindow();
            return SetGameProcessAsForeground(currentForgroundWindow);
        }
        private static GameEnum SetGameProcessAsForeground(IntPtr currentForgroundWindow)
        {
            foreach (var gameProcess in matchingProcesses)
            {
                if (processNames.Contains(gameProcess.ProcessName) &&
                    gameProcess.MainWindowHandle != (IntPtr)0 &&
                    gameProcess.MainWindowHandle != currentForgroundWindow)
                {
                    if (SetForegroundWindow(gameProcess.MainWindowHandle))
                    {
                        return gameProcess.ProcessName == "rFactor2" ? GameEnum.RF2_64BIT : GameEnum.LMU;
                    }
                    else
                    {
                        Console.WriteLine($"Couldn't set {processName} to be the current window");
                        throw new System.InvalidOperationException($"Couldn't set {processName} to be the current window");
                    }
                }
            }
            //throw new System.InvalidOperationException($"Neither RF2 nor LMU is running");
            return GameEnum.NONE;
        }
    }

}
#endif
