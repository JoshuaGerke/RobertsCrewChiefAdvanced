using CrewChiefV4;
using NUnit.Framework;

namespace UnitTest.Utils
{
    static class Processes
    {
        internal static bool vlcCheck()
        {
            string processName = "vlc";
            var windows = (Win32Stuff.FindWindowsWithText(processName));
            if (windows.Count != 0)
            {
                if (Win32Stuff.GetWindowText(windows[0]) == processName)
                {
                    return true;
                }
                else
                {
                    Assert.Inconclusive("VLC MUST BE PLAYING A FILE");
                }
            }
            else
            {
                {
                    Assert.Inconclusive("VLC MUST BE RUNNING");
                }
            }
            return false;
        }

        internal static bool runningCheck(string processName)
        {
            var windows = (Win32Stuff.FindWindowsWithText(processName));
            if (windows.Count == 0)
            {
                Assert.Inconclusive($"{processName} MUST BE RUNNING");
            }
            return windows.Count != 0;
        }
    }
    static class QuitIf
    {
        internal static void rF2orLMUrunning()
        {
            if (UnitTest.Rf2Live)
            {
                Assert.Inconclusive("rFactor 2 is running, cannot run this test");
            }
            if (UnitTest.LmuLive)
            {
                Assert.Inconclusive("LMU is running, cannot run this test");
            }
        }
        internal static void rF2notRunning()
        {
            if (!UnitTest.Rf2Live)
            {
                Assert.Inconclusive("RF2 is NOT running, cannot run this test");
            }
        }
        internal static void LMUnotRunning()
        {
            if (!UnitTest.LmuLive)
            {
                Assert.Inconclusive("LMU is NOT running, cannot run this test");
            }
        }
    }
}
