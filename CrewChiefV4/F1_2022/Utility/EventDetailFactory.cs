using F12022UdpNet;
using System;
using System.Text;

namespace CrewChiefV4.F1_2022.Utility
{
    public static class EventDetailFactory
    {
        public static object Parse(byte[] code, IntPtr pointer)
        {
            var eventTypeString = Encoding.UTF8.GetString(code);
            return Parse(eventTypeString, pointer);
        }

        public static object Parse(string code, IntPtr pointer)
        {
            switch (code)
            {
                case EventStringCode.FastestLap:
                    return pointer.PointerCast<FastestLap>();
                case EventStringCode.DriverRetires:
                    return pointer.PointerCast<Retirement>();
                case EventStringCode.TeamMateInPits:
                    return pointer.PointerCast<TeamMateInPits>();
                case EventStringCode.RaceWinner:
                    return pointer.PointerCast<RaceWinner>();
                case EventStringCode.Penalty:
                    return pointer.PointerCast<Penalty>();
                case EventStringCode.SpeedTrap:
                    return pointer.PointerCast<SpeedTrap>();
                case EventStringCode.StartLights:
                    return pointer.PointerCast<StartLights>();
                case EventStringCode.DriveThroughServed:
                    return pointer.PointerCast<DriveThroughPenaltyServed>();
                case EventStringCode.StopAndGoServed:
                    return pointer.PointerCast<StopAndGoPenaltyServed>();
                case EventStringCode.Flashback:
                    return pointer.PointerCast<Flashback>();
                case EventStringCode.ButtonStatusChanged:
                    return pointer.PointerCast<Buttons>();
            }

            return null;
        }
    }
}
